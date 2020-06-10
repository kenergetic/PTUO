using PirateTUO2.Classes;
using PirateTUO2.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PirateTUO2.Modules
{
    /// <summary>
    /// This class is responsible for creating seeds
    /// 
    /// TODO: Rupture deck
    /// 
    /// A blend of Player/CardManager responsibilities
    /// </summary>
    public class SeedManager
    {
        /// <summary>
        /// Create seeds for a player
        /// 
        /// Faction
        /// </summary>
        public static void GeneratePlayerSeeds(Player player)
        {
            if (player != null)
            {
                try
                {
                    player.Seeds.Clear();

                    var playerCards = player.Cards;

                    // Sort faction decks by power
                    var factionPower = new int[6] { 0, 0, 0, 0, 0, 0 };
                    var power = 0;

                    var impDeck = GetFactionDeck(playerCards, Faction.Imperial, ref factionPower[0]);
                    var raiderDeck = GetFactionDeck(playerCards, Faction.Raider, ref factionPower[1]);
                    var btDeck = GetFactionDeck(playerCards, Faction.Bloodthirsty, ref factionPower[2]);
                    var xenoDeck = GetFactionDeck(playerCards, Faction.Xeno, ref factionPower[3]);
                    var rtDeck = GetFactionDeck(playerCards, Faction.Righteous, ref factionPower[4]);
                    //var progDeck = GetFactionDeck(playerCards, Faction.Progen, ref factionPower[5]);

                    var powerDeck = GetPowerDeck(playerCards, ref power);
                    var tempoDeck = GetTempoDeck(playerCards);

                    //var summonDeck = GetSummonDeck(playerCards);
                    //var strikeDeck = GetStrikeDeck(playerCards);
                    //var huntDeck = GetHuntDeck(playerCards);
                    //var sunderDeck = GetSunderDeck(playerCards);
                    //var entrapDeck = GetEntrapDeck(playerCards);
                    //var ruptureDeck = GetRuptureDeck(playerCards);
                    //var mortarDeck = GetMortarDeck(playerCards);
                    //var revengeDeck = GetRevengeDeck(playerCards);
                    //var coaDeck = GetCoaDeck(playerCards);

                    // Get the 3 strongest factions
                    // this bit of code is a cosmic crime
                    var firstPower = -1;
                    var firstIndex = 0;
                    var secondPower = -1;
                    var secondIndex = 0;
                    var thirdPower = -1;
                    var thirdIndex = 0;

                    for (int i = 0; i < factionPower.Length; i++)
                    {
                        var currentPower = factionPower[i];

                        if (currentPower > firstPower)
                        {
                            // 2 -> 3
                            thirdPower = secondPower;
                            thirdIndex = secondIndex;

                            // 1 -> 2
                            secondPower = firstPower;
                            secondIndex = firstIndex;

                            // new -> 1
                            firstPower = currentPower;
                            firstIndex = i;
                        }
                        else if (currentPower > secondPower)
                        {
                            // 2 -> 3
                            thirdPower = secondPower;
                            thirdIndex = secondIndex;

                            // new -> 2
                            secondPower = currentPower;
                            secondIndex = i;
                        }
                        else if (currentPower > thirdPower)
                        {
                            thirdPower = currentPower;
                            thirdIndex = i;
                        }
                    }


                    // General decks
                    player.Seeds.Add(GetDeckString("//Power", powerDeck));
                    player.Seeds.Add(GetDeckString("//Tempo", tempoDeck));

                    // --------------------
                    // FACTION DECKS (PROGRAMMED)
                    // --------------------
                    if (true)
                    {
                        if (firstIndex == 0) player.Seeds.Add(GetDeckString("//FactionPower", impDeck));
                        else if (firstIndex == 1) player.Seeds.Add(GetDeckString("//FactionPower", raiderDeck));
                        else if (firstIndex == 2) player.Seeds.Add(GetDeckString("//FactionPower", btDeck));
                        else if (firstIndex == 3) player.Seeds.Add(GetDeckString("//FactionPower", xenoDeck));
                        else if (firstIndex == 4) player.Seeds.Add(GetDeckString("//FactionPower", rtDeck));

                        if (secondIndex == 0) player.Seeds.Add(GetDeckString("//FactionPower2", impDeck));
                        else if (secondIndex == 1) player.Seeds.Add(GetDeckString("//FactionPower2", raiderDeck));
                        else if (secondIndex == 2) player.Seeds.Add(GetDeckString("//FactionPower2", btDeck));
                        else if (secondIndex == 3) player.Seeds.Add(GetDeckString("//FactionPower2", xenoDeck));
                        else if (secondIndex == 4) player.Seeds.Add(GetDeckString("//FactionPower2", rtDeck));

                        //if (thirdIndex == 0) player.Seeds.Add(GetDeckString("//PowerFaction3", impDeck));
                        //else if (thirdIndex == 1) player.Seeds.Add(GetDeckString("//PowerFaction3", raiderDeck));
                        //else if (thirdIndex == 2) player.Seeds.Add(GetDeckString("//PowerFaction3", btDeck));
                        //else if (thirdIndex == 3) player.Seeds.Add(GetDeckString("//PowerFaction3", xenoDeck));
                        //else if (thirdIndex == 4) player.Seeds.Add(GetDeckString("//PowerFaction3", rtDeck));
                    }

                    // --------------------
                    // PRESET SEED DECKS
                    // --------------------
                    {
                        // For each seed, check if it exists. If it does, use that seed. If not, use an alternate deck
                        foreach (string seedName in CONSTANTS.SEEDS_FROM_CONFIG)
                        {
                            var seedDeck = CONFIG.PlayerConfigSeeds.Where(x => x.Item1 == player.KongName && x.Item2 == seedName).FirstOrDefault();

                            if (seedDeck != null)
                                player.Seeds.Add("//Seed-" + seedName + ": " + seedDeck.Item3);
                            else
                                player.Seeds.Add(GetDeckString("//Seed-" + seedName, powerDeck));
                        }
                    }

                    // Skill decks
                    //player.Seeds.Add(GetDeckString("//Skill-Strike", strikeDeck));
                    //player.Seeds.Add(GetDeckString("//Skill-Summon", summonDeck));
                    //player.Seeds.Add(GetDeckString("//Skill-Hunt", summonDeck));
                    //player.Seeds.Add(GetDeckString("//Skill-Sunder", sunderDeck));
                    //player.Seeds.Add(GetDeckString("//Skill-Rupture", ruptureDeck));
                    //player.Seeds.Add(GetDeckString("//Skill-Coalition", coaDeck));
                    //player.Seeds.Add(GetDeckString("//Skill-Entrap", entrapDeck));
                    //player.Seeds.Add(GetDeckString("//Skill-Mortar", mortarDeck));
                    //player.Seeds.Add(GetDeckString("//Skill-Revenge", revengeDeck));

                    // Faction decks
                    player.Seeds.Add(GetDeckString("//Faction-Imperial", impDeck));
                    player.Seeds.Add(GetDeckString("//Faction-Raider", raiderDeck));
                    player.Seeds.Add(GetDeckString("//Faction-Bloodthirsty", btDeck));
                    player.Seeds.Add(GetDeckString("//Faction-Xeno", xenoDeck));
                    player.Seeds.Add(GetDeckString("//Faction-Righteous", rtDeck));
                    //player.Seeds.Add(GetDeckString("//Faction-Prog", progDeck));

                }
                catch(Exception ex)
                {
                    Console.WriteLine("Error in GeneratePlayerSeeds() on player " + player.KongName + ": " + ex.Message);
                }
            }
        }

        /// <summary>
        /// This calls GeneratePlayerSeeds, but replaces the Power with the appropriate faction
        /// </summary>
        public static void GenerateFactionPlayerSeeds(Player player, int faction)
        {
            GeneratePlayerSeeds(player);

            // 0 faction (rainbow) skip this process
            if (faction == 0) return;

            // Remove existing powerseed
            var powerSeed = player.Seeds.Where(x => x.StartsWith("//Power:")).FirstOrDefault();
            if (powerSeed != null)
                player.Seeds.Remove(powerSeed);

            
            var factionSeed = player.Seeds.Where(x => x.StartsWith("//" + ((Faction)faction) + ":")).FirstOrDefault();
            factionSeed = factionSeed.Replace(((Faction)faction) + ":", "Power:");

            player.Seeds.Add(factionSeed);

        }

        /// <summary>
        /// Build a power deck
        /// Get big powerful cards. Rawr
        /// 
        /// Also returns the combined power
        /// </summary>
        public static List<Card> GetPowerDeck(ConcurrentDictionary<Card, int> playerCards, ref int power, int cardsToAdd = 6)
        {
            var seedDeck = new List<Card>();
            var powerCards = CardManager.GetPlayerCardsByPower(playerCards);
            var cardsInDeck = 0;

            try
            {
                // Commander
                seedDeck.Add(CardManager.GetBestCommander(playerCards));

                // 7 strongest power cards, no fancy stuff
                foreach (var c in powerCards.OrderByDescending(x => x.Key.Power))
                {
                    var currentCard = c.Key;
                    var currentCardCount = c.Value;

                    for (int i = 0; i < currentCardCount; i++)
                    {
                        seedDeck.Add(currentCard);
                        cardsInDeck++;
                        if (cardsInDeck >= 8) break;
                        continue; //No repeat
                    }
                    powerCards[currentCard] = 0;
                    if (cardsInDeck >= 8) break;
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine("Exception on GetPowerDeck(): " + ex);
            }

            return seedDeck;
        }

        /// <summary>
        /// Build a faction deck
        /// * 1 leader card (things with Stasis/Allegiance) 
        /// * 3 factionPower cards
        /// * 4 power cards within faction or progen
        /// 
        /// Also returns the combined factionPower
        /// </summary>
        private static List<Card> GetFactionDeck(ConcurrentDictionary<Card, int> playerCards, Faction faction, ref int seedDeckPower)
        {
            var seedDeck = new List<Card>();
            seedDeckPower = 0;
            Card currentCard;
            int cardsInDeck = 0;
            int currentCardCount = 0;

            try
            {
                var factionCards = CardManager.GetPlayerCardsByFaction(playerCards, faction, false);
                var powerCards = CardManager.GetPlayerCardsByPower(playerCards);
                var leaderCards = CardManager.GetPlayerCardsBySkills(factionCards, new List<string> { "Stasis", "Allegiance" });

                // Get commander
                seedDeck.Add(CardManager.GetBestCommander(playerCards, faction: faction));

                // Add 1 unique "leader" faction card
                foreach (var c in leaderCards.OrderByDescending(x => x.Key.FactionPower))
                {
                    currentCard = c.Key;
                    currentCardCount = c.Value;

                    if (currentCardCount > 0)
                    {
                        seedDeck.Add(currentCard);
                        seedDeckPower += currentCard.FactionPower;
                        cardsInDeck++;

                        // Remove this card from each card list
                        if (powerCards.ContainsKey(currentCard)) powerCards[currentCard]--;
                        if (leaderCards.ContainsKey(currentCard)) leaderCards[currentCard]--;
                        if (factionCards.ContainsKey(currentCard)) factionCards[currentCard]--;
                        
                        break;
                    }
                }

                // 3 faction cards, factionPower
                foreach (var c in factionCards.OrderByDescending(x => x.Key.FactionPower))
                {
                    currentCard = c.Key;
                    currentCardCount = c.Value;

                    for (int i = 0; i < currentCardCount; i++)
                    {
                        if (powerCards.ContainsKey(currentCard)) powerCards[currentCard]--;
                        if (leaderCards.ContainsKey(currentCard)) leaderCards[currentCard]--;
                        if (factionCards.ContainsKey(currentCard)) factionCards[currentCard]--;

                        seedDeck.Add(currentCard);
                        cardsInDeck++;
                        if (cardsInDeck >= 4) break;
                        //continue; //No repeat
                    }

                    if (cardsInDeck >= 4) break;
                }

                // 3 faction cards, power
                foreach (var c in factionCards.OrderByDescending(x => x.Key.Power))
                {
                    currentCard = c.Key;
                    currentCardCount = c.Value;

                    for (int i = 0; i < currentCardCount; i++)
                    {
                        if (powerCards.ContainsKey(currentCard)) powerCards[currentCard]--;
                        if (leaderCards.ContainsKey(currentCard)) leaderCards[currentCard]--;
                        if (factionCards.ContainsKey(currentCard)) factionCards[currentCard]--;

                        seedDeck.Add(currentCard);
                        cardsInDeck++;
                        if (cardsInDeck >= 4) break;
                        //continue; //No repeat
                    }

                    if (cardsInDeck >= 7) break;
                }

                // Add misc other cards
                if (cardsInDeck < 8)
                {
                    foreach (var c in powerCards.OrderByDescending(c => c.Key.Power))
                    {
                        currentCard = c.Key;
                        currentCardCount = c.Value;

                        for (int i = 0; i < currentCardCount; i++)
                        {
                            seedDeck.Add(currentCard);
                            seedDeckPower += currentCard.FactionPower;
                            cardsInDeck++;
                            if (cardsInDeck >= 8) break;
                        }

                        if (cardsInDeck >= 8) break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception on GetFactionDeck(): " + ex);
            }

            return seedDeck;
        }
        
        /// <summary>
        /// Build a fast jam/tempo deck
        /// </summary>
        private static List<Card> GetTempoDeck(ConcurrentDictionary<Card, int> playerCards)
        {
            var seedDeck = new List<Card>();
            var cardsInDeck = 0;
            //var currentSkill = "";
            Card currentCard;
            var currentCardCount = 0;
            var powerCards = CardManager.GetPlayerCardsByPower(playerCards);

            // Set Commander
            seedDeck.Add(CardManager.GetBestCommander(playerCards, strategy: "coalition"));

            var skillCards = CardManager.GetPlayerCardsBySkills(playerCards, new List<string> { "Jam" });


            // 3 fast power cards
            foreach (var c in powerCards.OrderByDescending(x => x.Key.Power))
            {
                for (int i = 0; i < c.Value; i++)
                {
                    currentCard = c.Key;
                    currentCardCount = c.Value;
                    if (currentCardCount <= 0) continue;
                    if (currentCard.Delay > 2) continue;

                    seedDeck.Add(currentCard);

                    // Remove this card from each card list
                    if (powerCards.ContainsKey(currentCard)) powerCards[currentCard]--;
                    if (skillCards.ContainsKey(currentCard)) skillCards[currentCard]--;

                    cardsInDeck++;
                    if (cardsInDeck >= 3) break;
                }
                if (cardsInDeck >= 3) break;
            }

            // 1 fast jam card - on-play, or 0-1 delay
            foreach (var c in skillCards.OrderByDescending(x => x.Key.Power))
            {
                for (int i = 0; i < c.Value; i++)
                {
                    currentCard = c.Key;
                    currentCardCount = c.Value;
                    if (currentCardCount <= 0) continue;

                    // Check for nice jams
                    bool addThisCard = false;

                    if (currentCard.s1.id == "Jam" && currentCard.s1.trigger == "Play") addThisCard = true;
                    else if (currentCard.s2.id == "Jam" && currentCard.s2.trigger == "Play") addThisCard = true;
                    else if (currentCard.s3.id == "Jam" && currentCard.s3.trigger == "Play") addThisCard = true;
                    else if (currentCard.Delay <= 1) addThisCard = true;

                    if (!addThisCard) continue;

                    seedDeck.Add(currentCard);

                    // Remove this card from each card list
                    if (powerCards.ContainsKey(currentCard)) powerCards[currentCard]--;
                    if (skillCards.ContainsKey(currentCard)) skillCards[currentCard]--;

                    cardsInDeck++;
                    if (cardsInDeck >= 4) break;
                }
                if (cardsInDeck >= 4) break;
            }

            // 3 fast power cards
            foreach (var c in powerCards.OrderByDescending(x => x.Key.Power))
            {
                for (int i = 0; i < c.Value; i++)
                {
                    currentCard = c.Key;
                    currentCardCount = c.Value;
                    if (currentCardCount <= 0) continue;
                    if (currentCard.Delay > 2) continue;

                    seedDeck.Add(currentCard);

                    // Remove this card from each card list
                    if (powerCards.ContainsKey(currentCard)) powerCards[currentCard]--;
                    if (skillCards.ContainsKey(currentCard)) skillCards[currentCard]--;

                    cardsInDeck++;
                    if (cardsInDeck >= 7) break;
                }
                if (cardsInDeck >= 7) break;
            }

            // power cards to fill out the deck
            foreach (var c in powerCards.OrderByDescending(c => c.Key.Power))
            {
                for (int i = 0; i < c.Value; i++)
                {
                    currentCard = c.Key;
                    currentCardCount = c.Value;
                    if (currentCardCount <= 0) continue;
                    seedDeck.Add(currentCard);

                    // Remove this card from each card list
                    if (powerCards.ContainsKey(currentCard)) powerCards[currentCard]--;

                    cardsInDeck++;
                    if (cardsInDeck >= 7) break;
                }
                if (cardsInDeck >= 7) break;
            }

            return seedDeck;
        }

        /// <summary>
        /// Build a summon deck
        /// </summary>
        private static List<Card> GetSummonDeck(ConcurrentDictionary<Card, int> playerCards)
        {
            var seedDeck = new List<Card>();
            var cardsInDeck = 0;
            var currentSkill = "";
            Card currentCard;
            var currentCardCount = 0;
            var powerCards = CardManager.GetPlayerCardsByPower(playerCards);

            // Set Commander
            seedDeck.Add(CardManager.GetBestCommander(playerCards));

            var skillCards = CardManager.GetPlayerCardsBySkills(playerCards, new List<string> { "Summon" });


            // 5 Summon cards - only 0-2 delays or on-play summons
            currentSkill = "Summon";
            foreach (var c in skillCards.OrderByDescending(x => x.Key.Power))
            {
                for (int i = 0; i < c.Value; i++)
                {
                    currentCard = c.Key;
                    currentCardCount = c.Value;
                    
                    // Reject 3+ delay cards unless they summon on play
                    if (currentCard.s1.trigger != "Play" && currentCard.s2.trigger != "Play" && currentCard.s3.trigger != "Play" && currentCard.Delay >= 3) continue;

                    // Summoncards: Accept summons that don't summon structures
                    try
                    {
                        string summonUnit = "";
                        if (!string.IsNullOrEmpty(currentCard.s1.summon)) summonUnit = currentCard.s1.summon;
                        else if (!string.IsNullOrEmpty(currentCard.s2.summon)) summonUnit = currentCard.s2.summon;
                        else if (!string.IsNullOrEmpty(currentCard.s3.summon)) summonUnit = currentCard.s3.summon;
                        else continue;

                        Card summonCard = CardManager.GetById(summonUnit);
                        if (summonCard != null && summonCard.CardType == CardType.Structure.ToString()) continue;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("GetSummonDeck(): Error reading what this card summons: Card " + currentCard.Name + ": "  + ex.Message);
                    }
                    

                    if (currentCardCount <= 0) continue;
                    if (c.Key.s1.id != currentSkill && c.Key.s2.id != currentSkill && c.Key.s3.id != currentSkill) continue;
                    if (currentCard.CardType == CardType.Structure.ToString()) continue;

                    seedDeck.Add(currentCard);

                    // Remove this card from each card list
                    if (powerCards.ContainsKey(currentCard)) powerCards[currentCard]--;
                    if (skillCards.ContainsKey(currentCard)) skillCards[currentCard]--;

                    cardsInDeck++;
                    if (cardsInDeck >= 5) break;
                }
                if (cardsInDeck >= 5) break;
            }

            // add power cards to fill out the deck
            foreach (var c in powerCards.OrderByDescending(c => c.Key.Power))
            {
                for (int i = 0; i < c.Value; i++)
                {
                    currentCard = c.Key;
                    currentCardCount = c.Value;
                    if (currentCardCount <= 0) continue;
                    seedDeck.Add(currentCard);

                    // Remove this card from each card list
                    if (powerCards.ContainsKey(currentCard)) powerCards[currentCard]--;

                    cardsInDeck++;
                    if (cardsInDeck >= 8) break;
                }
                if (cardsInDeck >= 8) break;
            }

            return seedDeck;
        }

        /// <summary>
        /// Build a strike deck - jam heavy
        /// </summary>
        private static List<Card> GetStrikeDeck(ConcurrentDictionary<Card, int> playerCards)
        {
            var seedDeck = new List<Card>();
            var cardsInDeck = 0;
            var currentSkill = "";
            Card currentCard;
            var currentCardCount = 0;
            var powerCards = CardManager.GetPlayerCardsByPower(playerCards);

            // Set Commander
            seedDeck.Add(CardManager.GetBestCommander(playerCards, strategy:"strike"));

            var skillCards = CardManager.GetPlayerCardsBySkills(playerCards, new List<string> { "Strike", "Enfeeble", "Overload", "Jam", "Counter" });


            // 1 overload card
            currentSkill = "Overload";
            foreach (var c in skillCards.OrderByDescending(x => x.Key.Power))
            {
                for (int i = 0; i < c.Value; i++)
                {
                    currentCard = c.Key;
                    currentCardCount = c.Value;
                    if (currentCardCount <= 0) continue;
                    if (c.Key.s1.id != currentSkill && c.Key.s2.id != currentSkill && c.Key.s3.id != currentSkill) continue;

                    seedDeck.Add(currentCard);

                    // Remove this card from each card list
                    if (powerCards.ContainsKey(currentCard)) powerCards[currentCard]--;
                    if (skillCards.ContainsKey(currentCard)) skillCards[currentCard]--;

                    cardsInDeck++;
                    if (cardsInDeck >= 1) break;
                }
                if (cardsInDeck >= 1) break;
            }

            // 1 enfeeble (assault) card
            currentSkill = "Enfeeble";
            foreach (var c in skillCards.OrderByDescending(x => x.Key.Power))
            {
                for (int i = 0; i < c.Value; i++)
                {
                    currentCard = c.Key;
                    currentCardCount = c.Value;
                    if (currentCardCount <= 0) continue;
                    if (c.Key.s1.id != currentSkill && c.Key.s2.id != currentSkill && c.Key.s3.id != currentSkill) continue;
                    if (currentCard.CardType == CardType.Structure.ToString()) continue;

                    seedDeck.Add(currentCard);

                    // Remove this card from each card list
                    if (powerCards.ContainsKey(currentCard)) powerCards[currentCard]--;
                    if (skillCards.ContainsKey(currentCard)) skillCards[currentCard]--;

                    cardsInDeck++;
                    if (cardsInDeck >= 2) break;
                }
                if (cardsInDeck >= 2) break;
            }

            // 2 strike (assault) card
            currentSkill = "Strike";
            foreach (var c in skillCards.OrderByDescending(x => x.Key.Power))
            {
                for (int i = 0; i < c.Value; i++)
                {
                    currentCard = c.Key;
                    currentCardCount = c.Value;
                    if (currentCardCount <= 0) continue;
                    if (c.Key.s1.id != currentSkill && c.Key.s2.id != currentSkill && c.Key.s3.id != currentSkill) continue;
                    if (currentCard.CardType == CardType.Structure.ToString()) continue;

                    seedDeck.Add(currentCard);

                    // Remove this card from each card list
                    if (powerCards.ContainsKey(currentCard)) powerCards[currentCard]--;
                    if (skillCards.ContainsKey(currentCard)) skillCards[currentCard]--;

                    cardsInDeck++;
                    if (cardsInDeck >= 4) break;
                }
                if (cardsInDeck >= 4) break;
            }


            // 2 jam - on-play, or 0-1 delay
            foreach (var c in skillCards.OrderByDescending(x => x.Key.Power))
            {
                for (int i = 0; i < c.Value; i++)
                {
                    currentCard = c.Key;
                    currentCardCount = c.Value;
                    if (currentCardCount <= 0) continue;

                    // Check for nice jams
                    bool addThisCard = false;

                    if (currentCard.s1.id == "Jam" && currentCard.s1.trigger == "Play") addThisCard = true;
                    else if (currentCard.s2.id == "Jam" && currentCard.s2.trigger == "Play") addThisCard = true;
                    else if (currentCard.s3.id == "Jam" && currentCard.s3.trigger == "Play") addThisCard = true;
                    else if (currentCard.Delay <= 1) addThisCard = true;

                    if (!addThisCard) continue;

                    seedDeck.Add(currentCard);

                    // Remove this card from each card list
                    if (powerCards.ContainsKey(currentCard)) powerCards[currentCard]--;
                    if (skillCards.ContainsKey(currentCard)) skillCards[currentCard]--;

                    cardsInDeck++;
                    if (cardsInDeck >= 6) break;
                }
                if (cardsInDeck >= 6) break;
            }

            // add power cards to fill out the deck
            foreach (var c in powerCards.OrderByDescending(c => c.Key.Power))
            {
                for (int i = 0; i < c.Value; i++)
                {
                    currentCard = c.Key;
                    currentCardCount = c.Value;
                    if (currentCardCount <= 0) continue;
                    seedDeck.Add(currentCard);

                    // Remove this card from each card list
                    if (powerCards.ContainsKey(currentCard)) powerCards[currentCard]--;

                    cardsInDeck++;
                    if (cardsInDeck >= 7) break;
                }
                if (cardsInDeck >= 7) break;
            }

            return seedDeck;
        }



        /// <summary>
        /// Given a list of cards, returns a deck string with the attached name
        /// (Example: "RecKening: (CardA, CardB, CardC, ...))
        /// </summary>
        private static string GetDeckString(string name, List<Card> cards)
        {
            var deckList = new StringBuilder();
            deckList.Append(name);
            deckList.Append(":");

            foreach (var card in cards.Select(x => x.Name).ToList())
            {
                deckList.Append(card);
                deckList.Append(",");
            }

            return deckList.ToString();

        }

    }



}
