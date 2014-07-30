﻿using System;
using System.IO;
using System.IO.Streams;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading.Tasks;
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
		//public static Dictionary<int, string> prefixes = new Dictionary<int, string>();
		public static RecConfig config { get; set; }
		public static string configDir { get { return Path.Combine(TShock.SavePath, "PluginConfigs"); } }
		public static string configPath { get { return Path.Combine(configDir, "AllRecipes.json"); } }
		public RecipeLog Log { get; set; }

		#region Info
		public override string Name
		{
			get { return "CommandRecipes"; }
		}

		public override string Author
		{
			get { return "aMoka and Enerdy"; }
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

				Log.Dispose();
			}
			base.Dispose(disposing);
		}
		#endregion

		public CmdRec(Main game)
			: base(game)
		{
			Order = -10;

			config = new RecConfig();
			Log = new RecipeLog();
		}

		#region OnInitialize
		void OnInitialize(EventArgs args)
		{
			Commands.ChatCommands.Add(new Command("cmdrec.player.craft", Craft, "craft")
				{
					HelpText = "Allows the player to craft items via command from config-defined recipes."
				});
			Commands.ChatCommands.Add(new Command("cmdrec.admin.reload", RecReload, "recrld")
				{
					HelpText = "Reloads AllRecipes.json"
				});

			//Utils.AddToPrefixes();
			Utils.SetUpConfig();
			Log.Initialize();
		}
		#endregion

		#region OnGreet
		void OnGreet(GreetPlayerEventArgs args)
		{
			RPlayers.Add(new RecPlayer(args.Who));

			var player = TShock.Players[args.Who];
			var RecPlayer = RPlayers.AddToList(new RecPlayer(args.Who));
		}
		#endregion

		#region OnLeave
		void OnLeave(LeaveEventArgs args)
		{
			var player = Utils.GetPlayer(args.Who);

			RPlayers.RemoveAll(pl => pl.Index == args.Who);
		}
		#endregion

		#region OnGetData
		void OnGetData(GetDataEventArgs args)
		{
			if (config.CraftFromInventory)
				return;

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
						if (player.activeRecipe != null && player.Index == args.Msg.whoAmI)
						{
							RecItem fulfilledIngredient = null;
							foreach (RecItem ing in player.activeIngredients)
							{
								//ing.prefix == -1 means accepts any prefix
								if (ing.name == item.name && (ing.prefix == -1 || ing.prefix == prefix))
								{
									ing.stack -= stacks;

									if (ing.stack > 0)
									{
										player.TSPlayer.SendInfoMessage("Drop another {0}.", Utils.FormatItem((Item)ing));
										player.droppedItems.Add(new RecItem(item.name, stacks, prefix));
										args.Handled = true;
										return;
									}
									else if (ing.stack < 0)
									{
										// All messages have periods now.
										player.TSPlayer.SendInfoMessage("Giving back {0}.", Utils.FormatItem((Item)ing));
										player.TSPlayer.GiveItem(item.type, item.name, item.width, item.height, Math.Abs(ing.stack), prefix);
										player.droppedItems.Add(new RecItem(item.name, stacks + ing.stack, prefix));
										fulfilledIngredient = ing;
										args.Handled = true;
									}
									else
									{
										player.TSPlayer.SendInfoMessage("Dropped {0}.", Utils.FormatItem((Item)ing, stacks));
										player.droppedItems.Add(new RecItem(item.name, stacks, prefix));
										fulfilledIngredient = ing;
										args.Handled = true;
									}
								}
							}

							if (fulfilledIngredient == null)
								return;

							player.activeIngredients.Remove(fulfilledIngredient);
							foreach (RecItem ing in player.activeRecipe.ingredients)
							{
								if (ing.name == item.name && ing.prefix == -1)
									ing.prefix = prefix;
							}

							if (player.activeIngredients.Count < 1)
							{
								foreach (RecItem pro in player.activeRecipe.products)
								{
									Item product = new Item();
									product.SetDefaults(pro.name);
									//itm.Prefix(-1) means at least a 25% chance to hit prefix = 0. if < -1, even chances. 
									product.Prefix(pro.prefix);
									pro.prefix = product.prefix;
									player.TSPlayer.GiveItem(product.type, product.name, product.width, product.height, pro.stack, product.prefix);
									player.TSPlayer.SendSuccessMessage("Received {0}.", Utils.FormatItem((Item)pro));
								}
								Log.Recipe(player.activeRecipe.Clone(), player.name);
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
		void Craft(CommandArgs args)
		{
			Item item;
			var player = Utils.GetPlayer(args.Player.Index);
			if (args.Parameters.Count == 0)
			{
				args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /craft <recipe/-quit/-list/-allcats/-cat{0}>",
					(config.CraftFromInventory) ? "/-confirm" : "");
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
							FooterFormat = "Type /craft -list {0} for more.",
							NothingToDisplayString = "There are currently no recipes defined!"
						});
					return;
				case "-allcats":
					int pge;
					if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pge))
						return;

					List<string> allCat = new List<string>();
					foreach (Recipe rec in CmdRec.config.Recipes)
						rec.categories.ForEach(i => { allCat.Add(i); });
					PaginationTools.SendPage(args.Player, 1, PaginationTools.BuildLinesFromTerms(allCat),
						new PaginationTools.Settings
						{
							HeaderFormat = "Recipe Categories ({0}/{1}):",
							FooterFormat = "Type /craft -cat {0} for more.",
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
							rec.categories.ForEach(i =>
							{
								if (cat.ToLower() == i.ToLower())
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
						item = new Item();
						item.SetDefaults(itm.name);
						args.Player.GiveItem(item.type, itm.name, item.width, item.height, itm.stack, itm.prefix);
						player.TSPlayer.SendInfoMessage("Returned {0}.", Utils.FormatItem((Item)itm));
					}
					player.activeRecipe = null;
					player.droppedItems.Clear();
					player.TSPlayer.SendInfoMessage("Successfully quit crafting.");
					return;
				case "-confirm":
					if (!config.CraftFromInventory)
					{
						args.Player.SendErrorMessage("Crafting from inventory is disabled!");
					}
					int count = 0;
					foreach (RecItem ing in player.activeIngredients)
					{
						//go backwards through inventory, because hotbar stuff is generally valuable?
						for (var i = 58; i >= 0; i--)
						{
							item = args.TPlayer.inventory[i];
							if (!args.Player.InventorySlotAvailable)
							{
								args.Player.SendErrorMessage("Insufficient inventory space!");
								player.activeRecipe = null;
								return;
							}

							if (ing.name == item.name && (ing.prefix == -1 || ing.prefix == item.prefix))
							{
								if (args.TPlayer.inventory[i].stack < ing.stack)
								{
									args.Player.SendErrorMessage("Insufficient amount of ingredients!");
									foreach (RecItem itm in player.droppedItems)
									{
										Item m = new Item();
										m.SetDefaults(itm.name);
										args.Player.GiveItem(m.type, m.name, m.width, m.height, itm.stack, itm.prefix);
									}
									player.activeRecipe = null;
									player.droppedItems.Clear();
									return;
								}
								player.droppedItems.Add(new RecItem(ing.name, ing.stack, args.TPlayer.inventory[i].prefix));
								args.TPlayer.inventory[i].stack -= ing.stack;
								ing.stack = 0;
								NetMessage.SendData((int)PacketTypes.PlayerSlot, -1, -1, "", args.Player.Index, i);
								count++;
								foreach (RecItem ingr in player.activeRecipe.ingredients)
								{
									if (ingr.name == item.name && ingr.prefix == -1)
										ingr.prefix = args.TPlayer.inventory[i].prefix;
								}
								break;
							}
						}
					}
					if (count < player.activeRecipe.ingredients.Count)
						return;
					
					foreach (RecItem pro in player.activeRecipe.products)
					{
						Item product = new Item();
						product.SetDefaults(pro.name);
						product.Prefix(pro.prefix);
						pro.prefix = product.prefix;
						player.TSPlayer.GiveItem(product.type, product.name, product.width, product.height, pro.stack, product.prefix);
						player.TSPlayer.SendSuccessMessage("Received {0}.", Utils.FormatItem((Item)pro));
					}
					Log.Recipe(player.activeRecipe.Clone(), player.name);
					player.activeRecipe = null;
					player.droppedItems.Clear();
					player.TSPlayer.SendInfoMessage("Finished crafting.");
					return;
				default:
					if (player.activeRecipe != null)
					{
						args.Player.SendErrorMessage("You must finish crafting or quit your current recipe!");
						return;
					}
					string str = string.Join(" ", args.Parameters);
					if (!recs.Contains(str.ToLower()))
					{
						args.Player.SendErrorMessage("Invalid recipe!");
						return;
					}
					foreach (Recipe rec in config.Recipes)
					{
						if (!rec.permissions.Contains("") && !args.Player.Group.CheckPermissions(rec.permissions))
						{
							args.Player.SendErrorMessage("You do not have the required permission to craft the recipe: {0}!", rec.name);
							return;
						}

						if (!Utils.CheckIfInRegion2(args.Player, rec.regions))
						{
							args.Player.SendErrorMessage("You are not in a valid region to craft the recipe: {0}!", rec.name);
							return;
						}

						if (str.ToLower() == rec.name.ToLower())
						{
							player.activeIngredients = new List<RecItem>(rec.ingredients.Count);
							rec.ingredients.ForEach(i =>
							{
								player.activeIngredients.Add(i.Clone());
							});
							player.activeRecipe = rec.Clone();
							break;
						}
					}
					if (player.activeRecipe != null)
					{
						List<string> inglist = Utils.ListIngredients(player.activeRecipe.ingredients);
						args.Player.SendInfoMessage("The {0} recipe requires {1} to craft. {2}", 
							player.activeRecipe.name,
							String.Join(", ", inglist.ToArray(), 0, inglist.Count - 1) + ", and " + inglist.LastOrDefault(),
							(config.CraftFromInventory) ? "Type \"/craft -confirm\" to craft." : "Please drop all required items.");
					}
					break;
			}
		}
		#endregion

		#region RecConfigReload
		public static void RecReload(CommandArgs args)
		{
			Utils.SetUpConfig();
			args.Player.SendInfoMessage("Attempted to reload the config file");
		}
		#endregion
		#endregion
	}
}
