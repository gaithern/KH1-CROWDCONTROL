---@diagnostic disable: undefined-global
--[[
    Bridges Crowd Control (Twitch channel-point redemptions) into KH1 via
    Crowd Control's SimpleTCP protocol: JSON messages, NUL-terminated, over a
    raw TCP connection this mod opens as a CLIENT to the Crowd Control app
    (which hosts the server side) -- see
    https://developer.crowdcontrol.live/sdk/simpletcp/index.html

    Requires KH1-LUA-LIBRARY to also be installed: kh1_lua_library.lua,
    json.lua, and kh1_native.dll are require()'d from wherever that sibling
    mod puts them -- OpenKH's Lua loader resolves require() across every
    installed mod's scripts/io_packages, not just this mod's own.

    This file must stay directly under scripts/ (NOT scripts/io_packages/) --
    confirmed against KH1-RANDOMIZER's real entry script
    (scripts/1fmRandoClient.lua): only .lua files placed directly under
    scripts/ are auto-loaded and get their global _OnFrame called every frame
    by the host. Files under scripts/io_packages/ are require()-only
    libraries that never run on their own (that's where kh1_lua_library.lua
    itself lives, and where this mod's own kh1_crowdcontrol_native.dll stays
    -- a require()'d native module doesn't need to be a top-level script).

    INCOMING MESSAGE PARSING (2026-07-20): this mod does NOT decode incoming
    JSON in Lua. Confirmed live: this modding environment's embedded Lua
    (LuaBackend) has a real bug where its own string library (string.byte,
    and anything built on it like string.find/gsub/json.decode) returns
    nil/garbage for strings longer than ~40 bytes that originated from a
    native lua_pushlstring call, while the native C API reads the exact same
    memory correctly. Real effect requests are 300+ bytes, so Lua-side
    parsing was silently swallowing every one of them -- Crowd Control was
    sending them correctly the entire time. ccnet.cc_poll_message() does all
    buffering/NUL-splitting/field-extraction natively in C++ instead, and
    only ever returns short already-extracted values (a numeric id/type/
    duration, a short code string), which stay safely under whatever length
    threshold triggers the bug. json.encode is still used for OUTGOING
    messages -- those are Lua-created strings, not native-pushed ones, so
    they aren't affected by this bug regardless of length.
]]

-- kh1_lua_library's functions (soraHP, zantHack, fnc_play_se2, etc.) read
-- version-specific globals that only exist after VersionCheck has required
-- the matching SteamGlobal_*/EGSGlobal_* file -- same require ORDER
-- KH1-RANDOMIZER's own entry script uses. Skipping this would leave every
-- kh1_lua_library call below silently operating on nil addresses.
require("VersionCheck")

local kh1 = require("kh1_lua_library")
local json = require("json")
local ccnet = require("kh1_crowdcontrol_native")

local CC_HOST = "127.0.0.1"
local CC_PORT = 43384 -- must match the port the Crowd Control app's SimpleTCP connector is configured to use for this pack
local RECONNECT_INTERVAL_SECONDS = 5

local socket_handle = nil
local connecting_handle = nil -- non-nil while a non-blocking connect is in flight (see try_connect)
local next_reconnect_attempt = 0

