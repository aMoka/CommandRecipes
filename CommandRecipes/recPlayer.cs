using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terraria;
using TShockAPI;

namespace CommandRecipes
{
	public class RecPlayer
	{
		public int Index;
		public Recipe activeRecipe;
		public List<RecItem> activeIngredients;
		public List<RecItem> droppedItems = new List<RecItem>();

		public string name { get { return Main.player[Index].name; } }
		public TSPlayer TSPlayer { get { return TShock.Players[Index]; } }

		public RecPlayer(int index)
		{
			Index = index;
		}
	}
}