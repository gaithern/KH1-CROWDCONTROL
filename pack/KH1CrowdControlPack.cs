using System;
using System.Collections.Generic;
using ConnectorLib.SimpleTCP;
using CrowdControl.Common;
using CrowdControl.Games.Packs;

namespace CrowdControl.Games.Packs.KH1CrowdControl
{
    public sealed class KH1CrowdControl : SimpleTCPPack<SimpleTCPServerConnector>
    {
        public KH1CrowdControl(UserRecord player, Func<CrowdControlBlock, bool> responseHandler,
            Action<object> statusUpdateHandler) : base(player, responseHandler, statusUpdateHandler)
        {
        }

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

        public override EffectList Effects => new List<Effect>
        {
            new Effect("Play Sound Effect", "sound_31")
                {Category = SoundFolder, Price = 25, Description = "Plays a known-safe KH1 sound effect."},

            new Effect("Play Sound #2", "sound_2") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 2."},
            new Effect("Play Sound #3", "sound_3") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 3."},
            new Effect("Play Sound #4", "sound_4") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 4."},
            new Effect("Play Sound #5", "sound_5") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 5."},
            new Effect("Play Sound #6", "sound_6") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 6."},
            new Effect("Play Sound #7", "sound_7") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 7."},
            new Effect("Play Sound #8", "sound_8") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 8."},
            new Effect("Play Sound #9", "sound_9") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 9."},
            new Effect("Play Sound #10", "sound_10") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 10."},
            new Effect("Play Sound #11", "sound_11") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 11."},
            new Effect("Play Sound #12", "sound_12") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 12."},
            new Effect("Play Sound #13", "sound_13") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 13."},
            new Effect("Play Sound #14", "sound_14") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 14."},
            new Effect("Play Sound #15", "sound_15") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 15."},
            new Effect("Play Sound #16", "sound_16") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 16."},
            new Effect("Play Sound #17", "sound_17") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 17."},
            new Effect("Play Sound #18", "sound_18") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 18."},
            new Effect("Play Sound #19", "sound_19") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 19."},
            new Effect("Play Sound #20", "sound_20") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 20."},
            new Effect("Play Sound #21", "sound_21") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 21."},
            new Effect("Play Sound #22", "sound_22") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 22."},
            new Effect("Play Sound #23", "sound_23") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 23."},
            new Effect("Play Sound #24", "sound_24") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 24."},
            new Effect("Play Sound #25", "sound_25") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 25."},
            new Effect("Play Sound #26", "sound_26") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 26."},
            new Effect("Play Sound #27", "sound_27") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 27."},
            new Effect("Play Sound #28", "sound_28") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 28."},
            new Effect("Play Sound #29", "sound_29") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 29."},
            new Effect("Play Sound #30", "sound_30") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 30."},
            new Effect("Play Sound #32", "sound_32") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 32."},
            new Effect("Play Sound #33", "sound_33") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 33."},
            new Effect("Play Sound #34", "sound_34") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 34."},
            new Effect("Play Sound #35", "sound_35") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 35."},
            new Effect("Play Sound #36", "sound_36") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 36."},
            new Effect("Play Sound #37", "sound_37") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 37."},
            new Effect("Play Sound #38", "sound_38") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 38."},
            new Effect("Play Sound #39", "sound_39") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 39."},
            new Effect("Play Sound #40", "sound_40") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 40."},
            new Effect("Play Sound #41", "sound_41") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 41."},
            new Effect("Play Sound #42", "sound_42") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 42."},
            new Effect("Play Sound #43", "sound_43") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 43."},
            new Effect("Play Sound #44", "sound_44") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 44."},
            new Effect("Play Sound #45", "sound_45") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 45."},
            new Effect("Play Sound #46", "sound_46") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 46."},
            new Effect("Play Sound #47", "sound_47") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 47."},
            new Effect("Play Sound #48", "sound_48") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 48."},
            new Effect("Play Sound #49", "sound_49") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 49."},
            new Effect("Play Sound #50", "sound_50") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 50."},
            new Effect("Play Sound #51", "sound_51") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 51."},
            new Effect("Play Sound #52", "sound_52") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 52."},
            new Effect("Play Sound #53", "sound_53") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 53."},
            new Effect("Play Sound #54", "sound_54") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 54."},
            new Effect("Play Sound #55", "sound_55") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 55."},
            new Effect("Play Sound #56", "sound_56") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 56."},
            new Effect("Play Sound #57", "sound_57") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 57."},
            new Effect("Play Sound #58", "sound_58") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 58."},
            new Effect("Play Sound #59", "sound_59") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 59."},
            new Effect("Play Sound #60", "sound_60") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 60."},
            new Effect("Play Sound #61", "sound_61") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 61."},
            new Effect("Play Sound #62", "sound_62") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 62."},
            new Effect("Play Sound #63", "sound_63") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 63."},
            new Effect("Play Sound #64", "sound_64") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 64."},
            new Effect("Play Sound #65", "sound_65") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 65."},
            new Effect("Play Sound #66", "sound_66") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 66."},
            new Effect("Play Sound #67", "sound_67") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 67."},
            new Effect("Play Sound #68", "sound_68") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 68."},
            new Effect("Play Sound #69", "sound_69") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 69."},
            new Effect("Play Sound #70", "sound_70") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 70."},
            new Effect("Play Sound #71", "sound_71") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 71."},
            new Effect("Play Sound #72", "sound_72") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 72."},
            new Effect("Play Sound #73", "sound_73") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 73."},
            new Effect("Play Sound #74", "sound_74") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 74."},
            new Effect("Play Sound #75", "sound_75") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 75."},
            new Effect("Play Sound #76", "sound_76") {Category = SoundFolder, Price = 25, Description = "Plays KH1 sound effect id 76."},

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

            new Effect("Show Message: GG", "message_gg") {Category = MessageFolder, Price = 50, Description = "Shows \"GG\" in the item-pickup popup."},
            new Effect("Show Message: Nice!", "message_nice") {Category = MessageFolder, Price = 50, Description = "Shows \"Nice!\" in the item-pickup popup."},
            new Effect("Show Message: Oops!", "message_oops") {Category = MessageFolder, Price = 50, Description = "Shows \"Oops!\" in the item-pickup popup."},
            new Effect("Show Message: Uh oh...", "message_uhoh") {Category = MessageFolder, Price = 50, Description = "Shows \"Uh oh...\" in the item-pickup popup."},
            new Effect("Show Message: Nooo!", "message_nooo") {Category = MessageFolder, Price = 50, Description = "Shows \"Nooo!\" in the item-pickup popup."},
            new Effect("Show Message: Yay!", "message_yay") {Category = MessageFolder, Price = 50, Description = "Shows \"Yay!\" in the item-pickup popup."},
            new Effect("Show Message: Hello!", "message_hello") {Category = MessageFolder, Price = 50, Description = "Shows \"Hello!\" in the item-pickup popup."},
            new Effect("Show Message: Whoops!", "message_whoops") {Category = MessageFolder, Price = 50, Description = "Shows \"Whoops!\" in the item-pickup popup."},
            new Effect("Show Message: So true", "message_sotrue") {Category = MessageFolder, Price = 50, Description = "Shows \"So true\" in the item-pickup popup."},
            new Effect("Show Message: Skill issue", "message_skillissue") {Category = MessageFolder, Price = 50, Description = "Shows \"Skill issue\" in the item-pickup popup."},
            new Effect("Show Message: Chaos!", "message_chaos") {Category = MessageFolder, Price = 50, Description = "Shows \"Chaos!\" in the item-pickup popup."},
            new Effect("Show Message: Good luck", "message_goodluck") {Category = MessageFolder, Price = 50, Description = "Shows \"Good luck\" in the item-pickup popup."},
            new Effect("Show Message: Bad luck", "message_badluck") {Category = MessageFolder, Price = 50, Description = "Shows \"Bad luck\" in the item-pickup popup."},
            new Effect("Show Message: Try again", "message_tryagain") {Category = MessageFolder, Price = 50, Description = "Shows \"Try again\" in the item-pickup popup."},
            new Effect("Show Message: W take", "message_wtake") {Category = MessageFolder, Price = 50, Description = "Shows \"W take\" in the item-pickup popup."},
            new Effect("Show Message: L take", "message_ltake") {Category = MessageFolder, Price = 50, Description = "Shows \"L take\" in the item-pickup popup."},

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

            new Effect("Magic Boost", "magic_boost")
                {Category = MagicFolder, Price = 100, Duration = 30, Description = "Temporarily boosts every spell's effectiveness by 50%."},
            new Effect("Magic Nerf", "magic_nerf")
                {Category = MagicFolder, Price = 100, Duration = 30, Description = "Temporarily halves every spell's effectiveness."},

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
