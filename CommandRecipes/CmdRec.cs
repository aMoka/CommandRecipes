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
using TShockAPI.DB;

namespace CommandRecipes
{
    [ApiVersion(1, 16)]
    public class CmdRec : TerrariaPlugin
    {
        public static List<string> cats = new List<string>();
        public static List<string> recs = new List<string>();
        public static List<RecPlayer> RPlayers = new List<RecPlayer>();
        public static Dictionary<int, string> prefixes = new Dictionary<int, string>();
        public static RecConfig config { get; set; }
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

            config = new RecConfig();
        }

        #region OnInitialize
        public static void OnInitialize(EventArgs args)
        {
            Commands.ChatCommands.Add(new Command("cmdrec.player.craft", Craft, "craftr")
                {
                    HelpText = "Allows the player to craft items via command from config-defined recipes."
                });
            Commands.ChatCommands.Add(new Command("cmdrec.admin.reload", RecReload, "recrld")
                {
                    HelpText = "Reloads AllRecipes.json"
                });

            Utils.AddToPrefixes();
            Utils.SetUpConfig();
        }
        #endregion

        #region OnGreet
        public static void OnGreet(GreetPlayerEventArgs args)
        {
            RPlayers.Add(new RecPlayer(args.Who));

            var player = TShock.Players[args.Who];
            var RecPlayer = RPlayers.AddToList(new RecPlayer(args.Who));
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

                    foreach (RecPlayer player in RPlayers)
                    {
                        if (player.activeRecipe != null)
                        {
                            RecItem fulfilledIngredient = null;
                            foreach (RecItem ing in player.activeIngredients)
                            {
                                if (ing.name == item.name && (ing.prefix != 0 || ing.prefix == item.prefix))
                                {
                                    ing.stack -= stacks;

                                    if (ing.stack > 0)
                                    {
                                        player.TSPlayer.SendInfoMessage("Drop another {0} {1}{2}(s).",
                                            ing.stack, (ing.prefix != 0) ? prefixes[ing.prefix] + " " : "", ing.name);
                                        player.droppedItems.Add(new RecItem(item.name, stacks, item.prefix));
                                        args.Handled = true;
                                        return;
                                    }
                                    else if (ing.stack < 0)
                                    {
                                        player.TSPlayer.SendInfoMessage("Giving back {0} {1}{2}(s)",
                                            Math.Abs(ing.stack), (ing.prefix != 0) ? prefixes[ing.prefix] + " " : "", ing.name);
                                        player.TSPlayer.GiveItem(item.type, item.name, item.width, item.height, Math.Abs(ing.stack), item.prefix);
                                        player.droppedItems.Add(new RecItem(item.name, stacks + ing.stack, item.prefix));
                                        fulfilledIngredient = ing;
                                        args.Handled = true;
                                    }
                                    else
                                    {
                                        player.TSPlayer.SendInfoMessage("Dropped {0} {1}{2}(s)",
                                            stacks, (ing.prefix != 0) ? prefixes[ing.prefix] + " " : "", ing.name);
                                        player.droppedItems.Add(new RecItem(item.name, stacks, item.prefix));
                                        fulfilledIngredient = ing;
                                        args.Handled = true;
                                    }
                                }
                            }

                            if (fulfilledIngredient == null)
                                return;
                            
                            player.activeIngredients.Remove(fulfilledIngredient);

                            if (player.activeRecipe.ingredients.Count < 1)
                            {
                                foreach (RecItem pro in player.activeRecipe.products)
                                {
                                    Item product = new Item();
                                    product.SetDefaults(pro.name);
                                    player.TSPlayer.GiveItem(product.type, product.name, product.width, product.height, pro.stack, pro.prefix);
                                    player.TSPlayer.SendSuccessMessage("Received {0} {1}{2}(s)",
                                        pro.stack, (pro.prefix != 0) ? prefixes[pro.prefix] + " " : "", product.name);
                                    Console.WriteLine(pro.prefix.ToString());
                                }
                                player.activeRecipe = null;
                                player.droppedItems.Clear();
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
            var player = Utils.GetPlayer(args.Player.Index);
            if (args.Parameters.Count == 0)
            {
                args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /craftr <recipe/-quit/-list/-cat>");
                return;
            }

            var subcmd = args.Parameters[0].ToLower();

            switch (subcmd)
            {
                case "-list":
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
                            NothingToDisplayString = "There are currently no recipes defined!"
                        });
                    return;
                case "-allcats":
                    int pge;
                    if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pge))
                        return;

                    List<string> allCat = new List<string>();
                    foreach (Recipe rec in CmdRec.config.Recipes)
                        rec.categories.ForEach((item) => { allCat.Add(item); });
                    PaginationTools.SendPage(args.Player, 1, PaginationTools.BuildLinesFromTerms(allCat),
                        new PaginationTools.Settings
                        {
                            HeaderFormat = "Recipe Categories ({0}/{1}):",
                            FooterFormat = "Type /craftr -cat {0} for more.",
                            NothingToDisplayString = "There are currently no categories defined!"
                        });
                    return;
                case "-cat":
                    if (args.Parameters.Count < 2)
                    {
                        args.Player.SendErrorMessage("Invalid category!");
                        return;
                    }

                    args.Parameters.RemoveAt(0);
                    string cat = string.Join(" ", args.Parameters);
                    if (!cats.Contains(cat.ToLower()))
                    {
                        args.Player.SendErrorMessage("Invalid category!");
                        return;
                    }
                    else
                    {
                        List<string> catrec = new List<string>();
                        foreach (Recipe rec in config.Recipes)
                        {
                            rec.categories.ForEach((item) =>
                            {
                                if (cat.ToLower() == item.ToLower())
                                    catrec.Add(rec.name);
                            });
                        }
                        args.Player.SendInfoMessage("Recipes in this category:");
                        args.Player.SendInfoMessage("{0}", String.Join(", ", catrec));
                    }
                    return;
                case "-quit":
                    args.Player.SendInfoMessage("Returning dropped items...");
                    foreach (RecItem itm in player.droppedItems)
                    {
                        Item item = new Item();
                        item.SetDefaults(itm.name);
                        args.Player.GiveItem(item.type, itm.name, item.width, item.height, itm.stack, itm.prefix);
                        player.TSPlayer.SendInfoMessage("Returned {0} {1}{2}(s)",
                                                itm.stack, (itm.prefix != 0) ? prefixes[itm.prefix] + " " : "", itm.name);
                    }
                    player.activeRecipe = null;
                    player.droppedItems.Clear();
                    player.TSPlayer.SendInfoMessage("Successfully quit crafting.");
                    return;
                default:
                    string str = string.Join(" ", args.Parameters);
                    if (!recs.Contains(str.ToLower()))
                    {
                        args.Player.SendErrorMessage("Invalid recipe!");
                        return;
                    }
                    else
                    {
                        foreach (Recipe rec in config.Recipes)
                        {
                            if (!rec.permissions.Contains("") && !args.Player.Group.permissions.Intersect(rec.permissions).Any())
                            {
                            args.Player.SendErrorMessage("You do not have the required permission to craft the recipe: {0}!", rec.name);
                                return;
                            }

                            if (!Utils.CheckIfInRegion(args.Player, rec.regions))
                            {
                                args.Player.SendErrorMessage("You are not in a valid region to craft the recipe: {0}!", rec.name);
                                return;
                            }

                            if (str.ToLower() == rec.name.ToLower())
                            {
                                player.activeIngredients = new List<RecItem>(rec.ingredients.Count);
                                rec.ingredients.ForEach((item) =>
                                {
                                    player.activeIngredients.Add(new RecItem(item.name, item.stack, item.prefix));
                                });
                                player.activeRecipe = new Recipe(rec.name, player.activeIngredients, rec.products);
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
                    break;
            }
        }
        #endregion

        #region recConfigReload
        public static void RecReload(CommandArgs args)
        {
            Utils.SetUpConfig();
            args.Player.SendInfoMessage("Attempted to reload the config file");
        }
        #endregion
        #endregion
    }
}
