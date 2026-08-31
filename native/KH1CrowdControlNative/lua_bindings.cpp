#include "pch.h"
#include <cstdio>
#include <cstring>
#include <string>
#include <map>
#include "log.h"
#include "lua_api.h"
#include "winsock.h"
#include "message_parser.h"

static const int RECV_BUFFER_SIZE = 4096;

static std::map<SOCKET, std::string> g_recvBuffers;

// Allows attempting to connect to CrowdControl
// via Winsock from Lua.
extern "C" int l_cc_connect(void* L) {
    if (!EnsureWinsock()) {
        p_lua_pushboolean(L, 0);
        p_lua_pushstring(L, "cc_connect: WSAStartup failed");
        return 2;
    }

    size_t hostLen = 0;
    const char* host = p_lua_tolstring(L, 1, &hostLen);
    long long port = p_lua_tointegerx(L, 2, nullptr);

    if (!host || port <= 0 || port > 65535) {
        p_lua_pushboolean(L, 0);
        p_lua_pushstring(L, "cc_connect: invalid host/port");
        return 2;
    }

    SOCKET s = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if (s == INVALID_SOCKET) {
        char msg[64];
        snprintf(msg, sizeof(msg), "cc_connect: socket() failed, err=%d", WSAGetLastError());
        LogDebug(msg);
        p_lua_pushboolean(L, 0);
        p_lua_pushstring(L, "cc_connect: socket() failed");
        return 2;
    }

    char hostBuf[256];
    size_t copyLen = hostLen < sizeof(hostBuf) - 1 ? hostLen : sizeof(hostBuf) - 1;
    memcpy(hostBuf, host, copyLen);
    hostBuf[copyLen] = 0;

    sockaddr_in addr = {};
    addr.sin_family = AF_INET;
    addr.sin_port = htons((u_short)port);
    if (InetPtonA(AF_INET, hostBuf, &addr.sin_addr) != 1) {
        closesocket(s);
        p_lua_pushboolean(L, 0);
        p_lua_pushstring(L, "cc_connect: host must be a dotted IPv4 address (e.g. 127.0.0.1)");
        return 2;
    }

    u_long nonBlocking = 1;
    ioctlsocket(s, FIONBIO, &nonBlocking);

    if (connect(s, (sockaddr*)&addr, sizeof(addr)) == SOCKET_ERROR) {
        int err = WSAGetLastError();
        if (err != WSAEWOULDBLOCK) {
            char msg[96];
            snprintf(msg, sizeof(msg), "cc_connect: connect() failed immediately, err=%d", err);
            LogDebug(msg);
            closesocket(s);
            p_lua_pushboolean(L, 0);
            p_lua_pushstring(L, "cc_connect: connection refused or unreachable");
            return 2;
        }
    }

    LogDebug("cc_connect: connect initiated (non-blocking), awaiting cc_connect_status");
    p_lua_pushboolean(L, 1);
    p_lua_pushinteger(L, (long long)s);
    return 2;
}

// Allows retrieving CrowdControl server connection
// status from Lua.
extern "C" int l_cc_connect_status(void* L) {
    SOCKET s = (SOCKET)p_lua_tointegerx(L, 1, nullptr);

    fd_set writeSet, exceptSet;
    FD_ZERO(&writeSet);
    FD_ZERO(&exceptSet);
    FD_SET(s, &writeSet);
    FD_SET(s, &exceptSet);

    timeval zeroTimeout = { 0, 0 };
    int result = select(0, nullptr, &writeSet, &exceptSet, &zeroTimeout);
    if (result == SOCKET_ERROR) {
        char msg[64];
        snprintf(msg, sizeof(msg), "cc_connect_status: select() failed, err=%d", WSAGetLastError());
        LogDebug(msg);
        p_lua_pushstring(L, "failed");
        return 1;
    }

    if (FD_ISSET(s, &exceptSet)) {
        LogDebug("cc_connect_status: connect failed (exception set)");
        p_lua_pushstring(L, "failed");
        return 1;
    }

    if (FD_ISSET(s, &writeSet)) {
        int soError = 0;
        int soErrorLen = sizeof(soError);
        if (getsockopt(s, SOL_SOCKET, SO_ERROR, (char*)&soError, &soErrorLen) == SOCKET_ERROR || soError != 0) {
            char msg[64];
            snprintf(msg, sizeof(msg), "cc_connect_status: connect failed, SO_ERROR=%d", soError);
            LogDebug(msg);
            p_lua_pushstring(L, "failed");
            return 1;
        }
        LogDebug("cc_connect_status: connected");
        p_lua_pushstring(L, "connected");
        return 1;
    }

    p_lua_pushstring(L, "connecting");
    return 1;
}

// Allows sending a packet to the CrowdControl
// server from Lua.
extern "C" int l_cc_send(void* L) {
    SOCKET s = (SOCKET)p_lua_tointegerx(L, 1, nullptr);
    size_t len = 0;
    const char* data = p_lua_tolstring(L, 2, &len);
    if (!data) {
        p_lua_pushboolean(L, 0);
        p_lua_pushstring(L, "cc_send: no data");
        return 2;
    }

    int sent = send(s, data, (int)len, 0);
    if (sent == SOCKET_ERROR) {
        char msg[64];
        snprintf(msg, sizeof(msg), "cc_send: send() failed, err=%d", WSAGetLastError());
        LogDebug(msg);
        p_lua_pushboolean(L, 0);
        p_lua_pushstring(L, "cc_send: send() failed");
        return 2;
    }

    p_lua_pushboolean(L, 1);
    return 1;
}

