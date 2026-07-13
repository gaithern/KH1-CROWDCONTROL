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

local kh1 = require("kh1_lua_library")
local json = require("json")
local ccnet = require("kh1_crowdcontrol_native")

local CC_HOST = "127.0.0.1"
local CC_PORT = 43384 -- must match the port the Crowd Control app's SimpleTCP connector is configured to use for this pack
local RECONNECT_INTERVAL_SECONDS = 5

local socket_handle = nil
local recv_buffer = ""
local next_reconnect_attempt = 0

-- ####################### --
-- # Effect dispatch table # --
-- ####################### --

-- Keyed by the effect "code" Crowd Control sends in each request -- these
-- must match the effect ids declared in the game pack (see
-- pack/kh1-crowdcontrol-pack.json). Each handler receives the decoded
-- request table and returns true/false for whether the effect applied.
local effect_handlers = {
    -- The only play_se2 id confirmed live/audible so far (see
    -- kh1_lua_library.lua's play_se2 comment) -- expand this list only with
    -- ids you've verified live, per that function's crash-risk warning.
    sound_31 = function(request)
        return kh1.play_se2(31, 0)
    end,

    -- TODO: PLACEHOLDER item_id, not verified safe. spawn_prize's own
    -- warning in kh1_lua_library.lua notes an unverified id can corrupt the
    -- pickup icon's animation -- confirm a real, tested item_id before
    -- enabling this effect for real redemptions.
    item_placeholder = function(request)
        return kh1.spawn_prize(1)
    end,

    -- Free-text effect: the redeemer's typed text (Crowd Control text
    -- parameter, see pack/kh1-crowdcontrol-pack.json) is shown as-is.
    message = function(request)
        local text = request.parameters and request.parameters.text
        if not text or text == "" then
            return false
        end
        return kh1.open_text_box(text, 1, 8)
    end,
}

-- ############### --
-- # Connection    # --
-- ############### --

local function try_connect()
    local ok, handle_or_err = ccnet.cc_connect(CC_HOST, CC_PORT)
    if ok then
        socket_handle = handle_or_err
        recv_buffer = ""
        ConsolePrint(string.format("[Crowd Control] Connected to %s:%d", CC_HOST, CC_PORT))
    else
        socket_handle = nil
    end
end

local function disconnect()
    if socket_handle then
        ccnet.cc_close(socket_handle)
        socket_handle = nil
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
    if handler then
        local call_ok, result = pcall(handler, request)
        ok = call_ok and result and true or false
    end
    send_response(request.id, ok)
end

-- ############### --
-- # Frame pump    # --
-- ############### --

function update_crowdcontrol()
    --[[Drives the whole connection: reconnects to the Crowd Control app on a
    timer while disconnected, otherwise drains any complete NUL-terminated
    JSON messages off the socket and dispatches each to effect_handlers.
    Call this every frame from _OnFrame -- mirrors kh1_lua_library's
    update_text_boxes() pattern; harmless/no-op while disconnected.]]
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
    update_crowdcontrol()
end
