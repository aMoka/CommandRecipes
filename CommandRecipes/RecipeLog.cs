using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace CommandRecipes
{
	public class RecipeLog
	{
		public RecipeLog()
		{

		}

		private static string path = Path.Combine(CmdRec.configDir, "CraftLog.txt");
		private FileStream _ostream;
		private FileStream _istream;
		public StreamReader Reader { get; protected set; }
		public StreamWriter Writer { get; protected set; }
		public Dictionary<string, List<Recipe>> CompletedRecipes { get; protected set; }

		#region Initialize
		public void Initialize()
		{
			_ostream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
			_istream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite);

			Writer = new StreamWriter(_ostream);
			Reader = new StreamReader(_istream);
			//Task.Factory.StartNew(() => Load());
			//Load();
			CompletedRecipes = new Dictionary<string, List<Recipe>>();
		}
		#endregion

		#region Dispose
		public void Dispose()
		{
			Writer.Close();
			Reader.Close();
		}
		#endregion

		#region Load
		/// <summary>
		/// Reloads the CompletedRecipes property
		/// </summary>
		public void Load()
		{
			try
			{
				CompletedRecipes = LoadRecipes();
			}
			catch (Exception ex)
			{
				Log.ConsoleError(ex.ToString());
			}
		}
		#endregion

		#region LogRecipe
		/// <summary>
		/// Logs a crafted recipe to the log file.
		/// </summary>
		public void Recipe(Recipe recipe, string player)
		{
			try
			{
				var list = new List<string>();
				recipe.ingredients.ForEach(i => list.Add(Utils.LogFormatItem((Item)i, i.stack)));
				var ingredients = String.Join(",", list);
				list.Clear();
				recipe.products.ForEach(i => list.Add(Utils.LogFormatItem((Item)i, i.stack)));
				var products = String.Join(",", list);
				var str = String.Format("Player ({0}) crafted recipe ({1}), using ({2}) and obtaining ({3}).",
					player,
					recipe.name,
					ingredients,
					products);
				Writer.WriteLine(str);
				Writer.Flush();
				CompletedRecipes.AddToList(new KeyValuePair<string, Recipe>(player, recipe));

			}
			catch (Exception ex)
			{
				Log.ConsoleError(ex.ToString());
			}
		}
		#endregion

		#region LoadRecipes
		/// <summary>
		/// Returns the list of crafted recipes directly from the log.
		/// </summary>
		public Dictionary<string, List<Recipe>> LoadRecipes()
		{
			var dic = new Dictionary<string, List<Recipe>>();
			KeyValuePair<string, Recipe> pair;
			while (!Reader.EndOfStream)
			{
				pair = ParseLine(Reader.ReadLine());
				if (!dic.ContainsKey(pair.Key))
				{
					dic.Add(pair.Key, new List<Recipe>());
				}
				dic[pair.Key].Add(pair.Value);
			}
			Reader.DiscardBufferedData();
			return dic;
		}
		#endregion

		#region GetRecipes
		/// <summary>
		/// Returns a list of Recipes crafted by player name
		/// </summary>
		public List<Recipe> GetRecipes(string player)
		{
			return CompletedRecipes[player];
		}
		#endregion

		#region Helper Methods
		#region ParseToString
		List<string> FormatItems(List<RecItem> items)
		{
			var list = new List<string>();
			string prefix;
			foreach (var item in items)
			{
				prefix = item.prefix > 0 ? String.Format("[{0}] ",
					Utils.GetPrefixById(item.prefix)) : "";
				list.Add(String.Format("{0} {1}{2}",
					item.stack,
					prefix,
					item.name));
			}
			return list;
		}
		#endregion
		#region ParseItems
		List<RecItem> ParseItems(string items)
		{
			var list = new List<RecItem>();
			string name = String.Empty;
			string stack = String.Empty;
			string prefix = String.Empty;
			ReadMode pos = ReadMode.Stack;
			foreach (char ch in items)
			{
				switch (ch)
				{
					case ',':
						name = name.Replace("(s)", String.Empty);
						list.Add(new RecItem(
							name.Trim(),
							Int32.Parse(stack),
							prefix == "" ? 0 : TShock.Utils.GetPrefixByName(prefix.Trim()).First()));
						break;
					case '[':
						pos = ReadMode.Prefix;
						break;
					case ']':
						pos = ReadMode.Name;
						break;
					default:
						if (Char.IsWhiteSpace(ch) && pos != ReadMode.Name)
						{
							pos = ReadMode.Name;
							break;
						}
						switch (pos)
						{
							case ReadMode.Stack:
								stack += ch;
								break;
							case ReadMode.Prefix:
								prefix += ch;
								break;
							case ReadMode.Name:
								name += ch;
								break;
						}
						break;
				}
			}
			// Additional one for the last item
			name = name.Replace("(s)", String.Empty);
			list.Add(new RecItem(
				name.Trim(),
				Int32.Parse(stack.Trim()),
				prefix == "" ? 0 : TShock.Utils.GetPrefixByName(prefix.Trim()).First()));
			return list;
		}
		#endregion
		#region ParseLine
		KeyValuePair<string, Recipe> ParseLine(string line)
		{
			bool reading = false;
			int pos = 0;
			var reader = new[]
				{
					String.Empty,
					String.Empty,
					String.Empty,
					String.Empty
				};
			foreach (char ch in line)
			{
				switch (ch)
				{
					case '(':
						reading = true;
						break;
					case ')':
						reading = false;
						pos++;
						break;
					default:
						if (reading)
						{
							reader[pos] += ch;
						}
						break;
				}
			}

			return new KeyValuePair<string, Recipe>(
				reader[0],
				new Recipe(reader[1], ParseItems(reader[2]), ParseItems(reader[3])));
		}
		#endregion
		#endregion

		enum ReadMode
		{
			Stack,
			Prefix,
			Name
		}
	}
}
