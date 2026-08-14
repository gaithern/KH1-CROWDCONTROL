---@diagnostic disable: undefined-global

require("VersionCheck")

local kh1 = require("kh1_lua_library")
local kh1_creature_data = require("kh1_lua_library.creature_data")
local json = require("json")
local ccnet = require("kh1_crowdcontrol_native")

local CC_HOST = "127.0.0.1"
local CC_PORT = 43384 -- must match the port the Crowd Control app's SimpleTCP connector is configured to use for this pack
local RECONNECT_INTERVAL_SECONDS = 5

local socket_handle = nil
local connecting_handle = nil -- non-nil while a non-blocking connect is in flight (see try_connect)
local next_reconnect_attempt = 0

-- Guarded: ConsolePrint isn't defined in every LuaBackend build, and an unguarded call would
-- abort the script at load.
local function log(msg)
    local line = "[Crowd Control] " .. msg
    ccnet.cc_log(line)
    --if ConsolePrint then ConsolePrint(line) end
end

local send_response

local PENDING_RESPONSE = {}

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
    local known = kh1_creature_data[model_path]
    local motion_path = known and known.motionPath
    effect_handlers[code] = {

        apply = function(request_id)
            local pos = kh1.get_sora_pos()
            local angle = math.random() * 2 * math.pi
            local radius = 250 + math.random() * 150
            local x = pos["X"] + math.cos(angle) * radius
            local z = pos["Z"] + math.sin(angle) * radius
            kh1.spawn_enemy(model_path, motion_path, x, pos["Y"], z, function(ok, result)
                local detail
                if ok and type(result) == "number" then
                    detail = string.format("spawn_enemy(\"%s\") = %s / entity=0x%X",
                        model_path, tostring(ok), math.floor(result))
                else
                    detail = string.format("spawn_enemy(\"%s\") = %s / %s",
                        model_path, tostring(ok), tostring(result))
                end
                log(detail)
                -- Spawn refusals are transient (slots, cutscene, entity budget) -- keep it queued.
                send_response(request_id, ok and STATUS_SUCCESS or STATUS_RETRY, detail)
            end)
            return PENDING_RESPONSE
        end,
    }
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

local STATUS_SUCCESS = 0
-- Retry is what the docs call "the normal failure response"; Failure and Unavailable both say
-- "you probably don't want this" (Failure refunds and ends it, Unavailable kills the effect).
local STATUS_RETRY   = 3
local STATUS_FAILURE = 1

local GAME_UPDATE_TYPE = 253

local function send_game_state(state)
    if not socket_handle then return end
    local msg = json.encode({ type = GAME_UPDATE_TYPE, state = state })
    log("TX state -> " .. msg)
    ccnet.cc_send(socket_handle, msg .. "\0")
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

-- This used to send "notready", which isn't a state Crowd Control recognises and was ignored.
local function current_game_state()
    -- Drops the second return value; callers pass this straight into message builders.
    local state = get_effect_readiness()
    return state
end

local function send_game_state_reply(request_id, state)
    if not socket_handle then return end
    local msg = json.encode({ id = request_id, type = GAME_UPDATE_TYPE, state = state })
    log("TX state-reply -> " .. msg)
    ccnet.cc_send(socket_handle, msg .. "\0")
end

local STATUS_SELECTABLE     = "selectable"
local STATUS_NOT_SELECTABLE = "notSelectable"

-- ResponseType.EffectStatus. NOT 0, which is EffectRequest -- with id=0 there's no pending
-- request to match on, so the type is the only thing marking this as a status message.
local RESPONSE_TYPE_EFFECT_STATUS = 0x01

-- EffectUpdate takes an `ids` ARRAY (its `code` field is deprecated), so one message covers
-- every effect sharing a status.
local function send_effect_class(ids, status, message)
    if not socket_handle or #ids == 0 then return end
    local payload = json.encode({
        id = 0, -- class messages aren't answering a request
        type = RESPONSE_TYPE_EFFECT_STATUS,
        ids = ids,
        status = status,
        message = message,
    })
    log("TX class -> " .. payload)
    ccnet.cc_send(socket_handle, payload .. "\0")
end

-- spawn_has_room persists across state flips; sent_selectable is what Crowd Control was last
-- told. Keeping them apart is what stops a cutscene wiping every per-creature verdict.
local spawn_has_room = {}
local sent_selectable = {}

local function desired_selectable(effect_code, state)
    if state ~= "ready" then return false end
    if ENEMY_SPAWN_EFFECTS[effect_code] then return spawn_has_room[effect_code] == true end
    return true
end

