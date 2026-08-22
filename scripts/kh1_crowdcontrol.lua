---@diagnostic disable: undefined-global

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

local function log(msg)
    ccnet.cc_log("[Crowd Control] " .. msg)
end

local send_response

local PENDING_RESPONSE = {}

local STATUS_SUCCESS = 0
local STATUS_RETRY   = 3
local STATUS_FAILURE = 1
local STATUS_WAIT    = 9

-- ####################### --
-- # Effect dispatch table # --
-- ####################### --

local effect_handlers = {
    -- ####################### --
    -- # Combo limit         # --
    -- ####################### --


    -- The game itself restores the combo limits on the next menu or room
    -- transition, so this is a one-shot effect with no revert timer.
    no_combos = {
        apply = function()
            kh1.set_ground_combo_length_limit(1)
            kh1.set_air_combo_length_limit(1)
            return true
        end,
    },

    -- ####################### --
    -- # KO / danger         # --
    -- ####################### --

    ko_sora = {
        apply = function()
            kh1.ko_sora()
            return true
        end,
    },

    heartless_angel_sora = {
        apply = function()
            kh1.heartless_angel_sora()
            return true
        end,
    },
}

-- ####################### --
-- # Ability grants        # --
-- ####################### --
local ABILITY_EFFECTS = {
    ability_vortex          = "Vortex",
    ability_aerial_sweep    = "Aerial Sweep",
    ability_counterattack   = "Counterattack",
    ability_blitz           = "Blitz",
    ability_guard           = "Guard",
    ability_dodge_roll      = "Dodge Roll",
    ability_cheer           = "Cheer",
    ability_slapshot        = "Slapshot",
    ability_sliding_dash    = "Sliding Dash",
    ability_hurricane_blast = "Hurricane Blast",
    ability_ripple_drive    = "Ripple Drive",
    ability_stun_impact     = "Stun Impact",
    ability_gravity_break   = "Gravity Break",
    ability_zantetsuken     = "Zantetsuken",
    ability_sonic_blade     = "Sonic Blade",
    ability_ars_arcanum     = "Ars Arcanum",
    ability_strike_raid     = "Strike Raid",
    ability_ragnarok        = "Ragnarok",
    ability_trinity_limit   = "Trinity Limit",
    ability_mp_haste        = "MP Haste",
    ability_mp_rage         = "MP Rage",
    ability_second_chance   = "Second Chance",
    ability_berserk         = "Berserk",
    ability_leaf_bracer     = "Leaf Bracer",
}
for code, ability_name in pairs(ABILITY_EFFECTS) do
    effect_handlers[code] = {
        apply = function()
            -- enable_ability returns nothing; a silent no-op on an unrecognised
            -- name is the only failure mode, and the names are fixed above.
            kh1.enable_ability(ability_name)
            return true
        end,
    }
end

-- ####################### --
-- # Item grants           # --
-- ####################### --
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
-- # Enemy spawns          # --
-- ####################### --

-- kh1.spawn_enemy is now a polled primitive: (model, x, y, z) -> ok, result_or_reason. We own
-- the retry loop it used to run internally (the old kh1.update_spawn_enemy). One attempt per
-- frame preserves the crash-containment invariant (never trigger multiple loads in a frame).
-- update_enemy_spawns (the driver) is defined near the frame pump, where STATUS_* are in scope.
local pending_enemy_spawns = {}
local ENEMY_SPAWN_TIMEOUT_SECONDS = 5.0   -- how long we wait on ONE load before giving up
local CC_ANSWER_SECONDS = 13.0           -- give up on a queued request just under the Wait window (15s)
local MAX_ENEMY_SPAWN_QUEUE = 3           -- reject overflow at once instead of leaving it unanswered
local SPAWN_WAIT_MS = 15000               -- Wait window we ask CC to hold before our terminal follow-up

local function enqueue_enemy_spawn(request_id, model_path, x, y, z)
    if #pending_enemy_spawns >= MAX_ENEMY_SPAWN_QUEUE then return false end
    pending_enemy_spawns[#pending_enemy_spawns + 1] = {
        id = request_id, model = model_path, x = x, y = y, z = z,
        queued = os.clock(),
        deadline = os.clock() + ENEMY_SPAWN_TIMEOUT_SECONDS,
    }
    return true
end

