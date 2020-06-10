using PirateTUO2.Classes;
using PirateTUO2.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PirateTUO2.Modules
{
    /// <summary>
    /// Handles pulling gauntlets or whatever from ogrelicious. Authentication to Ogrelicious is not stored 
    /// ** DEPRECATED
    /// </summary>
    public static class Ogrelicious
    {
        public static string ServerInfo = "";

        /// <summary>
        /// Connects to a deck sniffer server and pulls decks based on filters
        /// </summary>
        public async static void CreateGauntlet(MainForm form, bool includeGuild, bool includeTime, bool mergePlayerDecks, List<string> ignoreGuilds)
        {
            // Call the server
            string[] data = await CallServerSniffer(form.gtSearchTermComboBox.Text, form.gtDateComboBox.Text);

            // ServerSniffer threw an error
            if (data.Contains("CallServerSniffer(): Error"))
            {
                form.adminOutputTextBox.Text = data + "\r\n";
                return;
            }

            // Create a gauntlet from the data
            string result = CompileGauntlet(form.gtOutputNameTextBox.Text, data, includeGuild, includeTime, mergePlayerDecks, ignoreGuilds);

            // Display the data
            form.adminOutputTextBox.Text = result;
        }
        
        /// <summary>
        /// Finds one player and returns the most recent (or most complete) deck
        /// </summary>
        public static string FindPlayer(string playerName)
        {
            DateTime today = DateTime.Now;
            string[] decks = CallServerSniffer(playerName, today.ToString("M/.*/yy")).Result;

            // Look back further if needed
            if (decks.Length == 0) decks = CallServerSniffer(playerName, today.AddMonths(-1).ToString("M/.*/yy")).Result;
            if (decks.Length == 0) decks = CallServerSniffer(playerName, today.AddMonths(-2).ToString("M/.*/yy")).Result;
            if (decks.Length == 0) decks = CallServerSniffer(playerName, today.AddMonths(-3).ToString("M/.*/yy")).Result;
            if (decks.Length == 0) decks = CallServerSniffer(playerName, today.ToString(".*/.*/.*")).Result;

            //decks = CallServerSniffer(playerName, today.ToString("M/d/yy")).Result;

            // Return the result 
            return CompileGauntlet("", decks, false, true, true, new List<string>());
        }
        

        /// <summary>
        /// Lazy shortcut buttons
        /// </summary>
        public static void GuildNameShortcutButton(MainForm form, string controlName)
        {
            var now = DateTime.Now.AddHours(-2);
        }
        


        #region Helper Methods

        /// <summary>
        /// Get the data back from the server
        /// </summary>
        public async static Task<string[]> CallServerSniffer(string guild, string date)
        {
            try
            {
                var url = CONFIG.DeckSnifferUrl;


                // Pull raw data
                var cookieContainer = new CookieContainer();
                using (var handler = new HttpClientHandler() { CookieContainer = cookieContainer })
                {
                    using (var client = new HttpClient(handler))
                    {
                        client.DefaultRequestHeaders.Add("Authorization", "Basic b2dyZWxpY2lvdXM6b2dyZTRsaWZl");

                        // Create Form data to go with the call
                        var pairs = new Dictionary<string, string>();
                        pairs.Add("guild", guild);
                        pairs.Add("date1", date);
                        var content = new FormUrlEncodedContent(pairs);

                        // Response from the server
                        var response = client.PostAsync(url, content).Result;
                        if (response.IsSuccessStatusCode)
                        {
                            string data = await response.Content.ReadAsStringAsync();
                            data = data.Replace(",,", ", ");
                            data = data.Replace(", ,", ",");
                            data = Regex.Replace(data, @"(\d\d\d\d\d)", "[$1]");
                            //TODO: Try to translate [\d\d\d\d\d] into a card
                            string[] result = Regex.Split(data, "<br>");                            
                            return result;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //Helper.Popup(form, ex.ToString(), "Error connecting to yarr Ogre server");

                return new string[] { "CallServerSniffer: Error on search: " + ex.Message };
            }


            return new string[] { "CallServerSniffer(): Error" };

        }

        /// <summary>
        /// This takes the resulting decks and creates a gauntlet around them
        /// 
        /// It has a couple mechanisms for removing duplicates and really incomplete decks
        /// </summary>
        public static string CompileGauntlet(string gauntletName, string[] logOfDecks, bool includeGuild, bool includeTime, bool mergePlayerDecks, List<string> ignoreGuilds)
        {
            List<OgreliciousDeck> decks = new List<OgreliciousDeck>();
            List<OgreliciousDeck> tempDecks = new List<OgreliciousDeck>(); // Temp storage when sorting
            StringBuilder result = new StringBuilder();

            // Turn deck log lines into deck objects
            foreach (var deck in logOfDecks)
            {
                // <time>|<guild>|<name>|<0-1>|<deck>
                string[] deckParts = deck.Split('|');

                // SKIP: deckParts not the right size
                if (deckParts.Length < 5) continue;

                // Parse the dateTime from the log line
                DateTime dateTime = DateTime.TryParse(deckParts[0].Replace(" INFO:", ""), out DateTime dt) ? dt : DateTime.MinValue;

                // Init the deck
                OgreliciousDeck newDeck = new OgreliciousDeck(deckParts[2], deckParts[4], deckParts[1], dateTime);


                // SKIP: Decks in ignoreguild
                if (ignoreGuilds.Contains(newDeck.Guild)) continue;

                // SKIP: A deck that's 2 or less cards
                if (newDeck.DeckSize <= 2) continue;

                // SKIP: a duplicate deck
                OgreliciousDeck dupe = decks.FirstOrDefault(d => d.Player == newDeck.Player && d.DeckList == newDeck.DeckList);
                if (decks.Contains(dupe)) continue;

                // Add this to the tuple
                decks.Add(newDeck);
            }


            // Handle merging better
            // * Start with most recent deck, then add cards to it from past decks that weren't already in it
            if (mergePlayerDecks)
            {
                List<string> players = decks.GroupBy(x => x.Player).Select(x => x.First().Player).ToList(); // Each player
                int lookbackLimit = 3; // Max decks to look back at


                // For each player, figure out a final deck to add to the "decks2" list. 
                // Then overwrite decks with decks2
                foreach (var player in players)
                {
                    List<OgreliciousDeck> playerDecks = decks.Where(x => x.Player == player).OrderByDescending(x => x.Date).ToList();
                    string commander = "";
                    string dominion = "";

                    // Multiple decks found - try to merge them
                    if (playerDecks.Count > 1)
                    {
                        OgreliciousDeck mergedDeck = playerDecks[0];
                        commander = mergedDeck.DeckParts[0].Trim();
                        dominion = mergedDeck.DeckParts[1].Trim();

                        int maxPastDecks = Math.Min(lookbackLimit, playerDecks.Count);

                        // Foreach past player deck (skipping the first)
                        for (int i = 1; i < maxPastDecks; i++)
                        {
                            // Foreach past card
                            for (int j = 2; j < playerDecks[i].DeckParts.Length; j++)
                            {
                                var pastCard = playerDecks[i].DeckParts[j].Trim();

                                if (!mergedDeck.DeckParts.Contains(pastCard))
                                {
                                    // Reprocess merged deck
                                    mergedDeck.DeckList += "," + playerDecks[i].DeckParts[j];
                                    mergedDeck.DeckParts = mergedDeck.DeckList.Split(',');
                                    mergedDeck.DeckSize++;
                                    // Probably enough cards
                                    if (mergedDeck.DeckSize >= 10) continue;
                                }
                            }
                        }

                        tempDecks.Add(mergedDeck);
                    }
                    // One deck just passes through
                    else
                    {
                        tempDecks.Add(playerDecks[0]);
                    }
                }

                // Overwrite decks with decks2
                decks = tempDecks;
            }


            // Order results returned
            // -- Always group by player to do some possible merging --
            if (includeGuild)
            {
                // Guild, then Player, then Time
                decks = decks.OrderBy(d => d.Guild).ThenBy(d => d.Player).ThenBy(d => d.Date).ToList();
            }
            else
            {
                // Player, then Time
                decks = decks.OrderBy(d => d.Player).ThenBy(d => d.Date).ToList();
            }



            // Output the result
            foreach (var deck in decks)
            {
                var title = new StringBuilder();
                title.Append(gauntletName);

                if (gauntletName != "")
                    title.Append("_");

                if (includeGuild)
                    title.Append(deck.Guild + "_def_");

                title.Append(deck.Player);

                if (includeTime)
                    title.Append("_(" + deck.Date.Month + "/" + deck.Date.Day + ")");

                result.AppendLine(title.ToString() + ": " + deck.DeckList);
            }

            return result.ToString();
        }




        /// <summary>
        /// This takes the resulting gauntlets and creates a gauntlet around them
        /// 
        /// It has a couple mechanisms for removing duplicates or small decks (because player lost bad) 
        /// </summary>
        private static string AssembleCardInventories(MainForm form, string gauntletName, string[] decks)
        {
            StringBuilder result = new StringBuilder();
            decks.OrderBy(d => d.Split('|')[2])
                 .ThenBy(d => DateTime.Parse(d.Split('|')[0].Replace(" INFO:", "")));

            // player, card/quantity
            var allPlayersCardList = new Dictionary<string, Dictionary<string, int>>();
            var allPlayersDecks = new Dictionary<string, List<string>>();

            // Essentially create inventories from a compiled list of each target players cards
            foreach (var deck in decks)
            {
                var d = deck.Split('|');
                if (d.Length != 5) continue;

                var playerName = d[2].Trim();
                var playerDeck = d[4].Trim();
                var playerCards = playerDeck.Split(',');

                // Playername: No weirdo characters. Boring only
                Regex rgx = new Regex("[^a-zA-Z0-9 -]");
                playerName = rgx.Replace(playerName, "");

                // Add player to dictionary if its new
                if (!allPlayersCardList.ContainsKey(playerName))
                {
                    allPlayersCardList.Add(playerName, new Dictionary<string, int>());
                    allPlayersDecks.Add(playerName, new List<string>());
                }


                // Group duplicate cards
                var playerCardsAdjusted = from x in playerCards
                                          group x by x into g
                                          let count = g.Count()
                                          orderby count descending
                                          select new { Name = g.Key, Count = count };

                // Add to player decks
                allPlayersDecks[playerName].Add(playerDeck);

                // Add to player card list
                var playerCardList = allPlayersCardList[playerName];

                foreach (var card in playerCardsAdjusted)
                {
                    if (card.Name.Trim().Length == 0) continue;

                    if (playerCardList.ContainsKey(card.Name))
                    {
                        var count = playerCardList[card.Name];
                        if (card.Count > count) playerCardList[card.Name] = card.Count;
                    }
                    else
                    {
                        playerCardList.Add(card.Name, card.Count);
                    }
                }
            }
            
            foreach (var player in allPlayersCardList)
            {
                result.AppendLine("//--------------------");
                result.AppendLine("//Decks written. Restart to apply changes");
                result.AppendLine("//--------------------");
                
                //using (StreamWriter writer = new StreamWriter("./data/cards/"+ form.invBuilderGuildAcronymTextBox.Text + "_" + player.Key + ".txt"))
                //{
                //    result.AppendLine("//--------------------");
                //    result.AppendLine(player.Key);

                //    var playerDecks = allPlayersDecks[player.Key];
                //    playerDecks.Reverse();


                //    // Write base seeds
                //    if (playerDecks.Count >= 1)
                //        writer.WriteLine("//Seed_Atk_Ord:" + playerDecks[0]);
                //    else
                //        writer.WriteLine("//Seed_Atk_Ord:");
                //    if (playerDecks.Count >= 2)
                //    {
                //        writer.WriteLine("//Seed_Def:" + playerDecks[1]);
                //    }
                //    else
                //    {
                //        writer.WriteLine("//Seed_Def:");
                //    }

                //    foreach (var card in player.Value)
                //    {
                //        if (card.Value == 1)
                //        {
                //            result.AppendLine(card.Key);
                //            writer.WriteLine(card.Key);
                //        }
                //        else
                //        {
                //            result.AppendLine(card.Key + "#" + card.Value);
                //            writer.WriteLine(card.Key + "#" + card.Value);
                //        }
                //    }
                //}



            }

            return result.ToString();
        }



        #endregion
    }
}
