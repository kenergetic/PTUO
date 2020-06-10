using PirateTUO2.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PirateTUO2.Modules
{

    /// <summary>
    /// Class for common text cleanup
    /// </summary>
    public static class TextCleaner
    {
        /// <summary>
        /// Removes excess characters from a common deck string
        /// </summary>
        public static string RemoveTabSpaces(string input)
        {
            return input
                .Trim()
                .Replace("\r\n", "")
                .Replace("\n", "")
                .Replace("\t\t", ",")
                .Replace("\t", ",");
        }

        
        /// <summary>
        /// Attempt to clean up a deck string, which is either
        /// 
        /// PLAYER:WINRATE:DECK:DT
        /// PLAYER:WINRATE:DECK
        /// PLAYER:DECK
        /// </summary>
        public static string CleanDeckString(string input)
        {
            input = RemoveTabSpaces(input);

            // Clip ending guild colon (like "...:DT")
            if (input[input.Length - 3] == ':') input = input.Substring(0, input.Length - 3);

            // Only get last entry when splitting by colon (ex PLAYER:WINRATE:DECK would get DECK, or PLAYER:DECK would get DECK)
            string[] splitResult = input.Split(':');
            input = splitResult[splitResult.Length - 1];

            // Final results will display unit count. Ignore this for larger decks
            input.Replace("10 units: ", "");
            input.Replace("9 units: ", "");
            input.Replace("8 units: ", "");
            input.Replace("7 units: ", "");

            return input;
        }

        /// <summary>
        /// Attempts to detect whether a deck string has a dominion
        /// </summary>
        public static bool HasDominion(string input)
        {
            if (input.Contains("Alpha ") || input.Contains("'s Nexus") || input.Contains("' Nexus") || input.Contains("Nexus Dominion"))
                return true;
            else
                return false;
        }
        
        /// <summary>
        /// Hyphen commander quads in a deck
        /// EX: Tabitha Liberated, to Tabitha Liberated-1,
        /// </summary>
        public static string DeckHyphenQuadCommanders(string deck)
        {
            foreach (var commander in CONSTANTS.COMMANDER_QUADS)
            {
                deck = deck.Replace(commander + ",", commander + "-1,");
            }

            return deck;
        }

        /// <summary>
        /// Remove hyphen commander quads in a deck
        /// EX: Tabitha Liberated-1, to Tabitha Liberated,
        /// </summary>
        public static string CommanderRemoveHypen(string deck)
        {
            foreach (var commander in CONSTANTS.COMMANDER_QUADS)
            {
                deck = deck.Replace(commander + "-1", commander);
            }

            return deck;
        }

        /// <summary>
        /// Remove Dominions from a deck
        /// </summary>
        public static string DeckRemoveDominions(string deck)
        {
            foreach (var d in CONSTANTS.DOMINIONS)
            {
                deck = deck.Replace(d + ",", "");
                deck = deck.Replace(d + "-5,", "");
                deck = deck.Replace(d + "-4,", "");
                deck = deck.Replace(d + "-3,", "");
                deck = deck.Replace(d + "-2,", "");
                deck = deck.Replace(d + "-1,", "");
            }

            return deck;
        }

        /// <summary>
        /// Takes a comma separated list of cards (no Commander / Dominion) and covers 
        /// </summary>
        /// <returns></returns>
        public static List<string> CardOrdersForLivePlay(string cardsInDeck, int freeze, int extraFreezeCards)
        {
            List<string> allCardOrders = new List<string>();

            // Only one order
            if (extraFreezeCards <= 0)
            {
                allCardOrders.Add(cardsInDeck);
                return allCardOrders;
            }

            // Split the cards in deck into individual cards
            List<string> cardList = cardsInDeck.Split(',').ToList();

            // Freeze can't exceed the deck size
            if (freeze > cardList.Count) freeze = cardList.Count;

            // Parse the card list and split it into sections
            string frozenCards = ""; 
            List<string> orderedCards = new List<string>(); 
            string remainingCards = ""; 

            for(int i=1; i<=cardList.Count; i++)
            {
                var currentCard = cardList[i-1].Trim();

                // Already played (locked)
                if (i <= freeze)
                {
                    frozenCards += currentCard + ",";
                }
                // Cards that could possible be in the "extra frozen section"
                // if extraFreeze is 1, Card A/B/C could be in that slot
                // if extraFreeze is 2, Card A/B/C/D could be in that slot
                else if (i <= freeze + extraFreezeCards + 2)
                {
                    orderedCards.Add(currentCard);
                }
                // Cards reordered without thinking about order
                else
                {
                    remainingCards += currentCard + ",";
                }
            }

            // Trim remaining cards
            remainingCards.TrimEnd(' ');
            remainingCards.TrimEnd(',');
            remainingCards.TrimEnd(' ');



            // Get all cardOrders in the most crappy way possible
            string A = orderedCards.Count >= 1 ? orderedCards[0] + "," : "";
            string B = orderedCards.Count >= 2 ? orderedCards[1] + "," : "";
            string C = orderedCards.Count >= 3 ? orderedCards[2] + "," : "";
            string D = orderedCards.Count >= 4 ? orderedCards[3] + "," : "";
            string E = orderedCards.Count >= 5 ? orderedCards[4] + "," : "";

            // 3 valid combos
            // Only the position of the first letter matters
            if (extraFreezeCards == 1 || orderedCards.Count <= 3)
            {                
                allCardOrders.Add(frozenCards + A + B + C + remainingCards);
                allCardOrders.Add(frozenCards + B + A + C + remainingCards);
                allCardOrders.Add(frozenCards + C + A + B + remainingCards);
            }
            // 9 valid combos
            // Only the position of the first two letters matter
            else if (extraFreezeCards == 2 || orderedCards.Count == 4)
            {
                allCardOrders.Add(frozenCards + A + B + C + D + remainingCards);
                allCardOrders.Add(frozenCards + A + C + B + D + remainingCards);
                allCardOrders.Add(frozenCards + A + D + C + B + remainingCards);

                allCardOrders.Add(frozenCards + B + A + C + D + remainingCards);
                allCardOrders.Add(frozenCards + B + C + A + D + remainingCards);
                allCardOrders.Add(frozenCards + B + D + C + A + remainingCards);

                allCardOrders.Add(frozenCards + C + A + B + D + remainingCards);
                allCardOrders.Add(frozenCards + C + B + A + D + remainingCards);
                allCardOrders.Add(frozenCards + C + D + A + B + remainingCards);
            }
            // 27 valid combos
            else if (extraFreezeCards == 2 || orderedCards.Count == 5)
            {
                allCardOrders.Add(frozenCards + A + B + C + D + E + remainingCards);
                allCardOrders.Add(frozenCards + A + B + D + B + E + remainingCards);
                allCardOrders.Add(frozenCards + A + B + E + C + D + remainingCards);
                allCardOrders.Add(frozenCards + A + C + B + D + E + remainingCards);
                allCardOrders.Add(frozenCards + A + C + D + B + E + remainingCards);
                allCardOrders.Add(frozenCards + A + C + E + B + E + remainingCards);
                allCardOrders.Add(frozenCards + A + D + B + C + E + remainingCards);
                allCardOrders.Add(frozenCards + A + D + C + B + E + remainingCards);
                allCardOrders.Add(frozenCards + A + D + E + B + C + remainingCards);

                allCardOrders.Add(frozenCards + B + A + C + D + E + remainingCards);
                allCardOrders.Add(frozenCards + B + A + D + C + E + remainingCards);
                allCardOrders.Add(frozenCards + B + A + E + C + D + remainingCards);
                allCardOrders.Add(frozenCards + B + C + A + D + E + remainingCards);
                allCardOrders.Add(frozenCards + B + C + D + A + E + remainingCards);
                allCardOrders.Add(frozenCards + B + C + E + A + D + remainingCards);
                allCardOrders.Add(frozenCards + B + D + A + C + E + remainingCards);
                allCardOrders.Add(frozenCards + B + D + C + A + E + remainingCards);
                allCardOrders.Add(frozenCards + B + D + E + A + D + remainingCards);

                allCardOrders.Add(frozenCards + C + A + B + D + E + remainingCards);
                allCardOrders.Add(frozenCards + C + A + D + B + E + remainingCards);
                allCardOrders.Add(frozenCards + C + A + E + B + D + remainingCards);
                allCardOrders.Add(frozenCards + C + B + A + D + E + remainingCards);
                allCardOrders.Add(frozenCards + C + B + D + A + E + remainingCards);
                allCardOrders.Add(frozenCards + C + B + E + A + D + remainingCards);
                allCardOrders.Add(frozenCards + C + D + A + B + E + remainingCards);
                allCardOrders.Add(frozenCards + C + D + B + A + E + remainingCards);
                allCardOrders.Add(frozenCards + C + D + E + B + A + remainingCards);
            }

            //Prune stupid commas
            for (int i = 0; i < allCardOrders.Count; i++)
            {
                allCardOrders[i] = allCardOrders[i].TrimEnd(' ');
                allCardOrders[i] = allCardOrders[i].TrimEnd(',');
            }

            return allCardOrders;
        }

        /// <summary>
        /// Returns a deck with copied cards separated
        /// e.g. HyperSec Hunter#2 would be HyperSec Hunter, HyperSec Hunter
        /// </summary>
        public static List<string> UncompressDeck(List<string> deck)
        {
            List<string> result = new List<string>();

            // Terrible way to uncompress cards
            foreach (var card in deck)
            {
                var c = card.Trim();
                if (c.EndsWith("#2"))
                {
                    c = c.Substring(0, c.Length - 3);
                    result.Add(c);
                    result.Add(c);
                }
                else if (c.EndsWith("#3"))
                {
                    c = c.Substring(0, c.Length - 3);
                    result.Add(c);
                    result.Add(c);
                    result.Add(c);
                }
                else if (c.EndsWith("#4"))
                {
                    c = c.Substring(0, c.Length - 3);
                    result.Add(c);
                    result.Add(c);
                    result.Add(c);
                    result.Add(c);
                }
                else if (c.EndsWith("#5"))
                {
                    c = c.Substring(0, c.Length - 3);
                    result.Add(c);
                    result.Add(c);
                    result.Add(c);
                    result.Add(c);
                    result.Add(c);
                }
                else if (c.EndsWith("#6"))
                {
                    c = c.Substring(0, c.Length - 3);
                    result.Add(c);
                    result.Add(c);
                    result.Add(c);
                    result.Add(c);
                    result.Add(c);
                    result.Add(c);
                }
                else if (c.EndsWith("#7"))
                {
                    c = c.Substring(0, c.Length - 3);
                    result.Add(c);
                    result.Add(c);
                    result.Add(c);
                    result.Add(c);
                    result.Add(c);
                    result.Add(c);
                    result.Add(c);
                }
                else if (c.EndsWith("#8"))
                {
                    c = c.Substring(0, c.Length - 3);
                    result.Add(c);
                    result.Add(c);
                    result.Add(c);
                    result.Add(c);
                    result.Add(c);
                    result.Add(c);
                    result.Add(c);
                    result.Add(c);
                }
                else if (c.EndsWith("#9"))
                {
                    c = c.Substring(0, c.Length - 3);
                    result.Add(c);
                    result.Add(c);
                    result.Add(c);
                    result.Add(c);
                    result.Add(c);
                    result.Add(c);
                    result.Add(c);
                    result.Add(c);
                    result.Add(c);
                }
                else
                {
                    result.Add(c);
                }
            }

            return result;
        }

        /// <summary>
        /// Given a guild name, return a 2 digit shortname, or __ if no faction is provided
        /// </summary>
        public static string GetGuildShortName(string faction)
        {
            if (faction == null) faction = "";

            switch (faction)
            {
                // Main guilds
                case "DireTide":
                    faction = "DT";
                    break;
                case "TheFallenKnights":
                    faction = "TF";
                    break;
                case "TidalWave":
                    faction = "TW";
                    break;
                // Shells
                case "SerbianLawyers":
                    faction = "SL";
                    break;
                case "TheCaleuche":
                    faction = "TC";
                    break;
                case "WarHungryFTMFW":
                    faction = "WH";
                    break;

                // Defunct
                case "Jotunheimr":
                    faction = "jh";
                    break;
                case "KnightsOfAries":
                    faction = "ka";
                    break;
                case "LethalHamsters":
                    faction = "lh";
                    break;
                case "LadyKillerz":
                    faction = "lk";
                    break;
                case "WarThirstyFTMFW":
                    faction = "wt";
                    break;

                // Other
                case "AllHailBolas":
                    faction = "AB";
                    break;
                case "ASYLUM":
                    faction = "AS";
                    break;
                case "ChaosBairs":
                    faction = "CB";
                    break;
                case "EternalDYN":
                    faction = "ED";
                    break;
                case "gravybairs":
                    faction = "GB";
                    break;
                case "MetalSaints":
                    faction = "MS";
                    break;
                case "MasterJedis":
                    faction = "MJ";
                    break;
                case "NovaSlayers":
                    faction = "NS";
                    break;
                case "NewHope":
                    faction = "NH";
                    break;
                case "OmegaBeasts":
                    faction = "OB";
                    break;
                case "Paragons":
                    faction = "PA";
                    break;
                case "PrimalBairs":
                    faction = "PB";
                    break;
                case "RealmOfKings":
                    faction = "RK";
                    break;
                case "Russia":
                    faction = "RU";
                    break;
                case "SanctifiedDemons":
                    faction = "SD";
                    break;
                case "SupremeBairs":
                    faction = "SB";
                    break;
                case "TidalBeastsFTMFW":
                    faction = "TB";
                    break;
                case "TrypticonDYN":
                    faction = "TD";
                    break;
                case "UndyingDYN":
                    faction = "UD";
                    break;
                case "ForActivePlayers":
                    faction = "FA";
                    break;
                case "":
                case "_UNGUILDED":
                    faction = "__";
                    break;
                default:
                    if (faction.Length > 2)
                        faction = faction.Substring(0, 2);
                    break;
            }

            return faction;
        }
        
    }
}
