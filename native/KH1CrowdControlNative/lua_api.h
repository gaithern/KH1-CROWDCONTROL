#pragma once
#include <cstddef>

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

extern t_lua_gettop       p_lua_gettop;
extern t_lua_tointegerx   p_lua_tointegerx;
extern t_lua_tolstring    p_lua_tolstring;
extern t_lua_pushinteger  p_lua_pushinteger;
extern t_lua_pushboolean  p_lua_pushboolean;
extern t_lua_pushstring   p_lua_pushstring;
extern t_lua_pushlstring  p_lua_pushlstring;
extern t_lua_pushnil      p_lua_pushnil;
extern t_luaL_setfuncs    p_luaL_setfuncs;
extern t_lua_createtable  p_lua_createtable;

struct luaL_Reg { const char* name; void* func; };

bool ResolveLuaApi();
