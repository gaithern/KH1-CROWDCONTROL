// Crowd Control SimpleTCP pack definition for Kingdom Hearts Final Mix (PC).
//
// This file loads cleanly (no compiler errors, no QA warnings) in the real
// Crowd Control SDK's "Load Pack Source" (confirmed 2026-07-13), cross-
// checked against WarpWorld/CCPack-PC-Balatro's Balatro.cs (a real pack
// their own README says to load with this same SDK). Crowd Control's
// SimpleTCP packs are a C# source file loaded into the Crowd Control SDK app
// (download: https://developer.crowdcontrol.live/sdk/#download-the-sdk /
// https://github.com/WarpWorld/CrowdControl.SDK/releases) via that pack
// editor, not a standalone JSON file -- confirmed directly by the Crowd
// Control team on Discord ("I'd suggest a Simple TCP C# pack approach
// rather than a full blown socket implementation").
//
// Confirmed (against the SDK compiler/QA checker and/or Balatro.cs):
//   - `using ConnectorLib.SimpleTCP;` is required -- SimpleTCPServerConnector
//     lives there, not in CrowdControl.Common/Games.Packs.
//   - `SimpleTCPPack<SimpleTCPServerConnector>` as the base class, matching
//     Balatro.cs exactly -- this is Crowd Control's "app hosts the server,
//     game connects in" role, which is what kh1_crowdcontrol.lua does
//     (CC_HOST/CC_PORT).
//   - `Game(...)` takes exactly 4 args: `(string id, string name,
//     string platform, ConnectorType connectorType)`. `id` and `name` need
//     to be the exact same space-free string, matching the class name and
//     the last namespace segment (the SDK's QA checker enforces this --
//     see `KH1CrowdControl` used for all four below).
//   - `Effects` must be typed `EffectList`, not `List<Effect>`, but is
//     still initialized with a plain `new List<Effect> { ... }` collection
//     initializer.
//   - The wire protocol this pairs with (NUL-terminated JSON with
//     id/code/type/status/duration/parameters) matches
//     scripts/kh1_crowdcontrol.lua already, per Crowd Control's SimpleJSON
//     structure reference.
//   - `ParameterDef`/`Parameter` is the real "pick one option from a list"
//     API (Crowd Control's team confirmed on Discord there is NO free-text
//     input at all on this connector, only a numeric Quantity slider and
//     Parameters) -- confirmed against WarpWorld/CCPack-PC-DeepRockGalactic's
//     real, working usage. See MessagePreset below: `message` was reworked
//     from a free-text idea into a preset-list picker.
//
// Still NOT confirmed -- this loads clean, but hasn't been tested against a
// live effect redemption yet (use the SDK's "Test Effects" tab, or an
// actual Crowd Control session, with the KH1 mod running and connected):
//   - The `give_*` item effects use item_ids cross-confirmed against
//     KH-1FM-AP-LUA's write_item() and KH1-RANDOMIZER's gift-table encoding
//     (both of which run against real KH1FM), but NEITHER confirms
//     fnc_spawn_prize itself accepts the same id space -- see the
//     GIVE_ITEM_EFFECTS comment in kh1_crowdcontrol.lua. Deliberately
//     limited to simple, non-progression consumables/stat-ups.
//   - Every effect below still needs an actual live-fire test through the
//     game -- see the status table in README.md.

using System;
using System.Collections.Generic;
using ConnectorLib.SimpleTCP;
using CrowdControl.Common;
using CrowdControl.Games.Packs;

// SDK QA warning: "The pack namespace should be
// CrowdControl.Games.Packs.KH1 Crowd Control" -- namespaces can't contain
// spaces, so this is read as "match the class/game safename", same pattern
// as Balatro.cs's `namespace CrowdControl.Games.Packs.Balatro` for a game
// whose id/name was also "Balatro".
namespace CrowdControl.Games.Packs.KH1CrowdControl
{
    // Confirmed against WarpWorld/CCPack-PC-Balatro's real, currently-loadable
    // Balatro.cs (their README points at loading it with this same SDK):
    // SimpleTCPServerConnector lives in ConnectorLib.SimpleTCP, not
    // CrowdControl.Common/Games.Packs -- that missing `using` was the whole
    // CS0246 error, the type name itself was already right for our role
    // (Crowd Control app hosts the server, game connects in).
    public sealed class KH1CrowdControl : SimpleTCPPack<SimpleTCPServerConnector>
    {
        public KH1CrowdControl(UserRecord player, Func<CrowdControlBlock, bool> responseHandler,
            Action<object> statusUpdateHandler) : base(player, responseHandler, statusUpdateHandler)
        {
        }

