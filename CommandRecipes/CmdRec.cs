using System;
using System.IO;
using System.IO.Streams;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;
using System.ComponentModel;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace CommandRecipes
{
    [ApiVersion(1, 16)]
    public class CmdRec : TerrariaPlugin
    {
        public static List<string> cats = new List<string>();
        public static List<recPlayer> RPlayers = new List<recPlayer>();
        public static recConfig config { get; set; }
        public static string configDir { get { return Path.Combine(TShock.SavePath, "PluginConfigs"); } }
        public static string configPath { get { return Path.Combine(configDir, "AllRecipes.json"); } }

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

            config = new recConfig();
        }

        #region OnInitialize
        public static void OnInitialize(EventArgs args)
        {
            Commands.ChatCommands.Add(new Command("cmdrec.player.craft", Craft, "craftr")
                {
                    HelpText = "Allows the player to craft items via command from config-defined recipes."
                });
            Commands.ChatCommands.Add(new Command("cmdrec.admin.reload", recReload, "recrld")
                {
                    HelpText = "Reloads AllRecipes.json"
                });

            Utils.SetUpConfig();
        }
        #endregion

        #region OnGreet
        public static void OnGreet(GreetPlayerEventArgs args)
        {
            RPlayers.Add(new recPlayer(args.Who));

            var player = TShock.Players[args.Who];
            var recPlayer = Utils.GetPlayer(args.Who);
        }
        #endregion

        #region OnLeave
        private void OnLeave(LeaveEventArgs args)
        {
            var player = Utils.GetPlayer(args.Who);

            RPlayers.RemoveAll(pl => pl.Index == args.Who);
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

                    if (id == 0)
                        return;

                    foreach (recPlayer player in RPlayers)
                    {
                        if (player.activeRecipe != null)
                        {
                            recItem fulfilledIngredient = null;
                            foreach (recItem ing in player.activeRecipe.ingredients)
                            {
                                if (ing.name == item.name && (ing.prefix != 0 || ing.prefix == item.prefix))
                                {
                                    ing.stack -= stacks;

                                    if (ing.stack > 0)
                                    {
                                        player.TSPlayer.SendInfoMessage("Drop another {0} {1}{2}(s).", 
                                            ing.stack, (ing.prefix != 0) ? TShock.Utils.GetPrefixById(ing.prefix) + " " : "", ing.name);
                                        args.Handled = true;
                                        return;
                                    }
                                    else if (ing.stack < 0)
                                    {
                                        player.TSPlayer.SendInfoMessage("Giving back {0} {1}{2}(s)", 
                                            Math.Abs(ing.stack), (ing.prefix != 0) ? TShock.Utils.GetPrefixById(ing.prefix) + " " : "", ing.name);
                                        args.Handled = true;
                                        player.TSPlayer.GiveItem(item.type, item.name, item.width, item.height, Math.Abs(ing.stack), item.prefix);
                                        fulfilledIngredient = ing;
                                    }
                                    else
                                    {
                                        player.TSPlayer.SendInfoMessage("Dropped {0} {1}{2}(s)",
                                            stacks, (ing.prefix != 0) ? TShock.Utils.GetPrefixById(ing.prefix) + " " : "", ing.name);
                                        args.Handled = true;
                                        fulfilledIngredient = ing;
                                    }
                                }
                            }
                            if (fulfilledIngredient == null)
                                return;
                            
                            player.activeRecipe.ingredients.Remove(fulfilledIngredient);

                            if (player.activeRecipe.ingredients.Count < 1)
                            {
                                foreach (recItem pro in player.activeRecipe.products)
                                {
                                    Item product = new Item();
                                    product.SetDefaults(pro.name);
                                    player.TSPlayer.GiveItem(product.type, product.name, product.width, product.height, pro.stack, pro.prefix);
                                    player.TSPlayer.SendSuccessMessage("Received {0} {1}{2}(s)",
                                        pro.stack, (pro.prefix != 0) ? TShock.Utils.GetPrefixById(pro.prefix) + " " : "", product.name);
                                }
                                player.TSPlayer.SendInfoMessage("Finished crafting.");
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
            args.Player.SendInfoMessage(TShock.Utils.GetPrefixById(39));
            var player = Utils.GetPlayer(args.Player.Index);
            if (args.Parameters.Count == 0)
            {
                args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /craftr <recipe/-quit/-list>");
                return;
            }
            if (args.Parameters[0] == "-list")
            {
                int page;
                if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out page))
                    return;

                List<string> allRec = new List<string>();
                foreach (Recipe rec in CmdRec.config.Recipes)
                    allRec.Add(rec.name);
                PaginationTools.SendPage(args.Player, page, PaginationTools.BuildLinesFromTerms(allRec),
                    new PaginationTools.Settings
                    {
                        HeaderFormat = "Recipes ({0}/{1}):",
                        FooterFormat = "Type /craftr -list {0} for more.",
                        NothingToDisplayString = "There are currently no recipes defined."
                    });
                return;

            }
            if (args.Parameters[0] == "-quit")
            {
                //return everything in player.droppedItems then clear

                return;
            }

            string str = string.Join(" ", args.Parameters);
            if (!cats.Contains(str.ToLower()))
            {
                args.Player.SendErrorMessage("Invalid recipe!");
            }
            else
            {
                foreach (Recipe rec in config.Recipes)
                {
                    if (str.ToLower() == rec.name.ToLower())
                    {
                        player.activeRecipe = new Recipe(rec.name.ToString(), new List<recItem>(rec.ingredients), new List<recItem>(rec.products));
                        break;
                    }
                }
                if (player.activeRecipe != null)
                {
                    List<string> inglist = Utils.ListIngredients(player.activeRecipe.ingredients);
                    args.Player.SendInfoMessage("The {0} recipe requires {1} to craft. Please drop all required items.", player.activeRecipe.name,
                        String.Join(", ", inglist.ToArray(), 0, inglist.Count - 1) + ", and " + inglist.LastOrDefault());
                }
            }
        }
        #endregion

        #region recConfigReload
        public static void recReload(CommandArgs args)
        {
            Utils.SetUpConfig();
            args.Player.SendInfoMessage("Attempted to reload the config file");
        }
        #endregion
        #endregion
    }
}
