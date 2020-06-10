using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PirateTUO2
{
    /// <summary>
    /// Not all are really constants, some are config variables
    /// 
    /// These get set when we pull data in from the MongoDb
    /// </summary>
    public static class CONSTANTS
    {
        // Settings for API call
        public static string hashCode = "TR&Q$K";
        public static string clientVersion = "80";

        // Tuo xml server
        public static string baseUrl = @"http://mobile-dev.tyrantonline.com/assets/";

        // Folder directories for files
        public static string notePath = "./config/tuo_notes.txt";
        public static string PATH_CARDADDONS = "./config/card-addons/";
        public static string PATH_WHITELIST = "./config/whitelist/";

        // Location of Config files
        public static string logPath = "./config/_debug-log.txt";

        // This is the PTUO version that comes from config/appsettings. If it differs from MainForm.cs version
        public static double LATEST_PTUO_VERSION = 15.75;

        // Active event
        public static string ACTIVE_EVENT = "none";

        // Card sections
        public static int NEWEST_SECTION = 19;
        public static int COMPLETED_SECTIONS = NEWEST_SECTION - 1;

        // Current towers
        public static string WAR_OFFENSE_TOWERS = ""; // oldSettings["defaultWarOffenseTowers"].ToString();
        public static string WAR_DEFENSE_TOWERS = ""; //oldSettings["defaultWarDefenseTowers"].ToString();
        public static string CONQUEST_TOWERS = ""; //oldSettings["defaultCqTowers"].ToString();
        public static string CURRENT_RAID = "";

        public static List<string> GENETIC_FREE_DECKS = new List<string>();
        public static List<string> GENETIC_DOLPHIN_DECKS = new List<string>();
        public static List<string> GENETIC_WHALE_DECKS = new List<string>();


        // Current missions for grind
        public static List<string> SIDE_MISSIONS = new List<string>();
        public static List<string> TEMP_MISSIONS = new List<string>();
        public static List<string> END_MISSIONS = new List<string>();

        // In appsettings, this is for searching for key cards in inventories
        public static List<string> BIGCARDS_MYTHIC = new List<string>();
        public static List<string> BIGCARDS_VINDI = new List<string>();
        public static List<string> BIGCARDS_CHANCE = new List<string>();
        public static List<string> BIGCARDS_REWARD = new List<string>();

        // In appsettings, a list of cards okay to salvage (old rewards, old quads)
        public static List<string> BASE_EPICS = new List<string>();
        public static List<string> BASE_LEGENDS = new List<string>();
        public static List<string> SALVAGE_REWARDS = new List<string>();
        public static List<string> SALVAGE_AGGRESSIVE = new List<string>();

        // When assessing card power, these are overrides to that power that come from appsettings.txt
        public static Dictionary<string, int> CARD_POWER_ADJUSTMENTS = new Dictionary<string, int>();
        public static Dictionary<string, int> CARD_FACTIONPOWER_ADJUSTMENTS = new Dictionary<string, int>();

        // Dominions
        public static List<string> DOMINIONS = new List<string>();
        public static List<string> COMMANDER_QUADS = new List<string>();

        // Dominion upgrade paths (level 1 of the towers leading up to the quad)
        public static Dictionary<int, int[]> DOMINION_UPGRADE_MAP = new Dictionary<int, int[]>()
        {
            // Alpha Type-A
            {50032, new int[] { 50003, 50009, 50027 }}, // Alpha Shielding
            {50038, new int[] { 50003, 50009, 50033 }}, // Alpha Defense-Grid
            {50044, new int[] { 50003, 50009, 50039 }}, // Alpha Hardened

            {50050, new int[] { 50003, 50015, 50045 }}, // Alpha Desecrator 
            {50056, new int[] { 50003, 50015, 50051 }}, // Alpha Retainer
            {50062, new int[] { 50003, 50015, 50057 }}, // Alpha Dominator

            {50068, new int[] { 50003, 50021, 50063 }}, // Alpha Replicant 
            {50074, new int[] { 50003, 50021, 50069 }}, // Alpha Infiltration
            {50180, new int[] { 50003, 50021, 50075 }}, // Alpha Regenerator

            // Alpha Type-B
            {50110, new int[] { 50081, 50087, 50105 }}, // Alpha Ferocity
            {50116, new int[] { 50081, 50087, 50111 }}, // Alpha Terror
            {50122, new int[] { 50081, 50087, 50117 }}, // Alpha Control

            {50128, new int[] { 50081, 50093, 50123 }}, // Alpha Serrated
            {50134, new int[] { 50081, 50093, 50129 }}, // Alpha Disarmer
            {50140, new int[] { 50081, 50093, 50135 }}, // Alpha Siphon

            {50146, new int[] { 50081, 50099, 50141 }}, // Alpha Reciprocator 
            {50152, new int[] { 50081, 50099, 50147 }}, // Alpha Cooperator
            {50158, new int[] { 50081, 50099, 50153 }}, // Alpha Ender

            // Alpha Type-C
            {50188, new int[] { 50159, 50165, 50183 }}, // Alpha Disintegrator
            {50194, new int[] { 50159, 50165, 50189 }}, // Alpha Destroyer
            {50200, new int[] { 50159, 50165, 50195 }}, // Alpha Terminus

            {50206, new int[] { 50159, 50171, 50201 }}, // Alpha Bombard 
            {50212, new int[] { 50159, 50171, 50207 }}, // Alpha Slayer
            {50218, new int[] { 50159, 50171, 50213 }}, // Alpha Scimitar

            {50224, new int[] { 50159, 50177, 50219 }}, // Alpha Suppressor 
            {50230, new int[] { 50159, 50177, 50225 }}, // Alpha Sustainer
            {50236, new int[] { 50159, 50177, 50231 }}, // Alpha Uniter

            // Faction Nexus
            {50251, new int[] { 50240, 50246 }}, // Halcyon's Nexus 
            {50257, new int[] { 50240, 50252 }}, // Octane's Nexus
            {50263, new int[] { 50240, 50258 }}, // Cassius' Nexus

            {50275, new int[] { 50264, 50270 }}, // Barracus' Nexus 
            {50281, new int[] { 50264, 50276 }}, // Yurichs' Nexus
            {50287, new int[] { 50264, 50282 }}, // Silus' Nexus

            {50299, new int[] { 50288, 50294 }}, // Petrisis' Nexus 
            {50305, new int[] { 50288, 50300 }}, // Dracorex's Nexus
            {50311, new int[] { 50288, 50306 }}, // Broodmother's Nexus

            {50323, new int[] { 50312, 50318 }}, // Kleave's Nexus 
            {50329, new int[] { 50312, 50324 }}, // Kylen's Nexus
            {50335, new int[] { 50312, 50330 }}, // Krellus' Nexus

            {50347, new int[] { 50336, 50342 }}, // Empress' Nexus 
            {50353, new int[] { 50336, 50348 }}, // Constantine's Nexus
            {50359, new int[] { 50336, 50354 }}, // Gaia's Nexus

        };

        // card full name : card abbreviation
        public static Dictionary<string, string> CARDABBRS = new Dictionary<string, string>();

        // inGame names to kongName translations
        // -TODO: Add this to appsettings
        public static Dictionary<string, string> INGAME_TO_KONGNAME = new Dictionary<string, string>
        {
            { "Bobogdan", "bobogdan" },
            { "bustafart", "DanielC891" },
            { "C3DT", "0mniscientEye" },
            { "April (Sulf1re)", "flowirin" },
            { "Jester", "AndrewD59" },
            { "Kenpachi Zaraki", "KenpachNoZaraki" },
            { "misterspike", "Mtewes" },
            { "mrakr", "BairsKiller" },
            { "Nova", "Xalfir" },
            { "o1nyx", "o1nyx1" },
            { "Pandalicious!", "xcryox" },
            { "Picci_Kibo", "PicciKibo" },
            { "RecKening", "validname1" },
            { "Rich", "richladysguy" },
            { "Sergeant Bananas", "Clasam" },
            { "StefanW35", "Stefan0w" },
            { "Supreme Aleye", "joe_u22" },
            { "tchorisback", "tchorgnasse" },
            { "The Hoff", "Deathstar622" },
            { "TheJudge", "tjheap" },
            { "Tiny Chipmunk", "player11144818" },
            { "BankHamster", "bnlk789" },
            { "BLAQ ANGEL OF DEATH", "bigdestruxtion" },
            { "celalie", "Celalie" },
            { "CephulonB", "NebulonB" },
            { "Chiefrinsler", "GabrielB593" },
            { "DavidC982", "dizziedave" },
            { "JREmch", "TrickShots" },
            { "Krowl", "XlawlacaustX" },
            { "MR LAVA !!", "sprstoner" },
            { "NebulonPrime", "filipp_06" },
            { "SterlingAwesome", "sterlingawesome" },
            { "Tzeentsh", "Kezaelith" },
            { "TinouGVA", "titiGVA" },
            { "d14bloz2z", "D14bloz2z" },
        };


        // Player pull: Which guilds do we pull from the ogre google sheet, and what two letter tags to assign them
        // ** DEPRECATED
        public static Dictionary<string, string> GUILD_CODES_FOR_OGRESHEET = new Dictionary<string, string>
        {
            {"DireTide", "DT" },
            {"WarHungryFTMFW", "WH" },

            {"TidalWave", "TW" },
            {"TheFallenKnights", "TF" },
            {"SerbianLawyers", "SL" },
            
            {"ForActivePlayers", "ZF" },
            {"Deathandstuff", "ZZ" },
            {"Unguilded", "ZZ" },
            {"MasterJedis", "MJ" },
            {"GravyBairs", "GB" },
            {"SupremeBairs", "SB" },

            // Dead guilds
            {"WarThirstyFTMFW", "WT" },
            {"LadyKillerz", "LK" },
            {"Gay4Mk", "G4" },
            {"OmegaBeasts", "OB" },
            {"LethalHamsters", "ZH" },

        };

        // CQ Zone names. These are static and a new zone hasn't been added since 2016
        public static List<string> CQ_ZONES = new List<string>
        {
            "NOT A ZONE", //Zone 0
            "SPIRE", // 1 - Tier2
            "Norhaven", // 2 - Tier1
            "JOTUN", // 3 - Tier2
            "Tyrolian", // 4 - Tier1
            "Infested", // 5 - Tier1
            "BROOD", // 6 - Tier2
            "SKYCOM", // 7 - Tier2
            "REDMAW", // 8 - Tier2
            "MechGrave", // 9 - Tier1
            "Seismic", // 10 - Tier1
            "PHOBOS", // 11 - Tier 1.5
            "NEXUS", // 12 - Tier 2.5
            "MAGMA", // 13 - Tier2
            "Cleave", // 14 Tier1
            "Malorts", // 15 - Tier1
            "BOREAN", // 16 - Tier2
            "Enclave", // 17 - Tier1
            "ANDAR", // 18 - Tier2
            "Elder", // 19 - Tier1
            "BARON", // 20 - Tier2
            "ASHROCK", // 21 - Tier2
            "Colonial", // 22 - Tier1
        };


        // Logging: Don't record the logins of these players
        public static List<string> DATABASE_LOGINS_TO_IGNORE = new List<string>()
        {
                // officers
                "reck", "c3", "panda",
                "bjorn", "boingo", "caleb", "enoch", "kay", "Kroken", "mbrown", "monkey", "ogre", "Rushmoon", "Lindel", "Lindel2",
                "Xylem",
                // frequent users
                "april", "baxtex", "bloedbak", "cbob", "filip", "floorstone", "cbob333",
            //"cbob"
        };

        // Logging: Don't record the API calls of these players
        public static List<string> DATABASE_API_CALLS_TO_IGNORE = new List<string>()
        {
            // officers
            "reck", "c3", "panda", "ogre", "enoch", "boingo", "bjorn", //"mbrown", "caleb", "monkey", 
            // frequent users
            "bloedbak", "filip", "floorstone"

            // monkey login users = monkey, gravy, davy. TODO create separate ones and more placeholder accounts
        };

        // Non-programmatic seed files
        // -- TODO: Split "seeds:" in appsettings.txt, and pull from there. Have _ControlSetup add the preprogrammed ones and these 
        public static List<string> SEEDS_FROM_CONFIG = new List<string> {
                                "Brawl_Attack", "Brawl_Attack2", "Brawl_Attack3",
                                "Brawl_Defense", "Brawl_Defense2", "Brawl_Defense3",
                                //"Cq_Attack", "Cq_Attack2", "Cq_Defense", "Cq_Defense2",
                                "War_Attack", "War_Attack2", "War_Attack3",
                                "War_Defense", "War_Defense2", "War_Defense3",
                                "Panda_Ordered", "Panda_Random"
                            };

        // Names to exclude from logging
        public static List<string> LOG_NAMES = new List<string>()
        {
            // --- old list ---

            // DT
            "KobraKai3776", "0mniscientEye", "BaiikHailstorm", "BalticDemon", "Bilbon742", "bmmtnz", "BomBeats", "bostpellum", "Clasam", "CrouchTactics", "css_pc13", "DanielC891", "Deathstar622", "dkelson", "gereri", "joe_u22", "LethalHamster", "monkykid95", "Mtewes", "PicciKibo", "player11144818", "Pyrochild1337", "RatSlayer17", "richladysguy", "sprstoner", "tchorgnasse", "techboipede", "Ugaczaka", "validname1", "Xalfir", "XlawlacaustX", "lenakazak", "chrizzoaALBS", "KenpachNoZaraki", "tjheap", "AndrewD59", "cbob333", "Ewuare", "Muki13", "xcryox", "contact2", "Fatal_Turnip", "filipascarnacior", "Floorstone", "flowirin",
            // TW SL
             "alex6676", "andy4808400", "AndyB122", "BairsKiller", "BeneH2", "bigdestruxtion", "bnlk789", "Bloodhawk74", "Bothuwui", "cephyrael", "Celalie", "croaker43", "d34thr0b", "dizziedave", "dizziedc", "Doll_maker", "DyonQ", "filipp_06", "fredy74", "GabrielB593", "Hawaiiu", "Jburli", "JefersonStarship", "JODergy", "kokdivad", "kp_hunter", "likealoon", "marcorulez", "merchantman", "MixReveal", "MrOwl", "Musquatch", "NebulonB", "Prcoje", "ruffrider_matt", "schkdit", "seviech388", "Sinnergy", "SomeOldCrow", "sterlingawesome", "Talamassca", "Tandosil", "TankmanCR1", "TheBadgersNuts", "thecongregat", "Theonlyjoseph", "timepirate8888", "titiGVA", "TrickShots", "Trolljeger", "UltiVolt", "6misterx9", "caddyman", "Capiarex89", "cerosiven", "Cha0sL0rd", "chazzymoto", "Cimmeria", "ColeDaddyG", "cracra74", "Deiero", "Dominans", "DOP3ST93", "Durmal", "EvilOptimusX", "exe_11", "Executioner451", "eXheiro", "gordonlindsay", "h1Pk1T", "helix0815", "IconForHire", "jerryjaap", "Kacktus87", "K33gs", "Leirio", "morris70", "Mplisko", "nickrok", "Nixnutz", "nomercy0071", "Oquesi", "orangejay", "ouigz", "OuttaYaMind", "pandurs", "PhillipC40", "piratniak2", "poikkipatte", "ranrah", "Riversidex", "Shoper99", "Slayer008", "spartakmoscow", "Tankfister", "Thaylen1", "Thefinkel", "treborkhan", "UnfortunateChaos", "vikingreaper", "zuskmj",
            // WH TFK
            "m_brown", "AlienFear", "Axetheon", "Bongos222", "CCilly", "Curium", "D14bloz2z", "Duphrene", "ELECTRlCz", "Elgenitalo", "Harmondo", "JSMMH", "Kezaelith", "Kozakx", "LalaBlue", "LoveMarcus", "PepperApril", "Pluckyy", "Poooiu_", "Prcenzi", "Randomick", "Reiknar", "SonnyBecks", "Tenochteco", "TobyD13", "UWILLFAIL", "XBlaSTedX", "adn291991", "adn2991", "arab186", "bchang19702", "boloog", "bunky_luv_luv", "colliver", "contact69", "droid_75", "erbse3", "fleetcrasher", "frazza777", "gogoayane", "lameboss20", "luckypiggy", "mkroschel", "nolanhancock10", "pinkieisbestpony", "pipoke", "pizli2", "proceed25", "reopened11", "spartan2306", "RenoGO", "Barnabaa", "black9899", "Corny_Syrup", "EnglishLady", "infiniti333", "Kidalavo", "rolleee", "senga7", "SVT_GoD", "Stunnerbear", "thomasheap3", "thomasheap2", "wm746", "00TurboX", "lucasbc92", "skek6", "MaglakO", "MattM325", "Klasher", "BjornD18", "BowlWeevil", "goofygoose", "ElsonL1", "JohnJohn84", "KrokenD", "Son_of_anarchy1", "szsjhs", "hojdarn", "scoiatollo",
            // MJ
            "amenra78", "Menscha", "strateken", "DrunkenBuck", "ObiWanKilla", "cabbyjoe", "braumsnipples", "howardjb", "bangbanger80", "tyrantliker", "acp4", "aidenxu", "amenra78", "badone666", "bahrens", "beerrun77", "blackgoku75200", "boingo", "connor2k", "countach2", "dazdeth", "deathstar61", "defiance93", "diamondsho", "djoachim", "drunkenbuck", "enoch_61", "foldz12", "fookachu", "gger", "kenpachnozaraki", "lenakazak", "lindell1", "lindellou89", "madacus", "melcomc", "monkey_k", "nicolas3470", "obiwankilla", "o1nyx1", "phatkidd13", "prador", "rhomethal", "rogerawong", "ru5hm0on", "shrubsky", "skyvertex", "spawnsyxx9", "strateken", "sullylb21", "thibautw2", "tjheap", "triumph202", "tsorokean", "ultra_instinct21", "vinnyjames",

            // --- new list ---

            // ASY
            "BRIKHOUS", "CanisMajoris5", "DiegoM254", "DonH24", "ElleXey", "Hyanzith", "JamieDean93", "MAKcU", "Mageleader", "MarkB345", "menscha", "Pasaremos", "QElvisQ", "SaintJohan", "SiegL", "Snakebacon", "Stone_Navy", "Trillo12", "Uproar", "Voyager_96", "WaynesWorld182", "Yoshimori", "ZLOAF", "asfgi9", "aznhalf", "bahrens", "blued3vi1", "boundarier", "braumsnipples", "brikhous", "cabbyjoe", "chipelp3", "connor2k", "denixxlx", "dewjryo", "dugj9", "gordi0809", "icerevenge", "iiro97", "liandavid", "masterpeng14", "munchtime", "nmwno", "obiwankilla", "paulojfmota", "sheepoverfence", "shine01", "vexusmoo", "wittwill4", "zyob007",
            // CB
            "BairGrillz", "BairZerka", "BalooTheBair", "ColonelKroc", "CommandoEagle", "EZtArgT", "Grizzlybair8", "Jhwker", "Reallydrunkbair", "Reddew82", "SHredddeeRR", "Ssarai", "The_Altrancer", "UberKick", "XterminAtr8", "Zouga31", "corylp", "eggsellentBACON", "festerling", "keithknicks20", "qwqwqwwqwqwq", "vinegarhusbands", 
            // DB
            "Glinku",
            // MJ
            "acp4", "aidenxu", "aingree1", "amenra78", "badone666", "bangbanger80", "beerrun77", "blackgoku75200", "boingo", "contact69", "countach2", "dazdeth", "deathstar61", "defiance93", "djoachim", "drunkenbuck", "Earske1", "enoch_61", "foldz12", "fookachu", "gger", "halvy42", "howardjb", "kenpachnozaraki", "lindell1", "lindellou89", "madacus", "melcomc", "monkey_k", "nicolas3470", "o1nyx1", "phatkidd13", "prador", "rhomethal", "rogerawong", "ru5hm0on", "shrubsky", "skyvertex", "spawnsyxx9", "strateken", "sullylb21", "thibautw2", "tjheap", "triumph202", "tsorokean", "tyrantliker", "ultra_instinct21", "vinnyjames", "xcryox", 
            //Rebelscum
            "Aggros", "ArchanglofDeath", "CerberuS667", "DerangedCyclops", "Derek13", "FortisTalon", "HarlequinD", "Hennessey", "JFF_Gh0st", "JiriM17", "JoeB256", "JuggaloDbo", "KingCasi", "LilArgus", "OldBud", "PBernandoDeLaPaz", "RIdaad", "Rygard", "SHredddeeRR2", "SamSon666", "SilenceMate", "Stefan0w", "TwinCobra", "TylerrDurrden", "Ultroth", "UndeadHustler", "Yaminia", "appleandeve", "asurapownd", "chuedragon", "drafting3", "grasshopp33r666", "h0geR", "johnoe", "julesbadwinfeld", "mcfizz", "morelp1", "nameless006", "niaq000", "prestigitation", "raininguns", "savior31", "shadows96", "sn00pen", "thatDOUCHEbag", "thewav3", "Ex_Monkey",
            // SD
            "Carionhawk", "0pt1musPr1me", "0utback", "A1rra1d", "Beachc0mber", "bladeisbestpony2", "Bladetheblind", "Br0ads1de", "Carionhawk", "chickenstickers", "Cl1ffjumper", "Cosmicknowledge", "Cydaea", "Defens0r", "Demonmarcus", "Detr1tus", "El1ta0ne", "Fallentitan5", "Greenl1ght", "H01st", "H0tsp0t", "H0und", "ImaDTNHspyDyn", "M00nracer", "MyTUstory", "Nanbr0ke", "Nolantheweeb", "P0wergl1de", "P1pes", "Prowl", "RISKKILLer", "Seaspray", "Sk1ds", "Spankingchicken", "Spr1nger", "Super10n", "Ta1lgate", "ThebigCP", "Tra1lbreaker", "Tryptiflop", "V1bes", "W1ndcharger", "Weakanddishonor", "Wheel1e", "Monologue_B", "Notthebestpony", "paparalfschultze", "Voyager_II", "Whatsajamboozy",
            // SithHappens
            "3Eggs", "Ajr117", "baccie96", "BigSlicks", "BoatmanRambo", "c3_to_fit", "chantcall", "curleycc", "DAB0LTA", "deadlort", "deadlorth", "Deathbycandybar", "DireGraafGhoul", "dwxe86a", "eggochard", "eggsellentos", "eklicious", "El_Fronizio", "El_Fuegos", "El_Quistador", "EonSteamer", "Evandrael", "frogMANofwar", "Garganotos", "garkav", "gigitygig3", "gigitygig5", "glokn7", "GreenMerlin", "hellopeople0516", "hubrismensch77", "JonnyDaRam", "Kaind6", "LeTour", "main_brain", "MathieuC29", "MeowSaUrUs", "NairoQ", "Pick1eRick", "sexybaboo", "ShortDeath", "SHredddeeRR2", "SparkyAce", "Sycopath316", "Toxicwastekills", "trace88", "XenoMotherShip", "zombiehead5", "zombiehead7", 
            // TFK
            "adn291991", "adn2991", "AlienFear", "arab186", "bchang19702", "Bladewraith989", "boloog", "Bongos222", "bunky_luv_luv", "CCilly", "colliver", "Curium", "D14bloz2z", "Demon4747", "droid_75", "Duphrene", "ELECTRlCz", "Elgenitalo", "erbse3", "fleetcrasher", "gogoayane", "Harmondo", "Heinz0900", "JSMMH", "Kezaelith", "Kozakx", "LalaBlue", "lameboss20", "LoveMarcus", "m_brown", "MassimoY", "Menog", "mkroschel", "nolanhancock10", "pinkieisbestpony", "pizli2", "Pluckyy", "Poooiu_", "Prcenzi", "proceed25", "Randomick", "Reiknar", "reopened11", "SonnyBecks", "spartan2306", "Tenochteco", "TobyD13", "UWILLFAIL", "XBlaSTedX", "krilloth",
            // TWC
            "1nf4m0us", "aaaronw", "alexzero14", "CameronW73", "Chr0m1a", "drake1590", "Duphrene02", "F1NALF1A5H", "frazza777", "Gladiator_End", "glory76", "Goldeneye826", "googs1510", "Honninscrave", "jamboozy", "Jan_2004", "JosephM291", "Kati_2001", "King2nd", "King4th", "KlausMartinW", "LePobst", "LordDark666", "luckypiggy", "mainboss", "Manic_Antics", "Marrowgore", "Martini1979", "MatthewL339", "MilkySpray", "MurkyDismal", "Oconzer", "PepperApril", "perish5442", "pipoke", "Rathul", "Rhockar", "rudolfalex", "S1lverb0lt", "Sainteddemon", "TangoSierra", "TGSA", "twistedinsane", "Unloadingdaan", "V0lcan1cus", "VenomBlade999", "waldo_zilli", "WeaponX420", "xf_87", "zstorm6", "King4th",
            // WH
            "00TurboX", "Aristarchus", "Barnabaa", "BjornD18", "Black9899", "BowlWeevil", "Bustafart", "caleborate", "Corny_Syrup", "devilchaos", "ElsonL1", "farsight78", "goofygoose", "hojdarn", "infiniti333", "Iophyle", "JohnJohn84", "Kidalavo", "Klasher", "KrokenD", "lucasbc92", "MuhammadHaiY", "RenoGO", "Rolfpattersson", "scoiatollo", "senga777", "Simeling", "skeksis31", "Son_of_anarchy1", "Stunnerbear", "SVT_GoD", "szsjhs", "TankmanCR1", "thomasheap2", "v79r", "wanorion", "WhaleStorm", "wm746", "XHawKX", "yyjjang",


        };

        // Raid data. This is approximately what the enemy raid has. TODO: Move this to appsettings
        // TODO: Pull this from appsettings?
        // List the raid cards that are possible
        public static List<string> CURRENT_RAID_ASSAULTS = new List<string>();

        // Current Top guilds for gauntlet considerations
        public static List<string> GUILDS_TOP5 = new List<string>()
        {
        };
        public static List<string> GUILDS_TOP10 = new List<string>()
        {
        };
        public static List<string> GUILDS_TOP25 = new List<string>()
        {
        };


        // Commanders to ignore - these may change with major game updates (unlikely)
        public static string[] BannedCommanders = new string[]
        {
            "terrogor", "cyrus", "ascarius", "malika", "maion",
            "yurich", "broodmother", "empress", "vyvander", "broodmother", 
            "tabitha", "halcyon", "lordsilus", "barracus", "typhonvex", "petrisis", "dracorex", "malort", "kylen", "krellus", "alaric", "constantine", "arkadios", "nexor", "daedalus",
            "neocytefusioncore"
        };

        // Base epic / legend names - these are static
        public static string[] BaseEpics = new string[]
        {
            "Absorption Shield", "Aegis", "Tiamat", "Windreaver", "Blackrock", "Bulldozer", "Demon of Embers", "Iron Maiden", "Missile Silo", "Havoc", "Blight Crusher", "Blood Pool", "Draconian Queen", "Smog Tank", "Sinew Feeder", "Xeno Mothership", "Lurker Beast", "Genetics Pit", "Daemon", "Dreadship", "Contaminant Scour", "Equalizer", "Falcion", "Sanctuary", "Vigil"
        };
        public static string[] BaseLegends = new string[]
        {
            "Apex", "Omega", "Benediction", "Nimbus", "Malgoth"
        };

        // Login users who can do special things
        public static string[] GUILD_MOVERS = new string[] { "c3", "reck", "ceph", "ogre", "monkey", "kay", "panda", "enoch", "acp4", "bjorn", "caleb" };


        // Players have card whitelists based on their guilds or if they're in appsettings
        public static List<string> PLAYERS_LEVEL0 = new List<string>() { };
        public static List<string> PLAYERS_LEVEL1 = new List<string>() { };
        public static List<string> PLAYERS_LEVEL2 = new List<string>() { };
        public static List<string> PLAYERS_LEVEL3 = new List<string>() { };

        // Gauntlet dropdowns
        public static List<string> gauntletSearchTerms = new List<string>()
        {
        };


        // Whitelists: These are cards in s ections 1-1X to consider for top5 players

        // whiteListLevel1 is also used for determing what cards to use for power calculations, spam scores, etc
        public static List<string> whitelistLevel1 = new List<string>();
        public static List<string> whitelistLevel2 = new List<string>();
        public static List<string> whitelistLevel3 = new List<string>();

        // Cards in current section to ban        
        public static List<string> reverseWhitelistLevel2 = new List<string>();
        public static List<string> reverseWhitelistLevel3 = new List<string>();

    }
}