local ENEMY_SPAWN_EFFECTS = {
    spawn_shadow = "xa_ex_2020.mdls",
    spawn_soldier = "xa_ex_2010.mdls",
    spawn_powerwild = "xa_ex_2030.mdls",
    spawn_sea_neon = "xa_ex_2070.mdls",
    spawn_red_nocturne = "xa_ex_2110.mdls",
    spawn_blue_rhapsody = "xa_ex_2120.mdls",
    spawn_yellow_opera = "xa_ex_2130.mdls",
    spawn_green_requiem = "xa_ex_2140.mdls",
    spawn_air_soldier = "xa_ex_2160.mdls",
    spawn_bouncywild = "xa_ex_2040.mdls",
    spawn_large_body = "xa_ex_2050.mdls",
    spawn_fat_bandit = "xa_ex_2060.mdls",
    spawn_sheltering_zone = "xa_ex_2080.mdls",
    spawn_bandit = "xa_ex_2090.mdls",
    spawn_pirate = "xa_ex_2100.mdls",
    spawn_wight_knight = "xa_ex_2200.mdls",
    spawn_air_pirate = "xa_ex_2210.mdls",
    spawn_gargoyle = "xa_ex_2220.mdls",
    spawn_search_ghost = "xa_ex_2230.mdls",
    spawn_aquatank = "xa_ex_2240.mdls",
    spawn_screwdiver = "xa_ex_2250.mdls",
    spawn_darkball = "xa_ex_2280.mdls",
    spawn_bitsniper = "xa_ew_2010.mdls",
    spawn_wizard = "xa_ex_2150.mdls",
    spawn_invisible = "xa_ex_2290.mdls",
    spawn_wyvern = "xa_ex_2320.mdls",
    spawn_angel_star = "xa_ex_2330.mdls",
    spawn_defender = "xa_ex_2340.mdls",
    spawn_jet_balloon = "xa_ex_2480.mdls",
    spawn_stealth_sneak = "xa_ex_2430.mdls",
    spawn_missile_diver = "xa_ex_2490.mdls",
    spawn_sniperwild = "xa_ex_2450.mdls",
    spawn_pink_agaricus = "xa_ex_2410.mdls",
    spawn_black_fungi = "xa_ex_2380.mdls",
}
for code, model_path in pairs(ENEMY_SPAWN_EFFECTS) do
    effect_handlers[code] = {

        apply = function(request_id)
            local pos = kh1.get_sora_pos()
            local angle = math.random() * 2 * math.pi
            local radius = 250 + math.random() * 150
            local x = pos["X"] + math.cos(angle) * radius
            local z = pos["Z"] + math.sin(angle) * radius
            if enqueue_enemy_spawn(request_id, model_path, x, pos["Y"], z) then
                -- Acknowledge with Wait so CC holds the effect (no re-fire); we answer terminally on construct.
                send_response(request_id, STATUS_WAIT, "Spawning " .. model_path, SPAWN_WAIT_MS)
            else
                send_response(request_id, STATUS_FAILURE, "Too many spawns queued right now -- give it a moment.")
            end
            return PENDING_RESPONSE
        end,
    }
end

-- ####################### --
-- # Effect announcement   # --
-- ####################### --

-- Friendly display name for the in-game "who triggered what" text box.
local EFFECT_NAME_OVERRIDES = {
    ko_sora = "KO Sora",
    heartless_angel_sora = "Heartless Angel",
    no_combos = "No Combos",
}
local function display_name(code)
    if EFFECT_NAME_OVERRIDES[code] then return EFFECT_NAME_OVERRIDES[code] end
    if ABILITY_EFFECTS[code] then return ABILITY_EFFECTS[code] end
    if MESSAGE_PRESETS[code] then return "Message: " .. MESSAGE_PRESETS[code] end
    local base = code:gsub("^spawn_", ""):gsub("^give_", "")
    base = base:gsub("_", " "):gsub("(%a)([%w]*)", function(a, b) return a:upper() .. b end)
    return base
end

local EFFECT_TEXT_SECONDS = 4
local ANNOUNCE_STYLE = 0        -- 0 = subtitle text, no box (Ghidra-confirmed)
-- Position offsets the box CENTER from screen center (254,180): x=0 stays horizontally centered,
-- negative y moves it up. -130 puts the box near the top. Tweak y to taste (more negative = higher).
local ANNOUNCE_X = 0
local ANNOUNCE_Y = -130
local function announce_effect(code, viewer)
    if not kh1.open_text_box then return end
    local who = (viewer and viewer ~= "") and viewer or "the crowd"
    pcall(kh1.open_text_box, display_name(code) .. " by " .. who, 1,
        EFFECT_TEXT_SECONDS, ANNOUNCE_STYLE, ANNOUNCE_X, ANNOUNCE_Y)
end

-- ############### --
-- # Connection    # --
-- ############### --

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

local GAME_UPDATE_TYPE = 253

local function send_game_state(state)
    if not socket_handle then return end
    local msg = json.encode({ type = GAME_UPDATE_TYPE, state = state })
    ccnet.cc_send(socket_handle, msg .. "\0")
    log(string.format("Sent game state: %s", state))
