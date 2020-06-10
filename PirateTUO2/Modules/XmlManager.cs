using PirateTUO2.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace PirateTUO2.Modules
{
    /// <summary>
    /// This module communicates with the TU assets server
    /// http://mobile-dev.tyrantonline.com/assets/...
    /// </summary>
    public class XmlManager
    {
        /// <summary>
        /// Downloads TU xml. Returns true if there were any changes
        /// Download in chunks, because TUO hates a lot of concurrent connections 
        /// 
        /// This will return either "No changes detected" or what has changed
        /// </summary>
        public static string PullGameXml(MainForm form, bool forceUpdate = false)
        {
            var time = new Stopwatch();
            time.Start();


            // Each task is an xml file to download from TU
            //var queuedTasks = new List<Task<string>>();
            var result = new StringBuilder();

            // Attempt to get latest section from appsettings
            try
            {
                List<string> settings = FileIO.SimpleRead(form, "config/appsettings.txt", returnCommentedLines: true);
                string targetSetting = "latestCardSection:";
                string foundSetting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (foundSetting != null)
                {
                    foundSetting = foundSetting.Replace(targetSetting, "");                    
                    int.TryParse(foundSetting, out int newestSection);
                    if (newestSection > 0)
                    {
                        CONSTANTS.NEWEST_SECTION = newestSection;
                    }
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(form, ex.Message, "ReadFromAppSettings() error: Could not open config/appsettings.txt.\r\n" + ex.Message);
            }

            // -----------------------
            // Work
            // -----------------------

            // Add each xml URL to a list and download in parallel
            List<string> xmlsToDownload = new List<string>();
            for (int i = 1; i <= CONSTANTS.NEWEST_SECTION; i++)
            {
                xmlsToDownload.Add("cards_section_" + i + ".xml");
            }
            xmlsToDownload.Add("fusion_recipes_cj2.xml");
            xmlsToDownload.Add("missions.xml");
            xmlsToDownload.Add("skills_set.xml");
            xmlsToDownload.Add("levels.xml");

            Parallel.ForEach(xmlsToDownload,
                new ParallelOptions { MaxDegreeOfParallelism = 6 },
                xml =>
                {
                    result.Append(DownloadXml(form, xml, forceUpdate));
                    Console.WriteLine("Finished downloading " + xml);
                });


            time.Stop();
            Console.WriteLine("Pull XML from server: " + time.ElapsedMilliseconds + "ms ");

            // No changes
            if (result.ToString().Trim() == "")
            {
                return "Xml: No changes detected\r\n";
            }
            // Changes
            else
            {
                return "Xml:\r\n"+ result.ToString();
            }
        }

        
        /// <summary>
        /// Download an XML file, returning a message if the file was changed
        /// 
        /// Don't download a file if last modified was fairly recent
        /// </summary>
        private static string DownloadXml(MainForm form, string filename, bool forceUpdate)
        {
            var result = "";

            // This is all done so we have to download less files, which is a heavy load task
            var fileExists = File.Exists("./data/" + filename);
            if (fileExists) {

                var fileLastUpdated = File.GetLastWriteTime("./data/" + filename);

                // How old should a file be in minutes before we try downloading it 
                // -- Old card sections can update less frequently
                // -- Sections like mission.xml can be updated if 24 hours old
                var updateThresholdMinutes = 1;

                switch(filename)
                {
                    case "cards_section_1.xml":
                    case "cards_section_2.xml":
                    case "cards_section_3.xml":
                    case "cards_section_4.xml":
                    case "cards_section_5.xml":
                    case "cards_section_6.xml":
                    case "cards_section_7.xml":
                    case "cards_section_8.xml":
                    case "cards_section_9.xml":
                    case "cards_section_10.xml":
                    case "cards_section_11.xml":
                    case "cards_section_12.xml":
                    case "cards_section_13.xml":
                    case "cards_section_14.xml":
                    case "cards_section_15.xml":

                        // These should only change on Monday-Thursday. We should only pull them if they're super old
                        if (DateTime.Now.DayOfWeek == DayOfWeek.Friday || DateTime.Now.DayOfWeek == DayOfWeek.Saturday || DateTime.Now.DayOfWeek == DayOfWeek.Sunday)
                            updateThresholdMinutes = 4320;
                        else 
                            updateThresholdMinutes = 480;
                        break;

                    case "levels.xml":
                    case "missions.xml":
                    case "skills_set.xml":
                        // These should only change on Monday-Thursday. We should only pull them if they're super old
                        if (DateTime.Now.DayOfWeek == DayOfWeek.Friday || DateTime.Now.DayOfWeek == DayOfWeek.Saturday || DateTime.Now.DayOfWeek == DayOfWeek.Sunday)
                            updateThresholdMinutes = 4320;
                        else
                            updateThresholdMinutes = 480;
                        break;

                    case "cards_section_16.xml":
                    case "cards_section_17.xml":
                    case "cards_section_18.xml":
                    case "cards_section_19.xml":
                    case "cards_section_20.xml":
                    case "cards_section_21.xml":
                    case "cards_section_22.xml":
                    case "fusion_recipes_cj2.xml":
                    default:
                        // new xml sections, fusion recipes
                        updateThresholdMinutes = 0;
                        break;

                }

                // If this file's age is newer then the updateThreshold, don't download this again 
                if (!forceUpdate && fileLastUpdated > DateTime.Now.AddMinutes(-updateThresholdMinutes)) return "";
            }


            //Console.WriteLine("Downloading " + filename);
            try
            {
                var url = CONSTANTS.baseUrl + filename;

                var request = (HttpWebRequest)WebRequest.Create(url);
                var response = request.GetResponse();
                var output = "";
                using (var stream = response.GetResponseStream())
                {
                    using (var readerStream = new StreamReader(stream))
                    {
                        output = readerStream.ReadToEnd();
                    }

                    // If file already exists, save the new file and note if the file size changed
                    if (fileExists)
                    {
                        // Count size of old file
                        byte[] oldBytes = File.ReadAllBytes("./data/" + filename);

                        // Overwrite with the new file
                        FileIO.SimpleWrite(form, "data", filename, output);

                        // Count size of new file
                        byte[] newBytes = File.ReadAllBytes("./data/" + filename);

                        // Compare size and note if there's a change
                        if (!oldBytes.SequenceEqual(newBytes)) result += "Changes detected in: " + filename + "\r\n";
                    }
                    else
                    {
                        // Overwrite with the new file
                        FileIO.SimpleWrite(form, "data", filename, output);
                    }
                }
            }
            catch(Exception ex)
            {
                result += "Error downloading " + filename + ": " + ex.Message + "\r\n";
            }

            return result;
        }



        /// <summary>
        /// CUSTOM CARDS MUST FOLLOW THIS FORMAT LINE-BY-LINE
        /// Id:75000 Set:9500
        /// Jotun's Left Arm
        /// Righteous Vindicator
        /// 50/150/1
        /// Summon 75001 OnPlay (remove spaces from any skills)
        /// Swipe 100
        /// Jam 2 every 3
        /// </summary>
        public static string ModifyCardXML(MainForm form)
        {
            // Get card modifications from config/customcards1-3.txt
            Dictionary<int, List<string>> modifiedCards = new Dictionary<int, List<string>>();

            // Also process custom cards in config/customcardsX.txt
            List<string> customCardLines = FileIO.SimpleRead(form, "config/customcards1.txt", returnCommentedLines: false, skipWhitespace: true);
            customCardLines.AddRange(FileIO.SimpleRead(form, "config/customcards2.txt", returnCommentedLines: false, skipWhitespace: true));
            customCardLines.AddRange(FileIO.SimpleRead(form, "config/customcards3.txt", returnCommentedLines: false, skipWhitespace: true));
            customCardLines.AddRange(FileIO.SimpleRead(form, "config/customcards4.txt", returnCommentedLines: false, skipWhitespace: true));
            customCardLines.AddRange(FileIO.SimpleRead(form, "config/customcards5.txt", returnCommentedLines: false, skipWhitespace: true));
            customCardLines.AddRange(FileIO.SimpleRead(form, "config/customcards6.txt", returnCommentedLines: false, skipWhitespace: true));

            var index = 100000;
            foreach(var x in customCardLines)
            {
                modifiedCards.Add(index, new List<string> { x });
                index++;
            }

            if (modifiedCards.Count == 0) return "";

            // Location to add new cards
            string xmlFilePath = "data/cards_section_19.xml";

            // What to send back
            StringBuilder result = new StringBuilder();

            // What part of the card are we on?
            CustomCardStep currentStep = CustomCardStep.ID;
            int totalCustomCards = 0;

            try
            {
                XDocument doc = XDocument.Load(xmlFilePath);
                XElement unit;

                // Card stats
                int cardId = -1;
                string cardName = "";
                string cardType = "assault"; // structure, commander, dominion
                int cardAttack = -1;
                int cardHealth = -1;
                int cardCost = -1;
                int cardRarity = -1;
                //int cardFusion = -1;
                int cardFaction = -1;
                int cardSet = -1;

                // Skills
                List<CardSkill> cardSkills = new List<CardSkill>();

                //Assault 62470 ends at 49999 and continues at 55001
                //Commander 26124 ends at 1999 continues at 25000 - 29999
                //Structures 18828(end at 2699) ends at 9999 continues at 17000 - 24999 Fortress 2701
                //Dominions 50238 ends at 55000


                // For each line in appsettings that starts with CC: 
                foreach (List<string> line in modifiedCards.Values)
                {
                    if (line.Count == 0) continue;
                    string[] customCardLineSplit = line[0].Split(' ');

                    // If we're in the step where we're looking for a CustomCard Id, silently ignore lines until we find a valid line
                    if (currentStep == CustomCardStep.ID && !customCardLineSplit[0].ToLower().StartsWith("id"))
                    {
                        continue;
                    }

                    // Take action depending on the step we're on
                    switch(currentStep)
                    {
                        case CustomCardStep.ID:
                            // Reset card variables
                            {   
                                cardId = -1;
                                cardName = "";
                                cardType = "assault";
                                cardAttack = -1;
                                cardHealth = -1;
                                cardCost = -1;
                                cardRarity = -1;
                                //cardFusion = -1;
                                cardFaction = -1;
                                cardSet = -1;

                                cardSkills = new List<CardSkill>();
                            }

                            // If we successfully parse this line, move on to the next step
                            if (customCardLineSplit.Count() == 2)
                            {
                                string cardIdString = customCardLineSplit[0].ToLower().Replace("id:", "");
                                string cardSetString = customCardLineSplit[1].ToLower().Replace("set:", "");
                                int.TryParse(cardIdString, out cardId);
                                int.TryParse(cardSetString, out cardSet);

                                if (cardId > 0 && cardSet > 0) currentStep = CustomCardStep.NAME;
                                else result.AppendLine("Id:##### or Set:#### is not recognized in this line: " + line[0]);
                            }
                            else if (customCardLineSplit.Count() == 1 && customCardLineSplit[0].ToLower().StartsWith("id"))
                            {
                                string cardIdString = customCardLineSplit[0].ToLower().Replace("id:", "");
                                int.TryParse(cardIdString, out cardId);
                                cardSet = 2000;

                                if (cardId > 0) currentStep = CustomCardStep.NAME;
                                else result.AppendLine("Id:##### is not recognized in this line: " + line[0]);
                            }
                            else
                            {
                                currentStep = CustomCardStep.ID;
                                result.AppendLine("Did not find the ID/Set in this line: " + line[0]);
                            }
                            break;


                        // Don't use the space splitter to get the full card name
                        case CustomCardStep.NAME:
                            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
                            cardName = textInfo.ToTitleCase(line[0].Trim());

                            currentStep = CustomCardStep.FACTION_RARITY_TYPE;
                            break;

                        // If we successfully parse this line, move on to the next step
                        case CustomCardStep.FACTION_RARITY_TYPE:
                            if (customCardLineSplit.Count() >= 2)
                            {
                                string faction = customCardLineSplit[0].ToLower();
                                string rarity = customCardLineSplit[1].ToLower();
                                
                                if (customCardLineSplit.Count() >= 3)
                                    cardType = customCardLineSplit[2].ToLower();

                                if (faction == "imperial" || faction == "imp") cardFaction = 1;
                                else if (faction == "raider" || faction == "rd") cardFaction = 2;
                                else if (faction == "bloodthirsty" || faction == "bt") cardFaction = 3;
                                else if (faction == "xeno" || faction == "xn") cardFaction = 4;
                                else if (faction == "righteous" || faction == "rt") cardFaction = 5;
                                else cardFaction = 6; // progenitor, progen, prog

                                if (rarity == "epic" || rarity == "3") cardRarity = 3;
                                else if (rarity == "legendary" || rarity == "leg" || rarity == "4") cardRarity = 4;
                                else if (rarity == "vindicator" || rarity == "vind" || rarity == "vindi" || rarity == "5") cardRarity = 5;
                                else cardRarity = 6; // mythic, myth

                                // Make sure cardType is right
                                if (cardType != "assault" && cardType != "structure" && cardType != "commander" && cardType != "dominion" && cardType != "tower")
                                {
                                    currentStep = CustomCardStep.ID;
                                    result.AppendLine("Invalid card type (assault, structure, commander, dominion, tower are valid): " + line[0]);
                                    break;
                                }

                                currentStep = CustomCardStep.STATS;
                            }
                            // Implied Mythic
                            else
                            {
                                string faction = customCardLineSplit[0].ToLower();
                                if (faction == "imperial" || faction == "imp") cardFaction = 1;
                                else if (faction == "raider" || faction == "rd") cardFaction = 2;
                                else if (faction == "bloodthirsty" || faction == "bt") cardFaction = 3;
                                else if (faction == "xeno" || faction == "xn") cardFaction = 4;
                                else if (faction == "righteous" || faction == "rt") cardFaction = 5;
                                else if (faction == "progenitor" || faction == "progen" || faction == "prog") cardFaction = 6;

                                if (cardFaction > 0) {
                                    cardRarity = 6;
                                    currentStep = CustomCardStep.STATS;
                                }
                                else
                                {
                                    currentStep = CustomCardStep.ID;
                                    result.AppendLine("Did not find the ID/Set in this line: " + line[0]);
                                }
                            }
                            break;

                        case CustomCardStep.STATS:
                            // If we successfully parse this line, move on to the next step
                            string[] statsSplit = customCardLineSplit[0].Split('/');
                            if (cardType == "assault" && statsSplit.Length >= 3)
                            {
                                int.TryParse(statsSplit[0], out cardAttack);
                                int.TryParse(statsSplit[1], out cardHealth);
                                int.TryParse(statsSplit[2], out cardCost);
                            }
                            else if ((cardType == "structure" || cardType == "dominion" || cardType == "tower") && statsSplit.Length >= 2)
                            {
                                int.TryParse(statsSplit[0], out cardHealth);
                                int.TryParse(statsSplit[1], out cardCost);
                            }
                            else if (cardType == "commander")
                            {
                                int.TryParse(statsSplit[0], out cardHealth);
                            }
                            else { } // dominion maybe?

                            if (cardHealth <= 0)
                            {
                                currentStep = CustomCardStep.ID;
                                result.AppendLine("Invalid card type (assault, structure, commander are valid): " + line[0]);
                                Console.WriteLine("Invalid card type (assault, structure, commander are valid): " + line[0]);
                                break;
                            }

                            currentStep = CustomCardStep.SKILL1;
                            break;

                        // Skills
                        // TODO: Need to distinguish a card with 5 skills
                        // * Maybe add a tag by set
                        case CustomCardStep.SKILL1:
                        case CustomCardStep.SKILL2:
                        case CustomCardStep.SKILL3:

                            CardSkill cardSkill = new CardSkill();
                            
                            // For each part of the skill (separated by a space)
                            for (int i=0; i< customCardLineSplit.Length; i++)
                            {
                                string skillPart = customCardLineSplit[i].ToLower();
                                int.TryParse(skillPart, out int skillNumber); // Numeric value of the skill, if it is a number

                                // If this skill is a string 
                                if (skillNumber <= 0)
                                {
                                    // Name of the skill is always first
                                    if (i == 0) cardSkill.id = skillPart;

                                    // All modifier
                                    else if (skillPart == "all") cardSkill.all = true;

                                    // Faction modifier
                                    else if (skillPart == "imperial" || skillPart == "imp") cardSkill.y = 1;
                                    else if (skillPart == "raider" || skillPart == "rd") cardSkill.y = 2;
                                    else if (skillPart == "bloodthirsty" || skillPart == "bt") cardSkill.y = 3;
                                    else if (skillPart == "xeno" || skillPart == "xn") cardSkill.y = 4;
                                    else if (skillPart == "righteous" || skillPart == "rt") cardSkill.y = 5;
                                    else if (skillPart == "progenitor" || skillPart == "progen" || skillPart == "prog") cardSkill.y = 6;

                                    // A string after evolve or enhance that's not a number
                                    // Enhance armored 20, enhance all armored 50 onPlay
                                    // Evolve Poison to Venom
                                    // Evolve 2 Legion to Battalion
                                    else if (cardSkill.id == "enhance" && cardSkill.skill1 == null)
                                    {
                                        cardSkill.skill1 = skillPart;
                                    }
                                    else if (cardSkill.id == "evolve" && skillNumber <= 0 && skillPart != "to")
                                    {
                                        // If the first skill is blank, add this skill here
                                        if (string.IsNullOrWhiteSpace(cardSkill.skill1)) cardSkill.skill1 = skillPart;

                                        // If the first skill is not blank, make this the second skill
                                        else cardSkill.skill2 = skillPart;
                                    }

                                    // Triggers
                                    else if (skillPart == "onplay") cardSkill.trigger = "play";
                                    else if (skillPart == "ondeath") cardSkill.trigger = "death";
                                    else if (skillPart == "onattacked") cardSkill.trigger = "attacked";
                                }

                                // If this skill is a number
                                else 
                                {
                                    // n: 2nd line in the skill, and the skill name is (jam, overload, enrage, flurry, enhance)
                                    if (i == 1 && (cardSkill.id == "jam" || cardSkill.id == "overload" || cardSkill.id == "enrage" || cardSkill.id == "flurry" || cardSkill.id == "enhance"))
                                    {
                                        cardSkill.n = skillNumber;
                                    }
                                    // c: check for "every" before this line (flurry 2 every 2, flurry every 3, jam 2 every 2, jam every 3)
                                    else if (i >= 1 && customCardLineSplit[i - 1].ToLower() == "every")
                                    {
                                        cardSkill.c = skillNumber;
                                    }
                                    // summon: if the skill name is summon, the skillNumber is what it summons
                                    else if (cardSkill.id == "summon")
                                    {
                                        // summonID: if the skill name is summon
                                        cardSkill.summon = skillNumber.ToString();
                                    }
                                    // otherwise, assume this is X
                                    else
                                    {
                                        cardSkill.x = skillNumber;
                                    }
                                }
                            }
                            
                            // Add to cardSkills
                            cardSkills.Add(cardSkill);

                            // Advance to the next skill, or add this to the XML
                            if (currentStep == CustomCardStep.SKILL1) currentStep = CustomCardStep.SKILL2;
                            else if (currentStep == CustomCardStep.SKILL2) currentStep = CustomCardStep.SKILL3;
                            else
                            {

                                unit = AddCardToXml(cardId, cardName, cardType, cardAttack, cardHealth, cardCost, (Faction)cardFaction, (Rarity)cardRarity, FusionLevel.Quad, (CardSet)cardSet);
                                foreach(var skill in cardSkills)
                                {
                                    int skillAll = skill.all ? 1 : 0;
                                    unit.Add(AddSkillToXml(skill.id, skill.x, skill.y, skillAll, skill.c, skill.n, skill.skill1, skill.skill2, skill.trigger, skill.summon));
                                }

                                //result.AppendLine("Added custom card: " + cardName);
                                totalCustomCards++;

                                doc.Root.Add(unit);
                                currentStep = CustomCardStep.ID;

                            }

                            // TODO: Option for skills 4/5?

                            break;
                        default:
                            break;
                    }

                }// all attributes

                result.AppendLine(totalCustomCards + " custom cards added");

                doc.Save(xmlFilePath);
                //doc.Save(xmlFilePath, SaveOptions.DisableFormatting);
            }
            catch (Exception ex)
            {
                Console.WriteLine("ModifyCardXml(): Error - " + ex.Message);
                result.AppendLine("ModifyCardXml(): Error - " + ex.Message);
            }

            return result.ToString();
        }

        enum CustomCardStep { ID, NAME, FACTION_RARITY_TYPE, STATS, SKILL1, SKILL2, SKILL3 }

        /// <summary>
        /// Create a base Xelement Xml Card minus skills
        /// </summary>
        private static XElement AddCardToXml(int id, string name,string cardType, int attack, int health, int cost, Faction type, Rarity rarity, FusionLevel fusionLevel = FusionLevel.Quad, CardSet cardset = CardSet.BoxOrReward)
        {
            XElement unit;

            // Assault: default
            if (cardType == "assault")
            {
                unit = new XElement("unit",
                    new XElement("id", id),
                    new XElement("name", name),
                    new XElement("attack", attack),
                    new XElement("health", health),
                    new XElement("cost", cost),
                    new XElement("type", (int)type),
                    new XElement("rarity", (int)rarity),
                    new XElement("fusion_level", (int)fusionLevel),
                    new XElement("set", (int)cardset)
                );
            }

            // Structure: No <attack> element
            else if (cardType == "structure" || cardType == "dominion")
            {
                unit = new XElement("unit",
                    new XElement("id", id),
                    new XElement("name", name),
                    new XElement("health", health),
                    new XElement("cost", cost),
                    new XElement("type", (int)type),
                    new XElement("rarity", (int)rarity),
                    new XElement("fusion_level", (int)fusionLevel),
                    new XElement("set", (int)cardset)
                );
            }

            // Commander: No <cost> element
            else if (cardType == "commander")
            {
                unit = new XElement("unit",
                    new XElement("id", id),
                    new XElement("name", name),
                    new XElement("attack", attack),
                    new XElement("health", health),
                    new XElement("type", (int)type),
                    new XElement("rarity", (int)rarity),
                    new XElement("fusion_level", (int)fusionLevel),
                    new XElement("set", 7000)
                );
            }

            else
            {
                unit = new XElement("unit",
                    new XElement("id", id),
                    new XElement("name", name),
                    new XElement("attack", attack),
                    new XElement("health", health),
                    new XElement("cost", cost),
                    new XElement("type", (int)type),
                    new XElement("rarity", (int)rarity),
                    new XElement("fusion_level", (int)fusionLevel),
                    new XElement("set", (int)cardset)
                );
            }

            return unit;
        }

        /// <summary>
        /// Generate an Xml element for a skill
        /// 
        /// s1/s2: Evolve
        /// Trigger: On Enter/Death
        /// Summon: ID
        /// </summary>
        private static XElement AddSkillToXml(string id, int x = -1, int y= -1, int all = -1, int c= -1, int n = -1, string s1 = "", string s2 = "", string trigger = "", string summon = "")
        {
            XElement element = new XElement("skill", new XAttribute("id", id));
            XAttribute attr;

            try
            {
                // Some skills are named differently in xml from in game. CHange them here
                if (id == "armor") id = "armored";
                if (id == "mortar") id = "besiege";

                // Add attributes to this skill
                if (x > 0)
                {
                    attr = new XAttribute("x", x);
                    element.Add(attr);
                }
                if (y > 0)
                {
                    attr = new XAttribute("y", y);
                    element.Add(attr);
                }
                if (all > 0)
                {
                    attr = new XAttribute("all", 1);
                    element.Add(attr);
                }
                if (n > 0)
                {
                    attr = new XAttribute("n", n);
                    element.Add(attr);
                }
                if (c > 0)
                {
                    attr = new XAttribute("c", c);
                    element.Add(attr);
                }
                if (!string.IsNullOrWhiteSpace(s1))
                {
                    attr = new XAttribute("s", s1);
                    element.Add(attr);
                }
                if (!string.IsNullOrWhiteSpace(s2))
                {
                    attr = new XAttribute("s2", s2);
                    element.Add(attr);
                }
                if (!string.IsNullOrWhiteSpace(trigger))
                {
                    attr = new XAttribute("trigger", trigger);
                    element.Add(attr);
                }
                if (!string.IsNullOrWhiteSpace(summon))
                {
                    attr = new XAttribute("card_id", summon);
                    element.Add(attr);
                }

            }
            catch(Exception ex)
            {
                Console.WriteLine("AddSkillToXml() failed " + ex.Message);
            }

            return element;
        }
    }
}
