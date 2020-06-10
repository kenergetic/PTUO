using PirateTUO2.Modules;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PirateTUO2.Classes
{
    /// <summary>
    /// Read values from a config file
    /// </summary>
    public class CONFIG
    {
        // Database login info
        public static string userName = "";
        public static string password = "";
        public static string role = "";
        public static bool LoggedIn = false;

        // Kong info - now info is being stored in __api.txt
        //public static string kongId = "";
        //public static string kongName = "";
        //public static string kongToken = "";
        //public static string apiPassword = ""; // in api, "password", but we are doing some lazy string parsing in the login file, and don't want to confuse this with "password"
        //public static string syncode = "";
        //public static string userId = "";

        
        // When assigning power to a card (like Rally all 50 = +1 power), use this dictionary to reference it
        public static Dictionary<string, int> CardSkillPower = new Dictionary<string, int>();

        // Special officer strings
        public static string DeckSnifferUrl = "";

        // With users that have trouble logging in via firewall
        public static bool overrideNormalLogin = false;

        // Toggle minimize to tray
        public static bool minimizeToTray = true;
        // Toggle stay on top
        public static bool stayOnTop = false;

        // TODO: Pull players from database instead of csvs
        public static List<string> playerCsvURLs = new List<string>(); //URLs to the csv
        public static List<string> playerCsvs = new List<string>(); // The csv itself

        // Admin level access name-values
        public static Dictionary<string, string> AdminSettings = new Dictionary<string, string>();

        // StaticSeeds are used to store manual seeds for players if they exist. 
        // The seeds are stored in config/seeds.txt
        // File Format = Player : SeedType : Deck <- for static seeds
        public static List<Tuple<string, string, string>> PlayerConfigSeeds = new List<Tuple<string, string, string>>();
    }
}
