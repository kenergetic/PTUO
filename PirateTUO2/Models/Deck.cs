using PirateTUO2.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PirateTUO2.Models
{
    /// <summary>
    /// Deck object - used in sim modules
    /// </summary>
    public class Deck
    {
        // Deck name for gauntlets
        public string Name { get; set; }
        public string Description { get; set; } // For now captures errors

        // Raw deck string that comes in (if it comes in as Name:<deck>, this will be <deck>)
        public string OriginalString { get; set; }

        // Raw csv
        public string DeckCsv { get; set; }

        // List of cards - String form
        public List<string> Cards { get; set; }
        public List<string> CardAssaultsAndStructures { get; set; }
        public string Commander { get; set; }
        public string Dominion { get; set; }

        // List of card objects, and string form of cards it didn't recognize
        public Dictionary<Card, int> CardObjectsAssaultsAndStructures { get; set; }
        public Dictionary<string, int> CardsNotFound { get; set; }
        
        public bool HasDominion { get; set; }
        public bool HasCommander { get; set; }
        public int DeckCount { get; set; } //Size of the deck, excluding commander/dominion

        // Metadata
        // Allows a lazy sort, using the first letter of assaults/structures, sorted by rarer cards first
        public string SloppySort { get; set; } 

        // How alike this deck is to another deck
        public int MatchScore { get; set; } 


        #region Constructors

        public Deck()
        {
            Cards = new List<string>();
            CardAssaultsAndStructures = new List<string>();
            Name = "";
        }

        /// <summary>
        /// Given a deck string, attempts to convert it into a deck
        /// 
        /// This should accept and parse
        /// DECK (a,b,c,...)
        /// PLAYER:DECK
        /// PLAYER:WINRATE:DECK
        /// PLAYER:WINRATE:DECK:GUILD (guild must be 2 characters)
        /// </summary>
        public Deck(string deckString)
        {
            Cards = new List<string>();
            CardAssaultsAndStructures = new List<string>();
            CardObjectsAssaultsAndStructures = new Dictionary<Card, int>();
            CardsNotFound = new Dictionary<string, int>();

            // Get the title of the deck, if it exists
            deckString = deckString.Trim()
                                .Replace("\r\n", "")
                                .Replace("\n", "")
                                .Replace("\t\t", ",")
                                .Replace("\t", ",");

            string[] parsedDeckString = deckString.Split(':');

            OriginalString = parsedDeckString[0];

            if (parsedDeckString.Length == 2)
            {
                Name = parsedDeckString[1];
                OriginalString = parsedDeckString[1];
            }
            else if (parsedDeckString.Length >= 3)
            {
                Name = parsedDeckString[2];
                OriginalString = parsedDeckString[2];
            }
                        
            int deckCount = 0;
            // Record the first card, and use that as a commander if one is not found
            string firstCard = "";

            // Parse cards and try to define a commander/dominion
            foreach (var c in OriginalString.Split(','))
            {
                // Strip levels
                if (string.IsNullOrWhiteSpace(c.Trim())) continue;
                if (deckCount == 0) firstCard = c.Trim();

                // Get card name and count
                KeyValuePair<string, int> cardKvp = CardManager.FormatCard(c.Trim(), removeLevels: true);
                string cardName = cardKvp.Key;
                int cardCount = cardKvp.Value;

                // Try to find this card in the card database
                Card cardObject = CardManager.GetPlayerCardByName(cardName);

                // Card object found - commander
                if (HasCommander == false && cardObject != null && cardObject.CardType == CardType.Commander.ToString())
                {
                    Commander = cardObject.Name;
                    HasCommander = true;
                }

                // Card object found - Dominion
                else if (cardObject != null && cardObject.CardType == CardType.Dominion.ToString())
                {
                    Dominion = cardObject.Name;
                    HasDominion = true;
                }

                // Card object found 
                else if (cardObject != null)
                {
                    // Get card count
                    // Look for card#2, or card(2)
                    //var regex = new Regex(".*#([0-9]+).*");
                    //var regex2 = new Regex(".*\\(([0-9]+)\\).*");
                    //var match = regex.Match(cardName);
                    //var match2 = regex2.Match(cardName);

                    //int numberOfCards = 1;

                    //if (match.Success || match2.Success)
                    //{
                    //    numberOfCards = Int32.Parse(match.Groups[1].Value);
                    //}

                    // Add to Card assault list
                    if (cardCount == 1)
                        CardAssaultsAndStructures.Add(cardObject.Name);
                    else
                        CardAssaultsAndStructures.Add(cardObject.Name + "#" + cardCount);

                    // Add to CardObjects
                    if (!CardObjectsAssaultsAndStructures.ContainsKey(cardObject)) CardObjectsAssaultsAndStructures.Add(cardObject, cardCount);
                    else CardObjectsAssaultsAndStructures[cardObject]+= cardCount;

                    deckCount += cardCount;
                }

                // Card object not found
                else
                {
                    // Get card count - this is already done
                    // Look for card#2, or card(2)
                    //Regex regex = new Regex(".*#([0-9]+).*");
                    //Regex regex2 = new Regex(".*\\(([0-9]+)\\).*");
                    //Match match = regex.Match(c);
                    //Match match2 = regex2.Match(c);
                    
                    //if (match.Success || match2.Success)
                    //{
                    //    cardCount = int.Parse(match.Groups[1].Value);
                    //}

                    // If the card is a five-digit number, it came in like 58442 - add brackets around the numbers [58442]
                    if (cardName.Length == 5 && int.TryParse(cardName, out int cardId))
                    {
                        cardName = "[" + cardId + "]";
                    }


                    // Add to Card assault list
                    CardAssaultsAndStructures.Add(c);

                    // Add to CardsNotFound dictionary
                    if (!CardsNotFound.ContainsKey(cardName))
                    {
                        CardsNotFound.Add(cardName, cardCount);
                    }
                    else
                    {
                        CardsNotFound[cardName] += cardCount;
                    }

                    //if (match.Success || match2.Success)
                    //{
                    //    if (!CardsNotFound.ContainsKey(cardName))
                    //        CardsNotFound.Add(cardName, cardCount);
                    //    else
                    //        CardsNotFound[cardName] += cardCount;

                    //    deckCount += cardCount;
                    //}
                    //else
                    //{
                    //    if (!CardsNotFound.ContainsKey(cardName))
                    //        CardsNotFound.Add(cardName, 1);
                    //    else
                    //        CardsNotFound[cardName]++;
                    //    deckCount++;
                    //}
                }

                // Add the base
                Cards.Add(c);
            } //parse cards

            // Default commander if not found
            if (!HasCommander)
            {
                Commander = firstCard;

                List<string> tmpCards = new List<string>();
                tmpCards.Add(firstCard);
                tmpCards.AddRange(Cards);
                tmpCards = Cards;
            }

            // Create a sloppy sort string from the CardObjectsAssaultsAndStructures. This lets us sort the deck a little cleaner later
            foreach(var c in CardObjectsAssaultsAndStructures
                .OrderByDescending(x => x.Key.Rarity)
                .ThenByDescending(x => Math.Max(x.Key.Power/5, x.Key.FactionPower/5))
                .ThenByDescending(x => x.Key.Section)
                .ThenBy(x => x.Key.Name)
                .ToDictionary(t => t.Key, t => t.Value))
                //.OrderByDescending(x => (int)(x.Key.Power/5)) // Sort really powerful cards first
                //.ThenByDescending(x => x.Key.Rarity)
                //.ThenBy(x => x.Key.Name))
            {
                Card card = c.Key;
                // Higher rarity, and higher card count = better, so flipping the rarity/value integers 
                SloppySort += (7 - card.Rarity) + "" + card.Name[0] + "" + Math.Max(3 - c.Value, 0) + " ";
            }

            // Get deck size
            DeckCount = deckCount;
        }

        #endregion
        

        /// <summary>
        /// Gets this Deck in a string format
        /// </summary>
        public string DeckToString(bool includeCommander=true, bool includeDominion=true)
        {
            StringBuilder result = new StringBuilder();

            if (includeCommander && HasCommander)
            {
                result.Append(Commander);
                result.Append(", ");
            }
            if (includeDominion && HasDominion)
            {
                result.Append(Dominion);
                result.Append(", ");
            }

            foreach(var card in CardAssaultsAndStructures)
            {
                result.Append(card);
                result.Append(", ");
            }

            // Trim the last ", "
            if (result.Length > 2) result = result.Remove(result.Length - 2, 2);

            return result.ToString();
        }

        /// <summary>
        /// Modify a deck if it has a size limit (-L 7 8)
        /// If it has too many cards, trim it
        /// If it has too few, add Infantry
        /// </summary>
        public void FillDeck(int low, int high)
        {
            if (high > low) return;

            if (DeckCount - 1 < low)
            {
                int cardsToAdd = low - DeckCount - 1;
                for (int i = 0; i < cardsToAdd; i++)
                {
                    CardAssaultsAndStructures.Add("Infantry");
                }
            }
            //TODO: This will mess up if the last card is something like "HyperSec Hunter#4"
            if (DeckCount + 1 > high)
            {
                int cardsToRemove = DeckCount - high;
                for (int i = 1; i < cardsToRemove; i++)
                {
                    CardAssaultsAndStructures.RemoveAt(DeckCount - i);
                    DeckCount--;
                }
                
            }
        }
    }
}
