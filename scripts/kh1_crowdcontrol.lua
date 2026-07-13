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
local recv_buffer = ""
local next_reconnect_attempt = 0

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
-- DIFFERENT effect codes touching the same underlying value (e.g. mega_combos
-- and no_combos both writing the combo limit) -- whichever reverts last wins.
-- Acceptable jank for a chaos-mod feature; not worth a full resource-lock
-- system here.
local DEFAULT_DURATION_SECONDS = 30
local active_timed_effects = {} -- code -> { revert = fn, deadline = number }

local function effect_duration_seconds(request)
    if request.duration and request.duration > 0 then
        return request.duration / 1000 -- Crowd Control sends duration in milliseconds
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
-- # Effect helper factories # --
-- ####################### --

-- A well-defined on/off pair (force_scan, allow_air_items, etc. all already
-- define both directions themselves in kh1_lua_library) -- no state capture
-- needed, `off` IS the vanilla state.
local function toggle_effect(on_fn, off_fn)
    return function(request)
        on_fn()
        return true, off_fn
    end
end

-- ####################### --
-- # Effect dispatch table # --
-- ####################### --

-- Keyed by the effect "code" Crowd Control sends in each request -- these
-- must match the effect ids declared in the game pack (see
-- pack/kh1-crowdcontrol-pack.json). Each handler is `{ apply = function(request)
-- -> ok, revert_fn end }`; `revert_fn` is omitted for instant, one-shot
-- effects and required for anything that should auto-expire.
local effect_handlers = {
    -- The only play_se2 id confirmed live/audible so far (see
    -- kh1_lua_library.lua's play_se2 comment) -- expand this list only with
    -- ids you've verified live, per that function's crash-risk warning.
    -- Kept as its own entry (rather than folded into the SOUND_EFFECTS loop
    -- below) so this specific "confirmed good" status stays visible.
    sound_31 = {
        apply = function(request)
            return kh1.play_se2(31, 0)
        end,
    },

    -- Free-text effect: the redeemer's typed text is shown via the map-prize
    -- pickup popup (kh1.show_custom_item_popup -- the small window that
    -- normally names the item you just got, repurposed to show arbitrary
    -- text). Switched from open_text_box, which wasn't working. Truncated to
    -- 15 characters -- the popup's text field is a fixed-size display slot,
    -- not a free-scrolling box, so longer input would either overflow or
    -- get cut off unpredictably rather than cleanly.
    message = {
        apply = function(request)
            local text = request.parameters and request.parameters.text
            if not text or text == "" then
                return false
            end
            return kh1.show_custom_item_popup(text:sub(1, 15))
        end,
    },

    -- ############### --
    -- # Toggles      # --
    -- ############### --
    -- Each of these already has a real, library-defined "off" state, so
    -- reverting is exact -- no guessed baseline involved.

    force_scan = {
        apply = toggle_effect(
            function() kh1.force_scan(true) end,
            function() kh1.force_scan(false) end
        ),
    },
    force_combo_master = {
        apply = toggle_effect(
            function() kh1.force_combo_master(true) end,
            function() kh1.force_combo_master(false) end
        ),
    },
    summon_anywhere = {
        apply = toggle_effect(
            function() kh1.allow_summon_anywhere(true) end,
            function() kh1.allow_summon_anywhere(false) end
        ),
    },
    midair_dodge_guard = {
        apply = toggle_effect(
            function() kh1.allow_midair_dodge_roll_guard(true) end,
            function() kh1.allow_midair_dodge_roll_guard(false) end
        ),
    },
    air_items = {
        apply = toggle_effect(
            function() kh1.allow_air_items(true) end,
            function() kh1.allow_air_items(false) end
        ),
    },

    -- ####################### --
    -- # Combo limit           # --
    -- ####################### --
    -- Captures the real current limits via the library's own getters and
    -- restores exactly those, rather than assuming a hardcoded vanilla value
    -- (vanilla itself varies with equipped abilities -- see
    -- calculate_ground_combo_limit/calculate_air_combo_limit).

    mega_combos = {
        apply = function(request)
            local original_ground = kh1.get_ground_combo_length_limit()
            local original_air = kh1.get_air_combo_length_limit()
            kh1.set_ground_combo_length_limit(10)
            kh1.set_air_combo_length_limit(10)
            return true, function()
                kh1.set_ground_combo_length_limit(original_ground)
                kh1.set_air_combo_length_limit(original_air)
            end
        end,
    },
    no_combos = {
        apply = function(request)
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

    -- ####################### --
    -- # Animation speed       # --
    -- ####################### --
    -- Multiplies whatever the CURRENT speed reads as (rather than writing an
    -- absolute constant), so revert is always exact and this composes
    -- correctly even if something else already changed animation speed.

    slow_motion = {
        apply = function(request)
            local original = kh1.get_animation_speed()
            kh1.set_animation_speed(original * 0.5)
            return true, function() kh1.set_animation_speed(original) end
        end,
    },
    hyper_speed = {
        apply = function(request)
            local original = kh1.get_animation_speed()
            kh1.set_animation_speed(original * 2.0)
            return true, function() kh1.set_animation_speed(original) end
        end,
    },

    -- ####################### --
    -- # Summon time           # --
    -- ####################### --
    -- multiply_summon_time always computes from its own baked-in vanilla
    -- constant (3000), so reverting to multiplier 1.0 is always exact
    -- regardless of what the current value is.

    summon_time_half = {
        apply = function(request)
            kh1.multiply_summon_time(0.5)
            return true, function() kh1.multiply_summon_time(1.0) end
        end,
    },
    summon_time_double = {
        apply = function(request)
            kh1.multiply_summon_time(2.0)
            return true, function() kh1.multiply_summon_time(1.0) end
        end,
    },
}

-- ####################### --
-- # Ability grants        # --
-- ####################### --
-- One-shot, permanent (kh1_lua_library only exposes enable_ability, not a
-- matching disable -- there is nothing to revert to). Uses enable_ability's
-- own curated name->bit table rather than grant_sora_ability/
-- grant_shared_ability, which take raw numeric ability ids this repo has no
-- verified id table for -- same guessed-id risk already flagged for
-- spawn_prize's item_id.
local ABILITY_EFFECTS = {
    ability_vortex = "Vortex",
    ability_aerial_sweep = "Aerial Sweep",
    ability_counterattack = "Counterattack",
    ability_blitz = "Blitz",
    ability_guard = "Guard",
    ability_dodge_roll = "Dodge Roll",
    ability_cheer = "Cheer",
    ability_slapshot = "Slapshot",
    ability_sliding_dash = "Sliding Dash",
    ability_hurricane_blast = "Hurricane Blast",
    ability_ripple_drive = "Ripple Drive",
    ability_stun_impact = "Stun Impact",
    ability_gravity_break = "Gravity Break",
    ability_zantetsuken = "Zantetsuken",
    ability_sonic_blade = "Sonic Blade",
    ability_ars_arcanum = "Ars Arcanum",
    ability_strike_raid = "Strike Raid",
    ability_ragnarok = "Ragnarok",
    ability_trinity_limit = "Trinity Limit",
    ability_mp_haste = "MP Haste",
    ability_mp_rage = "MP Rage",
    ability_second_chance = "Second Chance",
    ability_berserk = "Berserk",
    ability_leaf_bracer = "Leaf Bracer",
}
for code, ability_name in pairs(ABILITY_EFFECTS) do
    effect_handlers[code] = {
        apply = function(request)
            kh1.enable_ability(ability_name)
            return true
        end,
    }
end

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
        apply = function(request)
            return kh1.spawn_prize(item_id)
        end,
    }
end

-- ####################### --
-- # Sound effects (range)  # --
-- ####################### --
-- One discrete effect per se_id (sound_2..sound_76, excluding sound_31
-- which already has its own entry above) rather than a single parameterized
-- effect -- every real, currently-loadable Crowd Control C# pack checked
-- while building this (WarpWorld/CCPack-PC-Balatro, TheUnknownCod3r/
-- CCPack-PC-TCGCardShopSimulator) uses only static, discrete Effect
-- declarations; no confirmed example of a numeric/free-input parameter
-- exists anywhere in this SDK ecosystem, so this matches the established
-- convention instead of guessing at an unconfirmed API (same reasoning
-- already applied to ABILITY_EFFECTS/GIVE_ITEM_EFFECTS above).
--
-- Range 1-76 is carried over from a DIFFERENT, older calling mechanism
-- (KH1-LUA-LIBRARY-DEV's code-cave injection driver) -- se_id=1 itself
-- already crashed the game through THIS exact call path
-- (kh1_native.call_function via play_se2), confirmed live 2026-07-12, so
-- it's excluded below. Every OTHER id 2-76 (aside from 31) is UNTESTED
-- through this function and may also crash -- a deliberate accepted
-- tradeoff for redemption variety, not an oversight (see README).
local SOUND_RANGE_MIN = 2
local SOUND_RANGE_MAX = 76
for se_id = SOUND_RANGE_MIN, SOUND_RANGE_MAX do
    if se_id ~= 31 then
        effect_handlers["sound_" .. se_id] = {
            apply = function(request)
                return kh1.play_se2(se_id, 0)
            end,
        }
    end
end

-- ####################### --
-- # Magic effectiveness   # --
-- ####################### --
-- EXPERIMENTAL / not live-verified: set_spell_effectiveness has no
-- documented safe value range anywhere in kh1_lua_library (unlike play_se2,
-- which does document a crash from a bad id). Scaling whatever the CURRENT
-- byte value already is (rather than writing a guessed absolute constant)
-- keeps the write inside the same valid byte range the game itself is
-- already using, which is a plain data multiplier (not an id/index lookup
-- like play_se2's se_id), so the crash risk profile should be much lower --
-- but this has not been confirmed live. Test before relying on it.
local SPELL_NAMES = {
    "Fire", "Fira", "Firaga", "Blizzard", "Blizzara", "Blizzaga",
    "Thunder", "Thundara", "Thundaga", "Cure", "Cura", "Curaga",
    "Gravity", "Gravira", "Graviga", "Stop", "Stopra", "Stopga",
    "Aero", "Aerora", "Aeroga",
}

local function spell_effectiveness_effect(multiplier)
    return function(request)
        local originals = {}
        for _, spell in ipairs(SPELL_NAMES) do
            originals[spell] = kh1.get_spell_effectiveness(spell)
        end
        for _, spell in ipairs(SPELL_NAMES) do
            local new_value = math.floor(originals[spell] * multiplier)
            if new_value > 255 then new_value = 255 end
            if new_value < 0 then new_value = 0 end
            kh1.set_spell_effectiveness(spell, new_value)
        end
        return true, function()
            for _, spell in ipairs(SPELL_NAMES) do
                kh1.set_spell_effectiveness(spell, originals[spell])
            end
        end
    end
end

effect_handlers.magic_boost = { apply = spell_effectiveness_effect(1.5) }
effect_handlers.magic_nerf = { apply = spell_effectiveness_effect(0.5) }

-- ####################### --
-- # Deliberately NOT wired # --
-- ####################### --
-- grant_sora_ability / grant_shared_ability: take raw numeric ability ids;
--   no verified id table exists in this repo (see ABILITY_EFFECTS above for
--   the name-based alternative that IS wired).
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
-- show_prompt: the `message` effect above already covers "show custom text"
--   more simply; show_prompt's multi-box/color parameter shape isn't worth
--   the extra complexity for the same end result.
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
        ConsolePrint(string.format("[Crowd Control] cc_connect failed: %s", tostring(handle_or_err)))
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
    recv_buffer = ""
end

-- ############### --
-- # Effect replies # --
-- ############### --

local STATUS_SUCCESS = 0
local STATUS_FAILURE = 1

local function send_response(request_id, ok)
    if not socket_handle then return end
    local response = json.encode({ id = request_id, type = 0, status = ok and STATUS_SUCCESS or STATUS_FAILURE })
    ccnet.cc_send(socket_handle, response .. "\0")
end

local function handle_request(request)
    local handler = effect_handlers[request.code]
    local ok = false

    if handler and handler.apply then
        local existing = active_timed_effects[request.code]
        if existing then
            -- Already active: extend the timer instead of re-applying (see
            -- active_timed_effects' comment for why re-applying would be
            -- wrong here).
            existing.deadline = os.clock() + effect_duration_seconds(request)
            ok = true
        else
            local call_ok, apply_ok, revert = pcall(handler.apply, request)
            ok = call_ok and apply_ok and true or false
            if ok and revert then
                active_timed_effects[request.code] = {
                    revert = revert,
                    deadline = os.clock() + effect_duration_seconds(request),
                }
            end
        end
    end

    send_response(request.id, ok)
end

-- ############### --
-- # Frame pump    # --
-- ############### --

function update_crowdcontrol()
    --[[Drives the whole connection: reconnects to the Crowd Control app on a
    timer while disconnected, reverts any timed effects whose deadline has
    passed, and otherwise drains any complete NUL-terminated JSON messages
    off the socket and dispatches each to effect_handlers. Call this every
    frame from _OnFrame -- mirrors kh1_lua_library's update_text_boxes()
    pattern.]]
    update_timed_effects()

    if connecting_handle then
        local status = ccnet.cc_connect_status(connecting_handle)
        if status == "connected" then
            socket_handle = connecting_handle
            connecting_handle = nil
            recv_buffer = ""
            ConsolePrint(string.format("[Crowd Control] Connected to %s:%d", CC_HOST, CC_PORT))
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

    local data, err = ccnet.cc_recv(socket_handle)
    if data == nil then
        ConsolePrint(string.format("[Crowd Control] Connection lost (%s), will retry", tostring(err)))
        disconnect()
        return
    end

    if data ~= "" then
        recv_buffer = recv_buffer .. data
    end

    while true do
        local nul_pos = string.find(recv_buffer, "\0", 1, true)
        if not nul_pos then break end
        local message = string.sub(recv_buffer, 1, nul_pos - 1)
        recv_buffer = string.sub(recv_buffer, nul_pos + 1)
        if message ~= "" then
            local decode_ok, request = pcall(json.decode, message)
            if decode_ok and type(request) == "table" then
                handle_request(request)
            end
        end
    end
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
