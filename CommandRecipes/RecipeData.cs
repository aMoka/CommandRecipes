using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terraria;
using TShockAPI;

namespace CommandRecipes
{
	public class RecipeData
	{
		public const string KEY = "CommandRecipes_Data";

		public Recipe activeRecipe;
		public List<Ingredient> activeIngredients;
		public List<RecItem> droppedItems = new List<RecItem>();

		public RecipeData()
		{
			activeIngredients = new List<Ingredient>();
			droppedItems = new List<RecItem>();
		}

		public RecipeData Clone()
		{
			RecipeData newData = new RecipeData();
			newData.activeRecipe = activeRecipe.Clone();
			newData.activeIngredients = new List<Ingredient>(activeIngredients);
			newData.droppedItems = new List<RecItem>(droppedItems);
			return newData;
		}
	}
}