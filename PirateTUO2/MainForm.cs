using PirateTUO2.Modules;
using System;
using System.Net;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Concurrent;
using System.Collections.Generic;
using MongoDB.Driver;
using System.Threading.Tasks;
using PirateTUO2.Models;
using System.Text;
using System.Drawing;
using PirateTUO2.Classes;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Deployment.Application;
using System.Reflection;

/// <summary>
/// 
/// When changing card pull system
/// * Rework possible_cards to list base epics
/// * Make sure owned and possible cards ignores already commented out lines. But save those to put in later
/// </summary>

namespace PirateTUO2
{
    /// <summary>
    /// Major components
    /// 
    /// CardManager - parses the XML into Card Objects
    /// PlayerManager - parses a CSV into Player objects (with a list of cards)
    /// DbManager - connects to the database
    /// GauntletManager - compiles sim output lines and run sims
    /// 
    /// </summary>
    public partial class MainForm : Form
    {
        // ---------------------
        // UPDATE NUMBERING ON PUSHES
        // ---------------------
        public static double THIS_PTUO_VERSION = 17.55;

        // ---------------------
        // DEBUGGING
        // ---------------------
        public bool debugMode = false;
        public bool debugSkipDownloadsFromTuServer = false;

        // --------------------------------------------------------
        // Counter to indicate the most recent job using the progress bar
        // --------------------------------------------------------
        public int grinderProgressBarJobNumber = 0;

        // Used for delayed textbox events
        TypeAssistant searchbarAssistant;

        // Used for running threads in parallel without tying up the main thread
        public Thread workerThread = null;
        public bool stopProcess = false;

        // Global unsorted vars
        public bool startPullerHunt = false;


        #region AppStart

        public MainForm()
        {
            InitializeComponent();

            // Version
            this.Text = "pTUO " + THIS_PTUO_VERSION;

            // TUO: What version are we running
            string tuoVersion = Helper.GetTuoVersion();
            outputTextBox.AppendText(tuoVersion + "\r\n\r\n");


            try
            {
                // Initialize watchers for a couple form controls
                searchbarAssistant = new TypeAssistant();
                searchbarAssistant.Idled += assistant_Idled;

                mainTabControl.TabPages.Remove(rumBarrelTab);
                mainTabControl.TabPages.Remove(adminTab);
                //sideTabControl.TabPages.Remove(adminUpdateTab);

                // ------------------------
                // Attempt to login. 
                // On success, initialize stuff
                // ------------------------
                AutoLogin();

            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on Init(): " + ex);
            }
        }

        #endregion


        #region Login

        /// <summary>
        /// Attempt to autologin
        /// If ./tuo/__login.txt exists, use the values from that
        /// </summary>
        public async void AutoLogin()
        {
            try
            {
                // Get the login info from the text file
                var loginForm = FileIO.SimpleRead(this, "./__login.txt", returnCommentedLines: false);

                // Loginform can be a one-line, comma separated file, or multiple lines

                // One line, CSV
                if (loginForm.Count == 1)
                {
                    var loginFormParts = loginForm[0].Split(',');
                    foreach (var part in loginFormParts)
                    {
                        var partTrimmed = part.Trim();

                        // Login
                        if (partTrimmed.StartsWith("user:"))
                            CONFIG.userName = partTrimmed.Replace("user:", "");
                        else if (partTrimmed.StartsWith("password:"))
                            CONFIG.password = partTrimmed.Replace("password:", "");
                    }
                }
                else if (loginForm.Count >= 2)
                {
                    foreach (var part in loginForm)
                    {
                        var partTrimmed = part.Trim();

                        // Login
                        if (partTrimmed.StartsWith("user:"))
                            CONFIG.userName = part.Replace("user:", "");
                        else if (partTrimmed.StartsWith("password:"))
                            CONFIG.password = part.Replace("password:", "");
                    }
                }

                // Attempt to login
                if (CONFIG.userName.Length > 0 && CONFIG.password.Length > 0)
                {
                    outputTextBox.AppendText("LOGIN: BootyBay database.. ");

                    // Debug
                    if (CONFIG.userName == "overboard" && CONFIG.password == "overboard")
                    {
                        CONFIG.overrideNormalLogin = true;

                        // Remove Login Page                    
                        mainTabControl.TabPages.Remove(this.loginTab);

                        // Successful login - get resources
                        await GetResources();
                    }

                    // Attempt to Login
                    else if (DbManager.Login(this))
                    {
                        outputTextBox.AppendText("Success. \r\n");

                        // Remove Login Page                    
                        mainTabControl.TabPages.Remove(this.loginTab);

                        // Successful login - get resources
                        await GetResources();
                    }
                    else
                    {
                        outputTextBox.AppendText("Wrong username and/or password. \r\n");
                    }
                }
                else
                {
                    Helper.OutputWindowMessage(this, "Could not read the Login file (__login.txt). When you login for the first time this should be created");
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Login failed: " + ex.ToString());
            }
        }

        /// <summary>
        /// Login button click
        /// </summary>
        private async void loginButton_Click(object sender, EventArgs e)
        {
            var user = loginUserTextBox.Text.Trim();
            var password = loginPasswordTextBox.Text.Trim();

            try
            {
                var loginInfo = "user:" + user + "\n" + "password:" + password;
                FileIO.SimpleWrite(this, "./", "__login.txt", loginInfo);

            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Creating the login file failed: " + ex);
            }

            try
            {
                CONFIG.userName = user;
                CONFIG.password = password;

                outputTextBox.AppendText("LOGIN: BootyBay.. ");
                if (DbManager.Login(this))
                {
                    outputTextBox.AppendText("Success. \r\n");
                    this.mainTabControl.TabPages.Remove(this.loginTab);
                    await GetResources();
                }
                else
                {
                    outputTextBox.AppendText("Wrong username and/or password. \r\n");
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error when attempting to login with userName: " + CONFIG.userName + " / password: " + CONFIG.password + "\r\n" + ex);
            }
        }

        #endregion

        #region Initialize

        /// <summary>
        /// Pulls files and builds player/card collections
        /// </summary>
        private async Task GetResources()
        {
            // Time each major operation
            Stopwatch stopwatch = new Stopwatch();

            progressBar.Maximum = 5;
            progressBar.Value = 0;
            progressBar.Step = 1;

            stopwatch.Start();

            // -- DEBUG MODE -- 
            bool skipDownloads = CONFIG.userName == "overboard" && CONFIG.password == "overboard";


            // -- DOWNLOAD FILES --
            if (!debugMode && !skipDownloads)
            {
                string[] results;

                outputTextBox.AppendText("DOWNLOADING - XMLs, PlayerFiles, GTs..\r\n");

                // Download game xml files / files from MongoDb
                var task1 = Task.Run(() => XmlManager.PullGameXml(this));
                var task2 = Task.Run(() => DbManager.DownloadFiles(this));

                if (!debugSkipDownloadsFromTuServer)
                {
                    results = await Task.WhenAll(new Task<string>[] { task1, task2 }).ConfigureAwait(true);
                }
                else
                {
                    results = await Task.WhenAll(new Task<string>[] { task2 }).ConfigureAwait(true);
                }

                // Report errors, or if xml differs from existing files
                foreach (var result in results)
                {
                    if (result == "Xml: No changes detected") continue;
                    this.InvokeEx(x => x.outputTextBox.AppendText(result + " - "));
                }


                // 2020 - CUSTOM CARDS - if config/customcardsX.txt contains user made cards, incorporate those into the game xml files
                string modifiedCards = XmlManager.ModifyCardXML(this);
                if (modifiedCards != "")
                {
                    outputTextBox.AppendText(modifiedCards + "\r\n\r\n");
                }

                // How long did this all take to finish
                outputTextBox.AppendText(" - " + Math.Round(((double)stopwatch.ElapsedMilliseconds / 1000), 1) + " seconds \r\n\r\n");
                progressBar.PerformStep();
                progressBar.PerformStep();
                stopwatch.Reset();
            }
            // -- DEBUG MODE: Skip downloading files --
            else
            {
                outputTextBox.AppendText("DOWNLOADING - You are in debug mode so we are skipping this..\r\n");
            }


            // -- Clear out customdecks_genetic.txt. Its not pulled online, but may be created later -- //
            FileIO.SimpleWrite(this, "data", "customdecks_genetic.txt", "");


            // -- CARD DATABASE --
            // * Parse XML to create a card dictionary
            // * We need this collection to perform the majority of this app's functionality
            // * This will judge each card using a power system, and give cards a 'power' and 'factionPower' score
            stopwatch.Start();
            outputTextBox.AppendText("BUILDING - Card database..\r\n");
            outputTextBox.AppendText(await CardManager.BuildCardDatabase(this) + " - " + Math.Round(((double)stopwatch.ElapsedMilliseconds / 1000), 1) + " seconds\r\n\r\n");
            progressBar.PerformStep();
            stopwatch.Reset();

            // -- WHITELIST FILES --
            // * Create card-addon files from tuo/config/card-addons to create additional card options when simming 
            stopwatch.Start();
            outputTextBox.AppendText("BUILDING - Addon Cards (boxes, whitelists)\r\n");
            outputTextBox.AppendText(PlayerManager.BuildCardAddonFiles(this) + " - " + Math.Round(((double)stopwatch.ElapsedMilliseconds / 1000), 1) + " seconds\r\n\r\n");
            progressBar.PerformStep();
            stopwatch.Reset();


            // -- FILES: PLAYERS --
            // * Deletes all player inventory files in tuo/data/cards
            // * Semi-Deprecated - this would create player inventories from a csv file, stored in mongo
            // * This file was created by looking at Ogre's google drive 'database' and parsing out relevant info

            PlayerManager.ImportPlayerCards(this);

            //stopwatch.Start();
            //outputTextBox.AppendText("DELETING - Previous Player Inventories\r\n");
            //outputTextBox.AppendText("CREATING - Player Inventories\r\n");
            //outputTextBox.AppendText(PlayerManager.ImportPlayerCards(this) + " - " + Math.Round(((double)stopwatch.ElapsedMilliseconds / 1000), 1) + " seconds \r\n\r\n");
            //progressBar.PerformStep();
            //progressBar.PerformStep();
            //stopwatch.Reset();

            // -- FORM: CONTROLS --
            // * Many textbox and dropdown values are stored on change, and this restores previous values
            stopwatch.Start();
            outputTextBox.AppendText("LOADING - Form Controls\r\n");
            _ControlSetup.Init(this);
            CardManager.FilterCards(this);
            progressBar.PerformStep();
            outputTextBox.AppendText("Success - " + Math.Round(((double)stopwatch.ElapsedMilliseconds / 1000), 1) + " seconds.\r\n\r\n");
            stopwatch.Stop();


            // Miscelleanous setup
            FileIO.AddCardAbbreviations();

            // -- CHECK VERSION --
            // * This will notify (annoy) the user that a newer version is available
            // * Run tuo/UpdatePtuo.exe to get that
            if (CONSTANTS.LATEST_PTUO_VERSION > THIS_PTUO_VERSION)
            {
                outputTextBox.AppendText("\r\n---------------------------\r\n");
                outputTextBox.AppendText("NEW PTUO VERSION AVAILABLE " + CONSTANTS.LATEST_PTUO_VERSION + "\r\n");
                outputTextBox.AppendText("To update, close this and run updateptuo.exe \r\n");
                outputTextBox.AppendText("---------------------------\r\n\r\n");
            }

            // -- DONE --
            outputTextBox.AppendText("\r\n------------\r\nReady\r\n------------\r\n");
            doneLoadingCheckBox.Checked = true;
            progressBar.Value = 0;
        }

        #endregion


        // ---- Captures generic WinForm Events to open files, save user input, etc ----


        #region Events - Generic

        /// <summary>
        /// Actions taken (typically saving input) when a control changes
        /// </summary>
        private void textbox_textChanged(object sender, EventArgs e)
        {
            // Don't fire events until the app is done loading
            if (!doneLoadingCheckBox.Checked) return;

            try
            {
                var control = (TextBox)sender;
                var controlName = control.Name;

                if (Properties.Settings.Default[controlName] != null)
                {
                    Properties.Settings.Default[controlName] = control.Text;
                    Properties.Settings.Default.Save();
                }

                // Sim Tab 1-2-3: Remove weird spacing out of "MyDeck"
                if (controlName.Contains("myDeckTextBox") || controlName.Contains("crowsNestSetDeckTextBox"))
                {
                    control.Text = control.Text.Replace("\r\n", "").Replace("\t", "").Replace("Optimized Deck: ", "");
                }

                // Cardfinder: Adjust the cards shown in the card finder section
                if (controlName.Contains("cardFinder"))
                {
                    CardManager.FilterCards(this);
                }
            }
            catch
            {
                Console.WriteLine("Current control doesn't have a Save setting");
            }
        }

        private void combobox_textChanged(object sender, EventArgs e)
        {
            // Don't fire events until the app is done loading
            if (!doneLoadingCheckBox.Checked) return;

            try
            {
                var control = (ComboBox)sender;
                var controlName = control.Name;
                var i = 1;
                if (controlName.Contains("2")) i = 2;
                else if (controlName.Contains("3")) i = 3;


                // Cardfinder: Adjust filter
                if (controlName.Contains("cardFinder"))
                {
                    CardManager.FilterCards(this);
                }

                // Dominion combobox - try to find the dominion and display stats
                if (controlName.Contains("myDominionComboBox"))
                {
                    try
                    {
                        // Dominion dropdown is empty
                        if (string.IsNullOrEmpty(Helper.GetControlText(this, "myDominionComboBox" + i))) return;
                        if (i == 1) myDominionLabel1.Text = CardManager.CardToString(control.Text, includeName: false, includeType: false, includeStats: true, includeMetadata: false);
                        else if (i == 2) myDominionLabel2.Text = CardManager.CardToString(control.Text, includeName: false, includeType: false, includeStats: true, includeMetadata: false);
                        else if (i == 3) myDominionLabel3.Text = CardManager.CardToString(control.Text, includeName: false, includeType: false, includeStats: true, includeMetadata: false);
                    }
                    catch { }
                }
                if (control.Name == "crowsNestResetDominionComboBox")
                {
                    if (!string.IsNullOrEmpty(crowsNestResetDominionComboBox.Text))
                    {
                        crowsNestDominionLabel.Text = CardManager.CardToString(control.Text, includeName: false, includeType: false, includeStats: true, includeMetadata: false);
                    }
                    // Don't save input for this
                    return;
                }

                // InventoryFilter combo box - select whatever the player is searching for
                if (controlName.Contains("inventoryFilterComboBox"))
                {
                    try
                    {
                        if (string.IsNullOrEmpty(Helper.GetControlText(this, "inventoryFilterComboBox" + i))) return;

                        var filterText = Helper.GetControlText(this, "inventoryFilterComboBox" + i);
                        var players = Helper.GetListBox(this, "inventoryListBox" + i).Items;
                        Helper.GetListBox(this, "inventoryListBox" + i).SelectedItems.Clear();

                        for (int x = 0; x < players.Count; x++)
                        {
                            if (players[x].ToString().ToLower().Contains(filterText.ToLower()))
                            {
                                Helper.GetListBox(this, "inventoryListBox" + i).SetSelected(x, true);
                                break;
                            }
                        }

                        if (Properties.Settings.Default[controlName] != null)
                        {
                            Properties.Settings.Default[controlName] = control.Text;
                            Properties.Settings.Default.Save();
                        }
                    }
                    catch { }
                    // Don't save input for this
                    return;
                }

                // Basic Sim /Game Mode - if genetic is selected, replace the user's MyDeck with the genetic deck gauntlet
                if (controlName.Contains("gameOperationComboBox"))
                {
                    if (Helper.GetComboBox(this, "gameOperationComboBox" + i).Text == "genetic")
                    {
                        string playerFile = Helper.GetListBox(this, "inventoryListBox" + i).Text ?? "_XX_KongName.txt";

                        // Split the string into (guild)_(playername).txt and only get the playername
                        // * junky split handling, this would break in rare circumstances
                        string[] playerNamePart = playerFile.Split(new char[] { '_' }, 3);
                        string playerName = playerNamePart[playerNamePart.Length - 1].Replace(".txt", "");

                        Helper.GetTextBox(this, "myDeckTextBox" + i).Text = playerName + "-Genetic";
                    }
                }

                // Save input (usually)
                if (Properties.Settings.Default[controlName] != null)
                {
                    Properties.Settings.Default[controlName] = control.Text;
                    Properties.Settings.Default.Save();
                }
            }
            catch
            {
                Console.WriteLine("Current control doesn't have a Save setting");
            }

        }

        private void listBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Don't fire events until the app is done loading
            if (!doneLoadingCheckBox.Checked) return;

            try
            {
                var control = (ListBox)sender;
                var controlName = control.Name;
                var tab = 1;
                var result = "";
                bool tryToSaveInput = false;

                if (controlName.Contains("1")) tab = 1;
                if (controlName.Contains("2")) tab = 2;
                else if (controlName.Contains("3")) tab = 3;

                foreach (var item in control.SelectedItems)
                {
                    result += item.ToString() + ",";
                }

                // inventorySeedDeckListBox: changes mydeck (if a valid player/seed are selected)
                if (control.Name.Contains("inventorySeedDeckListBox") && control.SelectedItem != null)
                {
                    var player = Helper.GetListBox(this, "inventoryListBox" + tab).SelectedItems;
                    var seed = Helper.GetListBox(this, "inventorySeedDeckListBox" + tab).SelectedItems;
                    if (player.Count > 0 && seed.Count > 0)
                    {
                        var seedDeck = "";
                        PlayerManager.SetPlayerDeckFromSeed(this, player[0].ToString(), seed[0].ToString(), out seedDeck);
                        Helper.GetTextBox(this, "myDeckTextBox" + tab).Text = seedDeck;
                    }
                }

                // inventoryListBox: changes player dominion
                if (control.Name.Contains("inventoryListBox") && control.SelectedItem != null)
                {
                    var playerName = control.SelectedItem.ToString();

                    if (control.SelectedItem.ToString() != "ownedcards.txt")
                    {
                        playerName = playerName.Replace(".txt", "").Trim();

                        if (playerName.StartsWith("_")) playerName = playerName.Substring(4); // Remove first 4 characters (guild tag)
                        else playerName = playerName.Substring(3); // Remove first 3 characters (guild tag)



                        var player = PlayerManager.Players.FirstOrDefault(p => p.KongName == playerName);
                        if (player != null)
                        {
                            if (player.DominionCards.Count >= 2)
                            {
                                if (controlName == "inventoryListBox1") myDominionComboBox1.Text = player.DominionCards[1].Name;
                                if (controlName == "inventoryListBox2") myDominionComboBox2.Text = player.DominionCards[1].Name;
                                if (controlName == "inventoryListBox3") myDominionComboBox3.Text = player.DominionCards[1].Name;
                            }

                            // Output player inventory to detail tab
                            detailTabLabel.Text = playerName;
                            string playerCards = PlayerManager.GetPlayerCardsAndSeeds(player, sortOrder: "rarity");
                            detailTabTextBox.Text = playerCards;
                        }
                    }
                }

                // Changing gauntletListBox changes enemyDeck
                // enemyDeckTextBox1
                if (controlName == "gauntletListBox1") enemyDeckTextBox1.Text = gauntletListBox1.Text;
                if (controlName == "gauntletListBox2") enemyDeckTextBox2.Text = gauntletListBox2.Text;
                if (controlName == "gauntletListBox3") enemyDeckTextBox3.Text = gauntletListBox3.Text;
                if (controlName == "navSimGauntletListBox") navSimEnemyDeckTextBox.Text = navSimGauntletListBox.Text;


                // Some listBoxes try to save selection. Most do not
                if (tryToSaveInput)
                {
                    if (Properties.Settings.Default[controlName] != null)
                    {
                        Properties.Settings.Default[controlName] = control.Text;
                        Properties.Settings.Default.Save();
                    }
                }
            }
            catch
            {
                Console.WriteLine("Current control doesn't have a Save setting");
            }
        }

        private void checkbox_checkChanged(object sender, EventArgs e)
        {
            // Don't fire events until the app is done loading
            if (!doneLoadingCheckBox.Checked) return;

            try
            {
                var control = (CheckBox)sender;
                var controlName = control.Name;
                var isChecked = control.Checked;

                if (Properties.Settings.Default[controlName] != null)
                {
                    Properties.Settings.Default[controlName] = (isChecked) ? "true" : "";
                    Properties.Settings.Default.Save();
                }

                // Card Tab: Adjust filters for cards
                if (controlName.Contains("cardFinder"))
                {
                    CardManager.FilterCards(this);
                }
            }
            catch
            {
                Console.WriteLine("Current control doesn't have a Save setting");
            }
        }

        private void updown_valueChanged(object sender, EventArgs e)
        {
            // Don't fire events until the app is done loading
            if (!doneLoadingCheckBox.Checked) return;

            try
            {
                var control = (UpDownBase)sender;
                var controlName = control.Name;


                if (Properties.Settings.Default[controlName] != null)
                {
                    Properties.Settings.Default[controlName] = control.Text;
                    Properties.Settings.Default.Save();
                }

                // Cardfinder: Adjust the cards shown in the card finder section
                if (controlName.Contains("cardFinder"))
                {
                    CardManager.FilterCards(this);
                }
            }
            catch
            {
                Console.WriteLine("Current control doesn't have a Save setting");
            }
        }

        #endregion

        #region Events - Menu Events

        // Top Menu or button click - Open a file or folder
        private void openFile_Click(object sender, EventArgs e)
        {
            var control = sender;
            var controlName = "";

            if (control is ToolStripMenuItem)
            {
                controlName = ((ToolStripMenuItem)control).Text;
            }
            else if (control is Button)
            {
                controlName = ((Button)control).Name;
            }

            try
            {
                switch (controlName)
                {
                    // Menu
                    case "brawl":
                    case "cq":
                    case "campaign":
                    case "pvp":
                    case "war":
                    case "warbig":
                    case "c3":
                    case "reck":
                    case "genetic":
                        FileIO.OpenFile("data/customdecks_" + controlName + ".txt", this);
                        break;
                    case "bges":
                    case "cardabbrs":
                    case "ownedcards":
                        FileIO.OpenFile("data/" + controlName + ".txt", this);
                        break;
                    case "cards_section_1":
                    case "cards_section_2":
                    case "cards_section_3":
                    case "cards_section_4":
                    case "cards_section_5":
                    case "cards_section_6":
                    case "cards_section_7":
                    case "cards_section_8":
                    case "cards_section_9":
                    case "cards_section_10":
                    case "cards_section_11":
                    case "cards_section_12":
                    case "cards_section_13":
                    case "cards_section_14":
                    case "cards_section_15":
                    case "cards_section_16":
                    case "cards_section_17":
                    case "cards_section_18":
                    case "cards_section_19":
                        // Current card section: 19
                    case "cards_section_20":
                    case "cards_section_21":
                    case "cards_section_22":
                    case "cards_section_23":
                    case "cards_section_24":
                    case "cards_section_25":
                    case "fusion_recipes_cj2":
                    case "missions":
                    case "raids":
                        FileIO.OpenFile("data/" + controlName + ".xml", this);
                        break;
                    case "Help":
                        FileIO.OpenFile("config/help.txt", this);
                        break;
                    case "seeds-dt":
                    case "seeds-tw":
                    case "seeds-tfk":
                    case "seeds-wh":
                    case "seeds-mj":
                    case "seeds-general":
                        FileIO.OpenFile("config/" + controlName + ".txt", this);
                        break;
                    case "Debug Log":
                        FileIO.OpenFile("config/debug_log.txt", this);
                        break;
                    case "Change Log":
                        FileIO.OpenFile("config/changelog.txt", this);
                        break;
                    case "recentlogs":
                        FileIO.OpenFile("config/recentlogs.txt", this);
                        break;
                    case "hardlogs":
                        FileIO.OpenFile("config/hardlogs.txt", this);
                        break;
                    case "appsettings":
                        FileIO.OpenFile("config/appsettings.txt", this);
                        break;
                    case "_boxes":
                    case "_commanders":
                    case "_fusions":
                    case "_singles":
                        FileIO.OpenFile(CONSTANTS.PATH_CARDADDONS + controlName + ".txt", this);
                        break;

                    // Gauntlet
                    case "openGauntletFileButton1":
                        string gauntletName = gauntletCustomDecksComboBox1.Text;
                        FileIO.OpenFile("data/customdecks_" + gauntletName + ".txt", this);
                        break;

                    // DeckFiles
                    case "openInventoryFileButton1":

                        foreach (var item in inventoryListBox1.SelectedItems)
                        {
                            FileIO.OpenFile("data/cards/" + item, this);
                            if (inventoryPossibleCardsCheckBox1.Checked)
                            {
                                var item2 = item.ToString().Replace(".txt", "_possible.txt");
                                FileIO.OpenFile("data/cards/" + item2, this);
                            }
                        }
                        foreach (var item in inventoryCardAddonsListBox1.SelectedItems)
                        {
                            FileIO.OpenFile(CONSTANTS.PATH_CARDADDONS + item, this);
                        }
                        break;
                    case "openInventoryFileButton2":
                        foreach (var item in inventoryListBox2.SelectedItems)
                        {
                            FileIO.OpenFile("data/cards/" + item, this);
                            if (inventoryPossibleCardsCheckBox2.Checked)
                            {
                                var item2 = item.ToString().Replace(".txt", "_possible.txt");
                                FileIO.OpenFile("data/cards/" + item2, this);
                            }
                        }
                        foreach (var item in inventoryCardAddonsListBox2.SelectedItems)
                        {
                            FileIO.OpenFile(CONSTANTS.PATH_CARDADDONS + item, this);
                        }
                        break;
                    case "openInventoryFileButton3":
                        foreach (var item in inventoryListBox3.SelectedItems)
                        {
                            FileIO.OpenFile("data/cards/" + item, this);
                            if (inventoryPossibleCardsCheckBox3.Checked)
                            {
                                var item2 = item.ToString().Replace(".txt", "_possible.txt");
                                FileIO.OpenFile("data/cards/" + item2, this);
                            }
                        }
                        foreach (var item in inventoryCardAddonsListBox3.SelectedItems)
                        {
                            FileIO.OpenFile(CONSTANTS.PATH_CARDADDONS + item, this);
                        }
                        break;
                    //case "openCardAddonsButton1":
                    //    foreach (var item in inventoryCardAddonsListBox1.SelectedItems)
                    //    {
                    //        FileIO.OpenFile(CONSTANTS.PATH_CARDADDONS + item, this);
                    //    }
                    //    break;
                    case "openCardAddonsButton2":
                        foreach (var item in inventoryCardAddonsListBox2.SelectedItems)
                        {
                            FileIO.OpenFile(CONSTANTS.PATH_CARDADDONS + item, this);
                        }
                        break;
                    case "openCardAddonsButton3":
                        foreach (var item in inventoryCardAddonsListBox3.SelectedItems)
                        {
                            FileIO.OpenFile(CONSTANTS.PATH_CARDADDONS + item, this);
                        }
                        break;

                    // Config files
                    case "level1":
                    case "level2":
                    case "level2reverse":
                    case "level3":
                    case "level3reverse":
                        FileIO.OpenFile(CONSTANTS.PATH_WHITELIST + controlName + ".txt", this);
                        break;

                    // New deck logs
                    case "decklogs":
                        FileIO.OpenFile("./config/decklogs.txt", this);
                        break;
                    // Old deck logs
                    case "rumBarrelOpenDeckLogs":
                        FileIO.OpenFile("./config/recentlogs.txt", this);
                        break;

                    // Custom cards files
                    case "customcards1":
                    case "customcards2":
                    case "customcards3":
                    case "customcards4":
                    case "customcards5":
                    case "customcards6":
                        FileIO.OpenFile("./config/" + controlName + ".txt", this);
                        break;

                    // Custom file
                    case "cardpower":
                        FileIO.OpenFile("./config/" + controlName + ".txt", this);
                        break;

                    // Folders
                    case "openInventoryButton1":
                    case "openInventoryButton2":
                    case "openInventoryButton3":
                        Process.Start(@"data\cards\");
                        break;
                    case "openCardAddonsFolderButton1":
                    case "openCardAddonsFolderButton2":
                    case "openCardAddonsFolderButton3":
                        Process.Start(@"config\card-addons\");
                        break;

                    default:
                        break;
                }
            }
            catch (FileNotFoundException ex)
            {
                Helper.Popup(this, ":(", "Can't find file" + ex);
            }
            catch (Exception ex)
            {
                Helper.Popup(this, ":(", "Error finding a file" + ex);
            }
        }

        // Top Menu - Update dropdown (repull xml, or gauntlets)
        private async void updateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var control = (ToolStripMenuItem)sender;
            var controlName = control.Text;
            try
            {
                switch (controlName)
                {
                    case "Card XML":
                        var result = XmlManager.PullGameXml(this, forceUpdate: true);
                        outputTextBox.AppendText("\r\n" + result + "\r\n");
                        if (result != "Xml: No changes detected")
                        {
                            // Modify card xml with custom cards
                            XmlManager.ModifyCardXML(this);

                            // Rebuild card database
                            await CardManager.BuildCardDatabase(this);

                            // Refresh card view tab
                            CardManager.FilterCards(this);
                        }
                        break;
                    case "Gauntlets":
                        outputTextBox.AppendText("\r\n" + await DbManager.DownloadFiles(this) + "\r\n");
                        _ControlSetup.RefreshGauntlets(this);

                        break;
                    // TODO: Reimplement this and pull from database
                    //case "Player Cards":
                    //    inventoryListBox1.Items.Clear();
                    //    inventoryListBox2.Items.Clear();
                    //    inventoryListBox3.Items.Clear();

                    //    outputTextBox.AppendText("\r\n" + PlayerManager.ImportPlayerCards(this) + "\r\n");
                    //    _ControlSetup.RefreshPlayers(this);
                    //    break;
                    // TODO: Point this to decklogs
                    case "RumBarrel Logs":
                        outputTextBox.Text += "Downloading Logs\r\n";
                        result = DbManager.DownloadLogs(this);
                        outputTextBox.Text += result + "\r\n";

                        break;

                    case "Refresh __users.txt":

                        rumBarrelPlayerComboBox.Items.Clear();
                        adminPlayerListBox.Items.Clear();

                        // Reads settings from API, if it exists
                        if (File.Exists("./__users.txt"))
                        {
                            List<string> userApiStrings = FileIO.SimpleRead(this, "./__users.txt", returnCommentedLines: false);
                            foreach (var user in userApiStrings)
                            {
                                // Add to rumBarrel dropdown
                                rumBarrelPlayerComboBox.Items.Add(user);

                                // Add to admin dropdown
                                adminPlayerListBox.Items.Add(user);
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Helper.Popup(this, "updateToolStripMenuItem_Click(): " + ex.Message);
            }
        }

        // Option for this app to minimize to taskbar instead of window
        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (CONFIG.minimizeToTray)
            {
                if (this.WindowState == FormWindowState.Minimized)
                {
                    pirateTUO.Visible = true; // This is the icon
                    this.ShowInTaskbar = false;
                }
            }
        }

        // Restore to tray
        private void notifyIcon1_MouseDoubleClick(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            pirateTUO.Visible = false;
        }

        // Toggle whether the app minimizes to tray or desktop
        private void toggleMinimizeToTrayToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CONFIG.minimizeToTray = !CONFIG.minimizeToTray;
            Properties.Settings.Default["minimizeToTray"] = CONFIG.minimizeToTray.ToString();
        }

        // Toggle whether to keep sim window on top
        private void windowOnTopToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CONFIG.stayOnTop = !CONFIG.stayOnTop;
            this.TopMost = CONFIG.stayOnTop;
        }

        #endregion

        #region Events - Queue/Run Sims

        /// <summary>
        /// Sim Tab - Launch a single sim outside of the app running tuo/tuo.exe
        /// </summary>
        private void simButton_Click(object sender, EventArgs e)
        {

        }

        /// <summary>
        /// Sim Tab - Launch a single sim outside of the app running tuo/tuo.exe
        /// 
        /// * Right click: Hidden option that overrides the sim operation with "reorder"
        /// </summary>
        private void simButton_Click(object sender, MouseEventArgs e)
        {
            string selectedTabName = mainTabControl.SelectedTab.Name;
            if (selectedTabName == "basicSimTab") selectedTabName = basicSimTabControl.SelectedTab.Name;

            // Right click - reorder instead of the selected operation
            bool useReorderOperation = e.Button == MouseButtons.Right;

            // Build the sim and run it
            List<string> sims = SimManager.BuildSimString(this, selectedTabName, overrideOperationWithReorder: useReorderOperation);
            if (sims.Count > 0) SimManager.RunOneSim(this, sims);
        }

        /// <summary>
        /// Output Tab - Queue sim button - added a sim string to the queued textbox
        /// </summary>
        private void outputQueueSimButton_Click(object sender, EventArgs e)
        {
            string selectedTab = mainTabControl.SelectedTab.Name;
            if (selectedTab == "basicSimTab") selectedTab = basicSimTabControl.SelectedTab.Name;

            var sims = SimManager.BuildSimString(this, selectedTab);

            foreach (var sim in sims)
            {
                outputQueuedTextBox.Text += sim + "\r\n";
            }
        }

        /// <summary>
        /// Output Tab - Runs all lines in the QueuedSims (outputQueuedTextBox) textbox
        /// </summary>
        private void outputRunSimButton_Click(object sender, EventArgs e)
        {
            this.stopProcess = false;
            this.workerThread = new Thread(new ThreadStart(this.runBatchSims));
            this.workerThread.Start();
        }

        /// <summary>
        /// Run batch sims (in a separate thread to not lock up the app)
        /// </summary>
        private void runBatchSims()
        {
            var simsInQueue = outputQueuedTextBox.Text.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            SimManager.RunQueuedSims(this, simsInQueue);
        }

        /// <summary>
        /// Kills all queued sims
        /// </summary>
        private void outputStopSimButton_Click(object sender, EventArgs e)
        {
            outputTextBox.AppendText("-- ------------------------------------------------ --\r\n");
            outputTextBox.AppendText("-- KILLED queued sims except currently running ones!--\r\n");
            outputTextBox.AppendText("-- ------------------------------------------------ --\r\n");
            this.stopProcess = true;
        }

        #endregion


        // ---- Basic Tab Events ----

        #region Events - Sim Tabs

        /// <summary>
        /// Sim Tab 1/2/3 - A shortcut Buttons sets many of the tab's values (e.g. "Brawl Attack")
        /// </summary>
        private void presetButton_Click(object sender, EventArgs e)
        {
            var control = (Button)sender;
            var controlName = control.Name;
            _ControlSetup.SimShortcutButton(this, controlName);
        }

        /// <summary>
        /// Sim Tab (and Batch Sim) - Refresh gauntlet listboxes
        /// </summary>
        private void refreshButton_Click(object sender, EventArgs e)
        {
            var control = (Button)sender;
            var controlName = control.Name;

            // Changing customdecks changes gauntletListBox
            if (controlName.Contains("gauntletRefreshButton") || controlName.Contains("batchSimRefreshButton") || 
                controlName.Contains("navSimRefreshButton") || controlName.Contains("firstCouncilSimRefresh"))
            {
                _ControlSetup.RefreshGauntlets(this);
            }
        }


        /// <summary>
        /// Change the Dominion label when the Dominion combobox changes
        /// </summary>
        private void myDominionComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            ComboBox control = (ComboBox)sender;
            string controlName = control.Name;
            string controlValue = control.Text;

            var i = 1;
            if (controlName.Contains("2")) i = 2;
            else if (controlName.Contains("3")) i = 3;

            try
            {
                // Dominion dropdown is empty
                if (string.IsNullOrEmpty(Helper.GetControlText(this, "myDominionComboBox" + i))) return;

                myDominionLabel1.Text = CardManager.CardToString(controlValue, includeName: false, includeType: false, includeStats: true, includeMetadata: false);
            }
            catch { }

        }

        /// <summary>
        /// Batch sim - Select decks in inventory based on the player dropdown. Waits a moment before searching
        /// </summary>
        private void batchSimShortcutGuildComboBox_textChanged(object sender, EventArgs e)
        {
            searchbarAssistant.TextChanged();
        }

        /// <summary>
        /// Sim Tab - Helper to wait a short while when typing in the search field
        /// </summary>
        private void assistant_Idled(object sender, EventArgs e)
        {
            this.Invoke(
            new MethodInvoker(() =>
            {
                BatchSimSearchFilter();
            }));
        }

        /// <summary>
        /// Batch sim - Filter to preselect some players/inventories
        /// </summary>
        private void BatchSimSearchFilter()
        {
            try
            {
                if (string.IsNullOrEmpty(batchSimShortcutGuildComboBox.Text.Trim())) return;

                // If the search string uses a ":", use the data after it
                var shortcutText = batchSimShortcutGuildComboBox.Text.Split(':');
                var searchType = "Player";
                string[] searchFilter;

                if (shortcutText.Length > 1)
                {
                    searchType = shortcutText[0];
                    searchFilter = shortcutText[1].Split(',');
                }
                else
                {
                    searchFilter = shortcutText[0].Split(',');
                }

                var players = batchSimInventoryListBox.Items;
                batchSimInventoryListBox.SelectedItems.Clear();

                for (int i = 0; i < players.Count; i++)
                {
                    var player = players[i].ToString();

                    // Guild
                    if (searchType == "Guild")
                    {
                        if (!searchFilter.Contains(",") && player.StartsWith(searchFilter[0]))
                        {
                            batchSimInventoryListBox.SetSelected(i, true);
                        }
                    }
                    // Other
                    else
                    {
                        foreach (var searchItem in searchFilter)
                        {
                            if (player.ToLower().Contains(searchItem.ToLower()))
                            {
                                batchSimInventoryListBox.SetSelected(i, true);
                                break;
                            }
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Batch sim - special sim options that reorder a list of decks in the batchSim input window
        /// </summary>
        private void batchSimSpecialSimComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            var specialSim = batchSimSpecialSimComboBox.Text;
            var sims = new List<string>();
            var simLines = new StringBuilder();

            if (specialSim.Contains("spamScore"))
            {
                sims = SimManager.BuildSimString(this, specialSim);

                foreach (var sim in sims)
                {
                    simLines.AppendLine(sim);
                }
                outputQueuedTextBox.Text += simLines.ToString();
            }
            else if (specialSim == "PvP Farming Deck") {
                sims = SimManager.BuildSimString(this, "pvpFarming");
                foreach (var sim in sims)
                {
                    outputQueuedTextBox.Text += sim + "\r\n";
                }
            }
            else if (specialSim == "Reorder Decklists")
            {
                var decks = outputQueuedTextBox.Text.Split(new[] { '\r', '\n' });
                var result = new StringBuilder();

                foreach (var d in decks)
                {
                    if (String.IsNullOrWhiteSpace(d)) continue;
                    var deck = d;
                    deck = Regex.Replace(d, ":DT", "");
                    deck = Regex.Replace(d, ":TW", "");
                    deck = Regex.Replace(d, ":WH", "");
                    deck = Regex.Replace(d, ":LK", "");
                    deck = Regex.Replace(d, ":Unknown", "");
                    deck = Regex.Replace(d, ".*\\|", "");
                    deck = Regex.Replace(d, ".*\\:", "");

                    result.Append("tuo \"");
                    result.Append(deck + " ");
                    result.Append("\" ");
                    result.Append("\"" + batchSimGauntletListBox.Text + "\" ");
                    result.Append("yf \"" + batchSimMyFortComboBox.Text + "\" ");
                    result.Append("ef \"" + batchSimEnemyFortComboBox.Text + "\" ");
                    result.Append("-e \"" + batchSimBgeComboBox.Text + "\" ");
                    result.Append("ye \"" + batchSimYourBgeComboBox.Text + "\" ");
                    result.Append("ee \"" + batchSimEnemyBgeComboBox.Text + "\" ");
                    result.Append("_" + batchSimCustomDecksComboBox.Text + " ");
                    result.Append("surge ordered reorder 100 mis 5");
                    result.Append("\r\n");
                }

                outputQueuedTextBox.Text = result.ToString();
            }
            else if (specialSim == "Flex Decklists")
            {
                var decks = outputQueuedTextBox.Text.Split(new[] { '\r', '\n' });
                var result = new StringBuilder();

                foreach (var d in decks)
                {
                    if (String.IsNullOrWhiteSpace(d)) continue;
                    var deck = d;
                    deck = Regex.Replace(d, ":DT", "");
                    deck = Regex.Replace(d, ":TW", "");
                    deck = Regex.Replace(d, ":WH", "");
                    deck = Regex.Replace(d, ":LK", "");
                    deck = Regex.Replace(d, ":Unknown", "");
                    deck = Regex.Replace(d, ".*\\|", "");
                    deck = Regex.Replace(d, ".*\\:", "");

                    result.Append("tuo \"");
                    result.Append(deck + " ");
                    result.Append("\" ");
                    result.Append("\"" + batchSimGauntletListBox.Text + "\" ");
                    result.Append("yf \"" + batchSimMyFortComboBox.Text + "\" ");
                    result.Append("ef \"" + batchSimEnemyFortComboBox.Text + "\" ");
                    result.Append("-e \"" + batchSimBgeComboBox.Text + "\" ");
                    result.Append("ye \"" + batchSimYourBgeComboBox.Text + "\" ");
                    result.Append("ee \"" + batchSimEnemyBgeComboBox.Text + "\" ");
                    result.Append("_" + batchSimCustomDecksComboBox.Text + " ");
                    result.Append("gw flexible flexible-iter 30 flexible-turn 4 sim 100 -v");
                    result.Append("\r\n");
                }

                outputQueuedTextBox.Text = result.ToString();
            }
            else if (specialSim == "Flex Defense")
            {
                var decks = outputQueuedTextBox.Text.Split(new[] { '\r', '\n' });
                var result = new StringBuilder();

                foreach (var d in decks)
                {
                    if (String.IsNullOrWhiteSpace(d)) continue;
                    var deck = d;
                    deck = Regex.Replace(d, ":DT", "");
                    deck = Regex.Replace(d, ":TW", "");
                    deck = Regex.Replace(d, ":WH", "");
                    deck = Regex.Replace(d, ":LK", "");
                    deck = Regex.Replace(d, ":Unknown", "");
                    deck = Regex.Replace(d, ".*\\|", "");
                    deck = Regex.Replace(d, ".*\\:", "");

                    result.Append("tuo \"");
                    result.Append(deck + " ");
                    result.Append("\" ");
                    result.Append("\"" + batchSimGauntletListBox.Text + "\" ");
                    result.Append("yf \"" + batchSimMyFortComboBox.Text + "\" ");
                    result.Append("ef \"" + batchSimEnemyFortComboBox.Text + "\" ");
                    result.Append("-e \"" + batchSimBgeComboBox.Text + "\" ");
                    result.Append("ye \"" + batchSimYourBgeComboBox.Text + "\" ");
                    result.Append("ee \"" + batchSimEnemyBgeComboBox.Text + "\" ");
                    result.Append("_" + batchSimCustomDecksComboBox.Text + " ");
                    result.Append("pvp enemy:flexible flexible-iter 25 flexible-turn 4 sim 100 -v");
                    result.Append("\r\n");
                }

                outputQueuedTextBox.Text = result.ToString();
            }
        }

        /// <summary>
        /// When clicking "MyDeck", this formats that deck to pirate format
        /// </summary>
        private void myDeckFormatLabel_Click(object sender, EventArgs e)
        {
            try
            {
                var control = (Label)sender;
                var controlName = control.Name;

                int i = 1;
                if (controlName.Contains("2")) i = 2;
                else if (controlName.Contains("3")) i = 3;

                string deck = Helper.GetTextBox(this, "myDeckTextBox" + i).Text;

                string playerFile = Helper.GetListBox(this, "inventoryListBox" + i).Text ?? "TW_KongName.txt";
                playerFile = playerFile.Replace(".txt", "");

                // Should split "DT_Fatal_Turnip" into "DT" and "Fatal_Turnip"
                string[] playerGuild = playerFile.Split(new char[] { '_' }, 3);

                string player = playerGuild[playerGuild.Length - 1];

                // Clean deck string
                deck = TextCleaner.CleanDeckString(deck);

                // * This was needed to set in ogrelicious 

                // Clip dominion
                // deck = TextCleaner.DeckRemoveDominions(deck);

                // Add -1 to commanders
                // deck = TextCleaner.DeckHyphenQuadCommanders(deck);


                deck = player + ":XX: " + deck; 

                Helper.GetTextBox(this, "myDeckTextBox" + i).Text = deck;
            }
            catch (Exception ex)
            {
                ControlExtensions.InvokeEx(outputTextBox, x => outputTextBox.AppendText("Deck formatter threw up: " + ex.Message + "\r\n"));
            }
        }


        /// <summary>
        /// Batch sim - special sim options that will take combinatorics of relevant cards in the meta
        /// </summary>
        private void firstCouncilQueueSimsButton_Click(object sender, EventArgs e)
        {
            StringBuilder simLines = new StringBuilder();

            // String components to make a deck
            StringBuilder deck = new StringBuilder();
            string commander = "Daedalus Charged";
            string dominion = "Broodmother's Nexus"; //TODO: Custom generic dom (Wall, OL1, Sunder 13)

            if (firstCouncilUseCustomComDomCheckBox.Checked)
            {
                commander = "Testcom";
                dominion = "Testdom";
            }

            // Deck combos
            bool simOneCardDecks = firstCouncilOneCardDeckCheckBox.Checked;
            bool simTwoCardDecks = firstCouncilTwoCardDeckCheckBox.Checked;
            bool simThreeCardDecks = firstCouncilThreeCardDeckCheckBox.Checked;
            bool simFourCardDecks = firstCouncilFourCardDeckCheckBox.Checked;
            bool simFiveCardDecks = firstCouncilFiveCardDeckCheckBox.Checked;

            // Cards to include
            bool simFreeCards = firstCouncilIncludeF2pCardsCheckBox.Checked;
            bool simBoxCards = firstCouncilIncludeBoxCardsCheckBox.Checked;
            bool simCustomFreeCards = firstCouncilIncludeCustomF2pCardsCheckBox.Checked;
            bool simCustomBoxCards = firstCouncilIncludeCustomBoxCardsCheckBox.Checked;

            bool simImpCards = firstCouncilImperialCheckBox.Checked;
            bool simRaiderCards = firstCouncilRaiderCheckBox.Checked;
            bool simBloodthirstyCards = firstCouncilBloodthirstyCheckBox.Checked;
            bool simXenoCards = firstCouncilXenoCheckBox.Checked;
            bool simRighteousCards = firstCouncilRighteousCheckBox.Checked;
            bool simProgenitorCards = firstCouncilProgenitorCheckBox.Checked;

            bool simMythicCards = firstCouncilMythicCheckBox.Checked;
            bool simVindicatorCards = firstCouncilVindCheckBox.Checked;
            bool simLegendaryCards = firstCouncilLegendCheckBox.Checked;
            bool simEpicCards = firstCouncilEpicCheckBox.Checked;

            bool climbDominions = firstCouncilSimDominionsCheckBox.Checked;
            bool climbCommanders = firstCouncilSimCommandersCheckBox.Checked;
            bool excludeWeirdCombos = firstCouncilExcludeWeirdCombosCheckBox.Checked;
            bool onlySimCustomCards = firstCouncilOnlySimForCustomCardsCheckBox.Checked;

            // Sim options
            string gameModeOperation = firstCouncilSimGameModeOperationComboBox.Text;
            int.TryParse(firstCouncilSimIterationsTextBox.Text, out int iterations);
            int.TryParse(firstCouncilSimCpuTextBox.Text, out int cpus);


            // Some deck manipulation based on settings
            // * Note: for custom cards - Use set=2500 for f2p rewards. Use set=2000 for custom box cards
            List<Card> allCards = new List<Card>();
            List<Card> freeCards = new List<Card>();
            List<Card> customFreeCards = new List<Card>();
            List<Card> boxCards = new List<Card>();
            List<Card> customBoxCards = new List<Card>();
            List<Card> cardsToSim = new List<Card>();

            Sim sim;
            string simTemplate = "";

            // -----------------------------
            // Create card lists
            // -----------------------------
            allCards = CardManager.GetCardTable().Values
                            .Where(x => x.Fusion_Level == 2)
                            .Where(x => x.Power >= 0 || 
                                        (x.Section >= 19 && x.Subset == "")) // Any card with power, or custom card in section19 without a subset                                                                                             
                            .Where(x => x.Set == 2500 || x.Set == 2000)
                            .Where(x => (x.Faction == 1 && simImpCards) ||
                                        (x.Faction == 2 && simRaiderCards) ||
                                        (x.Faction == 3 && simBloodthirstyCards) ||
                                        (x.Faction == 4 && simXenoCards) ||
                                        (x.Faction == 5 && simRighteousCards) ||
                                        (x.Faction == 6 && simProgenitorCards))
                            .Where(x => (x.Rarity == 6 && simMythicCards) ||
                                        (x.Rarity == 5 && simVindicatorCards) ||
                                        (x.Rarity == 4 && simLegendaryCards) ||
                                        (x.Rarity == 3 && simEpicCards))
                            .ToList();

            freeCards = allCards
                .Where(x => (x.Set == 2500 && x.Subset != "") ||
                            (x.Set == 2000 && (x.Subset == CardSubset.PvE_PvP_Reward.ToString() || x.Subset == CardSubset.PvE_Reward.ToString() || x.Subset == CardSubset.PvP_Reward.ToString())))
                .ToList();

            customFreeCards = allCards
                .Where(x => x.Section == 19)
                .Where(x => x.Set == 2500)
                .Where(x => x.Subset == "")
                .ToList();

            boxCards = allCards
                .Where(x => x.Set == 2000)
                .Where(x => x.Subset == CardSubset.Box.ToString() || x.Subset == CardSubset.Chance.ToString() || x.Subset == CardSubset.Cache.ToString())
                .ToList();
            
            customBoxCards = allCards
                .Where(x => x.Section == 19)
                .Where(x => x.Set == 2000)
                .Where(x => x.Subset == "")
                .ToList();


            // Figure out what cards are being included
            if (simFreeCards) cardsToSim.AddRange(freeCards);
            if (simBoxCards) cardsToSim.AddRange(boxCards);
            if (simCustomFreeCards) cardsToSim.AddRange(customFreeCards);
            if (simCustomBoxCards) cardsToSim.AddRange(customBoxCards);

            // Overrides from firstCouncilInclude/ExcludeCardsTextBox
            List<string> includedCards = firstCouncilSimIncludeCardsTextBox.Text.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            List<string> excludedCards = firstCouncilSimExcludeCardsTextBox.Text.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            List<string> forceIncludedCards = firstCouncilSimForceIncludeCardsTextBox.Text.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            bool mustForceCards = forceIncludedCards.Count > 0;
            bool mustForceAllCards = firstCouncilMustIncludeAllCheckBox.Checked;

            foreach (var cardName in includedCards)
            {
                Card c = CardManager.GetPlayerCardByName(cardName.Trim());
                if (c != null && cardsToSim.Contains(c) == false)
                {
                    cardsToSim.Add(c);
                }
            }
            foreach (var cardName in excludedCards)
            {
                Card c = CardManager.GetPlayerCardByName(cardName.Trim());
                if (c != null && cardsToSim.Contains(c))
                {
                    cardsToSim.Remove(c);
                }
            }


            cardsToSim = cardsToSim
                .OrderBy(x => x.Subset == "")
                .OrderBy(x => x.Set == 2000)
                .ThenByDescending(x => x.Rarity)
                .ThenBy(x => x.Name)
                .ToList();

            // -----------------------------
            // Create the base sim string
            // -----------------------------
            {
                try
                {
                    string gameMode = "surge";
                    string operation = "sim 500";
                    if (iterations < 10) iterations = 10;
                    if (cpus > 40 || cpus < 0) cpus = 4;

                    // TODO: whitelists, whether to go random/flex
                    switch (gameModeOperation)
                    {
                        case "Surge Sim - Random":
                            gameMode = "surge";
                            operation = "sim " + iterations;
                            break;
                        case "Surge Sim - Flex T2":
                            gameMode = "surge flexible flexible-iter " + 15 + " flexible-turn 2";
                            operation = "sim " + iterations;
                            break;
                        case "Surge Sim - Flex T3":
                            gameMode = "surge flexible flexible-iter " + 15 + " flexible-turn 3";
                            operation = "sim " + iterations;
                            break;
                        case "Surge Sim - Flex T4":
                            gameMode = "surge flexible flexible-iter " + 15 + " flexible-turn 4";
                            operation = "sim " + iterations;
                            break;
                        case "Surge Sim - Flex T5":
                            gameMode = "surge flexible flexible-iter " + 15 + " flexible-turn 5";
                            operation = "sim " + iterations;
                            break;
                        case "Surge Sim - Flex T6":
                            gameMode = "surge flexible flexible-iter " + 15 + " flexible-turn 6";
                            operation = "sim " + iterations;
                            break;
                        case "Surge Sim - Eval":
                            gameMode = "surge evaluate ";
                            operation = "sim " + iterations;
                            break;
                        case "Surge Sim - Eval2":
                            gameMode = "surge eval2 ";
                            operation = "sim " + iterations;
                            break;

                        case "Surge Climb - Random":
                            gameMode = "surge";
                            operation = "climb " + iterations;
                            break;
                        case "Surge Climb - Ordered":
                            gameMode = "surge";
                            operation = "ordered climb " + iterations;
                            break;
                        case "Surge Climb - Flex T2":
                            gameMode = "surge flexible flexible-iter " + 15 + " flexible-turn 2";
                            operation = "climb " + iterations;
                            break;
                        case "Surge Climb - Flex T3":
                            gameMode = "surge flexible flexible-iter " + 15 + " flexible-turn 3";
                            operation = "climb " + iterations;
                            break;
                        case "Surge Climb - Flex T4":
                            gameMode = "surge flexible flexible-iter " + 15 + " flexible-turn 4";
                            operation = "climb " + iterations;
                            break;
                        case "Surge Climb - Flex T5":
                            gameMode = "surge flexible flexible-iter " + 15 + " flexible-turn 5";
                            operation = "climb " + iterations;
                            break;
                        case "Surge Climb - Flex T6":
                            gameMode = "surge flexible flexible-iter " + 15 + " flexible-turn 6";
                            operation = "climb " + iterations;
                            break;
                        case "Surge Climb - Eval":
                            gameMode = "surge evaluate ";
                            operation = "climb " + iterations;
                            break;
                        case "Surge Climb - Eval2":
                            gameMode = "surge eval2 ";
                            operation = "climb " + iterations;
                            break;


                        case "Defense Sim - Random":
                            gameMode = "defense";
                            operation = "sim " + iterations;
                            break;
                        case "Defense Sim - Flex T2":
                            gameMode = "defense flexible flexible-iter " + 15 + " flexible-turn 2";
                            operation = "sim " + iterations;
                            break;
                        case "Defense Sim - Flex T3":
                            gameMode = "defense flexible flexible-iter " + 15 + " flexible-turn 3";
                            operation = "sim " + iterations;
                            break;
                        case "Defense Sim - Flex T4":
                            gameMode = "defense flexible flexible-iter " + 15 + " flexible-turn 4";
                            operation = "sim " + iterations;
                            break;
                        case "Defense Sim - Flex T5":
                            gameMode = "defense flexible flexible-iter " + 15 + " flexible-turn 5";
                            operation = "sim " + iterations;
                            break;
                        case "Defense Sim - Flex T6":
                            gameMode = "defense flexible flexible-iter " + 15 + " flexible-turn 6";
                            operation = "sim " + iterations;
                            break;
                        case "Defense Sim - Eval":
                            gameMode = "defense enemy:evaluate ";
                            operation = "sim " + iterations;
                            break;
                        case "Defense Sim - Eval2":
                            gameMode = "defense enemy:eval2 ";
                            operation = "sim " + iterations;
                            break;


                        case "Defense Climb - Random":
                            gameMode = "defense";
                            operation = "climb " + iterations;
                            break;
                        case "Defense Climb - Ordered":
                            gameMode = "defense";
                            operation = "ordered climb " + iterations;
                            break;
                        case "Defense Climb - Flex T2":
                            gameMode = "defense flexible flexible-iter " + 15 + " flexible-turn 2";
                            operation = "climb " + iterations;
                            break;
                        case "Defense Climb - Flex T3":
                            gameMode = "defense flexible flexible-iter " + 15 + " flexible-turn 3";
                            operation = "climb " + iterations;
                            break;
                        case "Defense Climb - Flex T4":
                            gameMode = "defense flexible flexible-iter " + 15 + " flexible-turn 4";
                            operation = "climb " + iterations;
                            break;
                        case "Defense Climb - Flex T5":
                            gameMode = "defense flexible flexible-iter " + 15 + " flexible-turn 5";
                            operation = "climb " + iterations;
                            break;
                        case "Defense Climb - Eval":
                            gameMode = "defense enemy:evaluate ";
                            operation = "climb " + iterations;
                            break;
                        case "Defense Climb - Eval2":
                            gameMode = "defense enemy:eval2 ";
                            operation = "climb " + iterations;
                            break;
                    }

                    string ownedCards = "";

                    if (climbCommanders) ownedCards += "-o=\"" + CONSTANTS.PATH_CARDADDONS + "x-Commanders.txt\" ";
                    if (climbDominions) ownedCards += "-o=\"" + CONSTANTS.PATH_CARDADDONS + "x-Dominions-Strong.txt\" ";


                    sim = new Sim
                    {
                        // Decks
                        MyDeck = new Deck("<REPLACEME>"),
                        EnemyDeck = (firstCouncilSimGauntletListBox.SelectedItems.Count > 0) ? firstCouncilSimGauntletListBox.SelectedItems[0].ToString() : "",

                        // Forts and BGEs
                        MyForts = firstCouncilSimMyFortComboBox.Text,
                        EnemyForts = firstCouncilSimEnemyFortComboBox.Text,
                        Bge = firstCouncilSimBgeComboBox.Text,
                        MyBge = firstCouncilSimYourBgeComboBox.Text,
                        EnemyBge = firstCouncilSimEnemyBgeComboBox.Text,
                        //MyDominion = dominion,

                        // GameMode / Operation
                        GameMode = gameMode,
                        Operation = operation,

                        // Addons
                        OwnedCards = ownedCards,

                        // Gauntlets and inventory
                        GauntletFile = "_" + firstCouncilSimCustomDecksComboBox.Text,

                        // Deck Limit

                        // Other flags                        
                        Verbose = false,
                        HarmonicMean = false,
                        CpuThreads = cpus,
                        //Fund = 0,
                        //ExtraTuoFlags = this.batchSimExtraFlagsTextBox.Text
                        ExtraTuoFlags = "mis 0.5"
                    };

                    //sim.GameMode.Replace("brawl", "gw");

                    simTemplate = sim.SimToString();
                }
                catch (Exception ex)
                {
                    outputTextBox.AppendText("firstCouncilQueueSimsButton_Click() - error when building out the sim: " + ex.Message);
                    return;
                }
            }

            // -----------------------------
            // Decks - one card combo
            // -----------------------------
            if (simOneCardDecks)
            {
                for (var i = 0; i < cardsToSim.Count; i++)
                {
                    Card cardOne = cardsToSim[i];
                    string cardName = cardOne.Name;

                    // OPTION - FORCE CARDS
                    if (onlySimCustomCards && cardOne.Subset != "")
                    {
                        continue;
                    }
                    if (mustForceCards && forceIncludedCards.Contains(cardOne.Name) == false)
                    {
                        continue;
                    }

                    // OPTION - EXCLUDE WEIRD CARD COMBOS
                    if (excludeWeirdCombos)
                    {
                        if (cardOne.CardType == "Structure") continue;
                    }


                    // Create the deck
                    deck = new StringBuilder();
                    deck.Append(commander + "," + dominion + "," + cardName + "#9");

                    // Merge the deck with the sim template, and add that to result
                    string simLine = simTemplate.Replace("<REPLACEME>", deck.ToString());
                    simLines.AppendLine(simLine + " -L 9 9");
                }
            }
            // -----------------------------
            // Decks - two card combo
            // -----------------------------
            if (simTwoCardDecks)
            {
                for (var i = 0; i < cardsToSim.Count; i++)
                {
                    for (var j = i + 1; j < cardsToSim.Count; j++)
                    {
                        var cardOne = cardsToSim[i];
                        var cardTwo = cardsToSim[j];

                        // OPTION - FORCE CARDS
                        if (onlySimCustomCards && cardOne.Subset != "" && cardTwo.Subset != "")
                        {
                            continue;
                        }
                        if (mustForceCards &&
                            forceIncludedCards.Contains(cardOne.Name) == false &&
                            forceIncludedCards.Contains(cardTwo.Name) == false)
                        {
                            continue;
                        }
                        if (mustForceCards && mustForceAllCards)
                        {
                            bool skipThisDeck = false;
                            foreach (var forceCard in forceIncludedCards)
                            {
                                if (forceCard != cardOne.Name &&
                                    forceCard != cardTwo.Name)
                                {
                                    skipThisDeck = true;
                                    break;
                                }
                            }
                            if (skipThisDeck) continue;
                        }
                        // OPTION - EXCLUDE WEIRD CARD COMBOS
                        if (excludeWeirdCombos)
                        {
                            // All structures
                            if (cardOne.CardType == "Structure" && cardTwo.CardType == "Structure") continue;

                            // Mismatched Allegiance
                            if ((cardOne.HasSkill("Allegiance") || cardTwo.HasSkill("Allegiance")) && cardOne.Faction != cardTwo.Faction) continue;

                            // Mismatched Legion
                            if ((cardOne.HasSkill("Legion") || cardTwo.HasSkill("Legion")) && cardOne.Faction != cardTwo.Faction) continue;

                            // Mismatched Coalition
                            if ((cardOne.HasSkill("Coalition") || cardTwo.HasSkill("Coalition")) && cardOne.Faction == cardTwo.Faction) continue;
                        }

                        // Create the deck
                        deck = new StringBuilder();
                        deck.Append(commander);
                        deck.Append(",");
                        deck.Append(dominion);
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
                        deck.Append(",");
                        deck.Append(cardOne.Name);
                        deck.Append(",");
                        deck.Append(cardTwo.Name);
                        deck.Append(",");
                        deck.Append(cardOne.Name);
                        deck.Append(",");
                        deck.Append(cardTwo.Name);

                        // Merge the deck with the sim template, and add that to result
                        string simLine = simTemplate.Replace("<REPLACEME>", deck.ToString());
                        simLines.AppendLine(simLine + " -L 10 10");
                    }//j
                }//i
            }
            // -----------------------------
            // Decks - three card combo
            // -----------------------------
            if (simThreeCardDecks)
            {
                for (var i = 0; i < cardsToSim.Count; i++)
                {
                    for (var j = i + 1; j < cardsToSim.Count; j++)
                    {
                        for (var k = j + 1; k < cardsToSim.Count; k++)
                        {
                            var cardOne = cardsToSim[i];
                            var cardTwo = cardsToSim[j];
                            var cardThree = cardsToSim[k];


                            // OPTION - FORCE CARDS
                            if (onlySimCustomCards && cardOne.Subset != "" && cardTwo.Subset != "" && cardThree.Subset != "")
                            {
                                continue;
                            }
                            if (mustForceCards && 
                                forceIncludedCards.Contains(cardOne.Name) == false && 
                                forceIncludedCards.Contains(cardTwo.Name) == false && 
                                forceIncludedCards.Contains(cardThree.Name) == false)
                            {
                                continue;
                            }
                            if (mustForceCards && mustForceAllCards)
                            {
                                bool skipThisDeck = false;
                                foreach (var forceCard in forceIncludedCards)
                                {
                                    if (forceCard != cardOne.Name &&
                                        forceCard != cardTwo.Name &&
                                        forceCard != cardThree.Name)
                                    {
                                        skipThisDeck = true;
                                        break;
                                    }
                                }
                                if (skipThisDeck) continue;
                            }
                            // OPTION - EXCLUDE WEIRD CARD COMBOS
                            if (excludeWeirdCombos)
                            {
                                // All structures
                                if (cardOne.CardType == "Structure" && cardTwo.CardType == "Structure" && cardThree.CardType == "Structure") continue;

                                // Mismatched Allegiance
                                if (cardOne.HasSkill("Allegiance") && cardOne.Faction != cardTwo.Faction && cardOne.Faction != cardThree.Faction) continue;
                                if (cardTwo.HasSkill("Allegiance") && cardTwo.Faction != cardOne.Faction && cardTwo.Faction != cardThree.Faction) continue;
                                if (cardThree.HasSkill("Allegiance") && cardThree.Faction != cardThree.Faction && cardOne.Faction != cardTwo.Faction) continue;

                                // Mismatched Legion
                                if (cardOne.HasSkill("Legion") && cardOne.Faction != cardTwo.Faction && cardOne.Faction != cardThree.Faction) continue;
                                if (cardTwo.HasSkill("Legion") && cardTwo.Faction != cardOne.Faction && cardTwo.Faction != cardThree.Faction) continue;
                                if (cardThree.HasSkill("Legion") && cardThree.Faction != cardOne.Faction && cardOne.Faction != cardTwo.Faction) continue;

                                // Mismatched Coalition
                                if (cardOne.HasSkill("Coalition") && cardOne.Faction == cardTwo.Faction && cardOne.Faction == cardThree.Faction) continue;
                                if (cardTwo.HasSkill("Coalition") && cardTwo.Faction == cardOne.Faction && cardTwo.Faction == cardThree.Faction) continue;
                                if (cardThree.HasSkill("Coalition") && cardThree.Faction == cardThree.Faction && cardOne.Faction == cardTwo.Faction) continue;
                            }

                            // Create the deck
                            deck = new StringBuilder();
                            deck.Append(commander);
                            deck.Append(",");
                            deck.Append(dominion);
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
                            string simLine = simTemplate.Replace("<REPLACEME>", deck.ToString());
                            simLines.AppendLine(simLine + " -L 9 9");
                        }//k
                    }//j
                }//i
            }

            // -----------------------------
            // Decks - four card combo (disgusting)
            // -----------------------------
            if (simFourCardDecks) 
            {
                for (var i = 0; i < cardsToSim.Count; i++)
                {
                    for (var j = i + 1; j < cardsToSim.Count; j++)
                    {
                        for (var k = j + 1; k < cardsToSim.Count; k++)
                        {
                            for (var l = k + 1; l < cardsToSim.Count; l++)
                            {
                                var cardOne = cardsToSim[i];
                                var cardTwo = cardsToSim[j];
                                var cardThree = cardsToSim[k];
                                var cardFour = cardsToSim[l];


                                // OPTION - FORCE CARDS
                                if (onlySimCustomCards && cardOne.Subset != "" && cardTwo.Subset != "" && cardThree.Subset != "" && cardFour.Subset != "")
                                {
                                    continue;
                                }

                                if (mustForceCards &&
                                    forceIncludedCards.Contains(cardOne.Name) == false &&
                                    forceIncludedCards.Contains(cardTwo.Name) == false &&
                                    forceIncludedCards.Contains(cardThree.Name) == false &&
                                    forceIncludedCards.Contains(cardFour.Name) == false)
                                {
                                    continue;
                                }
                                if (mustForceCards && mustForceAllCards)
                                {
                                    bool skipThisDeck = false;
                                    foreach (var forceCard in forceIncludedCards)
                                    {
                                        if (forceCard != cardOne.Name &&
                                            forceCard != cardTwo.Name &&
                                            forceCard != cardThree.Name &&
                                            forceCard != cardFour.Name)
                                        {
                                            skipThisDeck = true;
                                            break;
                                        }
                                    }
                                    if (skipThisDeck) continue;
                                }
                                // OPTION - EXCLUDE WEIRD CARD COMBOS
                                if (excludeWeirdCombos)
                                {
                                    if (cardOne.CardType == "Structure" && cardTwo.CardType == "Structure" && cardThree.CardType == "Structure" && cardFour.CardType == "Structure") continue;
                                }

                                // Create the deck
                                deck = new StringBuilder();
                                deck.Append(commander);
                                deck.Append(",");
                                deck.Append(dominion);
                                deck.Append(",");
                                deck.Append(cardOne.Name);
                                deck.Append(",");
                                deck.Append(cardTwo.Name);
                                deck.Append(",");
                                deck.Append(cardThree.Name);
                                deck.Append(",");
                                deck.Append(cardFour.Name);
                                deck.Append(",");
                                deck.Append(cardOne.Name);
                                deck.Append(",");
                                deck.Append(cardTwo.Name);
                                deck.Append(",");
                                deck.Append(cardThree.Name);
                                deck.Append(",");
                                deck.Append(cardFour.Name);

                                // Merge the deck with the sim template, and add that to result
                                string simLine = simTemplate.Replace("<REPLACEME>", deck.ToString());
                                simLines.AppendLine(simLine + " -L 8 8");
                            } //l
                        }//k
                    }//j
                }//i
            }

            // -----------------------------
            // Decks - four card combo (disgusting)
            // -----------------------------
            if (simFiveCardDecks)
            {
                if (cardsToSim.Count < 5) return;

                for (var i = 0; i < cardsToSim.Count; i++)
                {
                    for (var j = i + 1; j < cardsToSim.Count; j++)
                    {
                        for (var k = j + 1; k < cardsToSim.Count; k++)
                        {
                            for (var l = k + 1; l < cardsToSim.Count; l++)
                            {
                                for (var m = l + 1; m < cardsToSim.Count; m++)
                                {
                                    var cardOne = cardsToSim[i];
                                    var cardTwo = cardsToSim[j];
                                    var cardThree = cardsToSim[k];
                                    var cardFour = cardsToSim[l];
                                    var cardFive = cardsToSim[m];


                                    // OPTION - FORCE CARDS
                                    if (onlySimCustomCards && cardOne.Subset != "" && cardTwo.Subset != "" && cardThree.Subset != "" &&
                                        cardFour.Subset != "" && cardFive.Subset != "")
                                    {
                                        continue;
                                    }
                                    if (mustForceCards && forceIncludedCards.Contains(cardOne.Name) == false && forceIncludedCards.Contains(cardTwo.Name) == false &&
                                                          forceIncludedCards.Contains(cardThree.Name) == false && forceIncludedCards.Contains(cardFour.Name) == false &&
                                                          forceIncludedCards.Contains(cardFive.Name) == false)
                                    {
                                        continue;
                                    }
                                    if (mustForceCards && mustForceAllCards)
                                    {
                                        bool skipThisDeck = false;
                                        foreach (var forceCard in forceIncludedCards)
                                        {
                                            if (forceCard != cardOne.Name &&
                                                forceCard != cardTwo.Name &&
                                                forceCard != cardThree.Name &&
                                                forceCard != cardFour.Name &&
                                                forceCard != cardFive.Name)
                                            {
                                                skipThisDeck = true;
                                                break;
                                            }
                                        }
                                        if (skipThisDeck) continue;
                                    }
                                    // OPTION - EXCLUDE WEIRD CARD COMBOS
                                    if (excludeWeirdCombos)
                                    {
                                        if (cardOne.CardType == "Structure" && cardTwo.CardType == "Structure" && cardThree.CardType == "Structure" &&
                                            cardFour.CardType == "Structure" && cardFive.CardType == "Structure") continue;
                                    }

                                    // Create the deck
                                    deck = new StringBuilder();
                                    deck.Append(commander);
                                    deck.Append(",");
                                    deck.Append(dominion);
                                    deck.Append(",");
                                    deck.Append(cardOne.Name);
                                    deck.Append(",");
                                    deck.Append(cardTwo.Name);
                                    deck.Append(",");
                                    deck.Append(cardThree.Name);
                                    deck.Append(",");
                                    deck.Append(cardFour.Name);
                                    deck.Append(",");
                                    deck.Append(cardFive.Name);
                                    deck.Append(",");
                                    deck.Append(cardOne.Name);
                                    deck.Append(",");
                                    deck.Append(cardTwo.Name);
                                    deck.Append(",");
                                    deck.Append(cardThree.Name);
                                    deck.Append(",");
                                    deck.Append(cardFour.Name);
                                    deck.Append(",");
                                    deck.Append(cardFive.Name);

                                    // Merge the deck with the sim template, and add that to result
                                    string simLine = simTemplate.Replace("<REPLACEME>", deck.ToString());
                                    simLines.AppendLine(simLine + " -L 10 10");
                                } //m
                            } //l
                        }//k
                    }//j
                }//i
            }


            // Output the simlines to the queued window
            outputQueuedTextBox.AppendText(simLines.ToString());
        }

        //outputQueuedTextBox.Text = output

        #endregion

        #region Events - Cards Tab

        /// <summary>
        /// Players tab - List players who have the selected cards
        /// 
        /// TODO: ** DEPRECATED** Uses the old player manager players to search. Instead search through the new player inventories
        /// </summary>
        private void playerFilterSearchButton_Click(object sender, EventArgs e)
        {
            playerFilterListbox.Items.Clear();

            try
            {
                int playerCount = 0;
                string[] cardNames = playerFilterTextbox.Text.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                // Filters
                bool findPlayersWithoutCards = playerFilterInverseCheckBox.Checked;
                bool usePlayerOwnedCards = playerFilterUseOwnedCardsCheckBox.Checked;
                bool usePlayerPossibleCards = playerFilterUsePossibleCardsCheckBox.Checked;
                bool ignoreCommanders = playerFilterIgnoreCommandersCheckBox.Checked;
                bool ignoreDominions = playerFilterIgnoreDominionsCheckBox.Checked;

                // If look in owned/possible cards are both unchecked, default to owned
                if (!usePlayerOwnedCards && !usePlayerPossibleCards) usePlayerOwnedCards = true;


                // Guild filter: include all players, or just a guild
                List<Player> players = PlayerManager.Players
                    .Where(x => String.IsNullOrWhiteSpace(playerFilterGuildComboBox.Text) ||
                                x.Guild.Trim().ToLower() == playerFilterGuildComboBox.Text.Trim().ToLower())
                    .ToList();

                // Loop through players
                foreach (var player in players)
                {
                    bool playerHasCards = true;

                    // Does this player have each card in the list
                    foreach (string cardName in cardNames)
                    {
                        // Find the card object
                        KeyValuePair<string, int> formattedCard = CardManager.FormatCard(cardName, true);
                        Card card = CardManager.GetPlayerCardByName(formattedCard.Key);

                        if (card != null)
                        {
                            // Does this user have X copies of this card
                            int cardCount = formattedCard.Value;
                            int playerCardCount = 0;

                            // Option to ignore commanders/dominions
                            if (card.CardType == CardType.Commander.ToString() && ignoreCommanders) continue;
                            if (card.CardType == CardType.Dominion.ToString() && ignoreDominions) continue;

                            // Look in owned cards for X copies of this card
                            if (usePlayerOwnedCards && player.Cards.ContainsKey(card))
                            {
                                playerCardCount += player.Cards[card];
                            }

                            // Look in potential cards (if they exist) and add X copies of this card
                            if (usePlayerPossibleCards && player.PossibleCards.ContainsKey(card))
                            {
                                playerCardCount += player.PossibleCards[card];
                            }

                            // Player does not have this card (or enough of this card). Back out
                            if (playerCardCount < cardCount)
                            {
                                playerHasCards = false;
                                break;
                            }
                        }
                        else
                        {
                            outputTextBox.AppendText("CardSearch: Could not find this card: " + cardName);
                            return;
                        }
                    }

                    // This player has these cards - add it to the result list (or the inverse)
                    if (playerHasCards && !findPlayersWithoutCards)
                    {
                        playerFilterListbox.Items.Add(player.KongName);
                        playerCount++;
                    }
                    else if (!playerHasCards && findPlayersWithoutCards)
                    {
                        playerFilterListbox.Items.Add(player.KongName);
                        playerCount++;
                    }
                }

                playerFilterLabel.Text = playerCount + " Players";
            }
            catch (Exception ex)
            {
                ControlExtensions.InvokeEx(outputTextBox, x => outputTextBox.AppendText("playerFilterSearchButton_Click: " + ex.Message));
            }
        }

        /// <summary>
        /// Players tab - 
        /// Copy text in playerFilterListBox
        /// </summary>
        private void playerFilterCopyButton_Click(object sender, EventArgs e)
        {
            string s = "";
            foreach (object o in playerFilterListbox.Items)
            {
                s += o.ToString() + "\r\n";
            }
            Clipboard.SetText(s);
        }

        /// <summary>
        /// Select a player and display their cards
        /// 
        /// TODO: ** DEPRECATED** Uses the old player manager players to search. Instead search through the new player inventories
        /// </summary>
        private void playerFilterListbox_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                var player = PlayerManager.Players.Where(p => p.KongName == playerFilterListbox.SelectedItem.ToString()).FirstOrDefault();
                var cardList = new StringBuilder();
                var seedList = new StringBuilder();

                if (player != null)
                {
                    cardList.AppendLine("//Owned Cards");
                    foreach (var card in player.Cards)
                    {
                        cardList.AppendLine(card.Key.Name + (card.Value > 1 ? ("#" + card.Value) : ""));
                    }
                    cardList.AppendLine();

                    cardList.AppendLine("//Potential Cards");
                    foreach (var card in player.PossibleCards)
                    {
                        cardList.AppendLine(card.Key.Name + (card.Value > 1 ? ("#" + card.Value) : ""));
                    }
                    cardList.AppendLine();

                    cardList.AppendLine("//Weak Cards");
                    foreach (var card in player.WeakCards)
                    {
                        cardList.AppendLine(card.Key.Name + (card.Value > 1 ? ("#" + card.Value) : ""));
                    }
                    cardList.AppendLine();

                    cardList.AppendLine("//Unknown Cards");
                    foreach (var card in player.UnknownCards)
                    {
                        cardList.AppendLine(card.Key + (card.Value > 1 ? ("#" + card.Value) : ""));
                    }
                    cardList.AppendLine();


                    seedList.AppendLine("//Seeds");
                    foreach (var seed in player.ExternalSeeds)
                    {
                        seedList.AppendLine(seed);
                    }

                    playerFilterCardListTextbox.Text = cardList.ToString();
                    playerFilterSeedTextBox.Text = seedList.ToString();
                }
            }
            catch (Exception ex)
            {
                ControlExtensions.InvokeEx(outputTextBox, x => outputTextBox.AppendText("playerFilterListbox_SelectedIndexChanged: " + ex.Message));
            }
        }
        
        /// <summary>
        /// Cards Tab - Select one or more cards
        /// DEPRECATED
        /// </summary>
        private void cardFinderListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                var cardsToShow = new StringBuilder();

                //cardFinderFullListView.Items.Clear();

                if (cardFinderListView.SelectedItems.Count == 0) return;

                for (int i = 0; i < cardFinderListView.SelectedItems.Count; i++)
                {
                    string cardName = cardFinderListView.SelectedItems[i].Text;

                    Card card = CardManager.GetPlayerCardByName(cardName);
                    if (card != null)
                    {
                        var cardArray = new string[11];
                        cardArray[0] = card.Name;
                        cardArray[1] = ((Rarity)(card.Rarity)).ToString()[0].ToString();
                        cardArray[2] = ((Faction)(card.Faction)).ToString();
                        cardArray[3] = card.CardType.ToString();
                        cardArray[4] = card.Section + " " + card.Set + " " + card.Subset;
                        cardArray[5] = card.Attack.ToString();
                        cardArray[6] = card.Health.ToString();
                        cardArray[7] = card.Delay.ToString();
                        cardArray[8] = CardManager.GetSkillString(card.s1);
                        cardArray[9] = CardManager.GetSkillString(card.s2);
                        cardArray[10] = CardManager.GetSkillString(card.s3);

                        var item = new ListViewItem(cardArray);
                        switch (card.Faction)
                        {
                            case 1:
                                item.BackColor = Color.FromArgb(230, 230, 255);
                                break;
                            case 2:
                                item.BackColor = Color.FromArgb(220, 190, 180);
                                break;
                            case 3:
                                item.BackColor = Color.FromArgb(255, 230, 230);
                                break;
                            case 4:
                                item.BackColor = Color.FromArgb(128, 128, 128);
                                break;
                            case 5:
                                item.BackColor = Color.FromArgb(255, 255, 255);
                                break;
                            case 6:
                                item.BackColor = Color.FromArgb(255, 180, 255);
                                break;
                        }
                        //cardFinderFullListView.Items.Add(item);
                    }


                    if (cardFinderShowSkillsCheckBox.Checked && cardFinderShowInfoCheckBox.Checked)
                    {
                        cardsToShow.AppendLine(CardManager.CardToString(cardName, true, true, true, true, true, true));
                        cardsToShow.AppendLine();
                    }
                    // Show skills only
                    else if (cardFinderShowSkillsCheckBox.Checked)
                    {
                        cardsToShow.AppendLine(CardManager.CardToString(cardName, true, true, true, false));
                        cardsToShow.AppendLine();
                    }
                    // Show info only
                    else if (cardFinderShowInfoCheckBox.Checked)
                    {
                        cardsToShow.AppendLine(CardManager.CardToString(cardName, true, true, true, false));
                    }
                    // Hide everything except card name
                    else
                    {
                        cardsToShow.AppendLine(CardManager.CardToString(cardName, true, false, false, false));
                    }

                }

                cardFinderOutputTextBox.Text = cardsToShow.ToString();


            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "CardFinderListBox_selectedIndexChanged error: " + ex);
            }
        }


        #endregion

        #region Events - Gauntlet Tab

        /// <summary>
        /// gtBuilder shortcut buttons
        /// </summary>
        private void gtBuilderShortcutButton(object sender, EventArgs e)
        {
            var control = (Button)sender;
            var controlName = control.Name;
            Ogrelicious.GuildNameShortcutButton(this, controlName);
        }

        /// <summary>
        /// Build a gauntlet from a database of entries
        /// 
        /// ** DEPRECATED
        /// </summary>
        private void gtBuildGauntletButton_Click(object sender, EventArgs e)
        {
            var includeGuild = gtIncludeGuildCheckBox.Checked;
            var includeTime = gtIncludeTimeCheckBox.Checked;
            var mergePlayerDecks = gtMergePlayerDecksCheckBox.Checked;
            var ignoreAlliedGuilds = gtIgnoreAlliedGuildsCheckBox.Checked;
            var ignoreEnemyGuilds = gtIgnoreTop5CheckBox.Checked;
            var ignoreGuilds = new List<string>();

            //// Ignore allied guilds
            //if (ignoreAlliedGuilds)
            //{
            //    ignoreGuilds.AddRange(CONSTANTS.AlliedGuilds);
            //}
            //if (ignoreEnemyGuilds)
            //{
            //    ignoreGuilds.AddRange(CONSTANTS.EnemyGuilds);
            //}

            Ogrelicious.CreateGauntlet(this, includeGuild, includeTime, mergePlayerDecks, ignoreGuilds);
        }

        /// <summary>
        /// Get a list of cards to search for, and return players outside of our guilds
        /// </summary>
        private void gtGetRecruitsButton_Click(object sender, EventArgs e)
        {
            // Box/Chance cards from the last 2 sections
            // with sufficient power
            List<Card> whaleCards = CardManager.GetWhaleCards();

            var searchTerm = new StringBuilder();
            searchTerm.Append("-E '");

            foreach (var card in whaleCards)
            {
                var c = card.Name;
                c = c.Replace(" ", ".");
                c = c.Replace("'", ".");
                searchTerm.Append(c);
                searchTerm.Append("|");
            }

            searchTerm.Append("'");
            gtSearchTermComboBox.Text = searchTerm.ToString().Replace("|'", "'");
            gtOutputNameTextBox.Text = "";


            List<string> alliedGuilds = new List<string>(); // We have no allies
            Ogrelicious.CreateGauntlet(this, includeGuild: true, includeTime: false, mergePlayerDecks: true, ignoreGuilds: alliedGuilds);

        }

        /// <summary>
        /// Get only player names
        /// </summary>
        private void gtGetGuildRosterButton_Click(object sender, EventArgs e)
        {

        }

        #endregion

        // ---- Advanced Tab Events ----

        #region Events - Upload Tab

        /// <summary>
        /// Upload local gauntlet and card files to server
        /// </summary>
        private async void managerUploadButton_Click(object sender, EventArgs e)
        {
            if (CONFIG.role == "level3" || CONFIG.role == "newLevel3")
            {
                adminUploadTextBox.Text += await DbManager.UploadFiles(this) + "\r\n";
            }
            else
            {
                adminUploadTextBox.Text += "You don't have permission to upload. Contact an officer to have them update a gauntlet.\r\n";
            }
        }

        #endregion

        #region Events - Tools Tab

        /// <summary>
        /// Make a gauntlet out of a list of decks
        /// INPUT: name:x:deck:(guild)
        /// () = optional
        /// </summary>
        private void toolsMakeGauntletButton_Click(object sender, EventArgs e)
        {
            StringBuilder result = new StringBuilder();
            string gauntletName = !string.IsNullOrWhiteSpace(toolsGauntletNameTextBox.Text) ? toolsGauntletNameTextBox.Text : "Enemy_IsDefending";

            // Gauntlet regex
            result.AppendLine(gauntletName + ": /^" + gauntletName + "_.*$/");

            var lines = toolsInputTextBox.Text.Split('\n');
            var i = 1;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Split the line up by : or \t
                string[] deckParts = line.Trim().Split(new char[] { ':', '\t' });

                string actualDeck = "";

                if (deckParts.Length >= 3) actualDeck = deckParts[2];
                else if (deckParts.Length >= 2) actualDeck = deckParts[1];
                else actualDeck = deckParts[0];

                // Remove level 1-4 tags
                // Note this will break on stupid cards like Quartermaster XKR-2
                actualDeck = actualDeck.Replace("-1,", ",").Replace("-2,", ",").Replace("-3,", ",").Replace("-4", ",");

                // Add to result
                string output = gauntletName + "_" + i.ToString("D2") + ": " + actualDeck;
                result.AppendLine(output);
                i++;
            }

            toolsOutputTextBox.Text = result.ToString();
        }

        /// <summary>
        /// Make a seed name out of a list of decks
        /// INPUT - deck
        /// INPUT - name:deck
        /// INPUT - name:x:deck
        /// () = optional
        /// </summary>
        private void toolsMakeSeedButton_Click(object sender, EventArgs e)
        {
            StringBuilder result = new StringBuilder();
            string gauntletName = !string.IsNullOrWhiteSpace(toolsSeedNameTextBox.Text) ? toolsSeedNameTextBox.Text : "Enemy_IsDefending";

            var lines = toolsInputTextBox.Text.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Split the line up by : or \t
                string[] deckParts = line.Trim().Split(new char[] { ':', '\t' });

                string actualDeck = "";
                string playerName = "";

                if (deckParts.Length >= 3)
                {
                    actualDeck = deckParts[2];
                    playerName = deckParts[0];
                }
                else if (deckParts.Length >= 2)
                {
                    actualDeck = deckParts[1];
                    playerName = deckParts[0];
                }
                else
                {
                    actualDeck = deckParts[0];
                    playerName = "Zeemo";
                }

                // Remove level 1-4 tags
                // Note this will break on stupid cards like Quartermaster XKR-2
                actualDeck = actualDeck.Replace("-1,", ",").Replace("-2,", ",").Replace("-3,", ",").Replace("-4", ",");

                // Add to result
                string output = playerName + ":" + gauntletName + ": " + actualDeck;
                result.AppendLine(output);
            }

            toolsOutputTextBox.Text = result.ToString();
        }

        /// <summary>
        /// Sort gauntlet cards by rarity, name
        /// </summary>
        private void toolsSortGauntletButton_Click(object sender, EventArgs e)
        {
            StringBuilder result = new StringBuilder();
            List<Deck> decks = new List<Deck>();

            var lines = toolsInputTextBox.Text.Split('\n');
            int i = 1;

            // Convert all valid lines to decks
            foreach (var line in lines)
            {
                // Retreive gauntlet and deck strings
                if (!String.IsNullOrWhiteSpace(line))
                {
                    Deck deck = new Deck(line);

                    // Order by descending rarity
                    deck.CardObjectsAssaultsAndStructures = deck.CardObjectsAssaultsAndStructures
                        .OrderByDescending(x => x.Key.Rarity)
                        .ThenByDescending(x => Math.Max(x.Key.Power / 5, x.Key.FactionPower / 5))
                        .ThenByDescending(x => x.Key.Section)
                        .ThenBy(x => x.Key.Name)
                        .ToDictionary(t => t.Key, t => t.Value);

                    decks.Add(deck);
                }
            }

            foreach (var deck in decks.OrderBy(x => x.SloppySort))
            {
                if (deck.Name == "") deck.Name = "Deck_" + i;
                result.Append(deck.Name);
                result.Append(": ");
                result.Append(deck.Commander);
                result.Append(", ");
                result.Append(deck.Dominion);
                result.Append(", ");

                foreach (var cardPair in deck.CardObjectsAssaultsAndStructures)
                {
                    result.Append(cardPair.Key.Name);

                    if (cardPair.Value > 1)
                    {
                        result.Append("#");
                        result.Append(cardPair.Value);
                    }

                    result.Append(", ");
                }

                foreach (var cardPair in deck.CardsNotFound)
                {
                    result.Append(cardPair.Key);

                    if (cardPair.Value > 1)
                    {
                        result.Append("#");
                        result.Append(cardPair.Value);
                    }

                    result.Append(", ");
                }

                // Trim the end
                if (result.Length > 3) result = result.Remove(result.Length - 2, 2);
                result.Append("\r\n");

                i++;
            }

            toolsOutputTextBox.Text = result.ToString();
        }

        /// <summary>
        /// Using a list of decks, try to group "alike" decks in a gauntlet
        /// 
        /// * For each deck that has not been sorted
        ///    * Evaluate each other deck and assign a match score
        ///    * Higher match score for rarer cards, more card matches, etc
        ///    
        /// This is like O(x^infinity)
        /// </summary>
        private void toolsSimplifyGauntletButton_Click(object sender, EventArgs e)
        {
            // Sort first
            toolsSortGauntletButton_Click(sender, e);

            // Pull the sorted decks from toolsOutput
            StringBuilder result = new StringBuilder();
            string[] lines = toolsOutputTextBox.Text.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            // Initial decks
            List<Deck> masterDeckList = new List<Deck>();

            // Sets of decks that are really alike
            List<List<Deck>> superMatchDecks = new List<List<Deck>>();

            // Sets of decks that are alike
            List<List<Deck>> strongMatchDecks = new List<List<Deck>>();

            // Sets of decks that are similar
            List<List<Deck>> mediumMatchDecks = new List<List<Deck>>();

            // Sets of decks that are loosely related
            List<List<Deck>> weakMatchDecks = new List<List<Deck>>();

            int.TryParse(toolsGroupNumberTextBox.Text, out int SIMILAR_DECK_GROUPINGS);
            if (SIMILAR_DECK_GROUPINGS <= 1) SIMILAR_DECK_GROUPINGS = 2;
            if (SIMILAR_DECK_GROUPINGS > 25) SIMILAR_DECK_GROUPINGS = 25;

            int SUPER_THRESHOLD = 15;
            int STRONG_THRESHOLD = 9;
            int MEDIUM_THRESHOLD = 6;
            int WEAK_THRESHOLD = 4;

            // Ungrouped decks
            List<Deck> oddDecks = new List<Deck>();


            // Create a deck for each deck line
            foreach (var line in lines)
            {
                Deck deck = new Deck(line);
                masterDeckList.Add(deck);
            }

            masterDeckList.OrderByDescending(d => d.CardAssaultsAndStructures.Count);

            // ----------------------------------------------------------------------------
            // For each deck, compare it to other decks and get a matchScore
            // - If enough decks have a high match score, remove them from the masterDeckList and group them together
            // - Repeat this until we iterate through the masterDeckList
            // ----------------------------------------------------------------------------
            while (masterDeckList.Count > 0)
            {
                // Get a match score of all decks to this deck
                Deck masterDeck = masterDeckList[0];
                masterDeck.MatchScore = 100;
                masterDeckList.RemoveAt(0);
                int superMatchDecksCount = 0;
                int strongMatchDecksCount = 0;
                int mediumMatchDecksCount = 0;
                int weakMatchDeckCount = 0;

                // Debug: Master deck
                Console.WriteLine("MasterDeck: " + masterDeck.DeckToString());

                // See if this deck is faction oriented (5+ cards belonging to the same faction)
                // **Not picking up the card quantity (ugh)
                //string masterDeckFactionBased = "";

                //if (masterDeck.CardObjectsAssaultsAndStructures.Count(x => x.Key.Faction == 1) >= 5) masterDeckFactionBased = "Imperial";
                //else if (masterDeck.CardObjectsAssaultsAndStructures.Count(x => x.Key.Faction == 2) >= 5) masterDeckFactionBased = "Raider";
                //else if (masterDeck.CardObjectsAssaultsAndStructures.Count(x => x.Key.Faction == 3) >= 5) masterDeckFactionBased = "Bloodthirsty";
                //else if (masterDeck.CardObjectsAssaultsAndStructures.Count(x => x.Key.Faction == 4) >= 5) masterDeckFactionBased = "Xeno";
                //else if (masterDeck.CardObjectsAssaultsAndStructures.Count(x => x.Key.Faction == 5) >= 5) masterDeckFactionBased = "Righteous";

                // Loop through each remaining deck in masterDeckList
                foreach (Deck deck in masterDeckList)
                {
                    deck.MatchScore = 0;

                    // The masterDeck is faction based. See if this deck is as well (+7 points)
                    //if (masterDeckFactionBased != "")
                    //{
                    //    if (deck.CardObjectsAssaultsAndStructures.Count(x => x.Key.Faction == 1) >= 5) deck.MatchScore += FACTION_MATCH_SCORE;
                    //    else if (deck.CardObjectsAssaultsAndStructures.Count(x => x.Key.Faction == 2) >= 5) deck.MatchScore += FACTION_MATCH_SCORE;
                    //    else if (deck.CardObjectsAssaultsAndStructures.Count(x => x.Key.Faction == 3) >= 5) deck.MatchScore += FACTION_MATCH_SCORE;
                    //    else if (deck.CardObjectsAssaultsAndStructures.Count(x => x.Key.Faction == 4) >= 5) deck.MatchScore += FACTION_MATCH_SCORE;
                    //    else if (deck.CardObjectsAssaultsAndStructures.Count(x => x.Key.Faction == 5) >= 5) deck.MatchScore += FACTION_MATCH_SCORE;
                    //}

                    int totalMatchedCards = 0;

                    // ------------------------------------------------------
                    // * Add to matchScore if this deck has cards in the masterDeck
                    // ------------------------------------------------------
                    foreach (var card in deck.CardObjectsAssaultsAndStructures)
                    {
                        if (masterDeck.CardObjectsAssaultsAndStructures.ContainsKey(card.Key))
                        {
                            int cardsInMasterDeck = masterDeck.CardObjectsAssaultsAndStructures[card.Key];
                            int cardsInTargetDeck = card.Value;
                            int matchedCardCount = Math.Min(cardsInMasterDeck, cardsInTargetDeck);
                            totalMatchedCards += matchedCardCount;

                            // 3+4+5 points for a matching box mythic (except Prixis/Therion)
                            // 2+2+3 points for a matching box vind progen (Sun, Saiph, Steropes)
                            // 1+2+3 for a matching pve/pvp vind
                            // 3 for a matching chance card
                            // 1 otherwise
                            if (card.Key.Rarity == (int)Rarity.Mythic && card.Key.Subset == CardSubset.Box.ToString())
                            {
                                //Console.Write("M " + card.Key.Name + " (+" + matchingCards * 4 + ")");
                                if (matchedCardCount == 1) deck.MatchScore += 3;
                                else if (matchedCardCount == 2) deck.MatchScore += 7;
                                else if (matchedCardCount >= 3) deck.MatchScore += matchedCardCount * 5;
                            }
                            else if (card.Key.Rarity == (int)Rarity.Vindicator && card.Key.Faction == (int)Faction.Progen)
                            {
                                //Console.Write("V " + card.Key.Name + " (+" + matchingCards * 2 + ")");
                                if (matchedCardCount == 1) deck.MatchScore += 2;
                                else if (matchedCardCount == 2) deck.MatchScore += 4;
                                else if (matchedCardCount >= 3) deck.MatchScore += 7 + (matchedCardCount-3 * 3);
                            }
                            else if (card.Key.Rarity == (int)Rarity.Vindicator)
                            {
                                //Console.Write("V " + card.Key.Name + " (+" + matchingCards * 2 + ")");
                                if (matchedCardCount == 1) deck.MatchScore += 1;
                                else if (matchedCardCount == 2) deck.MatchScore += 3;
                                else if (matchedCardCount >= 3) deck.MatchScore += 6 + (matchedCardCount - 3 * 3);
                            }
                            else if (card.Key.Subset == CardSubset.Chance.ToString())
                            {
                                //Console.Write("C " + card.Key.Name + " (+" + matchingCards * 3 + ")");
                                deck.MatchScore += (matchedCardCount * 3);
                            }
                            else
                            {
                                //Console.Write(". " + card.Key.Name + " (+" + matchingCards * 1 + ")");
                                deck.MatchScore += matchedCardCount;
                            }

                            // 2/3/4 copies of the same card: +2/4/6
                            //if (matchedCardCount == 2)
                            //{
                            //    Console.Write(" (+1)");
                            //    deck.MatchScore += 2;
                            //}
                            //else if (matchedCardCount == 3)
                            //{
                            //    Console.Write(" (+3)");
                            //    deck.MatchScore += 4;
                            //}
                            //else if (matchedCardCount >= 4)
                            //{
                            //    Console.Write(" (+6)");
                            //    deck.MatchScore += 6;
                            //}
                            //Console.Write("\r\n");
                        }
                    }

                    // ------------------------------------------------------
                    // * Add to matchScore based on total matched cards
                    //   +0/0/1/1/1/2/3 points for matching cards
                    // ------------------------------------------------------
                    if (totalMatchedCards == 3) deck.MatchScore += 1;
                    else if (totalMatchedCards == 4) deck.MatchScore += 2;
                    else if (totalMatchedCards == 5) deck.MatchScore += 3;
                    else if (totalMatchedCards == 6) deck.MatchScore += 5;
                    else if (totalMatchedCards >= 7) deck.MatchScore += 8;

                    // ------------------------------------------------------
                    // * Add to matchScore if this deck has dominions of the masterDeck
                    //   +3 for Cassius' Nexus
                    // ------------------------------------------------------
                    if (masterDeck.Dominion == "Cassius' Nexus" && masterDeck.Dominion == deck.Dominion)
                    {
                        deck.MatchScore += 3;
                    }
                                        
                    // How strong of a match is this deck
                    Console.WriteLine(deck.DeckToString() + " score " + deck.MatchScore);
                    if (deck.MatchScore >= SUPER_THRESHOLD)
                    {
                        superMatchDecksCount++;
                        strongMatchDecksCount++;
                        mediumMatchDecksCount++;
                        weakMatchDeckCount++;
                    }
                    else if (deck.MatchScore >= STRONG_THRESHOLD)
                    {
                        strongMatchDecksCount++;
                        mediumMatchDecksCount++;
                        weakMatchDeckCount++;
                    }
                    else if (deck.MatchScore >= MEDIUM_THRESHOLD)
                    {
                        mediumMatchDecksCount++;
                        weakMatchDeckCount++;
                    }
                    else if (deck.MatchScore >= WEAK_THRESHOLD)
                    {
                        weakMatchDeckCount++;
                    }

                } //Deck loop


                // Get the 3 most matching decks. If those decks are greater then the threshold scores, add them to one of the result lists
                // and remove them from the masterDeckList

                List<Deck> decksToAdd = new List<Deck> { masterDeck };
                decksToAdd.AddRange(masterDeckList.OrderByDescending(x => x.MatchScore).Take(SIMILAR_DECK_GROUPINGS - 1).ToList());


                if (superMatchDecksCount >= SIMILAR_DECK_GROUPINGS) 
                {
                    superMatchDecks.Add(decksToAdd);
                    foreach (Deck d in decksToAdd) masterDeckList.Remove(d);
                }
                else if (strongMatchDecksCount >= SIMILAR_DECK_GROUPINGS - 1)
                {
                    strongMatchDecks.Add(decksToAdd);
                    foreach (Deck d in decksToAdd) masterDeckList.Remove(d);
                }
                else if (mediumMatchDecksCount >= SIMILAR_DECK_GROUPINGS - 1)
                {
                    mediumMatchDecks.Add(decksToAdd);
                    foreach (Deck d in decksToAdd) masterDeckList.Remove(d);
                }
                else if (weakMatchDeckCount >= SIMILAR_DECK_GROUPINGS - 1)
                {
                    weakMatchDecks.Add(decksToAdd);
                    foreach (Deck d in decksToAdd) masterDeckList.Remove(d);
                }
                else
                {
                    oddDecks.Add(masterDeck);
                }

            } //MasterDeck loop


            // --- TODO: Sort odd decks with a lower threshold, and add them to the tail end of this ---

            // Output results
            toolsOutputTextBox.Clear();
            toolsOutputTextBox.AppendText("//--------------------------\r\n");
            toolsOutputTextBox.AppendText("// Very similar decks\r\n");
            toolsOutputTextBox.AppendText("//--------------------------\r\n");
            foreach (List<Deck> decks in superMatchDecks)
            {
                foreach (Deck d in decks.OrderByDescending(x => x.MatchScore))
                {
                    toolsOutputTextBox.AppendText(d.DeckToString() + "\r\n");
                }
                toolsOutputTextBox.AppendText("\r\n");
            }
            toolsOutputTextBox.AppendText("\r\n");
            toolsOutputTextBox.AppendText("//--------------------------\r\n");
            toolsOutputTextBox.AppendText("// Strongly similar decks\r\n");
            toolsOutputTextBox.AppendText("//--------------------------\r\n");
            toolsOutputTextBox.AppendText("\r\n");
            foreach (List<Deck> decks in strongMatchDecks)
            {
                foreach (Deck d in decks.OrderByDescending(x => x.MatchScore))
                {
                    toolsOutputTextBox.AppendText(d.DeckToString() + "\r\n");
                }
                toolsOutputTextBox.AppendText("\r\n");
            }
            toolsOutputTextBox.AppendText("\r\n");
            toolsOutputTextBox.AppendText("//--------------------------\r\n");
            toolsOutputTextBox.AppendText("// Somewhat similar decks\r\n");
            toolsOutputTextBox.AppendText("//--------------------------\r\n");
            toolsOutputTextBox.AppendText("\r\n");
            foreach (List<Deck> decks in mediumMatchDecks)
            {
                foreach (Deck d in decks.OrderByDescending(x => x.MatchScore))
                {
                    toolsOutputTextBox.AppendText(d.DeckToString() + "\r\n");
                }
                toolsOutputTextBox.AppendText("\r\n");
            }
            toolsOutputTextBox.AppendText("\r\n");
            toolsOutputTextBox.AppendText("//--------------------------\r\n");
            toolsOutputTextBox.AppendText("// Barely similar decks\r\n");
            toolsOutputTextBox.AppendText("//--------------------------\r\n");
            toolsOutputTextBox.AppendText("\r\n");
            foreach (List<Deck> decks in weakMatchDecks)
            {
                foreach (Deck d in decks.OrderByDescending(x => x.MatchScore))
                {
                    toolsOutputTextBox.AppendText(d.DeckToString() + "\r\n");
                }
                toolsOutputTextBox.AppendText("\r\n\r\n");
            }
            toolsOutputTextBox.AppendText("//--------------------------\r\n");
            toolsOutputTextBox.AppendText("// Unsorted\r\n");
            toolsOutputTextBox.AppendText("//--------------------------\r\n");
            foreach (Deck d in oddDecks)
            {
                toolsOutputTextBox.AppendText(d.DeckToString() + "\r\n");
            }
        }

        /// <summary>
        /// If there are multiple deck lines with the same player name, take the highest
        /// FORMAT
        /// NAME:WIN_PERCENT:DECK:GUILD
        /// </summary>
        private void toolsHighestPercentButton_Click(object sender, EventArgs e)
        {
            var result = new StringBuilder();
            var playerList = new List<Tuple<string, double, string>>(); // player, winrate, deck

            var lines = toolsInputTextBox.Text.Split('\n');

            foreach (var line in lines)
            {
                var deck = line;
                if (string.IsNullOrWhiteSpace(deck)) continue;

                var deckPart = line.Split(new char[] { ':', '\t' }, 3);
                if (deckPart.Length >= 3)
                {
                    var player = deckPart[0];
                    var winPercent = 0.0;
                    Double.TryParse(deckPart[1], out winPercent);
                    var deckList = deckPart[2];

                    var existingPlayer = playerList.Where(p => p.Item1.Contains(player)).FirstOrDefault();

                    // New player entry
                    if (existingPlayer == null)
                    {
                        playerList.Add(new Tuple<string, double, string>(player, winPercent, deckList));
                    }
                    // Take the higher of the player deck
                    else
                    {
                        if (winPercent > existingPlayer.Item2)
                        {
                            playerList.Remove(existingPlayer);
                            playerList.Add(new Tuple<string, double, string>(player, winPercent, deckList));
                        }
                    }
                }
            }

            foreach (var p in playerList)
            {
                result.AppendLine(p.Item1 + ":" + p.Item2 + ":" + p.Item3.Trim());
            }

            toolsOutputTextBox.Text = result.ToString();
        }

        /// <summary>
        /// Removes dominions from text files
        /// </summary>
        private void toolsRemoveDominionsButton_Click(object sender, EventArgs e)
        {
            var result = new StringBuilder();
            var playerList = new List<Tuple<string, double, string>>(); // player, winrate, deck

            var lines = toolsInputTextBox.Text.Split('\n');

            foreach (var line in lines)
            {
                var deck = line;
                if (string.IsNullOrWhiteSpace(deck)) continue;

                result.AppendLine(TextCleaner.DeckRemoveDominions(deck));
            }

            foreach (var p in playerList)
            {
                result.AppendLine(p.Item1 + ":" + p.Item2 + ":" + p.Item3.Trim());
            }

            toolsOutputTextBox.Text = result.ToString();
        }

        /// <summary>
        /// Set custom seed decks for players
        /// </summary>
        private void functionCustomSeedButton_Click(object sender, EventArgs e)
        {
            // List of decks
            var deckList = toolsInputTextBox.Text.Split('\n');

            var control = (Button)sender;
            var controlName = control.Name;

            var customSeedNumber = controlName == "toolsManualSeed1Button" ? 1 : 2;

            foreach (var deck in deckList)
            {
                var output = "";
                var line = "";
                var foundSeedLine = false;

                var deckSplit = deck.Split(':');
                if (deckSplit.Length < 2) continue;

                // player name should be the first split
                string playerName = deckSplit[0].Trim();
                string playerDeck = "";

                // deck is either the 2nd or 3rd split
                if (deckSplit.Length == 2) playerDeck = deckSplit[1];
                else playerDeck = deckSplit[2].Trim();

                // Remove dominion from the seed deck
                // playerDeck = TextCleaner.DeckRemoveDominions(playerDeck);

                try
                {
                    // Each file that matches this
                    string[] files = Directory.GetFiles("./data/cards/", "*" + playerName + "*.txt", System.IO.SearchOption.TopDirectoryOnly);
                    if (files.Length > 0)
                    {
                        foreach (string filePath in files)
                        {
                            using (var reader = new StreamReader(filePath))
                            {
                                // Look for an existing TextEditor Seed line
                                while ((line = reader.ReadLine()) != null)
                                {
                                    if (!line.StartsWith("//TextEditor Seed " + customSeedNumber + ":")) output += line + "\r\n";
                                    else
                                    {
                                        output += "//TextEditor Seed " + customSeedNumber + ":" + playerDeck + "\r\n";
                                        foundSeedLine = true;
                                    }
                                }

                                if (!foundSeedLine)
                                {
                                    output += "//TextEditor Seed " + customSeedNumber + ":" + playerDeck + "\r\n";
                                }
                            }

                            // Write the modified playerfile back
                            using (var writer = new StreamWriter(filePath))
                            {
                                toolsOutputTextBox.AppendText("Writing deck to " + filePath + "\r\n");
                                writer.Write(output);
                            }
                        }
                    }
                    else
                    {
                        toolsOutputTextBox.AppendText("Could not find a player file for " + playerName + "\r\n");
                    }

                }
                catch { }
            }

            toolsOutputTextBox.AppendText("\r\nTextEditor Seed " + customSeedNumber + " set\r\n");
        }

        /// <summary>
        /// Get player cards - should this set them?
        /// ** DEPRECATED: The ogre database no longer exists
        /// 
        /// </summary>
        private void gtGetPlayerCardsButton_Click(object sender, EventArgs e)
        {
            string url = CONFIG.DeckSnifferUrl;
            string data = "";
            string player = url.Split('/').Last();
            StringBuilder result = new StringBuilder();

            // Call for data
            var request = (HttpWebRequest)WebRequest.Create(url);
            try
            {
                // Get json
                var response = request.GetResponse();
                using (var responseStream = response.GetResponseStream())
                {
                    StreamReader reader = new StreamReader(responseStream, Encoding.UTF8);
                    data = reader.ReadToEnd();
                }

                // Deserialize json
                var jobject = JObject.Parse(data);

                foreach (JProperty prop in jobject.Properties())
                {
                    int cardId = int.Parse(prop.Name);
                    int cardCount = int.Parse(prop.Value.ToString());
                    Card card = CardManager.GetById(cardId);

                    if (card != null) result.AppendLine(card.Name + "\t" + cardCount);
                    else result.AppendLine(cardId + "\t" + cardCount);
                }

                adminOutputTextBox.Text = player + "\r\n" + result.ToString();

            }
            catch (WebException ex)
            {
                var errorResponse = ex.Response;
                using (var responseStream = errorResponse.GetResponseStream())
                {
                    var reader = new StreamReader(responseStream, Encoding.GetEncoding("utf-8"));
                    adminOutputTextBox.Text = reader.ReadToEnd();
                }
            }

        }

        /// <summary>
        /// Convert each card into an ID
        /// This may not work for partially leveled cards
        /// Format: 45031|45031|46349|46349 - no commander
        /// 
        /// ** DEPRECATED: This was for the ogre database/raid function that automatically attacked raid. 
        /// We now livesim raids in the app
        /// </summary>
        private void toolsGetCardIDsButton_Click(object sender, EventArgs e)
        {
            StringBuilder result = new StringBuilder();
            string[] lines = toolsInputTextBox.Text.Split('\n');

            foreach (var line in lines)
            {
                // Reject empty lines
                var deck = line;
                if (string.IsNullOrWhiteSpace(deck)) continue;

                // Split the decklist into Player:WinPercent:Deck
                var deckPart = line.Split(new char[] { ':', '\t' });
                if (deckPart.Length >= 3)
                {
                    var player = deckPart[0];
                    var winPercent = deckPart[1];
                    var deckList = deckPart[2];

                    // Begin the result
                    //result.Append(player).Append(":").Append(winPercent).Append(":");

                    // Split the decklist into each card
                    // For each card (e.g. Majestic Falcon#2)
                    foreach (var cardString in deckList.Split(','))
                    {
                        // Split the card into "Majestic Falcon", 2
                        string baseCardName = cardString.Trim().Replace("-1", "");
                        KeyValuePair<string, int> formattedCard = CardManager.FormatCard(baseCardName, true);
                        string cardName = formattedCard.Key;
                        int cardCount = formattedCard.Value;

                        // Attempt to find this card
                        Card card = CardManager.GetPlayerCardByName(cardName);

                        if (card != null)
                        {
                            // Exclude commander / dominion, only list assaults
                            if (card.CardType != CardType.Commander.ToString() && card.CardType != CardType.Dominion.ToString())
                            {
                                // Get Card ID
                                for (int i = 0; i < cardCount; i++)
                                {
                                    result.Append(card.CardId);
                                    result.Append("|");
                                }
                            }
                        }
                        else
                        {
                            result.Append(baseCardName);
                        }
                    }
                    // Remove the last pipe ("|")
                    result.Length--;

                    result.AppendLine();
                }
            }

            toolsOutputTextBox.Text = result.ToString();
        }

        /// <summary>
        /// Lists all the BIGCARDS owned by players (in appsettings)
        /// 
        /// ** DEPRECATED
        /// </summary>
        private void toolsMythicButton_Click(object sender, EventArgs e)
        {
            StringBuilder result = new StringBuilder();

            // No longer used
            result.Append("Defunt feature. Player card pulls work differently at the moment\t");

            //foreach (var player in PlayerManager.Players.OrderBy(x => x.Guild).ThenBy(x => x.KongName))
            //{
            //    result.Append(player.Guild).Append("\t").Append(player.KongName).Append("\t");

            //    foreach (var card in player.Cards.OrderByDescending(x => x.Key.Power))
            //    {
            //        if (CONSTANTS.BIGCARDS_MYTHIC.Contains(card.Key.Name) ||
            //            CONSTANTS.BIGCARDS_VINDI.Contains(card.Key.Name) ||
            //            CONSTANTS.BIGCARDS_CHANCE.Contains(card.Key.Name) ||
            //            CONSTANTS.BIGCARDS_REWARD.Contains(card.Key.Name))
            //        {
            //            result.Append(card.Key.Name);
            //            if (card.Value > 1)
            //                result.Append("#").Append(card.Value);
            //            result.Append(",");
            //        }
            //    }

            //    result.Append("\t");

            //    foreach (var card in player.PossibleCards.OrderByDescending(x => x.Key.Power))
            //    {
            //        if (CONSTANTS.BIGCARDS_MYTHIC.Contains(card.Key.Name) ||
            //            CONSTANTS.BIGCARDS_VINDI.Contains(card.Key.Name) ||
            //            CONSTANTS.BIGCARDS_CHANCE.Contains(card.Key.Name) ||
            //            CONSTANTS.BIGCARDS_REWARD.Contains(card.Key.Name))
            //        {
            //            result.Append(card.Key.Name);
            //            if (card.Value > 1)
            //                result.Append("#").Append(card.Value);
            //            result.Append(",");
            //        }
            //    }
            //    result.AppendLine();
            //}

            toolsOutputTextBox.Text = result.ToString();
        }

        /// <summary>
        /// Converts a simple strong (PLAYER:WINRATE:DECK) to pirate format (PLAYER:WINRATE:DECK MINUS DOMINION/COMMANDER QUAD FORMATTED WITH -1:"DT")
        /// </summary>
        private void toolsSimpleToPirateButton_Click(object sender, EventArgs e)
        {
            var result = new StringBuilder();
            var playerList = new List<Tuple<string, double, string>>(); // player, winrate, deck

            var lines = toolsInputTextBox.Text.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var deck = line + ":DT";
                if (string.IsNullOrWhiteSpace(deck)) continue;

                result.AppendLine(TextCleaner.DeckRemoveDominions(deck));
            }

            toolsOutputTextBox.Text = result.ToString();
        }

        /// <summary>
        /// Convert a list of strings with tabs to colons
        /// </summary>
        private void toolsColonToTabButton_Click(object sender, EventArgs e)
        {
            var result = new StringBuilder();
            var lines = toolsInputTextBox.Text.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                string[] splitString = line.Split(':');

                for (var i = 0; i < splitString.Length; i++)
                {
                    if (splitString[i].Length == 0) continue;

                    result.Append(splitString[i]);
                    result.Append('\t');
                    if (i == 0) result.Append('\t');
                }
                result.AppendLine();
            }

            toolsOutputTextBox.Text = result.ToString();
        }

        /// <summary>
        /// Sim sheet format to colon format
        /// </summary>
        private void tabToColonButton_Click(object sender, EventArgs e)
        {
            var result = new StringBuilder();
            var lines = toolsInputTextBox.Text.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                string[] splitString = line.Split('\t');

                for (var i = 0; i < splitString.Length; i++)
                {
                    if (splitString[i].Length == 0) continue;

                    result.Append(splitString[i]);
                    result.Append(':');
                }
                result.Append("\r\n");
            }

            // Trim the trailing colons
            result = result.Replace(":\r\n", "\r\n");

            toolsOutputTextBox.Text = result.ToString();
        }

        /// <summary>
        /// Translate in-game names to kong names (example: C3DT to 0mniscientEye)
        /// - This is done so we can more easily set deck seeds
        /// </summary>
        private void toolsIngameToKongNameButton_Click(object sender, EventArgs e)
        {
            var result = new StringBuilder();
            var lines = toolsInputTextBox.Text.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                // <name>:<data...>
                string[] splitString = line.Split(new char[] { ':' }, 2);

                if (splitString.Count() > 1) {
                    string name = splitString[0];
                    string data = splitString[1];

                    // Found the in-game name, translate to KongName
                    if (CONSTANTS.INGAME_TO_KONGNAME.ContainsKey(name))
                        name = CONSTANTS.INGAME_TO_KONGNAME[name];

                    result.Append(name + ":" + data + "\r\n");
                }
            }

            toolsOutputTextBox.Text = result.ToString();
        }

        /// <summary>
        /// Converts a sim result line into a tab based, spam score line
        /// * INPUT - ":52.9: Daedalus Charged, Broodmother's Nexus, {CardA}, {CardB}, {CardA}, {CardB}, ..."
        /// * OUTPUT - "{CardA} \t {CardB} \t {CardC} \t WinRate \t <deck>
        /// </summary>
        private void toolsSpamSimToTabButton_Click(object sender, EventArgs e)
        {
            var result = new StringBuilder();
            var lines = toolsInputTextBox.Text.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                // Entire simResult
                string[] simResult = line.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                if (simResult.Length != 2) continue;

                string winRate = simResult[0]; // Winrate
                string simDeck = simResult[1]; // Deck

                string[] simDeckComponent = simDeck.Split(',');
                if (simDeckComponent.Length < 3) continue;

                string commander = simDeckComponent[0];
                string dominion = simDeckComponent[1];
                List<string> playerCards = new List<string>();

                // Process the deck until we have each unique card
                for (var i = 2; i < simDeckComponent.Length; i++)
                {
                    string cardName = simDeckComponent[i].Split('#')[0].Trim();
                    if (!playerCards.Contains(cardName)) playerCards.Add(cardName);
                }

                // Output
                // <CardA> \t <CardB> \t <CardC or blank> \t

                result.Append(winRate);
                result.Append("\t");
                result.Append(playerCards[0]);
                result.Append("\t");
                result.Append(playerCards.Count >= 2 ? playerCards[1] : "");
                result.Append("\t");
                result.Append(playerCards.Count >= 3 ? playerCards[2] : "");
                result.Append("\t");
                result.Append(playerCards.Count >= 4 ? playerCards[3] : "");
                result.Append("\t");
                result.Append(playerCards.Count >= 5 ? playerCards[4] : "");
                result.Append("\t");
                result.Append(simDeck);

                result.AppendLine();
            }

            toolsOutputTextBox.Text = result.ToString();
        }

        /// <summary>
        /// Find cards and try to convert them to a card abbrevation
        /// </summary>
        private void toolsCardAbbrsSeedButton_Click(object sender, EventArgs e)
        {
            var result = new StringBuilder();
            var lines = toolsInputTextBox.Text.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var l in lines)
            {
                string[] splitLine = l.Split(new char[] { ':' }, 4);
                string deckLine = "";

                // Get deckLine, and save data before it
                if (splitLine.Length == 1)
                {
                    deckLine = splitLine[0];
                }
                else if (splitLine.Length == 2)
                {
                    deckLine = splitLine[1];
                    result.Append(splitLine[0]);
                    result.Append(":");
                }
                else if (splitLine.Length >= 3)
                {
                    deckLine = splitLine[2];
                    result.Append(splitLine[0]);
                    result.Append(":");
                    result.Append(splitLine[1]);
                    result.Append(":");
                }

                // Deck should be the third element
                string[] cards = deckLine.Split(',');
                foreach (var card in cards)
                {
                    // Get card count
                    var regex = new Regex(".*#([0-9]+).*");
                    var regex2 = new Regex(".*\\(([0-9]+)\\).*");
                    var match = regex.Match(card);
                    var match2 = regex2.Match(card);

                    int numberOfCards = 1;

                    if (match.Success || match2.Success)
                    {
                        numberOfCards = Int32.Parse(match.Groups[1].Value);
                    }

                    // this is awful
                    var tmpCard = card.Split('#')[0].Trim();

                    if (CONSTANTS.CARDABBRS.ContainsKey(tmpCard))
                    {
                        result.Append(CONSTANTS.CARDABBRS[tmpCard]);
                        if (numberOfCards > 1) result.Append("#" + numberOfCards);
                    }
                    else
                    {
                        result.Append(card);
                    }
                    result.Append(", ");
                }
                if (result.Length > 2) result = result.Remove(result.Length - 2, 2);

                // Append the fourth chunk (if it exists) 
                if (splitLine.Length > 3)
                {
                    result.Append(splitLine[3]);
                }

                result.Append("\r\n");
            }

            toolsOutputTextBox.Text = result.ToString();
        }

        /// <summary>
        /// 15 card campaign deck - 15 good cards
        /// Set to offense 6
        /// </summary>
        private void toolsCampaignButton_Click(object sender, EventArgs e)
        {
            var result = new StringBuilder();

            foreach (var player in PlayerManager.Players)
            {
                // playerName:#:deck:GuildName
                result.Append(player.KongName + ":0:");

                // Commander
                var commander = CardManager.GetBestCommander(player.Cards);

                result.Append(commander.Name);
                if (commander.Fusion_Level == 2) result.Append("-1");
                result.Append(",");

                // Cards
                var cards = player.Cards.Where(x => x.Key.CardType == CardType.Assault.ToString() || x.Key.CardType == CardType.Structure.ToString()).OrderByDescending(x => x.Key.Power).Take(15);
                var cardsAdded = 0;
                foreach (var c in cards)
                {
                    result.Append(c.Key.Name);

                    var cardsToAdd = Math.Min(c.Value, 1);

                    if (cardsToAdd > 1) result.Append("#" + Math.Min(cardsToAdd, 15 - cardsAdded)); //Basically, add the max unless you go over 15
                    result.Append(",");

                    cardsAdded += cardsToAdd;
                    if (cardsAdded >= 15) break;
                }

                if (cardsAdded < 15)
                {
                    var weakCards = player.WeakCards.OrderByDescending(x => x.Key.Power).Take(15);
                    foreach (var c in weakCards)
                    {
                        result.Append(c.Key.Name);
                        if (c.Value > 1) result.Append("#" + Math.Min(c.Value, 15 - cardsAdded)); //Basically, add the max unless you go over 15
                        result.Append(",");

                        cardsAdded += c.Value;
                        if (cardsAdded >= 15) break;
                    }
                }

                // Guild sign
                result.Remove(result.Length - 1, 1);
                result.Append(":GD");
                result.AppendLine();
            }

            toolsOutputTextBox.Text = result.ToString();
        }

        /// <summary>
        /// Get guild for kongStrings
        /// </summary>
        private void toolsGetGuildButton_Click(object sender, EventArgs e)
        {
            List<string> results = new List<string>();
            var lines = toolsInputTextBox.Text.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            KongViewModel kongVm = new KongViewModel();

            foreach (var l in lines)
            {
                string kongInfo = l;
                string faction = "_UNGUILDED";
                string shortFaction = "__";
                StringBuilder result = new StringBuilder();

                kongVm = BotManager.UpdateFaction(this, kongInfo, pullPlayerDecks: false);

                if (kongVm.Faction.Name != null)
                {
                    faction = kongVm?.Faction?.Name;
                    if (faction == "") faction = "_UNGUILDED";

                    // Get the guild shortname
                    shortFaction = TextCleaner.GetGuildShortName(faction);

                    result.Append(faction);
                }
                else
                {
                    result.Append("_FAILED");
                    shortFaction = "!!";
                }

                // Guild \t KongInfo
                // kongInfo: if it starts with "XX, replace that with the new shortFaction tag
                result.Append("\t");
                if (kongInfo.Length > 3 && kongInfo[2] == ',')
                {
                    kongInfo = shortFaction + "," + kongInfo.Substring(3, kongInfo.Length - 3);
                }

                result.Append(kongInfo);
                results.Add(result.ToString());
            }

            results.Sort();
            toolsOutputTextBox.Text = string.Join("\r\n", results.ToArray());
        }

        /// <summary>
        /// Pulls guilds from a gauntlet (then Puller or Ogrelicious if not found), and does the group sort on them
        /// 
        /// This feature saves ridiculous time in creating a rough gauntlet of a guild or guilds
        /// </summary>
        private void toolsPullGuildDefenseButton_Click(object sender, EventArgs e)
        {
            string[] targetGuilds = toolsInputTextBox.Text.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            string gauntletName = toolsPullGuildGauntletTextBox.Text;
            List<string> gauntletLines = FileIO.SimpleRead(this, "data/customdecks_" + gauntletName + ".txt", returnCommentedLines: false, skipWhitespace: false, displayError: false);
            StringBuilder resultDecks = new StringBuilder();

            if (targetGuilds.Count() > 10)
            {
                toolsOutputTextBox.Text = "Too many guild lines. Put one guild name on each line, max 10";
                return;
            }


            // Look for guild decks
            // 1) Gauntlet
            // 2) puller file (admin)
            // 3) ogre logs (admin)
            // * Not as efficient as it could be, but speed is not an issue
            foreach (string guildName in targetGuilds)
            {
                // 1) Look in gauntlet
                foreach (string line in gauntletLines)
                {
                    if (line.StartsWith(guildName + "_def") && !line.Contains("/^")) {
                        resultDecks.AppendLine(line);
                    }
                }

                // 2) Look in puller for this guild
                //if (!guildFound)
                //{
                //    List<string> pullerLogs = FileIO.SimpleRead(this, "./__puller.txt", returnCommentedLines: false, displayError: false);
                //    string pullerGuildName = "";
                //    string kongInfo = "";
                //    List<string> userIds = new List<string>();

                //    // Create puller params. This is fragile and may need to be fixed
                //    foreach (var line in pullerLogs)
                //    {
                //        if (line.StartsWith("Guild")) pullerGuildName = line.Replace("Guild:", "");
                //        else if (line.StartsWith("kongName")) kongInfo = line;
                //        else userIds = line.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();

                //        // If this is the guild we're looking for, pull its members
                //        if (pullerGuildName == guildName && kongInfo != "" && userIds.Count > 0)
                //        {
                //            KongViewModel kongVm = new KongViewModel("getProfileData");
                //            ApiManager.GetKongInfoFromString(kongVm, kongInfo);

                //            // Call each userId
                //            foreach (var userId in userIds)
                //            {
                //                kongVm.Params = "target_user_id=" + userId;
                //                ApiManager.CallApi(kongVm);
                //            }

                //            //attackGauntlets.Add(myGuild + "_atk: /^" + myGuild + "_atk_.*$/");
                //            //foreach (var pi in kongVm.PlayerInfo.OrderBy(x => x.Name))
                //            //{
                //            //    if (pi.ActiveDeck != null)
                //            //    {
                //            //        string deck = myGuild + "_atk_" + pi.Name + ": " + pi.ActiveDeck.DeckToString();
                //            //        attackGauntlets.Add(deck);
                //            //        //adminOutputTextBox.AppendText(deck + "\r\n");
                //            //    }
                //            //}

                //            foreach (var pi in kongVm.PlayerInfo.OrderBy(x => x.Name))
                //            {
                //                if (pi.DefenseDeck != null)
                //                {
                //                    string deck = guildName + "_def_" + pi.Name + ": " + pi.DefenseDeck.DeckToString(false);
                //                    resultDecks.AppendLine(deck);
                //                }
                //            }
                //        }
                //    }
                //}

                // 3) Look in OgreLogs (admins)
                //if (!guildFound && !string.IsNullOrEmpty(CONFIG.DeckSnifferUrl))
                //{
                //    string[] ogreDecks = Ogrelicious.CallServerSniffer(guildName, DateTime.Now.ToString("M/.*/yy")).Result;
                //    List<string> emptyList = new List<string>();
                //    string formattedDecks = Ogrelicious.CompileGauntlet("", ogreDecks, true, false, true, emptyList);
                //    resultDecks.AppendLine(formattedDecks);
                //}
            }

            toolsOutputTextBox.Text = resultDecks.ToString();

            //Save current input
            string currentInputText = toolsInputTextBox.Text;

            // Set input to the decks found in GT
            toolsInputTextBox.Text = resultDecks.ToString();

            // Process the decks with simplify
            toolsSimplifyGauntletButton_Click(sender, e);

            //toolsInputTextBox.Text = currentInputText;
        }

        /// <summary>
        /// List current top5 guilds (hardcoded)
        /// </summary>
        private void toolsPullGuildDefenseTop5Button_Click(object sender, EventArgs e)
        {
            List<string> guilds = CONSTANTS.GUILDS_TOP5;
            toolsInputTextBox.Text = string.Join("\r\n", guilds);
        }

        /// <summary>
        /// List current top10 guilds (hardcoded)
        /// </summary>
        private void toolsPullGuildDefenseTop10Button_Click(object sender, EventArgs e)
        {
            List<string> guilds = CONSTANTS.GUILDS_TOP10;
            toolsInputTextBox.Text = string.Join("\r\n", guilds);
        }

        /// <summary>
        /// List current top11-25 guilds (hardcoded)
        /// </summary>
        private void toolsPullGuildDefenseTop11to25Button_Click(object sender, EventArgs e)
        {
            List<string> guilds = CONSTANTS.GUILDS_TOP25;

            toolsInputTextBox.Text = string.Join("\r\n", guilds);
        }

        #endregion

        // ---- Game Tab Events (RumBarrel ) ----
        
        #region Rum Barrel

        /// <summary>
        /// Gets battle data from the API 
        /// - either the current battle or last battle
        /// </summary>
        private void rumBarrelGetBattleButton_Click(object sender, EventArgs e)
        {
            try
            {
                // Parse selectedUser for kong login
                string userData = rumBarrelPlayerComboBox.Text;

                // Refresh
                KongViewModel kongVm = BotManager.GetCurrentOrLastBattle(this, userData, missingCardStrategy: rumBarrelMissingCardStrategyComboBox.Text);

                // Populate RumBarrel
                if (kongVm != null)
                {
                    FillOutRumBarrel(kongVm);
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on GetBattle(): \r\n" + ex);
            }
        }

        /// <summary>
        /// Run a live sim
        /// </summary>
        private void rumBarrelLiveSimButton_Click(object sender, EventArgs e)
        {
            try
            {
                BatchSim batchSim = NewSimManager.BuildLiveSimRumBarrel(this);

                // How many threads to concurrently run
                int.TryParse(rumBarrelProgressBar.Text, out int THREADS);
                if (THREADS <= 0) THREADS = 1;
                if (THREADS > 27) THREADS = 27;

                this.stopProcess = false;
                this.workerThread = new Thread(new ThreadStart(() => NewSimManager.RunLiveSimRumBarrel(this, batchSim, updateForm: true, threads: THREADS)));
                this.workerThread.Start();
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on LiveSim(): \r\n" + ex);
            }
        }

        /// <summary>
        /// Get battle data from the API, then live sim
        /// TODO: Try introducing a loop option with an X second wait. With the current threading solution this does not work
        /// </summary>
        private void rumBarrelGetBattleAndSimButton_Click(object sender, EventArgs e)
        {
            try
            {
                rumBarrelLoopProgressBar.Maximum = 3;

                this.stopProcess = false;
                this.workerThread = new Thread(
                    new ThreadStart(() => LoopRumBarrelLiveSim())
                );
                this.workerThread.Start();

                rumBarrelLoopProgressBar.Value = 0;
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on RefreshAndSim(): \r\n" + ex);
            }
        }

        /// <summary>
        /// Loops refreshing livesim in RumBarrel until killed
        /// </summary>
        private async void LoopRumBarrelLiveSim()
        {
            try
            {
                // Parse selectedUser for kong login
                string userInfo = rumBarrelPlayerComboBox.Text;
                int.TryParse(rumBarrelIterationsTextBox.Text, out int iterations);
                int.TryParse(rumBarrelCardsAheadComboBox.Text, out int extraLockedCards);

                // How many threads to concurrently run
                int.TryParse(rumBarrelProgressBar.Text, out int THREADS);
                if (THREADS <= 0) THREADS = 1;
                if (THREADS > 27) THREADS = 27; // 9 threads causes i7 to cause some tuo.exes to throw an error.. this may be for fancier machines

                // surge default, but brawl or gw will do scoring
                string gameMode = rumBarrelGameModeSelectionComboBox.Text;
                rumBarrelSimStringOutputTextBox.Clear();

                string previousTurn = "-1";
                string previousEnemy = "";

                // Loop the refresh battle
                for (int i = 0; i < 1000; i++)
                {
                    // Refresh
                    KongViewModel kongVm = BotManager.GetCurrentOrLastBattle(this, userInfo, missingCardStrategy: rumBarrelMissingCardStrategyComboBox.Text);
                    if (kongVm != null)
                    {
                        // Populate RumBarrel
                        FillOutRumBarrel(kongVm);

                        // Resim: Unless its the same turn
                        if (previousTurn != kongVm.BattleData.Turn || previousEnemy != kongVm.BattleData.EnemyName)
                        {
                            // Make the Sim objects
                            BatchSim batchSim = NewSimManager.BuildLiveSim(kongVm, gameMode, iterations: iterations, extraLockedCards: extraLockedCards);

                            // Display what sim we're sending to tuo
                            ControlExtensions.InvokeEx(rumBarrelSimStringOutputTextBox, x => rumBarrelSimStringOutputTextBox.Clear());

                            foreach (var sim in batchSim.Sims)
                            {
                                rumBarrelSimStringOutputTextBox.AppendText(sim.SimToString() + "\r\n");
                            }

                            // Run live sims
                            NewSimManager.RunLiveSimRumBarrel(this, batchSim, updateForm: true, threads: THREADS);


                            previousTurn = kongVm.BattleData.Turn;
                            previousEnemy = kongVm.BattleData.EnemyName;
                        }


                        // Loop this until the checkbox is cleared (or 1000 iterations pass)
                        if (rumBarrelGetBattleAndSimLoopCheckBox.Checked == false) break;

                        // How many seconds to wait (2 second minimum)
                        int.TryParse(rumBarrelGetBattleAndSimLoopTextBox.Text, out int ms);
                        int seconds = Math.Max(2000, ms) / 1000;

                        rumBarrelLoopProgressBar.Maximum = seconds * 2;

                        // every 0.5 seconds, increase the progress bar
                        for(int j=0; j<seconds * 2; j++)
                        {
                            rumBarrelLoopProgressBar.PerformStep();
                            await Helper.AsyncDelay(500);
                        }

                        rumBarrelLoopProgressBar.Value = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on RefreshAndSim(): \r\n" + ex);
            }

        }

        /// <summary>
        /// Determine if in active battle. If so, play cards until winner is shown or some error
        /// </summary>
        private async void rumBarrelPlayMatchButton_Click(object sender, EventArgs e)
        {
            try
            {
                // Parse selectedUser for kong login
                string userData = rumBarrelPlayerComboBox.Text;
                int.TryParse(rumBarrelIterationsTextBox.Text, out int iterations);
                int.TryParse(rumBarrelCardsAheadComboBox.Text, out int extraLockedCards);
                string gameMode = rumBarrelGameModeSelectionComboBox.Text;

                // Refresh
                KongViewModel kongVm = BotManager.GetCurrentOrLastBattle(this, userData, missingCardStrategy: rumBarrelMissingCardStrategyComboBox.Text);

                // Populate RumBarrel
                if (kongVm != null)
                {
                    //repeat
                    while (kongVm.BattleData.Winner == null) // or some error occurs
                    {
                        // Refresh
                        // TODO: Shouldn't BotManager.PlayCard return this data?
                        // kongVm = BotManager.GetBattle(this, userData);  

                        // Show data
                        FillOutRumBarrel(kongVm);

                        // Build sim
                        BatchSim batchSim = NewSimManager.BuildLiveSim(kongVm, gameMode, iterations: iterations, extraLockedCards: extraLockedCards);

                        // Run sim
                        await Task.Run(() => NewSimManager.RunLiveSimRumBarrel(this, batchSim, updateForm: true, threads: 3));

                        // Play next card
                        // Look through our simmed deck and find the first card we can legally play
                        Sim sim = batchSim.Sims.OrderByDescending(x => x.WinPercent).ThenByDescending(x => x.WinScore).FirstOrDefault();
                        BotManager.PlayCardRumBarrel(this, kongVm, sim);

                        // TODO: PlayMatch in its own BotManager?

                        // Some error happened
                        if (kongVm.Result == "False")
                        {
                            rumBarrelOutputTextBox.AppendText("An error has occured: " + kongVm.ResultMessage + "\r\n");
                            return;
                        }
                    }

                    FillOutRumBarrel(kongVm);
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on PlayMatch(): \r\n" + ex);
            }
        }

        // ------------------- BRAWL ------------------- //

        /// <summary>
        /// Get Brawl energy for this event
        /// </summary>
        private void rumBarrelGetBrawlEnergyButton_Click(object sender, EventArgs e)
        {
            try
            {
                // Call init to get energy
                string userData = rumBarrelPlayerComboBox.Text;
                KongViewModel kongVm = BotManager.Init(userData);

                int brawlEnergy = kongVm.BrawlData.Energy;
                rumBarrelBrawlEnergyTextBox.Text = brawlEnergy.ToString();

                // Refresh
                kongVm = BotManager.GetCurrentOrLastBattle(this, userData, missingCardStrategy: grindMissingCardStrategyComboBox.Text);
                FillOutRumBarrel(kongVm);
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on PlayMatch(): \r\n" + ex);
            }
        }

        /// <summary>
        /// Start a brawl battle
        /// </summary>
        private void rumBarrelStartBrawlButton_Click(object sender, EventArgs e)
        {
            try
            {
                // Parse selectedUser for kong login
                string userData = rumBarrelPlayerComboBox.Text;

                var kongVm = BotManager.Init(userData);
                if (kongVm.BattleToResume)
                {

                    outputTextBox.AppendText("**** WARNING: User is already in a battle!****\r\n");
                    outputTextBox.AppendText("***Click play match to finish it\r\n");
                    return;
                }

                kongVm = BotManager.StartBrawlMatch(this, userData);

                // Populate RumBarrel
                if (kongVm != null)
                {
                    // Show data
                    FillOutRumBarrel(kongVm);

                    // Some error happened
                    if (kongVm.Result == "False")
                    {
                        rumBarrelOutputTextBox.AppendText("An error has occured: " + kongVm.ResultMessage + "\r\n");
                        return;
                    }

                    outputTextBox.AppendText("** Brawling: " + kongVm.BattleData.EnemyName + "\r\n");
                }
            }

            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on StartCqBattle(): \r\n" + ex);
            }
        }


        /// <summary>
        /// Perform X Brawl Battles
        /// </summary>
        private async void rumBarrelAutoBrawlButton_Click(object sender, EventArgs e)
        {
            try
            {
                // Parse selectedUser for kong login
                string userData = rumBarrelPlayerComboBox.Text;

                // Attack params
                int.TryParse(rumBarrelIterationsTextBox.Text, out int iterations);
                int.TryParse(rumBarrelCardsAheadComboBox.Text, out int extraLockedCards);
                int.TryParse(rumBarrelBrawlAttacksComboBox.Text, out int numberOfAttacks);
                string gameMode = rumBarrelGameModeSelectionComboBox.Text;

                StringBuilder brawlTargets = new StringBuilder();

                while (numberOfAttacks > 0)
                {
                    var kongVm = BotManager.Init(userData);
                    if (kongVm.BattleToResume)
                    {
                        outputTextBox.AppendText("**** WARNING: User is already in a battle!****\r\n");
                        outputTextBox.AppendText("***Click play match to finish it\r\n");
                        return;
                    }

                    //KongViewModel kongVm = BotManager.Init(this, userData);
                    kongVm = BotManager.StartBrawlMatch(this, userData);

                    brawlTargets.AppendLine("* Brawl: " + kongVm.BattleData.EnemyName);

                    // Populate RumBarrel
                    if (kongVm != null)
                    {
                        //repeat
                        while (kongVm.BattleData.Winner == null) // or some error occurs
                        {
                            // Refresh
                            // TODO: Shouldn't BotManager.PlayCard return this data?
                            kongVm = BotManager.GetCurrentOrLastBattle(this, userData, missingCardStrategy: grindMissingCardStrategyComboBox.Text);

                            // Show data
                            FillOutRumBarrel(kongVm);

                            // Build sim
                            BatchSim batchSim = NewSimManager.BuildLiveSim(kongVm, gameMode, iterations: iterations, extraLockedCards: extraLockedCards);

                            // Run sim
                            await Task.Run(() => NewSimManager.RunLiveSim(batchSim));

                            // Play next card
                            // Look through our simmed deck and find the first card we can legally play
                            Sim sim = batchSim.Sims.OrderByDescending(x => x.WinPercent).ThenByDescending(x => x.WinScore).FirstOrDefault();

                            BotManager.PlayCardRumBarrel(this, kongVm, sim);

                            // Some error happened
                            if (kongVm.Result == "False")
                            {
                                rumBarrelOutputTextBox.AppendText("An error has occured: " + kongVm.ResultMessage + "\r\n");
                                return;
                            }
                        }

                        // Some error happened
                        if (kongVm.Result == "False")
                        {
                            rumBarrelOutputTextBox.AppendText("An error has occured: " + kongVm.ResultMessage + "\r\n");
                            return;
                        }

                        FillOutRumBarrel(kongVm);
                    }

                    numberOfAttacks--;
                    rumBarrelBrawlAttacksComboBox.Text = numberOfAttacks.ToString();

                    int remainingAttacks = int.Parse(rumBarrelBrawlEnergyTextBox.Text) - 1;
                    rumBarrelBrawlEnergyTextBox.Text = remainingAttacks.ToString();
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on StartBrawlBattle(): \r\n" + ex);
            }
        }


        // ------------------- WAR ------------------- //

        /// <summary>
        /// Get War energy
        /// </summary>
        private void rumBarrelGetWarEnergyButton_Click(object sender, EventArgs e)
        {
            try
            {
                // Call init to get energy
                string userData = rumBarrelPlayerComboBox.Text;
                KongViewModel kongVm = BotManager.Init(userData);

                int warEnergy = kongVm.WarData.Energy;
                rumBarrelWarEnergyTextBox.Text = warEnergy.ToString();

                // Refresh
                kongVm = BotManager.GetCurrentOrLastBattle(this, userData, missingCardStrategy: grindMissingCardStrategyComboBox.Text);
                FillOutRumBarrel(kongVm);
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on PlayMatch(): \r\n" + ex);
            }
        }

        /// <summary>
        /// Start War battle
        /// </summary>
        private void rumBarrelStartWarButton_Click(object sender, EventArgs e)
        {
            try
            {
                // Parse selectedUser for kong login
                string userData = rumBarrelPlayerComboBox.Text;

                var kongVm = BotManager.Init(userData);
                if (kongVm.BattleToResume)
                {

                    outputTextBox.AppendText("**** WARNING: User is already in a battle!****\r\n");
                    outputTextBox.AppendText("***Click play match to finish it\r\n");
                    return;
                }

                kongVm = BotManager.StartWarMatch(this, userData);

                // Populate RumBarrel
                if (kongVm != null)
                {
                    // Show data
                    FillOutRumBarrel(kongVm);

                    // Some error happened
                    if (kongVm.Result == "False")
                    {
                        rumBarrelOutputTextBox.AppendText("An error has occured: " + kongVm.ResultMessage + "\r\n");
                        return;
                    }

                    outputTextBox.AppendText("** Brawling: " + kongVm.BattleData.EnemyName + "\r\n");
                }
            }

            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on StartCqBattle(): \r\n" + ex);
            }
        }


        /// <summary>
        /// Perform X War Battles
        /// </summary>
        private async void rumBarrelAutoWarButton_Click(object sender, EventArgs e)
        {
            try
            {
                // Parse selectedUser for kong login
                string userData = rumBarrelPlayerComboBox.Text;

                // Attack params
                int.TryParse(rumBarrelIterationsTextBox.Text, out int iterations);
                int.TryParse(rumBarrelCardsAheadComboBox.Text, out int extraLockedCards);
                int.TryParse(rumBarrelWarAttacksComboBox.Text, out int numberOfAttacks);
                string gameMode = "gw";


                while (numberOfAttacks > 0)
                {
                    var kongVm = BotManager.Init(userData);
                    if (kongVm.BattleToResume)
                    {
                        outputTextBox.AppendText("**** WARNING: User is already in a battle!****\r\n");
                        outputTextBox.AppendText("***Click play match to finish it\r\n");
                        return;
                    }

                    //KongViewModel kongVm = BotManager.Init(this, userData);
                    kongVm = BotManager.StartWarMatch(this, userData);

                    //rumBarrelAutoResultOutputTextBox.AppendText("* WAR: " + kongVm.BattleData.EnemyName); //+ "\r\n");

                    // Populate RumBarrel
                    if (kongVm != null)
                    {
                        //repeat
                        while (kongVm.BattleData.Winner == null) // or some error occurs
                        {
                            // Refresh
                            // TODO: Shouldn't BotManager.PlayCard return this data?
                            kongVm = BotManager.GetCurrentOrLastBattle(this, userData, missingCardStrategy: grindMissingCardStrategyComboBox.Text);

                            // Show data
                            FillOutRumBarrel(kongVm);

                            // Build sim
                            BatchSim batchSim = NewSimManager.BuildLiveSim(kongVm, gameMode, iterations: iterations, extraLockedCards: extraLockedCards);

                            // Run sim
                            await Task.Run(() => NewSimManager.RunLiveSim(batchSim));

                            // Play next card
                            // Look through our simmed deck and find the first card we can legally play
                            Sim sim = batchSim.Sims.OrderByDescending(x => x.WinPercent).ThenByDescending(x => x.WinScore).FirstOrDefault();

                            BotManager.PlayCardRumBarrel(this, kongVm, sim);

                            // Some error happened
                            if (kongVm.Result == "False")
                            {
                                rumBarrelOutputTextBox.AppendText("An error has occured: " + kongVm.ResultMessage + "\r\n");
                                return;
                            }
                        }

                        // Some error happened
                        if (kongVm.Result == "False")
                        {
                            rumBarrelOutputTextBox.AppendText("An error has occured: " + kongVm.ResultMessage + "\r\n");
                            return;
                        }

                        FillOutRumBarrel(kongVm);
                    }

                    numberOfAttacks--;
                    rumBarrelWarAttacksComboBox.Text = numberOfAttacks.ToString();

                    int remainingAttacks = int.Parse(rumBarrelWarEnergyTextBox.Text) - 1;
                    rumBarrelWarEnergyTextBox.Text = remainingAttacks.ToString();
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on StartWarBattle(): \r\n" + ex);
            }
        }

        // ------------------- CQ ------------------- //

        /// <summary>
        /// Get CQ energy for this event
        /// </summary>
        private void rumBarrelGetCqEnergyButton_Click(object sender, EventArgs e)
        {
            string selectedUser = rumBarrelPlayerComboBox.Text;
            KongViewModel kongVm = BotManager.Init(selectedUser);

            int cqEnergy = kongVm.ConquestData.Energy;
            rumBarrelCqEnergyTextBox.Text = cqEnergy.ToString();

        }

        /// <summary>
        /// Perform X CQ Battles
        /// TODO: We need checks to see if CQ is up and to check energy
        /// </summary>
        private async void rumBarrelAutoCqButton_Click(object sender, EventArgs e)
        {
            try
            {
                // Parse selectedUser for kong login
                string userData = rumBarrelPlayerComboBox.Text;

                // CQ Attack params
                int.TryParse(rumBarrelIterationsTextBox.Text, out int iterations);
                int.TryParse(rumBarrelCardsAheadComboBox.Text, out int extraLockedCards);
                int.TryParse(rumBarrelCqAttacksComboBox.Text, out int numberOfAttacks);
                string gameMode = rumBarrelGameModeSelectionComboBox.Text;

                int zoneId = Helper.GetConquestZoneId(rumBarrelCqZoneComboBox.Text);
                if (zoneId > 0 && numberOfAttacks > 0)
                {
                    while (numberOfAttacks > 0)
                    {
                        //KongViewModel kongVm = BotManager.Init(this, userData);
                        var kongVm = BotManager.StartCqMatch(this, userData, zoneId);

                        // Populate RumBarrel
                        if (kongVm != null)
                        {
                            //repeat
                            while (kongVm.BattleData.Winner == null) // or some error occurs
                            {
                                // Refresh
                                // TODO: Shouldn't BotManager.PlayCard return this data?
                                kongVm = BotManager.GetCurrentOrLastBattle(this, userData, missingCardStrategy: grindMissingCardStrategyComboBox.Text);

                                // Show data
                                FillOutRumBarrel(kongVm);

                                // Build sim
                                BatchSim batchSim = NewSimManager.BuildLiveSim(kongVm, gameMode, iterations: iterations, extraLockedCards: extraLockedCards);

                                // Run sim
                                await Task.Run(() => NewSimManager.RunLiveSim(batchSim));

                                // Play next card
                                // Look through our simmed deck and find the first card we can legally play
                                Sim sim = batchSim.Sims.OrderByDescending(x => x.WinPercent).ThenByDescending(x => x.WinScore).FirstOrDefault();

                                BotManager.PlayCardRumBarrel(this, kongVm, sim);

                                // Some error happened
                                if (kongVm.Result == "False")
                                {
                                    rumBarrelOutputTextBox.AppendText("An error has occured: " + kongVm.ResultMessage + "\r\n");
                                    return;
                                }
                            }

                            // Some error happened
                            if (kongVm.Result == "False")
                            {
                                rumBarrelOutputTextBox.AppendText("An error has occured: " + kongVm.ResultMessage + "\r\n");
                                return;
                            }

                            FillOutRumBarrel(kongVm);
                        }

                        numberOfAttacks--;

                        int remainingAttacks = int.Parse(rumBarrelCqEnergyTextBox.Text) - 1;
                        rumBarrelCqEnergyTextBox.Text = remainingAttacks.ToString();
                    }
                }
                else
                {
                    outputTextBox.AppendText("Invalid CQ Zone selected\r\n");
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on StartCqBattle(): \r\n" + ex);
            }
        }



        // -------- Non Livesim stuff ---------- //

        /// <summary>
        /// DEBUG: Start a guild surge
        /// Hardcoded to Picci that bastard
        /// </summary>
        private void rumBarrelStartGuildSurgeButton_Click(object sender, EventArgs e)
        {
            try
            {
                // Parse selectedUser for kong login
                string userData = rumBarrelPlayerComboBox.Text;

                // Surge picci
                BotManager.StartGuildSurge(this, userData, "2039770");

                // Refresh
                KongViewModel kongVm = BotManager.GetCurrentOrLastBattle(this, userData, missingCardStrategy: grindMissingCardStrategyComboBox.Text);

                // Populate RumBarrel
                if (kongVm != null)
                {
                    FillOutRumBarrel(kongVm);
                }
            }
            catch (Exception ex)
            {
                rumBarrelOutputTextBox.AppendText("Error on StartGuildSurge(): \r\n" + ex);
            }
            //target_user_id:"2039770"
        }


        /// <summary>
        /// Update deck logs file
        /// </summary>
        private void rumBarrelUpdateDeckLogs_Click(object sender, EventArgs e)
        {
            string result = DbManager.DownloadLogs(this);
            rumBarrelOutputTextBox.Text = result + "\r\n";
        }

        /// <summary>
        /// Save current kong string to the __users.txt file
        /// </summary>
        private void rumBarrelSaveKongStringButton_Click(object sender, EventArgs e)
        {
            string userInfo = "\r\n" + rumBarrelPlayerComboBox.Text;

            // Append the new user to the _users text file
            FileIO.SimpleWrite(this, "./", "__users.txt", userInfo, true);

            // Add the user in the rumBarrel/admin tab
            rumBarrelPlayerComboBox.Items.Add(rumBarrelPlayerComboBox.Text);
            adminPlayerListBox.Items.Add(rumBarrelPlayerComboBox.Text);
        }

        /// <summary>
        /// Fill the rum barrel tab with battle data
        /// </summary>
        private void FillOutRumBarrel(KongViewModel kongVm)
        {
            // Sets RumBarrel with the KongVM info
            try
            {
                rumBarrelKongNameLabel.Text = kongVm.KongName;

                //rumBarrelApiOutputTextBox.Text = kongVm.ResultMessage;

                rumBarrelTurnTextBox.Text = kongVm.BattleData.Turn;
                rumBarrelGameModeLabel.Text = kongVm.BattleData.IsAttacker ? "Battle" : "Surge";

                rumBarrelEnemyTextBox.Text = kongVm.BattleData.EnemyGuild + ": " + kongVm.BattleData.EnemyName + "\r\n" + kongVm.BattleData.EnemySize.ToString() + " cards\r\n" + kongVm.EnemyDeckSource;


                // Set known deck cards
                rumBarrelMyDeckTextBox.Text = kongVm.BattleData.GetPlayerDeck();
                rumBarrelEnemyDeckTextBox.Text = kongVm.BattleData.GetEnemyDeck(includePlayer: false, includeDominion: false, commaSeparated: false);

                // Enemy deck: Original deck and one in gauntlet format
                rumBarrelEnemyDeckInitialTextBox.Text = kongVm.BattleData.GetEnemyDeck(includePlayer: false, includeDominion: false);
                rumBarrelEnemyDeckGauntletTextBox.Text = kongVm.BattleData.GetEnemyDeck(includePlayer: true, includeDominion: true);
                rumBarrelEnemyDeckPossibleRemainingCardsTextBox.Text = string.Join("\r\n", kongVm.BattleData.EnemyCardsRemaining);

                // Set BGE and forts
                rumBarrelMyFortTextBox.Text = string.Join(",", kongVm.BattleData.PlayerForts);
                rumBarrelEnemyFortTextBox.Text = string.Join(",", kongVm.BattleData.EnemyForts);
                rumBarrelBgeTextBox.Text = kongVm.BattleData.BGE;
                rumBarrelMyBgeTextBox.Text = kongVm.BattleData.PlayerBGE;
                rumBarrelEnemyBgeTextBox.Text = kongVm.BattleData.EnemyBGE;
                rumBarrelMyDominionTextBox.Text = kongVm.BattleData.PlayerDominion;
                rumBarrelEnemyDominionTextBox.Text = kongVm.BattleData.EnemyDominion;

                // Set draw order
                rumBarrelPlayerHandTextBox.Text = string.Join("\r\n", kongVm.BattleData.PlayerHand.Select(x => x.Name).ToList());
                rumBarrelDrawOrderTextBox.Text = string.Join("\r\n", kongVm.BattleData.PlayerDrawOrder.Select(x => x.Name).ToList());

                // Is this match over?
                if (kongVm.BattleData.Winner.HasValue)
                {
                    string winOrLoseOutput = kongVm.KongName + " - " + (kongVm.BattleData.Winner.Value ? "win" : "**LOST**") + " - " + kongVm.BattleData?.EnemyName + "\r\n";

                    string enemyGuild = !String.IsNullOrWhiteSpace(kongVm.BattleData?.EnemyGuild) ? kongVm.BattleData?.EnemyGuild : "_UNKNOWN";
                    string enemyDeckOutput = enemyGuild + "_def_" + kongVm.BattleData.GetEnemyDeck(includePlayer: true, includeDominion: true) + "\r\n";

                    rumBarrelAutoResultOutputTextBox.AppendText(winOrLoseOutput);
                    outputTextBox.AppendText(enemyDeckOutput);

                    rumBarrelOutputTextBox.Text = winOrLoseOutput;

                    Console.WriteLine(winOrLoseOutput);
                    Console.WriteLine(enemyDeckOutput);
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "GetBattleResults error: " + ex);
            }

            // TODO: Fill out these
            // Clear previous enemy deck stuff
            //rumBarrelEnemyDeckPossibleTextBox.Text = "";

        }

        #endregion

        #region CrowsNest

        /// <summary>
        /// Call API init and fill in the crows nest / builder form
        /// </summary>
        private void crowsNestRefreshButton_Click(object sender, EventArgs e)
        {
            StringBuilder sb = new StringBuilder();

            try
            {
                string userData = rumBarrelPlayerComboBox.Text;

                //HelmOutputTextBox.Text = ApiManager.CallApi();
                KongViewModel kongVm = BotManager.Init(userData, true);

                // Set user data
                if (kongVm != null)
                {
                    crowsNestPlayerLabel.Text = kongVm.KongName + "\r\n" + kongVm.Faction.Name + "\r\n"; // We don't get this yet + "\r\n" + kongVm.EnemyDeckSource;
                    if (kongVm.BattleToResume) crowsNestPlayerLabel.Text += "** In Battle with " + kongVm.BattleData.EnemyName + "**";

                    // ------------------------------------
                    // Account details
                    // ------------------------------------
                    RefreshAccountAndCards(kongVm);

                    // ------------------------------------
                    // Decks - attack/defense deck, dominions
                    // ------------------------------------
                    RefreshPlayerDeckAndDominions(kongVm);

                    // ------------------------------------
                    // Missions
                    // ------------------------------------
                    crowsNestQuestListBox.Items.Clear();
                    foreach (Quest quest in kongVm.Quests)
                    {
                        //// Don't show guild quests if complete
                        //if ((quest.Id == -1 || quest.Id == -2 || quest.Id == -3) &&
                        //    quest.Progress == quest.MaxProgress) continue;

                        // Don't show guild quests 
                        if (quest.Id == -1 || quest.Id == -2 || quest.Id == -3) continue;

                        // Don't show daily quests (Unsure on range)
                        if (quest.Id >= 2001 && quest.Id <= 2045) continue;

                        sb = new StringBuilder();
                        sb.Append(quest.Id);
                        sb.Append("\t");
                        sb.Append(quest.Name);
                        sb.Append("\t");
                        sb.Append(quest.Progress);
                        sb.Append("\t");
                        sb.Append(quest.MaxProgress);
                        sb.Append("\t");
                        if (quest.MissionId > 0)
                            sb.Append(quest.MissionId);
                        else if (quest.QuestId > 0)
                            sb.Append(quest.QuestId);
                        else
                            sb.Append("-1");

                        crowsNestQuestListBox.Items.Add(sb.ToString());
                    }

                    // ------------------------------------
                    // Current Event
                    // ------------------------------------
                    sb = new StringBuilder();

                    if (kongVm.BrawlActive)
                    {
                        crowsNestActionsGroupBox.Text = "EVENT - BRAWL";
                        crowsNestEventEnergyTextBox.Text = kongVm.BrawlData.Energy.ToString();

                        // How do we distinguish Brawl from GBrawl for the scoreboard aspect?
                        sb.AppendLine("Current Rank: \t" + kongVm.BrawlData.CurrentRank);
                        sb.AppendLine("Wins: \t" + kongVm.BrawlData.Wins);
                        sb.AppendLine("Losses: \t" + kongVm.BrawlData.Losses);
                        sb.AppendLine("Points per win: \t" + kongVm.BrawlData.PointsPerWin);
                        sb.AppendLine("");
                        sb.AppendLine("Win Streak: \t" + kongVm.BrawlData.WinStreak);
                    }
                    if (kongVm.ConquestActive)
                    {
                        crowsNestActionsGroupBox.Text = "EVENT - CQ";
                        crowsNestEventEnergyTextBox.Text = kongVm.ConquestData.Energy.ToString();
                    }
                    if (kongVm.RaidActive || kongVm.RaidRewardsActive)
                    {
                        crowsNestActionsGroupBox.Text = "EVENT - RAID";
                        crowsNestEventEnergyTextBox.Text = kongVm.RaidData.Energy.ToString();

                        sb.AppendLine("Boss Level: \t" + kongVm.RaidData.Level);
                        sb.AppendLine("Boss HP: \t" + kongVm.RaidData.Health + "/" + kongVm.RaidData.MaxHealth);
                        sb.AppendLine("Time left: \t" + kongVm.RaidData.TimeLeft.Hours + ":" + kongVm.RaidData.TimeLeft.Minutes);

                        // Scoreboard
                        eventOutputTextBox.Clear();
                        var players = new StringBuilder();
                        foreach (var player in kongVm.RaidData.RaidLeaderboard.OrderByDescending(x => int.Parse(x.Damage)))
                        {
                            players.AppendLine(player.Damage + "\t" + player.Name + "\t" + player.UserId);
                        }

                        // Autoraid button - show whenever raid is active
                        crowsNestAutoRaidButton.Visible = kongVm.RaidActive;
                        crowsNestLivesimRaidButton.Visible = kongVm.RaidActive;

                        eventOutputTextBox.Text = players.ToString();
                    }
                    if (kongVm.WarActive)
                    {
                        crowsNestActionsGroupBox.Text = "EVENT - WAR";
                        crowsNestEventEnergyTextBox.Text = kongVm.WarData.Energy.ToString();
                    }

                    outputTextBox.AppendText(sb.ToString() + "\r\n");


                    // Output box text
                    if (kongVm.Result != "False")
                    {
                        outputTextBox.AppendText("API: Called Init\r\n");
                    }
                    else
                    {
                        outputTextBox.AppendText("API: Failed on Init\r\n");
                    }
                }
            }
            catch (Exception ex)
            {
                string errorMessage = "Error on crows nest refresh: \r\n" + ex + "\r\n";
                Helper.OutputWindowMessage(this, errorMessage);
            }
        }

        /// <summary>
        /// Reset dominion
        /// </summary>
        private void crowsNestResetDominionButton_Click(object sender, EventArgs e)
        {
            KongViewModel kongVm = new KongViewModel();

            
                this.stopProcess = false;
                this.workerThread = new Thread(() => {
                try
                {
                    // Parse selectedUser for kong login
                    string userData = rumBarrelPlayerComboBox.Text;

                    // Dominions
                    string resetDominionName = crowsNestResetDominionComboBox.Text;
                    Card resetDominion = CardManager.GetPlayerCardByName(resetDominionName);

                    string alphaName = crowsNestAlphaDominionTextBox.Text;
                    string nexusName = crowsNestFactionDominionTextBox.Text;
                    Card alphaDominion = CardManager.GetPlayerCardByName(alphaName);
                    Card nexusDominion = CardManager.GetPlayerCardByName(nexusName);

                    // Get dominion shards - min 1000 to do a reset? 
                    int.TryParse(crowsNestDomShardsTextBox.Text, out int shards);

                    // Error checks
                    if (shards < 360)
                    {
                        outputTextBox.AppendText("Reset failed: 360 spare shards needed to reset\r\n");
                        return;
                    }
                    if (resetDominion == null)
                    {
                        outputTextBox.AppendText("Reset failed: " + resetDominionName + " not found\r\n");
                        return;
                    }

                    this.stopProcess = false;
                    this.workerThread = new Thread(() =>
                    {
                        // Alpha Dominion
                        if (resetDominionName.Contains("Alpha") && alphaDominion != null)
                        {
                            outputTextBox.AppendText("Resetting " + alphaName + " to " + resetDominionName + "\r\n");
                            kongVm = BotManager.ResetDominion(userData, alphaDominion.CardId, resetDominion.CardId, setDominionAlpha: true);

                            // Some error happened
                            if (kongVm.Result == "False")
                            {
                                outputTextBox.AppendText("An error has occured: " + kongVm.ResultMessage + "\r\n");
                            }
                            else
                            {
                                crowsNestAlphaDominionTextBox.Text = resetDominionName;
                            }
                        }
                        // Nexus Dominion
                        else if (resetDominionName.Contains("Nexus") && nexusName != null)
                        {
                            outputTextBox.AppendText("Resetting " + nexusName + " to " + resetDominionName + "\r\n");
                            kongVm = BotManager.ResetDominion(userData, nexusDominion.CardId, resetDominion.CardId, setDominionAlpha: false);

                            // Some error happened
                            if (kongVm.Result == "False")
                            {
                                outputTextBox.AppendText("An error has occured: " + kongVm.ResultMessage + "\r\n");
                            }
                            else
                            {
                                crowsNestFactionDominionTextBox.Text = resetDominionName;
                            }
                        }
                        // Error check
                        else
                        {
                            outputTextBox.AppendText("Unknown Dominion error\r\n");
                        }

                        outputTextBox.AppendText(kongVm.PtuoMessage);
                    });
                    this.workerThread.Start();
                }
                catch (Exception ex)
                {
                    Helper.OutputWindowMessage(this, "Error on crows nest build dominion: \r\n" + ex + "\r\n");
                }
            });
            this.workerThread.Start();
        }

        /// <summary>
        /// Buy gold until the player runs out of gold or gets near max SP
        /// </summary>
        private void crowsNestBuyGoldButton_Click(object sender, EventArgs e)
        {
            KongViewModel kongVm = new KongViewModel();

            try
            {
                this.stopProcess = false;
                this.workerThread = new Thread(() => BuyMaxGold(sender, e));
                this.workerThread.Start();

            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on crows nest buy gold: \r\n" + ex + "\r\n");
            }

            // Call init
            crowsNestRefreshButton_Click(sender, e);
        }

        /// <summary>
        /// Buy X gold packs. Its now 10
        /// </summary>
        private void crowsNestBuyFiveGoldButton_Click(object sender, EventArgs e)
        {
            try
            {
                this.stopProcess = false;
                this.workerThread = new Thread(() =>
                {
                    outputTextBox.AppendText("Buying gold\r\n");

                    string kongInfo = rumBarrelPlayerComboBox.Text;
                    KongViewModel kongVm = BotManager.BuyGold(this, kongInfo, 10);
                    // Api or error output
                    if (kongVm.Result == "False")
                    {
                        outputTextBox.AppendText("\r\n" + kongVm.ResultMessage);
                    }
                });
                this.workerThread.Start();



            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on crows nest buy gold: \r\n" + ex + "\r\n");
            }

            // Call init
            crowsNestRefreshButton_Click(sender, e);
        }

        /// <summary>
        /// Engage in pvp battles until depleted
        /// </summary>
        private void crowsNestAutoPvPButton_Click(object sender, EventArgs e)
        {
            string kongInfo = rumBarrelPlayerComboBox.Text;


            this.stopProcess = false;
            this.workerThread = new Thread(() =>
            {
                // If looping grind
                bool loopGrind = crowsNestGrindLoopCheckBox.Checked;
                bool doGuildQuests = crowsNestMissionAttackGQsCheckBox.Checked;
                bool grindedOnce = false;

                // Get kong name
                KongViewModel kongVm = new KongViewModel();
                ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                string kongName = kongVm.KongName;

                do
                {
                    try
                    {
                        // Call Init to get the player's stats and quest progress
                        kongVm = BotManager.Init(kongInfo);
                        if (kongVm.Result != "False")
                        {
                            //outputTextBox.AppendText(kongVm.KongName + " - Grinding\r\n");

                            // Player info
                            string name = kongVm.KongName;
                            int stamina = kongVm.UserData.Stamina;
                            int maxStamina = kongVm.UserData.MaxStamina;
                            int energy = kongVm.UserData.Energy;
                            int maxEnergy = kongVm.UserData.MaxEnergy;
                            List<MissionCompletion> missionCompletions = kongVm.MissionCompletions;
                            Quest quest1 = kongVm.Quests.Where(x => x.Id == -1).FirstOrDefault();
                            Quest quest2 = kongVm.Quests.Where(x => x.Id == -2).FirstOrDefault();
                            Quest quest3 = kongVm.Quests.Where(x => x.Id == -3).FirstOrDefault();

                            // ---------------------------------------------
                            // Pvp grind - all attacks 
                            // ---------------------------------------------
                            kongVm = BotManager.AutoPvpBattles(kongInfo, stamina);

                            // Show results of pvp
                            if (kongVm.Result == "False")
                            {
                                outputTextBox.AppendText(kongVm.KongName + " - Grinding error: " + kongVm.ResultMessage + "\r\n");
                            }
                            else
                            {
                                outputTextBox.AppendText(kongVm.PtuoMessage);
                                kongVm.PtuoMessage = "";
                            }

                            // ---------------------------------------------
                            // Mission Grind. If it has at least 100 energy, try to do missions
                            // ---------------------------------------------
                            if (energy > 125)
                            {
                                // Campaign if its up
                                if (kongVm.CampaignActive)
                                {
                                    while (energy > 125)
                                    {
                                        if (kongVm.Result != "False" && kongVm.CampaignActive)
                                        {
                                            int difficulty = 1;
                                            string difficultyMode = "Normal";
                                            if (kongVm.CampaignData.NormalRewardsToCollect)
                                            {
                                            }
                                            else if (kongVm.CampaignData.HeroicRewardsToCollect)
                                            {
                                                difficulty = 2;
                                                difficultyMode = "Heroic";
                                            }
                                            else if (kongVm.CampaignData.MythicRewardsToCollect)
                                            {
                                                difficulty = 3;
                                                difficultyMode = "Mythic";
                                            }
                                            else
                                            {
                                                adminOutputTextBox.AppendText(kongVm.KongName + " - Finished with campaign\r\n");
                                                break;
                                            }

                                            // Get active deck
                                            int attackDeckIndex = int.Parse(kongVm.UserData.ActiveDeck);

                                            Dictionary<string, Object> cards = new Dictionary<string, Object>();
                                            Dictionary<string, Object> reserveCards = new Dictionary<string, Object>();
                                            UserDeck attackDeck = kongVm.UserDecks[attackDeckIndex - 1];

                                            int commanderId = attackDeck.Commander.CardId;

                                            foreach (var cardKvp in attackDeck.Cards)
                                            {
                                                cards.Add(cardKvp.Key.CardId.ToString(), cardKvp.Value.ToString());
                                            }

                                            // Start campaign
                                            adminOutputTextBox.AppendText(kongVm.KongName + " - Autoing " + difficultyMode + " campaign " + kongVm.CampaignData.Id + " with existing deck\r\n");
                                            kongVm = BotManager.StartCampaign(kongInfo, kongVm.CampaignData.Id, difficulty, commanderId, cards, reserveCards);

                                            adminOutputTextBox.AppendText(kongVm.KongName + " - Campaign " + (kongVm.BattleData.Winner.HasValue && kongVm.BattleData.Winner.Value ? "won" : "lost") + "\r\n");


                                            kongVm = BotManager.Init(kongInfo);
                                            energy = kongVm.UserData.Energy;
                                        }
                                    }
                                }
                                else
                                {

                                    kongVm = BotManager.AutoMissions(kongInfo, 0, missionCompletions, kongVm.Quests ?? new List<Quest>(),
                                        doGuildQuests, true, true, false,
                                        quest1, quest2, quest3);
                                }
                            }

                            // Show results of missions
                            if (kongVm.Result == "False")
                            {
                                outputTextBox.AppendText(kongVm.KongName + " - Grinding error: " + kongVm.ResultMessage + "\r\n");
                            }
                            else
                            {
                                outputTextBox.AppendText(kongVm.PtuoMessage);
                            }
                        }
                        else
                        {
                            outputTextBox.AppendText(kongVm.KongName + " - " + kongVm.ApiStatName + " - API error\r\n");
                        }
                    }
                    catch (Exception ex)
                    {
                        Helper.OutputWindowMessage(this, "Error on crowsNestAutoPvPButton_Click(): \r\n" + ex);
                    }

                    crowsNestRefreshButton_Click(sender, e);

                    // If looping, check back in 2 hours
                    loopGrind = crowsNestGrindLoopCheckBox.Checked;
                    if (loopGrind)
                    {
                        outputTextBox.AppendText(kongVm.KongName + " - Grinding again in 2 hours\r\n");
                        //Thread.Sleep(60000); // 1 minute for testing
                        Thread.Sleep(TimeSpan.FromHours(2));
                        grindedOnce = true;
                    }
                    // Say we're stopping grind if we ran it
                    else if (grindedOnce)
                    {
                        adminOutputTextBox.AppendText("Stopping grind\r\n");
                    }
                }
                while (loopGrind);
            });
            this.workerThread.Start();


        }

        /// <summary>
        /// Buy as many epic/legend shard packs as possible
        /// </summary>
        private void crowsNestUseShardsButton_Click(object sender, EventArgs e)
        {
            try
            {
                string kongInfo = rumBarrelPlayerComboBox.Text;

                outputTextBox.AppendText("Consuming all epic/legend shards\r\n");

                KongViewModel kongVm = BotManager.ConsumeShards(this, kongInfo);

                // Api or error output
                if (kongVm.Result == "False")
                {
                    outputTextBox.AppendText("\r\n" + kongVm.ResultMessage);
                }
                outputTextBox.AppendText(kongVm.PtuoMessage);

                // Call init
                crowsNestRefreshButton_Click(sender, e);
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on crowsNestUseShardsButton_click: \r\n" + ex + "\r\n");
            }

            // Call init
            crowsNestRefreshButton_Click(sender, e);
        }

        /// <summary>
        /// Engage in raid battles until depleted
        /// </summary>
        private void crowsNestAutoRaidButton_Click(object sender, EventArgs e)
        {
            try
            {
                string kongInfo = rumBarrelPlayerComboBox.Text;

                outputTextBox.AppendText("Raid: Autoing");
                KongViewModel kongVm = BotManager.AutoRaidBattles(kongInfo);

                // Api or error output
                if (kongVm.Result == "False")
                {
                    outputTextBox.AppendText("\r\n" + kongVm.ResultMessage);
                }
                else
                {
                    // Show win/loss and update event energy
                    outputTextBox.AppendText(kongVm.PtuoMessage);
                    if (kongVm?.RaidData?.Energy > 0) crowsNestEventEnergyTextBox.Text = kongVm.RaidData.Energy.ToString();
                }

            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on crowsNestAutoRaidButton_click: \r\n" + ex + "\r\n");
            }

            // Call init
            crowsNestRefreshButton_Click(sender, e);
        }

        /// <summary>
        /// Livesim the raid
        /// </summary>
        private void crowsNestLivesimRaidButton_Click(object sender, EventArgs e)
        {

            string kongInfo = rumBarrelPlayerComboBox.Text;

            KongViewModel kongVm = new KongViewModel();
            ApiManager.GetKongInfoFromString(kongVm, kongInfo);
            string kongName = kongVm.KongName;

            try
            {
                kongVm = BotManager.Init(kongInfo);
                if (kongVm.Result != "False")
                {
                    bool inBattle = kongVm.BattleToResume;
                    int energy = kongVm.RaidData.Energy;
                    int wins = 0;
                    int losses = 0;
                    int raidLevel = kongVm.RaidData.Level;
                    int raidId = kongVm.RaidData.Id;

                    // Start of raid: no raid level
                    if (raidLevel < 1) raidLevel = 1;

                    ControlExtensions.InvokeEx(outputTextBox, x => outputTextBox.AppendText(kongVm.KongName + " - " + energy + " energy\r\n"));

                    // Mid-battle, don't interrupt if toggled
                    if (kongVm.BattleToResume)
                    {
                        outputTextBox.AppendText(kongVm.KongName + " is already in a battle! It must be finished\r\n");
                    }
                    else
                    {
                        while (energy > 0)
                        {
                            kongVm = new KongViewModel("getRaidInfo");
                            ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                            ApiManager.CallApi(kongVm);

                            // Refresh the raid level
                            raidLevel = kongVm.RaidData.Level;

                            // Start match
                            if (!inBattle) kongVm = BotManager.StartRaidBattle(this, kongInfo, raidId, raidLevel);


                            // Match started successfully
                            if (kongVm != null)
                            {
                                while (kongVm.BattleData.Winner == null)
                                {
                                    // Refresh the battle
                                    kongVm = BotManager.GetCurrentOrLastBattle(this, kongInfo, CONSTANTS.CURRENT_RAID_ASSAULTS);

                                    // If there is an enemy dominion, it should be an enemy fort instead
                                    if (!string.IsNullOrWhiteSpace(kongVm.BattleData.EnemyDominion))
                                    {
                                        kongVm.BattleData.EnemyForts.Add(kongVm.BattleData.EnemyDominion);
                                        kongVm.BattleData.EnemyDominion = "";
                                    }

                                    // We added the enemy assaults that were in the deck, 
                                    // now base the level of the remaining cards on the current raid level
                                    int enemyCardAverageLevel = 4;
                                    if (raidLevel >= 22) enemyCardAverageLevel = 10;
                                    else if (raidLevel >= 20) enemyCardAverageLevel = 9;
                                    else if (raidLevel >= 18) enemyCardAverageLevel = 8;
                                    else if (raidLevel >= 16) enemyCardAverageLevel = 7;
                                    else if (raidLevel >= 14) enemyCardAverageLevel = 6;
                                    else if (raidLevel >= 12) enemyCardAverageLevel = 5;

                                    // Modify remaining cards to use average level
                                    for (int i = 0; i < kongVm.BattleData.EnemyCardsRemaining.Count; i++)
                                    {
                                        if (enemyCardAverageLevel < 10)
                                            kongVm.BattleData.EnemyCardsRemaining[i] = kongVm.BattleData.EnemyCardsRemaining[i] + "-" + enemyCardAverageLevel;
                                    }

                                    // Then build and run a sim
                                    BatchSim batchSim = NewSimManager.BuildLiveSim(kongVm, "pvp", iterations: 1000, extraLockedCards: 1);
                                    NewSimManager.RunLiveSim(batchSim);
                                    Sim sim = batchSim.Sims.OrderByDescending(x => x.WinPercent).ThenByDescending(x => x.WinScore).FirstOrDefault();

                                    // Debug - make sure this looks good
                                    string winningSimString = sim.SimToString();

                                    // For some reason this will be null - it needs to be rerun
                                    if (sim.ResultDeck == null)
                                    {
                                        Console.WriteLine("NULL sim.ResultDeck - here's the sim string we tried: " + sim.SimToString());
                                        outputTextBox.AppendText("NULL sim.ResultDeck - here's the sim string we tried: " + sim.SimToString() + "\r\n");
                                        continue;
                                    }

                                    // Play the next card
                                    BotManager.PlayCard(this, kongVm, sim);

                                    if (kongVm.Result == "False")
                                    {
                                        outputTextBox.AppendText(kongVm.KongName + ": An error has occured: " + kongVm.ResultMessage + "\r\n");
                                        break;
                                    }
                                }

                                // Break out of the main loop
                                if (kongVm.Result == "False") break;

                                // Did we win or lose?
                                if (kongVm.BattleData.Winner == true)
                                {
                                    outputTextBox.AppendText(kongVm.KongName + " vs. " + CONSTANTS.CURRENT_RAID.Replace("-20", "") + ": Win\r\n");
                                    wins++;
                                }
                                else
                                {
                                    outputTextBox.AppendText(kongVm.KongName + " vs. " + CONSTANTS.CURRENT_RAID.Replace("-20", "") + ": **LOSS**\r\n");
                                    losses++;
                                }
                            }
                            // Match was not started successfully
                            else
                            {
                                outputTextBox.AppendText(kongVm.KongName + ": An error has occured: " + kongVm.ResultMessage + "\r\n");
                                break;
                            }

                            // Flag we're not in battle
                            inBattle = false;
                            // Decrease energy
                            energy--;
                        }//loop

                        // Total wins / losses
                        if (wins + losses > 0)
                        {
                            outputTextBox.AppendText(kongVm.KongName + " - " + wins + "/" + (wins + losses) + "\r\n");
                        }
                    }
                }
                else
                {
                    adminOutputTextBox.AppendText("API error when using this kong string: " + kongVm.ResultMessage);
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, kongName + ": Error on grindRaidAttackButton_Click(): \r\n" + ex.Message);
            }
            finally
            {
            }
        }

        /// <summary>
        /// Set a player's attack deck
        /// </summary>
        private void crowsNestSetDeckButton_Click(object sender, EventArgs e)
        {
            try
            {
                var control = (Button)sender;
                var controlName = control.Name;

                // Set attack or defense?
                bool activeDeck = true;
                if (controlName.Contains("Defense")) activeDeck = false;

                string userInfo = rumBarrelPlayerComboBox.Text;
                string deckString = crowsNestSetDeckTextBox.Text.Trim();

                // Deckstring: If there are tabs or colons, try to parse out the deck
                // Format is either
                // player:deck
                // player:winrate:deck
                // player:winrate:deck:guild
                string[] deckStringSplitter = deckString.Split(new char[] { ':', '\t' }, 4);
                if (deckStringSplitter.Length == 2) deckString = deckStringSplitter[1];
                else if (deckStringSplitter.Length == 3) deckString = deckStringSplitter[2];
                else if (deckStringSplitter.Length == 4) deckString = deckStringSplitter[2];

                //string finalDeckString = 
                string dominion1 = crowsNestAlphaDominionTextBox.Text;
                string dominion2 = crowsNestFactionDominionTextBox.Text;
                bool rebuildDominion = crowsNestRebuildDominionCheckBox.Checked;

                outputTextBox.AppendText("Setting deck.. (Note: If respeccing dominion this can take some time)\r\n");
                KongViewModel kongVm = BotManager.SetDeck(userInfo, deckString, dominion1, dominion2, settingActiveDeck: activeDeck, rebuildDominion: rebuildDominion);

                // Api or error output
                if (kongVm.Result == "False")
                {
                    outputTextBox.AppendText(kongVm.GetResultMessage());
                    // Call init
                    crowsNestRefreshButton_Click(sender, e);
                }
                else
                {
                    outputTextBox.Clear();
                    outputTextBox.AppendText((activeDeck ? "Attack" : "Defense") + " deck set\r\n");
                    outputTextBox.AppendText(kongVm.PtuoMessage);

                    // Get new decks
                    RefreshPlayerDeckAndDominions(kongVm);
                }

            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on crows nest set attack deck: \r\n" + ex + "\r\n");
            }
        }

        /// <summary>
        /// Return guild attack/defense decks
        /// </summary>
        private void crowsNestFetchGuildDecks_Click(object sender, EventArgs e)
        {
            try
            {
                string userData = rumBarrelPlayerComboBox.Text;
                KongViewModel kongVm = BotManager.UpdateFaction(this, userData);

                // Api or error output
                if (kongVm.Result == "False")
                {
                    outputTextBox.AppendText("\r\n" + kongVm.ResultMessage);
                }
                else
                {
                    outputTextBox.Clear();

                    // Show player / playerIDs
                    if (CONFIG.role == "level3" || CONFIG.role == "newLevel3" || debugMode == true)
                    {
                        foreach (var pi in kongVm.PlayerInfo.OrderBy(x => x.Name))
                        {
                            outputTextBox.AppendText(pi.Name + "\t" + pi.UserId + "\r\n");
                        }
                    }

                    outputTextBox.AppendText("\r\n");
                    outputTextBox.AppendText("\r\n");

                    // List guild attack decks
                    foreach (var pi in kongVm.PlayerInfo.OrderBy(x => x.Name))
                    {
                        outputTextBox.AppendText(kongVm.Faction.Name + "_atk_" + pi.Name + ": " + pi.ActiveDeck.DeckToString() + "\r\n");
                    }

                    outputTextBox.AppendText("\r\n");
                    outputTextBox.AppendText("\r\n");

                    // List guild defense decks
                    foreach (var pi in kongVm.PlayerInfo.OrderBy(x => x.Name))
                    {
                        outputTextBox.AppendText(kongVm.Faction.Name + "_def_" + pi.Name + ": " + pi.DefenseDeck.DeckToString() + "\r\n");
                    }
                }

            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on crows nest set attack deck: \r\n" + ex + "\r\n");
            }

            // Call init
            crowsNestRefreshButton_Click(sender, e);
        }

        /// <summary>
        /// Refill mission energy double-clicking the mission thing. Be careful with this!
        /// </summary>
        private void missionEnergyPictureBox_DoubleClick(object sender, EventArgs e)
        {
            try
            {
                string userInfo = rumBarrelPlayerComboBox.Text;
                KongViewModel kongVm = BotManager.MissionRefill(this, userInfo);

                // Api or error output
                if (kongVm.Result != "False")
                {
                    outputTextBox.AppendText("Refilling: " + kongVm.KongName + "\r\n");
                    crowsNestMissionEnergyTextBox.Text = kongVm.UserData.Energy.ToString();
                    crowsNestWbTextBox.Text = kongVm.UserData.Warbonds.ToString();
                }
                else
                {
                    outputTextBox.AppendText(kongVm.KongName + ": Error when doing a mission refill: " + kongVm.ResultMessage);
                }

            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on crows nest set attack deck: \r\n" + ex + "\r\n");
            }
        }

        /// <summary>
        /// Buy up to 20 gold packs
        /// </summary>
        private void goldPictureBox_Click(object sender, EventArgs e)
        {
            try
            {
                this.stopProcess = false;
                this.workerThread = new Thread(() =>
                {
                    outputTextBox.AppendText("Buying 30 gold packs\r\n");

                    string kongInfo = rumBarrelPlayerComboBox.Text;
                    KongViewModel kongVm = BotManager.BuyGold(this, kongInfo, 30);
                    // Api or error output
                    if (kongVm.Result == "False")
                    {
                        outputTextBox.AppendText("\r\n" + kongVm.ResultMessage);
                    }
                });
                this.workerThread.Start();



            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on crows nest buy gold: \r\n" + ex + "\r\n");
            }

            // Call init
            crowsNestRefreshButton_Click(sender, e);
        }

        /// <summary>
        /// Buy gold until the user runs out of gold, or maxes SP, or maxes Inventory
        /// </summary>
        private void BuyMaxGold(object sender, EventArgs e)
        {
            KongViewModel kongVm = new KongViewModel();

            try
            {
                // Call init
                crowsNestRefreshButton_Click(sender, e);

                // Get inventory, gold, and salvage
                string userData = rumBarrelPlayerComboBox.Text;
                int.TryParse(crowsNestGoldTextBox.Text, out int gold);
                int.TryParse(crowsNestInventoryTextBox.Text, out int inventory);
                int.TryParse(crowsNestMaxInventoryTextBox.Text, out int maxInventory);
                int.TryParse(crowsNestSalvageTextBox.Text, out int salvage);
                int.TryParse(crowsNestMaxSalvageTextBox.Text, out int maxSalvage);

                // If gold is less then 2000, don't do anything
                if (gold < 2000)
                {
                    outputTextBox.AppendText("Not enough money\r\n");
                    return;
                }


                // Buy up to 500,000 gold, but several things can stop this loop early
                for (int i = 0; i < 250; i++)
                {
                    // ------------------------------
                    // Buy gold packs
                    // ------------------------------
                    outputTextBox.AppendText("Buying gold\r\n");
                    kongVm = BotManager.BuyGold(this, userData);

                    // Some error happened
                    if (kongVm.Result == "False")
                    {
                        outputTextBox.AppendText("\r\n" + kongVm.ResultMessage + "\r\n");
                        if (kongVm.ResultMessage.Contains("You cannot afford")) break;
                    }

                    // ------------------------------
                    // Now, try to salvage commons/rares unless our SP is full
                    // ------------------------------
                    if (salvage < 0 || maxSalvage <= 0 || (maxSalvage - salvage < 100))
                    {
                        outputTextBox.AppendText("SP is near full. Stopping the gold train\r\n");
                        break;
                    }
                    else
                    {
                        kongVm = BotManager.SalvageL1CommonsAndRares(this, userData);
                        outputTextBox.AppendText("Salvaging commons and rares\r\n");
                    }

                    // ------------------------------
                    // Call init and refresh counts
                    // ------------------------------
                    crowsNestRefreshButton_Click(sender, e);
                    int.TryParse(crowsNestGoldTextBox.Text, out gold);
                    int.TryParse(crowsNestInventoryTextBox.Text, out inventory);
                    int.TryParse(crowsNestMaxInventoryTextBox.Text, out maxInventory);
                    int.TryParse(crowsNestSalvageTextBox.Text, out salvage);
                    int.TryParse(crowsNestMaxSalvageTextBox.Text, out maxSalvage);


                    // Check Inventory before buying gold packs
                    if (inventory <= 0 || maxInventory <= 0 || (maxInventory - inventory < 20))
                    {
                        outputTextBox.AppendText("Inventory is full and we alreadt tried to salvage commons/rares\r\n");
                        break;
                    }
                    // Check salvage
                    if (salvage < 0 || maxSalvage <= 0 || (maxSalvage - salvage < 100))
                    {
                        outputTextBox.AppendText("SP is full\r\n");
                        break;
                    }

                    // At the moment this is somehow breaking
                    // break;
                }


                //crowsNestRefreshButton_Click(sender, e);
                int.TryParse(crowsNestInventoryTextBox.Text, out inventory);
                int.TryParse(crowsNestMaxInventoryTextBox.Text, out maxInventory);
                int.TryParse(crowsNestSalvageTextBox.Text, out salvage);
                int.TryParse(crowsNestMaxSalvageTextBox.Text, out maxSalvage);

            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on crows nest buy gold: \r\n" + ex + "\r\n");
            }
        }
        
        /// <summary>
        /// Claim event rewards
        /// </summary>
        private void crowsNestEventRewardsButton_Click(object sender, EventArgs e)
        {
            KongViewModel kongVm2 = new KongViewModel();
            string kongInfo = rumBarrelPlayerComboBox.Text;

            try
            {
                KongViewModel kongVm = new KongViewModel();
                string result = "";

                // Which events are active
                bool warRewardsActive = false;
                bool conquestRewardsActive = false;
                bool brawlRewardsActive = false;
                bool raidRewardsActive = false;
                string raidId = "0";

                kongVm = BotManager.Init(kongInfo);
                if (kongVm.Result != "False")
                {
                    warRewardsActive = kongVm.WarRewardsActive;
                    brawlRewardsActive = kongVm.BrawlRewardsActive;
                    conquestRewardsActive = kongVm.ConquestRewardsActive;
                    raidRewardsActive = kongVm.RaidRewardsActive;

                    if (kongVm?.RaidData?.Id > 0) raidId = kongVm?.RaidData?.Id.ToString();
                }

                // Attempt to claim raid reward
                if (raidRewardsActive)
                {
                    kongVm = BotManager.ClaimRaidReward(this, kongInfo, raidId);
                    if (kongVm.Result != "False") result = kongVm.KongName + ": Claimed Raid rewards\r\n";
                    else result = kongVm.KongName + ": Failed - " + kongVm.ResultMessage.Replace("\r\n", "") + "\r\n";
                    outputTextBox.AppendText(result);
                }
                // Attempt to claim Brawl reward
                if (brawlRewardsActive)
                {
                    kongVm = BotManager.ClaimBrawlReward(this, kongInfo);
                    if (kongVm.Result != "False") result = kongVm.KongName + ": Claimed Brawl rewards\r\n";
                    else result = kongVm.KongName + ": Failed - " + kongVm.ResultMessage.Replace("\r\n", "") + "\r\n";
                    outputTextBox.AppendText(result);
                }
                // Attempt to claim CQ reward
                if (conquestRewardsActive)
                {
                    kongVm = BotManager.ClaimConquestReward(this, kongInfo);
                    if (kongVm.Result != "False") result = kongVm.KongName + ": Claimed CQ rewards\r\n";
                    else result = kongVm.KongName + ": Failed - " + kongVm.ResultMessage.Replace("\r\n", "") + "\r\n";
                    outputTextBox.AppendText(result);
                }
                // Attempt to claim War reward
                if (warRewardsActive)
                {
                    kongVm = BotManager.ClaimFactionWarReward(this, kongInfo);
                    if (kongVm.Result != "False") result = kongVm.KongName + ": Claimed War rewards\r\n";
                    else result = kongVm.KongName + ": Failed - " + kongVm.ResultMessage.Replace("\r\n", "") + "\r\n";
                    outputTextBox.AppendText(result);
                }

            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, MethodBase.GetCurrentMethod() + ": Error - \r\n" + ex);
            }
            finally
            {
                // Track progress
                grinderProgressBar.PerformStep();
                if (grinderProgressBar.Value >= grinderProgressBar.Maximum)
                {
                    adminOutputTextBox.AppendText("Done\r\n");
                    grinderProgressBar.Value = 0;
                }
            }

            // Call init
            crowsNestRefreshButton_Click(sender, e);
        }

        /// <summary>
        /// Create inventory file for this user
        /// </summary>
        private void crowsNestCreateInventoryButton_Click(object sender, EventArgs e)
        {
            // If filtering, only pull Commander duals/quads, Dominions, and CardPower>0
            bool filterPlayerCards = crowsNestCreateInventoryFilterCardsCheckBox.Checked;
            bool includePossibleCards = crowsNestCreateInventoryPossibleCardsCheckBox.Checked;
            string kongString = rumBarrelPlayerComboBox.Text;

            try
            {
                // Init and get user cards
                KongViewModel kongVm = BotManager.Init(kongString, true);
                StringBuilder inventoryFile = new StringBuilder();
                StringBuilder possibleInventoryFile = new StringBuilder();
                StringBuilder filteredOutCards = new StringBuilder();

                // DEBUG: Some cards show up in init but are missing in the inventory pull. Why is this?
                string myCards = "";
                foreach (var card in kongVm.PlayerCards)
                {
                    myCards += card.Key.Name + ", ";
                }
                Console.WriteLine(myCards);


                if (kongVm != null)
                {
                    inventoryFile.AppendLine();
                    inventoryFile.Append("// ");
                    inventoryFile.Append(kongVm.KongName);
                    inventoryFile.AppendLine();

                    // -----------------------------------------
                    // Create rough deck seeds for this player
                    // -----------------------------------------
                    {
                        inventoryFile.AppendLine(PlayerManager.CreateExternalSeed(kongVm.PlayerCards.ToDictionary(x => x.Key, x => x.Value), 0));
                        inventoryFile.AppendLine(PlayerManager.CreateExternalSeed(kongVm.PlayerCards.ToDictionary(x => x.Key, x => x.Value), 1));
                        inventoryFile.AppendLine(PlayerManager.CreateExternalSeed(kongVm.PlayerCards.ToDictionary(x => x.Key, x => x.Value), 2));
                        inventoryFile.AppendLine(PlayerManager.CreateExternalSeed(kongVm.PlayerCards.ToDictionary(x => x.Key, x => x.Value), 3));
                        inventoryFile.AppendLine(PlayerManager.CreateExternalSeed(kongVm.PlayerCards.ToDictionary(x => x.Key, x => x.Value), 4));
                        inventoryFile.AppendLine(PlayerManager.CreateExternalSeed(kongVm.PlayerCards.ToDictionary(x => x.Key, x => x.Value), 5));
                        inventoryFile.AppendLine(PlayerManager.CreateExternalSeedTempoDeck(kongVm.PlayerCards.ToDictionary(x => x.Key, x => x.Value)));
                        inventoryFile.AppendLine(PlayerManager.CreateExternalSeedStrikeDeck(kongVm.PlayerCards.ToDictionary(x => x.Key, x => x.Value)));


                        // Get player active / defense deck
                        string activeDeckId = kongVm.UserData.ActiveDeck;
                        string defenseDeckId = kongVm.UserData.DefenseDeck;

                        UserDeck activeDeck = kongVm.UserDecks.FirstOrDefault(x => x.Id == activeDeckId);
                        UserDeck defenseDeck = kongVm.UserDecks.FirstOrDefault(x => x.Id == defenseDeckId);


                        // For each seed, check if it exists in the seeds file. 
                        // * If it does, use that seed. If not, use the current active/defense deck
                        foreach (string seedName in CONSTANTS.SEEDS_FROM_CONFIG)
                        {
                            var seedDeck = CONFIG.PlayerConfigSeeds.Where(x => x.Item1 == kongVm.KongName && x.Item2 == seedName).FirstOrDefault();
                            if (seedDeck != null)
                            {
                                inventoryFile.Append("//Seed-" + seedName + ": " + seedDeck.Item3 + "\r\n");
                            }
                            else if (seedName.Contains("Attack"))
                            {
                                inventoryFile.Append("//Seed-" + seedName + ": " + activeDeck.DeckToString() + "\r\n");
                            }
                            else
                            {
                                inventoryFile.Append("//Seed-" + seedName + ": " + defenseDeck.DeckToString() + "\r\n");
                            }
                        }
                    }

                    // -----------------------------------------
                    // Add Player cards to the result string 
                    // -----------------------------------------
                    {
                        // Assaults/structures
                        var assaultsAndStructures = kongVm.PlayerCards
                            .Where(x => x.Key.CardType == CardType.Assault.ToString() ||
                                        x.Key.CardType == CardType.Structure.ToString())
                                    .OrderByDescending(x => x.Key.Fusion_Level)
                                    .ThenByDescending(x => x.Key.Rarity)
                                    .ThenByDescending(x => x.Key.Faction)
                                    .ThenByDescending(x => x.Key.Name)
                                    .ToList();

                        inventoryFile.AppendLine();
                        inventoryFile.AppendLine("// --- Quads ---");
                        int currentFusionLevel = 2;

                        foreach (var cardDict in assaultsAndStructures)
                        {
                            string cardName = cardDict.Key.Name;

                            int fusionLevel = cardDict.Key.Fusion_Level;
                            int cardCount = cardDict.Value;

                            // ------------------------------------
                            // Comment out low powered cards
                            // ------------------------------------
                            if (filterPlayerCards && cardDict.Key.Power <= 0)
                            {
                                filteredOutCards.Append("//" + cardName);
                                if (cardCount > 1)
                                {
                                    filteredOutCards.Append("#");
                                    filteredOutCards.Append(cardCount);
                                }
                                filteredOutCards.Append("\r\n");
                                continue;
                            }

                            // ------------------------------------
                            // Add card
                            // ------------------------------------
                            else
                            {

                                inventoryFile.Append(cardName);
                                if (cardCount > 1)
                                {
                                    inventoryFile.Append("#");
                                    inventoryFile.Append(cardCount);
                                }
                                inventoryFile.Append("\r\n");

                                // Add spaces when fusion level changes
                                if (currentFusionLevel == 2 && fusionLevel == 1)
                                {
                                    currentFusionLevel = 1;
                                    inventoryFile.AppendLine("// --- Duals/Singles ---");
                                }
                            }
                        }


                        inventoryFile.AppendLine();
                        inventoryFile.AppendLine("// --- Commanders/Dominions ---");

                        // Commanders/Dominions
                        var dominions = kongVm.PlayerCards.Keys
                            .Where(x => x.CardType == CardType.Dominion.ToString())
                            .ToList();

                        foreach (var dominion in dominions)
                        {
                            inventoryFile.AppendLine(dominion.Name);
                        }

                        var commanders = kongVm.PlayerCards.Keys
                            .Where(x => x.CardType == CardType.Commander.ToString())
                                    .OrderByDescending(x => x.Fusion_Level)
                                    .ThenByDescending(x => x.Rarity)
                                    .ThenByDescending(x => x.Faction)
                                    .ToList();

                        foreach (var commander in commanders)
                        {
                            if (commander.Fusion_Level == 0) continue;
                            if (filterPlayerCards && commander.Fusion_Level == 1) continue;

                            inventoryFile.AppendLine(commander.Name);
                        }


                        inventoryFile.Append("\r\n\r\n");
                        inventoryFile.Append(filteredOutCards.ToString());
                    }


                    // -----------------------------------------
                    // Add Restore cards with cardPower > 1 if the checkbox is checked
                    // -----------------------------------------
                    if (includePossibleCards)
                    {
                        inventoryFile.AppendLine();
                        inventoryFile.AppendLine("// ---------------------");
                        inventoryFile.AppendLine("// --- Restore Cards ---");
                        inventoryFile.AppendLine("// ---------------------");

                        // Assaults/structures
                        var assaultsAndStructures = kongVm.RestoreCards
                            .Where(x => x.Key.CardType == CardType.Assault.ToString() ||
                                        x.Key.CardType == CardType.Structure.ToString())
                                    .OrderByDescending(x => x.Key.Fusion_Level)
                                    .ThenByDescending(x => x.Key.Rarity)
                                    .ThenByDescending(x => x.Key.Faction)
                                    .ThenByDescending(x => x.Key.Name)
                                    .ToList();

                        inventoryFile.AppendLine();
                        inventoryFile.AppendLine("// --- Quads ---");
                        int currentFusionLevel = 2;

                        foreach (var cardDict in assaultsAndStructures)
                        {
                            string cardName = cardDict.Key.Name;
                            int fusionLevel = cardDict.Key.Fusion_Level;
                            int cardCount = cardDict.Value;

                            // Get the leveled name and Card object of this card (restore has a card as Quad level 1)
                            // * Doing this to retrieve the power of the card
                            cardName = cardName.Substring(0, cardName.Length - 2);
                            Card leveledCard = CardManager.GetPlayerCardByName(cardName);


                            // ------------------------------------
                            // Comment out low power cards
                            // ------------------------------------
                            if (filterPlayerCards && leveledCard.Power <= 0)
                            {
                                filteredOutCards.Append("//" + cardName);
                                if (cardCount > 1)
                                {
                                    filteredOutCards.Append("#");
                                    filteredOutCards.Append(cardCount);
                                }
                                filteredOutCards.Append("\r\n");
                                continue;
                            }

                            // ------------------------------------
                            // Add card
                            // ------------------------------------
                            else
                            {
                                inventoryFile.Append(cardName);
                                if (cardCount > 1)
                                {
                                    inventoryFile.Append("(+");
                                    inventoryFile.Append(cardCount);
                                    inventoryFile.Append(")");
                                }
                                inventoryFile.Append("\r\n");

                                // Add spaces when fusion level changes
                                if (currentFusionLevel == 2 && fusionLevel == 1)
                                {
                                    currentFusionLevel = 1;
                                    inventoryFile.AppendLine("// --- Duals/Singles ---");
                                }
                            }
                        }
                    }



                    // -----------------------------------------
                    // Write the result string to a card file and add it to the dropdowns
                    // -----------------------------------------
                    {
                        string fileName = "_XX_" + kongVm.KongName + ".txt";
                        FileIO.SimpleWrite(this, "data/cards", fileName, inventoryFile.ToString());
                        inventoryListBox1.Items.Insert(0, fileName);
                        inventoryListBox2.Items.Insert(0, fileName);
                        inventoryListBox3.Items.Insert(0, fileName);
                        batchSimInventoryListBox.Items.Insert(0, fileName);
                    }
                }
                else
                {
                    outputTextBox.AppendText("Failed to call TU on " + kongString + "\r\n");
                }

            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on creating inventories from API: \r\n" + ex + "\r\n");
            }

            outputTextBox.AppendText("Done\r\n");
        }

        #endregion

        #region CrowsNest - Builder


        /// <summary>
        /// Show card stats of the first card selected
        /// </summary>
        private void builderInventoryListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            var items = builderInventoryListBox.SelectedItems;
            if (items.Count > 0)
            {
                string[] splitString = items[0].ToString().Split('#');
                string cardName = splitString[0];

                builderSelectedCardTextBox.Text = CardManager.CardToString(cardName, includeMetadata: false, includeFusionFrom: true, includeFusionTo: true);
            }
        }

        /// <summary>
        /// Upgrade selected cards
        /// </summary>
        private void builderUpgradeButton_Click(object sender, EventArgs e)
        {
            try
            {
                KongViewModel kongVm = new KongViewModel();
                string selectedUser = rumBarrelPlayerComboBox.Text;
                List<string> cardsToUpgrade = builderInventoryListBox.SelectedItems.Cast<string>().ToList();

                ApiManager.GetKongInfoFromString(kongVm, selectedUser);

                // Try to upgrade cards
                foreach (string cardToUpgrade in cardsToUpgrade)
                {
                    string[] splitString = cardToUpgrade.Trim().Split('#');
                    string cardName = splitString[0];
                    int cardCount = 1;
                    if (splitString.Length > 1) int.TryParse(splitString[1], out cardCount);

                    // -- Call API
                    outputTextBox.AppendText(kongVm.KongName + " - Upgrading " + cardName.ToString() + ": ");
                    kongVm = BotManager.UpgradeCard(selectedUser, cardName);
                    
                    // Success - update inventoryListBox
                    if (kongVm.Result != "False" && !String.IsNullOrWhiteSpace(kongVm.ResultNewCardId))
                    {
                        int index = builderInventoryListBox.Items.IndexOf(cardToUpgrade);
                        if (cardCount == 1)
                        {
                            // There was only one copy of this card, replace the index
                            builderInventoryListBox.Items[index] = cardName.Substring(0, cardName.Length - 2);
                        }
                        else
                        {
                            // Decrement count and add a new item
                            builderInventoryListBox.Items[index] = cardName + "#" + (cardCount - 1);
                            builderInventoryListBox.Items.Insert(index, cardName.Substring(0, cardName.Length - 2));
                        }

                        outputTextBox.AppendText(kongVm.KongName + " - Done\r\n");
                    }
                    // Failure 
                    else
                    {
                        outputTextBox.AppendText(kongVm.KongName + " - Failed: " + kongVm.ResultMessage.Replace("\r\n", "") + "\r\n");
                        return;
                    }
                }

                // Refresh account info (SP count, not card lists)
                RefreshAccountAndCards(kongVm, fullInit: false);
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on upgrading: \r\n" + ex + "\r\n");
            }
        }

        /// <summary>
        /// Restore selected card
        /// </summary>
        private void builderRestoreButton_Click(object sender, EventArgs e)
        {
            try
            {
                string selectedUser = rumBarrelPlayerComboBox.Text;
                string cardToRestore = builderRestoreListBox.SelectedItem.ToString();

                // Try to split the card (ex: Aegis#44) into the card and number
                string[] splitString = cardToRestore.Split('#');
                string cardName = splitString[0];
                int cardCount = 1;

                // Split the string into <card>#<count>, if it has a number
                if (splitString.Length > 1) int.TryParse(splitString[1], out cardCount);

                outputTextBox.AppendText("Restoring and upgrading " + cardName + ": ");
                KongViewModel kongVm = BotManager.RestoreCard(selectedUser, cardName);

                // Success
                if (kongVm.Result != "False")
                {
                    outputTextBox.AppendText("Success\r\n");

                    // Decrement this item in store
                    int index = builderRestoreListBox.SelectedIndex;

                    if (cardCount == 1) builderRestoreListBox.Items.RemoveAt(index);
                    else if (cardCount == 2) builderRestoreListBox.Items[index] = cardName;
                    else builderRestoreListBox.Items[index] = cardName + "#" + (cardCount - 1);

                    // Increment this item in inventory
                    string restoredCardName = cardName.Substring(0, cardName.Length - 2);
                    //builderInventoryListBox.Items.Add();

                    // Find this item or item#x in inventory
                    string matchingInventoryItem = builderInventoryListBox.Items.Cast<string>()
                        .Where(x => x == restoredCardName || x.Contains(restoredCardName + "#"))
                        .FirstOrDefault();

                    // Simply add a new entry
                    if (matchingInventoryItem == null)
                    {
                        builderInventoryListBox.Items.Add(restoredCardName);
                    }
                    // Find the entry and increment count
                    else
                    {
                        index = builderInventoryListBox.Items.IndexOf(matchingInventoryItem);

                        // Get existing count
                        splitString = matchingInventoryItem.Split('#');
                        cardCount = 1;
                        if (splitString.Length > 1) int.TryParse(splitString[1], out cardCount);

                        builderInventoryListBox.Items[index] = restoredCardName + "#" + cardCount + 1;
                    }



                    // Refresh account info (SP count)
                    RefreshAccountAndCards(kongVm, fullInit: false);
                }
                else
                {
                    outputTextBox.AppendText("Failed to restore: " + kongVm.ResultMessage + "\r\n");

                    // Refresh account info (SP count)
                    RefreshAccountAndCards(kongVm, fullInit: true);
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on restoring a card: \r\n" + ex + "\r\n");
            }
        }

        /// <summary>
        /// Salvage L1 commons and rares
        /// </summary>
        private void builderSalvageCommonsButton_Click(object sender, EventArgs e)
        {
            try
            {
                string selectedUser = rumBarrelPlayerComboBox.Text;
                KongViewModel kongVm = BotManager.SalvageL1CommonsAndRares(this, selectedUser);
                outputTextBox.AppendText("Salvaging commons and rares\r\n");

                // Success
                if (kongVm.Result != "False")
                {
                    outputTextBox.AppendText("Done\r\n");

                    // Refresh account info (SP count)
                    RefreshAccountAndCards(kongVm, fullInit: true);
                }
                // Fail
                else
                {
                    outputTextBox.AppendText("Error when salvaging: " + kongVm.ResultMessage + "\r\n");
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on Salvaging: \r\n" + ex + "\r\n");
            }

            // Call init
            crowsNestRefreshButton_Click(sender, e);
        }

        /// <summary>
        /// Salvage target card
        /// </summary>
        private void builderSalvageButton_Click(object sender, EventArgs e)
        {
            //this.stopProcess = false;
            //this.workerThread = new Thread(() => {
                try
                {
                    string selectedUser = rumBarrelPlayerComboBox.Text;
                    List<string> cardsToSalvage = builderInventoryListBox.SelectedItems.Cast<string>().ToList();
                    KongViewModel kongVm = new KongViewModel();
                    bool salvageLockedCards = builderSalvageLockedCheckBox.Checked;
                    bool salvageAllCopies = builderSalvageAllCardsCheckBox.Checked;

                    // If trimming to X copies, how many copies to keep (default 4)
                    bool salvageTrimToX = builderSalvageTrimCheckBox.Checked;                    
                    int.TryParse(builderSalvageTrimTextBox.Text, out int trimLimit);
                    if (trimLimit <= 0)
                    {
                        builderSalvageTrimTextBox.Text = "4";
                        trimLimit = 4;
                    }

                    // Get this user's salvage (this may not be valid)
                    int.TryParse(crowsNestSalvageTextBox.Text, out int salvage);
                    int.TryParse(crowsNestMaxSalvageTextBox.Text, out int maxSalvage);

                    if (salvage > maxSalvage && maxSalvage > 0)
                    {
                        outputTextBox.AppendText("SP is full. If this is not correct, refresh\r\n");
                        return;
                    }

                    // Find the card
                    foreach (string cardToSalvage in cardsToSalvage)
                    {
                        string[] splitString = cardToSalvage.Split('#');
                        string cardName = splitString[0];
                        int cardCount = 1;
                        if (splitString.Length > 1) int.TryParse(splitString[1], out cardCount);

                        outputTextBox.AppendText("Salvaging " + cardName.ToString() + ": ");

                        for (int i = cardCount; i > 0; i--)
                        {
                            // TrimToX - only salvage if cardCount > X
                            if (i <= trimLimit && salvageTrimToX) { break; }

                            // -- Salvage, and adjust the card list
                            kongVm = BotManager.SalvageCard(selectedUser, cardName, salvageLockedCards);
                            if (kongVm.Result != "False")
                            {
                                // Remove the entry from the list of cards, or decrement it
                                int index = builderInventoryListBox.Items.IndexOf(cardName);
                                if (index > 0) builderInventoryListBox.Items.Remove(cardName);

                                index = builderInventoryListBox.Items.IndexOf(cardName + "#" + i);
                                if (index > 0)
                                {
                                    if (i - 1 <= 1) builderInventoryListBox.Items[index] = cardName;
                                    else builderInventoryListBox.Items[index] = cardName + "#" + (i - 1);
                                }

                                outputTextBox.AppendText("Done\r\n");
                            }
                            else
                            {
                                outputTextBox.AppendText("Error when salvaging: " + kongVm.ResultMessage + "\r\n");
                            }

                            // Refresh account info (SP count)
                            RefreshAccountAndCards(kongVm, fullInit: false);

                            // If not salvaging all cards, only loop once
                            if (salvageAllCopies == false && salvageTrimToX == false) break;

                            // Don't salvage anymore if SP is maxed
                            if (salvage > maxSalvage && maxSalvage > 0)
                            {
                                outputTextBox.AppendText("SP is full. If this is not correct, refresh\r\n");
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Helper.OutputWindowMessage(this, "Error on Salvaging: \r\n" + ex + "\r\n");
                }
            //});
            //this.workerThread.Start();
        }

        /// <summary>
        /// Copy player cards to clipboard
        /// </summary>
        private void builderClipboardCards_Click(object sender, EventArgs e)
        {
            try
            {
                string userData = rumBarrelPlayerComboBox.Text;

                //HelmOutputTextBox.Text = ApiManager.CallApi();
                KongViewModel kongVm = BotManager.Init(userData, true);

                // Get user cards
                if (kongVm != null)
                {
                    StringBuilder sb = new StringBuilder();

                    sb.Append("// ");
                    sb.Append(kongVm.KongName);
                    sb.AppendLine();

                    // Add Assaults and structures
                    var assaultsAndStructures = kongVm.PlayerCards
                        .Where(x => x.Key.CardType == CardType.Assault.ToString() ||
                                x.Key.CardType == CardType.Structure.ToString())
                                .OrderByDescending(x => x.Key.Fusion_Level)
                                .ThenByDescending(x => x.Key.Rarity)
                                .ThenByDescending(x => x.Key.Faction)
                                .ToList();

                    sb.AppendLine("// --- Quads ---");
                    int currentFusionLevel = 2;

                    foreach (var cardDict in assaultsAndStructures)
                    {
                        string cardName = cardDict.Key.Name;
                        int fusionLevel = cardDict.Key.Fusion_Level;
                        int cardCount = cardDict.Value;

                        // If only listing quads, break once we hit a dual
                        if (builderClipboardQuadsOnlyCheckBox.Checked && fusionLevel == 1)
                            break;

                        sb.Append(cardName);
                        if (cardCount > 1)
                        {
                            sb.Append("#");
                            sb.Append(cardCount);
                        }
                        sb.Append("\r\n");

                        // Add spaces when fusion level changes
                        if (currentFusionLevel == 2 && fusionLevel == 1)
                        {
                            currentFusionLevel = 1;
                            sb.AppendLine("// --- Duals/Singles ---");
                        }
                    }

                    sb.AppendLine();
                    sb.AppendLine("// --- Commanders/Dominions ---");

                    // Dominions
                    var dominions = kongVm.PlayerCards
                        .Where(x => x.Key.CardType == CardType.Dominion.ToString())
                        .ToList();

                    foreach (var cardDict in dominions)
                    {
                        string cardName = cardDict.Key.Name;
                        sb.AppendLine(cardName);
                    }


                    // Commanders
                    var commanders = kongVm.PlayerCards
                        .Where(x => x.Key.CardType == CardType.Commander.ToString())
                                .OrderByDescending(x => x.Key.Fusion_Level)
                                .ThenByDescending(x => x.Key.Rarity)
                                .ThenByDescending(x => x.Key.Faction)
                                .ToList();

                    foreach (var cardDict in commanders)
                    {
                        string cardName = cardDict.Key.Name;
                        sb.AppendLine(cardName);
                    }

                    Clipboard.SetText(sb.ToString());
                    outputTextBox.AppendText("Inventory copied\r\n");
                    builderClipboardCards.BackColor = Color.Pink;
                    Thread.Sleep(1000);
                    builderClipboardCards.BackColor = Color.FromArgb(224, 224, 224);
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on copying to clipboard: \r\n" + ex + "\r\n");
            }
        }

        /// <summary>
        /// Try to autobuild a card
        /// </summary>
        private void builderAutoBuildButton_Click(object sender, EventArgs e)
        {
            try
            {
                string kongInfo = rumBarrelPlayerComboBox.Text;
                string[] cardsToBuild = builderFuseCardTextBox.Text.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                KongViewModel kongVm = new KongViewModel();
                List<string> inventoryCards = builderInventoryListBox.Items.Cast<String>().ToList();
                List<string> restoreCards = builderRestoreListBox.Items.Cast<String>().ToList();

                ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                string kongName = kongVm.KongName;

                this.stopProcess = false;
                this.workerThread = new Thread(() => {
                    try
                    {
                        // Cards to try to build
                        foreach (var cardToBuild in cardsToBuild)
                        {
                            // Build a card. If a cardToBuild starts with "<" (<COMMAND>), instead do a command
                            if (!cardToBuild.StartsWith("<") && cardToBuild != "Neocyte Fusion Shard")
                            {
                                // Own this card, but its not max level
                                if (inventoryCards.Where(x => x.StartsWith(cardToBuild + "-1")).FirstOrDefault() != null)
                                {
                                    outputTextBox.AppendText(kongName + " - " + cardToBuild + " In inventory - Upgrading\r\n");
                                    kongVm = BotManager.UpgradeCard(kongInfo, cardToBuild + "-1");
                                    continue;
                                }
                                else if (inventoryCards.Where(x => x.StartsWith(cardToBuild + "-2")).FirstOrDefault() != null)
                                {
                                    outputTextBox.AppendText(kongName + " - " + cardToBuild + " In inventory - Upgrading\r\n");
                                    kongVm = BotManager.UpgradeCard(kongInfo, cardToBuild + "-2");
                                    continue;
                                }
                                else if (inventoryCards.Where(x => x.StartsWith(cardToBuild + "-3")).FirstOrDefault() != null)
                                {
                                    outputTextBox.AppendText(kongName + " - " + cardToBuild + " In inventory - Upgrading\r\n");
                                    kongVm = BotManager.UpgradeCard(kongInfo, cardToBuild + "-3");
                                    continue;
                                }
                                else if (inventoryCards.Where(x => x.StartsWith(cardToBuild + "-4")).FirstOrDefault() != null)
                                {
                                    outputTextBox.AppendText(kongName + " - " + cardToBuild + " In inventory - Upgrading\r\n");
                                    kongVm = BotManager.UpgradeCard(kongInfo, cardToBuild + "-4");
                                    continue;
                                }
                                else if (inventoryCards.Where(x => x.StartsWith(cardToBuild + "-5")).FirstOrDefault() != null)
                                {
                                    outputTextBox.AppendText(kongName + " - " + cardToBuild + " In inventory - Upgrading\r\n");
                                    kongVm = BotManager.UpgradeCard(kongInfo, cardToBuild + "-5");
                                    continue;
                                }

                                // Own this card in restore
                                else if (restoreCards.Where(x => x.StartsWith(cardToBuild + "-1")).FirstOrDefault() != null)
                                {
                                    outputTextBox.AppendText(kongName + " - " + cardToBuild + " Found in Restore - Restoring\r\n");
                                    kongVm = BotManager.RestoreCard(kongInfo, cardToBuild + "-1");
                                }
                                else
                                {
                                    // Get the card object to figure out what this card is made from
                                    string cardName = !cardToBuild.EndsWith("-1") ? cardToBuild + "-1" : cardToBuild;
                                    Card card = CardManager.GetPlayerCardByName(cardName);

                                    // Quad commanders have one level and "-1" won't be found
                                    if (card == null) card = CardManager.GetPlayerCardByName(cardName.Replace("-1", ""));

                                    // Card still not found, or has nothing to fuse from
                                    if (card == null)
                                    {
                                        outputTextBox.AppendText(kongName + " - Could not find card " + cardName + "\r\n");
                                        continue;
                                    }
                                    if (card.FusesFrom.Count == 0)
                                    {
                                        outputTextBox.AppendText(kongName + " - " + card.Name + " did not find any cards to 'fuse from'. Card ID: " + card.CardId + "\r\n");
                                        continue;
                                    }

                                    outputTextBox.AppendText(kongName + " - Attempting to make: " + cardName + "\r\n");
                                    bool buildCardFailed = false;

                                    // Recursively go through the card's recipe, making what it needs to finally build it
                                    kongVm = BuildCard(card, inventoryCards, restoreCards, kongInfo, ref buildCardFailed);
                                }
                            }

                            // <COMMAND> to buy gold or salvage things
                            else
                            {
                                // Repeating code here - should refactor later
                                string command = cardToBuild.ToLower().Replace(" ", "");
                                List<string> cardsToSalvage = new List<string>();

                                switch (command)
                                {
                                    case "<buygold>":
                                        outputTextBox.AppendText(kongName + " - COMMAND: BUY GOLD\r\n");
                                        // Call Init to get the player's stats
                                        kongVm = BotManager.Init(kongInfo);
                                        if (kongVm.Result != "False")
                                        {
                                            // Get inventory, gold, and salvage
                                            int gold = kongVm.UserData.Gold;
                                            int inventory = kongVm.UserData.Inventory;
                                            int maxInventory = kongVm.UserData.MaxInventory;
                                            int salvage = kongVm.UserData.Salvage;
                                            int maxSalvage = kongVm.UserData.MaxSalvage;

                                            // Player has gold to buy
                                            if (gold > 2000)
                                            {
                                                // Buy gold packs until out of gold, inventory is full, or SP is full
                                                for (int i = 0; i < 50; i++)
                                                {
                                                    // ------------------------------
                                                    // Check salvage
                                                    // ------------------------------
                                                    if (maxSalvage - salvage < 50)
                                                    {
                                                        outputTextBox.AppendText(kongVm.KongName + " - Near max SP\r\n");
                                                        break;
                                                    }

                                                    // ------------------------------
                                                    // Buy gold packs
                                                    // ------------------------------
                                                    outputTextBox.AppendText(kongVm.KongName + " - Buying gold\r\n");
                                                    kongVm = BotManager.BuyGold(this, kongInfo, goldPacks: 50, displayGoldBuys: false);

                                                    // * An error happened (usually inventory space). But if its because gold is empty, we're done
                                                    if (kongVm.Result == "False" && kongVm.ResultMessage.Contains("You cannot afford"))
                                                    {
                                                        outputTextBox.AppendText(kongVm.KongName + " - " + kongVm.ResultMessage.Replace("\r\n", "") + "\r\n");
                                                        break;
                                                    }


                                                    // ------------------------------
                                                    // Salvage commons/rares
                                                    // ------------------------------
                                                    Console.WriteLine(kongVm.KongName + " - Salvaging commons and rares");
                                                    kongVm = BotManager.SalvageL1CommonsAndRares(this, kongInfo);


                                                    // ------------------------------
                                                    // Check inventory space
                                                    // ------------------------------
                                                    if (kongVm.Result != "False")
                                                    {
                                                        gold = kongVm.UserData.Gold;
                                                        inventory = kongVm.UserData.Inventory;
                                                        salvage = kongVm.UserData.Salvage;

                                                        Console.WriteLine(kongVm.KongName + " - " + salvage + "/" + maxSalvage + " SP. " + inventory + "/" + maxInventory + " cards");

                                                        // ------------------------------
                                                        // Check Inventory
                                                        // ------------------------------
                                                        if (inventory <= 0 || maxInventory <= 0 || (maxInventory - inventory < 20))
                                                        {
                                                            adminOutputTextBox.AppendText(kongVm.KongName + " - Inventory almost full: " + inventory + "/" + maxInventory + "\r\n");

                                                            break;
                                                        }

                                                        // ------------------------------
                                                        // Check salvage again
                                                        // ------------------------------
                                                        if (salvage < 0 || maxSalvage <= 0 || (maxSalvage - salvage < 100))
                                                        {
                                                            adminOutputTextBox.AppendText(kongVm.KongName + " - SP almost max: " + salvage + "/" + maxSalvage + "\r\n");
                                                            break;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        Console.WriteLine(kongVm.KongName + " - " + kongVm.ResultMessage.Replace("\r\n", ""));
                                                        break;
                                                    }

                                                }
                                            }
                                            //else
                                            //{
                                            outputTextBox.AppendText(kongVm.KongName + " - " + gold + " gold left\r\n");
                                            //}
                                        }
                                        else
                                        {
                                            outputTextBox.AppendText(kongVm.KongName + " - API error: " + kongVm.ResultMessage);
                                        }
                                        break;
                                    case "<salvage>":
                                        outputTextBox.AppendText(kongName + " - COMMAND: SALVAGE\r\n(Trim base cards, old rewards)\r\n");

                                        Helper.RefreshSalvageList(this);

                                        cardsToSalvage = new List<string>();
                                        cardsToSalvage.AddRange(CONSTANTS.SALVAGE_REWARDS);
                                        cardsToSalvage.AddRange(CONSTANTS.BASE_EPICS);
                                        cardsToSalvage.AddRange(CONSTANTS.BASE_LEGENDS);

                                        // SP selected cards    
                                        try
                                        {
                                            // Salvage commons/rares first
                                            kongVm = BotManager.SalvageL1CommonsAndRares(this, kongInfo);
                                            kongVm = BotManager.Init(kongInfo);

                                            if (kongVm.Result != "False")
                                            {
                                                // Player cards
                                                Dictionary<Card, int> playerCards = kongVm.PlayerCards;
                                                int salvage = kongVm.UserData.Salvage;
                                                int maxSalvage = kongVm.UserData.MaxSalvage;

                                                // Don't salvage if capped
                                                if (maxSalvage - salvage < 20)
                                                {
                                                    outputTextBox.AppendText(kongName + " - SP is full\r\n");
                                                }
                                                else
                                                {
                                                    // speed up card search by searching on names to see if the card exists
                                                    // (playerCards.Keys.Name is a slow lookup)
                                                    List<string> playerCardNames = playerCards.Keys.Select(x => x.Name).ToList();

                                                    foreach (string cardName in cardsToSalvage)
                                                    {
                                                        if (!playerCardNames.Contains(cardName) &&
                                                            !playerCardNames.Contains(cardName + "-1") &&
                                                            !playerCardNames.Contains(cardName + "-2") &&
                                                            !playerCardNames.Contains(cardName + "-3") &&
                                                            !playerCardNames.Contains(cardName + "-4") &&
                                                            !playerCardNames.Contains(cardName + "-5"))
                                                        {
                                                            continue;
                                                        }

                                                        List<Card> foundCards = playerCards.Keys
                                                                    .Where(x => x.Name == cardName || x.Name == cardName + "-1" ||
                                                                                x.Name == cardName + "-2" || x.Name == cardName + "-3" ||
                                                                                x.Name == cardName + "-4" || x.Name == cardName + "-5")
                                                                    .ToList();

                                                        foreach (var c in foundCards)
                                                        {
                                                            int count = playerCards[c];

                                                            // Remove base epics, but keep 5 copies
                                                            if (CONSTANTS.BaseEpics.Contains(c.Name.Replace("-1", ""))) count -= 5;
                                                            if (count <= 0) continue;

                                                            // Remove base legends, but keep 10 copies
                                                            if (CONSTANTS.BaseLegends.Contains(c.Name.Replace("-1", ""))) count -= 10;
                                                            if (count <= 0) continue;

                                                            Console.WriteLine(kongName + " - salving " + count + " " + c.Name);

                                                            for (int i = 0; i < count; i++)
                                                            {
                                                                kongVm = BotManager.SalvageCard(kongInfo, c.CardId, salvageLockedCards: true);

                                                                // Was this a success?
                                                                if (kongVm.Result != "False")
                                                                    outputTextBox.AppendText(kongName + " - Salvaged " + c.Name + "\r\n");
                                                                else
                                                                    outputTextBox.AppendText(kongName + " - Failed salvaging " + c.Name + ": " + kongVm.ResultMessage + "\r\n");
                                                            }
                                                        }

                                                        // Get new salvage
                                                        kongVm = BotManager.GetUserAccount(kongInfo);
                                                        salvage = kongVm.UserData.Salvage;

                                                        // User is near salvage cap and we can end early
                                                        if (maxSalvage - salvage <= 150)
                                                        {
                                                            adminOutputTextBox.AppendText(kongName + " SP near max\r\n");
                                                            break;
                                                        }
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                adminOutputTextBox.AppendText(kongName + " - API error" + kongVm.ResultMessage);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Helper.OutputWindowMessage(this, MethodBase.GetCurrentMethod().Name + ": Error - \r\n" + ex);
                                        }
                                        finally
                                        {
                                            // Track progress
                                            grinderProgressBar.PerformStep();
                                            if (grinderProgressBar.Value >= grinderProgressBar.Maximum)
                                            {
                                                adminOutputTextBox.AppendText("Done\r\n");
                                                grinderProgressBar.Value = 0;
                                            }
                                        }
                                        break;

                                    case "<supersalvage>":
                                        adminOutputTextBox.AppendText(kongName + " COMMAND: AGGRESSIVE SALVAGE\r\n");

                                        Helper.RefreshSalvageList(this);

                                        cardsToSalvage = new List<string>();
                                        cardsToSalvage.AddRange(CONSTANTS.SALVAGE_REWARDS);
                                        cardsToSalvage.AddRange(CONSTANTS.BASE_EPICS);
                                        cardsToSalvage.AddRange(CONSTANTS.BASE_LEGENDS);
                                        cardsToSalvage.AddRange(CONSTANTS.SALVAGE_AGGRESSIVE);

                                        // SP selected cards    
                                        try
                                        {
                                            // Salvage commons/rares first
                                            kongVm = BotManager.SalvageL1CommonsAndRares(this, kongInfo);
                                            kongVm = BotManager.Init(kongInfo);

                                            if (kongVm.Result != "False")
                                            {
                                                // Player cards
                                                Dictionary<Card, int> playerCards = kongVm.PlayerCards;
                                                List<string> playerCardNames = new List<string>(); // Faster reference of playerCards. Doing searches on objects is painfully slow
                                                foreach(Card c in playerCards.Keys)
                                                {
                                                    playerCardNames.Add(c.Name);
                                                }


                                                int salvage = kongVm.UserData.Salvage;
                                                int maxSalvage = kongVm.UserData.MaxSalvage;


                                                // Don't salvage if capped
                                                if (maxSalvage - salvage < 20)
                                                {
                                                    outputTextBox.AppendText(kongName + " - SP is full\r\n");
                                                }
                                                else
                                                {
                                                    foreach (string card in cardsToSalvage)
                                                    {
                                                        // Debug
                                                        if (card.Contains("Gigantos Machine"))
                                                        {

                                                        }

                                                        // First pass against the quicker list - does this card exist?
                                                        string hypenedCard = card + "-";

                                                        var aaa = playerCardNames.FirstOrDefault(x => x == card || x.StartsWith(hypenedCard));

                                                        if (playerCardNames.FirstOrDefault(x => x == card || x.StartsWith(hypenedCard)) == null)
                                                        {
                                                            continue;
                                                        }

                                                        // Now look for the specific card object(s)
                                                        List<Card> foundCards = playerCards.Keys
                                                                    .Where(x => x.Name == card || x.Name == card + "-1" ||
                                                                                x.Name == card + "-2" || x.Name == card + "-3" ||
                                                                                x.Name == card + "-4" || x.Name == card + "-5")
                                                                    .ToList();

                                                        foreach (var c in foundCards)
                                                        {
                                                            int count = playerCards[c];

                                                            // Remove base epics, but keep 5 copies
                                                            if (CONSTANTS.BaseEpics.Contains(c.Name.Replace("-1", ""))) count -= 5;
                                                            if (count <= 0) continue;

                                                            // Remove base legends, but keep 10 copies
                                                            if (CONSTANTS.BaseLegends.Contains(c.Name.Replace("-1", ""))) count -= 10;
                                                            if (count <= 0) continue;

                                                            Console.WriteLine(kongName + " - salving " + count + " " + c.Name);

                                                            for (int i = 0; i < count; i++)
                                                            {
                                                                kongVm = BotManager.SalvageCard(kongInfo, c.CardId, salvageLockedCards: true);

                                                                // Was this a success?
                                                                if (kongVm.Result != "False")
                                                                    outputTextBox.AppendText(kongName + " - Salvaged " + c.Name + "\r\n");
                                                                else
                                                                    outputTextBox.AppendText(kongName + " - Failed salvaging " + c.Name + ": " + kongVm.ResultMessage + "\r\n");
                                                            }
                                                        }

                                                        // Get new salvage
                                                        kongVm = BotManager.GetUserAccount(kongInfo);
                                                        salvage = kongVm.UserData.Salvage;

                                                        // User is near salvage cap and we can end early
                                                        if (maxSalvage - salvage <= 150)
                                                        {
                                                            outputTextBox.AppendText(kongName + " SP near max\r\n");
                                                            break;
                                                        }
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                outputTextBox.AppendText(kongName + " - API error" + kongVm.ResultMessage);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Helper.OutputWindowMessage(this, MethodBase.GetCurrentMethod().Name + ": Error - \r\n" + ex);
                                        }
                                        finally
                                        {
                                            // Track progress
                                            grinderProgressBar.PerformStep();
                                            if (grinderProgressBar.Value >= grinderProgressBar.Maximum)
                                            {
                                                adminOutputTextBox.AppendText("Done\r\n");
                                                grinderProgressBar.Value = 0;
                                            }
                                        }
                                        break;

                                    // At the moment hardcoded
                                    // * What we should try
                                    // 1) <respec commander>
                                    //  - Count player neocyte shards (Core, Core-1, Fusion Core)
                                    //  - if >= 4 make commander
                                    // 2) Else
                                    //  - Does player have X commander? If yes, reset, and build
                                    case "<respechalcyon>":
                                        try
                                        {
                                            // Commander to respec
                                            string cardName = "Imperator Halcyon";
                                            Card card = CardManager.GetPlayerCardByName(cardName);
                                            bool buildCardFailed = false;
                                            List<string> commandersToRespec = new List<string>
                                            {
                                                "Octane Optimized",
                                                "Barracus the Traitor",
                                                "Tabitha Liberated",
                                                "Nexor the Farseer",
                                                "Broodmother Queen",
                                                "Yurich the Honorable",
                                                "Razogoth Immortal",
                                                "Dracorex Hivegod",
                                                "Arkadios Ultimate"
                                            };

                                            // Count Neocyte cores
                                            int neocyteCoreCount = 0;
                                            int neocyteFusionCoreCount = 0;

                                            string neocyteCoresLevel1 = inventoryCards.Where(x => x.StartsWith("Neocyte Core-1")).FirstOrDefault();
                                            string neocyteCores = inventoryCards.Where(x => x == "Neocyte Core" || x.StartsWith("Neocyte Core#")).FirstOrDefault();
                                            string neocyteFusionCores = inventoryCards.Where(x => x.StartsWith("Neocyte Fusion Core")).FirstOrDefault();

                                            if (neocyteCoresLevel1 != null)
                                            {
                                                if (!neocyteCoresLevel1.Contains("#")) neocyteCoreCount++;
                                                else neocyteCoreCount += int.Parse(neocyteCoresLevel1.Split('#')[1]);
                                            }
                                            if (neocyteCores != null)
                                            {
                                                if (!neocyteCores.Contains("#")) neocyteCoreCount++;
                                                else neocyteCoreCount += int.Parse(neocyteCores.Split('#')[1]);
                                            }
                                            if (neocyteFusionCores != null)
                                            {
                                                neocyteFusionCoreCount++;
                                            }

                                            if (neocyteCoreCount >= 4 || (neocyteCoreCount >= 1 && neocyteFusionCoreCount >= 1))
                                            {
                                                outputTextBox.AppendText(kongName + " - Has enough cores to make: " + cardName + "\r\n");
                                                outputTextBox.AppendText(kongName + " - Attempting to make: " + cardName + "\r\n");

                                                // Recursively go through the card's recipe, making what it needs to finally build it
                                                kongVm = BuildCard(card, inventoryCards, restoreCards, kongInfo, ref buildCardFailed);

                                            }
                                            else
                                            {
                                                bool respeccedCommander = false;

                                                foreach (var commanderToReset in commandersToRespec)
                                                {
                                                    // Commanders to attempt to respec
                                                    if (inventoryCards.Contains("Barracus the Traitor"))
                                                    {
                                                        Card resetComm = CardManager.GetPlayerCardByName(commanderToReset);
                                                        outputTextBox.AppendText(kongName + " - Resetting " + commanderToReset + "\r\n");
                                                        kongVm = BotManager.ResetCommander(kongInfo, resetComm.CardId);

                                                        respeccedCommander = true;
                                                        break;
                                                    }
                                                }

                                                if (respeccedCommander)
                                                {
                                                    outputTextBox.AppendText(kongName + " - Attempting to make: " + cardName + "\r\n");

                                                    // Recursively go through the card's recipe, making what it needs to finally build it
                                                    kongVm = BuildCard(card, inventoryCards, restoreCards, kongInfo, ref buildCardFailed);
                                                }
                                            }

                                        }
                                        catch { }
                                        break;
                                    default:
                                        adminOutputTextBox.AppendText(kongName + " UNKNOWN COMMAND\r\n");
                                        break;
                                }
                            }


                            // Refresh card list - unless the kongString in the select box changed. Then we don't need to
                            if (kongInfo == rumBarrelPlayerComboBox.Text)
                            {
                                kongVm = BotManager.Init(kongInfo, true);
                                RefreshAccountAndCards(kongVm, fullInit: true);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Helper.OutputWindowMessage(this, "Error on building: \r\n" + ex + "\r\n");
                    }
                });
                this.workerThread.Start();
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on building: \r\n" + ex + "\r\n");
            }
        }

        /// <summary>
        /// Reset selected commander quad
        /// </summary>
        private void builderResetCommanderButton_Click(object sender, EventArgs e)
        {
            try
            {
                KongViewModel kongVm = new KongViewModel();
                string kongInfo = rumBarrelPlayerComboBox.Text;

                // Split selected card (in case there's 2 quad commanders)
                string commanderName = builderInventoryListBox.SelectedItem.ToString().Split('#')[0];
                Card card = CardManager.GetPlayerCardByName(commanderName);

                if (card != null)
                {
                    outputTextBox.AppendText("** Resetting Commander ** " + card.Name + "\r\n");
                    kongVm = BotManager.ResetCommander(kongInfo, card.CardId);

                    if (!string.IsNullOrEmpty(kongVm.ResultMessage))
                    {
                        outputTextBox.AppendText(kongVm.GetResultMessage());
                    }

                    // Refresh
                    kongVm = BotManager.Init(kongInfo, true);
                    RefreshAccountAndCards(kongVm, fullInit: true);
                }
                else
                {
                    outputTextBox.AppendText("Could not find commander " + commanderName + "\r\n");
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on Salvaging: \r\n" + ex + "\r\n");
            }
        }

        //  Crows nest helper //

        /// <summary>
        /// Refresh parts of Crows Nest
        /// </summary>
        private void RefreshAccountAndCards(KongViewModel kongVm, bool fullInit = true)
        {
            // ------------------------------------
            // Account stats
            // ------------------------------------
            crowsNestGoldTextBox.Text = kongVm.UserData.Gold.ToString();
            crowsNestMissionEnergyTextBox.Text = kongVm.UserData.Energy.ToString();
            crowsNestStaminaTextBox.Text = kongVm.UserData.Stamina.ToString();
            crowsNestSalvageTextBox.Text = kongVm.UserData.Salvage.ToString();
            crowsNestWbTextBox.Text = kongVm.UserData.Warbonds.ToString();
            crowsNestInventoryTextBox.Text = kongVm.UserData.Inventory.ToString();

            // Account maxes don't come unless init is called
            if (fullInit)
            {
                crowsNestMaxMissionEnergyTextBox.Text = kongVm.UserData.MaxEnergy.ToString();
                crowsNestMaxStaminaTextBox.Text = kongVm.UserData.MaxStamina.ToString();
                crowsNestMaxSalvageTextBox.Text = kongVm.UserData.MaxSalvage.ToString();
                crowsNestMaxInventoryTextBox.Text = kongVm.UserData.MaxInventory.ToString();
            }

            // ------------------------------------
            // Cards
            // ------------------------------------
            if (fullInit)
            {
                builderInventoryListBox.Items.Clear();
                builderRestoreListBox.Items.Clear();
                builderInventoryFilterTextBox.Text = "";
                builderRestoreFilterTextBox.Text = "";

                var playerCards = kongVm.PlayerCards
                    // No Dominions / Dominion Shards
                    .Where(x => x.Key.CardType != CardType.Dominion.ToString())
                    .Where(x => x.Key.Name != "Dominion Shard")
                    // Put commanders list
                    .OrderBy(x => x.Key.CardType == CardType.Commander.ToString())

                    // Sort by fusion level (quads first), then rarity 
                    .ThenByDescending(x => x.Key.Fusion_Level)
                    .ThenByDescending(x => x.Key.Rarity)
                    .ThenBy(x => x.Key.Set == 1000 || x.Key.Set == 2500) // Base fusion materials
                    .ThenBy(x => x.Key.Faction)
                    .ThenBy(x => x.Key.Name)
                    .ToList();


                int currentFusionLevel = -1;
                int currentRarity = -1;
                bool isCommander = false;

                List<string> items = new List<string>();
                List<string> trophyItems = new List<string>();
                List<string> cardsToIgnore = new List<string> { "Cyrus", "Malika", "Ascaris", "Terrogor", "Maion" };

                foreach (var card in playerCards)
                {
                    string cardName = card.Key.Name;
                    int cardCount = card.Value;

                    // Don't list some cards
                    if (cardsToIgnore.Contains(cardName)) continue;


                    // Neocyte shards - set code has changed, add these with commanders now
                    if (cardName == "Neocyte Shard" || cardName == "Neocyte Shard-1")
                    {
                        trophyItems.Add(cardName + (cardCount > 1 ? ("#" + cardCount) : ""));
                        continue;
                    }

                    // Trophy cards - add to the end
                    if (cardName == "Ballroom Blitzplate" || cardName == "Gil's Shard" || cardName == "Orbo")
                    {
                        trophyItems.Add(cardName + (cardCount > 1 ? ("#" + cardCount) : ""));
                        continue;
                    }


                    // First commander seen - add a seperator
                    if (card.Key.CardType == CardType.Commander.ToString() && !isCommander)
                    {
                        items.Add("// -------------------");
                        items.Add("// --COMMANDERS--");
                        items.Add("// -------------------");
                        isCommander = true;
                    }

                    // Label first fusion level and rarity
                    if (!isCommander)
                    {
                        if (currentFusionLevel != card.Key.Fusion_Level)
                        {
                            currentFusionLevel = card.Key.Fusion_Level;
                            //items.Add("// ------------");
                            //items.Add("// --" + (FusionLevel)card.Key.Fusion_Level + " " + + "--");
                            //items.Add("// ------------");
                        }

                        if (currentRarity != card.Key.Rarity && card.Key.Rarity >= 2)
                        {
                            currentRarity = card.Key.Rarity;
                            items.Add("// -------------------");
                            items.Add("// " + (Rarity)card.Key.Rarity + " " + (FusionLevel)card.Key.Fusion_Level);
                            items.Add("// -------------------");

                        }
                    }

                    // Add the card
                    items.Add(cardName + (cardCount > 1 ? ("#" + cardCount) : ""));
                }

                // Add trophy items to the bottom
                items.AddRange(trophyItems);

                builderInventoryListBox.Items.AddRange(items.ToArray());

                if (kongVm.RestoreCards.Count > 0)
                {
                    items = new List<string>();
                    foreach (var playerCard in kongVm.RestoreCards.OrderBy(x => x.Key.Name))
                    {
                        string cardName = playerCard.Key.Name;
                        int cardCount = playerCard.Value;
                        items.Add(cardName + (cardCount > 1 ? ("#" + cardCount) : ""));

                    }
                    builderRestoreListBox.Items.AddRange(items.ToArray());
                }
            }


            // ------------------------------------
            // Items
            // 2100: Commander Respec, 2000: Epic Shard, 2001: Legendary Shard
            // ------------------------------------
            crowsNestItemsTextBox.Clear();

            foreach (var item in kongVm.Items)
            {
                string name = item.Key;
                int count = item.Value;

                if (name == "2100") crowsNestItemsTextBox.AppendText("Respecs: " + count + "\r\n");
                if (name == "2000") crowsNestItemsTextBox.AppendText("Epic Packs: " + count/500 + "\r\n");
                if (name == "2001") crowsNestItemsTextBox.AppendText("Legend Packs: " + count/1000 + "\r\n");
            }
        }

        /// <summary>
        /// Refresh parts of Crows nest
        /// </summary>
        private void RefreshPlayerDeckAndDominions(KongViewModel kongVm)
        {
            string activeDeckId = kongVm.UserData.ActiveDeck;
            string defenseDeckId = kongVm.UserData.DefenseDeck;

            UserDeck activeDeck = kongVm.UserDecks.FirstOrDefault(x => x.Id == activeDeckId);
            if (activeDeck != null)
            {
                crowsNestAttackDeckTextBox.Text = activeDeck.DeckToString();
            }

            UserDeck defenseDeck = kongVm.UserDecks.FirstOrDefault(x => x.Id == defenseDeckId);
            if (defenseDeck != null)
            {
                crowsNestDefenseDeckTextBox.Text = defenseDeck.DeckToString();
            }

            Card alphaDom = kongVm.PlayerCards.Keys.Where(x => x.CardType == CardType.Dominion.ToString() && x.Name.Contains("Alpha")).FirstOrDefault();
            Card nexusDom = kongVm.PlayerCards.Keys.Where(x => x.CardType == CardType.Dominion.ToString() && !x.Name.Contains("Alpha")).FirstOrDefault();
            Card dominionShards = kongVm.PlayerCards.Keys.Where(x => x.Name == "Dominion Shard").FirstOrDefault();

            string alphaName = alphaDom != null ? alphaDom.Name : "";
            string nexusName = nexusDom != null ? nexusDom.Name : "";

            if (dominionShards != null)
            {
                crowsNestDomShardsTextBox.Text = kongVm.PlayerCards[dominionShards].ToString();
            }

            crowsNestAlphaDominionTextBox.Text = alphaName;
            crowsNestFactionDominionTextBox.Text = nexusName;
        }

        /// <summary>
        /// Build a card. Recursively goes down its fusesFrom path to get the base components, then build that
        /// </summary>
        private KongViewModel BuildCard(Card card, List<string> inventoryCards, List<string> restoreCards, string kongInfo, ref bool buildCardFailed, bool verboseOutput=true)
        {

            KongViewModel kongVm = new KongViewModel();
            string finalCardName = card.Name;

            // If anything fails in build card, we want to stop all the recursive stuff
            if (buildCardFailed) return kongVm;


            // ------------------------------------------------------------
            // Build card overrides - commander related
            // ------------------------------------------------------------
            if (card.Name == "Neocyte Fusion Core-1") // 42745
            {
                kongVm = BotManager.FuseCard(this, kongInfo, "Neocyte Fusion Core");
                if (kongVm.Result != null && kongVm.Result != "False")
                {
                    outputTextBox.AppendText(kongVm.KongName + " - Success\r\n");
                }
                return kongVm;
            }


            // ------------------------------------------------------------
            // Look for each card that this card is fused from
            // Then either build it, restore it, or if not found, recursively search its "fusedFrom" and make those
            // ------------------------------------------------------------
            foreach (var recipe in card.FusesFrom)
            {
                string recipeCardName = recipe.Key.Name;
                int recipeCount = recipe.Value;

                if (verboseOutput) outputTextBox.AppendText(card.Name + " REQUIRES: " + recipeCardName + " #" + recipeCount + "\r\n");

                // If something takes 2+ of the same card
                for (int i = 0; i < recipeCount; i++)
                {
                    // Is it in the player's inventory or restore?
                    bool cardInInventory = false;
                    string inventoryCard = "";
                    bool cardInRestore = false;

                    // Does the card need to be ugpraded?
                    bool cardNeedsUpgrading = true;

                    // Check if this card is in inventory or restore
                    {
                        if (inventoryCards.Where(x => x == recipeCardName || x.StartsWith(recipeCardName + "#")).FirstOrDefault() != null)
                        {
                            inventoryCard = recipeCardName;
                            cardInInventory = true;
                            cardNeedsUpgrading = false;
                        }
                        else if (inventoryCards.Where(x => x.StartsWith(recipeCardName + "-1")).FirstOrDefault() != null)
                        {
                            inventoryCard = recipeCardName + "-1";
                            cardInInventory = true;
                        }
                        else if (inventoryCards.Where(x => x.StartsWith(recipeCardName + "-2")).FirstOrDefault() != null)
                        {
                            inventoryCard = recipeCardName + "-2";
                            cardInInventory = true;
                        }
                        else if (inventoryCards.Where(x => x.StartsWith(recipeCardName + "-3")).FirstOrDefault() != null)
                        {
                            inventoryCard = recipeCardName + "-3";
                            cardInInventory = true;
                        }
                        else if (inventoryCards.Where(x => x.StartsWith(recipeCardName + "-4")).FirstOrDefault() != null)
                        {
                            inventoryCard = recipeCardName + "-4";
                            cardInInventory = true;
                        }
                        else if (inventoryCards.Where(x => x.StartsWith(recipeCardName + "-5")).FirstOrDefault() != null)
                        {
                            inventoryCard = recipeCardName + "-5";
                            cardInInventory = true;
                        }

                        // Restore
                        else if (restoreCards.Where(x => x.StartsWith(recipeCardName + "-1")).FirstOrDefault() != null)
                        {
                            cardInRestore = true;
                        }
                    }

                    // ------------------------------
                    // Match and upgrade this recipe card
                    // ------------------------------
                    if (cardInInventory)
                    {
                        if (!cardNeedsUpgrading)
                        {
                            if (verboseOutput) outputTextBox.AppendText(kongVm.KongName + " - Have " + inventoryCard + "\r\n");
                        }
                        else
                        {
                            if (verboseOutput) outputTextBox.AppendText(kongVm.KongName + " - Upgrading " + inventoryCard + "\r\n");

                            kongVm = BotManager.UpgradeCard(kongInfo, inventoryCard);

                            // ** Fail **
                            if (kongVm.Result == "False" || String.IsNullOrWhiteSpace(kongVm.ResultNewCardId))
                            {
                                if (verboseOutput) outputTextBox.AppendText(kongVm.KongName + " - Failed: " + kongVm.ResultMessage + "\r\n");
                                buildCardFailed = true;
                                return kongVm;
                            }
                        }

                        // Remove a copy of the recipe card from inventory so it doesn't get used again
                        int index = inventoryCards.FindIndex(x => x == inventoryCard || x.StartsWith(inventoryCard + "#"));
                        if (index >= 0)
                        {
                            string[] splitString = inventoryCards[index].Split('#');
                            string cardName = splitString[0];
                            int cardCount = 1;
                            if (splitString.Length > 1) int.TryParse(splitString[1], out cardCount);

                            // There was only one copy of this card, replace the index
                            if (cardCount <= 1) inventoryCards.RemoveAt(index);
                            else if (cardCount == 2) inventoryCards[index] = cardName;
                            else inventoryCards[index] = cardName + "#" + (cardCount - 1); // cardCount >= 3
                        }
                        else
                        {
                            if (verboseOutput) outputTextBox.AppendText(kongVm.KongName + " - Warning: Did not find " + recipeCardName + " in inventory\r\n");
                            buildCardFailed = true;
                            return kongVm;
                        }
                    }

                    // ------------------------------
                    // Restore this recipe card. Then upgrade it
                    // ------------------------------
                    else if (cardInRestore)
                    {
                        if (verboseOutput) outputTextBox.AppendText(kongVm.KongName + " - " + recipeCardName + " is in restore. Restoring..\r\n");
                        kongVm = BotManager.RestoreCard(kongInfo, recipeCardName);
                        
                        // ** Fail **
                        if (kongVm.Result == "False")
                        {
                            outputTextBox.AppendText(kongVm.KongName + " - Failed: " + kongVm.ResultMessage.Replace("\r\n", "") + "\r\n");
                            buildCardFailed = true;
                            return kongVm;
                        }

                        // Update restore list
                        int index = restoreCards.FindIndex(x => x.StartsWith(recipeCardName + "-1"));
                        if (index > 0)
                        {
                            string[] splitString = restoreCards[index].Split('#');
                            string cardName = splitString[0];
                            int cardCount = 1;

                            if (splitString.Length > 1) int.TryParse(splitString[1], out cardCount);

                            // Decrement restore list
                            if (cardCount <= 1) restoreCards.Remove(cardName);
                            else if (cardCount == 2) restoreCards[index] = cardName;
                            else restoreCards[index] = cardName + "#" + (cardCount - 1);

                            // Don't add this card in inventory; assume it is consumed
                        }
                        else
                        {
                            outputTextBox.AppendText(kongVm.KongName + " - Warning: Did not find " + recipeCardName + " in restore\r\n");
                        }
                    }

                    // ------------------------------
                    // If this recipe card is missing, look at what its fused from
                    // Try to build those child cards recursively
                    // ------------------------------
                    else
                    {
                        if (!recipeCardName.EndsWith("-1")) recipeCardName = recipeCardName + "-1";
                        Card recipeCardObj = CardManager.GetPlayerCardByName(recipeCardName);
                        if (recipeCardObj != null)
                        {
                            if (verboseOutput) outputTextBox.AppendText(kongVm.KongName + " - " + recipeCardName + " needs to be built\r\n");

                            // Make the child cards that make up this card
                            if (recipeCardObj.FusesFrom.Count > 0)
                            {
                                BuildCard(recipeCardObj, inventoryCards, restoreCards, kongInfo, ref buildCardFailed, verboseOutput: verboseOutput);
                                //foreach (var childCard in recipeCardObj.FusesFrom)
                                //{
                                //    for (int j = 0; j < childCard.Value; j++)
                                //    {
                                //        //outputTextBox.AppendText("- Attempting to build: " + childRecipe.Key + "\r\n");
                                //        //Card childRecipeCard = CardManager.GetCardByName(childRecipe.Key);
                                //        BuildCard(recipeCardObj, inventoryCards, restoreCards, kongInfo, ref buildCardFailed);
                                //    }
                                //}
                            }
                            else
                            {
                                if (verboseOutput) outputTextBox.AppendText(kongVm.KongName + " - Failed: " + recipeCardName + " has no recipe cards associated\r\n");
                                buildCardFailed = true;
                                return kongVm;
                            }
                        }
                        else
                        {
                            if (verboseOutput) outputTextBox.AppendText(kongVm.KongName + " - Failed: Did not recognize " + recipeCardName + "\r\n");
                            buildCardFailed = true;
                            return kongVm;
                        }
                    }

                }
            } // all recipe cards made


            // ------------------------------------------------------------
            // We now have the recipe cards
            // Fuse and upgrade
            // ------------------------------------------------------------
            if (!buildCardFailed)
            {
                if (verboseOutput) outputTextBox.AppendText(kongVm.KongName + " - Fusing and upgrading: " + finalCardName + "\r\n");
                kongVm = BotManager.FuseCard(this, kongInfo, finalCardName);

                // Failure
                if (kongVm.Result == "False")
                {
                    if (verboseOutput) outputTextBox.AppendText(kongVm.KongName + " - Fusion failed: " + kongVm.ResultMessage.Replace("\r\n", "") + "\r\n");
                    buildCardFailed = true;
                    return kongVm;
                }

                // If the final card is a quad commander, don't upgrade (it has one level)
                if (CONSTANTS.COMMANDER_QUADS.Contains(finalCardName))
                {
                    return kongVm;
                }

                // Upgrade
                kongVm = BotManager.UpgradeCard(kongInfo, finalCardName);
                if (kongVm.Result == "False") // || String.IsNullOrWhiteSpace(kongVm.ResultNewCardId))
                {
                    if (verboseOutput) outputTextBox.AppendText("Failed: " + kongVm.ResultMessage.Replace("\r\n", "") + "\r\n");
                    buildCardFailed = true;
                    return kongVm;
                }

                // Add fused card to inventory list
                // inventoryCards.Add(card.Name.Substring(0, finalCardName.Length - 2));
            }

            return kongVm;
        }

        /// <summary>
        /// Search for the first card in the builderInventory that starts with the filter string
        /// </summary>
        private void builderInventoryFilterTextBox_TextChanged(object sender, EventArgs e)
        {
            string filterText = builderInventoryFilterTextBox.Text.ToLower();

            if (!string.IsNullOrEmpty(filterText))
            {
                builderInventoryListBox.SelectedItems.Clear();
                var cards = builderInventoryListBox.Items;

                for (int x = 0; x < cards.Count; x++)
                {
                    string targetCard = cards[x].ToString().ToLower();

                    if (targetCard.StartsWith(filterText))
                    {
                        builderInventoryListBox.SetSelected(x, true);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Search for the first card in the restoreInventory that starts with the filter string
        /// </summary>
        private void builderRestoreFilterTextBox_TextChanged(object sender, EventArgs e)
        {
            string filterText = builderRestoreFilterTextBox.Text.ToLower();

            if (!string.IsNullOrEmpty(filterText))
            {
                builderRestoreListBox.SelectedItems.Clear();
                var cards = builderRestoreListBox.Items;

                for (int x = 0; x < cards.Count; x++)
                {
                    string targetCard = cards[x].ToString().ToLower();

                    if (targetCard.StartsWith(filterText))
                    {
                        builderRestoreListBox.SetSelected(x, true);
                        break;
                    }
                }
            }
        }

        #endregion

        #region CrowsNest - Mission

        /// <summary>
        /// Auto the selected mission if it has a mission ID
        /// </summary>
        private void crowsNestAutoQuestButton_Click(object sender, EventArgs e)
        {
            string kongInfo = rumBarrelPlayerComboBox.Text;
            KongViewModel kongVm = new KongViewModel();
            ApiManager.GetKongInfoFromString(kongVm, kongInfo);
            string kongName = kongVm.KongName;

            try
            {
                // Clear out previous quests
                //outputTextBox.Clear();

                // How many times to run this quest (default 1)
                int.TryParse(crowsNestRepeatQuestTextBox.Text, out int loops);
                if (loops < 1) loops = 1;

                if (!string.IsNullOrWhiteSpace(crowsNestQuestListBox.SelectedItem.ToString()))
                {
                    string[] selectedMissionSplit = crowsNestQuestListBox.SelectedItem.ToString().Split('\t');

                    // Ex: 
                    // -2 \t QuestName \t 8 \t 12 \t 69
                    int.TryParse(selectedMissionSplit?[0], out int questId);
                    string questName = selectedMissionSplit?[1];
                    string questProgress = selectedMissionSplit?[2];
                    string questMaxProgress = selectedMissionSplit?[3];

                    // This has a mission or quest ID associated with it - let's try to run it
                    int.TryParse(selectedMissionSplit?[4], out int missionId);
                    if (missionId > 0)
                    {
                        this.stopProcess = false;
                        this.workerThread = new Thread(() => RunQuests(kongInfo, loops, missionId, questId));
                        this.workerThread.Start();
                    }
                    else
                    {
                        outputTextBox.AppendText(kongName + " - This is not a valid mission or quest\r\n");
                    }
                }
            }
            catch (Exception ex)
            {
                string errorMessage = kongName + " - Error on crows nest autoQuest: \r\n" + ex + "\r\n";
                Helper.OutputWindowMessage(this, errorMessage);
            }
        }

        /// <summary>
        /// Auto a target mission ID x times, where x is crowsNestRepeatQuestTextBox.Text
        /// </summary>
        private void crowsNestAutoMissionButton_Click(object sender, EventArgs e)
        {
            string kongInfo = rumBarrelPlayerComboBox.Text;
            KongViewModel kongVm = new KongViewModel();
            ApiManager.GetKongInfoFromString(kongVm, kongInfo);
            string kongName = kongVm.KongName;

            try
            {
                // Clear out previous quests
                //outputTextBox.Clear();

                // Which mission
                int.TryParse(crowsNestMissionIdTextBox.Text, out int missionId);
                if (missionId < 1)
                {
                    outputTextBox.AppendText(kongName + " - This is not a valid mission ID\r\n");
                }

                // How many times to run this quest (default 1)
                int.TryParse(crowsNestRepeatQuestTextBox.Text, out int loops);
                if (loops < 1) loops = 1;

                this.stopProcess = false;
                this.workerThread = new Thread(() => RunQuests(kongInfo, loops, missionId));
                this.workerThread.Start();

                outputTextBox.AppendText(kongName + " - Done\r\n");
            }
            catch (Exception ex)
            {
                string errorMessage = kongName + " - Error on crows nest autoQuest: \r\n" + ex + "\r\n";
                Helper.OutputWindowMessage(this, errorMessage);
            }
        }

        private void crowsNestLivesimMutantButton_Click(object sender, EventArgs e)
        {
            string kongInfo = rumBarrelPlayerComboBox.Text;
            KongViewModel kongVm = new KongViewModel();
            ApiManager.GetKongInfoFromString(kongVm, kongInfo);
            string kongName = kongVm.KongName;

            try
            {
                // Clear out previous quests
                //crowsNestQuestOutputTextBox.Clear();

                // How many times to run this mission (default 1)
                int.TryParse(crowsNestRepeatQuestTextBox.Text, out int loops);
                if (loops < 1) loops = 1;

                // Mission ID
                Int32.TryParse(crowsNestMissionIdTextBox.Text, out int missionId);


                // Valid mission ID integer
                if (missionId > 0)
                {
                    //LiveSimMission(kongInfo, loops, missionId);

                    this.stopProcess = false;
                    this.workerThread = new Thread(() =>
                    {
                        try
                        {
                            kongVm = BotManager.Init(kongInfo);

                            // If the mission is a mutant (50 energy) use the player's energy to determine loops instead 
                            if (missionId >= 1512 && missionId <= 1516)
                            {
                                loops = kongVm.UserData.Energy / 50;
                            }


                            while (loops > 0)
                            {
                                if (kongVm.BattleToResume)
                                {
                                    ControlExtensions.InvokeEx(outputTextBox, x => outputTextBox.AppendText("**** WARNING: " + kongVm.KongName + " is in a battle!****\r\n"));
                                    ControlExtensions.InvokeEx(outputTextBox, x => outputTextBox.AppendText("***Click play match to finish it\r\n"));
                                    return;
                                }

                                //KongViewModel kongVm = BotManager.Init(this, kongInfo);
                                kongVm = BotManager.StartMissionOrQuest(kongInfo, missionId.ToString());

                                // Populate RumBarrel
                                if (kongVm != null)
                                {
                                    //repeat
                                    while (kongVm.BattleData.Winner == null) // or some error occurs
                                    {
                                        List<string> manualEnemyDeck = new List<string>();
                                        string manualEnemyCommander = "";
                                        string manualEnemyFortress = "";

                                        // List pve missions we care to livesim here

                                        // Forsaken Mutant
                                        if (missionId == 1512)
                                        {
                                            manualEnemyCommander = "Reaper Mutant";
                                            manualEnemyFortress = "Forsaken Junkyard";
                                            manualEnemyDeck = new List<string>
                                            {
                                                "Psycho Mutant",
                                                "Fenrir Mutant", "Fenrir Mutant",
                                                "Corruptor Mutant", "Corruptor Mutant",
                                                "Slayer Mutant",
                                                "Hunter Mutant", "Hunter Mutant",
                                                "Dante Mutant", "Dante Mutant",
                                            };
                                        }
                                        // Hydra Mutant
                                        if (missionId == 1514)
                                        {
                                            manualEnemyCommander = "Hydra Mutant";
                                            manualEnemyDeck = new List<string>
                                            {
                                                "Delver Mutant", "Delver Mutant",
                                                "Watcheye Mutant", "Watcheye Mutant",
                                                "Ghost Mutant", "Ghost Mutant",
                                                "Heretic Mutant", "Heretic Mutant",
                                                "Vaporwing Mutant", "Vaporwing Mutant",
                                            };
                                        }
                                        // Mesmerize Mutant
                                        if (missionId == 1516)
                                        {
                                            manualEnemyCommander = "Mesmerize Mutant";
                                            manualEnemyDeck = new List<string>
                                            {
                                                "Winterheart Mutant", "Winterheart Mutant",
                                                "The Mass Mutant", "The Mass Mutant",
                                                "Aurora Mutant", "Aurora Mutant",
                                                "Macroseismic Mutant", "Shaper Mutant", "Shaper Mutant", "Monitor Mutant"
                                            };
                                        }

                                        // Refresh
                                        // TODO: BotManager.PlayCard may return this data?
                                        // What does playcard return if we win?
                                        kongVm = BotManager.GetCurrentOrLastBattle(this, kongInfo, manualEnemyDeck, manualEnemyCommander, manualEnemyFortress);

                                        // Show data
                                        // FillOutRumBarrel(kongVm);

                                        // Build sim
                                        BatchSim batchSim = NewSimManager.BuildLiveSim(kongVm);

                                        // Run sim
                                        //await Task.Run(() => NewSimManager.RunLiveSim(this, batchSim));
                                        NewSimManager.RunLiveSim(batchSim);


                                        // Play next card
                                        // Look through our simmed deck and find the first card we can legally play
                                        Sim sim = batchSim.Sims.OrderByDescending(x => x.WinPercent).ThenByDescending(x => x.WinScore).FirstOrDefault();

                                        BotManager.PlayCardRumBarrel(this, kongVm, sim);

                                        // Some error happened
                                        if (kongVm.Result == "False")
                                        {
                                            ControlExtensions.InvokeEx(outputTextBox, x => outputTextBox.AppendText(kongName + " - An error has occured: " + kongVm.ResultMessage + "\r\n"));
                                            return;
                                        }
                                    }

                                    // Some error happened
                                    if (kongVm.Result == "False")
                                    {
                                        ControlExtensions.InvokeEx(outputTextBox, x => outputTextBox.AppendText(kongName + " - An error has occured: " + kongVm.ResultMessage + "\r\n"));
                                        return;
                                    }

                                    // Output match result
                                    if (kongVm.BattleData.Winner != null)
                                    {
                                        ControlExtensions.InvokeEx(outputTextBox, x => outputTextBox.AppendText(kongName + ": " + missionId + ": " + (kongVm.BattleData.Winner.Value ? "WIN!\r\n" : "**LOSS**\r\n")));
                                    }

                                    //FillOutRumBarrel(kongVm);
                                }

                                loops--;
                            }
                        }
                        catch (Exception ex)
                        {
                            Helper.OutputWindowMessage(this, "Error on crowsNestLivesimMutantButton_Click(): \r\n" + ex);
                        }
                    });
                    this.workerThread.Start();
                }
                else
                {
                    outputTextBox.AppendText(kongName + " - " + missionId + " is not a valid mission\r\n");
                }
            }
            catch (Exception ex)
            {
                string errorMessage = "Error on crows nest autoQuest: \r\n" + ex + "\r\n";
                Helper.OutputWindowMessage(this, errorMessage);
            }
        }

        /// <summary>
        /// Auto battles X missions or guild quests
        /// TODO: Should this be in BotManager?
        /// </summary>
        private void RunQuests(string kongInfo, int loops, int missionId, int questId = 0)
        {
            KongViewModel kongVm = new KongViewModel();

            for (int i = 0; i < loops; i++)
            {
                // Guild quest
                if (questId < 0)
                {
                    kongVm = BotManager.AutoMissionOrQuest(kongInfo, missionId.ToString(), guildQuest: true);
                }
                // Regular mission
                else
                {
                    kongVm = BotManager.AutoMissionOrQuest(kongInfo, missionId.ToString(), guildQuest: false);
                }

                int missionEnergy = kongVm.UserData.Energy;
                if (missionEnergy == -1) break;

                if (kongVm.Result != "False")
                {
                    ControlExtensions.InvokeEx(crowsNestMissionEnergyTextBox, x => outputTextBox.AppendText(kongVm.ResultMessage));
                }

            }

            // Get energy back if we had it
            if (kongVm.UserData.Energy > 0)
            {
                ControlExtensions.InvokeEx(crowsNestMissionEnergyTextBox, x => crowsNestMissionEnergyTextBox.Clear());
                ControlExtensions.InvokeEx(crowsNestMissionEnergyTextBox, x => crowsNestMissionEnergyTextBox.AppendText(kongVm.UserData.Energy.ToString()));
            }

            ControlExtensions.InvokeEx(outputTextBox, x => outputTextBox.AppendText(kongVm.KongName + " - Done questing\r\n"));
        }



        #endregion

        #region Crows Nest - PvP

        /// <summary>
        /// Get pvp targets
        /// </summary>
        private void pvpHuntButton_Click(object sender, EventArgs e)
        {
            // Initialize KongViewModel. But we need the kong data
            KongViewModel kongVm = new KongViewModel("getHuntingTargets");

            // Parse selectedUser for the kong tokens, and insert them into the kong vm
            string selectedUser = rumBarrelPlayerComboBox.Text;
            ApiManager.GetKongInfoFromString(kongVm, selectedUser);
            ApiManager.CallApi(kongVm);

            // This call failed
            if (kongVm.HuntingTargets.Count == 0)
            {
                pvpPlayersListBox.Items.Clear();
                pvpPlayersListBox.Items.Add("No records returned");
                return;
            }

            // Sets RumBarrel with the KongVM info
            try
            {
                // Enemies
                pvpPlayersListBox.Items.Clear();
                foreach (var target in kongVm.HuntingTargets.OrderBy(x => x.Guild == "Decepticon").ThenBy(x => x.Guild))
                {
                    pvpPlayersListBox.Items.Add(target.Guild + "\t" + target.Name + "\t" + target.UserId);
                }

                // Player stats
                if (kongVm.UserData != null)
                {
                    crowsNestStaminaTextBox.Text = kongVm.UserData.Stamina.ToString();
                    crowsNestGoldTextBox.Text = kongVm.UserData.Gold.ToString();
                    crowsNestWbTextBox.Text = kongVm.UserData.Warbonds.ToString();
                }
            }
            catch (Exception ex)
            {
                ControlExtensions.InvokeEx(outputTextBox, x => outputTextBox.AppendText("pvpHuntButton_Click threw up: " + ex.Message));
            }
        }

        /// <summary>
        /// PvP refresh
        /// </summary>
        private void pvpRefreshButton_Click(object sender, EventArgs e)
        {
            // Initialize KongViewModel. But we need the kong data
            KongViewModel kongVm = new KongViewModel("buyRivalsRefresh");

            // Parse selectedUser for the kong tokens, and insert them into the kong vm
            string selectedUser = rumBarrelPlayerComboBox.Text;
            ApiManager.GetKongInfoFromString(kongVm, selectedUser);
            ApiManager.CallApi(kongVm);

            // This call failed
            if (kongVm.HuntingTargets.Count == 0)
            {
                pvpPlayersListBox.Items.Clear();
                pvpPlayersListBox.Items.Add("No records returned");
                return;
            }

            // Sets RumBarrel with the KongVM info
            try
            {
                pvpPlayersListBox.Items.Clear();
                foreach (var target in kongVm.HuntingTargets.OrderBy(x => x.Guild == "Decepticon").ThenBy(x => x.Guild))
                {
                    pvpPlayersListBox.Items.Add(target.Guild + "\t" + target.Name + "\t" + target.UserId);
                }

                // Player stats
                if (kongVm.UserData != null)
                {
                    crowsNestStaminaTextBox.Text = kongVm.UserData.Stamina.ToString();
                    crowsNestGoldTextBox.Text = kongVm.UserData.Gold.ToString();
                    crowsNestWbTextBox.Text = kongVm.UserData.Warbonds.ToString();
                }
            }
            catch (Exception ex)
            {
                ControlExtensions.InvokeEx(outputTextBox, x => outputTextBox.AppendText("pvpRefreshButton_Click threw up: " + ex.Message));
            }
        }

        /// <summary>
        /// Attack the targeted player
        /// </summary>
        private void pvpAttackButton_Click(object sender, EventArgs e)
        {
            // Get selected userId
            KongViewModel kongVm = new KongViewModel();
            string kongInfo = rumBarrelPlayerComboBox.Text;

            var selectedPlayers = pvpPlayersListBox.SelectedItems;
            var targetPlayers = new List<string>();
            foreach (var p in selectedPlayers)
            {
                targetPlayers.Add(p.ToString());
            }

            // Sets RumBarrel with the KongVM info
            try
            {
                foreach (var targetPlayer in targetPlayers)
                {
                    string guild = targetPlayer.ToString().Split('\t')[0]?.Trim() ?? "NOGUILD";
                    string userId = targetPlayer.ToString().Split('\t')[2].Trim();

                    // Initialize KongViewModel. But we need the kong data
                    kongVm = BotManager.AutoPvpBattle(kongInfo, userId);

                    // Player stats
                    crowsNestStaminaTextBox.Text = kongVm.UserData.Stamina.ToString();
                    crowsNestGoldTextBox.Text = kongVm.UserData.Gold.ToString();
                    crowsNestWbTextBox.Text = kongVm.UserData.Warbonds.ToString();

                    // Enemy deck
                    string enemyDeck = kongVm.BattleData.GetEnemyDeck(includePlayer: true, includeDominion: true);
                    outputTextBox.AppendText(guild + "_def_" + enemyDeck + "\r\n");

                    // Api or error output
                    if (kongVm.Result == "False")
                    {
                        break;
                    }
                }

                // Get new hunting targets
                kongVm = new KongViewModel("getHuntingTargets");
                ApiManager.CallApi(kongVm);

                // Enemies
                pvpPlayersListBox.Items.Clear();
                foreach (var target in kongVm.HuntingTargets.OrderBy(x => x.Guild == "Decepticon").ThenBy(x => x.Guild))
                {
                    pvpPlayersListBox.Items.Add(target.Guild + "\t" + target.Name + "\t" + target.UserId);
                }
            }
            catch (Exception ex)
            {
                ControlExtensions.InvokeEx(outputTextBox, x => outputTextBox.AppendText("pvpAttackButton_Click threw up: " + ex.Message));
            }

        }

        /// <summary>
        /// Refresh up to X times, attacking a guild
        /// </summary>
        private async void pvpSuperHuntButton_Click(object sender, EventArgs e)
        {
            // Get current PvP targets
            KongViewModel kongVm = new KongViewModel("getHuntingTargets");
            string kongInfo = rumBarrelPlayerComboBox.Text;
            ApiManager.GetKongInfoFromString(kongVm, kongInfo);
            ApiManager.CallApi(kongVm);
            string kongName = kongVm.KongName;

            // Settings
            string targetGuildOrPlayer = pvpSuperHuntGuild.Text;
            int.TryParse(pvpSuperHuntRefresh.Text, out int refreshes);
            int.TryParse(crowsNestStaminaTextBox.Text, out int stamina);
            int.TryParse(crowsNestGoldTextBox.Text, out int gold);

            // Records players we hit
            List<HuntingTarget> huntingTargets = new List<HuntingTarget>();
            // Names of the players we found
            List<string> foundPlayerNamesWithFullDecks = new List<string>();
            // Decks of the players we found
            List<string> foundDecks = new List<string>();

            if (stamina <= 0)
            {
                pvpPlayersListBox.Items.Add("No PvP stamina");
                return;
            }
            if (refreshes <= 0)
            {
                pvpPlayersListBox.Items.Add("Refresh count must be at least 1");
                return;
            }
            if (targetGuildOrPlayer == "")
            {
                pvpPlayersListBox.Items.Add("Enter a name or guild in the target box");
                return;
            }
            if (kongVm.HuntingTargets.Count == 0)
            {
                pvpPlayersListBox.Items.Add("API error: No records returned");
                return;
            }

            // Add decks already in the result box to our lists
            string[] existingDecks = pvpOutputTextBox.Text.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var existingDeck in existingDecks)
            {
                string[] splitLine = existingDeck.Split(new string[] { "_def_", ":" }, StringSplitOptions.RemoveEmptyEntries);
                if (splitLine.Length == 3)
                {
                    foundDecks.Add(existingDeck);

                    // Only count names without full decks (like Dec_def_Bob: <deck>, but not Dec_def_Bob(3/9): <deck>
                    if (!splitLine[1].Contains("(") && !splitLine[1].Contains(")"))
                    {
                        foundPlayerNamesWithFullDecks.Add(splitLine[1]);
                    }
                }
            }

            // Clear pvp box
            pvpPlayersListBox.Items.Clear();

            // Setup initial enemies
            huntingTargets = kongVm.HuntingTargets.ToList();
            foreach (var target in huntingTargets)
            {
                pvpPlayersListBox.Items.Add(target.Guild + "\t" + target.Name + "\t" + target.UserId);
            }

            outputTextBox.AppendText("Starting superhunt\r\n");

            // ---- SuperHunt ----
            while (refreshes > 0 && gold > 50 && stamina > 0)
            {
                bool foundEnemy = false;

                // Look at current pvp targets until we don't find a target
                foreach (var huntingTarget in huntingTargets)
                {
                    string huntingName = huntingTarget.Name.Replace(":", "");

                    if ((huntingTarget.Guild == targetGuildOrPlayer || huntingName == targetGuildOrPlayer) &&
                        !foundPlayerNamesWithFullDecks.Contains(huntingName)) // Don't constantly hit a target we have most of a deck on
                    {
                        try
                        {
                            await Task.Run(() => kongVm = BotManager.AutoPvpBattle(kongInfo, huntingTarget.UserId));

                            // Enemy deck
                            string enemyDeck = kongVm.BattleData.GetEnemyDeck(includePlayer: true, includeDominion: true);
                            string enemyDeckFormatted = huntingTarget.Guild + "_def_" + enemyDeck;

                            // If we found a full deck, let's save that enemy's name and remove other duplicates
                            int enemyCardsMissing = Int32.Parse(kongVm.BattleData.EnemySize) - kongVm.BattleData.EnemyCardsPlayed.Count;
                            if (enemyCardsMissing <= 1)
                            {
                                foundPlayerNamesWithFullDecks.Add(huntingName);

                                // Remove other entries of this target from the foundDecks list
                                foundDecks.RemoveAll(x => x.Contains(huntingName + ":"));
                                foundDecks.RemoveAll(x => x.Contains(huntingName + "("));
                            }

                            foundDecks.Add(enemyDeckFormatted); // + " //" + huntingTarget.UserId);


                            // Show new decks in output
                            // pvpEnemyDeckTextBox.AppendText(enemyDeckFormatted + "\r\n");
                            outputTextBox.AppendText(enemyDeckFormatted + "\r\n");

                            // Api or error output
                            if (kongVm.Result == "False")
                            {
                                rumBarrelOutputTextBox.AppendText("An error has occured: " + kongVm.ResultMessage + "\r\n");
                                return;
                            }

                            foundEnemy = true;

                            // Decrement stamina
                            stamina--;
                            crowsNestStaminaTextBox.Text = stamina.ToString();

                            break;
                        }
                        catch (Exception ex)
                        {
                            ControlExtensions.InvokeEx(outputTextBox, x => outputTextBox.AppendText("pvpAttackButton_Click threw up: " + ex.Message));
                        }
                    }
                }

                // If we did not find any enemy, refresh
                if (!foundEnemy)
                {
                    // Initialize KongViewModel. But we need the kong data
                    kongVm = new KongViewModel("buyRivalsRefresh");

                    // Parse selectedUser for the kong tokens, and insert them into the kong vm
                    ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                    ApiManager.CallApi(kongVm);

                    // This call failed
                    if (kongVm.HuntingTargets.Count == 0)
                    {
                        pvpPlayersListBox.Items.Clear();
                        pvpPlayersListBox.Items.Add("Refresh error: No records returned");
                        return;
                    }

                    // Decrement counters
                    gold -= 50;
                    crowsNestGoldTextBox.Text = gold.ToString();

                    // Set refresh count in the textbox. Hacky way where we can set it to 0 to end this early
                    pvpSuperHuntRefresh.Text = (refreshes - 1).ToString();
                    Thread.Sleep(1);
                    int.TryParse(pvpSuperHuntRefresh.Text, out refreshes);

                    if (refreshes % 10 == 0)
                        outputTextBox.AppendText("R\r\n");
                }


                // Whether we attacked someone or refreshed, let's update the hunting list
                try
                {
                    huntingTargets = kongVm.HuntingTargets.ToList();

                    pvpPlayersListBox.Items.Clear();
                    foreach (var target in huntingTargets)
                    {
                        pvpPlayersListBox.Items.Add(target.Guild + "\t" + target.Name + "\t" + target.UserId);
                    }
                    Thread.Sleep(1);

                }
                catch (Exception ex)
                {
                    ControlExtensions.InvokeEx(outputTextBox, x => outputTextBox.AppendText("pvpRefreshButton_Click threw up: " + ex.Message));
                }


            }//superHunt

            if (foundDecks.Count > 0)
            {
                foundDecks.Sort();
                pvpOutputTextBox.Clear();
                foreach (var deck in foundDecks)
                {
                    pvpOutputTextBox.AppendText(deck + "\r\n");
                }
            }
            //rumBarrelApiOutputTextBox.Text = string.Join("\r\n", resultDecks);

        }

        #endregion

        #region c3 Tab

        /// <summary>
        /// Play multiple players in a CQ zone
        /// </summary>
        private void c3PlayCqButton_Click(object sender, EventArgs e)
        {
            try
            {
                // user strings - energy count
                Dictionary<string, int> users = new Dictionary<string, int>();
                //if (c3player1ComboBox.Text != "") users.Add(c3player1ComboBox.Text, 0);
                //if (c3player2ComboBox.Text != "") users.Add(c3player2ComboBox.Text, 0);
                //if (c3player3ComboBox.Text != "") users.Add(c3player3ComboBox.Text, 0);
                //if (c3player4ComboBox.Text != "") users.Add(c3player4ComboBox.Text, 0);
                //if (c3player5ComboBox.Text != "") users.Add(c3player5ComboBox.Text, 0);


                // CQ Attack params
                //int.TryParse(c3cqIterationsComboBox.Text, out int iterations);
                //int.TryParse(c3cqCardsAheadComboBox.Text, out int extraLockedCards);
                //int.TryParse(c3CqNumberOfAttacksTextBox.Text, out int numberOfAttacks);

                // Get energy for each player
                List<string> userKeys = new List<string>(users.Keys);
                foreach (var key in userKeys)
                {
                    KongViewModel kongVm = BotManager.Init(key);

                    int cqEnergy = kongVm.ConquestData.Energy;
                    users[key] = cqEnergy;
                }

                // Loop
                //int zoneId = Helper.GetConquestZoneId(rumBarrelCqZoneComboBox.Text);
                //if (zoneId > 0 && numberOfAttacks > 0)
                //{
                //    while (numberOfAttacks > 0)
                //    {
                //        //KongViewModel kongVm = BotManager.Init(this, userData);
                //        // var kongVm = BotManager.StartCqMatch(this, userData, zoneId);

                //        //        // Populate RumBarrel
                //        //        if (kongVm != null)
                //        //        {
                //        //            //repeat
                //        //            while (kongVm.BattleData.Winner == null) // or some error occurs
                //        //            {
                //        //                // Show data
                //        //                FillOutRumBarrel(kongVm);

                //        //                // Build sim
                //        //                BatchSim batchSim = NewSimManager.BuildLiveSim(kongVm, iterations: iterations, extraLockedCards: extraLockedCards);

                //        //                // Run sim
                //        //                await Task.Run(() => NewSimManager.RunLiveSim(this, batchSim));

                //        //                // Play next card
                //        //                // Look through our simmed deck and find the first card we can legally play
                //        //                Sim sim = batchSim.Sims.OrderByDescending(x => x.WinPercent).ThenByDescending(x => x.WinScore).FirstOrDefault();

                //        //                bool success = BotManager.PlayCard(this, kongVm, sim);
                //        //                if (!success) break;
                //        //            }

                //        //            // Need some error trapping
                //        //            Console.WriteLine("Status: " + kongVm.StatusMessage);

                //        //            FillOutRumBarrel(kongVm);
                //        //        }

                //        //        numberOfAttacks--;
                //        //    }
                //        //}
                //        //else
                //        //{
                //        //    outputTextBox.AppendText("Invalid CQ Zone selected\r\n");
                //        //}
                //    }
                //}
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on c3PlayCqButton(): \r\n" + ex);
            }
        }

        /// <summary>
        /// Set 1 or more player decks
        /// </summary>
        private void adminSetDeckButton_Click(object sender, EventArgs e)
        {
            // These are the players we have kong strings for
            List<string> kongInfos = new List<string>();
            foreach (var user in FileIO.SimpleRead(this, "__users.txt", returnCommentedLines: false))
            {
                kongInfos.Add(user.ToString());
            }

            adminOutputTextBox.Text = "";
            
            this.stopProcess = false;
            this.workerThread = new Thread(() =>
            {
                try
                {
                    // Set attack or defense?
                    Button control = (Button)sender;
                    string controlName = control.Name;
                    bool activeDeck = true;
                    if (controlName.Contains("Defense")) activeDeck = false;

                    // These are the users we want to set decks for
                    List<string> deckStrings = new List<string>();
                    foreach (var deckString in setDecksInputTextBox.Text.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        deckStrings.Add(deckString.ToString());
                    }

                    Parallel.ForEach(deckStrings, new ParallelOptions { MaxDegreeOfParallelism = 10 }, deckString =>
                    {
                        try
                        {
                            // Extract player name from deck string
                            string playerName = "";
                            string playerDeck = "";
                            bool validDeckString = true;

                            // Deckstring: If there are tabs or colons, try to parse out the deck
                            // Format is either
                            // player:deck
                            // player:winrate:deck
                            // player:winrate:deck:guild
                            string[] deckStringSplitter = deckString.Split(new char[] { ':', '\t' }, 4, StringSplitOptions.RemoveEmptyEntries);
                            if (deckStringSplitter.Length == 2) playerDeck = deckStringSplitter[1];
                            else if (deckStringSplitter.Length == 3) playerDeck = deckStringSplitter[2];
                            else if (deckStringSplitter.Length == 4) playerDeck = deckStringSplitter[2];
                            else
                            {
                                adminOutputTextBox.AppendText("ERROR: Deck string not recognized: " + deckString + "\r\n");
                                validDeckString = false;
                            }

                            // Valid deck string - attempt to set deck
                            if (validDeckString)
                            {
                                // Similar names may break this
                                playerName = deckStringSplitter[0].Trim();

                                string kongInfo = kongInfos.Where(x => x.Contains(playerName)).FirstOrDefault();
                                if (!string.IsNullOrWhiteSpace(kongInfo))
                                {
                                    // Get the player's kongInfo from _users.txt
                                    bool rebuildDominion = adminRespecDominionCheckBox.Checked;
                                    bool rebuildDominionIfHighShards = adminRespecDominionHighShardsCheckBox.Checked;

                                    // Call a less expensive getUserAccount
                                    if (!rebuildDominion)
                                    {
                                        // Get the player's dominions and dominion shards
                                        KongViewModel kongVm = BotManager.GetUserAccount(kongInfo);
                                        if (kongVm.Result != "False")
                                        {
                                            kongVm = BotManager.SetDeck(kongInfo, playerDeck, settingActiveDeck: activeDeck, rebuildDominion: false);

                                            // Output
                                            if (kongVm.Result == "False" && string.IsNullOrWhiteSpace(kongVm.PtuoMessage)) adminOutputTextBox.AppendText(kongVm.GetResultMessage());                                            
                                            else adminOutputTextBox.AppendText(playerName + ": " + playerDeck + "\r\n");

                                            // If a player does not have a dominion, display this
                                            if (!string.IsNullOrWhiteSpace(kongVm.PtuoMessage)) adminOutputTextBox.AppendText(kongVm.PtuoMessage);
                                        }
                                    }
                                    // Need to call init to get shards and dominions. Reset player dominion if needed
                                    else
                                    {
                                        // Get the player's dominions and dominion shards
                                        KongViewModel kongVm = BotManager.Init(kongInfo, getCardsFromInit: true);
                                        if (kongVm.Result != "False")
                                        {
                                            Card alphaDom = kongVm.PlayerCards.Keys.Where(x => x.CardType == CardType.Dominion.ToString() && x.Name.Contains("Alpha")).FirstOrDefault();
                                            Card nexusDom = kongVm.PlayerCards.Keys.Where(x => x.CardType == CardType.Dominion.ToString() && !x.Name.Contains("Alpha")).FirstOrDefault();
                                            Card dominionShards = kongVm.PlayerCards.Keys.Where(x => x.Name == "Dominion Shard").FirstOrDefault();

                                            string dominion1 = alphaDom?.Name;
                                            string dominion2 = nexusDom?.Name;
                                            int shards = kongVm.PlayerCards?[dominionShards] ?? 0;


                                            //adminOutputTextBox.AppendText(playerName + ": " + shards + " shards. " + dominion1 + ". " + dominion2 + "\r\n");

                                            bool respecDominion = ((rebuildDominion && !rebuildDominionIfHighShards) ||
                                                                        (rebuildDominion && rebuildDominionIfHighShards && shards >= 1000));

                                            kongVm = BotManager.SetDeck(kongInfo, playerDeck, dominion1, dominion2, settingActiveDeck: activeDeck, rebuildDominion: rebuildDominion);

                                            // Output
                                            if (kongVm.Result == "False") adminOutputTextBox.AppendText(kongVm.GetResultMessage());
                                            else adminOutputTextBox.AppendText(playerName + ": " + playerDeck + "\r\n");
                                        }
                                    }
                                }

                                else
                                {
                                    adminOutputTextBox.AppendText("ERROR: " + playerName + ": No kongInfo found\r\n");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(MethodInfo.GetCurrentMethod() + " - " + ex.Message);
                        }
                    });


                    //foreach(var deckString in decks)
                    //{
                    //    // Extract player name from deck string
                    //    string playerName = "";
                    //    string playerDeck = "";

                    //    // Deckstring: If there are tabs or colons, try to parse out the deck
                    //    // Format is either
                    //    // player:deck
                    //    // player:winrate:deck
                    //    // player:winrate:deck:guild
                    //    string[] deckStringSplitter = deckString.Split(new char[] { ':', '\t' }, 4, StringSplitOptions.RemoveEmptyEntries);
                    //    if (deckStringSplitter.Length == 2) playerDeck = deckStringSplitter[1];
                    //    else if (deckStringSplitter.Length == 3) playerDeck = deckStringSplitter[2];
                    //    else if (deckStringSplitter.Length == 4) playerDeck = deckStringSplitter[2];
                    //    else
                    //    {
                    //        adminOutputTextBox.AppendText("Deck string not recognized\r\n");
                    //        continue; // deckStringSplitter.Length == 1
                    //    }

                    //    playerName = deckStringSplitter[0].Trim();

                    //    string userData = users.Where(x => x.Contains(playerName)).FirstOrDefault();
                    //    if (string.IsNullOrWhiteSpace(userData))
                    //    {
                    //        adminOutputTextBox.AppendText(playerName + ": No kong string found\r\n");
                    //    }

                    //    // Need to call out init to get their dominion (expensive)
                    //    KongViewModel kongVm = BotManager.Init(this, userData, true);
                    //    Card alphaDom = kongVm.PlayerCards.Keys.Where(x => x.CardType == CardType.Dominion.ToString() && x.Name.Contains("Alpha")).FirstOrDefault();
                    //    Card nexusDom = kongVm.PlayerCards.Keys.Where(x => x.CardType == CardType.Dominion.ToString() && !x.Name.Contains("Alpha")).FirstOrDefault();
                    //    Card dominionShards = kongVm.PlayerCards.Keys.Where(x => x.Name == "Dominion Shard").FirstOrDefault();

                    //    string dominion1 = alphaDom?.Name;
                    //    string dominion2 = nexusDom?.Name;
                    //    int.TryParse(kongVm.PlayerCards[dominionShards].ToString(), out int shards);

                    //    bool rebuildDominion = guildSetterRespecDominionCheckBox.Checked;
                    //    bool rebuildDominionIfHighShards = guildSetterRespecDominionHighShardsCheckBox.Checked;
                    //    bool respecDominion = (rebuildDominion || (rebuildDominionIfHighShards && shards >= 5000));


                    //    kongVm = BotManager.SetDeck(this, userData, playerDeck, dominion1, dominion2, activeDeck: activeDeck, rebuildDominion: rebuildDominion);

                    //    // Api or error output
                    //    if (kongVm.Result == "False")
                    //    {
                    //        adminOutputTextBox.AppendText(kongVm.ResultMessage + "\r\n");
                    //    }
                    //    else
                    //    {
                    //        adminOutputTextBox.AppendText(playerName + ": " + playerDeck + "\r\n");
                    //    }
                    //}
                    adminOutputTextBox.AppendText("Done\r\n");

                }
                catch (Exception ex)
                {
                    Helper.OutputWindowMessage(this, "Error on crows nest set attack deck: \r\n" + ex + "\r\n");
                }
            });
            this.workerThread.Start();
        }
        #endregion

        #region Navigator Sim Tab

        /// <summary>
        /// Queue a Navigator batchsim
        /// </summary>
        private void navigateQueueButton_Click(object sender, EventArgs e)
        {
            NewSimManager.BuildSim_Navigator(this);
        }


        /// <summary>
        /// Run all Navigator batchsims
        /// </summary>
        private void navigateSimButton_Click(object sender, EventArgs e)
        {
            List<BatchSim> batchSims = new List<BatchSim>();
            var lines = navigatorQueuedTextBox.Text.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            // For each line, parse out if this line is a valid batchsim
            foreach (var line in lines)
            {
                var batchSim = NewSimManager.FindBatchSimByString(line);
                if (batchSim != null)
                {
                    batchSims.Add(batchSim);
                }
            }


            this.stopProcess = false;
            this.workerThread = new Thread(new ThreadStart(() => NewSimManager.RunNavigatorSims(this, batchSims)));
            this.workerThread.Start();

            // We found the entries stored in NewSimManager.. remove them
            //navigatorQueuedTextBox.Text = "";
            //NewSimManager.BatchSims.RemoveAll(x => x.Mode == SimMode.NAVIGATOR);
        }

        /// <summary>
        /// Help button
        /// </summary>
        private void navSimHelpButton_Click(object sender, EventArgs e)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("* What is it?");
            sb.AppendLine("Navigator is a new sim method for tweaking a deck. It will produce a more realistic winrate, but it can't climb new cards.");
            sb.AppendLine("");
            sb.AppendLine("");
            sb.AppendLine("* What does it solve?");
            sb.AppendLine("Example: Say we are simming against a gauntlet of 10 decks. Five decks are very aggressive, and the other half are slow. The ordered sim will try to apply the same order to both sets. This makes stuff like [Abel's Resuscitator] a good early play against slow decks, but a poor early play against fast decks. ");
            sb.AppendLine("");
            sb.AppendLine("This throws the win rate off and won't tell you whether adding another fast/slow card will really help your deck.");
            sb.AppendLine("");
            sb.AppendLine("");
            sb.AppendLine("* How to use this");
            sb.AppendLine("Enter your deck, including Dominion, and choose a gauntlet file and gauntlet. An example is provided below");

            Helper.Popup(this, sb.ToString());
        }

        #endregion

        #region Event Tab

        /// <summary>
        /// Get guild brawl ranks
        /// </summary>
        private void EventGetScoresButton_Click(object sender, EventArgs e)
        {
            StringBuilder players = new StringBuilder();

            // TODO: Different call based on current event (Brawl, War, Raid)

            // Try getting brawl first

            // Initialize KongViewModel
            KongViewModel kongVm = new KongViewModel("getBrawlMemberLeaderboard");

            // Parse selectedUser for the kong tokens, and insert them into the kong vm
            string selectedUser = rumBarrelPlayerComboBox.Text;
            ApiManager.GetKongInfoFromString(kongVm, selectedUser);
            ApiManager.CallApi(kongVm);


            // Brawl scores
            if (kongVm.BrawlLeaderboard.Count > 0)
            {
                // Sets RumBarrel with the KongVM info
                try
                {
                    // Enemies
                    eventOutputTextBox.Clear();
                    foreach (var player in kongVm.BrawlLeaderboard)
                    {
                        players.AppendLine(player.Points + ": " + player.Name + "\t" + player.UserId);
                    }

                    eventOutputTextBox.Text = players.ToString();
                }
                catch (Exception ex)
                {
                    ControlExtensions.InvokeEx(outputTextBox, x => outputTextBox.AppendText("eventScoresButton_click threw up: " + ex.Message));
                }
                return;
            }

            // Brawl failed, try Raid
            kongVm = new KongViewModel("getRaidInfo");
            ApiManager.GetKongInfoFromString(kongVm, selectedUser);
            ApiManager.CallApi(kongVm);

            if (kongVm.RaidData.RaidLeaderboard.Count > 0)
            {
                // Sets RumBarrel with the KongVM info
                try
                {
                    foreach (var player in kongVm.RaidData.RaidLeaderboard)
                    {
                        players.AppendLine(player.Damage + ": " + player.Name + "\t" + player.UserId);
                    }
                    eventOutputTextBox.Text = players.ToString();
                }
                catch (Exception ex)
                {
                    ControlExtensions.InvokeEx(outputTextBox, x => outputTextBox.AppendText("eventScoresButton_click threw up: " + ex.Message));
                }
                return;
            }


            // Raid failed, try CQ
            kongVm = new KongViewModel("getGuildInfluenceLeaderboard");
            ApiManager.GetKongInfoFromString(kongVm, selectedUser);
            ApiManager.CallApi(kongVm);

            // Raid failed, try CQ
            if (kongVm.CqInfluenceLeaderboard.Count > 0)
            {
                // Sets RumBarrel with the KongVM info
                try
                {
                    foreach (var player in kongVm.CqInfluenceLeaderboard)
                    {
                        players.AppendLine(player.Influence + ": " + player.Name + "\t" + player.UserId);
                    }
                    eventOutputTextBox.Text = players.ToString();
                    eventOutputTextBox.Text = players.ToString();
                }
                catch (Exception ex)
                {
                    ControlExtensions.InvokeEx(outputTextBox, x => outputTextBox.AppendText("eventScoresButton_click threw up: " + ex.Message));
                }
                return;
            }
        }

        /// <summary>
        /// Get guild brawl ranks without calling init / last online time
        /// </summary>
        private void rankRaidButton_Click(object sender, EventArgs e)
        {
            // Initialize KongViewModel. But we need the kong data
            KongViewModel kongVm = new KongViewModel("getRaidInfo");

            // Parse selectedUser for the kong tokens, and insert them into the kong vm
            string selectedUser = rumBarrelPlayerComboBox.Text;
            ApiManager.GetKongInfoFromString(kongVm, selectedUser);
            ApiManager.CallApi(kongVm);

            // This call failed
            if (kongVm.RaidData.RaidLeaderboard.Count == 0)
            {
                eventOutputTextBox.Text = "No records returned";
                return;
            }

            // Sets RumBarrel with the KongVM info
            try
            {
                // Enemies
                eventOutputTextBox.Clear();
                var players = new StringBuilder();
                foreach (var player in kongVm.RaidData.RaidLeaderboard.OrderByDescending(x => int.Parse(x.Damage)))
                {
                    players.AppendLine(player.UserId + "\t" + player.Name + "\t" + player.Damage);
                }

                eventOutputTextBox.Text = players.ToString();
            }
            catch (Exception ex)
            {
                ControlExtensions.InvokeEx(outputTextBox, x => outputTextBox.AppendText("rankRaidButton_Click threw up: " + ex.Message));
            }
        }

        /// <summary>
        /// Which decks can this player copy within one card (barring commander, dominion)
        /// </summary>
        private void eventDeckCopycatButton_Click(object sender, EventArgs e)
        {
            try
            {
                string userInfo = rumBarrelPlayerComboBox.Text;
                var playerCards = new Dictionary<Card, int>();


                // Call init to get player cards
                KongViewModel kongVm = BotManager.Init(userInfo, true);

                // Set user data
                if (kongVm.Result != "False")
                {
                    eventOutputTextBox.AppendText("Init failed\r\n");
                    return;
                }

                playerCards = kongVm.PlayerCards;

                // Get guild decks
                kongVm = BotManager.UpdateFaction(this, userInfo);

                // Api or error output
                if (kongVm.Result == "False")
                {
                    eventOutputTextBox.AppendText(kongVm.ResultMessage + "\r\n");
                }
                else
                {
                    outputTextBox.Clear();

                    // Show player / playerIDs
                    if (CONFIG.role == "level3" || CONFIG.role == "newLevel3" || debugMode == true)
                    {
                        foreach (var pi in kongVm.PlayerInfo.OrderBy(x => x.Name))
                        {
                            outputTextBox.AppendText(pi.Name + "\t" + pi.UserId + "\r\n");
                        }
                    }

                    // List guild attack decks
                    foreach (var pi in kongVm.PlayerInfo.OrderBy(x => x.Name))
                    {
                        outputTextBox.AppendText(kongVm.Faction.Name + "_atk_" + pi.Name + ": " + pi.ActiveDeck.DeckToString() + "\r\n");
                    }

                    // List guild defense decks
                    foreach (var pi in kongVm.PlayerInfo.OrderBy(x => x.Name))
                    {
                        outputTextBox.AppendText(kongVm.Faction.Name + "_def_" + pi.Name + ": " + pi.DefenseDeck.DeckToString() + "\r\n");
                    }
                }

            }
            catch (Exception ex)
            {
                ControlExtensions.InvokeEx(outputTextBox, x => outputTextBox.AppendText("rankGBrawlButton_Click threw up: " + ex.Message));
            }
        }

        #endregion

        #region Admin Tab
        
        /// <summary>
        /// Create the puller file for each kongString in the input box
        /// * Format: 
        /// * Guild:GuildName
        /// * (kongInfo string)
        /// * (comma separated list of userIDs in that guild)        
        /// </summary>
        private void adminCreatePullerButton_Click(object sender, EventArgs e)
        {
            StringBuilder result = new StringBuilder();
            List<PullerParams> pullerParams = new List<PullerParams>();
            string[] kongStrings = pullerInputTextBox.Text.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            adminOutputTextBox.Text = "";

            try
            {
                foreach (string kongInfo in kongStrings)
                {
                    // Get this player's guildmates IDs
                    KongViewModel kongVm = BotManager.UpdateFaction(this, kongInfo, pullPlayerDecks: false);
                    if (kongVm.Result != "False")
                    {
                        string guildName = (!string.IsNullOrWhiteSpace(kongVm.Faction.Name)) ? kongVm.Faction.Name : "_UNGUILDED";
                        List<string> userIds = new List<string>();

                        adminOutputTextBox.AppendText(guildName + "\r\n");

                        // If unguilded, just use the playerID
                        if (kongVm.Faction.Members.Count > 0)
                        {
                            foreach (var guildMember in kongVm.Faction.Members)
                            {
                                userIds.Add(guildMember.UserId);
                            }
                        }
                        // just use the player's userID
                        else
                        {
                            userIds.Add(kongVm.UserId);
                        }

                        // If this guild doesn't exist, add it
                        if (guildName == "_UNGUILDED" || pullerParams.Count(x => x.Guild == guildName) == 0)
                        {
                            pullerParams.Add(new PullerParams
                            {
                                Guild = guildName,
                                KongInfo = kongInfo,
                                UserIds = userIds
                            });
                        }
                    }
                    else
                    {
                        adminOutputTextBox.AppendText("\r\n" + kongVm.ResultMessage);
                    }
                }

                // Add each puller param to the stringbuilder
                foreach (var pullerParam in pullerParams)
                {
                    result.AppendLine("Guild:" + pullerParam.Guild);
                    result.AppendLine(pullerParam.KongInfo);
                    result.AppendLine(string.Join(",", pullerParam.UserIds.ToArray()));
                }

                adminOutputTextBox.Text = result.ToString();
                adminOutputTextBox.AppendText("\r\n\r\n__puller.txt updated\r\n");
                FileIO.SimpleWrite(this, ".", "__puller.txt", result.ToString(), append: false);
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on crows nest set attack deck: \r\n" + ex + "\r\n");
            }
        }

        /// <summary>
        /// Refresh guilds and users on the existing puller file
        /// </summary>
        private void adminPullerRefreshPullerButton_Click(object sender, EventArgs e)
        {
            StringBuilder result = new StringBuilder();
            List<PullerParams> pullerParams = new List<PullerParams>();

            List<string> kongInfoStrings = new List<string>();
            adminOutputTextBox.Text = "";

            // Open the existing puller file and extract the puller strings
            List<string> existingPullerFile = FileIO.SimpleRead(this, "__puller.txt", returnCommentedLines: false);
            foreach (var line in existingPullerFile)
            {
                // Skip Guild: (this is the guild line)
                if (line.StartsWith("Guild:")) continue;

                // Skip lines that start with 4 or more digits (this is the userID line)
                var regex = new Regex("^\\d\\d\\d.*");
                if (regex.IsMatch(line)) continue;

                // kongstrings start with "kongName", "kong_name", or "name"
                kongInfoStrings.Add(line);
            }

            try
            {
                foreach (string kongInfo in kongInfoStrings)
                {
                    // Get this player's guildmates IDs
                    KongViewModel kongVm = BotManager.UpdateFaction(this, kongInfo, pullPlayerDecks: false);
                    if (kongVm.Result != "False")
                    {
                        string guildName = (!string.IsNullOrWhiteSpace(kongVm.Faction.Name)) ? kongVm.Faction.Name : "_UNGUILDED";
                        List<string> userIds = new List<string>();

                        adminOutputTextBox.AppendText(guildName + "\r\n");

                        // If unguilded, just use the playerID
                        if (kongVm.Faction.Members.Count > 0)
                        {
                            foreach (var guildMember in kongVm.Faction.Members)
                            {
                                userIds.Add(guildMember.UserId);
                            }
                        }
                        // just use the player's userID
                        else
                        {
                            userIds.Add(kongVm.UserId);
                        }

                        // If this guild doesn't exist, add it
                        if (guildName == "_UNGUILDED" || pullerParams.Count(x => x.Guild == guildName) == 0)
                        {
                            pullerParams.Add(new PullerParams
                            {
                                Guild = guildName,
                                KongInfo = kongInfo,
                                UserIds = userIds
                            });
                        }
                    }
                    else
                    {
                        adminOutputTextBox.AppendText("\r\n" + kongVm.ResultMessage);
                    }
                }

                // Add each puller param to the stringbuilder
                foreach (var pullerParam in pullerParams.OrderBy(x => x.Guild))
                {
                    result.AppendLine("Guild:" + pullerParam.Guild);
                    result.AppendLine(pullerParam.KongInfo);
                    result.AppendLine(string.Join(",", pullerParam.UserIds.ToArray()));
                }

                adminOutputTextBox.Text = result.ToString();
                adminOutputTextBox.AppendText("\r\n\r\n__puller.txt updated\r\n");
                FileIO.SimpleWrite(this, ".", "__puller.txt", result.ToString(), append: false);
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on crows nest set attack deck: \r\n" + ex + "\r\n");
            }
        }

        /// <summary>
        /// Use the puller file to update gauntlets
        /// </summary>
        private async void adminPullerUpdateGauntletButton_Click(object sender, EventArgs e)
        {

            while(true)
            {

                List<PullerParams> pullerParams = new List<PullerParams>();
                string guild = "";
                string kongInfo = "";
                List<string> userIds = new List<string>();

                bool updateBrawlGauntlet = adminPullerUpdateBrawlCheckBox.Checked;
                bool updateCqGauntlet = adminPullerUpdateCqCheckBox.Checked;
                bool updateWarGauntlet = adminPullerUpdateWarCheckBox.Checked;
                bool updatePvpGauntlet = adminPullerUpdatePvPCheckBox.Checked;
                bool updateReckGauntlet = adminPullerUpdateReckCheckBox.Checked;

                bool pullTop5 = adminPullerUpdatePullTop5CheckBox.Checked;
                bool pullTop10 = adminPullerUpdatePullTop10CheckBox.Checked;
                bool pullOtherGuilds = adminPullerUpdatePullOthersCheckBox.Checked;


                // TODO: Move this to appsettings
                List<string> top5Guilds = CONSTANTS.GUILDS_TOP5;
                List<string> top10Guilds = CONSTANTS.GUILDS_TOP10;

                // Which guilds did we successfully retrieve from the puller?
                List<string> guildsToUpdate = new List<string>();
                List<string> attackGauntlets = new List<string>();
                List<string> defenseGauntlets = new List<string>();
                bool loopPuller = adminPullerAutoUpdateCheckBox.Checked;
                int.TryParse(adminPullerAutoUpdateMinutesTextBox.Text, out int loopMinutes);

                try
                {
                    // -------------------------------------------
                    // First, update gauntlets from the database
                    // -------------------------------------------
                    adminOutputTextBox.AppendText("Downloading existing gauntles..\r\n");
                    await DbManager.DownloadFiles(this);

                    // -------------------------------------------
                    // Then, get each guild in the puller file and extract which guilds we have
                    // -------------------------------------------
                    adminOutputTextBox.AppendText("Creating gauntlets from __puller.txt..\r\n");

                    // Open puller and extract guild groupings
                    List<string> pullerLogs = FileIO.SimpleRead(this, "./__puller.txt", returnCommentedLines: false, displayError: false);

                    // Create puller params. This is fragile and may need to be fixed
                    foreach (var line in pullerLogs)
                    {
                        if (line.StartsWith("Guild")) guild = line.Replace("Guild:", "");
                        else if (line.ToLower().StartsWith("kongname") || line.ToLower().StartsWith("name:")) kongInfo = line;
                        else userIds = line.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();

                        // Complete guild, add it to pullerParams
                        if (guild != "" && kongInfo != "" && userIds.Count > 0)
                        {
                            pullerParams.Add(new PullerParams
                            {
                                Guild = guild,
                                KongInfo = kongInfo,
                                UserIds = userIds
                            });
                            guild = "";
                            kongInfo = "";
                            userIds = new List<string>();
                        }
                    }

                    // -------------------------------------------
                    // Make gauntlets from each pullerParam
                    // -------------------------------------------
                    foreach (var pullerParam in pullerParams)
                    {
                        KongViewModel kongVm = new KongViewModel("getProfileData");
                        ApiManager.GetKongInfoFromString(kongVm, pullerParam.KongInfo);

                        // Get current guild
                        string myGuild = pullerParam.Guild;
                        adminOutputTextBox.AppendText(myGuild);
                        
                        // Option to skip some guilds
                        if (top5Guilds.Contains(myGuild) && !pullTop5)
                        {
                            adminOutputTextBox.AppendText(" - skipping Top5 guild\r\n");
                            continue;
                        }
                        if (top10Guilds.Contains(myGuild) && !pullTop10)
                        {
                            adminOutputTextBox.AppendText(" - skipping Top10 guild\r\n");
                            continue;
                        }
                        if (!top5Guilds.Contains(myGuild) && !top10Guilds.Contains(myGuild) && !pullOtherGuilds)
                        {
                            adminOutputTextBox.AppendText(" - skipping\r\n");
                            continue;
                        }

                        // Call each userId
                        foreach (var userId in pullerParam.UserIds)
                        {
                            if (string.IsNullOrWhiteSpace(userId))
                            {
                                adminOutputTextBox.AppendText("\r\n");
                                continue;
                            }

                            kongVm.Params = "target_user_id=" + userId;
                            ApiManager.CallApi(kongVm);
                            adminOutputTextBox.AppendText(".");
                        }
                        adminOutputTextBox.AppendText("\r\n");

                        attackGauntlets.Add(myGuild + "_atk: /^" + myGuild + "_atk_.*$/");
                        foreach (var pi in kongVm.PlayerInfo.OrderBy(x => x.Name))
                        {
                            if (pi.ActiveDeck != null)
                            {
                                string playerName = pi.Name;
                                // Replace names with odd characters (:Ghost:)
                                playerName = playerName.Replace(":Ghost:", "Ghost");

                                string deck = myGuild + "_atk_" + playerName + ": " + pi.ActiveDeck.DeckToString();
                                attackGauntlets.Add(deck);
                                //adminOutputTextBox.AppendText(deck + "\r\n");
                            }
                        }
                        //outputTextBox.AppendText("\r\n");

                        defenseGauntlets.Add(myGuild + "_def: /^" + myGuild + "_def_.*$/");
                        foreach (var pi in kongVm.PlayerInfo.OrderBy(x => x.Name))
                        {
                            if (pi.DefenseDeck != null)
                            {
                                string deck = myGuild + "_def_" + pi.Name + ": " + pi.DefenseDeck.DeckToString(false);
                                defenseGauntlets.Add(deck);
                                //adminOutputTextBox.AppendText(deck + "\r\n");
                            }
                        }

                        guildsToUpdate.Add(myGuild);

                    }

                    // -------------------------------------------
                    // Update selected gauntlets, first deleting existing guild lines, then appending new ones
                    // -------------------------------------------
                    if (updateBrawlGauntlet)
                    {
                        List<string> gauntletLines = FileIO.SimpleRead(this, "data/customdecks_brawl.txt", returnCommentedLines: true, skipWhitespace: false, displayError: false);
                        StringBuilder sb = new StringBuilder();

                        // Basically, purge any line starting with GUILD_def or GUILD_atk
                        foreach (string gauntletLine in gauntletLines)
                        {
                            bool skipLine = false;

                            foreach (string guildName in guildsToUpdate)
                            {
                                if (gauntletLine.StartsWith(guildName + "_def")) skipLine = true;
                            }

                            if (!skipLine) sb.AppendLine(gauntletLine);
                        }

                        // Append new data
                        sb.AppendLine(string.Join("\r\n", defenseGauntlets));

                        // Write back to the gauntlet
                        FileIO.SimpleWrite(this, "data/", "customdecks_brawl.txt", sb.ToString(), append: false);

                        // Upload it
                        adminOutputTextBox.AppendText("Uploading changes..\r\n");

                        // Check update brawl
                        adminUploadBrawlCheckBox.Checked = true;

                        await DbManager.UploadFiles(this);
                    }

                    if (updateCqGauntlet)
                    {
                        List<string> gauntletLines = FileIO.SimpleRead(this, "data/customdecks_cq.txt", returnCommentedLines: true, skipWhitespace: false, displayError: false);
                        StringBuilder sb = new StringBuilder();

                        // Basically, purge any line starting with GUILD_def or GUILD_atk
                        foreach (string gauntletLine in gauntletLines)
                        {
                            bool skipLine = false;

                            foreach (string guildName in guildsToUpdate)
                            {
                                if (gauntletLine.StartsWith(guildName + "_def")) skipLine = true;
                            }

                            if (!skipLine) sb.AppendLine(gauntletLine);
                        }

                        // Append new data
                        sb.AppendLine(string.Join("\r\n", defenseGauntlets));

                        // Write back to the gauntlet
                        FileIO.SimpleWrite(this, "data/", "customdecks_cq.txt", sb.ToString(), append: false);

                        // Upload it
                        adminOutputTextBox.AppendText("Uploading changes..\r\n");

                        // Check update 
                        adminUploadCqCheckBox.Checked = true;

                        await DbManager.UploadFiles(this);
                    }

                    if (updateWarGauntlet)
                    {
                        List<string> gauntletLines = FileIO.SimpleRead(this, "data/customdecks_war.txt", returnCommentedLines: true, skipWhitespace: false, displayError: false);
                        StringBuilder sb = new StringBuilder();

                        // Basically, purge any line starting with GUILD_def or GUILD_atk
                        foreach (string gauntletLine in gauntletLines)
                        {
                            bool skipLine = false;

                            foreach (string guildName in guildsToUpdate)
                            {
                                if (gauntletLine.StartsWith(guildName + "_def")) skipLine = true;
                            }

                            if (!skipLine) sb.AppendLine(gauntletLine);
                        }

                        // Append new data
                        sb.AppendLine(string.Join("\r\n", defenseGauntlets));

                        // Write back to the gauntlet
                        FileIO.SimpleWrite(this, "data/", "customdecks_war.txt", sb.ToString(), append: false);

                        // Upload it
                        adminOutputTextBox.AppendText("Uploading changes..\r\n");

                        // Check update 
                        adminUploadWarCheckBox.Checked = true;

                        await DbManager.UploadFiles(this);
                    }

                    if (updatePvpGauntlet)
                    {
                        List<string> gauntletLines = FileIO.SimpleRead(this, "data/customdecks_pvp.txt", returnCommentedLines: true, skipWhitespace: false, displayError: false);
                        StringBuilder sb = new StringBuilder();

                        // Basically, purge any line starting with GUILD_def or GUILD_atk
                        foreach (string gauntletLine in gauntletLines)
                        {
                            bool skipLine = false;

                            foreach (string guildName in guildsToUpdate)
                            {
                                if (gauntletLine.StartsWith(guildName + "_def")) skipLine = true;
                            }

                            if (!skipLine) sb.AppendLine(gauntletLine);
                        }

                        // Append new data
                        sb.AppendLine(string.Join("\r\n", defenseGauntlets));

                        // Write back to the gauntlet
                        FileIO.SimpleWrite(this, "data/", "customdecks_pvp.txt", sb.ToString(), append: false);

                        // Upload it
                        adminOutputTextBox.AppendText("Uploading changes..\r\n");

                        // Check update 
                        adminUploadPvpCheckBox.Checked = true;

                        await DbManager.UploadFiles(this);
                    }

                    if (updateReckGauntlet)
                    {
                        List<string> gauntletLines = FileIO.SimpleRead(this, "data/customdecks_reck.txt", returnCommentedLines: true, skipWhitespace: false, displayError: false);
                        StringBuilder sb = new StringBuilder();

                        // Basically, purge any line starting with GUILD_def or GUILD_atk
                        foreach (string gauntletLine in gauntletLines)
                        {
                            bool skipLine = false;

                            foreach (string guildName in guildsToUpdate)
                            {
                                if (gauntletLine.StartsWith(guildName + "_def")) skipLine = true;
                                else if (gauntletLine.StartsWith(guildName + "_atk")) skipLine = true;
                            }

                            if (!skipLine) sb.AppendLine(gauntletLine);
                        }

                        // Append new data
                        sb.AppendLine(string.Join("\r\n", attackGauntlets));
                        sb.AppendLine("\r\n\r\n");
                        sb.AppendLine(string.Join("\r\n", defenseGauntlets));

                        // Write back to the gauntlet
                        FileIO.SimpleWrite(this, "data/", "customdecks_reck.txt", sb.ToString(), append: false);

                        // Upload it
                        adminOutputTextBox.AppendText("Uploading changes..\r\n");

                        // Check update 
                        adminUploadReckCheckBox.Checked = true;

                        await DbManager.UploadFiles(this);
                    }

                    adminOutputTextBox.AppendText("Done updating\r\n");
                }
                catch (Exception ex)
                {
                    Helper.OutputWindowMessage(this, "Error on adminPullerUpdateGauntletButton_Click: \r\n" + ex + "\r\n");
                    break;
                }


                // Keep looping 
                if (loopPuller == false || loopMinutes > 0)
                {
                    break;
                }
                else
                {
                    adminOutputTextBox.AppendText("Pulling from guilds again in " + adminPullerAutoUpdateMinutesTextBox + " minutes\r\n");
                    int delayInMilliseconds = ((int)loopMinutes) * 1000 * 60;
                    Thread.Sleep(delayInMilliseconds);
                }
            }

        }

        /// <summary>
        /// Given input Guild:X\r\n KongInfo:x\r\n (csv of userIds).. 
        /// Get each deck using userID and make a gauntlet (not calling init for a soft pull)
        /// </summary>
        private void adminSoftPullButton_Click(object sender, EventArgs e)
        {
            KongViewModel kongVm = new KongViewModel();
            List<string> lines = pullerInputTextBox.Text.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            List<string> userIds = new List<string>();
            string guild = "";
            string kongInfo = "";

            try
            {
                foreach (var line in lines)
                {
                    if (line.StartsWith("Guild")) guild = line.Replace("Guild:", "");
                    else if (line.StartsWith("kongName")) kongInfo = line;
                    else userIds = line.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                }

                // Pull userIDs
                if (userIds.Count > 0)
                {
                    kongVm = new KongViewModel("getProfileData");
                    ApiManager.GetKongInfoFromString(kongVm, kongInfo);


                    // Get each user id
                    foreach (var userId in userIds)
                    {
                        if (!string.IsNullOrWhiteSpace(userId))
                        {
                            kongVm.Params = "target_user_id=" + userId.Trim();
                            ApiManager.CallApi(kongVm);
                        }
                    }

                    adminOutputTextBox.AppendText(guild + "_atk: /^" + guild + "_atk_.*$/\r\n");
                    foreach (var pi in kongVm.PlayerInfo.OrderBy(x => x.Name))
                    {
                        string name = pi.Name.Replace(":", "");
                        adminOutputTextBox.AppendText(guild + "_atk_" + name + ": " + pi.ActiveDeck.DeckToString() + "\r\n");
                    }

                    outputTextBox.AppendText("\r\n\r\n");

                    adminOutputTextBox.AppendText(guild + "_def: /^" + guild + "_def_.*$/\r\n");
                    foreach (var pi in kongVm.PlayerInfo.OrderBy(x => x.Name))
                    {
                        string name = pi.Name.Replace(":", "");
                        adminOutputTextBox.AppendText(guild + "_def_" + name + ": " + pi.DefenseDeck.DeckToString() + "\r\n");
                        //adminOutputTextBox.AppendText(guild + "_def_" + name + ": " + pi.DefenseDeck.DeckToString(false) + "\r\n");
                    }

                    outputTextBox.AppendText("\r\n // -- " + userIds.Count + " players --\r\n");

                    // Reset user list
                    userIds = new List<string>();
                }

            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on adminSoftPullButton_Click: \r\n" + ex + "\r\n");
            }
        }

        /// <summary>
        /// AutoPvP and auto mission on each player
        /// </summary>
        private void adminGrindAccountsButton_Click(object sender, EventArgs e)
        {
            adminOutputTextBox.Text = "";

            try
            {
                string[] kongInfos = setDecksInputTextBox.Text.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                ConcurrentDictionary<string, string> playerResults = new ConcurrentDictionary<string, string>();

                Parallel.ForEach(kongInfos,
                    new ParallelOptions { MaxDegreeOfParallelism = 10 },
                    kongInfo =>
                    {
                        try
                        {
                            // Call Init to get the player's stats
                            KongViewModel kongVm = BotManager.Init(kongInfo);
                            if (kongVm.Result != "False")
                            {
                                string name = kongVm.KongName;

                                // Get player's mission/pvp energy
                                int stamina = kongVm.UserData.Stamina;
                                int maxStamina = kongVm.UserData.MaxStamina;
                                int energy = kongVm.UserData.Energy;
                                int maxEnergy = kongVm.UserData.MaxEnergy;

                                if (stamina > 0 && maxStamina > 0)
                                {
                                    // Pvp grind to x%

                                    //string kongInfo = rumBarrelPlayerComboBox.Text;

                                    //// Get the pvp stamina of this player
                                    //int.TryParse(crowsNestStaminaTextBox.Text, out int numberOfAttacks);

                                    //outputTextBox.AppendText("PVPing all the things\r\n");

                                    //this.stopProcess = false;
                                    //this.workerThread = new Thread(() =>
                                    //{
                                    //    KongViewModel kongVm = BotManager.AutoPvpBattles(this, kongInfo, numberOfAttacks);
                                    //    // Api or error output
                                    //    if (kongVm.Result == "False")
                                    //    {
                                    //        outputTextBox.AppendText("\r\n" + kongVm.ResultMessage);
                                    //    }
                                    //});
                                    //this.workerThread.Start();
                                }
                                else if (maxStamina <= 0)
                                {
                                    adminOutputTextBox.AppendText(name + ": Max stamina value can't be 0!");
                                }

                                if (energy > 0 && maxEnergy > 0)
                                {
                                    // Mission grind to x%
                                    // Prefer a mission order
                                }
                                else
                                {
                                }
                            }
                            else
                            {
                                adminOutputTextBox.AppendText("API error when using this kong string: " + kongInfo);
                            }
                        }
                        catch (Exception ex)
                        {
                            Helper.OutputWindowMessage(this, "Error on adminGrindAccountsButton_Click(): \r\n" + ex);
                        }
                    });


                adminOutputTextBox.AppendText("\r\n\r\n");

                foreach (var playerResult in playerResults.OrderBy(x => x.Key))
                {
                    adminOutputTextBox.AppendText(playerResult.Key + ": " + playerResult.Value + "\r\n");
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on adminPlayerBuildCardButton_Click(): \r\n" + ex);
            }
        }

        /// <summary>
        /// Get CQ influence
        /// </summary>
        private void adminPullerCqHardButton_Click(object sender, EventArgs e)
        {
            StringBuilder result = new StringBuilder();
            List<PullerParams> pullerParams = new List<PullerParams>();

            List<string> kongInfoStrings = new List<string>();
            adminOutputTextBox.Text = "";

            // Open the existing puller file and extract the puller strings
            List<string> existingPullerFile = FileIO.SimpleRead(this, "__puller.txt", returnCommentedLines: false);
            foreach (var line in existingPullerFile)
            {
                // Skip Guild: (this is the guild line)
                if (line.StartsWith("Guild:")) continue;

                // Skip lines that start with 4 or more digits (this is the userID line)
                var regex = new Regex("^\\d\\d\\d.*");
                if (regex.IsMatch(line)) continue;

                // kongstrings start with "kongName", "kong_name", or "name"
                kongInfoStrings.Add(line);
            }

            // End result
            StringBuilder sb = new StringBuilder();
            KongViewModel kongVm = new KongViewModel();

            List<List<CqZoneData>> allGuildInfluence = new List<List<CqZoneData>>();


            // Get each guilds direct CQ Influence in each zone
            try
            {
                foreach (string kongInfo in kongInfoStrings)
                {
                    this.stopProcess = false;
                    this.workerThread = new Thread(() =>
                    {
                        try
                        {
                            List<CqZoneData> guildInfluence = new List<CqZoneData>();

                            kongVm = BotManager.GetConquestUpdate(kongInfo, guildInfluence);
                            if (kongVm.Result != "False")
                            {
                                // Capture personal scores, lose the ranking data
                                guildInfluence = kongVm.ConquestData.ConquestZones;
                                guildInfluence.ForEach(z => z.Rankings = new List<CqZoneDataRanking>());

                                allGuildInfluence.Add(guildInfluence);
                            }
                            else
                            {
                                adminOutputTextBox.Text += kongVm.KongName + "\t" + kongVm.ResultMessage + "\r\n";
                            }
                        }
                        catch (Exception ex)
                        {
                            Helper.OutputWindowMessage(this, MethodBase.GetCurrentMethod().Name + ": Error - \r\n" + ex);
                        }
                    });
                    this.workerThread.Start();
                }
            }
            catch(Exception ex)
            {
                outputTextBox.AppendText("Error on Admin cq button: " + ex.Message);
            }

            List<CqZoneData> masterZoneData = new List<CqZoneData>();

            // Parse the super object
            foreach(var zones in allGuildInfluence)
            {
                foreach(var zone in zones)
                {
                    var masterZoneZone = masterZoneData.FirstOrDefault(x => x.Name == zone.Name);

                    // Zone does not exist in the master zone data - add it
                    if (masterZoneZone == null)
                    {
                        CqZoneData zoneData = new CqZoneData();
                        CqZoneDataRanking zoneDataRanking = new CqZoneDataRanking();
                        zoneDataRanking.Influence = zone.MyGuildInfluence;
                        zoneDataRanking.Name = zone.Name;
                        zoneDataRanking.Rank = 0;

                        zoneData.Rankings.Add(zoneDataRanking);
                        masterZoneData.Add(zoneData);                        
                    }

                    // Zone exists
                    else
                    {
                        CqZoneDataRanking zoneDataRanking = new CqZoneDataRanking();
                        zoneDataRanking.Influence = zone.MyGuildInfluence;
                        zoneDataRanking.Name = zone.Name;
                        zoneDataRanking.Rank = 0;

                        masterZoneZone.Rankings.Add(zoneDataRanking);
                        masterZoneZone.Rankings.OrderByDescending(z => z.Influence);
                    }
                }
            }

            // Output
            foreach (var zone in kongVm.ConquestData.ConquestZones.OrderByDescending(x => x.Tier))
            {
                sb.Append(zone.Name).Append("\r\n");
                foreach (var zoneRanking in zone.Rankings.OrderByDescending(x => x.Influence))
                {
                    sb.Append(zoneRanking.Name).Append("\t");
                    sb.Append(zoneRanking.Influence).Append("\r\n");
                }
                sb.Append("\r\n\r\n");
            }

            adminOutputTextBox.Text = sb.ToString();
        }

        /// <summary>
        /// Refresh config/decklogs.txt, using the puller file
        /// 
        /// NYI
        /// </summary>
        private void adminLogsRefreshFromPullerButton_Click(object sender, EventArgs e)
        {
            // * Pull decks from puller

            List<string> decks = new List<string>();

            // while(true) - if loopBox=false break

            // * Convert the decklogs file into a List
            
            // * Foreach deck
            // - Add or replace the line in the deck log
            // - Save the deck log



            //List<PullerParams> pullerParams = new List<PullerParams>();
            //string guild = "";
            //string kongInfo = "";
            //List<string> userIds = new List<string>();

            //bool updateBrawlGauntlet = adminPullerUpdateBrawlCheckBox.Checked;
            //bool updateCqGauntlet = adminPullerUpdateCqCheckBox.Checked;
            //bool updateWarGauntlet = adminPullerUpdateWarCheckBox.Checked;
            //bool updatePvpGauntlet = adminPullerUpdatePvPCheckBox.Checked;
            //bool updateReckGauntlet = adminPullerUpdateReckCheckBox.Checked;
            //bool skipSoftGuilds = adminPullerUpdateSkipSoftGuildsCheckBox.Checked; // Have this ignore top25 guilds

            //List<string> guildsToUpdate = new List<string>();
            //List<string> softGuilds = new List<string>
            //{
            //};

            //List<string> attackGauntlets = new List<string>();
            //List<string> defenseGauntlets = new List<string>();

            //try
            //{
            //    // -------------------------------------------
            //    // First, update gauntlets from the database
            //    // -------------------------------------------
            //    adminOutputTextBox.AppendText("Downloading existing gauntles..\r\n");
            //    await DbManager.DownloadFiles(this);

            //    // -------------------------------------------
            //    // Then, get each guild in the puller file and extract which guilds we have
            //    // -------------------------------------------
            //    adminOutputTextBox.AppendText("Creating gauntlets from __puller.txt..\r\n");

            //    // Open puller and extract guild groupings
            //    List<string> pullerLogs = FileIO.SimpleRead(this, "./__puller.txt", returnCommentedLines: false, displayError: false);

            //    // Create puller params. This is fragile and may need to be fixed
            //    foreach (var line in pullerLogs)
            //    {
            //        if (line.StartsWith("Guild")) guild = line.Replace("Guild:", "");
            //        else if (line.StartsWith("kongName")) kongInfo = line;
            //        else userIds = line.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();

            //        // Complete guild, add it to pullerParams
            //        if (guild != "" && kongInfo != "" && userIds.Count > 0)
            //        {
            //            pullerParams.Add(new PullerParams
            //            {
            //                Guild = guild,
            //                KongInfo = kongInfo,
            //                UserIds = userIds
            //            });
            //            guild = "";
            //            kongInfo = "";
            //            userIds = new List<string>();
            //        }
            //    }

            //    // -------------------------------------------
            //    // Make gauntlets from each pullerParam
            //    // -------------------------------------------
            //    foreach (var pullerParam in pullerParams)
            //    {
            //        KongViewModel kongVm = new KongViewModel("getProfileData");
            //        ApiManager.GetKongInfoFromString(kongVm, pullerParam.KongInfo);

            //        // Get current guild
            //        string myGuild = pullerParam.Guild;
            //        adminOutputTextBox.AppendText(myGuild);

            //        // Option to skip soft guilds
            //        if (skipSoftGuilds && softGuilds.Contains(myGuild)) continue;

            //        // Call each userId
            //        foreach (var userId in pullerParam.UserIds)
            //        {
            //            if (string.IsNullOrWhiteSpace(userId))
            //            {
            //                adminOutputTextBox.AppendText("\r\n");
            //                continue;
            //            }

            //            kongVm.Params = "target_user_id=" + userId;
            //            ApiManager.CallApi(kongVm);
            //            adminOutputTextBox.AppendText(".");
            //        }
            //        adminOutputTextBox.AppendText("\r\n");

            //        attackGauntlets.Add(myGuild + "_atk: /^" + myGuild + "_atk_.*$/");
            //        foreach (var pi in kongVm.PlayerInfo.OrderBy(x => x.Name))
            //        {
            //            if (pi.ActiveDeck != null)
            //            {
            //                string playerName = pi.Name;
            //                // Replace names with odd characters (:Ghost:)
            //                playerName = playerName.Replace(":Ghost:", "Ghost");

            //                string deck = myGuild + "_atk_" + playerName + ": " + pi.ActiveDeck.DeckToString();
            //                attackGauntlets.Add(deck);
            //                //adminOutputTextBox.AppendText(deck + "\r\n");
            //            }
            //        }
            //        //outputTextBox.AppendText("\r\n");

            //        defenseGauntlets.Add(myGuild + "_def: /^" + myGuild + "_def_.*$/");
            //        foreach (var pi in kongVm.PlayerInfo.OrderBy(x => x.Name))
            //        {
            //            if (pi.DefenseDeck != null)
            //            {
            //                string deck = myGuild + "_def_" + pi.Name + ": " + pi.DefenseDeck.DeckToString(false);
            //                defenseGauntlets.Add(deck);
            //                //adminOutputTextBox.AppendText(deck + "\r\n");
            //            }
            //        }

            //        guildsToUpdate.Add(myGuild);

            //    }

            //    // -------------------------------------------
            //    // Update selected gauntlets, first deleting existing guild lines, then appending new ones
            //    // -------------------------------------------
            //    if (updateBrawlGauntlet)
            //    {
            //        List<string> gauntletLines = FileIO.SimpleRead(this, "data/customdecks_brawl.txt", returnCommentedLines: true, skipWhitespace: false, displayError: false);
            //        StringBuilder sb = new StringBuilder();

            //        // Basically, purge any line starting with GUILD_def or GUILD_atk
            //        foreach (string gauntletLine in gauntletLines)
            //        {
            //            bool skipLine = false;

            //            foreach (string guildName in guildsToUpdate)
            //            {
            //                if (gauntletLine.StartsWith(guildName + "_def")) skipLine = true;
            //            }

            //            if (!skipLine) sb.AppendLine(gauntletLine);
            //        }

            //        // Append new data
            //        sb.AppendLine(string.Join("\r\n", defenseGauntlets));

            //        // Write back to the gauntlet
            //        FileIO.SimpleWrite(this, "data/", "customdecks_brawl.txt", sb.ToString(), append: false);

            //        // Upload it
            //        adminOutputTextBox.AppendText("Uploading changes..\r\n");

            //        // Check update brawl
            //        adminUploadBrawlCheckBox.Checked = true;

            //        await DbManager.UploadFiles(this);
            //    }

            //    if (updateCqGauntlet)
            //    {
            //        List<string> gauntletLines = FileIO.SimpleRead(this, "data/customdecks_cq.txt", returnCommentedLines: true, skipWhitespace: false, displayError: false);
            //        StringBuilder sb = new StringBuilder();

            //        // Basically, purge any line starting with GUILD_def or GUILD_atk
            //        foreach (string gauntletLine in gauntletLines)
            //        {
            //            bool skipLine = false;

            //            foreach (string guildName in guildsToUpdate)
            //            {
            //                if (gauntletLine.StartsWith(guildName + "_def")) skipLine = true;
            //            }

            //            if (!skipLine) sb.AppendLine(gauntletLine);
            //        }

            //        // Append new data
            //        sb.AppendLine(string.Join("\r\n", defenseGauntlets));

            //        // Write back to the gauntlet
            //        FileIO.SimpleWrite(this, "data/", "customdecks_cq.txt", sb.ToString(), append: false);

            //        // Upload it
            //        adminOutputTextBox.AppendText("Uploading changes..\r\n");

            //        // Check update 
            //        adminUploadCqCheckBox.Checked = true;

            //        await DbManager.UploadFiles(this);
            //    }

            //    if (updateWarGauntlet)
            //    {
            //        List<string> gauntletLines = FileIO.SimpleRead(this, "data/customdecks_war.txt", returnCommentedLines: true, skipWhitespace: false, displayError: false);
            //        StringBuilder sb = new StringBuilder();

            //        // Basically, purge any line starting with GUILD_def or GUILD_atk
            //        foreach (string gauntletLine in gauntletLines)
            //        {
            //            bool skipLine = false;

            //            foreach (string guildName in guildsToUpdate)
            //            {
            //                if (gauntletLine.StartsWith(guildName + "_def")) skipLine = true;
            //            }

            //            if (!skipLine) sb.AppendLine(gauntletLine);
            //        }

            //        // Append new data
            //        sb.AppendLine(string.Join("\r\n", defenseGauntlets));

            //        // Write back to the gauntlet
            //        FileIO.SimpleWrite(this, "data/", "customdecks_war.txt", sb.ToString(), append: false);

            //        // Upload it
            //        adminOutputTextBox.AppendText("Uploading changes..\r\n");

            //        // Check update 
            //        adminUploadWarCheckBox.Checked = true;

            //        await DbManager.UploadFiles(this);
            //    }

            //    if (updateWarGauntlet)
            //    {
            //        List<string> gauntletLines = FileIO.SimpleRead(this, "data/customdecks_warbig.txt", returnCommentedLines: true, skipWhitespace: false, displayError: false);
            //        StringBuilder sb = new StringBuilder();

            //        // Basically, purge any line starting with GUILD_def or GUILD_atk
            //        foreach (string gauntletLine in gauntletLines)
            //        {
            //            bool skipLine = false;

            //            foreach (string guildName in guildsToUpdate)
            //            {
            //                if (gauntletLine.StartsWith(guildName + "_def")) skipLine = true;
            //            }

            //            if (!skipLine) sb.AppendLine(gauntletLine);
            //        }

            //        // Append new data
            //        sb.AppendLine(string.Join("\r\n", defenseGauntlets));

            //        // Write back to the gauntlet
            //        FileIO.SimpleWrite(this, "data/", "customdecks_warbig.txt", sb.ToString(), append: false);

            //        // Upload it
            //        adminOutputTextBox.AppendText("Uploading changes..\r\n");

            //        // Check update 
            //        adminUploadWarbigCheckBox.Checked = true;

            //        await DbManager.UploadFiles(this);
            //    }

            //    if (updatePvpGauntlet)
            //    {
            //        List<string> gauntletLines = FileIO.SimpleRead(this, "data/customdecks_pvp.txt", returnCommentedLines: true, skipWhitespace: false, displayError: false);
            //        StringBuilder sb = new StringBuilder();

            //        // Basically, purge any line starting with GUILD_def or GUILD_atk
            //        foreach (string gauntletLine in gauntletLines)
            //        {
            //            bool skipLine = false;

            //            foreach (string guildName in guildsToUpdate)
            //            {
            //                if (gauntletLine.StartsWith(guildName + "_def")) skipLine = true;
            //            }

            //            if (!skipLine) sb.AppendLine(gauntletLine);
            //        }

            //        // Append new data
            //        sb.AppendLine(string.Join("\r\n", defenseGauntlets));

            //        // Write back to the gauntlet
            //        FileIO.SimpleWrite(this, "data/", "customdecks_pvp.txt", sb.ToString(), append: false);

            //        // Upload it
            //        adminOutputTextBox.AppendText("Uploading changes..\r\n");

            //        // Check update 
            //        adminUploadPvpCheckBox.Checked = true;

            //        await DbManager.UploadFiles(this);
            //    }

            //    if (updateReckGauntlet)
            //    {
            //        List<string> gauntletLines = FileIO.SimpleRead(this, "data/customdecks_reck.txt", returnCommentedLines: true, skipWhitespace: false, displayError: false);
            //        StringBuilder sb = new StringBuilder();

            //        // Basically, purge any line starting with GUILD_def or GUILD_atk
            //        foreach (string gauntletLine in gauntletLines)
            //        {
            //            bool skipLine = false;

            //            foreach (string guildName in guildsToUpdate)
            //            {
            //                if (gauntletLine.StartsWith(guildName + "_def")) skipLine = true;
            //                else if (gauntletLine.StartsWith(guildName + "_atk")) skipLine = true;
            //            }

            //            if (!skipLine) sb.AppendLine(gauntletLine);
            //        }

            //        // Append new data
            //        sb.AppendLine(string.Join("\r\n", attackGauntlets));
            //        sb.AppendLine("\r\n\r\n");
            //        sb.AppendLine(string.Join("\r\n", defenseGauntlets));

            //        // Write back to the gauntlet
            //        FileIO.SimpleWrite(this, "data/", "customdecks_reck.txt", sb.ToString(), append: false);

            //        // Upload it
            //        adminOutputTextBox.AppendText("Uploading changes..\r\n");

            //        // Check update 
            //        adminUploadReckCheckBox.Checked = true;

            //        await DbManager.UploadFiles(this);
            //    }
            //    // TODO: CQ, War, PvP, Reck. Reck does atk

            //    adminOutputTextBox.AppendText("Done updating\r\n");
            //}
            //catch (Exception ex)
            //{
            //    Helper.OutputWindowMessage(this, "Error on adminPullerUpdateGauntletButton_Click: \r\n" + ex + "\r\n");
            //}
        }

        /// <summary>
        /// Represents one guild in the puller file
        /// </summary>
        private class PullerParams
        {
            public string Guild { get; set; }
            public string KongInfo { get; set; }
            public List<string> UserIds { get; set; }
            public PullerParams() { }
        }

        /// <summary>
        /// Continually pull the offense deck of selected guilds, noting deck changes
        /// </summary>
        private void pullerStartHuntButton_Click(object sender, EventArgs e)
        {

            this.stopProcess = false;
            this.workerThread = new Thread(() =>
            {
                bool isFirstPull = true;
                List<Tuple<string, string, string>> originalDecks = new List<Tuple<string, string, string>>();
                List<Tuple<string, string, string>> newDecks = new List<Tuple<string, string, string>>();
                int.TryParse(pullerHunterRefreshMinutesTextBox.Text, out int loopMinutes);

                adminOutputTextBox.AppendText("Starting the hunt\r\n\r\n");
                startPullerHunt = true;

                // Loop the hunt
                while (startPullerHunt)
                {
                    // Which guilds in puller are we tracking?
                    List<string> guildsToPull = pullerHunterInputTextBox.Text.Split(new string[] { "\r\n" }, StringSplitOptions.None).ToList();


                    try
                    {
                        List<PullerParams> pullerParams = new List<PullerParams>();
                        string guild = "";
                        string kongInfo = "";
                        List<string> userIds = new List<string>();
                        List<string> pullerLogs = FileIO.SimpleRead(this, "./__puller.txt", returnCommentedLines: false, displayError: false);


                        // -------------------------------------------
                        // Extract guild data from the puller file
                        // -------------------------------------------
                        foreach (var line in pullerLogs)
                        {
                            if (line.StartsWith("Guild")) guild = line.Replace("Guild:", "");
                            else if (line.ToLower().StartsWith("kongname") || line.ToLower().StartsWith("name:") || line.ToLower().StartsWith("kong_name")) kongInfo = line;
                            else userIds = line.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();

                            // Complete guild, add it to pullerParams
                            if (guild != "" && kongInfo != "" && userIds.Count > 0)
                            {
                                pullerParams.Add(new PullerParams
                                {
                                    Guild = guild,
                                    KongInfo = kongInfo,
                                    UserIds = userIds
                                });
                                guild = "";
                                kongInfo = "";
                                userIds = new List<string>();
                            }
                        }

                        // -------------------------------------------
                        // Get attack decks for selected guilds
                        // -------------------------------------------
                        foreach (var pullerParam in pullerParams)
                        {
                            KongViewModel kongVm = new KongViewModel("getProfileData");
                            ApiManager.GetKongInfoFromString(kongVm, pullerParam.KongInfo);

                            // Get current guild
                            string currentGuild = pullerParam.Guild;

                            // If this guild is one of the selected, pull offense
                            if (guildsToPull.Contains(currentGuild))
                            {
                                // Call each userId
                                foreach (var userId in pullerParam.UserIds)
                                {
                                    if (!string.IsNullOrWhiteSpace(userId))
                                    {
                                        kongVm.Params = "target_user_id=" + userId;
                                        ApiManager.CallApi(kongVm);
                                    }
                                }

                                foreach (var pi in kongVm.PlayerInfo.OrderBy(x => x.Name))
                                {
                                    if (pi.ActiveDeck != null)
                                    {
                                        string playerName = pi.Name;
                                        playerName = playerName.Replace(":Ghost:", "Ghost");
                                        string deck = pi.ActiveDeck.DeckToString();

                                        Tuple<string, string, string> deckTuple = new Tuple<string, string, string>(currentGuild, playerName, deck);


                                        // First pull - add this deck to the original decks found
                                        if (isFirstPull)
                                        {
                                            originalDecks.Add(deckTuple);
                                        }
                                        else
                                        {
                                            // Is this deck different from the original deck / any new deck?
                                            string originalDeck = originalDecks
                                                    .Where(x => x.Item2 == playerName)
                                                    .FirstOrDefault()?
                                                    .Item3;

                                            if (string.IsNullOrEmpty(originalDeck) || originalDeck != deck)
                                            {
                                                List<string> newDecklist = newDecks
                                                    .Where(x => x.Item2 == playerName)
                                                    .Select(x => x.Item3)
                                                    .ToList();

                                                if (!newDecklist.Contains(deck))
                                                {
                                                    newDecks.Add(deckTuple);
                                                }
                                            }
                                        }
                                    }
                                }//eachPlayer
                            }//currentGuild
                        }//eachGuild

                        // Output either original decks or all changed decks
                        if (isFirstPull)
                        {
                            adminOutputTextBox.AppendText("----------\r\nOG Decks\r\n----------\r\n");
                            foreach (var deckKvp in originalDecks.OrderBy(x => x.Item1).ThenBy(x => x.Item2))
                            {
                                adminOutputTextBox.AppendText(deckKvp.Item1 + "_atk_" + deckKvp.Item2 + ": " + deckKvp.Item3 + "\r\n");
                            }
                            adminOutputTextBox.AppendText("\r\n");
                            isFirstPull = false;
                        }
                        else if (newDecks.Count > 0)
                        {
                            adminOutputTextBox.AppendText("----------\r\nNew Decks\r\n----------\r\n");
                            string previousDeckName = "";
                            int count = 1;
                            foreach (var deckKvp in newDecks.OrderBy(x => x.Item1).ThenBy(x => x.Item2))
                            {
                                string name = deckKvp.Item2;
                                if (previousDeckName == name)
                                {
                                    name = name + "_" + count;
                                    count++;
                                }
                                else
                                {
                                    count = 1;
                                }

                                adminOutputTextBox.AppendText(deckKvp.Item1 + "_atk_" + name + ": " + deckKvp.Item3 + "\r\n");
                                previousDeckName = deckKvp.Item2;
                            }
                            isFirstPull = false;
                        }

                    }
                    catch (Exception ex)
                    {
                        Helper.OutputWindowMessage(this, "Error on pullerStartHuntButton_Click: \r\n" + ex + "\r\n");
                    }


                    // Sleep for X minutes
                    if (loopMinutes < 1)
                    {
                        pullerHunterRefreshMinutesTextBox.Text = "1";
                        loopMinutes = 1;
                    }
                    int delayInMilliseconds = ((int)loopMinutes) * 1000 * 60;
                    Thread.Sleep(delayInMilliseconds);
                }
            });
            this.workerThread.Start();

        }

        private void pullerStopHuntButton_Click(object sender, EventArgs e)
        {
            startPullerHunt = false;
            adminOutputTextBox.AppendText("Stopping the hunt");
        }
        #endregion

        #region Admin - Grinder

        //TODO: For event grinding, separate the meat of the API calls in their own method, so we can have different things call them with different params

        /// <summary>
        /// Grind these players' mission and pvp energy
        /// </summary>
        private void grinderGrindButton_Click(object sender, EventArgs e)
        {
            // Setup the form
            int batchJobNumber = ++grinderProgressBarJobNumber;

            GrindParameters gp = SetupGrindParams();
            if (gp.SelectedUsers.Count == 0)
            {
                adminOutputTextBox.Text = "Select users in the left users dropdown (defined by /__users.txt) to use this action";
                return;
            }
            

            this.stopProcess = false;
            this.workerThread = new Thread(() =>
            {
                // If looping the grind
                bool loopGrind = grindGrindLoopCheckBox.Checked;

                // If looping grind, also loop the current event
                bool loopEvent = grindEventLoopCheckBox.Checked;

                // If looping, how many hours
                int.TryParse(grinderHoursTextBox.Text, out int hoursToLoop);
                if (hoursToLoop < 1) hoursToLoop = 1;

                // Number of threads to run. The more threads running the more prone to error this becomes
                int.TryParse(grindThreadsTextBox.Text, out int threads);
                if (threads <= 0) threads = 1;
                if (threads > 25) threads = 25;


                // This runs once unless looped
                while (true)
                {
                    // Record the time. If grinding took longer then the loop time, loop again immediately. Otherwise, wait for the time difference
                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();

                    string beginGrindMessage = loopGrind ? "** Looping " + gp.SelectedUsers.Count + " users **" :
                                                           "** Grinding " + gp.SelectedUsers.Count + " users **";

                    adminOutputTextBox.AppendText(beginGrindMessage + "\r\n------------------\r\n");

                    // Init the first account to figure out what event we're in
                    KongViewModel kongVmInit = BotManager.Init(gp.SelectedUsers[0]);
                    bool brawlActive = kongVmInit.BrawlActive;
                    bool raidActive = kongVmInit.RaidActive;
                    int raidId = kongVmInit.RaidData.Id;
                    bool warActive = kongVmInit.WarActive; // Prevent attacks during downtime somehow
                    bool cqActive = kongVmInit.ConquestActive; // Zone

                    
                    // ---------------------------------------------
                    // PVP / Mission grind
                    // ---------------------------------------------
                    Parallel.ForEach(gp.SelectedUsers, new ParallelOptions { MaxDegreeOfParallelism = threads }, kongInfo =>
                    {
                        KongViewModel kongVm = new KongViewModel();
                        ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                        string kongName = kongVm.KongName;
                        string resultMessage = "";

                        try
                        {
                            // Init gets all player data. UpdateFaction returns some
                            if (grinderSimpleGrindCheckBox.Checked == false)
                            {
                                kongVm = BotManager.Init(kongInfo);

                                // Api.Init does not have mission_completions
                                // * It does. Keep this just in case
                                // kongVm = BotManager.UpdateFactionSimple(kongInfo);

                                // Claim daily bonus if available 
                                if (kongVm.DailyBonusAvailable) BotManager.UseDailyBonus(kongInfo);
                            }
                            else
                            {
                                // This returns partial Init data. It will run guild quests and mission 142
                                kongVm = BotManager.UpdateFactionSimple(kongInfo);
                                gp.PvpAllAttacks = true;
                                gp.DoTempMissions = false;
                                gp.DoSideMissions = false;
                                // - this still seems to not work why 
                                BotManager.UseDailyBonus(kongInfo);
                            }


                            // Is campaign up?
                            bool campaignActive = kongVm.CampaignActive;

                            // Claim rewards if available
                            if (kongVm.RaidRewardsActive || kongVm.BrawlRewardsActive || kongVm.WarRewardsActive || kongVm.ConquestRewardsActive)
                            {
                                if (kongVm.RaidRewardsActive && !kongVm.ClaimedRaidRewards)
                                {
                                    KongViewModel kongVm2 = BotManager.ClaimRaidReward(this, kongInfo, raidId.ToString());
                                    if (kongVm2.Result != "False") adminOutputTextBox.AppendText(kongVm.KongName + ": Claimed Raid rewards\r\n");
                                    //else adminOutputTextBox.AppendText(kongVm.KongName + ": Failed - " + kongVm.ResultMessage.Replace("\r\n", "") + "\r\n");
                                }
                                else if (kongVm.BrawlRewardsActive && !kongVm.ClaimedBrawlRewards)
                                {
                                    KongViewModel kongVm2 = BotManager.ClaimBrawlReward(this, kongInfo);
                                    if (kongVm2.Result != "False") adminOutputTextBox.AppendText(kongVm.KongName + ": Claimed Brawl rewards\r\n");
                                    //else adminOutputTextBox.AppendText(kongVm.KongName + ": Failed - " + kongVm.ResultMessage.Replace("\r\n", "") + "\r\n");
                                }
                                else if (kongVm.WarRewardsActive && !kongVm.ClaimedWarRewards)
                                {
                                    KongViewModel kongVm2 = BotManager.ClaimFactionWarReward(this, kongInfo);
                                    if (kongVm2.Result != "False") adminOutputTextBox.AppendText(kongVm.KongName + ": Claimed War rewards\r\n");
                                    //else adminOutputTextBox.AppendText(kongVm.KongName + ": Failed - " + kongVm.ResultMessage.Replace("\r\n", "") + "\r\n");
                                }
                                else if (kongVm.ConquestRewardsActive && !kongVm.ClaimedConquestRewards)
                                {
                                    KongViewModel kongVm2 = BotManager.ClaimConquestReward(this, kongInfo);
                                    if (kongVm2.Result != "False") adminOutputTextBox.AppendText(kongVm.KongName + ": Claimed Cq rewards\r\n");
                                    //else adminOutputTextBox.AppendText(kongVm.KongName + ": Failed - " + kongVm.ResultMessage.Replace("\r\n", "") + "\r\n");
                                }
                            }

                            // Grind
                            if (kongVm.Result != "False")
                            {
                                adminOutputTextBox.AppendText(kongVm.KongName + " - Grinding\r\n");

                                // Player info
                                string name = kongVm.KongName;
                                int stamina = kongVm.UserData.Stamina;
                                int maxStamina = kongVm.UserData.MaxStamina;
                                int energy = kongVm.UserData.Energy;
                                int maxEnergy = kongVm.UserData.MaxEnergy;
                                List<MissionCompletion> missionCompletions = kongVm.MissionCompletions;
                                Quest quest1 = kongVm.Quests.Where(x => x.Id == -1).FirstOrDefault();
                                Quest quest2 = kongVm.Quests.Where(x => x.Id == -2).FirstOrDefault();
                                Quest quest3 = kongVm.Quests.Where(x => x.Id == -3).FirstOrDefault();

                                // ** Grind Pvp **
                                if (stamina > 0)
                                {
                                    //int numberOfAttacks = gp.PvpAllAttacks ? stamina : gp.PvpStaminaToSpend;
                                    int numberOfAttacks = stamina;

                                    if (gp.PvpAllAttacks) { }

                                    // Spend x attacks
                                    else if (!gp.PvpAllAttacks && grinderPvpSpendCheckBox.Checked)
                                    {
                                        int.TryParse(grinderStaminaTextBox.Text, out numberOfAttacks);
                                        if (numberOfAttacks > stamina) numberOfAttacks = stamina;
                                    }

                                    // Spend to: Spend down to X/maxStamina
                                    // But if max stamina is 50 or less, spend down to at least 25
                                    else if (!gp.PvpAllAttacks && grinderPvpSpendToCheckBox.Checked)
                                    {
                                        int.TryParse(grinderStaminaToTextBox.Text, out int pvpThreshold);
                                        numberOfAttacks = stamina - pvpThreshold;

                                        // some hackiness for low stamina players
                                        if (maxStamina <= 50 && numberOfAttacks < 20)
                                        {
                                            numberOfAttacks = 20;
                                        }

                                    }

                                    // *** Do pvp attacks ***
                                    KongViewModel kongVm2 = BotManager.AutoPvpBattles(kongInfo, numberOfAttacks);

                                    // Result of pvp
                                    resultMessage += kongVm2.Result != "False" ? kongVm2.PtuoMessage : kongName + " - TU error: " + kongVm2.ResultMessage + "\r\n";
                                }

                                // ** Grind Missions **
                                if (energy >= 0 && campaignActive == false)
                                {
                                    kongVm = BotManager.AutoMissions(kongInfo, gp.MissionEnergyThreshold, missionCompletions, kongVm.Quests ?? new List<Quest>(),
                                        gp.DoGuildQuests, gp.DoTempMissions, gp.DoSideMissions, gp.SkipMissionsIfMesmerize,
                                        quest1, quest2, quest3);

                                    resultMessage += kongVm.Result != "False" ? kongVm.PtuoMessage : kongName + " - TU error: " + kongVm.ResultMessage + "\r\n";
                                }
                                // ** Grind Campaign **
                                else if (campaignActive)
                                {
                                    while (energy > 125)
                                    {
                                        int difficulty = 1;
                                        string difficultyMode = "Normal";
                                        if (kongVm.CampaignData.NormalRewardsToCollect)
                                        {
                                        }
                                        else if (kongVm.CampaignData.HeroicRewardsToCollect)
                                        {
                                            difficulty = 2;
                                            difficultyMode = "Heroic";
                                        }
                                        else if (kongVm.CampaignData.MythicRewardsToCollect)
                                        {
                                            difficulty = 3;
                                            difficultyMode = "Mythic";
                                        }
                                        else
                                        {
                                            adminOutputTextBox.AppendText(kongVm.KongName + " - Finished with campaign\r\n");
                                            break;
                                        }

                                        // Get active deck
                                        int attackDeckIndex = int.Parse(kongVm.UserData.ActiveDeck);

                                        Dictionary<string, Object> cards = new Dictionary<string, Object>();
                                        Dictionary<string, Object> reserveCards = new Dictionary<string, Object>();
                                        UserDeck attackDeck = kongVm.UserDecks[attackDeckIndex - 1];

                                        int commanderId = attackDeck.Commander.CardId;

                                        foreach (var cardKvp in attackDeck.Cards)
                                        {
                                            cards.Add(cardKvp.Key.CardId.ToString(), cardKvp.Value.ToString());
                                        }

                                        // Start campaign
                                        adminOutputTextBox.AppendText(kongVm.KongName + " - Autoing " + difficultyMode + " campaign " + kongVm.CampaignData.Id + " with existing deck\r\n");
                                        kongVm = BotManager.StartCampaign(kongInfo, kongVm.CampaignData.Id, difficulty, commanderId, cards, reserveCards);

                                        adminOutputTextBox.AppendText(kongVm.KongName + " - Campaign " + (kongVm.BattleData.Winner.HasValue && kongVm.BattleData.Winner.Value ? "won" : "lost") + "\r\n");


                                        kongVm = BotManager.Init(kongInfo);
                                        energy = kongVm.UserData.Energy;
                                    }
                                }

                            }
                            else
                            {
                                adminOutputTextBox.AppendText(kongName + " - " + kongVm.ApiStatName + " - API error\r\n");
                            }
                        }
                        catch (Exception ex)
                        {
                            Helper.OutputWindowMessage(this, kongName + ": Error on Grind() when running mission/pvp: \r\n" + ex.Message);
                        }
                        finally
                        {
                            // Output Mission and Grind progress
                            adminOutputTextBox.AppendText(resultMessage);

                            // Track progress
                            if (batchJobNumber == grinderProgressBarJobNumber)
                            {
                                grinderProgressBar.PerformStep();
                                if (grinderProgressBar.Value >= grinderProgressBar.Maximum)
                                {
                                    grinderProgressBar.Value = 0;
                                }
                            }
                        }
                    });

                    // ---------------------------------------------
                    // Event grind IF in an Event loop
                    // ---------------------------------------------
                    if (loopEvent && loopGrind)
                    {
                        try
                        {
                            // - Figure out current event
                            // - Call corresponding method
                            // ** This may just run on whichever uses are currently selected, not the original ones **
                            if (brawlActive)
                            {
                                adminOutputTextBox.AppendText("-------BRAWL-----\r\n");
                                Parallel.ForEach(gp.SelectedUsers, new ParallelOptions { MaxDegreeOfParallelism = threads }, kongInfo =>
                                    {
                                        KongViewModel kongVm = new KongViewModel();
                                        ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                                        string kongName = kongVm.KongName;

                                        try
                                        {
                                            kongVm = BotManager.Init(kongInfo);
                                            if (kongVm.Result != "False")
                                            {
                                                bool inBattle = kongVm.BattleToResume;
                                                int energy = kongVm.BrawlData.Energy;
                                                int wins = 0;
                                                int losses = 0;

                                                ControlExtensions.InvokeEx(adminOutputTextBox, x => adminOutputTextBox.AppendText(kongVm.KongName + " - " + energy + " energy\r\n"));

                                                // Mid-battle, don't interrupt if toggled
                                                if (gp.PreventBlackBox && kongVm.BattleToResume)
                                                {
                                                    adminOutputTextBox.AppendText(kongVm.KongName + " is already in a battle! It must be finished\r\n");
                                                }
                                                else
                                                {
                                                    while (energy > 0 && energy > gp.EventEnergyThreshold)
                                                    {
                                                        // Start match
                                                        if (!inBattle) kongVm = BotManager.StartBrawlMatch(this, kongInfo);

                                                        // Match started successfully
                                                        if (kongVm != null)
                                                        {
                                                            while (kongVm.BattleData.Winner == null)
                                                            {
                                                                // Refresh the battle
                                                                kongVm = BotManager.GetCurrentOrLastBattle(this, kongInfo, missingCardStrategy: grindMissingCardStrategyComboBox.Text);

                                                                // Then build and run a sim
                                                                BatchSim batchSim = NewSimManager.BuildLiveSim(kongVm, gp.GameMode, gp.Iterations, gp.ExtraLockedCards);
                                                                NewSimManager.RunLiveSim(batchSim);
                                                                Sim sim = batchSim.Sims.OrderByDescending(x => x.WinPercent).ThenByDescending(x => x.WinScore).FirstOrDefault();

                                                                // For some reason this will be null - it needs to be rerun
                                                                if (sim.ResultDeck == null)
                                                                {
                                                                    Console.WriteLine("NULL sim.ResultDeck - here's the sim string we tried: " + sim.SimToString());
                                                                    Console.WriteLine(sim.StatusMessage);
                                                                    continue;
                                                                }

                                                                // Play the next card
                                                                BotManager.PlayCard(this, kongVm, sim);

                                                                if (kongVm.Result == "False")
                                                                {
                                                                    adminOutputTextBox.AppendText(kongVm.KongName + ": An error has occured: " + kongVm.ResultMessage + "\r\n");
                                                                    break;
                                                                }
                                                            }

                                                            // Break out of the main loop
                                                            if (kongVm.Result == "False") break;

                                                            // Did we win or lose?
                                                            if (kongVm.BattleData.Winner == true)
                                                            {
                                                                adminOutputTextBox.AppendText(kongVm.KongName + " vs. " + kongVm.BattleData.EnemyName + ": Win\r\n");
                                                                wins++;
                                                            }
                                                            else
                                                            {
                                                                adminOutputTextBox.AppendText(kongVm.KongName + " vs. " + kongVm.BattleData.EnemyName + ": **LOSS**\r\n");
                                                                losses++;
                                                            }

                                                            // Whether to output enemy deck
                                                            string enemyGuild = !String.IsNullOrWhiteSpace(kongVm.BattleData?.EnemyGuild) ? kongVm.BattleData?.EnemyGuild : "_UNKNOWN";
                                                            string enemyDeckOutput = enemyGuild + "_def_" + kongVm.BattleData.GetEnemyDeck(includePlayer: true, includeDominion: true) + "\r\n";

                                                            if (grinderOutputDecksOnUnknownCheckBox.Checked && string.IsNullOrEmpty(kongVm.BattleData?.EnemyGuild) ||
                                                               (grinderOutputDecksOnWinCheckBox.Checked && kongVm.BattleData.Winner == true) ||
                                                               (grinderOutputDecksOnLossCheckBox.Checked && kongVm.BattleData.Winner == false))
                                                            {
                                                                outputTextBox.AppendText(enemyDeckOutput);
                                                            }
                                                        }
                                                        // Match was not started successfully
                                                        else
                                                        {
                                                            adminOutputTextBox.AppendText(kongVm.KongName + ": An error has occured: " + kongVm.ResultMessage + "\r\n");
                                                            break;
                                                        }

                                                        // Flag we're not in battle
                                                        inBattle = false;
                                                        // Decrease energy
                                                        energy--;
                                                    }//loop

                                                    // Total wins / losses
                                                    if (wins + losses > 0)
                                                    {
                                                        adminOutputTextBox.AppendText(kongVm.KongName + " - " + wins + "/" + (wins + losses) + "\r\n");
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                adminOutputTextBox.AppendText("API error when using this kong string: " + kongVm.ResultMessage);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Helper.OutputWindowMessage(this, kongName + ": Error on grindBrawlAttackButton_Click(): \r\n" + ex.Message);
                                        }
                                        finally
                                        {
                                            // Track progress
                                            if (batchJobNumber == grinderProgressBarJobNumber)
                                            {
                                                grinderProgressBar.PerformStep();
                                                if (grinderProgressBar.Value >= grinderProgressBar.Maximum) grinderProgressBar.Value = 0;
                                            }
                                        }
                                    });

                            }
                            else if (raidActive)
                            {
                                adminOutputTextBox.AppendText("-------RAID------\r\n");
                                // AUTO RAID
                                if (!grindEventLoopLivesimRaidCheckBox.Checked)
                                {
                                    Parallel.ForEach(gp.SelectedUsers, new ParallelOptions { MaxDegreeOfParallelism = threads }, kongInfo =>
                                    {
                                        KongViewModel kongVm = new KongViewModel();
                                        ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                                        string kongName = kongVm.KongName;

                                        try
                                        {
                                            kongVm = BotManager.Init(kongInfo);
                                            if (kongVm.Result != "False")
                                            {
                                                // Mid-battle, don't interrupt if toggled
                                                if (gp.PreventBlackBox && kongVm.BattleToResume)
                                                {
                                                    adminOutputTextBox.AppendText(kongName + " is already in a battle! It must be finished\r\n");
                                                }
                                                else
                                                {
                                                    int raidEnergy = kongVm.RaidData.Energy;
                                                    adminOutputTextBox.AppendText(kongName + " has " + raidEnergy + " energy\r\n");

                                                    if (raidEnergy > 0 && raidEnergy > gp.EventEnergyThreshold)
                                                    {
                                                        kongVm = BotManager.AutoRaidBattles(kongInfo, gp.EventEnergyThreshold);


                                                        // Some error happened
                                                        if (kongVm.Result == "False")
                                                        {
                                                            adminOutputTextBox.AppendText(kongVm.KongName + ": An error has occured: " + kongVm.ResultMessage + "\r\n");
                                                            grinderProgressBar.PerformStep();
                                                            if (grinderProgressBar.Value >= grinderProgressBar.Maximum)
                                                            {
                                                                adminOutputTextBox.AppendText("Done raid grinding\r\n");
                                                                grinderProgressBar.Value = 0;
                                                            }
                                                            return;
                                                        }
                                                        else
                                                        {
                                                            adminOutputTextBox.AppendText(kongVm.PtuoMessage);
                                                        }
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                adminOutputTextBox.AppendText("API error when using this kong string: " + kongVm.ResultMessage);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Helper.OutputWindowMessage(this, kongName + ": Error on grindRaidAttackButton_Click(): \r\n" + ex.Message);
                                        }
                                        finally
                                        {
                                            // Track progress
                                            if (batchJobNumber == grinderProgressBarJobNumber)
                                            {
                                                grinderProgressBar.PerformStep();
                                                if (grinderProgressBar.Value >= grinderProgressBar.Maximum) grinderProgressBar.Value = 0;
                                            }
                                        }
                                    });
                                }
                                // LIVESIM RAID 
                                else
                                {
                                    Parallel.ForEach(gp.SelectedUsers, new ParallelOptions { MaxDegreeOfParallelism = threads }, kongInfo =>
                                    {
                                        KongViewModel kongVm = new KongViewModel();
                                        ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                                        string kongName = kongVm.KongName;

                                        try
                                        {
                                            kongVm = BotManager.Init(kongInfo);
                                            if (kongVm.Result != "False")
                                            {
                                                bool inBattle = kongVm.BattleToResume;
                                                int energy = kongVm.RaidData.Energy;
                                                int wins = 0;
                                                int losses = 0;
                                                int raidLevel = kongVm.RaidData.Level;

                                                // Start of raid: no raid level
                                                if (raidLevel < 1) raidLevel = 1;

                                                ControlExtensions.InvokeEx(adminOutputTextBox, x => adminOutputTextBox.AppendText(kongVm.KongName + " - " + energy + " energy\r\n"));

                                                // Mid-battle, don't interrupt if toggled
                                                if (gp.PreventBlackBox && kongVm.BattleToResume)
                                                {
                                                    adminOutputTextBox.AppendText(kongVm.KongName + " is already in a battle! It must be finished\r\n");
                                                }
                                                else
                                                {
                                                    while (energy > 0 && energy > gp.EventEnergyThreshold)
                                                    {
                                                        kongVm = new KongViewModel("getRaidInfo");
                                                        ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                                                        ApiManager.CallApi(kongVm);

                                                        // Refresh the raid level
                                                        raidLevel = kongVm.RaidData.Level;

                                                        // Start match
                                                        if (!inBattle) kongVm = BotManager.StartRaidBattle(this, kongInfo, raidId, raidLevel);


                                                        // Match started successfully
                                                        if (kongVm != null)
                                                        {
                                                            while (kongVm.BattleData.Winner == null)
                                                            {
                                                                // Refresh the battle
                                                                kongVm = BotManager.GetCurrentOrLastBattle(this, kongInfo, CONSTANTS.CURRENT_RAID_ASSAULTS);

                                                                // If there is an enemy dominion, it should be an enemy fort instead
                                                                if (!string.IsNullOrWhiteSpace(kongVm.BattleData.EnemyDominion))
                                                                {
                                                                    kongVm.BattleData.EnemyForts.Add(kongVm.BattleData.EnemyDominion);
                                                                    kongVm.BattleData.EnemyDominion = "";
                                                                }

                                                                // We added the enemy assaults that were in the deck, 
                                                                // now base the level of the remaining cards on the current raid level
                                                                int enemyCardAverageLevel = 4;
                                                                if (raidLevel >= 22) enemyCardAverageLevel = 10;
                                                                else if (raidLevel >= 20) enemyCardAverageLevel = 9;
                                                                else if (raidLevel >= 18) enemyCardAverageLevel = 8;
                                                                else if (raidLevel >= 16) enemyCardAverageLevel = 7;
                                                                else if (raidLevel >= 14) enemyCardAverageLevel = 6;
                                                                else if (raidLevel >= 12) enemyCardAverageLevel = 5;

                                                                // Modify remaining cards to use average level
                                                                for (int i = 0; i < kongVm.BattleData.EnemyCardsRemaining.Count; i++)
                                                                {
                                                                    if (enemyCardAverageLevel < 10)
                                                                        kongVm.BattleData.EnemyCardsRemaining[i] = kongVm.BattleData.EnemyCardsRemaining[i] + "-" + enemyCardAverageLevel;
                                                                }

                                                                // Then build and run a sim
                                                                BatchSim batchSim = NewSimManager.BuildLiveSim(kongVm, gp.GameMode, gp.Iterations, gp.ExtraLockedCards);
                                                                NewSimManager.RunLiveSim(batchSim);
                                                                Sim sim = batchSim.Sims.OrderByDescending(x => x.WinPercent).ThenByDescending(x => x.WinScore).FirstOrDefault();

                                                                // Debug - make sure this looks good
                                                                string winningSimString = sim.SimToString();

                                                                // For some reason this will be null - it needs to be rerun
                                                                if (sim.ResultDeck == null)
                                                                {
                                                                    Console.WriteLine("NULL sim.ResultDeck - here's the sim string we tried: " + sim.SimToString());
                                                                    continue;
                                                                }

                                                                // Play the next card
                                                                BotManager.PlayCard(this, kongVm, sim);

                                                                if (kongVm.Result == "False")
                                                                {
                                                                    adminOutputTextBox.AppendText(kongVm.KongName + ": An error has occured: " + kongVm.ResultMessage + "\r\n");
                                                                    break;
                                                                }
                                                            }

                                                            // Break out of the main loop
                                                            if (kongVm.Result == "False") break;

                                                            // Did we win or lose?
                                                            if (kongVm.BattleData.Winner == true)
                                                            {
                                                                adminOutputTextBox.AppendText(kongVm.KongName + " vs. " + CONSTANTS.CURRENT_RAID.Replace("-20","") + ": Win\r\n");
                                                                wins++;
                                                            }
                                                            else
                                                            {
                                                                adminOutputTextBox.AppendText(kongVm.KongName + " vs. " + CONSTANTS.CURRENT_RAID.Replace("-20", "") + ": **LOSS**\r\n");
                                                                losses++;
                                                            }
                                                        }
                                                        // Match was not started successfully
                                                        else
                                                        {
                                                            adminOutputTextBox.AppendText(kongVm.KongName + ": An error has occured: " + kongVm.ResultMessage + "\r\n");
                                                            break;
                                                        }

                                                        // Flag we're not in battle
                                                        inBattle = false;
                                                        // Decrease energy
                                                        energy--;
                                                    }//loop

                                                    // Total wins / losses
                                                    if (wins + losses > 0)
                                                    {
                                                        adminOutputTextBox.AppendText(kongVm.KongName + " - " + wins + "/" + (wins + losses) + "\r\n");
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                adminOutputTextBox.AppendText("API error when using this kong string: " + kongVm.ResultMessage);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Helper.OutputWindowMessage(this, kongName + ": Error on grindRaidAttackButton_Click(): \r\n" + ex.Message);
                                        }
                                        finally
                                        {
                                            // Track progress
                                            if (batchJobNumber == grinderProgressBarJobNumber)
                                            {
                                                grinderProgressBar.PerformStep();
                                                if (grinderProgressBar.Value >= grinderProgressBar.Maximum) grinderProgressBar.Value = 0;
                                            }
                                        }
                                    });
                                }
                            }
                            else if (cqActive)
                            {
                                adminOutputTextBox.AppendText("---------CQ------\r\n");

                                // crappy copypaste from the brawl function
                                string zoneName = grindCqZoneComboBox.Text;
                                int zoneId = Helper.GetConquestZoneId(zoneName);
                                if (zoneId <= 0)
                                {
                                    adminOutputTextBox.Text = "Need a valid CQ Zone";
                                    return;
                                }

                                Parallel.ForEach(gp.SelectedUsers, new ParallelOptions { MaxDegreeOfParallelism = threads }, kongInfo =>
                                {
                                    KongViewModel kongVm = new KongViewModel();
                                    ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                                    string kongName = kongVm.KongName;

                                    try
                                    {
                                        kongVm = BotManager.Init(kongInfo);
                                        if (kongVm.Result != "False")
                                        {
                                            bool inBattle = kongVm.BattleToResume;
                                            int energy = kongVm.ConquestData.Energy;
                                            int wins = 0;
                                            int losses = 0;

                                            adminOutputTextBox.AppendText(kongVm.KongName + " - " + energy + " energy\r\n");


                                            // Mid-battle, don't interrupt if toggled
                                            if (gp.PreventBlackBox && kongVm.BattleToResume)
                                            {
                                                adminOutputTextBox.AppendText(kongVm.KongName + " is already in a battle! It must be finished\r\n");
                                            }
                                            else
                                            {
                                                while (energy > 0 && energy > gp.EventEnergyThreshold)
                                                {
                                                    // Start match
                                                    if (!inBattle)
                                                        kongVm = BotManager.StartCqMatch(this, kongInfo, zoneId);


                                                    // Match started successfully
                                                    if (kongVm != null)
                                                    {
                                                        while (kongVm.BattleData.Winner == null)
                                                        {
                                                            // Refresh the battle
                                                            kongVm = BotManager.GetCurrentOrLastBattle(this, kongInfo, missingCardStrategy: grindMissingCardStrategyComboBox.Text);

                                                            // Then build and run a sim
                                                            BatchSim batchSim = NewSimManager.BuildLiveSim(kongVm, gp.GameMode, gp.Iterations, gp.ExtraLockedCards);
                                                            NewSimManager.RunLiveSim(batchSim);
                                                            Sim sim = batchSim.Sims.OrderByDescending(x => x.WinPercent).ThenByDescending(x => x.WinScore).FirstOrDefault();

                                                            // For some reason this will be null - it needs to be rerun
                                                            if (sim.ResultDeck == null)
                                                            {
                                                                Console.WriteLine("NULL sim.ResultDeck - here's the sim string we tried: " + sim.SimToString());
                                                                continue;
                                                            }

                                                            // Play the next card
                                                            BotManager.PlayCard(this, kongVm, sim);

                                                            if (kongVm.Result == "False")
                                                            {
                                                                adminOutputTextBox.AppendText(kongVm.KongName + ": An error has occured: " + kongVm.ResultMessage + "\r\n");
                                                                break;
                                                            }
                                                        }

                                                        // Break out of the main loop
                                                        if (kongVm.Result == "False") break;

                                                        // Did we win or lose?
                                                        if (kongVm.BattleData.Winner == true)
                                                        {
                                                            adminOutputTextBox.AppendText(kongVm.KongName + " vs. " + kongVm.BattleData.EnemyName + ": Win\r\n");
                                                            wins++;
                                                        }
                                                        else
                                                        {
                                                            adminOutputTextBox.AppendText(kongVm.KongName + " vs. " + kongVm.BattleData.EnemyName + ": **LOSS**\r\n");
                                                            losses++;
                                                        }

                                                        // Whether to output enemy deck
                                                        string enemyGuild = !String.IsNullOrWhiteSpace(kongVm.BattleData?.EnemyGuild) ? kongVm.BattleData?.EnemyGuild : "_UNKNOWN";
                                                        string enemyDeckOutput = enemyGuild + "_def_" + kongVm.BattleData.GetEnemyDeck(includePlayer: true, includeDominion: true) + "\r\n";

                                                        if (grinderOutputDecksOnUnknownCheckBox.Checked && string.IsNullOrEmpty(kongVm.BattleData?.EnemyGuild) || 
                                                           (grinderOutputDecksOnWinCheckBox.Checked && kongVm.BattleData.Winner == true) ||
                                                           (grinderOutputDecksOnLossCheckBox.Checked && kongVm.BattleData.Winner == false))
                                                        {
                                                            outputTextBox.AppendText(enemyDeckOutput);
                                                        }
                                                    }
                                                    // Match was not started successfully
                                                    else
                                                    {
                                                        adminOutputTextBox.AppendText(kongVm.KongName + ": An error has occured: " + kongVm.ResultMessage + "\r\n");
                                                        break;
                                                    }

                                                    // Flag we're not in battle
                                                    inBattle = false;
                                                    // Decrease energy
                                                    energy--;
                                                }//loop

                                                // Total wins / losses
                                                if (wins + losses > 0)
                                                {
                                                    adminOutputTextBox.AppendText(kongVm.KongName + " - " + wins + "/" + (wins + losses) + "\r\n");
                                                }
                                            }
                                        }
                                        else
                                        {
                                            adminOutputTextBox.AppendText("API error when using this kong string: " + kongVm.ResultMessage);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Helper.OutputWindowMessage(this, kongName + ": Error on grindCqAttackButton_Click(): \r\n" + ex.Message);
                                    }
                                    finally
                                    {
                                        // Track progress
                                        if (batchJobNumber == grinderProgressBarJobNumber)
                                        {
                                            grinderProgressBar.PerformStep();
                                            if (grinderProgressBar.Value >= grinderProgressBar.Maximum) grinderProgressBar.Value = 0;
                                        }
                                    }
                                });
                            }
                            else if (warActive)
                            {
                                adminOutputTextBox.AppendText("--------WAR------\r\n");
                                Parallel.ForEach(gp.SelectedUsers, new ParallelOptions { MaxDegreeOfParallelism = threads }, kongInfo =>
                                {
                                    KongViewModel kongVm = new KongViewModel();
                                    ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                                    string kongName = kongVm.KongName;

                                    try
                                    {
                                        kongVm = BotManager.Init(kongInfo);
                                        if (kongVm.Result != "False")
                                        {
                                            bool inBattle = kongVm.BattleToResume;
                                            int energy = kongVm?.WarData?.Energy ?? -1;
                                            int wins = 0;
                                            int losses = 0;

                                            adminOutputTextBox.AppendText(kongVm.KongName + " - " + energy + " energy\r\n");

                                            // Mid-battle, don't interrupt if toggled
                                            if (gp.PreventBlackBox && kongVm.BattleToResume)
                                            {
                                                adminOutputTextBox.AppendText(kongVm.KongName + " is already in a battle! It must be finished\r\n");
                                            }
                                            else
                                            {
                                                while (energy > 0 && energy > gp.EventEnergyThreshold)
                                                {
                                                    // Start match
                                                    if (!inBattle)
                                                        kongVm = BotManager.StartWarMatch(this, kongInfo);

                                                    // Match started successfully
                                                    if (kongVm != null)
                                                    {
                                                        while (kongVm.BattleData.Winner == null)
                                                        {
                                                            // Refresh the battle
                                                            kongVm = BotManager.GetCurrentOrLastBattle(this, kongInfo, missingCardStrategy: grindMissingCardStrategyComboBox.Text);

                                                            // Then build and run a sim
                                                            BatchSim batchSim = NewSimManager.BuildLiveSim(kongVm, gp.GameMode, gp.Iterations, gp.ExtraLockedCards);
                                                            NewSimManager.RunLiveSim(batchSim);
                                                            Sim sim = batchSim.Sims.OrderByDescending(x => x.WinPercent).ThenByDescending(x => x.WinScore).FirstOrDefault();

                                                            // For some reason this will be null - it needs to be rerun
                                                            if (sim.ResultDeck == null)
                                                            {
                                                                Console.WriteLine("NULL sim.ResultDeck - here's the sim string we tried: " + sim.SimToString());
                                                                continue;
                                                            }

                                                            // Play the next card
                                                            BotManager.PlayCard(this, kongVm, sim);

                                                            if (kongVm.Result == "False")
                                                            {
                                                                adminOutputTextBox.AppendText(kongVm.KongName + ": An error has occured: " + kongVm.ResultMessage + "\r\n");
                                                                break;
                                                            }
                                                        }

                                                        // Break out of the main loop
                                                        if (kongVm.Result == "False") break;

                                                        // Did we win or lose?
                                                        if (kongVm.BattleData.Winner == true)
                                                        {
                                                            adminOutputTextBox.AppendText(kongVm.KongName + " vs. " + kongVm.BattleData.EnemyName + ": Win\r\n");
                                                            wins++;
                                                        }
                                                        else
                                                        {
                                                            adminOutputTextBox.AppendText(kongVm.KongName + " vs. " + kongVm.BattleData.EnemyName + ": **LOSS**\r\n");
                                                            losses++;
                                                        }

                                                        // Whether to output enemy deck
                                                        string enemyGuild = !String.IsNullOrWhiteSpace(kongVm.BattleData?.EnemyGuild) ? kongVm.BattleData?.EnemyGuild : "_UNKNOWN";
                                                        string enemyDeckOutput = enemyGuild + "_def_" + kongVm.BattleData.GetEnemyDeck(includePlayer: true, includeDominion: true) + "\r\n";

                                                        if (grinderOutputDecksOnUnknownCheckBox.Checked && string.IsNullOrEmpty(kongVm.BattleData?.EnemyGuild) ||
                                                           (grinderOutputDecksOnWinCheckBox.Checked && kongVm.BattleData.Winner == true) ||
                                                           (grinderOutputDecksOnLossCheckBox.Checked && kongVm.BattleData.Winner == false))
                                                        {
                                                            outputTextBox.AppendText(enemyDeckOutput);
                                                        }
                                                    }
                                                    // Match was not started successfully
                                                    else
                                                    {
                                                        adminOutputTextBox.AppendText(kongVm.KongName + ": An error has occured: " + kongVm.ResultMessage + "\r\n");
                                                        break;
                                                    }

                                                    // Flag we're not in battle
                                                    inBattle = false;
                                                    // Decrease energy
                                                    energy--;
                                                }//loop

                                                // Total wins / losses
                                                if (wins + losses > 0)
                                                {
                                                    adminOutputTextBox.AppendText(kongVm.KongName + " - " + wins + "/" + (wins + losses) + "\r\n");
                                                }
                                            }
                                        }
                                        else
                                        {
                                            adminOutputTextBox.AppendText("API error when using this kong string: " + kongVm.ResultMessage);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Helper.OutputWindowMessage(this, kongName + ": Error on grindWarAttackButton_Click(): \r\n" + ex.Message);
                                    }
                                    finally
                                    {
                                        // Track progress
                                        if (batchJobNumber == grinderProgressBarJobNumber)
                                        {
                                            grinderProgressBar.PerformStep();
                                            if (grinderProgressBar.Value >= grinderProgressBar.Maximum) grinderProgressBar.Value = 0;
                                        }
                                    }
                                });
                            }
                            else
                            {
                                adminOutputTextBox.AppendText("-----NO EVENTS----\r\n");
                            }
                        }
                        catch (Exception ex)
                        {
                            Helper.OutputWindowMessage(this, "Error on Grind() when doing an event: \r\n" + ex.Message);
                        }
                        finally
                        {
                            grinderProgressBar.Value = 0;
                            adminOutputTextBox.AppendText("------------------\r\n");
                        }
                    }


                    // Recheck grind variables
                    loopGrind = grindGrindLoopCheckBox.Checked;
                    loopEvent = grindEventLoopCheckBox.Checked;
                    int.TryParse(grinderHoursTextBox.Text, out hoursToLoop);
                    if (hoursToLoop < 1) hoursToLoop = 1;
                    int.TryParse(grindThreadsTextBox.Text, out threads);
                    if (threads <= 0) threads = 1;
                    if (threads > 25) threads = 25;


                    long totalMinutesGrinding = stopwatch.ElapsedMilliseconds / 1000 / 60;
                    stopwatch.Reset();

                    adminOutputTextBox.AppendText("GRIND TIME: " + totalMinutesGrinding + " minutes\r\n");

                    if (loopGrind)
                    {
                        if (totalMinutesGrinding > hoursToLoop * 60)
                        {
                            adminOutputTextBox.AppendText("Loop is ON; grind time took more then " + hoursToLoop + " hours. Running again..\r\n");
                        }
                        else
                        {
                            adminOutputTextBox.AppendText("Loop is ON; grinding in " + (hoursToLoop * 60 - totalMinutesGrinding) + " minutes\r\n");
                            adminOutputTextBox.AppendText("------------------\r\n");
                            int delayInMilliseconds = (hoursToLoop * 60 - (int)totalMinutesGrinding) * 1000 * 60;
                            Thread.Sleep(delayInMilliseconds);
                        }
                    }
                    else
                    {
                        adminOutputTextBox.AppendText("Done grinding\r\n");
                        adminOutputTextBox.AppendText("------------------\r\n");
                        break;
                    }
                }
            });
            this.workerThread.Start();

        }

        /// <summary>
        /// Raid down to X attacks, going full auto
        /// </summary>
        private void grindRaidAttackButton_Click(object sender, EventArgs e)
        {
            // Setup the form
            int batchJobNumber = ++grinderProgressBarJobNumber;
            GrindParameters gp = SetupGrindParams();
            if (gp.SelectedUsers.Count == 0)
            {
                adminOutputTextBox.Text = "Select users in the left users dropdown (defined by /__users.txt) to use this action";
                return;
            }

            // Number of threads to run. The more threads running the more prone to error this becomes
            int.TryParse(grindThreadsTextBox.Text, out int threads);
            if (threads <= 0) threads = 1;
            if (threads > 25) threads = 25;

            adminOutputTextBox.AppendText("Sending in " + gp.SelectedUsers.Count + " users to Raid (Auto) on " + threads + " threads.\r\n");

            this.stopProcess = false;
            this.workerThread = new Thread(() =>
            {
                Parallel.ForEach(gp.SelectedUsers, new ParallelOptions { MaxDegreeOfParallelism = threads }, kongInfo =>
                {
                    KongViewModel kongVm = new KongViewModel();
                    ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                    string kongName = kongVm.KongName;

                    try
                    {
                        kongVm = BotManager.Init(kongInfo);
                        if (kongVm.Result != "False")
                        {
                            // Mid-battle, don't interrupt if toggled
                            if (gp.PreventBlackBox && kongVm.BattleToResume)
                            {
                                adminOutputTextBox.AppendText(kongName + " is already in a battle! It must be finished\r\n");
                            }
                            else
                            {
                                int raidEnergy = kongVm.RaidData.Energy;
                                adminOutputTextBox.AppendText(kongName + " has " + raidEnergy + " energy\r\n");

                                if (raidEnergy > 0 && raidEnergy > gp.EventEnergyThreshold)
                                {
                                    kongVm = BotManager.AutoRaidBattles(kongInfo, gp.EventEnergyThreshold);


                                    // Some error happened
                                    if (kongVm.Result == "False")
                                    {
                                        adminOutputTextBox.AppendText(kongVm.KongName + ": An error has occured: " + kongVm.ResultMessage + "\r\n");
                                        grinderProgressBar.PerformStep();
                                        if (grinderProgressBar.Value >= grinderProgressBar.Maximum)
                                        {
                                            adminOutputTextBox.AppendText("Done raid grinding\r\n");
                                            grinderProgressBar.Value = 0;
                                        }
                                        return;
                                    }
                                    else
                                    {
                                        adminOutputTextBox.AppendText(kongVm.PtuoMessage);
                                    }
                                }
                            }
                        }
                        else
                        {
                            adminOutputTextBox.AppendText("API error when using this kong string: " + kongVm.ResultMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        Helper.OutputWindowMessage(this, kongName + ": Error on grindRaidAttackButton_Click(): \r\n" + ex.Message);
                    }
                    finally
                    {
                        // Track progress
                        grinderProgressBar.PerformStep();
                        if (grinderProgressBar.Value >= grinderProgressBar.Maximum)
                        {
                            adminOutputTextBox.AppendText("Done raid grinding\r\n");
                            grinderProgressBar.Value = 0;
                        }
                    }
                });
            });
            this.workerThread.Start();
        }

        /// <summary>
        /// Raid down to X attacks, using livesim with manual raid enemy deck input (the level of incoming enemy cards must be guessed)
        /// </summary>
        private void grindRaidLiveSimAttackButton_Click(object sender, EventArgs e)
        {
            // Setup the form
            int batchJobNumber = ++grinderProgressBarJobNumber;
            GrindParameters gp = SetupGrindParams("pvp");
            if (gp.SelectedUsers.Count == 0)
            {
                adminOutputTextBox.Text = "Select users in the left users dropdown (defined by /__users.txt) to use this action";
                return;
            }

            // Number of threads to run. The more threads running the more prone to error this becomes
            int.TryParse(grindThreadsTextBox.Text, out int threads);
            if (threads <= 0) threads = 1;
            if (threads > 25) threads = 25;

            adminOutputTextBox.AppendText("Sending in " + gp.SelectedUsers.Count + " users to Raid (LSE) on " + threads + " threads.\r\n");


            this.stopProcess = false;
            this.workerThread = new Thread(() =>
            {
                Parallel.ForEach(gp.SelectedUsers, new ParallelOptions { MaxDegreeOfParallelism = threads }, kongInfo =>
                {
                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();

                    KongViewModel kongVm = new KongViewModel();
                    ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                    string kongName = kongVm.KongName;

                    try
                    {
                        kongVm = BotManager.Init(kongInfo);
                        if (kongVm.Result != "False")
                        {
                            bool inBattle = kongVm.BattleToResume;
                            int energy = kongVm.RaidData.Energy;
                            int wins = 0;
                            int losses = 0;
                            int raidId = kongVm.RaidData.Id;
                            int raidLevel = kongVm.RaidData.Level;

                            // Start of raid: no raid level
                            if (raidLevel < 1) raidLevel = 1;

                            ControlExtensions.InvokeEx(adminOutputTextBox, x => adminOutputTextBox.AppendText(kongVm.KongName + " - " + energy + " energy\r\n"));

                            // Mid-battle, don't interrupt if toggled
                            if (gp.PreventBlackBox && kongVm.BattleToResume)
                            {
                                adminOutputTextBox.AppendText(kongVm.KongName + " is already in a battle! It must be finished\r\n");
                            }
                            else
                            {
                                while (energy > 0 && energy > gp.EventEnergyThreshold)
                                {
                                    kongVm = new KongViewModel("getRaidInfo");
                                    ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                                    ApiManager.CallApi(kongVm);

                                    // Refresh the raid level
                                    raidLevel = kongVm.RaidData.Level;

                                    // Start match
                                    if (!inBattle) kongVm = BotManager.StartRaidBattle(this, kongInfo, raidId, raidLevel);


                                    // Match started successfully
                                    if (kongVm != null)
                                    {
                                        while (kongVm.BattleData.Winner == null)
                                        {
                                            // Refresh the battle
                                            kongVm = BotManager.GetCurrentOrLastBattle(this, kongInfo, CONSTANTS.CURRENT_RAID_ASSAULTS);

                                            // If there is an enemy dominion, it should be an enemy fort instead
                                            if (!string.IsNullOrWhiteSpace(kongVm.BattleData.EnemyDominion))
                                            {
                                                kongVm.BattleData.EnemyForts.Add(kongVm.BattleData.EnemyDominion);
                                                kongVm.BattleData.EnemyDominion = "";
                                            }

                                            // We added the enemy assaults that were in the deck, 
                                            // now base the level of the remaining cards on the current raid level
                                            int enemyCardAverageLevel = 4;
                                            if (raidLevel >= 22) enemyCardAverageLevel = 10;
                                            else if (raidLevel >= 20) enemyCardAverageLevel = 9;
                                            else if (raidLevel >= 18) enemyCardAverageLevel = 8;
                                            else if (raidLevel >= 16) enemyCardAverageLevel = 7;
                                            else if (raidLevel >= 14) enemyCardAverageLevel = 6;
                                            else if (raidLevel >= 12) enemyCardAverageLevel = 5;

                                            // Modify remaining cards to use average level
                                            for (int i = 0; i < kongVm.BattleData.EnemyCardsRemaining.Count; i++)
                                            {
                                                if (enemyCardAverageLevel < 10)
                                                    kongVm.BattleData.EnemyCardsRemaining[i] = kongVm.BattleData.EnemyCardsRemaining[i] + "-" + enemyCardAverageLevel;
                                            }

                                            // Then build and run a sim
                                            BatchSim batchSim = NewSimManager.BuildLiveSim(kongVm, gp.GameMode, gp.Iterations, gp.ExtraLockedCards);
                                            NewSimManager.RunLiveSim(batchSim);
                                            Sim sim = batchSim.Sims.OrderByDescending(x => x.WinPercent).ThenByDescending(x => x.WinScore).FirstOrDefault();

                                            // Debug - make sure this looks good
                                            string winningSimString = sim.SimToString();

                                            // For some reason this will be null - it needs to be rerun
                                            if (sim.ResultDeck == null)
                                            {
                                                Console.WriteLine("NULL sim.ResultDeck - here's the sim string we tried: " + sim.SimToString());
                                                continue;
                                            }

                                            // Play the next card
                                            BotManager.PlayCard(this, kongVm, sim);

                                            Console.WriteLine("Sim: " + winningSimString);

                                            if (kongVm.Result == "False")
                                            {
                                                adminOutputTextBox.AppendText(kongVm.KongName + ": An error has occured: " + kongVm.ResultMessage + "\r\n");
                                                break;
                                            }
                                        }

                                        // Break out of the main loop
                                        if (kongVm.Result == "False") break;

                                        // Did we win or lose?
                                        if (kongVm.BattleData.Winner == true)
                                        {
                                            adminOutputTextBox.AppendText(kongVm.KongName + " vs. " + CONSTANTS.CURRENT_RAID.Replace("-20", "") + ": Win\r\n");
                                            wins++;
                                        }
                                        else
                                        {
                                            adminOutputTextBox.AppendText(kongVm.KongName + " vs. " + CONSTANTS.CURRENT_RAID.Replace("-20", "") + ": **LOSS**\r\n");
                                            losses++;
                                        }
                                    }
                                    // Match was not started successfully
                                    else
                                    {
                                        adminOutputTextBox.AppendText(kongVm.KongName + ": An error has occured: " + kongVm.ResultMessage + "\r\n");
                                        break;
                                    }

                                    // Flag we're not in battle
                                    inBattle = false;
                                    // Decrease energy
                                    energy--;
                                }//loop

                                // Total wins / losses
                                if (wins + losses > 0)
                                {
                                    stopwatch.Stop();
                                    int totalTime = (int)stopwatch.ElapsedMilliseconds / 1000;
                                    int timePerBattle = (int)(stopwatch.ElapsedMilliseconds / 1000 / (wins + losses));
                                    adminOutputTextBox.AppendText(kongVm.KongName + " - " + wins + "/" + (wins + losses) + " -- Avg  " + timePerBattle / 60 + ":" + timePerBattle % 60 + " per battle.\r\n");
                                }
                            }
                        }
                        else
                        {
                            adminOutputTextBox.AppendText("API error when using this kong string: " + kongVm.ResultMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        Helper.OutputWindowMessage(this, kongName + ": Error on grindRaidAttackButton_Click(): \r\n" + ex.Message);
                    }
                    finally
                    {
                        // Track progress
                        grinderProgressBar.PerformStep();
                        if (grinderProgressBar.Value >= grinderProgressBar.Maximum)
                        {
                            adminOutputTextBox.AppendText("Done raid grinding\r\n");
                            grinderProgressBar.Value = 0;
                        }
                    }
                });
            });
            this.workerThread.Start();
        }

        /// <summary>
        /// * USE AT YOUR OWN PERIL *
        /// Refills, then livesim grinds the raid. Requires at least 1 refill to grinddown
        /// </summary>
        private void grindRaidHeroButton_Click(object sender, EventArgs e)
        {
            try
            {
                // Setup the form
                int batchJobNumber = ++grinderProgressBarJobNumber;
                GrindParameters gp = SetupGrindParams("pvp");
                if (gp.SelectedUsers.Count == 0)
                {
                    adminOutputTextBox.Text = "Select users in the left users dropdown (defined by /__users.txt) to use this action";
                    return;
                }

                // Number of threads to run. The more threads running the more prone to error this becomes
                int.TryParse(grindRaidHeroLoopTextBox.Text, out int refills);
                if (refills <= 0) refills = 0;
                if (refills > 50) refills = 50;


                this.stopProcess = false;
                this.workerThread = new Thread(() =>
                {
                    Parallel.ForEach(gp.SelectedUsers, new ParallelOptions { MaxDegreeOfParallelism = 1 }, kongInfo =>
                    {
                        Stopwatch stopwatch = new Stopwatch();
                        stopwatch.Start();

                        try
                        {
                            // Get raid info 
                            KongViewModel kongVm = new KongViewModel("getRaidInfo");
                            ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                            ApiManager.CallApi(kongVm);
                            int wins = 0;
                            int losses = 0;
                            int raidId = kongVm.RaidData.Id;
                            int raidLevel = kongVm.RaidData.Level;

                            adminOutputTextBox.AppendText("Sending in hero " + kongVm.KongName + " to bust this raid. They are refilling " + refills + " times.\r\n");

                            // Loop X times
                            for (int k = 0; k < refills; k++)
                            {
                                // Refill raid energy
                                kongVm.Message = "refillEventBattleEnergy";
                                kongVm.Params = "event_id=" + raidId + "&event_type=2";
                                ApiManager.CallApi(kongVm);

                                // Energy didn't refill - but try attacking with whatever raid energy is available
                                if (kongVm.ResultMessage != "")
                                {
                                    adminOutputTextBox.AppendText("An error has occured when trying to refill: " + kongVm.ResultMessage);
                                }

                                // Make sure the user isn't in a battle
                                kongVm = BotManager.Init(kongInfo);
                                if (kongVm.Result != "False")
                                {
                                    bool inBattle = kongVm.BattleToResume;
                                    int energy = kongVm.RaidData.Energy;
                                    raidId = kongVm.RaidData.Id;
                                    raidLevel = kongVm.RaidData.Level;

                                    ControlExtensions.InvokeEx(adminOutputTextBox, x => adminOutputTextBox.AppendText(kongVm.KongName + " - " + energy + " energy\r\n"));

                                    // Mid-battle, don't interrupt if toggled
                                    if (gp.PreventBlackBox && kongVm.BattleToResume)
                                    {
                                        adminOutputTextBox.AppendText(kongVm.KongName + " is already in a battle! It must be finished\r\n");
                                        break;
                                    }

                                    // Burn through all energy
                                    while (energy > 0)
                                    {
                                        kongVm = new KongViewModel("getRaidInfo");
                                        ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                                        ApiManager.CallApi(kongVm);

                                        // Refresh the raid level
                                        raidLevel = kongVm.RaidData.Level;

                                        // Start of raid: no raid level
                                        if (raidLevel < 1) raidLevel = 1;

                                        // Start match
                                        if (!inBattle) kongVm = BotManager.StartRaidBattle(this, kongInfo, raidId, raidLevel);


                                        // Match started successfully
                                        if (kongVm != null)
                                        {
                                            while (kongVm.BattleData.Winner == null)
                                            {
                                                // Refresh the battle
                                                kongVm = BotManager.GetCurrentOrLastBattle(this, kongInfo, CONSTANTS.CURRENT_RAID_ASSAULTS);

                                                // If there is an enemy dominion, it should be an enemy fort instead
                                                // * This happens when the enemy has 2 starting forts, and my code assumes one is a dominion. tuo will reject that "dominion" unless its moved to the fort section
                                                if (!string.IsNullOrWhiteSpace(kongVm.BattleData.EnemyDominion))
                                                {
                                                    kongVm.BattleData.EnemyForts.Add(kongVm.BattleData.EnemyDominion);
                                                    kongVm.BattleData.EnemyDominion = "";
                                                }

                                                // We added the enemy assaults that were in the deck, 
                                                // now base the level of the remaining cards on the current raid level
                                                int enemyCardAverageLevel = 4;
                                                if (raidLevel >= 22) enemyCardAverageLevel = 10;
                                                else if (raidLevel >= 20) enemyCardAverageLevel = 9;
                                                else if (raidLevel >= 18) enemyCardAverageLevel = 8;
                                                else if (raidLevel >= 16) enemyCardAverageLevel = 7;
                                                else if (raidLevel >= 14) enemyCardAverageLevel = 6;
                                                else if (raidLevel >= 12) enemyCardAverageLevel = 5;

                                                // Modify remaining cards to use average level
                                                for (int i = 0; i < kongVm.BattleData.EnemyCardsRemaining.Count; i++)
                                                {
                                                    if (enemyCardAverageLevel < 10)
                                                        kongVm.BattleData.EnemyCardsRemaining[i] = kongVm.BattleData.EnemyCardsRemaining[i] + "-" + enemyCardAverageLevel;
                                                }

                                                // Then build and run a sim
                                                BatchSim batchSim = NewSimManager.BuildLiveSim(kongVm, gp.GameMode, gp.Iterations, gp.ExtraLockedCards);
                                                NewSimManager.RunLiveSim(batchSim);
                                                Sim sim = batchSim.Sims.OrderByDescending(x => x.WinPercent).ThenByDescending(x => x.WinScore).FirstOrDefault();

                                                // For some reason this will be null - it needs to be rerun
                                                if (sim.ResultDeck == null)
                                                {
                                                    Console.WriteLine("NULL sim.ResultDeck - here's the sim string we tried: " + sim.SimToString());
                                                    continue;
                                                }

                                                // Play the next card
                                                BotManager.PlayCard(this, kongVm, sim);

                                                if (kongVm.Result == "False")
                                                {
                                                    adminOutputTextBox.AppendText(kongVm.KongName + ": An error has occured: " + kongVm.ResultMessage + "\r\n");
                                                    break;
                                                }
                                            }

                                            // Break out of the main loop
                                            if (kongVm.Result == "False") break;

                                            // Did we win or lose?
                                            if (kongVm.BattleData.Winner == true)
                                            {
                                                adminOutputTextBox.AppendText(kongVm.KongName + " vs. " + CONSTANTS.CURRENT_RAID.Replace("-20", "") + ": Win\r\n");
                                                wins++;
                                            }
                                            else
                                            {
                                                adminOutputTextBox.AppendText(kongVm.KongName + " vs. " + CONSTANTS.CURRENT_RAID.Replace("-20", "") + ": **LOSS**\r\n");
                                                losses++;
                                            }
                                        }
                                        // Match was not started successfully
                                        else
                                        {
                                            adminOutputTextBox.AppendText(kongVm.KongName + ": An error has occured: " + kongVm.ResultMessage + "\r\n");
                                            break;
                                        }

                                        // Flag we're not in battle
                                        inBattle = false;
                                        // Decrease energy
                                        energy--;
                                    }//loop
                                }
                                else
                                {
                                    adminOutputTextBox.AppendText("API error when using this kong string: " + kongVm.ResultMessage);
                                }
                            }//refill loop

                            // Total wins / losses
                            if (wins + losses > 0)
                            {
                                stopwatch.Stop();
                                int totalTime = (int)stopwatch.ElapsedMilliseconds / 1000;
                                int timePerBattle = (int)(stopwatch.ElapsedMilliseconds / 1000 / (wins + losses));
                                adminOutputTextBox.AppendText(kongVm.KongName + " - " + wins + "/" + (wins + losses) + " -- Avg  " + timePerBattle / 60 + ":" + timePerBattle % 60 + " per battle.\r\n");
                            }
                        }
                        catch (Exception ex)
                        {
                            Helper.OutputWindowMessage(this, "Error on grindRaidAttackButton_Click(): \r\n" + ex);
                        }
                        finally
                        {
                            // Track progress
                            grinderProgressBar.PerformStep();
                            if (grinderProgressBar.Value >= grinderProgressBar.Maximum)
                            {
                                adminOutputTextBox.AppendText("Done raid grinding\r\n");
                                grinderProgressBar.Value = 0;
                            }
                        }
                    });
                });
                this.workerThread.Start();


            }
            catch (Exception ex)
            {
                adminOutputTextBox.AppendText("An error has occured when trying to hero: " + ex);
            }
        }

        /// <summary>
        /// Brawl down to X attacks, using livesim
        /// </summary>
        private void grindBrawlAttackButton_Click(object sender, EventArgs e)
        {
            // Setup the form
            int batchJobNumber = ++grinderProgressBarJobNumber;
            GrindParameters gp = SetupGrindParams("brawl");
            if (gp.SelectedUsers.Count == 0)
            {
                adminOutputTextBox.Text = "Select users in the left users dropdown (defined by /__users.txt) to use this action";
                return;
            }


            // Number of threads to run. The more threads running the more prone to error this becomes
            int.TryParse(grindThreadsTextBox.Text, out int threads);
            if (threads <= 0) threads = 1;
            if (threads > 25) threads = 25;

            adminOutputTextBox.AppendText("Sending in " + gp.SelectedUsers.Count + " users to Brawl on " + threads + " threads.\r\n");


            this.stopProcess = false;
            this.workerThread = new Thread(() =>
            {
                Parallel.ForEach(gp.SelectedUsers, new ParallelOptions { MaxDegreeOfParallelism = threads }, kongInfo =>
                {
                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();

                    KongViewModel kongVm = new KongViewModel();
                    ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                    string kongName = kongVm.KongName;

                    try
                    {
                        kongVm = BotManager.Init(kongInfo);
                        if (kongVm.Result != "False")
                        {
                            bool inBattle = kongVm.BattleToResume;
                            int energy = kongVm.BrawlData.Energy;
                            int wins = 0;
                            int losses = 0;

                            ControlExtensions.InvokeEx(adminOutputTextBox, x => adminOutputTextBox.AppendText(kongVm.KongName + " - " + energy + " energy\r\n"));

                            // Mid-battle, don't interrupt if toggled
                            if (gp.PreventBlackBox && kongVm.BattleToResume)
                            {
                                adminOutputTextBox.AppendText(kongVm.KongName + " is already in a battle! It must be finished\r\n");
                            }
                            else
                            {
                                while (energy > 0 && energy > gp.EventEnergyThreshold)
                                {
                                    // Start match
                                    if (!inBattle) kongVm = BotManager.StartBrawlMatch(this, kongInfo);

                                    // Match started successfully
                                    if (kongVm != null)
                                    {
                                        while (kongVm.BattleData.Winner == null)
                                        {
                                            // Refresh the battle
                                            kongVm = BotManager.GetCurrentOrLastBattle(this, kongInfo, missingCardStrategy: grindMissingCardStrategyComboBox.Text);

                                            // Then build and run a sim
                                            BatchSim batchSim = NewSimManager.BuildLiveSim(kongVm, gp.GameMode, gp.Iterations, gp.ExtraLockedCards);
                                            NewSimManager.RunLiveSim(batchSim);
                                            Sim sim = batchSim.Sims.OrderByDescending(x => x.WinPercent).ThenByDescending(x => x.WinScore).FirstOrDefault();

                                            // For some reason this will be null - it needs to be rerun
                                            if (sim.ResultDeck == null)
                                            {
                                                Console.WriteLine("NULL sim.ResultDeck - here's the sim string we tried: " + sim.SimToString());
                                                continue;
                                            }

                                            // Play the next card
                                            BotManager.PlayCard(this, kongVm, sim);

                                            if (kongVm.Result == "False")
                                            {
                                                adminOutputTextBox.AppendText(kongVm.KongName + ": An error has occured: " + kongVm.ResultMessage + "\r\n");
                                                break;
                                            }
                                        }

                                        // Break out of the main loop
                                        if (kongVm.Result == "False") break;

                                        // Did we win or lose?
                                        if (kongVm.BattleData.Winner == true)
                                        {
                                            adminOutputTextBox.AppendText(kongVm.KongName + " vs. " + kongVm.BattleData.EnemyName + ": Win\r\n");
                                            wins++;
                                        }
                                        else
                                        {
                                            adminOutputTextBox.AppendText(kongVm.KongName + " vs. " + kongVm.BattleData.EnemyName + ": **LOSS**\r\n");
                                            losses++;
                                        }

                                        // Whether to output enemy deck
                                        string enemyGuild = !String.IsNullOrWhiteSpace(kongVm.BattleData?.EnemyGuild) ? kongVm.BattleData?.EnemyGuild : "_UNKNOWN";
                                        string enemyDeckOutput = enemyGuild + "_def_" + kongVm.BattleData.GetEnemyDeck(includePlayer: true, includeDominion: true) + "\r\n";

                                        if (grinderOutputDecksOnUnknownCheckBox.Checked && string.IsNullOrEmpty(kongVm.BattleData?.EnemyGuild) ||
                                           (grinderOutputDecksOnWinCheckBox.Checked && kongVm.BattleData.Winner == true) ||
                                           (grinderOutputDecksOnLossCheckBox.Checked && kongVm.BattleData.Winner == false))
                                        {
                                            outputTextBox.AppendText(enemyDeckOutput);
                                        }

                                    }
                                    // Match was not started successfully
                                    else
                                    {
                                        adminOutputTextBox.AppendText(kongVm.KongName + ": An error has occured: " + kongVm.ResultMessage + "\r\n");
                                        break;
                                    }

                                    // Flag we're not in battle
                                    inBattle = false;
                                    // Decrease energy
                                    energy--;
                                }//loop

                                // Total wins / losses
                                if (wins + losses > 0)
                                {
                                    stopwatch.Stop();
                                    int totalTime = (int)stopwatch.ElapsedMilliseconds / 1000;
                                    int timePerBattle = (int)(stopwatch.ElapsedMilliseconds / 1000 / (wins + losses));
                                    adminOutputTextBox.AppendText(kongVm.KongName + " - " + wins + "/" + (wins + losses) + " -- Avg  " + timePerBattle / 60 + ":" + timePerBattle % 60 + " per battle.\r\n");
                                }
                            }
                        }
                        else
                        {
                            adminOutputTextBox.AppendText("API error when using this kong string: " + kongVm.ResultMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        Helper.OutputWindowMessage(this, kongName + ": Error on grindBrawlAttackButton_Click(): \r\n" + ex.Message);
                    }
                    finally
                    {
                        // Track progress
                        grinderProgressBar.PerformStep();
                        if (grinderProgressBar.Value >= grinderProgressBar.Maximum)
                        {
                            adminOutputTextBox.AppendText("Done brawl grinding\r\n");
                            grinderProgressBar.Value = 0;
                        }
                    }
                });
            });
            this.workerThread.Start();
        }

        /// <summary>
        /// CQ down to X attacks, using livesim
        /// </summary>
        private void grindCqAttackButton_Click(object sender, EventArgs e)
        {
            // Setup the form
            int batchJobNumber = ++grinderProgressBarJobNumber;
            GrindParameters gp = SetupGrindParams();
            if (gp.SelectedUsers.Count == 0)
            {
                adminOutputTextBox.Text = "Select users in the left users dropdown (defined by /__users.txt) to use this action";
                return;
            }

            string zoneName = grindCqZoneComboBox.Text;
            int zoneId = Helper.GetConquestZoneId(zoneName);
            if (zoneId <= 0)
            {
                adminOutputTextBox.Text = "Need a valid CQ Zone";
                return;
            }

            // Number of threads to run. The more threads running the more prone to error this becomes
            int.TryParse(grindThreadsTextBox.Text, out int threads);
            if (threads <= 0) threads = 1;
            if (threads > 25) threads = 25;

            adminOutputTextBox.AppendText("Sending in " + gp.SelectedUsers.Count + " users to CQ on " + threads + " threads.\r\n");

            this.stopProcess = false;
            this.workerThread = new Thread(() =>
            {
                Parallel.ForEach(gp.SelectedUsers, new ParallelOptions { MaxDegreeOfParallelism = threads }, kongInfo =>
                {
                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();

                    KongViewModel kongVm = new KongViewModel();
                    ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                    string kongName = kongVm.KongName;

                    try
                    {
                        kongVm = BotManager.Init(kongInfo);
                        if (kongVm.Result != "False")
                        {
                            bool inBattle = kongVm.BattleToResume;
                            int energy = kongVm.ConquestData.Energy;
                            int wins = 0;
                            int losses = 0;

                            adminOutputTextBox.AppendText(kongVm.KongName + " - " + energy + " energy\r\n");


                            // Mid-battle, don't interrupt if toggled
                            if (gp.PreventBlackBox && kongVm.BattleToResume)
                            {
                                adminOutputTextBox.AppendText(kongVm.KongName + " is already in a battle! It must be finished\r\n");
                            }
                            else
                            {
                                while (energy > 0 && energy > gp.EventEnergyThreshold)
                                {
                                    // Start match
                                    if (!inBattle)
                                        kongVm = BotManager.StartCqMatch(this, kongInfo, zoneId);


                                    // Match started successfully
                                    if (kongVm != null)
                                    {
                                        while (kongVm.BattleData.Winner == null)
                                        {
                                            // Refresh the battle
                                            kongVm = BotManager.GetCurrentOrLastBattle(this, kongInfo, missingCardStrategy: grindMissingCardStrategyComboBox.Text);

                                            // Then build and run a sim
                                            BatchSim batchSim = NewSimManager.BuildLiveSim(kongVm, gp.GameMode, gp.Iterations, gp.ExtraLockedCards);
                                            NewSimManager.RunLiveSim(batchSim);
                                            Sim sim = batchSim.Sims.OrderByDescending(x => x.WinPercent).ThenByDescending(x => x.WinScore).FirstOrDefault();

                                            // For some reason this will be null - it needs to be rerun
                                            if (sim.ResultDeck == null)
                                            {
                                                Console.WriteLine("NULL sim.ResultDeck - here's the sim string we tried: " + sim.SimToString());
                                                continue;
                                            }

                                            // Play the next card
                                            BotManager.PlayCard(this, kongVm, sim);

                                            if (kongVm.Result == "False")
                                            {
                                                adminOutputTextBox.AppendText(kongVm.KongName + ": An error has occured: " + kongVm.ResultMessage + "\r\n");
                                                break;
                                            }
                                        }

                                        // Break out of the main loop
                                        if (kongVm.Result == "False") break;

                                        // Did we win or lose?
                                        if (kongVm.BattleData.Winner == true)
                                        {
                                            adminOutputTextBox.AppendText(kongVm.KongName + " vs. " + kongVm.BattleData.EnemyName + ": Win\r\n");
                                            wins++;
                                        }
                                        else
                                        {
                                            adminOutputTextBox.AppendText(kongVm.KongName + " vs. " + kongVm.BattleData.EnemyName + ": **LOSS**\r\n");
                                            losses++;
                                        }

                                        // Whether to output enemy deck
                                        string enemyGuild = !String.IsNullOrWhiteSpace(kongVm.BattleData?.EnemyGuild) ? kongVm.BattleData?.EnemyGuild : "_UNKNOWN";
                                        string enemyDeckOutput = enemyGuild + "_def_" + kongVm.BattleData.GetEnemyDeck(includePlayer: true, includeDominion: true) + "\r\n";

                                        if (grinderOutputDecksOnUnknownCheckBox.Checked && string.IsNullOrEmpty(kongVm.BattleData?.EnemyGuild) ||
                                           (grinderOutputDecksOnWinCheckBox.Checked && kongVm.BattleData.Winner == true) ||
                                           (grinderOutputDecksOnLossCheckBox.Checked && kongVm.BattleData.Winner == false))
                                        {
                                            outputTextBox.AppendText(enemyDeckOutput);
                                        }
                                    }
                                    // Match was not started successfully
                                    else
                                    {
                                        adminOutputTextBox.AppendText(kongVm.KongName + ": An error has occured: " + kongVm.ResultMessage + "\r\n");
                                        break;
                                    }

                                    // Flag we're not in battle
                                    inBattle = false;
                                    // Decrease energy
                                    energy--;
                                }//loop

                                // Total wins / losses
                                if (wins + losses > 0)
                                {
                                    stopwatch.Stop();
                                    int totalTime = (int)stopwatch.ElapsedMilliseconds / 1000;
                                    int timePerBattle = (int)(stopwatch.ElapsedMilliseconds / 1000 / (wins + losses));
                                    adminOutputTextBox.AppendText(kongVm.KongName + " - " + wins + "/" + (wins + losses) + " -- Avg  " + timePerBattle / 60 + ":" + timePerBattle % 60 + " per battle.\r\n");
                                }
                            }
                        }
                        else
                        {
                            adminOutputTextBox.AppendText("API error when using this kong string: " + kongVm.ResultMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        Helper.OutputWindowMessage(this, kongName + ": Error on grindCqAttackButton_Click(): \r\n" + ex.Message);
                    }
                    finally
                    {
                        // Track progress
                        grinderProgressBar.PerformStep();
                        if (grinderProgressBar.Value >= grinderProgressBar.Maximum)
                        {
                            adminOutputTextBox.AppendText("Done CQ Grinding\r\n");
                            grinderProgressBar.Value = 0;
                        }
                    }
                });
            });
            this.workerThread.Start();
        }

        /// <summary>
        /// War down to X attacks, using livesim
        /// </summary>
        private void grindWarAttackButton_Click(object sender, EventArgs e)
        {
            // Setup the form
            int batchJobNumber = ++grinderProgressBarJobNumber;
            GrindParameters gp = SetupGrindParams("gw");
            if (gp.SelectedUsers.Count == 0)
            {
                adminOutputTextBox.Text = "Select users in the left users dropdown (defined by /__users.txt) to use this action";
                return;
            }

            // Number of threads to run. The more threads running the more prone to error this becomes
            int.TryParse(grindThreadsTextBox.Text, out int threads);
            if (threads <= 0) threads = 1;
            if (threads > 25) threads = 25;
            adminOutputTextBox.AppendText("Sending in " + gp.SelectedUsers.Count + " users to War on " + threads + " threads.\r\n");


            //TODO: Is putting this in a new thread causing odd errors
            this.stopProcess = false;
            this.workerThread = new Thread(() =>
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                Parallel.ForEach(gp.SelectedUsers, new ParallelOptions { MaxDegreeOfParallelism = threads }, kongInfo =>
                {
                    KongViewModel kongVm = new KongViewModel();
                    ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                    string kongName = kongVm.KongName;

                    try
                    {
                        kongVm = BotManager.Init(kongInfo);
                        if (kongVm.Result != "False")
                        {
                            bool inBattle = kongVm.BattleToResume;
                            int energy = kongVm?.WarData?.Energy ?? -1;
                            int wins = 0;
                            int losses = 0;

                            adminOutputTextBox.AppendText(kongVm.KongName + " - " + energy + " energy\r\n");

                            // Mid-battle, don't interrupt if toggled
                            if (gp.PreventBlackBox && kongVm.BattleToResume)
                            {
                                adminOutputTextBox.AppendText(kongVm.KongName + " is already in a battle! It must be finished\r\n");
                            }
                            else
                            {
                                while (energy > 0 && energy > gp.EventEnergyThreshold)
                                {
                                    // Start match
                                    if (!inBattle)
                                        kongVm = BotManager.StartWarMatch(this, kongInfo);

                                    // Match started successfully
                                    if (kongVm != null)
                                    {
                                        while (kongVm.BattleData.Winner == null)
                                        {
                                            // Refresh the battle
                                            kongVm = BotManager.GetCurrentOrLastBattle(this, kongInfo, missingCardStrategy: grindMissingCardStrategyComboBox.Text);

                                            // Then build and run a sim
                                            BatchSim batchSim = NewSimManager.BuildLiveSim(kongVm, gp.GameMode, gp.Iterations, gp.ExtraLockedCards);
                                            NewSimManager.RunLiveSim(batchSim);
                                            Sim sim = batchSim.Sims.OrderByDescending(x => x.WinPercent).ThenByDescending(x => x.WinScore).FirstOrDefault();

                                            // For some reason this will be null - it needs to be rerun
                                            if (sim.ResultDeck == null)
                                            {
                                                Console.WriteLine("NULL sim.ResultDeck - here's the sim string we tried: " + sim.SimToString());
                                                continue;
                                            }

                                            // Play the next card
                                            BotManager.PlayCard(this, kongVm, sim);

                                            if (kongVm.Result == "False")
                                            {
                                                adminOutputTextBox.AppendText(kongVm.KongName + ": An error has occured: " + kongVm.ResultMessage + "\r\n");
                                                break;
                                            }
                                        }

                                        // Break out of the main loop
                                        if (kongVm.Result == "False") break;

                                        // Did we win or lose?
                                        if (kongVm.BattleData.Winner == true)
                                        {
                                            adminOutputTextBox.AppendText(kongVm.KongName + " vs. " + kongVm.BattleData.EnemyName + ": Win\r\n");
                                            wins++;
                                        }
                                        else
                                        {
                                            adminOutputTextBox.AppendText(kongVm.KongName + " vs. " + kongVm.BattleData.EnemyName + ": **LOSS**\r\n");
                                            losses++;
                                        }

                                        // Whether to output enemy deck
                                        string enemyGuild = !String.IsNullOrWhiteSpace(kongVm.BattleData?.EnemyGuild) ? kongVm.BattleData?.EnemyGuild : "_UNKNOWN";
                                        string enemyDeckOutput = enemyGuild + "_def_" + kongVm.BattleData.GetEnemyDeck(includePlayer: true, includeDominion: true) + "\r\n";

                                        if (grinderOutputDecksOnUnknownCheckBox.Checked && string.IsNullOrEmpty(kongVm.BattleData?.EnemyGuild) ||
                                           (grinderOutputDecksOnWinCheckBox.Checked && kongVm.BattleData.Winner == true) ||
                                           (grinderOutputDecksOnLossCheckBox.Checked && kongVm.BattleData.Winner == false))
                                        {
                                            outputTextBox.AppendText(enemyDeckOutput);
                                        }
                                    }
                                    // Match was not started successfully
                                    else
                                    {
                                        adminOutputTextBox.AppendText(kongVm.KongName + ": An error has occured: " + kongVm.ResultMessage + "\r\n");
                                        break;
                                    }

                                    // Flag we're not in battle
                                    inBattle = false;
                                    // Decrease energy
                                    energy--;
                                }//loop

                                // Total wins / losses
                                if (wins + losses > 0)
                                {
                                    stopwatch.Stop();
                                    int totalTime = (int)stopwatch.ElapsedMilliseconds / 1000;
                                    int timePerBattle = (int)(stopwatch.ElapsedMilliseconds / 1000 / (wins + losses));
                                    adminOutputTextBox.AppendText(kongVm.KongName + " - " + wins + "/" + (wins + losses) + " -- Avg  " + timePerBattle / 60 + ":" + timePerBattle % 60 + " per battle.\r\n");
                                }
                            }
                        }
                        else
                        {
                            adminOutputTextBox.AppendText("API error when using this kong string: " + kongVm.ResultMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        Helper.OutputWindowMessage(this, kongName + ": Error on grindWarAttackButton_Click(): \r\n" + ex.Message);
                    }
                    finally
                    {
                        // Track progress
                        grinderProgressBar.PerformStep();
                        if (grinderProgressBar.Value >= grinderProgressBar.Maximum)
                        {
                            adminOutputTextBox.AppendText("Done\r\n");
                            grinderProgressBar.Value = 0;
                        }
                    }
                });
            });
            this.workerThread.Start();
        }

        /// <summary>
        /// Attack campaign, doing auto with the current deck
        /// TODO: Implement
        /// </summary>
        private void grindCampAttackButton_Click(object sender, EventArgs e)
        {
            // Setup the form
            GrindParameters gp = SetupGrindParams("gw");
            if (gp.SelectedUsers.Count == 0)
            {
                adminOutputTextBox.Text = "Select users in the left users dropdown (defined by /__users.txt) to use this action";
                return;
            }

            // Number of threads to run. The more threads running the more prone to error this becomes
            int.TryParse(grindThreadsTextBox.Text, out int threads);
            if (threads <= 0) threads = 3;
            if (threads > 10) threads = 10;
            adminOutputTextBox.AppendText("Sending in " + gp.SelectedUsers.Count + " users to Campaign on " + threads + " threads.\r\n");


            //TODO: Is putting this in a new thread causing odd errors
            this.stopProcess = false;
            this.workerThread = new Thread(() =>
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                Parallel.ForEach(gp.SelectedUsers, new ParallelOptions { MaxDegreeOfParallelism = threads }, kongInfo =>
                {
                    try
                    {
                        KongViewModel kongVm = BotManager.Init(kongInfo);
                        int missionEnergy = kongVm.UserData.Energy;

                        while (missionEnergy > 125 && missionEnergy > gp.EventEnergyThreshold)
                        {
                            if (kongVm.Result != "False" && kongVm.CampaignActive)
                            {
                                int difficulty = 1;
                                string difficultyMode = "Normal";
                                if (kongVm.CampaignData.NormalRewardsToCollect)
                                {
                                }
                                else if (kongVm.CampaignData.HeroicRewardsToCollect)
                                {
                                    difficulty = 2;
                                    difficultyMode = "Heroic";
                                }
                                else if (kongVm.CampaignData.MythicRewardsToCollect)
                                {
                                    difficulty = 3;
                                    difficultyMode = "Mythic";
                                }
                                else
                                {
                                    adminOutputTextBox.AppendText(kongVm.KongName + " - Completed campaign\r\n");
                                    break;
                                }

                                // Get active deck
                                int attackDeckIndex = int.Parse(kongVm.UserData.ActiveDeck);

                                Dictionary<string, Object> cards = new Dictionary<string, Object>();
                                Dictionary<string, Object> reserveCards = new Dictionary<string, Object>();
                                UserDeck attackDeck = kongVm.UserDecks[attackDeckIndex - 1];

                                int commanderId = attackDeck.Commander.CardId;

                                foreach (var cardKvp in attackDeck.Cards)
                                {
                                    cards.Add(cardKvp.Key.CardId.ToString(), cardKvp.Value.ToString());
                                }

                                // Start campaign
                                adminOutputTextBox.AppendText(kongVm.KongName + " - Autoing " + difficultyMode + " campaign " + kongVm.CampaignData.Id + " with existing deck\r\n");
                                kongVm = BotManager.StartCampaign(kongInfo, kongVm.CampaignData.Id, difficulty, commanderId, cards, reserveCards);

                                adminOutputTextBox.AppendText(kongVm.KongName + " - Campaign " + (kongVm.BattleData.Winner.HasValue && kongVm.BattleData.Winner.Value ? "won" : "lost") + "\r\n");


                                kongVm = BotManager.Init(kongInfo);
                                missionEnergy = kongVm.UserData.Energy;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Helper.OutputWindowMessage(this, "Error on grindBrawlAttackButton_Click(): \r\n" + ex);
                    }
                    finally
                    {
                        // Track progress
                        grinderProgressBar.PerformStep();
                        if (grinderProgressBar.Value >= grinderProgressBar.Maximum)
                        {
                            adminOutputTextBox.AppendText("Done\r\n");
                            grinderProgressBar.Value = 0;
                        }
                    }
                });
            });
            this.workerThread.Start();
        }

        /// <summary>
        /// If the user is currently in a battle, play it out
        /// </summary>
        private void grinderFinishBattleButton_Click(object sender, EventArgs e)
        {
            List<string> selectedUsers = adminPlayerListBox.SelectedItems.Cast<string>().ToList();
            if (selectedUsers.Count == 0)
            {
                adminOutputTextBox.Text = "Select users in the left users dropdown (defined by /__users.txt) to use this action";
                return;
            }

            this.stopProcess = false;
            this.workerThread = new Thread(() =>
            {
                Parallel.ForEach(selectedUsers, new ParallelOptions { MaxDegreeOfParallelism = 5 }, kongInfo =>
                {
                    try
                    {
                        //KongViewModel kongVm = BotManager.Init(kongInfo);

                        // Refresh the battle
                        KongViewModel kongVm = BotManager.GetCurrentOrLastBattle(this, kongInfo, missingCardStrategy: grindMissingCardStrategyComboBox.Text);
                        if (kongVm.Result != "False" && kongVm.BattleToResume)
                        {
                            // limit finishing a battle to 20 executions (just in case that this is stuck. At max it should be 9-10 iterations)
                            for (int i = 1; i <= 20; i++)
                            {
                                // What turn is the player on
                                int.TryParse(kongVm?.BattleData?.Turn, out int turn);
                                adminOutputTextBox.AppendText(kongVm.KongName + " - Turn " + turn + "\r\n");

                                // Build and run a sim
                                BatchSim batchSim = NewSimManager.BuildLiveSim(kongVm, "surge", iterations: 500, extraLockedCards: 0);
                                NewSimManager.RunLiveSim(batchSim);
                                Sim sim = batchSim.Sims.OrderByDescending(x => x.WinPercent).ThenByDescending(x => x.WinScore).FirstOrDefault();

                                // For some reason this will be null - it needs to be rerun
                                if (sim.ResultDeck == null)
                                {
                                    adminOutputTextBox.AppendText("NULL sim.ResultDeck - here's the sim string we tried: " + sim.SimToString());
                                    kongVm = BotManager.GetCurrentOrLastBattle(this, kongInfo);
                                    continue;
                                }

                                // Play the next card. This will return battleData
                                string cardToPlay = BotManager.PlayCard(this, kongVm, sim);
                                adminOutputTextBox.AppendText(kongVm.KongName + " - Playing card " + cardToPlay + " (" + sim.WinPercent + ")\r\n");


                                // kongVm error
                                if (kongVm.Result == "False")
                                {
                                    adminOutputTextBox.AppendText(kongVm.KongName + "An error has occured: " + kongVm.ResultMessage + "\r\n");
                                    break;
                                }

                                // Winner found, break out
                                if (kongVm.BattleData.Winner != null)
                                {
                                    adminOutputTextBox.AppendText(kongVm.KongName + " - " + (kongVm.BattleData.Winner == true ? "Win" : "**LOSS**") + "\r\n");
                                    break;
                                }

                                // Some unknown error happened
                                if (i == 20) adminOutputTextBox.AppendText(kongVm.KongName + " - Could not break out of battle!");
                            }
                        }
                        else if (kongVm.Result == "False")
                        {
                            adminOutputTextBox.AppendText(kongVm.KongName + "An error has occured: " + kongVm.ResultMessage + "\r\n");
                        }
                        else //kongVm.BattleData.Winner != null
                        {
                            adminOutputTextBox.AppendText(kongVm.KongName + " - Not in a battle, and " + (kongVm.BattleData.Winner == true ? "won" : "lost") + " the last battle\r\n");
                        }
                    }
                    catch (Exception ex)
                    {
                        Helper.OutputWindowMessage(this, "grinderFinishBattleButton_Click(): Error - \r\n" + ex);
                    }
                    finally
                    {
                        // Track progress
                        grinderProgressBar.PerformStep();
                        if (grinderProgressBar.Value >= grinderProgressBar.Maximum)
                        {
                            adminOutputTextBox.AppendText("Done\r\n");
                            grinderProgressBar.Value = 0;
                        }
                    }
                });
            });

            this.workerThread.Start();
        }

        /// <summary>
        /// On each account, buy gold and salvage commons/rares until we run out, hit SP max, or can't because of inventory
        /// </summary>
        private void grindBuyMaxGoldButton_Click(object sender, EventArgs e)
        {
            // Setup the form
            GrindParameters gp = SetupGrindParams();
            if (gp.SelectedUsers.Count == 0)
            {
                adminOutputTextBox.Text = "Select users in the left users dropdown (defined by /__users.txt) to use this action";
                return;
            }


            // Number of threads to run. The more threads running the more prone to error this becomes
            int.TryParse(grindThreadsTextBox.Text, out int threads);
            if (threads <= 0) threads = 1;
            if (threads > 25) threads = 25;

            this.stopProcess = false;
            this.workerThread = new Thread(() =>
            {
                Parallel.ForEach(gp.SelectedUsers, new ParallelOptions { MaxDegreeOfParallelism = threads }, kongInfo =>
                {
                    try
                    {
                        // Call Init to get the player's stats
                        KongViewModel kongVm = BotManager.Init(kongInfo);
                        if (kongVm.Result != "False")
                        {
                            // Get inventory, gold, and salvage
                            int gold = kongVm.UserData.Gold;
                            int inventory = kongVm.UserData.Inventory;
                            int maxInventory = kongVm.UserData.MaxInventory;
                            int salvage = kongVm.UserData.Salvage;
                            int maxSalvage = kongVm.UserData.MaxSalvage;

                            // Player has gold to buy
                            if (gold > 2000)
                            {
                                // Buy gold packs until out of gold, inventory is full, or SP is full
                                for (int i = 0; i < 50; i++)
                                {
                                    // ------------------------------
                                    // Check salvage
                                    // ------------------------------
                                    if (maxSalvage - salvage < 50)
                                    {
                                        adminOutputTextBox.AppendText(kongVm.KongName + " - Near max SP\r\n");
                                        break;
                                    }

                                    // ------------------------------
                                    // Buy gold packs
                                    // ------------------------------
                                    adminOutputTextBox.AppendText(kongVm.KongName + " - Buying gold\r\n");
                                    kongVm = BotManager.BuyGold(this, kongInfo, goldPacks: 50, displayGoldBuys: false);

                                    // * An error happened (usually inventory space). But if its because gold is empty, we're done
                                    if (kongVm.Result == "False" && kongVm.ResultMessage.Contains("You cannot afford"))
                                    {
                                        adminOutputTextBox.AppendText(kongVm.KongName + " - " + kongVm.ResultMessage + "\r\n");
                                        break;
                                    }


                                    // ------------------------------
                                    // Salvage commons/rares
                                    // ------------------------------
                                    Console.WriteLine(kongVm.KongName + " - Salvaging commons and rares");
                                    kongVm = BotManager.SalvageL1CommonsAndRares(this, kongInfo);


                                    // ------------------------------
                                    // Check inventory space
                                    // ------------------------------
                                    if (kongVm.Result != "False")
                                    {
                                        gold = kongVm.UserData.Gold;
                                        inventory = kongVm.UserData.Inventory;
                                        salvage = kongVm.UserData.Salvage;

                                        Console.WriteLine(kongVm.KongName + " - " + salvage + "/" + maxSalvage + " SP. " + inventory + "/" + maxInventory + " cards");

                                        // ------------------------------
                                        // Check Inventory
                                        // ------------------------------
                                        if (inventory <= 0 || maxInventory <= 0 || (maxInventory - inventory < 20))
                                        {
                                            adminOutputTextBox.AppendText(kongVm.KongName + ": Inventory near full: " + inventory + "/" + maxInventory + "\r\n");

                                            break;
                                        }

                                        // ------------------------------
                                        // Check salvage again
                                        // ------------------------------
                                        if (salvage < 0 || maxSalvage <= 0 || (maxSalvage - salvage < 100))
                                        {
                                            adminOutputTextBox.AppendText(kongVm.KongName + ": SP is near max: " + salvage + "/" + maxSalvage + "\r\n");
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine(kongVm.KongName + " - " + kongVm.ResultMessage);
                                        break;
                                    }

                                }
                            }
                            //else
                            //{
                            outputTextBox.AppendText(kongVm.KongName + " - " + gold + " gold left\r\n");
                            //}
                        }
                        else
                        {
                            adminOutputTextBox.AppendText(kongVm.KongName + " - API error: " + kongVm.ResultMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        Helper.OutputWindowMessage(this, "grindBuyMaxGoldButton_Click(): Error - \r\n" + ex);
                    }
                    finally
                    {
                        // Track progress
                        grinderProgressBar.PerformStep();
                        if (grinderProgressBar.Value >= grinderProgressBar.Maximum)
                        {
                            adminOutputTextBox.AppendText("Done - Gold buying\r\n");
                            grinderProgressBar.Value = 0;
                        }
                    }
                });
            });
            this.workerThread.Start();
        }

        /// <summary>
        /// On each account, attempt to build a card
        /// </summary>
        private void grindBuildCardButton_Click(object sender, EventArgs e)
        {
            // Setup the form
            int batchJobNumber = ++grinderProgressBarJobNumber;
            GrindParameters gp = SetupGrindParams();
            if (gp.SelectedUsers.Count == 0)
            {
                adminOutputTextBox.Text = "Select users in the left users dropdown (defined by /__users.txt) to use this action";
                return;
            }

            // Number of threads to run. The more threads running the more prone to error this becomes
            int.TryParse(grindThreadsTextBox.Text, out int threads);
            if (threads <= 0) threads = 1;
            if (threads > 25) threads = 25;


            // List of cards to make. Each one is comma separated
            List<string> cardsToProcess = gp.CardToBuild.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            cardsToProcess = cardsToProcess.Select(x => x.Trim()).ToList();

            // If salvaging, refresh the salvage list
            if (grindBuildCardMassSpAfterCheckBox.Checked)
            {
                Helper.RefreshSalvageList(this);
            }


            this.stopProcess = false;
            this.workerThread = new Thread(() =>
            {
                Parallel.ForEach(gp.SelectedUsers, new ParallelOptions { MaxDegreeOfParallelism = threads }, kongInfo =>
                {
                    string kongName = "";
                    bool verboseOutput = !grindBuildCardSimpleOutputCheckBox.Checked;

                    try
                    {
                        // ------------------------------------------------
                        // For each card in the list of cards
                        // ------------------------------------------------
                        foreach (var cardToBuild in cardsToProcess)
                        {
                            KongViewModel kongVm = BotManager.Init(kongInfo);
                            ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                            kongName = kongVm.KongName;
                            List<string> inventoryCards = new List<string>();
                            List<string> restoreCards = new List<string>();

                            // --------------------------------
                            // Build the card
                            // --------------------------------
                            if (kongVm.Result != "False")
                            {
                                // Get player inventory/restore list
                                foreach (var playerCard in kongVm.PlayerCards)
                                {
                                    string cardName = playerCard.Key.Name;
                                    int cardCount = playerCard.Value;
                                    inventoryCards.Add(cardName + (cardCount > 1 ? ("#" + cardCount) : ""));
                                }

                                foreach (var playerCard in kongVm.RestoreCards.OrderBy(x => x.Key.Name))
                                {
                                    string cardName = playerCard.Key.Name;
                                    int cardCount = playerCard.Value;
                                    restoreCards.Add(cardName + (cardCount > 1 ? ("#" + cardCount) : ""));
                                }

                                // --------------------------------
                                // COPY CHECK - Does the player already have enough copies of this card?
                                // --------------------------------
                                int.TryParse(grindBuildCardHasCardCountTextBox.Text, out int cardCopyThreshold);
                                if (grindBuildCardHasCardCheckBox.Checked && cardCopyThreshold > 0)
                                {
                                    var playerCardKvp = kongVm.PlayerCards.Where(x => x.Key.Name == cardToBuild).FirstOrDefault();
                                    if (playerCardKvp.Key != null && playerCardKvp.Value >= cardCopyThreshold)
                                    {
                                        if (verboseOutput) adminOutputTextBox.AppendText(kongName + " - Success. Player already has " + cardCopyThreshold + " copies of " + cardToBuild + "\r\n");
                                        break;
                                    }
                                }
                                
                                // --------------------------------
                                // UPGRADE CHECK 
                                // --------------------------------
                                // Do we already have this card but its not fully upgraded
                                if (inventoryCards.FirstOrDefault(x => x.StartsWith(cardToBuild + "-")) != null)
                                { 
                                    if (inventoryCards.Where(x => x.StartsWith(cardToBuild + "-1")).FirstOrDefault() != null)
                                        kongVm = BotManager.UpgradeCard(kongInfo, cardToBuild + "-1");
                                    else if (inventoryCards.Where(x => x.StartsWith(cardToBuild + "-2")).FirstOrDefault() != null)
                                        kongVm = BotManager.UpgradeCard(kongInfo, cardToBuild + "-2");
                                    else if (inventoryCards.Where(x => x.StartsWith(cardToBuild + "-3")).FirstOrDefault() != null)
                                        kongVm = BotManager.UpgradeCard(kongInfo, cardToBuild + "-3");
                                    else if (inventoryCards.Where(x => x.StartsWith(cardToBuild + "-4")).FirstOrDefault() != null)
                                        kongVm = BotManager.UpgradeCard(kongInfo, cardToBuild + "-4");
                                    else if (inventoryCards.Where(x => x.StartsWith(cardToBuild + "-5")).FirstOrDefault() != null)
                                        kongVm = BotManager.UpgradeCard(kongInfo, cardToBuild + "-5");

                                    // Output success/fail
                                    if (kongVm.Result != "False")
                                    {
                                        if (verboseOutput) adminOutputTextBox.AppendText(kongName + " - Success\r\n");
                                    }
                                    else
                                    {
                                        adminOutputTextBox.AppendText(kongName + " - Fail: " + kongVm.ResultMessage.Replace("\r\n", "") + "\r\n");
                                    }
                                }
                                // --------------------------------
                                // RESTORE CHECK - this is in restore
                                // --------------------------------
                                else if (restoreCards.Where(x => x.StartsWith(cardToBuild + "-1")).FirstOrDefault() != null)
                                {
                                    kongVm = BotManager.RestoreCard(kongInfo, cardToBuild + "-1");

                                    // Was this a success?
                                    if (kongVm.Result != "False" && verboseOutput) adminOutputTextBox.AppendText(kongName + " - Success\r\n");
                                    else if (kongVm.Result != "False") { }
                                    else adminOutputTextBox.AppendText(kongName + " - Fail: " + kongVm.ResultMessage.Replace("\r\n", "") + "\r\n");
                                }

                                // --------------------------------
                                // TRY TO BUILD THIS CARD
                                // --------------------------------
                                else
                                {
                                    // Find the card object
                                    string cardName = !cardToBuild.EndsWith("-1") ? cardToBuild + "-1" : cardToBuild;
                                    Card card = CardManager.GetPlayerCardByName(cardName);

                                    //if (card == null) card = CardManager.GetPlayerCardByName(cardName.Replace("-1", ""));
                                    if (card != null)
                                    {
                                        if (verboseOutput) adminOutputTextBox.AppendText(kongName + ": Attempting to make: " + cardName + "\r\n");
                                        bool buildCardFailed = false;

                                        // Recursively go through the card's recipe, making what it needs to finally build it
                                        kongVm = BuildCard(card, inventoryCards, restoreCards, kongInfo, ref buildCardFailed, verboseOutput: verboseOutput);

                                        // Was this a success?
                                        if (buildCardFailed && kongVm.ResultMessage != "")
                                        {
                                            adminOutputTextBox.AppendText(kongName + ": Fail - " + kongVm.ResultMessage.Replace("\r\n", "").Replace("[", "").Replace("]", "") + "\r\n");
                                        }
                                        else if (buildCardFailed)
                                        {
                                            if (verboseOutput) adminOutputTextBox.AppendText(kongName + ": Fail - Does not have " + card.Name + "\r\n");
                                        }
                                        else if (kongVm.Result != "False" && !buildCardFailed && verboseOutput) adminOutputTextBox.AppendText(kongName + " - Success\r\n");
                                    }
                                    else
                                    {
                                        adminOutputTextBox.AppendText(kongName + " - Card not recognized\r\n");
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                adminOutputTextBox.AppendText(kongName + " - API error: " + kongVm.ResultMessage.Replace("\r\n", ""));
                                return;
                            }

                            // ------------------------------
                            // BUY GOLD OPTION
                            // ------------------------------
                            if (grindBuildCardGoldAfterCheckBox.Checked)
                            {
                                // Call Init to get the player's stats
                                //kongVm = BotManager.Init(kongInfo);
                                kongVm = BotManager.GetUserAccount(kongInfo); // DOUBLE CHECK IF THIS RETURNS RIGHT DATA
                                if (kongVm.Result != "False")
                                {
                                    // Get inventory, gold, and salvage
                                    int gold = kongVm.UserData.Gold;
                                    int inventory = kongVm.UserData.Inventory;
                                    int maxInventory = kongVm.UserData.MaxInventory;
                                    int salvage = kongVm.UserData.Salvage;
                                    int maxSalvage = kongVm.UserData.MaxSalvage;

                                    if (verboseOutput) adminOutputTextBox.AppendText(kongName + " - Gold: " + gold / 1000 + "K. SP: " + salvage + "/" + maxSalvage + "\r\n");

                                    // Player has gold to buy
                                    if (gold > 2000)
                                    {
                                        // Buy gold packs until out of gold, inventory is full, or SP is full
                                        for (int i = 0; i < 30; i++)
                                        {
                                            // ------------------------------
                                            // Check salvage
                                            // ------------------------------
                                            if (maxSalvage - salvage < 100) break;

                                            // ------------------------------
                                            // Buy gold packs
                                            // ------------------------------
                                            if (verboseOutput) adminOutputTextBox.AppendText(kongVm.KongName + " - Buying gold\r\n");
                                            kongVm = BotManager.BuyGold(this, kongInfo, goldPacks: 30, displayGoldBuys: false);

                                            // NO GOLD LEFT
                                            if (kongVm.Result == "False" && kongVm.ResultMessage.Contains("You cannot afford"))
                                            {
                                                Console.WriteLine(kongVm.KongName + " - OUT OF GOLD\r\n");
                                                break;
                                            }

                                            // ------------------------------
                                            // Salvage commons/rares
                                            // ------------------------------
                                            kongVm = BotManager.SalvageL1CommonsAndRares(this, kongInfo);
                                            

                                            // ------------------------------
                                            // Check inventory space
                                            // ------------------------------
                                            if (kongVm.Result != "False")
                                            {
                                                gold = kongVm.UserData.Gold;
                                                inventory = kongVm.UserData.Inventory;
                                                salvage = kongVm.UserData.Salvage;

                                                // ------------------------------
                                                // Check Inventory
                                                // ------------------------------
                                                if (inventory <= 0 || maxInventory <= 0 || (maxInventory - inventory < 25))
                                                {
                                                    Console.WriteLine(kongVm.KongName + " - " + salvage + "/" + maxSalvage + " SP. " + inventory + "/" + maxInventory + " cards");
                                                    adminOutputTextBox.AppendText(kongVm.KongName + ": Inventory at max!\r\n");
                                                    break;
                                                }

                                                // ------------------------------
                                                // Check salvage again
                                                // ------------------------------
                                                if (salvage < 0 || maxSalvage <= 0 || (maxSalvage - salvage < 100))
                                                {
                                                    Console.WriteLine(kongVm.KongName + " - " + salvage + "/" + maxSalvage + " SP. " + inventory + "/" + maxInventory + " cards");
                                                    break;
                                                }
                                            }
                                            else
                                            {
                                                adminOutputTextBox.AppendText(kongVm.KongName + " - Error buying gold: " + kongVm.ResultMessage + "\r\n");
                                                Console.WriteLine(kongVm.KongName + " - " + kongVm.ResultMessage);
                                                break;
                                            }
                                        }
                                    }

                                    // User's remaining gold
                                    if (gold > 400000)
                                    {
                                        if (verboseOutput) outputTextBox.AppendText(kongVm.KongName + " - " + gold / 1000 + "K gold\r\n");
                                    }
                                    else if (gold > 200000)
                                    {
                                        if (verboseOutput) outputTextBox.AppendText(kongVm.KongName + " - " + gold / 1000 + "K gold\r\n");
                                    }
                                    else if (gold > 100000)
                                    {
                                        outputTextBox.AppendText(kongVm.KongName + " - " + gold / 1000 + "K gold\r\n");
                                    }
                                    else
                                    {
                                        outputTextBox.AppendText(kongVm.KongName + " - " + gold / 1000 + "K gold - LOW\r\n");
                                    }
                                }
                                else
                                {
                                    adminOutputTextBox.AppendText(kongVm.KongName + " - API error: " + kongVm.ResultMessage.Replace("\r\n", ""));
                                }
                            }


                            // ------------------------------
                            // SALVAGE OPTION
                            // ------------------------------
                            if (grindBuildCardMassSpAfterCheckBox.Checked)
                            {
                                string cardList = "";
                                if (grinderMassSpOldRewardsCheckBox.Checked) cardList += string.Join(",", CONSTANTS.SALVAGE_REWARDS);
                                if (grinderMassSpBaseEpicsCheckBox.Checked) cardList += string.Join(",", CONSTANTS.BASE_EPICS);
                                if (grinderMassSpBaseLegendsCheckBox.Checked) cardList += string.Join(",", CONSTANTS.BASE_LEGENDS);

                                if (grinderMassSpOldBoxCheckBox.Checked) cardList += string.Join(",", CONSTANTS.SALVAGE_AGGRESSIVE);

                                string[] cardsToSalvage = cardList.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                                cardsToSalvage = cardsToSalvage.Select(s => s.Trim()).ToArray();

                                // Salvage commons/rares first
                                kongVm = BotManager.SalvageL1CommonsAndRares(this, kongInfo);

                                // Call Init to get the player's inventory
                                kongVm = BotManager.Init(kongInfo);
                                ApiManager.GetKongInfoFromString(kongVm, kongInfo);

                                // Store player cards
                                var playerCards = kongVm.PlayerCards;

                                if (kongVm.Result != "False")
                                {
                                    int salvage = kongVm.UserData.Salvage;
                                    int maxSalvage = kongVm.UserData.MaxSalvage;

                                    // Don't salvage if capped
                                    if (maxSalvage - salvage <= 100) break;
                                    
                                    // speed up card search by searching on names to see if the card exists
                                    // (playerCards.Keys.Name is a slow lookup)
                                    List<string> playerCardNames = playerCards.Keys.Select(x => x.Name).ToList();


                                    foreach (string cardName in cardsToSalvage)
                                    {
                                        // TODO: Regex check here

                                        if (!playerCardNames.Contains(cardName) &&
                                            !playerCardNames.Contains(cardName + "-1") &&
                                            !playerCardNames.Contains(cardName + "-2") &&
                                            !playerCardNames.Contains(cardName + "-3") &&
                                            !playerCardNames.Contains(cardName + "-4") &&
                                            !playerCardNames.Contains(cardName + "-5"))
                                        {
                                            continue;
                                        }

                                        // This is done in case there's multiple copies of the card at different levels
                                        List<Card> foundCards = playerCards.Keys
                                                .Where(x => x.Name == cardName || x.Name == cardName + "-1" ||
                                                            x.Name == cardName + "-2" || x.Name == cardName + "-3" ||
                                                            x.Name == cardName + "-4" || x.Name == cardName + "-5")
                                                .ToList();

                                        foreach (var c in foundCards)
                                        {
                                            int count = playerCards[c];

                                            // Remove up to 5 of each base epic from playerCards
                                            if (grinderKeepFiveBaseEpicsCheckBox.Checked)
                                            {
                                                if (CONSTANTS.BaseEpics.Contains(c.Name.Replace("-1", ""))) count -= 5;
                                                if (count <= 0) continue;
                                            }
                                            if (grinderKeepSomeBaseLegendsCheckBox.Checked)
                                            {
                                                if (CONSTANTS.BaseLegends.Contains(c.Name.Replace("-1", ""))) count -= 10;
                                                if (count <= 0) continue;
                                            }

                                            Console.WriteLine(kongName + " - salvaging " + count + " " + c.Name);

                                            for (int i = 0; i < count; i++)
                                            {
                                                kongVm = BotManager.SalvageCard(kongInfo, c.CardId, salvageLockedCards: true);

                                                // Was this a success?
                                                if (kongVm.Result != "False" && verboseOutput)
                                                    outputTextBox.AppendText(kongName + " - Salvaged " + c.Name + "\r\n");
                                                else if (kongVm.Result == "False")
                                                    outputTextBox.AppendText(kongName + " - Failed salvaging " + c.Name + ": " + kongVm.ResultMessage + "\r\n");
                                            }
                                        }

                                        // Get new salvage
                                        kongVm = BotManager.GetUserAccount(kongInfo);
                                        salvage = kongVm.UserData.Salvage;

                                        // User is near salvage cap and we can end early
                                        if (maxSalvage - salvage <= 100) break;
                                    }

                                    if (maxSalvage - salvage <= 200)
                                    {
                                        if (verboseOutput) adminOutputTextBox.AppendText(kongName + " - Near max SP\r\n");
                                    }
                                    // Supress SP count below 2750
                                    else if (salvage <= 2750)
                                    {
                                        if (verboseOutput) adminOutputTextBox.AppendText(kongName + " - " + salvage + "/" + maxSalvage + " SP\r\n");
                                    }
                                    else
                                    {
                                    }
                                }
                                else
                                {
                                    adminOutputTextBox.AppendText(kongName + " - API error" + kongVm.ResultMessage);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Helper.OutputWindowMessage(this, "grindBuildCardButton_click(): Error - \r\n" + ex);
                    }
                    finally
                    {
                        //adminOutputTextBox.AppendText(kongName + " - Done\r\n");

                        // Track progress
                        grinderProgressBar.PerformStep();
                        if (grinderProgressBar.Value >= grinderProgressBar.Maximum)
                        {
                            grinderProgressBar.Value = 0;
                        }
                    }
                });
            });
            this.workerThread.Start();
        }

        /// <summary>
        /// On each account, attempt to restore a card
        /// </summary>
        private void grindRestoreCardButton_Click(object sender, EventArgs e)
        {
            // Setup the form
            int batchJobNumber = ++grinderProgressBarJobNumber;
            GrindParameters gp = SetupGrindParams();
            if (gp.SelectedUsers.Count == 0)
            {
                adminOutputTextBox.Text = "Select users in the left users dropdown (defined by /__users.txt) to use this action";
                return;
            }

            // Number of threads to run. The more threads running the more prone to error this becomes
            int.TryParse(grindThreadsTextBox.Text, out int threads);
            if (threads <= 0) threads = 1;
            if (threads > 25) threads = 25;


            // List of cards to make. Each one is comma separated
            List<string> cardsToProcess = gp.CardToBuild.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            cardsToProcess = cardsToProcess.Select(x => x.Trim()).ToList();

            // If salvaging, refresh the salvage list
            if (grindBuildCardMassSpAfterCheckBox.Checked)
            {
                Helper.RefreshSalvageList(this);
            }


            this.stopProcess = false;
            this.workerThread = new Thread(() =>
            {
                Parallel.ForEach(gp.SelectedUsers, new ParallelOptions { MaxDegreeOfParallelism = threads }, kongInfo =>
                {
                    string kongName = "";
                    bool verboseOutput = !grindBuildCardSimpleOutputCheckBox.Checked;

                    try
                    {
                        // ------------------------------------------------
                        // For each card in the list of cards
                        // ------------------------------------------------
                        foreach (var cardToBuild in cardsToProcess)
                        {
                            KongViewModel kongVm = BotManager.Init(kongInfo);
                            ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                            kongName = kongVm.KongName;
                            List<string> inventoryCards = new List<string>();
                            List<string> restoreCards = new List<string>();

                            // --------------------------------
                            // Restore the card
                            // --------------------------------
                            if (kongVm.Result != "False")
                            {

                                foreach (var playerCard in kongVm.RestoreCards.OrderBy(x => x.Key.Name))
                                {
                                    string cardName = playerCard.Key.Name;
                                    int cardCount = playerCard.Value;
                                    restoreCards.Add(cardName + (cardCount > 1 ? ("#" + cardCount) : ""));
                                }

                                // --------------------------------
                                // COPY CHECK - Does the player already have enough copies of this card?
                                // --------------------------------
                                int.TryParse(grindBuildCardHasCardCountTextBox.Text, out int cardCopyThreshold);
                                if (grindBuildCardHasCardCheckBox.Checked && cardCopyThreshold > 0)
                                {
                                    var playerCardKvp = kongVm.PlayerCards.Where(x => x.Key.Name == cardToBuild).FirstOrDefault();
                                    if (playerCardKvp.Key != null && playerCardKvp.Value >= cardCopyThreshold)
                                    {
                                        if (verboseOutput) adminOutputTextBox.AppendText(kongName + " - Success. Player already has " + cardCopyThreshold + " copies of " + cardToBuild + "\r\n");
                                        break;
                                    }
                                }

                                // --------------------------------
                                // RESTORE CHECK - this is in restore
                                // --------------------------------
                                if (restoreCards.Where(x => x.StartsWith(cardToBuild + "-1")).FirstOrDefault() != null)
                                {
                                    kongVm = BotManager.RestoreCard(kongInfo, cardToBuild + "-1");

                                    // Was this a success?
                                    if (kongVm.Result != "False" && verboseOutput) adminOutputTextBox.AppendText(kongName + " - Success\r\n");
                                    else if (kongVm.Result != "False") { }
                                    else adminOutputTextBox.AppendText(kongName + " - Fail: " + kongVm.ResultMessage.Replace("\r\n", "") + "\r\n");
                                }
                                else
                                {
                                    if (verboseOutput) adminOutputTextBox.AppendText(kongName + ": Fail - Does not have " + cardToBuild + " in restore\r\n");
                                }
                            }
                            else
                            {
                                adminOutputTextBox.AppendText(kongName + " - API error: " + kongVm.ResultMessage.Replace("\r\n", ""));
                                return;
                            }

                            // ------------------------------
                            // BUY GOLD OPTION
                            // ------------------------------
                            if (grindBuildCardGoldAfterCheckBox.Checked)
                            {
                                // Call Init to get the player's stats
                                //kongVm = BotManager.Init(kongInfo);
                                kongVm = BotManager.GetUserAccount(kongInfo); // DOUBLE CHECK IF THIS RETURNS RIGHT DATA
                                if (kongVm.Result != "False")
                                {
                                    // Get inventory, gold, and salvage
                                    int gold = kongVm.UserData.Gold;
                                    int inventory = kongVm.UserData.Inventory;
                                    int maxInventory = kongVm.UserData.MaxInventory;
                                    int salvage = kongVm.UserData.Salvage;
                                    int maxSalvage = kongVm.UserData.MaxSalvage;

                                    if (verboseOutput) adminOutputTextBox.AppendText(kongName + " - Gold: " + gold / 1000 + "K. SP: " + salvage + "/" + maxSalvage + "\r\n");

                                    // Player has gold to buy
                                    if (gold > 2000)
                                    {
                                        // Buy gold packs until out of gold, inventory is full, or SP is full
                                        for (int i = 0; i < 30; i++)
                                        {
                                            // ------------------------------
                                            // Check salvage
                                            // ------------------------------
                                            if (maxSalvage - salvage < 100) break;

                                            // ------------------------------
                                            // Buy gold packs
                                            // ------------------------------
                                            if (verboseOutput) adminOutputTextBox.AppendText(kongVm.KongName + " - Buying gold\r\n");
                                            kongVm = BotManager.BuyGold(this, kongInfo, goldPacks: 30, displayGoldBuys: false);

                                            // NO GOLD LEFT
                                            if (kongVm.Result == "False" && kongVm.ResultMessage.Contains("You cannot afford"))
                                            {
                                                Console.WriteLine(kongVm.KongName + " - OUT OF GOLD\r\n");
                                                break;
                                            }

                                            // ------------------------------
                                            // Salvage commons/rares
                                            // ------------------------------
                                            kongVm = BotManager.SalvageL1CommonsAndRares(this, kongInfo);


                                            // ------------------------------
                                            // Check inventory space
                                            // ------------------------------
                                            if (kongVm.Result != "False")
                                            {
                                                gold = kongVm.UserData.Gold;
                                                inventory = kongVm.UserData.Inventory;
                                                salvage = kongVm.UserData.Salvage;

                                                // ------------------------------
                                                // Check Inventory
                                                // ------------------------------
                                                if (inventory <= 0 || maxInventory <= 0 || (maxInventory - inventory < 25))
                                                {
                                                    Console.WriteLine(kongVm.KongName + " - " + salvage + "/" + maxSalvage + " SP. " + inventory + "/" + maxInventory + " cards");
                                                    adminOutputTextBox.AppendText(kongVm.KongName + ": Inventory at max!\r\n");
                                                    break;
                                                }

                                                // ------------------------------
                                                // Check salvage again
                                                // ------------------------------
                                                if (salvage < 0 || maxSalvage <= 0 || (maxSalvage - salvage < 100))
                                                {
                                                    Console.WriteLine(kongVm.KongName + " - " + salvage + "/" + maxSalvage + " SP. " + inventory + "/" + maxInventory + " cards");
                                                    break;
                                                }
                                            }
                                            else
                                            {
                                                adminOutputTextBox.AppendText(kongVm.KongName + " - Error buying gold: " + kongVm.ResultMessage + "\r\n");
                                                Console.WriteLine(kongVm.KongName + " - " + kongVm.ResultMessage);
                                                break;
                                            }
                                        }
                                    }

                                    // User's remaining gold
                                    if (gold > 400000)
                                    {
                                        if (verboseOutput) outputTextBox.AppendText(kongVm.KongName + " - " + gold / 1000 + "K gold\r\n");
                                    }
                                    else if (gold > 200000)
                                    {
                                        if (verboseOutput) outputTextBox.AppendText(kongVm.KongName + " - " + gold / 1000 + "K gold\r\n");
                                    }
                                    else if (gold > 100000)
                                    {
                                        outputTextBox.AppendText(kongVm.KongName + " - " + gold / 1000 + "K gold\r\n");
                                    }
                                    else
                                    {
                                        outputTextBox.AppendText(kongVm.KongName + " - " + gold / 1000 + "K gold - LOW\r\n");
                                    }
                                }
                                else
                                {
                                    adminOutputTextBox.AppendText(kongVm.KongName + " - API error: " + kongVm.ResultMessage.Replace("\r\n", ""));
                                }
                            }


                            // ------------------------------
                            // SALVAGE OPTION
                            // ------------------------------
                            if (grindBuildCardMassSpAfterCheckBox.Checked)
                            {
                                string cardList = "";
                                if (grinderMassSpOldRewardsCheckBox.Checked) cardList += string.Join(",", CONSTANTS.SALVAGE_REWARDS);
                                if (grinderMassSpBaseEpicsCheckBox.Checked) cardList += string.Join(",", CONSTANTS.BASE_EPICS);
                                if (grinderMassSpBaseLegendsCheckBox.Checked) cardList += string.Join(",", CONSTANTS.BASE_LEGENDS);

                                if (grinderMassSpOldBoxCheckBox.Checked) cardList += string.Join(",", CONSTANTS.SALVAGE_AGGRESSIVE);

                                string[] cardsToSalvage = cardList.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                                cardsToSalvage = cardsToSalvage.Select(s => s.Trim()).ToArray();

                                // Salvage commons/rares first
                                kongVm = BotManager.SalvageL1CommonsAndRares(this, kongInfo);

                                // Call Init to get the player's inventory
                                kongVm = BotManager.Init(kongInfo);
                                ApiManager.GetKongInfoFromString(kongVm, kongInfo);

                                // Store player cards
                                var playerCards = kongVm.PlayerCards;

                                if (kongVm.Result != "False")
                                {
                                    int salvage = kongVm.UserData.Salvage;
                                    int maxSalvage = kongVm.UserData.MaxSalvage;

                                    // Don't salvage if capped
                                    if (maxSalvage - salvage <= 100) break;

                                    // speed up card search by searching on names to see if the card exists
                                    // (playerCards.Keys.Name is a slow lookup)
                                    List<string> playerCardNames = playerCards.Keys.Select(x => x.Name).ToList();


                                    foreach (string cardName in cardsToSalvage)
                                    {
                                        // TODO: Regex check here

                                        if (!playerCardNames.Contains(cardName) &&
                                            !playerCardNames.Contains(cardName + "-1") &&
                                            !playerCardNames.Contains(cardName + "-2") &&
                                            !playerCardNames.Contains(cardName + "-3") &&
                                            !playerCardNames.Contains(cardName + "-4") &&
                                            !playerCardNames.Contains(cardName + "-5"))
                                        {
                                            continue;
                                        }

                                        // This is done in case there's multiple copies of the card at different levels
                                        List<Card> foundCards = playerCards.Keys
                                                .Where(x => x.Name == cardName || x.Name == cardName + "-1" ||
                                                            x.Name == cardName + "-2" || x.Name == cardName + "-3" ||
                                                            x.Name == cardName + "-4" || x.Name == cardName + "-5")
                                                .ToList();

                                        foreach (var c in foundCards)
                                        {
                                            int count = playerCards[c];

                                            // Remove up to 5 of each base epic from playerCards
                                            if (grinderKeepFiveBaseEpicsCheckBox.Checked)
                                            {
                                                if (CONSTANTS.BaseEpics.Contains(c.Name.Replace("-1", ""))) count -= 5;
                                                if (count <= 0) continue;
                                            }
                                            if (grinderKeepSomeBaseLegendsCheckBox.Checked)
                                            {
                                                if (CONSTANTS.BaseLegends.Contains(c.Name.Replace("-1", ""))) count -= 10;
                                                if (count <= 0) continue;
                                            }

                                            Console.WriteLine(kongName + " - salvaging " + count + " " + c.Name);

                                            for (int i = 0; i < count; i++)
                                            {
                                                kongVm = BotManager.SalvageCard(kongInfo, c.CardId, salvageLockedCards: true);

                                                // Was this a success?
                                                if (kongVm.Result != "False" && verboseOutput)
                                                    outputTextBox.AppendText(kongName + " - Salvaged " + c.Name + "\r\n");
                                                else if (kongVm.Result == "False")
                                                    outputTextBox.AppendText(kongName + " - Failed salvaging " + c.Name + ": " + kongVm.ResultMessage + "\r\n");
                                            }
                                        }

                                        // Get new salvage
                                        kongVm = BotManager.GetUserAccount(kongInfo);
                                        salvage = kongVm.UserData.Salvage;

                                        // User is near salvage cap and we can end early
                                        if (maxSalvage - salvage <= 100) break;
                                    }

                                    if (maxSalvage - salvage <= 200)
                                    {
                                        if (verboseOutput) adminOutputTextBox.AppendText(kongName + " - Near max SP\r\n");
                                    }
                                    // Supress SP count below 2750
                                    else if (salvage <= 2750)
                                    {
                                        if (verboseOutput) adminOutputTextBox.AppendText(kongName + " - " + salvage + "/" + maxSalvage + " SP\r\n");
                                    }
                                    else
                                    {
                                    }
                                }
                                else
                                {
                                    adminOutputTextBox.AppendText(kongName + " - API error" + kongVm.ResultMessage);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Helper.OutputWindowMessage(this, "grindBuildCardButton_click(): Error - \r\n" + ex);
                    }
                    finally
                    {
                        //adminOutputTextBox.AppendText(kongName + " - Done\r\n");

                        // Track progress
                        grinderProgressBar.PerformStep();
                        if (grinderProgressBar.Value >= grinderProgressBar.Maximum)
                        {
                            grinderProgressBar.Value = 0;
                        }
                    }
                });
            });
            this.workerThread.Start();
        }

        /// <summary>
        /// On each account, attempt to salvage card(s)
        /// </summary>
        private void grindSalvageCardButton_Click(object sender, EventArgs e)
        {
            // Setup the form
            int batchJobNumber = ++grinderProgressBarJobNumber;
            GrindParameters gp = SetupGrindParams();
            if (gp.SelectedUsers.Count == 0)
            {
                adminOutputTextBox.Text = "Select users in the left users dropdown (defined by /__users.txt) to use this action";
                return;
            }

            // List of cards to salvage separated by comma. We're assuming we salvage all copies
            string cardList = "";

            if (grinderMassSpOldRewardsCheckBox.Checked) cardList += string.Join(",", CONSTANTS.SALVAGE_REWARDS) + ",";
            if (grinderMassSpBaseEpicsCheckBox.Checked) cardList += string.Join(",", CONSTANTS.BASE_EPICS) + ",";
            if (grinderMassSpBaseLegendsCheckBox.Checked) cardList += string.Join(",", CONSTANTS.BASE_LEGENDS) + ",";
            if (grinderMassSpOldBoxCheckBox.Checked) cardList += string.Join(",", CONSTANTS.SALVAGE_AGGRESSIVE);

            string[] cardsToSalvage = cardList.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            cardsToSalvage = cardsToSalvage.Select(s => s.Trim()).ToArray();

            // Number of threads to run. The more threads running the more prone to error this becomes
            int.TryParse(grindThreadsTextBox.Text, out int threads);
            if (threads <= 0) threads = 1;
            if (threads > 25) threads = 25;

            this.stopProcess = false;
            this.workerThread = new Thread(() =>
            {
                Parallel.ForEach(gp.SelectedUsers, new ParallelOptions { MaxDegreeOfParallelism = threads }, kongInfo =>
                {
                    try
                    {
                        // Salvage commons/rares first
                        KongViewModel kongVm = BotManager.SalvageL1CommonsAndRares(this, kongInfo);

                        // Call Init to get the player's inventory
                        kongVm = BotManager.Init(kongInfo);
                        ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                        string kongName = kongVm.KongName;

                        // Store player cards
                        var playerCards = kongVm.PlayerCards;

                        if (kongVm.Result != "False")
                        {
                            int salvage = kongVm.UserData.Salvage;
                            int maxSalvage = kongVm.UserData.MaxSalvage;

                            // Don't salvage if capped
                            if (maxSalvage - salvage < 40)
                            {
                                adminOutputTextBox.AppendText(kongName + " - SP is full\r\n");
                            }
                            else
                            {
                                // speed up card search by searching on names to see if the card exists
                                // (playerCards.Keys.Name is a slow lookup)
                                List<string> playerCardNames = playerCards.Keys.Select(x => x.Name).ToList();

                                foreach (string cardName in cardsToSalvage)
                                {
                                    if (!playerCardNames.Contains(cardName) &&
                                        !playerCardNames.Contains(cardName + "-1") &&
                                        !playerCardNames.Contains(cardName + "-2") &&
                                        !playerCardNames.Contains(cardName + "-3") &&
                                        !playerCardNames.Contains(cardName + "-4") &&
                                        !playerCardNames.Contains(cardName + "-5"))
                                    {
                                        continue;
                                    }

                                    // This is done in case there's multiple copies of the card at different levels
                                    List<Card> foundCards = playerCards.Keys
                                            .Where(x => x.Name == cardName || x.Name == cardName + "-1" ||
                                                        x.Name == cardName + "-2" || x.Name == cardName + "-3" ||
                                                        x.Name == cardName + "-4" || x.Name == cardName + "-5")
                                            .ToList();

                                    foreach (var c in foundCards)
                                    {
                                        int count = playerCards[c];

                                        // Remove up to 5 of each base epic from playerCards
                                        if (grinderKeepFiveBaseEpicsCheckBox.Checked)
                                        {
                                            if (CONSTANTS.BaseEpics.Contains(c.Name.Replace("-1", ""))) count -= 5;
                                            if (count <= 0) continue;
                                        }
                                        if (grinderKeepSomeBaseLegendsCheckBox.Checked)
                                        {
                                            if (CONSTANTS.BaseLegends.Contains(c.Name.Replace("-1", ""))) count -= 10;
                                            if (count <= 0) continue;
                                        }

                                        Console.WriteLine(kongName + " - salvaging " + count + " " + c.Name);

                                        for (int i = 0; i < count; i++)
                                        {
                                            kongVm = BotManager.SalvageCard(kongInfo, c.CardId, salvageLockedCards: true);

                                            // Was this a success?
                                            if (kongVm.Result != "False")
                                                outputTextBox.AppendText(kongName + " - Salvaged " + c.Name + "\r\n");
                                            else
                                                outputTextBox.AppendText(kongName + " - Failed salvaging " + c.Name + ": " + kongVm.ResultMessage + "\r\n");
                                        }
                                    }

                                    // Get new salvage
                                    kongVm = BotManager.GetUserAccount(kongInfo);
                                    salvage = kongVm.UserData.Salvage;

                                    // User is near salvage cap and we can end early
                                    if (maxSalvage - salvage <= 150)
                                    {
                                        adminOutputTextBox.AppendText(kongName + " SP near max\r\n");
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            adminOutputTextBox.AppendText(kongName + " - API error" + kongVm.ResultMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        Helper.OutputWindowMessage(this, MethodBase.GetCurrentMethod().Name + ": Error - \r\n" + ex);
                    }
                    finally
                    {
                        // Track progress
                        grinderProgressBar.PerformStep();
                        if (grinderProgressBar.Value >= grinderProgressBar.Maximum)
                        {
                            adminOutputTextBox.AppendText("Done\r\n");
                            grinderProgressBar.Value = 0;
                        }
                    }
                });
            });
            this.workerThread.Start();
        }

        /// <summary>
        /// On each account, attempt to salvage safe cards. This string is found in app settings
        /// </summary>
        private void grindSalvageSafeCardsButton_Click(object sender, EventArgs e)
        {
            // SP selected cards    
            grindSalvageCardButton_Click(sender, e);
            //string.Join(",", CONSTANTS.SAFE_CARDS_TO_SALVAGE);

            grindCardNameTextBox.Clear();
        }

        /// <summary>
        /// Claim rewards on these accounts
        /// </summary>
        private void grinderClaimReward_click(object sender, EventArgs e)
        {
            KongViewModel kongVm2 = new KongViewModel();

            // Setup the form
            int batchJobNumber = ++grinderProgressBarJobNumber;
            GrindParameters gp = SetupGrindParams();
            if (gp.SelectedUsers.Count == 0)
            {
                adminOutputTextBox.Text = "Select users in the left users dropdown (defined by /__users.txt) to use this action";
                return;
            }

            // Which events are active
            bool warRewardsActive = false;
            bool conquestRewardsActive = false;
            bool brawlRewardsActive = false;
            bool raidRewardsActive = false;

            // Call init on the first user to see which event is up, and to get the raid id
            string raidId = "0";
            try
            {
                kongVm2 = BotManager.Init(gp.SelectedUsers[0]);
                if (kongVm2.Result != "False")
                {
                    warRewardsActive = kongVm2.WarRewardsActive;
                    brawlRewardsActive = kongVm2.BrawlRewardsActive;
                    conquestRewardsActive = kongVm2.ConquestRewardsActive;
                    raidRewardsActive = kongVm2.RaidRewardsActive;

                    if (kongVm2?.RaidData?.Id > 0) raidId = kongVm2?.RaidData?.Id.ToString();
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, MethodBase.GetCurrentMethod().Name + ": Error - \r\n" + ex);
                return;
            }

            this.stopProcess = false;
            this.workerThread = new Thread(() =>
            {
                Parallel.ForEach(gp.SelectedUsers, new ParallelOptions { MaxDegreeOfParallelism = 10 }, kongInfo =>
                {
                    try
                    {
                        KongViewModel kongVm = new KongViewModel();
                        string result = "";

                        // Attempt to claim raid reward
                        if (raidRewardsActive)
                        {
                            kongVm = BotManager.ClaimRaidReward(this, kongInfo, raidId);
                            if (kongVm.Result != "False") result = kongVm.KongName + ": Claimed Raid rewards\r\n";
                            else result = kongVm.KongName + ": Failed - " + kongVm.ResultMessage.Replace("\r\n", "") + "\r\n";
                            adminOutputTextBox.AppendText(result);
                        }
                        // Attempt to claim Brawl reward
                        if (brawlRewardsActive)
                        {
                            kongVm = BotManager.ClaimBrawlReward(this, kongInfo);
                            if (kongVm.Result != "False") result = kongVm.KongName + ": Claimed Brawl rewards\r\n";
                            else result = kongVm.KongName + ": Failed - " + kongVm.ResultMessage.Replace("\r\n", "") + "\r\n";
                            adminOutputTextBox.AppendText(result);
                        }
                        // Attempt to claim CQ reward
                        if (conquestRewardsActive)
                        {
                            kongVm = BotManager.ClaimConquestReward(this, kongInfo);
                            if (kongVm.Result != "False") result = kongVm.KongName + ": Claimed CQ rewards\r\n";
                            else result = kongVm.KongName + ": Failed - " + kongVm.ResultMessage.Replace("\r\n", "") + "\r\n";
                            adminOutputTextBox.AppendText(result);
                        }
                        // Attempt to claim War reward
                        if (warRewardsActive)
                        {
                            kongVm = BotManager.ClaimFactionWarReward(this, kongInfo);
                            if (kongVm.Result != "False") result = kongVm.KongName + ": Claimed War rewards\r\n";
                            else result = kongVm.KongName + ": Failed - " + kongVm.ResultMessage.Replace("\r\n", "") + "\r\n";
                            adminOutputTextBox.AppendText(result);
                        }

                    }
                    catch (Exception ex)
                    {
                        Helper.OutputWindowMessage(this, MethodBase.GetCurrentMethod().Name + ": Error - \r\n" + ex);
                    }
                    finally
                    {
                        // Track progress
                        grinderProgressBar.PerformStep();
                        if (grinderProgressBar.Value >= grinderProgressBar.Maximum)
                        {
                            adminOutputTextBox.AppendText("Done\r\n");
                            grinderProgressBar.Value = 0;
                        }
                    }
                });
            });
            this.workerThread.Start();
        }

        /// <summary>
        /// Spend epic and legendary shards on these accounts
        /// </summary>
        private void grinderUseShardsButton_Click(object sender, EventArgs e)
        {
            // Setup the form
            int batchJobNumber = ++grinderProgressBarJobNumber;
            GrindParameters gp = SetupGrindParams();
            if (gp.SelectedUsers.Count == 0)
            {
                adminOutputTextBox.Text = "Select users in the left users dropdown (defined by /__users.txt) to use this action";
                return;
            }

            // Number of threads to run. The more threads running the more prone to error this becomes
            int.TryParse(grindThreadsTextBox.Text, out int threads);
            if (threads <= 0) threads = 1;
            if (threads > 25) threads = 25;

            this.stopProcess = false;
            this.workerThread = new Thread(() =>
            {
                Parallel.ForEach(gp.SelectedUsers, new ParallelOptions { MaxDegreeOfParallelism = threads }, kongInfo =>
                {
                    try
                    {
                        // Turn in shards on this account
                        KongViewModel kongVm = BotManager.ConsumeShards(this, kongInfo);

                        // Api or error output
                        if (kongVm.Result == "False")
                        {
                            adminOutputTextBox.AppendText(kongVm.KongName + " - " + kongVm.ResultMessage);
                        }
                        else
                        {
                            adminOutputTextBox.AppendText(kongVm.KongName + " - " + kongVm.PtuoMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        Helper.OutputWindowMessage(this, "grinderUseShardsButton_Click(): Error - \r\n" + ex);
                    }
                    finally
                    {
                        // Track progress
                        grinderProgressBar.PerformStep();
                        if (grinderProgressBar.Value >= grinderProgressBar.Maximum)
                        {
                            adminOutputTextBox.AppendText("Done\r\n");
                            grinderProgressBar.Value = 0;
                        }
                    }
                });
            });
            this.workerThread.Start();
        }

        /// <summary>
        /// Redeems Ascension altars (things that upgrade an Epic commander) for an older player that still has them
        /// </summary>
        private void grinderAscensionButton_Click(object sender, EventArgs e)
        {
            // Setup the form
            int batchJobNumber = ++grinderProgressBarJobNumber;
            GrindParameters gp = SetupGrindParams();
            if (gp.SelectedUsers.Count == 0)
            {
                adminOutputTextBox.Text = "Select users in the left users dropdown (defined by /__users.txt) to use this action";
                return;
            }

            // Number of threads to run. The more threads running the more prone to error this becomes
            int.TryParse(grindThreadsTextBox.Text, out int threads);
            if (threads <= 0) threads = 1;
            if (threads > 25) threads = 25;

            this.stopProcess = false;
            this.workerThread = new Thread(() =>
            {
                Parallel.ForEach(gp.SelectedUsers, new ParallelOptions { MaxDegreeOfParallelism = threads }, kongInfo =>
                {
                    try
                    {
                        // Turn in shards on this account
                        KongViewModel kongVm = BotManager.Init(kongInfo, getCardsFromInit: true);

                        // Api or error output
                        if (kongVm.Result != "False")
                        {
                            adminOutputTextBox.AppendText(kongVm.KongName + " - " + kongVm.ResultMessage);

                            // Ascension Altar
                            if (kongVm.PlayerCards.Keys.Where(x => x.CardId == 49970).FirstOrDefault() != null)
                            {
                                // - Upgrade Epic commanders if needed
                                List<int> keys = kongVm.PlayerCards.Keys.Select(x => x.CardId).ToList();

                                // Octane
                                if (kongVm.PlayerCards.Keys.Where(x => x.CardId == 1003).FirstOrDefault() != null)
                                    BotManager.UpgradeCard(kongInfo, 1003);
                                if (kongVm.PlayerCards.Keys.Where(x => x.CardId == 1004).FirstOrDefault() != null)
                                    BotManager.UpgradeCard(kongInfo, 1004);
                                if (kongVm.PlayerCards.Keys.Where(x => x.CardId == 1005).FirstOrDefault() != null)
                                    BotManager.UpgradeCard(kongInfo, 1005);
                                if (kongVm.PlayerCards.Keys.Where(x => x.CardId == 1006).FirstOrDefault() != null)
                                    BotManager.UpgradeCard(kongInfo, 1006);
                                if (kongVm.PlayerCards.Keys.Where(x => x.CardId == 1104).FirstOrDefault() != null)
                                    BotManager.UpgradeCard(kongInfo, 1104);
                                // 1111: Level 6

                                // Yurich
                                if (kongVm.PlayerCards.Keys.Where(x => x.CardId == 1053).FirstOrDefault() != null)
                                    BotManager.UpgradeCard(kongInfo, 1053);
                                if (kongVm.PlayerCards.Keys.Where(x => x.CardId == 1054).FirstOrDefault() != null)
                                    BotManager.UpgradeCard(kongInfo, 1054);
                                if (kongVm.PlayerCards.Keys.Where(x => x.CardId == 1055).FirstOrDefault() != null)
                                    BotManager.UpgradeCard(kongInfo, 1055);
                                if (kongVm.PlayerCards.Keys.Where(x => x.CardId == 1056).FirstOrDefault() != null)
                                    BotManager.UpgradeCard(kongInfo, 1056);
                                if (kongVm.PlayerCards.Keys.Where(x => x.CardId == 1057).FirstOrDefault() != null)
                                    BotManager.UpgradeCard(kongInfo, 1057);
                                // 1058: Level 6

                                // Brood Mother
                                if (kongVm.PlayerCards.Keys.Where(x => x.CardId == 1035).FirstOrDefault() != null)
                                    BotManager.UpgradeCard(kongInfo, 1035);
                                if (kongVm.PlayerCards.Keys.Where(x => x.CardId == 1036).FirstOrDefault() != null)
                                    BotManager.UpgradeCard(kongInfo, 1036);
                                if (kongVm.PlayerCards.Keys.Where(x => x.CardId == 1037).FirstOrDefault() != null)
                                    BotManager.UpgradeCard(kongInfo, 1037);
                                if (kongVm.PlayerCards.Keys.Where(x => x.CardId == 1038).FirstOrDefault() != null)
                                    BotManager.UpgradeCard(kongInfo, 1038);
                                if (kongVm.PlayerCards.Keys.Where(x => x.CardId == 1039).FirstOrDefault() != null)
                                    BotManager.UpgradeCard(kongInfo, 1039);
                                // 1040: Level 6

                                // Vyander
                                if (kongVm.PlayerCards.Keys.Where(x => x.CardId == 1041).FirstOrDefault() != null)
                                    BotManager.UpgradeCard(kongInfo, 1041);
                                if (kongVm.PlayerCards.Keys.Where(x => x.CardId == 1042).FirstOrDefault() != null)
                                    BotManager.UpgradeCard(kongInfo, 1042);
                                if (kongVm.PlayerCards.Keys.Where(x => x.CardId == 1043).FirstOrDefault() != null)
                                    BotManager.UpgradeCard(kongInfo, 1043);
                                if (kongVm.PlayerCards.Keys.Where(x => x.CardId == 1044).FirstOrDefault() != null)
                                    BotManager.UpgradeCard(kongInfo, 1044);
                                if (kongVm.PlayerCards.Keys.Where(x => x.CardId == 1045).FirstOrDefault() != null)
                                    BotManager.UpgradeCard(kongInfo, 1045);
                                // 1046: Level 6

                                // Empress
                                if (kongVm.PlayerCards.Keys.Where(x => x.CardId == 1047).FirstOrDefault() != null)
                                    BotManager.UpgradeCard(kongInfo, 1047);
                                if (kongVm.PlayerCards.Keys.Where(x => x.CardId == 1048).FirstOrDefault() != null)
                                    BotManager.UpgradeCard(kongInfo, 1048);
                                if (kongVm.PlayerCards.Keys.Where(x => x.CardId == 1049).FirstOrDefault() != null)
                                    BotManager.UpgradeCard(kongInfo, 1049);
                                if (kongVm.PlayerCards.Keys.Where(x => x.CardId == 1050).FirstOrDefault() != null)
                                    BotManager.UpgradeCard(kongInfo, 1050);
                                if (kongVm.PlayerCards.Keys.Where(x => x.CardId == 1051).FirstOrDefault() != null)
                                    BotManager.UpgradeCard(kongInfo, 1051);
                                // 1052: Level 6

                                // Octane Legend
                                kongVm = BotManager.FuseCard(this, kongInfo, 25583);
                                //if (kongVm.ResultMessage != null) adminOutputTextBox.AppendText(kongVm.KongName + " - " + kongVm.ResultMessage);
                                if (kongVm.ResultMessage != "false") adminOutputTextBox.AppendText(kongVm.KongName + " - Ascended Octane\r\n");

                                // Yurich Legend
                                kongVm = BotManager.FuseCard(this, kongInfo, 25595);
                                //if (kongVm.ResultMessage != null) adminOutputTextBox.AppendText(kongVm.KongName + " - " + kongVm.ResultMessage);
                                if (kongVm.ResultMessage != "false") adminOutputTextBox.AppendText(kongVm.KongName + " - Ascended Yurich\r\n");

                                // Brood Mother Legend
                                kongVm = BotManager.FuseCard(this, kongInfo, 25607);
                                //if (kongVm.ResultMessage != null) adminOutputTextBox.AppendText(kongVm.KongName + " - " + kongVm.ResultMessage);
                                if (kongVm.ResultMessage != "false") adminOutputTextBox.AppendText(kongVm.KongName + " - Ascended Brood Mother\r\n");

                                // Vyander Legend
                                kongVm = BotManager.FuseCard(this, kongInfo, 25619);
                                //if (kongVm.ResultMessage != null) adminOutputTextBox.AppendText(kongVm.KongName + " - " + kongVm.ResultMessage);
                                if (kongVm.ResultMessage != "false") adminOutputTextBox.AppendText(kongVm.KongName + " - Ascended Vyander\r\n");

                                // Empress Legend
                                kongVm = BotManager.FuseCard(this, kongInfo, 25631);
                                //if (kongVm.ResultMessage != null) adminOutputTextBox.AppendText(kongVm.KongName + " - " + kongVm.ResultMessage);
                                if (kongVm.ResultMessage != "false") adminOutputTextBox.AppendText(kongVm.KongName + " - Ascended Empress\r\n");
                            }
                            else
                            {
                                adminOutputTextBox.AppendText(kongVm.KongName + " - has no Ascension Altars\r\n");

                            }
                        }
                        else
                        {
                            adminOutputTextBox.AppendText(kongVm.KongName + " - " + kongVm.PtuoMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        Helper.OutputWindowMessage(this, "grinderUseShardsButton_Click(): Error - \r\n" + ex);
                    }
                    finally
                    {
                        // Track progress
                        grinderProgressBar.PerformStep();
                        if (grinderProgressBar.Value >= grinderProgressBar.Maximum)
                        {
                            adminOutputTextBox.AppendText("Done\r\n");
                            grinderProgressBar.Value = 0;
                        }
                    }
                });
            });
            this.workerThread.Start();
        }

        // ---- Grinder helpers ----- //

        /// <summary>
        /// Common class for params, and resets the form
        /// </summary>
        private GrindParameters SetupGrindParams( string gameMode = "surge")
        {
            GrindParameters gp = new GrindParameters();

            // Which users?
            gp.SelectedUsers = adminPlayerListBox.SelectedItems.Cast<string>().ToList();

            // Reset progress bar, output window
            adminOutputTextBox.Text = "";
            grinderProgressBar.Maximum = gp.SelectedUsers.Count;
            grinderProgressBar.Step = 1;
            grinderProgressBar.Value = 0;

            // PvP + Mission params
            gp.PvpAllAttacks = grinderPvpSpendAllCheckBox.Checked;
            if (int.TryParse(grinderStaminaTextBox.Text, out int x)) gp.PvpStaminaToSpend = x;
            gp.DoGuildQuests = grinderMissionAttackGQsCheckBox.Checked;
            gp.DoSideMissions = true;
            gp.DoTempMissions = true;
            if (int.TryParse(grinderMissionEnergyTextBox.Text, out x)) gp.MissionEnergyThreshold = x;

            // Event
            gp.GameMode = gameMode; // surge, brawl, gw

            if (int.TryParse(grindEventEnergyThresholdTextBox.Text, out x)) gp.EventEnergyThreshold = x;
            if (int.TryParse(grinderIterationsTextBox.Text, out x)) gp.Iterations = x;

            gp.PreventBlackBox = grinderDontBlackBoxCheckBox.Checked;
            gp.AttackFast = grindAttackFasterCheckBox.Checked;
            gp.ExtraLockedCards = gp.AttackFast ? 0 : 1; // Livesim faster if attackfast is set

            // Cards to build
            gp.CardToBuild = grindCardNameTextBox.Text;

            return gp;
        }

        /// <summary>
        /// Common parameters for grind operations
        /// </summary>
        private class GrindParameters
        {
            // Which users
            public List<string> SelectedUsers { get; set; }

            // For mission/pvp grind
            public bool PvpAllAttacks { get; set; }
            public int PvpStaminaToSpend { get; set; }
            public bool DoGuildQuests { get; set; }
            public bool DoSideMissions { get; set; }
            public bool DoTempMissions { get; set; }
            public bool SkipMissionsIfMesmerize { get; set; }
            public int MissionEnergyThreshold { get; set; }

            // For event grind
            public string GameMode { get; set; }
            public bool PreventBlackBox { get; set; }
            public bool AttackFast { get; set; }
            public int EventEnergyThreshold { get; set; }
            public int Iterations { get; set; }
            public int ExtraLockedCards { get; set; }

            // For card building
            public string CardToBuild { get; set; }

            public GrindParameters()
            {
                SelectedUsers = new List<string>();
                GameMode = "surge";
                Iterations = 500;
            }
        }

        private class EventRow
        {
            public string Name { get; set; }
            public string Guild { get; set; }
            public int Energy { get; set; }
            public int Score { get; set; }
            public int CurrentScore { get; set; }

            public double AttackWinPercent { get; set; }
            public int AttackWins { get; set; }
            public int AttackLosses { get; set; }

            public string AttackDeck { get; set; }
            public string DefenseDeck { get; set; }
            public string Status { get; set; }

            // Brawl stuff
            public double PointsPerWin { get; set; }

            // War stuff
            public double DefendWinPercent { get; set; }
            public int DefendWins { get; set; }
            public int DefendLosses { get; set; }

        }

        #endregion

        #region Admin - Report

        /// <summary>
        /// Get stats of selected users in a tabbed csv format
        /// </summary>
        private void reportCardButton_Click(object sender, EventArgs e)
        {
            // Setup the form
            int batchJobNumber = ++grinderProgressBarJobNumber;
            GrindParameters gp = SetupGrindParams();
            if (gp.SelectedUsers.Count == 0)
            {
                adminOutputTextBox.Text = "Select users in the left users dropdown (defined by /__users.txt) to use this action";
                return;
            }

            // End result
            ConcurrentDictionary<string, string> userReportCard = new ConcurrentDictionary<string, string>();
            StringBuilder sb = new StringBuilder();

            // Toggle what gets displayed
            bool displayCurrency = reportCurrencyCheckBox.Checked;
            bool displayEnergy = reportEnergyCheckBox.Checked;
            bool displayGuild = reportGuildCheckBox.Checked;
            bool displayDecks = reportDecksCheckBox.Checked;
            bool displayQuests = reportQuestsCheckBox.Checked;
            bool displayCards = reportCardsCheckBox.Checked;
            bool displayRestoreCards = reportInventoryRestoreCardsCheckBox.Checked;
            bool displaySheetPossibleCards = reportPossibleCardsCheckBox.Checked;
            bool displayEvent = reportEventCheckBox.Checked;
            //bool displayGuildInfo = = reportCurrencyCheckBox.Checked; - online time? pvp rank / BR?
            bool showEnergyPercentages = showEnergyPercentagesCheckBox.Checked;

            bool brawlActive = false;
            bool cqActive = false;
            bool raidActive = false;
            bool warActive = false;

            // Count how many displayed cards there are
            string[] cards = reportCardsTextBox.Text.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);


            this.stopProcess = false;
            this.workerThread = new Thread(() =>
            {
                adminOutputTextBox.AppendText("Running..\r\n");

                try
                {
                    KongViewModel kongVm2 = BotManager.Init(gp.SelectedUsers[0]);
                    if (kongVm2.Result != "False")
                    {
                        brawlActive = kongVm2.BrawlActive;
                        cqActive = kongVm2.ConquestActive;
                        raidActive = kongVm2.RaidActive;
                        warActive = kongVm2.WarActive;
                    }
                }
                catch (Exception ex)
                {
                    Helper.OutputWindowMessage(this, MethodBase.GetCurrentMethod().Name + ": Error - \r\n" + ex);
                    return;
                }

                // -----------------
                // Header row
                // -----------------
                sb.Append("__Name\t");

                if (displayGuild)
                {
                    sb.Append("Guild\t");
                }

                // Currency: Gold, WB, SP, Inventory
                if (displayCurrency)
                {
                    sb.Append("WB\t");
                    sb.Append("Gold (K)\t");

                    sb.Append("SP\t");
                    if (!showEnergyPercentages) sb.Append("\t");
                    sb.Append("Inventory\t");
                    if (!showEnergyPercentages) sb.Append("\t");
                }
                // Energy: Mission / Stamina
                if (displayEnergy)
                {
                    sb.Append("Mission\t");
                    if (!showEnergyPercentages) sb.Append("\t");
                    sb.Append("Stamina\t");
                    if (!showEnergyPercentages) sb.Append("\t");
                }
                // Quest progress - except GuildQuests/PvP Quests
                if (displayQuests)
                {
                    sb.Append("Quests\t");
                }
                // Event
                if (displayEvent)
                {
                    if (brawlActive)
                    {
                        sb.Append("Energy\tRank\tPoints\tW\tL\tWin%\tPPW\tWS\t");
                    }
                    else if (cqActive)
                    {
                        sb.Append("Score\tEnergy\t");
                    }
                    else if (raidActive)
                    {
                        sb.Append("Energy\tDamage\t");
                    }
                    else if (warActive)
                    {
                        sb.Append("** Use Grind - Event Stats - War/Brawl for the scoreboard..\t");
                    }
                    else
                    {
                        sb.Append("No event detected\t");
                    }
                    // Scoreboard, did user claim rewards (remember this isn't in eventActive)
                }
                // Decks - attack/defense
                if (displayDecks)
                {
                    sb.Append("Attack Deck\t");
                    sb.Append("Defense Deck\t");
                }
                // Owned cards the player has
                if (displayCards)
                {
                    foreach (var card in cards)
                    {
                        sb.Append(card + "\t");
                    }
                }
                // API Restore cards the player has (only already made cards)
                if (displayRestoreCards)
                {
                    foreach (var card in cards)
                    {
                        sb.Append(card + " (Restore)\t");
                    }
                }
                // Inventory possible cards the player has (ogre inventory autocalculates it)
                if (displaySheetPossibleCards)
                {
                    foreach (var card in cards)
                    {
                        sb.Append(card + " (Buildable)\t");
                    }
                }

                // Newline
                sb.Append("\r\n");

                userReportCard.TryAdd("___", sb.ToString());

                Parallel.ForEach(gp.SelectedUsers, new ParallelOptions { MaxDegreeOfParallelism = 10 }, kongInfo =>
                {
                    try
                    {
                        KongViewModel kongVm = BotManager.Init(kongInfo);

                        // TODO: Last online time - figure out who to call first and mark everyone else

                        if (kongVm.Result != "False")
                        {
                            string name = kongVm.KongName;
                            sb = new StringBuilder();
                            sb.Append(kongVm.KongName + "\t");

                            if (displayGuild)
                            {
                                sb.Append(kongVm.Faction.Name + "\t");
                            }

                            // Currency
                            if (displayCurrency)
                            {
                                sb.Append(kongVm.UserData.Warbonds + "\t");
                                sb.Append(kongVm.UserData.Gold / 1000 + "\t");
                                if (showEnergyPercentages)
                                {
                                    double salvagePercent = kongVm.UserData.MaxSalvage > 0 ? ((double)kongVm.UserData.Salvage / kongVm.UserData.MaxSalvage * 100) : 0;
                                    double inventoryPercent = kongVm.UserData.MaxInventory > 0 ? ((double)kongVm.UserData.Inventory / kongVm.UserData.MaxInventory * 100) : 0;
                                    sb.Append(Math.Round(salvagePercent, 0) + "%\t");
                                    sb.Append(Math.Round(inventoryPercent, 0) + "%\t");
                                }
                                else
                                {
                                    sb.Append(kongVm.UserData.Salvage + "\t");
                                    sb.Append(kongVm.UserData.MaxSalvage + "\t");
                                    sb.Append(kongVm.UserData.Inventory + "\t");
                                    sb.Append(kongVm.UserData.MaxInventory + "\t");
                                }
                            }

                            // Mission Energy / stam
                            if (displayEnergy)
                            {
                                if (!showEnergyPercentages)
                                {
                                    sb.Append(kongVm.UserData.Energy + "\t");
                                    sb.Append(kongVm.UserData.MaxEnergy + "\t");
                                    sb.Append(kongVm.UserData.Stamina + "\t");
                                    sb.Append(kongVm.UserData.MaxStamina + "\t");
                                }
                                else
                                {
                                    double energyPercent = kongVm.UserData.MaxEnergy > 0 ? ((double)kongVm.UserData.Energy / kongVm.UserData.MaxEnergy * 100) : 0;
                                    double staminaPercent = kongVm.UserData.MaxStamina > 0 ? ((double)kongVm.UserData.Stamina / kongVm.UserData.MaxStamina * 100) : 0;
                                    sb.Append(Math.Round(energyPercent, 0) + "%\t");
                                    sb.Append(Math.Round(staminaPercent, 0) + "%\t");
                                }
                            }

                            // What quests does the player have
                            if (displayQuests)
                            {
                                kongVm.Quests.Reverse(); //List out more recent stuff first?
                                foreach (var quest in kongVm.Quests)
                                {
                                    // Usually Guild Quests
                                    if (quest.Id <= 0) continue;
                                    // Pvp quests usually, or a super-quest
                                    if (quest.MissionId <= 0) continue;

                                    sb.Append(quest.Name);
                                    sb.Append(": ");
                                    sb.Append(quest.Progress);
                                    //sb.Append("/");
                                    //sb.Append(quest.MaxProgress);
                                    sb.Append(", ");
                                }
                                sb.Append("\t");
                            }

                            // Event data
                            if (displayEvent)
                            {
                                if (kongVm.BrawlActive)
                                {
                                    double winPercent = (kongVm.BrawlData.Wins + kongVm.BrawlData.Losses != 0) ?
                                                        (double)kongVm.BrawlData.Wins / (double)(kongVm.BrawlData.Wins + kongVm.BrawlData.Losses) * 100 : 0;

                                    sb.Append(kongVm.BrawlData.Energy + "\t");
                                    sb.Append(kongVm.BrawlData.CurrentRank + "\t");
                                    sb.Append(kongVm.BrawlData.Points + "\t");
                                    sb.Append(kongVm.BrawlData.Wins + "\t");
                                    sb.Append(kongVm.BrawlData.Losses + "\t");

                                    sb.Append(winPercent + "%\t");
                                    sb.Append(kongVm.BrawlData.PointsPerWin + "\t");
                                    sb.Append(kongVm.BrawlData.WinStreak + "\t");
                                }
                                else if (kongVm.ConquestActive)
                                {
                                    sb.Append(kongVm.ConquestData.Influence + "\t");
                                    sb.Append(kongVm.ConquestData.Energy + "\t");
                                }
                                else if (kongVm.RaidActive)
                                {
                                    sb.Append(kongVm.RaidData.Energy + "\t");
                                    // TODO: Get player score not through scoreboard?
                                    //sb.Append(kongVm.RaidData + "\t");
                                }
                                else if (kongVm.WarActive)
                                {
                                    sb.Append(kongVm.WarData.Energy + "\t");
                                }
                                else { sb.Append("0\t"); }
                            }

                            // Decks
                            if (displayDecks)
                            {
                                int.TryParse(kongVm.UserData.ActiveDeck, out int attackDeck);
                                int.TryParse(kongVm.UserData.DefenseDeck, out int defenseDeck);

                                if (attackDeck > 0) sb.Append(kongVm.UserDecks[attackDeck - 1].DeckToString() + "\t");
                                else sb.Append("\t");

                                if (defenseDeck > 0) sb.Append(kongVm.UserDecks[defenseDeck - 1].DeckToString() + "\t");
                                else sb.Append("\t");
                            }

                            // How many cards listed does the player have in their inventory
                            if (displayCards)
                            {
                                foreach (var card in cards)
                                {
                                    //Card pCard = kongVm.PlayerCards.Keys.FirstOrDefault(x => x.Name.ToLowerInvariant() == card.ToLowerInvariant());

                                    // Starts with name, for -1, -2, -3, -4, -5
                                    List<Card> pCards = kongVm.PlayerCards.Keys.Where(x => x.Name.ToLowerInvariant() == card.ToLowerInvariant()).ToList();
                                    pCards.AddRange(kongVm.PlayerCards.Keys.Where(x => x.Name.ToLowerInvariant() == card.ToLowerInvariant() + "-1").ToList());
                                    pCards.AddRange(kongVm.PlayerCards.Keys.Where(x => x.Name.ToLowerInvariant() == card.ToLowerInvariant() + "-2").ToList());
                                    pCards.AddRange(kongVm.PlayerCards.Keys.Where(x => x.Name.ToLowerInvariant() == card.ToLowerInvariant() + "-3").ToList());
                                    pCards.AddRange(kongVm.PlayerCards.Keys.Where(x => x.Name.ToLowerInvariant() == card.ToLowerInvariant() + "-4").ToList());
                                    pCards.AddRange(kongVm.PlayerCards.Keys.Where(x => x.Name.ToLowerInvariant() == card.ToLowerInvariant() + "-5").ToList());

                                    int count = 0;
                                    foreach (var c in pCards)
                                    {
                                        count += kongVm.PlayerCards[c];
                                    }
                                    sb.Append(count + "\t");
                                }
                            }
                            // How many cards listed does the player have in restore
                            if (displayRestoreCards)
                            {
                                foreach (var card in cards)
                                {
                                    //restore cards are level 1
                                    string restoreCardName = card + "-1";

                                    Card pCard = kongVm.RestoreCards.Keys.FirstOrDefault(x => x.Name.ToLowerInvariant() == restoreCardName.ToLowerInvariant());
                                    if (pCard != null)
                                    {
                                        int count = kongVm.RestoreCards[pCard];
                                        sb.Append(count + "\t");
                                    }
                                    else
                                        sb.Append("0\t");
                                }
                            }
                            // How many cards listed does the player have in the _possiblecards inventory file
                            if (displaySheetPossibleCards)
                            {
                                // Ptuo names should match kongname (there are a couple exceptions)
                                Player player = PlayerManager.Players
                                        .Where(x => x.KongName.Replace("_sheet", "").Replace("_noset", "").ToLowerInvariant() == kongVm.KongName.ToLowerInvariant())
                                        .FirstOrDefault();

                                foreach (var card in cards)
                                {
                                    if (player != null)
                                    {
                                        Card pCard = player.PossibleCards.Keys.FirstOrDefault(x => x.Name.ToLowerInvariant() == card.ToLowerInvariant());
                                        if (pCard != null)
                                        {
                                            int count = player.PossibleCards[pCard];
                                            sb.Append(count + "\t");
                                        }
                                        else
                                            sb.Append("0\t");
                                    }
                                    else
                                    {
                                        sb.Append("-\t");
                                    }
                                }
                            }

                            // Newline
                            sb.Append("\r\n");

                            userReportCard.TryAdd(name, sb.ToString());
                        }
                        else
                        {
                            userReportCard.TryAdd(kongVm.KongName, kongVm.KongName + "\t" + kongVm.ResultMessage.Replace("\r\n", "") + "\r\n");
                        }
                    }
                    catch (Exception ex)
                    {
                        Helper.OutputWindowMessage(this, MethodBase.GetCurrentMethod().Name + ": Error - \r\n" + ex);
                    }
                    finally
                    {
                        // Track progress
                        grinderProgressBar.PerformStep();
                        if (grinderProgressBar.Value >= grinderProgressBar.Maximum)
                        {
                            //adminOutputTextBox.AppendText("Done\r\n");
                            grinderProgressBar.Value = 0;
                        }
                    }
                });

                // Output the data
                adminOutputTextBox.Text = "";

                foreach (var userReport in userReportCard.OrderBy(x => x.Key))
                {
                    adminOutputTextBox.AppendText(userReport.Value);
                }
            });
            this.workerThread.Start();
        }

        /// <summary>
        /// Gets the War GuildBGE for the selected guild
        /// </summary>
        private void reportWarBgeButton_Click(object sender, EventArgs e)
        {
            // Setup the form
            int batchJobNumber = ++grinderProgressBarJobNumber;
            GrindParameters gp = SetupGrindParams();
            if (gp.SelectedUsers.Count == 0)
            {
                adminOutputTextBox.Text = "Select users in the left users dropdown (defined by /__users.txt) to use this action";
                return;
            }

            try
            {
                KongViewModel kongVm = BotManager.Init(gp.SelectedUsers[0]);

                if (kongVm.Result != "False" && kongVm.WarActive)
                {
                    adminOutputTextBox.Text = kongVm.WarData.AttackerFactionName + " - " + kongVm.WarData.AttackerBGE + "\r\n" +
                        kongVm.WarData.DefenderFactionName + " - " + kongVm.WarData.DefenderBGE;
                }
                else
                {
                    adminOutputTextBox.Text = "Api error";
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, MethodBase.GetCurrentMethod().Name + ": Error - \r\n" + ex);
            }
            finally
            {
                // Track progress
                grinderProgressBar.PerformStep();
                if (grinderProgressBar.Value >= grinderProgressBar.Maximum)
                {
                    //adminOutputTextBox.AppendText("Done\r\n");
                    grinderProgressBar.Value = 0;
                }
            }
        }

        /// <summary>
        /// Get the current event, and display stats for it
        /// </summary>
        private void reportWarBrawlStatsButton_Click(object sender, EventArgs e)
        {
            // Setup the form
            int batchJobNumber = ++grinderProgressBarJobNumber;
            GrindParameters gp = SetupGrindParams();
            if (gp.SelectedUsers.Count == 0)
            {
                adminOutputTextBox.Text = "Select users in the left users dropdown (defined by /__users.txt) to use this action";
                return;
            }

            // End result
            StringBuilder sb = new StringBuilder();
            KongViewModel initialKongVm = new KongViewModel();
            KongViewModel decksKongVm = new KongViewModel();
            List<EventRow> eventRows = new List<EventRow>();
            string firstKongInfo = gp.SelectedUsers[0];
            string headerRow = "";
            string headerRow2 = "";

            this.stopProcess = false;
            this.workerThread = new Thread(() =>
            {
                // ----------------------------------
                // Init to find the currently active event and create the first header row
                // ----------------------------------
                try
                {
                    initialKongVm = BotManager.Init(firstKongInfo);
                    if (initialKongVm.Result != "False")
                    {
                        sb.Append("__Name\t");
                        sb.Append("Guild\t");
                        sb.Append("Energy\t");
                        sb.Append("Score\t");

                        if (initialKongVm.WarActive)
                        {
                            sb.Append("WScore\t");
                            sb.Append("ATK %\t");
                            sb.Append("W\t");
                            sb.Append("L\t");
                            sb.Append("DEF %\t");
                            sb.Append("W\t");
                            sb.Append("L\t");
                        }
                        else if (initialKongVm.BrawlActive)
                        {
                            sb.Append("Wins\t");
                            sb.Append("Losses\t");
                            sb.Append("Points per win\t");
                        }
                        else if (initialKongVm.RaidActive)
                        {
                            //TODO: 
                        }
                        else if (initialKongVm.ConquestActive)
                        {
                            //TODO: 
                        }

                        sb.Append("ATK Deck\t");
                        sb.Append("DEF Deck\t");
                        sb.Append("Status\t");
                        sb.Append("\r\n");
                        headerRow = sb.ToString();
                    }
                    else
                    {
                        headerRow = initialKongVm.KongName + "\t" + initialKongVm.ResultMessage;
                    }
                }
                catch (Exception ex)
                {
                    Helper.OutputWindowMessage(this, MethodBase.GetCurrentMethod().Name + ": Error - \r\n" + ex);
                }

                // ----------------------------------
                // Depending on the event, cycle through one or many users for data
                // ----------------------------------
                try
                {
                    if (initialKongVm.WarActive || initialKongVm.BrawlActive || initialKongVm.ConquestActive || initialKongVm.RaidActive)
                    {
                        adminOutputTextBox.Text = "Event found. Processing..";
                    }

                    // -----------------
                    // War - only pull the first user
                    // -----------------
                    if (initialKongVm.WarActive)
                    {
                        string myGuild = initialKongVm.Faction.Name;

                        string attackerGuild = initialKongVm.WarData.AttackerFactionName;
                        string defenderGuild = initialKongVm.WarData.DefenderFactionName;

                        // How many wins and energy did this guild get
                        int totalAttackerWins = 0;
                        int totalDefenderWins = 0;
                        int totalAttackerDefenseWins = 0;
                        int totalAttackerDefenseLosses = 0;
                        int totalDefenderDefenseWins = 0;
                        int totalDefenderDefenseLosses = 0;
                        int totalAttackerEnergy = 0;
                        int totalDefenderEnergy = 0;

                        // Averages
                        double attackerPointsPerEnergy = 0.0;
                        double defenderPointsPerEnergy = 0.0;
                        double myGuildAttackWinPercent = 0.0;
                        double myGuildDefenseWinPercent = 0.0;

                        // What's this guilds total score
                        int attackerScore = initialKongVm.WarData.AttackerScore;
                        int defenderScore = initialKongVm.WarData.DefenderScore;


                        // Collect Scoreboard data
                        foreach (var attacker in initialKongVm.WarData.AttackerLeaderboard)
                        {
                            double attackWinPercent = 0;
                            if (attacker.Wins == 0) attackWinPercent = 0;
                            else if (attacker.Losses == 0) attackWinPercent = 100;
                            else attackWinPercent = Math.Round((double)attacker.Wins / (attacker.Wins + attacker.Losses) * 100, 0);

                            double defendWinPercent = 0;
                            if (attacker.DefenseWins == 0) defendWinPercent = 0;
                            else if (attacker.DefenseLosses == 0) defendWinPercent = 100;
                            else defendWinPercent = Math.Round((double)attacker.DefenseWins / (attacker.DefenseWins + attacker.DefenseLosses) * 100, 0);

                            EventRow row = new EventRow
                            {
                                Name = attacker.Name,
                                Guild = attackerGuild,
                                Energy = attacker.Energy,
                                Score = attacker.TotalScore,
                                CurrentScore = attacker.Score,
                                AttackWinPercent = attackWinPercent,
                                AttackWins = attacker.Wins,
                                AttackLosses = attacker.Losses,
                                DefendWinPercent = defendWinPercent,
                                DefendWins = attacker.DefenseWins,
                                DefendLosses = attacker.DefenseLosses,
                            };

                            // Count total wins/energy
                            totalAttackerWins += attacker.Wins;
                            totalAttackerDefenseWins += attacker.DefenseWins;
                            totalAttackerDefenseLosses += attacker.DefenseLosses;
                            totalAttackerEnergy += attacker.Energy;

                            // If this is the player's guild, fetch decks
                            if (attackerGuild == myGuild)
                            {
                                decksKongVm = BotManager.GetDecksOfPlayer(firstKongInfo, attacker.UserId);
                                if (decksKongVm.PlayerInfo.Count > 0)
                                {
                                    // Try to pull this deck
                                    row.AttackDeck = decksKongVm.PlayerInfo[0].ActiveDeck.DeckToString();
                                    row.DefenseDeck = decksKongVm.PlayerInfo[0].DefenseDeck.DeckToString();
                                }
                            }

                            // Add this row if its your guild, or we want enemy stats
                            if (attackerGuild == myGuild || adminEnemyWarStatsCheckBox.Checked)
                            {
                                eventRows.Add(row);
                            }
                        }


                        foreach (var defender in initialKongVm.WarData.DefenderLeaderboard)
                        {
                            double attackWinPercent = 0;
                            if (defender.Wins == 0) attackWinPercent = 0;
                            else if (defender.Losses == 0) attackWinPercent = 100;
                            else attackWinPercent = Math.Round((double)defender.Wins / (defender.Wins + defender.Losses) * 100, 0);

                            double defendWinPercent = 0;
                            if (defender.DefenseWins == 0) defendWinPercent = 0;
                            else if (defender.DefenseLosses == 0) defendWinPercent = 100;
                            else defendWinPercent = Math.Round((double)defender.DefenseWins / (defender.DefenseWins + defender.DefenseLosses) * 100, 0);

                            EventRow row = new EventRow
                            {
                                Name = defender.Name,
                                Guild = defenderGuild,
                                Energy = defender.Energy,
                                Score = defender.TotalScore,
                                CurrentScore = defender.Score,
                                AttackWinPercent = attackWinPercent,
                                AttackWins = defender.Wins,
                                AttackLosses = defender.Losses,
                                DefendWinPercent = defendWinPercent,
                                DefendWins = defender.DefenseWins,
                                DefendLosses = defender.DefenseLosses,
                            };

                            // Count total wins/energy
                            totalDefenderWins += defender.Wins;
                            totalDefenderDefenseWins += defender.DefenseWins;
                            totalDefenderDefenseLosses += defender.DefenseLosses;
                            totalDefenderEnergy += defender.Energy;

                            // If this is the player's guild, fetch decks
                            if (defenderGuild == myGuild)
                            {
                                decksKongVm = BotManager.GetDecksOfPlayer(firstKongInfo, defender.UserId);
                                if (decksKongVm.PlayerInfo.Count > 0)
                                {
                                    // Try to pull this deck
                                    row.AttackDeck = decksKongVm.PlayerInfo[0].ActiveDeck.DeckToString();
                                    row.DefenseDeck = decksKongVm.PlayerInfo[0].DefenseDeck.DeckToString();
                                }
                            }

                            // Add this row if its your guild, or we want enemy stats
                            if (defenderGuild == myGuild || adminEnemyWarStatsCheckBox.Checked)
                            {
                                eventRows.Add(row);
                            }
                        }

                        // Header row 2/3, aggregated stats
                        // This may divide by 0 if 
                        attackerPointsPerEnergy = 1000 - totalAttackerEnergy != 0 ? Math.Round((double)(attackerScore / (1000 - totalAttackerEnergy)), 1) : 0;
                        defenderPointsPerEnergy = 1000 - totalDefenderEnergy != 0 ? Math.Round((double)(defenderScore / (1000 - totalDefenderEnergy)), 1) : 0;

                        if (attackerGuild == myGuild)
                        {
                            myGuildAttackWinPercent = 1000 - totalAttackerEnergy != 0 ?
                                                      (double)totalAttackerWins / (1000 - totalAttackerEnergy) * 100 : 0;

                            myGuildDefenseWinPercent = totalAttackerDefenseWins + totalAttackerDefenseLosses > 0 ?
                                                      Math.Round((double)totalAttackerDefenseWins / (totalAttackerDefenseWins + totalAttackerDefenseLosses) * 100, 2) : 0;
                        }
                        else
                        {
                            myGuildAttackWinPercent = 1000 - totalDefenderEnergy != 0 ?
                                                        (double)totalDefenderWins / (1000 - totalDefenderEnergy) * 100 : 0;
                            myGuildDefenseWinPercent = totalDefenderDefenseWins + totalDefenderDefenseLosses > 0 ?
                                                       Math.Round((double)totalDefenderDefenseWins / (totalDefenderDefenseWins + totalDefenderDefenseLosses) * 100, 2) : 0;
                        }


                        // Output

                        // Header row 2/3
                        sb = new StringBuilder();
                        sb.Append(attackerGuild + ":" + attackerScore + " - " + totalAttackerEnergy + "energy left (" + attackerPointsPerEnergy + " PPE)\r\n");
                        sb.Append(defenderGuild + ":" + defenderScore + " - " + totalDefenderEnergy + "energy left (" + defenderPointsPerEnergy + " PPE)");
                        sb.Append("\t\t\t\t\t" + myGuildAttackWinPercent + "%\t\t\t" + myGuildDefenseWinPercent + "%\r\n");
                        headerRow2 = sb.ToString();

                        adminOutputTextBox.Text = headerRow;
                        adminOutputTextBox.AppendText(headerRow2);


                        foreach (var row in eventRows.OrderBy(x => x.Guild).ThenByDescending(x => x.CurrentScore))
                        {
                            sb = new StringBuilder();
                            sb.Append(row.Name + "\t");
                            sb.Append(row.Guild + "\t");
                            sb.Append(row.Energy + "\t");
                            sb.Append(row.Score + "\t");
                            sb.Append(row.CurrentScore + "\t");
                            sb.Append(row.AttackWinPercent + "\t");
                            sb.Append(row.AttackWins + "\t");
                            sb.Append(row.AttackLosses + "\t");
                            sb.Append(row.DefendWinPercent + "\t");
                            sb.Append(row.DefendWins + "\t");
                            sb.Append(row.DefendLosses + "\t");
                            sb.Append(row.AttackDeck + "\t");
                            sb.Append(row.DefenseDeck + "\t");
                            sb.Append(row.Status + "\t");
                            sb.Append("\r\n");
                            adminOutputTextBox.AppendText(sb.ToString());
                        }
                    }

                    // -----------------
                    // Brawl - pull each user
                    // -----------------
                    if (initialKongVm.BrawlActive)
                    {
                        // TODO: Header

                        Parallel.ForEach(gp.SelectedUsers, new ParallelOptions { MaxDegreeOfParallelism = 5 }, kongInfo =>
                        {
                            try
                            {
                                KongViewModel kongVm = BotManager.Init(kongInfo);
                                if (kongVm.Result != "False")
                                {
                                    EventRow row = new EventRow
                                    {
                                        Name = kongVm.KongName,
                                        Guild = kongVm.Faction.Name,
                                        Energy = kongVm.BrawlData.Energy,
                                        Score = kongVm.BrawlData.Points,
                                        AttackWins = kongVm.BrawlData.Wins,
                                        AttackLosses = kongVm.BrawlData.Losses,
                                        PointsPerWin = kongVm.BrawlData.PointsPerWin
                                    };
                                    eventRows.Add(row);

                                    //TODO: Other Brawl info
                                }
                                else
                                {
                                    EventRow row = new EventRow
                                    {
                                        Name = kongVm.KongName,
                                        Status = kongVm.ResultMessage
                                    };
                                }
                            }
                            catch (Exception ex)
                            {
                                Helper.OutputWindowMessage(this, MethodBase.GetCurrentMethod().Name + ": Error - \r\n" + ex);
                            }
                        });

                        // Output
                        sb = new StringBuilder();
                        sb.Append("Name\tGuild\tEnergy\tScore\tW\tL\tPPW\r\n\r\n");

                        foreach (var row in eventRows)
                        {
                            sb.Append(row.Name + "\t");
                            sb.Append(row.Guild + "\t");
                            sb.Append(row.Energy + "\t");
                            sb.Append(row.Score + "\t");
                            sb.Append(row.AttackWins + "\t");
                            sb.Append(row.AttackLosses + "\t");
                            sb.Append(row.PointsPerWin + "\t");
                            sb.Append(row.Status + "\t");
                            sb.Append("\r\n");
                        }
                    }

                    // Conquest - do we just want influence from one user? And guild scores?
                    if (initialKongVm.ConquestActive)
                    {
                        // 2 scenarios - if puller file exists, get multiples. Otherwise, get first selected player

                        sb.Append("NOT YET IMPLEMENTED\r\n");
                    }

                    // Raid - pull each user (maybe.. if we want energy)
                    if (initialKongVm.RaidActive)
                    {
                        sb.Append("NOT YET IMPLEMENTED\r\n");
                    }
                }
                catch (Exception ex)
                {
                    Helper.OutputWindowMessage(this, MethodBase.GetCurrentMethod().Name + ": Error - \r\n" + ex);
                }
                finally
                {
                    // Track progress
                    //grinderProgressBar.PerformStep();
                    //if (grinderProgressBar.Value >= grinderProgressBar.Maximum)
                    //{
                    //adminOutputTextBox.AppendText("Done\r\n");
                    grinderProgressBar.Value = 0;
                    //}
                }
            });
            this.workerThread.Start();
        }

        /// <summary>
        /// Get the current CQ event scores
        /// </summary>
        private void reportCqZonesButton_Click(object sender, EventArgs e)
        {
            // Two scenarios - 

            // * Use the first account selected

            // * TODO: Use puller accounts


            // Setup the form
            int batchJobNumber = ++grinderProgressBarJobNumber;
            GrindParameters gp = SetupGrindParams();
            if (gp.SelectedUsers.Count == 0)
            {
                adminOutputTextBox.Text = "Select users in the left users dropdown (defined by /__users.txt) to use this action";
                return;
            }

            // End result
            StringBuilder sb = new StringBuilder();
            KongViewModel kongVm = new KongViewModel();
            string firstKongInfo = gp.SelectedUsers[0];
            List<CqZoneData> cqZoneDatas = new List<CqZoneData>(); //Empty list of Cq Zone data to start out with

            this.stopProcess = false;
            this.workerThread = new Thread(() =>
            {
                try
                {
                    kongVm = BotManager.GetConquestUpdate(firstKongInfo, cqZoneDatas);
                    if (kongVm.Result != "False")
                    {
                        // Print out zones 
                        foreach (var zone in kongVm.ConquestData.ConquestZones.OrderByDescending(x => x.Tier))
                        {
                            sb.Append(zone.Name).Append("\r\n");
                            foreach (var zoneRanking in zone.Rankings.OrderByDescending(x => x.Influence))
                            {
                                sb.Append(zoneRanking.Rank).Append("\t");
                                sb.Append(zoneRanking.Name).Append("\t");
                                sb.Append(zoneRanking.Influence).Append("\r\n");
                            }
                            sb.Append("\r\n\r\n");
                        }

                        adminOutputTextBox.Text = sb.ToString();
                    }
                    else
                    {
                        adminOutputTextBox.Text += kongVm.KongName + "\t" + kongVm.ResultMessage + "\r\n";
                    }
                }
                catch (Exception ex)
                {
                    Helper.OutputWindowMessage(this, MethodBase.GetCurrentMethod().Name + ": Error - \r\n" + ex);
                }
            });
            this.workerThread.Start();
        }


        /// <summary>
        /// Loops through a list of kong info strings, and returns the guild of that player
        /// </summary>
        private void reportGetUserGuildsButton_Click(object sender, EventArgs e)
        {
            // Get the layout of the __users file, and remember the order (we're doing operations in parallel and need to reorder it later)
            Dictionary<int, string> userLines = new Dictionary<int, string>();
            ConcurrentDictionary<int, string> newUserLines = new ConcurrentDictionary<int, string>();
            int i = 1;

            foreach (var line in adminPlayerListBox.Items.Cast<string>().ToList())
            {
                userLines.Add(i, line);
                i++;
            }

            // We're either returning the users file with guild tags altered, or just a list of ordered kongStrings
            bool kongStringsOnly = adminGetUserGuildsKongStringsOnlyCheckBox.Checked;

            KongViewModel kongVm = new KongViewModel();

            // Track progress
            grinderProgressBar.Maximum = adminPlayerListBox.Items.Count;
            grinderProgressBar.Step = 1;
            grinderProgressBar.Value = 0;


            Parallel.ForEach(userLines, new ParallelOptions { MaxDegreeOfParallelism = 10 }, userLine =>
            {
                try
                {
                    int index = userLine.Key;
                    string kongInfo = userLine.Value;

                    // Commented lines - record these, move on
                    if (kongInfo.StartsWith("//") || kongInfo.StartsWith("kongName:-"))
                    {
                        newUserLines.TryAdd(index, kongInfo);
                    }
                    // Placeholder lines (e.g. "kongName:Baxtex") - Don't have the required kongInfo
                    else if (!kongInfo.ToLower().Contains("user_id") && !kongInfo.ToLower().Contains("userid"))
                    {
                        newUserLines.TryAdd(index, kongInfo);
                    }
                    else
                    {
                        string faction = "_UNGUILDED";
                        string shortFaction = "__";

                        // If this user already has a guild tag, note it in shortFaction
                        if (kongInfo.Length > 3 && kongInfo[2] == ',')
                            shortFaction = kongInfo.Substring(0, 2);

                        // Call UpdateFaction to get this users' guild
                        kongVm = BotManager.UpdateFaction(this, kongInfo, pullPlayerDecks: false);

                        // Successful call
                        if (kongVm.Faction.Name != null)
                        {
                            faction = kongVm?.Faction?.Name;
                            if (faction == "") faction = "_UNGUILDED";

                            // Get the guild shortname
                            shortFaction = TextCleaner.GetGuildShortName(faction);
                        }
                        // Broken
                        else
                        {
                            shortFaction = "!!";
                        }

                        // Guild \t KongInfo
                        // kongInfo: if it starts with "XX, replace that with the new shortFaction tag
                        //result.Append("\t");
                        if (kongInfo.Length > 3 && kongInfo[2] == ',')
                        {
                            string newKongInfo = shortFaction + "," + kongInfo.Substring(3, kongInfo.Length - 3);
                            newUserLines.TryAdd(index, newKongInfo);
                        }
                        else
                        {
                            newUserLines.TryAdd(index, kongInfo);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Helper.OutputWindowMessage(this, MethodBase.GetCurrentMethod().Name + ": Error - \r\n" + ex);
                }
                finally
                {
                    grinderProgressBar.PerformStep();
                }
            });

            grinderProgressBar.Value = 0;

            // Format output
            List<string> results = new List<string>();
            if (kongStringsOnly)
            {
                foreach (var kvp in newUserLines.OrderBy(x => x.Value))
                {
                    string kongInfo = kvp.Value;
                    if (kongInfo.StartsWith("//") || kongInfo.StartsWith("kongName:-")) continue;
                    results.Add(kongInfo);
                }
            }
            else
            {
                foreach (var kvp in newUserLines.OrderBy(x => x.Key))
                {
                    results.Add(kvp.Value);
                }
            }

            adminOutputTextBox.Text = string.Join("\r\n", results.ToArray());
        }


        #endregion

        #region Admin - Guild Management

        /// <summary>
        /// Makes this user leave guild
        /// </summary>
        private void guildManagementLeaveGuildButton_Click(object sender, EventArgs e)
        {
            // Allow specific admins to this
            if (!CONSTANTS.GUILD_MOVERS.Contains(CONFIG.userName)) return;

            // Setup the form
            int batchJobNumber = ++grinderProgressBarJobNumber;
            GrindParameters gp = SetupGrindParams();
            if (gp.SelectedUsers.Count == 0)
            {
                adminOutputTextBox.Text = "Select users in the left users dropdown (defined by /__users.txt) to use this action";
                return;
            }

            try
            {
                KongViewModel kongVm = BotManager.LeaveFaction(gp.SelectedUsers[0]);

                if (kongVm.Result != "False")
                {
                    adminOutputTextBox.AppendText(kongVm.KongName + " has left guild\r\n");
                }
                else
                {
                    adminOutputTextBox.AppendText(kongVm.KongName + " - " + kongVm.ResultMessage.Replace("[", "").Replace("]", ""));
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, MethodBase.GetCurrentMethod().Name + ": Error - \r\n" + ex);
            }
            finally
            {
                // Track progress
                grinderProgressBar.PerformStep();
                if (grinderProgressBar.Value >= grinderProgressBar.Maximum)
                {
                    //adminOutputTextBox.AppendText("Done\r\n");
                    grinderProgressBar.Value = 0;
                }
            }
        }

        /// <summary>
        /// Accepts a guild invite (faction_id)
        /// </summary>
        private void guildManagementAcceptGuildButton_Click(object sender, EventArgs e)
        {
            // Allow specific admins to this
            if (!CONSTANTS.GUILD_MOVERS.Contains(CONFIG.userName)) return;

            // Setup the form
            int batchJobNumber = ++grinderProgressBarJobNumber;
            GrindParameters gp = SetupGrindParams();
            if (gp.SelectedUsers.Count == 0)
            {
                adminOutputTextBox.Text = "Select users in the left users dropdown (defined by /__users.txt) to use this action";
                return;
            }

            try
            {
                // FactionID input is either '103901002' or 'TidalWave:103901002'
                string[] factionIdInput = guildManagementFactionIdComboBox.Text.Split(new char[] { ':' }, 2);
                string factionId = factionIdInput[factionIdInput.Length - 1].Trim();

                KongViewModel kongVm = BotManager.AcceptFactionInvite(gp.SelectedUsers[0], factionId);

                if (kongVm.Result != "False")
                {
                    adminOutputTextBox.AppendText(kongVm.KongName + " has accepted a guild invite to " + kongVm?.Faction?.Name + "\r\n");
                }
                else
                {
                    adminOutputTextBox.AppendText(kongVm.KongName + " - " + kongVm.ResultMessage.Replace("[", "").Replace("]", ""));
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, MethodBase.GetCurrentMethod().Name + ": Error - \r\n" + ex);
            }
            finally
            {
                // Track progress
                grinderProgressBar.PerformStep();
                if (grinderProgressBar.Value >= grinderProgressBar.Maximum)
                {
                    //adminOutputTextBox.AppendText("Done\r\n");
                    grinderProgressBar.Value = 0;
                }
            }
        }

        /// <summary>
        /// Invite a player (target_user_id) to this guild. Requires officer/guild leader rank
        /// </summary>
        private void guildManagementInvitePlayerButton_Click(object sender, EventArgs e)
        {
            // Allow specific admins to this
            // if (!CONSTANTS.GUILD_MOVERS.Contains(CONFIG.userName)) return;

            // Setup the form
            int batchJobNumber = ++grinderProgressBarJobNumber;
            GrindParameters gp = SetupGrindParams();
            if (gp.SelectedUsers.Count == 0)
            {
                adminOutputTextBox.Text = "Select users in the left users dropdown (defined by /__users.txt) to use this action";
                return;
            }

            // Try to find the officer's userId in the adminPlayerListBox. If we can't, then we can't continue
            string[] officerIdInput = guildManagementOfficerIdComboBox.Text.Split(new char[] { ':' }, 2);
            string officerId = officerIdInput[officerIdInput.Length - 1].Trim();
            string officerKongString = adminPlayerListBox.Items.Cast<string>().ToList().FirstOrDefault(x => x.Contains(officerId));

            if (officerKongString == null)
            {
                adminOutputTextBox.Text = "Cound not find this officer";
                return;
            }


            try
            {
                // First, get the userId of who we're kicking
                KongViewModel kongVm = new KongViewModel();
                ApiManager.GetKongInfoFromString(kongVm, gp.SelectedUsers[0]);
                string targetUserId = kongVm.UserId;

                // Manual userId instead
                if (guildManagementUserIdCheckBox.Checked)
                {
                    targetUserId = guildManagementUserIdTextBox.Text;
                }

                // If we know the user's kongstring
                // * Have them leave their current guild first (if applicable)
                if (!guildManagementUserIdCheckBox.Checked)
                {
                    kongVm = BotManager.LeaveFaction(gp.SelectedUsers[0]);
                }


                // Now call the officer string
                kongVm = BotManager.SendFactionInvite(officerKongString, targetUserId);

                // Successful invite
                if (kongVm.Result != "False")
                {
                    adminOutputTextBox.AppendText(kongVm.KongName + " has invited " + targetUserId + " to  " + kongVm?.Faction?.Name + "\r\n");

                    // If we have that player in __users, have that player accept the invite
                    string targetFactionId = kongVm.Faction.Id;
                    string targetUser = adminPlayerListBox.Items.Cast<string>().ToList().FirstOrDefault(x => x.Contains(targetUserId));
                    if (targetUser != null)
                    {
                        kongVm = BotManager.AcceptFactionInvite(targetUser, targetFactionId);

                        if (kongVm.Result != "False")
                        {
                            adminOutputTextBox.AppendText(kongVm.KongName + " has accepted a guild invite to " + kongVm?.Faction?.Name + "\r\n");
                        }
                        else
                        {
                            adminOutputTextBox.AppendText(kongVm.KongName + " - " + kongVm.ResultMessage.Replace("[", "").Replace("]", ""));
                        }
                    }
                }
                // User was already invited - try to have that user accept invite
                else if (kongVm.ResultMessage.Contains("already invited"))
                {
                    // If we have that player in __users, have that player accept the invite
                    string targetFactionId = kongVm.Faction.Id;
                    string targetUser = adminPlayerListBox.Items.Cast<string>().ToList().FirstOrDefault(x => x.Contains(targetUserId));
                    if (targetUser != null)
                    {
                        kongVm = BotManager.AcceptFactionInvite(targetUser, targetFactionId);

                        if (kongVm.Result != "False")
                        {
                            adminOutputTextBox.AppendText(kongVm.KongName + " has accepted a guild invite to " + kongVm?.Faction?.Name + "\r\n");
                        }
                        else
                        {
                            adminOutputTextBox.AppendText(kongVm.KongName + " - " + kongVm.ResultMessage.Replace("[", "").Replace("]", ""));
                        }
                    }
                }
                else
                {
                    adminOutputTextBox.AppendText(kongVm.KongName + " - " + kongVm.ResultMessage.Replace("[", "").Replace("]", ""));
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, MethodBase.GetCurrentMethod().Name + ": Error - \r\n" + ex);
            }
            finally
            {
                // Track progress
                grinderProgressBar.PerformStep();
                if (grinderProgressBar.Value >= grinderProgressBar.Maximum)
                {
                    //adminOutputTextBox.AppendText("Done\r\n");
                    grinderProgressBar.Value = 0;
                }
            }
        }


        /// <summary>
        /// Kick a player (target_user_id) from this guild. Requires officer/guild leader rank
        /// </summary>
        private void guildManagementKickFactionMemberButton_Click(object sender, EventArgs e)
        {
            // Allow specific admins to this
            if (!CONSTANTS.GUILD_MOVERS.Contains(CONFIG.userName))
            {
                adminOutputTextBox.Text = "You need elevated ptuo permission to kick";
                return;
            }

            // Setup the form
            int batchJobNumber = ++grinderProgressBarJobNumber;
            GrindParameters gp = SetupGrindParams();
            if (gp.SelectedUsers.Count == 0)
            {
                adminOutputTextBox.Text = "Select users in the left users dropdown (defined by /__users.txt) to use this action";
                return;
            }

            // Try to find the officer's userId in the adminPlayerListBox. If we can't, then we can't continue
            string[] officerIdInput = guildManagementOfficerIdComboBox.Text.Split(new char[] { ':' }, 2);
            string officerId = officerIdInput[officerIdInput.Length - 1].Trim();
            string officerKongString = adminPlayerListBox.Items.Cast<string>().ToList().FirstOrDefault(x => x.Contains(officerId));

            if (officerKongString == null)
            {
                adminOutputTextBox.Text = "Cound not find this officer";
                return;
            }


            try
            {
                // First, get the userId of who we're kicking
                KongViewModel kongVm = new KongViewModel();
                ApiManager.GetKongInfoFromString(kongVm, gp.SelectedUsers[0]);
                string targetUserId = kongVm.UserId;

                // Manual userId instead
                if (guildManagementUserIdCheckBox.Checked)
                {
                    targetUserId = guildManagementUserIdTextBox.Text;
                }

                // Now call the officer string
                kongVm = BotManager.KickFactionMember(officerKongString, targetUserId);

                if (kongVm.Result != "False")
                {
                    adminOutputTextBox.AppendText(kongVm.KongName + " has kicked " + targetUserId + " from " + kongVm?.Faction?.Name + "\r\n");
                }
                else
                {
                    adminOutputTextBox.AppendText(kongVm.KongName + " - " + kongVm.ResultMessage.Replace("[", "").Replace("]", ""));
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, MethodBase.GetCurrentMethod().Name + ": Error - \r\n" + ex);
            }
            finally
            {
                // Track progress
                grinderProgressBar.PerformStep();
                if (grinderProgressBar.Value >= grinderProgressBar.Maximum)
                {
                    //adminOutputTextBox.AppendText("Done\r\n");
                    grinderProgressBar.Value = 0;
                }
            }
        }

        /// <summary>
        /// Promote a faction member
        /// </summary>
        private void guildManagementPromoteMemberButton_Click(object sender, EventArgs e)
        {
            // Allow specific admins to this
            if (!CONSTANTS.GUILD_MOVERS.Contains(CONFIG.userName))
            {
                adminOutputTextBox.Text = "You need elevated ptuo permission to promote";
                return;
            }

            // Setup the form
            int batchJobNumber = ++grinderProgressBarJobNumber;
            GrindParameters gp = SetupGrindParams();
            if (gp.SelectedUsers.Count == 0)
            {
                adminOutputTextBox.Text = "Select users in the left users dropdown (defined by /__users.txt) to use this action";
                return;
            }

            // Try to find the officer's userId in the adminPlayerListBox. If we can't, then we can't continue
            string[] officerIdInput = guildManagementOfficerIdComboBox.Text.Split(new char[] { ':' }, 2);
            string officerId = officerIdInput[officerIdInput.Length - 1].Trim();
            string officerKongString = adminPlayerListBox.Items.Cast<string>().ToList().FirstOrDefault(x => x.Contains(officerId));

            if (officerKongString == null)
            {
                adminOutputTextBox.Text = "Cound not find this officer";
                return;
            }


            try
            {
                // First, get the userId of who we're kicking
                KongViewModel kongVm = new KongViewModel();
                ApiManager.GetKongInfoFromString(kongVm, gp.SelectedUsers[0]);
                string targetUserId = kongVm.UserId;

                // Manual userId instead
                if (guildManagementUserIdCheckBox.Checked)
                {
                    targetUserId = guildManagementUserIdTextBox.Text;
                }

                // Now call the officer string
                kongVm = BotManager.PromoteFactionMember(officerKongString, targetUserId);

                if (kongVm.Result != "False")
                {
                    adminOutputTextBox.AppendText(kongVm.KongName + " has promoted " + targetUserId + " in " + kongVm?.Faction?.Name + "\r\n");
                }
                else
                {
                    adminOutputTextBox.AppendText(kongVm.KongName + " - " + kongVm.ResultMessage.Replace("[", "").Replace("]", ""));
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, MethodBase.GetCurrentMethod().Name + ": Error - \r\n" + ex);
            }
            finally
            {
                // Track progress
                grinderProgressBar.PerformStep();
                if (grinderProgressBar.Value >= grinderProgressBar.Maximum)
                {
                    //adminOutputTextBox.AppendText("Done\r\n");
                    grinderProgressBar.Value = 0;
                }
            }
        }

        /// <summary>
        /// Get who is in this guild
        /// </summary>
        private void guildManagementGetRosterButton_Click(object sender, EventArgs e)
        {
            // Setup the form
            int batchJobNumber = ++grinderProgressBarJobNumber;
            GrindParameters gp = SetupGrindParams();
            if (gp.SelectedUsers.Count == 0)
            {
                adminOutputTextBox.Text = "Select users in the left users dropdown (defined by /__users.txt) to use this action";
                return;
            }

            // Clear out the listView
            guildManagementRosterListView.Items.Clear();

            // We also want to recognize which 
            List<string> kongInfos = adminPlayerListBox.Items.Cast<string>().ToList();

            try
            {
                KongViewModel kongVm = BotManager.UpdateFaction(this, gp.SelectedUsers[0], pullPlayerDecks: false);
                if (kongVm.Result != "False")
                {
                    // -----------------------------
                    // ROSTER
                    // -----------------------------

                    // Display guild in the listView
                    guildManagementGuildNameLabel.Text = kongVm.Faction.Name + " (" + kongVm.Faction.Id + ") [" + kongVm.Faction.Members.Count + "/50]";

                    adminOutputTextBox.AppendText(kongVm.Faction.Name + "\tUserId\tRole\tOnline\tLvl\r\n");

                    // Display each member in the listView
                    foreach (var member in kongVm.Faction.Members.OrderByDescending(x => x.Role).ThenBy(x => x.Name))
                    {
                        string role = "";
                        if (member.Role == 31) role = "L";
                        else if (member.Role == 21) role = "O";

                        string lastOnline = member.LastUpdateTime.Days * -1 > 0 ?
                             "*" + member.LastUpdateTime.Days * -1 + "D" + member.LastUpdateTime.Hours * -1 + "h" :
                             member.LastUpdateTime.Hours * -1 + "h" + member.LastUpdateTime.Minutes * -1 + "m";

                        string level = member.Level < 25 ? member.Level.ToString() : "";

                        ListViewItem listViewItem = new ListViewItem(new string[] {
                            member.Name,
                            member.UserId,
                            role,
                            lastOnline,
                            level
                        });
                        listViewItem.UseItemStyleForSubItems = false;

                        // Highlight poor PVP rank
                        if (member.Level < 23) listViewItem.SubItems[4].BackColor = Color.Yellow;

                        // Highlight officers
                        if (member.Role == 21) listViewItem.SubItems[2].BackColor = Color.DarkCyan;
                        else if (member.Role == 31) listViewItem.SubItems[2].BackColor = Color.DarkCyan;

                        // Check to see if we have this userID. If we do, assume its covered
                        string kongInfo = kongInfos.FirstOrDefault(x => x.Contains(member.UserId));
                        if (kongInfo != null)
                        {
                            // If marked as a shell, reflect it
                            if (kongInfo.ToLower().Contains("[shell]"))
                                listViewItem.SubItems[0].Text = "[Shell] " + listViewItem.SubItems[0].Text;

                            listViewItem.SubItems[0].BackColor = Color.LightGreen;
                        }


                        // Highlight poor lastOnlineTimes
                        if (member.LastUpdateTime.Days * -1 > 0)
                        {
                            listViewItem.SubItems[3].BackColor = Color.OrangeRed;
                        }

                        //adminOutputTextBox.AppendText(member.Name + "\t" + member.UserId + "\t" + role + "\t" + lastOnline + "\t" + level + "\r\n");


                        guildManagementRosterListView.Items.Add(listViewItem);
                    }
                    //guildManagementRosterListView.Items.Add



                    // -----------------------------
                    // FORTS
                    // -----------------------------
                    guildFortsGemsTextBox.Text = kongVm.Faction.GuildPoints.ToString();
                    guildFortsSuppliesTextBox.Text = kongVm.Faction.GuildSupplies.ToString();

                    if (kongVm.Faction.Fort11 != null) guildFortsDefenseFort1TextBox.Text = kongVm.Faction.Fort11.Name;
                    if (kongVm.Faction.Fort12 != null) guildFortsDefenseFort2TextBox.Text = kongVm.Faction.Fort12.Name;
                    if (kongVm.Faction.Fort21 != null) guildFortsOffenseFort1TextBox.Text = kongVm.Faction.Fort21.Name;
                    if (kongVm.Faction.Fort22 != null) guildFortsOffenseFort2TextBox.Text = kongVm.Faction.Fort22.Name;


                    // -----------------------------
                    // GBGE
                    // * Only call Init on a weekend when war would potentially happen
                    // -----------------------------

                    // TODO: See if getFaction retrieves what gBGEs are available

                    // TODO: Is there a non-init call to get GBGE?

                    if (Helper.IsEventWeekend())
                    {
                        kongVm = BotManager.Init(gp.SelectedUsers[0]);
                        if (kongVm.Result != "False")
                        {
                            guildWarBgeStatusTextBox.Text = kongVm.WarData.AttackerFactionName + " - " + kongVm.WarData.AttackerBGE + "\r\n" +
                                "VS.\r\n" +
                                kongVm.WarData.DefenderFactionName + " - " + kongVm.WarData.DefenderBGE;
                        }
                        else
                        {
                            adminOutputTextBox.Text = "Api error";
                        }
                    }
                }
                else
                {
                    adminOutputTextBox.AppendText(kongVm.KongName + " - " + kongVm.ResultMessage.Replace("[", "").Replace("]", ""));
                }
                adminOutputTextBox.AppendText("\r\n");
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, MethodBase.GetCurrentMethod().Name + ": Error - \r\n" + ex);
            }
            finally
            {
                // Track progress
                grinderProgressBar.PerformStep();
                if (grinderProgressBar.Value >= grinderProgressBar.Maximum)
                {
                    //adminOutputTextBox.AppendText("Done\r\n");
                    grinderProgressBar.Value = 0;
                }
            }

        }

        /// <summary>
        /// If a user is selected, and we have that userID in the users file, select it
        /// </summary>
        private void guildManagementRosterListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                if (guildManagementRosterListView.SelectedIndices.Count > 0)
                {
                    int index = guildManagementRosterListView.SelectedIndices[0];
                    ListViewItem item = guildManagementRosterListView.Items[index];

                    // Select this player
                    int.TryParse(item.SubItems[1].Text, out int userId);
                    if (userId > 0)
                    {
                        guildManagementUserIdTextBox.Text = userId.ToString();

                        foreach (var line in adminPlayerListBox.Items.Cast<string>())
                        {
                            string kongString = line.ToLower().Trim();
                            if (kongString.Contains("user_id:" + userId) || kongString.Contains("userid:" + userId))
                            {
                                adminPlayerListBox.SelectedItems.Clear();
                                adminPlayerListBox.SelectedItem = line;
                                break;
                            }
                        }
                    }
                }
            }
            catch { }
        }

        #endregion

        #region Admin - Fort/GBGE Management

        /// <summary>
        /// Get fort/GBGE data for the selected user
        /// </summary>
        private void guildFortsButton_Click(object sender, EventArgs e)
        {
            // Setup the form
            GrindParameters gp = SetupGrindParams();
            if (gp.SelectedUsers.Count == 0)
            {
                adminOutputTextBox.Text = "Select users in the left users dropdown (defined by /__users.txt) to use this action";
                return;
            }

            // Get the first selected users' faction data and display it
            try
            {
                KongViewModel kongVm = BotManager.UpdateFaction(this, gp.SelectedUsers[0], pullPlayerDecks: false);
                if (kongVm.Result != "False")
                {
                    guildFortsGemsTextBox.Text = kongVm.Faction.GuildPoints.ToString();
                    guildFortsSuppliesTextBox.Text = kongVm.Faction.GuildSupplies.ToString();

                    if (kongVm.Faction.Fort11 != null) guildFortsDefenseFort1TextBox.Text = kongVm.Faction.Fort11.Name;
                    if (kongVm.Faction.Fort12 != null) guildFortsDefenseFort2TextBox.Text = kongVm.Faction.Fort12.Name;
                    if (kongVm.Faction.Fort21 != null) guildFortsOffenseFort1TextBox.Text = kongVm.Faction.Fort21.Name;
                    if (kongVm.Faction.Fort22 != null) guildFortsOffenseFort2TextBox.Text = kongVm.Faction.Fort22.Name;
                }
                else
                {
                    adminOutputTextBox.AppendText(kongVm.KongName + " - " + kongVm.ResultMessage.Replace("[", "").Replace("]", ""));
                }
                adminOutputTextBox.AppendText("\r\n");
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, MethodBase.GetCurrentMethod().Name + ": Error - \r\n" + ex);
            }
            finally
            {
                // Track progress
                grinderProgressBar.PerformStep();
                if (grinderProgressBar.Value >= grinderProgressBar.Maximum)
                {
                    //adminOutputTextBox.AppendText("Done\r\n");
                    grinderProgressBar.Value = 0;
                }
            }
        }

        /// <summary>
        /// Buy a guild fort, or set it
        /// - Seems like TU Api backend handles whether to buy or equip
        /// </summary>
        private void guildFortsBuyFortButton_Click(object sender, EventArgs e)
        {
            // Setup the form
            GrindParameters gp = SetupGrindParams();
            if (gp.SelectedUsers.Count == 0)
            {
                adminOutputTextBox.Text = "Select users in the left users dropdown (defined by /__users.txt) to use this action";
                return;
            }

            int fortressId = -1;
            int slotId = -1;

            // Get fortress slot
            switch (guildBuyFortressSlotComboBox.Text)
            {
                case "Off1":
                    slotId = 21;
                    break;
                case "Off2":
                    slotId = 22;
                    break;
                case "Def1":
                    slotId = 11;
                    break;
                case "Def2":
                    slotId = 12;
                    break;
            }

            // Get card ID (hardcoded at the moment)
            switch (guildBuyFortressComboBox.Text)
            {
                case "Medical Center-1":
                    fortressId = 2906;
                    break;
                case "Medical Center-2":
                    fortressId = 2907;
                    break;
                case "Medical Center-3":
                    fortressId = 2908;
                    break;
                case "Medical Center":
                    fortressId = 2909;
                    break;
                case "Lightning Cannon-1":
                    fortressId = 2736;
                    break;
                case "Lightning Cannon-2":
                    fortressId = 2737;
                    break;
                case "Lightning Cannon-3":
                    fortressId = 2738;
                    break;
                case "Lightning Cannon":
                    fortressId = 2739;
                    break;
                case "Death Factory-1":
                    fortressId = 2754;
                    break;
                case "Death Factory-2":
                    fortressId = 2755;
                    break;
                case "Death Factory-3":
                    fortressId = 2756;
                    break;
                case "Death Factory":
                    fortressId = 2757;
                    break;
                case "Corrosive Spore-1":
                    fortressId = 2742;
                    break;
                case "Corrosive Spore-2":
                    fortressId = 2743;
                    break;
                case "Corrosive Spore-3":
                    fortressId = 2744;
                    break;
                case "Corrosive Spore":
                    fortressId = 2745;
                    break;
                case "Darkspire-1":
                    fortressId = 2910;
                    break;
                case "Darkspire-2":
                    fortressId = 2911;
                    break;
                case "Darkspire-3":
                    fortressId = 2912;
                    break;
                case "Darkspire":
                    fortressId = 2913;
                    break;
                case "Inspiring Altar-1":
                    fortressId = 2900;
                    break;
                case "Inspiring Altar-2":
                    fortressId = 2901;
                    break;
                case "Inspiring Altar-3":
                    fortressId = 2902;
                    break;
                case "Inspiring Altar":
                    fortressId = 2903;
                    break;
                case "Tesla Coil-1":
                    fortressId = 2700;
                    break;
                case "Tesla Coil-2":
                    fortressId = 2701;
                    break;
                case "Tesla Coil-3":
                    fortressId = 2702;
                    break;
                case "Tesla Coil":
                    fortressId = 2703;
                    break;
                case "Minefield-1":
                    fortressId = 2706;
                    break;
                case "Minefield-2":
                    fortressId = 2707;
                    break;
                case "Minefield-3":
                    fortressId = 2708;
                    break;
                case "Minefield":
                    fortressId = 2709;
                    break;
                case "Foreboding Archway-1":
                    fortressId = 2712;
                    break;
                case "Foreboding Archway-2":
                    fortressId = 2713;
                    break;
                case "Foreboding Archway-3":
                    fortressId = 2714;
                    break;
                case "Foreboding Archway":
                    fortressId = 2715;
                    break;
                case "Forcefield-1":
                    fortressId = 2718;
                    break;
                case "Forcefield-2":
                    fortressId = 2719;
                    break;
                case "Forcefield-3":
                    fortressId = 2720;
                    break;
                case "Forcefield":
                    fortressId = 2721;
                    break;
                case "Illuminary Blockade-1":
                    fortressId = 2724;
                    break;
                case "Illuminary Blockade-2":
                    fortressId = 2725;
                    break;
                case "Illuminary Blockade-3":
                    fortressId = 2726;
                    break;
                case "Illuminary Blockade":
                    fortressId = 2727;
                    break;
            }

            if (fortressId == -1)
            {
                adminOutputTextBox.AppendText("Fortress card not recognized");
                return;
            }
            if (slotId == -1)
            {
                adminOutputTextBox.AppendText("Select a valid fortress slot");
                return;
            }


            KongViewModel kongVm = BotManager.BuyFactionCard(gp.SelectedUsers[0], slotId, fortressId);
            if (kongVm.Result != "False")
            {
                guildFortsGemsTextBox.Text = kongVm.Faction.GuildPoints.ToString();
                guildFortsSuppliesTextBox.Text = kongVm.Faction.GuildSupplies.ToString();

                if (kongVm.Faction.Fort11 != null) guildFortsDefenseFort1TextBox.Text = kongVm.Faction.Fort11.Name;
                if (kongVm.Faction.Fort12 != null) guildFortsDefenseFort2TextBox.Text = kongVm.Faction.Fort12.Name;
                if (kongVm.Faction.Fort21 != null) guildFortsOffenseFort1TextBox.Text = kongVm.Faction.Fort21.Name;
                if (kongVm.Faction.Fort22 != null) guildFortsOffenseFort2TextBox.Text = kongVm.Faction.Fort22.Name;
            }
            else
            {
                adminOutputTextBox.AppendText(kongVm.KongName + " - " + kongVm.ResultMessage.Replace("[", "").Replace("]", ""));
            }
            adminOutputTextBox.AppendText("\r\n");
        }

        /// <summary>
        /// Set the GBGE for the selected user's guild
        /// * The user must be a guild officer/leader to do this
        /// </summary>
        private void guildWarBgeSetButton_Click(object sender, EventArgs e)
        {
            // Setup the form
            GrindParameters gp = SetupGrindParams();
            if (gp.SelectedUsers.Count == 0)
            {
                adminOutputTextBox.Text = "Select an officer in the left users dropdown (defined by /__users.txt) to use this action";
                return;
            }

            // Format
            // <name>:<gbgeId>
            // <gbgeId>
            string[] selectedBgeSplit = guildWarBgeComboBox.Text.Split(new char[] { ':' }, 2, StringSplitOptions.RemoveEmptyEntries);
            string selectedBgeString = selectedBgeSplit.Length == 1 ? selectedBgeSplit[0] : selectedBgeSplit[1];
            int selectedBgeId = 0;

            switch(selectedBgeString)
            {
                case "Progenitor Tech":
                    selectedBgeId = 2010;
                    break;
                case "Plasma Burst":
                    selectedBgeId = 2029;
                    break;

                case "Tartarian Gift":
                    selectedBgeId = 2016;
                    break;
                case "Artillery":
                    selectedBgeId = 2017;
                    break;
                case "BlightBlast":
                    selectedBgeId = 2002;
                    break;
                case "Charged Up":
                    selectedBgeId = 2005;
                    break;

                case "Divine Blessing":
                    selectedBgeId = 2012;
                    break;
                case "Emergency Aid":
                    selectedBgeId = 2020;
                    break;
                case "Landmine":
                    selectedBgeId = 2028;
                    break;

                case "Mirror Madness":
                    selectedBgeId = 2022;
                    break;
                case "Sandblast":
                    selectedBgeId = 2030;
                    break;
                case "Triage":
                    selectedBgeId = 2004;
                    break;
                case "Winter Tempest":
                    selectedBgeId = 2026;
                    break;
                default:
                    int.TryParse(selectedBgeString, out selectedBgeId);
                    break;
            }

            if (selectedBgeId <= 0)
            {
                adminOutputTextBox.Text = "Selected Bge not recognized - input either the name, or the GBGE ID (ex: 'Winter Tempest', 2026)";
                return;
            }

            KongViewModel kongVm = BotManager.SetGuildBge(gp.SelectedUsers[0], selectedBgeId);
            if (kongVm.Result != "False")
            {
                adminOutputTextBox.AppendText("**********\r\n" + selectedBgeString + " has been set \r\n**********\r\n");
            }
            else
            {
                adminOutputTextBox.AppendText(kongVm.KongName + " - " + kongVm.ResultMessage.Replace("[", "").Replace("]", ""));
            }
            adminOutputTextBox.AppendText("\r\n");
        }

        #endregion

        #region Admin - Misc

        /// <summary>
        /// This will pull inventories for all users in __users.txt with a string starting with XX, where XX is the guild tag
        /// Ex: "DT" will pull this string: "DT,kongName=..."
        /// </summary>
        private void adminMiscCreateInventoryButton_Click(object sender, EventArgs e)
        {
            // If filtering, only pull Commander duals/quads, Dominions, and CardPower>0
            bool filterPlayerCards = adminMiscCreateInventoryFilterCardsCheckBox.Checked;

            List<string> extraBlacklistedCards = adminMiscCreateInventoryExtraFilteredCardsTextBox.Text
                .Split(new string[] { "\r\n", "," }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .ToList();
            

            // Get the guild(s) to pull
            List<string> targetGuilds = adminMiscGuildTextBox.Text.Trim().Split(',').ToList();
            if (targetGuilds.Count == 0)
            {
                adminOutputTextBox.AppendText("A guild tag is required (e.g. 'TF')\r\n");
                return;
            }

            // ----------------
            // Refresh seeds from config/seed-xx.txt (they could have changed)
            // ----------------
            {
                CONFIG.PlayerConfigSeeds.Clear();
                List<string> seedFileNames = new List<string>()
                {
                    "seeds-dt.txt",
                    "seeds-general.txt",
                    "seeds-mj.txt",
                    "seeds-tfk.txt",
                    "seeds-tw.txt",
                    "seeds-wh.txt",
                };

                foreach(var seedFileName in seedFileNames)
                {
                    List<string> seeds = FileIO.SimpleRead(this, "config/" + seedFileName, returnCommentedLines: false);
                    foreach (var seed in seeds)
                    {
                        string[] playerSeed = seed.Split(':');
                        if (playerSeed.Length != 3) continue;

                        string name = playerSeed[0].Trim();
                        string seedType = playerSeed[1].Trim();
                        string deck = playerSeed[2].Trim();

                        CONFIG.PlayerConfigSeeds.Add(new Tuple<string, string, string>(name, seedType, deck));
                    }

                }
            }

            // Create a gauntlet for each user that's their seed decks.
            // Store it in customdecks_genetic.txt
            ConcurrentDictionary<string, List<string>> geneticSeeds = new ConcurrentDictionary<string, List<string>>();


            if (targetGuilds.Count > 0)
            {
                adminOutputTextBox.AppendText("Clearing /data/customdecks_genetic - where player seeds for the genetic sim are kept. \r\n");
                FileIO.SimpleWrite(this, "data", "customdecks_genetic.txt", "");
            }

            // ----------------
            // For each guild
            // ----------------
            this.stopProcess = false;
            this.workerThread = new Thread(() => {
                foreach (var targetGuild in targetGuilds)
                {
                    List<string> kongStrings = adminPlayerListBox.Items.Cast<string>().Where(x => x.StartsWith(targetGuild)).ToList();
                    if (kongStrings.Count == 0)
                    {
                        adminOutputTextBox.AppendText("No kongstrings starting with " + targetGuild + "\r\n");
                        continue;
                    }
                    adminOutputTextBox.AppendText("Getting inventories for " + kongStrings.Count + " players\r\n");

                    // Successful filenames created
                    ConcurrentBag<string> fileNames = new ConcurrentBag<string>();


                    // ----------------
                    // For each user, create or update a ptuo inventory file
                    // ----------------
                    Parallel.ForEach(kongStrings, new ParallelOptions { MaxDegreeOfParallelism = 5 }, kongString =>
                    {
                        try
                        {
                            // Init and get user cards
                            KongViewModel kongVm = BotManager.Init(kongString, true);
                            StringBuilder inventoryFile = new StringBuilder();
                            StringBuilder filteredOutCards = new StringBuilder();
                            StringBuilder possibleInventoryFile = new StringBuilder();
                            bool includePossibleCards = adminMiscCreateInventoryIncludeRestoreCardsCheckBox.Checked;

                            // ** TODO 5/20 ** - create a possibleCards file and checkbox system to include it in sims, and always pull possiblecards
                            // * Possibly 'fuse up' singles/duals of cards with power > 0 (besides base fusions) and add those to the count

                            if (kongVm != null)
                            {
                                List<string> playerGeneticSeeds = new List<string>();
                                List<string> modifiedBlacklistedCards = new List<string>();
                                
                                // Specific guild filtering - will clip a guild tag off a filtered card (e.g. "DT:Miasma Master" -> "Miasma Master"
                                if (kongVm?.Faction?.Name == "DireTide")
                                {
                                    extraBlacklistedCards.ForEach(x => { modifiedBlacklistedCards.Add(x.Replace("DT:", ""));  });
                                }
                                else if (kongVm?.Faction?.Name == "TidalWave")
                                {
                                    extraBlacklistedCards.ForEach(x => { modifiedBlacklistedCards.Add(x.Replace("TW:", "")); });
                                }
                                else if (kongVm?.Faction?.Name == "MasterJedis")
                                {
                                    extraBlacklistedCards.ForEach(x => { modifiedBlacklistedCards.Add(x.Replace("MJ:", "")); });
                                }
                                else
                                {
                                    modifiedBlacklistedCards = extraBlacklistedCards;
                                }

                                inventoryFile.AppendLine();
                                inventoryFile.Append("// ");
                                inventoryFile.Append(kongVm.KongName);
                                inventoryFile.AppendLine();

                                // -----------------------------------------
                                // Create seeds for this player
                                // * Faction and power seeds are created from player cards
                                // * Manual seeds (ex: config/seeds-dt.txt) are also created
                                // -----------------------------------------
                                {
                                    var playerSeedCards = kongVm.PlayerCards.ToDictionary(x => x.Key, x => x.Value);

                                    // Include restoreCards with power > 1 in player seeds
                                    if (includePossibleCards)
                                    {
                                        var restoreCards = kongVm.RestoreCards.Where(x => x.Key.Power >= 1).ToDictionary(x => x.Key, x => x.Value);
                                        foreach (var restoreCard in restoreCards)
                                        {
                                            if (playerSeedCards.ContainsKey(restoreCard.Key))
                                            {
                                                playerSeedCards[restoreCard.Key] += restoreCard.Value;
                                            }
                                            else
                                            {
                                                playerSeedCards.Add(restoreCard.Key, restoreCard.Value);
                                            }
                                        }
                                    }

                                    // Create seed strings
                                    string seedPower = PlayerManager.CreateExternalSeed(playerSeedCards, factionSeed: 0);
                                    string seedImperial = PlayerManager.CreateExternalSeed(playerSeedCards, factionSeed: 1);
                                    string seedRaider = PlayerManager.CreateExternalSeed(playerSeedCards, factionSeed: 2);
                                    string seedBloodthirsty = PlayerManager.CreateExternalSeed(playerSeedCards, factionSeed: 3);
                                    string seedXeno = PlayerManager.CreateExternalSeed(playerSeedCards, factionSeed: 4);
                                    string seedRighteous = PlayerManager.CreateExternalSeed(playerSeedCards, factionSeed: 5);

                                    // Extra strings for genetic sims
                                    string seedPower2 = PlayerManager.CreateExternalSeed(playerSeedCards, factionSeed: 0, maxDeckSize: 10, seedNumber: 2);
                                    string seedImperial2 = PlayerManager.CreateExternalSeed(playerSeedCards, factionSeed: 1, sortByFactionPower: false, maxCopies: 2, seedNumber: 2);
                                    string seedRaider2 = PlayerManager.CreateExternalSeed(playerSeedCards, factionSeed: 2, sortByFactionPower: false, maxCopies: 2, seedNumber: 2);
                                    string seedBloodthirsty2 = PlayerManager.CreateExternalSeed(playerSeedCards, factionSeed: 3, sortByFactionPower: false, maxCopies: 2, seedNumber: 2);
                                    string seedXeno2 = PlayerManager.CreateExternalSeed(playerSeedCards, factionSeed: 4, sortByFactionPower: false, maxCopies: 2, seedNumber: 2);
                                    string seedRighteous2 = PlayerManager.CreateExternalSeed(playerSeedCards, factionSeed: 5, sortByFactionPower: false, maxCopies: 2, seedNumber: 2);

                                    // Tempo and Strike
                                    string seedTempo = PlayerManager.CreateExternalSeedTempoDeck(playerSeedCards);
                                    string seedStrike = PlayerManager.CreateExternalSeedStrikeDeck(playerSeedCards);

                                    inventoryFile.AppendLine(seedPower);
                                    inventoryFile.AppendLine(seedPower2);
                                    inventoryFile.AppendLine(seedTempo);
                                    inventoryFile.AppendLine(seedStrike);
                                    inventoryFile.AppendLine();
                                    inventoryFile.AppendLine(seedImperial);
                                    inventoryFile.AppendLine(seedImperial2);
                                    inventoryFile.AppendLine(seedRaider);
                                    inventoryFile.AppendLine(seedRaider2);
                                    inventoryFile.AppendLine(seedBloodthirsty);
                                    inventoryFile.AppendLine(seedBloodthirsty2);
                                    inventoryFile.AppendLine(seedXeno);
                                    inventoryFile.AppendLine(seedXeno2);
                                    inventoryFile.AppendLine(seedRighteous);
                                    inventoryFile.AppendLine(seedRighteous2);
                                    inventoryFile.AppendLine();

                                    // Get player active / defense deck
                                    string activeDeckId = kongVm.UserData.ActiveDeck;
                                    string defenseDeckId = kongVm.UserData.DefenseDeck;

                                    UserDeck activeDeck = kongVm.UserDecks.FirstOrDefault(x => x.Id == activeDeckId);
                                    UserDeck defenseDeck = kongVm.UserDecks.FirstOrDefault(x => x.Id == defenseDeckId);

                                    playerGeneticSeeds.Add("//Seed-ActiveDeck: " + activeDeck.DeckToString());
                                    playerGeneticSeeds.Add("//Seed-DefenseDeck: " + defenseDeck.DeckToString());
                                    playerGeneticSeeds.Add(seedPower);
                                    playerGeneticSeeds.Add(seedPower2);
                                    playerGeneticSeeds.Add(seedTempo);
                                    playerGeneticSeeds.Add(seedStrike);
                                    playerGeneticSeeds.Add(seedImperial);
                                    playerGeneticSeeds.Add(seedImperial2);
                                    playerGeneticSeeds.Add(seedRaider);
                                    playerGeneticSeeds.Add(seedRaider2);
                                    playerGeneticSeeds.Add(seedBloodthirsty);
                                    playerGeneticSeeds.Add(seedBloodthirsty2);
                                    playerGeneticSeeds.Add(seedXeno);
                                    playerGeneticSeeds.Add(seedXeno2);
                                    playerGeneticSeeds.Add(seedRighteous);
                                    playerGeneticSeeds.Add(seedRighteous2);

                                    // For each seed, check if it exists in the seeds file. 
                                    // * If it does, use that seed. If not, use the current active/defense deck
                                    foreach (string seedName in CONSTANTS.SEEDS_FROM_CONFIG)
                                    {
                                        var seedDeck = CONFIG.PlayerConfigSeeds.Where(x => x.Item1 == kongVm.KongName && x.Item2 == seedName).FirstOrDefault();
                                        if (seedDeck != null)
                                        {
                                            playerGeneticSeeds.Add("//Seed-" + seedName + ": " + seedDeck.Item3);
                                            inventoryFile.Append("//Seed-" + seedName + ": " + seedDeck.Item3 + "\r\n");
                                        }
                                        else if (seedName.Contains("Attack"))
                                        {
                                            inventoryFile.Append("//Seed-" + seedName + ": " + activeDeck.DeckToString() + "\r\n");
                                        }
                                        else
                                        {
                                            inventoryFile.Append("//Seed-" + seedName + ": " + defenseDeck.DeckToString() + "\r\n");
                                        }
                                    }
                                }

                                // -----------------------------------------
                                // Add Player cards with 
                                // * cardPower > 1
                                // * aren't in the manually filtered list (if provided)
                                // -----------------------------------------
                                {
                                    // Assaults/structures
                                    var assaultsAndStructures = kongVm.PlayerCards
                                        .Where(x => x.Key.CardType == CardType.Assault.ToString() ||
                                                    x.Key.CardType == CardType.Structure.ToString())
                                                .OrderByDescending(x => x.Key.Fusion_Level)
                                                .ThenByDescending(x => x.Key.Rarity)
                                                .ThenByDescending(x => x.Key.Faction)
                                                .ThenByDescending(x => x.Key.Name)
                                                .ToList();

                                    inventoryFile.AppendLine();
                                    inventoryFile.AppendLine("// --- Quads ---");
                                    int currentFusionLevel = 2;

                                    foreach (var cardDict in assaultsAndStructures)
                                    {
                                        string cardName = cardDict.Key.Name;
                                        int fusionLevel = cardDict.Key.Fusion_Level;
                                        int cardCount = cardDict.Value;

                                        // ------------------------------------
                                        // Comment out low power cards
                                        // ------------------------------------
                                        if (filterPlayerCards && cardDict.Key.Power <= 0)
                                        {
                                            filteredOutCards.Append("//" + cardName);
                                            if (cardCount > 1)
                                            {
                                                filteredOutCards.Append("#");
                                                filteredOutCards.Append(cardCount);
                                            }
                                            filteredOutCards.Append("\r\n");
                                            continue;
                                        }
                                        // ------------------------------------
                                        // Comment out manually filtered cards
                                        // ------------------------------------
                                        else if (filterPlayerCards && modifiedBlacklistedCards.Contains(cardName))
                                        {
                                            filteredOutCards.Append("//" + cardName);
                                            if (cardCount > 1)
                                            {
                                                filteredOutCards.Append("#");
                                                filteredOutCards.Append(cardCount);
                                            }
                                            filteredOutCards.Append("\r\n");
                                            continue;
                                        }

                                        // ------------------------------------
                                        // Add card
                                        // ------------------------------------
                                        else
                                        {
                                            inventoryFile.Append(cardName);
                                            if (cardCount > 1)
                                            {
                                                inventoryFile.Append("#");
                                                inventoryFile.Append(cardCount);
                                            }
                                            inventoryFile.Append("\r\n");

                                            // Add spaces when fusion level changes
                                            if (currentFusionLevel == 2 && fusionLevel == 1)
                                            {
                                                currentFusionLevel = 1;
                                                inventoryFile.AppendLine("// --- Duals/Singles ---");
                                            }
                                        }
                                    }

                                    inventoryFile.AppendLine();
                                    inventoryFile.AppendLine("// --- Commanders/Dominions ---");

                                    // Commanders/Dominions
                                    var dominions = kongVm.PlayerCards.Keys
                                        .Where(x => x.CardType == CardType.Dominion.ToString())
                                        .ToList();

                                    foreach (var dominion in dominions)
                                    {
                                        string dominionName = dominion.Name;
                                        if (filterPlayerCards && modifiedBlacklistedCards.Contains(dominionName))
                                        {
                                            filteredOutCards.Append("//" + dominionName);
                                            filteredOutCards.Append("\r\n");
                                            continue;
                                        }

                                        inventoryFile.AppendLine(dominionName);
                                    }

                                    var commanders = kongVm.PlayerCards.Keys
                                        .Where(x => x.CardType == CardType.Commander.ToString())
                                                .OrderByDescending(x => x.Fusion_Level)
                                                .ThenByDescending(x => x.Rarity)
                                                .ThenByDescending(x => x.Faction)
                                                .ToList();

                                    foreach (var commander in commanders)
                                    {
                                        string commanderName = commander.Name;
                                        if (filterPlayerCards && modifiedBlacklistedCards.Contains(commanderName))
                                        {
                                            filteredOutCards.Append("//" + commanderName);
                                            filteredOutCards.Append("\r\n");
                                            continue;
                                        }
                                        if (commander.Fusion_Level == 0) continue;
                                        if (filterPlayerCards && commander.Fusion_Level == 1) continue;

                                        inventoryFile.AppendLine(commanderName);
                                    }
                                }

                                // -----------------------------------------
                                // Add Restore cards with cardPower > 1 if the checkbox is checked
                                // -----------------------------------------
                                if (includePossibleCards)
                                {
                                    inventoryFile.AppendLine();
                                    inventoryFile.AppendLine("// ---------------------");
                                    inventoryFile.AppendLine("// --- Restore Cards ---");
                                    inventoryFile.AppendLine("// ---------------------");

                                    // Assaults/structures
                                    var assaultsAndStructures = kongVm.RestoreCards
                                        .Where(x => x.Key.CardType == CardType.Assault.ToString() ||
                                                    x.Key.CardType == CardType.Structure.ToString())
                                                .OrderByDescending(x => x.Key.Fusion_Level)
                                                .ThenByDescending(x => x.Key.Rarity)
                                                .ThenByDescending(x => x.Key.Faction)
                                                .ThenByDescending(x => x.Key.Name)
                                                .ToList();

                                    inventoryFile.AppendLine();
                                    inventoryFile.AppendLine("// --- Quads ---");
                                    int currentFusionLevel = 2;

                                    foreach (var cardDict in assaultsAndStructures)
                                    {
                                        string cardName = cardDict.Key.Name;
                                        int fusionLevel = cardDict.Key.Fusion_Level;
                                        int cardCount = cardDict.Value;

                                        // Get the leveled name and Card object of this card (restore has a card as Quad level 1)
                                        // * Doing this to retrieve the power of the card
                                        cardName = cardName.Substring(0, cardName.Length - 2);
                                        Card leveledCard = CardManager.GetPlayerCardByName(cardName);


                                        // ------------------------------------
                                        // Comment out low power cards
                                        // ------------------------------------
                                        if (filterPlayerCards && leveledCard.Power <= 0)
                                        {
                                            //filteredOutCards.Append("//" + cardName);
                                            //if (cardCount > 1)
                                            //{
                                            //    filteredOutCards.Append("#");
                                            //    filteredOutCards.Append(cardCount);
                                            //}
                                            //filteredOutCards.Append("\r\n");
                                            //continue;
                                        }

                                        // ------------------------------------
                                        // Comment out manually filtered cards
                                        // ------------------------------------
                                        else if (filterPlayerCards && extraBlacklistedCards.Contains(cardName))
                                        {
                                            //filteredOutCards.Append("//" + cardName);
                                            //if (cardCount > 1)
                                            //{
                                            //    filteredOutCards.Append("#");
                                            //    filteredOutCards.Append(cardCount);
                                            //}
                                            //filteredOutCards.Append("\r\n");
                                            //continue;
                                        }

                                        // ------------------------------------
                                        // Add card
                                        // ------------------------------------
                                        else
                                        {
                                            inventoryFile.Append(cardName);
                                            if (cardCount > 1)
                                            {
                                                inventoryFile.Append("(+");
                                                inventoryFile.Append(cardCount);
                                                inventoryFile.Append(")");
                                            }
                                            inventoryFile.Append("\r\n");

                                            // Add spaces when fusion level changes
                                            if (currentFusionLevel == 2 && fusionLevel == 1)
                                            {
                                                currentFusionLevel = 1;
                                                inventoryFile.AppendLine("// --- Duals/Singles ---");
                                            }
                                        }
                                    }
                                }



                                // -----------------------------------------
                                // Add filtered out commented cards
                                // -----------------------------------------
                                inventoryFile.AppendLine();
                                inventoryFile.AppendLine();
                                inventoryFile.AppendLine("// -- Cards filtered out -- //");
                                inventoryFile.Append(filteredOutCards.ToString());

                                // -----------------------------------------
                                // Write the result string to a card file and add it to the dropdowns
                                // -----------------------------------------
                                {
                                    string fileName = "_" + targetGuild + "_" + kongVm.KongName + ".txt";
                                    FileIO.SimpleWrite(this, "data/cards", fileName, inventoryFile.ToString());
                                    fileNames.Add(fileName);
                                }

                                // -----------------------------------------
                                // Add the list of seeds to the genetic file
                                // -----------------------------------------
                                geneticSeeds.TryAdd(kongVm.KongName, playerGeneticSeeds);
                            }
                            else
                            {
                                adminOutputTextBox.AppendText("Failed to call TU on " + kongString + "\r\n");
                            }

                        }
                        catch (Exception ex)
                        {
                            Helper.OutputWindowMessage(this, "Error on creating inventories from API: \r\n" + ex + "\r\n");
                        }
                    });

                    // Sort filenames, then add them to the inventory listboxes
                    List<string> names = fileNames.ToList();
                    names.Sort();
                    names.Reverse();
                    foreach (var fileName in names)
                    {
                        if (!inventoryListBox1.Items.Contains(fileName))
                        {
                            inventoryListBox1.Items.Insert(0, fileName);
                            inventoryListBox2.Items.Insert(0, fileName);
                            inventoryListBox3.Items.Insert(0, fileName);
                            batchSimInventoryListBox.Items.Insert(0, fileName);
                        }
                    }

                    adminOutputTextBox.AppendText(names.Count + " files added to inventory list boxes\r\n");
                    adminOutputTextBox.AppendText("Done\r\n");
                }

                // ----------------
                // Finally, modify customdecks_genetic.txt
                // ----------------
                StringBuilder sb = new StringBuilder();
                if (!File.Exists("data/customdecks_genetic.txt"))
                {
                    FileIO.SimpleWrite(this, "data", "customdecks_genetic.txt", "");
                }

                foreach(var geneticSeed in geneticSeeds)
                {
                    string playerName = geneticSeed.Key;
                    List<string> playerDecks = geneticSeed.Value;

                    sb.AppendLine(playerName + "-Genetic: /^" + playerName + "_.*$/");
                    sb.AppendLine(playerName + "-GeneticShort: /^" + playerName + "_(Power|Seed-).*$/");
                    sb.AppendLine(playerName + "-GeneticFree: /^(" + playerName + "_|GeneticFree_).*$/");
                    sb.AppendLine(playerName + "-GeneticDolphin: /^(" + playerName + "_|GeneticFree_|GeneticDolphin_).*$/");
                    sb.AppendLine(playerName + "-GeneticWhale: /^(" + playerName + "_|GeneticFree_|GeneticDolphin_|GeneticWhale_).*$/");
                    foreach (var deck in playerDecks)
                    {
                        sb.AppendLine(deck.Replace("//", playerName + "_"));
                    }
                    sb.AppendLine();
                }

                // Append the "Genetic_(Free|Dolphin|Whale)" pulled from appsettings
                foreach(var deck in CONSTANTS.GENETIC_FREE_DECKS) sb.AppendLine(deck);
                sb.AppendLine();
                foreach (var deck in CONSTANTS.GENETIC_DOLPHIN_DECKS) sb.AppendLine(deck);
                sb.AppendLine();
                foreach (var deck in CONSTANTS.GENETIC_WHALE_DECKS) sb.AppendLine(deck);

                // TODO: Instead of appending, can we replace existing player seeds with newer ones
                FileIO.SimpleWrite(this, "data", "customdecks_genetic.txt", sb.ToString(), append:true);
            });
            this.workerThread.Start();
        }

        /// <summary>
        /// Count alive threds
        /// </summary>
        private void adminMiscCountThreadsButton_Click(object sender, EventArgs e)
        {
            ProcessThreadCollection threads = Process.GetCurrentProcess().Threads;

            adminOutputTextBox.AppendText(threads.Count + "\r\n");

            foreach (ProcessThread thread in threads)
            {
                adminOutputTextBox.AppendText(thread.Id + ": " + thread.StartTime + "\r\n");
            }
        }

        /// <summary>
        /// Send requests to licious - signin/signout
        /// Mini hacky postman 
        /// </summary>
        private void adminSendLiciousRequestButton_Click(object sender, EventArgs e)
        {
            try
            {
                string[] input = adminInputTextBox.Text.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

                // Line-by-line
                // 0: <command>
                // 1: url
                // 2: authorization
                // 3: postParams, request1
                // 4: postParams, request2
                // 5....

                if (input.Length >= 4)
                {
                    string command = input[0];
                    string url = input[1];
                    string authorization = input[2];


                    // Foreach web request
                    for (int i = 3; i < input.Length; i++)
                    {
                        if (input[i].Trim().StartsWith("//")) continue;

                        // Postdata
                        string postData = input[i].Trim();

                        // Modify post data based on command
                        // - Sign-in, Sign-out
                        if (command.Contains("&action=IN") || command.Contains("&action=OUT"))
                        {
                            postData = command.Replace("XXXXX", postData);
                        }
                        else
                        {

                        }

                        byte[] data = Encoding.ASCII.GetBytes(postData);

                        WebRequest request = HttpWebRequest.Create(url);
                        request.Method = "POST";
                        request.ContentType = "application/x-www-form-urlencoded";
                        request.Headers.Add("Authorization", authorization);
                        request.ContentLength = data.Length;

                        using (var stream = request.GetRequestStream())
                        {
                            stream.Write(data, 0, data.Length);
                        }

                        // Get the api response
                        HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                        string responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();

                        outputTextBox.AppendText(responseString + "\r\n");
                    }
                    outputTextBox.AppendText("Done\r\n");
                }
                else
                {
                    outputTextBox.AppendText("Error: Not enough input lines, or some misconfiguration\r\n");
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(this, "Error on adminSendLiciousRequestButton_Click: \r\n" + ex + "\r\n");
            }
        }

        #endregion

        #region Admin - MagiTech

        /// <summary>
        /// Add the selected players to this script
        /// </summary>
        private void adminMagitechAddScriptButton_Click(object sender, EventArgs e)
        {
            List<string> selectedUsers = adminPlayerListBox.SelectedItems.Cast<string>().ToList();
            List<string> script = adminMagitechCreateScriptTextBox.Text.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();

            StringBuilder selectedUserNames = new StringBuilder();
            KongViewModel kongVm = new KongViewModel();


            // Get the kongName for each kongInfo string
            foreach (var user in selectedUsers)
            {
                ApiManager.GetKongInfoFromString(kongVm, user);
                if (!string.IsNullOrWhiteSpace(kongVm.KongName)) selectedUserNames.AppendLine("player: " + kongVm.KongName.Trim());
            }


            // Format:
            // *** SCRIPT START ***
            // player: kongName1
            // player: kongName2
            // duration: Every 1 hours
            // command: mission - {all|10 energy|to 10 energy}
            // command: pvp - {all|10 energy|to 10 energy}
            // command: event - {all|10 energy|to 10 energy} - {zone}
            // ex: cq Nexus to 5 energy
            // *** SCRIPT END ***
            adminMagitechScriptTextBox.AppendText(
                "*** SCRIPT START ***\r\n" +
                selectedUserNames.ToString() +
                string.Join("\r\n", script) +
                "\r\n*** SCRIPT END ***\r\n\r\n"
            );
        }

        /// <summary>
        /// Run the selected players
        /// </summary>
        private void adminMagitechRunScriptButton_click(object sender, EventArgs e)
        {
            int loopHour = 0;

            this.stopProcess = false;
            this.workerThread = new Thread(() =>
            {
                // Loop until the scriptLoop box is unchecked
                do
                {
                    // Record the time it took to run the script
                    adminOutputTextBox.AppendText("\r\nMAGITECH - Starting hour #" + loopHour+1 + "\r\n");
                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();

                    // Read through the script and execute each script block
                    List<string> scriptLines = adminMagitechScriptTextBox.Text.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    List<string> allKongUsers = adminPlayerListBox.Items.Cast<string>().ToList();
                    List<string> kongUsers = new List<string>();
                    List<string> commands = new List<string>();
                    List<string> reportOptions = new List<string>();
                    int hours = 0;
                    string state = "START";

                    foreach (var l in scriptLines)
                    {
                        string scriptLine = l.ToLower().Trim();

                        if (scriptLine == "*** script start ***")
                        {
                            state = "PROCESSING";
                        }
                        else if (scriptLine.StartsWith("duration:") && state == "PROCESSING")
                        {
                            string duration = l.Replace("duration:", "").Replace("every", "").Replace("hour", "").Replace("hours", "").Trim();
                            int.TryParse(duration, out hours);
                            if (hours <= 0) hours = 1;
                        }
                        else if (scriptLine.StartsWith("player:") && state == "PROCESSING")
                        {
                            string playerName = l.Replace("player:", "").Trim();
                            string kongUser = allKongUsers.FirstOrDefault(x => x.Contains(playerName));
                            if (!string.IsNullOrWhiteSpace(kongUser)) kongUsers.Add(kongUser);
                        }
                        else if (scriptLine.StartsWith("command:") && state == "PROCESSING")
                        {
                            string command = l.Replace("command:", "").Trim();
                            commands.Add(command);
                        }
                        else if (scriptLine.StartsWith("report:") && state == "PROCESSING")
                        {
                            string report = l.Replace("report:", "").Trim();
                            reportOptions.Add(report);
                        }
                        else if (scriptLine == "*** script end ***")
                        {
                            state = "START";

                            // Do operations
                            if (kongUsers.Count == 0) adminOutputTextBox.AppendText("Warning: Script has no users!\r\n");
                            if (commands.Count == 0) adminOutputTextBox.AppendText("Warning: Script has no commands!\r\n");

                            MagitechRunScripts(loopHour, hours, kongUsers, commands, reportOptions, threads: 5);

                            // Reset variables
                            kongUsers.Clear();
                            commands.Clear();
                            reportOptions.Clear();
                        }
                    }

                    // Delay until the next hour
                    long totalMinutesGrinding = stopwatch.ElapsedMilliseconds / 1000;// / 60;
                    stopwatch.Reset();

                    adminOutputTextBox.AppendText("Total Script time: " + totalMinutesGrinding + " minutes\r\n");
                    adminOutputTextBox.AppendText("Loop is ON; grinding in " + (60 - totalMinutesGrinding) + " minutes\r\n");

                    //int delayInMilliseconds = (60 - (int)totalMinutesGrinding) * 1000 * 60;
                    //if (delayInMilliseconds > 0) Thread.Sleep(delayInMilliseconds);

                    loopHour++;

                } while (adminMagitechLoopScriptCheckBox.Checked);

                adminOutputTextBox.AppendText("End script-grind\r\n");
            });
            this.workerThread.Start();
        }

        /// <summary>
        /// Run each user through the bot script
        /// 
        /// TODO: Pass some values that persist between script calls (like if a user is stuck on a battle, if its the same battle for X hours, resume it)
        /// 
        /// TODO: Claim event rewards (but not all the time - check kongVm for a claim flag)
        /// TODO: Cool people report settings and email
        /// TODO: Log events somewhere (FireBase?) and only log weirdo decks
        /// TODO: CampaignInTheAss
        /// </summary>
        public void MagitechRunScripts(int loopHour, int hours, List<string> kongUsers, List<string> commands, List<string> reportOptions, int threads=5, bool dontBlackbox=true)
        {
            // DEBUG
            threads = 1;
            bool debugOutput = true;

            if (threads <= 0) threads = 3;
            if (threads > 10) threads = 10;

            // If the main loop is 0, or loopHour % hours = 0, run this script. This is so scrips run every X hours
            if (loopHour != 0 && loopHour % hours != 0) return;

            // ---------------------------------------------
            // Running X players in parallel
            // ---------------------------------------------
            Parallel.ForEach(kongUsers, new ParallelOptions { MaxDegreeOfParallelism = threads }, kongInfo =>
            {
                KongViewModel kongVm = new KongViewModel(); // Object that contains all the data API can return, and parses our login KongString
                ApiManager.GetKongInfoFromString(kongVm, kongInfo);
                string kongName = kongVm.KongName;
                string resultMessage = "";
                int i = 0;

                try
                {
                    // Format:
                    // *** SCRIPT START ***
                    // player: kongName1
                    // player: kongName2
                    // duration: Every 1 hours
                    // command: mission - {all|10 energy|to 10 energy}
                    // command: pvp - {all|10 energy|to 10 energy}
                    // command: event - {all|10 energy|to 10 energy} - {zone}
                    // ex: cq Nexus to 5 energy
                    // *** SCRIPT END ***
                    //
                    // *** SCRIPT START ***
                    // player: kongName3
                    // ...

                    // Call Init to get the player's stats
                    kongVm = BotManager.Init(kongInfo);

                    // ---------------------------------------------
                    // Grind loop. Figure out what commands to call
                    // ---------------------------------------------
                    if (kongVm.Result != "False")
                    {
                        // Player info
                        string name = kongVm.KongName;

                        // Quest and Pvp stats
                        int stamina = kongVm.UserData.Stamina;
                        int maxStamina = kongVm.UserData.MaxStamina;
                        int missionEnergy = kongVm.UserData.Energy;
                        int maxMissionEnergy = kongVm.UserData.MaxEnergy;
                        List<MissionCompletion> missionCompletions = kongVm.MissionCompletions;
                        Quest quest1 = kongVm.Quests.Where(x => x.Id == -1).FirstOrDefault();
                        Quest quest2 = kongVm.Quests.Where(x => x.Id == -2).FirstOrDefault();
                        Quest quest3 = kongVm.Quests.Where(x => x.Id == -3).FirstOrDefault();

                        // Event stats
                        bool brawlActive = kongVm.BrawlActive;
                        bool raidActive = kongVm.RaidActive;
                        bool warActive = kongVm.WarActive; // Prevent attacks during downtime somehow
                        bool cqActive = kongVm.ConquestActive; // Zone
                        bool brawlRewardsActive = kongVm.BrawlRewardsActive;
                        bool raidRewardsActive = kongVm.RaidRewardsActive;
                        bool warRewardsActive = kongVm.WarRewardsActive;
                        bool cqRewardsActive = kongVm.ConquestRewardsActive;
                        int eventEnergy = -1;
                        if (brawlActive) eventEnergy = kongVm.BrawlData.Energy;

                        // ---------------------------------------------
                        // Spend mission energy
                        // ---------------------------------------------
                        string missionCommand = commands.FirstOrDefault(x => x.StartsWith("mission"));
                        if (!string.IsNullOrWhiteSpace(missionCommand))
                        {
                            // Parse command to figure out how much energy to spend
                            if (debugOutput) outputTextBox.AppendText(resultMessage);
                            missionCommand = missionCommand.Split('-')[1].Replace("energy", "").Trim(); // all, X, to X
                            int energyThreshold = 0;

                            if (missionCommand == "all") { }
                            else if (missionCommand.Contains("to"))
                            {
                                string threshold = missionCommand.Replace("to", "").Trim();
                                int.TryParse(threshold, out energyThreshold);
                            }
                            else
                            {
                                int.TryParse(missionCommand, out i);
                                if (i >= 0) energyThreshold = missionEnergy - i;
                            }

                            energyThreshold = Math.Max(0, energyThreshold);
                            if (energyThreshold < 0) adminOutputTextBox.AppendText("Debug: Something is wrong on mission energy math - " + missionCommand + "\r\n");


                            // -- Run missions --
                            kongVm = BotManager.AutoMissions(kongInfo, energyThreshold, missionCompletions, kongVm.Quests ?? new List<Quest>(), true, true, true, false, quest1, quest2, quest3);

                            // Result of missions
                            resultMessage = kongVm.Result != "False" ? kongVm.PtuoMessage : kongName + " - TU error: " + kongVm.ResultMessage + "\r\n";
                            if (debugOutput) outputTextBox.AppendText("Missions on " + kongName + ": " + resultMessage);
                        }


                        // ---------------------------------------------
                        // Spend pvp energy
                        // ---------------------------------------------
                        string pvpCommand = commands.FirstOrDefault(x => x.StartsWith("pvp"));
                        if (!string.IsNullOrWhiteSpace(pvpCommand))
                        {
                            // Parse command to figure out how much energy to spend
                            pvpCommand = pvpCommand.Split('-')[1].Replace("energy", "").Trim(); // all, X, to X
                            int staminaToSpend = stamina;

                            if (pvpCommand == "all") { }
                            else if (pvpCommand.Contains("to"))
                            {
                                string pvpThreshold = pvpCommand.Replace("to", "").Trim();
                                int.TryParse(pvpThreshold, out i);
                                if (i >= 0) staminaToSpend = stamina - i;
                            }
                            else
                            {
                                int.TryParse(pvpCommand, out staminaToSpend);
                            }

                            //staminaToSpend = Math.Max(0, staminaToSpend);
                            //if (staminaToSpend < 0) adminOutputTextBox.AppendText("Debug: Something is wrong on pvp energy math - " + pvpCommand + "\r\n");


                            // -- Run pvp battles --
                            kongVm = BotManager.AutoPvpBattles(kongInfo, staminaToSpend);

                            // Result of missions
                            resultMessage = kongVm.Result != "False" ? kongVm.PtuoMessage : kongName + " - TU error: " + kongVm.ResultMessage + "\r\n";
                            if (debugOutput) outputTextBox.AppendText("Pvp on " + kongName + ": " + resultMessage);
                        }


                        // ---------------------------------------------
                        // Brawl
                        // TODO: Code
                        // ---------------------------------------------
                        // TODO: Settings for iterations, [] attack faster, # of threads with defaults

                        // ---------------------------------------------
                        // Conquest
                        // TODO: Code zones
                        // ---------------------------------------------

                        // ---------------------------------------------
                        // Raid
                        // TODO: SmartRaid
                        // ---------------------------------------------
                        string raidCommand = commands.FirstOrDefault(x => x.StartsWith("raidCommand"));
                        if (raidActive && !string.IsNullOrWhiteSpace(raidCommand))
                        {
                            // Parse command to figure out how much energy to spend
                            raidCommand = raidCommand.Split('-')[1].Replace("energy", "").Trim(); // all, X, to X
                            int energyThreshold = 0;

                            if (raidCommand == "all") { }
                            else if (raidCommand.Contains("to"))
                            {
                                string threshold = raidCommand.Replace("to", "").Trim();
                                int.TryParse(threshold, out energyThreshold);
                            }
                            else
                            {
                                int.TryParse(raidCommand, out i);
                                if (i >= 0) energyThreshold = eventEnergy - i;
                            }

                            //energyThreshold = Math.Max(0, energyThreshold);
                            //if (energyThreshold < 0) adminOutputTextBox.AppendText("Debug: Something is wrong on mission energy math - " + missionCommand + "\r\n");


                            // -- Auto raid battles --
                            kongVm = BotManager.AutoRaidBattles(kongInfo, energyThreshold);

                            // Result of missions
                            resultMessage = kongVm.Result != "False" ? kongVm.PtuoMessage : kongName + " - TU error: " + kongVm.ResultMessage + "\r\n";
                            if (debugOutput) outputTextBox.AppendText("Raid on " + kongName + ": " + resultMessage);
                        }
                        else if (!string.IsNullOrWhiteSpace(raidCommand))
                        {
                            if (debugOutput) outputTextBox.AppendText("Raid is not active to attack");
                        }
                    }
                    else
                    {
                        adminOutputTextBox.AppendText(kongName + " - " + kongVm.ApiStatName + " - API error\r\n");
                    }
                }
                catch (Exception ex)
                {
                    Helper.OutputWindowMessage(this, kongName + ": Error on Grind(): \r\n" + ex.Message);
                }
                finally
                {
                    // Track progress
                    grinderProgressBar.PerformStep();
                    if (grinderProgressBar.Value >= grinderProgressBar.Maximum)
                    {
                        adminOutputTextBox.AppendText("Done pvp/mission\r\n");
                        grinderProgressBar.Value = 0;
                    }
                }
            });

        }

        #endregion

        #region Admin Buy Store Item

        private void adminBuyStoreButton_Click(object sender, EventArgs e)
        {
            // Setup the form
            GrindParameters gp = SetupGrindParams();
            if (gp.SelectedUsers.Count == 0)
            {
                adminOutputTextBox.Text = "Select users in the left users dropdown (defined by /__users.txt) to use this action";
                return;
            }

            this.stopProcess = false;
            this.workerThread = new Thread(() =>
            {
                Parallel.ForEach(gp.SelectedUsers, new ParallelOptions { MaxDegreeOfParallelism = 5 }, kongInfo =>
                {
                    try
                    {
                        // Call Init to get the player's stats
                        int itemId = int.Parse(adminBuyStoreItemIdTextBox.Text);
                        int itemType = int.Parse(adminBuyStoreItemTypeTextBox.Text);
                        int boxDiscountId = int.Parse(adminBuyStoreBoxDiscountIdTextBox.Text);

                        int howMany = int.Parse(adminBuyStoreBoxQuantityTextBox.Text);

                        KongViewModel kongVm = new KongViewModel();

                        for (int i = 0; i < howMany; i++)
                        {
                            kongVm = BotManager.BuyStorePromoTokens(this, kongInfo, itemId, itemType, boxDiscountId);
                        }

                        if (kongVm.Result != "False")
                        {
                            // Display results
                            // ... TODO: Display new WB total
                            adminOutputTextBox.AppendText(kongVm.KongName + " - " + kongVm.ResultMessage + "\r\n");
                        }
                        else
                        {
                            adminOutputTextBox.AppendText(kongVm.KongName + " - API error: " + kongVm.ResultMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        Helper.OutputWindowMessage(this, "adminBuyStoreButton_Click(): Error - \r\n" + ex);
                    }
                    finally
                    {
                        // Track progress
                        grinderProgressBar.PerformStep();
                        if (grinderProgressBar.Value >= grinderProgressBar.Maximum)
                        {
                            adminOutputTextBox.AppendText("Done - Buying\r\n");
                            grinderProgressBar.Value = 0;
                        }
                    }
                });
            });
            this.workerThread.Start();
        }

        #endregion

        /// <summary>
        /// Open the ./simresults/xxx file or the folder
        /// </summary>
        private void queuedSimOutputOpenFileTextBox_Click(object sender, EventArgs e)
        {
            var fileName = queuedSimOutputFileTextBox.Text;

            // Open selected file
            if (File.Exists("/sim results/" + fileName + ".txt"))
            {
                FileIO.OpenFile("/sim results/" + fileName + ".txt", this);
            }
            // Open simresults folder
            else
            {
                System.Diagnostics.Process.Start(@".\\sim results\\");
            }
        }

    }

}
