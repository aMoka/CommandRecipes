using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TShockAPI;

namespace CommandRecipes
{
	public static class Extensions
	{
		public static RecipeData GetRecipeData(this TSPlayer player, bool createIfNotExists = false)
		{
			if (!player.ContainsData(RecipeData.KEY) && createIfNotExists)
			{
				player.SetData(RecipeData.KEY, new RecipeData());
			}
			return player.GetData<RecipeData>(RecipeData.KEY);
		}

		public static void AddToList(this Dictionary<string, List<Recipe>> dic, KeyValuePair<string, Recipe> pair)
		{
			if (dic.ContainsKey(pair.Key))
			{
				dic[pair.Key].Add(pair.Value);
			}
			else
			{
				dic.Add(pair.Key, new List<Recipe>() { pair.Value });
			}
		}

		// The old method didn't work for superadmin, sadly :(
		public static bool CheckPermissions(this TShockAPI.Group group, List<string> perms)
		{
			foreach (var perm in perms)
			{
				if (group.HasPermission(perm))
					return true;
			}
			return false;
		}

		public static Ingredient GetIngredient(this List<Ingredient> lItem, string name, int prefix)
		{
			foreach (var ing in lItem)
				if (ing.name == name && (ing.prefix == prefix || ing.prefix == -1))
					return ing;
			return null;
		}

		public static bool ContainsItem(this List<RecItem> lItem, string name, int prefix)
		{
			foreach (var item in lItem)
				if (item.name == name && (item.prefix == prefix || item.prefix == -1))
					return true;
			return false;
		}

		public static RecItem GetItem(this List<RecItem> lItem, string name, int prefix)
		{
			foreach (var item in lItem)
				if (item.name == name & (item.prefix == prefix || item.prefix == -1))
					return item;
			return null;
		}
	}
}