end

local last_reported_state = nil

-- Is it safe to fire an effect right now? Returns a Crowd Control GameState + a reason.
-- Not loaded into a world: 0xFF is the "no world" sentinel, live-confirmed on the title screen.
local function get_effect_readiness()
    if kh1.get_world() == 0xFF then
        return "menu", "the game isn't in a world (title screen or loading)"
    end
    if ReadInt(inCutscene) ~= 0 then
        return "cutscene", "a cutscene is playing"
    end
    if ReadByte(menu) ~= 0 then
        return "menu", "a menu is open"
    end
    if kh1.is_in_gummi_garage() then
        return "wrongMode", "the player is in the Gummi ship"
    end
    if kh1.sora_koed() then
        return "badPlayerState", "Sora is KO'd"
    end
    return "ready", nil
end

-- All effects are always available now -- the queue/Wait system handles timing -- so we always
-- report "ready" to Crowd Control and it never greys anything out. (get_effect_readiness is still
-- used on the fire path to defer firing into a cutscene/menu.)
local function current_game_state()
    return "ready"
end

local function send_game_state_reply(request_id, state)
    if not socket_handle then return end
    local msg = json.encode({ id = request_id, type = GAME_UPDATE_TYPE, state = state })
    ccnet.cc_send(socket_handle, msg .. "\0")
end

-- The state flaps across transitions (ready<->cutscene several times a second) and every change
-- re-marks 90 effects, so only report once it has held still.
local STATE_DEBOUNCE_SECONDS = 0.5
local candidate_state, candidate_state_since = nil, 0

local function settled_game_state()
    local state = current_game_state()
    if state ~= candidate_state then
        candidate_state, candidate_state_since = state, os.clock()
        return nil
    end
    if os.clock() - candidate_state_since < STATE_DEBOUNCE_SECONDS then return nil end
    return state
end

local EFFECT_READY_SETTLE_SECONDS = 2.0

local function effects_ready_now()
    return candidate_state == "ready"
        and (os.clock() - candidate_state_since) >= EFFECT_READY_SETTLE_SECONDS
end

local spawn_dispatched_this_frame = false

function send_response(request_id, status, message, time_remaining)
    if not socket_handle then return end
    local payload = { id = request_id, type = 0, status = status }
    if message then
        payload.message = message
    end
    if time_remaining then
        payload.timeRemaining = time_remaining
    end
    ccnet.cc_send(socket_handle, json.encode(payload) .. "\0")
end

local function handle_request(id, msg_type, code, duration, viewer)
    log(string.format("Received request: id=%s type=%s code=%s duration=%s viewer=%s",
        tostring(id), tostring(msg_type), tostring(code), tostring(duration), tostring(viewer)))

    if msg_type == GAME_UPDATE_TYPE then
        send_game_state_reply(id, current_game_state())
        return
    end

    if code == nil then
        return
    end

    local state, reason = get_effect_readiness()
    if state ~= "ready" then
        local message = string.format("Not right now -- %s. Queued; it'll fire shortly.",
                                      reason or state)
        log(string.format("Deferred '%s': %s", tostring(code), message))
        -- Retry, not Failure: the viewer keeps their coins and Crowd Control re-sends.
        send_response(id, STATUS_RETRY, message)
        return
    end

    if not effects_ready_now() then
        local message = "Not right now -- the game is mid-transition. Queued; it'll fire shortly."
        log(string.format("Deferred '%s': ready but not settled", tostring(code)))
        send_response(id, STATUS_RETRY, message)
        return
    end

    if ENEMY_SPAWN_EFFECTS[code] then
        if spawn_dispatched_this_frame then
            local message = "Another spawn is in flight. Queued; it'll fire shortly."
            log(string.format("Deferred '%s': spawn already dispatched this frame", tostring(code)))
            send_response(id, STATUS_RETRY, message)
            return
        end
        spawn_dispatched_this_frame = true
    end

    local handler = effect_handlers[code]
    local ok = false
    local message = nil

    if not handler or not handler.apply then
        message = string.format("No handler for code '%s'", tostring(code))
        log(message)
    else
        announce_effect(code, viewer)
        local call_ok, apply_ok = pcall(handler.apply, id)
        if not call_ok then
            message = string.format("Effect '%s' errored: %s", tostring(code), tostring(apply_ok))
            log(message)
        elseif apply_ok == PENDING_RESPONSE then
            log(string.format("Effect '%s' deferred its response (resolves asynchronously)", tostring(code)))
            return
        elseif not apply_ok then
            message = string.format("Effect '%s' handler returned false (bad input?)", tostring(code))
            log(message)
        else
            log(string.format("Effect '%s' handler returned true (apply call succeeded)", tostring(code)))
        end
        ok = call_ok and apply_ok and true or false
    end

    send_response(id, ok and STATUS_SUCCESS or STATUS_FAILURE, message)