// Allows receiving the raw bytes that happen
// to be in the socket at the moment from the
// connection to CrowdControl server in Lua.
// Currently unused.
extern "C" int l_cc_recv(void* L) {
    SOCKET s = (SOCKET)p_lua_tointegerx(L, 1, nullptr);
    char buf[RECV_BUFFER_SIZE];

    int received = recv(s, buf, sizeof(buf), 0);
    if (received > 0) {
        p_lua_pushlstring(L, buf, (size_t)received);
        return 1;
    }
    if (received == 0) {
        LogDebug("cc_recv: peer closed connection");
        p_lua_pushnil(L);
        p_lua_pushstring(L, "closed");
        return 2;
    }

    int err = WSAGetLastError();
    if (err == WSAEWOULDBLOCK) {
        p_lua_pushlstring(L, "", 0);
        return 1;
    }

    char msg[64];
    snprintf(msg, sizeof(msg), "cc_recv: recv() failed, err=%d", err);
    LogDebug(msg);
    p_lua_pushnil(L);
    p_lua_pushstring(L, "closed");
    return 2;
}

// Allows getting the bytes in the connection to 
// CrowdControl's socket and appends them to a
// per-socket accumulation buffer - parsing out
// id, type, code, duration, and viewer from Lua.
extern "C" int l_cc_poll_message(void* L) {
    SOCKET s = (SOCKET)p_lua_tointegerx(L, 1, nullptr);

    char buf[RECV_BUFFER_SIZE];
    int received = recv(s, buf, sizeof(buf), 0);
    if (received > 0) {
        g_recvBuffers[s].append(buf, (size_t)received);
    } else if (received == 0) {
        LogDebug("cc_poll_message: peer closed connection");
        g_recvBuffers.erase(s);
        p_lua_pushstring(L, "closed");
        p_lua_pushstring(L, "peer closed connection");
        return 2;
    } else {
        int err = WSAGetLastError();
        if (err != WSAEWOULDBLOCK) {
            char msg[64];
            snprintf(msg, sizeof(msg), "cc_poll_message: recv() failed, err=%d", err);
            LogDebug(msg);
            g_recvBuffers.erase(s);
            p_lua_pushstring(L, "closed");
            p_lua_pushstring(L, "recv() failed");
            return 2;
        }
    }

    std::string& buffer = g_recvBuffers[s];
    size_t nulPos = buffer.find('\0');
    if (nulPos == std::string::npos) {
        p_lua_pushstring(L, "none");
        return 1;
    }

    std::string message = buffer.substr(0, nulPos);
    buffer.erase(0, nulPos + 1);

    long long id = 0, msgType = 0, duration = 0;
    bool hasId = ExtractIntField(message, "id", id);
    bool hasType = ExtractIntField(message, "type", msgType);
    bool hasDuration = ExtractIntField(message, "duration", duration);
    std::string code, viewer;
    bool hasCode = ExtractStringField(message, "code", code);
    bool hasViewer = ExtractStringField(message, "viewer", viewer);

    p_lua_pushstring(L, "message");
    if (hasId) p_lua_pushinteger(L, id); else p_lua_pushnil(L);
    if (hasType) p_lua_pushinteger(L, msgType); else p_lua_pushnil(L);
    if (hasCode) p_lua_pushlstring(L, code.c_str(), code.size()); else p_lua_pushnil(L);
    if (hasDuration) p_lua_pushinteger(L, duration); else p_lua_pushnil(L);
    if (hasViewer) p_lua_pushlstring(L, viewer.c_str(), viewer.size()); else p_lua_pushnil(L);
    return 6;
}

// Allows closing the connection to the CrowdControl
// server from Lua.
extern "C" int l_cc_close(void* L) {
    SOCKET s = (SOCKET)p_lua_tointegerx(L, 1, nullptr);
    g_recvBuffers.erase(s);
    closesocket(s);
    return 0;
}

// Allows writing to the log for debugging from Lua.
extern "C" int l_cc_log(void* L) {
    size_t len = 0;
    const char* msg = p_lua_tolstring(L, 1, &len);
    if (msg) {
        std::string msgCopy(msg, len);
        LogDebug(msgCopy.c_str());
    }
    return 0;
}

// Define the registration table for functions lua
// side can call.
static const luaL_Reg kh1_crowdcontrol_native_lib[] = {
    {"cc_connect", reinterpret_cast<void*>(l_cc_connect)},
    {"cc_connect_status", reinterpret_cast<void*>(l_cc_connect_status)},
    {"cc_send", reinterpret_cast<void*>(l_cc_send)},
    {"cc_recv", reinterpret_cast<void*>(l_cc_recv)},
    {"cc_poll_message", reinterpret_cast<void*>(l_cc_poll_message)},
    {"cc_close", reinterpret_cast<void*>(l_cc_close)},
    {"cc_log", reinterpret_cast<void*>(l_cc_log)},
    {nullptr, nullptr}
};

// Require "kh1_crowdcontrol_native" from the lua side searches specifically for
// "luaopen_kh1_crowdcontrol_native" - comprised from "luaopen_" + module_name.
// Cannot be changed.
extern "C" __declspec(dllexport) int luaopen_kh1_crowdcontrol_native(void* L) {
    LogDebug("luaopen_kh1_crowdcontrol_native called");

    if (!ResolveLuaApi()) {
        LogDebug("luaopen_kh1_crowdcontrol_native: failed to resolve Lua API exports, aborting safely");
        return 0;
    }

    p_lua_createtable(L, 0, 7);
    p_luaL_setfuncs(L, kh1_crowdcontrol_native_lib, 0);
    return 1;
}
