using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommandRecipes
{
    public class Utils
    {
        #region GetPlayers
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

        public static recPlayer GetPlayers(int index)
        {
            foreach (recPlayer player in CmdRec.RPlayers)
            {
                if (player.Index == index)
                {
                    return player;
                }
            }
            return null;
        }
        #endregion

        #region SetUpConfig
        public static void SetUpConfig()
        {
            try
            {
                if (!Directory.Exists(CmdRec.configDir))
                    Directory.CreateDirectory(CmdRec.configDir);

                if (File.Exists(CmdRec.configPath))
                    CmdRec.config = RecConfig.Read(CmdRec.configPath);
                else
                    CmdRec.config.Write(CmdRec.configPath);

                foreach (Category cat in CmdRec.config.Categories)
                {
                    if (!CmdRec.cats.Contains(cat.parent))
                        CmdRec.cats.Add(cat.parent.ToLower());
                }
                foreach (Recipe rec in CmdRec.config.Recipes)
                {
                    if (!CmdRec.recs.Contains(rec.name))
                        CmdRec.recs.Add(rec.name.ToLower());
                }
            }
            catch
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error in RecConfig.json!");
                Console.ResetColor();
            }
        }
        #endregion
    }
}
