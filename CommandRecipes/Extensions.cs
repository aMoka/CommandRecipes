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
    }
}