end

-- ############### --
-- # Frame pump    # --
-- ############### --
local MIN_SPAWN_INTERVAL_SECONDS = 1.0
local last_spawn_finish = 0

-- Human-readable reasons surfaced to Crowd Control, so a failed spawn shows WHY instead of a
-- generic retry / TCP timeout. Keyed by the code kh1.spawn_enemy returns.
local SPAWN_FAIL_MESSAGES = {
    ctor_failed      = "Too many enemies on screen -- the game refused to add another.",
    handles_full     = "Resource handles exhausted -- the game needs a restart.",
    no_data          = "No spawn data for this creature.",
    cutscene         = "Can't spawn during a cutscene.",
    boss_slowdown    = "Can't spawn during the boss-defeat slow-mo.",
    load_failed      = "Failed to start the creature load.",
    splice_failed    = "Internal spawn error (placement splice).",
    install_failed   = "Spawn system failed to install.",
    registry_full    = "Spawn registry is full.",
    buf_alloc_failed = "Out of memory for the spawn buffer.",
    mint_failed      = "Internal spawn error (resource handle).",
    bad_args         = "Bad spawn request.",
}

local RETRY_REASONS = { cutscene = true, boss_slowdown = true }

local function update_enemy_spawns()
    local now = os.clock()
    for i = #pending_enemy_spawns, 2, -1 do
        local r = pending_enemy_spawns[i]
        if now - r.queued >= CC_ANSWER_SECONDS then
            table.remove(pending_enemy_spawns, i)
            send_response(r.id, STATUS_FAILURE, "The game was too busy spawning to reach this one.")
        end
    end

    local req = pending_enemy_spawns[1]
    if not req then return end
    if not req.started then
        if now - last_spawn_finish < MIN_SPAWN_INTERVAL_SECONDS then return end
        req.started = true
    end
    local ok, result = kh1.spawn_enemy(req.model, req.x, req.y, req.z)
    if ok then
        table.remove(pending_enemy_spawns, 1)
        last_spawn_finish = os.clock()
        log(string.format("spawn_enemy(\"%s\") entity=0x%X", req.model, math.floor(result)))
        send_response(req.id, STATUS_SUCCESS, "Spawned " .. req.model)
    elseif result == "loading" then
        -- Already acknowledged with STATUS_WAIT when queued, so CC is holding it -- just keep driving.
        if os.clock() >= req.deadline then
            table.remove(pending_enemy_spawns, 1)
            last_spawn_finish = os.clock()
            log(string.format("spawn_enemy(\"%s\") load timed out", req.model))
            send_response(req.id, STATUS_FAILURE, "The creature took too long to load.")
        end
    else

        table.remove(pending_enemy_spawns, 1)
        local reason = SPAWN_FAIL_MESSAGES[result] or ("Spawn failed: " .. tostring(result))
        local status = RETRY_REASONS[result] and STATUS_RETRY or STATUS_FAILURE
        log(string.format("spawn_enemy(\"%s\") failed: %s -- %s", req.model, tostring(result), reason))
        send_response(req.id, status, reason)
    end
end

function update_crowdcontrol()
    update_enemy_spawns()
    if kh1.update_text_boxes then kh1.update_text_boxes() end
    spawn_dispatched_this_frame = false

    if connecting_handle then
        local status = ccnet.cc_connect_status(connecting_handle)
        if status == "connected" then
            socket_handle = connecting_handle
            connecting_handle = nil
            log(string.format("Connected to %s:%d", CC_HOST, CC_PORT))
            last_reported_state = current_game_state()
            send_game_state(last_reported_state)
        elseif status == "failed" then
            ccnet.cc_close(connecting_handle)
            connecting_handle = nil
            next_reconnect_attempt = os.clock() + RECONNECT_INTERVAL_SECONDS
        end
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

    local state = settled_game_state()
    if state and state ~= last_reported_state then
        last_reported_state = state
        send_game_state(state)
    end

    while true do
        local status, a, b, c, d, e = ccnet.cc_poll_message(socket_handle)
        if status == "message" then
            handle_request(a, b, c, d, e)
        elseif status == "closed" then
            log(string.format("Connection lost (%s), will retry", tostring(a)))
            disconnect()
            break
        else
            break
        end
    end
end

function _OnInit()
end

function _OnFrame()
    if canExecute then
        update_crowdcontrol()
    end
end
