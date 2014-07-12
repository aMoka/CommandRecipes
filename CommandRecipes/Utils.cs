using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TShockAPI;

namespace CommandRecipes
{
    public class Utils
    {
        public static List<recPlayer> GetPlayerList(string name)
        {
            foreach (recPlayer player in CmdRec.RPlayers)
            {
                if (player.name.ToLower().Contains(name.ToLower()))
                {
                    return new List<recPlayer>() { player };
                }
            }
            return new List<recPlayer>();
        }

        public static recPlayer GetPlayer(int index)
        {
            foreach (recPlayer player in CmdRec.RPlayers)
                if (player.Index == index)
                    return player;

            return null;
        }

        public static List<string> ListIngredients(List<recItem> actIngs)
        {
            List<string> lActIngs = new List<string>();
            foreach (recItem item in actIngs)
            {
                lActIngs.Add(String.Concat(item.stack.ToString(), " ",
                    (item.prefix != 0) ? TShock.Utils.GetPrefixById(item.prefix) + " ": "", item.name, "(s)"));
            }
            return lActIngs;
        }

        #region SetUpConfig
        public static void SetUpConfig()
        {
            try
            {
                if (!Directory.Exists(CmdRec.configDir))
                    Directory.CreateDirectory(CmdRec.configDir);

                if (File.Exists(CmdRec.configPath))
                    CmdRec.config = recConfig.Read(CmdRec.configPath);
                else
                    CmdRec.config.Write(CmdRec.configPath);

                foreach (Recipe rec in CmdRec.config.Recipes)
                {
                    if (!CmdRec.cats.Contains(rec.name.ToLower()))
                        CmdRec.cats.Add(rec.name.ToLower());
                }
            }
            catch
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error in recConfig.json!");
                Console.ResetColor();
            }
        }
        #endregion
    }
}
