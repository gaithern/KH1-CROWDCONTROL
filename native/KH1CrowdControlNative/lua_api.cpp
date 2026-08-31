#include "pch.h"
#include <tlhelp32.h>
#include <cstdio>
#include "log.h"
#include "lua_api.h"

// Empty function pointers to be found.
t_lua_gettop       p_lua_gettop       = nullptr;
t_lua_tointegerx   p_lua_tointegerx   = nullptr;
t_lua_tolstring    p_lua_tolstring    = nullptr;
t_lua_pushinteger  p_lua_pushinteger  = nullptr;
t_lua_pushboolean  p_lua_pushboolean  = nullptr;
t_lua_pushstring   p_lua_pushstring   = nullptr;
t_lua_pushlstring  p_lua_pushlstring  = nullptr;
t_lua_pushnil      p_lua_pushnil      = nullptr;
t_luaL_setfuncs    p_luaL_setfuncs    = nullptr;
t_lua_createtable  p_lua_createtable  = nullptr;

// Identifies the Lua module to acquire
// Lua C API function pointers from.
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

// Assigns Lua C API function pointers for the required
// funcions.  Returns true if they all were found.
bool ResolveLuaApi() {
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

    return p_lua_gettop && p_lua_tointegerx && p_lua_tolstring && p_lua_pushinteger
        && p_lua_pushboolean && p_lua_pushstring && p_lua_pushlstring && p_lua_pushnil
        && p_luaL_setfuncs && p_lua_createtable;
}