-- Sends only what actually changed, so a ready<->cutscene flip costs nothing for effects
-- already in the right state.
local function push_selectability(state)
    local on, off = {}, {}
    for effect_code in pairs(effect_handlers) do
        local want = desired_selectable(effect_code, state)
        if sent_selectable[effect_code] ~= want then
            sent_selectable[effect_code] = want
            local bucket = want and on or off
            bucket[#bucket + 1] = effect_code
        end
    end
    if #on == 0 and #off == 0 then return end
    -- One update per state, matching WarpWorld's Enable/DisableEffects. No visible assert:
    -- we never hide anything, and the reference doesn't pair the two.
    send_effect_class(on, STATUS_SELECTABLE)
    send_effect_class(off, STATUS_NOT_SELECTABLE, state ~= "ready"
        and "Not possible right now -- check back in a moment."
        or "Not enough free creature slots right now.")
    log(string.format("Selectability for '%s': %d available, %d unavailable", state, #on, #off))
end

-- A push sent before Crowd Control finished registering the pack is dropped, and diffs alone
-- would never re-send it. Re-assert the whole set periodically so it self-heals.
local FULL_REFRESH_INTERVAL_SECONDS = 10
local next_full_refresh = 0

local function refresh_selectability(state)
    local now = os.clock()
    if now < next_full_refresh then return end
    next_full_refresh = now + FULL_REFRESH_INTERVAL_SECONDS
    sent_selectable = {}
    push_selectability(state)
end

-- Slot needs differ per creature (Invisible 4, Shadow 1). One probe at a time: can_spawn_enemy
-- walks live entities per call, so all 34 at once would be costly.
local SPAWN_PROBE_INTERVAL_SECONDS = 0.1
local next_spawn_probe = 0
local spawn_probe_list = {}
local spawn_probe_index = 0
for effect_code, model_path in pairs(ENEMY_SPAWN_EFFECTS) do
    spawn_probe_list[#spawn_probe_list + 1] = { code = effect_code, model = model_path }
end

local function update_one_spawn_effect_availability(state)
    -- Only probe while ready: can_spawn_enemy's own cutscene/Gummi gates would otherwise
    -- report every creature as roomless and wipe the verdicts.
    if state ~= "ready" or #spawn_probe_list == 0 then return end
    local now = os.clock()
    if now < next_spawn_probe then return end
    next_spawn_probe = now + SPAWN_PROBE_INTERVAL_SECONDS

    spawn_probe_index = (spawn_probe_index % #spawn_probe_list) + 1
    local entry = spawn_probe_list[spawn_probe_index]
    local verdict, code = kh1.can_spawn_enemy(entry.model)
    local has_room = (verdict == "ready")
    if spawn_has_room[entry.code] == has_room then return end
    spawn_has_room[entry.code] = has_room
    log(string.format("%s has room: %s (%s)", entry.code, tostring(has_room), tostring(code)))
    push_selectability(state)
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

function send_response(request_id, status, message)
    if not socket_handle then return end
    local payload = { id = request_id, type = 0, status = status }
    if message then
        payload.message = message
    end
    local encoded = json.encode(payload)
    log("TX response -> " .. encoded)
    ccnet.cc_send(socket_handle, encoded .. "\0")
end

local function handle_request(id, msg_type, code, duration)
    log(string.format("Received request: id=%s type=%s code=%s duration=%s",
        tostring(id), tostring(msg_type), tostring(code), tostring(duration)))

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

    local handler = effect_handlers[code]
    local ok = false
    local message = nil

    if not handler or not handler.apply then
        message = string.format("No handler for code '%s'", tostring(code))
        log(message)
    else
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

function update_crowdcontrol()
    kh1.update_spawn_enemy()

    if connecting_handle then
        local status = ccnet.cc_connect_status(connecting_handle)
        if status == "connected" then
            socket_handle = connecting_handle
            connecting_handle = nil
            log(string.format("Connected to %s:%d", CC_HOST, CC_PORT))
            last_reported_state = current_game_state()
            send_game_state(last_reported_state)
            -- Crowd Control doesn't remember what a previous session reported. The re-assert is
            -- delayed a few seconds because this first push lands before the pack is registered.
            sent_selectable = {}
            next_full_refresh = os.clock() + 3
            push_selectability(last_reported_state)
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
    if state then
        if state ~= last_reported_state then
            last_reported_state = state
            send_game_state(state)
        end
        push_selectability(state)
        refresh_selectability(state)
        update_one_spawn_effect_availability(state)
    end

    while true do
        local status, a, b, c, d = ccnet.cc_poll_message(socket_handle)
        if status == "message" then
            local id, msg_type, code, duration = a, b, c, d
            handle_request(id, msg_type, code, duration)
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
