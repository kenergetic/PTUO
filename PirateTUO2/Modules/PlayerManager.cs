using PirateTUO2.Classes;
using PirateTUO2.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace PirateTUO2.Modules
{
    /// <summary>
    /// This module updates player inventories in data/cards
    /// 
    /// DEPRECATED - puller player inventories no longer comes from a Google sheet 'database'
    ///            - We can instead pull directly from the API
    /// </summary>
    public static class PlayerManager
    {
        // Temp until placed in database
        public static List<string> csvPaths = new List<string>();

        public static List<Player> Players = new List<Player>();

        public static HashSet<string> blacklistLevel1 = new HashSet<string>();
        public static HashSet<string> blacklistLevel2 = new HashSet<string>();
        public static HashSet<string> blacklistLevel3 = new HashSet<string>();

        static NameValueCollection appSettings = ConfigurationManager.AppSettings;
        static int newestCardSection = Int32.Parse(appSettings["newestCardSection"].ToString());

        // This string tracks errors when loading pTUO
        static string status = "";

        /// <summary>
        /// Master method
        /// 
        /// Pull data from a CSV file into Players
        /// </summary>
        public static string ImportPlayerCards(MainForm form, bool skip=false)
        {
            try
            {
                if (skip) return "Pull player cards skipped";

                // Clean out player files
                if (!CONFIG.overrideNormalLogin)
                {
                    if (Directory.Exists("data/cards/")) 
                        status += FileIO.DeleteFromDirectory("data/cards/");
                }

                // Clear player database
                Players.Clear();

                // Create a list of cards to "blacklist" from player inventories
                BuildCardBlackLists();

                // Create player-card inventories
                BuildPlayers();

                // Create player-card inventories
                BuildTestAccounts();

                // Write player inventories to text files
                WritePlayerInventories(form);

                if (status == "") status = "Success";                
            }
            catch(Exception ex)
            {
                status += "\r\nImportPlayerCards() failed: " + ex.Message;                
            }

            return status;
        }

        /// <summary>
        /// Given a player and seed, return a deck
        /// </summary>
        public static bool SetPlayerDeckFromSeed(MainForm form, string player, string seedName, out string resultDeck, string cardPath="cards")
        {
            resultDeck = "";

            try
            {
                var playerFile = FileIO.SimpleRead(form, "data/" + cardPath + "/" + player, returnCommentedLines: true);

                foreach(var line in playerFile)
                {
                    if (line.Contains(seedName))
                    {
                        var deck = line.Split(new char[] { ':' }, 2);
                        if (deck.Length > 1 && !String.IsNullOrEmpty(deck[1]))
                        {                                
                            resultDeck = deck[1];
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
            }
            catch (FileNotFoundException ex)
            {
                ControlExtensions.InvokeEx(form.outputTextBox, x => form.outputTextBox.AppendText("SetPlayerDeckFromSeed() error: " + ex.Message));
                return false;
            }
            catch (Exception ex)
            {
                ControlExtensions.InvokeEx(form.outputTextBox, x => form.outputTextBox.AppendText("SetPlayerDeckFromSeed() error: " + ex.Message));
                return false;
            }

            return false;
        }


        /// <summary>
        /// Build files for extra potential cards in config/card-addons/...
        /// 
        /// TODO: Move out of PlayerManager
        /// </summary>
        public static string BuildCardAddonFiles(MainForm form)
        {        
            var currentFileName = "";
            var addonContent = new StringBuilder();
            var addonFileDictionary = new Dictionary<string, string>();

            var metaFiles = new List<string>
                {
                    "_boxes.txt",
                    "_commanders.txt",
                    "_fusions.txt",
                    "_singles.txt"
                };


            try
            {
                // Delete .txt files in config/cardaddons
                var directoryInfo = new DirectoryInfo(CONSTANTS.PATH_CARDADDONS);
                foreach (var file in directoryInfo.GetFiles())
                {
                    //Ignore the _files that start with underscore - these files are the cerator files
                    if (file.Name.StartsWith("_")) continue;

                    File.SetAttributes(file.FullName, FileAttributes.Normal);
                    file.Delete();
                }
            }
            catch (Exception ex)
            {
                return "\r\nERROR: BuildCardAddonFiles() failed - could not delete files: " + ex.Message;
            }

            // For each base card file (boxes, commanders, etc) - split those files into subfiles separated by // lines
            //
            // Ex: boxes.txt with 
            // // Box-12-Santaslayer
            // Card1..
            // Card2..
            // // Box-12-Gingerbread
            // Card1..
            // 
            // would be config/card-addons/Box12-Santaslayer.txt and config/card-addons/Box12.gingerbread.txt
            // For each meta file (_boxes, _commanders, etc)
            foreach (var metaFile in metaFiles)
            {
                try
                {
                    // Get the data in this meta file
                    List<string> addonFile = FileIO.SimpleRead(form, CONSTANTS.PATH_CARDADDONS + metaFile, returnCommentedLines:true);

                    // Process each line in the addon file
                    foreach (var data in addonFile)
                    {
                        // SKIP: Blank lines, lines commented out and start with dashes
                        if (data.Trim() == "" || data.StartsWith("// --") || data.StartsWith("//--")) continue;


                        // Line: This line denotes to make a new file 
                        // (ex: // Box-08-Anniversary)
                        if (data.StartsWith("//"))
                        {
                            // Save previous chunk of data
                            if (!addonFileDictionary.ContainsKey(currentFileName) && currentFileName.Length > 0)
                            {
                                addonFileDictionary.Add(currentFileName, addonContent.ToString());
                                addonContent.Clear();
                            }
                            currentFileName = data.Replace("//", "").Trim();
                        }
                        // Line: This line denotes an item to 'add' to the current file
                        // (ex: // Box-08-Anniversary)
                        else
                        {
                            addonContent.AppendLine(data);
                        }
                    }

                    // Get the last file
                    addonFileDictionary.Add(currentFileName, addonContent.ToString());
                    addonContent.Clear();

                }
                catch (Exception ex)
                {
                    return "\r\nERROR: BuildCardAddonFiles() failed - Making box files: " + ex.Message;
                }
            }


            try
            {
                int filesCreated = 0;

                foreach (var file in addonFileDictionary)
                {
                    // Members: Skip adding old boxes (Start with zBox) to the whitelist
                    // This is for less moving parts rather then specific access
                    if (CONFIG.role != "level3" && file.Key.Contains("zBox")) continue;

                    FileIO.SimpleWrite(form, CONSTANTS.PATH_CARDADDONS, file.Key + ".txt", file.Value);

                    filesCreated++;
                }
            }
            catch(Exception ex)
            {
                return "\r\nERROR: BuildCardAddonFiles() failed - Writing box files: " + ex.Message;
            }

            return "Success";
        }



        #region ImportPlayerCards Methods


        /// <summary>
        /// Create a list of cards to "blacklist" from player inventories
        /// </summary>
        private static void BuildCardBlackLists()
        {
            try
            {
                var time = new Stopwatch();
                time.Start();

                // Banlist
                foreach (var cardKvp in CardManager.GetCardTable())
                {
                    Card card = cardKvp.Value;
                    KeyValuePair<string,int> formattedCard = CardManager.FormatCard(card.Name, removeLevels: true);


                    // Ignore pve/summon cards
                    if (card.Rarity <= 2 || card.Set == (int)CardSet.GameOnly || card.Set == (int)CardSet.Summon || card.Set == (int)CardSet.Unknown || card.Set == (int)CardSet.Dominion) continue;
                    
                    // Ban basic commanders, except Daedalus
                    var bannedCommanders = CONSTANTS.BannedCommanders;

                    foreach (var bannedCommander in bannedCommanders)
                    {
                        blacklistLevel1.Add(bannedCommander);
                        blacklistLevel2.Add(bannedCommander);
                        blacklistLevel3.Add(bannedCommander);
                    }

                    // -- Now does all card sections -- 

                    // Finished card sections: (e.g. cards_section1-15.xml)
                    // Ban all cards unless that card is on the "levelX.txt" file
                    //if (card.Section <= CONSTANTS.COMPLETED_SECTIONS)
                    //{
                        if (!CONSTANTS.whitelistLevel1.Contains(card.Name))
                        {
                            blacklistLevel1.Add(formattedCard.Key);
                        }

                        if (!CONSTANTS.whitelistLevel2.Contains(card.Name))
                        {
                            blacklistLevel2.Add(formattedCard.Key);
                        }

                        if (!CONSTANTS.whitelistLevel3.Contains(card.Name))
                        {
                            blacklistLevel3.Add(formattedCard.Key);
                        }
                    //}

                    // Most recent card section: Only ban cards in the levelX_reverse list
                    // This is because new cards keep coming in and we don't want to keep up with the list
                    //else
                    //{
                    //    // Blacklist non-quads. Allow dual mythics/vinds
                    //    if (card.Fusion_Level < (int)FusionLevel.Quad && card.Rarity < (int)Rarity.Vindicator)
                    //    {
                    //        if (!CONSTANTS.whitelistLevel1.Contains(card.Name))
                    //        {
                    //            blacklistLevel1.Add(formattedCard.Key);
                    //        }

                    //        if (!CONSTANTS.whitelistLevel2.Contains(card.Name))
                    //        {
                    //            blacklistLevel2.Add(formattedCard.Key);
                    //        }

                    //        if (!CONSTANTS.whitelistLevel3.Contains(card.Name))
                    //        {
                    //            blacklistLevel3.Add(formattedCard.Key);
                    //        }
                    //    }
                    //    // Blacklist cards in reverseWhitelists
                    //    else
                    //    {
                    //        if (CONSTANTS.reverseWhitelistLevel2.Contains(card.Name))
                    //        {
                    //            blacklistLevel2.Add(formattedCard.Key);
                    //        }
                    //        if (CONSTANTS.reverseWhitelistLevel3.Contains(card.Name))
                    //        {
                    //            blacklistLevel3.Add(formattedCard.Key);
                    //        }
                    //    }
                    //}
                }

                //// Debug / Send to crossbones
                //FileIO.SimpleWrite("config/debug", "banlistsoft.txt", <string of softban list>)

                time.Stop();
                Console.WriteLine(MethodInfo.GetCurrentMethod() + " " + time.ElapsedMilliseconds + "ms ");
            }
            catch(Exception ex)
            {
                Console.WriteLine("Blacklist failed: " + ex);
                status += "\r\nImportPlayerCards(): Build blacklist failed: " + ex.Message;
            }
        }

        /// <summary>
        /// Create players from the xbones "database"
        /// </summary>
        private static void BuildPlayers()
        {
            var time = new Stopwatch();
            time.Start();

            try
            {
                // Default dominion if one isn't found
                var defaultDominions = new List<Card>();

                defaultDominions.Add(CardManager.GetById(50038)); // Alpha shielding
                defaultDominions.Add(CardManager.GetById(50299)); // Petrisis' Nexus

                // Players are blacklisted by guild, except in these cases
                CONSTANTS.PLAYERS_LEVEL0 = FileIO.ReadFromAppSettings("LEVEL0")[0];
                CONSTANTS.PLAYERS_LEVEL1 = FileIO.ReadFromAppSettings("LEVEL1")[0];
                CONSTANTS.PLAYERS_LEVEL2 = FileIO.ReadFromAppSettings("LEVEL2")[0];
                CONSTANTS.PLAYERS_LEVEL3 = FileIO.ReadFromAppSettings("LEVEL3")[0];

                //foreach (var playerCsv in CONFIG.playerCsvs)
                //{
                Parallel.ForEach(CONFIG.playerCsvs,
                    new ParallelOptions { MaxDegreeOfParallelism = 5 },
                    playerCsv =>
                    {
                        var playerLines = playerCsv.Split('\n');

                        // For each player (Each line is a player)
                        foreach (string line in playerLines)
                        {
                            try
                            {
                                // Which list of cards to exclude
                                var myBanlist = new HashSet<string>();
                            
                                // Rename specific players
                                // BuildPlayers_FixCardLine(line);

                                // Each tabbed area is player data
                                // [0] = player name (0mniscientEye)
                                // [1] = special attribute
                                // [2] = old way to get guild
                                // [3] = new way to get guild (DireTide@2017-08-10 03:09) <- remove the date chunk
                                // [4] = cards (csv)
                                // ---Optional---
                                // [5] = last time data was pulled
                                // [6] = possibleCards (csv)

                                string[] playerData = line.Split('\t');

                                if (playerData.Length < 5) continue;

                                string playerName = playerData[0].Trim();
                                //string playerAttr = playerData[1]; // Currently not used
                                string playerGuildOld = playerData[2].Length >= 2 ? playerData[2].Substring(0, Math.Max(2, playerData[2].Length)) : "";
                                string playerGuild = playerData[3].ToString().Split('@')[0];
                                string cards = playerData[4].Trim();
                                string lastUpdate = playerData.Length >= 6 ? playerData[5].Trim() : "";
                                string possibleCards = playerData.Length >= 7 ? playerData[6].Trim() : "";

                                // Get guild shortcut name (2 characters, like DT, TW)
                                // If the guild does not exist, look in the old guild
                                string shortGuildName = CONSTANTS.GUILD_CODES_FOR_OGRESHEET.ContainsKey(playerGuild) ? CONSTANTS.GUILD_CODES_FOR_OGRESHEET[playerGuild] : playerGuildOld;
                            
                                // Validation
                                if (String.IsNullOrEmpty(playerName)) continue;
                                if (shortGuildName.Length != 2) continue;

                            
                                // Use blacklist by playername (in appsettings)
                                if (CONSTANTS.PLAYERS_LEVEL0.Contains(playerName.ToLower()))
                                {
                                    myBanlist = new HashSet<string>();
                                }
                                else if (CONSTANTS.PLAYERS_LEVEL1.Contains(playerName.ToLower()))
                                {
                                    myBanlist = blacklistLevel1;
                                }
                                else if (CONSTANTS.PLAYERS_LEVEL2.Contains(playerName.ToLower()))
                                {
                                    myBanlist = blacklistLevel2;
                                }
                                else if (CONSTANTS.PLAYERS_LEVEL3.Contains(playerName.ToLower()))
                                {
                                    myBanlist = blacklistLevel3;
                                }

                                // Use blacklist by guild
                                else if (playerGuild == "WarHungryFTMFW" || playerGuild == "WarHungry")
                                {
                                    myBanlist = new HashSet<string>();
                                }
                                else if (playerGuild == "TheFallenKnights" || playerGuild == "MasterJedis")
                                {
                                    myBanlist = blacklistLevel1;
                                }
                                else if (playerGuild == "TidalWave")
                                {
                                    myBanlist = blacklistLevel1;
                                }
                                else if (playerGuild == "DireTide")
                                {
                                    myBanlist = blacklistLevel2;
                                }
                                else // Level0
                                {
                                    myBanlist = new HashSet<string>();
                                }

                                // Playername overrides - trying to use kongnames
                                if (playerName == "Nova") playerName = "Xalfir";

                                // Create player object
                                var player = new Player
                                {
                                    KongName = playerName,
                                    Guild = shortGuildName,
                                    DominionCards = new List<Card>(),
                                    LastUpdated = lastUpdate,
                                    Cards = new ConcurrentDictionary<Card, int>(),
                                    WeakCards = new ConcurrentDictionary<Card, int>(),
                                    PossibleCards = new ConcurrentDictionary<Card, int>(),
                                    UnknownCards = new ConcurrentDictionary<string, int>(),
                                    ExternalSeeds = new List<string>()
                                };

                                // Add player to Players
                                Players.Add(player);

                                // Add cards to Player
                                foreach (var c in cards.Split(','))
                                {
                                    BuildPlayerCardDatabase_AddCardToPlayer(player, c, myBanlist);
                                }
                                foreach (var c in possibleCards.Split(','))
                                {
                                    BuildPlayerCardDatabase_AddCardToPlayer(player, c, myBanlist, isPotentialCard:true);
                                }
                                // Add default dominions if one wasn't found
                                if (player.DominionCards.Count == 0)
                                {
                                    player.DominionCards = defaultDominions;
                                    foreach (var d in defaultDominions)
                                    {
                                        player.Cards.TryAdd(d, 1);
                                    }
                                }

                                // ---------------------------------------
                                // Overrides for possible cards and the bugs that ogre-magic doesn't get right
                                // ---------------------------------------         
                                // WORKAROUND: Vind reactor fusions; for ogre autoquad, we're getting way too many vind fusions because of shared fusion materials
                                // Cut the vind count down
                                foreach (var c in player.PossibleCards.Where(x => x.Key.Set == 2500 && x.Key.Rarity == 5))
                                {
                                    Card card = c.Key;
                                    int count = c.Value;

                                    if (count <= 3) player.PossibleCards[card] = 1;
                                    else if (count <= 6) player.PossibleCards[card] = 2;
                                    else if (count <= 8) player.PossibleCards[card] = 3;
                                    else player.PossibleCards[card] = 4;
                                }

                                // WORKAROUND: If a player has a dual fusion reward card, Ogre code will list it in owned and possible cards
                                foreach (var c in player.Cards.Where(x => x.Key.Set == 2000 && x.Key.Fusion_Level == 1))
                                {
                                    Card card = c.Key;
                                    int count = c.Value;

                                    if (player.PossibleCards.ContainsKey(card))
                                    {
                                        player.PossibleCards.TryRemove(card, out int x);
                                    }
                                }

                                // --------------------------------------- 

                                SeedManager.GeneratePlayerSeeds(player);

                                // Order player cards by - dominion, commander, section, faction
                                //player.Cards
                                //    .OrderBy(p => p.Key.CardType == CardType.Dominion.ToString())
                                //    .ThenBy(p => p.Key.CardType == CardType.Commander.ToString())
                                //    .ThenByDescending(p => p.Key.Section)
                                //    .ThenBy(p => p.Key.Faction);

                                //player.PossibleCards
                                //    .OrderBy(p => p.Key.CardType == CardType.Commander.ToString())
                                //    .ThenByDescending(p => p.Key.Section)
                                //    .ThenBy(p => p.Key.Faction);

                                //player.WeakCards
                                //    .OrderBy(p => p.Key.CardType == CardType.Commander.ToString())
                                //    .ThenBy(p => p.Key.Set)
                                //    .ThenBy(p => p.Key.Section)
                                //    .ThenBy(p => p.Key.Faction);
                            }
                            catch(Exception ex)
                            {
                                status += "\r\nBuildPlayers(): Error pulling player " + line.Split('\t')[0] + ". " + ex.Message;
                            }
                        }
                        //} // playerCsv

                });
            }
            catch (Exception ex)
            { 
                status += "\r\nImportPlayerCards(): Build player database failed: " + ex.Message;
            }

            time.Stop();
            Console.WriteLine(MethodInfo.GetCurrentMethod() + " " + time.ElapsedMilliseconds + "ms ");
            
            // If there's ever a sheet/screenname difference
            //if (BuildExtraPlayerInventories_OverridePlayer(ref playerName, ref guild))
            //{
            //    continue;
            //}
        }


        /// <summary>
        /// Write out imaginary player inventories
        /// </summary>
        private static void BuildTestAccounts()
        {
            var time = new Stopwatch();
            time.Start();

            try
            {

                // Default dominion if one isn't found
                var defaultDominions = new List<Card>();

                defaultDominions.Add(CardManager.GetById(50128)); // Alpha Serrated
                defaultDominions.Add(CardManager.GetById(50038)); // Alpha shielding
                defaultDominions.Add(CardManager.GetById(50311)); // Broodmother's Nexus

                defaultDominions.Add(CardManager.GetById(50263)); // Cassius' Nexus
                defaultDominions.Add(CardManager.GetById(50275)); // Barracus' Nexus
                defaultDominions.Add(CardManager.GetById(50299)); // Petrisis' Nexus
                defaultDominions.Add(CardManager.GetById(50323)); // Kleave's Nexus
                defaultDominions.Add(CardManager.GetById(50353)); // Constantine's Nexus


                var player = new Player
                {
                    KongName = "F2P-Inventory",
                    DominionCards = defaultDominions,
                    Guild = "ZZ",
                    Cards = CardManager.BuildInventory_F2P()
                };
                PlayerManager.Players.Add(player);
                SeedManager.GeneratePlayerSeeds(player);

                player = new Player
                {
                    KongName = "BigWhale",
                    DominionCards = defaultDominions,
                    Guild = "ZZ",
                    Cards = CardManager.BuildInventory_BigWhale()
                };
                PlayerManager.Players.Add(player);
                SeedManager.GeneratePlayerSeeds(player);

                player = new Player
                {
                    KongName = "BigPanda",
                    DominionCards = defaultDominions,
                    Guild = "ZZ",
                    Cards = CardManager.BuildInventory_AllCards()
                };
                PlayerManager.Players.Add(player);
                SeedManager.GeneratePlayerSeeds(player);
                
            }
            catch (Exception ex)
            {
                status += "\r\n BuildTestAccounts(): Failed: " + ex.Message;
            }

            time.Stop();
            Console.WriteLine(MethodInfo.GetCurrentMethod() + " " + time.ElapsedMilliseconds + "ms ");
        }

        /// <summary>
        ///  Write out player inventories
        /// </summary>
        private static void WritePlayerInventories(MainForm form)
        {
            var time = new Stopwatch();
            time.Start();

            // Players
            // Does this go faster async?
            List<Task<string>> playerTasks = new List<Task<string>>();

            // Try this in parallel with a limit. We had issues with this earlier
            Parallel.ForEach(Players,
                new ParallelOptions { MaxDegreeOfParallelism = 5 },
                player =>
                {
                    try
                    {
                        WritePlayerInventory(form, player);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error on writing player file: " + ex.Message);
                    }
                }
            );

            //foreach (var player in Players)
            //{
            //    WritePlayerInventory(form, player);
            //}

            time.Stop();
            Console.WriteLine(MethodInfo.GetCurrentMethod() + " " + time.ElapsedMilliseconds + "ms ");

        }

        private static string WritePlayerInventory(MainForm form, Player player)
        {
            string result = "";

            try
            {
                // Convert player cards and seeds to a string
                string playerCardsFile = GetPlayerCardsAndSeeds(player, sortOrder: "rarity");
                string playerPossibleCardsFile = GetPlayerCardsAndSeeds(player, sortOrder: "type", isPossibleCardFile: true);

                // Name of the player file
                string playerGuildName = player.Guild + "_" + player.KongName;

                // Write
                //status += 
                FileIO.SimpleWrite(form, "data/cards", playerGuildName + ".txt", playerCardsFile);
                //status += 
                FileIO.SimpleWrite(form, "data/cards", playerGuildName + "_possible.txt", playerPossibleCardsFile);
            }
            catch (Exception ex)
            {
                status += "\r\nWritePlayerInventory(): Failed on player " + player + ": " + ex.Message + ", ";
            }

            return result;
        }


        #endregion

        #region Helpers

        /// <summary>
        /// Put a player's seeds and cards in a string, adding comment lines (//) for seeds, weak cards, and other metadata
        /// </summary>
        public static string GetPlayerCardsAndSeeds(Player player, string sortOrder = "", bool listSeeds = true, bool isPossibleCardFile = false)
        {
            var result = new StringBuilder();

            // Cards to write at the end of the player file
            StringBuilder cardsToWriteLast = new StringBuilder();
            StringBuilder cardsToWriteLastCommanders = new StringBuilder();
            StringBuilder cardsToWriteLastDominions = new StringBuilder();

            // Markers
            int lastFaction = 0;
            int lastRarity = 0;
            int lastSection = 0;
            string lastSubset = "";

            // Sorted player card list
            IOrderedEnumerable<KeyValuePair<Card, int>> sortedCards = null;


            // -- Top section: Last update and seeds -- //
            result.AppendLine("// Last Updated: " + player.LastUpdated);

            // -------------------------
            // Write player cards file
            // -------------------------
            if (!isPossibleCardFile)
            {
                result.AppendLine();
                result.AppendLine("// ----- PLAYER SEEDS ----- //");
                foreach (var seed in player.ExternalSeeds)
                {
                    result.AppendLine(seed);
                }
                foreach (var seed in player.Seeds)
                {
                    result.AppendLine(seed);
                }
                result.AppendLine("// ----------------------- //");
                result.AppendLine();


                // Sort Cards
                switch (sortOrder)
                {
                    // Rarity -> Section -> CardId
                    case "rarity":
                        sortedCards = player.Cards.OrderByDescending(c => c.Key.Rarity).ThenByDescending(c => c.Key.Section).ThenByDescending(c => c.Key.CardId);
                        break;
                    // Set (2500) then (2000), then box/change vs. other stuff
                    case "type":
                        sortedCards = player.Cards.OrderByDescending(c => c.Key.Set).ThenByDescending(c => c.Key.Subset == CardSubset.Box.ToString()).ThenByDescending(c => c.Key.Subset == CardSubset.Chance.ToString()).ThenByDescending(c => c.Key.Faction).ThenByDescending(c => c.Key.Name);
                        break;
                    // Faction -> Section -> Power
                    case "faction":
                    default:
                        sortedCards = player.Cards.OrderByDescending(c => c.Key.Faction).ThenByDescending(c => c.Key.Section).ThenByDescending(c => c.Key.Power);
                        break;
                }

                // Write out cards
                foreach (var c in sortedCards)
                {
                    try
                    {
                        Card card = c.Key;
                        int cardCount = c.Value;

                        // Skip blanks and nulls
                        if (String.IsNullOrWhiteSpace(card.Name)) continue;

                        // Commanders: List last
                        if (card.CardType == CardType.Commander.ToString())
                        {
                            cardsToWriteLastCommanders.AppendLine(card.Name);
                        }
                        // Shards: Ignore
                        else if (card.Name == "Dominion Shard")
                        {
                            cardsToWriteLast.AppendLine("// " + card.Name + "#" + cardCount);
                        }
                        // Dominions: List last
                        else if (card.CardType == CardType.Dominion.ToString())
                        {
                            cardsToWriteLastDominions.AppendLine(card.Name);
                        }
                        // All other cards: List first, in the specified order
                        else
                        {
                            // This adds line breaks between rarity or faction
                            if (card.Rarity != lastRarity && sortOrder == "rarity")
                            {
                                result.AppendLine();
                                result.AppendLine();
                                lastRarity = card.Rarity;
                            }
                            if (card.Faction != lastFaction && sortOrder == "faction")
                            {
                                result.AppendLine();
                                result.AppendLine();
                                lastFaction = card.Faction;
                            }
                            if (card.Faction != lastFaction && sortOrder == "set")
                            {
                                result.AppendLine();
                                result.AppendLine();
                                lastSubset = card.Subset;
                            }

                            result.Append(card.Name);

                            if (cardCount > 1)
                            {
                                result.Append("#");
                                result.Append(Math.Min(cardCount, 10));
                            };

                            result.AppendLine();
                        }
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine("error parsing card on player " + player + ": " + ex);
                    }
                }

                // Write out Dominion shards 
                //result.AppendLine("Dominion Shard#3000");
                //result.AppendLine();

                // Write cards marked to be list
                result.AppendLine();
                result.Append(cardsToWriteLast);
                result.AppendLine();
                result.AppendLine("//---Commanders---");
                result.Append(cardsToWriteLastCommanders);
                result.AppendLine();
                result.AppendLine("//---Dominions----");
                result.Append(cardsToWriteLastDominions);
                result.AppendLine();
                result.AppendLine("//--------------");
                result.AppendLine("// Cards excluded");
                result.AppendLine("//--------------");
                lastSection = 0;

                // Write out player weak cards
                foreach (var c in player.WeakCards.OrderByDescending(c => c.Key.Section).ThenByDescending(c => c.Key.Power))
                {
                    var card = c.Key;
                    var cardCount = c.Value;

                    // Ignore dual fusions that aren't mythics. These are usually base fusions
                    if (card.CardType != CardType.Commander.ToString() && card.Rarity < 6 && card.Fusion_Level <= 1) continue;

                    // Seperate sections by a space
                    if (card.Section != lastSection)
                    {
                        result.Append("\r\n");
                        lastSection = card.Section;
                    }

                    result.Append("//");
                    result.Append(card.Name);
                    if (cardCount > 1)
                    {
                        result.Append("#");
                        result.Append(Math.Min(cardCount, 10));
                    }
                    result.Append("\r\n");
                }

                result.AppendLine();
                result.AppendLine("//--------------");
                result.AppendLine("// Cards not found");
                result.AppendLine("//--------------");
                foreach (var c in player.UnknownCards)
                {
                    result.Append("//");
                    result.Append(c.Key);
                    if (c.Value > 1)
                    {
                        result.Append("#");
                        result.Append(c.Value);
                    };
                    result.Append("\r\n");
                }

                // For dominion climbing add a crapload of shards
                //result.AppendLine("Dominion Shard#3000");
            }

            // -------------------------
            // Write Possible cards file
            // -------------------------
            else
            {
                // Cards to write at the end
                cardsToWriteLast = new StringBuilder();
                cardsToWriteLastCommanders = new StringBuilder();

                result.AppendLine("//--------------");
                result.AppendLine("// Possible cards");
                result.AppendLine("//--------------");
                foreach (var c in player.PossibleCards.OrderBy(c => c.Key.Faction).ThenByDescending(c => c.Key.Section).ThenByDescending(c => c.Key.Power))
                {
                    var card = c.Key;
                    var cardCount = Math.Min(c.Value, 10);

                    // Write cards
                    // TODO: Main card section uses "weak cards". Make a list called "weak possible cards" and replace this

                    // Possible commanders last
                    if (card.CardType == CardType.Commander.ToString())
                    {
                        cardsToWriteLastCommanders.AppendLine(card.Name);
                    }
                    // Stronger cards first
                    else if (card.Power >= 1 || card.FactionPower >= 1)
                    {
                        result.Append(card.Name);
                        result.Append("(+");
                        result.Append(cardCount);
                        result.Append(")");
                        result.Append("\r\n");
                    }
                    else
                    {
                        cardsToWriteLast.Append("//");
                        cardsToWriteLast.Append(card.Name);
                        cardsToWriteLast.Append("(+");
                        cardsToWriteLast.Append(cardCount);
                        cardsToWriteLast.Append(")");
                        cardsToWriteLast.Append("\r\n");
                    }
                }

                // Write commented out cards
                result.AppendLine();
                result.AppendLine();
                result.Append(cardsToWriteLast);
                result.AppendLine();
                result.AppendLine();
                result.Append(cardsToWriteLastCommanders);
            }

            return result.ToString();
        }

        /// <summary>
        /// Some player names are overridden
        /// false: don't skip this name
        /// </summary>
        private static bool BuildPlayers_OverrideName(ref string playerName, ref string guild)
        {
            switch (playerName)
            {
                // Picci
                case "Picci_Kibo":
                    playerName = "PicciKibo";
                    guild = "DT";
                    break;
            }

            return false;
        }

        /// <summary>
        /// Adds a card object to a player object
        /// </summary>
        private static void BuildPlayerCardDatabase_AddCardToPlayer(Player player, string cardName, HashSet<string> myBlacklist, bool isPotentialCard = false)
        {
            try
            {

                // Don't process a blank card
                if (String.IsNullOrWhiteSpace(cardName)) return;

                // Ignore comment lines
                if (cardName.StartsWith("//")) return;

                // Ignore dominion shards for now
                if (cardName.Contains("Dominion Shard")) return;

                KeyValuePair<string, int> baseCard = CardManager.FormatCard(cardName, removeLevels: true);
                string formattedCardName = baseCard.Key;
                int currentCount = baseCard.Value;

                // Ogre csv lists these with levels and our cardDb does not
                if (formattedCardName == "nexusdominion-2") formattedCardName = "nexusdominion";
                if (formattedCardName == "alphadominion-2") formattedCardName = "alphadominion";

                // If a card comes in as [####], look for its id instead of name
                Card card = null;
                if (formattedCardName.StartsWith("[") && formattedCardName.EndsWith("]"))
                    card = CardManager.GetById(formattedCardName);
                else
                    card = CardManager.GetPlayerCardByName(formattedCardName);


                // Attempt to find the card
                if (card != null)
                {
                    // Include Dominion as a special card
                    if (card.Set == 8500)
                    {
                        player.Cards[card] = 1;
                        player.DominionCards.Add(card);
                        return;
                    }

                    // Always include quad commanders
                    // Dual commanders must pass the banlist
                    if (card.Set == 7000 && card.Fusion_Level == 2)
                    {
                        // Add to cards or possiblecards
                        if (!isPotentialCard)
                        {
                            player.Cards.AddOrUpdate(card, 1, (c, count) => 1);
                        }
                        else
                        {
                            player.PossibleCards.AddOrUpdate(card, 1, (c, count) => 1);
                        }
                        return;
                    }

                    // Add card if not in banlist
                    if (!myBlacklist.Contains(formattedCardName))
                    {
                        // Add to cards or possiblecards
                        if (!isPotentialCard)
                        {
                            player.Cards.AddOrUpdate(card, currentCount, (c, count) => Math.Min(count + currentCount, 10));
                            //if (debugOutput) Console.WriteLine("Adding: " + card.Name);
                        }
                        else
                        {
                            player.PossibleCards.AddOrUpdate(card, currentCount, (c, count) => Math.Min(count + currentCount, 10));
                        }

                        return;
                    }
                    else
                    {
                        if (!isPotentialCard)
                        {
                            player.WeakCards.AddOrUpdate(card, currentCount, (c, count) => Math.Min(count + currentCount, 10));
                            //if (debugOutput) Console.WriteLine("Weak card: " + card.Name);
                        }
                        return;
                    }
                }
                else if (!isPotentialCard)
                {
                    player.UnknownCards.AddOrUpdate(formattedCardName, currentCount, (c, count) => Math.Min(count + currentCount, 10));
                    //if (debugOutput) Console.WriteLine("What is this?: " + currentCardName.Key);
                }
            

                // Can't find card in database
                else
                {
                    player.UnknownCards.AddOrUpdate(formattedCardName, currentCount, (c, count) => Math.Min(count + currentCount, 10));
                    //if (debugOutput) Console.WriteLine("What is this?: " + currentCardName.Key);
                }
            }
            catch (Exception ex)
            {
                var baseCardName = CardManager.FormatCard(cardName, true).Key; // <card, count>
                Console.WriteLine("BuildPlayerCardDatabase_AddCardToPlayer(): Error processing card " + baseCardName + ": " + ex.Message);
            }
        }


        #endregion

        #region Helpers - External player seed creation

        /// <summary>
        /// Creates a seed string for an external card list. 
        /// 0 = power seed
        /// 1-5 = factions eed (1=Imp, 2=Raider, 3=BT, 4=Xeno, 5=RT)
        /// </summary>
        public static string CreateExternalSeed(Dictionary<Card, int> playerCards, int factionSeed=0, int maxDeckSize = 9, int maxCopies = 3, bool sortByFactionPower = true, int seedNumber = 1)
        {
            string result = "";

            // Create a power deck
            Card commander = playerCards.Keys
                .Where(x => x.CardType == CardType.Commander.ToString())
                .OrderByDescending(x => x.Name == "Daedalus Charged")
                .ThenBy(x => x.Name == "Darius Caporegime")
                .ThenBy(x => x.Name == "Gaia the Purifier")
                .ThenByDescending(x => x.Fusion_Level)
                .FirstOrDefault();

            Card dominion = playerCards.Keys
                .Where(x => x.CardType == CardType.Dominion.ToString())
                .OrderBy(x => x.Fusion_Level)
                .FirstOrDefault();


            Dictionary<Card, int> sortedCards = new Dictionary<Card, int>();

            // Factionless
            if (factionSeed <= 0 || factionSeed > 5)
            {
                sortedCards = playerCards
                    .Where(x => x.Key.CardType == CardType.Assault.ToString() || x.Key.CardType == CardType.Structure.ToString())
                    .Where(x => x.Key.Power > 0)
                    .OrderByDescending(x => x.Key.Power)
                    .ToDictionary(x => x.Key, x => x.Value);
            }
            // Faction decks
            else
            {
                sortedCards = playerCards
                    .Where(x => x.Key.CardType == CardType.Assault.ToString() || x.Key.CardType == CardType.Structure.ToString())
                    .Where(x => x.Key.Fusion_Level == 2 || x.Key.Power > 0)
                    .OrderByDescending(x => x.Key.Faction == factionSeed)
                    .ThenByDescending(x => x.Key.Faction == 6)
                    .ThenByDescending(x => (sortByFactionPower) ? x.Key.FactionPower : x.Key.Power)
                    .ThenByDescending(x => x.Key.Power)
                    .ToDictionary(x => x.Key, x => x.Value);
            }

            StringBuilder sb = new StringBuilder();
            int deckCount = 0;

            // Add the most powerful cards until we have a DECK_SIZE card deck
            foreach (var cardPair in sortedCards)
            {
                string cardName = cardPair.Key.Name;
                int count = Math.Min(cardPair.Value, maxCopies); // Only 3 copies of a card

                if (count == 1)
                {
                    sb.Append(cardName);
                    sb.Append(", ");
                    deckCount++;
                }
                else
                {
                    // Trim in case this puts the deck over 10 cards
                    if (deckCount + count > maxDeckSize) count = maxDeckSize - deckCount;

                    sb.Append(cardName);
                    sb.Append("#" + count + ", ");
                    deckCount += count;
                }

                if (deckCount >= maxDeckSize) break;
            }

            if (commander != null && sortedCards.Count() > 0)
            {
                string seedName = "Power";
                if (factionSeed == 1) seedName = "Faction-Imperial";
                else if (factionSeed == 2) seedName = "Faction-Raider";
                else if (factionSeed == 3) seedName = "Faction-Bloodthirsty";
                else if (factionSeed == 4) seedName = "Faction-Xeno";
                else if (factionSeed == 5) seedName = "Faction-Righteous";

                // For duplicates (Faction-Imperial2)
                if (seedNumber > 1)
                {
                    seedName += seedNumber;
                }

                if (dominion != null)
                    result = "//" + seedName + ": " + commander.Name + ", " + dominion.Name + ", " + sb.ToString();
                else
                    result = "//" + seedName + ": " + commander.Name + ", " + sb.ToString();
            }

            return result;
        }

        /// <summary>
        /// Creates a seed string for an external card list. 
        /// TempoDeck - Up to 3 onPlay summons, 6 fast cards
        /// </summary>
        public static string CreateExternalSeedTempoDeck(Dictionary<Card, int> playerCards, int maxDeckSize = 9, int maxCopies = 3)
        {
            string result = "";

            // Create a power deck
            Card commander = playerCards.Keys
                .Where(x => x.CardType == CardType.Commander.ToString())
                .OrderByDescending(x => x.Name == "Gaia the Purifier")
                .ThenBy(x => x.Name == "Daedalus Charged")
                .ThenByDescending(x => x.Fusion_Level)
                .FirstOrDefault();

            Card dominion = playerCards.Keys
                .Where(x => x.CardType == CardType.Dominion.ToString())
                .OrderBy(x => x.Fusion_Level)
                .FirstOrDefault();



            StringBuilder sb = new StringBuilder();
            int deckCount = 0;

            // Get up to 3 cards that instantly summon
            Dictionary<Card, int> skillCards = CardManager.GetPlayerCardsBySkills(playerCards, new List<string> { "Summon" });
            foreach (var cardPair in skillCards.OrderByDescending(x => x.Key.Power))
            {
                string cardName = cardPair.Key.Name;
                int count = Math.Min(cardPair.Value, 2);

                if (count == 1)
                {
                    sb.Append(cardName);
                    sb.Append(", ");
                    deckCount++;
                }
                else
                {
                    sb.Append(cardName);
                    sb.Append("#" + count + ", ");
                    deckCount += count;
                }

                // Remove these from the dictionary so further card additions don't add them
                playerCards.Remove(cardPair.Key);

                if (deckCount >= 3) break;
            }

            // Fill in the rest of the deck with <= 2 delay, or cards with onPlay effects
            Dictionary<Card, int> sortedCards = playerCards
                    .Where(x => x.Key.CardType == CardType.Assault.ToString() || x.Key.CardType == CardType.Structure.ToString())
                    .Where(x => x.Key.Power > 0)
                    .Where(x => x.Key.Delay <= 2 ||
                                x.Key.s1.trigger == "Play" || x.Key.s3.trigger == "Play" || x.Key.s3.trigger == "Play")
                    .OrderByDescending(x => x.Key.Power)
                    .ToDictionary(x => x.Key, x => x.Value);

            foreach (var cardPair in sortedCards)
            {
                string cardName = cardPair.Key.Name;
                int count = Math.Min(cardPair.Value, maxCopies); // Only 3 copies of a card

                if (count == 1)
                {
                    sb.Append(cardName);
                    sb.Append(", ");
                    deckCount++;
                }
                else
                {
                    // Trim in case this puts the deck over 10 cards
                    if (deckCount + count > maxDeckSize) count = maxDeckSize - deckCount;

                    sb.Append(cardName);
                    sb.Append("#" + count + ", ");
                    deckCount += count;
                }

                if (deckCount >= maxDeckSize) break;
            }

            if (commander != null && sortedCards.Count() > 0)
            {
                string seedName = "Seed-Tempo";

                if (dominion != null)
                    result = "//" + seedName + ": " + commander.Name + ", " + dominion.Name + ", " + sb.ToString();
                else
                    result = "//" + seedName + ": " + commander.Name + ", " + sb.ToString();
            }

            return result;
        }


        /// <summary>
        /// Creates a seed string for an external card list. 
        /// StrikeDeck - Up to 3 strike cards, 2 overload cards, 1 Mortar card. Rest power
        /// </summary>
        public static string CreateExternalSeedStrikeDeck(Dictionary<Card, int> playerCards, int maxDeckSize = 9, int maxCopies = 3)
        {
            string result = "";

            // Create a power deck
            Card commander = playerCards.Keys
                .Where(x => x.CardType == CardType.Commander.ToString())
                .OrderByDescending(x => x.Name == "Gaia the Purifier")
                .ThenBy(x => x.Name == "Daedalus Charged")
                .ThenByDescending(x => x.Fusion_Level)
                .FirstOrDefault();

            Card dominion = playerCards.Keys
                .Where(x => x.CardType == CardType.Dominion.ToString())
                .OrderBy(x => x.Fusion_Level)
                .FirstOrDefault();



            StringBuilder sb = new StringBuilder();
            int deckCount = 0;

            // Get up to 3 cards that strike
            Dictionary<Card, int> skillCards = CardManager.GetPlayerCardsBySkills(playerCards, new List<string> { "Strike" });
            foreach (var cardPair in skillCards.OrderByDescending(x => x.Key.Power))
            {
                string cardName = cardPair.Key.Name;
                int count = Math.Min(cardPair.Value, 2);

                if (count == 1)
                {
                    sb.Append(cardName);
                    sb.Append(", ");
                    deckCount++;
                }
                else
                {
                    // Trim in case this puts the deck over 10 cards
                    if (deckCount + count > maxDeckSize) count = maxDeckSize - deckCount;

                    sb.Append(cardName);
                    sb.Append("#" + count + ", ");
                    deckCount += count;
                }

                // Remove these from the dictionary so further card additions don't add them
                playerCards.Remove(cardPair.Key);

                if (deckCount >= 3) break;
            }

            // Get up to 2 cards that overload
            skillCards = CardManager.GetPlayerCardsBySkills(playerCards, new List<string> { "Overload" });
            foreach (var cardPair in skillCards.OrderByDescending(x => x.Key.Power))
            {
                string cardName = cardPair.Key.Name;
                int count = Math.Min(cardPair.Value, 2);

                if (count == 1)
                {
                    sb.Append(cardName);
                    sb.Append(", ");
                    deckCount++;
                }
                else
                {
                    // Trim in case this puts the deck over 10 cards
                    if (deckCount + count > maxDeckSize) count = maxDeckSize - deckCount;

                    sb.Append(cardName);
                    sb.Append("#" + count + ", ");
                    deckCount += count;
                }

                // Remove these from the dictionary so further card additions don't add them
                playerCards.Remove(cardPair.Key);

                if (deckCount >= 5) break;
            }

            // Get up to 1 card that mortars
            skillCards = CardManager.GetPlayerCardsBySkills(playerCards, new List<string> { "Mortar" });
            foreach (var cardPair in skillCards.OrderByDescending(x => x.Key.Power))
            {
                string cardName = cardPair.Key.Name;
                int count = Math.Min(cardPair.Value, 2);

                if (count == 1)
                {
                    sb.Append(cardName);
                    sb.Append(", ");
                    deckCount++;
                }
                else
                {
                    // Trim in case this puts the deck over 10 cards
                    if (deckCount + count > maxDeckSize) count = maxDeckSize - deckCount;

                    sb.Append(cardName);
                    sb.Append("#" + count + ", ");
                    deckCount += count;
                }

                // Remove these from the dictionary so further card additions don't add them
                playerCards.Remove(cardPair.Key);

                if (deckCount >= 6) break;
            }

            // Fill in the rest of the deck power
            Dictionary<Card, int> sortedCards = playerCards
                    .Where(x => x.Key.CardType == CardType.Assault.ToString() || x.Key.CardType == CardType.Structure.ToString())
                    .Where(x => x.Key.Power > 0)
                    .OrderByDescending(x => x.Key.Power)
                    .ToDictionary(x => x.Key, x => x.Value);

            foreach (var cardPair in sortedCards)
            {
                string cardName = cardPair.Key.Name;
                int count = Math.Min(cardPair.Value, maxCopies); // Only 3 copies of a card

                if (count == 1)
                {
                    sb.Append(cardName);
                    sb.Append(", ");
                    deckCount++;
                }
                else
                {
                    // Trim in case this puts the deck over 10 cards
                    if (deckCount + count > maxDeckSize) count = maxDeckSize - deckCount;

                    sb.Append(cardName);
                    sb.Append("#" + count + ", ");
                    deckCount += count;
                }

                // Remove these from the dictionary so further card additions don't add them
                playerCards.Remove(cardPair.Key);

                if (deckCount >= maxDeckSize) break;
            }

            if (commander != null && sortedCards.Count() > 0)
            {
                string seedName = "Seed-Strike";

                if (dominion != null)
                    result = "//" + seedName + ": " + commander.Name + ", " + dominion.Name + ", " + sb.ToString();
                else
                    result = "//" + seedName + ": " + commander.Name + ", " + sb.ToString();
            }

            return result;
        }


        #endregion
    }
}