-- ConsolePrint output isn't visible anywhere by default in this modding
-- environment (confirmed live 2026-07-13) -- everything this mod logs goes
-- through cc_log instead, which appends to kh1_crowdcontrol_native.log
-- (the same file the native DLL's own connection/socket logging already
-- uses -- see README's Troubleshooting section).
local function log(msg)
    ccnet.cc_log("[Crowd Control] " .. msg)
end

-- ####################### --
-- # Timed-effect tracking # --
-- ####################### --

-- Effects that return a `revert` function from `apply` get auto-reverted
-- this many seconds later (Crowd Control sends `duration` in milliseconds on
-- the request when the pack marks an effect as timed; this is the fallback
-- for untimed/test triggers). Keyed by effect CODE, not request id: a second
-- redemption of the same still-active effect just extends the deadline
-- rather than re-applying (re-applying would re-capture "current" state as
-- the new baseline mid-effect and revert to the wrong value, or race a
-- second revert against the first). This does NOT protect against two
-- DIFFERENT effect codes touching the same underlying value -- whichever
-- reverts last wins. Acceptable jank for a chaos-mod feature; not worth a
-- full resource-lock system here.
local DEFAULT_DURATION_SECONDS = 30
local active_timed_effects = {} -- code -> { revert = fn, deadline = number }

local function effect_duration_seconds(duration_ms)
    if duration_ms and duration_ms > 0 then
        return duration_ms / 1000 -- Crowd Control sends duration in milliseconds
    end
    return DEFAULT_DURATION_SECONDS
end

local function update_timed_effects()
    local now = os.clock()
    for code, effect in pairs(active_timed_effects) do
        if now >= effect.deadline then
            pcall(effect.revert)
            active_timed_effects[code] = nil
        end
    end
end

-- ####################### --
-- # Effect dispatch table # --
-- ####################### --

-- Keyed by the effect "code" Crowd Control sends in each request -- these
-- must match the effect ids declared in the game pack (see
-- pack/KH1CrowdControlPack.cs). Each handler is `{ apply = function() ->
-- ok, revert_fn end }`; `revert_fn` is omitted for instant, one-shot effects
-- and required for anything that should auto-expire.
local effect_handlers = {
    -- ####################### --
    -- # Combo limit           # --
    -- ####################### --
    -- Captures the real current limits via the library's own getters and
    -- restores exactly those, rather than assuming a hardcoded vanilla value
    -- (vanilla itself varies with equipped abilities -- see
    -- calculate_ground_combo_limit/calculate_air_combo_limit).

    no_combos = {
        apply = function()
            local original_ground = kh1.get_ground_combo_length_limit()
            local original_air = kh1.get_air_combo_length_limit()
            kh1.set_ground_combo_length_limit(1)
            kh1.set_air_combo_length_limit(1)
            return true, function()
                kh1.set_ground_combo_length_limit(original_ground)
                kh1.set_air_combo_length_limit(original_air)
            end
        end,
    },
}

-- ####################### --
-- # Item grants           # --
-- ####################### --
-- One-shot: spawns a real, collectible item pickup near Sora via
-- kh1.spawn_prize(item_id) (see that function's own doc comment in
-- kh1_lua_library.lua). item_ids below are the "regular item" offsets from
-- KH1's own native item-id space -- cross-confirmed against
-- KH-1FM-AP-LUA/1fmAPConnector.lua's write_item() (writes directly into the
-- inventory byte array at this same offset) and
-- KH1-RANDOMIZER/mod/scripts/1fmRandoGiftTable.lua's gift-table encoding
-- (`{0xF0, item_id % 1000}`, where 0xF0 is the game's own "regular item"
-- type marker), both of which actually run against real KH1FM -- but NEITHER
-- confirms fnc_spawn_prize specifically accepts the same id space (a
-- different native function: spawns a physical pickup entity, not a chest/
-- gift grant or inventory write). Deliberately limited to simple,
-- non-progression consumables/stat-ups -- no keyblades, accessories, world
-- unlocks, or other story-relevant items -- to keep the blast radius small
-- until at least one of these is confirmed live.
local GIVE_ITEM_EFFECTS = {
    give_potion = 1,
    give_hi_potion = 2,
    give_ether = 3,
    give_elixir = 4,
    give_mega_potion = 6,
    give_mega_ether = 7,
    give_megalixir = 8,
    give_tent = 142,
    give_camping_set = 143,
    give_cottage = 144,
    give_power_up = 152,
    give_defense_up = 153,
    give_ap_up = 154,
}
for code, item_id in pairs(GIVE_ITEM_EFFECTS) do
    effect_handlers[code] = {
        apply = function()
            return kh1.spawn_prize(item_id)
        end,
    }
end

-- ####################### --
-- # Message (preset list) # --
-- ####################### --
-- Shows a preset message via the map-prize pickup popup
-- (kh1.show_custom_item_popup -- the small window that normally names the
-- item you just got, repurposed to show custom text). Originally meant to
-- be free-typed viewer text, but Crowd Control's team confirmed on Discord
-- (2026-07-13) the SimpleTCP C# pack SDK has NO free-text input at all --
-- only a numeric Quantity slider and Parameters (pick one option from a
-- list, or a hex color). Reworked as discrete per-message effects instead,
-- matching the give_* pattern -- keys here must match the codes declared in
-- pack/KH1CrowdControlPack.cs exactly.
local MESSAGE_PRESETS = {
    message_gg = "GG",
    message_nice = "Nice!",
    message_oops = "Oops!",
    message_uhoh = "Uh oh...",
    message_nooo = "Nooo!",
    message_yay = "Yay!",
    message_hello = "Hello!",
    message_whoops = "Whoops!",
    message_sotrue = "So true",
    message_skillissue = "Skill issue",
    message_chaos = "Chaos!",
    message_goodluck = "Good luck",
    message_badluck = "Bad luck",
    message_tryagain = "Try again",
    message_wtake = "W take",
    message_ltake = "L take",
}
for code, text in pairs(MESSAGE_PRESETS) do
    effect_handlers[code] = {
        apply = function()
            return kh1.show_custom_item_popup(text)
        end,
    }
