# KH1 Crowd Control

Lets Twitch [Crowd Control](https://crowdcontrol.live/) redemptions trigger real effects in Kingdom Hearts Final Mix (PC) — spawn items and Heartless near Sora, pop messages on screen, grant abilities, cripple combos, or KO him outright.

The mod is a thin connector: it opens a TCP connection to the Crowd Control app and dispatches the effects it receives into [KH1-LUA-LIBRARY](../KH1-LUA-LIBRARY), which does the actual game-memory work. It does not run standalone.

## Installation

- Kingdom Hearts Final Mix (PC, Steam or EGS) with [OpenKH](https://openkh.dev/) (LuaBackend and Panacea).
- [gaithern//KH1-CROWDCONTROL](https://github.com/gaithern/KH1-CROWDCONTROL) installed in OpenKH.
- [gaithern/KH1-LUA-LIBRARY](https://github.com/gaithern/KH1-LUA-LIBRARY) installed in OpenKH.
- The [Crowd Control app](https://crowdcontrol.live/) running on the same machine.

## Effects

Around 90 effects, all one-shot — item spawns, Heartless spawns, preset on-screen messages, ability grants, a combo-limit crush, Heartless Angel, and an instant KO. The full list lives in the `effect_handlers` table in the Lua script and mirrors the pack definition.

The mod reports real game state (cutscene, menu, Gummi ship, KO'd) back to Crowd Control, so effects are greyed out or queued for retry instead of firing into situations where they'd be wasted.

## Troubleshooting

Logs are written next to the DLLs: `kh1_crowdcontrol_native.log` for this mod's connection and effect activity, `kh1_native.log` for the underlying game-function calls from KH1-LUA-LIBRARY. If nothing fires, check the first for whether the TCP connection succeeded and whether requests are arriving.
