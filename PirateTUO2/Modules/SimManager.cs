using PirateTUO2.Classes;
using PirateTUO2.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PirateTUO2.Modules
{
    /// <summary>
    /// Manages sims
    /// </summary>
    public static class SimManager
    {
        /// <summary>
        /// Build a sim command line string
        /// 
        /// TODO: Return multiple sims for a wide seed
        /// </summary>
        public static List<string> BuildSimString(MainForm form, string mode, BattleData battleData = null, bool overrideOperationWithReorder=false)
        {
            var result = new List<string>();
            Sim sim;

            // for spamsims
            var spamSims = new List<string>();
            var dominions = new List<string> { "Broodmother's Nexus", "Alpha Replicant" };

            try
            {
                switch(mode)
                {
                    case "basicSimTab1":
                        sim = BuildPlayerSim(form, 1, overrideOperationWithReorder);
                        if (!string.IsNullOrWhiteSpace(sim.StatusMessage))
                        {
                            result.Add(sim.StatusMessage);
                        }
                        else
                        {
                            result.Add(sim.SimToString(replaceMyDeckIfGenetic: false));
                        }
                        break;
                    case "basicSimTab2":
                        sim = BuildPlayerSim(form, 2, overrideOperationWithReorder);
                        if (!string.IsNullOrWhiteSpace(sim.StatusMessage))
                        {
                            result.Add(sim.StatusMessage);
                        }
                        else
                        {
                            result.Add(sim.SimToString(replaceMyDeckIfGenetic: false));
                        }
                        break;
                    case "basicSimTab3":
                        sim = BuildPlayerSim(form, 3, overrideOperationWithReorder);
                        if (!string.IsNullOrWhiteSpace(sim.StatusMessage))
                        {
                            result.Add(sim.StatusMessage);
                        }
                        else
                        {
                            result.Add(sim.SimToString(replaceMyDeckIfGenetic: false));
                        }
                        break;
                    case "batchSimTab":
                        
                        // Loop: Sim each selected player, seed, and dominion (if a list is used)
                        foreach (var player in form.batchSimInventoryListBox.SelectedItems)
                        {
                            if (player.ToString() == "none") continue;

                            
                            foreach (var seed in form.batchSimSeedListBox.SelectedItems)
                            {
                                // One or more Dominions was selected
                                if (form.batchSimDominionListBox.SelectedItems.Count > 0)
                                {
                                    foreach (var dominion in form.batchSimDominionListBox.SelectedItems)
                                    {
                                        var selectedDominion = dominion.ToString();
                                        sim = BuildBatchSim(form, player.ToString(), seed.ToString(), dominion: selectedDominion);
                                        if (!string.IsNullOrWhiteSpace(sim.StatusMessage))
                                        {
                                            result.Add(sim.StatusMessage);
                                        }
                                        else
                                        {
                                            result.Add(sim.SimToString());
                                        }
                                    }
                                }
                                // Use Player dominion
                                else
                                {
                                    sim = BuildBatchSim(form, player.ToString(), seed.ToString());
                                    if (!string.IsNullOrWhiteSpace(sim.StatusMessage))
                                    {
                                        result.Add(sim.StatusMessage);
                                    }
                                    else
                                    {
                                        result.Add(sim.SimToString());
                                    }
                                }
                                
                            }
                        }
                        break;

                    // Special batch sim tab modes
                    //case "pvpFarming":
                    //    // Loop: Sim each selected player and seed
                    //    foreach (var player in form.batchSimInventoryListBox.SelectedItems)
                    //    {
                    //        if (player.ToString() == "none") continue;

                    //        // Use the PvP seed, and VIP out fast cards
                    //        var fastCardList = CardManager.GetAggressiveCards().Select(x => x.Name).ToList();
                    //        var fastCards = "";

                    //        foreach (var card in fastCardList)
                    //        {
                    //            fastCards += card + ",";
                    //        }
                    //        fastCards = fastCards.TrimEnd(',');

                    //        sim = BuildBatchSim(form, player.ToString(), "PvpHunt", "pvp", "vip \"" + fastCards + "\"");
                    //        result.Add(sim.SimToString());
                    //    }
                    //    break;

                    // ----------------------------------------------
                    // Create a bunch of batches for a "spam" sim
                    // ----------------------------------------------
                    // One card
                    case "spamScoreF2p":
                        spamSims = BuildSpamSims(form, dominions, cardPower: 0,  f2p: true);
                        result.AddRange(spamSims);
                        break;
                    case "spamScoreWhale":
                        spamSims = BuildSpamSims(form, dominions);
                        result.AddRange(spamSims);
                        break;
                    case "spamScoreCommon":
                        spamSims = BuildSpamSims(form, dominions, freeVindMax:0, chanceLegendMax:0);
                        result.AddRange(spamSims);
                        break;
                    // Two cards              
                    case "spamScore2F2p":
                        spamSims = BuildSpamSims(form, dominions, numberOfCards: 3, cardPower: 2, freeVindMax: 1, f2p: true);
                        result.AddRange(spamSims);
                        break;
                    case "spamScore2Whale":
                        spamSims = BuildSpamSims(form, dominions, numberOfCards: 2, cardPower: 7, chanceLegendMax:1);
                        result.AddRange(spamSims);
                        break;
                    case "spamScore2Common":
                        spamSims = BuildSpamSims(form, dominions, numberOfCards: 2, cardPower: 6, chanceLegendMax: 0, mythicMax: 0);
                        result.AddRange(spamSims);
                        break;
                    // Three cards
                    case "spamScore3F2p":
                        spamSims = BuildSpamSims(form, dominions, numberOfCards: 3, cardPower: 2, freeVindMax: 1, f2p: true);
                        result.AddRange(spamSims);
                        break;
                    case "spamScore3Whale":
                        spamSims = BuildSpamSims(form, dominions, numberOfCards: 3, cardPower: 7, chanceLegendMax: 1);
                        result.AddRange(spamSims);
                        break;
                    case "spamScore3Common":
                        spamSims = BuildSpamSims(form, dominions, numberOfCards: 3, cardPower: 6, chanceLegendMax: 0, mythicMax: 0);
                        result.AddRange(spamSims);
                        break;
                    default:
                        break;

                }

                
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(form, "BuildSimString(): Error on input: " + ex);
            }
            return result;
        }

        /// <summary>
        /// Launch a sim outside of this app
        /// </summary>
        public static bool RunOneSim(MainForm form, List<string> simStrings)
        {
            try
            {
                // cmd.exe or powershell
                var commandLine = "cmd.exe";
                
                var simString = simStrings[0];

                // Run TUO
                var process = new Process();
                var psi = process.StartInfo;
                psi.FileName = commandLine;
                psi.Arguments = "/c " + simString + "";

                // Run in command window
                form.outputTextBox.AppendText( "\r\n-- Run one sim -- \r\n" + simString + "\r\n");

                psi.Arguments += " & pause";
                process.Start();
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(form, "RunSimStrings(): Error on sim: " + ex);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Launch all queued sims
        /// 
        /// TODO: Sim string needs to be a sim result object. We should store this and do stuff to it later
        /// </summary>
        public static bool RunQueuedSims(MainForm form, List<string> simStrings)
        {
            try
            {
                /// Temp storage for sim results
                 List<SimResult> simResults = new List<SimResult>();

                // cmd.exe or powershell
                var commandLine = "cmd.exe"; 
                
                ControlExtensions.InvokeEx(form.outputTextBox, x => form.outputTextBox.AppendText( "\r\n-- Running " + simStrings.Count + " Sims in background--\r\n"));
                ControlExtensions.InvokeEx(form.outputTextBox, x => form.outputTextBox.AppendText( "-- Run additional queued sims at your peril, pirate! --\r\n"));

                ControlExtensions.InvokeEx(form.progressBar, x => form.progressBar.Maximum = simStrings.Count);
                ControlExtensions.InvokeEx(form.progressBar, x => form.progressBar.Value = 0);
                ControlExtensions.InvokeEx(form.progressBar, x => form.progressBar.Step = 1);


                foreach (var simString in simStrings)
                {
                    if (form.stopProcess)
                    {
                        ControlExtensions.InvokeEx(form.outputTextBox, x => form.outputTextBox.AppendText( "-- Queued sims stopped --\r\n\r\n"));
                        break;
                    }

                    try
                    {
                        // Raw output of one sim
                        var simOutput = new List<string>();

                        // Timeout of a single TUO run
                        var timeout = form.gameTimeoutTextBox.Text;
                        var waitTime = !String.IsNullOrEmpty(timeout) ? Int32.Parse(timeout) * 1000 : 3600000; //1 hour default timeout


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
                            delegate (object sender, DataReceivedEventArgs e)
                            {
                                // append the new data to the data already read-in
                                if (e.Data != null)
                                    simOutput.Add(e.Data);
                            }
                        );


                        // Run the process until waitTime (ms)
                        process.Start();
                        process.BeginOutputReadLine();
                        var processId = process.Id;
                        process.WaitForExit(waitTime);

                        KillProcessAndChildren(processId);

                        // Handle the result of this sim
                        SimResult simResult = RunQueuedSims_GetResult(form, form.batchSimOutputTextBox, simString, simOutput);
                        simResults.Add(simResult);


                        ControlExtensions.InvokeEx(form.progressBar, x => form.progressBar.PerformStep());
                        
                    }
                    catch(Exception ex)
                    {
                        ControlExtensions.InvokeEx(form.outputTextBox, x => form.outputTextBox.AppendText( "Error on RunQueuedSims(): " + ex + "\r\n\r\n"));
                        ControlExtensions.InvokeEx(form.progressBar, x => form.progressBar.PerformStep());
                        //ControlExtensions.InvokeEx(form.progressBar, x => form.progressBar.Value = 0);
                    }
                }//simStrings


                //Report on the last run sims
                RunQueuedSims_ProcessResults(form, form.batchSimOutputTextBox, simResults);

            }
            catch (Exception ex)
            {
                ControlExtensions.InvokeEx(form.outputTextBox, x => form.outputTextBox.AppendText( "RunSimStrings(): Error on sim: " + ex));
                ControlExtensions.InvokeEx(form.progressBar, x => form.progressBar.Value = 0);
                return false;
            }

            ControlExtensions.InvokeEx(form.progressBar, x => form.progressBar.Value = 0);
            return true;
        }


        #region Helpers - Build a Sim line

        /// <summary>
        /// Build a sim for a single player
        /// </summary>
        private static Sim BuildPlayerSim(MainForm form, int i, bool overrideOperationWithReorder=false)
        {
            try
            {
                // If x86 is checked, 32 bit mode
                bool useX86 = Helper.GetCheckBox(form, "gameCPUx86CheckBox" + i).Checked;

                bool usePossibleCards = Helper.GetCheckBox(form, "inventoryPossibleCardsCheckBox" + i).Checked;
                List<string> selectedInventories = Helper.GetListBox(form, "inventoryListBox" + i).SelectedItems.Cast<string>().ToList();
                List<string> selectedCardAddons = Helper.GetListBox(form, "inventoryCardAddonsListBox" + i).SelectedItems.Cast<string>().ToList();
                string ownedCards = GetOwnedCards(usePossibleCards, selectedInventories, selectedCardAddons);

                // Cleanup mydeck and enemydeck of newlines and replace tabs with commas
                form.myDeckTextBox1.Text = TextCleaner.RemoveTabSpaces(form.myDeckTextBox1.Text);
                form.myDeckTextBox2.Text = TextCleaner.RemoveTabSpaces(form.myDeckTextBox2.Text);
                form.myDeckTextBox3.Text = TextCleaner.RemoveTabSpaces(form.myDeckTextBox3.Text);
                form.enemyDeckTextBox1.Text = TextCleaner.RemoveTabSpaces(form.enemyDeckTextBox1.Text);
                form.enemyDeckTextBox2.Text = TextCleaner.RemoveTabSpaces(form.enemyDeckTextBox2.Text);
                form.enemyDeckTextBox3.Text = TextCleaner.RemoveTabSpaces(form.enemyDeckTextBox3.Text);

                Deck myDeck = new Deck(Helper.GetControlText(form, "myDeckTextBox" + i));
                int.TryParse(Helper.GetControlText(form, "gameIterationsComboBox" + i), out int iterations);
                if (iterations <= 0) iterations = 100;

                // * If doing sim dominion, add the card-addon instead of the sim dominion flag, as custom dominions bleed into this
                if (Helper.GetCheckBox(form, "gameSimDominionCheckBox" + i).Checked)
                {
                    ownedCards +=  "-o=\"" + CONSTANTS.PATH_CARDADDONS + "x-Dominions.txt\" ";
                }

                Sim sim = new Sim
                {
                    // Usually tuo
                    TuoExecutable = !(Helper.GetCheckBox(form, "gameTUODebugCheckBox" + i)).Checked ? "tuo" : "tuo-debug",
                    UseX86 = useX86,

                    // Decks
                    MyDeck = new Deck(Helper.GetControlText(form, "myDeckTextBox" + i)),
                    EnemyDeck = Helper.GetControlText(form, "enemyDeckTextBox" + i),

                    MyDominion = Helper.GetControlText(form, "myDominionComboBox" + i).Replace("\r\n", "").Replace("\n", ""),

                    // Forts and BGEs
                    MyForts = Helper.GetControlText(form, "myFortComboBox" + i),
                    EnemyForts = Helper.GetControlText(form, "enemyFortComboBox" + i),
                    Bge = Helper.GetControlText(form, "bgeComboBox" + i),
                    MyBge = Helper.GetControlText(form, "yourBgeComboBox" + i),
                    EnemyBge = Helper.GetControlText(form, "enemyBgeComboBox" + i),

                    // GameMode / Operation
                    GameMode = GetGameMode(form, Helper.GetControlText(form, "gameModeComboBox" + i), iterations),
                    Operation = Helper.GetControlText(form, "gameOperationComboBox" + i),
                    Iterations = iterations,

                    // Gauntlets and inventory
                    GauntletFile = "_" + Helper.GetControlText(form, "gauntletCustomDecksComboBox" + i),
                    OwnedCards = ownedCards,

                    // Deck Limit
                    DeckLimitLow = Int32.TryParse(Helper.GetControlText(form, "gameDeckLimitLowTextBox" + i), out int x) ? x : -1,
                    DeckLimitHigh = Int32.TryParse(Helper.GetControlText(form, "gameDeckLimitHighTextBox" + i), out x) ? x : -1,

                    // Other flags
                    CpuThreads = Int32.TryParse(Helper.GetControlText(form, "gameCPUThreadsTextBox" + i), out x) ? x : 4,
                    Verbose = Helper.GetCheckBox(form, "gameTUODebugCheckBox" + i).Checked,
                    HarmonicMean = Helper.GetCheckBox(form, "gameHarmonicMeanCheckBox" + i).Checked,
                    SimDominion = Helper.GetCheckBox(form, "gameSimDominionCheckBox" + i).Checked,
                    MinIncrementScore = Double.TryParse(Helper.GetControlText(form, "gameMISTextBox" + i), out double x2) ? x2 : 0,
                    Fund = Int32.TryParse(Helper.GetControlText(form, "gameFundTextBox" + i), out x) ? x : 0,
                    ExtraTuoFlags = Helper.GetControlText(form, "gameExtraFlagsTextBox" + i),

                    //Freeze
                    //Hand
                    //Enemy:hand
                    //Timeout
                };

                // Operation: when forcing the sim to reorder regardless of input
                if (overrideOperationWithReorder)
                {
                    sim.Operation = "reorder " + sim.Iterations.ToString();
                }
                // Operation: climbex - uses climbex X Y. Use iterations / 10 
                else if (sim.Operation == "climbex")
                {
                    sim.Operation += " " + (sim.Iterations / 10) + " " + sim.Iterations;
                }
                // Operation: anneal - uses anneal X Y Z (default 500 100 0.001)
                else if (sim.Operation == "anneal")
                {
                    sim.Operation += " " + sim.Iterations + " " + (sim.Iterations / 5) + " " + "0.001";
                }
                // Operation: genetic - add _genetic to include customdecks_genetic.txt
                else if (sim.Operation == "genetic")
                {
                    sim.Operation += " " + sim.Iterations.ToString();
                    sim.ExtraTuoFlags += " _genetic";
                }
                else
                {
                    sim.Operation += " " + sim.Iterations.ToString();
                }


                // Fill deck to match deck limit (low or high)
                if (sim.DeckLimitLow > 0 || sim.DeckLimitHigh > 0)
                {
                    myDeck.FillDeck(sim.DeckLimitLow, sim.DeckLimitHigh);
                }

                return sim;
            }
            catch(Exception ex)
            {
                return new Sim { StatusMessage = "Syntax error in BuildPlayerSim(): " + ex };
            }
        }

        /// <summary>
        /// Build a list of sims for batch
        /// </summary>
        private static Sim BuildBatchSim(MainForm form, string playerFile, string seed, string dominion="", string gameMode = "", string extraFlags="")
        {
            try
            {
                var myDeckStr = "";
                var playerName = playerFile.Remove(0, 3).Replace(".txt", "");

                // Find player object
                var player = PlayerManager.Players.Where(p => p.KongName.ToLower() == playerName.ToLower()).FirstOrDefault();

                // Get player inventory
                bool usePossibleCards = form.batchSimPossibleCardsCheckBox.Checked;
                List<string> selectedAddons = form.batchSimInventoryCardAddonsListBox.SelectedItems.Cast<String>().ToList();
                string ownedCards = GetOwnedCards(usePossibleCards, new List<string> { playerFile }, selectedAddons);
                int x = 0;
                double d = 0.0;
                bool useX86 = form.batchSimUseX86CheckBox.Checked;

                string seedDeck = "";
                if (PlayerManager.SetPlayerDeckFromSeed(form, playerFile, seed, out seedDeck))
                {
                    myDeckStr = seedDeck.Trim();
                }
                else
                {
                    // remove the guild and .txt
                    if (player != null)
                    {
                        int pow = 0;
                        List<Card> powerDeck = SeedManager.GetPowerDeck(player.Cards, ref pow);

                        myDeckStr = string.Join(",", powerDeck.Select(card => card.Name).ToList());
                    }
                }

                Deck myDeck = new Deck(myDeckStr);

                // Sim iterations
                int iterations = Int32.TryParse(form.batchSimIterationsComboBox.Text, out x) ? x : 100;

                var sim = new Sim
                {
                    // 32 bit mode when needed
                    UseX86 = useX86,

                    // Decks
                    MyDeck = myDeck,
                    EnemyDeck = form.batchSimGauntletListBox.SelectedItems.Count > 0 ? form.batchSimGauntletListBox.SelectedItems[0].ToString() : "",

                    MyDominion = dominion,

                    // Forts and BGEs
                    MyForts = form.batchSimMyFortComboBox.Text,
                    EnemyForts = form.batchSimEnemyFortComboBox.Text,
                    Bge = form.batchSimBgeComboBox.Text,
                    MyBge = form.batchSimYourBgeComboBox.Text,
                    EnemyBge = form.batchSimEnemyBgeComboBox.Text,

                    // GameMode / Operation
                    GameMode = String.IsNullOrEmpty(gameMode) ? GetGameMode(form, form.batchSimGameModeComboBox.Text, iterations) : gameMode,
                    Operation = form.batchSimGameOperationComboBox.Text,
                    Iterations = iterations,

                    // Gauntlets and inventory
                    GauntletFile = "_" + form.batchSimCustomDecksComboBox.Text,
                    OwnedCards = ownedCards,

                    // Deck Limit
                    DeckLimitLow = Int32.TryParse(form.batchSimDeckSizeLowTextBox.Text, out x) ? x : 0,
                    DeckLimitHigh = Int32.TryParse(form.batchSimDeckSizeHighTextBox.Text, out x) ? x : 0,

                    // Other flags
                    CpuThreads = Int32.TryParse(form.batchSimCPUThreadsComboBox.Text, out x) ? x : 4,
                    Verbose = false,
                    HarmonicMean = false,
                    MinIncrementScore = Double.TryParse(form.batchSimMisTextBox.Text, out d) ? d : 0.0,
                    Fund = Int32.TryParse(form.batchSimFundTextBox.Text, out x) ? x : 0,
                    ExtraTuoFlags = form.batchSimExtraFlagsTextBox.Text + " " + extraFlags

                    //Freeze
                    //Hand
                    //Enemy:hand
                    //Timeout
                };

                // If no dominion selected, use player dominion
                if (sim.MyDominion == "" && form.batchSimUsePlayerDominionCheckBox.Checked && player != null)
                {
                    if (player.DominionCards.Count >= 2)
                        sim.MyDominion = player.DominionCards[1].Name != null ? player.DominionCards[1].Name : "";
                    else if (player.DominionCards.Count >= 1)
                        sim.MyDominion = player.DominionCards[0].Name != null ? player.DominionCards[0].Name : "";
                }

                // Enemy deck override: Use batchSimPvEGauntletTextBox (raid/mutant deck) instead of gauntlet listbox
                if (!String.IsNullOrEmpty(form.batchSimPvEGauntletTextBox.Text))
                {
                    sim.EnemyDeck = form.batchSimPvEGauntletTextBox.Text;
                }

                // Operation: climbex - uses climbex X Y. Use iterations / 10 
                if (sim.Operation == "climbex")
                {
                    sim.Operation += " " + (sim.Iterations / 10) + " " + sim.Iterations;
                }
                // Operation: anneal - uses anneal X Y Z (default 500 100 0.001)
                else if (sim.Operation == "anneal")
                {
                    sim.Operation += " " + sim.Iterations + " " + (sim.Iterations / 5) + " " + "0.001";
                }
                // Operation: genetic - add _genetic to include customdecks_genetic.txt
                // PlayerDeck: replace with "KongName-Genetic"
                else if (sim.Operation == "genetic")
                {
                    sim.MyDeck = new Deck();
                    sim.ExtraTuoFlags += " _genetic";
                    sim.Operation += " " + sim.Iterations.ToString();
                }
                else
                {
                    sim.Operation += " " + sim.Iterations.ToString();
                }

                // Fill deck to match deck limit (low or high)
                if (sim.DeckLimitLow > 0 || sim.DeckLimitHigh > 0)
                {
                    myDeck.FillDeck(sim.DeckLimitLow, sim.DeckLimitHigh);
                }

                return sim;
            }
            catch(Exception ex)
            {
                return new Sim { StatusMessage = "Syntax error in PlayerSimString(): " + ex };
            }
        }

        /// <summary>
        /// When doing a spam score test - we need a list of possible cards (reduced), then to combine those cards into 1, 2, or 3 card spam decks.
        /// Return a list of sim lines
        /// </summary>
        private static List<string> BuildSpamSims(MainForm form, List<string> dominions, int numberOfCards=1, int cardPower=5, int freeVindMax = -1, int chanceLegendMax = -1, int vindMax = -1, int mythicMax = -1, bool f2p=false)
        {
            var result = new List<string>();
            var spamCards = new List<Card>();

            // Get a list of recent quads
            if (f2p)
                spamCards = CardManager.GetSpamscoreCardsF2p(cardPower);
            else
                spamCards = CardManager.GetSpamscoreCards(cardPower);

            var spamSimTemplate = BuildSpamSim(form).SimToString();


            // Loop: For each dominion/card, make a sim
            foreach (var dominion in dominions)
            {

                // ---------------------
                // Handle one card
                // ---------------------
                if (numberOfCards == 1)
                {
                    for (var i = 0; i < spamCards.Count; i++)
                    {
                        var cardOne = spamCards[i];

                        // Don't sim a structure
                        if (cardOne.CardType == "Structure") continue;

                        if (freeVindMax >= 0 && cardOne.Set == 2500 && cardOne.Rarity == 5) continue;
                        if (freeVindMax >= 0 && cardOne.Subset == CardSubset.PvP_Reward.ToString() && cardOne.Rarity == 5) continue;
                        if (mythicMax >= 0 && cardOne.Subset == CardSubset.Box.ToString() && cardOne.Rarity == 6) continue;
                        if (vindMax >= 0 && cardOne.Subset == CardSubset.Box.ToString() && cardOne.Rarity == 5) continue;
                        if (chanceLegendMax >= 0 && cardOne.Subset == CardSubset.Chance.ToString() && cardOne.Rarity >= 4) continue;

                        // Create the deck
                        var commander = SpamSim_PickCommander(new List<Card> { cardOne });
                        var deck = dominion + "," + commander + "," + spamCards[i].Name + "#8";

                        // Merge the deck with the sim template, and add that to result
                        var simLine = spamSimTemplate.Replace("REPLACEME", deck);
                        result.Add(simLine);
                    }
                }
                else if (numberOfCards == 2)
                {
                    for (var i = 0; i < spamCards.Count; i++)
                    {
                        for (var j = i + 1; j < spamCards.Count; j++)
                        {
                            var cardOne = spamCards[i];
                            var cardTwo = spamCards[j];

                            // -------------------------
                            // General card restrictions
                            // -------------------------
                            if (1 == 1)
                            {
                                // Don't sim 2 structures
                                if (cardOne.CardType == "Structure" && cardTwo.CardType == "Structure") continue;

                                // Don't sim a deck of 4-delays
                                if (cardOne.Delay >= 4 && cardTwo.Delay >= 4) continue;

                                if (cardOne.Name != "Loathe Abysswing" && cardTwo.Name != "Loathe Abysswing")
                                {
                                    if (cardOne.Faction != cardTwo.Faction &&
                                        cardOne.CardType != "Structure" && cardTwo.CardType != "Structure" &&
                                        (
                                            cardOne.s1.id == "Allegiance" || cardOne.s2.id == "Allegiance" || cardOne.s3.id == "Allegiance" ||
                                            cardTwo.s1.id == "Allegiance" || cardTwo.s2.id == "Allegiance" || cardTwo.s3.id == "Allegiance" ||
                                            cardOne.s1.id == "Stasis" || cardOne.s2.id == "Stasis" || cardOne.s3.id == "Stasis" ||
                                            cardTwo.s1.id == "Stasis" || cardTwo.s2.id == "Stasis" || cardTwo.s3.id == "Stasis" ||
                                            cardOne.s1.id == "Legion" || cardOne.s2.id == "Legion" || cardOne.s3.id == "Legion" ||
                                            cardTwo.s1.id == "Legion" || cardTwo.s2.id == "Legion" || cardTwo.s3.id == "Legion"
                                        )) continue;
                                }

                                // Don't sim a card with Skill All X Y with a nonProgen card not in its faction (Rally all Xeno 10 with an Imperial)
                                if (cardOne.Faction != cardTwo.Faction &&
                                    cardOne.Faction != 6 && cardTwo.Faction != 6 &&
                                    cardOne.CardType != "Structure" && cardTwo.CardType != "Structure" &&
                                (
                                    (cardOne.s1.y > 0 && cardOne.s1.all) || (cardOne.s2.y > 0 && cardOne.s2.all) || (cardOne.s3.y > 0 && cardOne.s3.all) ||
                                    (cardTwo.s1.y > 0 && cardTwo.s1.all) || (cardTwo.s2.y > 0 && cardTwo.s2.all) || (cardTwo.s3.y > 0 && cardTwo.s3.all)
                                )) continue;

                                // Don't sim a card with Coalition with a card in its faction
                                if (cardOne.Faction == cardTwo.Faction &&
                                    (
                                        cardOne.s1.id == "Coalition" || cardOne.s2.id == "Coalition" || cardOne.s3.id == "Coalition" ||
                                        cardTwo.s1.id == "Coalition" || cardTwo.s2.id == "Coalition" || cardTwo.s3.id == "Coalition"
                                    )) continue;


                                // Don't sim Evolve structures without the targeted skill
                                if (cardOne.s1.id == "Evolve" && (cardOne.s1.skill1 != cardTwo.s1.id || cardOne.s1.skill1 != cardTwo.s2.id || cardOne.s1.skill1 != cardTwo.s3.id)) continue;
                                if (cardTwo.s1.id == "Evolve" && (cardTwo.s1.skill1 != cardOne.s1.id || cardTwo.s1.skill1 != cardOne.s2.id || cardTwo.s1.skill1 != cardOne.s3.id)) continue;
                            }
                            // -------------------------
                            // Harder card restrictions
                            // -------------------------
                            if (chanceLegendMax >= 0)
                            {
                                // If restricting cards, don't sim chance vinds
                                var chanceLegendCount = 0;

                                if (cardOne.Subset == CardSubset.Chance.ToString() && cardOne.Rarity >= 4) chanceLegendCount++;
                                if (cardTwo.Subset == CardSubset.Chance.ToString() && cardTwo.Rarity >= 4) chanceLegendCount++;

                                if (chanceLegendCount > chanceLegendMax) continue;
                            }
                            if (mythicMax >= 0)
                            {
                                var mythicCount = 0;
                                if (cardOne.Subset == CardSubset.Box.ToString() && cardOne.Rarity >= 6) mythicCount++;
                                if (cardTwo.Subset == CardSubset.Box.ToString() && cardOne.Rarity >= 6) mythicCount++;

                                if (mythicCount > mythicMax) continue;
                            }
                            if (vindMax >= 0)
                            {
                                var vindCount = 0;
                                if (cardOne.Subset == CardSubset.Box.ToString() && cardOne.Rarity == 5) vindCount++;
                                if (cardTwo.Subset == CardSubset.Box.ToString() && cardOne.Rarity == 5) vindCount++;

                                if (vindCount > vindMax) continue;
                            }
                            if (freeVindMax >= 0)
                            {
                                var freeVindCount = 0;
                                if (cardOne.Subset == CardSubset.PvP_Reward.ToString() && cardOne.Rarity == 5) freeVindCount++;
                                if (cardTwo.Subset == CardSubset.PvP_Reward.ToString() && cardTwo.Rarity == 5) freeVindCount++;
                                if (cardTwo.Set == 2000 && cardOne.Rarity == 5) freeVindCount++;
                                if (cardOne.Set == 2500 && cardOne.Rarity == 5) freeVindCount++;
                                if (cardTwo.Set == 2500 && cardOne.Rarity == 5) freeVindCount++;

                                if (freeVindCount > freeVindMax) continue;
                            }

                            // Swap cards on rarity
                            if (cardTwo.Subset == CardSubset.Box.ToString() && cardTwo.Rarity >= 6)
                            {
                                var tmp = cardOne;
                                cardOne = cardTwo;
                                cardTwo = tmp;
                            }
                            if (cardTwo.Subset == CardSubset.Chance.ToString() && cardTwo.Rarity >= 5)
                            {
                                var tmp = cardOne;
                                cardOne = cardTwo;
                                cardTwo = tmp;
                            }

                            // Create the deck
                            var commander = SpamSim_PickCommander(new List<Card> { cardOne, cardTwo });
                            var deck = new StringBuilder();
                            deck.Append(dominion);
                            deck.Append(",");
                            deck.Append(commander);
                            deck.Append(",");

                            // ABABABAB
                            if (cardOne.CardType != "Structure" && cardTwo.CardType != "Structure")
                            {
                                deck.Append(cardOne.Name);
                                deck.Append(",");
                                deck.Append(cardTwo.Name);
                                deck.Append(",");
                                deck.Append(cardOne.Name);
                                deck.Append(",");
                                deck.Append(cardTwo.Name);
                                deck.Append(",");
                                deck.Append(cardOne.Name);
                                deck.Append(",");
                                deck.Append(cardTwo.Name);
                                deck.Append(",");
                                deck.Append(cardOne.Name);
                                deck.Append(",");
                                deck.Append(cardTwo.Name);
                            }
                            // ABAAAAAB
                            else if (cardOne.CardType == "Structure")
                            {
                                deck.Append(cardTwo.Name);
                                deck.Append(",");
                                deck.Append(cardOne.Name);
                                deck.Append(",");
                                deck.Append(cardTwo.Name);
                                deck.Append("#5,");
                                deck.Append(cardOne.Name);
                            }
                            else //if (cardTwo.CardType == "Structure")
                            {
                                deck.Append(cardOne.Name);
                                deck.Append(",");
                                deck.Append(cardTwo.Name);
                                deck.Append(",");
                                deck.Append(cardOne.Name);
                                deck.Append("#5,");
                                deck.Append(cardTwo.Name);

                            }

                            // Merge the deck with the sim template, and add that to result
                            var simLine = spamSimTemplate.Replace("REPLACEME", deck.ToString());
                            result.Add(simLine);
                        }//j
                    }//i
                }
                else if (numberOfCards == 3)
                {
                    for (var i = 0; i < spamCards.Count; i++)
                    {
                        for (var j = i + 1; j < spamCards.Count; j++)
                        {
                            for (var k = j + 1; k < spamCards.Count; k++)
                            {
                                var cardOne = spamCards[i];
                                var cardTwo = spamCards[j];
                                var cardThree = spamCards[k];


                                var legendCount = 0;
                                var vindCount = 0;
                                var mythicCount = 0;
                                var chanceLegendCount = 0;

                                // -------------------------
                                // General card restrictions
                                // -------------------------
                                if (1 == 1)
                                {
                                    // Count structures
                                    var structureCount = 0;
                                    if (cardOne.CardType == "Structure") structureCount++;
                                    if (cardTwo.CardType == "Structure") structureCount++;
                                    if (cardThree.CardType == "Structure") structureCount++;


                                    // Count matching faction units 
                                    var assaultFactionCount = 0; // nonstructure faction match
                                    var assaultSimilarFactionCount = 0; // faction + progen

                                    if (cardOne.Faction == cardTwo.Faction && cardOne.CardType != "Structure" && cardTwo.CardType != "Structure")
                                    {
                                        assaultFactionCount++;
                                    }
                                    if (cardOne.Faction == cardThree.Faction && cardOne.CardType != "Structure" && cardThree.CardType != "Structure")
                                    {
                                        assaultFactionCount++;
                                    }
                                    if (cardTwo.Faction == cardThree.Faction && cardTwo.CardType != "Structure" && cardThree.CardType != "Structure")
                                    {
                                        assaultFactionCount++;
                                    }
                                    if (cardOne.Faction == 6 && cardOne.CardType != "Structure") assaultSimilarFactionCount++;
                                    if (cardTwo.Faction == 6 && cardTwo.CardType != "Structure") assaultSimilarFactionCount++;
                                    if (cardThree.Faction == 6 && cardThree.CardType != "Structure") assaultSimilarFactionCount++;


                                    // -- Filters --

                                    // Don't sim two structures
                                    if (structureCount >= 2) continue;

                                    // Don't sim 2 really slow cards
                                    if (cardOne.Delay >= 4 && cardTwo.Delay >= 4) continue;
                                    if (cardOne.Delay >= 4 && cardThree.Delay >= 4) continue;
                                    if (cardTwo.Delay >= 4 && cardThree.Delay >= 4) continue;

                                    // Don't sim Evolve structures without the targeted skill
                                    if (cardOne.s1.skill1 == "Pierce" && cardTwo.s1.id != "Pierce" && cardTwo.s2.id != "Pierce" && cardTwo.s3.id != "Pierce" && cardThree.s1.id != "Pierce" && cardThree.s2.id != "Pierce" && cardThree.s3.id != "Pierce") continue;
                                    if (cardTwo.s1.skill1 == "Pierce" && cardOne.s1.id != "Pierce" && cardOne.s2.id != "Pierce" && cardOne.s3.id != "Pierce" && cardThree.s1.id != "Pierce" && cardThree.s2.id != "Pierce" && cardThree.s3.id != "Pierce") continue;
                                    if (cardThree.s1.skill1 == "Pierce" && cardOne.s1.id != "Pierce" && cardOne.s2.id != "Pierce" && cardOne.s3.id != "Pierce" && cardTwo.s1.id != "Pierce" && cardTwo.s2.id != "Pierce" && cardTwo.s3.id != "Pierce") continue;

                                    // Don't sim a nonprogen card with <skill> All <faction> X, unless the other cards are in the same faction or progen
                                    if (cardOne.Faction != 6 &&
                                        (cardOne.s1.y > 0 && cardOne.s1.all ||
                                        cardOne.s2.y > 0 && cardOne.s2.all ||
                                        cardOne.s3.y > 0 && cardOne.s3.all))
                                    {
                                        if (cardTwo.Faction != 6 && cardOne.Faction != cardTwo.Faction) continue;
                                        if (cardThree.Faction != 6 && cardOne.Faction != cardThree.Faction) continue;
                                    }
                                    if (cardTwo.Faction != 6 &&
                                        (cardTwo.s1.y > 0 && cardTwo.s1.all ||
                                        cardTwo.s2.y > 0 && cardTwo.s2.all ||
                                        cardTwo.s3.y > 0 && cardTwo.s3.all))
                                    {
                                        if (cardOne.Faction != 6 && cardTwo.Faction != cardOne.Faction) continue;
                                        if (cardThree.Faction != 6 && cardTwo.Faction != cardThree.Faction) continue;
                                    }
                                    if (cardThree.Faction != 6 &&
                                        (cardThree.s1.y > 0 && cardThree.s1.all ||
                                        cardThree.s2.y > 0 && cardThree.s2.all ||
                                        cardThree.s3.y > 0 && cardThree.s3.all))
                                    {
                                        if (cardOne.Faction != 6 && cardThree.Faction != cardOne.Faction) continue;
                                        if (cardTwo.Faction != 6 && cardThree.Faction != cardTwo.Faction) continue;
                                    }

                                    // Don't sim a card with allegiance/stasis/legion with a card that is not in its faction
                                    // Ignore Loathe with Legion
                                    if (cardOne.Name != "Loathe Abysswing" && cardTwo.Name != "Loathe Abysswing" && cardThree.Name != "Loathe Abysswing")
                                    {
                                        if ((cardOne.Faction != cardTwo.Faction || cardOne.Faction != cardThree.Faction || cardTwo.Faction != cardThree.Faction) &&
                                            cardOne.CardType != "Structure" && cardTwo.CardType != "Structure" && cardThree.CardType != "Structure" &&
                                            (
                                                cardOne.s1.id == "Allegiance" || cardOne.s2.id == "Allegiance" || cardOne.s3.id == "Allegiance" ||
                                                cardTwo.s1.id == "Allegiance" || cardTwo.s2.id == "Allegiance" || cardTwo.s3.id == "Allegiance" ||
                                                cardThree.s1.id == "Allegiance" || cardThree.s2.id == "Allegiance" || cardThree.s3.id == "Allegiance" ||
                                                cardOne.s1.id == "Stasis" || cardOne.s2.id == "Stasis" || cardOne.s3.id == "Stasis" ||
                                                cardTwo.s1.id == "Stasis" || cardTwo.s2.id == "Stasis" || cardTwo.s3.id == "Stasis" ||
                                                cardThree.s1.id == "Stasis" || cardThree.s2.id == "Stasis" || cardThree.s3.id == "Stasis" ||
                                                cardOne.s1.id == "Legion" || cardOne.s2.id == "Legion" || cardOne.s3.id == "Legion" ||
                                                cardTwo.s1.id == "Legion" || cardTwo.s2.id == "Legion" || cardTwo.s3.id == "Legion" ||
                                                cardThree.s1.id == "Legion" || cardThree.s2.id == "Legion" || cardThree.s3.id == "Legion"
                                            )) continue;
                                    }
                                    else
                                    {
                                        // Don't sim loathe without an enfeebler
                                        if (cardOne.Name == "Loathe Abysswing" &&
                                                cardTwo.s1.id != "Enfeeble" && cardTwo.s2.id != "Enfeeble" && cardTwo.s3.id != "Enfeeble" &&
                                                cardThree.s1.id != "Enfeeble" && cardThree.s2.id != "Enfeeble" && cardThree.s3.id != "Enfeeble") continue;

                                    }

                                    var coalitionCount = 0;
                                    coalitionCount += (cardOne.s1.id == "Coalition" || cardOne.s2.id == "Coalition" || cardOne.s3.id == "Coalition") ? 1 : 0;
                                    coalitionCount += (cardTwo.s1.id == "Coalition" || cardTwo.s2.id == "Coalition" || cardTwo.s3.id == "Coalition") ? 1 : 0;
                                    coalitionCount += (cardThree.s1.id == "Coalition" || cardThree.s2.id == "Coalition" || cardThree.s3.id == "Coalition") ? 1 : 0;

                                    // If a card has Coalition, only sim if there's and 2 coa cards
                                    if (coalitionCount >= 2 && assaultFactionCount > 1) continue;

                                    // If not coalition - Only sim combos with similar factions (either all 3, or 2 + Progen, or 2 + Structure)
                                    if (assaultFactionCount + assaultSimilarFactionCount + structureCount < 3 && coalitionCount <= 1) continue;


                                    if (cardOne.Rarity == 4) legendCount++;
                                    if (cardTwo.Rarity == 4) legendCount++;
                                    if (cardThree.Rarity == 4) legendCount++;
                                    if (cardOne.Rarity == 5) vindCount++;
                                    if (cardTwo.Rarity == 5) vindCount++;
                                    if (cardThree.Rarity == 5) vindCount++;

                                    if (cardOne.Subset == CardSubset.Chance.ToString() && cardOne.Rarity >= 4) chanceLegendCount++;
                                    if (cardTwo.Subset == CardSubset.Chance.ToString() && cardTwo.Rarity >= 4) chanceLegendCount++;
                                    if (cardThree.Subset == CardSubset.Chance.ToString() && cardThree.Rarity >= 4) chanceLegendCount++;

                                    // Arbitrary hate - Don't sim 3 Legends unless they're all the same faction
                                    if (legendCount == 3 && assaultFactionCount < 3) continue;
                                }
                                // -------------------------
                                // Harder card restrictions
                                // -------------------------
                                if (chanceLegendMax >= 0)
                                {
                                    if (cardOne.Subset == CardSubset.Chance.ToString() && cardOne.Rarity >= 4) chanceLegendCount++;
                                    if (cardTwo.Subset == CardSubset.Chance.ToString() && cardTwo.Rarity >= 4) chanceLegendCount++;
                                    if (cardThree.Subset == CardSubset.Chance.ToString() && cardThree.Rarity >= 4) chanceLegendCount++;

                                    // Arbitrary hate - Don't sim 3 Legends unless they're all the same faction
                                    if (chanceLegendCount > chanceLegendMax) continue;
                                }
                                if (mythicMax >= 0)
                                {
                                    if (cardOne.Subset == CardSubset.Box.ToString() && cardOne.Rarity >= 6) mythicCount++;
                                    if (cardTwo.Subset == CardSubset.Box.ToString() && cardOne.Rarity >= 6) mythicCount++;
                                    if (cardThree.Subset == CardSubset.Box.ToString() && cardThree.Rarity >= 6) mythicCount++;

                                    if (mythicCount > mythicMax) continue;
                                }
                                if (vindMax >= 0)
                                {
                                    if (cardOne.Subset == CardSubset.Box.ToString() && cardOne.Rarity == 5) vindCount++;
                                    if (cardTwo.Subset == CardSubset.Box.ToString() && cardOne.Rarity == 5) vindCount++;
                                    if (cardThree.Subset == CardSubset.Box.ToString() && cardThree.Rarity == 5) vindCount++;
                                }
                                if (freeVindMax >= 0)
                                {
                                    var freeVindCount = 0;
                                    if (cardOne.Set == 2500 && cardOne.Rarity == 5) freeVindCount++;
                                    if (cardTwo.Set == 2500 && cardOne.Rarity == 5) freeVindCount++;
                                    if (cardThree.Set == 2500 && cardThree.Rarity == 5) freeVindCount++;
                                    if (cardOne.Subset == CardSubset.PvP_Reward.ToString() && cardOne.Rarity == 5) freeVindCount++;
                                    if (cardTwo.Subset == CardSubset.PvP_Reward.ToString() && cardTwo.Rarity == 5) freeVindCount++;
                                    if (cardThree.Subset == CardSubset.PvP_Reward.ToString() && cardThree.Rarity == 5) freeVindCount++;

                                    if (freeVindCount > freeVindMax) continue;
                                }


                                // Swap cards on rarity
                                if (cardTwo.Subset == CardSubset.Box.ToString() && cardTwo.Rarity >= 6)
                                {
                                    var tmp = cardOne;
                                    cardOne = cardTwo;
                                    cardTwo = tmp;
                                }
                                if (cardTwo.Subset == CardSubset.Chance.ToString() && cardTwo.Rarity >= 5)
                                {
                                    var tmp = cardOne;
                                    cardOne = cardTwo;
                                    cardTwo = tmp;
                                }
                                // Swap cards on rarity
                                if (cardThree.Subset == CardSubset.Box.ToString() && cardThree.Rarity >= 6)
                                {
                                    var tmp = cardOne;
                                    cardOne = cardThree;
                                    cardThree = tmp;
                                }
                                if (cardThree.Subset == CardSubset.Chance.ToString() && cardThree.Rarity >= 5)
                                {
                                    var tmp = cardOne;
                                    cardOne = cardThree;
                                    cardThree = tmp;
                                }


                                // Create the deck
                                var commander = SpamSim_PickCommander(new List<Card> { cardOne, cardTwo, cardThree });
                                var deck = new StringBuilder();
                                deck.Append(dominion);
                                deck.Append(",");
                                deck.Append(commander);
                                deck.Append(",");
                                deck.Append(cardOne.Name);
                                deck.Append(",");
                                deck.Append(cardTwo.Name);
                                deck.Append(",");
                                deck.Append(cardThree.Name);
                                deck.Append(",");
                                deck.Append(cardOne.Name);
                                deck.Append(",");
                                deck.Append(cardTwo.Name);
                                deck.Append(",");
                                deck.Append(cardThree.Name);
                                deck.Append(",");
                                deck.Append(cardOne.Name);
                                deck.Append(",");
                                deck.Append(cardTwo.Name);
                                deck.Append(",");
                                deck.Append(cardThree.Name);

                                // Merge the deck with the sim template, and add that to result
                                var simLine = spamSimTemplate.Replace("REPLACEME", deck.ToString());
                                result.Add(simLine);
                            }//k
                        }//j
                    }//i
                }
            }

            return result;
        }

        /// <summary>
        /// Builds a template for a spam deck. Uses "REPLACEME" as myDeck, that gets replaced later
        /// </summary>
        private static Sim BuildSpamSim(MainForm form)
        {
            try
            {
                int iterations = 100;

                var sim = new Sim
                {
                    // Decks
                    MyDeck = new Deck("REPLACEME"),
                    EnemyDeck = (form.batchSimGauntletListBox.SelectedItems.Count > 0) ? form.batchSimGauntletListBox.SelectedItems[0].ToString() : "",

                    // Forts and BGEs
                    MyForts = form.batchSimMyFortComboBox.Text,
                    EnemyForts = form.batchSimEnemyFortComboBox.Text,
                    Bge = form.batchSimBgeComboBox.Text,
                    MyBge = form.batchSimYourBgeComboBox.Text,
                    EnemyBge = form.batchSimEnemyBgeComboBox.Text,
                    //MyDominion = dominion,

                    // GameMode / Operation
                    GameMode = GetGameMode(form, form.batchSimGameModeComboBox.Text, iterations),
                    Operation = "sim " + iterations,

                    // Gauntlets and inventory
                    GauntletFile = "_" + form.batchSimCustomDecksComboBox.Text,

                    // Deck Limit

                    // Other flags
                    CpuThreads = 4,
                    Verbose = false,
                    HarmonicMean = false,
                    Fund = 0,
                    ExtraTuoFlags = form.batchSimExtraFlagsTextBox.Text
                };

                sim.GameMode.Replace("brawl", "gw");

                return sim;
            }
            catch (Exception ex)
            {
                return new Sim { StatusMessage = "Syntax error in PlayerSimString(): " + ex };
            }
        }

        #endregion



        #region Helpers - Building a sim line
        
        /// <summary>
        /// Get the number of cards (non-commanders) in a deck string
        /// </summary>
        public static int GetDeckCount(string myDeck)
        {
            var deck = myDeck.Split(',');
            var deckCount = 0;

            // Negate the commander in deck
            deckCount--;

            // Negate if a Dominion is in the deck
            if (TextCleaner.HasDominion(myDeck)) deckCount--;
            
            if (deck.Length > 0)
            {
                for (int i = 0; i < deck.Length; i++)
                {
                    // Remove odd characters from the deck
                    string card = deck[i].Trim().Replace(" ", "").Replace("\r\n", "").Replace("\t", "");

                    // Look for card#2, or card(2)
                    var regex = new Regex(".*#([0-9]+).*");
                    var regex2 = new Regex(".*\\(([0-9]+)\\).*");
                    var match = regex.Match(card);
                    var match2 = regex2.Match(card);

                    if (match.Success || match2.Success)
                    {
                        deckCount += Int32.Parse(match.Groups[1].Value);
                    }
                    else
                    {
                        deckCount++;
                    }
                }
            }

            return deckCount;
        }

        /// <summary>
        /// Translate TUO GameMode from Pirate TUO "game mode"
        /// 
        /// Pass iterations when doing flex
        /// </summary>
        public static string GetGameMode(MainForm form, string gameMode, int iterations)
        {
            // Fox flex mode
            int flexIterations = Math.Min(iterations / 3, 50);
            int fastFlexIterations = Math.Min(iterations / 2, 50);
            if (flexIterations < 10) flexIterations = 10;
            if (fastFlexIterations < 10) fastFlexIterations = 10;

            // Variances of the flex sim with X turns allowed. 
            // * Fell said Turn 5 is generally accurate, T4 is signficiantly faster with 1% winrate variance, T3 even faster but 5%+ variance

            switch (gameMode)
            {
                // --- Surge mode: Enemy goes first, only assess win rate --- //
                // * [outdated] Conquest mode also goes by win rate
                case "Surge - Ordered":
                    return "surge ordered";
                case "Surge - Random":
                    return "surge";
                case "Surge - Flex":
                    return "surge flexible flexible-iter " + flexIterations + " flexible-turn 5 ";
                case "Surge - Flex6":
                    return "surge flexible flexible-iter " + flexIterations + " flexible-turn 6 ";
                case "Surge - Flex3":
                    return "surge flexible flexible-iter " + flexIterations + " flexible-turn 3 ";
                case "Surge - Flex2":
                    return "surge flexible flexible-iter " + flexIterations + " flexible-turn 2 ";
                case "Surge - Flex1":
                    return "surge flexible flexible-iter " + flexIterations + " flexible-turn 1 ";
                case "Surge - FlexSlow":
                    return "surge flexible flexible-iter " + flexIterations + " ";
                case "Surge - FlexFast":
                    return "surge flexible flexible-iter " + fastFlexIterations + " flexible-turn 3 ";

                case "Surge - Evaluate":
                    return "surge evaluate ";
                case "Surge - Evaluate2":
                    return "surge evaluate2 ";
                case "Surge - EvaluateFast":
                    return "surge evaluate eval-iter 5 eval-turn 5 ";
                case "Surge - EvaluateSlow":
                    return "surge evaluate eval-iter 25 ";
                case "Surge - EvaluateSuperSlow":
                    return "surge evaluate eval-iter 75 ";


                // --- War mode: Enemy goes first, scoring is based on how fast you win --- //
                case "War - Ordered":
                    return "gw ordered";
                case "War - Random":
                    return "gw";
                case "War - Flex":
                    return "gw flexible flexible-iter " + flexIterations + " flexible-turn 5 ";
                case "War - Flex6":
                    return "gw flexible flexible-iter " + flexIterations + " flexible-turn 6 ";
                case "War - Flex3":
                    return "gw flexible flexible-iter " + flexIterations + " flexible-turn 3 ";
                case "War - Flex2":
                    return "gw flexible flexible-iter " + flexIterations + " flexible-turn 2 ";
                case "War - Flex1":
                    return "gw flexible flexible-iter " + flexIterations + " flexible-turn 1 ";
                case "War - FlexSlow":
                    return "gw flexible flexible-iter " + flexIterations + " ";
                case "War - FlexFast":
                    return "gw flexible flexible-iter " + fastFlexIterations + " flexible-turn 3 ";

                case "War - Evaluate":
                    return "gw evaluate ";
                case "War - Evaluate2":
                    return "gw evaluate2 ";
                case "War - EvaluateFast":
                    return "gw evaluate eval-iter 5 eval-turn 5 ";
                case "War - EvaluateSlow":
                    return "gw evaluate eval-iter 25 ";
                case "War - EvaluateSuperSlow":
                    return "gw evaluate eval-iter 75 ";



                // --- Brawl mode: Enemy goes first, scoring is based on --
                // * how many units you kill (plus cards left in opponents deck)
                // * -1 point for each card under 10 in your deck
                // * -1 point for every turn that goes by where your opponent's deck and hand are empty

                case "Brawl - Ordered":
                    return "brawl ordered";
                case "Brawl - Random":
                    return "brawl";
                case "Brawl - Flex":
                    return "brawl flexible flexible-iter " + flexIterations + " flexible-turn 5 ";
                case "Brawl - Flex6":
                    return "brawl flexible flexible-iter " + flexIterations + " flexible-turn 6 ";
                case "Brawl - Flex3":
                    return "brawl flexible flexible-iter " + flexIterations + " flexible-turn 3 ";
                case "Brawl - Flex2":
                    return "brawl flexible flexible-iter " + flexIterations + " flexible-turn 2 ";
                case "Brawl - Flex1":
                    return "brawl flexible flexible-iter " + flexIterations + " flexible-turn 1 ";
                case "Brawl - FlexSlow":
                    return "brawl flexible flexible-iter " + flexIterations + " ";
                case "Brawl - FlexFast":
                    return "brawl flexible flexible-iter " + fastFlexIterations + " flexible-turn 3 ";
                case "Beam Brawl - Random":
                    return "beam 100 brawl";
                case "Beam Brawl - Ordered":
                    return "beam 100 brawl ordered";
                case "Beam Brawl - Flex":
                    return "beam 100 brawl flexible flexible-iter " + flexIterations + " flexible-turn 4 ";
                case "Brawl - Evaluate":
                    return "brawl evaluate ";
                case "Brawl - Evaluate2":
                    return "brawl evaluate2 ";
                case "Brawl - EvaluateFast":
                    return "brawl evaluate eval-iter 5 eval-turn 5 ";
                case "Brawl - EvaluateSlow":
                    return "brawl evaluate eval-iter 25 ";
                case "Brawl - EvaluateSuperSlow":
                    return "brawl evaluate eval-iter 75 ";



                // -- Beam mode: Experimental tuo mode, similar to anneal? --
                case "Beam Surge - Random":
                    return "beam 100 surge";
                case "Beam Surge - Ordered":
                    return "beam 100 surge ordered";
                case "Beam Surge - Flex":
                    return "beam 100 surge flexible flexible-iter " + flexIterations + " flexible-turn 4 ";


                // --- Defense mode: You go first and play randomly. Assessing winrate (plus stall rate) --- //
                case "Defense - Enemy Ordered":
                    return "defense enemy:ordered";
                case "Defense - Enemy Random":
                    return "defense";

                case "Defense - Flex":
                    return "defense enemy:flexible flexible-iter " + flexIterations + " flexible-turn 5 ";
                case "Defense - Flex6":
                    return "defense enemy:flexible flexible-iter " + flexIterations + " flexible-turn 6 ";
                case "Defense - Flex4":
                    return "defense enemy:flexible flexible-iter " + flexIterations + " flexible-turn 4 ";
                case "Defense - Flex3":
                    return "defense enemy:flexible flexible-iter " + flexIterations + " flexible-turn 3 ";
                case "Defense - Flex2":
                    return "defense enemy:flexible flexible-iter " + flexIterations + " flexible-turn 2 ";
                case "Defense - Flex1":
                    return "defense enemy:flexible flexible-iter " + flexIterations + " flexible-turn 1 ";
                case "Defense - FlexSlow":
                    return "defense enemy:flexible flexible-iter " + flexIterations + " ";
                case "Defense - FlexFast":
                    return "defense enemy:flexible flexible-iter " + fastFlexIterations + " flexible-turn 3 ";

                case "Defense - Evaluate":
                    return "defense enemy:evaluate ";
                case "Defense - Evaluate2":
                    return "defense enemy:evaluate2 ";
                case "Defense - EvaluateFast":
                    return "defense enemy:evaluate eval-iter 5 eval-turn 5 ";
                case "Defense - EvaluateSlow":
                    return "defense enemy:evaluate eval-iter 25 ";
                case "Defense - EvaluateSuperSlow":
                    return "defense enemy:evaluate eval-iter 75 ";

                // --- Battle Mode - you go first. Win rate only --- //
                case "Battle - Ordered":
                    return "pvp ordered";
                case "Battle - Random":
                    return "pvp";
                case "Battle - Flex":
                    return "pvp flexible flexible-iter " + flexIterations + " flexible-turn 5 ";
                case "Battle - Flex6":
                    return "pvp flexible flexible-iter " + flexIterations + " flexible-turn 6 ";
                case "Battle - Flex3":
                    return "pvp flexible flexible-iter " + flexIterations + " flexible-turn 3 ";
                case "Battle - Flex2":
                    return "pvp flexible flexible-iter " + flexIterations + " flexible-turn 2 ";
                case "Battle - Flex1":
                    return "pvp flexible flexible-iter " + flexIterations + " flexible-turn 1 ";
                case "Battle - FlexSlow":
                    return "pvp flexible flexible-iter " + flexIterations + " ";
                case "Battle - FlexFast":
                    return "pvp flexible flexible-iter " + fastFlexIterations + " flexible-turn 3 ";

                case "Battle - Evaluate":
                    return "pvp evaluate ";
                case "Battle - Evaluate2":
                    return "pvp evaluate2 ";
                case "Battle - EvaluateFast":
                    return "pvp evaluate eval-iter 5 eval-turn 5 ";
                case "Battle - EvaluateSlow":
                    return "pvp evaluate eval-iter 25 ";
                case "Battle - EvaluateSuperSlow":
                    return "pvp evaluate eval-iter 75 ";

                // --- Battle Mode - you go first. Win rate only --- //
                case "BattleDefense - Ordered":
                    return "pvp-defense enemy:ordered";
                case "BattleDefense - Random":
                    return "pvp-defense";
                case "BattleDefense - Flex":
                    return "pvp-defense enemy:flexible flexible-iter " + flexIterations + " flexible-turn 5 ";
                case "BattleDefense - Flex6":
                    return "pvp-defense enemy:flexible flexible-iter " + flexIterations + " flexible-turn 6 ";
                case "BattleDefense - Flex3":
                    return "pvp-defense enemy:flexible flexible-iter " + flexIterations + " flexible-turn 3 ";
                case "BattleDefense - Flex2":
                    return "pvp-defense enemy:flexible flexible-iter " + flexIterations + " flexible-turn 2 ";
                case "BattleDefense - Flex1":
                    return "pvp-defense enemy:flexible flexible-iter " + flexIterations + " flexible-turn 1 ";
                case "BattleDefense - FlexSlow":
                    return "pvp-defense enemy:flexible flexible-iter " + flexIterations + " ";
                case "BattleDefense - FlexFast":
                    return "pvp-defense enemy:flexible flexible-iter " + fastFlexIterations + " flexible-turn 3 ";

                case "BattleDefense - Evaluate":
                    return "pvp-defense enemy:evaluate ";
                case "BattleDefense - Evaluate2":
                    return "pvp-defense enemy:evaluate2 ";
                case "BattleDefense - EvaluateFast":
                    return "pvp-defense enemy:evaluate eval-iter 5 eval-turn 5 ";
                case "BattleDefense - EvaluateSlow":
                    return "pvp-defense enemy:evaluate eval-iter 25 eval-turn 6 ";
                case "BattleDefense - EvaluateSuperSlow":
                    return "pvp-defense enemy:evaluate eval-iter 75 ";


                // --- Campaign- opponent goes first. -10 points for each card you lose --- //
                case "Campaign - Ordered":
                    return "campaign ordered";
                case "Campaign - Random":
                    return "campaign";

                // --- Raid Mode - you go first. Scoring is mostly winrate, but you score slightly more when you lose and destroy enemy units --- //
                case "Raid - Ordered":
                    return "raid ordered";
                case "Raid - Random":
                    return "raid";
                case "Raid - Flex":
                    return "raid flexible flexible-iter " + flexIterations + " flexible-turn 3 ";

                //case "Surge - Reorder":
                //    return "gw ordered";
                default:
                    return "surge";
            }
        }

        /// <summary>
        /// Get lists of cards to sim with. player, player_possible, and extra cards
        /// </summary>
        private static string GetOwnedCards(bool usePossibleCards, List<string> inventory, List<string> cardAddons)
        {
            var ownedCards = "";
            foreach (var item in inventory)
            {
                // Not specifying anything will default to ownedcards.txt. To prevent that, we need to reference an empty file
                if (item.ToString() == "none")
                {
                    ownedCards += @"-o=""data/cards/ZZ_F2P-Inventory_possible.txt"" ";
                }
                // ownedcards: special case to use this file
                else if (item.ToString() == "ownedcards.txt")
                {
                    ownedCards += @"-o=""data/cards/ownedcards.txt"" ";
                }
                else
                {
                    ownedCards += @"-o=""data/cards/" + item.ToString() + @""" ";

                    // If possible cards were checked also add them
                    if (usePossibleCards)
                    {
                        ownedCards += @"-o=""data/cards/" + item.ToString().Replace(".txt", "") + @"_possible.txt"" ";
                    }
                }
            }

            // Card addons: Apply these
            foreach (var item in cardAddons)
            {
                if (item.ToString() == "none") continue;
                ownedCards += "-o=\"" + CONSTANTS.PATH_CARDADDONS + item.ToString() + "\" ";
            }

            return ownedCards;
        }

        /// <summary>
        /// Does this string contain a dominion (done in a really bad way)
        /// </summary>
        public static bool DeckContainsDominion(string deckList)
        {
            if (deckList.ToLower().Contains("alpha ")) return true;
            if (deckList.ToLower().Contains("'s nexus ")) return true;
            if (deckList.ToLower().Contains("s' nexus ")) return true;
            return false;
        }

        #endregion

        #region Helpers - Run queued sims

        /// <summary>
        /// Handles one sim result 
        /// - Adds it to the queued result output window
        /// - Adds it to List<SimResult>
        /// </summary>
        public static SimResult RunQueuedSims_GetResult(MainForm form, TextBox outputTextBox, string simString, List<string> simOutput, bool outputToWindow = true, bool writeToFile = true)
        {
            var simResult = new SimResult();
            
            // Batch sim - Output options
            var fileName = ControlExtensions.InvokeEx(form.queuedSimOutputFileTextBox, x => form.queuedSimOutputFileTextBox.Text);
            if (String.IsNullOrEmpty(fileName)) fileName = "PirateTuoSim";

            var isOffenseSim = ControlExtensions.InvokeEx(form.batchSimGameModeComboBox, x => !form.batchSimGameModeComboBox.Text.Contains("Defense"));
            var simResultFormatted = "";


            // Get sim info from the input sim string
            RunQueuedSim_ParseSimString(simResult, simString);

            var deckList = "";
            var winPercent = "";


            try
            {
                if (simOutput.Count > 0)
                {
                    // ------------------------------------
                    // Climb mode output
                    // ------------------------------------
                    if (!simOutput[0].StartsWith("win%:"))
                    {
                        // Reject wins below this percent (if the batch sim setting is set)
                        var rejectWinRate = ControlExtensions.InvokeEx(form.queuedSimMinWinRateTextBox, x => form.queuedSimMinWinRateTextBox.Text);

                        // Get result line
                        //NewSimManager.ParseSimResult(sim, simOutput);
                        bool approveSim = RunQueuedSims_GetWinLine(form, simOutput, ref deckList, ref winPercent);
                        if (!approveSim) return simResult;
                        
                        // Sim result, formatted
                        simResultFormatted = simResult.Player + ":" + winPercent + ":" + deckList;

                        try
                        {
                            if (outputToWindow)
                            {
                                simResultFormatted = simResultFormatted + "\r\n";
                                ControlExtensions.InvokeEx(outputTextBox, x => outputTextBox.AppendText(simResultFormatted));
                            }
                            if (writeToFile)
                            {
                                var simResultDirectory = "./sim results";
                                FileIO.SimpleWrite(form, simResultDirectory, fileName + ".txt", simResultFormatted, append: true);
                            }
                        }
                        catch { }

                        // Write output to a simResult.Player seed (temp)
                        RunQueuedSims_WritePlayerSeed(simResult.Player, deckList, simResult.Guild);
                    }


                    // ------------------------------------
                    // Sim mode output
                    // ------------------------------------
                    else
                    {
                        //// Scrub out dominion and commander for spamscores
                        //var deckBreakdown = simResult.PlayerDeck.Split(',');
                        //var dominion = deckBreakdown[0];
                        //var commander = deckBreakdown[1];
                        //var cardOne = deckBreakdown[2];
                        //var cardTwo = deckBreakdown.Length > 3 ? deckBreakdown[3] : "";
                        //var cardThree = deckBreakdown.Length > 4 ? deckBreakdown[4] : "";

                        //if (cardThree.EndsWith("#5")) cardThree = cardThree.Replace("#5", "");

                        // Get win percent
                        winPercent = simOutput[0].Split(' ')[1];
                        if (Double.TryParse(winPercent, out double winPercentDecimal))
                        {
                            winPercentDecimal = Math.Round(winPercentDecimal, 1);
                        }

                        // Discard results below a certain percent
                        var winRate = ControlExtensions.InvokeEx(form.queuedSimMinWinRateTextBox, x => form.queuedSimMinWinRateTextBox.Text);

                        if (!String.IsNullOrWhiteSpace(winRate))
                        {
                            int rejectPercent;
                            if (Int32.TryParse(winRate, out rejectPercent))
                            {
                                if (winPercentDecimal < rejectPercent) return simResult;
                            }
                        }

                        // Sim result, formatted
                        simResultFormatted = simResult.Player + ":" + winPercentDecimal + ":" + simResult.PlayerDeck;
                        if (outputToWindow)
                        {
                            simResultFormatted = simResultFormatted + "\r\n";
                            ControlExtensions.InvokeEx(outputTextBox, x => outputTextBox.AppendText(simResultFormatted));
                        }
                    }


                    // -------------------------
                    // Add to sim result
                    // -------------------------
                    var d = 0.0;
                    simResult.WinPercent = double.TryParse(winPercent, out d) ? Math.Round(d, 1) : 0.0;
                    simResult.PlayerDeck = deckList;

                }
            }
            catch(Exception ex)
            {
                ControlExtensions.InvokeEx(outputTextBox, x => outputTextBox.AppendText( "Error on batch simming player " + simResult.Player + ": " + ex.Message + "\r\n\r\n"));
            }

            return simResult;
        }
        

        /// <summary>
        /// Hacky way to extract player name from a queued sim string
        /// TODO: Is this used anymore
        /// </summary>
        public static void RunQueuedSim_ParseSimString(SimResult simResult, string simString)
        {
            // Extract the player name from the data/cards/<guild>_<player name>.txt part of the sim string
            var regex = new Regex(@"data\/cards\/_?.._(.*?)\.txt");
            var match = regex.Match(simString);

            // Try to find player
            if (match.Success)
            {
                simResult.Player = match.Groups[1].Value.Replace(".txt ", "");
            }

            // Try to find guild
            if (simString.Contains("cards/_DT_")) simResult.Guild = "DT";
            if (simString.Contains("cards/_TW_")) simResult.Guild = "TW";
            if (simString.Contains("cards/_TF_")) simResult.Guild = "TF";
            if (simString.Contains("cards/_WH_")) simResult.Guild = "WH";
            if (simString.Contains("cards/_SL_")) simResult.Guild = "SL";
            if (simString.Contains("cards/_MJ_")) simResult.Guild = "SL";
            //if (simString.Contains("cards/_LK_")) simResult.Guild = "LK";
            //if (simString.Contains("cards/_FAP_")) simResult.Guild = "FAP";
            if (simString.Contains("cards/_")) simResult.Guild = "__";

            // Try to find decks
            regex = new Regex("\"[^\"]*\"");
            var matches = regex.Matches(simString);
            if (matches.Count >= 2)
            {
                simResult.PlayerDeck = matches[0].Value.Replace("\"", "");
                simResult.EnemyDeck = matches[1].Value.Replace("\"", "");
            }

            // Try to find forts
            regex = new Regex(" yf \".*\"");
            match = regex.Match(simString);
            if (match.Success)
            {
                simResult.PlayerForts = matches[0].Value.Replace(" yf ", "").Replace("\"", "");
            }
            regex = new Regex(" ef \".*\"");
            match = regex.Match(simString);
            if (match.Success)
            {
                simResult.EnemyForts = matches[0].Value.Replace(" ef ", "").Replace("\"", "");
            }
            regex = new Regex(" -e \".*\"");
            match = regex.Match(simString);
            if (match.Success)
            {
                simResult.PlayerForts = matches[0].Value.Replace(" -e ", "").Replace("\"", "");
            }
            regex = new Regex(" ye \".*\"");
            match = regex.Match(simString);
            if (match.Success)
            {
                simResult.PlayerForts = matches[0].Value.Replace(" ye ", "").Replace("\"", "");
            }
            regex = new Regex(" ee \".*\"");
            match = regex.Match(simString);
            if (match.Success)
            {
                simResult.PlayerForts = matches[0].Value.Replace(" ee ", "").Replace("\"", "");
            }

            // Game mode
            regex = new Regex(" climbex [0-9]+");
            match = regex.Match(simString);
            if (match.Success)
            {
                simResult.GameMode = "climbex";
                return;
            }
            regex = new Regex(" reorder [0-9]+");
            match = regex.Match(simString);
            if (match.Success)
            {
                simResult.GameMode = "reorder";
                return;
            }
            regex = new Regex(" climb [0-9]+");
            match = regex.Match(simString);
            if (match.Success)
            {
                simResult.GameMode = "climb";
                return;
            }
            regex = new Regex(" sim [0-9]+");
            match = regex.Match(simString);
            if (match.Success)
            {
                simResult.GameMode = "sim";
                return;
            }
            regex = new Regex(" anneal [0-9]+");
            match = regex.Match(simString);
            if (match.Success)
            {
                simResult.GameMode = "anneal";
                return;
            }

        }

        /// <summary>
        /// Attempt to write a player's deck result to "Last Batch Sim"
        /// </summary>
        private static void RunQueuedSims_WritePlayerSeed(string player, string deckList, string guild)
        {
            var output = "";
            var line = "";

            if (String.IsNullOrWhiteSpace(player)) return;
            //if (guild == "Batch" || guild == "__") return;

            try
            {
                string[] files = Directory.GetFiles("./data/cards/", "*" + player + ".txt", System.IO.SearchOption.TopDirectoryOnly);
                foreach(var file in files)
                {
                    bool foundBatchSeedLine = false;
                    using (var reader = new StreamReader(file))
                    {
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (!line.StartsWith("//Last Batch Sim:")) output += line + "\r\n";
                            else
                            {
                                output += "//Last Batch Sim:" + deckList + "\r\n";
                                foundBatchSeedLine = true;
                            }
                        }

                        if (!foundBatchSeedLine)
                        {
                            output += "//Last Batch Sim:" + deckList + "\r\n";
                        }
                    }

                    using (var writer = new StreamWriter(file))
                    {
                        writer.Write(output);
                    }
                }

            }
            catch(Exception ex)
            {
                Console.WriteLine("Error when doing a batch sim, and trying to write the //Batch Sim 1: <deck> to file: " + ex.Message);
            }
        }
        
        /// <summary>
        /// Parses command line output for the first "win line"
        /// 
        /// Returns false if we don't want to output the result
        /// </summary>
        public static bool RunQueuedSims_GetWinLine(MainForm form, List<string> simResult, ref string deckList, ref string winPercent)
        {
            simResult.Reverse();

            // Looks for most recent line that contains "units". This won't work on the "Sim" game mode
            foreach (var item in simResult)
            {
                if (item == null || !item.Contains("units:"))
                    continue;

                // Gw/Brawl format
                // {{Optimized Deck}} X units: (N.NN% win) z.zz [zzz per win]: <deck>

                // Surge/PvP/Anneal format
                // {{Optimized Deck}} X units: N.NN: <deck>
                // {{Optimized Deck}} X units: (z.zz% stall) N.NN: 

                // Anneal format: Midsim
                // Deck improved: QC5XFRhPUXhPUXhFEWhFEWhPUXhFEWh: (temp=57.9353) :X units: N.NN: <deck>

                // Genetic format: Midsim
                // Generation 23
                // Deck improved: XU4bDRhBLOhbRQhBaVhZRZhZRZhHCehPHfhTIfhPKfhBLfh: X units: N.NN: <deck>

                string resultLine = item;

                // --anneal sim
                // Remove Deck improved: xxxx: (temp=xx.xxx) :
                resultLine = Regex.Replace(resultLine, @"Deck improved.*\(temp=\d.*\.\d*\) :", "");

                // --genetic sim
                // Remove Deck improved: XU4bDRhBLOhbRQhBaVhZRZhZRZhHCehPHfhTIfhPKfhBLfh: X units: N.NN: <deck>
                resultLine = Regex.Replace(resultLine, @"Deck improved.*units: ", "");

                // Remove optimized deck: chunk
                resultLine = Regex.Replace(resultLine, @"Optimized Deck: ", "");

                // Remove x units:
                resultLine = Regex.Replace(resultLine, @"\d* units: ", "");

                // Remove optimized stall chunk
                resultLine = Regex.Replace(resultLine, @"\(.*stall\) ", "");

                // Remove [x per win] chunk
                resultLine = Regex.Replace(resultLine, @"\[.*\]", "");

                // Remove "% win)" chunk
                resultLine = Regex.Replace(resultLine, @"% win\).*:", ":");

                // Remove any (
                resultLine = Regex.Replace(resultLine, @"\(", "");

                var simLine = resultLine.Split(':');
                
                winPercent = simLine.ElementAt(0);
                deckList = simLine.ElementAt(1);
                

                // Modify output text
                try
                {
                    // Round down win percent
                    if (Double.TryParse(winPercent, out double winRounded))
                    {
                        winPercent = Math.Round(winRounded, 1).ToString();
                    }

                    // Discard results below a certain percent
                    var winRate = ControlExtensions.InvokeEx(form.queuedSimMinWinRateTextBox, x => form.queuedSimMinWinRateTextBox.Text);

                    if (!String.IsNullOrWhiteSpace(winRate))
                    {
                        if (double.TryParse(winRate, out double rejectPercent))
                        {
                            if (winRounded < rejectPercent) return false;
                        }
                    }

                    // Decklist: Add -1 to quad commanders
                    deckList = TextCleaner.DeckHyphenQuadCommanders(deckList);
                }
                catch { }

                // Found the sim win% and line
                return true;
            }

            return false;
        }
        
        /// <summary>
        /// After running batch sims, handle the output
        /// </summary>
        public static void RunQueuedSims_ProcessResults(MainForm form, TextBox outputTextBox, List<SimResult> simResults, bool navigatorMetrics=false, bool navigatorDetail=true)
        {
            var resultOutput = new StringBuilder();

            if (simResults.Count > 0)
            {
                double averageWinPercent = simResults.Average(x => x.WinPercent); //Average win

                // Regular output
                if (!navigatorMetrics)
                {
                    resultOutput.AppendLine("\n----");
                    resultOutput.AppendLine(simResults.Count + " results");
                    resultOutput.AppendLine("Average win(%): " + Math.Round(averageWinPercent, 1));
                }
                // Navigator sim - detail
                else if (navigatorDetail)
                {
                    var worstDecks = simResults.Where(x => x.WinPercent <= 50).OrderBy(x => x.WinPercent).ToList(); //Navigator sim: 50% or worse
                    resultOutput.AppendLine("\n----");
                    resultOutput.AppendLine(simResults[0].PlayerDeck);
                    resultOutput.AppendLine("Average win(%): " + Math.Round(averageWinPercent, 1));
                    resultOutput.AppendLine(simResults.Count + " results");

                    resultOutput.AppendLine("Scored below 50%: " + worstDecks.Count + " decks");
                    foreach (var deck in worstDecks)
                    {
                        resultOutput.AppendLine(Math.Round(deck.WinPercent, 1) + ": " + deck.EnemyDeck);
                    }
                }
                // Navigator sim - simple output
                else
                {
                    resultOutput.AppendLine(Math.Round(averageWinPercent, 1) + ": " + simResults[0].PlayerDeck);
                }
            }
            else
            {
                ControlExtensions.InvokeEx(outputTextBox, x => outputTextBox.AppendText("No sim results seen"));
            }

            ControlExtensions.InvokeEx(outputTextBox, x => outputTextBox.AppendText( resultOutput.ToString()));

        }

        #endregion

        #region Helpers - Other


        /// <summary>
        /// Given a list of cards, pick a commander
        /// </summary>
        public static string SpamSim_PickCommander(List<Card> cards)
        {            
            if (cards.Count == 0) return "Daedalus Enraged";

            var chosenCommander = "Daedalus Enraged";
            var faction = cards[0].Faction;

            foreach (var card in cards)
            {
                // Use daedalus for jam or mimic cards
                if (card.s1.id == "Jam" || card.s2.id == "Jam" || card.s3.id == "Jam" ||
                    card.s1.id == "Mimic" || card.s2.id == "Mimic" || card.s3.id == "Mimic")
                {
                    chosenCommander = card.Faction != 2 ? "Daedalus Enraged" : "Silus the Warlord";
                    return chosenCommander;
                }
                if (card.Faction == 2 || card.Faction == 6)
                {
                    if (card.s1.id == "Strike" || card.s2.id == "Strike" || card.s3.id == "Strike" ||
                        card.s1.id == "Enfeeble" || card.s2.id == "Enfeeble" || card.s3.id == "Enfeeble")

                    chosenCommander = "Silus the Warlord";
                    return chosenCommander;
                }

                // Two different factions. Progens (6) is fine
                if (card.Faction != 6 && card.Faction != faction)
                {
                    faction = 0;
                }
            }                

            switch(faction)
            {
                case 0:
                    chosenCommander = "Arkadios Ultimate";
                    break;
                case 1:
                case 6:
                    chosenCommander = "Tabitha Liberated";
                    break;
                case 2:
                    chosenCommander = "Typhon the Insane";
                    break;
                case 3:
                    chosenCommander = "Dracorex Hivegod";
                    break;
                case 4:
                    chosenCommander = "Nexor the Farseer";
                    break;
                case 5:
                    chosenCommander = "Councilor Constantine";
                    break;

            }

            return chosenCommander;
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
                var proc = Process.GetProcessById(pid);
                if (!proc.HasExited) proc.Kill();
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

        #endregion
    }        
}
