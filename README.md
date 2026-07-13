# KH1 Crowd Control

Lets Twitch [Crowd Control](https://crowdcontrol.live/) redemptions trigger real effects in Kingdom Hearts Final Mix (PC): play a sound, spawn an item near Sora, pop a custom message on screen, grant abilities, mess with combo limits/animation speed/summon time, and toggle a handful of gameplay restrictions on and off.

This mod is a thin connector: it opens a TCP connection to the Crowd Control app and dispatches whatever effects it receives into KH1-LUA-LIBRARY functions. It does not work standalone — **KH1-LUA-LIBRARY must be installed alongside it**.

> **Status:** this repo briefly went down two wrong paths before landing here — first a JSON pack targeting the SimpleTCP connector (wrong file format), then a full PubSub/WebSocket rewrite (wrong connector entirely, based on a docs page that turned out not to apply). The Crowd Control team confirmed directly on Discord: *"I'd suggest a Simple TCP C# pack approach rather than a full blown socket implementation."* `pack/KH1CrowdControlPack.cs` now **loads clean in the real Crowd Control SDK** (no compiler errors, no QA warnings) — confirmed 2026-07-13. Still untested: an actual live effect redemption end-to-end through the game (see the effects status table below and the `TODO(verify)` comments remaining in `scripts/kh1_crowdcontrol.lua`).

## Prerequisites

- Kingdom Hearts Final Mix (PC, Steam or EGS) with [OpenKH Panacea](https://openkh.dev/) and **LuaBackend** installed and working.
- The [KH1-LUA-LIBRARY](../KH1-LUA-LIBRARY) mod installed (provides `kh1_lua_library.lua`, `json.lua`, `VersionCheck.lua`, `kh1_native.dll`).
- The [Crowd Control desktop app](https://crowdcontrol.live/), running on the same machine as the game.
- The [Crowd Control SDK](https://developer.crowdcontrol.live/sdk/#download-the-sdk) ([GitHub releases](https://github.com/WarpWorld/CrowdControl.SDK/releases)) — a separate downloadable installer from the main streaming app. This is what actually loads `pack/KH1CrowdControlPack.cs` via its "Load Pack Source" feature for local testing, and its compiler errors are the fastest way to correct anything guessed-wrong in that file (see its own header comment for what's unconfirmed).
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

`scripts/kh1_crowdcontrol.lua` connects out to the Crowd Control app as a TCP client (Crowd Control hosts the server side — this is its ["SimpleTCP"](https://developer.crowdcontrol.live/sdk/simpletcp/index.html) connector, confirmed by the Crowd Control team as the right approach for this kind of memory-editing integration):

```lua
local CC_HOST = "127.0.0.1"
local CC_PORT = 43384
```

- `CC_PORT` must match whatever port you configure for this pack's SimpleTCP connector.
- The mod retries the connection every 5 seconds if the Crowd Control app isn't up yet, so load order between the game and the app doesn't matter.

`pack/KH1CrowdControlPack.cs` is the game pack definition (effect list + connector config) for the Crowd Control side — a C# source file loaded into the Crowd Control **SDK** app (see Prerequisites) via its "Load Pack Source" pack editor, not JSON. It **loads clean** (no compiler errors, no QA warnings) as of 2026-07-13 — see the file's own header comment for exactly what that confirmed (the `ConnectorLib.SimpleTCP` namespace for `SimpleTCPServerConnector`, `Game(...)`'s real 4-argument signature, `Effects` needing to be typed `EffectList`) versus what's still untested (an actual live effect firing through the game). Once a live redemption is confirmed working, public listing requires reaching out on Crowd Control's Discord per their [SDK overview](https://developer.crowdcontrol.live/sdk/).

## 4. Effects currently wired up

See the `effect_handlers` table in `scripts/kh1_crowdcontrol.lua` (40 effects total) and the matching entries in `pack/KH1CrowdControlPack.cs`.

**Timed effects** auto-revert: Crowd Control sends a `duration` (ms) with the request; the mod tracks a deadline per effect *code* (keyed by code, not per-redemption — see the `active_timed_effects` comment in the script for why) and reverts automatically via `_OnFrame`, falling back to 30s if no duration is given. A second redemption of an already-active effect just extends its timer rather than re-applying.

| Effect code(s) | Does what | Status |
|---|---|---|
| `sound_31` | Plays KH1 sound effect id 31 | Confirmed live/audible |
| `item_placeholder` | Spawns item id `1` near Sora via `spawn_prize` | **Disabled** (`inactive: true` in the pack) — placeholder id, not verified safe |
| `message` | Shows viewer-typed free text via `open_text_box` for 8 seconds | Untested end-to-end |
| `force_scan`, `force_combo_master`, `summon_anywhere`, `midair_dodge_guard`, `air_items` | Toggle one of `kh1_lua_library`'s on/off gameplay patches on for 30s, then off | Untested end-to-end; the on/off values themselves are from the existing library |
| `mega_combos` / `no_combos` | Sets ground+air combo limit to 10 / 1 for 30s, restores the real captured original after | Untested end-to-end |
| `slow_motion` / `hyper_speed` | Multiplies current animation speed by 0.5 / 2.0 for 30s, restores original after | Untested end-to-end |
| `summon_time_half` / `summon_time_double` | Multiplies summon duration by 0.5 / 2.0 for 45s, restores 1.0x after | Untested end-to-end |
| `magic_boost` / `magic_nerf` | Multiplies every spell's effectiveness byte by 1.5 / 0.5 for 30s, restores captured originals after | **Experimental** — `set_spell_effectiveness` has no documented safe value range anywhere in `kh1_lua_library`; scaling the current value (rather than writing a guessed constant) should be low-risk but hasn't been confirmed live |
| `ability_*` (24 effects, one per named ability in `enable_ability`) | Grants that ability effect via `kh1.enable_ability("...")` | One-shot, **permanent** — there's no matching disable, so nothing to revert to |

Only `se_id` values you've personally confirmed live should ever be added to `effect_handlers` — `play_se2`'s own comment in `kh1_lua_library.lua` documents at least one id that crashed the game outright. Same caution applies to `spawn_prize`'s `item_id`: a wrong id has been observed corrupting the pickup icon's animation rather than failing cleanly.

Message text comes straight from the Crowd Control redemption with no filtering — moderate at the Crowd Control-effect level (per-effect cooldowns/blocklists in the app) if that matters for your stream.

### Deliberately not wired

See the comment block above `-- Deliberately NOT wired` in `scripts/kh1_crowdcontrol.lua` for the full reasoning per function. Short version: `grant_sora_ability`/`grant_shared_ability` (no verified numeric ability-id table), `set_stock_at_index`/`set_gummi_qty_at_index` (no known index→item mapping), `set_sora_walk_speed`/`set_sora_run_speed` (no getter, no documented vanilla baseline to revert to), `set_spell_cost` (no getter), `set_attack_animation_data`/`set_command_data` (undocumented raw-array write shapes — highest guessed-risk functions in the library), `show_prompt` (redundant with `message`), `make_sora_actionable` (a debug/unstick utility, not really a chaos effect).

## Troubleshooting

Both the game process and this mod log to files next to wherever the DLLs are loaded from:

- `kh1_crowdcontrol_native.log` — connection attempts, socket errors.
- `kh1_native.log` (from KH1-LUA-LIBRARY) — the underlying game-function calls (`play_se2`, `spawn_prize`, text box hooks).

If effects never fire, check `kh1_crowdcontrol_native.log` first for whether the TCP connection to the Crowd Control app ever succeeded.
