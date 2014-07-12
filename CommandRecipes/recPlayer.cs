using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terraria;
using TShockAPI;

namespace CommandRecipes
{
    public class recPlayer
    {
        public int Index;
        public Recipe activeRecipe = null;
        public List<recItem> droppedItems;

        public string name { get { return Main.player[Index].name; } }
        public TSPlayer TSPlayer { get { return TShock.Players[Index]; } }

        public recPlayer(int index)
        {
            Index = index;
        }
    }
}