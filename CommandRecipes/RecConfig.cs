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
    public class recItem
    {
        public string name;
        public int stack;
        public int prefix;

        public recItem(string name, int stack, int prefix = 0)
        {
            this.name = name;
            this.stack = stack;
            this.prefix = prefix;
        }
    }

    public class Recipe
    {
        public string name;
        public List<recItem> ingredients;
        public List<recItem> products;

        public Recipe(string name, List<recItem> ingredients, List<recItem> products)
        {
            this.name = name;
            this.ingredients = ingredients;
            this.products = products;
        }
    }

    public class recConfig
    {
        public List<Recipe> Recipes;

        public static recConfig Read(string path)
        {
            if (!File.Exists(path))
                return new recConfig();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return Read(fs);
            }
        }

        public static recConfig Read(Stream stream)
        {
            using (var sr = new StreamReader(stream))
            {
                var cf = JsonConvert.DeserializeObject<recConfig>(sr.ReadToEnd());
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
                Recipes = new List<Recipe>();
                Recipes.Add(new Recipe("Copper Broadsword",
                    new List<recItem>() { new recItem("Copper Bar", 8), new recItem("Stone Block", 20), new recItem("Wooden Hammer", 1) },
                    new List<recItem>() { new recItem("Copper Broadsword", 1, 41), new recItem("Wooden Hammer", 1, 39) }));
                Recipes.Add(new Recipe("Iron Broadsword",
                    new List<recItem>() { new recItem("Iron Bar", 8), new recItem("Stone Block", 20), new recItem("Wooden Hammer", 1) },
                    new List<recItem>() { new recItem("Iron Broadsword", 1, 41), new recItem("Wooden Hammer", 1, 39) }));
            }

            var str = JsonConvert.SerializeObject(this, Formatting.Indented);
            using (var sw = new StreamWriter(stream))
            {
                sw.Write(str);
            }
        }

        public static Action<recConfig> ConfigRead;
    }
}