end

-- ####################### --
-- # Deliberately NOT wired # --
-- ####################### --
-- grant_sora_ability / grant_shared_ability / enable_ability: take raw
--   numeric ability ids (or a curated name table, for enable_ability) but
--   there's no ability-granting effect category wired up.
-- set_stock_at_index / set_gummi_qty_at_index: index-based with no known
--   index->item mapping (same guessed-id risk as spawn_prize's item_id).
-- set_sora_walk_speed / set_sora_run_speed: no getter exists, so there's no
--   way to capture-and-restore the real original -- and no documented
--   vanilla baseline anywhere in this codebase to revert to instead.
-- set_spell_cost: no getter exists (unlike effectiveness), so cost changes
--   can't be safely reverted either.
-- set_attack_animation_data / set_command_data: raw array writes into
--   engine tables with no documented safe shape/range -- highest guessed-risk
--   functions in the library.
-- show_prompt: the MESSAGE_PRESETS effects above already cover "show custom
--   text" more simply; show_prompt's multi-box/color parameter shape isn't
--   worth the extra complexity for the same end result.
-- make_sora_actionable: an unstick/debug utility, not really a chaos effect
--   (no inverse action, nothing meaningful to revert).

-- ############### --
-- # Connection    # --
-- ############### --

-- Starts a NON-BLOCKING connect (see cc_connect's own comment in
-- dllmain.cpp -- a blocking connect here was observed causing multi-second
-- game freezes on every reconnect attempt). This only kicks off the
-- attempt; update_crowdcontrol polls cc_connect_status(connecting_handle)
-- every frame until it resolves to "connected" or "failed".
local function try_connect()
    local ok, handle_or_err = ccnet.cc_connect(CC_HOST, CC_PORT)
    if ok then
        connecting_handle = handle_or_err
    else
        connecting_handle = nil
        log(string.format("cc_connect failed: %s", tostring(handle_or_err)))
    end
end

local function disconnect()
    if socket_handle then
        ccnet.cc_close(socket_handle)
        socket_handle = nil
    end
    if connecting_handle then
        ccnet.cc_close(connecting_handle)
        connecting_handle = nil
    end
end

-- ############### --
-- # Effect replies # --
-- ############### --

local STATUS_SUCCESS = 0
local STATUS_FAILURE = 1

-- GameUpdate message (type 0xFD / 253): a REQUEST from Crowd Control for
-- updated game state, not a plain keepalive (corrected 2026-07-20 -- see
-- header comment). "ready" is the only game state where Crowd Control will
-- actually dispatch effects.
local GAME_UPDATE_TYPE = 253

local function send_game_state(state)
    if not socket_handle then return end
    local msg = json.encode({ type = GAME_UPDATE_TYPE, state = state })
    ccnet.cc_send(socket_handle, msg .. "\0")
    log(string.format("Sent game state: %s", state))
end

-- Reply to a SPECIFIC incoming GameUpdate request (echoes its `id`), as
-- opposed to send_game_state's unprompted announcement.
local function send_game_state_reply(request_id, state)
    if not socket_handle then return end
    local msg = json.encode({ id = request_id, type = GAME_UPDATE_TYPE, state = state })
    ccnet.cc_send(socket_handle, msg .. "\0")
end

local function send_response(request_id, ok)
    if not socket_handle then return end
    local response = json.encode({ id = request_id, type = 0, status = ok and STATUS_SUCCESS or STATUS_FAILURE })
    ccnet.cc_send(socket_handle, response .. "\0")
end

-- id/msg_type/code/duration come pre-extracted from ccnet.cc_poll_message --
-- see header comment for why this mod no longer decodes JSON in Lua at all.
local function handle_request(id, msg_type, code, duration)
    log(string.format("Received request: id=%s type=%s code=%s duration=%s",
        tostring(id), tostring(msg_type), tostring(code), tostring(duration)))

    if msg_type == GAME_UPDATE_TYPE then
        send_game_state_reply(id, "ready")
        return
    end

    -- Other non-effect protocol messages (no `code` field) -- e.g. type
    -- 0x02 EffectStop, which Crowd Control's reference documents but this
    -- mod doesn't yet implement handling for. Deliberately left unanswered
    -- rather than guessing at an unconfirmed response shape.
    if code == nil then
        return
    end

    local handler = effect_handlers[code]
    local ok = false

    if not handler or not handler.apply then
        log(string.format("No handler for code '%s'", tostring(code)))
    else
        local existing = active_timed_effects[code]
        if existing then
            -- Already active: extend the timer instead of re-applying (see
            -- active_timed_effects' comment for why re-applying would be
            -- wrong here).
            existing.deadline = os.clock() + effect_duration_seconds(duration)
            ok = true
        else
            local call_ok, apply_ok, revert = pcall(handler.apply)
            if not call_ok then
                -- apply_ok holds the error message here, not a boolean --
                -- pcall's second return on failure is the error, not
                -- apply_ok's normal meaning.
                log(string.format("Effect '%s' errored: %s", tostring(code), tostring(apply_ok)))
            elseif not apply_ok then
                log(string.format("Effect '%s' handler returned false (bad input?)", tostring(code)))
            else
                log(string.format("Effect '%s' handler returned true (apply call succeeded)", tostring(code)))
            end
            ok = call_ok and apply_ok and true or false
            if ok and revert then
                active_timed_effects[code] = {
                    revert = revert,
                    deadline = os.clock() + effect_duration_seconds(duration),
                }
            end
        end
    end

    send_response(id, ok)
end

-- ############### --
-- # Frame pump    # --
-- ############### --

function update_crowdcontrol()
    --[[Drives the whole connection: reconnects to the Crowd Control app on a
    timer while disconnected, reverts any timed effects whose deadline has
    passed, and otherwise drains any complete messages off the socket (via
    ccnet.cc_poll_message, which parses natively -- see header comment) and
    dispatches each to effect_handlers. Call this every frame from _OnFrame
    -- mirrors kh1_lua_library's update_text_boxes() pattern.]]
    update_timed_effects()

    if connecting_handle then
        local status = ccnet.cc_connect_status(connecting_handle)
        if status == "connected" then
            socket_handle = connecting_handle
            connecting_handle = nil
            log(string.format("Connected to %s:%d", CC_HOST, CC_PORT))
            send_game_state("ready")
        elseif status == "failed" then
            ccnet.cc_close(connecting_handle)
            connecting_handle = nil
            next_reconnect_attempt = os.clock() + RECONNECT_INTERVAL_SECONDS
        end
        -- status == "connecting": keep waiting, poll again next frame.
        return
    end

    if not socket_handle then
        local now = os.clock()
        if now >= next_reconnect_attempt then
            next_reconnect_attempt = now + RECONNECT_INTERVAL_SECONDS
            try_connect()
        end
        return
    end

    -- Drain every complete message buffered this frame -- one recv() can
    -- contain several back-to-back messages.
    while true do
        local status, a, b, c, d = ccnet.cc_poll_message(socket_handle)
        if status == "message" then
            local id, msg_type, code, duration = a, b, c, d
            handle_request(id, msg_type, code, duration)
        elseif status == "closed" then
            log(string.format("Connection lost (%s), will retry", tostring(a)))
            disconnect()
            break
        else -- "none"
            break
        end
    end
end

-- The host's Lua loader warns ("the event handler for initialization either
-- has errors or does not exist") if a top-level script never defines
-- _OnInit -- every real script in KH1-RANDOMIZER's scripts/ folder has one.
-- All of this mod's actual init work (VersionCheck's version detection,
-- setting canExecute) already ran synchronously above via require()
-- before this point, so there's nothing left to do here besides exist.
function _OnInit()
end

-- This is a top-level scripts/ entry file (see header comment), not a
-- required library, so it owns _OnFrame outright -- same plain global-function
-- style as KH1-RANDOMIZER's scripts/1fmRandoClient.lua. Each top-level script
-- gets its own globals, so this doesn't collide with other installed mods'
-- own _OnFrame.
function _OnFrame()
    if canExecute then
        update_crowdcontrol()
    end
end
