using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using PirateTUO2.Classes;
using PirateTUO2.Models;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace PirateTUO2.Modules
{
    /// <summary>
    /// Super function that handles API calls
    /// </summary>
    public class BotManager
    {
        #region General

        // Stores decks from recentlogs.txt and hardlogs.txt into memory
        // * Whenever we need this data, check its last update time
        // * Refresh if its over 1 minute old
        // * Performance - if multiple threads keep reading it, the file gets read locked

        public static List<string> logs = new List<string>();
        public static DateTime logsLastUpdate = DateTime.Now.AddMinutes(-60);

        public static List<string> pullerLogs = new List<string>();
        public static DateTime pullerLogsLastUpdate = DateTime.Now.AddMinutes(-60);

        public static List<string> recentPullerLog = new List<string>();
        public static DateTime recentPullerLogLastUpdate = DateTime.Now.AddMinutes(-60);

        /// <summary>
        /// Call Init and toggle if we want to process cards (getting cards takes time)
        /// </summary>
        public static KongViewModel Init(string userData, bool getCardsFromInit = false)
        {
            KongViewModel kongVm = new KongViewModel("init");
            kongVm.GetCardsFromInit = getCardsFromInit;

            try
            {
                // Call API
                ApiManager.GetKongInfoFromString(kongVm, userData);
                ApiManager.CallApi(kongVm);
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }

        /// <summary>
        /// Call GetUserAccount for user_data (has some player stats)
        /// Much faster to call then init
        /// </summary>
        public static KongViewModel GetUserAccount(string userData)
        {
            KongViewModel kongVm = new KongViewModel("getUserAccount");

            try
            {
                // Call API
                ApiManager.GetKongInfoFromString(kongVm, userData);
                ApiManager.CallApi(kongVm);
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }

        /// <summary>
        /// Get the daily bonus card
        /// </summary>
        public static KongViewModel UseDailyBonus(string userData)
        {
            KongViewModel kongVm = new KongViewModel("useDailyBonus");

            try
            {
                // Call API
                ApiManager.GetKongInfoFromString(kongVm, userData);
                ApiManager.CallApi(kongVm);
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }


        #endregion

        #region Battle

        /// <summary>
        /// Get active or previous battle data
        /// ** If the user is currently in a battle, kongVm.BattleData.Winner will have a value
        /// ** If Pve deck is passed, use that as the enemy deck instead of looking through player logs
        /// </summary>
        public static KongViewModel GetCurrentOrLastBattle(MainForm form, string userData, List<string> manualEnemyDeck = null, string manualEnemyCommander = "", string manualEnemyFortress = "", string missingCardStrategy="")
        {
            // Initialize KongViewModel. But we need the kong data
            KongViewModel kongVm = new KongViewModel("getBattleResults");

            try
            {
                ApiManager.GetKongInfoFromString(kongVm, userData);
                ApiManager.CallApi(kongVm);

                // Player opponent: Guess at enemy guild and remaining deck
                if (manualEnemyDeck == null)
                {
                    GuessEnemyRemainingDeck(form, kongVm, missingCardStrategy);
                }

                // Mission opponent: Feeding this a deck
                else
                {
                    List<string> mEnemyDeck = new List<string>(manualEnemyDeck);

                    if (manualEnemyCommander != "") kongVm.BattleData.EnemyCommander = manualEnemyCommander;
                    if (manualEnemyFortress != "") kongVm.BattleData.EnemyForts.Add(manualEnemyFortress);

                    List<string> enemyPlayedCards = kongVm.BattleData.EnemyCardsPlayed.Select(x => x.Name).ToList();

                    // Remove played cards from the pve deck
                    foreach (var card in enemyPlayedCards)
                    {
                        // Strip level off the card
                        card.Replace("-1", "").Replace("-2", "").Replace("-3", "").Replace("-4", "").Replace("-5", "").Replace("-6", "").Replace("-7", "").Replace("-8", "").Replace("-9", "");

                        if (mEnemyDeck.Contains(card)) mEnemyDeck.Remove(card);
                    }

                    // Randomize the enemy cards remaining (in case the deck size is more then 10)
                    Helper.Shuffle(mEnemyDeck);
                    
                    // Add remaining cards
                    foreach (var card in mEnemyDeck)
                    {
                        kongVm.BattleData.EnemyCardsRemaining.Add(card);
                        if (enemyPlayedCards.Count + kongVm.BattleData.EnemyCardsRemaining.Count >= 10) break;
                    }
                }
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }



        /// <summary>
        /// Play the best card from a given sim
        /// Look through our simmed deck and find the first card we can legally play
        /// </summary>
        public static string PlayCard(MainForm form, KongViewModel kongVm, Sim sim, int skipBattle = 0)
        {
            MappedCard cardToPlay = null;

            try
            {
                if (kongVm.BattleData.Winner == null)
                {
                    // Look through our deck and find the first card we can legally play
                    if (sim != null)
                    {
                        int freezeCards = sim.Freeze;
                        List<string> playerCards = sim.ResultDeck.Split(',').ToList();
                        List<string> playerCardsUncompressed = TextCleaner.UncompressDeck(playerCards);
                        string[] cards = sim.ResultDeck.Split(',');

                        // Find the first card in the ordered result that is in the player's hand
                        for (int i = (freezeCards + 1); i < playerCardsUncompressed.Count; i++)
                        {
                            string card = playerCardsUncompressed?[i];
                            if (card == null)
                            {

                            }
                            cardToPlay = sim.ApiHand.Where(x => x.Name == card).FirstOrDefault();
                            if (cardToPlay != null)
                            {
                                break;
                            }
                        }

                        // Play that card
                        if (cardToPlay != null)
                        {
                            string debugOutput = kongVm.KongName + ": T" + kongVm.BattleData.Turn + " - " + sim.WinPercent + "% - " + cardToPlay.Name + " [" + cardToPlay.Id + "]";

                            Console.WriteLine(debugOutput);

                            kongVm.ApiStatName = "playCard";
                            kongVm.Message = "playCard";
                            kongVm.Params = "card_uid=" + cardToPlay.Id + "&skip=" + skipBattle;

                            // small delay if doing pure reorder
                            // if (sim.ExtraFreezeCards == 0) Thread.Sleep(50);
                            ApiManager.CallApi(kongVm);

                        }
                        else
                        {
                            string debugOutput = kongVm.KongName + ": T" + kongVm.BattleData.Turn + " - " + sim.WinPercent + "% - No card to play";
                            Console.WriteLine(debugOutput);
                        }
                    }

                    // ** TODO: We need to save this guessed deck somehow so we don't keep recalling the logic **

                    // Guess at enemy guild and remaining deck
                    GuessEnemyRemainingDeck(form, kongVm);
                }
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex;
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.StackTrace;

                string debugOutput = kongVm.KongName + ": Error when trying to play a card: \r\n" + ex.Message;
                Console.WriteLine(debugOutput);
            }

            return cardToPlay.Name;
        }

        /// <summary>
        /// Play the best card from a given sim
        /// Look through our simmed deck and find the first card we can legally play
        /// </summary>
        public static void PlayCardRumBarrel(MainForm form, KongViewModel kongVm, Sim sim, int skipBattle = 0)
        {
            MappedCard cardToPlay = null;

            try
            {
                if (kongVm.BattleData.Winner == null)
                {
                    // Look through our deck and find the first card we can legally play
                    if (sim != null)
                    {
                        int freezeCards = sim.Freeze;
                        List<string> playerCards = sim.ResultDeck.Split(',').ToList();
                        List<string> playerCardsUncompressed = TextCleaner.UncompressDeck(playerCards);

                        // Find the first card in the ordered result that is in the player's hand
                        for (int i = (freezeCards + 1); i < playerCardsUncompressed.Count; i++)
                        {
                            string card = playerCardsUncompressed?[i];
                            if (card == null)
                            {

                            }
                            cardToPlay = sim.ApiHand.Where(x => x.Name == card).FirstOrDefault();
                            if (cardToPlay != null)
                            {
                                break;
                            }
                        }

                        // Play that card
                        if (cardToPlay != null)
                        {
                            string debugOutput = kongVm.KongName + ": T" + kongVm.BattleData.Turn + " - " + sim.WinPercent + "% - " + cardToPlay.Name + " [" + cardToPlay.Id + "]";

                            form.rumBarrelOutputTextBox.AppendText(debugOutput + "\r\n");
                            Console.WriteLine(debugOutput);

                            kongVm.ApiStatName = "playCard";
                            kongVm.Message = "playCard";
                            kongVm.Params = "card_uid=" + cardToPlay.Id + "&skip=" + skipBattle;

                            // small delay if doing pure reorder
                            // if (sim.ExtraFreezeCards == 0) Thread.Sleep(50);
                            ApiManager.CallApi(kongVm);

                        }
                        else
                        {
                            string debugOutput = kongVm.KongName + ": T" + kongVm.BattleData.Turn + " - " + sim.WinPercent + "% - FOUND NO CARD TO PLAY";
                            form.rumBarrelOutputTextBox.AppendText(debugOutput + "\r\n");
                            Console.WriteLine(debugOutput);
                        }
                    }
                    else
                    {

                    }

                    // ** TODO: We need to save this guessed deck somehow so we don't keep recalling the logic **

                    // Guess at enemy guild and remaining deck
                    // GuessEnemyRemainingDeck(form, kongVm);
                }
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;


                string debugOutput = kongVm.KongName + ": Error when trying to play a card: \r\n" + ex.Message;

                // -- additional info --
                debugOutput += "\r\n" +
                    "Sim sent to extract card: " + sim.SimToString() + "\r\n" +
                    "Frozen card count: " + sim.Freeze + "\r\n" +
                    "Player card order: " + TextCleaner.UncompressDeck(sim.ResultDeck.Split(',').ToList()) + "\r\n" +
                    "cardToPlay MappedCard is: Id=" + cardToPlay.Id + ", Name=" + cardToPlay.Name + "\r\n";


                form.rumBarrelOutputTextBox.AppendText(debugOutput + "\r\n");
                Console.WriteLine(debugOutput);
            }
        }

        /// <summary>
        /// DEBUG
        /// Starts a guild surge battle (targetUserId = jerk)
        /// </summary>
        public static KongViewModel StartGuildSurge(MainForm form, string kongInfo, string targetUserId) // bool isInFight?
        {
            // Initialize KongViewModel. But we need the kong data
            KongViewModel kongVm = new KongViewModel("fightPracticeBattle");
            kongVm.Params = "target_user_id=" + targetUserId + "&fight_attack_deck=0&is_surge=1";

            try
            {
                ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                ApiManager.CallApi(kongVm);

                // Guess at enemy guild and remaining deck
                GuessEnemyRemainingDeck(form, kongVm);
            }
            catch(Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }

        /// <summary>
        /// Start a conquest battle
        /// </summary>
        public static KongViewModel StartCqMatch(MainForm form, string kongInfo, int zoneId) // bool isInFight?
        {
            // Initialize KongViewModel. But we need the kong data
            KongViewModel kongVm = new KongViewModel("fightConquestBattle", "fightConquestBattle", "&zone_id=" + zoneId);

            try
            {
                ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                ApiManager.CallApi(kongVm);

                // Guess at enemy guild and remaining deck
                GuessEnemyRemainingDeck(form, kongVm);

            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }
            return kongVm;
        }

        /// <summary>
        /// Start a brawl battle 
        /// </summary>
        public static KongViewModel StartBrawlMatch(MainForm form, string kongInfo) // bool isInFight?
        {
            // Initialize KongViewModel. But we need the kong data
            KongViewModel kongVm = new KongViewModel("fightBrawlBattle");

            try
            {
                ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                ApiManager.CallApi(kongVm);

                // Guess at enemy guild and remaining deck
                GuessEnemyRemainingDeck(form, kongVm);

            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }
            return kongVm;
        }

        /// <summary>
        /// Start a raid battle 
        /// </summary>
        public static KongViewModel StartRaidBattle(MainForm form, string kongInfo, int raidId, int raidLevel) // bool isInFight?
        {
            // Initialize KongViewModel. But we need the kong data
            KongViewModel kongVm = new KongViewModel("fightRaidBattle", apiParams: "raid_id=" + raidId + "&raid_level=" + raidLevel);

            try
            {
                ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                ApiManager.CallApi(kongVm);

                // Guess at enemy guild and remaining deck
                GuessEnemyRemainingDeck(form, kongVm);

            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }
            return kongVm;
        }

        /// <summary>
        /// Play a brawl battle
        /// TODO: Make this
        /// </summary>
        public static KongViewModel PlayBrawlMatch(string kongInfo)
        {
            return new KongViewModel();
        }

        /// <summary>
        /// Start a war battle 
        /// </summary>
        public static KongViewModel StartWarMatch(MainForm form, string kongInfo) // bool isInFight?
        {
            // Initialize KongViewModel. But we need the kong data
            KongViewModel kongVm = new KongViewModel("startFactionWarBattle", "startFactionWarBattle", "slot_id=1"); //1 = core

            try
            {
                ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                ApiManager.CallApi(kongVm);

                // Guess at enemy guild and remaining deck
                GuessEnemyRemainingDeck(form, kongVm);

            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }
            return kongVm;
        }

        /// <summary>
        /// Start a pve mission or quest
        /// </summary>
        public static KongViewModel StartMissionOrQuest(string kongInfo, string missionId, bool guildQuest=false) // bool isInFight?
        {
            KongViewModel kongVm = new KongViewModel();

            if (guildQuest)
            {
                kongVm = new KongViewModel("fightFactionQuest", "fightFactionQuest", "quest_id=" + missionId);
            }
            else
            {
                kongVm = new KongViewModel("startMission", "startMission", "mission_id=" + missionId);
            }

            try
            {
                ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                ApiManager.CallApi(kongVm);

            }
            catch (Exception ex)
            {                
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }
            return kongVm;
        }

        // public static KongViewModel StartPvpBattle(...)

        /// <summary>
        /// Start a mission or guild quest, then auto it
        /// </summary>
        public static KongViewModel AutoMissionOrQuest(string kongInfo, string missionId, bool guildQuest=false) 
        {
            KongViewModel kongVm = StartMissionOrQuest(kongInfo, missionId, guildQuest);
            if (kongVm.Result == "False") return kongVm;

            try
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                // Start battle
                ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                ApiManager.CallApi(kongVm);
                Thread.Sleep(100);
                if (kongVm.Result == "False") return kongVm;

                // Set auto to true
                kongVm = new KongViewModel("playCard", "setUserFlag", "flag=autopilot&value=1");
                ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                ApiManager.CallApi(kongVm);
                Thread.Sleep(100);
                if (kongVm.Result == "False") return kongVm;


                // Play card then hit skip
                kongVm = new KongViewModel("playCard", "playCard", "card_uid=1&skip=1");
                ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                ApiManager.CallApi(kongVm);
                Thread.Sleep(100);
                if (kongVm.Result == "False") return kongVm;

                // Get last fight results
                kongVm = new KongViewModel("getBattleResults");
                ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                ApiManager.CallApi(kongVm);
                if (kongVm.Result == "False") return kongVm;

                // Store if they won the battle
                bool wonBattle = kongVm.BattleData.Winner.HasValue ? kongVm.BattleData.Winner.Value : false;
                
                // Get userdata to retrieve energy
                kongVm = new KongViewModel("getUserAccount");
                ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                ApiManager.CallApi(kongVm);

                // Re-record if they won the fight or not
                kongVm.BattleData.Winner = wonBattle;
                kongVm.ResultMessage = kongVm.KongName + ": " + missionId + ": " + (kongVm.BattleData.Winner.Value ? "WON!" : "**LOSS**") + "\r\n";

                // Try to wait 3 seconds between completing a mission and going again
                double timeElapsed = ((double)stopwatch.ElapsedMilliseconds / 1000);
                Thread.Sleep(2500);

                stopwatch.Stop();

                

            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }

        /// <summary>
        /// Auto a pvp match
        /// KongVm should have getBattleResults, with target_user_id
        /// </summary>
        public static KongViewModel AutoPvpBattle(string userData, string targetUserId) // bool isInFight?
        {
            // Initialize KongViewModel. But we need the kong data
            KongViewModel kongVm = new KongViewModel("startHuntingBattle", "startHuntingBattle", "target_user_id=" + targetUserId);

            try
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                // Start battle
                ApiManager.GetKongInfoFromString(kongVm, userData);
                ApiManager.CallApi(kongVm);
                Thread.Sleep(200);
                if (kongVm.Result == "False") return kongVm;

                // Set auto to true
                kongVm = new KongViewModel("playCard", "setUserFlag", "flag=autopilot&value=1");
                ApiManager.GetKongInfoFromString(kongVm, userData);
                ApiManager.CallApi(kongVm);
                Thread.Sleep(50);
                if (kongVm.Result == "False") return kongVm;


                // Play card then hit skip
                kongVm = new KongViewModel("playCard", "playCard", "card_uid=1&skip=1");
                ApiManager.GetKongInfoFromString(kongVm, userData);
                ApiManager.CallApi(kongVm);
                Thread.Sleep(200);
                if (kongVm.Result == "False") return kongVm;

                // Get hunting targets
                kongVm = new KongViewModel("getHuntingTargets", "playCard");
                ApiManager.GetKongInfoFromString(kongVm, userData);
                ApiManager.CallApi(kongVm);
                Thread.Sleep(200);
                if (kongVm.Result == "False") return kongVm;

                // Store stamina from getHuntingTargets
                int stamina = kongVm.UserData.Stamina;

                // Get last fight results
                kongVm = new KongViewModel("getBattleResults");
                ApiManager.GetKongInfoFromString(kongVm, userData);
                ApiManager.CallApi(kongVm);

                // Also return stamina
                if (stamina >= 0) kongVm.UserData.Stamina = stamina;

                // If less then 3 seconds pass, wait a bit
                double timeElapsed = (double)stopwatch.ElapsedMilliseconds / 1000;
                Thread.Sleep(2500);
                stopwatch.Stop();

            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }

        /// <summary>
        /// Livesim X turns of a pvp match, then auto
        /// ** NOT YET COMPLETE
        /// </summary>
        public static KongViewModel LivesimPvpBattle(string userData, string targetUserId, int turnsToLivesim)
        {
            // Initialize KongViewModel. But we need the kong data
            KongViewModel kongVm = new KongViewModel("startHuntingBattle", "startHuntingBattle", "target_user_id=" + targetUserId);
                        

            try
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                // Start battle
                ApiManager.GetKongInfoFromString(kongVm, userData);
                ApiManager.CallApi(kongVm);
                Thread.Sleep(50);
                if (kongVm.Result == "False") return kongVm;

                // List of cards livesim played. We need this to figure out which card to play when choosing to auto
                List<string> livesimCardsPlayed = new List<string>();

                // Debug
                Console.WriteLine(kongVm.KongName + " starting battle against " + targetUserId + ", LSEing " + turnsToLivesim + " turns");

                // Set auto to false
                kongVm = new KongViewModel("playCard", "setUserFlag", "flag=autopilot&value=0");
                ApiManager.GetKongInfoFromString(kongVm, userData);
                ApiManager.CallApi(kongVm);
                Thread.Sleep(50);
                if (kongVm.Result == "False") return kongVm;

                for (int i=0; i< turnsToLivesim; i++)
                {
                    // ------------------------------------
                    // Match started successfully
                    // ------------------------------------
                    if (kongVm != null)
                    {
                        // Refresh the battle
                        kongVm = BotManager.GetCurrentOrLastBattle(null, userData);

                        // Then build and run a sim
                        BatchSim batchSim = NewSimManager.BuildLiveSim(kongVm, "pvp", 200, 0);
                        NewSimManager.RunLiveSim(batchSim);
                        Sim sim = batchSim.Sims.OrderByDescending(x => x.WinPercent).ThenByDescending(x => x.WinScore).FirstOrDefault();

                        // For some reason this will be null - it needs to be rerun
                        if (sim.ResultDeck == null)
                        {
                            Console.WriteLine("NULL sim.ResultDeck - here's the sim string we tried: " + sim.SimToString());
                            continue;
                        }

                        // Play the next card
                        string cardPlayed = BotManager.PlayCard(null, kongVm, sim);
                        livesimCardsPlayed.Add(cardPlayed);

                        // Some error - break out 
                        if (kongVm.Result == "False")
                        {
                            Console.WriteLine(kongVm.KongName + " An error has occured: " + kongVm.GetResultMessage());
                            break;
                        }
                        // If we won or lost - break out
                        if (kongVm.BattleData.Winner.HasValue)
                        {
                            Console.WriteLine(kongVm.KongName + " vs. " + kongVm.BattleData.EnemyName + ": " + (kongVm.BattleData.Winner == true ? "Win" : "Loss"));
                            break;
                        }
                    }

                    // ------------------------------------
                    // Match was not started successfully
                    // ------------------------------------
                    else
                    {
                        Console.WriteLine(kongVm.KongName + ": An error has occured: " + kongVm.ResultMessage);
                        break;
                    }
                }

                // Set auto to true and attack
                KongViewModel kongVm2 = new KongViewModel("playCard", "setUserFlag", "flag=autopilot&value=1");
                ApiManager.GetKongInfoFromString(kongVm2, userData);
                ApiManager.CallApi(kongVm2);
                Thread.Sleep(50);
                if (kongVm.Result == "False") return kongVm;


                // Play card then hit skip

                // - TODO: FIGURE OUT WHICH CARD TO PLAY - 
                // * kongVm battle data should have battleData.attack_deck: 1: "cardId", 2: "cardId"
                // * Eliminate cards from string of cards, then play the first

                kongVm = new KongViewModel("playCard", "playCard", "card_uid=1&skip=1");
                ApiManager.GetKongInfoFromString(kongVm, userData);
                ApiManager.CallApi(kongVm);
                Thread.Sleep(50);
                if (kongVm.Result == "False") return kongVm;

                // Get hunting targets
                kongVm = new KongViewModel("getHuntingTargets", "playCard");
                ApiManager.GetKongInfoFromString(kongVm, userData);
                ApiManager.CallApi(kongVm);
                Thread.Sleep(50);
                if (kongVm.Result == "False") return kongVm;

                // Store stamina from getHuntingTargets
                int stamina = kongVm.UserData.Stamina;

                // Get last fight results
                kongVm = new KongViewModel("getBattleResults");
                ApiManager.GetKongInfoFromString(kongVm, userData);
                ApiManager.CallApi(kongVm);

                // Also return stamina
                if (stamina >= 0) kongVm.UserData.Stamina = stamina;

                //// If less then 2 seconds pass, wait a bit
                //double timeElapsed = (double)stopwatch.ElapsedMilliseconds / 1000;
                //if (timeElapsed < 2000) Thread.Sleep(1000);
                //stopwatch.Stop();

            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }

        /// <summary>
        /// Auto missions, going down a list of mission types (guild quest, temp missions, etc)
        /// </summary>
        public static KongViewModel AutoMissions(string kongInfo, 
            int energyThreshold,
            List<MissionCompletion> missionCompletions, // From Api.Init
            List<Quest> quests, // From Api.Init or Api.GetFactionData (we don't pull that atm)
            bool doGuildQuests = true, bool doTempMissions = true, bool doSideMissions = true, bool skipIfMesmerize = false, 
            Quest quest1 = null, Quest quest2 = null, Quest quest3 = null)
        {
            KongViewModel kongVm = new KongViewModel();
            ApiManager.GetKongInfoFromString(kongVm, kongInfo);
            string kongName = kongVm.KongName;

            StringBuilder result = new StringBuilder();

            // ------------------------
            // Skip mission grind if Mesmerize Mutant (1516) is not 50/50 and this flag is set
            //
            // TODO: Manage Mutant livesims - we'd want to sim for those, set deck, then reset
            // ------------------------
            if (skipIfMesmerize)
            {
                MissionCompletion mc = missionCompletions.Where(x => x.Id == 1516).FirstOrDefault();
                if (mc == null || mc.NumberOfCompletions < 50)
                {
                    kongVm.PtuoMessage = kongName + " - skipping. Mesmerize mutant at " + mc.NumberOfCompletions + "/50\r\n";
                    return kongVm;
                }
            }


            // ---------------------------------------------
            // Guild quests - Always cost 50 energy
            // ---------------------------------------------
            kongVm = BotManager.GetUserAccount(kongInfo);
            int userEnergy = kongVm.UserData.Energy;
            if (doGuildQuests && userEnergy > 50)
            {
                int won = 0;
                int lost = 0;
                Console.WriteLine(kongName + " - grinding guild missions");

                // Loops 20 times (not indefinitely in case something breaks)
                for (int i = 0; i < 20; i++)
                {
                    if (quest1 != null && quest1.Progress < quest1.MaxProgress)
                    {
                        kongVm = BotManager.AutoMissionOrQuest(kongInfo, quest1.QuestId.ToString(), guildQuest: true);
                        if (kongVm.BattleData?.Winner == true)
                        {
                            quest1.Progress++;
                            won++;
                        }
                        else lost++;
                        userEnergy -= 50;
                    }
                    else if (quest2 != null && quest2.Progress < quest2.MaxProgress)
                    {
                        kongVm = BotManager.AutoMissionOrQuest(kongInfo, quest2.QuestId.ToString(), guildQuest: true);
                        if (kongVm.BattleData?.Winner == true)
                        {
                            quest2.Progress++;
                            won++;
                        }
                        else lost++;
                        userEnergy -= 50;
                    }
                    else if (quest3 != null && quest3.Progress < quest3.MaxProgress)
                    {
                        kongVm = BotManager.AutoMissionOrQuest(kongInfo, quest3.QuestId.ToString(), guildQuest: true);
                        if (kongVm.BattleData?.Winner == true)
                        {
                            quest3.Progress++;
                            won++;
                        }
                        else lost++;
                        userEnergy -= 50;
                    }
                    else break; // No guild quests need doing

                    // Running a mission ran into an issue
                    if (kongVm.Result == "False")
                    {
                        result.AppendLine(kongName + " - Encountered an error when running guild quests: " + kongVm.ResultMessage);
                        break;
                    }

                    // Not enough energy
                    if (userEnergy < energyThreshold || userEnergy < 50)
                    {
                        break;
                    }
                }

                // Report on wins/losses
                if (won + lost > 0) result.AppendLine(kongName + " - GQs: " + won + "/" + (won + lost));
                Console.WriteLine(kongName + " - GQs: " + won + "/" + (won + lost));
            }

            // ---------------------------------------------
            // Side missions - These typically cost 100 energy            
            // ---------------------------------------------
            // Most of these unlock when 142 is complete. Code to grind basic missions is needed if we get a new account
            kongVm = BotManager.GetUserAccount(kongInfo);
            userEnergy = kongVm.UserData.Energy;
            if (doSideMissions && userEnergy > 50)
            {
                Console.WriteLine(kongName + " - grinding side missions");

                // Eat away at some of the old quests first
                List<int> oldQuestIds = new List<int> {
                    410, // Savior of Acheron
                    3112, // Quest for Vindication - Supernatural Aid (Step 1)
                    3114, // Quest for Vindication - Challenges (Step 3)
                    3116, // Quest for Vindication - ?? (Step 5)
                    3118, // Quest for Vindication - Atonement (Step 7)
                    3120, // Quest for Vindication - The Return (Step 9)
                    4112, // Mythical Tyrant - Iron Master? (Step 2)
                    4114, // Mythical Tyrant - Steel Master (Step 4)
                    4116, // Mythical Tyrant - Supremacy Master (Step 6)
                    4118, // Mythical Tyrant - Pandemonium Master (Step 8)
                    1147, // Mesmerize Mutant Level 1,
                    1148, // Mesmerize Mutant Level 2,
                    1149, // Mesmerize Mutant Level 3,
                    1150, // Mesmerize Mutant Level 4,
                    1151, // Mesmerize Mutant Level 5,
                    1152, // Mesmerize Mutant Level 6,
                    1153, // Mesmerize Mutant Level 7,
                    1154, // Mesmerize Mutant Level 8,
                    1082, // Barracus Lost 4
                    1106, // Edge of Dominion

                    411 // Alpha Echo Master (200e x 50)
                };
                Quest oldQuest = quests.FirstOrDefault(x => oldQuestIds.Contains(x.Id));
                if (oldQuest != null)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        Console.WriteLine(kongName + " running mission " + oldQuest.MissionId);
                        kongVm = BotManager.AutoMissionOrQuest(kongInfo, oldQuest.MissionId.ToString());

                        // Stop grinding if mission energy is below threshold
                        if (userEnergy < energyThreshold || userEnergy < 100) break;
                    }
                }

                // If a mission hasn't been done 10 times, run it
                foreach (string missionId in CONSTANTS.SIDE_MISSIONS)
                {
                    // Most missions require 10 wins, but we add a symbol to indicate if a mission needs more
                    int completionsNeeded = 10;
                    if (missionId.Contains("+")) completionsNeeded = 25;
                    else if (missionId.Contains("*")) completionsNeeded = 50;

                    string mId = missionId.Replace("*", "").Replace("+", "").Trim();

                    // Get current wins on this mission
                    MissionCompletion mc = missionCompletions.Where(x => x.Id.ToString() == mId).FirstOrDefault();
                    if (mc == null) mc = new MissionCompletion { Id = 0, NumberOfCompletions = 0 };


                    // Run this mission until completed
                    if (mc.NumberOfCompletions < completionsNeeded)
                    {
                        int won = 0;
                        int lost = 0;

                        for (int i = 0; i < 25; i++)
                        {
                            Console.WriteLine(kongName + " running mission " + mId);
                            kongVm = BotManager.AutoMissionOrQuest(kongInfo, mId);

                            // Record mission wins/losses
                            if (kongVm.Result != "False")
                            {
                                if (kongVm.BattleData?.Winner == true)
                                {
                                    won++;
                                    mc.NumberOfCompletions++;
                                    if (mc.NumberOfCompletions >= completionsNeeded) break;
                                }
                                else
                                {
                                    lost++;
                                }

                                //kongVm = BotManager.GetUserAccount(kongInfo);
                                userEnergy = kongVm.UserData.Energy;
                            }
                            else
                            {
                                result.AppendLine(kongName + " - Encountered an error when running mission " + missionId + ": " + kongVm.ResultMessage);
                                Console.WriteLine(kongName + " - Encountered an error when running mission " + missionId + ": " + kongVm.ResultMessage);
                                break;
                            }

                            // Stop grinding if mission energy is below threshold
                            if (userEnergy < energyThreshold || userEnergy < 100) break;
                        }

                        // Report on wins/losses
                        if (won + lost > 0) result.AppendLine(kongName + " - Mission #" + missionId + ": " + won + "/" + (won + lost));
                        Console.WriteLine(kongName + " - Mission #" + missionId + ": " + won + "/" + (won + lost));
                    } //single mission

                    if (userEnergy < energyThreshold || userEnergy < 100)
                    {
                        break;
                    }
                } //sidemissions
            }

            // ---------------------------------------------
            // Try to do temporary event missions
            // These cost anywhere from 30 - 100 energy
            // ---------------------------------------------
            kongVm = BotManager.GetUserAccount(kongInfo);
            userEnergy = kongVm.UserData.Energy;
            if (doTempMissions && userEnergy > 50)
            {
                Console.WriteLine(kongName + " - grinding temporary event missions");

                // If a mission hasn't been done X times, run it
                foreach (string missionId in CONSTANTS.TEMP_MISSIONS)
                {
                    // Most missions require 10 wins, but we add a symbol to indicate if a mission needs more
                    int completionsNeeded = 10;
                    if (missionId.Contains("+")) completionsNeeded = 25;
                    else if (missionId.Contains("*")) completionsNeeded = 50;

                    string mId = missionId.Replace("*", "").Replace("+", "").Trim();

                    // Get current wins on this mission
                    MissionCompletion mc = missionCompletions.Where(x => x.Id.ToString() == mId).FirstOrDefault();
                    if (mc == null) mc = new MissionCompletion { Id = 0, NumberOfCompletions = 0 };

                    // Run this mission until completed
                    if (mc.NumberOfCompletions < completionsNeeded)
                    {
                        int won = 0;
                        int lost = 0;

                        for (int i = 0; i < 25; i++)
                        {
                            Console.WriteLine(kongName + " running mission " + mId);
                            kongVm = BotManager.AutoMissionOrQuest(kongInfo, mId);
                            
                            // Record mission wins/losses
                            if (kongVm.Result != "False")
                            {
                                if (kongVm.BattleData?.Winner == true)
                                {
                                    won++;
                                    mc.NumberOfCompletions++;
                                    if (mc.NumberOfCompletions >= completionsNeeded) break;
                                }
                                else
                                {
                                    lost++;
                                }

                                //kongVm = BotManager.GetUserAccount(kongInfo);
                                userEnergy = kongVm.UserData.Energy;
                            }
                            else
                            {
                                result.AppendLine(kongName + " - Encountered an error when running mission " + missionId + ": " + kongVm.ResultMessage);
                                Console.WriteLine(kongName + " - Encountered an error when running mission " + missionId + ": " + kongVm.ResultMessage);
                                break;
                            }

                            // Stop grinding if mission energy is below threshold
                            Console.WriteLine(kongName + " - " + kongVm.UserData.Energy + " left");
                            if (userEnergy < energyThreshold || userEnergy < 100) break;
                        }

                        // Report on wins/losses
                        if (won + lost > 0) result.AppendLine(kongName + " - Event #" + missionId + ": " + won + "/" + (won + lost));
                        Console.WriteLine(kongName + " - Event #" + missionId + ": " + won + "/" + (won + lost));

                        if (userEnergy < energyThreshold || userEnergy < 100) break;
                    } //single mission

                    if (userEnergy < energyThreshold || userEnergy < 100) break;
                } //sidemissions
            }

            // ---------------------------------------------
            // 142. Dump remaining energy into Alpha Echo (142)
            // ---------------------------------------------
            kongVm = BotManager.GetUserAccount(kongInfo);
            userEnergy = kongVm.UserData.Energy;


            if (userEnergy > 200 && kongVm.KongName != "KobraKai3776")
            {
                for (int i = 0; i < 5; i++)
                {
                    int won = 0;
                    int lost = 0;
                    Console.WriteLine(kongName + " running mission 142");
                    kongVm = BotManager.AutoMissionOrQuest(kongInfo, "142");

                    // Record mission wins/losses
                    if (kongVm.Result != "False")
                    {
                        if (kongVm.BattleData?.Winner == true) won++;
                        else lost++;

                        //kongVm = BotManager.GetUserAccount(kongInfo);
                        userEnergy = kongVm.UserData.Energy;
                    }
                    else
                    {
                        result.AppendLine(kongName + " - Encountered an error when running Alpha Echo: " + kongVm.ResultMessage);
                        Console.WriteLine(kongName + " - Encountered an error when running Alpha Echo: " + kongVm.ResultMessage);
                        break;
                    }

                    // Stop grinding if mission energy is below threshold
                    if (userEnergy < energyThreshold || userEnergy < 200) break;
                }
            }
            // Run mission 1 a lot on specific users. For metrics
            else if (kongVm.KongName == "KobraKai3776")
            {
                for (int i = 0; i < 20; i++)
                {
                    int won = 0;
                    int lost = 0;
                    Console.WriteLine(kongName + " running mission 1");
                    kongVm = BotManager.AutoMissionOrQuest(kongInfo, "1");

                    // Record mission wins/losses
                    if (kongVm.Result != "False")
                    {
                        if (kongVm.BattleData?.Winner == true) won++;
                        else lost++;

                        //kongVm = BotManager.GetUserAccount(kongInfo);
                        userEnergy = kongVm.UserData.Energy;
                    }
                    else
                    {
                        result.AppendLine(kongName + " - Encountered an error when running Mission 1: " + kongVm.ResultMessage);
                        Console.WriteLine(kongName + " - Encountered an error when running Mission 1: " + kongVm.ResultMessage);
                        break;
                    }

                    // Stop grinding if mission energy is below threshold
                    if (userEnergy < 15) break;
                }
            }

            Console.WriteLine(kongName + " - Done grinding missions. " + userEnergy + " energy left");

            // Spit out remaining energy
            if (kongVm.Result != "False")
            {
                //result.AppendLine(kongName + " - Missions done\r\n");
            }

            kongVm.PtuoMessage = result.ToString();
            return kongVm;
        }

        /// <summary>
        /// Auto a pvp match
        /// KongVm should have getBattleResults, with target_user_id
        /// </summary>
        public static KongViewModel AutoPvpBattles(string kongInfo, int numberOfAttacks, string ignoreGuilds="DireTide,TidalWave", bool liveSim=false, int turnsToLivesim=1)
        {
            // Get hunting targets
            KongViewModel kongVm = new KongViewModel();
            int wins = 0;
            int losses = 0;
            string kongName = "";

            try
            {
                while (numberOfAttacks > 0)
                {
                    // Get pvp targets
                    kongVm = new KongViewModel("getHuntingTargets");
                    ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                    ApiManager.CallApi(kongVm);

                    kongName = kongVm.KongName;


                    // This call somehow failed
                    if (kongVm.HuntingTargets.Count == 0)
                    {
                        kongVm.Result = "False";
                        kongVm.ResultMessage += kongName + " - PTUO: No Hunting target found.";
                        return kongVm;
                    }

                    // Don't fight listed guilds, unless our pvp list is full of them
                    List<HuntingTarget> huntingTargets = kongVm.HuntingTargets;
                    List<string> ignoreGuildsList = ignoreGuilds.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    if (ignoreGuildsList.Count > 0)
                    {
                        huntingTargets = huntingTargets.Where(x => !ignoreGuildsList.Contains(x.Guild)).ToList();
                        if (huntingTargets.Count == 0) huntingTargets = kongVm.HuntingTargets;
                    }

                    // Engage each pvp target in the current hunting list
                    foreach (var target in huntingTargets)
                    {
                        // Attack one target
                        if (liveSim == false)
                        {
                            kongVm = BotManager.AutoPvpBattle(kongInfo, target.UserId);
                        }
                        else
                        {
                            kongVm = BotManager.LivesimPvpBattle(kongInfo, target.UserId, turnsToLivesim);
                        }
                        
                        // If an error returns, stop here
                        if (kongVm.Result == "False")
                        {
                            return kongVm;
                        }

                        // Win or lose last battle
                        if (kongVm.BattleData.Winner == true) wins++;
                        else if (kongVm.BattleData.Winner == false) losses++;
                        else Console.WriteLine("Did not get a win or loss from this battle");

                        // Get attacks left                        
                        int stamina = kongVm.UserData.Stamina;
                        if (stamina > 0 && stamina < numberOfAttacks)
                        {
                            numberOfAttacks = stamina; // Attacks from the API
                        }
                        else
                        {
                            numberOfAttacks--; // Attacks from the counter
                        }

                        //form.crowsNestStaminaTextBox.Text = numberOfAttacks.ToString();
                        Thread.Sleep(10);

                        // If attacks are 0
                        if (numberOfAttacks == 0) break;
                    }
                }
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += kongName + " - PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }
            
            // Success - send back wins/losses
            kongVm.PtuoMessage = kongName + " PVP - " + wins + "/" + (wins + losses) + "\r\n";

            return kongVm;
        }

        /// <summary>
        /// Auto a pvp match and refill. Be careful on this
        /// </summary>
        public static KongViewModel AutoPvpBattlesWithRefills(MainForm form, string userData, int numberOfAttacks, int pvpRefills)
        {
            // Get hunting targets
            KongViewModel kongVm = new KongViewModel();
            int wins = 0;
            int losses = 0;

            try
            {
                while (numberOfAttacks > 0)
                {
                    // Get pvp targets
                    kongVm = new KongViewModel("getHuntingTargets");
                    ApiManager.GetKongInfoFromString(kongVm, userData);
                    ApiManager.CallApi(kongVm);


                    // This call somehow failed
                    if (kongVm.HuntingTargets.Count == 0)
                    {
                        kongVm.Result = "False";
                        kongVm.ResultMessage += "PTUO: No Hunting target found.";
                        return kongVm;
                    }

                    // Engage each pvp target in the current hunting list
                    foreach (var target in kongVm.HuntingTargets)
                    {
                        // Attack one target
                        kongVm = BotManager.AutoPvpBattle(userData, target.UserId);

                        // If an error returns, stop here
                        if (kongVm.Result == "False")
                        {
                            return kongVm;
                        }

                        // Win or lose last battle
                        if (kongVm.BattleData.Winner == true) wins++;
                        else if (kongVm.BattleData.Winner == false) losses++;
                        else Console.WriteLine("Did not get a win or loss from this battle");

                        // Get attacks left                        
                        int stamina = kongVm.UserData.Stamina;
                        if (stamina > 0)
                        {
                            numberOfAttacks = stamina; // Attacks from the API
                        }
                        else
                        {
                            numberOfAttacks--; // Attacks from the counter
                        }

                        //form.crowsNestStaminaTextBox.Text = numberOfAttacks.ToString();
                        Thread.Sleep(10);

                        // If attacks are 0
                        if (numberOfAttacks == 0) break;
                    }

                    if (numberOfAttacks == 0)
                    {
                        pvpRefills--;
                        if (pvpRefills > 0)
                        {
                            //Console.WriteLine("Refilling");
                            //kongVm.Message = "buyStaminaRefillTokens";
                            //ApiManager.CallApi(kongVm);
                            numberOfAttacks = 100;
                        }
                        else
                        {
                            Console.WriteLine("Done refilling");
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            // Success - send back wins/losses
            kongVm.PtuoMessage = kongVm.KongName + "PvP: " + wins + "/" + (wins + losses) + "\r\n";

            return kongVm;
        }

        /// <summary>
        /// Auto all raid battles
        /// KongVm should have getBattleResults, with target_user_id
        /// </summary>
        public static KongViewModel AutoRaidBattles(string kongInfo, int burnAttacksDownTo = 0) // bool isInFight?
        {
            // Get hunting targets
            KongViewModel kongVm = new KongViewModel();
            int raidId = 0;
            int raidLevel = 0;
            int wins = 0;
            int losses = 0;
            int energy = 100; // temp - the first thing we will do is get the players raid energy

            try
            {
                while (energy > 0 && energy > burnAttacksDownTo)
                {
                    // Get raid info to check level/energy
                    kongVm = new KongViewModel("getRaidInfo");
                    ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                    ApiManager.CallApi(kongVm);
                    Thread.Sleep(500);

                    raidId = kongVm.RaidData.Id;
                    raidLevel = kongVm.RaidData.Level;
                    energy = kongVm.RaidData.Energy;

                    kongVm = new KongViewModel("fightRaidBattle", apiParams: "raid_id=" + raidId + "&raid_level=" + raidLevel);
                    ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                    ApiManager.CallApi(kongVm);
                    Thread.Sleep(200);
                    if (kongVm.Result == "False") return kongVm;

                    // Set auto to true
                    kongVm = new KongViewModel("setUserFlag", "playCard", apiParams: "flag=autopilot&value=1");
                    ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                    ApiManager.CallApi(kongVm);
                    Thread.Sleep(200);
                    if (kongVm.Result == "False") return kongVm;

                    // Play card then hit skip
                    kongVm = new KongViewModel("playCard", apiParams: "card_uid=1&skip=1");
                    ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                    ApiManager.CallApi(kongVm);
                    Thread.Sleep(200);
                    if (kongVm.Result == "False") return kongVm;

                    // Get last fight results
                    kongVm = new KongViewModel("getBattleResults");
                    ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                    ApiManager.CallApi(kongVm);
                    if (kongVm.Result == "False") return kongVm;

                    if (kongVm.BattleData.Winner == true)
                    {
                        wins++;
                    }
                    else
                    {
                        losses++;
                    }

                    energy--;
                }

                // Get raid info to check level/energy
                kongVm = new KongViewModel("getRaidInfo");
                ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                ApiManager.CallApi(kongVm);

                // Show raid energy
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            // Spit out wins/losses
            kongVm.PtuoMessage = kongVm.KongName + ": " + wins + "/" + (wins + losses) + "\r\n";

            return kongVm;
        }

        /// <summary>
        /// Auto and play a campaign
        /// - No reserves yet
        /// </summary>
        public static KongViewModel StartCampaign(string kongInfo, int campaignId, int difficulty, 
                                                  int commanderId, Dictionary<string, Object> cards, Dictionary<string, Object> reserveCards)
        {
            // Get hunting targets
            KongViewModel kongVm = new KongViewModel();
            ApiManager.GetKongInfoFromString(kongVm, kongInfo);
            ApiManager.CallApi(kongVm);
            string kongName = kongVm.KongName;

            try
            {
                string cardsJson = JsonConvert.SerializeObject(cards);

                kongVm = new KongViewModel("startCampaign", "campaign_id=" + campaignId + "&difficulty=" + difficulty +
                    "&commander_id=" + commanderId +
                    "&cards=" + cardsJson +
                    "&reserves={}");
                ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                ApiManager.CallApi(kongVm);

                for (int i = 0; i < 7; i++)
                {
                    kongVm = new KongViewModel("fightCampaignBattle", "campaign_id=" + campaignId);
                    ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                    ApiManager.CallApi(kongVm);
                    Thread.Sleep(1000);

                    // Set auto to true
                    kongVm = new KongViewModel("playCard", "setUserFlag", "flag=autopilot&value=1");
                    ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                    ApiManager.CallApi(kongVm);
                    if (kongVm.Result == "False") break;

                    // Play card then hit skip
                    kongVm = new KongViewModel("playCard", "playCard", "card_uid=101&skip=1");
                    ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                    ApiManager.CallApi(kongVm);
                    Thread.Sleep(1000);
                    if (kongVm.Result == "False") break;

                    kongVm = new KongViewModel("getBattleResults");
                    ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                    ApiManager.CallApi(kongVm);
                    if (kongVm.Result == "False") break;

                    // Break out if taking a loss
                    if (kongVm.BattleData?.Winner == false) break;
                }
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += kongName + " - PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }

        #endregion

        #region Deck and Dominion

        /// <summary>
        /// Set the user's deck
        /// Will attempt to set the user's dominion if it is a legal dominion
        /// </summary>
        public static KongViewModel SetDeck(string userInfo, string deckString, string dominion1 = "", string dominion2 = "", bool settingActiveDeck = true, bool rebuildDominion = false)
        {
            KongViewModel kongVm = new KongViewModel();

            try
            {
                Deck deck = new Deck(deckString);
                StringBuilder apiParams = new StringBuilder();

                // Get user data to get the id
                kongVm = new KongViewModel("getUserAccount");
                ApiManager.GetKongInfoFromString(kongVm, userInfo);
                ApiManager.CallApi(kongVm);

                // Save user data - other calls lose some of this data
                // We need this for the Active/Defense deck IDs
                UserData userData = kongVm.UserData;

                // Are we setting active or defense deck?
                string deckId = "1";
                if (settingActiveDeck) deckId = kongVm.UserData.ActiveDeck;
                else deckId = kongVm.UserData.DefenseDeck;

                // If attack deck = defense deck, set active deck=1, defense deck=2
                if (kongVm.UserData.ActiveDeck == kongVm.UserData.DefenseDeck)
                {
                    // Move attack market to slot 1, defense marker to slot 2
                    kongVm = new KongViewModel("setActiveDeck", "deck_id=1");
                    ApiManager.GetKongInfoFromString(kongVm, userInfo);
                    ApiManager.CallApi(kongVm);

                    kongVm = new KongViewModel("setDefenseDeck", "deck_id=2");
                    ApiManager.GetKongInfoFromString(kongVm, userInfo);
                    ApiManager.CallApi(kongVm);
                    if (settingActiveDeck)
                    {
                        deckId = "1";
                    }
                    else
                    {
                        deckId = "2";
                    }
                }

                string activeYN = "1";
                if (!settingActiveDeck) activeYN = "0";

                // Some card(s) are not recognized
                if (deck.CardsNotFound.Count > 0)
                {
                    kongVm.Result = "False";
                    kongVm.ResultMessage += "PTUO: One or more cards not recognized: " + deck.CardsNotFound.First().Key + "\r\n";
                    return kongVm;
                }
                // No cards are recognized
                if (deck.Cards.Count < 1)
                {
                    kongVm.Result = "False";
                    kongVm.ResultMessage += "PTUO: No assault/structure cards recognized\r\n";
                    return kongVm;
                }

                // Construct apiParams
                Card commander = CardManager.GetPlayerCardByName(deck.Commander);
                if (commander == null)
                {
                    kongVm.Result = "False";
                    return kongVm;
                }

                apiParams.Append("commander_id=");
                apiParams.Append(commander.CardId);
                apiParams.Append("&deck_id=");
                apiParams.Append(deckId);
                apiParams.Append("&dominion_id=0&activeYN=" + activeYN + "&cards={");

                foreach (var cardPair in deck.CardObjectsAssaultsAndStructures)
                {
                    apiParams.Append("\"");
                    apiParams.Append(cardPair.Key.CardId);
                    apiParams.Append("\":\"");
                    apiParams.Append(cardPair.Value);
                    apiParams.Append("\",");
                }

                apiParams.Remove(apiParams.Length - 1, 1);
                apiParams.Append("}");


                // Setting deck
                kongVm = new KongViewModel("setDeckCards", "setDeckCards", apiParams.ToString());
                ApiManager.GetKongInfoFromString(kongVm, userInfo);
                ApiManager.CallApi(kongVm);
                if (kongVm.Result == "False" || !string.IsNullOrWhiteSpace(kongVm.ResultMessage))
                {
                    return kongVm;
                }

                // Set dominion if needed
                if (deck.HasDominion) // && dominion1 != "" && dominion2 != ""
                {
                    Card setDominionCard = CardManager.GetPlayerCardByName(deck.Dominion);
                    string setDominionName = setDominionCard.Name;
                    
                    if (setDominionCard != null)
                    {
                        // Player has this dominion already, set it
                        if (setDominionName == dominion1 || setDominionName == dominion2)
                        {
                            kongVm = new KongViewModel("setDeckDominion", "setDeckDominion", "deck_id=" + deckId + "&dominion_id=" + setDominionCard.CardId);
                            ApiManager.GetKongInfoFromString(kongVm, userInfo);
                            ApiManager.CallApi(kongVm);
                        }

                        // Player does not have this dominion and will reset
                        else if (rebuildDominion)
                        {
                            // Call init to get dominions and shards
                            kongVm.Message = "Init";
                            kongVm.Params = "";
                            ApiManager.CallApi(kongVm);

                            if (kongVm.Result != "False")
                            {
                                Card alphaDominion = kongVm.PlayerCards.Keys.Where(x => x.CardType == CardType.Dominion.ToString() && x.Name.Contains("Alpha")).FirstOrDefault();
                                Card nexusDominion = kongVm.PlayerCards.Keys.Where(x => x.CardType == CardType.Dominion.ToString() && !x.Name.Contains("Alpha")).FirstOrDefault();
                                Card dominionShards = kongVm.PlayerCards.Keys.Where(x => x.Name == "Dominion Shard").FirstOrDefault();
                                int.TryParse(kongVm.PlayerCards[dominionShards].ToString(), out int shards);
                                
                                // Error checks
                                if (shards < 360)
                                {
                                    kongVm.Result = "False";
                                    kongVm.ResultMessage = "Reset failed: At least 360 shards needed to reset\r\n";
                                    kongVm.PtuoMessage += "Reset failed: At least 360 shards needed to reset\r\n";
                                    return kongVm;
                                }
                                else
                                {
                                    // Alpha Dominion
                                    if (setDominionName.Contains("Alpha") && alphaDominion != null)
                                    {
                                        kongVm.PtuoMessage += "Reset " + alphaDominion.Name + " to " + setDominionName;
                                        kongVm = BotManager.ResetDominion(userInfo, alphaDominion.CardId, setDominionCard.CardId, setDominionAlpha: true);

                                        // An error happened when trying to reset
                                        if (kongVm.Result == "False")
                                        {
                                            return kongVm;
                                        }
                                    }
                                    // Nexus Dominion
                                    else if (setDominionName.Contains("Nexus") && nexusDominion.Name != null)
                                    {
                                        kongVm.PtuoMessage += "Reset " + alphaDominion.Name + " to " + setDominionName;
                                        kongVm = BotManager.ResetDominion(userInfo, nexusDominion.CardId, setDominionCard.CardId, setDominionAlpha: false);

                                        // An error happened when trying to reset
                                        if (kongVm.Result == "False")
                                        {
                                            return kongVm;
                                        }
                                    }
                                }
                            }
                        }
                        // Try setting the dominion, report failure if it doesn't work
                        else
                        {

                            kongVm = new KongViewModel("setDeckDominion", "setDeckDominion", "deck_id=" + deckId + "&dominion_id=" + setDominionCard.CardId);
                            ApiManager.GetKongInfoFromString(kongVm, userInfo);
                            ApiManager.CallApi(kongVm);

                            if (kongVm.Result == "False")
                                kongVm.PtuoMessage += kongVm.KongName + ": Does not have dominion " + setDominionName + "\r\n";
                        }
                    }
                }

                // Get user data back
                kongVm.UserData = userData;
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }

        /// <summary>
        /// ResetDominion
        /// </summary>
        public static KongViewModel ResetDominion(string kongInfo, int oldDominionId, int newDominionId, bool setDominionAlpha)
        {
            KongViewModel kongVm = new KongViewModel("respecDominionCard");

            try
            {
                int[] baseDominionIds = new int[] { 50001, 50002, 50238, 50239 };


                // Reset dominion unless its the base dominion
                if (!baseDominionIds.Contains(oldDominionId))
                {
                    kongVm.Params = "card_id=" + oldDominionId;

                    // Reset dominion
                    ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                    ApiManager.CallApi(kongVm);

                    // An error happened. Maybe we didn't get the correct old dominion. Let's try that first
                    if (kongVm.Result == "False")
                    {
                        Console.WriteLine("Reset dominion - error when trying to respec dominion: " + kongVm.ResultMessage);
                        return kongVm;
                    }
                }


                // Start to upgrade dominion
                if (CONSTANTS.DOMINION_UPGRADE_MAP.ContainsKey(newDominionId))
                {
                    int[] upgradePath = CONSTANTS.DOMINION_UPGRADE_MAP[newDominionId];

                    // Do X upgrades (Alpha does 3, Nexus 2)
                    for (int i = 0; i < upgradePath.Length; i++)
                    {
                        int currentCardId = upgradePath[i];
                        Card card = CardManager.GetById(currentCardId);
                        if (card != null)
                        {
                            string cardName = card.Name;

                            // Fusions
                            kongVm.PtuoMessage += "Fusing " + cardName + "\r\n";
                            //form.crowsNestOutputTextBox.AppendText("Fusing " + cardName + "\r\n");
                            //form.crowsNestDominionLabel.Text = "Fusing " + cardName;

                            kongVm = new KongViewModel("fuseCard");
                            kongVm.Params = "card_id=" + currentCardId;
                            ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                            ApiManager.CallApi(kongVm);

                            // Some error happened
                            if (kongVm.Result == "False")
                            {
                                return kongVm;
                            }

                            //form.crowsNestOutputTextBox.AppendText("Upgrading " + cardName + "\r\n");

                            // Upgrading
                            for (int k = 0; k < 5; k++)
                            {
                                kongVm = new KongViewModel("upgradeDominionCard");
                                kongVm.Params = "card_id=" + currentCardId;
                                ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                                ApiManager.CallApi(kongVm);

                                // Some error happened
                                if (kongVm.Result == "False")
                                {
                                    return kongVm;
                                }

                                currentCardId++;
                            }

                            kongVm.PtuoMessage += "Dominion reset\r\n";
                            //form.crowsNestDominionLabel.Text = "Dominion reset";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }

        #endregion

        #region Card Building

        /// <summary>
        /// Buy x gold packs, or until we can't (no gold, inventory space)
        /// If no packs are specified, buy until we can't
        /// </summary>
        public static KongViewModel BuyGold(MainForm form, string userData, int goldPacks = 0, bool displayGoldBuys = true)
        {
            KongViewModel kongVm = new KongViewModel("buyStorePromoGold");
            if (goldPacks == 0) goldPacks = 200;

            try
            {
                // Has to be called before buying
                kongVm = new KongViewModel("getStoreData");
                ApiManager.GetKongInfoFromString(kongVm, userData);
                ApiManager.CallApi(kongVm);

                kongVm = new KongViewModel("getPurchaseIdentifiers", "");
                ApiManager.GetKongInfoFromString(kongVm, userData);
                ApiManager.CallApi(kongVm);

                while (goldPacks > 0)
                {
                    kongVm = new KongViewModel("buyStorePromoGold", "buyStorePromoGold", "expected_cost=2000&item_id=48&item_type=3");

                    ApiManager.GetKongInfoFromString(kongVm, userData);
                    ApiManager.CallApi(kongVm);

                    // Not enough gold
                    if (kongVm.UserData.Gold < 2000)
                    {
                        if (displayGoldBuys) form.outputTextBox.AppendText("\r\nNot enough gold\r\n");
                        break;
                    }

                    // An error occured. Likely inventory related
                    if (kongVm.Result == "False")
                    {
                        if (displayGoldBuys) form.outputTextBox.AppendText(kongVm.ResultMessage + "\r\n");
                        break;
                    }

                    if (displayGoldBuys)
                    {
                        if (goldPacks % 10 == 0) form.outputTextBox.AppendText("$");
                        else form.outputTextBox.AppendText(".");

                    }

                    goldPacks--;

                    // Inventory space is stopping this - try to salvage commons/rares
                    //if (kongVm.Result == "False" && kongVm.ResultMessage == "")
                    //{
                    //    outputTextBox.AppendText(kongVm.ResultMessage + "\r\n");
                    //    break;
                    //}


                    // SP max
                    //if (int.Parse(kongVm.UserData.Salvage) > 11900)
                    //{
                    //    form.outputTextBox.AppendText("Not enough gold\r\n");
                    //    break;
                    //}
                }
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }

        /// <summary>
        /// Consume shards
        /// Use the epic shards (itemId 2000) and legend shards (itemId 2001) until we can't
        /// </summary>
        public static KongViewModel ConsumeShards(MainForm form, string userData, int goldPacks = 0)
        {
            KongViewModel kongVm = new KongViewModel();
            int epicPacks = 0;
            int legendPacks = 0;

            try
            {
                // Buy epic shards until we hit an error, or buy 100 (safeguard)
                for (int i = 0; i < 100; i++)
                {
                    kongVm = new KongViewModel("consumeItem", "consumeItem", "item_id=2000");
                    ApiManager.GetKongInfoFromString(kongVm, userData);
                    ApiManager.CallApi(kongVm);

                    if (kongVm.Result == "False")
                    {
                        //form.outputTextBox.AppendText(kongVm.ResultMessage + "\r\n");
                        //form.outputTextBox.AppendText("Claimed " + epicPacks + " packs\r\n");
                        break;
                    }
                    else
                    {
                        epicPacks++;
                    }
                }

                // Buy legend shards until we hit an error, or buy 100 (safeguard)
                for (int i = 0; i < 100; i++)
                {
                    kongVm = new KongViewModel("consumeItem", "consumeItem", "item_id=2001");
                    ApiManager.GetKongInfoFromString(kongVm, userData);
                    ApiManager.CallApi(kongVm);

                    if (kongVm.Result == "False")
                    {
                        //form.outputTextBox.AppendText("Claimed " + legendPacks + " packs\r\n");
                        break;
                    }
                    else
                    {
                        legendPacks++;
                    }
                }

                // Clear out the result - we expect an error when out of shards
                kongVm.Result = "True";
                kongVm.ResultMessage = "";
                kongVm.PtuoMessage = "Claimed " + epicPacks + " epic packs, " + legendPacks + " legend packs\r\n";
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }

        /// <summary>
        /// Upgrade a card to max level, if able
        /// </summary>
        public static KongViewModel UpgradeCard(string userData, int cardId)
        {
            // Initialize KongViewModel. But we need the kong data
            KongViewModel kongVm = new KongViewModel("upgradeCard", "card_id=" + cardId);

            try
            {
                int upgrades = 0;

                // Upgrade, max 5 times
                while (upgrades <= 5)
                {
                    ApiManager.GetKongInfoFromString(kongVm, userData);
                    ApiManager.CallApi(kongVm);

                    if (kongVm.Result != "False" && !String.IsNullOrWhiteSpace(kongVm.ResultNewCardId))
                    {
                        upgrades++;

                        // Check to see if this is max level
                        int.TryParse(kongVm.ResultNewCardId, out int newCardId);
                        Card card = CardManager.GetById(newCardId);
                        if (card != null && card.Level == card.MaxLevel)
                        {
                            return kongVm;
                        }
                        

                        // Not done upgrading, use the new card id for the next call
                        kongVm.Params = "card_id=" + kongVm.ResultNewCardId;
                    }
                    // Some error happened
                    else
                    {
                        return kongVm;
                    }
                }
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }

        /// <summary>
        /// Upgrade a card by name instead of id
        /// </summary>
        public static KongViewModel UpgradeCard(string userData, string cardName)
        {
            KongViewModel kongVm = new KongViewModel();

            Card card = CardManager.GetPlayerCardByName(cardName.ToString());
            if (card != null)
            {
                kongVm = BotManager.UpgradeCard(userData, card.CardId);

                // Exception: Brood Mother and Empress have two rarities. If this fails, try the other upgrade path
                if (kongVm.Result == "False")
                {
                    if (cardName == "Brood Mother-1") kongVm = BotManager.UpgradeCard(userData, 25602);
                    else if (cardName == "Empress-1") kongVm = BotManager.UpgradeCard(userData, 25626);
                    else if (cardName == "Brood Mother-2") kongVm = BotManager.UpgradeCard(userData, 25603);
                    else if (cardName == "Brood Mother-3") kongVm = BotManager.UpgradeCard(userData, 25604);
                    else if (cardName == "Brood Mother-4") kongVm = BotManager.UpgradeCard(userData, 25605);
                    else if (cardName == "Brood Mother-5") kongVm = BotManager.UpgradeCard(userData, 25606);
                    else if (cardName == "Empress-2") kongVm = BotManager.UpgradeCard(userData, 25627);
                    else if (cardName == "Empress-3") kongVm = BotManager.UpgradeCard(userData, 25628);
                    else if (cardName == "Empress-4") kongVm = BotManager.UpgradeCard(userData, 25629);
                    else if (cardName == "Empress-5") kongVm = BotManager.UpgradeCard(userData, 25630);
                }
            }
            else
            {
                kongVm.Result = "False";
                kongVm.ResultMessage = "PTUO: Could not find card " + cardName;
            }

            return kongVm;
        }

        /// <summary>
        /// Restore a card to its max level
        /// </summary>
        public static KongViewModel RestoreCard(string kongInfo, int cardId)
        {
            // Initialize KongViewModel. But we need the kong data
            KongViewModel kongVm = new KongViewModel("buybackCard", "card_id=" + cardId + "&number=1");

            try
            {
                ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                ApiManager.CallApi(kongVm);

                // No result is returned on success
                if (kongVm.Result == null) kongVm.Result = "True";

                // Attempt to upgrade this card
                if (kongVm.Result != "False")
                {
                    kongVm = BotManager.UpgradeCard(kongInfo, cardId);

                    // ** Fail **
                    if (kongVm.Result == "False" || String.IsNullOrWhiteSpace(kongVm.ResultNewCardId))
                    {
                        kongVm.ResultMessage = "Restored, but failed on upgrading: " + kongVm.ResultMessage;
                        kongVm.PtuoMessage = "Restored, but failed on upgrading: " + kongVm.ResultMessage;
                    }
                }
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }

        /// <summary>
        /// Restore a card by its name instead of id
        /// </summary>
        public static KongViewModel RestoreCard(string userData, string cardName)
        {
            KongViewModel kongVm = new KongViewModel();

            if (!cardName.EndsWith("-1")) cardName += "-1";

            Card card = CardManager.GetPlayerCardByName(cardName.ToString());
            if (card != null)
            {
                kongVm = BotManager.RestoreCard(userData, card.CardId);
            }
            else
            {
                kongVm.Result = "False";
                kongVm.ResultMessage = "PTUO: Could not find card " + cardName;
                kongVm.PtuoMessage = "PTUO: Could not find card " + cardName;
            }

            return kongVm;
        }

        /// <summary>
        /// Salvage a card
        /// </summary>
        public static KongViewModel SalvageCard(string kongInfo, int cardId, bool salvageLockedCards)
        {
            // Initialize KongViewModel. But we need the kong data
            KongViewModel kongVm = new KongViewModel("salvageCard", "card_id=" + cardId);

            try
            {
                ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                ApiManager.CallApi(kongVm);

                // If this card is locked, and we want to unlock it, unlock and try again
                if (salvageLockedCards && kongVm.ResultMessage.Contains("That card is currently locked"))
                {
                    kongVm.Message = "setCardLock";
                    kongVm.Params = "card_id=" + cardId + "&locked=0";
                    kongVm.Result = null;
                    kongVm.ResultMessage = "";
                    ApiManager.CallApi(kongVm);

                    return SalvageCard(kongInfo, cardId, true);
                }

            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }

        /// <summary>
        /// Salvage a card by its name instead of id
        /// </summary>
        public static KongViewModel SalvageCard(string kongInfo, string cardName, bool salvageLockedCards)
        {
            KongViewModel kongVm = new KongViewModel();

            Card card = CardManager.GetPlayerCardByName(cardName.ToString());
            if (card != null)
            {
                kongVm = BotManager.SalvageCard(kongInfo, card.CardId, salvageLockedCards);
            }
            else if (cardName.StartsWith("[") && cardName.EndsWith("]"))
            {
                int cardId = int.Parse(cardName.Replace("[", "").Replace("]", ""));
                kongVm = BotManager.SalvageCard(kongInfo, cardId, salvageLockedCards);
            }
            else
            {
                kongVm.Result = "False";
                kongVm.ResultMessage = "PTUO: Could not find card " + cardName;
            }



            return kongVm;
        }

        /// <summary>
        /// Salvage a card
        /// </summary>
        public static KongViewModel FuseCard(MainForm form, string userInfo, int cardId)
        {
            // Initialize KongViewModel. But we need the kong data
            KongViewModel kongVm = new KongViewModel("fuseCard", "card_id=" + cardId);

            try
            {
                ApiManager.GetKongInfoFromString(kongVm, userInfo);
                ApiManager.CallApi(kongVm);

                // No result is returned on success 
                if (kongVm.Result == null) kongVm.Result = "True";

            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }

        /// <summary>
        /// Fuse a card
        /// </summary>
        public static KongViewModel FuseCard(MainForm form, string userInfo, string cardName)
        {
            KongViewModel kongVm = new KongViewModel();

            if (!cardName.EndsWith("-1")) cardName = cardName + "-1";

            // -- Get card ID
            Card card = CardManager.GetPlayerCardByName(cardName);
            if (card == null) card = CardManager.GetPlayerCardByName(cardName.Replace("-1", ""));
            if (card.CardId == 42744) card.CardId = 42745; // Neocyte fusion core - fuse level 2 instead of level 1

            // -- Fuse card
            if (card != null)
            {
                kongVm = BotManager.FuseCard(form, userInfo, card.CardId);
            }
            else
            {
                kongVm.Result = "False";
                kongVm.ResultMessage = "PTUO: Could not fuse card " + cardName;
            }

            return kongVm;
        }
        
        /// <summary>
        /// Salvage L1 Commons and Rares
        /// </summary>
        public static KongViewModel SalvageL1CommonsAndRares(MainForm form, string userInfo)
        {
            // Initialize KongViewModel. But we need the kong data
            KongViewModel kongVm = new KongViewModel("salvageL1CommonCards"); // dummy=data

            try
            {
                ApiManager.GetKongInfoFromString(kongVm, userInfo);
                ApiManager.CallApi(kongVm);

                kongVm = new KongViewModel("salvageL1RareCards"); // dummy=data
                ApiManager.GetKongInfoFromString(kongVm, userInfo);
                ApiManager.CallApi(kongVm);
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }


        /// <summary>
        /// Reset a quad commander, getting back 4 Neocyte Fusion shards
        /// </summary>
        public static KongViewModel ResetCommander(string userData, int cardId)
        {
            // Initialize KongViewModel. But we need the kong data
            KongViewModel kongVm = new KongViewModel("resetCommanderCard", "card_id=" + cardId);

            try
            {
                ApiManager.GetKongInfoFromString(kongVm, userData);
                ApiManager.CallApi(kongVm);
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }

        #endregion

        #region Guild

        /// <summary>
        /// Get faction, and guild offense/defense decks
        /// ** This does not update last online time, but we need the player id
        /// </summary>
        public static KongViewModel UpdateFaction(MainForm form, string userInfo, bool pullPlayerDecks = true)
        {
            KongViewModel kongVm = new KongViewModel("updateFaction");

            try
            {
                // Get faction data
                kongVm = new KongViewModel("updateFaction", "updateFaction", "last_activity_id=0");
                ApiManager.GetKongInfoFromString(kongVm, userInfo);
                ApiManager.CallApi(kongVm);

                List<FactionMember> factionMembers = kongVm.Faction.Members;

                // Get profile data and scan each user's decks
                kongVm.ApiStatName = "getProfileData";
                kongVm.Message = "getProfileData";

                // Success - we need to do stuff
                if (kongVm.Result != "False")
                {
                    if (pullPlayerDecks)
                    {
                        // List of members
                        foreach (var factionMember in factionMembers)
                        {
                            // Set params to this player
                            kongVm.Params = "target_user_id=" + factionMember.UserId;

                            // Add an item to List<PlayerInfo>
                            ApiManager.GetKongInfoFromString(kongVm, userInfo);
                            ApiManager.CallApi(kongVm); // will add to KongVm.PlayerInfo

                            // debug
                            //form.outputTextBox.AppendText(member.Key + "\t" + member.Value + "\r\n");
                            form.outputTextBox.AppendText(".");
                        }
                        form.outputTextBox.AppendText("\r\n");
                    }
                }


            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }

        /// <summary>
        /// Call UpdateFaction
        /// ** This does not update last online time, but we need the player id
        /// </summary>
        public static KongViewModel UpdateFactionSimple(string userInfo)
        {
            KongViewModel kongVm = new KongViewModel("updateFaction");

            try
            {
                // Get faction data
                kongVm = new KongViewModel("updateFaction", "updateFaction", "last_activity_id=0");
                ApiManager.GetKongInfoFromString(kongVm, userInfo);
                ApiManager.CallApi(kongVm);

            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }


        /// <summary>
        /// Call buyFactionCard&slot_id=[11|12|21|22]&card_id=[fort_id]
        /// </summary>
        public static KongViewModel BuyFactionCard(string userInfo, int slotId, int fortressId)
        {
            KongViewModel kongVm = new KongViewModel("buyFactionCard");

            try
            {
                // Get faction data
                kongVm = new KongViewModel("buyFactionCard", "buyFactionCard", "slot_id=" + slotId + "&card_id=" + fortressId);
                ApiManager.GetKongInfoFromString(kongVm, userInfo);
                ApiManager.CallApi(kongVm);

            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }


        /// <summary>
        /// Leave the current guild
        /// </summary>
        public static KongViewModel LeaveFaction(string userInfo)
        {
            KongViewModel kongVm = new KongViewModel("leaveFaction");

            try
            {
                ApiManager.GetKongInfoFromString(kongVm, userInfo);
                ApiManager.CallApi(kongVm);

                // Success
                if (kongVm.Result != "False")
                {
                }
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }

        /// <summary>
        /// Join a guild with faction_id=x
        /// </summary>
        public static KongViewModel AcceptFactionInvite(string userInfo, string factionId)
        {
            KongViewModel kongVm = new KongViewModel("acceptFactionInvite", "faction_id=" + factionId);

            try
            {
                ApiManager.GetKongInfoFromString(kongVm, userInfo);
                ApiManager.CallApi(kongVm);

                // Success
                if (kongVm.Result != "False")
                {
                }
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }

        /// <summary>
        /// Send an invite to a player with target_user_id=x
        /// This requires an officer or guild leader
        /// </summary>
        public static KongViewModel SendFactionInvite(string userInfo, string targetUserId)
        {
            KongViewModel kongVm = new KongViewModel("sendFactionInvite", "target_user_id=" + targetUserId);

            try
            {
                ApiManager.GetKongInfoFromString(kongVm, userInfo);
                ApiManager.CallApi(kongVm);

                // Success
                if (kongVm.Result != "False")
                {
                }
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }

        /// <summary>
        /// Kick a player with target_user_id=x
        /// This requires an officer or guild leader
        /// </summary>
        public static KongViewModel KickFactionMember(string userInfo, string targetUserId)
        {
            KongViewModel kongVm = new KongViewModel("kickFactionMember", "target_user_id=" + targetUserId);

            try
            {
                ApiManager.GetKongInfoFromString(kongVm, userInfo);
                ApiManager.CallApi(kongVm);

                // Success
                if (kongVm.Result != "False")
                {
                }
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }

        /// <summary>
        /// Promote a member to officer (21)
        /// This requires guild leader
        /// </summary>
        public static KongViewModel PromoteFactionMember(string userInfo, string targetUserId)
        {
            KongViewModel kongVm = new KongViewModel("setMemberRank", "target_user_id=" + targetUserId + "&rank=21");

            try
            {
                ApiManager.GetKongInfoFromString(kongVm, userInfo);
                ApiManager.CallApi(kongVm);

                // Success
                if (kongVm.Result != "False")
                {
                }
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }

        /// <summary>
        /// Demote a member to member (1) 
        /// This requires guild leader
        /// </summary>
        public static KongViewModel DemoteFactionMember(string userInfo, string targetUserId)
        {
            KongViewModel kongVm = new KongViewModel("setMemberRank", "target_user_id=" + targetUserId + "&rank=21");

            try
            {
                ApiManager.GetKongInfoFromString(kongVm, userInfo);
                ApiManager.CallApi(kongVm);

                // Success
                if (kongVm.Result != "False")
                {
                }
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }


        /// <summary>
        /// Call buyFactionCard&slot_id=[11|12|21|22]&card_id=[fort_id]
        /// </summary>
        public static KongViewModel SetGuildBge(string userInfo, int bgeId)
        {
            KongViewModel kongVm = new KongViewModel("buyFactionBattlegroundEffect");

            try
            {
                // Get faction data
                kongVm = new KongViewModel("buyFactionBattlegroundEffect", "buyFactionBattlegroundEffect", "effect_id=" + bgeId);
                            //+ "&previous_effect_id=" + 0);
                ApiManager.GetKongInfoFromString(kongVm, userInfo);
                ApiManager.CallApi(kongVm);

            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }


        //kickFactionMember&target_user_id: 4710331 - faction is returned

        #endregion

        #region Store Buying


        /// <summary>
        /// Buy x gold packs, or until we can't (no gold, inventory space)
        /// If no packs are specified, buy until we can't
        /// </summary>
        public static KongViewModel BuyStorePromoTokens(MainForm form, string userData, int itemId, int itemType, int boxDiscountId=0)
        {
            KongViewModel kongVm = new KongViewModel("buyStorePromoTokens");

            try
            {
                // Has to be called before buying
                kongVm = new KongViewModel("getStoreData");
                ApiManager.GetKongInfoFromString(kongVm, userData);
                ApiManager.CallApi(kongVm);

                kongVm = new KongViewModel("getPurchaseIdentifiers", "");
                ApiManager.GetKongInfoFromString(kongVm, userData);
                ApiManager.CallApi(kongVm);

                // API Params - box_discount_id is optional when doing a box discount
                string apiParams = "expected_cost=60&item_id=" + itemId + "&item_type=" + itemType;
                if (boxDiscountId > 0) apiParams += "&box_discount_id=" + boxDiscountId;
                

                kongVm = new KongViewModel("buyStorePromoTokens", "buyStorePromoTokens", apiParams);

                ApiManager.GetKongInfoFromString(kongVm, userData);
                ApiManager.CallApi(kongVm);

                // An error occured.
                if (kongVm.Result == "False")
                {
                    form.outputTextBox.AppendText(kongVm.ResultMessage + "\r\n");
                }
                else
                {
                    form.outputTextBox.AppendText(kongVm.KongName + " - " + kongVm.StorePromoNewCards.Cards.Count + " new cards\r\n");

                    foreach (var card in kongVm.StorePromoNewCards.Cards.OrderByDescending(x => x.Rarity).ThenBy(x => x.Name))
                    {
                        form.outputTextBox.AppendText(kongVm.KongName + " - " + card.Name + "\r\n");
                    }

                    form.outputTextBox.AppendText("\r\n");
                }
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }


        #endregion

        /// <summary>
        /// Call GetConquestUpdate and parse it for CQ scores. The most accurate score will be the current guilds
        /// </summary>
        public static KongViewModel GetConquestUpdate(string kongInfo, List<CqZoneData> cqZoneDatas)
        {
            KongViewModel kongVm = new KongViewModel("getConquestUpdate");

            try
            {
                // If previous CQ Data exists, append this to the existing Cq to get a more accurate scoreboard
                kongVm.ConquestData.ConquestZones = cqZoneDatas;

                // Call API
                ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                ApiManager.CallApi(kongVm);
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }


        /// <summary>
        /// Get decks from one player 
        /// ** This does not update last online time, but we need the player id
        /// </summary>
        public static KongViewModel GetDecksOfPlayer(string userInfo, string targetUserId)
        {
            KongViewModel kongVm = new KongViewModel();

            try
            {
                // Get faction data
                kongVm = new KongViewModel("getProfileData", "getProfileData", "target_user_id=" + targetUserId);
                ApiManager.GetKongInfoFromString(kongVm, userInfo);
                ApiManager.CallApi(kongVm);
                
                // Success adds this to kongVm.PlayerInfo                
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }

        /// <summary>
        /// Refill mission energy
        /// </summary>
        public static KongViewModel MissionRefill(MainForm form, string kongInfo)
        {
            // Initialize KongViewModel. But we need the kong data
            KongViewModel kongVm = new KongViewModel("buyEnergyRefillTokens");

            try
            {
                ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                ApiManager.CallApi(kongVm);
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }

        #region Claim Rewards

        /// <summary>
        /// Claim raid reward
        /// </summary>
        public static KongViewModel ClaimRaidReward(MainForm form, string kongInfo, string raidId)
        {
            // Initialize KongViewModel
            KongViewModel kongVm = new KongViewModel("claimRaidReward", "raid_id=" + raidId);

            try
            {
                ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                ApiManager.CallApi(kongVm);
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }

        /// <summary>
        /// Claim brawl reward
        /// </summary>
        public static KongViewModel ClaimBrawlReward(MainForm form, string kongInfo)
        {
            // Initialize KongViewModel
            KongViewModel kongVm = new KongViewModel("claimBrawlRewards");

            try
            {
                ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                ApiManager.CallApi(kongVm);
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }

        /// <summary>
        /// Claim CQ reward
        /// </summary>
        public static KongViewModel ClaimConquestReward(MainForm form, string kongInfo)
        {
            // Initialize KongViewModel
            KongViewModel kongVm = new KongViewModel("claimConquestReward");

            try
            {
                ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                ApiManager.CallApi(kongVm);
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }

        /// <summary>
        /// Claim CQ reward
        /// </summary>
        public static KongViewModel ClaimFactionWarReward(MainForm form, string kongInfo)
        {
            // Initialize KongViewModel
            KongViewModel kongVm = new KongViewModel("claimFactionWarRewards");

            try
            {
                ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                ApiManager.CallApi(kongVm);
            }
            catch (Exception ex)
            {
                kongVm.Result = "False";
                kongVm.ResultMessage += "PTUO: An exception has occured in " + MethodBase.GetCurrentMethod().Name + ": " + ex.Message;
            }

            return kongVm;
        }

        #endregion


        #region Find or Guess Enemy Deck

        /// <summary>
        /// If the enemy deck is missing cards, we need to fill out the rest of the enemy deck
        ///  (1) Does its userId exist in the puller file (./_pullers.txt file)
        ///  (2) Check recentlogs
        ///  (3) Look through ogrelogs (-- outdated --)
        ///  (4) Fill out missing cards with "guessed" cards 
        ///      - option 1: use a random mix of player deck cards (default)
        ///      - option 2: use a random mix of existing enemy cards
        /// </summary>
        private static void GuessEnemyRemainingDeck(MainForm form, KongViewModel kongVm, string missingCardStrategy="")
        {
            // Used for tracking other player's API calls
            KongViewModel kongVm2 = new KongViewModel();


            try
            {
                // BattleData is not populated
                if (kongVm.BattleData.Turn == null) { return;  }

                // Enemy data
                string enemyName = kongVm.BattleData.EnemyName.Replace(":", ""); // Colons mess up tuo, parse out names like ":Ghost:"
                string enemyCommander = kongVm.BattleData.EnemyCommander;
                string enemyGuild = kongVm.BattleData.EnemyGuild;
                string enemyId = kongVm.BattleData.EnemyId;
                List<string> enemyPlayedCards = kongVm.BattleData.EnemyCardsPlayed.Select(x => x.Name).ToList();
                int enemyPlayedCardsCount = enemyPlayedCards.Count;

                int.TryParse(kongVm.BattleData.EnemySize, out int enemyDeckSize);
                if (enemyDeckSize <= 0) enemyDeckSize = 10;

                // Enemy logs
                List<string> enemyLoggedDecks = new List<string>();

                // Possible enemy cards remaining
                List<string> enemyUnplayedCards = new List<string>();
                List<string> enemyUnreliableCards = new List<string>();

                // Did we find a matching deck?
                bool stopSearching = false;

                // If we find a logged deck, but its short cards
                bool isLogMissingCards = false;


                // ------------------------------------------------------------
                // Enemy deck is NOT FULL - find enemy's remaining cards
                // 
                // ------------------------------------------------------------
                if (enemyPlayedCardsCount != enemyDeckSize)
                {
                    // ------------------------------------------------------------------------------------------------------------------------
                    // (0) If this is a brand new player (has a rare commander, name starts with Player), fill in their deck with commons
                    // ------------------------------------------------------------------------------------------------------------------------
                    
                    if (enemyName.StartsWith("Player") &&
                       (enemyCommander.StartsWith("Cyrus") || enemyCommander.StartsWith("Malika")))
                    {
                        // Remove everything up to the last :, leaving only their deck
                        string enemyDeck = "Infantry, Infantry, Infantry, Infantry, Infantry, Infantry, Infantry, Infantry, Infantry, Infantry";
                        enemyLoggedDecks.Add(enemyDeck.Trim());
                        stopSearching = true;

                        // Set guild to Unicorn
                        enemyGuild = "UNICORN";
                    }

                    // ------------------------------------------------------------
                    // Refresh lists that store logs
                    // ------------------------------------------------------------
                    if ((DateTime.Now - recentPullerLogLastUpdate).TotalSeconds > 120)
                    {
                        Console.WriteLine("Clearing recently pulled enemies - " + DateTime.Now.ToShortTimeString());
                        recentPullerLogLastUpdate = DateTime.Now;

                        lock (recentPullerLog)
                        {
                            recentPullerLog.Clear();
                        }
                    }

                    if ((DateTime.Now - pullerLogsLastUpdate).TotalSeconds > 180)
                    {
                        Console.WriteLine("Refreshing puller logs - " + DateTime.Now.ToShortTimeString());
                        pullerLogsLastUpdate = DateTime.Now;

                        List<string> pullerLogsFile = FileIO.SimpleRead(form, "./__puller.txt", false, displayError: false);
                        lock (pullerLogs)
                        {
                            pullerLogs = pullerLogsFile;
                        }
                    }

                    // Refresh pullerLogs if the date is new
                    if ((DateTime.Now - logsLastUpdate).TotalSeconds > 60)
                    {
                        Console.WriteLine("Refreshing recent/hard logs - " + DateTime.Now.ToShortTimeString());
                        logsLastUpdate = DateTime.Now;

                        List<string> logFile = FileIO.SimpleRead(form, "./config/recentlogs.txt", returnCommentedLines: false);
                                    logFile.AddRange(FileIO.SimpleRead(form, "./config/hardlogs.txt", returnCommentedLines: false));
                        lock (logs)
                        {
                            logs = logFile;
                        }
                    }


                    // ------------------------------------------------------------
                    // (1a) Check recentPullerLogs (what puller stores to)
                    // ------------------------------------------------------------
                    if (!stopSearching)
                    {
                        try
                        {
                            // To prevent concurrency. We could also try a ConcurrentQueue/Stack
                            List<string> recentPullerLogTemp = recentPullerLog.ToList();

                            foreach (var log in recentPullerLogTemp)
                            {
                                if (log.Contains(enemyName + ":") || log.Contains(enemyName + "("))
                                {
                                    // Get guild
                                    string[] enemyGuildSplit = log.Split(new string[] { "_def_" }, StringSplitOptions.RemoveEmptyEntries);
                                    if (enemyGuildSplit.Length == 2)
                                    {
                                        kongVm.BattleData.EnemyGuild = enemyGuildSplit[0];
                                        enemyGuild = enemyGuildSplit[0];
                                    }

                                    // Remove everything up to the last :, leaving only their deck
                                    string enemyDeck = Regex.Replace(log, "^.*:", "");
                                    string[] enemyCards = enemyDeck.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                                    // Add this to decks
                                    enemyLoggedDecks.Add(enemyDeck.Trim());
                                    kongVm.EnemyDeckSource = "Puller";

                                    Console.WriteLine("Found in recent puller memory");
                                    stopSearching = true;
                                }
                            }
                        }
                        catch(Exception ex)
                        {
                            Console.WriteLine("Error in GuessEnemyRemainingDeck: recentPullerLogs - " + ex.Message + "\r\n");
                            stopSearching = true;
                        }
                    }

                    // ------------------------------------------------------------
                    // (1b) Check tuo/_puller.txt, if it exists
                    // 
                    // ** First check if this enemy was recently pulled. Use that data
                    // 
                    // Each guild in the puller file is 3 lines:
                    // * GuildName: X
                    // * kongInfo: <kongString of user in guild>
                    // * <CSV of userIDs in guild>
                    // ------------------------------------------------------------
                    if (!stopSearching)
                    {
                        try
                        {
                            List<string> userIds = new List<string>();
                            int userId = 0;
                            if (pullerLogs.Count > 0)
                            {
                                // To prevent concurrency. We could also try a ConcurrentQueue/Stack
                                List<string> pullerLogsTemp = pullerLogs.ToList();

                                // Get every player ID in the file
                                foreach (var entry in pullerLogsTemp)
                                {
                                    // Comment
                                    if (entry.Trim().StartsWith("//")) continue;

                                    // Current guild line
                                    if (entry.Trim().StartsWith("Guild:")) continue;

                                    // KongInfo line
                                    if (entry.Trim().StartsWith("kong") || entry.Trim().StartsWith("user") || entry.Trim().StartsWith("password") || entry.Trim().StartsWith("syncode")) continue;

                                    // Probably a line for userIDs
                                    string[] splitLine = entry.Split(',');
                                    if (splitLine.Length > 1)
                                    {
                                        foreach (var userIdString in splitLine)
                                        {
                                            Int32.TryParse(userIdString, out userId);
                                            if (userId > 0) userIds.Add(userId.ToString());
                                        }
                                    }
                                }

                                // This playerID is in the puller
                                if (userIds.Contains(enemyId))
                                {
                                    try
                                    {
                                        string currentPullerGuild = "UnknownGuild";
                                        string currentKongInfo = "";

                                        foreach (var entry in pullerLogsTemp)
                                        {
                                            // Comment
                                            if (entry.Trim().StartsWith("//")) continue;

                                            // What's the current guild
                                            if (entry.Trim().StartsWith("Guild:"))
                                            {
                                                currentPullerGuild = entry.Split(':')[1];
                                                continue;
                                            }

                                            // Which name is this player id under
                                            if (entry.Trim().StartsWith("kong") || entry.Trim().StartsWith("user") || entry.Trim().StartsWith("password") || entry.Trim().StartsWith("syncode"))
                                            {
                                                currentKongInfo = entry;
                                                continue;
                                            }


                                            // Probably a line for userIDs
                                            string[] splitLine = entry.Split(',');
                                            foreach (var userIdString in splitLine)
                                            {
                                                // Found this player's ID - attempt to get their info
                                                if (userIdString.Trim() == enemyId)
                                                {
                                                    kongVm2 = BotManager.GetDecksOfPlayer(currentKongInfo, enemyId);

                                                    // Successful match
                                                    if (kongVm2.PlayerInfo.Count > 0)
                                                    {
                                                        // Try to pull this deck
                                                        //form.rumBarrelOutputTextBox.AppendText("reader: Match!\r\n");
                                                        string enemyDeck = kongVm2.PlayerInfo[0].DefenseDeck.DeckToString(groupCards: false);

                                                        lock (enemyLoggedDecks)
                                                        {
                                                            enemyLoggedDecks.Add(enemyDeck);
                                                        }

                                                        // Also add this to recently pulled players
                                                        pullerLogs.Add(enemyDeck);

                                                        enemyGuild = currentPullerGuild;
                                                        stopSearching = true;
                                                        kongVm.EnemyDeckSource = "Puller";
                                                        break;
                                                    }
                                                    else
                                                    {
                                                        // Try to pull this deck
                                                        ControlExtensions.InvokeEx(form.outputTextBox, x => form.outputTextBox.AppendText("** Could not get info from this player ID: " + enemyId + "\r\n"));
                                                    }
                                                }
                                            }

                                            // Found the enemy successfully
                                            if (stopSearching) break;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        ControlExtensions.InvokeEx(form.rumBarrelOutputTextBox, x => form.rumBarrelOutputTextBox.AppendText("Error when trying to pull this ID: " + ex.Message + "\r\n"));
                                    }
                                }
                            }
                            else
                            {
                                Console.WriteLine("pullerLogs is empty");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Error in GuessEnemyRemainingDeck: reading from _puller - " + ex.Message + "\r\n");
                            stopSearching = true;
                        }
                    }

                    // ------------------------------------------------------------
                    // (2) Check log files
                    // * ./config/recentlogs.txt: Next locations to check
                    // * ./config/hardlogs.txt: 
                    // * Basic Format: [guild]_def_[name]: <deck>
                    // ------------------------------------------------------------
                    if (!stopSearching)
                    {
                        try
                        {
                            foreach (var log in logs)
                            {
                                // Find a deck match or partial match
                                // * Ex: EGuild_def_Badguy: <deck>
                                // TODO: Property regex match partial logs "EGuild_def_Badguy(6/10): <deck>" 

                                if (log.Contains(enemyName + ":") || log.Contains(enemyName + "("))
                                {
                                    // Get guild
                                    string[] enemyGuildSplit = log.Split(new string[] { "_def_" }, StringSplitOptions.RemoveEmptyEntries);
                                    if (enemyGuildSplit.Length == 2)
                                    {
                                        kongVm.BattleData.EnemyGuild = enemyGuildSplit[0];
                                        enemyGuild = enemyGuildSplit[0];
                                    }

                                    // Remove everything up to the last :, leaving only their deck
                                    string enemyDeck = Regex.Replace(log, "^.*:", "");
                                    string[] enemyCards = enemyDeck.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                                    // Add this to decks
                                    enemyLoggedDecks.Add(enemyDeck.Trim());
                                    kongVm.EnemyDeckSource = "Logs";

                                    // If this deck matches the current enemy commander and deck size, skip other logs
                                    if (enemyCards.Count() > 2 &&
                                        enemyCards[0].Trim() == enemyCommander.Trim() && // Matching commander
                                        enemyCards.Count() - 2 >= enemyDeckSize) // Matching size minus commander/dominion
                                    {
                                        stopSearching = true;
                                        kongVm.EnemyDeckSource = "Logs - MATCH comm/decksize";

                                        enemyLoggedDecks.Clear();
                                        enemyLoggedDecks.Add(enemyDeck.Trim());
                                        stopSearching = true;
                                        break;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Error in GuessEnemyRemainingDeck: recent/hardlogs - " + ex.Message + "\r\n");
                            stopSearching = true;
                        }
                    }





                    // ------------------------------------------------------------
                    // (3) Process each loggedDeck, adding cards that we think are missing
                    // 
                    // ------------------------------------------------------------      
                    foreach (var enemyDeck in enemyLoggedDecks)
                    {
                        string[] cards = enemyDeck.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        if (cards.Length < 3) continue;

                        string eCommander = cards[0].Replace("-1", "").Trim();
                        string eDominion = cards[1].Trim();

                        // If Logged commander matches enemy commander, add cards to "enemyUnplayedCards"
                        // that weren't already played
                        if (eCommander == enemyCommander)
                        {
                            for (int i = 2; i < cards.Length; i++)
                            {
                                string enemyCard = cards[i].Trim();

                                // add this card it was was not played
                                if (!enemyPlayedCards.Contains(enemyCard))
                                {
                                    enemyUnplayedCards.Add(enemyCard);
                                }
                                // this card was already played - remove it from enemyCardsPlayed
                                else
                                {
                                    enemyPlayedCards.Remove(enemyCard);
                                }
                            }
                        }
                        // Commander does not match
                        else
                        {
                            for (int i = 2; i < cards.Length; i++)
                            {
                                string enemyCard = cards[i].Trim();

                                enemyUnreliableCards.Add(enemyCard.ToUpperInvariant());

                                // Break if the deck gets over the enemyDeckSize
                                if (enemyPlayedCards.Count + enemyUnreliableCards.Count >= enemyDeckSize) break;
                            }
                        }
                    }

                    // ADD MISSING CARDS TO ENEMY DECK
                    // Use X cards from 
                    // * unplayedCards
                    // * unreliableCards (opponent)
                    // * missingCardStrategy - random player cards or random enemy cards

                    int cardsToAdd = enemyDeckSize - enemyPlayedCardsCount;
                    Random r = new Random();

                    List<MappedCard> playerRandomCards = new List<MappedCard>();
                    playerRandomCards.AddRange(kongVm.BattleData.PlayerHand);
                    playerRandomCards.AddRange(kongVm.BattleData.PlayerDrawOrder);
                    playerRandomCards.AddRange(kongVm.BattleData.PlayerCardsPlayed);

                    List<MappedCard> enemyRandomCards = new List<MappedCard>();
                    enemyRandomCards.AddRange(kongVm.BattleData.EnemyCardsPlayed);

                    List<string> randomPlayerCards = playerRandomCards.Select(x => x.Name).ToList();
                    List<string> randomEnemyCards = enemyRandomCards.Select(x => x.Name).ToList();
                    //randomPlayerCards.RemoveAt(0);
                    //randomPlayerCards.RemoveAt(1);

                    isLogMissingCards = cardsToAdd > 0 ? true : false;

                    for (int i = 0; i < cardsToAdd; i++)
                    {
                        if (enemyUnplayedCards.Count > 0)
                        {
                            // TODO: Add unreliable vs reliable cards to the kongVm metadata, so we can distinguish them
                            kongVm.BattleData.EnemyCardsRemaining.Add(enemyUnplayedCards[0]);
                            enemyUnplayedCards.RemoveAt(0);
                        }
                        else if (enemyUnreliableCards.Count > 0)
                        {
                            kongVm.BattleData.EnemyCardsRemaining.Add(enemyUnreliableCards[0].ToUpper());
                            enemyUnreliableCards.RemoveAt(0);
                        }
                        // Add random cards to enemy deck
                        else
                        {
                            if (missingCardStrategy == "add own cards")
                            {
                                if (randomPlayerCards.Count > 0)
                                {
                                    // Randomly pick a player card to add
                                    int next = r.Next(randomPlayerCards.Count);
                                    kongVm.BattleData.EnemyCardsRemaining.Add(randomPlayerCards[next].ToUpper());
                                    randomPlayerCards.RemoveAt(next);
                                }
                            }
                            else
                            {
                                if (randomEnemyCards.Count > 0)
                                {
                                    // Randomly pick an enemy card to add. Don't remove that card from the pool
                                    int next = r.Next(randomEnemyCards.Count);
                                    kongVm.BattleData.EnemyCardsRemaining.Add(randomEnemyCards[next].ToUpper());
                                }
                            }
                        }
                    }
                }


                // ------------------------------------------------------------
                // Enemy deck is FULL but we don't have this player's guild
                // * Player guild is sometimes transmitted from the API, but not always
                // ------------------------------------------------------------
                else if (string.IsNullOrEmpty(enemyGuild))
                {
                    // Check logs
                    // Basic Format: [guild]_def_[name]: <deck>
                    foreach (var log in logs)
                    {
                        //if (log.Contains(enemyName + ":"))
                        if (log.Contains("_" + enemyName + ":"))
                        {
                            string[] enemyGuildSplit = log.Split(new string[] { "_def_" }, StringSplitOptions.RemoveEmptyEntries);
                            if (enemyGuildSplit.Length == 2)
                            {
                                kongVm.BattleData.EnemyGuild = enemyGuildSplit[0];
                            }
                            break;
                        }
                    }
                }


                // Set enemy guild
                if (string.IsNullOrWhiteSpace(kongVm.BattleData.EnemyGuild))
                {
                    kongVm.BattleData.EnemyGuild = enemyGuild;
                }

                // LOGGING PURPOSES - Set enemyGuild to empty if we're missing cards. This makes sure the enemy deck output
                // will continue capturing partials of this deck
                if (isLogMissingCards && kongVm.BattleData.EnemyGuild == "_UNKNOWN")
                {
                    kongVm.BattleData.EnemyGuild = "";
                }
            }
            catch (Exception ex)
            {
                form.outputTextBox.Text += "Error when trying to figure out enemy deck: \r\n" + ex.Message + "\r\n";
            }
        }

        // guessEnemyRemainingCards() - when this was integrated with ogrelicious
        // 
        // ------------------------------------------------------------
        // Officers - Check Ogre Logs if entry count is low
        // ------------------------------------------------------------
        //if (!stopSearching && enemyLoggedDecks.Count < 2 &&
        //    (CONFIG.role == "level3" || CONFIG.role == "newLevel3") && !form.debugMode)
        //{
        //    try
        //    {
        //        // stupid enemy name symbols
        //        string formattedEnemyName = enemyName.Replace("[", "").Replace("]", "").Replace(".", "");
        //        string results = Ogrelicious.FindPlayer(formattedEnemyName);

        //        if (!string.IsNullOrEmpty(results))
        //        {
        //            string[] ogreLogs = results.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        //            foreach (var ogreLog in ogreLogs)
        //            {
        //                //replace text decoration around player (GUILD_def_playername: DECK --> playername_: DECK)
        //                string enemyLog = Regex.Replace(ogreLog, "^.*_def_", "")
        //                                                .Replace("\\|0\\|", ":")
        //                                                .Replace("\\|-1\\|", ":")
        //                                                .Replace(",,", ",")
        //                                                .Replace(", ,", ",");

        //                string[] enemyLogSplit = enemyLog.Split(':');

        //                if (enemyLogSplit.Length >= 2)
        //                {
        //                    string loggedName = enemyLogSplit[0];
        //                    string loggedDeck = enemyLogSplit[1];
        //                    if (loggedName.StartsWith(enemyName) && !enemyLoggedDecks.Contains(loggedDeck))
        //                    {
        //                        // Try to assign a guild to this player
        //                        if (string.IsNullOrWhiteSpace(kongVm.BattleData.EnemyGuild))
        //                        {
        //                            string[] enemyGuildSplit = ogreLog.Split(new string[] { "_def_" }, StringSplitOptions.RemoveEmptyEntries);
        //                            if (enemyGuildSplit.Length == 2) kongVm.BattleData.EnemyGuild = enemyGuildSplit[0];
        //                        }

        //                        // Add this to decks
        //                        enemyLoggedDecks.Add(loggedDeck);
        //                        kongVm.EnemyDeckSource = "OgreLogs";

        //                        // Save to local logs so we don't keep calling it
        //                        //string saveDeckToLog = "\r\n" + enemyLog;
        //                        //FileIO.SimpleWrite(form, "./config", "recentlogs.txt", saveDeckToLog, append: true);
        //                    }
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        //this.outputTextBox.Text += "Could not find this player in the log file. Searching Ogrelogs also failed";
        //        Console.WriteLine("GetBattle(): Error searching ogre logs " + ex);
        //    }
        //}

        #endregion

    }
}
