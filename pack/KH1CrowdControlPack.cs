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
        };
    }
}
