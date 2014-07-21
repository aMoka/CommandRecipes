using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommandRecipes
{
    public static class Extensions
    {
        public static RecPlayer AddToList(this List<RecPlayer> list, RecPlayer item)
        {
            if (!list.Contains(item))
                list.Add(item);
            return item;
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
    }
}
