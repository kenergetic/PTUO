using PirateTUO2.Classes;
using PirateTUO2.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PirateTUO2.Modules
{

    /// <summary>
    /// Manages sims - attempts to keep sim objects in check
    /// </summary>
    public static class NewSimManager
    {
        // These are batch sims that have not yet run
        public static List<BatchSim> BatchSims = new List<BatchSim>();

        public static string commandLine = "cmd.exe";
        public static int simId = 1;
        public static int batchSimId = 1;
        
        #region Live Sim 

        /// <summary>
        /// Create a live sim from KongVm info
        /// * This uses the entire player draw order, locking cards in play and reordering
        /// * As an option, it will lock X cards ahead
        /// </summary>
        public static BatchSim BuildLiveSim(KongViewModel kongVm, string specialGameMode="", int iterations = 500, int extraLockedCards = 0)
        {
            int id = Helper.GetNewId();
            BatchSim batchSim = new BatchSim(id, SimMode.LIVESIM_COMPLEX);
            bool useTuoX86 = false;

            try
            {
                // player
                string player = kongVm.KongName;

                // MyDeck
                var regex = new Regex(".*?,");
                string myCommander = kongVm.BattleData.PlayerCommander;
                string myCardsInPlay = string.Join(",", kongVm.BattleData.PlayerCardsPlayed.Select(x => x.Name).ToList());
                string myCardsInHand = string.Join(",", kongVm.BattleData.PlayerHand.Select(x => x.Name).ToList());
                string myCardsInDeck = string.Join(",", kongVm.BattleData.PlayerDrawOrder.Select(x => x.Name).ToList());

                string myDeckWithCommander = myCommander + "," + myCardsInPlay + "," + myCardsInHand + "," + myCardsInDeck;
                string myDeckWithoutCommander = regex.Replace(myDeckWithCommander, "", 1);
                Deck myDeck = new Deck(myDeckWithCommander);

                // EnemyDeck
                string enemyCommander = kongVm.BattleData.EnemyCommander;
                string enemyPlayedCards = string.Join(",", kongVm.BattleData.EnemyCardsPlayed.Select(x => x.Name).ToList());
                string enemyPossibleCards = string.Join(",", kongVm.BattleData.EnemyCardsRemaining);
                string enemyDeck = enemyCommander + "," + enemyPlayedCards + "," + enemyPossibleCards;

                // Dominion
                string myDominion = kongVm.BattleData.PlayerDominion;
                string enemyDominion = kongVm.BattleData.EnemyDominion;
                if (myDominion == "[-1]") myDominion = "";
                if (enemyDominion == "[-1]") enemyDominion = "";

                string myForts = string.Join(",", kongVm.BattleData.PlayerForts);
                string enemyForts = string.Join(",", kongVm.BattleData.EnemyForts);
                //TODO: Replace Phobos Station with [8507], [8506], or [8505] to make tuo stop complaining
                myForts = myForts.Replace("Phobos Station-1", "[8505]")
                                .Replace("Phobos Station-2", "[8506]")
                                .Replace("Phobos Station-2", "[8507]");

                // BGEs
                string bge = kongVm.BattleData.BGE;
                string myBge = kongVm.BattleData.PlayerBGE;
                string enemyBge = kongVm.BattleData.EnemyBGE;

                // Game mode
                bool battleMode = kongVm.BattleData.IsAttacker;
                string gameMode = battleMode ? "pvp" : "surge";
                if (specialGameMode == "gw" || specialGameMode == "brawl") gameMode = specialGameMode;

                // Turns
                int.TryParse(kongVm.BattleData.Turn, out int turns);

                // extraLockedCards: How many cards ahead are we locking?

                // How many cards to freeze. t/2 will work for battle and surge
                int frozenCards = battleMode ? (turns / 2) : ((turns - 1) / 2);

                int SIM_TIMEOUT = 120000;

                // All combinations of player cards 
                // Example: Player cards are ..[123]4..., where 123 are cards in hand and 4 is one card ahead
                // Valid combinations if we play 2 cards before doing the rest random are [12] [13] [14] - [21] [23] [24] - [31] [32] [34] , but not [4x] because 
                List<string> playerCardCombinations = new List<string>();

                var myOrderedDecks = TextCleaner.CardOrdersForLivePlay(myDeck.DeckToString(includeCommander: true, includeDominion: false), frozenCards + 1, extraLockedCards);

                foreach (var myOrderedDeck in myOrderedDecks)
                {
                    Sim sim = new Sim
                    {
                        Id = id,
                        Mode = SimMode.LIVESIM_COMPLEX,
                        Player = player,
                        Guild = "",

                        // Usually tuo
                        TuoExecutable = "tuo",
                        UseX86 = useTuoX86,

                        // Decks
                        MyDeck = new Deck(myOrderedDeck),
                        EnemyDeck = enemyDeck,

                        // Towers
                        MyDominion = myDominion,
                        EnemyDominion = enemyDominion,
                        MyForts = myForts,
                        EnemyForts = enemyForts,

                        // Bges
                        Bge = bge,
                        MyBge = myBge,
                        EnemyBge = enemyBge,

                        // GameMode / Operation
                        GameMode = gameMode,
                        Iterations = iterations,
                        Operation = "reorder " + iterations,

                        // Reorder: freeze beyond the initial cards
                        Freeze = frozenCards + extraLockedCards,
                        ExtraFreezeCards = extraLockedCards,
                        Hand = myDeckWithoutCommander, // fell puts entire deck in hand
                        EnemyHand = enemyPlayedCards,

                        Timeout = SIM_TIMEOUT,

                        // Metadata
                        ApiHand = kongVm.BattleData.PlayerHand
                    };

                    // DEBUG
                    //Console.WriteLine(sim.SimToString()); //TODO: Output this from the returning function
                    batchSim.Sims.Add(sim);
                }
            }
            catch (Exception ex)
            {
                batchSim.StatusMessage = "Syntax error in BuildSim_Live(): " + ex.Message;
            }

            return batchSim;
        }

        /// <summary>
        /// Create a live sim from RumBarrel form
        /// * This uses the entire player draw order, locking cards in play and reordering
        /// * As an option, it will lock X cards ahead
        /// </summary>
        public static BatchSim BuildLiveSimRumBarrel(MainForm form)
        {
            int id = Helper.GetNewId();
            BatchSim batchSim = new BatchSim(id, SimMode.LIVESIM_COMPLEX);
            bool useTuoX86 = false;

            try
            {
                // Turns
                if (!int.TryParse(form.rumBarrelTurnTextBox.Text, out int turns)) turns = 0;

                // extraFreezeCards
                Int32.TryParse(form.rumBarrelCardsAheadComboBox.Text, out int extraFreezeCards);
                if (extraFreezeCards == -1) extraFreezeCards = 0;

                Int32.TryParse(form.rumBarrelIterationsTextBox.Text, out int iterations);
                if (iterations <= 0) iterations = 500;

                // Game mode
                string gameMode = "surge";
                bool surgeMode = true;
                if (form.rumBarrelGameModeLabel.Text == "Battle")
                {
                    gameMode = "pvp";
                    surgeMode = false;
                }
                else if (form.rumBarrelGameModeSelectionComboBox.Text == "gw") gameMode = "gw";
                else if (form.rumBarrelGameModeSelectionComboBox.Text == "brawl") gameMode = "brawl";
                else gameMode = "surge";

                // How many cards to freeze. t/2 will work for battle and surge
                int frozenCards = surgeMode ? ((turns - 1) / 2) : (turns / 2);

                // Assemble MyDeck
                var regex = new Regex(".*?,");
                string myCardsInPlay = form.rumBarrelMyDeckTextBox.Text.Replace("\r\n", ",").TrimEnd(',');
                string myCardsInHand = form.rumBarrelPlayerHandTextBox.Text.Replace("\r\n", ", ").TrimEnd(',');
                string myCardsInDeck = form.rumBarrelDrawOrderTextBox.Text.Replace("\r\n", ", ").TrimEnd(',');

                string myDeckWithCommander = myCardsInPlay + "," + myCardsInHand + "," + myCardsInDeck;
                string myDeckWithoutCommander = regex.Replace(myDeckWithCommander, "", 1);
                if (myDeckWithoutCommander.StartsWith(",")) myDeckWithoutCommander = myDeckWithoutCommander.Substring(1, myDeckWithoutCommander.Length - 1);

                Deck myDeck = new Deck(myDeckWithCommander);

                // Assemble EnemyDeck
                string enemyPlayedCardsWithCommander = form.rumBarrelEnemyDeckInitialTextBox.Text.Trim().TrimEnd(',');
                string enemyPlayedCards = regex.Replace(enemyPlayedCardsWithCommander, "", 1);
                string enemyPossibleCards = form.rumBarrelEnemyDeckPossibleRemainingCardsTextBox.Text.Replace("\r\n", ", ").Trim().TrimEnd(',');
                string enemyDeck = enemyPlayedCardsWithCommander;
                if (enemyPossibleCards.Length > 0) enemyDeck += ", " + enemyPossibleCards;

                
                // All combinations of player cards 
                // Example: Player cards are ..[123]4..., where 123 are cards in hand and 4 is one card ahead
                // Valid combinations if we play 2 cards before doing the rest random are [12] [13] [14] - [21] [23] [24] - [31] [32] [34] , but not [4x] because 
                List<string> playerCardCombinations = new List<string>();

                var myOrderedDecks = TextCleaner.CardOrdersForLivePlay(myDeck.DeckToString(includeCommander: true, includeDominion: false), frozenCards + 1, extraFreezeCards);

                // milliseconds
                int SIM_TIMEOUT = 120000;
                // DEBUG - TO FORCE A TIMEOUT
                // SIM_TIMEOUT = 10;

                foreach (var myOrderedDeck in myOrderedDecks)
                {
                    Sim sim = new Sim
                    {
                        Id = id,
                        Mode = SimMode.LIVESIM_COMPLEX,
                        Player = form.rumBarrelKongNameLabel.Text,
                        Guild = "",

                        // Usually tuo
                        TuoExecutable = "tuo",
                        UseX86 = useTuoX86,

                        // Decks
                        MyDeck = new Deck(myOrderedDeck),
                        EnemyDeck = enemyDeck,

                        // Towers
                        MyDominion = form.rumBarrelMyDominionTextBox.Text,
                        EnemyDominion = form.rumBarrelEnemyDominionTextBox.Text,
                        MyForts = form.rumBarrelMyFortTextBox.Text,
                        EnemyForts = form.rumBarrelEnemyFortTextBox.Text,

                        // Bges
                        Bge = form.rumBarrelBgeTextBox.Text,
                        MyBge = form.rumBarrelMyBgeTextBox.Text,
                        EnemyBge = form.rumBarrelEnemyBgeTextBox.Text,

                        // GameMode / Operation
                        GameMode = gameMode,
                        Iterations = iterations,
                        Operation = "reorder " + iterations,

                        // Reorder: freeze beyond the initial cards
                        Freeze = frozenCards + extraFreezeCards,
                        ExtraFreezeCards = extraFreezeCards,
                        Hand = myDeckWithoutCommander, // fell puts entire deck in hand
                        EnemyHand = enemyPlayedCards,

                        Timeout = SIM_TIMEOUT,
                    };

                    // Metadata - get hand. If KongVm is passed, extract it from there. Otherwise, extract it from RumBarrel
                    // Using this to get the MappedCardId
                    //if (kongVm != null)
                    //{
                    //    sim.ApiHand = kongVm.BattleData.PlayerHand;
                    //}
                    //else
                    //{
                        string[] rumBarrelHand = form.rumBarrelPlayerHandTextBox.Text.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                        foreach(var card in rumBarrelHand)
                        {
                            sim.ApiHand.Add(new MappedCard(0, card));
                        }
                    //}

                    form.rumBarrelSimStringOutputTextBox.AppendText(sim.SimToString() + "\r\n");
                    batchSim.Sims.Add(sim);                    
                }

            }
            catch (Exception ex)
            {
                batchSim.StatusMessage = "Syntax error in BuildSim_Live(): " + ex.Message;
            }

            return batchSim;
        }

        /// <summary>
        /// Run a complex live sim. This is a compilation of many sims
        /// </summary>
        public static void RunLiveSim(BatchSim batchSim)
        {
            try
            {
                // This is run from a parallel method, and chaining might be breaking stuff
                var numberOfSims = batchSim.Sims.Count;
                foreach(var sim in batchSim.Sims)
                {
                    RunSim(sim);
                }

                // Run sims in parallel, but only let 3 at a time run
                //Parallel.ForEach(batchSim.Sims, new ParallelOptions { MaxDegreeOfParallelism = 3 }, sim =>
                //{
                //    RunSim(sim);
                //});

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error on RunLiveSim_Complex(): " + ex + "\r\n");
            }
        }

        /// <summary>
        /// Run a complex live sim. This is a compilation of many sims
        /// </summary>
        public static void RunLiveSimRumBarrel(MainForm form, BatchSim batchSim, bool updateForm=true, int threads=3)
        {
            try
            {
                // Start the progress bar in navigator
                if (updateForm)
                {
                    ControlExtensions.InvokeEx(form.rumBarrelProgressBar, x => form.rumBarrelProgressBar.Maximum = batchSim.Sims.Count);
                    ControlExtensions.InvokeEx(form.rumBarrelProgressBar, x => form.rumBarrelProgressBar.Value = 0);
                    ControlExtensions.InvokeEx(form.rumBarrelProgressBar, x => form.rumBarrelProgressBar.Step = 1);
                }

                ControlExtensions.InvokeEx(form.rumBarrelOutputTextBox, x => form.rumBarrelOutputTextBox.Text = "");

                

                // Run sims in parallel, but only let 3 at a time run
                var numberOfSims = batchSim.Sims.Count;
                Parallel.ForEach(batchSim.Sims, new ParallelOptions { MaxDegreeOfParallelism = threads }, sim =>
                {
                    RunSim(sim);
                    if (updateForm)
                    {
                        ControlExtensions.InvokeEx(form.outputTextBox, x => form.outputTextBox.AppendText(sim.StatusMessage));
                        ControlExtensions.InvokeEx(form.rumBarrelProgressBar, x => form.rumBarrelProgressBar.PerformStep());
                    }
                });
                
                // Write out sims
                foreach(Sim sim in batchSim.Sims.OrderByDescending(x => x.WinPercent).ThenByDescending(x => x.WinScore))
                {
                    ControlExtensions.InvokeEx(form.rumBarrelOutputTextBox, x => form.rumBarrelOutputTextBox.AppendText(WriteLiveSim(form, sim)));
                    ControlExtensions.InvokeEx(form.rumBarrelOutputTextBox, x => form.rumBarrelOutputTextBox.SelectionStart = 0);
                    ControlExtensions.InvokeEx(form.rumBarrelOutputTextBox, x => form.rumBarrelOutputTextBox.ScrollToCaret());
                }

            }
            catch (Exception ex)
            {
                if (updateForm)
                {
                    ControlExtensions.InvokeEx(form.outputTextBox, x => form.outputTextBox.AppendText("Error on RunLiveSim(): " + ex + "\r\n\r\n"));
                    ControlExtensions.InvokeEx(form.rumBarrelProgressBar, x => form.rumBarrelProgressBar.Value = 0);
                }
            }

            if (updateForm)
            {
                ControlExtensions.InvokeEx(form.rumBarrelProgressBar, x => form.rumBarrelProgressBar.Value = 0);
            }
        }

        /// <summary>
        /// Write a LiveSim result to the output window
        /// </summary>
        public static string WriteLiveSim(MainForm form, Sim sim)
        {
            try
            {
                StringBuilder result = new StringBuilder();

                // This sim did not run
                if (sim.ResultDeck == null) return "This sim did not run - possibly from a timeout\r\n";

                // WinRate / WinScore 
                // FrozenCards, [Next three], Remaining cards
                result.AppendLine("Win Rate: " + sim.WinPercent.ToString() + (sim.WinScore > 0 ? " (" + sim.WinScore + " points)" : ""));

                string modifiedPlayerDeck = "";
                List<string> playerCards = sim.ResultDeck.Split(',').ToList();
                List<string> playerCardsUncompressed = TextCleaner.UncompressDeck(playerCards);

                int cardsPlayedCount = sim.Freeze - sim.ExtraFreezeCards;

                // Locking 1+ cards ahead of turn
                if (sim.ExtraFreezeCards > 0)
                {
                    for (int i = 0; i < playerCardsUncompressed.Count; i++)
                    {
                        var card = playerCardsUncompressed[i];
                        if (card == "") break;

                        // First 2 cards: Commander, dominion
                        if (i < 2)
                        {
                            //modifiedPlayerDeck += card + ",";
                        }
                        // Frozen cards plus commander
                        else if (i < cardsPlayedCount + 1)
                        {
                            modifiedPlayerDeck += card + ", ";
                        }
                        else if (i == cardsPlayedCount + 1)
                        {
                            modifiedPlayerDeck += card + "\r\n";
                        }
                        // Cards whose play order is considered
                        else if (i < sim.Freeze + 1)
                        {
                            modifiedPlayerDeck += "[" + card + "], ";
                        }
                        else if (i == sim.Freeze + 1)
                        {
                            modifiedPlayerDeck += "[" + card + "]\r\n";
                        }
                        // Remaining in 'order'
                        else
                        {
                            if (i - 1 == sim.Freeze) modifiedPlayerDeck += "\r\n";
                            modifiedPlayerDeck += card + ", ";
                        }
                    }
                }
                // Single sim - we need to look at the order and find the first card that is in hand
                else
                {
                    bool foundCardToPlay = false;

                    for (int i = 0; i < playerCardsUncompressed.Count; i++)
                    {
                        var card = playerCardsUncompressed[i];
                        if (card == "") break;

                        // First 2 cards: Commander, dominion
                        if (i < 2)
                        {
                            //modifiedPlayerDeck += card + ",";
                        }
                        // Frozen cards plus commander
                        else if (i < cardsPlayedCount + 1)
                        {
                            modifiedPlayerDeck += card + ", ";
                        }
                        else if (i == cardsPlayedCount + 1)
                        {
                            modifiedPlayerDeck += card + "\r\n";
                        }
                        // Write out the rest, but highlight the first card that can be played
                        else
                        {
                            if (!foundCardToPlay && sim.ApiHand.Select(x => x.Name).Contains(card))
                            {
                                modifiedPlayerDeck += "[" + card + "], ";
                                foundCardToPlay = true;
                            }
                            else
                            {
                                modifiedPlayerDeck += card + ", ";
                            }
                        }
                    }
                }

                result.AppendLine(modifiedPlayerDeck);
                result.AppendLine();
                return result.ToString();
            }
            catch(Exception ex)
            {
                return "Error on WriteLiveSim() - " + ex.Message + "\r\n";
            }

        }

        #endregion

        #region Navigator Sim

        /// <summary>
        /// Build a Navigator sim
        /// * This does a 'reorder/flex' against an entire enemy gauntlet
        /// </summary>
        public static void BuildSim_Navigator(MainForm form)
        {
            // Each line is a player deck
            string[] decks = form.navigatorPlayerDeckTextBox.Text.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            try
            {

                // ----------------------------------
                // Open the gauntlet 
                // - Make sure we can create a gauntlet out of it
                // - Look for matching gauntlet decks
                // - Turn each into a reorder sim
                // ----------------------------------
                string file = form.navSimCustomDecksComboBox.Text;
                List<string> gauntletFile = FileIO.SimpleRead(form, "./data/customdecks_" + file + ".txt", returnCommentedLines: false);
                string enemyGauntletName = form.navSimEnemyDeckTextBox.Text;
                string pattern = "";
                List<string> enemyDecks = new List<string>();

                foreach (var line in gauntletFile)
                {
                    // Get the gauntlet line and extract the regex
                    if (line.StartsWith(enemyGauntletName) && Regex.IsMatch(line, "/\\^.*\\$/"))
                    {
                        pattern = Regex.Match(line, "/\\^.*\\$/").Value.Replace("/^", "").Replace("$/", "");
                        continue;
                    }

                    // If we have the pattern, find decks that match the pattern
                    if (pattern.Length > 0 &&
                        !line.Contains("/^") &&
                        Regex.IsMatch(line, pattern))
                    {
                        // Add an existing deck to the pattern
                        var firstColon = line.IndexOf(":");
                        var enemyDeck = line.Substring(firstColon).Replace(":", "");
                        enemyDecks.Add(enemyDeck);
                    }
                }


                if (enemyDecks.Count > 0)
                {
                    // Queue 1..many decks in NavSim
                    foreach (var deck in decks)
                    {
                        int id = Helper.GetNewId();
                        BatchSim batchSim = new BatchSim(id, SimMode.NAVIGATOR);

                        // Add ! before every card in myDeck to force-keep the card in during reorder
                        string myDeck = deck.Replace("\r\n", "").Replace("\t", "").Replace(", ", ",").Replace(",", ",!");

                        // ----------------------------------
                        // Add a sim for each enemy deck
                        // ----------------------------------
                        batchSim.Description += myDeck + " **VS.** " + enemyGauntletName + "(" + enemyDecks.Count + " decks)";

                        // ----------------------------------
                        // Game Mode
                        // ----------------------------------
                        Int32.TryParse(form.navSimIterationsComboBox.Text, out int iterations);
                        if (iterations <= 0) iterations = 100;

                        string gameMode = "";
                        string operation = "";
                        if (form.navSimModeComboBox.Text == "flex")
                        {
                            int flexIterations = iterations / 3;
                            if (flexIterations < 10) flexIterations = 10;

                            gameMode = "surge flexible flexible-iter " + iterations / 3;
                            
                            operation = "climb " + iterations;
                        }
                        else // reorder
                        {
                            gameMode = "surge ordered ";
                            operation = "reorder " + iterations;
                        }

                        foreach (var enemyDeck in enemyDecks)
                        {
                            // Create a navigator sim
                            Sim sim = new Sim
                            {
                                //Id = Helper.GetNewId(),
                                Description = "NAVIGATOR",
                                Mode = SimMode.NAVIGATOR,

                                // Usually tuo
                                TuoExecutable = "tuo",
                                UseX86 = false,

                                // Decks
                                MyDeck = new Deck(myDeck),
                                EnemyDeck = enemyDeck,

                                // Towers
                                // MyDominion = form.navigatorPlayerDominionTextBox.Text,
                                MyForts = form.navSimMyFortComboBox.Text,
                                EnemyForts = form.navSimEnemyFortComboBox.Text,

                                // Bges
                                Bge = form.navSimBgeComboBox.Text,
                                MyBge = form.navSimYourBgeComboBox.Text,
                                EnemyBge = form.navSimEnemyBgeComboBox.Text,

                                // GameMode / Operation - Flexible
                                GameMode = gameMode,
                                Operation = operation,
                                Iterations = iterations,
                            };

                            batchSim.Sims.Add(sim);
                        }//enemyDecks

                        // Add compiled result to the master list of batchsims                        
                        NewSimManager.BatchSims.Add(batchSim);

                        // Output to the navsim textbox
                        form.navigatorQueuedTextBox.AppendText(BatchSim.BatchSimToString(batchSim) + "\r\n");
                    }
                }
                else
                {
                    form.outputTextBox.Text = "Could not find decks in the enemy gauntlet\r\n";
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(form, "Navigator Sim error: " + ex);
            }
        }
        
        /// <summary>
        /// Run Navigator type sims
        /// </summary>
        public static void RunNavigatorSims(MainForm form, List<BatchSim> batchSims)
        {
            if (batchSims.Count == 0) return;

            // Start the progress bar in navigator
            int totalSims = batchSims.Sum(x => x.Sims.Count);
            ControlExtensions.InvokeEx(form.navigatorProgressBar, x => form.navigatorProgressBar.Maximum = totalSims);
            ControlExtensions.InvokeEx(form.navigatorProgressBar, x => form.navigatorProgressBar.Value = 0);
            ControlExtensions.InvokeEx(form.navigatorProgressBar, x => form.navigatorProgressBar.Step = 1);

            try
            {
                foreach (var batchSim in batchSims)
                {
                    // Raw output of one sim
                    var simOutput = new List<string>();

                    // List of strings to sim
                    List<string> simStrings = new List<string>();
                    foreach (Sim sim in batchSim.Sims)
                    {
                        simStrings.Add(sim.SimToString());
                    }

                    // Sim results
                    List<SimResult> simResults = new List<SimResult>();
                    // Run each batch
                    foreach (var simString in simStrings)
                    {
                        if (form.stopProcess)
                        {
                            ControlExtensions.InvokeEx(form.navigatorOutputTextBox, x => form.navigatorOutputTextBox.AppendText("-- Queued sims stopped --\r\n\r\n"));
                            break;
                        }

                        try
                        {
                            // Timeout of a single TUO run
                            var timeout = 30000;

                            // Create a process to run TUO
                            var process = new Process();
                            var processStartInfo = new ProcessStartInfo
                            {
                                FileName = commandLine,
                                Arguments = "/c " + simString + "",
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                RedirectStandardOutput = true,
                            };
                            process.StartInfo = processStartInfo;


                            // Capture TUO console window output to simResult
                            process.OutputDataReceived += new DataReceivedEventHandler
                            (
                                // append the new data to the data already read-in
                                delegate (object sender, DataReceivedEventArgs e)
                                {
                                    if (e.Data != null) simOutput.Add(e.Data);
                                }
                            );

                            // Run the process until waitTime (ms)
                            process.Start();
                            process.BeginOutputReadLine();
                            var processId = process.Id;
                            process.WaitForExit(timeout);

                            KillProcessAndChildren(processId);

                            // Handle the result of this sim
                            SimResult simResult = SimManager.RunQueuedSims_GetResult(form, form.navigatorOutputTextBox, simString, simOutput, false, false);
                            simResults.Add(simResult);

                            ControlExtensions.InvokeEx(form.navigatorProgressBar, x => form.navigatorProgressBar.PerformStep());
                        }
                        catch (Exception ex)
                        {
                            ControlExtensions.InvokeEx(form.navigatorOutputTextBox, x => form.navigatorOutputTextBox.AppendText("Error on RunNavigatorSims(): " + ex + "\r\n\r\n"));
                        }
                    }//simStrings

                    //Report on the last run sims
                    SimManager.RunQueuedSims_ProcessResults(form, form.navigatorOutputTextBox, simResults, navigatorMetrics:true, navigatorDetail:form.navSimShowDetailsCheckBox.Checked);

                }//batchSim

            }
            catch (Exception ex)
            {
                ControlExtensions.InvokeEx(form.navigatorOutputTextBox, x => form.navigatorOutputTextBox.AppendText("Error on RunNavigatorSims(): " + ex + "\r\n\r\n"));
                ControlExtensions.InvokeEx(form.navigatorProgressBar, x => form.navigatorProgressBar.Value = 0);
            }

            ControlExtensions.InvokeEx(form.navigatorProgressBar, x => form.navigatorProgressBar.Value = 0);
        }

        #endregion
        

        #region Helpers

        /// <summary>
        /// Given an id, return the BatchSim
        /// </summary>
        public static BatchSim FindBatchSimById(int id)
        {
            var batchSim = BatchSims.Where(x => x.Id == id).FirstOrDefault();
            return batchSim;
        }

        /// <summary>
        /// Given a line of text, return the BatchSim
        /// </summary>
        public static BatchSim FindBatchSimByString(string line)
        {
            // Found a match
            if (line.StartsWith("BatchSim:"))
            {
                var batchSimSplit = line.Split(':');
                if (batchSimSplit.Length >= 2)
                {
                    int.TryParse(batchSimSplit[1], out int batchSimId);
                    if (batchSimId > 0)
                    {
                        return FindBatchSimById(batchSimId);
                    }
                }
            }

            return null;
        }
        
        /// <summary>
        /// Run a sim async, and return a simresult
        /// </summary>        
        public static void RunSim(Sim sim)
        {
            // TUO Console output
            List<string> consoleOutput = new List<string>();

            // ****
            // TODO: Rerun this on a fail up to 2 or 3 times
            // ****
            
            //int numberOfRetries = 3;
            //for(int i=0; i<numberOfRetries; i++)
            //{
            //  if (success) break;
            //}

            try
            {
                // Create a process to run TUO
                var process = new Process();
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = commandLine,
                    Arguments = "/c " + sim.SimToString() + "",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                process.StartInfo = processStartInfo;


                // Capture TUO console window output to simResult
                process.OutputDataReceived += new DataReceivedEventHandler
                (
                    delegate (object sender, DataReceivedEventArgs e)
                    {
                        // append the new data to the data already read-in
                        if (e.Data != null)
                            consoleOutput.Add(e.Data);
                    }
                );


                process.ErrorDataReceived += (sender, errorLine) => {
                    if (errorLine.Data != null)
                    {
                        Trace.WriteLine(errorLine.Data);
                        Console.WriteLine(errorLine.Data);
                    }
                };


                // Run the process until waitTime (ms)
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                var processId = process.Id;


                process.WaitForExit(sim.Timeout);
                //process.WaitForExit(10000);
                //process.WaitForInputIdle(10000);

                //KillProcessAndChildren(processId);


                // -- Get sim result --

                // Is this a sim or climb/reorder
                if (consoleOutput.Count > 0)
                {
                    // Sim output
                    if (consoleOutput[0].StartsWith("win%"))
                    {
                        string winPercentLine = consoleOutput[0].Split(' ')[1];

                        // Get win / deck result line
                        double.TryParse(winPercentLine, out double d);
                        sim.WinPercent = d;
                        sim.ResultDeck = sim.MyDeck.DeckToString();
                    }
                    // Climb/reorder output
                    else
                    {
                        NewSimManager.ParseSimResult(sim, consoleOutput);
                    }
                }
                else
                {
                    sim.StatusMessage += "This sim did not run (Timeout?): " + sim.SimToString() + "\r\n";
                }
            }
            catch (Exception ex)
            {
                sim.StatusMessage = "Error running sim: " + ex;
            }
        }

        /// <summary>
        /// Parses command line output for the first "win line" to extract values
        /// Returns false if we don't want to output the result
        /// </summary>
        public static bool ParseSimResult(Sim sim, List<string> simOutput)
        {
            // Looks for most recent line that contains "units:" - this is the latest line with a win score
            simOutput.Reverse();

            // Climb - Surge
            // Optimized Deck: 10 units: 71.1864: < deck >
            // 10 units: 61.0169: < deck >

            // War - Surge
            // 10 units: (75.4237 % win) 129.492[165.169 per win]: < deck >
            // Optimized Deck: 10 units: (75.4237 % win) 129.492[165.169 per win]: < deck >

            // Brawl - Surge
            // 10 units: (77.1186 % win) 49.7458[63.022 per win]: < deck >
            // 
            // Anneal
            // Deck improved: HW4bDRhHSXhJOYhBZMhJOYhFHQhbRQhbRQhJOYhHSXhPUXh: (temp=22.9068) :10 units: (76.0678% win) 47.8919 [61.3865 per win]: <deck>

            double d;

            foreach (var line in simOutput)
            {
                string resultLine = line;
                if (resultLine != null && resultLine.Contains("units:"))
                {
                    string[] resultSplit = resultLine.Split(':');
                    if (resultSplit.Length >= 2)
                    {
                        // Decklist: Get, and add -1 to quad commanders
                        sim.ResultDeck = resultSplit[resultSplit.Length - 1].Trim();
                        //sim.ResultDeck = TextCleaner.DeckHyphenQuadCommanders(sim.ResultDeck);
                        
                        string initialWinline = resultSplit[resultSplit.Length - 2].Trim();
                        
                        // Simple win line - Surge/Def/Pvp
                        if (double.TryParse(initialWinline, out d))
                        {
                            sim.WinPercent = d;
                        }
                        // Complex sim line
                        else 
                        {
                            string[] splitWinLine = initialWinline.Split(new string[] { "(", "% win)", "[", "]" }, StringSplitOptions.RemoveEmptyEntries);
                            if (splitWinLine.Length >= 1)
                            {
                                double.TryParse(splitWinLine[0], out d);
                                sim.WinPercent = Math.Round(d, 1);
                            }
                            if (splitWinLine.Length >= 2)
                            {
                                double.TryParse(splitWinLine[1], out d);
                                sim.WinScore = Math.Round(d, 0);
                            }
                            if (splitWinLine.Length >= 3)
                            {
                                double.TryParse(splitWinLine[2], out d);
                                sim.ScorePerWin = Math.Round(d, 0);
                            }
                        }
                    }

                    return true;
                }
                

            }

            return false;
        }
        
        /// <summary>
        /// Kill a specific TUO process
        /// https://stackoverflow.com/questions/30249873/process-kill-doesnt-seem-to-kill-the-process
        /// </summary>
        public static void KillProcessAndChildren(int pid)
        {
            var processSearcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid);
            var processCollection = processSearcher.Get();

            try
            {
                var proc = GetProcByID(pid);
                if (proc != null && !proc.HasExited) proc.Kill();
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine(ex);
            }

            try
            {
                if (processCollection != null && processCollection.Count > 0)
                {
                    foreach (ManagementObject mo in processCollection)
                    {
                        KillProcessAndChildren(Convert.ToInt32(mo["ProcessID"])); //kill child processes(also kills childrens of childrens etc.)
                    }
                }
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine(ex);
            }
        }

        public static Process GetProcByID(int id)
        {
            Process[] processlist = Process.GetProcesses();
            return processlist.FirstOrDefault(pr => pr.Id == id);
        }

        #endregion 

    }
}