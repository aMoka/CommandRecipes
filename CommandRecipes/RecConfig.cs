using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace CommandRecipes
{
    public class Ingredient
    {
        public int itemid;
        public int amount;
        public int prefix;

        public Ingredient(int itemid, int amount)
        {
            this.itemid = itemid;
            this.amount = amount;
        }
    }

    public class Product
    {
        public int itemid;
        public int amount;
        public int prefix;

        public Product(int itemid, int amount, int prefix = 0)
        {
            this.itemid = itemid;
            this.amount = amount;
            this.prefix = prefix;
        }
    }

    public class Category
    {
        public string parent;
        public List<string> options;

        public Category(string parent, List<string> options)
        {
            this.parent = parent;
            this.options = options;
        }
    }

    public class Recipe
    {
        public string name;
        public List<Ingredient> ingredients;
        public List<Product> products;

        public Recipe(string name, List<Ingredient> ingredients, List<Product> products)
        {
            this.name = name;
            this.ingredients = ingredients;
            this.products = products;
        }
    }

    public class RecConfig
    {
        public List<Category> Categories;
        public List<Recipe> Recipes;

        public static RecConfig Read(string path)
        {
            if (!File.Exists(path))
                return new RecConfig();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return Read(fs);
            }
        }

        public static RecConfig Read(Stream stream)
        {
            using (var sr = new StreamReader(stream))
            {
                var cf = JsonConvert.DeserializeObject<RecConfig>(sr.ReadToEnd());
                if (ConfigRead != null)
                    ConfigRead(cf);
                return cf;
            }
        }

        public void Write(string path)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Write))
            {
                Write(fs);
            }
        }

        public void Write(Stream stream)
        {
            {
                Categories = new List<Category>();
                Categories.Add(new Category("Main", 
                    new List<string>() { "Weapons", "Armor", "Consumables" }));
                Categories.Add(new Category("Weapons", 
                    new List<string>() { "Copper Broadsword", "Iron Broadsword" }));
                Categories.Add(new Category("Armor", 
                    new List<string>() { "Copper Armor", "Iron Armor" }));
                Categories.Add(new Category("Copper Armor", 
                    new List<string>() { "Copper Helmet" }));
                Categories.Add(new Category("Consumables", 
                    new List<string>() { "Lesser Healing Potion" }));

                Recipes = new List<Recipe>();
                Recipes.Add(new Recipe("Copper Broadsword", 
                    new List<Ingredient>() { new Ingredient(20, 8), new Ingredient(7, 1) }, 
                    new List<Product>() { new Product(-14, 1, 41), new Product(7, 1, 40) }));
                Recipes.Add(new Recipe("Iron Broadsword",
                    new List<Ingredient>() { new Ingredient(22, 8), new Ingredient(7, 1) },
                    new List<Product>() { new Product(4, 1, 41), new Product(7, 1, 40) }));
                Recipes.Add(new Recipe("Copper Helmet",
                    new List<Ingredient>() { new Ingredient(20 , 15), new Ingredient(7, 1) },
                    new List<Product>() { new Product(89, 1), new Product(7, 1, 40) }));
                Recipes.Add(new Recipe("Lesser Healing Potion",
                    new List<Ingredient>() { new Ingredient(261, 1), new Ingredient(23, 2), new Ingredient(31, 2) },
                    new List<Product>() { new Product(28, 2) }));
            }

            var str = JsonConvert.SerializeObject(this, Formatting.Indented);
            using (var sw = new StreamWriter(stream))
            {
                sw.Write(str);
            }
        }

        public static Action<RecConfig> ConfigRead;
    }
}
