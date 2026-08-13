using System;
using System.Collections.Generic;
using ConnectorLib.SimpleTCP;
using CrowdControl.Common;
using CrowdControl.Games.Packs;

namespace CrowdControl.Games.Packs.KH1FM
{
    public sealed class KH1FM : SimpleTCPPack<SimpleTCPServerConnector>
    {
        public KH1FM(UserRecord player, Func<CrowdControlBlock, bool> responseHandler,
            Action<object> statusUpdateHandler) : base(player, responseHandler, statusUpdateHandler)
        {
        }

        public override Game Game { get; } = new Game("KH1FM", "KH1FM",
            "PC", ConnectorType.SimpleTCPServerConnector);

        // Must match CC_HOST / CC_PORT in scripts/kh1_crowdcontrol.lua.
        public override string Host { get; } = "127.0.0.1";
        public override ushort Port { get; } = 43384;

        private const string ItemFolder = "Items";
        private const string MessageFolder = "Message";
        private const string ComboFolder = "Combos";
        private const string DangerFolder = "Danger";
        private const string EnemyFolder = "Enemies";

        public override EffectList Effects => new List<Effect>
        {
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

            new Effect("No Combos", "no_combos")
                {Category = ComboFolder, Price = 100, Duration = 30, Description = "Temporarily drops Sora's ground and air combo limits to 1 (no combo strings)."},

            new Effect("KO Sora", "ko_sora") {Category = DangerFolder, Price = 150, Description = "Instantly triggers Sora's real in-game KO sequence (death animation + Game Over)."},
            new Effect("Heartless Angel", "heartless_angel_sora") {Category = DangerFolder, Price = 90, Description = "Drops Sora to 1 HP, same as Sephiroth's Heartless Angel move."},

            new Effect("Spawn Shadow", "spawn_shadow") {Category = EnemyFolder, Price = 100, Description = "Spawns a Shadow near Sora."},
            new Effect("Spawn Soldier", "spawn_soldier") {Category = EnemyFolder, Price = 100, Description = "Spawns a Soldier near Sora."},
            new Effect("Spawn Powerwild", "spawn_powerwild") {Category = EnemyFolder, Price = 100, Description = "Spawns a Powerwild near Sora."},
            new Effect("Spawn Sea Neon", "spawn_sea_neon") {Category = EnemyFolder, Price = 100, Description = "Spawns a Sea Neon near Sora."},
            new Effect("Spawn Red Nocturne", "spawn_red_nocturne") {Category = EnemyFolder, Price = 100, Description = "Spawns a Red Nocturne near Sora."},
            new Effect("Spawn Blue Rhapsody", "spawn_blue_rhapsody") {Category = EnemyFolder, Price = 100, Description = "Spawns a Blue Rhapsody near Sora."},
            new Effect("Spawn Yellow Opera", "spawn_yellow_opera") {Category = EnemyFolder, Price = 100, Description = "Spawns a Yellow Opera near Sora."},
            new Effect("Spawn Green Requiem", "spawn_green_requiem") {Category = EnemyFolder, Price = 100, Description = "Spawns a Green Requiem near Sora."},
            new Effect("Spawn Air Soldier", "spawn_air_soldier") {Category = EnemyFolder, Price = 100, Description = "Spawns an Air Soldier near Sora."},
            new Effect("Spawn Bouncywild", "spawn_bouncywild") {Category = EnemyFolder, Price = 100, Description = "Spawns a Bouncywild near Sora."},
            new Effect("Spawn Large Body", "spawn_large_body") {Category = EnemyFolder, Price = 100, Description = "Spawns a Large Body near Sora."},
            new Effect("Spawn Fat Bandit", "spawn_fat_bandit") {Category = EnemyFolder, Price = 100, Description = "Spawns a Fat Bandit near Sora."},
            new Effect("Spawn Sheltering Zone", "spawn_sheltering_zone") {Category = EnemyFolder, Price = 100, Description = "Spawns a Sheltering Zone near Sora."},
            new Effect("Spawn Bandit", "spawn_bandit") {Category = EnemyFolder, Price = 100, Description = "Spawns a Bandit near Sora."},
            new Effect("Spawn Pirate", "spawn_pirate") {Category = EnemyFolder, Price = 100, Description = "Spawns a Pirate near Sora."},
            new Effect("Spawn Wight Knight", "spawn_wight_knight") {Category = EnemyFolder, Price = 100, Description = "Spawns a Wight Knight near Sora."},
            new Effect("Spawn Air Pirate", "spawn_air_pirate") {Category = EnemyFolder, Price = 100, Description = "Spawns an Air Pirate near Sora."},
            new Effect("Spawn Gargoyle", "spawn_gargoyle") {Category = EnemyFolder, Price = 100, Description = "Spawns a Gargoyle near Sora."},
            new Effect("Spawn Search Ghost", "spawn_search_ghost") {Category = EnemyFolder, Price = 100, Description = "Spawns a Search Ghost near Sora."},
            new Effect("Spawn Aquatank", "spawn_aquatank") {Category = EnemyFolder, Price = 100, Description = "Spawns an Aquatank near Sora."},
            new Effect("Spawn Screwdiver", "spawn_screwdiver") {Category = EnemyFolder, Price = 100, Description = "Spawns a Screwdiver near Sora."},
            new Effect("Spawn Darkball", "spawn_darkball") {Category = EnemyFolder, Price = 100, Description = "Spawns a Darkball near Sora."},
            new Effect("Spawn Bitsniper", "spawn_bitsniper") {Category = EnemyFolder, Price = 100, Description = "Spawns a Bitsniper near Sora."},
            new Effect("Spawn Wizard", "spawn_wizard") {Category = EnemyFolder, Price = 100, Description = "Spawns a Wizard near Sora."},
            new Effect("Spawn Invisible", "spawn_invisible") {Category = EnemyFolder, Price = 100, Description = "Spawns an Invisible near Sora."},
            new Effect("Spawn Wyvern", "spawn_wyvern") {Category = EnemyFolder, Price = 100, Description = "Spawns a Wyvern near Sora."},
            new Effect("Spawn Angel Star", "spawn_angel_star") {Category = EnemyFolder, Price = 100, Description = "Spawns an Angel Star near Sora."},
            new Effect("Spawn Defender", "spawn_defender") {Category = EnemyFolder, Price = 100, Description = "Spawns a Defender near Sora."},
            new Effect("Spawn Jet Balloon", "spawn_jet_balloon") {Category = EnemyFolder, Price = 100, Description = "Spawns a Jet Balloon near Sora."},
            new Effect("Spawn Stealth Sneak", "spawn_stealth_sneak") {Category = EnemyFolder, Price = 100, Description = "Spawns a Stealth Sneak near Sora."},
            new Effect("Spawn Missile Diver", "spawn_missile_diver") {Category = EnemyFolder, Price = 100, Description = "Spawns a Missile Diver near Sora."},
            new Effect("Spawn Sniperwild", "spawn_sniperwild") {Category = EnemyFolder, Price = 100, Description = "Spawns a Sniperwild near Sora."},
            new Effect("Spawn Pink Agaricus", "spawn_pink_agaricus") {Category = EnemyFolder, Price = 100, Description = "Spawns a Pink Agaricus near Sora."},
            new Effect("Spawn Black Fungi", "spawn_black_fungi") {Category = EnemyFolder, Price = 100, Description = "Spawns a Black Fungi near Sora."},
            // Reuses a creature already loaded in the room, so it needs no free slots and works in
            // busy rooms where the fixed-creature spawns above have to refuse.
            new Effect("Spawn Random (Already Loaded)", "spawn_random_loaded") {Category = EnemyFolder, Price = 100, Description = "Spawns a random creature that this room already has loaded, near Sora."},
        };
    }
}
