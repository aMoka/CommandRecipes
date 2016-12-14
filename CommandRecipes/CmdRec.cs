using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Streams;
using System.Linq;
using System.Reflection;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace CommandRecipes
{
	[ApiVersion(2, 0)]
	public class CmdRec : TerrariaPlugin
	{
		public static List<string> cats = new List<string>();
		public static List<string> recs = new List<string>();
		public static RecipeDataManager Memory { get; private set; }
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
			get { return "aMoka & Enerdy"; }
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
			PlayerHooks.PlayerPostLogin += OnLogin;
			PlayerHooks.PlayerLogout += OnLogout;
			ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
			ServerApi.Hooks.NetGetData.Register(this, OnGetData);
		}
		#endregion

		#region Dispose
		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				PlayerHooks.PlayerPostLogin -= OnLogin;
				PlayerHooks.PlayerLogout -= OnLogout;
				ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
				ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);

				Log.Dispose();
			}
		}
		#endregion

		public CmdRec(Main game)
			: base(game)
		{
			// Why did we need a lower order again?
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

			Memory = new RecipeDataManager();
			//Utils.AddToPrefixes();
			Utils.SetUpConfig();
			Log.Initialize();
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

					if (id != 400)
						return;

					TSPlayer tsplayer = TShock.Players[args.Msg.whoAmI];
					RecipeData recData;
					if (tsplayer != null && tsplayer.Active && (recData = tsplayer.GetRecipeData()) != null && recData.activeRecipe != null)
					{
						List<Ingredient> fulfilledIngredient = new List<Ingredient>();
						foreach (Ingredient ing in recData.activeIngredients)
						{
							//ing.prefix == -1 means accepts any prefix
							if (ing.name == item.name && (ing.prefix == -1 || ing.prefix == prefix))
							{
								ing.stack -= stacks;

								if (ing.stack > 0)
								{
									tsplayer.SendInfoMessage("Drop another {0}.", Utils.FormatItem((Item)ing));
									if (recData.droppedItems.Exists(i => i.name == ing.name))
										recData.droppedItems.Find(i => i.name == ing.name).stack += stacks;
									else
										recData.droppedItems.Add(new RecItem(item.name, stacks, prefix));
									args.Handled = true;
									return;
								}
								else if (ing.stack < 0)
								{
									tsplayer.SendInfoMessage("Giving back {0}.", Utils.FormatItem((Item)ing));
									tsplayer.GiveItem(item.type, item.name, item.width, item.height, Math.Abs(ing.stack), prefix);
									if (recData.droppedItems.Exists(i => i.name == ing.name))
										recData.droppedItems.Find(i => i.name == ing.name).stack += (stacks + ing.stack);
									else
										recData.droppedItems.Add(new RecItem(item.name, stacks + ing.stack, prefix));
									foreach (Ingredient ingr in recData.activeIngredients)
										if ((ingr.group == 0 && ingr.name == ing.name) || (ingr.group != 0 && ingr.group == ing.group))
											fulfilledIngredient.Add(ingr);
									args.Handled = true;
								}
								else
								{
									tsplayer.SendInfoMessage("Dropped {0}.", Utils.FormatItem((Item)ing, stacks));
									if (recData.droppedItems.Exists(i => i.name == ing.name))
										recData.droppedItems.Find(i => i.name == ing.name).stack += stacks;
									else
										recData.droppedItems.Add(new RecItem(item.name, stacks, prefix));
									foreach (Ingredient ingr in recData.activeIngredients)
										if ((ingr.group == 0 && ingr.name == ing.name) || (ingr.group != 0 && ingr.group == ing.group))
											fulfilledIngredient.Add(ingr);
									args.Handled = true;
								}
							}
						}

						if (fulfilledIngredient.Count < 1)
							return;

						recData.activeIngredients.RemoveAll(i => fulfilledIngredient.Contains(i));

						foreach (Ingredient ing in recData.activeRecipe.ingredients)
						{
							if (ing.name == item.name && ing.prefix == -1)
								ing.prefix = prefix;
						}

						if (recData.activeIngredients.Count < 1)
						{
							List<Product> lDetPros = Utils.DetermineProducts(recData.activeRecipe.products);
							foreach (Product pro in lDetPros)
							{
								Item product = new Item();
								product.SetDefaults(pro.name);
								//itm.Prefix(-1) means at least a 25% chance to hit prefix = 0. if < -1, even chances. 
								product.Prefix(pro.prefix);
								pro.prefix = product.prefix;
								tsplayer.GiveItem(product.type, product.name, product.width, product.height, pro.stack, product.prefix);
								tsplayer.SendSuccessMessage("Received {0}.", Utils.FormatItem((Item)pro));
							}
							List<RecItem> prods = new List<RecItem>();
							lDetPros.ForEach(i => prods.Add(new RecItem(i.name, i.stack, i.prefix)));
							Log.Recipe(new LogRecipe(recData.activeRecipe.name, recData.droppedItems, prods), tsplayer.Name);
							// Commands :o (NullReferenceException-free :l)
							recData.activeRecipe.Clone().ExecuteCommands(tsplayer);
							recData.activeRecipe = null;
							recData.droppedItems.Clear();
							tsplayer.SendInfoMessage("Finished crafting.");
						}
					}
				}
			}
		}
		#endregion

		void OnLogin(PlayerPostLoginEventArgs args)
		{
			// Note to self: During login, TSPlayer.Active is set to False
			if (args.Player == null)
				return;

			if (Memory.Contains(args.Player.Name))
				args.Player.SetData(RecipeData.KEY, Memory.Load(args.Player.Name));
		}

		void OnLogout(PlayerLogoutEventArgs args)
		{
			if (args.Player == null || !args.Player.Active)
				return;

			RecipeData data = args.Player.GetRecipeData();
			if (data != null && data.activeRecipe != null)
				Memory.Save(args.Player);
		}

		#region Commands
		#region Craft
		void Craft(CommandArgs args)
		{
			Item item;
			var recData = args.Player.GetRecipeData(true);
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

					// Add any recipe that isn't invisible kappa
					foreach (Recipe rec in CmdRec.config.Recipes.FindAll(r => !r.invisible))
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

					// Another ditto from -list
					foreach (Recipe rec in CmdRec.config.Recipes.FindAll(r => !r.invisible))
						rec.categories.ForEach(i =>
						{
							if (!allCat.Contains(i))
								allCat.Add(i);
						});
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

						// Keep bringing them!
						foreach (Recipe rec in config.Recipes.FindAll(r => !r.invisible))
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
					if (recData.activeRecipe == null)
					{
						args.Player.SendErrorMessage("You aren't crafting anything!");
					}
					else
					{
						args.Player.SendInfoMessage("Returning dropped items...");
						foreach (RecItem itm in recData.droppedItems)
						{
							item = new Item();
							item.SetDefaults(itm.name);
							args.Player.GiveItem(item.type, itm.name, item.width, item.height, itm.stack, itm.prefix);
							args.Player.SendInfoMessage("Returned {0}.", Utils.FormatItem((Item)itm));
						}
						recData.activeRecipe = null;
						recData.droppedItems.Clear();
						args.Player.SendInfoMessage("Successfully quit crafting.");
					}
					return;
				case "-confirm":
					if (!config.CraftFromInventory)
					{
						args.Player.SendErrorMessage("Crafting from inventory is disabled!");
					}
					int count = 0;
					Dictionary<int, bool> finishedGroup = new Dictionary<int, bool>();
					Dictionary<int, int> slots = new Dictionary<int, int>();
					int ingredientCount = recData.activeIngredients.Count;
					foreach (Ingredient ing in recData.activeIngredients)
					{
						if (!finishedGroup.ContainsKey(ing.group))
						{
							finishedGroup.Add(ing.group, false);
						}
						else if (ing.group != 0)
							ingredientCount--;
					}
					foreach (Ingredient ing in recData.activeIngredients)
					{
						if (ing.group == 0 || !finishedGroup[ing.group])
						{
							Dictionary<int, RecItem> ingSlots = new Dictionary<int, RecItem>();
							for (int i = 58; i >= 0; i--)
							{
								item = args.TPlayer.inventory[i];
								if (ing.name == item.name && (ing.prefix == -1 || ing.prefix == item.prefix))
								{
									ingSlots.Add(i, new RecItem(item.name, item.stack, item.prefix));
								}
							}
							if (ingSlots.Count == 0)
								continue;

							int totalStack = 0;
							foreach (var key in ingSlots.Keys)
								totalStack += ingSlots[key].stack;

							if (totalStack >= ing.stack)
							{
								foreach (var key in ingSlots.Keys)
									slots.Add(key, (ingSlots[key].stack < ing.stack) ? args.TPlayer.inventory[key].stack : ing.stack);
								if (ing.group != 0)
									finishedGroup[ing.group] = true;
								count++;
							}
						}
					}
					if (count < ingredientCount)
					{
						args.Player.SendErrorMessage("Insufficient ingredients!");
						return;
					}
					if (!args.Player.InventorySlotAvailable)
					{
						args.Player.SendErrorMessage("Insufficient inventory space!");
						return;
					}
					foreach (var slot in slots)
					{
						item = args.TPlayer.inventory[slot.Key];
						var ing = recData.activeIngredients.GetIngredient(item.name, item.prefix);
						if (ing.stack > 0)
						{
							int stack;
							if (ing.stack < slot.Value)
								stack = ing.stack;
							else
								stack = slot.Value;

							item.stack -= stack;
							ing.stack -= stack;
							NetMessage.SendData((int)PacketTypes.PlayerSlot, -1, -1, "", args.Player.Index, slot.Key);
							if (!recData.droppedItems.ContainsItem(item.name, item.prefix))
								recData.droppedItems.Add(new RecItem(item.name, stack, item.prefix));
							else
								recData.droppedItems.GetItem(item.name, item.prefix).stack += slot.Value;
						}
					}
					List<Product> lDetPros = Utils.DetermineProducts(recData.activeRecipe.products);
					foreach (Product pro in lDetPros)
					{
						Item product = new Item();
						product.SetDefaults(pro.name);
						product.Prefix(pro.prefix);
						pro.prefix = product.prefix;
						args.Player.GiveItem(product.type, product.name, product.width, product.height, pro.stack, product.prefix);
						args.Player.SendSuccessMessage("Received {0}.", Utils.FormatItem((Item)pro));
					}
					List<RecItem> prods = new List<RecItem>();
					lDetPros.ForEach(i => prods.Add(new RecItem(i.name, i.stack, i.prefix)));
					Log.Recipe(new LogRecipe(recData.activeRecipe.name, recData.droppedItems, prods), args.Player.Name);
					recData.activeRecipe.Clone().ExecuteCommands(args.Player);
					recData.activeRecipe = null;
					recData.droppedItems.Clear();
					args.Player.SendInfoMessage("Finished crafting.");
					return;
				default:
					if (recData.activeRecipe != null)
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
						if (str.ToLower() == rec.name.ToLower())
						{
							if (!rec.permissions.Contains("") && !args.Player.Group.CheckPermissions(rec.permissions))
							{
								args.Player.SendErrorMessage("You do not have the required permission to craft the recipe: {0}!", rec.name);
								return;
							}
							if (!Utils.CheckIfInRegion(args.Player, rec.regions))
							{
								args.Player.SendErrorMessage("You are not in a valid region to craft the recipe: {0}!", rec.name);
								return;
							}
							recData.activeIngredients = new List<Ingredient>(rec.ingredients.Count);
							rec.ingredients.ForEach(i =>
							{
								recData.activeIngredients.Add(i.Clone());
							});
							recData.activeRecipe = rec.Clone();
							break;
						}
					}
					if (recData.activeRecipe != null)
					{
						List<string> inglist = Utils.ListIngredients(recData.activeRecipe.ingredients);
						if (!args.Silent)
						{
							args.Player.SendInfoMessage("The {0} recipe requires {1} to craft. {2}",
							  recData.activeRecipe.name,
							  (inglist.Count > 1) ? String.Join(", ", inglist.ToArray(), 0, inglist.Count - 1) + ", and " + inglist.LastOrDefault() : inglist[0],
							  (config.CraftFromInventory) ? "Type \"/craft -confirm\" to craft." : "Please drop all required items.");
						}
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