        // Confirmed against Balatro.cs's real, working
        // `new("Balatro", "Balatro", "PC", ConnectorType.SimpleTCPServerConnector)`
        // -- Game only takes 4 args (id, name, platform, connector type), no
        // separate "path" string and no uint at all; the CS1503 errors
        // against our old 5-arg guess were a completely different, wrong
        // overload. The QA warning's "expected namespace" text tracks
        // `name`, not `id` (it kept suggesting "...KH1 Crowd Control" even
        // after `id` alone was changed) -- C# namespaces can't contain
        // spaces, and Balatro.cs sidesteps this entirely by using the exact
        // same space-free string for both id and name. Doing the same here:
        // the human-readable "KH1 Crowd Control" display name is dropped in
        // favor of matching id/name/namespace/classname exactly, same as
        // Balatro.cs.
        public override Game Game { get; } = new Game("KH1CrowdControl", "KH1CrowdControl",
            "PC", ConnectorType.SimpleTCPServerConnector);

        // Must match CC_HOST / CC_PORT in scripts/kh1_crowdcontrol.lua.
        public override string Host { get; } = "127.0.0.1";
        public override ushort Port { get; } = 43384;

        private const string SoundFolder = "Sound";
        private const string ItemFolder = "Items";
        private const string MessageFolder = "Message";
        private const string ToggleFolder = "Toggles";
        private const string ComboFolder = "Combos";
        private const string SpeedFolder = "Speed";
        private const string SummonFolder = "Summons";
        private const string MagicFolder = "Magic";
        private const string AbilityFolder = "Abilities";

        // Confirmed against WarpWorld/CCPack-PC-DeepRockGalactic's real,
        // working `ParameterDef`/`Parameter` usage (`new("Target Player",
        // "targetPlayerType", new Parameter("Host", "1"), ...)`, used as
        // `Parameters = TargetsMain` on an Effect) -- this is Crowd
        // Control's actual "pick one option from a list" mechanism, per the
        // team's own Discord confirmation that free text isn't supported at
        // all on this connector.
        //
        // CONFIRMED against a real logged request (2026-07-13): the wire
        // request for a Parameters-based effect IS just the base code with a
        // nested parameters object -- {"code":"message","parameters":
        // {"text":{"value":"gg","title":"Message","type":"options"}}} -- NOT
        // a compound "message_gg" code. (An earlier attempt guessed compound
        // codes from the SDK's own Output-panel display text, e.g.
        // "[message_GG]" -- that turned out to just be a UI label, not the
        // real wire code; reverted.) kh1_crowdcontrol.lua reads
        // request.parameters.text.value and looks it up in its own
        // MESSAGE_PRESETS table. Each Parameter's second arg (the value)
        // just needs to match a MESSAGE_PRESETS key there -- kept as short
        // lowercase identifiers here for clarity, not because the wire
        // format requires it.
        private readonly ParameterDef MessagePreset = new("Message", "text",
            new Parameter("GG", "gg"),
            new Parameter("Nice!", "nice"),
            new Parameter("Oops!", "oops"),
            new Parameter("Uh oh...", "uhoh"),
            new Parameter("Nooo!", "nooo"),
            new Parameter("Yay!", "yay"),
            new Parameter("Hello!", "hello"),
            new Parameter("Whoops!", "whoops"),
            new Parameter("So true", "sotrue"),
            new Parameter("Skill issue", "skillissue"),
            new Parameter("Chaos!", "chaos"),
            new Parameter("Good luck", "goodluck"),
            new Parameter("Bad luck", "badluck"),
            new Parameter("Try again", "tryagain"),
            new Parameter("W take", "wtake"),
            new Parameter("L take", "ltake")
        );

