using PirateTUO2.Classes;
using PirateTUO2.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace PirateTUO2.Modules
{
    // ** TODO: Switch CardTable key/value - many cards have the same name ** 

    /// <summary>
    /// Parses XML cards and common card functions
    /// </summary>
    public class CardManager
    {
        // This string tracks errors on first load
        public static string status = "";

        // Int based dictionary, as many cards have the same name
        private static ConcurrentDictionary<int, Card> CardTable = new ConcurrentDictionary<int, Card>();

        // String based dictionary, using a cards formatted name (lowercase, no spacing)
        // - This should only contain cards players could get to reduce duplicates
        // - We do a lot of string based lookups which the main cardTable struggles with
        public static ConcurrentDictionary<string, Card> PlayerCardTable = new ConcurrentDictionary<string, Card>();



        /// <summary>
        /// ** MAIN METHOD **
        /// Construct the card database
        /// </summary>
        public async static Task<string> BuildCardDatabase(MainForm form)
        {
            int threads = 6; 

            // Erase the card table
            CardTable.Clear();
            PlayerCardTable.Clear();

            ParallelOptions options = new ParallelOptions { MaxDegreeOfParallelism = threads };

            // -----------------------------------------------
            // Parse the card XMLs for cards - and create a dictionary of Card objects
            // (cards_section_i.xml)
            // -----------------------------------------------  
            Parallel.For(1, CONSTANTS.NEWEST_SECTION + 1, options, i =>
            {
                XElement doc = XElement.Load("./data/cards_section_" + i + ".xml");
                IEnumerable<XElement> units = doc.Elements();

                try
                {
                    // Each <unit> element represents one unit. Units typically have multiple levels, and we would create x cards
                    foreach (var unit in units)
                    {
                        List<Card> cards = CreateCards_ParseCardXml(unit, i);
                        foreach (Card c in cards)
                        {
                            // Add to main cardTable
                            CardTable.TryAdd(c.CardId, c);
                        }
                    }
                }
                catch (Exception ex)
                {
                    status += MethodInfo.GetCurrentMethod() + " - Tried to parse card and failed: " + ex.Message;
                }
            });

            // -----------------------------------------------
            // Add power metadata to the cardTable cards
            // - card subset data 
            // - card fusion paths
            // -----------------------------------------------   
            try
            {
                CreateCards_ParseFusionXml();
            }
            catch (Exception ex)
            {
                Console.WriteLine(MethodInfo.GetCurrentMethod() + " - Tried to get card fusions and failed: " + ex.Message);
            }

            // Build whitelists (cards from all sections older then the current card section (e.g. cards_section_15) are blacklisted from player inventories - 
            // except those specified below
            try
            {
                BuildCardDatabase_BuildWhitelists(form);
            }
            catch (Exception ex)
            {
                status += "BuildCardDatabase(): BuildWhitelists() failed: " + ex.Message + "\r\n";
            }

            // Assign relative power to cards. This 'power' metric is used to help construct player deck seeds
            try
            {
                // Get card skillPower variables
                try
                {
                    List<string> settings = FileIO.SimpleRead(form, "config/cardpower.txt", returnCommentedLines: true);
                    List<string> cardSkillPower = settings.Where(x => x.StartsWith("skillPower:")).ToList();

                    foreach (string skillPower in cardSkillPower)
                    {
                        string[] s = skillPower.Split(new char[] { '=', ':' });
                        if (s.Length == 3)
                        {
                            string name = s[1].Trim();
                            int value = int.Parse(s[2].Trim());

                            if (!CONFIG.CardSkillPower.ContainsKey(name))
                                CONFIG.CardSkillPower.Add(name, value);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Helper.OutputWindowMessage(form, ex.Message, "BuildFromCardDatabase() error: Could not find config/cardPower lines.\r\n" + ex.Message);
                }



                BuildCardDatabase_GetCardPower(form);
                await Task.Delay(5);
            }
            catch (Exception ex)
            {
                status += "BuildCardDatabase(): GetCardPower() failed: " + ex.Message + "\r\n";
            }

            // Add player cards to playerCardTable
            foreach(var cardPair in CardTable.OrderBy(x => x.Key))
            {
                Card card = cardPair.Value;

                if (!PlayerCardTable.ContainsKey(card.FormattedName) && card.Set != 6000 && card.Set != 9999)
                {
                    PlayerCardTable.TryAdd(card.FormattedName, card);
                }
            }

            status += "Success - " + CardTable.Count + " cards";
            return status;

        }


        /// <summary>
        /// Creates a list of card names that match search filters, then refreshes the cardfinder tab
        /// </summary>
        public static void FilterCards(MainForm form)
        {
            int powerLow = 0;
            int powerHigh = 999;
            int factionPowerLow = 0;
            int factionPowerHigh = 999;

            Int32.TryParse(form.cardFinderPowerLowUpDown.Text, out powerLow);
            Int32.TryParse(form.cardFinderPowerHighUpDown.Text, out powerHigh);
            Int32.TryParse(form.cardFinderFactionPowerLowUpDown.Text, out factionPowerLow);
            Int32.TryParse(form.cardFinderFactionPowerHighUpDown.Text, out factionPowerHigh);


            // Filter on skills
            var skill = form.cardFinderSkillTextBox.Text.Trim().ToLower();

            var filteredCards = CardManager.CardTable.Values.Where(c =>

                // Only max level cards
                (c.Level == c.MaxLevel) && 

                // Filter on fusion level
                ((c.Fusion_Level <= 0 && form.cardFinderUnfusedCheckbox.Checked) ||
                 (c.Fusion_Level == 1 && form.cardFinderDualCheckbox.Checked) ||
                 (c.Fusion_Level == 2 || c.Subset == CardSubset.Summon.ToString()) && form.cardFinderQuadCheckbox.Checked) &&

                //Filter on rarity
                ((!form.cardFinderEpicCheckbox.Checked && !form.cardFinderLegendCheckbox.Checked && !form.cardFinderVindCheckbox.Checked && !form.cardFinderMythicCheckbox.Checked) ||
                (c.Rarity == 3 && form.cardFinderEpicCheckbox.Checked) ||
                (c.Rarity == 4 && form.cardFinderLegendCheckbox.Checked) ||
                (c.Rarity == 5 && form.cardFinderVindCheckbox.Checked) ||
                (c.Rarity == 6 && form.cardFinderMythicCheckbox.Checked)) &&

                // Filter on section
                ((c.Section <= 14 && form.cardFinderSection1to14Checkbox.Checked) ||
                (c.Section == 15 && form.cardFinderSection15Checkbox.Checked) ||
                (c.Section == 16 && form.cardFinderSection16Checkbox.Checked) ||
                (c.Section == 17 && form.cardFinderSection17Checkbox.Checked) ||
                (c.Section == 18 && form.cardFinderSection18Checkbox.Checked) ||
                (c.Section == 19 && form.cardFinderSection19Checkbox.Checked) ||
                (c.Section == 20 && form.cardFinderSection19Checkbox.Checked) ||
                (c.Section == 21 && form.cardFinderSection19Checkbox.Checked)) &&

                // Filter on faction
                ((!form.cardFinderImpCheckbox.Checked && !form.cardFinderRaiderCheckbox.Checked && !form.cardFinderBtCheckbox.Checked &&
                 !form.cardFinderXenoCheckbox.Checked && !form.cardFinderRtCheckbox.Checked && !form.cardFinderProgenCheckbox.Checked) || //nothing's checked
                (c.Faction == 1 && form.cardFinderImpCheckbox.Checked) ||
                (c.Faction == 2 && form.cardFinderRaiderCheckbox.Checked) ||
                (c.Faction == 3 && form.cardFinderBtCheckbox.Checked) ||
                (c.Faction == 4 && form.cardFinderXenoCheckbox.Checked) ||
                (c.Faction == 5 && form.cardFinderRtCheckbox.Checked) ||
                (c.Faction == 6 && form.cardFinderProgenCheckbox.Checked)) &&

                // Filter on Structure / Assault / Commander (or try to)
                ((c.CardType == "Assault" && form.cardFinderAssaultCheckbox.Checked) ||
                (c.CardType == "Structure" && form.cardFinderStructureCheckbox.Checked) ||
                (c.CardType == "Commander" && form.cardFinderCommanderCheckbox.Checked) ||
                (c.CardType == "EventStructure" && form.cardFinderEventStructureCheckbox.Checked) ||
                (c.CardType == "Dominion" && form.cardFinderDominionCheckbox.Checked) ||
                (!form.cardFinderAssaultCheckbox.Checked && !form.cardFinderStructureCheckbox.Checked && !form.cardFinderCommanderCheckbox.Checked && !form.cardFinderEventStructureCheckbox.Checked && !form.cardFinderDominionCheckbox.Checked)) &&

                // Filter on card set
                ((c.Set == (int)CardSet.BoxOrReward && form.cardFinderSet2000Checkbox.Checked) ||
                (c.Set == (int)CardSet.Fusion && form.cardFinderSet2500Checkbox.Checked) ||
                (c.Set == (int)CardSet.GameOnly && form.cardFinderSet6000Checkbox.Checked) ||
                (c.Set == (int)CardSet.Commander && form.cardFinderSet7000Checkbox.Checked) ||
                (c.Set == (int)CardSet.Fortress && form.cardFinderSet8000Checkbox.Checked) ||
                (c.Set == (int)CardSet.Dominion && form.cardFinderSet8500Checkbox.Checked) ||
                (c.Set == (int)CardSet.Summon && form.cardFinderSet9500Checkbox.Checked) ||
                (c.Set == (int)CardSet.Unknown && form.cardFinderSet9999Checkbox.Checked) ||
                (c.Set != 2000 && c.Set != 2500 && c.Set != 6000 && c.Set != 7000 && c.Set != 8000 && c.Set != 8500 && c.Set != 9500 && c.Set != 9999 && form.cardFinderSetOtherCheckBox.Checked)) &&

                // Filter on card subset
                (
                    c.Set != 2000 ||
                    (c.Set == 2000 && c.Subset == "") ||
                    ((c.Subset == CardSubset.Box.ToString() || c.Subset == CardSubset.Cache.ToString()) && form.cardFinderBoxCheckbox.Checked) ||
                    (c.Subset == CardSubset.Chance.ToString() && form.cardFinderChanceCheckbox.Checked) ||
                    ((c.Subset == CardSubset.PvP_Reward.ToString() || c.Subset == CardSubset.PvE_PvP_Reward.ToString()) && form.cardFinderPvPRewardCheckbox.Checked) ||
                    ((c.Subset == CardSubset.PvE_Reward.ToString() || c.Subset == CardSubset.PvE_PvP_Reward.ToString()) && form.cardFinderPvERewardCheckbox.Checked)
                ) &&

                // Filter on skill
                (skill == "" ||
                 (c.s1.id != null && c.s1.id.ToLower() == skill) ||
                 (c.s2.id != null && c.s2.id.ToLower() == skill) ||
                 (c.s3.id != null && c.s3.id.ToLower() == skill)) &&

                // Filter on power
                (c.Power >= powerLow) &&
                (c.Power <= powerHigh) &&
                (c.FactionPower >= factionPowerLow) &&
                (c.FactionPower <= factionPowerHigh))

            .ToList();

            switch (form.cardFinderSortByComboBox.Text)
            {
                case "XML Order":
                    filteredCards = filteredCards.OrderByDescending(c => c.Section).ToList();
                    break;
                case "Name":
                    filteredCards = filteredCards.OrderBy(c => c.Name).ToList();
                    break;
                case "Faction":
                    filteredCards = filteredCards.OrderBy(c => c.Faction).ToList();
                    break;
                case "Power":
                    filteredCards = filteredCards.OrderByDescending(c => c.Power).ToList();
                    break;
                case "Faction Power":
                    filteredCards = filteredCards.OrderByDescending(c => c.FactionPower).ToList();
                    break;
                default:
                    break;
            }

            form.cardFinderCountLabel.Text = filteredCards.Count + " card(s)";


            form.cardFinderListView.Items.Clear();
            var cardNames = filteredCards.Select(c => c.Name).ToArray();

            for (int i = 0; i < filteredCards.Count; i++)
            {
                form.cardFinderListView.Items.Add(cardNames[i]);

                var faction = filteredCards[i].Faction;
                var color = Color.Black;
                switch (faction)
                {
                    case 1:
                        color = Color.Blue;
                        break;
                    case 2:
                        color = Color.Brown;
                        break;
                    case 3:
                        color = Color.Red;
                        break;
                    case 4:
                        color = Color.Black;
                        break;
                    case 5:
                        color = Color.DarkGray;
                        break;
                    case 6:
                        color = Color.DeepPink;
                        break;
                }

                form.cardFinderListView.Items[i].ForeColor = color;
            }

        }

        /// <summary>
        /// Only a couple things outside of CardManager should directly touch card table
        /// </summary>
        public static ConcurrentDictionary<int, Card> GetCardTable()
        {
            return CardTable;
        }

        #region Search Cards - Single

        /// <summary>
        /// Returns the card with the given cardId, or null
        /// </summary>
        public static Card GetById(int id)
        {
            return CardTable.ContainsKey(id) ? CardTable[id] : null;
        }
        public static Card GetById(string id)
        {
            // If it comes in as [39046], strip the []
            id = id.Replace("[", "").Replace("]", "");

            int.TryParse(id, out int idInt);
            return GetById(idInt);
        }

        /// <summary>
        /// Returns the card with the given cardId, or null
        /// </summary>
        public static Card GetPlayerCardByName(string name)
        {
            string originalName = name.Trim().Replace("!", ""); // remove the force flag (!) if its there
            string formattedName = originalName.Replace(" ", "").ToLower();
            string formattedNameLv1 = formattedName.Replace("-1", "");
            Card card = null;

            // Check player card table
            if (PlayerCardTable.ContainsKey(formattedName))
                return PlayerCardTable[formattedName];

            // Check without -1 (quad commanders)
            if (PlayerCardTable.ContainsKey(formattedNameLv1))
                return PlayerCardTable[formattedNameLv1];

            // Try full cardTable (slow lookup)
            card = CardTable.Values.Where(x => x.FormattedName == formattedName || x.FormattedName == formattedNameLv1).FirstOrDefault();

            Console.WriteLine("Did not find card " + originalName + " in PlayerCardTable.. checking CardTable");
            if (card == null) Console.WriteLine("**WARNING: Did not find this card in either CardTable**");

            return card;
        }

        /// <summary>
        /// Returns the strongest commander. Optionally, specify a faction to return a faction commander
        /// </summary>
        public static Card GetBestCommander(ConcurrentDictionary<Card, int> cards, Faction faction = Faction.Rainbow, string strategy = "")
        {

            // Default
            Card commander = CardManager.CardTable[1504]; // Daedalus
            if (cards == null || cards.Count == 0) return commander;

            // Duals
            var daed2 = CardManager.CardTable[25238];
            var barr2 = CardManager.CardTable[25250];
            var darius2 = CardManager.CardTable[25679];
            var gaia2 = CardManager.CardTable[25751];

            // Quad Commanders
            var hal3 = CardManager.CardTable[25227];
            var tab3 = CardManager.CardTable[25233];
            var daed3 = CardManager.CardTable[25239]; 
            var oct3 = CardManager.CardTable[25644];
            var cass3 = CardManager.CardTable[25656];
            var vex3 = CardManager.CardTable[25245];
            var barr3 = CardManager.CardTable[25251];
            var silus3 = CardManager.CardTable[25257];
            var yurich3 = CardManager.CardTable[25668];
            var darius3 = CardManager.CardTable[25680];
            var drac3 = CardManager.CardTable[25263];
            var pet3 = CardManager.CardTable[25269];
            var malort3 = CardManager.CardTable[25275];
            var queen3 = CardManager.CardTable[25692];
            var razo3 = CardManager.CardTable[25704];
            var krellus3 = CardManager.CardTable[25281];
            var nexor3 = CardManager.CardTable[25287];
            var kylen3 = CardManager.CardTable[25293];
            var vyv3 = CardManager.CardTable[25716];
            var kleave3 = CardManager.CardTable[25728];
            var alaric3 = CardManager.CardTable[25299];
            var const3 = CardManager.CardTable[25305];
            var ark3 = CardManager.CardTable[25311];
            var emp3 = CardManager.CardTable[25740];
            var gaia3 = CardManager.CardTable[25752];

            // Pick Darius over anything
            if (cards.ContainsKey(darius3))
                commander = darius3;
            else if (cards.ContainsKey(oct3))
                commander = oct3;
            else if (cards.ContainsKey(gaia3))
                commander = gaia3;
            else if (cards.ContainsKey(daed3))
                commander = daed3;
            else if (cards.ContainsKey(ark3))
                commander = ark3;
            else if (cards.ContainsKey(daed2))
                commander = daed2;

            // Pick a commander based on strategy
            if (strategy != "")
            {
                switch (strategy)
                {
                    case "strike":
                        if (cards.ContainsKey(barr3))
                            commander = barr3;
                        else if (cards.ContainsKey(barr2))
                            commander = barr2;
                        else if (cards.ContainsKey(nexor3))
                            commander = nexor3;
                        else if (cards.ContainsKey(pet3))
                            commander = pet3;
                        else if (cards.ContainsKey(gaia3))
                            commander = gaia3;
                        break;
                }
            }
            // Pick a commander based on faction 
            else if (faction != Faction.Rainbow)
            {
                switch ((int)faction)
                {
                    case 1:
                        if (cards.ContainsKey(tab3))
                            commander = tab3;
                        else if (cards.ContainsKey(cass3))
                            commander = cass3;
                        else if (cards.ContainsKey(hal3))
                            commander = hal3;

                        // Default
                        break;
                    case 2:
                        if (cards.ContainsKey(silus3))
                            commander = silus3;
                        else if (cards.ContainsKey(yurich3))
                            commander = yurich3;
                        else if (cards.ContainsKey(vex3))
                            commander = vex3;

                        // Default
                        break;
                    case 3:
                        if (cards.ContainsKey(drac3))
                            commander = drac3;
                        else if (cards.ContainsKey(queen3))
                            commander = queen3;
                        else if (cards.ContainsKey(malort3))
                            commander = malort3;

                        // Default
                        break;
                    case 4:
                        if (cards.ContainsKey(nexor3))
                            commander = nexor3;
                        else if (cards.ContainsKey(kleave3))
                            commander = kleave3;
                        else if (cards.ContainsKey(krellus3))
                            commander = krellus3;
                        else if (cards.ContainsKey(vyv3))
                            commander = vyv3;

                        // Default
                        break;
                    case 5:
                        if (cards.ContainsKey(alaric3))
                            commander = alaric3;
                        else if (cards.ContainsKey(emp3))
                            commander = emp3;
                        else if (cards.ContainsKey(const3))
                            commander = const3;

                        // Default
                        break;
                    default:
                        // Default
                        break;
                }
            }

            return commander;
        }

        /// <summary>
        /// Gets cards by delay
        /// </summary>
        public static ConcurrentDictionary<Card, int> GetPlayerCardsByDelay(ConcurrentDictionary<Card, int> cards, int min = 0, int max = 1)
        {
            var result = cards
                .Where(c => c.Key.CardType != CardType.Commander.ToString())
                .Where(c => c.Key.CardType != CardType.Dominion.ToString())
                .Where(c => (c.Key.Delay >= min && c.Key.Delay <= max))
                .OrderByDescending(c => c.Key.Power)
                .Take(10)
                .ToDictionary(t => t.Key, t => t.Value);
            var resultConcurrentDictionary = new ConcurrentDictionary<Card, int>(result);
            return resultConcurrentDictionary;
        }

        /// <summary>
        /// Gets cards by faction
        /// </summary>
        public static ConcurrentDictionary<Card, int> GetPlayerCardsByFaction(ConcurrentDictionary<Card, int> cards, Faction faction, bool includeProgens = false)
        {
            var result = cards
                .Where(c => c.Key.CardType != CardType.Commander.ToString())
                .Where(c => c.Key.CardType != CardType.Dominion.ToString())
                .Where(c => (c.Key.Faction == (int)faction) || (includeProgens && c.Key.Faction == (int)Faction.Progen))
                .OrderByDescending(c => c.Key.FactionPower)
                .Take(25)
                .ToDictionary(t => t.Key, t => t.Value);

            var resultConcurrentDictionary = new ConcurrentDictionary<Card, int>(result);
            return resultConcurrentDictionary;

        }

        /// <summary>
        /// Gets cards by power
        /// </summary>
        public static ConcurrentDictionary<Card, int> GetPlayerCardsByPower(ConcurrentDictionary<Card, int> cards)
        {
            var result = cards.OrderByDescending(c => c.Key.Power)
                .Where(c => c.Key.CardType != CardType.Commander.ToString())
                .Where(c => c.Key.CardType != CardType.Dominion.ToString())
                    .OrderByDescending(c => c.Key.Power)
                    .ToDictionary(t => t.Key, t => t.Value);

            var resultConcurrentDictionary = new ConcurrentDictionary<Card, int>(result);
            return resultConcurrentDictionary;
        }

        /// <summary>
        /// Gets cards by skill and minimum magnitude
        /// </summary>
        public static ConcurrentDictionary<Card, int> GetPlayerCardsBySkill(ConcurrentDictionary<Card, int> cards, string skill, int xThreshold = int.MinValue, int nThreshold = int.MinValue)
        {
            var result = cards
                .Where(c => c.Key.CardType != CardType.Commander.ToString())
                .Where(c => c.Key.CardType != CardType.Dominion.ToString())
                .Where(c => (c.Key.s1.id == skill && c.Key.s1.x >= xThreshold && c.Key.s1.n >= nThreshold) ||
                                    (c.Key.s2.id == skill && c.Key.s2.x >= xThreshold && c.Key.s2.n >= nThreshold) ||
                                    (c.Key.s3.id == skill && c.Key.s3.x >= xThreshold && c.Key.s3.n >= nThreshold))
                                    .ToDictionary(t => t.Key, t => t.Value);
            var resultConcurrentDictionary = new ConcurrentDictionary<Card, int>(result);
            return resultConcurrentDictionary;
        }

        /// <summary>
        /// Gets cards by skill and minimum magnitude
        /// </summary>
        public static ConcurrentDictionary<Card, int> GetPlayerCardsBySkillEvolve(ConcurrentDictionary<Card, int> cards, string skill)
        {
            var result = cards
                .Where(c => c.Key.CardType == CardType.Structure.ToString())
                .Where(c => c.Key.s1.skill2 == skill ||
                                    c.Key.s2.skill2 == skill ||
                                    c.Key.s3.skill2 == skill)
                .ToDictionary(t => t.Key, t => t.Value);
            var resultConcurrentDictionary = new ConcurrentDictionary<Card, int>(result);
            return resultConcurrentDictionary;
        }

        /// <summary>
        /// Gets cards by a grouping of skills
        /// </summary>
        public static ConcurrentDictionary<Card, int> GetPlayerCardsBySkills(ConcurrentDictionary<Card, int> cards, List<string> skills)
        {
            var result = cards
                .Where(c => c.Key.CardType != CardType.Commander.ToString())
                .Where(c => c.Key.CardType != CardType.Dominion.ToString())
                .Where(c => skills.Contains(c.Key.s1.id) ||
                                    skills.Contains(c.Key.s2.id) ||
                                    skills.Contains(c.Key.s3.id))
                .ToDictionary(t => t.Key, t => t.Value);
            var resultConcurrentDictionary = new ConcurrentDictionary<Card, int>(result);
            return resultConcurrentDictionary;
        }

        /// <summary>
        /// Gets cards by a grouping of skills
        /// </summary>
        public static Dictionary<Card, int> GetPlayerCardsBySkills(Dictionary<Card, int> cards, List<string> skills)
        {
            var result = cards
                .Where(c => c.Key.CardType != CardType.Commander.ToString())
                .Where(c => c.Key.CardType != CardType.Dominion.ToString())
                .Where(c => skills.Contains(c.Key.s1.id) ||
                                    skills.Contains(c.Key.s2.id) ||
                                    skills.Contains(c.Key.s3.id))
                .ToDictionary(t => t.Key, t => t.Value);

            return result;
        }

        // -- Older stuff --


        /// <summary>
        /// Returns a list of cards that end the game fast
        /// * Cards with Flurry
        /// * Cards with Enrage All
        /// * Cards with Rally All 
        /// </summary>
        public static List<Card> GetAggressiveCards()
        {
            return CardTable.Values
                .Where(c => c.CardType != CardType.Structure.ToString())
                .Where(c => c.CardType != CardType.Commander.ToString())
                .Where(c => c.CardType != CardType.Dominion.ToString())
                .Where(c => c.Section >= 10)
                .Where(c => c.Power >= 5 || c.FactionPower >= 7)
                .Where(c => (c.s1.id == "Summon" && c.s1.trigger == "Play") ||
                            ((c.s1.id == "Flurry" && c.s1.x >= 2 && c.Delay <= 2) || (c.s1.id == "Enrage" && c.s1.all) || c.s1.id == "Rally" && c.s1.all) ||
                            ((c.s1.id == "Flurry" && c.s1.x >= 2 && c.Delay <= 2) || (c.s2.id == "Enrage" && c.s2.all) || c.s2.id == "Rally" && c.s2.all) ||
                            ((c.s1.id == "Flurry" && c.s1.x >= 2 && c.Delay <= 2) || (c.s3.id == "Enrage" && c.s3.all) || c.s3.id == "Rally" && c.s3.all))
                .ToList();
        }



        /// <summary>
        /// OLD METHOD
        /// Get spam score cards using only free stuff
        /// </summary>
        public static List<Card> GetSpamscoreCardsF2p(int powerMinimum = 0)
        {
            // Get recent quads
            var spamCards = CardManager.CardTable.Values
                            .Where(x => x.Fusion_Level == 2)
                            .Where(x => x.Power >= powerMinimum || x.FactionPower >= powerMinimum)
                            .Where(x => x.Set == 2500 ||
                                        (x.Set == 2000 && x.Subset == CardSubset.PvE_PvP_Reward.ToString()) ||
                                        (x.Set == 2000 && x.Subset == CardSubset.PvE_Reward.ToString()) ||
                                        (x.Set == 2000 && x.Subset == CardSubset.PvP_Reward.ToString()))

                            // Some nonf2p cards that slipped in
                            .Where(x => x.Name != "Spoiled Cadaver")


                            .ToList();

            return spamCards;
        }

        /// <summary>
        /// OLD METHOD
        /// Gets a combinatorical list of spamscore cards
        /// </summary>
        /// <returns></returns>
        public static List<Card> GetSpamscoreCards(int powerMinimum = 5)
        {
            // Get recent quads
            var spamCards = CardManager.CardTable.Values
                            .Where(x => x.Fusion_Level == 2)
                            .Where(x => x.CardType != CardType.Dominion.ToString())
                            .Where(x => x.CardType != CardType.Commander.ToString())
                            .Where(x => x.Power >= powerMinimum || x.FactionPower >= powerMinimum)
                            .Where(x => x.Section == CONSTANTS.NEWEST_SECTION || CONSTANTS.whitelistLevel1.Contains(x.Name))
                            .Select(x => x)
                            .ToList();

            return spamCards;
        }

        /// <summary>
        /// Returns high power or recent mythics and high power chance cards
        /// </summary>
        /// <returns></returns>
        public static List<Card> GetWhaleCards()
        {
            List<Card> cards = CardTable.Values
                .Where(x => x.Set == 2000)
                .Where(x => (x.Subset == CardSubset.Box.ToString() && x.Rarity == 6 && x.Section >= CONSTANTS.NEWEST_SECTION) ||
                            (x.Subset == CardSubset.Box.ToString() && x.Rarity == 6 && x.Fusion_Level >= 1 && (x.Power >= 8 || x.FactionPower >= 8)) ||
                            (x.Subset == CardSubset.Chance.ToString() && x.Fusion_Level == 2 && (x.Power >= 8 || x.FactionPower >= 8)))
                .ToList();

            return cards;
        }

        #endregion


        #region Format Card

        /// <summary>
        /// Return a card and card count from a string
        /// (ex: Ultimata Ragefueled#3 would return <"ultimataragefueled", 3>)
        /// </summary>
        public static KeyValuePair<string, int> FormatCard(string card, bool removeLevels=true, bool formatCardName=true)
        {
            StringBuilder s = new StringBuilder();
            string resultCard = "";
            int cardCount = 1;

            // Remove -6
            card = card.Trim();
            card = card.EndsWith("-6") ? card.Substring(0, card.Length - 2) : card;

            // We want to format the name and split out the card from the card quantity (ex: Super Grunt#2 -> supergrunt, 2)
            if (formatCardName)
            {
                string[] cardParts = card.Replace(" ", "").Replace("\t", "").Split(new char[] { '(', '#' }, 2);
                resultCard = cardParts[0].ToLower();

                // (x) or #x or (+x)
                if (cardParts.Length > 1)
                {
                    string cardNumber = cardParts[1].Replace("(", "").Replace(")", "").Replace("#", "").Replace("+", "");
                    int.TryParse(cardNumber, out cardCount);
                }
            }

            // We don't want to format the name, just split out the card from the card quantity (ex: Super Grunt#2 -> Super Grunt, 2)
            else
            {
                string[] cardParts = card.Replace("\t", "").Split(new char[] { '(', '#' }, 2);
                resultCard = cardParts[0];

                // (x) or #x or (+x)
                if (cardParts.Length > 1)
                {
                    string cardNumber = cardParts[1].Replace("(", "").Replace(")", "").Replace("#", "").Replace("+", "");
                    int.TryParse(cardNumber, out cardCount);
                }
            }
            

            
            
            // Hotfix for some odd cards with -X on their end (Quartermaster XKR-2 and Chaste TR-18.. add these hotfixes back in when the card is good)


            return new KeyValuePair<string, int>(resultCard, cardCount);
        }

        /// <summary>
        /// Returns card stats in a string
        /// </summary>
        public static string CardToString(string name, bool includeName=true, bool includeType = true, bool includeStats = true, bool includeMetadata = true, bool includeFusionFrom = false, bool includeFusionTo = false)
        {
            var result = new StringBuilder();

            var card = CardManager.CardTable.Values.Where(c => c.Name.ToLower() == name.ToLower()).FirstOrDefault();
            if (card == null) return "Card not found: " + name;

            // Card name and type
            // ex: Daedalus' Kingmaker (Mythic Imperial)
            if (includeName)
            {
                result.Append(card.Name);

                if (includeType)
                {
                    result.Append(" (");

                    // Rarity or summon
                    if (card.Set != (int)CardSet.Summon)
                    {
                        result.Append((Rarity)card.Rarity + " ");
                    }
                    else
                    {
                        result.Append("SUMMON ");
                    }
                    

                    // Faction
                    result.Append((Faction)card.Faction + " ");

                    // Fusion                   
                    if (card.Set == (int)CardSet.GameOnly)
                    {
                        result.Append("PvE ");
                    }
                    else if (card.Fusion_Level < 2)
                    {
                        result.Append((FusionLevel)card.Fusion_Level + " ");
                    }

                    // Assault or other
                    if (card.CardType != "Assault") result.Append(card.CardType);
                    result.Append(")");
                }
                result.AppendLine();
            }

            // Card stats
            if (includeStats)
            {
                result.AppendLine(card.Attack + "/" + card.Health + "/" + card.Delay);

                if (card.s1 != null && card.s1.id != null) result.AppendLine(GetSkillString(card.s1));
                if (card.s2 != null && card.s2.id != null) result.AppendLine(GetSkillString(card.s2));
                if (card.s3 != null && card.s3.id != null) result.AppendLine(GetSkillString(card.s3));
                if (card.s4 != null && card.s4.id != null) result.AppendLine(GetSkillString(card.s4));
                if (card.s5 != null && card.s5.id != null) result.AppendLine(GetSkillString(card.s5));
                result.AppendLine();
            }

            // Card Metadata
            if (includeMetadata)
            {
                result.AppendLine();
                result.AppendLine("Card ID: " + card.CardId.ToString());
                result.AppendLine("Section: " + card.Section);
                result.AppendLine("Set: " + card.Set + "\tSubset: " + card.Subset);
                result.AppendLine("Xml Comment: " + card.Comment);
                result.AppendLine("");
                result.AppendLine("Power: " + card.Power);
                result.AppendLine("Faction Power: " + card.FactionPower);
                if (card.PowerModifier != 0) result.AppendLine("Modified Power: " + card.PowerModifier);
                result.AppendLine();
                result.AppendLine("-----------");
                result.AppendLine("Power Breakdown");
                result.AppendLine("Section Power: " + card.SectionPower);
                result.AppendLine("Set Power: " + card.SetPower);
                result.AppendLine("Health Power: " + card.HealthPower);
                result.AppendLine("Skill 1 Power: " + card.s1.skillPower + " (" + card.s1.factionSkillPower + ")");
                result.AppendLine("Skill 2 Power: " + card.s2.skillPower + " (" + card.s2.factionSkillPower + ")");
                result.AppendLine("Skill 3 Power: " + card.s3.skillPower + " (" + card.s3.factionSkillPower + ")");

                //if (card.PowerModifier > 0) result.Append(" (" + card.PowerModifier + ")\r\n");
                //else result.Append("\r\n");
            }

            // What does this card fuse from
            if (includeFusionFrom)
            {
                result.AppendLine();
                result.AppendLine("Fuses From:");
                foreach (var recipe in card.FusesFrom)
                {
                    result.Append(recipe.Key.Name);
                    if (recipe.Value > 1) result.Append("#" + recipe.Value);
                    result.AppendLine();
                }
            }

            // What does this card fuse to
            if (includeFusionTo)
            {
                result.AppendLine();
                result.AppendLine("Fuses into:");
                foreach (var recipe in card.FusesInto)
                {
                    result.AppendLine(recipe.Name.Replace("-1", ""));
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Return a card's skill string in plain text
        /// Ex: <skill id="strike" x="5" all="1"> would be "Strike All 5"
        /// </summary>
        public static string GetSkillString(CardSkill skill)
        {
            var result = new StringBuilder();

            // Skill
            result.Append(skill.id + " ");

            // All or n
            if (skill.all) result.Append("all ");
            else if (skill.n > 0) result.Append(skill.n + " ");

            // s1 (to s2) - Enhance/Evolve
            if (!string.IsNullOrEmpty(skill.skill1))
            {
                result.Append(skill.skill1 + " ");
                if (!string.IsNullOrEmpty(skill.skill2))
                    result.Append("to " + skill.skill2 + " ");
            }

            // Faction
            if (skill.y > 0) result.Append((Faction)skill.y + " ");

            // x
            if (skill.x > 0) result.Append(skill.x + " ");

            // c
            if (skill.c > 0) result.Append("every " + skill.c + " ");

            // skill2 - holds trigger skills "On Enter/Death" 
            if (!string.IsNullOrEmpty(skill.trigger))
            {
                result.Append(" on " + skill.trigger + " ");
            }

            // summon
            if (!string.IsNullOrEmpty(skill.summon))
            {
                // Summon provides a cardid - attempt to find this card in the CardTable and get its name
                int id;
                if (Int32.TryParse(skill.summon, out id))
                {
                    var summonName = skill.summon;
                    var card = CardTable.Values.Where(x => x.CardId == id).FirstOrDefault();
                    if (card != null)
                    {
                        result.Append(" " + card.Name + " ");
                    }
                    else
                    {
                        result.Append(" " + skill.summon + " ");
                    }
                }

            }


            return result.ToString();
        }

        #endregion


        #region Generate AI Default Player Inventories

        /// <summary>
        /// Programmatically builds out a f2p inventory, using recent or upgraded cards
        /// </summary>
        public static ConcurrentDictionary<Card, int> BuildInventory_F2P(int faction = 0)
        {
            var result = new ConcurrentDictionary<Card, int>();
            int powerThreshold = 1;

            // -------------------------
            // Dual commanders
            // -------------------------
            var commanders = CardTable
                .Where(c => c.Value.Set == (int)CardSet.Commander)
                .Where(c => c.Value.Fusion_Level == 2)
                .Where(c => c.Value.Section == 1)
                .ToList();
            
            foreach (var c in commanders)
            {
                Card card = c.Value;
                int cardsToAdd = 1;
                bool isCardInFaction = faction == 0 || card.Faction == faction;

                // Skip Neocyte Fusion core
                if (c.Key == 42744 || c.Key == 42745) continue;

                if (isCardInFaction)
                {
                    result.AddOrUpdate(card, cardsToAdd, (x, count) => count + cardsToAdd);
                }
            }

            // -------------------------
            // Add fusions above a certain power threshold
            // -------------------------
            var cards = CardTable
                .Where(x => x.Value.Set == (int)CardSet.Fusion)
                .Where(x => x.Value.Power >= powerThreshold || x.Value.FactionPower >= powerThreshold)
                .ToDictionary(t => t.Key, t => t.Value);

            foreach (var c in cards)
            {
                Card card = c.Value;
                int cardsToAdd = 1;

                // Is this card in the extraPlayer's faction (or if faction is 0 / unspecified)
                bool isCardInFaction = faction == 0 || card.Faction == faction;

                switch (card.Rarity)
                {
                    case (int)Rarity.Epic:
                    case (int)Rarity.Legendary:
                        cardsToAdd = 10;
                        break;
                    case (int)Rarity.Vindicator:
                        cardsToAdd = 5;
                        break;
                }

                if (cardsToAdd > 0)
                {
                    result.AddOrUpdate(card, cardsToAdd, (x, count) => count + cardsToAdd);
                }
            }

            // -------------------------
            // Add rewards above a certain power threshold
            // -------------------------
            var rewards = CardTable
                .Where(c => c.Value.Subset == CardSubset.PvE_Reward.ToString() ||
                            c.Value.Subset == CardSubset.PvP_Reward.ToString() ||
                            c.Value.Subset == CardSubset.PvE_PvP_Reward.ToString())
                .Where(c => c.Value.Power > powerThreshold)
                .ToDictionary(t => t.Key, t => t.Value);

            foreach (var c in rewards)
            {
                Card card = c.Value;
                int cardsToAdd = 1;

                // Is this card in the extraPlayer's faction (or if faction is 0 / unspecified)
                bool isCardInFaction = faction == 0 || card.Faction == faction;

                switch (card.Rarity)
                {
                    case (int)Rarity.Epic:
                    case (int)Rarity.Legendary:
                        cardsToAdd = 8;
                        break;
                    case (int)Rarity.Vindicator:
                        cardsToAdd = 4;
                        break;
                    case (int)Rarity.Mythic:
                        cardsToAdd = 1;                        
                        break;
                }

                if (cardsToAdd > 0)
                {
                    result.AddOrUpdate(card, cardsToAdd, (x, count) => count + cardsToAdd);
                }
            }

            return result;
        }

        /// <summary>
        /// 8x Box Vinds
        /// 4x Box Mythics
        /// 3x Chance Epics
        /// </summary>
        public static ConcurrentDictionary<Card, int> BuildInventory_BigWhale(int faction = 0)
        {
            var result = BuildInventory_F2P();
            int powerThreshold = 1;

            // -------------------------
            // Quad commanders
            // -------------------------
            var commanders = CardTable.Values
                .Where(c => c.Set == 7000)
                .Where(c => c.Fusion_Level == 2)
                .Where(c => c.Section == 1)
                .ToList();

            
            foreach (var card in commanders)
            {
                result.AddOrUpdate(card, 1, (x, count) => count + 1);
            }


            // -------------------------
            // Box / Reward cards
            // -------------------------
            var setCards = CardTable.Values
                .Where(c => c.Set == 2000)
                .Where(c => c.Power >= 1 || c.FactionPower >= 1)
                .ToList();

            foreach (var card in setCards)
            {
                int cardsToAdd = 1;

                // Is this card in the extraPlayer's faction (or if faction is 0 / unspecified)
                var isCardInFaction = faction == 0 || card.Faction == faction;

                switch (card.Rarity)
                {
                    case (int)Rarity.Epic:
                        if (card.Subset == CardSubset.Box.ToString()) cardsToAdd = 10;
                        else if (card.Subset == CardSubset.Chance.ToString()) cardsToAdd = 10; //chance
                        else cardsToAdd = 8; //rewards
                        break;

                    case (int)Rarity.Legendary:
                        if (card.Subset == CardSubset.Box.ToString()) cardsToAdd = 10;
                        else if (card.Subset == CardSubset.Chance.ToString()) cardsToAdd = 2; //chance
                        else cardsToAdd = 8; //rewards
                        break;

                    case (int)Rarity.Vindicator:
                        if (card.Subset == CardSubset.Box.ToString()) cardsToAdd = 8;
                        else if (card.Subset == CardSubset.Chance.ToString()) cardsToAdd = 2; //chance
                        else cardsToAdd = 4; //rewards
                        break;

                    // This is more manual - we want to capture common Mythics, not every one
                    case (int)Rarity.Mythic:
                        cardsToAdd = 4;
                        break;
                }

                if (cardsToAdd > 0)
                {
                    result.AddOrUpdate(card, cardsToAdd, (x, count) => count + cardsToAdd);
                }
            }

            // -------------------------
            // Fusions
            // -------------------------
            var fusionCards = CardTable.Values
                .Where(c => c.Set == 2500)
                .Where(c => c.Power >= powerThreshold || c.FactionPower >= powerThreshold)
                .ToList();

            foreach (var card in fusionCards)
            {
                int cardsToAdd = 1;

                // Is this card in the extraPlayer's faction (or if faction is 0 / unspecified)
                var isCardInFaction = faction == 0 || card.Faction == faction;

                switch (card.Rarity)
                {
                    case (int)Rarity.Epic:
                    case (int)Rarity.Legendary:
                        cardsToAdd = 8;
                        break;
                    case (int)Rarity.Vindicator:
                    case (int)Rarity.Mythic:                        
                        cardsToAdd = 8;
                        break;
                }

                if (cardsToAdd > 0)
                {
                    result.AddOrUpdate(card, cardsToAdd, (x, count) => count + cardsToAdd);
                }
            }

            return result;
        }


        /// <summary>
        /// 10x Box cards
        /// 10x Fusions
        /// 10x Chance epics
        /// 4x Chance Legends/Vinds
        /// 1x Raid rewards
        /// 2x Rewards
        /// </summary>
        public static ConcurrentDictionary<Card, int> BuildInventory_AllCards(int faction = 0)
        {
            var result = new ConcurrentDictionary<Card, int>();
            int powerThreshold = 1;

            // -------------------------
            // Quad commanders
            // -------------------------
            var commanders = CardTable.Values
                .Where(c => c.Set == 7000)
                .Where(c => c.Fusion_Level == 2)
                .Where(c => c.Section == 1)
                .ToList();

            foreach (var card in commanders)
            {
                result.AddOrUpdate(card, 1, (x, count) => count + 1);
            }


            // -------------------------
            // Box cards
            // -------------------------
            var boxCards = CardTable.Values
                .Where(c => c.Set == 2000)
                .Where(c => c.Subset == CardSubset.Box.ToString() || c.Subset == CardSubset.Cache.ToString())
                .Where(c => c.Fusion_Level == 2)
                .Where(c => c.Power >= powerThreshold || c.FactionPower >= powerThreshold)
                .ToList();

            foreach (var card in boxCards)
            {
                result.AddOrUpdate(card, 10, (x, count) => count + 10);
            }

            // -------------------------
            // Fusions
            // -------------------------
            var fusionCards = CardTable.Values
                .Where(c => c.Set == 2500)
                .Where(c => c.Fusion_Level == 2)
                .Where(c => c.Power >= 1 || c.FactionPower >= 1)
                .ToList();

            foreach (var card in fusionCards)
            {
                result.AddOrUpdate(card, 10, (x, count) => count + 10);
            }

            // -------------------------
            // Chance cards
            // -------------------------
            var chanceEpicCards = CardTable.Values
                .Where(c => c.Set == 2000)
                .Where(c => c.Subset == CardSubset.Chance.ToString())
                .Where(c => c.Rarity <= 4)
                .Where(c => c.Power >= 1 || c.FactionPower >= 1)
                .ToList();

            foreach (var card in chanceEpicCards)
            {
                result.AddOrUpdate(card, 10, (x, count) => count + 10);
            }

            var chanceCards = CardTable.Values
                .Where(c => c.Set == 2000)
                .Where(c => c.Subset == CardSubset.Chance.ToString())
                .Where(c => c.Fusion_Level == 2)
                .Where(c => c.Rarity >= 5)
                .Where(c => c.Power >= 1 || c.FactionPower >= 1)
                .ToList();

            // 4x chance vinds/mythics, but could be made less..
            foreach (var card in chanceCards)
            {
                result.AddOrUpdate(card, 4, (x, count) => count + 4);
            }

            // -------------------------
            // Raid cards - 2x
            // -------------------------
            var raidCards = CardTable.Values
                .Where(c => c.Set == 2000)
                .Where(c => c.Subset == CardSubset.PvE_Reward.ToString() || c.Subset == CardSubset.PvP_Reward.ToString() || c.Subset == CardSubset.PvE_PvP_Reward.ToString())
                .Where(c => c.Fusion_Level == 2)
                .Where(c => c.Rarity >= 5)
                .Where(c => c.Power >= 1 || c.FactionPower >= 1)
                .ToList();

            foreach (var card in raidCards)
            {
                result.AddOrUpdate(card, 2, (x, count) => count + 2);
            }

            // -------------------------
            // Reward cards - 8x, except 1x on mythics
            // -------------------------
            var rewardCards = CardTable.Values
                .Where(c => c.Set == 2000)
                .Where(c => c.Subset == CardSubset.PvE_Reward.ToString() || c.Subset == CardSubset.PvP_Reward.ToString() || c.Subset == CardSubset.PvE_PvP_Reward.ToString())
                .Where(c => c.Fusion_Level == 2)
                .Where(c => c.Power >= 1 || c.FactionPower >= 1)
                .ToList();

            foreach (var card in rewardCards)
            {
                if (card.Rarity == 6)
                    result.AddOrUpdate(card, 1, (x, count) => count + 1);
                else
                    result.AddOrUpdate(card, 4, (x, count) => count + 8);
            }

            return result;
        }

        #endregion


        #region Helpers

        /// <summary>
        /// Read a <unit>...</unit> of a card XML, creating a set of cards for it
        /// </summary>
        private static List<Card> CreateCards_ParseCardXml(XElement unit, int section)
        {
            List<Card> cards = new List<Card>();
            string name = "";
            string formattedName = "";
            int cardId = -1;
            int cardIdOrigin = -1;
            int delay = -1;
            int attack = -1;
            int health = -1;
            int faction = -1;
            int fusionLevel = -1;
            int rarity = -1;
            int set = 9999;
            string cardType = "Assault";

            // Power is a metadata stat stat to guesstimate the strength of a card. It will get defined later
            int power = -1;

            CardSkill s1 = new CardSkill();
            CardSkill s2 = new CardSkill();
            CardSkill s3 = new CardSkill();
            CardSkill s4 = new CardSkill();
            CardSkill s5 = new CardSkill();

            try
            {
                name = unit.Element("name").Value;
                cardId = int.Parse(unit?.Element("id").Value);
                cardIdOrigin = cardId;
                IEnumerable<XElement> upgrades = unit.Elements("upgrade");

                if (cardId == 50001 || cardId == 1003 || cardId == 1041)
                {

                }

                // ------------------------------------------------
                // Create a card for the base <card> element
                // ------------------------------------------------
                if (!string.IsNullOrWhiteSpace(name))
                {
                    // ------------------------------------------------
                    // Card's basic stats 
                    // ------------------------------------------------
                    {
                        // - Attack, Health, and Cost can be undefined (-1) and help distinguish a commander or structure card
                        attack = unit.Element("attack") != null && unit.Element("attack").Value.ToString() != "" ? Int32.Parse(unit.Element("attack")?.Value) : -1;
                        health = unit.Element("health") != null && unit.Element("health").Value.ToString() != "" ? Int32.Parse(unit.Element("health")?.Value) : -1;
                        delay = unit.Element("cost") != null && unit.Element("cost").Value.ToString() != "" ? Int32.Parse(unit.Element("cost").Value) : -1;

                        // - Type = faction
                        faction = unit.Element("type") != null && unit.Element("type").Value.ToString() != "" ? Int32.Parse(unit.Element("type")?.Value) : -1;
                        fusionLevel = unit.Element("fusion_level") != null ? Int32.Parse(unit.Element("fusion_level").Value) : 0;
                        rarity = unit.Element("rarity") != null && unit.Element("rarity").Value.ToString() != "" ? Int32.Parse(unit.Element("rarity").Value) : -1;

                        // - Base card set. For quads, 2000 = Box/Chance/Reward, 2500 = Fusion, etc.
                        set = unit.Element("set") != null ? Int32.Parse(unit.Element("set").Value) : -1;

                        // - Give summoned units the quad fusion level
                        if (set == (int)CardSet.Summon) fusionLevel = 2;
                    }

                    // ------------------------------------------------
                    // Card's base level skills
                    // ------------------------------------------------
                    var baseSkills = unit?.Elements("skill");
                    if (baseSkills != null)
                    {
                        foreach (var skill in baseSkills.ToList())
                        {
                            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
                            string skillId = skill.Attribute("id").Value;
                            CardSkill newSkill = new CardSkill
                            {
                                id = textInfo.ToTitleCase(skillId)
                            };

                            // Parse the skill for its attributes
                            CreateCards_ReadSkill(skill, newSkill);

                            // Assign skill to a slot
                            if (string.IsNullOrEmpty(s1.id)) s1 = newSkill;
                            else if (string.IsNullOrEmpty(s2.id)) s2 = newSkill;
                            else if (string.IsNullOrEmpty(s3.id)) s3 = newSkill;
                            else if (string.IsNullOrEmpty(s4.id)) s4 = newSkill;
                            else if (string.IsNullOrEmpty(s5.id)) s5 = newSkill;
                        }
                    }

                    // ------------------------------------------------
                    // Determine the card type
                    // The types are: Assault, Structure, Commander, Dominion, Fortress
                    // ------------------------------------------------
                    switch (set)
                    {
                        case (int)CardSet.Commander:
                            cardType = CardType.Commander.ToString();
                            power = 0;
                            break;
                        case (int)CardSet.Fortress: // And section8?
                            cardType = CardType.Fortress.ToString();
                            power = 0;
                            break;
                        case (int)CardSet.Dominion:
                            cardType = CardType.Dominion.ToString();
                            power = 0;
                            break;
                        default:
                            // -1 attack = structure
                            // -1 attack and -1 delay = commander
                            if (attack < 0 && delay >= 0)
                            {
                                attack = 0;
                                cardType = CardType.Structure.ToString();
                            }
                            else if (attack < 0 && delay < 0)
                            {
                                // Exception: Ballroom Blitzplate
                                if (name != "Ballroom Blitzplate")
                                {
                                    attack = 0;
                                    cardType = CardType.Commander.ToString();
                                }
                                else cardType = CardType.Assault.ToString();
                            }
                            // Some walls have an <attack> stat even though they shouldn't
                            // Some fake commanders have wall, but its skill slot 4 or 5
                            else if (s1.id == "Wall")
                            {
                                cardType = CardType.Structure.ToString();
                            }
                            else
                            {
                                cardType = CardType.Assault.ToString();
                            }
                            break;
                    }

                    // ------------------------------------------------
                    // Add this card. If it has upgrades, hypen its name with -1 (level 1)
                    // ------------------------------------------------
                    bool cardHasUpgrades = upgrades != null && upgrades.Count() > 0;

                    string baseCardName = (cardHasUpgrades) ? name + "-1" : name;
                    formattedName = baseCardName.ToLower().Replace(" ", "");


                    Card card = new Card
                    {
                        CardId = cardId,
                        CardIdOrigin = cardIdOrigin,
                        CardType = cardType,
                        Name = baseCardName,
                        FormattedName = formattedName,
                        Faction = faction,
                        Fusion_Level = fusionLevel,
                        Level = 1,
                        MaxLevel = 1,
                        Attack = attack,
                        Health = health,
                        Delay = delay,
                        Rarity = rarity,
                        Set = set,

                        s1 = s1.Copy(),
                        s2 = s2.Copy(),
                        s3 = s3.Copy(),
                        s4 = s4.Copy(),
                        s5 = s5.Copy(),

                        // non-xml meta stats
                        Section = section,
                        Comment = "",
                        Power = power,
                    };
                    cards.Add(card);
                }

                // ------------------------------------------------
                // For each upgrade, create another card
                // ------------------------------------------------
                if (upgrades != null && upgrades.Count() > 0)
                {
                    int currentLevel = 2;

                    // Get the max level of this card
                    int maxLevel = 1 + upgrades.Count();
                    cards[0].MaxLevel = maxLevel;

                    foreach (var upgrade in upgrades.ToList())
                    {
                        // Get updated card_id, attack, health, cost, and new skills
                        if (upgrade.Element("card_id") != null)
                        {
                            int.TryParse(upgrade.Element("card_id").Value, out cardId);
                        }
                        if (upgrade.Element("attack") != null && !string.IsNullOrEmpty(upgrade.Element("attack").Value))
                        {
                            int.TryParse(upgrade.Element("attack").Value, out attack);
                        }
                        if (upgrade.Element("health") != null)
                        {
                            int.TryParse(upgrade.Element("health").Value, out health);
                        }
                        if (upgrade.Element("cost") != null)
                        {
                            int.TryParse(upgrade.Element("cost").Value, out delay);
                        }

                        // Update skills (edit existing ones or add new ones)
                        IEnumerable<XElement> skills = upgrade.Elements("skill");
                        if (skills != null)
                        {
                            var skillList = skills.ToList();

                            // If the card has more then 3 skills, reset the first 3.
                            // This is done because raid and mutant cards skills can change per level
                            if (skillList.Count >= 3)
                            {
                                s1 = new CardSkill();
                                s2 = new CardSkill();
                                s3 = new CardSkill();
                            }

                            foreach (var skill in skillList)
                            {
                                var skillId = skill.Attribute("id").Value;

                                // Is this an upgrade of an existing skill
                                if (s1.id != null && s1.id.ToLower() == skillId.ToLower()) CreateCards_ReadSkill(skill, s1);
                                else if (s2.id != null && s2.id.ToLower() == skillId.ToLower()) CreateCards_ReadSkill(skill, s2);
                                else if (s3.id != null && s3.id.ToLower() == skillId.ToLower()) CreateCards_ReadSkill(skill, s3);
                                else if (s4.id != null && s4.id.ToLower() == skillId.ToLower()) CreateCards_ReadSkill(skill, s4);
                                else if (s5.id != null && s5.id.ToLower() == skillId.ToLower()) CreateCards_ReadSkill(skill, s5);

                                // New skill
                                else
                                {
                                    var textInfo = new CultureInfo("en-US", false).TextInfo;
                                    var newSkill = new CardSkill();
                                    newSkill.id = textInfo.ToTitleCase(skillId);

                                    CreateCards_ReadSkill(skill, newSkill);

                                    // Assign skill to a slot
                                    if (string.IsNullOrEmpty(s1.id)) s1 = newSkill;
                                    else if (string.IsNullOrEmpty(s2.id)) s2 = newSkill;
                                    else if (string.IsNullOrEmpty(s3.id)) s3 = newSkill;
                                    else if (string.IsNullOrEmpty(s4.id)) s4 = newSkill;
                                    else if (string.IsNullOrEmpty(s5.id)) s5 = newSkill;
                                }
                            }
                        }
                        
                        // ------------------------------------------------
                        // Add this card. If its the last upgrade, remove the -X
                        // ------------------------------------------------
                        bool isCardFinalLevel = upgrade == upgrades.Last();

                        string upgradedCardName = isCardFinalLevel ? name : name + "-" + currentLevel;
                        formattedName = upgradedCardName.ToLower().Replace(" ", "");


                        Card card = new Card
                        {
                            CardId = cardId,
                            CardIdOrigin = cardIdOrigin,
                            CardType = cardType,
                            Name = upgradedCardName,
                            FormattedName = formattedName,
                            Faction = faction,
                            Level = currentLevel,
                            MaxLevel = maxLevel,
                            Fusion_Level = fusionLevel,
                            Attack = attack,
                            Health = health,
                            Delay = delay,
                            Rarity = rarity,
                            Set = set,

                            s1 = s1.Copy(),
                            s2 = s2.Copy(),
                            s3 = s3.Copy(),
                            s4 = s4.Copy(),
                            s5 = s5.Copy(),

                            // non-xml meta stats
                            Section = section,
                            Comment = "",
                            Power = power,
                        };
                        cards.Add(card);

                        currentLevel++;
                    }//<upgrade> loop
                }

                // ------------------------------------------------
                // Set the CardOrigin and CardFinal (min/max level of the card) to the first and last card in the list of cards
                // ------------------------------------------------
                foreach (var card in cards)
                {
                    card.CardOrigin = cards.First();
                    card.CardFinal = cards.Last();
                }
            }
            catch (Exception ex)
            {
                status += "Exception when parsing cards_section_" + section + ".xml for card " + cardId + ": " + ex.Message;
            }

            return cards;
        }

        /// <summary>
        /// Looks at fusion_recipes_cj2.xml to guess at what a card's origin is
        /// Also attempts to label summons
        /// 
        /// Why? Box, Chance, PvP, and PvE quads have a <set>2000</set>, that need a "subset" to differentiate them
        /// </summary>
        private static void CreateCards_ParseFusionXml()
        {
            XmlReader reader = XmlReader.Create("./data/fusion_recipes_cj2.xml");
            string currentComment = "";


            // Keep track of the previous fusion parent card
            Card parentCard = null;
            int parentCardId = -1;
            int resourceCardId = -1;

            // Xml Format:
            //<!-- December 2018 Vindicator Fusions -->
            //<fusion_recipe>
            //	<card_id>62144</card_id>
            //	<resource card_id="37837" number="1"/>
            //	<resource card_id="57347" number="1"/>
            //	<resource card_id="56717" number="1"/>
            //</fusion_recipe>
            while (reader.Read())
            {
                try
                {
                    switch (reader.NodeType)
                    {
                        // Track the previous XML comment to guess at where this card came from
                        case XmlNodeType.Comment:
                            currentComment = reader.Value;
                            break;

                        // <card_id> or <resource>
                        case XmlNodeType.Element:
                            var element = reader.Name;

                            // <card_id>: Level 1 of a fused card. There are 1 or more <resource> tags below it
                            // * Use the previous comment to assign a "subset" to this card
                            // * Save the parent_card_id for fusions 
                            if (element == "card_id")
                            {
                                var value = reader.Value;

                                if (reader.Read())
                                {
                                    // Keep track of the last parentCardId for the recipe_cards
                                    int.TryParse(reader.Value, out parentCardId);

                                    if (CardTable.TryGetValue(parentCardId, out parentCard))
                                    {
                                        // Get this card's subset
                                        CreateCards_GetSubset(parentCard, currentComment);


                                        // --------------------------------
                                        // For each level on this card, set the subset/comment on the same card
                                        // 
                                        // * MOST cards when leveling have incrementing card IDs
                                        // * Some don't, like commanders / old cards
                                        // * Doing searches on a value's property is extremely slow, try to increment the ID first
                                        // --------------------------------
                                        bool sequentialCardIds = true;
                                        for (int i = 1; i < parentCard.MaxLevel; i++)
                                        {
                                            CardTable.TryGetValue(parentCardId + i, out Card leveledCard);
                                            if (parentCard.CardIdOrigin == leveledCard?.CardIdOrigin)
                                            {
                                                leveledCard.Subset = parentCard.Subset;
                                                leveledCard.Comment = parentCard.Comment;
                                            }
                                            else
                                            {
                                                sequentialCardIds = false;
                                                break;
                                            }
                                        }

                                        // Go through the table and find all cards with this cardIdOrigin
                                        if (!sequentialCardIds)
                                        {
                                            List<Card> leveledCards = CardTable.Values.Where(x => x.CardIdOrigin == parentCardId).ToList();
                                            foreach (Card card in leveledCards)
                                            {
                                                card.Subset = parentCard.Subset;
                                                card.Comment = parentCard.Comment;
                                            }
                                        }

                                    }
                                }
                            }

                            // <resource>: Used to construct fusion recipes
                            else if (element == "resource")
                            {
                                if (reader.HasAttributes && reader.GetAttribute("card_id") != null)
                                {
                                    // Get the resource cardId. Then update 
                                    // * cardId - from the previous <card_id> element - this is the fusion
                                    // * resourceCardId - from the <resource> element - this is one of the fusion's components
                                    // * resourceCount

                                    int.TryParse(reader.GetAttribute("card_id"), out resourceCardId);
                                    CardTable.TryGetValue(resourceCardId, out Card resourceCard);

                                    // How many of this resource card does it take? If number is not defined, assume 1
                                    int.TryParse(reader.GetAttribute("number"), out int resourceCardCount);
                                    resourceCardCount = Math.Max(1, resourceCardCount);

                                    // Add fusesFrom to the parentCard, and fusesTo to the resourceCard
                                    if (parentCard != null && resourceCard != null)
                                    {
                                        // Go through the parentCard and find all cards with its cardIdOrigin
                                        // - Add the resourceCard into fusesFrom
                                        bool sequentialCardIds = true;
                                        int parentCardIdOrigin = parentCard.CardIdOrigin;

                                        for (int i = 0; i < parentCard.MaxLevel; i++)
                                        {
                                            CardTable.TryGetValue(parentCardIdOrigin + i, out Card card);
                                            if (parentCard.CardIdOrigin == card?.CardIdOrigin)
                                            {
                                                card.FusesFrom.Add(resourceCard, resourceCardCount);
                                            }
                                            else
                                            {
                                                sequentialCardIds = false;
                                                break;
                                            }
                                        }
                                        // If the card's upgrades aren't sequentially numbered, this is a slower way to handle it
                                        if (!sequentialCardIds)
                                        {
                                            List<Card> leveledCards = CardTable.Values.Where(x => x.CardIdOrigin == parentCardId).ToList();
                                            foreach (Card card in leveledCards)
                                            {
                                                if (!card.FusesFrom.ContainsKey(resourceCard))
                                                    card.FusesFrom.Add(resourceCard, resourceCardCount);
                                            }
                                        }


                                        // Go through the resourceCard and find all the cards with its cardOrigin
                                        // - Add the parentCard into fusesTo
                                        sequentialCardIds = true;
                                        for (int i = 0; i < resourceCard.MaxLevel; i++)
                                        {
                                            CardTable.TryGetValue(resourceCardId - i, out Card card);
                                            if (resourceCard.CardIdOrigin == card?.CardIdOrigin)
                                            {
                                                card.FusesInto.Add(parentCard);
                                            }
                                            else
                                            {
                                                sequentialCardIds = false;
                                                break;
                                            }
                                        }

                                        if (!sequentialCardIds)
                                        {
                                            List<Card> resourceCards = CardTable.Values.Where(x => x.CardIdOrigin == resourceCard.CardIdOrigin).ToList();
                                            foreach (Card card in resourceCards)
                                            {
                                                card.FusesInto.Add(parentCard);
                                            }
                                        }
                                    }

                                    // Card Exceptions whose CardIDs aren't linear
                                    //if (targetId == 33500) targetId = 334500;
                                    //if (targetId == 15055) targetId = 15056;
                                }
                            }

                            break;
                        default:
                            break;
                    }
                }
                catch (Exception ex)
                {
                    status += "Error on reading fusions.xml - " + ex.Message;
                    if (CardTable.ContainsKey(parentCardId))
                    {
                        Console.WriteLine(CardTable[parentCardId].Name);
                    }
                    if (CardTable.ContainsKey(resourceCardId))
                    {
                        Console.WriteLine(CardTable[resourceCardId].Name);
                    }
                }
            }

            // Close the reader
            reader.Close();
        }

        /// <summary>
        /// Get the card subset. Its usually gotten based on the previous XML comment
        /// Some older cards needs their subset hardcoded. The comment formatting is more consistent after ~2016
        /// </summary>
        public static void CreateCards_GetSubset(Card card, string currentComment)
        {
            string cardSubset = "";

            // --------------------------------
            // One off comments from very old xml comments
            // --------------------------------
            if (currentComment.Contains("Therion Fusion")) cardSubset = CardSubset.PvE_Reward.ToString();
            else if (currentComment.Contains("Week 2 Dimensional Terror")) cardSubset = CardSubset.Box.ToString();
            else if (currentComment == " Event Fusion ") cardSubset = CardSubset.Box.ToString();
            else if (currentComment.Contains("Tabitha's Exile")) cardSubset = CardSubset.Box.ToString();
            else if (currentComment.Contains("END Event Fusions")) cardSubset = CardSubset.Fusion.ToString();
            else if (currentComment.Contains("Vaults of Valhalla")) cardSubset = CardSubset.PvE_Reward.ToString();
            else if (currentComment.Contains("Worldship Infested Week")) cardSubset = CardSubset.Box.ToString();

            // --------------------------------
            // Set 2000: Box, Chance, Reward cards
            // --------------------------------
            else if (currentComment.Contains("Box") || currentComment.Contains("Event Fusions"))
                cardSubset = CardSubset.Box.ToString();
            else if (currentComment.Contains("Cache"))
                cardSubset = CardSubset.Cache.ToString();
            else if (currentComment.Contains("Pack") || currentComment.Contains("Chance") || currentComment.Contains("Exclusive"))
                cardSubset = CardSubset.Chance.ToString();
            else if (currentComment.Contains("Mission") || currentComment.Contains("Phase") || currentComment.Contains("Campaign") || currentComment.Contains("Raid") || currentComment.Contains("Mutant") || currentComment.Contains("Week"))
                cardSubset = CardSubset.PvE_Reward.ToString();
            else if (currentComment.Contains("War") || currentComment.Contains("Brawl") || currentComment.Contains("Conquest"))
                cardSubset = CardSubset.PvP_Reward.ToString();
            else if (currentComment.Contains("Fusion") && card.Set != 2000)
                cardSubset = CardSubset.Fusion.ToString();
            // Likely a reward card
            else if (currentComment.Contains("Reward"))
                cardSubset = CardSubset.PvE_PvP_Reward.ToString();
            // Likely a box card
            else if (card.Set == 2000)
                cardSubset = CardSubset.Box.ToString();

            // --------------------------------
            // Other player cards
            // --------------------------------
            else if (card.Set == (int)CardSet.Commander) cardSubset = CardSubset.Commander.ToString() + " - " + currentComment;
            else if (card.Set == (int)CardSet.Dominion) cardSubset = CardSubset.Dominion.ToString() + " - " + currentComment;
            else if (card.Set == (int)CardSet.Summon) cardSubset = CardSubset.Summon.ToString();
            else cardSubset = CardSubset.Unknown.ToString() + " - " + currentComment;

            // ----------------------------
            // Set card comment / subset
            // ----------------------------
            card.Subset = cardSubset;
            card.Comment = currentComment;
        }

        /// <summary>
        /// Create card whitelists
        /// </summary>
        public static void BuildCardDatabase_BuildWhitelists(MainForm form)
        {
            var time = new Stopwatch();
            time.Start();

            try
            {
                CONSTANTS.whitelistLevel1.AddRange(FileIO.SimpleRead(form, CONSTANTS.PATH_WHITELIST + "level1.txt", returnCommentedLines:false));
                CONSTANTS.whitelistLevel2.AddRange(FileIO.SimpleRead(form, CONSTANTS.PATH_WHITELIST + "level2.txt", returnCommentedLines: false));
                CONSTANTS.whitelistLevel3.AddRange(FileIO.SimpleRead(form, CONSTANTS.PATH_WHITELIST + "level3.txt", returnCommentedLines: false));
                CONSTANTS.reverseWhitelistLevel2.AddRange(FileIO.SimpleRead(form, CONSTANTS.PATH_WHITELIST + "level2reverse.txt", returnCommentedLines: false));
                CONSTANTS.reverseWhitelistLevel3.AddRange(FileIO.SimpleRead(form, CONSTANTS.PATH_WHITELIST + "level3reverse.txt", returnCommentedLines: false));
            }
            catch (Exception ex)
            {
                ControlExtensions.InvokeEx(form.outputTextBox, x => form.outputTextBox.AppendText("Error on BuildWhitelists(): " + ex.Message));
            }

            time.Stop();
            Console.WriteLine(MethodInfo.GetCurrentMethod() + " " + time.ElapsedMilliseconds + "ms ");

        }

        /// <summary>
        /// Get card power for each relevant card
        /// 
        /// Also write card power/stats to a debug file
        /// </summary>
        private static void BuildCardDatabase_GetCardPower(MainForm form)
        {

            // Read card power adjustments from appsettings. These are power overrides on a card that add or remove power
            // Format:
            // cardPower:<card name>:<integer>
            try
            {
                List<string> settings = FileIO.SimpleRead(form, "config/cardpower.txt", returnCommentedLines: true);
                List<string> cardPowerAdjustments = settings.Where(x => x.StartsWith("cardPower:")).ToList();

                if (cardPowerAdjustments.Count > 0)
                {
                    foreach (string cardPowerAdjustment in cardPowerAdjustments)
                    {
                        string[] splitter = cardPowerAdjustment.Split(new char[] { '=', ':' });
                        if (splitter.Length == 3)
                        {
                            string cardName = splitter[1].Trim();
                            int.TryParse(splitter[2].Trim(), out int cardPower);
                            if (!CONSTANTS.CARD_POWER_ADJUSTMENTS.ContainsKey(cardName))
                            {
                                CONSTANTS.CARD_POWER_ADJUSTMENTS.Add(cardName, cardPower);
                            }
                        }
                    }
                }
                
                List<string> factionCardPowerAdjustments = settings.Where(x => x.StartsWith("factionCardPower:")).ToList();

                if (factionCardPowerAdjustments.Count > 0)
                {
                    foreach (string factionCardPowerAdjustment in factionCardPowerAdjustments)
                    {
                        string[] splitter = factionCardPowerAdjustment.Split(new char[] { '=', ':' });
                        if (splitter.Length == 3)
                        {
                            string cardName = splitter[1].Trim();
                            int.TryParse(splitter[2].Trim(), out int cardPower);
                            if (!CONSTANTS.CARD_FACTIONPOWER_ADJUSTMENTS.ContainsKey(cardName))
                            {
                                CONSTANTS.CARD_FACTIONPOWER_ADJUSTMENTS.Add(cardName, cardPower);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(form, ex.Message, "ReadFromAppSettings() error: Could not find cardPowerAdjustments lines.\r\n" + ex.Message);
            }

            // If writing power calculations to a file..
            //StringBuilder powerOutput = new StringBuilder();
            //powerOutput.Append("Name\t");
            //powerOutput.Append("Power\tFactionPower\t");
            //powerOutput.Append("Section\tFaction\tRarity\tSubset\t");
            //powerOutput.Append("Attack\tHealth\tDelay\t");
            //powerOutput.Append("S1\tS2\tS3\t");
            //powerOutput.Append("SectionPower\tSetPower\tHealthPower\tManual\t");
            //powerOutput.Append("S1 Power\tS2 Power\tS3 Power\t");
            //powerOutput.Append("Skill string\r\n");

            // Assess the power of player assaults and structures
            var cards = CardManager.CardTable.Values
                .Where(x => x.Level == x.MaxLevel) // only max level cards  
                .Where(x => x.Fusion_Level == 2 || (x.Fusion_Level == 1 && x.Rarity >= 5)) // Only quads, or vindi/mythic level duals
                .Where(x => x.Set == (int)CardSet.BoxOrReward ||
                            x.Set == (int)CardSet.Fusion ||
                            x.Set == (int)CardSet.Summon)
                //.Where(x => x.Section >= CONSTANTS.NEWEST_SECTION - 3 || CONSTANTS.whitelistLevel1.Contains(x.Name))
                .Where(x => CONSTANTS.whitelistLevel1.Contains(x.Name) || x.Section == 17)
                .ToList();

            foreach (var c in cards)
            {
                try
                {
                    // Get the power rating of this card
                    if (CONSTANTS.whitelistLevel1.Contains(c.Name))
                    {
                        var powerString = CardPower.CalculatePower(c);
                    }
                    // Custom cards: Should these be power=0?
                    //else if (c.CardId >= 70000)
                    //{
                    //    c.Power = 0;
                    //    c.FactionPower = 0;
                    //}
                    //This card is deemed so weak it gets negative power to filter out of the card list
                    else
                    {
                        c.Power = -1;
                        c.FactionPower = -1;
                    }

                }
                catch(Exception ex)
                {
                    Console.WriteLine("CardPower: Error when assessing card " + c.Name + ": " + ex.Message);
                }
            }

            // Write power results to /debug/
            // FileIO.SimpleWrite(form, "./config/debug", "power.txt", powerOutput.ToString());

        }

        /// <summary>
        /// Gets attributes from a skill
        /// </summary>
        private static void CreateCards_ReadSkill(XElement skill, CardSkill mySkill)
        {
            // Get x (magnitude)
            if (skill.Attribute("x") != null)
            {
                var newX = Int32.Parse(skill.Attribute("x").Value);
                mySkill.x = Math.Max(mySkill.x, newX);
            }
            // Get y (faction)
            if (skill.Attribute("y") != null)
            {
                mySkill.y = Int32.Parse(skill.Attribute("y").Value);
            }
            // Get highest n
            if (skill.Attribute("n") != null)
            {
                var newN = Int32.Parse(skill.Attribute("n").Value);
                if (mySkill.n <= 0) mySkill.n = newN;
                else mySkill.n = Math.Max(mySkill.n, newN);
            }
            // Get lowest c unless c is 0
            if (skill.Attribute("c") != null)
            {
                var newC = Int32.Parse(skill.Attribute("c").Value);
                if (mySkill.c == 0) mySkill.c = newC;
                else mySkill.c = Math.Min(mySkill.c, newC);
            }
            // Get all
            if (skill.Attribute("all") != null)
            {
                mySkill.all = true;
            }
            // Get skill 1 (Enhance or Evolve)
            if (skill.Attribute("s") != null)
            {
                var textInfo = new CultureInfo("en-US", false).TextInfo;
                mySkill.skill1 = textInfo.ToTitleCase(skill.Attribute("s").Value);
            }
            // Get skill 2 (Evolve)
            if (skill.Attribute("s2") != null)
            {
                var textInfo = new CultureInfo("en-US", false).TextInfo;
                mySkill.skill2 = textInfo.ToTitleCase(skill.Attribute("s2").Value);
            }

            // Enter/Death Trigger
            if (skill.Attribute("trigger") != null)
            {
                var textInfo = new CultureInfo("en-US", false).TextInfo;
                mySkill.trigger = textInfo.ToTitleCase(skill.Attribute("trigger").Value);
            }

            // Summon card_id
            // TODO: Eventually get the card id but it'd be an after-the-skill 
            if (skill.Attribute("card_id") != null)
            {
                var textInfo = new CultureInfo("en-US", false).TextInfo;
                mySkill.summon = textInfo.ToTitleCase(skill.Attribute("card_id").Value);
            }
        }

        #endregion
    }
}



//#region SP Costs of a card

///// <summary>
///// Salvage point cost to restore a card. Does not factor in half price on specials
///// </summary>
//public static int GetCardRestoreCost(Card card)
//{
//    int salvageCost = 0;
//    if (card.Rarity == 3 && card.Fusion_Level == 0) salvageCost = 40;
//    else if (card.Rarity == 3 && card.Fusion_Level == 1) salvageCost = 160;
//    else if (card.Rarity == 3 && card.Fusion_Level == 2) salvageCost = 360;
//    else if (card.Rarity == 4 && card.Fusion_Level == 0) salvageCost = 80;
//    else if (card.Rarity == 4 && card.Fusion_Level == 1) salvageCost = 300;
//    else if (card.Rarity == 4 && card.Fusion_Level == 2) salvageCost = 620;
//    else if (card.Rarity == 5 && card.Fusion_Level == 0) salvageCost = 160;
//    else if (card.Rarity == 5 && card.Fusion_Level == 1) salvageCost = 600;
//    else if (card.Rarity == 5 && card.Fusion_Level == 2) salvageCost = 1000;
//    else if (card.Rarity == 6 && card.Fusion_Level == 0) salvageCost = 240;
//    else if (card.Rarity == 6 && card.Fusion_Level == 1) salvageCost = 800;
//    else if (card.Rarity == 6 && card.Fusion_Level == 2) salvageCost = 1400;

//    return salvageCost;
//}

///// <summary>
///// Salvage point cost to salvage a card
///// </summary>
//public static int GetCardSalvageValue(Card card)
//{
//    int salvageGain = 0;

//    if (card.Rarity == 3 && card.Fusion_Level == 0 && card.Level == 1) salvageGain = 20;
//    else if (card.Rarity == 3 && card.Fusion_Level == 0 && card.Level == 2) salvageGain = 25;
//    else if (card.Rarity == 3 && card.Fusion_Level == 0 && card.Level == 3) salvageGain = 30;
//    else if (card.Rarity == 3 && card.Fusion_Level == 0 && card.Level == 4) salvageGain = 40;
//    else if (card.Rarity == 3 && card.Fusion_Level == 0 && card.Level == 5) salvageGain = 50;
//    else if (card.Rarity == 3 && card.Fusion_Level == 0 && card.Level == 6) salvageGain = 65;
//    else if (card.Rarity == 3 && card.Fusion_Level == 1 && card.Level == 1) salvageGain = 80;
//    else if (card.Rarity == 3 && card.Fusion_Level == 1 && card.Level == 2) salvageGain = 85;
//    else if (card.Rarity == 3 && card.Fusion_Level == 1 && card.Level == 3) salvageGain = 95;
//    else if (card.Rarity == 3 && card.Fusion_Level == 1 && card.Level == 4) salvageGain = 110;
//    else if (card.Rarity == 3 && card.Fusion_Level == 1 && card.Level == 5) salvageGain = 125;
//    else if (card.Rarity == 3 && card.Fusion_Level == 1 && card.Level == 6) salvageGain = 150;
//    else if (card.Rarity == 3 && card.Fusion_Level == 2 && card.Level == 1) salvageGain = 180;
//    else if (card.Rarity == 3 && card.Fusion_Level == 2 && card.Level == 2) salvageGain = 185;
//    else if (card.Rarity == 3 && card.Fusion_Level == 2 && card.Level == 3) salvageGain = 195;
//    else if (card.Rarity == 3 && card.Fusion_Level == 2 && card.Level == 4) salvageGain = 210;
//    else if (card.Rarity == 3 && card.Fusion_Level == 2 && card.Level == 5) salvageGain = 225;
//    else if (card.Rarity == 3 && card.Fusion_Level == 2 && card.Level == 6) salvageGain = 275;

//    else if (card.Rarity == 4 && card.Fusion_Level == 0 && card.Level == 1) salvageGain = 40;
//    else if (card.Rarity == 4 && card.Fusion_Level == 0 && card.Level == 2) salvageGain = 45;
//    else if (card.Rarity == 4 && card.Fusion_Level == 0 && card.Level == 3) salvageGain = 60;
//    else if (card.Rarity == 4 && card.Fusion_Level == 0 && card.Level == 4) salvageGain = 75;
//    else if (card.Rarity == 4 && card.Fusion_Level == 0 && card.Level == 5) salvageGain = 100;
//    else if (card.Rarity == 4 && card.Fusion_Level == 0 && card.Level == 6) salvageGain = 125;
//    else if (card.Rarity == 4 && card.Fusion_Level == 1 && card.Level == 1) salvageGain = 150;
//    else if (card.Rarity == 4 && card.Fusion_Level == 1 && card.Level == 2) salvageGain = 155;
//    else if (card.Rarity == 4 && card.Fusion_Level == 1 && card.Level == 3) salvageGain = 165;
//    else if (card.Rarity == 4 && card.Fusion_Level == 1 && card.Level == 4) salvageGain = 180;
//    else if (card.Rarity == 4 && card.Fusion_Level == 1 && card.Level == 5) salvageGain = 200;
//    else if (card.Rarity == 4 && card.Fusion_Level == 1 && card.Level == 6) salvageGain = 225;
//    else if (card.Rarity == 4 && card.Fusion_Level == 2 && card.Level == 1) salvageGain = 310;
//    else if (card.Rarity == 4 && card.Fusion_Level == 2 && card.Level == 2) salvageGain = 315;
//    else if (card.Rarity == 4 && card.Fusion_Level == 2 && card.Level == 3) salvageGain = 325;
//    else if (card.Rarity == 4 && card.Fusion_Level == 2 && card.Level == 4) salvageGain = 340;
//    else if (card.Rarity == 4 && card.Fusion_Level == 2 && card.Level == 5) salvageGain = 360;
//    else if (card.Rarity == 4 && card.Fusion_Level == 2 && card.Level == 6) salvageGain = 400;

//    else if (card.Rarity == 5 && card.Fusion_Level == 0 && card.Level == 1) salvageGain = 80;
//    else if (card.Rarity == 5 && card.Fusion_Level == 0 && card.Level == 2) salvageGain = 85;
//    else if (card.Rarity == 5 && card.Fusion_Level == 0 && card.Level == 3) salvageGain = 100;
//    else if (card.Rarity == 5 && card.Fusion_Level == 0 && card.Level == 4) salvageGain = 125;
//    else if (card.Rarity == 5 && card.Fusion_Level == 0 && card.Level == 5) salvageGain = 175;
//    else if (card.Rarity == 5 && card.Fusion_Level == 0 && card.Level == 6) salvageGain = 250;
//    else if (card.Rarity == 5 && card.Fusion_Level == 1 && card.Level == 1) salvageGain = 300;
//    else if (card.Rarity == 5 && card.Fusion_Level == 1 && card.Level == 2) salvageGain = 305;
//    else if (card.Rarity == 5 && card.Fusion_Level == 1 && card.Level == 3) salvageGain = 315;
//    else if (card.Rarity == 5 && card.Fusion_Level == 1 && card.Level == 4) salvageGain = 330;
//    else if (card.Rarity == 5 && card.Fusion_Level == 1 && card.Level == 5) salvageGain = 350;
//    else if (card.Rarity == 5 && card.Fusion_Level == 1 && card.Level == 6) salvageGain = 380;
//    else if (card.Rarity == 5 && card.Fusion_Level == 2 && card.Level == 1) salvageGain = 500;
//    else if (card.Rarity == 5 && card.Fusion_Level == 2 && card.Level == 2) salvageGain = 505;
//    else if (card.Rarity == 5 && card.Fusion_Level == 2 && card.Level == 3) salvageGain = 510;
//    else if (card.Rarity == 5 && card.Fusion_Level == 2 && card.Level == 4) salvageGain = 525;
//    else if (card.Rarity == 5 && card.Fusion_Level == 2 && card.Level == 5) salvageGain = 550;
//    else if (card.Rarity == 5 && card.Fusion_Level == 2 && card.Level == 6) salvageGain = 600;

//    else if (card.Rarity == 6 && card.Fusion_Level == 0 && card.Level == 1) salvageGain = 120;
//    else if (card.Rarity == 6 && card.Fusion_Level == 0 && card.Level == 2) salvageGain = 125;
//    else if (card.Rarity == 6 && card.Fusion_Level == 0 && card.Level == 3) salvageGain = 140;
//    else if (card.Rarity == 6 && card.Fusion_Level == 0 && card.Level == 4) salvageGain = 165;
//    else if (card.Rarity == 6 && card.Fusion_Level == 0 && card.Level == 5) salvageGain = 225;
//    else if (card.Rarity == 6 && card.Fusion_Level == 0 && card.Level == 6) salvageGain = 300;
//    else if (card.Rarity == 6 && card.Fusion_Level == 1 && card.Level == 1) salvageGain = 400;
//    else if (card.Rarity == 6 && card.Fusion_Level == 1 && card.Level == 2) salvageGain = 405;
//    else if (card.Rarity == 6 && card.Fusion_Level == 1 && card.Level == 3) salvageGain = 415;
//    else if (card.Rarity == 6 && card.Fusion_Level == 1 && card.Level == 4) salvageGain = 430;
//    else if (card.Rarity == 6 && card.Fusion_Level == 1 && card.Level == 5) salvageGain = 450;
//    else if (card.Rarity == 6 && card.Fusion_Level == 1 && card.Level == 6) salvageGain = 490;
//    else if (card.Rarity == 6 && card.Fusion_Level == 2 && card.Level == 1) salvageGain = 700;
//    else if (card.Rarity == 6 && card.Fusion_Level == 2 && card.Level == 2) salvageGain = 705;
//    else if (card.Rarity == 6 && card.Fusion_Level == 2 && card.Level == 3) salvageGain = 710;
//    else if (card.Rarity == 6 && card.Fusion_Level == 2 && card.Level == 4) salvageGain = 725;
//    else if (card.Rarity == 6 && card.Fusion_Level == 2 && card.Level == 5) salvageGain = 750;
//    else if (card.Rarity == 6 && card.Fusion_Level == 2 && card.Level == 6) salvageGain = 800;

//    return salvageGain;
//}

//#endregion

