using System;
using System.IO;
using System.IO.Streams;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace CommandRecipes
{
    [ApiVersion(1, 16)]
    public class CmdRec : TerrariaPlugin
    {
        public static List<recPlayer> RPlayers = new List<recPlayer>();
        public static List<string> cats = new List<string>();
        public static List<string> recs = new List<string>();
        public static RecConfig config { get; set; }
        public static string configDir { get { return Path.Combine(TShock.SavePath, "PluginConfigs"); } }
        public static string configPath { get { return Path.Combine(configDir, "RecConfig.json"); } }

        #region Info
        public override string Name
        {
            get { return "CommandRecipes"; }
        }

        public override string Author
        {
            get { return "aMoka"; }
        }

        public override string Description
        {
            get { return "Recipes through commands and chat."; }
        }

        public override Version Version
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }
        #endregion

        #region Initialize
        public override void Initialize()
        {
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.ServerChat.Register(this, OnChat);
            ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreet);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);
        }
        #endregion

        #region Dispose
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                ServerApi.Hooks.ServerChat.Deregister(this, OnChat);
                ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnGreet);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
            }
            base.Dispose(disposing);
        }
        #endregion

        public CmdRec(Main game)
            : base(game)
        {
            Order = -10;

            config = new RecConfig();
        }

        #region OnInitialize
        public static void OnInitialize(EventArgs args)
        {
            Commands.ChatCommands.Add(new Command("cmdrec", Craft, "rcraft"));
            Commands.ChatCommands.Add(new Command("cmdrec.admin.reload", recReload, "recrld"));

            Utils.SetUpConfig();
        }
        #endregion

        #region OnGreet
        public static void OnGreet(GreetPlayerEventArgs args)
        {
            RPlayers.Add(new recPlayer(args.Who));

            var player = TShock.Players[args.Who];
            var recPlayer = Utils.GetPlayers(args.Who);
        }
        #endregion

        #region OnLeave
        private void OnLeave(LeaveEventArgs args)
        {
            var player = Utils.GetPlayers(args.Who);

            RPlayers.RemoveAll(pl => pl.Index == args.Who);
        }
        #endregion

        #region OnChat
        public void OnChat(ServerChatEventArgs args)
        {
            recPlayer player = Utils.GetPlayers(args.Who);

            if (player.isCrafting)
            {
                if (args.Text.ToLower() == "/rcraft")
                {
                    player.menuTrack.Add("main");
                    player.TSPlayer.SendInfoMessage("Choose a category/recipe by typing it.");
                    player.activeMenu = player.menuTrack[player.menuTrack.Count - 1];
                }
                else if (player.menuTrack.Count > 0 && cats.Contains(player.activeMenu)
                    && !player.menuTrack.Contains(args.Text.ToLower()))
                {
                    player.menuTrack.Add(args.Text.ToLower());
                    player.activeMenu = player.menuTrack[player.menuTrack.Count - 1];
                }
                else if (args.Text.ToLower() == "back" && player.menuTrack.Count > 1)
                {
                    player.menuTrack.RemoveAt(player.menuTrack.Count - 1);
                    player.activeIngs.Clear();
                    player.activePros.Clear();
                    player.activeRec = false;
                    player.activeMenu = player.menuTrack[player.menuTrack.Count - 1];
                }
                else
                {
                    player.TSPlayer.SendErrorMessage("Invalid choice! Type in a valid option or '/rcraft' to exit the crafting menu.");
                }

                if (cats.Contains(player.activeMenu))
                {
                    foreach (Category cat in config.Categories)
                    {
                        if (cat.parent.ToLower() == player.activeMenu)
                        {
                            for (int i = 0; i < cat.options.Count; i++)
                            {
                                player.TSPlayer.SendMessage(cat.options[i], Color.White);
                            }
                        }
                    }
                }
                else if (recs.Contains(player.activeMenu))
                {
                    foreach (Recipe rec in config.Recipes)
                    {
                        if (rec.name.ToLower() == player.activeMenu)
                        {
                            List<string> recIngs = new List<string>();
                            List<string> recPros = new List<string>();
                            foreach (Ingredient ing in rec.ingredients)
                            {
                                string iName = TShock.Utils.GetItemById(ing.itemid).name;
                                string iAmt = ing.amount.ToString();
                                string iPrefix = TShock.Utils.GetPrefixById(ing.itemid).ToString();

                                recPros.Add((iPrefix == "0") ? string.Format("{0} {1} {2}(s)", iAmt, iPrefix, iName) :
                                    string.Format("{0} {1}(s)", iAmt, iName));

                                foreach (Ingredient actIng in player.activeIngs)
                                {
                                    if (actIng.itemid == ing.itemid)
                                    {
                                        actIng.amount += ing.amount;
                                    }
                                    else
                                        player.activeIngs.Add(new Ingredient(ing.itemid, ing.amount));
                                }
                                foreach (Product pro in rec.products)
                                {
                                    string pName = TShock.Utils.GetItemById(pro.itemid).name;
                                    string pAmt = pro.amount.ToString();
                                    string pPrefix = TShock.Utils.GetPrefixById(pro.prefix).ToString();

                                    recPros.Add((pPrefix == "0") ? string.Format("{0} {1} {2}(s)", pAmt, pPrefix, pName) :
                                        string.Format("{0} {1}(s)", pAmt, pName));
                                    player.activePros.Add(new Product(pro.itemid, pro.amount, pro.prefix));
                                }
                            }
                            player.TSPlayer.SendInfoMessage("{0} requires {1}", rec.name, string.Join(", ", recIngs));
                            player.TSPlayer.SendInfoMessage("to create {0}", string.Join(", ", recPros));
                            player.TSPlayer.SendInfoMessage("Drop the materials, type /rcraft to stop crafting, or back to go back.");
                            player.activeRec = true;
                        }
                    }
                    args.Handled = true;
                    return;
                }
            }
        }
        #endregion

        #region OnGetData
        public static void OnGetData(GetDataEventArgs args)
        {
            if (args.MsgID == PacketTypes.ItemDrop)
            {
                if (args.Handled)
                    return;

                using (var data = new MemoryStream(args.Msg.readBuffer, args.Index, args.Length))
                {
                    Int16 id = data.ReadInt16();
                    float posx = data.ReadSingle();
                    float posy = data.ReadSingle();
                    float velx = data.ReadSingle();
                    float vely = data.ReadSingle();
                    Int16 stacks = data.ReadInt16();
                    int prefix = data.ReadByte();
                    bool nodelay = data.ReadBoolean();
                    Int16 netid = data.ReadInt16();

                    Item item = new Item();
                    item.SetDefaults(netid);

                    foreach (recPlayer player in RPlayers)
                    {
                        if (player.isCrafting)
                        {
                            foreach (Ingredient actIng in player.activeIngs)
                            {
                                if (actIng.itemid == netid)
                                {
                                    actIng.amount -= stacks;

                                    if (actIng.amount > 0)
                                    {
                                        player.TSPlayer.SendInfoMessage("Drop another {0} {1}(s).", player.activeIngs[netid], item.name);
                                        args.Handled = true;
                                        return;
                                    }
                                    else if (actIng.amount < 0)
                                    {
                                        player.TSPlayer.SendInfoMessage("Giving back {0} {1}(s)", Math.Abs(actIng.amount), item.name);
                                        args.Handled = true;
                                        player.TSPlayer.GiveItem(item.type, item.name, item.width, item.height, Math.Abs(actIng.amount));
                                        player.activeIngs.Remove(actIng);
                                    }
                                    else
                                    {
                                        player.TSPlayer.SendInfoMessage("Dropped {0} {1}(s)", stacks, item.name);
                                        player.activeIngs.Remove(actIng);
                                        args.Handled = true;
                                    }

                                    if (player.activeIngs.Count < 1 && player.activeRec)
                                    {
                                        foreach (Product pro in player.activePros)
                                        {
                                            Item pItem = new Item();
                                            pItem.SetDefaults(pro.itemid);
                                            player.TSPlayer.GiveItem(pItem.type, pItem.name, pItem.width, pItem.height, pro.amount, pro.prefix);
                                            player.TSPlayer.SendInfoMessage("Received {0} {1} {2}(s)", pro.amount, TShock.Utils.GetPrefixById(pro.prefix), pItem.name);
                                        }
                                        player.activeRec = false;
                                        player.isCrafting = false;
                                        player.TSPlayer.SendInfoMessage("You have exited the crafting menu.");
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        #endregion

        #region Commands
        #region Craft
        public static void Craft(CommandArgs args)
        {
            var player = Utils.GetPlayers(args.Player.Index);
            player.isCrafting = !player.isCrafting;
            player.activeRec = false;
            player.activeIngs.Clear();
            player.activePros.Clear();
            player.menuTrack.Clear();
            player.TSPlayer.SendInfoMessage(string.Format("You have {0} the crafting menu.", (player.isCrafting) ? "entered" : "exited"));
        }
        #endregion

        #region RecConfigReload
        public static void recReload(CommandArgs args)
        {
            Utils.SetUpConfig();
            args.Player.SendInfoMessage("Attempted to reload the config file");
        }
        #endregion
        #endregion
    }
}
