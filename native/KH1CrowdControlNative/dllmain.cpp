#include "pch.h"
#include <tlhelp32.h>
#include <cstdio>
#include <cstring>
#include <cstdint>

#pragma comment(lib, "ws2_32.lib")

// --- DEBUG LOGGING ---
static char g_dllDir[MAX_PATH] = "";

static void LogDebug(const char* msg) {
    if (!g_dllDir[0]) return;
    char path[MAX_PATH];
    snprintf(path, MAX_PATH, "%skh1_crowdcontrol_native.log", g_dllDir);
    FILE* f = nullptr;
    if (fopen_s(&f, path, "a") == 0 && f) {
        SYSTEMTIME st;
        GetLocalTime(&st);
        fprintf(f, "[%02d:%02d:%02d] %s\n", st.wHour, st.wMinute, st.wSecond, msg);
        fclose(f);
    }
}

// --- LUA FUNCTION POINTERS ---
// Resolved against whichever already-loaded module in the host process exports
// the Lua C API (no Lua headers needed) -- same technique as kh1_native.dll's
// dllmain.cpp (see FindLuaModule() below); duplicated here rather than shared
// because this module is intentionally independent of it.
typedef int          (__cdecl* t_lua_gettop)(void* L);
typedef long long    (__cdecl* t_lua_tointegerx)(void* L, int idx, int* isnum);
typedef const char*  (__cdecl* t_lua_tolstring)(void* L, int idx, size_t* len);
typedef void         (__cdecl* t_lua_pushinteger)(void* L, long long n);
typedef void         (__cdecl* t_lua_pushboolean)(void* L, int b);
typedef const char*  (__cdecl* t_lua_pushstring)(void* L, const char* s);
typedef const char*  (__cdecl* t_lua_pushlstring)(void* L, const char* s, size_t len);
typedef void         (__cdecl* t_lua_pushnil)(void* L);
typedef void         (__cdecl* t_luaL_setfuncs)(void* L, const void* l, int nup);
typedef void         (__cdecl* t_lua_createtable)(void* L, int narr, int nrec);

static t_lua_gettop       p_lua_gettop       = nullptr;
static t_lua_tointegerx   p_lua_tointegerx   = nullptr;
static t_lua_tolstring    p_lua_tolstring    = nullptr;
static t_lua_pushinteger  p_lua_pushinteger  = nullptr;
static t_lua_pushboolean  p_lua_pushboolean  = nullptr;
static t_lua_pushstring   p_lua_pushstring   = nullptr;
static t_lua_pushlstring  p_lua_pushlstring  = nullptr;
static t_lua_pushnil      p_lua_pushnil      = nullptr;
static t_luaL_setfuncs    p_luaL_setfuncs    = nullptr;
static t_lua_createtable  p_lua_createtable  = nullptr;

struct luaL_Reg { const char* name; void* func; };

// --- WINSOCK STATE ---
static bool g_wsaInitialized = false;

static bool EnsureWinsock() {
    if (g_wsaInitialized) return true;
    WSADATA wsaData;
    if (WSAStartup(MAKEWORD(2, 2), &wsaData) != 0) {
        LogDebug("EnsureWinsock: WSAStartup failed");
        return false;
    }
    g_wsaInitialized = true;
    return true;
}

// cc_connect(host, port) -> ok(boolean), handle(integer) | errorMessage(string)
//
// Deliberately a BLOCKING connect() call -- this is only ever used to reach
// the Crowd Control app on the local machine (127.0.0.1/localhost), where a
// loopback connect either succeeds or is refused near-instantly (no real
// network round trip), so there's no risk of hanging the game's frame loop.
// This sidesteps having to track "connection in progress" state for a
// non-blocking connect, keeping this bridge as small as kh1_native.dll's.
// The socket is switched to non-blocking immediately afterward, so all
// subsequent cc_send/cc_recv calls never block.
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

    if (connect(s, (sockaddr*)&addr, sizeof(addr)) == SOCKET_ERROR) {
        char msg[96];
        snprintf(msg, sizeof(msg), "cc_connect: connect() failed, err=%d", WSAGetLastError());
        LogDebug(msg);
        closesocket(s);
        p_lua_pushboolean(L, 0);
        p_lua_pushstring(L, "cc_connect: connection refused or unreachable");
        return 2;
    }

    u_long nonBlocking = 1;
    ioctlsocket(s, FIONBIO, &nonBlocking);

    LogDebug("cc_connect: connected");
    p_lua_pushboolean(L, 1);
    p_lua_pushinteger(L, (long long)s);
    return 2;
}

// cc_send(handle, data) -> ok(boolean), errorMessage(string, only on failure)
// `data` is sent exactly as given (binary-safe, via lua_tolstring's explicit
// length -- Crowd Control's SimpleTCP framing appends its own NUL terminator
// per message, which the Lua glue is responsible for, not this function).
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

// cc_recv(handle) -> data(string) | nil, errorMessage(string)
// Non-blocking: returns "" (not nil) when the socket is open but nothing is
// available yet -- only returns nil on an actual close/reset, so the glue
// script can tell "poll again next frame" apart from "reconnect".
static const int RECV_BUFFER_SIZE = 4096;

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

