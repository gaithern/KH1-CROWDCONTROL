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
//
// Still NOT confirmed -- this loads clean, but hasn't been tested against a
// live effect redemption yet (use the SDK's "Test Effects" tab, or an
// actual Crowd Control session, with the KH1 mod running and connected):
//   - The `message` effect's viewer-submitted free text has no confirmed
//     Effect-parameter equivalent in the SDK -- Crowd Control's parameter
//     system is documented for effect variants/targets, not arbitrary text
//     input, so it's left as a plain instant effect below pending
//     verification. If free text isn't supported this way, this effect may
//     need to be dropped or reworked.
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
        private const string MessageFolder = "Message";
        private const string ToggleFolder = "Toggles";
        private const string ComboFolder = "Combos";
        private const string SpeedFolder = "Speed";
        private const string SummonFolder = "Summons";
        private const string MagicFolder = "Magic";
        private const string AbilityFolder = "Abilities";

        // Confirmed against Balatro.cs (compiler CS1715 also said as much):
        // the property type must be EffectList, not List<Effect> -- but
        // EffectList itself is still initialized with a plain
        // `new List<Effect> { ... }` collection initializer (implicit
        // conversion), same as Balatro.cs does.
        public override EffectList Effects { get; } = new List<Effect>
        {
            // The only play_se2 id confirmed live/audible so far -- see
            // kh1_lua_library.lua's play_se2 comment before adding more.
            new Effect("Play Sound Effect", "sound_31")
                {Category = SoundFolder, Price = 25, Description = "Plays a known-safe KH1 sound effect."},

            // TODO(verify): free-text viewer parameter not yet confirmed
            // against the SDK -- see file header note.
            new Effect("Show Message", "message")
                {Category = MessageFolder, Price = 50, Description = "Displays viewer-submitted text on screen for a few seconds."},

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