        // Confirmed against Balatro.cs (compiler CS1715 also said as much):
        // the property type must be EffectList, not List<Effect> -- but
        // EffectList itself is still initialized with a plain
        // `new List<Effect> { ... }` collection initializer (implicit
        // conversion), same as Balatro.cs does. Uses `=>` (expression-bodied
        // property), NOT `{ get; } = ...` (field initializer) -- confirmed
        // against WarpWorld/CCPack-PC-DeepRockGalactic's real Effects
        // declaration: a field initializer can't reference another instance
        // field (CS0236, hit here once MessagePreset was added below), but
        // an expression-bodied property can since it's evaluated lazily on
        // each access rather than during construction.
        public override EffectList Effects => new List<Effect>
        {
            // The only play_se2 id confirmed live/audible so far -- see
            // kh1_lua_library.lua's play_se2 comment before adding more.
            new Effect("Play Sound Effect", "sound_31")
                {Category = SoundFolder, Price = 25, Description = "Plays a known-safe KH1 sound effect."},

            // Discrete effect per se_id (2-76, excluding 31 above) rather
            // than a parameterized effect -- no confirmed example of a
            // numeric/free-input parameter exists in any real, currently-
            // loadable Crowd Control C# pack checked (WarpWorld/
            // CCPack-PC-Balatro, TheUnknownCod3r/CCPack-PC-TCGCardShopSimulator),
            // so this matches the established convention instead of
            // guessing at an unconfirmed API. se_id=1 is deliberately
            // excluded -- confirmed live 2026-07-12 to crash the game
            // through this exact call path. Every id below is otherwise
            // UNTESTED and may also crash -- a deliberate accepted tradeoff
            // for redemption variety, not an oversight (see README).
            new Effect("Play Sound #2", "sound_2") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 2 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #3", "sound_3") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 3 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #4", "sound_4") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 4 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #5", "sound_5") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 5 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #6", "sound_6") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 6 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #7", "sound_7") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 7 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #8", "sound_8") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 8 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #9", "sound_9") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 9 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #10", "sound_10") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 10 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #11", "sound_11") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 11 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #12", "sound_12") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 12 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #13", "sound_13") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 13 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #14", "sound_14") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 14 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #15", "sound_15") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 15 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #16", "sound_16") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 16 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #17", "sound_17") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 17 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #18", "sound_18") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 18 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #19", "sound_19") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 19 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #20", "sound_20") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 20 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #21", "sound_21") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 21 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #22", "sound_22") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 22 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #23", "sound_23") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 23 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #24", "sound_24") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 24 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #25", "sound_25") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 25 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #26", "sound_26") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 26 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #27", "sound_27") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 27 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #28", "sound_28") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 28 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #29", "sound_29") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 29 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #30", "sound_30") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 30 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #32", "sound_32") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 32 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #33", "sound_33") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 33 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #34", "sound_34") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 34 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #35", "sound_35") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 35 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #36", "sound_36") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 36 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #37", "sound_37") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 37 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #38", "sound_38") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 38 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #39", "sound_39") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 39 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #40", "sound_40") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 40 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #41", "sound_41") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 41 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #42", "sound_42") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 42 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #43", "sound_43") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 43 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #44", "sound_44") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 44 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #45", "sound_45") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 45 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #46", "sound_46") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 46 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #47", "sound_47") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 47 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #48", "sound_48") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 48 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #49", "sound_49") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 49 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #50", "sound_50") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 50 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #51", "sound_51") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 51 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #52", "sound_52") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 52 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #53", "sound_53") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 53 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #54", "sound_54") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 54 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #55", "sound_55") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 55 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #56", "sound_56") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 56 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #57", "sound_57") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 57 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #58", "sound_58") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 58 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #59", "sound_59") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 59 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #60", "sound_60") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 60 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #61", "sound_61") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 61 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #62", "sound_62") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 62 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #63", "sound_63") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 63 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #64", "sound_64") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 64 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #65", "sound_65") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 65 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #66", "sound_66") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 66 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #67", "sound_67") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 67 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #68", "sound_68") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 68 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #69", "sound_69") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 69 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #70", "sound_70") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 70 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #71", "sound_71") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 71 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #72", "sound_72") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 72 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #73", "sound_73") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 73 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #74", "sound_74") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 74 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #75", "sound_75") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 75 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},
            new Effect("Play Sound #76", "sound_76") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 76 -- UNTESTED, may crash (see kh1_crowdcontrol.lua)."},

            // item_ids cross-confirmed against KH-1FM-AP-LUA/KH1-RANDOMIZER's
            // native item-id encoding, but not yet against fnc_spawn_prize
            // itself -- see kh1_crowdcontrol.lua's GIVE_ITEM_EFFECTS comment.
            // Limited to simple consumables/stat-ups, no progression items.
            new Effect("Give Potion", "give_potion") {Category = ItemFolder, Price = 40, Description = "Spawns a Potion pickup near Sora."},
            new Effect("Give Hi-Potion", "give_hi_potion") {Category = ItemFolder, Price = 60, Description = "Spawns a Hi-Potion pickup near Sora."},
            new Effect("Give Ether", "give_ether") {Category = ItemFolder, Price = 60, Description = "Spawns an Ether pickup near Sora."},
            new Effect("Give Elixir", "give_elixir") {Category = ItemFolder, Price = 100, Description = "Spawns an Elixir pickup near Sora."},
            new Effect("Give Mega-Potion", "give_mega_potion") {Category = ItemFolder, Price = 80, Description = "Spawns a Mega-Potion pickup near Sora."},
            new Effect("Give Mega-Ether", "give_mega_ether") {Category = ItemFolder, Price = 80, Description = "Spawns a Mega-Ether pickup near Sora."},
            new Effect("Give Megalixir", "give_megalixir") {Category = ItemFolder, Price = 120, Description = "Spawns a Megalixir pickup near Sora."},
            new Effect("Give Tent", "give_tent") {Category = ItemFolder, Price = 50, Description = "Spawns a Tent pickup near Sora."},
            new Effect("Give Camping Set", "give_camping_set") {Category = ItemFolder, Price = 60, Description = "Spawns a Camping Set pickup near Sora."},
            new Effect("Give Cottage", "give_cottage") {Category = ItemFolder, Price = 90, Description = "Spawns a Cottage pickup near Sora."},
            new Effect("Give Power Up", "give_power_up") {Category = ItemFolder, Price = 70, Description = "Spawns a Power Up pickup near Sora."},
            new Effect("Give Defense Up", "give_defense_up") {Category = ItemFolder, Price = 70, Description = "Spawns a Defense Up pickup near Sora."},
            new Effect("Give AP Up", "give_ap_up") {Category = ItemFolder, Price = 70, Description = "Spawns an AP Up pickup near Sora."},

            // Confirmed by Crowd Control's team on Discord: no free-text
            // input on this connector, only Quantity and Parameters
            // (pick-one-from-a-list/hex-color) -- see MessagePreset above.
            new Effect("Show Message", "message")
                {Category = MessageFolder, Price = 50, Parameters = MessagePreset, Description = "Shows a preset message in the item-pickup popup."},

            new Effect("Force Scan", "force_scan")
                {Category = ToggleFolder, Price = 75, Duration = 30, Description = "Temporarily forces the Scan ability effect on, regardless of whether it's equipped."},
            new Effect("Force Combo Master", "force_combo_master")
                {Category = ToggleFolder, Price = 75, Duration = 30, Description = "Temporarily forces Combo Master behavior on."},
            new Effect("Summon Anywhere", "summon_anywhere")
                {Category = ToggleFolder, Price = 75, Duration = 30, Description = "Temporarily allows summoning anywhere, regardless of the normal restriction."},
            new Effect("Midair Dodge Roll / Guard", "midair_dodge_guard")
                {Category = ToggleFolder, Price = 75, Duration = 30, Description = "Temporarily allows Dodge Roll and Guard to be used in midair."},
            new Effect("Air Items", "air_items")
                {Category = ToggleFolder, Price = 75, Duration = 30, Description = "Temporarily allows items to be used while airborne."},

            new Effect("Mega Combos", "mega_combos")
                {Category = ComboFolder, Price = 100, Duration = 30, Description = "Temporarily raises Sora's ground and air combo limits to 10."},
            new Effect("No Combos", "no_combos")
                {Category = ComboFolder, Price = 100, Duration = 30, Description = "Temporarily drops Sora's ground and air combo limits to 1 (no combo strings)."},

            new Effect("Slow Motion", "slow_motion")
                {Category = SpeedFolder, Price = 75, Duration = 30, Description = "Temporarily halves Sora's animation speed."},
            new Effect("Hyper Speed", "hyper_speed")
                {Category = SpeedFolder, Price = 75, Duration = 30, Description = "Temporarily doubles Sora's animation speed."},

            new Effect("Short Summons", "summon_time_half")
                {Category = SummonFolder, Price = 60, Duration = 45, Description = "Temporarily halves summon duration."},
            new Effect("Long Summons", "summon_time_double")
                {Category = SummonFolder, Price = 60, Duration = 45, Description = "Temporarily doubles summon duration."},

            // EXPERIMENTAL, not live-verified -- see kh1_crowdcontrol.lua's
            // spell_effectiveness_effect comment.
            new Effect("Magic Boost", "magic_boost")
                {Category = MagicFolder, Price = 100, Duration = 30, Description = "EXPERIMENTAL, not live-verified: temporarily boosts every spell's effectiveness by 50%."},
            new Effect("Magic Nerf", "magic_nerf")
                {Category = MagicFolder, Price = 100, Duration = 30, Description = "EXPERIMENTAL, not live-verified: temporarily halves every spell's effectiveness."},

            // One-shot, permanent -- no matching disable exists in
            // kh1_lua_library, so nothing to revert to (see ABILITY_EFFECTS
            // comment in kh1_crowdcontrol.lua).
            new Effect("Grant: Vortex", "ability_vortex") {Category = AbilityFolder, Price = 150, Description = "Grants the Vortex ability effect."},
            new Effect("Grant: Aerial Sweep", "ability_aerial_sweep") {Category = AbilityFolder, Price = 150, Description = "Grants the Aerial Sweep ability effect."},
            new Effect("Grant: Counterattack", "ability_counterattack") {Category = AbilityFolder, Price = 150, Description = "Grants the Counterattack ability effect."},
            new Effect("Grant: Blitz", "ability_blitz") {Category = AbilityFolder, Price = 150, Description = "Grants the Blitz ability effect."},
            new Effect("Grant: Guard", "ability_guard") {Category = AbilityFolder, Price = 150, Description = "Grants the Guard ability effect."},
            new Effect("Grant: Dodge Roll", "ability_dodge_roll") {Category = AbilityFolder, Price = 150, Description = "Grants the Dodge Roll ability effect."},
            new Effect("Grant: Cheer", "ability_cheer") {Category = AbilityFolder, Price = 150, Description = "Grants the Cheer ability effect."},
            new Effect("Grant: Slapshot", "ability_slapshot") {Category = AbilityFolder, Price = 150, Description = "Grants the Slapshot ability effect."},
            new Effect("Grant: Sliding Dash", "ability_sliding_dash") {Category = AbilityFolder, Price = 150, Description = "Grants the Sliding Dash ability effect."},
            new Effect("Grant: Hurricane Blast", "ability_hurricane_blast") {Category = AbilityFolder, Price = 150, Description = "Grants the Hurricane Blast ability effect."},
            new Effect("Grant: Ripple Drive", "ability_ripple_drive") {Category = AbilityFolder, Price = 150, Description = "Grants the Ripple Drive ability effect."},
            new Effect("Grant: Stun Impact", "ability_stun_impact") {Category = AbilityFolder, Price = 150, Description = "Grants the Stun Impact ability effect."},
            new Effect("Grant: Gravity Break", "ability_gravity_break") {Category = AbilityFolder, Price = 150, Description = "Grants the Gravity Break ability effect."},
            new Effect("Grant: Zantetsuken", "ability_zantetsuken") {Category = AbilityFolder, Price = 150, Description = "Grants the Zantetsuken ability effect."},
            new Effect("Grant: Sonic Blade", "ability_sonic_blade") {Category = AbilityFolder, Price = 150, Description = "Grants the Sonic Blade ability effect."},
            new Effect("Grant: Ars Arcanum", "ability_ars_arcanum") {Category = AbilityFolder, Price = 150, Description = "Grants the Ars Arcanum ability effect."},
            new Effect("Grant: Strike Raid", "ability_strike_raid") {Category = AbilityFolder, Price = 150, Description = "Grants the Strike Raid ability effect."},
            new Effect("Grant: Ragnarok", "ability_ragnarok") {Category = AbilityFolder, Price = 150, Description = "Grants the Ragnarok ability effect."},
            new Effect("Grant: Trinity Limit", "ability_trinity_limit") {Category = AbilityFolder, Price = 150, Description = "Grants the Trinity Limit ability effect."},
            new Effect("Grant: MP Haste", "ability_mp_haste") {Category = AbilityFolder, Price = 150, Description = "Grants the MP Haste ability effect."},
            new Effect("Grant: MP Rage", "ability_mp_rage") {Category = AbilityFolder, Price = 150, Description = "Grants the MP Rage ability effect."},
            new Effect("Grant: Second Chance", "ability_second_chance") {Category = AbilityFolder, Price = 150, Description = "Grants the Second Chance ability effect."},
            new Effect("Grant: Berserk", "ability_berserk") {Category = AbilityFolder, Price = 150, Description = "Grants the Berserk ability effect."},
            new Effect("Grant: Leaf Bracer", "ability_leaf_bracer") {Category = AbilityFolder, Price = 150, Description = "Grants the Leaf Bracer ability effect."},
        };
    }
}