// cc_close(handle) -> (none)
extern "C" int l_cc_close(void* L) {
    SOCKET s = (SOCKET)p_lua_tointegerx(L, 1, nullptr);
    closesocket(s);
    return 0;
}

static const luaL_Reg kh1_crowdcontrol_native_lib[] = {
    {"cc_connect", reinterpret_cast<void*>(l_cc_connect)},
    {"cc_send", reinterpret_cast<void*>(l_cc_send)},
    {"cc_recv", reinterpret_cast<void*>(l_cc_recv)},
    {"cc_close", reinterpret_cast<void*>(l_cc_close)},
    {nullptr, nullptr}
};

// LuaBackend (the OpenKH Lua host) embeds the Lua 5.4 runtime in its own DLL
// rather than loading a separate "lua54.dll", and that host DLL's name varies
// by build/game. So instead of guessing a filename, walk every module loaded
// in this process and use whichever one actually exports the Lua C API.
static HMODULE FindLuaModule() {
    HANDLE snap = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, GetCurrentProcessId());
    if (snap == INVALID_HANDLE_VALUE) return nullptr;

    HMODULE found = nullptr;
    MODULEENTRY32W me = {};
    me.dwSize = sizeof(me);
    if (Module32FirstW(snap, &me)) {
        do {
            if (GetProcAddress(me.hModule, "lua_gettop")) {
                found = me.hModule;
                char msg[MAX_PATH + 32];
                snprintf(msg, sizeof(msg), "Found Lua API in module: %ls", me.szModule);
                LogDebug(msg);
                break;
            }
        } while (Module32NextW(snap, &me));
    }
    CloseHandle(snap);
    return found;
}

extern "C" __declspec(dllexport) int luaopen_kh1_crowdcontrol_native(void* L) {
    LogDebug("luaopen_kh1_crowdcontrol_native called");

    HMODULE hLua = FindLuaModule();
    if (hLua && !p_lua_gettop) {
        p_lua_gettop      = (t_lua_gettop)      GetProcAddress(hLua, "lua_gettop");
        p_lua_tointegerx  = (t_lua_tointegerx)  GetProcAddress(hLua, "lua_tointegerx");
        p_lua_tolstring   = (t_lua_tolstring)   GetProcAddress(hLua, "lua_tolstring");
        p_lua_pushinteger = (t_lua_pushinteger) GetProcAddress(hLua, "lua_pushinteger");
        p_lua_pushboolean = (t_lua_pushboolean) GetProcAddress(hLua, "lua_pushboolean");
        p_lua_pushstring  = (t_lua_pushstring)  GetProcAddress(hLua, "lua_pushstring");
        p_lua_pushlstring = (t_lua_pushlstring) GetProcAddress(hLua, "lua_pushlstring");
        p_lua_pushnil     = (t_lua_pushnil)     GetProcAddress(hLua, "lua_pushnil");
        p_luaL_setfuncs   = (t_luaL_setfuncs)   GetProcAddress(hLua, "luaL_setfuncs");
        p_lua_createtable = (t_lua_createtable) GetProcAddress(hLua, "lua_createtable");
    }

    if (!p_lua_gettop || !p_lua_tointegerx || !p_lua_tolstring || !p_lua_pushinteger || !p_lua_pushboolean ||
        !p_lua_pushstring || !p_lua_pushlstring || !p_lua_pushnil || !p_luaL_setfuncs || !p_lua_createtable) {
        // Couldn't find a loaded module exporting the Lua C API -- bail out
        // without touching any of them. Returning 0 (no pushed values) makes
        // require() hand back `true` rather than crashing on a null function
        // pointer call.
        LogDebug("luaopen_kh1_crowdcontrol_native: failed to resolve Lua API exports, aborting safely");
        return 0;
    }

    p_lua_createtable(L, 0, 4);
    p_luaL_setfuncs(L, kh1_crowdcontrol_native_lib, 0);
    return 1;
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID lpReserved) {
    if (reason == DLL_PROCESS_ATTACH) {
        GetModuleFileNameA(hModule, g_dllDir, MAX_PATH);
        char* last = strrchr(g_dllDir, '\\');
        if (last) *(last + 1) = '\0';

        // Pin ourselves in memory with an extra reference we never release.
        // LuaBackend's script-refresh feature appears to FreeLibrary() native
        // modules it required as part of giving scripts a clean reload -- an
        // open TCP socket handle is process state that must survive a hot
        // reload the same way kh1_native.dll's installed hooks do (see that
        // module's DllMain for the fuller explanation), otherwise a reload
        // mid-session would silently orphan the Crowd Control connection.
        char selfPath[MAX_PATH];
        GetModuleFileNameA(hModule, selfPath, MAX_PATH);
        LoadLibraryA(selfPath);
    } else if (reason == DLL_PROCESS_DETACH) {
        if (g_wsaInitialized) {
            WSACleanup();
        }
    }
    return TRUE;
}
