using PirateTUO2.Classes;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PirateTUO2.Modules
{
    /// <summary>
    /// This class sets up all the presets and previous options in the MainForm
    /// </summary>
    public class _ControlSetup
    {
        //static NameValueCollection appSettings = ConfigurationManager.AppSettings;

        static List<string> warForts  = new List<string>();
        static List<string> cqForts  = new List<string>();

        static List<string> bges  = new List<string>();
        static List<string> cqBges  = new List<string>();
        static List<string> warbges  = new List<string>();

        static List<string> modes  = new List<string>();
        static List<string> operations  = new List<string>();
        static List<string> customdecks  = new List<string>();
        static List<string> customdecksLevel2 = new List<string>();
        static List<string> customdecksLevel3 = new List<string>();
        static List<string> seeds  = new List<string>();

        static List<string> batchSimGuilds  = new List<string>();
        static List<string> batchSimOutputs  = new List<string>();
        static List<string> batchSimSpeeds  = new List<string>();
        static List<string> batchSimSpecialSims  = new List<string>();

        static List<string> specialSimModes = new List<string>();

        // Which cq zones are active this event cycle
        static List<string> cqActiveZones = new List<string>();

        // Guild/player Ids
        static List<string> guildIds = new List<string>();
        static List<string> playerIds = new List<string>();

        #region Initialize Forms

        /// <summary>
        /// Setup controls
        /// </summary>
        public static void Init(MainForm form)
        {
            // Creates a column for the cardFinder list view
            ColumnHeader header = new ColumnHeader();
            header.Text = "Card";
            header.Width = form.cardFinderListView.Width - 5;
            header.Name = "col1";
            form.cardFinderListView.Columns.Add(header);
            form.cardFinderListView.View = View.Details;

            // Get form controls and stuff
            _ControlSetup.PopulateFormControls(form);
            _ControlSetup.RefreshGauntlets(form);
            _ControlSetup.RefreshPlayers(form);
        }

        /// <summary>
        /// Set values for form controls
        /// * Fill in dropdowns
        /// * Get last used player selections
        /// </summary>
        public static void PopulateFormControls(MainForm form)
        {
            // Stop updating until finished
            form.SuspendLayout();

            // Don't throw up because another thread touched a form control
            Control.CheckForIllegalCrossThreadCalls = false;

            var time = new Stopwatch();
            time.Start();
            try
            {
                // Reads settings from appsettings.txt. Failing that, it will use app.config
                ReadFromAppSettings(form);


                // Populate Tab controls
                PopulateSimTabs(form);
                PopulateBatchSimTab(form);
                PopulateNavSimTab(form);
                PopulateCardFinderTab(form);
                PopulateRumBarrelTab(form);
                PopulateAdminTab(form);

                // ---------------------------- //
                // player tab
                form.playerFilterGuildComboBox.Items.Add("DT");
                form.playerFilterGuildComboBox.Items.Add("TW");
                form.playerFilterGuildComboBox.Items.Add("TF");
                form.playerFilterGuildComboBox.Items.Add("WH");
                form.playerFilterGuildComboBox.Items.Add("SL");
                form.playerFilterGuildComboBox.Items.Add("TC");

                // ---------------------------- //
                // gauntlet tab
                
                form.gtSearchTermComboBox.Text = "TargetGuild";
                form.gtSearchTermComboBox.Items.AddRange(CONSTANTS.gauntletSearchTerms.ToArray());

                //form.gtResultStrategyComboBox.Items.Add("1 - Merge copied results together");
                //form.gtResultStrategyComboBox.Items.Add("2 - Return all results with 3 or more cards");

                // I think the server is CST now. It ignores DST
                var cstNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"));
                                
                form.gtDateComboBox.Text = cstNow.ToString("M/d/yy");
                form.gtDateComboBox.Items.Add(cstNow.ToString("M/d/yy"));
                form.gtDateComboBox.Items.Add(cstNow.Month + "/(\\d)*/" + cstNow.ToString("yy"));

                form.gtOutputNameTextBox.Text = "";


                // ---------------------------- //
                // enemy inventory tab
                //form.invBuilderDateTextBox.Text = "[" + (now.Month - 1) + "-" + now.Month + "]/../" + now.Year.ToString().Substring(2, 2);
                //form.invBuilderPlayerOrGuildTextBox.Text = "Prophecy";

                // ---------------------------- //
                // rumBarrel
                form.rumBarrelCqZoneComboBox.Items.AddRange(cqActiveZones.ToArray());
                form.grindCqZoneComboBox.Items.AddRange(cqActiveZones.ToArray());

                // ---------------------------- //
                // show some tabs for nonofficer roles

                // Level 2 / Debug - RumBarrel
                if (CONFIG.role == "level2" || CONFIG.role == "newLevel2" || CONFIG.userName == "overboard")
                {
                    form.mainTabControl.TabPages.Insert(form.mainTabControl.TabPages.Count, form.rumBarrelTab);
                }

                // Level 3
                else if (form.debugMode == true || CONFIG.role == "level3" || CONFIG.role == "newLevel3")
                {
                    form.mainTabControl.TabPages.Insert(form.mainTabControl.TabPages.Count, form.rumBarrelTab);
                    form.mainTabControl.TabPages.Insert(form.mainTabControl.TabPages.Count, form.adminTab);
                    //form.sideTabControl.TabPages.Insert(form.sideTabControl.TabPages.Count, form.adminUpdateTab);
                }
            }
            catch(Exception ex)
            {
                Helper.OutputWindowMessage(form, "Error in PopulateFormControls(): " + ex);
            }

            time.Stop();
            Console.WriteLine(MethodInfo.GetCurrentMethod() + " " + time.ElapsedMilliseconds + "ms ");


            // Resume updating 
            form.ResumeLayout();
        }


        /// <summary>
        /// Refreshes all gauntlet listboxes based on the shown customdecks gauntlet file
        /// 
        /// It digs in these files for gaunlet regex
        /// </summary>
        public static void RefreshGauntlets(MainForm form)
        {
            var time = new Stopwatch();
            time.Start();

            // Sim Tabs 1-2-3 
            for (int i = 1; i <= 3; i++)
            {
                try
                {
                    var gauntletComboBox = ((ComboBox)form.Controls.Find("gauntletCustomDecksComboBox" + i, true).FirstOrDefault());
                    var gauntletListBox = ((ListBox)form.Controls.Find("gauntletListBox" + i, true).FirstOrDefault());

                    // Clear out the listbox and repopulate it
                    gauntletListBox.Items.Clear();

                    
                    // Open the gauntlet file (or try to)
                    string customdecksName = gauntletComboBox.Text.Replace("customdecks_", "").Replace(".txt", "");
                    if (String.IsNullOrWhiteSpace(customdecksName)) continue;

                    string filePath = "data/customdecks_" + customdecksName + ".txt";

                    // If blank, default to data/customdecks.txt
                    if (filePath == "") filePath = "data/customdecks.txt";


                    // Read each line and find /^...$/ that should indicate a gauntlet name
                    var gauntletFile = FileIO.SimpleRead(form, filePath, returnCommentedLines:true);
                    foreach(var line in gauntletFile)
                    {
                        // Gauntlet Line
                        if (line.Contains("/^") && line.Contains("$/"))
                        {
                            var firstColon = line.IndexOf(":");
                            gauntletListBox.Items.Add(line.Substring(0, firstColon));
                        }
                        // Gauntlet comment line
                        else if (line.Trim().StartsWith("//!!"))
                        {
                            gauntletListBox.Items.Add(line);
                        }
                    }

                    // Try to select the first gauntlet that doesn't start with 
                    if (gauntletListBox.SelectedItems.Count == 0)
                    {
                        for (int x = 0; x < gauntletListBox.Items.Count; x++)
                        {
                            string gauntletName = gauntletListBox.Items[x].ToString();
                            if (!gauntletName.StartsWith("--"))
                            {
                                gauntletListBox.SelectedIndex = x;
                                break ;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Helper.OutputWindowMessage(form, ex.Message, "Avast! Thar be an error on the RefreshGauntlets() method!");
                }
            }

            // Batch sim
            try
            {
                // Clear out the listbox and repopulate it
                form.batchSimGauntletListBox.Items.Clear();

                // Open the gauntlet file (or try to)
                var customdecksName = form.batchSimCustomDecksComboBox.Text;
                if (string.IsNullOrWhiteSpace(customdecksName)) return;

                var filePath = !(customdecksName == "" || customdecksName == "(customdecks)") ?
                            ".\\data\\customdecks_" + customdecksName + ".txt" :
                            ".\\data\\customdecks.txt";


                // Open a gauntlet file, and look for lines with /^..$/
                var gauntletFile = FileIO.SimpleRead(form, filePath, returnCommentedLines:true);
                foreach (var line in gauntletFile)
                {
                    if (line.Contains("/^") && line.Contains("$/"))
                    {
                        var firstColon = line.IndexOf(":");
                        form.batchSimGauntletListBox.Items.Add(line.Substring(0, firstColon));
                    }
                }

                // Try to select the first gauntlet that doesn't start with ---
                if (form.batchSimGauntletListBox.SelectedItems.Count == 0)
                {
                    for(int i= 0; i< form.batchSimGauntletListBox.Items.Count; i++)
                    {
                        string gauntletName = form.batchSimGauntletListBox.Items[i].ToString();
                        if (!gauntletName.StartsWith("--"))
                        { 
                            form.batchSimGauntletListBox.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(form, ex.Message, "Avast! Thar be an error on the RefreshGauntlets() method!");
            }

            // Nav sim
            try
            {
                // Clear out the listbox and repopulate it
                form.navSimGauntletListBox.Items.Clear();

                // Open the gauntlet file (or try to)
                var customdecksName = form.navSimCustomDecksComboBox.Text;
                if (!string.IsNullOrWhiteSpace(customdecksName))
                {

                    var filePath = !(customdecksName == "" || customdecksName == "(customdecks)") ?
                                ".\\data\\customdecks_" + customdecksName + ".txt" :
                                ".\\data\\customdecks.txt";


                    // Open a gauntlet file, and look for lines with /^..$/
                    var gauntletFile = FileIO.SimpleRead(form, filePath, returnCommentedLines: true);
                    foreach (var line in gauntletFile)
                    {
                        if (line.Contains("/^") && line.Contains("$/"))
                        {
                            var firstColon = line.IndexOf(":");
                            form.navSimGauntletListBox.Items.Add(line.Substring(0, firstColon));
                        }
                    }

                    // Try to select the first gauntlet that doesn't start with ---
                    if (form.navSimGauntletListBox.SelectedItems.Count == 0)
                    {
                        for (int i = 0; i < form.navSimGauntletListBox.Items.Count; i++)
                        {
                            string gauntletName = form.navSimGauntletListBox.Items[i].ToString();
                            if (!gauntletName.StartsWith("--"))
                            {
                                form.navSimGauntletListBox.SelectedIndex = i;
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(form, ex.Message, "Avast! Thar be an error on the RefreshGauntlets() method!");
            }

            // FirstCouncil sim
            try
            {
                // Clear out the listbox and repopulate it
                form.firstCouncilSimGauntletListBox.Items.Clear();

                // Open the gauntlet file (or try to)
                var customdecksName = form.firstCouncilSimCustomDecksComboBox.Text;
                if (string.IsNullOrWhiteSpace(customdecksName)) return;

                var filePath = !(customdecksName == "" || customdecksName == "(customdecks)") ?
                            ".\\data\\customdecks_" + customdecksName + ".txt" :
                            ".\\data\\customdecks.txt";


                // Open a gauntlet file, and look for lines with /^..$/
                var gauntletFile = FileIO.SimpleRead(form, filePath, returnCommentedLines: true);
                foreach (var line in gauntletFile)
                {
                    if (line.Contains("/^") && line.Contains("$/"))
                    {
                        var firstColon = line.IndexOf(":");
                        form.firstCouncilSimGauntletListBox.Items.Add(line.Substring(0, firstColon));
                    }
                }

                // Try to select the first gauntlet that doesn't start with ---
                if (form.firstCouncilSimGauntletListBox.SelectedItems.Count == 0)
                {
                    for (int i = 0; i < form.firstCouncilSimGauntletListBox.Items.Count; i++)
                    {
                        string gauntletName = form.firstCouncilSimGauntletListBox.Items[i].ToString();
                        if (!gauntletName.StartsWith("--"))
                        {
                            form.firstCouncilSimGauntletListBox.SelectedIndex = i;
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(form, ex.Message, "Avast! Thar be an error on the RefreshGauntlets() method!");
            }

            time.Stop();
            Console.WriteLine(MethodInfo.GetCurrentMethod() + " " + time.ElapsedMilliseconds + "ms ");
        }

        /// <summary>
        /// Refresh player inventory lists
        /// </summary>
        public static void RefreshPlayers(MainForm form)
        {
            var time = new Stopwatch();
            time.Start();

            // Player cards
            form.inventoryListBox1.Items.Clear();
            form.inventoryListBox2.Items.Clear();
            form.inventoryListBox3.Items.Clear();
            form.batchSimInventoryListBox.Items.Clear();

            // Add customdecks.txt
            form.inventoryListBox1.Items.Add("none");
            form.inventoryListBox1.Items.Add("customdecks.txt");
            form.inventoryListBox2.Items.Add("none");
            form.inventoryListBox2.Items.Add("customdecks.txt");
            form.inventoryListBox3.Items.Add("none");
            form.inventoryListBox3.Items.Add("customdecks.txt");
            form.batchSimInventoryListBox.Items.Add("none");
            form.batchSimInventoryListBox.Items.Add("customdecks.txt");

            var cardFiles = Directory.GetFiles(".\\data\\cards", "*.txt")
                                    .Select(path => Path.GetFileName(path))
                                    .ToList();

            foreach (var cardFile in cardFiles)
            {
                if (cardFile.Contains("[Conflict]")) continue; //google drive interference
                if (cardFile.Contains("_possible.txt")) continue;
                if (cardFile.Contains(" - Copy.txt")) continue;

                form.inventoryListBox1.Items.Add(cardFile);
                form.inventoryListBox2.Items.Add(cardFile);
                form.inventoryListBox3.Items.Add(cardFile);
                form.batchSimInventoryListBox.Items.Add(cardFile);
            }

            // Inventory/Extra Cards
            form.inventoryCardAddonsListBox1.Items.Clear();
            form.inventoryCardAddonsListBox2.Items.Clear();
            form.inventoryCardAddonsListBox3.Items.Clear();
            form.batchSimInventoryCardAddonsListBox.Items.Clear();
            form.inventoryCardAddonsListBox1.Items.Add("none");
            form.inventoryCardAddonsListBox2.Items.Add("none");
            form.inventoryCardAddonsListBox3.Items.Add("none");
            form.batchSimInventoryCardAddonsListBox.Items.Add("none");

            var boxFiles = //Directory.GetFiles(".\\config\\card-addons\\", "*.txt")
                            Directory.GetFiles(CONSTANTS.PATH_CARDADDONS, "*.txt")
                                    .Select(path => Path.GetFileName(path))
                                    .ToList();

            foreach (var boxFile in boxFiles)
            {
                //Ignore the creator files 
                if (boxFile.StartsWith("_")) continue;

                form.inventoryCardAddonsListBox1.Items.Add(boxFile);
                form.inventoryCardAddonsListBox2.Items.Add(boxFile);
                form.inventoryCardAddonsListBox3.Items.Add(boxFile);
                form.batchSimInventoryCardAddonsListBox.Items.Add(boxFile);
            }

            // --------------------------
            // Restore previous selections
            // --------------------------
            //var items = Properties.Settings.Default["inventoryListBox1"].ToString().Split(',');
            //foreach (var item in items)
            //{
            //    if (String.IsNullOrEmpty(item)) continue;
            //    form.inventoryListBox1.SelectedItems.Add(item);
            //}

            //items = Properties.Settings.Default["inventoryCardAddonsListBox1"].ToString().Split(',');
            //foreach (var item in items)
            //{
            //    if (String.IsNullOrEmpty(item)) continue;
            //    form.inventoryCardAddonsListBox1.SelectedItems.Add(item);
            //}

            // --------------------------
            // Restore previous filter
            // --------------------------
            //form.inventoryFilterComboBox1.Text = Properties.Settings.Default["inventoryFilterComboBox1"].ToString();
            //form.inventoryFilterComboBox2.Text = Properties.Settings.Default["inventoryFilterComboBox2"].ToString();
            //form.inventoryFilterComboBox3.Text = Properties.Settings.Default["inventoryFilterComboBox3"].ToString();

            time.Stop();
            Console.WriteLine(MethodInfo.GetCurrentMethod() + " " + time.ElapsedMilliseconds + "ms ");
        }

        #endregion

        #region Preset Game Modes

        /// <summary>
        /// Button that presets values in Sim Tab or Batch tab
        /// </summary>
        public static void SimShortcutButton(MainForm form, string controlName)
        {
            var i = 1;
            if (controlName.Contains("2")) i = 2;
            else if (controlName.Contains("3")) i = 3;
            else if (controlName.Contains("4")) i = 4; //batchSim

            try
            {
                // ----------------
                // Sim Tab 1/2/3
                // ----------------
                if (i <= 3)
                {
                    if (controlName.Contains("presetBrawlButton"))
                    {
                        Helper.GetComboBox(form, "myFortComboBox" + i).Text = "";
                        Helper.GetComboBox(form, "enemyFortComboBox" + i).Text = "";
                        Helper.GetComboBox(form, "yourBgeComboBox" + i).Text = "";
                        Helper.GetComboBox(form, "enemyBgeComboBox" + i).Text = "";

                        Helper.GetComboBox(form, "gameModeComboBox" + i).Text = "Brawl - Ordered";
                        Helper.GetComboBox(form, "gameOperationComboBox" + i).Text = "climb";
                        Helper.GetComboBox(form, "gameIterationsComboBox" + i).Text = "250";

                        Helper.GetTextBox(form, "gameMISTextBox" + i).Text = "0.01";
                        Helper.GetTextBox(form, "gameDeckLimitLowTextBox" + i).Text = "10";
                        Helper.GetTextBox(form, "gameDeckLimitHighTextBox" + i).Text = "10";

                        // Set gauntlet to brawl
                        if (Helper.GetComboBox(form, "gauntletCustomDecksComboBox" + i).Text != "brawl")
                        {
                            Helper.GetComboBox(form, "gauntletCustomDecksComboBox" + i).Text = "brawl";
                            _ControlSetup.RefreshGauntlets(form);
                        }
                    }
                    else if (controlName.Contains("presetBrawlDefButton"))
                    {
                        Helper.GetComboBox(form, "myFortComboBox" + i).Text = "";
                        Helper.GetComboBox(form, "enemyFortComboBox" + i).Text = "";
                        Helper.GetComboBox(form, "yourBgeComboBox" + i).Text = "";
                        Helper.GetComboBox(form, "enemyBgeComboBox" + i).Text = "";

                        Helper.GetComboBox(form, "gameModeComboBox" + i).Text = "Defense - Flex";
                        Helper.GetComboBox(form, "gameOperationComboBox" + i).Text = "climb";
                        Helper.GetComboBox(form, "gameIterationsComboBox" + i).Text = "35";

                        Helper.GetTextBox(form, "gameMISTextBox" + i).Text = "0.01";
                        Helper.GetTextBox(form, "gameDeckLimitLowTextBox" + i).Text = "";
                        Helper.GetTextBox(form, "gameDeckLimitHighTextBox" + i).Text = "";

                        // Set gauntlet to brawl
                        if (Helper.GetComboBox(form, "gauntletCustomDecksComboBox" + i).Text != "brawl")
                        {
                            Helper.GetComboBox(form, "gauntletCustomDecksComboBox" + i).Text = "brawl";
                            _ControlSetup.RefreshGauntlets(form);
                        }

                    }
                    else if (controlName.Contains("presetWarButton"))
                    {
                        Helper.GetComboBox(form, "myFortComboBox" + i).Text = CONSTANTS.WAR_OFFENSE_TOWERS;
                        Helper.GetComboBox(form, "enemyFortComboBox" + i).Text = CONSTANTS.WAR_DEFENSE_TOWERS;
                        //Helper.GetComboBox(form,"yourBgeComboBox" + i).Text = "";
                        //Helper.GetComboBox(form,"enemyBgeComboBox" + i).Text = "";

                        Helper.GetComboBox(form, "gameModeComboBox" + i).Text = "War - Random";
                        Helper.GetComboBox(form, "gameOperationComboBox" + i).Text = "climb";
                        Helper.GetComboBox(form, "gameIterationsComboBox" + i).Text = "150";

                        Helper.GetTextBox(form, "gameMISTextBox" + i).Text = "0.01";
                        Helper.GetTextBox(form, "gameDeckLimitLowTextBox" + i).Text = "";
                        Helper.GetTextBox(form, "gameDeckLimitHighTextBox" + i).Text = "";

                        // Set gauntlet to war
                        if (Helper.GetComboBox(form, "gauntletCustomDecksComboBox" + i).Text != "war")
                        {
                            Helper.GetComboBox(form, "gauntletCustomDecksComboBox" + i).Text = "war";
                            _ControlSetup.RefreshGauntlets(form);
                        }
                    }
                    else if (controlName.Contains("presetWarDefButton"))
                    {
                        Helper.GetComboBox(form, "myFortComboBox" + i).Text = CONSTANTS.WAR_DEFENSE_TOWERS;
                        Helper.GetComboBox(form, "enemyFortComboBox" + i).Text = CONSTANTS.WAR_OFFENSE_TOWERS;
                        //Helper.GetComboBox(form,"yourBgeComboBox" + i).Text = "";
                        //Helper.GetComboBox(form,"enemyBgeComboBox" + i).Text = "";

                        Helper.GetComboBox(form, "gameModeComboBox" + i).Text = "Defense - Flex";
                        Helper.GetComboBox(form, "gameOperationComboBox" + i).Text = "climb";
                        Helper.GetComboBox(form, "gameIterationsComboBox" + i).Text = "40";

                        Helper.GetTextBox(form, "gameMISTextBox" + i).Text = "0.01";
                        Helper.GetTextBox(form, "gameDeckLimitLowTextBox" + i).Text = "";
                        Helper.GetTextBox(form, "gameDeckLimitHighTextBox" + i).Text = "";

                        // Set gauntlet to war
                        if (Helper.GetComboBox(form, "gauntletCustomDecksComboBox" + i).Text != "war")
                        {
                            Helper.GetComboBox(form, "gauntletCustomDecksComboBox" + i).Text = "war";
                            _ControlSetup.RefreshGauntlets(form);
                        }
                    }
                    //else if (controlName.Contains("presetWarBigButton"))
                    //{
                    //    Helper.GetComboBox(form, "myFortComboBox" + i).Text = CONSTANTS.WAR_OFFENSE_TOWERS;
                    //    Helper.GetComboBox(form, "enemyFortComboBox" + i).Text = CONSTANTS.WAR_DEFENSE_TOWERS;
                    //    //Helper.GetComboBox(form,"yourBgeComboBox" + i).Text = "";
                    //    //Helper.GetComboBox(form,"enemyBgeComboBox" + i).Text = "";

                    //    Helper.GetComboBox(form, "gameModeComboBox" + i).Text = "Surge - Random";
                    //    Helper.GetComboBox(form, "gameOperationComboBox" + i).Text = "climb";
                    //    Helper.GetComboBox(form, "gameIterationsComboBox" + i).Text = "250";

                    //    Helper.GetTextBox(form, "gameMISTextBox" + i).Text = "0.001";
                    //    Helper.GetTextBox(form, "gameDeckLimitLowTextBox" + i).Text = "";
                    //    Helper.GetTextBox(form, "gameDeckLimitHighTextBox" + i).Text = "";

                    //    // Set gauntlet to war
                    //    if (Helper.GetComboBox(form, "gauntletCustomDecksComboBox" + i).Text != "warbig")
                    //    {
                    //        Helper.GetComboBox(form, "gauntletCustomDecksComboBox" + i).Text = "warbig";
                    //        _ControlSetup.RefreshGauntlets(form);
                    //    }
                    //}
                    //else if (controlName.Contains("presetWarBigDefButton"))
                    //{
                    //    Helper.GetComboBox(form, "myFortComboBox" + i).Text = CONSTANTS.WAR_DEFENSE_TOWERS;
                    //    Helper.GetComboBox(form, "enemyFortComboBox" + i).Text = CONSTANTS.WAR_OFFENSE_TOWERS;
                    //    //Helper.GetComboBox(form,"yourBgeComboBox" + i).Text = "";
                    //    //Helper.GetComboBox(form,"enemyBgeComboBox" + i).Text = "";

                    //    Helper.GetComboBox(form, "gameModeComboBox" + i).Text = "Defense - FlexFast";
                    //    Helper.GetComboBox(form, "gameOperationComboBox" + i).Text = "climb";
                    //    Helper.GetComboBox(form, "gameIterationsComboBox" + i).Text = "25";

                    //    Helper.GetTextBox(form, "gameMISTextBox" + i).Text = "0.005";
                    //    Helper.GetTextBox(form, "gameDeckLimitLowTextBox" + i).Text = "";
                    //    Helper.GetTextBox(form, "gameDeckLimitHighTextBox" + i).Text = "";

                    //    // Set gauntlet to war
                    //    if (Helper.GetComboBox(form, "gauntletCustomDecksComboBox" + i).Text != "warbig")
                    //    {
                    //        Helper.GetComboBox(form, "gauntletCustomDecksComboBox" + i).Text = "warbig";
                    //        _ControlSetup.RefreshGauntlets(form);
                    //    }
                    //}
                    else if (controlName.Contains("presetCQButton"))
                    {
                        Helper.GetComboBox(form, "myFortComboBox" + i).Text = CONSTANTS.CONQUEST_TOWERS;
                        Helper.GetComboBox(form, "enemyFortComboBox" + i).Text = CONSTANTS.CONQUEST_TOWERS;
                        Helper.GetComboBox(form, "yourBgeComboBox" + i).Text = "";
                        Helper.GetComboBox(form, "enemyBgeComboBox" + i).Text = "";

                        Helper.GetComboBox(form, "gameModeComboBox" + i).Text = "Surge - Ordered";
                        Helper.GetComboBox(form, "gameOperationComboBox" + i).Text = "climb";
                        Helper.GetComboBox(form, "gameIterationsComboBox" + i).Text = "250";

                        Helper.GetTextBox(form, "gameMISTextBox" + i).Text = "0.01";
                        Helper.GetTextBox(form, "gameDeckLimitLowTextBox" + i).Text = "";
                        Helper.GetTextBox(form, "gameDeckLimitHighTextBox" + i).Text = "";

                        // Set gauntlet to cq
                        if (Helper.GetComboBox(form, "gauntletCustomDecksComboBox" + i).Text != "cq")
                        {
                            Helper.GetComboBox(form, "gauntletCustomDecksComboBox" + i).Text = "cq";
                            _ControlSetup.RefreshGauntlets(form);
                        }

                    }
                    else if (controlName.Contains("presetCQDefButton"))
                    {
                        Helper.GetComboBox(form, "myFortComboBox" + i).Text = CONSTANTS.CONQUEST_TOWERS;
                        Helper.GetComboBox(form, "enemyFortComboBox" + i).Text = CONSTANTS.CONQUEST_TOWERS;
                        Helper.GetComboBox(form, "yourBgeComboBox" + i).Text = "";
                        Helper.GetComboBox(form, "enemyBgeComboBox" + i).Text = "";

                        Helper.GetComboBox(form, "gameModeComboBox" + i).Text = "Defense - Flex";
                        Helper.GetComboBox(form, "gameOperationComboBox" + i).Text = "climb";
                        Helper.GetComboBox(form, "gameIterationsComboBox" + i).Text = "35";

                        Helper.GetTextBox(form, "gameMISTextBox" + i).Text = "0.005";
                        Helper.GetTextBox(form, "gameDeckLimitLowTextBox" + i).Text = "";
                        Helper.GetTextBox(form, "gameDeckLimitHighTextBox" + i).Text = "";

                        // Set gauntlet to cq
                        if (Helper.GetComboBox(form, "gauntletCustomDecksComboBox" + i).Text != "cq")
                        {
                            Helper.GetComboBox(form, "gauntletCustomDecksComboBox" + i).Text = "cq";
                            _ControlSetup.RefreshGauntlets(form);
                        }

                    }
                    else if (controlName.Contains("presetRaidButton"))
                    {
                        Helper.GetTextBox(form, "enemyDeckTextBox" + i).Text = CONSTANTS.CURRENT_RAID;
                        Helper.GetComboBox(form, "myFortComboBox" + i).Text = "LC#2";
                        Helper.GetComboBox(form, "enemyFortComboBox" + i).Text = "";
                        Helper.GetComboBox(form, "yourBgeComboBox" + i).Text = "";
                        Helper.GetComboBox(form, "enemyBgeComboBox" + i).Text = "";

                        Helper.GetComboBox(form, "gameModeComboBox" + i).Text = "Battle - Random";
                        Helper.GetComboBox(form, "gameOperationComboBox" + i).Text = "climb";
                        Helper.GetComboBox(form, "gameIterationsComboBox" + i).Text = "10000";

                        Helper.GetTextBox(form, "gameMISTextBox" + i).Text = "";
                        Helper.GetTextBox(form, "gameDeckLimitLowTextBox" + i).Text = "";
                        Helper.GetTextBox(form, "gameDeckLimitHighTextBox" + i).Text = "";
                    }
                    else if (controlName.Contains("presetPvpButton"))
                    {
                        Helper.GetTextBox(form, "enemyDeckTextBox" + i).Text = "";
                        Helper.GetComboBox(form, "myFortComboBox" + i).Text = "";
                        Helper.GetComboBox(form, "enemyFortComboBox" + i).Text = "";
                        Helper.GetComboBox(form, "yourBgeComboBox" + i).Text = "";
                        Helper.GetComboBox(form, "enemyBgeComboBox" + i).Text = "";

                        Helper.GetComboBox(form, "gameModeComboBox" + i).Text = "Battle - Random";
                        Helper.GetComboBox(form, "gameOperationComboBox" + i).Text = "climb";
                        Helper.GetComboBox(form, "gameIterationsComboBox" + i).Text = "500";

                        Helper.GetTextBox(form, "gameMISTextBox" + i).Text = "0.01";
                        Helper.GetTextBox(form, "gameDeckLimitLowTextBox" + i).Text = "";
                        Helper.GetTextBox(form, "gameDeckLimitHighTextBox" + i).Text = "";

                        // Set gauntlet to pvp
                        if (Helper.GetComboBox(form, "gauntletCustomDecksComboBox" + i).Text != "pvp")
                        {
                            Helper.GetComboBox(form, "gauntletCustomDecksComboBox" + i).Text = "pvp";
                            _ControlSetup.RefreshGauntlets(form);
                        }
                    }

                    // Order
                    if (controlName.Contains("presetSurgeOrderButton"))
                    {
                        Helper.GetComboBox(form, "gameModeComboBox" + i).Text = "Surge - Ordered";
                        Helper.GetComboBox(form, "gameOperationComboBox" + i).Text = "climb";
                        return;
                    }
                    // Random surge
                    if (controlName.Contains("presetSurgeRandomButton"))
                    {
                        Helper.GetComboBox(form, "gameModeComboBox" + i).Text = "Surge - Random";
                        Helper.GetComboBox(form, "gameOperationComboBox" + i).Text = "climb";
                        return;
                    }
                    // Reorder
                    if (controlName.Contains("presetReorderButton")) 
                    {
                        Helper.GetComboBox(form, "gameModeComboBox" + i).Text = "Surge - Ordered";
                        Helper.GetComboBox(form, "gameOperationComboBox" + i).Text = "reorder";
                        return;                        
                    }

                    // Raid button - don't select a gauntlet by default
                    if (controlName.Contains("RaidButton")) return;

                    // Get the gauntlet names
                    ListBox gauntletBox = Helper.GetListBox(form, "gauntletListBox" + i);

                    // Defense gauntlet, set the gauntlet to the first instance of a gauntlet name containing "_atk"
                    if (controlName.Contains("DefButton"))
                    {
                        for(int k=0; k<=gauntletBox.Items.Count; k++)
                        {
                            if (gauntletBox.Items[k].ToString().ToLower().Contains("_atk"))
                            {
                                gauntletBox.SelectedIndex = k;
                                return;
                            }

                        }
                    }
                    // Attack gauntlet, set the gauntlet to the first instance of a gauntlet name containing "_def"
                    else
                    { 
                        for (int k = 0; k <= gauntletBox.Items.Count; k++)
                        {
                            if (gauntletBox.Items[k].ToString().ToLower().Contains("_def"))
                            {
                                gauntletBox.SelectedIndex = k;
                                return;
                            }

                        }
                    }
                }

                // ----------------
                // Batchsim Tab
                // ----------------
                else if (i == 4)
                {
                    if (controlName.Contains("presetBrawlButton"))
                    {
                        Helper.GetComboBox(form, "batchSimMyFortComboBox").Text = "";
                        Helper.GetComboBox(form, "batchSimEnemyFortComboBox").Text = "";
                        Helper.GetComboBox(form, "batchSimYourBgeComboBox").Text = "";
                        Helper.GetComboBox(form, "batchSimEnemyBgeComboBox").Text = "";

                        Helper.GetComboBox(form, "batchSimGameModeComboBox").Text = "Brawl - Random";
                        Helper.GetComboBox(form, "batchSimGameOperationComboBox").Text = "climb";
                        Helper.GetComboBox(form, "batchSimIterationsComboBox").Text = "500";

                        Helper.GetTextBox(form, "batchSimMisTextBox").Text = "0.001";
                        Helper.GetTextBox(form, "batchSimDeckSizeLowTextBox").Text = "8";
                        Helper.GetTextBox(form, "batchSimDeckSizeHighTextBox").Text = "10";

                        Helper.GetComboBox(form, "batchSimCustomDecksComboBox").Text = "brawl";

                    }
                    else if (controlName.Contains("presetBrawlDefButton"))
                    {
                        Helper.GetComboBox(form, "batchSimMyFortComboBox").Text = "";
                        Helper.GetComboBox(form, "batchSimEnemyFortComboBox").Text = "";
                        Helper.GetComboBox(form, "batchSimYourBgeComboBox").Text = "";
                        Helper.GetComboBox(form, "batchSimEnemyBgeComboBox").Text = "";

                        Helper.GetComboBox(form, "batchSimGameModeComboBox").Text = "Defense - FlexFast";
                        Helper.GetComboBox(form, "batchSimGameOperationComboBox").Text = "climb";
                        Helper.GetComboBox(form, "batchSimIterationsComboBox").Text = "25";

                        Helper.GetTextBox(form, "batchSimMisTextBox").Text = "0.005";
                        Helper.GetTextBox(form, "batchSimDeckSizeLowTextBox").Text = "";
                        Helper.GetTextBox(form, "batchSimDeckSizeHighTextBox").Text = "";

                        Helper.GetComboBox(form, "batchSimCustomDecksComboBox").Text = "brawl";

                    }
                    else if (controlName.Contains("presetWarButton"))
                    {
                        Helper.GetComboBox(form, "batchSimMyFortComboBox").Text = CONSTANTS.WAR_OFFENSE_TOWERS;
                        Helper.GetComboBox(form, "batchSimEnemyFortComboBox").Text = CONSTANTS.WAR_DEFENSE_TOWERS;
                        //Helper.GetComboBox(form, "batchSimYourBgeComboBox").Text = "";
                        //Helper.GetComboBox(form, "batchSimEnemyBgeComboBox").Text = "";

                        Helper.GetComboBox(form, "batchSimGameModeComboBox").Text = "Surge - Random";
                        Helper.GetComboBox(form, "batchSimGameOperationComboBox").Text = "climb";
                        Helper.GetComboBox(form, "batchSimIterationsComboBox").Text = "250";

                        Helper.GetTextBox(form, "batchSimMisTextBox").Text = "0.001";
                        Helper.GetTextBox(form, "batchSimDeckSizeLowTextBox").Text = "";
                        Helper.GetTextBox(form, "batchSimDeckSizeHighTextBox").Text = "";

                        Helper.GetComboBox(form, "batchSimCustomDecksComboBox").Text = "war";

                    }
                    else if (controlName.Contains("presetWarDefButton"))
                    {
                        Helper.GetComboBox(form, "batchSimMyFortComboBox").Text = CONSTANTS.WAR_DEFENSE_TOWERS;
                        Helper.GetComboBox(form, "batchSimEnemyFortComboBox").Text = CONSTANTS.WAR_OFFENSE_TOWERS;
                        //Helper.GetComboBox(form, "batchSimYourBgeComboBox").Text = "";
                        //Helper.GetComboBox(form, "batchSimEnemyBgeComboBox").Text = "";

                        Helper.GetComboBox(form, "batchSimGameModeComboBox").Text = "Defense - FlexFast";
                        Helper.GetComboBox(form, "batchSimGameOperationComboBox").Text = "climb";
                        Helper.GetComboBox(form, "batchSimIterationsComboBox").Text = "25";

                        Helper.GetTextBox(form, "batchSimMisTextBox").Text = "0.005";
                        Helper.GetTextBox(form, "batchSimDeckSizeLowTextBox").Text = "";
                        Helper.GetTextBox(form, "batchSimDeckSizeHighTextBox").Text = "";

                        Helper.GetComboBox(form, "batchSimCustomDecksComboBox").Text = "war";
                    }
                    else if (controlName.Contains("presetCQButton"))
                    {
                        Helper.GetComboBox(form, "batchSimMyFortComboBox").Text = CONSTANTS.CONQUEST_TOWERS;
                        Helper.GetComboBox(form, "batchSimEnemyFortComboBox").Text = CONSTANTS.CONQUEST_TOWERS;
                        Helper.GetComboBox(form, "batchSimYourBgeComboBox").Text = "";
                        Helper.GetComboBox(form, "batchSimEnemyBgeComboBox").Text = "";

                        Helper.GetComboBox(form, "batchSimGameModeComboBox").Text = "Surge - Random";
                        Helper.GetComboBox(form, "batchSimGameOperationComboBox").Text = "climb";
                        Helper.GetComboBox(form, "batchSimIterationsComboBox").Text = "250";

                        Helper.GetTextBox(form, "batchSimMisTextBox").Text = "0.001";
                        Helper.GetTextBox(form, "batchSimDeckSizeLowTextBox").Text = "";
                        Helper.GetTextBox(form, "batchSimDeckSizeHighTextBox").Text = "";

                        Helper.GetComboBox(form, "batchSimCustomDecksComboBox").Text = "cq";

                    }
                    else if (controlName.Contains("presetCQDefButton"))
                    {
                        Helper.GetComboBox(form, "batchSimMyFortComboBox").Text = CONSTANTS.CONQUEST_TOWERS;
                        Helper.GetComboBox(form, "batchSimEnemyFortComboBox").Text = CONSTANTS.CONQUEST_TOWERS;
                        Helper.GetComboBox(form, "batchSimYourBgeComboBox").Text = "";
                        Helper.GetComboBox(form, "batchSimEnemyBgeComboBox").Text = "";

                        Helper.GetComboBox(form, "batchSimGameModeComboBox").Text = "Defense - FlexFast";
                        Helper.GetComboBox(form, "batchSimGameOperationComboBox").Text = "climb";
                        Helper.GetComboBox(form, "batchSimIterationsComboBox").Text = "25";

                        Helper.GetTextBox(form, "batchSimMisTextBox").Text = "0.005";
                        Helper.GetTextBox(form, "batchSimDeckSizeLowTextBox").Text = "";
                        Helper.GetTextBox(form, "batchSimDeckSizeHighTextBox").Text = "";

                        Helper.GetComboBox(form, "batchSimCustomDecksComboBox").Text = "cq";

                    }
                    else if (controlName.Contains("presetRaidButton"))
                    {
                        Helper.GetComboBox(form, "batchSimMyFortComboBox").Text = "LC#2";
                        Helper.GetComboBox(form, "batchSimEnemyFortComboBox").Text = "";
                        Helper.GetComboBox(form, "batchSimYourBgeComboBox").Text = "";
                        Helper.GetComboBox(form, "batchSimEnemyBgeComboBox").Text = "";

                        Helper.GetComboBox(form, "batchSimGameModeComboBox").Text = "Battle - Random";
                        Helper.GetComboBox(form, "batchSimGameOperationComboBox").Text = "climb";
                        Helper.GetComboBox(form, "batchSimIterationsComboBox").Text = "10000";

                        Helper.GetTextBox(form, "batchSimMisTextBox").Text = "0.001";
                        Helper.GetTextBox(form, "batchSimDeckSizeLowTextBox").Text = "";
                        Helper.GetTextBox(form, "batchSimDeckSizeHighTextBox").Text = "";

                        Helper.GetTextBox(form, "batchSimPvEGauntletTextBox").Text = CONSTANTS.CURRENT_RAID;
                    }

                    _ControlSetup.RefreshGauntlets(form);


                    // Raid button - don't select a gauntlet by default
                    if (controlName.Contains("RaidButton")) return;
                    
                    // Defense gauntlet, set the gauntlet to the first instance of a gauntlet name containing "_atk"
                    if (controlName.Contains("DefButton"))
                    {
                        for (int k = 0; k <= form.batchSimGauntletListBox.Items.Count; k++)
                        {
                            if (form.batchSimGauntletListBox.Items[k].ToString().ToLower().Contains("_atk"))
                            {
                                form.batchSimGauntletListBox.SelectedIndex = k;
                                return;
                            }

                        }
                    }
                    // Attack gauntlet, set the gauntlet to the first instance of a gauntlet name containing "_def"
                    else
                    {
                        for (int k = 0; k <= form.batchSimGauntletListBox.Items.Count; k++)
                        {
                            if (form.batchSimGauntletListBox.Items[k].ToString().ToLower().Contains("_def"))
                            {
                                form.batchSimGauntletListBox.SelectedIndex = k;
                                return;
                            }

                        }
                    }
                }
            }
            catch(Exception ex)
            {
                Helper.OutputWindowMessage(form, ex.Message, "Avast! Thar be an error on the SimShortcuts() method! Yell at Reck for this one");
            }
        }
        

        #endregion

        #region Helpers

        /// <summary>
        /// Reads settings from appsettings.txt. Failing that, it will use app.config
        /// </summary>
        private static void ReadFromAppSettings(MainForm form)
        {
            List<string> settings = new List<string>();
            List<string> targetSettings = new List<string>();
            string setting;
            string targetSetting;


            try
            {
                settings = FileIO.SimpleRead(form, "config/appsettings.txt", returnCommentedLines: true);
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(form, ex.Message, "ReadFromAppSettings() error: Could not open config/appsettings.txt.\r\n" + ex.Message);
            }

            // ---------------------------------------------------------------------
            // Get latest ptuo version, and alert if our version is out of date
            // ---------------------------------------------------------------------
            try
            { 
                targetSetting = "latestptuoversion:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    setting = setting.Replace(targetSetting, "");
                    double.TryParse(setting, out CONSTANTS.LATEST_PTUO_VERSION);
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(form, ex.Message, "ReadFromAppSettings() error: Could not find the latestptuoversion line.\r\n" + ex.Message);
            }

            // ---------------------------------------------------------------------
            // Get hash and version. We have a default if not
            // ---------------------------------------------------------------------
            try
            {
                targetSetting = "hashCode:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    setting = setting.Replace(targetSetting, "");
                    CONSTANTS.hashCode = setting;
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(form, ex.Message, "ReadFromAppSettings() error: Could not find the hashCode line.\r\n" + ex.Message);
            }

            try
            {
                targetSetting = "clientVersion:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    setting = setting.Replace(targetSetting, "");
                    CONSTANTS.clientVersion = setting;
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(form, ex.Message, "ReadFromAppSettings() error: Could not find the clientVersion line.\r\n" + ex.Message);
            }

            // ---------------------------------------------------------------------
            // Get mission IDs we care about
            // ---------------------------------------------------------------------
            try
            {
                targetSetting = "sideMissions:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    CONSTANTS.SIDE_MISSIONS = setting.Replace(targetSetting, "").Split(',').ToList();
                }

                targetSetting = "tempMissions:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    CONSTANTS.TEMP_MISSIONS = setting.Replace(targetSetting, "").Split(',').ToList();
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(form, ex.Message, "ReadFromAppSettings() error: Could not find the side/temp missions line.\r\n" + ex.Message);
            }

            // ---------------------------------------------------------------------
            // Get raid assaults for livesimming
            // ---------------------------------------------------------------------
            try
            {
                targetSetting = "currentRaidAssaults:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    CONSTANTS.CURRENT_RAID_ASSAULTS = setting.Replace(targetSetting, "").Split(',').ToList();
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(form, ex.Message, "ReadFromAppSettings() error: Could not find the current raid line.\r\n" + ex.Message);
            }


            // ---------------------------------------------------------------------
            // Get current TopX guilds
            // ---------------------------------------------------------------------
            try
            {
                targetSetting = "top5Guilds:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    CONSTANTS.GUILDS_TOP5 = setting.Replace(targetSetting, "").Split(',').ToList();
                    CONSTANTS.GUILDS_TOP5 = CONSTANTS.GUILDS_TOP5.Select(x => x.Trim()).ToList();
                }
                targetSetting = "top10Guilds:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    CONSTANTS.GUILDS_TOP10 = setting.Replace(targetSetting, "").Split(',').ToList();
                    CONSTANTS.GUILDS_TOP10 = CONSTANTS.GUILDS_TOP10.Select(x => x.Trim()).ToList();
                }
                targetSetting = "top25Guilds:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    CONSTANTS.GUILDS_TOP25 = setting.Replace(targetSetting, "").Split(',').ToList();
                    CONSTANTS.GUILDS_TOP25 = CONSTANTS.GUILDS_TOP25.Select(x => x.Trim()).ToList();
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(form, ex.Message, "ReadFromAppSettings() error: Could not find the current raid line.\r\n" + ex.Message);
            }

            // ---------------------------------------------------------------------
            // Load settings for form dropdowns
            // ---------------------------------------------------------------------

            // Dominions, Commanders, Fortresses, BGEs
            try
            {
                targetSetting = "dominions:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    CONSTANTS.DOMINIONS = setting.Replace(targetSetting, "").Split('|').ToList();
                }
                // Identify commander quads
                targetSetting = "commanderQuads:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    CONSTANTS.COMMANDER_QUADS = setting.Replace(targetSetting, "").Split('|').ToList();
                }

                // Forts in dropdown
                targetSetting = "warForts:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    warForts = setting.Replace(targetSetting, "").Split('|').ToList();
                }
                targetSetting = "cqForts:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    cqForts = setting.Replace(targetSetting, "").Split('|').ToList();
                }
                // Cq zones currently active
                targetSetting = "cqActiveZones:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    cqActiveZones = setting.Replace(targetSetting, "").Split('|').ToList();
                }

                // ** ADMIN TOOL **
                // Guilds with IDs
                targetSetting = "GuildIds:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    guildIds = setting.Replace(targetSetting, "").Split('|').ToList();
                }
                targetSetting = "PlayerIds:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    playerIds = setting.Replace(targetSetting, "").Split('|').ToList();
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(form, ex.Message, "ReadFromAppSettings() error: Could not find the game modes/operations line.\r\n" + ex.Message);
            }

            // Salvage list
            try
            {
                // BGEs in dropdown
                targetSetting = "bges:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    bges = setting.Replace(targetSetting, "").Split('|').ToList();
                }

                targetSetting = "cqBges:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    cqBges = setting.Replace(targetSetting, "").Split('|').ToList();
                }

                targetSetting = "warBges:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    warbges = setting.Replace(targetSetting, "").Split('|').ToList();
                }

                // Game modes/operations in dropdown
                targetSetting = "modes:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    modes = setting.Replace(targetSetting, "").Split('|').ToList();
                }

                targetSetting = "operations:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    operations = setting.Replace(targetSetting, "").Split('|').ToList();
                }

                // Lists of old cards to salvage
                targetSetting = "baseEpics:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    CONSTANTS.BASE_EPICS = setting.Replace(targetSetting, "").Split(',').Select(x => x.Trim()).ToList();
                }
                targetSetting = "baseLegends:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    CONSTANTS.BASE_LEGENDS = setting.Replace(targetSetting, "").Split(',').Select(x => x.Trim()).ToList();
                }
                targetSetting = "rewardsToSalvage:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    CONSTANTS.SALVAGE_REWARDS = setting.Replace(targetSetting, "").Split(',').Select(x => x.Trim()).ToList();
                }

                targetSettings = new List<string>
                {
                    "quadsDeepInventoryClean:",
                    "quadsDeepRewardsClean:",

                    "quadsBoxEpics:",
                    "quadsBoxLegends:",
                    "quadsBoxVinds:",
                    "quadsPvpEpics:",
                    "quadsPveEpics:",
                    "quadsPvpLegends:",
                    "quadsPveLegends:",
                    "quadsPvpVinds:",
                    "quadsPveVinds:",
                    "quadsFusionEpics:",
                    "quadsFusionLegends:",
                    "quadsFusionVinds:",

                };

                foreach (string ts in targetSettings)
                {
                    setting = settings.FirstOrDefault(x => x.StartsWith(ts));
                    if (setting != null)
                    {
                        CONSTANTS.SALVAGE_AGGRESSIVE.AddRange(setting.Replace(ts, "").Split(',').Select(x => x.Trim()).ToList());
                    }
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(form, ex.Message, "ReadFromAppSettings() error: Could not find the game modes/operations line.\r\n" + ex.Message);
            }

            // Gauntlet files in dropdown
            try
            { 
                targetSetting = "customdecks:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    customdecks = setting.Replace(targetSetting, "").Split('|').ToList();
                }
                targetSetting = "customdecksLevel2:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    customdecksLevel2 = setting.Replace(targetSetting, "").Split('|').ToList();
                }
                targetSetting = "customdecksLevel3:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    customdecksLevel3 = setting.Replace(targetSetting, "").Split('|').ToList();
                }

                // Seeds in dropdown
                targetSetting = "seeds:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    seeds = setting.Replace(targetSetting, "").Split('|').ToList();
                }

                targetSetting = "batchsimGuilds:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    batchSimGuilds = setting.Replace(targetSetting, "").Split('|').ToList();
                }

                targetSetting = "batchSimSpecialSims:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    batchSimSpecialSims = setting.Replace(targetSetting, "").Split('|').ToList();
                }

                // Towers used when clicking War/CQ preset button
                targetSetting = "defaultWarOffenseTowers:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    CONSTANTS.WAR_OFFENSE_TOWERS = setting.Replace(targetSetting, "");
                }

                targetSetting = "defaultWarDefenseTowers:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    CONSTANTS.WAR_DEFENSE_TOWERS = setting.Replace(targetSetting, "");
                }

                targetSetting = "defaultCqTowers:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    CONSTANTS.CONQUEST_TOWERS = setting.Replace(targetSetting, "");
                }

                targetSetting = "currentRaid:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    CONSTANTS.CURRENT_RAID = setting.Replace(targetSetting, "");
                }

                targetSetting = "currentRaid:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    CONSTANTS.CURRENT_RAID = setting.Replace(targetSetting, "");
                }


                targetSetting = "GeneticFree_";
                if (setting != null)
                {
                    CONSTANTS.GENETIC_FREE_DECKS = settings.Where(x => x.StartsWith(targetSetting)).ToList();
                }

                targetSetting = "GeneticDolphin_";
                if (setting != null)
                {
                    CONSTANTS.GENETIC_DOLPHIN_DECKS = settings.Where(x => x.StartsWith(targetSetting)).ToList();
                }

                targetSetting = "GeneticWhale_";
                if (setting != null)
                {
                    CONSTANTS.GENETIC_WHALE_DECKS = settings.Where(x => x.StartsWith(targetSetting)).ToList();
                }
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(form, ex.Message, "ReadFromAppSettings() error: Could not find misc lines.\r\n" + ex.Message);
            }

            // List of "big cards" to look for
            try
            {
                //targetSetting = "bigCardsMythic:";
                //setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                //if (setting != null)
                //{
                //    CONSTANTS.BIGCARDS_MYTHIC = setting.Replace(targetSetting, "").Split(',').ToList();
                //}
                //targetSetting = "bigCardsVindi:";
                //setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                //if (setting != null)
                //{
                //    CONSTANTS.BIGCARDS_VINDI = setting.Replace(targetSetting, "").Split(',').ToList();
                //}
                //targetSetting = "bigCardsChance:";
                //setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                //if (setting != null)
                //{
                //    CONSTANTS.BIGCARDS_CHANCE = setting.Replace(targetSetting, "").Split(',').ToList();
                //}
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(form, ex.Message, "ReadFromAppSettings() error: Could not find bigCards lines.\r\n" + ex.Message);
            }
        }

        /// <summary>
        /// Sim Tabs 1, 2, and 3
        /// </summary>
        private static void PopulateSimTabs(MainForm form)
        {
            try
            {
                // --------------------------
                // Restore previous settings
                // --------------------------

                // Pirate TUO options
                CONFIG.minimizeToTray = Properties.Settings.Default["minimizeToTray"].ToString() == "true" ? true : false;


                // Sim Tabs
                form.myDeckTextBox1.Text = Properties.Settings.Default["myDeckTextBox1"].ToString();
                form.enemyDeckTextBox1.Text = Properties.Settings.Default["enemyDeckTextBox1"].ToString();
                form.myFortComboBox1.Text = Properties.Settings.Default["myFortComboBox1"].ToString();
                form.enemyFortComboBox1.Text = Properties.Settings.Default["enemyFortComboBox1"].ToString();
                form.myDominionComboBox1.Text = Properties.Settings.Default["myDominionComboBox1"].ToString();
                form.bgeComboBox1.Text = Properties.Settings.Default["bgeComboBox1"].ToString();
                form.yourBgeComboBox1.Text = Properties.Settings.Default["yourBgeComboBox1"].ToString();
                form.enemyBgeComboBox1.Text = Properties.Settings.Default["enemyBgeComboBox1"].ToString();
                form.gameModeComboBox1.Text = Properties.Settings.Default["gameModeComboBox1"].ToString();
                form.gameOperationComboBox1.Text = Properties.Settings.Default["gameOperationComboBox1"].ToString();
                form.gameIterationsComboBox1.Text = Properties.Settings.Default["gameIterationsComboBox1"].ToString();
                form.gameMISTextBox1.Text = Properties.Settings.Default["gameMISTextBox1"].ToString();
                form.gameDeckLimitLowTextBox1.Text = Properties.Settings.Default["gameDeckLimitLowTextBox1"].ToString();
                form.gameDeckLimitHighTextBox1.Text = Properties.Settings.Default["gameDeckLimitHighTextBox1"].ToString();
                //form.gameHarmonicMeanCheckBox1.Checked = Properties.Settings.Default["gameHarmonicMeanCheckBox1"].ToString() == "true" ? true : false;
                form.gameFundTextBox1.Text = Properties.Settings.Default["gameFundTextBox1"].ToString();
                form.gameExtraFlagsTextBox1.Text = Properties.Settings.Default["gameExtraFlagsTextBox1"].ToString();
                form.gameTUODebugCheckBox1.Checked = Properties.Settings.Default["gameTUODebugCheckBox1"].ToString() == "true" ? true : false;
                form.gameCPUThreadsTextBox1.Text = Properties.Settings.Default["gameCPUThreadsTextBox1"].ToString();
                form.gameCPUx86CheckBox1.Checked = Properties.Settings.Default["gameCPUx86CheckBox1"].ToString() == "true" ? true : false;
                form.gauntletCustomDecksComboBox1.Text = Properties.Settings.Default["gauntletCustomDecksComboBox1"].ToString();

                form.myDeckTextBox2.Text = Properties.Settings.Default["myDeckTextBox2"].ToString();
                form.enemyDeckTextBox2.Text = Properties.Settings.Default["enemyDeckTextBox2"].ToString();
                form.myFortComboBox2.Text = Properties.Settings.Default["myFortComboBox2"].ToString();
                form.enemyFortComboBox2.Text = Properties.Settings.Default["enemyFortComboBox2"].ToString();
                form.myDominionComboBox2.Text = Properties.Settings.Default["myDominionComboBox2"].ToString();
                form.bgeComboBox2.Text = Properties.Settings.Default["bgeComboBox2"].ToString();
                form.yourBgeComboBox2.Text = Properties.Settings.Default["yourBgeComboBox2"].ToString();
                form.enemyBgeComboBox2.Text = Properties.Settings.Default["enemyBgeComboBox2"].ToString();
                form.gameModeComboBox2.Text = Properties.Settings.Default["gameModeComboBox2"].ToString();
                form.gameOperationComboBox2.Text = Properties.Settings.Default["gameOperationComboBox2"].ToString();
                form.gameIterationsComboBox2.Text = Properties.Settings.Default["gameIterationsComboBox2"].ToString();
                form.gameMISTextBox2.Text = Properties.Settings.Default["gameMISTextBox2"].ToString();
                form.gameDeckLimitLowTextBox2.Text = Properties.Settings.Default["gameDeckLimitLowTextBox2"].ToString();
                form.gameDeckLimitHighTextBox2.Text = Properties.Settings.Default["gameDeckLimitHighTextBox2"].ToString();
                //form.gameHarmonicMeanCheckBox2.Checked = Properties.Settings.Default["gameHarmonicMeanCheckBox2"].ToString() == "true" ? true : false;
                form.gameFundTextBox2.Text = Properties.Settings.Default["gameFundTextBox2"].ToString();
                form.gameExtraFlagsTextBox2.Text = Properties.Settings.Default["gameExtraFlagsTextBox2"].ToString();
                form.gameTUODebugCheckBox2.Checked = Properties.Settings.Default["gameTUODebugCheckBox2"].ToString() == "true" ? true : false;
                form.gameCPUThreadsTextBox2.Text = Properties.Settings.Default["gameCPUThreadsTextBox2"].ToString();
                form.gameCPUx86CheckBox2.Checked = Properties.Settings.Default["gameCPUx86CheckBox2"].ToString() == "true" ? true : false;
                form.gauntletCustomDecksComboBox2.Text = Properties.Settings.Default["gauntletCustomDecksComboBox2"].ToString();

                form.myDeckTextBox3.Text = Properties.Settings.Default["myDeckTextBox3"].ToString();
                form.enemyDeckTextBox3.Text = Properties.Settings.Default["enemyDeckTextBox3"].ToString();
                form.myFortComboBox3.Text = Properties.Settings.Default["myFortComboBox3"].ToString();
                form.enemyFortComboBox3.Text = Properties.Settings.Default["enemyFortComboBox3"].ToString();
                form.myDominionComboBox3.Text = Properties.Settings.Default["myDominionComboBox3"].ToString();
                form.bgeComboBox3.Text = Properties.Settings.Default["bgeComboBox3"].ToString();
                form.yourBgeComboBox3.Text = Properties.Settings.Default["yourBgeComboBox3"].ToString();
                form.enemyBgeComboBox3.Text = Properties.Settings.Default["enemyBgeComboBox3"].ToString();
                form.gameModeComboBox3.Text = Properties.Settings.Default["gameModeComboBox3"].ToString();
                form.gameOperationComboBox3.Text = Properties.Settings.Default["gameOperationComboBox3"].ToString();
                form.gameIterationsComboBox3.Text = Properties.Settings.Default["gameIterationsComboBox3"].ToString();
                form.gameMISTextBox3.Text = Properties.Settings.Default["gameMISTextBox3"].ToString();
                form.gameDeckLimitLowTextBox3.Text = Properties.Settings.Default["gameDeckLimitLowTextBox3"].ToString();
                form.gameDeckLimitHighTextBox3.Text = Properties.Settings.Default["gameDeckLimitHighTextBox3"].ToString();
                //form.gameHarmonicMeanCheckBox3.Checked = Properties.Settings.Default["gameHarmonicMeanCheckBox3"].ToString() == "true" ? true : false;
                form.gameFundTextBox3.Text = Properties.Settings.Default["gameFundTextBox3"].ToString();
                form.gameExtraFlagsTextBox3.Text = Properties.Settings.Default["gameExtraFlagsTextBox3"].ToString();
                form.gameTUODebugCheckBox3.Checked = Properties.Settings.Default["gameTUODebugCheckBox3"].ToString() == "true" ? true : false;
                form.gameCPUThreadsTextBox3.Text = Properties.Settings.Default["gameCPUThreadsTextBox3"].ToString();
                form.gameCPUx86CheckBox3.Checked = Properties.Settings.Default["gameCPUx86CheckBox3"].ToString() == "true" ? true : false;
                form.gauntletCustomDecksComboBox3.Text = Properties.Settings.Default["gauntletCustomDecksComboBox3"].ToString();



                // --------------------------
                // Add items to comboboxes
                // - Set the default value first to improve loadtime
                // --------------------------

                // Dominions
                
                form.myDominionComboBox1.Items.AddRange(CONSTANTS.DOMINIONS.ToArray());
                form.myDominionComboBox2.Items.AddRange(CONSTANTS.DOMINIONS.ToArray());
                form.myDominionComboBox3.Items.AddRange(CONSTANTS.DOMINIONS.ToArray());
                form.batchSimDominionListBox.Items.AddRange(CONSTANTS.DOMINIONS.ToArray());
                form.crowsNestResetDominionComboBox.Items.AddRange(CONSTANTS.DOMINIONS.ToArray());

                // Forts
                form.myFortComboBox1.Items.AddRange(warForts.ToArray());
                form.myFortComboBox2.Items.AddRange(warForts.ToArray());
                form.myFortComboBox3.Items.AddRange(warForts.ToArray());
                form.myFortComboBox1.Items.AddRange(cqForts.ToArray());
                form.myFortComboBox2.Items.AddRange(cqForts.ToArray());
                form.myFortComboBox3.Items.AddRange(cqForts.ToArray());
                form.enemyFortComboBox1.Items.AddRange(warForts.ToArray());
                form.enemyFortComboBox2.Items.AddRange(warForts.ToArray());
                form.enemyFortComboBox3.Items.AddRange(warForts.ToArray());
                form.enemyFortComboBox1.Items.AddRange(cqForts.ToArray());
                form.enemyFortComboBox2.Items.AddRange(cqForts.ToArray());
                form.enemyFortComboBox3.Items.AddRange(cqForts.ToArray());

                form.batchSimMyFortComboBox.Items.AddRange(warForts.ToArray());
                form.batchSimMyFortComboBox.Items.AddRange(cqForts.ToArray());
                form.batchSimEnemyFortComboBox.Items.AddRange(warForts.ToArray());
                form.batchSimEnemyFortComboBox.Items.AddRange(cqForts.ToArray());

                form.firstCouncilSimMyFortComboBox.Items.AddRange(warForts.ToArray());
                form.firstCouncilSimMyFortComboBox.Items.AddRange(cqForts.ToArray());
                form.firstCouncilSimEnemyFortComboBox.Items.AddRange(warForts.ToArray());
                form.firstCouncilSimEnemyFortComboBox.Items.AddRange(cqForts.ToArray());

                form.navSimMyFortComboBox.Items.AddRange(warForts.ToArray());
                form.navSimMyFortComboBox.Items.AddRange(cqForts.ToArray());
                form.navSimEnemyFortComboBox.Items.AddRange(warForts.ToArray());
                form.navSimEnemyFortComboBox.Items.AddRange(cqForts.ToArray());

                // BGEs
                form.bgeComboBox1.Items.AddRange(bges.ToArray());
                form.bgeComboBox2.Items.AddRange(bges.ToArray());
                form.bgeComboBox3.Items.AddRange(bges.ToArray());
                form.bgeComboBox1.Items.AddRange(cqBges.ToArray());
                form.bgeComboBox2.Items.AddRange(cqBges.ToArray());
                form.bgeComboBox3.Items.AddRange(cqBges.ToArray());
                form.yourBgeComboBox1.Items.AddRange(warbges.ToArray());
                form.yourBgeComboBox2.Items.AddRange(warbges.ToArray());
                form.yourBgeComboBox3.Items.AddRange(warbges.ToArray());
                form.enemyBgeComboBox1.Items.AddRange(warbges.ToArray());
                form.enemyBgeComboBox2.Items.AddRange(warbges.ToArray());
                form.enemyBgeComboBox3.Items.AddRange(warbges.ToArray());

                form.batchSimBgeComboBox.Items.AddRange(bges.ToArray());
                form.batchSimYourBgeComboBox.Items.AddRange(warbges.ToArray());
                form.batchSimEnemyBgeComboBox.Items.AddRange(warbges.ToArray());

                form.navSimBgeComboBox.Items.AddRange(bges.ToArray());
                form.navSimBgeComboBox.Items.AddRange(cqBges.ToArray());
                form.navSimYourBgeComboBox.Items.AddRange(bges.ToArray());
                form.navSimYourBgeComboBox.Items.AddRange(warbges.ToArray());
                form.navSimEnemyBgeComboBox.Items.AddRange(bges.ToArray());
                form.navSimEnemyBgeComboBox.Items.AddRange(warbges.ToArray());

                form.firstCouncilSimBgeComboBox.Items.AddRange(bges.ToArray());
                form.firstCouncilSimYourBgeComboBox.Items.AddRange(warbges.ToArray());
                form.firstCouncilSimEnemyBgeComboBox.Items.AddRange(warbges.ToArray());

                // Game mode
                form.gameModeComboBox1.Items.AddRange(modes.ToArray());
                form.gameModeComboBox2.Items.AddRange(modes.ToArray());
                form.gameModeComboBox3.Items.AddRange(modes.ToArray());
                form.batchSimGameModeComboBox.Items.AddRange(modes.ToArray());

                // Game operation
                form.gameOperationComboBox1.Items.AddRange(operations.ToArray());
                form.gameOperationComboBox2.Items.AddRange(operations.ToArray());
                form.gameOperationComboBox3.Items.AddRange(operations.ToArray());
                form.batchSimGameOperationComboBox.Items.AddRange(operations.ToArray());

                // Customdecks
                form.gauntletCustomDecksComboBox1.Items.AddRange(customdecks.ToArray());
                form.gauntletCustomDecksComboBox2.Items.AddRange(customdecks.ToArray());
                form.gauntletCustomDecksComboBox3.Items.AddRange(customdecks.ToArray());

                form.batchSimCustomDecksComboBox.Items.AddRange(customdecks.ToArray());
                form.navSimCustomDecksComboBox.Items.AddRange(customdecks.ToArray());
                form.firstCouncilSimCustomDecksComboBox.Items.AddRange(customdecks.ToArray());


                if (CONFIG.role == "level2" || CONFIG.role == "newLevel2")
                {
                    form.gauntletCustomDecksComboBox1.Items.AddRange(customdecksLevel2.ToArray());
                    form.gauntletCustomDecksComboBox2.Items.AddRange(customdecksLevel2.ToArray());
                    form.gauntletCustomDecksComboBox3.Items.AddRange(customdecksLevel2.ToArray());
                    form.batchSimCustomDecksComboBox.Items.AddRange(customdecksLevel2.ToArray());
                    form.navSimCustomDecksComboBox.Items.AddRange(customdecksLevel2.ToArray());
                    form.firstCouncilSimCustomDecksComboBox.Items.AddRange(customdecks.ToArray());
                }
                if (CONFIG.role == "level3" || CONFIG.role == "newLevel3")
                {
                    form.gauntletCustomDecksComboBox1.Items.AddRange(customdecksLevel3.ToArray());
                    form.gauntletCustomDecksComboBox2.Items.AddRange(customdecksLevel3.ToArray());
                    form.gauntletCustomDecksComboBox3.Items.AddRange(customdecksLevel3.ToArray());
                    form.batchSimCustomDecksComboBox.Items.AddRange(customdecksLevel3.ToArray());
                    form.navSimCustomDecksComboBox.Items.AddRange(customdecksLevel3.ToArray());
                    form.firstCouncilSimCustomDecksComboBox.Items.AddRange(customdecks.ToArray());
                }

                // Seeds
                form.inventorySeedDeckListBox1.Items.Add("none");
                form.inventorySeedDeckListBox2.Items.Add("none");
                form.inventorySeedDeckListBox3.Items.Add("none");
                form.inventorySeedDeckListBox1.Items.AddRange(seeds.ToArray());
                form.inventorySeedDeckListBox2.Items.AddRange(seeds.ToArray());
                form.inventorySeedDeckListBox3.Items.AddRange(seeds.ToArray());
                form.inventorySeedDeckListBox1.SelectedIndex = 0;
                form.inventorySeedDeckListBox2.SelectedIndex = 0;
                form.inventorySeedDeckListBox3.SelectedIndex = 0;

                // Admin inventory tab
                form.adminMiscCreateInventoryExtraFilteredCardsTextBox.Text = Properties.Settings.Default["adminMiscCreateInventoryExtraFilteredCardsTextBox"].ToString();

                // Player cards
                RefreshPlayers(form);

            }
            catch (DirectoryNotFoundException ex)
            {
                Helper.OutputWindowMessage(form, ex.Message, "Avast! Thar be an error on the PopulateSimTabs() method!");
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(form, ex.Message, "Avast! Thar be an error on the PopulateSimTabs() method!");
            }
        }

        /// <summary>
        /// Guild sim tab
        /// </summary>
        private static void PopulateBatchSimTab(MainForm form)
        {
            try
            {
                // --------------------------
                // Restore previous settings
                // --------------------------
                form.batchSimBgeComboBox.Text = Properties.Settings.Default["batchSimBgeComboBox"].ToString();
                form.batchSimYourBgeComboBox.Text = Properties.Settings.Default["batchSimYourBgeComboBox"].ToString();
                form.batchSimEnemyBgeComboBox.Text = Properties.Settings.Default["batchSimEnemyBgeComboBox"].ToString();

                form.batchSimMyFortComboBox.Text = Properties.Settings.Default["batchSimMyFortComboBox"].ToString();
                form.batchSimEnemyFortComboBox.Text = Properties.Settings.Default["batchSimEnemyFortComboBox"].ToString();

                form.batchSimCustomDecksComboBox.Text = Properties.Settings.Default["batchSimCustomDecksComboBox"].ToString();

                // First council - mimic batch sim setting
                form.firstCouncilSimCustomDecksComboBox.Text = Properties.Settings.Default["batchSimCustomDecksComboBox"].ToString();

                var items = Properties.Settings.Default["batchSimInventoryListBox"].ToString().Split(',');
                foreach (var item in items)
                {
                    if (String.IsNullOrEmpty(item)) continue;
                    form.batchSimInventoryListBox.SelectedItem = item;
                }
                items = Properties.Settings.Default["batchSimSeedListBox"].ToString().Split(',');
                foreach (var item in items)
                {
                    if (String.IsNullOrEmpty(item)) continue;
                    form.batchSimSeedListBox.SelectedItem = item;
                }

                form.queuedSimOutputFileTextBox.Text = Properties.Settings.Default["queuedSimOutputFileTextBox"].ToString();
                
                form.batchSimExtraFlagsTextBox.Text = Properties.Settings.Default["batchSimExtraFlagsTextBox"].ToString();
                form.batchSimDeckSizeLowTextBox.Text = Properties.Settings.Default["batchSimDeckSizeLowTextBox"].ToString();
                form.batchSimDeckSizeHighTextBox.Text = Properties.Settings.Default["batchSimDeckSizeHighTextBox"].ToString();

                //form.batchSimGameHarmonicMeanCheckBox.Checked = Properties.Settings.Default["batchSimGameHarmonicMeanCheckBox"].ToString() == "true" ? true : false;
                //form.batchSimGameHarmonicTuoDebugCheckBox.Checked =  Properties.Settings.Default["batchSimGameHarmonicTuoDebugCheckBox"].ToString() == "true" ? true : false;

                //form.batchSimSeedListBox.SelectedItem = Properties.Settings.Default["batchSimSeedComboBox"].ToString();


                // Shortcuts
                form.batchSimShortcutGuildComboBox.Items.AddRange(batchSimGuilds.ToArray());
                
                // Seeds
                form.batchSimSeedListBox.Items.AddRange(seeds.ToArray());

                // Special Game modes
                form.batchSimSpecialSimComboBox.Items.AddRange(batchSimSpecialSims.ToArray());

            }
            catch (DirectoryNotFoundException ex)
            {
                Helper.OutputWindowMessage(form, ex.Message, "Avast! Thar PopulateBatchSimTab() method could not find a directory! " + ex.Message);
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(form, ex.Message, "Avast! Thar PopulateBatchSimTab() method threw an error trying to restore previous input! " + ex.Message);
            }
        }

        /// <summary>
        /// Nav sim tab
        /// </summary>
        private static void PopulateNavSimTab(MainForm form)
        {
            try
            {
                // --------------------------
                // Restore previous settings
                // --------------------------
                form.navigatorPlayerDeckTextBox.Text = Properties.Settings.Default["navigatorPlayerDeckTextBox"].ToString();
                form.navSimEnemyDeckTextBox.Text = Properties.Settings.Default["navSimEnemyDeckTextBox"].ToString();

                form.navSimBgeComboBox.Text = Properties.Settings.Default["navSimBgeComboBox"].ToString();
                form.navSimYourBgeComboBox.Text = Properties.Settings.Default["navSimYourBgeComboBox"].ToString();
                form.navSimEnemyBgeComboBox.Text = Properties.Settings.Default["navSimEnemyBgeComboBox"].ToString();

                form.navSimMyFortComboBox.Text = Properties.Settings.Default["navSimMyFortComboBox"].ToString();
                form.navSimEnemyFortComboBox.Text = Properties.Settings.Default["navSimEnemyFortComboBox"].ToString();

                form.navSimCustomDecksComboBox.Text = Properties.Settings.Default["navSimCustomDecksComboBox"].ToString();


            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(form, ex.Message, "Avast! Thar PopulateNavSimTab() method threw an error trying to restore previous input! " + ex.Message);
            }
        }

        /// <summary>
        /// Card finder tab
        /// </summary>
        private static void PopulateCardFinderTab(MainForm form)
        {
            form.cardFinderUnfusedCheckbox.Checked = Properties.Settings.Default["cardFinderUnfusedCheckbox"].ToString() == "true" ? true : false;
            form.cardFinderDualCheckbox.Checked = Properties.Settings.Default["cardFinderDualCheckbox"].ToString() == "true" ? true : false;
            form.cardFinderQuadCheckbox.Checked = Properties.Settings.Default["cardFinderQuadCheckbox"].ToString() == "true" ? true : false;

            form.cardFinderEpicCheckbox.Checked = Properties.Settings.Default["cardFinderEpicCheckbox"].ToString() == "true" ? true : false;
            form.cardFinderLegendCheckbox.Checked = Properties.Settings.Default["cardFinderLegendCheckbox"].ToString() == "true" ? true : false;
            form.cardFinderVindCheckbox.Checked = Properties.Settings.Default["cardFinderVindCheckbox"].ToString() == "true" ? true : false;
            form.cardFinderMythicCheckbox.Checked = Properties.Settings.Default["cardFinderMythicCheckbox"].ToString() == "true" ? true : false;

            //form.cardFinderImpCheckbox.Checked = Properties.Settings.Default["cardFinderImpCheckbox"].ToString() == "true" ? true : false;
            //form.cardFinderRaiderCheckbox.Checked = Properties.Settings.Default["cardFinderRaiderCheckbox"].ToString() == "true" ? true : false;
            //form.cardFinderBtCheckbox.Checked = Properties.Settings.Default["cardFinderBtCheckbox"].ToString() == "true" ? true : false;
            //form.cardFinderXenoCheckbox.Checked = Properties.Settings.Default["cardFinderXenoCheckbox"].ToString() == "true" ? true : false;
            //form.cardFinderRtCheckbox.Checked = Properties.Settings.Default["cardFinderRtCheckbox"].ToString() == "true" ? true : false;
            //form.cardFinderProgenCheckbox.Checked = Properties.Settings.Default["cardFinderProgenCheckbox"].ToString() == "true" ? true : false;
            form.cardFinderImpCheckbox.Checked = false;
            form.cardFinderRaiderCheckbox.Checked = false;
            form.cardFinderBtCheckbox.Checked = false;
            form.cardFinderXenoCheckbox.Checked = false;
            form.cardFinderRtCheckbox.Checked = false;
            form.cardFinderProgenCheckbox.Checked = false;

            form.cardFinderSection1to14Checkbox.Checked = Properties.Settings.Default["cardFinderSection1to14Checkbox"].ToString() == "true" ? true : false;
            form.cardFinderSection15Checkbox.Checked = Properties.Settings.Default["cardFinderSection15Checkbox"].ToString() == "true" ? true : false;
            form.cardFinderSection16Checkbox.Checked = Properties.Settings.Default["cardFinderSection16Checkbox"].ToString() == "true" ? true : false;
            form.cardFinderSection17Checkbox.Checked = Properties.Settings.Default["cardFinderSection17Checkbox"].ToString() == "true" ? true : false;
            form.cardFinderSection18Checkbox.Checked = Properties.Settings.Default["cardFinderSection18Checkbox"].ToString() == "true" ? true : false;
            form.cardFinderSection19Checkbox.Checked = Properties.Settings.Default["cardFinderSection19Checkbox"].ToString() == "true" ? true : false;
            //form.cardFinderSection20Checkbox.Checked = Properties.Settings.Default["cardFinderSection20Checkbox"].ToString() == "true" ? true : false;

            form.cardFinderSet2000Checkbox.Checked = Properties.Settings.Default["cardFinderSet2000Checkbox"].ToString() == "true" ? true : false;
            form.cardFinderSet2500Checkbox.Checked = Properties.Settings.Default["cardFinderSet2500Checkbox"].ToString() == "true" ? true : false;
            form.cardFinderSet6000Checkbox.Checked = Properties.Settings.Default["cardFinderSet6000Checkbox"].ToString() == "true" ? true : false;
            form.cardFinderSet7000Checkbox.Checked = Properties.Settings.Default["cardFinderSet7000Checkbox"].ToString() == "true" ? true : false;
            form.cardFinderSet8000Checkbox.Checked = Properties.Settings.Default["cardFinderSet8000Checkbox"].ToString() == "true" ? true : false;
            form.cardFinderSet8500Checkbox.Checked = Properties.Settings.Default["cardFinderSet8500Checkbox"].ToString() == "true" ? true : false;
            form.cardFinderSet9500Checkbox.Checked = Properties.Settings.Default["cardFinderSet9500Checkbox"].ToString() == "true" ? true : false;
            form.cardFinderSet9999Checkbox.Checked = Properties.Settings.Default["cardFinderSet9999Checkbox"].ToString() == "true" ? true : false;
            form.cardFinderSetOtherCheckBox.Checked = Properties.Settings.Default["cardFinderSetOtherCheckBox"].ToString() == "true" ? true : false;

            form.cardFinderBoxCheckbox.Checked = Properties.Settings.Default["cardFinderBoxCheckbox"].ToString() == "true" ? true : false;
            form.cardFinderChanceCheckbox.Checked = Properties.Settings.Default["cardFinderChanceCheckbox"].ToString() == "true" ? true : false;
            //form.cardFinderCacheCheckbox.Checked = Properties.Settings.Default["cardFinderCacheCheckbox"].ToString() == "true" ? true : false;
            form.cardFinderPvPRewardCheckbox.Checked = Properties.Settings.Default["cardFinderPvPRewardCheckbox"].ToString() == "true" ? true : false;
            form.cardFinderPvERewardCheckbox.Checked = Properties.Settings.Default["cardFinderPvERewardCheckbox"].ToString() == "true" ? true : false;

            form.cardFinderAssaultCheckbox.Checked = Properties.Settings.Default["cardFinderAssaultCheckbox"].ToString() == "true" ? true : false;
            form.cardFinderStructureCheckbox.Checked = Properties.Settings.Default["cardFinderStructureCheckbox"].ToString() == "true" ? true : false;
            form.cardFinderCommanderCheckbox.Checked = Properties.Settings.Default["cardFinderCommanderCheckbox"].ToString() == "true" ? true : false;
            form.cardFinderEventStructureCheckbox.Checked = Properties.Settings.Default["cardFinderEventStructureCheckbox"].ToString() == "true" ? true : false;
            form.cardFinderDominionCheckbox.Checked = Properties.Settings.Default["cardFinderDominionCheckbox"].ToString() == "true" ? true : false;

            form.cardFinderPowerLowUpDown.Text = Properties.Settings.Default["cardFinderPowerLowUpDown"].ToString();
            form.cardFinderPowerHighUpDown.Text = Properties.Settings.Default["cardFinderPowerHighUpDown"].ToString();
            form.cardFinderFactionPowerLowUpDown.Text = Properties.Settings.Default["cardFinderFactionPowerLowUpDown"].ToString();
            form.cardFinderFactionPowerHighUpDown.Text = Properties.Settings.Default["cardFinderFactionPowerHighUpDown"].ToString();

            form.cardFinderSkillTextBox.Text = Properties.Settings.Default["cardFinderSkillTextBox"].ToString();

            form.cardFinderSortByComboBox.Text = Properties.Settings.Default["cardFinderSortByComboBox"].ToString();
            
            // Init the card list
            CardManager.FilterCards(form);
        }


        /// <summary>
        /// API controls tab
        /// </summary>
        private static void PopulateRumBarrelTab(MainForm form)
        {
            // Reads settings from API, if it exists
            if (File.Exists("./__users.txt"))
            {
                List<string> userApiStrings = FileIO.SimpleRead(form, "./__users.txt", returnCommentedLines: false);
                foreach (var user in userApiStrings)
                {
                    // Add to rumBarrel dropdown
                    form.rumBarrelPlayerComboBox.Items.Add(user);

                    // Add to admin dropdown
                    form.adminPlayerListBox.Items.Add(user);
                }
            }
            

            if (form.rumBarrelPlayerComboBox.Items.Count > 0) form.rumBarrelPlayerComboBox.SelectedIndex = 0;
        }

        private static void PopulateAdminTab(MainForm form)
        {
            form.guildManagementFactionIdComboBox.Items.AddRange(guildIds.ToArray());
            form.guildManagementOfficerIdComboBox.Items.AddRange(playerIds.ToArray());
        }

        #endregion

    }
}
