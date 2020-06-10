using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using PirateTUO2.Classes;
using PirateTUO2.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace PirateTUO2.Modules
{
    // TODO: Try this for json parsing
    //using (var reader = new JsonTextReader(new StringReader(jsonText)))
    //{
    //    while (reader.Read())
    //    {
    //        Console.WriteLine("{0} - {1} - {2}", reader.TokenType, reader.ValueType, reader.Value);
    //    }
    //}

    /// <summary>
    /// This module communicates with the TU API
    /// </summary>
    public class ApiManager
    {
        public static string apiUrl = "https://mobile.tyrantonline.com/api.php";
        private static string clientSignature = "b0e95f827a3862019fe580bdc0bacd9f"; //This gets passed back from getUserAccount
        private static int dataUsage = 0;

        //private static int maxLogTransactions = 10;

        // TODO: Put this in appsettings as "knownUsers" or something

        

        /// <summary>
        /// Call the TU Api
        /// 
        /// If we don't have the user's current Id/KongToken, call "getUserAccount" first
        /// </summary>
        public static void CallApi(KongViewModel kongVm)
        {
            try
            {
                // Execute the request in vm.ApiStatName + vm.ApiParams
                var document = ExecuteApiRequest(kongVm);

                // Process webresponse, and handle results
                switch (kongVm.ApiStatName)
                {
                    // ---------------------------------
                    // Core
                    // ---------------------------------
                    // Gather all cards owned by this user. init is EXPENSIVE
                    case "init":
                        ApiInit(kongVm, document);
                        break;

                    case "getUserAccount":
                        ApiUserData(kongVm, document);
                        break;

                    // Get battle data / decks of the current or previous match
                    case "getBattleResults":
                        ApiGetBattleData(kongVm, document);
                        break;

                    // ---------------------------------
                    // Combat
                    // ---------------------------------
                    case "playCard": // play a card
                        ApiGetBattleData(kongVm, document);
                        break;
                    case "setUserFlag": // set auto flag (1=true, 0=false)
                        ApiSimpleCall(kongVm, document);
                        break;

                    // ---------------------------------
                    // Card Building 
                    // ---------------------------------
                    case "upgradeCard":
                        ApiUserData(kongVm, document);
                        Thread.Sleep(150);
                        break;
                    case "fuseCard":
                        ApiSimpleCall(kongVm, document);
                        Thread.Sleep(150);
                        break;
                    case "salvageCard":
                        ApiUserData(kongVm, document);
                        Thread.Sleep(10);
                        break;
                    case "salvageL1CommonCards":
                        ApiUserData(kongVm, document);
                        Thread.Sleep(50);
                        break;
                    case "salvageL1RareCards":
                        ApiUserData(kongVm, document);
                        Thread.Sleep(50);
                        break;
                    case "buybackCard":
                        ApiUserData(kongVm, document);
                        Thread.Sleep(250);
                        break;
                    case "setCardLock":
                        ApiSimpleCall(kongVm, document);
                        break;

                    // ---------------------------------
                    // Dominion / Deck Setting
                    // ---------------------------------
                    case "setDeckCards":
                    case "setDeckDominion":
                    case "respecDominionCard":
                    case "upgradeDominionCard":
                        ApiUserData(kongVm, document);
                        Thread.Sleep(150);
                        break;


                    // ---------------------------------
                    // Missions
                    // ---------------------------------
                    case "startMission": 
                    case "fightFactionQuest":
                        ApiGetBattleData(kongVm, document);
                        break;
                    case "buyEnergyRefillTokens":
                        ApiUserData(kongVm, document);
                        break;

                    // ---------------------------------
                    // PVP
                    // ---------------------------------
                    case "getHuntingTargets": // Get current pvp targets
                    case "buyRivalsRefresh": // Pvp refresh
                        ApiGetHuntingTargets(kongVm, document);
                        break;
                    // Start pvp battle
                    case "startHuntingBattle":
                        ApiGetBattleData(kongVm, document);
                        break;
                    case "buyStaminaRefillTokens":
                        ApiUserData(kongVm, document);
                        Thread.Sleep(5);
                        break;

                    // ---------------------------------
                    // Guild 
                    // ---------------------------------
                    case "recordEvent": //apiStatName, but the message is updateFaction
                    case "updateFaction":
                    case "buyFactionCard":
                        ApiUpdateFaction(kongVm, document);
                        break;
                    case "getProfileData": //target_user_id:"4005652"
                        ApiGetProfileData(kongVm, document);
                        break;
                    case "fightPracticeBattle":
                        ApiGetBattleData(kongVm, document);
                        break;
                    case "leaveFaction":
                    case "sendFactionInvite":
                    case "kickFactionMember":
                    case "acceptFactionInvite":
                    case "setMemberRank":
                        ApiUpdateFaction(kongVm, document);
                        break;
                    case "buyFactionBattlegroundEffect":
                        // Not sure what this returns yet
                        ApiUpdateFaction(kongVm, document);
                        break;

                    // ---------------------------------
                    // EVENT - Conquest 
                    // ---------------------------------
                    case "getGuildInfluenceLeaderboard":
                        ApiGetCqMemberLeaderboard(kongVm, document);
                        break;
                    case "getConquestUpdate":
                        ApiGetConquestUpdate(kongVm, document);
                        break;
                    case "fightConquestBattle":
                        ApiGetBattleData(kongVm, document); 
                        break;

                    // ---------------------------------
                    // EVENT - Raid 
                    // ---------------------------------
                    case "getRaidInfo":
                        ApiGetRaidMemberLeaderboard(kongVm, document);
                        break;
                    case "fightRaidBattle":
                        ApiGetBattleData(kongVm, document);
                        break;
                    case "claimRaidReward":
                    case "claimBrawlRewards":
                    case "claimConquestReward":
                    case "claimFactionWarRewards":
                        ApiSimpleCall(kongVm, document);
                        break;

                    // ---------------------------------
                    // EVENT - Brawl
                    // ---------------------------------
                    case "fightBrawlBattle":
                        ApiGetBattleData(kongVm, document);
                        break;

                    case "getBrawlMemberLeaderboard": // guild brawl
                        ApiGetBrawlMemberLeaderboard(kongVm, document);
                        break;

                    // ---------------------------------
                    // EVENT - War
                    // ---------------------------------
                    case "startFactionWarBattle":
                        ApiGetBattleData(kongVm, document);
                        break;

                    // ---------------------------------
                    // EVENT - Conquest
                    // ---------------------------------
                    case "getConquestData":
                        ApiGetConquestUpdate(kongVm, document);
                        break;

                    // ---------------------------------
                    // EVENT - Campaign
                    // ---------------------------------
                    case "startCampaign":
                    case "fightCampaignBattle":
                        ApiSimpleCall(kongVm, document);
                        break;

                    // ---------------------------------
                    // Store -
                    // ---------------------------------
                    case "buyStorePromoGold":
                        ApiUserData(kongVm, document);
                        Thread.Sleep(50);
                        break;
                    case "buyStorePromoTokens":
                        ApiBuyFromStore(kongVm, document);
                        Thread.Sleep(50);
                        break;

                    // ---------------------------------
                    // Items
                    // ---------------------------------
                    // Use player's shards (typically epic/legend packs)
                    case "consumeItem":
                        ApiSimpleCall(kongVm, document);
                        break;
                    case "resetCommanderCard": //card_id: xxxxx
                        ApiSimpleCall(kongVm, document);
                        break;
                    default:
                        break;
                }

                // Debug: Return the string that came back
                //string formattedResponseString = responseString.Replace(",", ",\r\n");
                //vm.StatusMessage = formattedResponseString;
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured: " + ex.Message;
            }

            //vm.StatusMessage += output.ToString();
        }

        /// <summary>
        /// Parses a string to get player info, then injects it into the KongViewModel
        /// 
        /// Format [kongname:blah,kongid:blah,kongtoken:blah,apipassword:blah,syncode:blah,userid:blah]
        /// 
        /// </summary>
        public static void GetKongInfoFromString(KongViewModel kongVm, string playerInfo)
        {
            List<string> settings = playerInfo.Split(new char[] { ',', '&' }).ToList();

            try
            {
                foreach (var setting in settings)
                {
                    List<string> settingParams = setting.Split(new char[] { ':', '=' }).ToList();
                    if (settingParams.Count == 2)
                    {
                        switch (settingParams[0].Trim().ToLower())
                        {
                            // The necessary pieces are either 
                            // * UserId plus correct kongToken (Gotten from a browser session)
                            // * UserId plus correct syncode and password

                            case "kn":
                            case "name":
                            case "kongname":
                            case "kong_name":
                                kongVm.KongName = settingParams[1].Trim();
                                break;
                            case "kongid":
                            case "kong_id":
                                kongVm.KongId = settingParams[1].Trim();
                                break;
                            case "kongtoken":
                            case "kong_token":
                                kongVm.KongToken = settingParams[1].Trim();
                                break;
                            case "password":
                            case "apipassword":
                                kongVm.Password = settingParams[1].Trim();
                                break;
                            case "syncode":
                                kongVm.Syncode = settingParams[1].Trim();
                                break;
                            case "userid":
                            case "user_id":
                                kongVm.UserId = settingParams[1].Trim();
                                break;

                            // Optional portion for organizing stuff
                            case "guild":
                                kongVm.Guild = settingParams[1].Trim();
                                break;
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: KongInfo format issue: " + ex.Message;
            }
        }



        #region Api Parsing

        /// <summary>
        /// Simple call where we only want the result flag
        /// </summary>
        private static void ApiSimpleCall(KongViewModel kongVm, JObject document)
        {
            try
            {
                // When there's an error, result / result_message will get returned
                var result = document["result"];
                var resultMessage = document["result_message"];

                if (result != null) // && resultMessage != null)
                {
                    kongVm.Result = document.Property("result").Value.ToString();
                    if (resultMessage != null)
                    {
                        kongVm.ResultMessage = document.Property("result_message").First().ToString();
                    }

                    if (kongVm.Result == "False")
                    {
                        Console.WriteLine("API Error on " + kongVm.Message);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured: " + ex.Message;
            }
        }

        /// <summary>
        /// Get response.user_data from api requests
        /// </summary>
        private static void ApiUserData(KongViewModel kongVm, JObject document, bool updateInventory=true)
        {
            try
            {
                // --------------
                // User data
                // --------------
                var userData = document["user_data"];
                if (userData != null)
                {
                    kongVm.UserData.Warbonds = userData["tokens"].Value<int>();
                    kongVm.UserData.Gold = userData["money"].Value<int>();

                    // Mission energy
                    kongVm.UserData.Energy = userData["energy"].Value<int>();

                    // Pvp energy
                    kongVm.UserData.Stamina = userData["stamina"].Value<int>();

                    kongVm.UserData.BattleEnergy = userData["battle_energy"].Value<int>();

                    // SP
                    kongVm.UserData.Salvage = userData["salvage"].Value<int>();
                    kongVm.UserData.MaxSalvage = userData["caps"]["max_salvage"].Value<int>();

                    // Active / Defense deck
                    kongVm.UserData.ActiveDeck = userData["active_deck"].Value<string>();
                    kongVm.UserData.DefenseDeck = userData["defense_deck"].Value<string>();

                    kongVm.UserData.MaxInventory = userData["caps"]["max_cards"].Value<int>();

                    // Daily bonus
                    //kongVm.DailyBonusAvailable = userData["daily_bonus"].Value<int>() == 1 ? true : false;
                    kongVm.DailyBonusAvailable = true;
                }

                // --------------
                // User decks - some calls return this
                // --------------
                var apiUserDecks = document["user_decks"];
                if (apiUserDecks != null)
                {
                    foreach (JProperty apiUserDeck in apiUserDecks)
                    {
                        try
                        {
                            // { 1: {deck_id: 1, commander_id: 25305, cards: {45699: 2, 46385: 2,...}}
                            string deckId = apiUserDeck.Name;
                            int? commanderId = apiUserDeck.First()["commander_id"].Value<int?>();
                            int? dominionId = apiUserDeck.First()?["dominion_id"]?.Value<int?>();

                            if (commanderId == null || commanderId <= 0) continue; // Commander null - this slot isn't used
                            if (dominionId == null) dominionId = 50003; // Default Alpha Type-A-1
                            JObject deckCards = (JObject)apiUserDeck.First()["cards"];

                            // Create the userDeck
                            UserDeck userDeck = new UserDeck();
                            userDeck.Id = deckId;

                            userDeck.Commander = CardManager.GetById(commanderId.Value);
                            userDeck.Dominion = CardManager.GetById(dominionId.Value);

                            foreach (var cardObject in deckCards)
                            {
                                int.TryParse(cardObject.Key, out int cardId);
                                int.TryParse(cardObject.Value.ToString(), out int cardCount);
                                
                                Card card = CardManager.GetById(cardId);
                                if (card != null && cardCount > 0)
                                {
                                    userDeck.Cards[card] = cardCount;
                                }
                            }

                            // Add the userDeck
                            kongVm.UserDecks.Add(userDeck);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Error getting user decks: " + ex.Message);
                        }
                    }
                }

                // --------------
                // When there's an error, result / result_message will get returned
                // --------------
                var result = document["result"];
                var resultMessage = document["result_message"];
                if (result != null && resultMessage != null)
                {
                    kongVm.Result = document.Property("result").Value.ToString();
                    kongVm.ResultMessage = document.Property("result_message").First().ToString();

                    if (kongVm.Result == "False")
                    {
                        Console.WriteLine("API Error on " + kongVm.Message);
                        return;
                    }
                }

                // --------------
                // If a new card was returned
                // --------------
                var newCard = document["new_card_id"];
                if (newCard != null)
                {
                    kongVm.ResultNewCardId = document.Property("new_card_id").Value.ToString();
                }

                // --------------
                // If cards were returned and we want to count them
                // --------------
                if (updateInventory)
                {
                    HelperApiPlayerInventory(kongVm, document);
                }


            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured: " + ex.Message;
            }
        }
        
        /// <summary>
        /// Handle the API Init() call
        /// </summary>
        private static void ApiInit(KongViewModel kongVm, JObject document)
        {
            StringBuilder output = new StringBuilder();

            // ------------------------
            // Is the user currently in battle?
            // ------------------------
            try
            {
                var apiBattleToResume = document["battle_to_resume"];
                if (apiBattleToResume != null)
                {
                    //string resume = document["battle_to_resume"].Value<int>;
                    //kongVm.BattleToResume = resume == "1" ? true : false;
                    if (document["battle_to_resume"].Value<string>() == "1") kongVm.BattleToResume = true;

                    // Set battle results
                    kongVm.Message = "getBattleResults";
                    var battleDoc = ExecuteApiRequest(kongVm);
                    ApiGetBattleData(kongVm, battleDoc);
                }
            }
            catch { }

            // ------------------------
            // Debug: Try to get syncode. This doesn't seem to come unless its part of the request
            // TODO: Don't pass anything in syncode
            // ------------------------
            try
            {
                //var apiBattleToResume = document["request"];
                //if (apiBattleToResume != null && apiBattleToResume["syncode"] != null)
                //{
                //    Console.WriteLine(apiBattleToResume["syncode"].Value<string>());
                //}
            }
            catch { }


            // ------------------------
            // What events are active?
            // ------------------------
            try
            {
                // -- Brawl --
                {
                    // Event data (start/endtime)
                    var activeBrawlData = document["active_brawl_data"];

                    if (!JsonExtensions.IsNullOrEmpty(activeBrawlData) && activeBrawlData["start_time"] != null)
                    {
                        string startTime = activeBrawlData["start_time"].Value<string>();
                        string endTime = activeBrawlData["end_time"].Value<string>();

                        kongVm.BrawlStartTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(startTime));
                        kongVm.BrawlEndTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(endTime));

                        if (DateTimeOffset.Now > kongVm.BrawlStartTime && DateTimeOffset.Now < kongVm.BrawlEndTime)
                        {
                            kongVm.BrawlActive = true;
                        }

                        if (DateTimeOffset.Now >= kongVm.BrawlEndTime)
                        {
                            kongVm.BrawlRewardsActive = true;
                        }
                    }

                    // Player data (energy, wins/losses)
                    var playerBrawlData = document["player_brawl_data"];
                    if (playerBrawlData.HasValues)
                    {
                        string claimedRewards = playerBrawlData?["claimed_rewards"]?.ToString();
                    }

                    if (!JsonExtensions.IsNullOrEmpty(playerBrawlData) && playerBrawlData["energy"] != null)
                    {
                        // Get brawl stats
                        int.TryParse(((JValue)playerBrawlData["energy"]?["battle_energy"])?.Value?.ToString(), out int energy);
                        int.TryParse(playerBrawlData?["wins"]?.ToString(), out int wins);
                        int.TryParse(playerBrawlData?["losses"]?.ToString(), out int losses);
                        int.TryParse(playerBrawlData?["points"]?.ToString(), out int points);
                        int.TryParse(playerBrawlData?["current_rank"]?.ToString(), out int currentRank);
                        int.TryParse(playerBrawlData?["win_streak"]?.ToString(), out int winStreak);
                        int.TryParse(playerBrawlData?["seeding"]?.ToString(), out int seeding); // Only TU gods know how this works

                        kongVm.BrawlData.Energy = energy;
                        kongVm.BrawlData.Wins = wins;
                        kongVm.BrawlData.Losses = losses;
                        kongVm.BrawlData.CurrentRank = currentRank;
                        kongVm.BrawlData.Points = points;
                        kongVm.BrawlData.WinStreak = winStreak;
                        kongVm.BrawlData.Seeding = seeding;

                        if (wins > 0 && points > 0)
                        {
                            kongVm.BrawlData.PointsPerWin = Math.Round(((double)points) / wins, 2);
                        }


                    }

                    // Leaderboard (guild scores)
                    if (kongVm.BrawlActive)
                    {
                        // Get the brawl_leaderboard element from result.
                        var brawlLeaderboard = document["brawl_leaderboard"];
                        if (brawlLeaderboard != null)
                        {
                            var players = brawlLeaderboard.Children();
                            foreach (var player in players)
                            {
                                BrawlLeaderboard brawlStat = new BrawlLeaderboard();

                                brawlStat.UserId = player["user_id"].Value<string>();
                                brawlStat.Name = player["name"].Value<string>();
                                brawlStat.Points = player["points"].Value<string>();
                                brawlStat.PointsRank = player["points_rank"].Value<string>();

                                // userId: { HuntingTarget }
                                //var map = (JProperty)player;
                                //int mapId = Int32.Parse(map.Name);
                                //var targetData = (JObject)map.Value;

                                //brawlStat.Name = targetData["name"]?.ToString() ?? "";
                                //brawlStat.Points = targetData["points"]?.ToString() ?? "";
                                //brawlStat.PointsRank = targetData["points_rank"]?.ToString() ?? "";

                                kongVm.BrawlLeaderboard.Add(brawlStat);
                            }
                        }
                    }
                }

                // Conquest
                {
                    // Event data (start/endtime)
                    var cqData = document["conquest_data"];
                    if (!JsonExtensions.IsNullOrEmpty(cqData) && cqData["start_time"] != null)
                    {
                        kongVm.ConquestRewardsActive = true;
                        string startTime = cqData["start_time"].Value<string>();
                        string endTime = cqData["end_time"].Value<string>();
                        
                        kongVm.ConquestStartTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(startTime));
                        kongVm.ConquestEndTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(endTime));
                        
                        if (DateTimeOffset.Now > kongVm.ConquestStartTime && DateTimeOffset.Now < kongVm.ConquestEndTime)
                        {
                            kongVm.ConquestActive = true;
                        }
                    }

                    // User energy and influence
                    if (!JsonExtensions.IsNullOrEmpty(cqData) && cqData?["user_conquest_data"]["energy"] != null)
                    {
                        string energy = ((JValue)cqData?["user_conquest_data"]?["energy"]?["battle_energy"])?.Value?.ToString();
                        int influence = ((JValue)cqData?["user_conquest_data"]?["influence"])?.Value<int>() ?? 0;

                        kongVm.ConquestActive = true;
                        kongVm.ConquestData.Energy = Int32.Parse(energy);
                        kongVm.ConquestData.Influence = influence;
                    }
                }

                // Raid 
                {
                    var raidData = document["raid_info"];

                    try
                    {
                        if (!JsonExtensions.IsNullOrEmpty(raidData))
                        {
                            // Look at response.raid_info for the current raid

                            JObject currentRaid = (JObject)raidData?.First()?.First();
                            if (currentRaid != null)
                            {
                                string energy = currentRaid?["energy"]["battle_energy"]?.ToString();
                                string id = currentRaid?["raid_id"]?.ToString();
                                string health = currentRaid?["health"]?.ToString();
                                string maxHealth = currentRaid?["max_health"]?.ToString();
                                string level = currentRaid?["raid_level"]?.ToString();
                                string raidLevelEnd = currentRaid?["raid_level_end"]?.ToString();

                                long.TryParse(raidLevelEnd, out long levelEnd);

                                kongVm.RaidActive = true;
                                kongVm.RaidData.Id = int.Parse(id);
                                kongVm.RaidData.Energy = int.Parse(energy);
                                kongVm.RaidData.Health = int.Parse(health);
                                kongVm.RaidData.MaxHealth = int.Parse(maxHealth);
                                kongVm.RaidData.Level = int.Parse(level);
                                kongVm.RaidData.LevelEnd = DateTimeOffset.FromUnixTimeSeconds(levelEnd);
                                kongVm.RaidData.TimeLeft = kongVm.RaidData.LevelEnd.Subtract(DateTimeOffset.Now);

                                // Get the raid scores
                                var players = currentRaid?["members"]?.Children();
                                foreach (var player in players)
                                {
                                    RaidLeaderboard raidPlayer = new RaidLeaderboard();

                                    raidPlayer.UserId = player.First()["member_id"].Value<string>();
                                    raidPlayer.Name = player.First()["member_name"].Value<string>();
                                    raidPlayer.Damage = player.First()["damage"].Value<string>();

                                    kongVm.RaidData.RaidLeaderboard.Add(raidPlayer);
                                }

                                // Check response.current_raids to see if the raid is actually active
                                string raidStartTime = document?["current_raids"]?.First()?.First()?["start_time"].ToString();
                                string raidEndTime = document?["current_raids"]?.First()?.First()?["end_time"].ToString();

                                if (!string.IsNullOrWhiteSpace(raidEndTime))
                                    kongVm.RaidEndTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(raidEndTime));
                                if (!string.IsNullOrWhiteSpace(raidEndTime))
                                    kongVm.RaidStartTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(raidStartTime));

                                if (DateTimeOffset.Now > kongVm.RaidStartTime && DateTimeOffset.Now < kongVm.RaidEndTime)
                                {
                                    kongVm.RaidActive = true;
                                }
                                else
                                {
                                    kongVm.RaidActive = false;
                                }
                                
                                // Check if raid is over, and if rewards need to be claimed
                                string claimedRewards = currentRaid?["claimed_rewards"]?.ToString();

                                if (DateTimeOffset.Now > kongVm.RaidEndTime)
                                    kongVm.RaidRewardsActive = true;
                                if (DateTimeOffset.Now > kongVm.RaidEndTime && claimedRewards != null && claimedRewards == "0")
                                {
                                    kongVm.ClaimedRaidRewards = false;
                                }

                            }
                        }
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine("kongVm: Failed reading raid data: " + ex.Message);
                    }
                }

                // War
                {
                    // Event data (start/endtime)
                    var activeWarEvent = document["active_war_event"];
                    if (!JsonExtensions.IsNullOrEmpty(activeWarEvent) && activeWarEvent["start_time"] != null)
                    {
                        string startTime = activeWarEvent["start_time"].Value<string>();
                        string endTime = activeWarEvent["end_time"].Value<string>();

                        kongVm.WarStartTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(startTime));
                        kongVm.WarEndTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(endTime));

                        if (DateTimeOffset.Now > kongVm.WarStartTime && DateTimeOffset.Now < kongVm.WarEndTime)
                        {
                            kongVm.WarActive = true;
                        }


                        if (DateTimeOffset.Now > kongVm.WarEndTime)
                        {
                            kongVm.WarRewardsActive = true;
                        }

                        // Unsure if war has this field - if it does this marks if we claimed rewards
                        string claimedRewards = activeWarEvent?["claimed_rewards"]?.ToString();
                        if (DateTimeOffset.Now > kongVm.WarEndTime && claimedRewards != null && claimedRewards == "1")
                        {
                            kongVm.ClaimedWarRewards = true;
                        }
                    }

                    var factionWar = document["faction_war"];
                    if (!JsonExtensions.IsNullOrEmpty(factionWar) && factionWar["battle_energy"] != null)
                    {
                        string energy = ((JValue)factionWar["battle_energy"])?.Value?.ToString();
                        kongVm.WarData.Energy = Int32.Parse(energy);

                        kongVm.WarData.AttackerFactionName = ((JValue)factionWar["attacker_faction_name"])?.Value?.ToString();
                        kongVm.WarData.DefenderFactionName = ((JValue)factionWar["defender_faction_name"])?.Value?.ToString();

                        string score = ((JValue)factionWar["attacker_faction_points"])?.Value?.ToString();
                        kongVm.WarData.AttackerScore = Int32.Parse(score);
                        score = ((JValue)factionWar["defender_faction_points"])?.Value?.ToString();
                        kongVm.WarData.DefenderScore = Int32.Parse(score);

                        // Guild BGE. Not always here
                        var attackerBgeToken = factionWar.SelectToken("attacker_faction_battleground");
                        var defenderBgeToken = factionWar.SelectToken("defender_faction_battleground");
                                      
                        if (attackerBgeToken != null && attackerBgeToken.HasValues)
                        {
                            try
                            {
                                string bgeId = attackerBgeToken.First().First().Value<string>();
                                kongVm.WarData.AttackerBGE = bgeId.ToString();

                                if (bgeId == "2001") kongVm.WarData.AttackerBGE = "Inspired";
                                else if (bgeId == "2002") kongVm.WarData.AttackerBGE = "Blightblast";
                                else if (bgeId == "2004") kongVm.WarData.AttackerBGE = "Triage";
                                else if (bgeId == "2005") kongVm.WarData.AttackerBGE = "Charged Up";
                                else if (bgeId == "2006") kongVm.WarData.AttackerBGE = "Combined Arms";
                                else if (bgeId == "2010") kongVm.WarData.AttackerBGE = "Progenitor Tech";
                                else if (bgeId == "2012") kongVm.WarData.AttackerBGE = "Divine Blessing";
                                else if (bgeId == "2016") kongVm.WarData.AttackerBGE = "Tartarian Gift";
                                else if (bgeId == "2017") kongVm.WarData.AttackerBGE = "Artillery";
                                else if (bgeId == "2020") kongVm.WarData.AttackerBGE = "Emergency Aid";
                                else if (bgeId == "2022") kongVm.WarData.AttackerBGE = "Mirror Madness";
                                else if (bgeId == "2026") kongVm.WarData.AttackerBGE = "Winter Tempest";
                                else if (bgeId == "2028") kongVm.WarData.AttackerBGE = "Landmine";
                                else if (bgeId == "2029") kongVm.WarData.AttackerBGE = "Plasma Burst";
                                else if (bgeId == "2030") kongVm.WarData.AttackerBGE = "Sandblast";
                                else if (bgeId == "2035") kongVm.WarData.AttackerBGE = "Halcyon's Command";
                            }
                            catch (Exception ex) { Console.WriteLine(ex); }
                        }
                        if (defenderBgeToken != null && defenderBgeToken.HasValues)
                        {
                            try
                            {
                                string bgeId = defenderBgeToken.First().First().Value<string>();
                                kongVm.WarData.DefenderBGE = bgeId.ToString();

                                if (bgeId == "2001") kongVm.WarData.DefenderBGE = "Inspired";
                                else if (bgeId == "2002") kongVm.WarData.DefenderBGE = "Blightblast";
                                else if (bgeId == "2004") kongVm.WarData.DefenderBGE = "Triage";
                                else if (bgeId == "2005") kongVm.WarData.DefenderBGE = "Charged Up";
                                else if (bgeId == "2006") kongVm.WarData.DefenderBGE = "Combined Arms";
                                else if (bgeId == "2010") kongVm.WarData.DefenderBGE = "Progenitor Tech";
                                else if (bgeId == "2012") kongVm.WarData.DefenderBGE = "Divine Blessing";
                                else if (bgeId == "2016") kongVm.WarData.DefenderBGE = "Tartarian Gift";
                                else if (bgeId == "2017") kongVm.WarData.DefenderBGE = "Artillery";
                                else if (bgeId == "2020") kongVm.WarData.DefenderBGE = "Emergency Aid";
                                else if (bgeId == "2022") kongVm.WarData.DefenderBGE = "Mirror Madness";
                                else if (bgeId == "2026") kongVm.WarData.DefenderBGE = "Winter Tempest";
                                else if (bgeId == "2028") kongVm.WarData.DefenderBGE = "Landmine";
                                else if (bgeId == "2029") kongVm.WarData.DefenderBGE = "Plasma Burst";
                                else if (bgeId == "2030") kongVm.WarData.DefenderBGE = "Sandblast";
                                else if (bgeId == "2035") kongVm.WarData.DefenderBGE = "Halcyon's Command";
                            }
                            catch (Exception ex) { Console.WriteLine(ex); }
                        }




                        // TODO: Extract fortresses

                        // Player scoreboard data
                        var players = factionWar["attacker_faction_members"].Children();
                        foreach (var player in players)
                        {
                            WarLeaderboard attackingPlayer = new WarLeaderboard();

                            attackingPlayer.UserId = player["member_id"].Value<string>();
                            attackingPlayer.Name = player["member_name"].Value<string>();
                            attackingPlayer.Energy = player["battle_energy"].Value<int>();
                            attackingPlayer.Score = player["current_war_points"].Value<int>();
                            attackingPlayer.TotalScore = player["faction_war_points"].Value<int>();
                            attackingPlayer.Wins = player["wins"].Value<int>();
                            attackingPlayer.Losses = player["losses"].Value<int>();
                            attackingPlayer.DefenseWins = player["defense_wins"].Value<int>();
                            attackingPlayer.DefenseLosses = player["defense_losses"].Value<int>();
                            attackingPlayer.Winstreak = player["win_streak"].Value<int>();

                            kongVm.WarData.AttackerLeaderboard.Add(attackingPlayer);
                        }
                        players = factionWar["defender_faction_members"].Children();
                        foreach (var player in players)
                        {
                            WarLeaderboard defendingPlayer = new WarLeaderboard();

                            defendingPlayer.UserId = player["member_id"].Value<string>();
                            defendingPlayer.Name = player["member_name"].Value<string>();
                            defendingPlayer.Energy = player["battle_energy"].Value<int>();
                            defendingPlayer.Score = player["current_war_points"].Value<int>();
                            defendingPlayer.TotalScore = player["faction_war_points"].Value<int>();
                            defendingPlayer.Wins = player["wins"].Value<int>();
                            defendingPlayer.Losses = player["losses"].Value<int>();
                            defendingPlayer.DefenseWins = player["defense_wins"].Value<int>();
                            defendingPlayer.DefenseLosses = player["defense_losses"].Value<int>();
                            defendingPlayer.Winstreak = player["win_streak"].Value<int>();

                            kongVm.WarData.DefenderLeaderboard.Add(defendingPlayer);
                        }
                    }
                }

                // Campaign
                {
                    var currentCampaignData = document["current_campaigns"];
                    if (!JsonExtensions.IsNullOrEmpty(currentCampaignData))
                    {
                        string id = ((JObject)currentCampaignData.First().First())["id"].ToString();
                        int.TryParse(id, out int campaignId);
                        kongVm.CampaignData.Id = campaignId;

                        string levelCompleted = ((JObject)currentCampaignData.First().First())["level_completed"].ToString();
                        int.TryParse(levelCompleted, out int campaignLevelCompleted);
                        kongVm.CampaignData.LevelCompleted = campaignLevelCompleted;

                        string maxProgress = ((JObject)currentCampaignData.First().First())["max_progress"].ToString();
                        int.TryParse(id, out int campaignMaxProgress);
                        kongVm.CampaignData.MaxProgress = campaignMaxProgress;

                        string startTime = ((JObject)currentCampaignData.First().First())["start_time"].ToString();
                        string endTime = ((JObject)currentCampaignData.First().First())["end_time"].ToString();

                        kongVm.CampaignStartTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(startTime));
                        kongVm.CampaignEndTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(endTime));

                        if (DateTimeOffset.Now > kongVm.CampaignStartTime && DateTimeOffset.Now < kongVm.CampaignEndTime)
                        {
                            kongVm.CampaignActive = true;
                        }

                        // Hacky way to figure out which difficulty is not complete
                        kongVm.CampaignData.NormalRewardsToCollect = false;
                        kongVm.CampaignData.HeroicRewardsToCollect = false;
                        kongVm.CampaignData.MythicRewardsToCollect = false;

                        // Check for "collected": "0", or "amount": "2", "collected": "1". If either exist, there's a normal reward to collect
                        var normalRewards = ((JObject)currentCampaignData.First().First())["rewards"]["1"];
                        foreach (var rewards in normalRewards)
                        {
                            if (rewards.ToString().Contains("\"collected\": \"0\"") ||
                                rewards.ToString().Contains("\"amount\": \"2\",\r\n\"collected\": \"1\"") ||
                                rewards.ToString().Contains("\"amount\": \"2\",\r\n\t\"collected\": \"1\""))
                            {
                                kongVm.CampaignData.NormalRewardsToCollect = true;
                                break;
                            }
                        }
                        var heroicRewards = ((JObject)currentCampaignData.First().First())["rewards"]["2"];
                        foreach (var rewards in heroicRewards)
                        {
                            if (rewards.ToString().Contains("\"collected\": \"0\""))
                            {
                                kongVm.CampaignData.HeroicRewardsToCollect = true;
                                break;
                            }
                        }
                        var mythicRewards = ((JObject)currentCampaignData.First().First())["rewards"]["3"];
                        foreach (var rewards in mythicRewards)
                        {
                            if (rewards.ToString().Contains("\"collected\": \"0\""))
                            {
                                kongVm.CampaignData.MythicRewardsToCollect = true;
                                break;
                            }
                        }

                    }
                }

            }
            catch (Exception ex)
            {
                //kongVm.Result = "False";
                Console.WriteLine("Error getting init() event data: " + ex);
            }

            // ------------------------
            // What missions have been done?
            // ------------------------
            try
            {
                var missionCompletions = document["mission_completions"];
                if (missionCompletions != null)
                {
                    foreach (JProperty missionCompletion in missionCompletions)
                    {
                        Int32.TryParse(missionCompletion.Name, out int missionId);
                        JToken mission = missionCompletion.Value;

                        if (missionId > 0)
                        {
                            MissionCompletion mc = new MissionCompletion();
                            mc.Id = missionId;
                            mc.NumberOfCompletions = mission["number"].Value<int>();
                            kongVm.MissionCompletions.Add(mc);
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error getting init() mission data: " + ex);
            }

            // ------------------------
            // What quests are active?
            // ------------------------
            try
            {
                var userAchievements = document["user_achievements"];
                if (userAchievements != null)
                {
                    foreach (JProperty userAchievement in userAchievements)
                    {
                        var questInfo = userAchievement.Value;

                        Quest quest = new Quest();
                        quest.Id = questInfo["achievement_id"].Value<int>();
                        quest.Name = questInfo["name"].Value<string>();
                        quest.Progress = questInfo["progress"].Value<int>();
                        quest.MaxProgress = questInfo["max_progress"].Value<int>();

                        // Not always here
                        quest.QuestId = questInfo?["quest_id"]?.Value<int>() ?? -1;

                        if (questInfo?["req"]?["mission"] != null)
                        {
                            quest.MissionId = questInfo["req"]["mission"].Value<int>();
                        }

                        kongVm.Quests.Add(quest);
                    }
                }

            }
            catch (Exception ex)
            {
                //kongVm.Result = "False";
                Quest quest = new Quest { Id = -999, Name = "Error pulling quest data" };
                kongVm.Quests.Add(quest);

                Console.WriteLine("Error getting init() quest data: " + ex);
            }

            // -----------------------
            // Guild name, basic guild data
            // - ApiUpdateFaction() calls ApiUserData() to get player cards
            // -----------------------
            try
            {
                ApiUpdateFaction(kongVm, document);
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured: " + ex.Message;
                return;
            }

            // ------------------------
            // User data and cards if they exist
            // ------------------------
            //try
            //{
            //    ApiUserData(kongVm, document);
                
            //    kongVm.UserData.MaxEnergy = document["max_energy"].Value<int>();
            //    kongVm.UserData.MaxStamina = document["max_stamina"].Value<int>();

            //    if (kongVm.Result == "False") return;
                
            //}
            //catch (Exception ex)
            //{
            //    kongVm.Result = "False";
            //    kongVm.ResultMessage += "PTUO: An exception has occured: " + ex.Message;
            //    return;
            //}

            // ------------------------
            // TODO: I think this is repeated. Remove it?
            // ------------------------
            try
            {
                // Get the user_decks element from result. This should contain the player's decks
                var apiUserDecks = document["user_decks"];

                if (apiUserDecks != null)
                {
                    foreach (JProperty apiUserDeck in apiUserDecks)
                    {
                        try
                        {
                            // { 1: {deck_id: 1, commander_id: 25305, cards: {45699: 2, 46385: 2,...}}
                            string deckId = apiUserDeck.Name;
                            int? commanderId = apiUserDeck.First()["commander_id"].Value<int?>();
                            int? dominionId = apiUserDeck.First()?["dominion_id"]?.Value<int?>();

                            if (commanderId == null || commanderId <= 0) continue; // Commander null - this slot isn't used
                            if (dominionId == null) dominionId = 50003; // Default Alpha Type-A-1

                            JObject deckCards = (JObject)apiUserDeck.First()["cards"];

                            // Create the userDeck
                            UserDeck userDeck = new UserDeck();
                            userDeck.Id = deckId;

                            userDeck.Commander = CardManager.GetById(commanderId.Value);
                            userDeck.Dominion = CardManager.GetById(dominionId.Value);

                            foreach (var cardObject in deckCards)
                            {
                                int.TryParse(cardObject.Key, out int cardId);
                                int.TryParse(cardObject.Value.ToString(), out int cardCount);

                                Card card = CardManager.GetById(cardId);
                                
                                if (card != null && cardCount > 0)
                                {
                                    userDeck.Cards[card] = cardCount;
                                }
                            }

                            // Add the userDeck
                            kongVm.UserDecks.Add(userDeck);
                        }
                        catch (Exception ex)
                        {
                            output.AppendLine("Error getting user decks: " + ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("init(): user decks broke\r\n: " + ex);
            }

            // ------------------------
            // Items
            // ------------------------
            try
            {
                // Add user items. Relevant items:
                //2100: Commander Respec, 2000: Epic Shard, 2001: Legendary Shard
                var apiItems = document["user_items"];

                if (apiItems != null)
                {
                    foreach (JProperty apiItem in apiItems)
                    {
                        try
                        {
                            // {1012: {number: "1", num_used: "0"}, 1021: {...}, ...}
                            string itemName = apiItem.Name;
                            int? numberOfItems = apiItem.First()["number"].Value<int?>();
                            
                            kongVm.Items.Add(itemName, numberOfItems ?? 0);
                        }
                        catch (Exception ex)
                        {
                            output.AppendLine("Error getting user items: " + ex.Message);
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured: " + ex.Message;
                return;
            }


        }

        /// <summary>
        /// Get response.faction - guild data
        /// </summary>
        private static void ApiUpdateFaction(KongViewModel kongVm, JObject document)
        {
            try
            {
                var faction = document["faction"];

                // Get Faction name, message, and players/IDs
                if (faction != null && faction["faction_id"].Value<string>() != "0")
                {
                    kongVm.Faction.Id = faction["faction_id"].Value<string>();
                    kongVm.Faction.Name = faction["name"].Value<string>();
                    kongVm.Faction.Message = faction["message"].Value<string>();

                    // Guild supplies and gems - see if this fails during a guild event reset
                    kongVm.Faction.GuildPoints = faction?["guild_points"]?.Value<int>() ?? 0;

                    if (faction["faction_items"].HasValues)
                        kongVm.Faction.GuildSupplies = faction?["faction_items"]?["4001"]?["number"]?.Value<int>() ?? 0;

                    kongVm.UserData.MaxEnergy = document["max_energy"].Value<int>();
                    kongVm.UserData.MaxStamina = document["max_stamina"].Value<int>();
                    kongVm.DailyBonusAvailable = document["user_data"]["daily_bonus"].Value<int>() == 1 ? true: false;

                    // TODO: Get the list of BGEs
                    // battleground_data: {
                    // 2001: {
                    //      id: 2001, name: "Inspired", desc: "..", 
                    //          war_event_config: { 
                    //          cooldown: "57600", duration: "28800", item_cost: {id: "4001", number: "500" }, num_available: "2", start_time: 0
                    //          }, 
                    // 2002: {...}, 2003: {...} 
                    // }

                    // TODO: Get owned towers
                    // faction_cards: { 2724: { 2724: num_owned: "0" }, 2725, etc } <- this only displays the towers if at least one is bought, and all 4 levels of that tower


                    // Get the hunting_targets element from result.
                    var memberTokens = faction.SelectToken("members").Children();
                    foreach (var token in memberTokens)
                    {
                        FactionMember member = new FactionMember
                        {
                            Name = token.First()["name"].ToString(),
                            UserId = token.First()["user_id"].ToString(),
                            //LastUpdateTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(token.First()["last_update_time"].ToString())),

                            LastUpdateTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(token.First()["last_update_time"].ToString()))
                                                .Subtract(DateTimeOffset.Now),
                            Level = token.First()["level"].Value<int>(),
                            Rating = token.First()["rating"].Value<int>(),
                            Role = token.First()["member_role"].Value<int>()
                        };

                        kongVm.Faction.Members.Add(member);
                    }

                    // 11, 12: Defense towers (if card_id is null, no tower is there)
                    // 21, 22: Offense towers
                    JToken fortressSlots = document["fortress_slots"];
                    if (fortressSlots != null)
                    {
                        int[] slots = new int[] { 11, 12, 21, 22 };

                        foreach (var slot in slots)
                        {
                            int.TryParse(fortressSlots?[slot.ToString()]?["card_id"]?.Value<string>(), out int cardId);
                            if (cardId > 0)
                            {
                                Card card = CardManager.GetById(cardId);

                                WarFortress fort = new WarFortress
                                {
                                    CardId = cardId.ToString(),
                                    Name = card?.Name ?? cardId.ToString(),
                                    Health = "4000"
                                };

                                if (slot == 11) kongVm.Faction.Fort11 = fort;
                                else if (slot == 12) kongVm.Faction.Fort12 = fort;
                                else if (slot == 21) kongVm.Faction.Fort21 = fort;
                                else kongVm.Faction.Fort22 = fort;

                            }
                        }


                        //kongVm.Faction.AttackTowers.Add();
                    }

                }
                else
                {
                    kongVm.Faction.Name = "_UNGUILDED";
                }

                // Pull user data
                ApiUserData(kongVm, document);

                // Pull player energy data
                kongVm.UserData.MaxEnergy = document["max_energy"].Value<int>();
                kongVm.UserData.MaxStamina = document["max_stamina"].Value<int>();

                // Don't need to pull this

                //// ------------------------
                //// What quests are active?
                //// ------------------------
                //try
                //{
                //    var userAchievements = document["user_achievements"];
                //    if (userAchievements != null)
                //    {
                //        foreach (JProperty userAchievement in userAchievements)
                //        {
                //            var questInfo = userAchievement.Value;

                //            Quest quest = new Quest();
                //            quest.Id = questInfo["achievement_id"].Value<int>();
                //            quest.Name = questInfo["name"].Value<string>();
                //            quest.Progress = questInfo["progress"].Value<int>();
                //            quest.MaxProgress = questInfo["max_progress"].Value<int>();

                //            // Not always here
                //            quest.QuestId = questInfo?["quest_id"]?.Value<int>() ?? -1;

                //            if (questInfo?["req"]?["mission"] != null)
                //            {
                //                quest.MissionId = questInfo["req"]["mission"].Value<int>();
                //            }

                //            kongVm.Quests.Add(quest);
                //        }
                //    }
                //}
                //catch (Exception ex)
                //{
                //    //kongVm.Result = "False";
                //    Quest quest = new Quest { Id = -999, Name = "Error pulling quest data" };
                //    kongVm.Quests.Add(quest);

                //    Console.WriteLine("Error getting init() quest data: " + ex);
                //}


                // TODO: See if war_data comes with the update faction call (it should)
                // * Populate war data object with enemy BGE and stuff


                // When there's an error, result / result_message will get returned
                var result = document["result"];
                var resultMessage = document["result_message"];
                if (result != null && resultMessage != null)
                {
                    kongVm.Result = document.Property("result").Value.ToString();
                    kongVm.ResultMessage = document.Property("result_message").First().ToString();

                    if (kongVm.Result == "False")
                    {
                        Console.WriteLine("API Error on " + kongVm.Message);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured: " + ex.Message;
            }
        }

        /// <summary>
        /// Get response.player_info - data for one member
        /// </summary>
        private static void ApiGetProfileData(KongViewModel kongVm, JObject document)
        {
            try
            {
                var doc = document["player_info"];

                PlayerInfo pi = new PlayerInfo();
                pi.UserId = doc["user_id"].ToString();
                pi.Name = doc["name"].ToString();
                // TODO: Get date diff between now and then
                pi.LastUpdateTime = ((int)(DateTimeOffset.Now - DateTimeOffset.FromUnixTimeSeconds(long.Parse(doc["last_update_time"].ToString()))).TotalSeconds).ToString();

                // Get the deck and defense_deck from player_info
                pi.ActiveDeck = new UserDeck();
                pi.DefenseDeck = new UserDeck();
                                
                var deck = doc["deck"];
                if (deck != null)
                {
                    try
                    {
                        // {deck_id: 1, commander_id: 25305, cards: [45699, 45699, 46385, ...]

                        int.TryParse(deck["commander_id"].Value<object>().ToString(), out int commanderId);
                        int.TryParse(deck["dominion_id"].Value<object>().ToString(), out int dominionId);
                        var deckCards = deck["cards"].Values();

                        // Create the userDeck
                        UserDeck userDeck = new UserDeck();
                        userDeck.Commander = CardManager.GetById(commanderId);
                        userDeck.Dominion = CardManager.GetById(dominionId);

                        foreach (var cardObject in deckCards)
                        {
                            Card card = CardManager.GetById(cardObject.ToString());
                            if (card != null)
                            {
                                if (!userDeck.Cards.ContainsKey(card)) userDeck.Cards[card] = 1;
                                else userDeck.Cards[card]++;
                            }
                        }
                        
                        // Assign to UserInfo
                        pi.ActiveDeck = userDeck;
                    }
                    catch (Exception ex)
                    {
                        kongVm.Result = "False";
                        kongVm.ResultMessage += "PTUO: An exception has occured: " + ex.Message;
                    }
                }

                // Defense deck
                deck = doc["defense_deck"];
                if (deck != null)
                {
                    try
                    {
                        // {deck_id: 1, commander_id: 25305, cards: [45699, 45699, 46385, ...]
                        int.TryParse(deck["commander_id"].Value<object>().ToString(), out int commanderId);
                        int.TryParse(deck["dominion_id"].Value<object>().ToString(), out int dominionId);
                        var deckCards = deck["cards"].Values();

                        // Create the userDeck
                        UserDeck userDeck = new UserDeck();
                        userDeck.Commander = CardManager.GetById(commanderId);
                        userDeck.Dominion = CardManager.GetById(dominionId);

                        foreach (var cardObject in deckCards)
                        {
                            Card card = CardManager.GetById(cardObject.ToString());
                            if (card != null)
                            {
                                if (!userDeck.Cards.ContainsKey(card)) userDeck.Cards[card] = 1;
                                else userDeck.Cards[card]++;
                            }
                        }

                        // Assign to UserInfo
                        pi.DefenseDeck = userDeck;
                    }
                    catch (Exception ex)
                    {
                        kongVm.Result = "False";
                        kongVm.ResultMessage += "PTUO: An exception has occured: " + ex.Message;
                    }
                }

                kongVm.PlayerInfo.Add(pi);
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured: " + ex.Message;
            }
        }

        /// <summary>
        /// Get response.battle_data call
        /// </summary>
        private static void ApiGetBattleData(KongViewModel kongVm, JObject document)
        {
            StringBuilder output = new StringBuilder();
            BattleData battleData = new BattleData();
            Card card;
            string value;

            try
            {
                // Get the user_cards element from result. This should contain all cards the player could have
                var bd = document["battle_data"];
                if (bd != null)
                {
                    // Is player attacking? This sets up who the "attacker" and "defender" are in the API
                    battleData.IsAttacker = bool.Parse(bd.SelectToken("host_is_attacker").ToString());

                    // --------------------------------------------
                    // Get Turn number and simple metadata
                    // --------------------------------------------
                    value = "-1";
                    var turnInfoToken = bd.SelectToken("turn").Children();
                    foreach (var t in turnInfoToken)
                    {
                        value = ((JProperty)t).Name;
                    }
                    battleData.Turn = value;

                    // Player ID
                    battleData.EnemyId = bd.SelectToken("enemy_id")?.ToString();

                    // Deck size
                    battleData.PlayerSize = bd.SelectToken("player_size")?.ToString();
                    battleData.EnemySize = bd.SelectToken("enemy_size")?.ToString();
                    //battleData.Hds = battleDataTokens.SelectToken("hds").ToString();
                    //battleData.Eds = battleDataTokens.SelectToken("eds").ToString();

                    // Deck Power
                    battleData.PlayerPower = bd.SelectToken("defend_power")?.ToString();
                    battleData.EnemyPower = bd.SelectToken("attack_power")?.ToString();

                    // Enemy name
                    //TODO: Attempt a guild grab
                    if (bd.SelectToken("enemy_name") != null)
                    {
                        battleData.EnemyName = bd.SelectToken("enemy_name")?.ToString();
                    }
                    else
                    {
                        battleData.EnemyName = "Unknown";
                    }


                    // --------------------------------------------
                    // Deck data
                    // --------------------------------------------

                    // Commanders
                    int atkCommanderId = bd.SelectToken("attack_commander").ToObject<int>();
                    int defCommanderId = bd.SelectToken("defend_commander").ToObject<int>();

                    string attackCommander = CardManager.GetById(atkCommanderId) != null ? CardManager.GetById(atkCommanderId).Name : "Cyrus";
                    string defendCommander = CardManager.GetById(defCommanderId) != null ? CardManager.GetById(defCommanderId).Name : "Cyrus";

                    if (battleData.IsAttacker)
                    {
                        battleData.PlayerCommander = attackCommander;
                        battleData.EnemyCommander = defendCommander;
                    }
                    else
                    {
                        battleData.PlayerCommander = defendCommander;
                        battleData.EnemyCommander = attackCommander;
                    }


                    // BGEs
                    var globalBgeToken = bd.SelectToken("battleground_effects.global");
                    var attackerBgeToken = bd.SelectToken("battleground_effects.attacker");
                    var defenderBgeToken = bd.SelectToken("battleground_effects.defender");

                    var globalBge = "";
                    var attackerBge = "";
                    var defenderBge = "";

                    // This seems to sometimes have a {carduid: 0} on a blank BGE
                    if (globalBgeToken != null && globalBgeToken.HasValues)
                    {
                        try
                        {
                            var bgeToken = globalBgeToken.First().First(); //was breaking on card_uid, but meh
                            int tokenValues = bgeToken.Values().Count();
                            foreach (var val in bgeToken)
                            {
                                if (val is JProperty)
                                {
                                    var name = ((JProperty)val).Name;
                                    var v = ((JProperty)val).Value;
                                    if (name == "name")
                                    {
                                        globalBge = v.ToString();
                                        globalBge = bgeToken["name"] != null ? bgeToken["name"].ToString().Replace(" ", "-") : "";


                                        // --- Global Battleground Effect (GBGE) configurations ---
                                        // TODO: Add this to config
                                        // * Effects from the game can have a variable that does not show, and behaves differently from tuo.exe
                                        //
                                        //      ex: Devour': Units with refresh that deal damage permanently gain X/4 attack and X/4 health
                                        //          * In tuo this would be 'Devour 4'. 
                                        //          * From the TU API this would be 'Devour'
                                        //          * This could be variable (a stronger Devour would be X/2) so we need to adjust our formula

                                        // Replace Devour with Devour 4 (x/4). Do other effects need this?
                                        if (globalBge == "Devour") globalBge = "Devour 4";
                                        else if (globalBge == "Heroism") globalBge = "Heroism 2";
                                        else if (globalBge == "Superheroism") globalBge = "Superheroism 2";
                                        // Superheroism: Starts at x, but could be greater?

                                        break;
                                    }

                                }
                            }
                        }
                        catch (Exception ex) { Console.WriteLine(ex); }
                    }

                    if (attackerBgeToken != null && attackerBgeToken.HasValues)
                    {
                        try
                        {
                            attackerBge = attackerBgeToken?.First()?.First()?["name"]?.ToString();
                            if (attackerBge == null) attackerBge = "";
                        }
                        catch { }
                    }

                    if (defenderBgeToken != null && defenderBgeToken.HasValues)
                    {
                        try
                        {
                            defenderBge = defenderBgeToken?.First()?.First()?["name"]?.ToString();
                            if (defenderBge == null) defenderBge = "";
                        }
                        catch { }
                    }

                    battleData.BGE = globalBge;

                    if (battleData.IsAttacker)
                    {
                        battleData.PlayerBGE = defenderBge;
                        battleData.EnemyBGE = attackerBge;
                        //    battleData.PlayerBGE = attackerBge;
                        //    battleData.EnemyBGE = defenderBge;
                    }
                    else
                    {
                        battleData.PlayerBGE = attackerBge;
                        battleData.EnemyBGE = defenderBge;
                        //battleData.PlayerBGE = defenderBge;
                        //battleData.EnemyBGE = attackerBge;
                    }


                    // battleData.PlayerDrawOrder - Get draw order of player's deck
                    JEnumerable<JToken> playerDrawOrder = new JEnumerable<JToken>();
                    if (battleData.IsAttacker)
                        playerDrawOrder = bd.SelectToken("attack_deck").Children();
                    else
                        playerDrawOrder = bd.SelectToken("defend_deck").Children();

                    foreach (JProperty playerDrawCard in playerDrawOrder)
                    {
                        // Get the map id
                        int.TryParse(playerDrawCard.Name, out int cardMapId);
                        string cardId = playerDrawCard.Value.ToString();

                        // Get the TU card ID and try to find it

                        // Get the TU card ID and add the card or its cardId if not found
                        card = CardManager.GetById(cardId);
                        string cardString = card != null ? card.Name : "[" + cardId.ToString() + "]";

                        battleData.PlayerDrawOrder.Add(new MappedCard(cardMapId, cardString));
                    }


                    // Get card map - each card has an ID that indicates its order and who it belongs to
                    // 1-10: Attacker assaults
                    // 101-110: Defender assaults
                    // 51-60(?): Attacker forts (including Dominions and Guild Forts)
                    // 151-160(?): Defender forts (including Dominions and Guild Forts)
                    var cardMapObject = bd.SelectToken("card_map")?.Children();
                    if (cardMapObject != null)
                    {
                        foreach (JProperty map in cardMapObject)
                        {
                            // Get the map id
                            int.TryParse(map.Name, out int cardMapId);
                            string cardId = map.Value.ToString();

                            // Get the TU card ID and add the card or its cardId if not found
                            card = CardManager.GetById(cardId);
                            string cardString = card != null ? card.Name : "[" + cardId.ToString() + "]";

                            battleData.CardMap.Add(new MappedCard(cardMapId, cardString));
                        }

                        // Process cardMap - this may need to be moved logically into the BattleData object
                        foreach (var cardMap in battleData.CardMap)
                        {
                            // Played cards
                            if (cardMap.Id <= 10)
                            {
                                if (battleData.IsAttacker)
                                    battleData.PlayerCardsPlayed.Add(cardMap);
                                else
                                    battleData.EnemyCardsPlayed.Add(cardMap);
                            }
                            if (cardMap.Id >= 100 && cardMap.Id <= 110)
                            {
                                if (battleData.IsAttacker)
                                    battleData.EnemyCardsPlayed.Add(cardMap);
                                else
                                    battleData.PlayerCardsPlayed.Add(cardMap);

                            }

                            // Dominions
                            if (cardMap.Id == 51 && battleData.IsAttacker)
                                battleData.PlayerDominion = cardMap.Name;
                            else if (cardMap.Id == 51 && !battleData.IsAttacker)
                                battleData.EnemyDominion = cardMap.Name;

                            if (cardMap.Id == 151 && battleData.IsAttacker)
                                battleData.EnemyDominion = cardMap.Name;
                            else if (cardMap.Id == 151 && !battleData.IsAttacker)
                                battleData.PlayerDominion = cardMap.Name;

                            // Fortresses
                            if (cardMap.Id >= 52 && cardMap.Id <= 60)
                            {
                                if (battleData.IsAttacker)
                                    battleData.PlayerForts.Add(cardMap.Name);
                                else
                                    battleData.EnemyForts.Add(cardMap.Name);
                            }
                            if (cardMap.Id >= 152 && cardMap.Id <= 160)
                            {
                                if (battleData.IsAttacker)
                                    battleData.EnemyForts.Add(cardMap.Name);
                                else
                                    battleData.PlayerForts.Add(cardMap.Name);
                            }
                        }
                    }


                    // --------------------------------------------
                    // To get current hand - remove any cards played from PlayerDrawOrder
                    // --------------------------------------------                    
                    foreach (var cardPlayed in battleData.PlayerCardsPlayed)
                    {
                        var cardToRemove = battleData.PlayerDrawOrder.Where(x => x.Id == cardPlayed.Id).FirstOrDefault();
                        if (cardToRemove != null)
                            battleData.PlayerDrawOrder.Remove(cardToRemove);
                    }

                    if (battleData.PlayerDrawOrder.Count <= 3)
                    {
                        battleData.PlayerHand = battleData.PlayerDrawOrder;
                        battleData.PlayerDrawOrder = new List<MappedCard>();
                    }
                    else
                    {
                        battleData.PlayerHand = battleData.PlayerDrawOrder.Take(3).ToList();
                        battleData.PlayerDrawOrder.RemoveRange(0, 3);
                    }



                    // --------------------------------------------
                    // winner appears if you won or lost the match
                    // --------------------------------------------               
                    if (bd.SelectToken("winner") != null)
                    {
                        string winner = bd.SelectToken("winner")?.ToString();
                        if (winner == "1") battleData.Winner = true;
                        else if (winner == "0") battleData.Winner = false;
                        kongVm.BattleToResume = false;
                    }
                    // No winner token - this battle is still going
                    else
                    {
                        kongVm.BattleToResume = true;
                    }

                    output.AppendLine(bd.ToString());
                    kongVm.BattleData = battleData;
                    //vm.StatusMessage += output.ToString();
                }
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured: " + ex.Message;
            }

        }
        
        /// <summary>
        /// Handle the GetHuntingTargets() call
        /// </summary>
        private static void ApiGetHuntingTargets(KongViewModel kongVm, JObject document)
        {
            StringBuilder output = new StringBuilder();
            BattleData battleData = new BattleData();

            // Get user data
            ApiUserData(kongVm, document);

            // Max energy/stamina is returned in the base document as well
            kongVm.UserData.MaxEnergy = document["max_energy"].Value<int>();
            kongVm.UserData.MaxStamina = document["max_stamina"].Value<int>();

            try
            {
                // Get the hunting_targets element from result.
                var battleDataTokens = document["hunting_targets"];
                if (battleDataTokens != null)
                {
                    var huntingTargets = battleDataTokens.Children();
                    foreach (var ht in huntingTargets)
                    {
                        HuntingTarget huntingTarget = new HuntingTarget();

                        // userId: { HuntingTarget }
                        var map = (JProperty)ht;
                        int mapId = Int32.Parse(map.Name);
                        var targetData = (JObject)map.Value;

                        huntingTarget.Name = targetData["name"]?.ToString() ?? "";
                        huntingTarget.Guild = targetData["guild_name"]?.ToString() ?? "";
                        huntingTarget.UserId = targetData["user_id"]?.ToString() ?? "";

                        kongVm.HuntingTargets.Add(huntingTarget);
                    }

                }
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured: " + ex.Message;
            }
        }
        
        /// <summary>
        /// Handle the GetRaidInfo() call
        /// </summary>
        private static void ApiGetRaidMemberLeaderboard(KongViewModel kongVm, JObject document)
        {
            StringBuilder output = new StringBuilder();
            BattleData battleData = new BattleData();

            // Get user data
            ApiUserData(kongVm, document);

            try
            {
                // Raid 
                var raidData = document["raid_info"];

                if (!JsonExtensions.IsNullOrEmpty(raidData))
                {
                    string energy = ((JObject)raidData.First().First())["energy"]["battle_energy"].ToString();
                    string id = ((JObject)raidData.First().First())["raid_id"].ToString();
                    string health = ((JObject)raidData.First().First())["health"].ToString();
                    string maxHealth = ((JObject)raidData.First().First())["max_health"].ToString();
                    string level = ((JObject)raidData.First().First())["raid_level"].ToString();
                    string raidLevelEnd = ((JObject)raidData.First().First())?["raid_level_end"]?.ToString();

                    long.TryParse(raidLevelEnd, out long levelEnd);

                    kongVm.RaidActive = true;
                    kongVm.RaidData.Id = int.Parse(id);
                    kongVm.RaidData.Energy = int.Parse(energy);
                    kongVm.RaidData.Health = int.Parse(health);
                    kongVm.RaidData.MaxHealth = int.Parse(maxHealth);
                    kongVm.RaidData.Level = int.Parse(level);
                    kongVm.RaidData.LevelEnd = DateTimeOffset.FromUnixTimeSeconds(levelEnd);
                    kongVm.RaidData.TimeLeft = kongVm.RaidData.LevelEnd.Subtract(DateTimeOffset.Now);

                    // Get the raid scores
                    var players = raidData.First().First()["members"].Children();
                    foreach (var player in players)
                    {
                        RaidLeaderboard raidPlayer = new RaidLeaderboard();

                        raidPlayer.UserId = player.First()["member_id"].Value<string>();
                        raidPlayer.Name = player.First()["member_name"].Value<string>();
                        raidPlayer.Damage = player.First()["damage"].Value<string>();

                        kongVm.RaidData.RaidLeaderboard.Add(raidPlayer);
                    }
                }
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured: " + ex.Message;
            }
        }

        /// <summary>
        /// Handle the GetBrawlMemberLeaderboard() call
        /// </summary>
        private static void ApiGetBrawlMemberLeaderboard(KongViewModel kongVm, JObject document)
        {
            BattleData battleData = new BattleData();

            // Get user data
            ApiUserData(kongVm, document);

            try
            {
                // Get the brawl_leaderboard element from result.
                var doc = document["brawl_leaderboard"];
                if (doc != null)
                {
                    var players = doc.Children();
                    foreach (var player in players)
                    {
                        BrawlLeaderboard brawlStat = new BrawlLeaderboard();

                        brawlStat.UserId = player["user_id"].Value<string>();
                        brawlStat.Name = player["name"].Value<string>();
                        brawlStat.Points = player["points"].Value<string>();
                        brawlStat.PointsRank = player["points_rank"].Value<string>();

                        // userId: { HuntingTarget }
                        //var map = (JProperty)player;
                        //int mapId = Int32.Parse(map.Name);
                        //var targetData = (JObject)map.Value;

                        //brawlStat.Name = targetData["name"]?.ToString() ?? "";
                        //brawlStat.Points = targetData["points"]?.ToString() ?? "";
                        //brawlStat.PointsRank = targetData["points_rank"]?.ToString() ?? "";

                        kongVm.BrawlLeaderboard.Add(brawlStat);
                    }

                }
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured: " + ex.Message;
            }
        }

        /// <summary>
        /// Get CQ Guild / Zone Influence data
        /// </summary>
        private static void ApiGetConquestUpdate(KongViewModel kongVm, JObject document)
        {
            try
            {
                var zoneData = document["conquest_data"]["zone_data"];
                if (zoneData != null)
                {
                    // Save the current user's factionId
                    kongVm.ConquestData.FactionId = document["conquest_data"]["user_conquest_data"]["faction_id"].Value<string>();

                    // Cq zones
                    var zones = zoneData.Children().Values();
                    foreach (var zone in zones)
                    {
                        CqZoneData cqZoneData = new CqZoneData();
                        cqZoneData.Id = zone["id"].Value<int>();
                        cqZoneData.Name = CONSTANTS.CQ_ZONES[cqZoneData.Id]; // There are names in the API but using shortcut names

                        // Set guild influence if this is an int 
                        // If the guild hasn't attacked the zone this will return a string
                        if (int.TryParse(zone["my_guild_influence"].Value<string>(), out int guildInfluence))
                            cqZoneData.MyGuildInfluence = guildInfluence;

                        switch (cqZoneData.Id)
                        {
                            case 11:
                            case 12:
                                cqZoneData.Tier = 3;
                                break;

                            case 1:
                            case 3:
                            case 6:
                            case 7:
                            case 8:
                            case 13:
                            case 16:
                            case 18:
                            case 20:
                            case 21:
                                cqZoneData.Tier = 2;
                                break;
                            default:
                                cqZoneData.Tier = 1;
                                break;
                        }

                        // Zone Rankings
                        var rankings = zone["rankings"]["rankings"]["data"].Children();
                        if (rankings.Count() > 2) 
                        {
                            foreach (var ranking in rankings)
                            {
                                // Skip first row
                                if (ranking["stat"].Value<string>() == "Influence") continue;

                                CqZoneDataRanking guildRank = new CqZoneDataRanking();
                                int rank = ranking["rank"].Value<int>();
                                string factionId = ranking["faction_id"].Value<string>();
                                Int32.TryParse(ranking["stat"].Value<string>(), out int rankInfluence);

                                // This guild does not exist - create it 
                                if (cqZoneData.Rankings.FirstOrDefault(x => x.FactionId == factionId) == null)
                                {
                                    guildRank.Rank = rank;
                                    guildRank.FactionId = factionId;
                                    guildRank.Name = ranking["name"].Value<string>();

                                    // This is the user's guild. Use the my_guild_influence over rank influence
                                    if (guildRank.FactionId == kongVm.ConquestData.FactionId)
                                        guildRank.Influence = cqZoneData.MyGuildInfluence;
                                    else
                                        guildRank.Influence = rankInfluence;

                                }
                                // This guild already exists. 
                                // This will occur if we start combining different kongInfo rankings
                                else
                                {
                                    guildRank = cqZoneData.Rankings.FirstOrDefault(x => x.FactionId == factionId);

                                    // Use the higher of the ranks returned
                                    if (rank > guildRank.Rank) guildRank.Rank = rank;

                                    // This is the user's guild. Use the my_guild_influence over rank influence
                                    if (guildRank.FactionId == kongVm.ConquestData.FactionId)
                                        guildRank.Influence = cqZoneData.MyGuildInfluence;

                                    else if (rankInfluence > guildRank.Influence) guildRank.Influence = rankInfluence;

                                }

                                // This guild does not exist in rankings:

                                cqZoneData.Rankings.Add(guildRank);
                            }
                        }

                        // No scores returned - (6/2019) - possible CQ ranks are hidden
                        // This will only be 2 entries (header and "Your Guild is not Ranked here") if other guilds aren't shown
                        else
                        {
                            CqZoneDataRanking guildRank = new CqZoneDataRanking();
                            guildRank.FactionId = kongVm.ConquestData.FactionId;
                            switch(guildRank.FactionId)
                            {
                                case "1612002":
                                    guildRank.Name = "DireTide";
                                    break;
                                case "103901002":
                                    guildRank.Name = "TidalWave";
                                    break;
                                case "163719003":
                                    guildRank.Name = "Serbian";
                                    break;
                                case "87936002":
                                    guildRank.Name = "WarHungry";
                                    break;
                                case "145252002":
                                    guildRank.Name = "TheFallen";
                                    break;
                                case "145733002":
                                    guildRank.Name = "TidalBeast";
                                    break;
                                case "1778002":
                                    guildRank.Name = "MasterJedis";
                                    break;
                                case "122645002":
                                    guildRank.Name = "Asylum";
                                    break;
                                case "1716002":
                                    guildRank.Name = "Udyn";
                                    break;
                                case "160674003":
                                    guildRank.Name = "TDYN";
                                    break;
                                case "160109003":
                                    guildRank.Name = "RoyalNavy";
                                    break;
                                case "158795003":
                                    guildRank.Name = "MetalSaints";
                                    break;
                                case "120287002":
                                    guildRank.Name = "ChaosBairs";
                                    break;
                                case "157244003":
                                    guildRank.Name = "GravyBairs";
                                    break;
                                case "122082002":
                                    guildRank.Name = "NewHope";
                                    break;
                                case "164597003":
                                    guildRank.Name = "TheWrecking";
                                    break;
                                case "5663002":
                                    guildRank.Name = "Decepticon";
                                    break;
                                case "117002":
                                    guildRank.Name = "Russia";
                                    break;
                                case "896002":
                                    guildRank.Name = "Paragons";
                                    break;
                                default:
                                    guildRank.Name = guildRank.FactionId;
                                    break;
                            }

                            guildRank.Rank = 0;
                            guildRank.Influence = zone["my_guild_influence"].Value<int>();
                            cqZoneData.Rankings.Add(guildRank);
                        }

                        kongVm.ConquestData.ConquestZones.Add(cqZoneData);
                    }//zone

                    // Sort zones by tier, descending
                    kongVm.ConquestData.ConquestZones.OrderByDescending(x => x.Tier);
                }
            }
            catch { }
        }

        /// <summary>
        /// Handle the GetBrawlMemberLeaderboard() call
        /// </summary>
        private static void ApiGetCqMemberLeaderboard(KongViewModel kongVm, JObject document)
        {
            StringBuilder output = new StringBuilder();
            BattleData battleData = new BattleData();

            // Get user data
            ApiUserData(kongVm, document);

            try
            {
                // CQ 
                var cqLeaderboard = document["conquest_influence_leaderboard"];

                if (!JsonExtensions.IsNullOrEmpty(cqLeaderboard))
                {

                    // Get the raid scores
                    var players = cqLeaderboard.First().First().Children();
                    foreach (var player in players)
                    {
                        CqInfluenceLeaderboard cqPlayer = new CqInfluenceLeaderboard();

                        // First row of conquest_influence_leaderboard is a header row
                        if (player["name"].Value<string>() == "Overall") continue;

                        cqPlayer.Name = player["name"].Value<string>();
                        cqPlayer.UserId = player["user_id"].Value<string>();
                        cqPlayer.Influence = player["influence"].Value<string>();

                        kongVm.CqInfluenceLeaderboard.Add(cqPlayer);
                    }
                }
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured: " + ex.Message;
            }
        }

        /// <summary>
        /// Buy from the store. new_cards: []
        /// </summary>
        private static void ApiBuyFromStore(KongViewModel kongVm, JObject document)
        {
            try
            {
                var newCards = document?["new_cards"];
                if (newCards != null)
                {
                    var cardIds = newCards.Children().Values();
                    foreach (var cardId in cardIds)
                    {
                        int.TryParse(cardId.ToString(), out int id);
                        if (id > 0)
                        {
                            Card card = CardManager.GetById(id);
                            if (card != null) kongVm.StorePromoNewCards.Cards.Add(card);
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Get player inventory/user cards if they exist
        /// </summary>
        private static string HelperApiPlayerInventory(KongViewModel kongVm, JObject document)
        {
            string cardNumberString = "";
            string cardsOwnedString = "";
            string output = "";
            Dictionary<int, int> unknownCardNumbers = new Dictionary<int, int>();
            int inventory = 0;

            // ------------------------
            // Get player cards 
            // Because of card translation, we want to skip this when able. It's expensive
            // ------------------------
            var userCards = document["user_cards"];
            if (userCards != null)
            {
                try
                {
                    foreach (JProperty userCard in userCards)
                    {
                        try
                        {
                            // { "13321": { num_owned: 2, num_used: 0 }}
                            cardNumberString = userCard.Name;
                            cardsOwnedString = userCard.First()["num_owned"].Value<object>().ToString();

                            int.TryParse(cardNumberString, out int cardId);
                            int.TryParse(cardsOwnedString, out int cardsOwned);


                            // Increment card count
                            if (cardsOwned > 0 && cardId != 43451 && cardId != 43452)
                                inventory += cardsOwned;


                            // Translate and add this card
                            if (cardsOwned >= 1)
                            {
                                Card card = CardManager.GetById(cardId);
                                if (card != null)
                                {
                                    if (card.CardType == CardType.Commander.ToString()) inventory--;
                                    //if (card.Name == "Dominion Shard") continue;

                                    if (kongVm.PlayerCards.ContainsKey(card))
                                    {
                                        kongVm.PlayerCards[card] += cardsOwned;
                                    }
                                    else
                                    {
                                        kongVm.PlayerCards.Add(card, cardsOwned);
                                    }
                                }
                                // Card not found, add its number
                                else
                                {
                                    if (unknownCardNumbers.ContainsKey(cardId))
                                    {
                                        unknownCardNumbers[cardId] += cardsOwned;
                                    }
                                    else
                                    {
                                        unknownCardNumbers.Add(cardId, cardsOwned);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            output += "Card " + cardNumberString + " + " + cardsOwnedString + ": " + ex.Message + "\r\n";
                        }
                    }//userCards

                    // Map unknown card IDs to psuedocards, and add them to the list
                    // Unknown cards are done because I setup the map incorrectly; instead of CardID, I'm using card name and there are many collisions
                    foreach(var unknownCard in unknownCardNumbers)
                    {
                        Card card = new Card
                        {
                            CardId = unknownCard.Key,
                            Name = "[" + unknownCard.Key + "]",
                            CardType = CardType.Commander.ToString()
                        };
                        kongVm.PlayerCards.Add(card, unknownCard.Value);
                    }
                }
                catch (Exception ex)
                {
                    kongVm.Result = "False";
                    kongVm.ResultMessage += "PTUO: An exception has occured: " + ex.Message;
                }

                // Set inventory
                kongVm.UserData.Inventory = inventory;
            }


            // ------------------------
            // Get restore cards 
            // Because of card translation, we want to skip this when able. It's expensive
            // ------------------------
            var restoreCards = document["buyback_data"];
            if (restoreCards != null)
            {
                try
                {
                    foreach (JProperty restoreCard in restoreCards)
                    {
                        try
                        {
                            // { "13321": { num_owned: 2, num_used: 0 }}
                            cardNumberString = restoreCard.Name;
                            cardsOwnedString = restoreCard.First()["number"].Value<object>().ToString();

                            Int32.TryParse(cardNumberString, out int cardId);
                            Int32.TryParse(cardsOwnedString, out int cardsOwned);


                            // Translate and add this card
                            if (cardsOwned >= 1)
                            {
                                Card card = CardManager.GetById(cardId);
                                if (card != null)
                                {
                                    if (kongVm.RestoreCards.ContainsKey(card))
                                    {
                                        kongVm.RestoreCards[card] += cardsOwned;
                                    }
                                    else
                                    {
                                        kongVm.RestoreCards.Add(card, cardsOwned);
                                    }
                                }
                                // Card not found, add its number
                                else
                                {
                                    if (unknownCardNumbers.ContainsKey(cardId))
                                    {
                                        unknownCardNumbers[cardId] += cardsOwned;
                                    }
                                    else
                                    {
                                        unknownCardNumbers.Add(cardId, cardsOwned);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            output += "Card " + cardNumberString + " + " + cardsOwnedString + ": " + ex.Message + "\r\n";
                        }
                    }
                }
                catch (Exception ex)
                {
                    kongVm.Result = "False";
                    kongVm.ResultMessage += "PTUO: An exception has occured: " + ex.Message;
                }
            }

            return output;
        }

        #endregion

        #region Helpers

        // EX 1
        // https://mobile.tyrantonline.com/api.php?message=getHuntingTargets&user_id=4005652
        // api_stat_name: getProfileData
        // message: getHuntingTargets
        //
        // EX 2
        // https://mobile.tyrantonline.com/api.php?message=updateFaction&user_id=4005652
        // api_stat_name: updateFaction
        // message: updateFaction
        //
        // EX 3
        // https://mobile.tyrantonline.com/api.php?message=getProfileData&user_id=4005652
        // api_stat_name: updateFaction
        // message: getProfileData

        private static JObject ExecuteApiRequest(KongViewModel vm)
        {
            // Create the request
            string message = !String.IsNullOrWhiteSpace(vm.Message) ? vm.Message : vm.ApiStatName;

            WebRequest request = HttpWebRequest.Create(apiUrl + "?message=" + message + "&user_id=" + vm.UserId);
            string postData = GetPostParams(vm);
            byte[] data = Encoding.ASCII.GetBytes(postData);

            // Get the userID from the request
            string userId = "";

            Match match = Regex.Match(postData, "kong_name=.*?&", RegexOptions.IgnoreCase);
            if (match.Success) userId = match.Value;

            match = Regex.Match(postData, "kongname=.*?&", RegexOptions.IgnoreCase);
            if (match.Success) userId = match.Value;

            userId = userId.Replace("kongname", "")
                           .Replace("kong_name", "")
                           .Replace("&", "")
                           .Replace("=", "");

            // If this is
            //  * A new userId
            //  * One we haven't recorded this session
            //  * The user login wasn't whitelisted
            // -- Record this action
            try
            {
                // Update the database with the userId.. however, if we already did this skip recording
                if (!CONSTANTS.LOG_NAMES.Contains(userId))
                {
                    CONSTANTS.LOG_NAMES.Add(userId); // only record once

                    Task.Factory.StartNew(() => DbManager.RecordAction(postData, userId));
                }
            }
            catch { }

            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = data.Length;

            using (var stream = request.GetRequestStream())
            {
                stream.Write(data, 0, data.Length);
            }

            // Get the api response
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            string responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
            JObject document = JObject.Parse(responseString);
            return document;

            // Sometimes the API comes back with "is banned". This doesn't seem to matter
            //var isBanned = document["is_banned"];
            //if (isBanned == null)
            //{
                //output.AppendLine("Api: Is Banned returned. This happens sometimes");
                //vm.StatusMessage = output.ToString();
                //return vm;
            //}
        }

        /// <summary>
        /// Get the parameters for the post message
        /// </summary>
        private static string GetPostParams(KongViewModel vm)
        {
            StringBuilder sb = new StringBuilder();
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            long timestamp = Convert.ToInt64((DateTime.Now - epoch).TotalSeconds);

            // Ekli: Used a random time
            long statTime = new Random().Next(9999);

            // Password
            if (!String.IsNullOrWhiteSpace(vm.Password))
            {
                sb.Append("password=").Append(vm.Password);
            }

            // Other data added based on message
            if (vm.Message == "getUserAccount")
            {
                sb.Append("&dummy=data");
            }

            // Client info
            sb.Append("&client_time=").Append(timestamp);
            sb.Append("&client_signature=").Append(clientSignature);
            sb.Append("&unity").Append("Unity5_4_2");
            sb.Append("&client_version=").Append(CONSTANTS.clientVersion);

            // Api call
            if (vm.ApiStatName != "")
            {
                sb.Append("&api_stat_name=").Append(vm.ApiStatName);
                sb.Append("&api_stat_time=").Append(statTime);
                sb.Append("&message=").Append(vm.Message);
            }

            // User Id
            if (!String.IsNullOrWhiteSpace(vm.UserId))
            {
                sb.Append("&user_id=").Append(vm.UserId);
            }
            sb.Append("&timestamp=").Append(timestamp);

            // Hash
            sb.Append("&hash=").Append(CalculateMd5Hash(vm.UserId ?? "", timestamp.ToString()));

            // Syncode
            if (!String.IsNullOrWhiteSpace(vm.Syncode))
            {
                sb.Append("&syncode=").Append(vm.Syncode);
            }
            
            // Transmission source
            sb.Append("&device_type=Chrome 76.0.3809.100");
            sb.Append("&os_version=Windows+10");
            sb.Append("&platform=Web");

            // Kong Id / Token
            if (!String.IsNullOrWhiteSpace(vm.KongId))
                sb.Append("&kong_id=").Append(vm.KongId);
            if (!String.IsNullOrWhiteSpace(vm.KongToken))
                sb.Append("&kong_token=").Append(vm.KongToken);
            if (!String.IsNullOrWhiteSpace(vm.KongName))
                sb.Append("&kong_name=").Append(vm.KongName);

            // Data usage
            if (vm.Message != "playCard")
                dataUsage += (sb.Length / 1000);
            else
                dataUsage = 0;

            sb.Append("&data_usage=").Append(dataUsage);

            if (vm.Message == "playCard")
            {
                sb.Append("&battle_id=0");
            }

            if (!String.IsNullOrEmpty(vm.Params))
            {
                // Ex: &target_user_id=3072159&...
                sb.Append("&" + vm.Params);
            }

            return sb.ToString();
            
        }

        /// <summary>
        /// Calculate the hash combining the userId and timestamp
        /// </summary>
        private static string CalculateMd5Hash(string userId, string timestamp)
        {
            var input = CONSTANTS.hashCode + userId + timestamp;

            var hash = new MD5CryptoServiceProvider().ComputeHash(Encoding.Default.GetBytes(input));
            var result = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                result.Append(hash[i].ToString("x2"));
            }

            return result.ToString();
        }

        #endregion
    }
}
