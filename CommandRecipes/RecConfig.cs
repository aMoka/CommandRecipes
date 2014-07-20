using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.Serialization;
using TShockAPI.DB;
using Newtonsoft.Json;

namespace CommandRecipes
{
    public class RecItem
    {
        public string name;
        public int stack;
        public int prefix;

        public RecItem(string name, int stack, int prefix = 0)
        {
            this.name = name;
            this.stack = stack;
            this.prefix = prefix;
        }
    }

    public class Recipe
    {
        public string name;
        public List<RecItem> ingredients;
        public List<RecItem> products;
        public List<string> categories = new List<string>();
        public List<string> permissions = new List<string>();
        public List<string> regions = new List<string>();

        public Recipe(string name, List<RecItem> ingredients, List<RecItem> products, List<string> categories = null, List<string> permissions = null, List<string> regions = null)
        {
            this.name = name;
            this.ingredients = ingredients;
            this.products = products;
            this.categories = categories;
            this.permissions = permissions;
            this.regions = regions;
        }
    }

    public class RecConfig
    {
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
                Recipes = new List<Recipe>();
                Recipes.Add(new Recipe("Copper Broadsword",
                    new List<RecItem>() { new RecItem("Copper Bar", 8), new RecItem("Stone Block", 20), new RecItem("Wooden Hammer", 1) },
                    new List<RecItem>() { new RecItem("Copper Broadsword", 1, 41), new RecItem("Wooden Hammer", 1, 39) },
                    new List<string> { "Example" }, new List<string> { "" }, new List<string> { "" }));
                Recipes.Add(new Recipe("Iron Broadsword",
                    new List<RecItem>() { new RecItem("Iron Bar", 8), new RecItem("Stone Block", 20), new RecItem("Wooden Hammer", 1) },
                    new List<RecItem>() { new RecItem("Iron Broadsword", 1, 41), new RecItem("Wooden Hammer", 1, 39) },
                    new List<string> { "Example", "Example2" }, new List<string> { "cmdrec.craft.example", "craft" }, new List<string> { "" }));
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
