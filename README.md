# KH1 Crowd Control

Lets Twitch [Crowd Control](https://crowdcontrol.live/) redemptions trigger real effects in Kingdom Hearts Final Mix (PC): play a sound, spawn an item near Sora, or pop a custom message on screen.

This mod is a thin connector: it opens a TCP connection to the Crowd Control app and dispatches whatever effects it receives into KH1-LUA-LIBRARY functions (`play_se2`, `spawn_prize`, `open_text_box`). It does not work standalone — **KH1-LUA-LIBRARY must be installed alongside it**.

## Prerequisites

- Kingdom Hearts Final Mix (PC, Steam or EGS) with [OpenKH Panacea](https://openkh.dev/) and **LuaBackend** installed and working.
- The [KH1-LUA-LIBRARY](../KH1-LUA-LIBRARY) mod installed (provides `kh1_lua_library.lua`, `json.lua`, `VersionCheck.lua`, `kh1_native.dll`).
- The [Crowd Control desktop app](https://crowdcontrol.live/), running on the same machine as the game.
- Visual Studio 2022 with the "Desktop development with C++" workload — only needed if you're building the native DLL yourself rather than using a prebuilt one.

## 1. Build the native DLL

`scripts/io_packages/kh1_crowdcontrol_native.dll` is a small WinSock bridge (Lua has no socket library on its own). Build it with:

```powershell
native/KH1CrowdControlNative/build.ps1
```

This compiles Release|x64 and drops the DLL directly into `scripts/io_packages/kh1_crowdcontrol_native.dll`, so the repo is immediately ready to install after a build. (The DLL is committed to the repo too, so most users won't need to rebuild it — only do this if you've changed `dllmain.cpp`.)

## 2. Install the mod

Same as any other OpenKH Lua mod:

1. Copy this repo's `scripts/` folder (both `kh1_crowdcontrol.lua` and `io_packages/kh1_crowdcontrol_native.dll`) into your KH1 mods folder, alongside KH1-LUA-LIBRARY's own files — or install both mods through the OpenKH Mod Manager if you've packaged them there.
2. Confirm both mods' files end up merged into the *same* `scripts/` tree in the game's mod-load folder. `require("kh1_lua_library")` and `require("kh1_crowdcontrol_native")` both need to resolve at runtime; OpenKH's Lua loader searches across all installed mods' `scripts/io_packages/` for this.

`kh1_crowdcontrol.lua` must stay directly under `scripts/` (not `scripts/io_packages/`) — that's what makes OpenKH auto-load it and call its `_OnFrame` every frame. See the comment at the top of that file if you're restructuring anything.

## 3. Point it at Crowd Control

`scripts/kh1_crowdcontrol.lua` connects out to the Crowd Control app as a TCP client (Crowd Control hosts the server side — this is its ["SimpleTCP"](https://developer.crowdcontrol.live/sdk/simpletcp/index.html) connector, the option meant for exactly this kind of memory-editing integration):

```lua
local CC_HOST = "127.0.0.1"
local CC_PORT = 43384
```

- `CC_PORT` must match whatever port you configure for this pack's SimpleTCP connector in the Crowd Control app.
- The mod retries the connection every 5 seconds if the Crowd Control app isn't up yet, so load order between the game and the app doesn't matter.

`pack/kh1-crowdcontrol-pack.json` is a **draft** game pack definition (effect list + connector config) for the Crowd Control side. It hasn't been validated against Crowd Control's actual pack-submission format yet — check it against [developer.crowdcontrol.live](https://developer.crowdcontrol.live/sdk/simpletcp/properties.html) (or Crowd Control's pack-builder tooling, if you're already set up with that) before relying on it.

## 4. Effects currently wired up

See the `effect_handlers` table in `scripts/kh1_crowdcontrol.lua`:

| Effect code       | Does what                                    | Status |
|--------------------|-----------------------------------------------|--------|
| `sound_31`          | Plays KH1 sound effect id 31                  | Confirmed live/audible |
| `item_placeholder`  | Spawns item id `1` near Sora via `spawn_prize` | **Disabled** (`inactive: true` in the pack) — placeholder id, not verified safe |
| `message`           | Shows viewer-typed free text via `open_text_box` for 8 seconds | Untested end-to-end |

Only `se_id` values you've personally confirmed live should ever be added to `effect_handlers` — `play_se2`'s own comment in `kh1_lua_library.lua` documents at least one id that crashed the game outright. Same caution applies to `spawn_prize`'s `item_id`: a wrong id has been observed corrupting the pickup icon's animation rather than failing cleanly.

Message text comes straight from the Crowd Control redemption with no filtering — moderate at the Crowd Control-effect level (per-effect cooldowns/blocklists in the app) if that matters for your stream.

## Troubleshooting

Both the game process and this mod log to files next to wherever the DLLs are loaded from:

- `kh1_crowdcontrol_native.log` — connection attempts, socket errors.
- `kh1_native.log` (from KH1-LUA-LIBRARY) — the underlying game-function calls (`play_se2`, `spawn_prize`, text box hooks).

If effects never fire, check `kh1_crowdcontrol_native.log` first for whether the TCP connection to the Crowd Control app ever succeeded.
