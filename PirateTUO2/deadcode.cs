// Hiding dead code here


/// <summary>
/// TODO: Given a config somewhere, add or update cards in the cards_section_xx.xml
/// - To sim updated cards before they're released
/// - To create/use fantasy cards
/// </summary>
//public static string ModifyCardXML()
//{
//    // Get card modifications
//    Dictionary<int, List<string>> modifiedCards = FileIO.ReadFromAppSettings("NewCard");

//    // Location to add new cards
//    string xmlFilePath = "data/cards_section_17.xml";

//    // Nothing returned, don't change the file
//    if (modifiedCards[0].Count == 0) return "";


//    try
//    {
//        XDocument doc = XDocument.Load(xmlFilePath);
//        XElement unit;
//        //Tryparse var
//        int x;

//        // Each CustomCard: Id=123, Unit=My Card, Atk=10, Hp=60, ...
//        foreach (var cardLine in modifiedCards.Values)
//        {
//            int cardId = -1;
//            string cardName = "";
//            string cardType = "assault"; // structure, commander, dominion
//            int cardAttack = -1;
//            int cardHealth = -1;
//            int cardCost = -1;
//            int cardRarity = -1;
//            int cardFusion = -1;
//            int cardFaction = -1;
//            int cardSet = -1;

//            // Skills
//            string skill1_name = "";
//            int skill1_x = -1;
//            int skill1_y = -1;
//            int skill1_c = -1;
//            int skill1_n = -1;
//            int skill1_all = -1;
//            string skill1_s1 = "";
//            string skill1_s2 = "";
//            string skill1_trigger = "";
//            string skill1_summon = "";

//            string skill2_name = "";
//            int skill2_x = -1;
//            int skill2_y = -1;
//            int skill2_c = -1;
//            int skill2_n = -1;
//            int skill2_all = -1;
//            string skill2_s1 = "";
//            string skill2_s2 = "";
//            string skill2_trigger = "";
//            string skill2_summon = "";

//            string skill3_name = "";
//            int skill3_x = -1;
//            int skill3_y = -1;
//            int skill3_c = -1;
//            int skill3_n = -1;
//            int skill3_all = -1;
//            string skill3_s1 = "";
//            string skill3_s2 = "";
//            string skill3_trigger = "";
//            string skill3_summon = "";

//            // Process each element
//            foreach (var cardElement in cardLine)
//            {
//                // Id=123 --> [Id, 123]
//                var attributes = cardElement.Split(new char[] { '=', ':' });

//                if (attributes.Length != 2) continue;

//                var key = attributes[0].Trim();
//                var value = attributes[1].Trim();

//                switch (key)
//                {
//                    case "id":
//                        if (Int32.TryParse(value, out x)) cardId = x;
//                        break;
//                    case "name":
//                        cardName = value;
//                        break;
//                    case "cardtype": // If unspecified - assault
//                        cardType = value;
//                        break;
//                    case "attack":
//                        if (Int32.TryParse(value, out x)) cardAttack = x;
//                        break;
//                    case "health":
//                        if (Int32.TryParse(value, out x)) cardHealth = x;
//                        break;
//                    case "cost":
//                    case "delay":
//                        if (Int32.TryParse(value, out x)) cardCost = x;
//                        break;
//                    case "rarity":
//                        if (Int32.TryParse(value, out x)) cardRarity = x;
//                        break;
//                    case "fusion":
//                        if (Int32.TryParse(value, out x)) cardFusion = x;
//                        break;
//                    case "faction":
//                        if (Int32.TryParse(value, out x)) cardFaction = x;
//                        break;
//                    case "set":
//                        if (Int32.TryParse(value, out x)) cardSet = x;
//                        break;

//                    // Skill
//                    case "skill1_name":
//                    case "s1":
//                        skill1_name = value;
//                        break;
//                    case "skill1_x":
//                    case "s1x":
//                        if (Int32.TryParse(value, out x)) skill1_x = x;
//                        break;
//                    case "skill1_y":
//                    case "s1y":
//                        if (Int32.TryParse(value, out x)) skill1_y = x;
//                        break;
//                    case "skill1_all":
//                    case "s1all":
//                    case "s1a":
//                        if (Int32.TryParse(value, out x)) skill1_all = 1;
//                        break;
//                    case "skill1_c":
//                    case "s1c":
//                        if (Int32.TryParse(value, out x)) skill1_c = x;
//                        break;
//                    case "skill1_n":
//                    case "s1n":
//                        if (Int32.TryParse(value, out x)) skill1_n = x;
//                        break;
//                    case "skill1_s1":
//                    case "s1_s1":
//                        skill1_s1 = value;
//                        break;
//                    case "skill1_s2":
//                    case "s1_s2":
//                        skill1_s2 = value;
//                        break;
//                    case "skill1_trigger":
//                    case "s1trigger":
//                    case "s1_t":
//                        skill1_trigger = value;
//                        break;
//                    case "skill1_summon":
//                    case "s1summon":
//                        skill1_summon = value;
//                        break;

//                    // Skill
//                    case "skill2_name":
//                    case "s2":
//                        skill2_name = value;
//                        break;
//                    case "skill2_x":
//                    case "s2x":
//                        if (Int32.TryParse(value, out x)) skill2_x = x;
//                        break;
//                    case "skill2_y":
//                    case "s2y":
//                        if (Int32.TryParse(value, out x)) skill2_y = x;
//                        break;
//                    case "skill2_all":
//                    case "s2all":
//                    case "s2a":
//                        if (Int32.TryParse(value, out x)) skill2_all = 1;
//                        break;
//                    case "skill2_c":
//                    case "s2c":
//                        if (Int32.TryParse(value, out x)) skill2_c = x;
//                        break;
//                    case "skill2_n":
//                    case "s2n":
//                        if (Int32.TryParse(value, out x)) skill2_n = x;
//                        break;
//                    case "skill2_s1":
//                    case "s2_s1":
//                        skill2_s1 = value;
//                        break;
//                    case "skill2_s2":
//                    case "s2_s2":
//                        skill2_s2 = value;
//                        break;
//                    case "skill2_trigger":
//                    case "s2trigger":
//                    case "s2_t":
//                        skill2_trigger = value;
//                        break;
//                    case "skill2_summon":
//                    case "s2summon":
//                        skill2_summon = value;
//                        break;

//                    // Skill
//                    case "skill3_name":
//                    case "s3":
//                        skill3_name = value;
//                        break;
//                    case "skill3_x":
//                    case "s3x":
//                        if (Int32.TryParse(value, out x)) skill3_x = x;
//                        break;
//                    case "skill3_y":
//                    case "s3y":
//                        if (Int32.TryParse(value, out x)) skill3_y = x;
//                        break;
//                    case "skill3_all":
//                    case "s3all":
//                    case "s3a":
//                        if (Int32.TryParse(value, out x)) skill3_all = 1;
//                        break;
//                    case "skill3_c":
//                    case "s3c":
//                        if (Int32.TryParse(value, out x)) skill3_c = x;
//                        break;
//                    case "skill3_n":
//                    case "s3n":
//                        if (Int32.TryParse(value, out x)) skill3_n = x;
//                        break;
//                    case "skill3_s1":
//                    case "s3_s1":
//                        skill3_s1 = value;
//                        break;
//                    case "skill3_s2":
//                    case "s3_s2":
//                        skill3_s2 = value;
//                        break;
//                    case "skill3_trigger":
//                    case "s3trigger":
//                    case "s3_t":
//                        skill3_trigger = value;
//                        break;
//                    case "skill3_summon":
//                    case "s3summon":
//                        skill3_summon = value;
//                        break;
//                }//switch
//            }//single line attribute

//            // Create a card
//            if (cardName != "")
//            {
//                unit = AddCardToXml(cardId, cardName, cardType, cardAttack, cardHealth, cardCost, (Faction)cardFaction, (Rarity)cardRarity);

//                // Add skills
//                if (skill1_name != "")
//                    unit.Add(AddSkillToXml(skill1_name, skill1_x, skill1_y, skill1_all, skill1_c, skill1_n, skill1_s1, skill1_s2, skill1_trigger, skill1_summon));
//                if (skill2_name != "")
//                    unit.Add(AddSkillToXml(skill2_name, skill2_x, skill2_y, skill2_all, skill2_c, skill2_n, skill2_s1, skill2_s2, skill2_trigger, skill2_summon));
//                if (skill3_name != "")
//                    unit.Add(AddSkillToXml(skill3_name, skill3_x, skill3_y, skill3_all, skill3_c, skill3_n, skill3_s1, skill3_s2, skill3_trigger, skill3_summon));

//                doc.Root.Add(unit);
//            }

//        }// all attributes

//        doc.Save(xmlFilePath);
//        //doc.Save(xmlFilePath, SaveOptions.DisableFormatting);
//    }
//    catch (Exception ex)
//    {
//        Console.WriteLine("ModifyCardXml(): Error - " + ex.Message);
//        return "ModifyCardXml(): Error - " + ex.Message;
//    }

//    return "Added " + modifiedCards.Count + " custom cards (old method)";
//}



#region Mass Gold Buy


///// <summary>
///// Buy Gold on player and salvage commons/rares until their SP is capped
///// </summary>
//private void adminMaxPlayerGoldButton_Click(object sender, EventArgs e)
//{
//    adminOutputTextBox.Text = "";

//    try
//    {
//        string[] kongInfos = adminInputTextBox.Text.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

//        Parallel.ForEach(kongInfos,
//            new ParallelOptions { MaxDegreeOfParallelism = 10 },
//            kongInfo =>
//            {
//                StringBuilder sb = new StringBuilder();

//                try
//                {
//                            // Call Init to get the player's stats
//                            KongViewModel kongVm = BotManager.Init(this, kongInfo);
//                    if (kongVm.Result != "False")
//                    {
//                        string name = kongVm.KongName;

//                                // Get inventory, gold, and salvage
//                                int gold = kongVm.UserData.Gold;
//                        int inventory = kongVm.UserData.Inventory;
//                        int maxInventory = kongVm.UserData.MaxInventory;
//                        int salvage = kongVm.UserData.Salvage;
//                        int maxSalvage = kongVm.UserData.MaxSalvage;

//                                // Player has gold to buy
//                                if (gold > 2000)
//                        {
//                                    // Buy gold packs until out of gold, inventory is full, or SP is full
//                                    for (int i = 0; i < 100; i++)
//                            {

//                                        // Buy gold packs until we hit an error
//                                        adminOutputTextBox.AppendText(name + ": Buying gold\r\n");
//                                kongVm = BotManager.BuyGold(this, kongInfo, goldPacks: 0, displayGoldBuys: false);

//                                        // Some error happened. If its because they're broke, stop
//                                        if (kongVm.Result == "False")
//                                {
//                                    adminOutputTextBox.AppendText(name + ": " + kongVm.ResultMessage + "\r\n");
//                                    if (kongVm.ResultMessage.Contains("You cannot afford")) break;
//                                }

//                                        // Unless SP is full, salvage commons and rares
//                                        if (maxSalvage - salvage < 100)
//                                {
//                                    adminOutputTextBox.AppendText(name + ": Near max salvage\r\n");
//                                    break;
//                                }
//                                else
//                                {
//                                    kongVm = BotManager.SalvageL1CommonsAndRares(this, kongInfo);
//                                    adminOutputTextBox.AppendText(name + ": Salvaging commons and rares\r\n");
//                                }

//                                        // ------------------------------
//                                        // Call init and refresh counts
//                                        // ------------------------------
//                                        kongVm = BotManager.Init(this, kongInfo);
//                                gold = kongVm.UserData.Gold;
//                                inventory = kongVm.UserData.Inventory;
//                                maxInventory = kongVm.UserData.MaxInventory;
//                                salvage = kongVm.UserData.Salvage;
//                                maxSalvage = kongVm.UserData.MaxSalvage;


//                                        // Check salvage
//                                        if (salvage < 0 || maxSalvage <= 0 || (maxSalvage - salvage < 100))
//                                {
//                                    adminOutputTextBox.AppendText(name + ": Approaching max salvage\r\n");
//                                    break;
//                                }
//                                        // Check Inventory 
//                                        if (inventory <= 0 || maxInventory <= 0 || (maxInventory - inventory < 20))
//                                {
//                                    adminOutputTextBox.AppendText(name + ": Inventory is near full\r\n");
//                                    break;
//                                }
//                            }
//                        }
//                        else
//                        {
//                            outputTextBox.AppendText(name + ": is out of gold!\r\n");
//                        }
//                    }
//                    else
//                    {
//                        adminOutputTextBox.AppendText("API error when using this kong string: " + kongInfo);
//                    }
//                }
//                catch (Exception ex)
//                {
//                    Helper.OutputWindowMessage(this, "Error on adminMaxPlayerGoldButton_Click(): \r\n" + ex);
//                }
//            });
//    }
//    catch (Exception ex)
//    {
//        Helper.OutputWindowMessage(this, "Error on adminMaxPlayerGoldButton_Click(): \r\n" + ex);
//    }
//}


#endregion

#region Old mass Brawl and mass CQ Autoburn code


///// <summary>
///// For each account, brawl them to X
///// </summary>
//private async void adminBrawlAccountsButton_Click(object sender, EventArgs e)
//{
//    adminOutputTextBox.Text = "";

//    try
//    {
//        string[] accounts = adminInputTextBox.Text.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

//        // Attack params
//        int iterations = 1500;
//        int extraLockedCards = 1;
//        int energy = 0;
//        int.TryParse(adminBurnAttacksToTextBox.Text, out int burnAttacksDownTo);
//        string gameMode = rumBarrelGameModeSelectionComboBox.Text;

//        // LSE Faster: Only do one sim per turn
//        if (grindAttackFasterCheckBox.Checked)
//        {
//            iterations = 100;
//            extraLockedCards = 0;
//        }

//        foreach (string kongInfo in accounts)
//        {
//            KongViewModel kongVm = BotManager.Init(this, kongInfo);
//            if (kongVm.Result != "False")
//            {
//                if (kongVm.BattleToResume)
//                {
//                    adminOutputTextBox.AppendText(kongVm.KongName + " is already in a battle! It must be finished\r\n");
//                    continue;
//                }

//                energy = kongVm.BrawlData.Energy;
//                adminOutputTextBox.AppendText(kongVm.KongName + " has " + energy + " energy\r\n");

//                while (energy > 0 && energy > burnAttacksDownTo)
//                {
//                    kongVm = BotManager.StartBrawlMatch(this, kongInfo);

//                    adminOutputTextBox.AppendText(kongVm.BattleData.EnemyName + ": ");

//                    // Populate RumBarrel
//                    if (kongVm != null)
//                    {
//                        //repeat
//                        while (kongVm.BattleData.Winner == null) // or some error occurs
//                        {
//                            // Refresh
//                            // TODO: Shouldn't BotManager.PlayCard return this data?
//                            kongVm = BotManager.GetCurrentOrLastBattle(this, kongInfo);

//                            // Build sim
//                            BatchSim batchSim = NewSimManager.BuildLiveSim(kongVm, gameMode, iterations: iterations, extraLockedCards: extraLockedCards);

//                            // Run sim
//                            await Task.Run(() => NewSimManager.RunLiveSim(this, batchSim));

//                            // Play next card
//                            // Look through our simmed deck and find the first card we can legally play
//                            Sim sim = batchSim.Sims.OrderByDescending(x => x.WinPercent).ThenByDescending(x => x.WinScore).FirstOrDefault();

//                            BotManager.PlayCard(this, kongVm, sim);

//                            // Some error happened
//                            if (kongVm.Result == "False")
//                            {
//                                adminOutputTextBox.AppendText(kongVm.KongName + ": An error has occured: " + kongVm.ResultMessage + "\r\n");
//                                break;
//                            }
//                        }

//                        // Some error happened
//                        if (kongVm.Result == "False")
//                        {
//                            adminOutputTextBox.AppendText(kongVm.KongName + ": An error has occured: " + kongVm.ResultMessage + "\r\n");
//                            return;
//                        }
//                        // Show if we won or lost
//                        else
//                        {
//                            adminOutputTextBox.AppendText((kongVm.BattleData.Winner == true ? "WIN" : "**LOSS**") + "\r\n");
//                        }
//                    }
//                    else
//                    {
//                        adminOutputTextBox.AppendText(kongVm.KongName + ": An error has occured: " + kongVm.ResultMessage + "\r\n");
//                        break;
//                    }

//                    // Decrease energy
//                    energy--;
//                }
//            }
//            else
//            {
//                adminOutputTextBox.AppendText("API error when using this kong string: " + kongInfo);
//            }

//            StringBuilder brawlTargets = new StringBuilder();
//        }
//    }
//    catch (Exception ex)
//    {
//        Helper.OutputWindowMessage(this, "Error on StartBrawlBattle(): \r\n" + ex);
//    }
//}

///// <summary>
///// For each account, CQ them to X
///// </summary>
//private async void adminCqAccountsButton_Click(object sender, EventArgs e)
//{
//    adminOutputTextBox.Text = "";

//    try
//    {
//        string[] accounts = adminInputTextBox.Text.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

//        // Attack params
//        int iterations = 50;
//        int extraLockedCards = 1;
//        int energy = 0;
//        int.TryParse(adminBurnAttacksToTextBox.Text, out int burnAttacksDownTo);
//        int zoneId = Helper.GetConquestZoneId(grindCqZoneComboBox.Text);
//        string gameMode = "surge";

//        // LSE Faster: Only do one sim per turn
//        if (grindAttackFasterCheckBox.Checked)
//        {
//            iterations = 100;
//            extraLockedCards = 0;
//        }

//        if (zoneId <= 0)
//        {
//            adminOutputTextBox.Text = "** WARNING: Zone required **";
//        }

//        // Parallel doesn't work here for some reason. Fails on the RunLiveSim() - maybe chaining parallel?

//        foreach (string kongInfo in accounts)
//        {
//            KongViewModel kongVm = BotManager.Init(this, kongInfo);
//            if (kongVm.Result != "False")
//            {
//                if (kongVm.BattleToResume)
//                {
//                    adminOutputTextBox.AppendText(kongVm.KongName + " is already in a battle! It must be finished\r\n");
//                    continue;
//                }

//                energy = kongVm.ConquestData.Energy;
//                adminOutputTextBox.AppendText(kongVm.KongName + " has " + energy + " energy\r\n");

//                while (energy > 0 && energy > burnAttacksDownTo)
//                {
//                    kongVm = BotManager.StartCqMatch(this, kongInfo, zoneId);

//                    adminOutputTextBox.AppendText(kongVm.BattleData.EnemyName + ": ");

//                    // Populate RumBarrel
//                    if (kongVm != null)
//                    {
//                        //repeat
//                        while (kongVm.BattleData.Winner == null) // or some error occurs
//                        {
//                            // Refresh
//                            // TODO: Shouldn't BotManager.PlayCard return this data?
//                            kongVm = BotManager.GetCurrentOrLastBattle(this, kongInfo);

//                            // Build sim
//                            BatchSim batchSim = NewSimManager.BuildLiveSim(kongVm, gameMode, iterations: iterations, extraLockedCards: extraLockedCards);

//                            // Run sim
//                            await Task.Run(() => NewSimManager.RunLiveSim(this, batchSim));

//                            // Play next card
//                            // Look through our simmed deck and find the first card we can legally play
//                            Sim sim = batchSim.Sims.OrderByDescending(x => x.WinPercent).ThenByDescending(x => x.WinScore).FirstOrDefault();

//                            BotManager.PlayCard(this, kongVm, sim);

//                            // Some error happened
//                            if (kongVm.Result == "False")
//                            {
//                                adminOutputTextBox.AppendText(kongVm.KongName + ": An error has occured: " + kongVm.ResultMessage + "\r\n");
//                                break;
//                            }
//                        }

//                        // Some error happened
//                        if (kongVm.Result == "False")
//                        {
//                            adminOutputTextBox.AppendText(kongVm.KongName + ": An error has occured: " + kongVm.ResultMessage + "\r\n");
//                            return;
//                        }
//                        // Show if we won or lost
//                        else
//                        {
//                            adminOutputTextBox.AppendText((kongVm.BattleData.Winner == true ? "WIN" : "**LOSS**") + "\r\n");
//                        }
//                    }
//                    else
//                    {
//                        adminOutputTextBox.AppendText(kongVm.KongName + ": An error has occured: " + kongVm.ResultMessage + "\r\n");
//                        break;
//                    }

//                    // Decrease energy
//                    energy--;
//                }
//            }
//            else
//            {
//                adminOutputTextBox.AppendText("API error when using this kong string: " + kongInfo);
//            }
//        }
//    }
//    catch (Exception ex)
//    {
//        Helper.OutputWindowMessage(this, "Error on StartCqBattle(): \r\n" + ex);
//    }
//}



#endregion

#region Events - Notes Tab

///// <summary>
///// Save notes
///// </summary>
//private void noteTextBox_TextChanged(object sender, EventArgs e)
//{
//    try
//    {
//        if (File.Exists(CONSTANTS.notePath))
//        {
//            using (var writer = new StreamWriter(CONSTANTS.notePath))
//            {
//                writer.Write(noteTextBox.Text);
//            }
//        }
//    }
//    catch //(Exception ex)
//    { }
//}

#endregion

#region List player quests

///// <summary>
///// List out all player quests
///// </summary>
//private void adminGetPlayerQuestsButton_Click(object sender, EventArgs e)
//{
//    adminOutputTextBox.Text = "";

//    try
//    {
//        string[] kongInfos = adminInputTextBox.Text.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

//        Parallel.ForEach(kongInfos,
//            new ParallelOptions { MaxDegreeOfParallelism = 5 },
//            kongInfo =>
//            {
//                StringBuilder sb = new StringBuilder();

//                try
//                {
//                    KongViewModel kongVm = BotManager.Init(this, kongInfo);
//                    if (kongVm.Result != "False")
//                    {
//                        sb.AppendLine("-----------");
//                        sb.AppendLine(kongVm.KongName);
//                        sb.AppendLine("-----------");

//                        foreach (Quest quest in kongVm.Quests)
//                        {
//                                    // Ignore daily and pvp quests
//                                    if (quest.Id < 0) continue;
//                            if (quest.Id > 2000 && quest.Id < 3000) continue;

//                            sb.Append(quest.Id);
//                            sb.Append("\t");
//                            sb.Append(quest.Name);
//                            sb.Append("\t");
//                            sb.Append(quest.Progress);
//                            sb.Append("/");
//                            sb.Append(quest.MaxProgress);
//                            sb.Append("\t");
//                            sb.Append(quest.MissionId);
//                            sb.Append("\r\n");
//                        }
//                        sb.Append("-----------\r\n\r\n");

//                        adminOutputTextBox.AppendText(sb.ToString());
//                    }
//                    else
//                    {
//                        adminOutputTextBox.AppendText("API error when using this kong string: " + kongInfo);
//                    }
//                }
//                catch (Exception ex)
//                {
//                    Helper.OutputWindowMessage(this, "Error on GetPlayerQuests(): \r\n" + ex);
//                }
//            });
//    }
//    catch (Exception ex)
//    {
//        Helper.OutputWindowMessage(this, "Error on GetPlayerQuests(): \r\n" + ex);
//    }
//}



#endregion

#region Mass card build


/// <summary>
/// Attempt to build a card on each player
/// </summary>
//private void adminPlayerBuildCardButton_Click(object sender, EventArgs e)
//{
//    adminOutputTextBox.Text = "";

//    try
//    {
//        string[] kongInfos = adminInputTextBox.Text.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
//        string cardToBuild = grindCardNameTextBox.Text;

//        ConcurrentDictionary<string, string> playerResults = new ConcurrentDictionary<string, string>();


//        Parallel.ForEach(kongInfos,
//            new ParallelOptions { MaxDegreeOfParallelism = 10 },
//            kongInfo =>
//            {
//                try
//                {
//                            // Call Init to get the player's stats
//                            KongViewModel kongVm = BotManager.Init(this, kongInfo);
//                    if (kongVm.Result != "False")
//                    {
//                                // Put player cards in list<string>

//                                string name = kongVm.KongName;
//                        List<string> inventoryCards = new List<string>();
//                        List<string> restoreCards = new List<string>();

//                        foreach (var playerCard in kongVm.PlayerCards.OrderBy(x => x.Key.Name))
//                        {
//                            string cardName = playerCard.Key.Name;
//                            int cardCount = playerCard.Value;
//                            inventoryCards.Add(cardName + (cardCount > 1 ? ("#" + cardCount) : ""));

//                        }
//                        foreach (var playerCard in kongVm.RestoreCards.OrderBy(x => x.Key.Name))
//                        {
//                            string cardName = playerCard.Key.Name;
//                            int cardCount = playerCard.Value;
//                            restoreCards.Add(cardName + (cardCount > 1 ? ("#" + cardCount) : ""));

//                        }

//                                // We already have this card but its not max level
//                                if (inventoryCards.Contains(cardToBuild + "-1"))
//                        {
//                            adminOutputTextBox.AppendText(name + ": " + cardToBuild + " QUAD FOUND: Just needs leveling");
//                            kongVm = BotManager.UpgradeCard(this, kongInfo, cardToBuild + "-1");
//                                    // Save this result
//                                    if (kongVm.Result != "False") playerResults.TryAdd(name, "Success!");
//                            else playerResults.TryAdd(name, "FAIL: " + kongVm.ResultMessage);
//                        }
//                        else if (inventoryCards.Contains(cardToBuild + "-2"))
//                        {
//                            adminOutputTextBox.AppendText(name + ": " + cardToBuild + " QUAD FOUND: Just needs leveling");
//                            kongVm = BotManager.UpgradeCard(this, kongInfo, cardToBuild + "-2");
//                                    // Save this result
//                                    if (kongVm.Result != "False") playerResults.TryAdd(name, "Success!");
//                            else playerResults.TryAdd(name, "FAIL: " + kongVm.ResultMessage);
//                        }
//                        else if (inventoryCards.Contains(cardToBuild + "-3"))
//                        {
//                            adminOutputTextBox.AppendText(name + ": " + cardToBuild + " QUAD FOUND: Just needs leveling");
//                            kongVm = BotManager.UpgradeCard(this, kongInfo, cardToBuild + "-3");
//                                    // Save this result
//                                    if (kongVm.Result != "False") playerResults.TryAdd(name, "Success!");
//                            else playerResults.TryAdd(name, "FAIL: " + kongVm.ResultMessage);
//                        }
//                        else if (inventoryCards.Contains(cardToBuild + "-4"))
//                        {
//                            adminOutputTextBox.AppendText(name + ": " + cardToBuild + " QUAD FOUND: Just needs leveling");
//                            kongVm = BotManager.UpgradeCard(this, kongInfo, cardToBuild + "-4");
//                                    // Save this result
//                                    if (kongVm.Result != "False") playerResults.TryAdd(name, "Success!");
//                            else playerResults.TryAdd(name, "FAIL: " + kongVm.ResultMessage);
//                        }
//                        else if (inventoryCards.Contains(cardToBuild + "-5"))
//                        {
//                            adminOutputTextBox.AppendText(name + ": " + cardToBuild + " QUAD FOUND: Just needs leveling");
//                            kongVm = BotManager.UpgradeCard(this, kongInfo, cardToBuild + "-5");
//                                    // Save this result
//                                    if (kongVm.Result != "False") playerResults.TryAdd(name, "Success!");
//                            else playerResults.TryAdd(name, "FAIL: " + kongVm.ResultMessage);
//                        }
//                        else
//                        {
//                                    // Get the card object to figure out what this card is made from
//                                    string cardName = !cardToBuild.EndsWith("-1") ? cardToBuild + "-1" : cardToBuild;
//                            Card card = CardManager.GetCardByName(cardName);

//                            if (card != null)
//                            {
//                                adminOutputTextBox.AppendText(name + ": Attempting to make: " + cardName + "\r\n");
//                                bool buildCardFailed = false;

//                                        // Recursively go through the card's recipe, making what it needs to finally build it
//                                        kongVm = BuildCard(card, inventoryCards, restoreCards, kongInfo, ref buildCardFailed);

//                                        // Save this result
//                                        if (kongVm.Result != "False" && !buildCardFailed) playerResults.TryAdd(name, "Success!");
//                                else playerResults.TryAdd(name, "FAILED: " + kongVm?.ResultMessage);
//                            }
//                            else
//                            {
//                                playerResults.TryAdd(name, cardName + " not recognized.\r\n");
//                            }
//                        }

//                    }
//                    else
//                    {
//                        adminOutputTextBox.AppendText("API error when using this kong string: " + kongInfo);
//                    }
//                }
//                catch (Exception ex)
//                {
//                    Helper.OutputWindowMessage(this, "Error on adminPlayerBuildCardButton_Click(): \r\n" + ex);
//                }
//            });


//        adminOutputTextBox.AppendText("\r\n\r\n");

//        foreach (var playerResult in playerResults.OrderBy(x => x.Key))
//        {
//            adminOutputTextBox.AppendText(playerResult.Key + ": " + playerResult.Value + "\r\n");
//        }
//    }
//    catch (Exception ex)
//    {
//        Helper.OutputWindowMessage(this, "Error on adminPlayerBuildCardButton_Click(): \r\n" + ex);
//    }
//}

#endregion

/// <summary>
/// Fuse target card by name (for now)
/// </summary>
//private void builderFuseButton_Click(object sender, EventArgs e)
//{
//    // TODO: We may remove this, replacing it with the autobuild

//    try
//    {
//        string selectedUser = rumBarrelPlayerComboBox.Text;
//        string[] cardsToFuse = builderFuseCardTextBox.Text.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
//        KongViewModel kongVm = new KongViewModel();

//        foreach (var cardToFuse in cardsToFuse)
//        {
//            string cardName = !cardToFuse.EndsWith("-1") ? cardToFuse + "-1" : cardToFuse;
//            Card card = CardManager.GetCardByName(cardName);

//            // Attempt to fuse
//            if (card != null)
//            {
//                kongVm = BotManager.FuseCard(this, selectedUser, card.CardId.ToString());

//                // Success
//                if (kongVm.Result != "False" && !String.IsNullOrWhiteSpace(kongVm.ResultNewCardId))
//                {
//                    outputTextBox.AppendText("Fusion complete\r\n");

//                    // Refresh account info and card info
//                    // RefreshAccountAndCards(kongVm, fullInit: true);
//                }
//                // Failure
//                else
//                {
//                    outputTextBox.AppendText("Fusion failed: " + kongVm.ResultMessage + "\r\n");
//                }
//            }
//        }

//        // Call init - userdata.cards sometimes returns stuff, and sometimes fails
//        kongVm = BotManager.Init(this, selectedUser, true);


//    }
//    catch (Exception ex)
//    {
//        Helper.OutputWindowMessage(this, "Error on Fusing: \r\n" + ex + "\r\n");
//    }
//}


// Write kongVm.PlayerCards in some fashion
// When outputting cards, use one of the preset blacklists (if needed)
//var myBlacklist = PlayerManager.blacklistLevel1;
////var myBlacklist = new HashSet<string>();
//int lastSection = 0;
//// Write these items last
//StringBuilder cardsToWriteLast = new StringBuilder();
//bool cleanupCards = false;

// Find each card listed in the database, translate it to its string name
//foreach (var cardPair in kongVm.PlayerCards.OrderByDescending(c => c.Key.Rarity).ThenBy(c => c.Key.Section).ThenBy(c => c.Key.Faction))
//{
//    try
//    {
//        Card card = cardPair.Key;
//        int cardCount = cardPair.Value;

//        if (card != null)
//        {
//            string baseCardName = CardManager.FormatCard(card.Name, false).Key;

//            // Return all cards
//            if (!cleanupCards)
//            {
//                output.Append(card.Name);
//                output.AppendLine();
//            }
//            // Return some cards, ignoring junky stuff
//            else
//            {
//                // Add commander legendaries - duals or quads
//                if (card.CardType == CardType.Commander.ToString() && card.Rarity > 3 && card.Fusion_Level > 0)
//                {
//                    output.Append(card.Name);
//                    output.AppendLine();
//                }
//                // Ignore commons, rares
//                else if (card.Rarity < 3)
//                {
//                    continue;
//                }
//                // Skip base commanders
//                else if (card.CardType == CardType.Commander.ToString())
//                {
//                    cardsToWriteLast.Append("//").Append(card.Name).AppendLine();
//                }
//                // Skip these blacklisted cards
//                else if (myBlacklist.Contains(baseCardName))
//                {
//                    cardsToWriteLast.Append("//").Append(card.Name);
//                    if (cardCount > 1) cardsToWriteLast.Append("#").Append(cardCount);
//                    cardsToWriteLast.AppendLine();
//                }
//                // Add card if not in blacklist
//                else
//                {
//                    // Write the card
//                    output.Append(card.Name);
//                    if (cardCount > 1) output.Append("#").Append(cardCount);
//                    output.AppendLine();
//                }
//                // Not sure what this is doing
//                if (card.Section != lastSection)
//                {
//                    lastSection = card.Section;
//                }
//            }
//        }
//    }
//    catch { }
//}

//if (!cleanupCards)
//{
//    output.AppendLine();
//    output.AppendLine("// ----------------------");
//    output.AppendLine("// Unknown or weak cards");
//    output.AppendLine("// ----------------------");
//    output.AppendLine(cardsToWriteLast.ToString());


//    foreach (var unknownCard in unknownCardNumbers)
//    {
//        if (unknownCard.Value > 0)
//            output.AppendLine("[" + unknownCard.Key + "]#" + unknownCard.Value);
//    }
//}

///// <summary>
///// Import cards tab - Call API init and get player cards
///// </summary>
//private void importCardsButton_Click(object sender, EventArgs e)
//{
//    string userData = importPlayerComboBox.Text;

//    //HelmOutputTextBox.Text = ApiManager.CallApi();
//    KongViewModel kongVm = BotManager.Init(this, userData, true);

//    StringBuilder cardList = new StringBuilder();
//    foreach (var card in kongVm.PlayerCards)
//    {
//        cardList.Append(card.Key.Name);
//        if (card.Value > 1) cardList.Append("#" + card.Value);
//        cardList.Append("\r\n");
//    }

//    importCardsTextBox.Text = cardList.ToString();
//}


///// <summary>
///// Import cards tab - Call API init and get player cards
///// </summary>
//private void importCardsButton_Click(object sender, EventArgs e)
//{
//    string userData = importPlayerComboBox.Text;

//    //HelmOutputTextBox.Text = ApiManager.CallApi();
//    KongViewModel kongVm = BotManager.Init(this, userData, true);

//    StringBuilder cardList = new StringBuilder();
//    foreach (var card in kongVm.PlayerCards)
//    {
//        cardList.Append(card.Key.Name);
//        if (card.Value > 1) cardList.Append("#" + card.Value);
//        cardList.Append("\r\n");
//    }

//    importCardsTextBox.Text = cardList.ToString();
//}

///// <summary>
///// Write cards to target player file
///// </summary>
//private void importWriteToPlayerFile_Click(object sender, EventArgs e)
//{
//    if (importPlayerFileComboBox.Text != "")
//    {
//        FileIO.SimpleWrite(this, "data/cards", importPlayerFileComboBox.Text, importCardsTextBox.Text, append: false);
//    }
//}


///// <summary>
///// Import cards tab - Get help on getting this data
///// </summary>
//private void importCardsHelpButton_Click(object sender, EventArgs e)
//{
//    StringBuilder sb = new StringBuilder();
//    sb.AppendLine("--------------------");
//    sb.AppendLine("Chrome Browser");
//    sb.AppendLine("--------------------");
//    sb.AppendLine("");
//    sb.AppendLine("1. Login to Kong and go to the TU page ");
//    sb.AppendLine("(http://www.kongregate.com/games/synapticon/tyrant-unleashed-web)");
//    sb.AppendLine("");
//    sb.AppendLine("2. Hit [F12], or [RightClick -> Inspect]");
//    sb.AppendLine("* This will bring up the Inspect Window");
//    sb.AppendLine("");
//    sb.AppendLine("3. Click [Console] on the top bar");
//    sb.AppendLine("* Look for \"Filters\" right below console. Type \"php\"");
//    sb.AppendLine("");
//    sb.AppendLine("4. Wait for the game to fully load");
//    sb.AppendLine("** If the game loads before the console window, Refresh the page");
//    sb.AppendLine("");
//    sb.AppendLine("5. Look for a line that looks like this. Click it");
//    sb.AppendLine("* https://mobile.tyrantonline.com/api.php?message=getUserAccount&user_id=#####");
//    sb.AppendLine("");
//    sb.AppendLine("* This brings you to the [Network] tab. Click that");
//    sb.AppendLine("api.php?message=getUserAccount&user_id=###");
//    sb.AppendLine("");
//    sb.AppendLine("* Click [Preview] tab. Then Expand [Request]");
//    sb.AppendLine("");
//    sb.AppendLine("");
//    sb.AppendLine("6. Retreive these values:");
//    sb.AppendLine("* kong_id");
//    sb.AppendLine("* kong_name");
//    sb.AppendLine("* kong_token");
//    sb.AppendLine("* password");
//    sb.AppendLine("* syncode");
//    sb.AppendLine("* user_id");
//    sb.AppendLine("");
//    sb.AppendLine("7. Create a file in your tuo folder called \"__users.txt\". Then add this");
//    sb.AppendLine("kongName:xxx,kongId:xxx,kongToken:xxx,apiPassword:xxx,syncode:xxx,userId:xxx");
//    sb.AppendLine("");
//    sb.AppendLine("Restart (or a temp solution: add that string to the player combobox");

//    Helper.Popup(this, sb.ToString());
//}


/// <summary>
/// Refresh -> Sim -> PlayCard -> Refresh
/// Attempt to play next card
/// </summary>
//private async void rumBarrelPlayNextCardButton_Click(object sender, EventArgs e)
//{
//    try
//    {
//        // Parse selectedUser for kong login
//        string userData = rumBarrelPlayerComboBox.Text;
//        int.TryParse(rumBarrelIterationsTextBox.Text, out int iterations);
//        int.TryParse(rumBarrelCardsAheadComboBox.Text, out int extraLockedCards);

//        // Refresh
//        KongViewModel kongVm = BotManager.GetBattle(this, userData);

//        // Populate RumBarrel
//        if (kongVm != null)
//        {
//            // Show data
//            FillOutRumBarrel(kongVm);

//            // Build sim
//            BatchSim batchSim = NewSimManager.BuildLiveSim(kongVm, iterations: iterations, extraLockedCards: extraLockedCards);

//            // Run sim
//            await Task.Run(() => NewSimManager.RunLiveSim(this, batchSim));

//            // Play next card
//            // Look through our simmed deck and find the first card we can legally play
//            Sim sim = batchSim.Sims.OrderByDescending(x => x.WinPercent).ThenByDescending(x => x.WinScore).FirstOrDefault();
//            BotManager.PlayCard(this, kongVm, sim);

//            // Some error happened
//            if (kongVm.Result == "False")
//            {
//                rumBarrelOutputTextBox.AppendText("An error has occured: " + kongVm.ResultMessage + "\r\n");
//                return;
//            }


//            // Fill out RumBarrel again
//            FillOutRumBarrel(kongVm);
//        }
//    }
//    catch (Exception ex)
//    {
//        Helper.OutputWindowMessage(this, "Error on PlayNextCard(): \r\n" + ex);
//    }

//}


///// <summary>
///// Function - Get deck set strings that contain 15 cards and a commander for each player
///// </summary>
//private void functionCampaignButton_Click(object sender, EventArgs e)
//{
//    var result = new StringBuilder();

//    foreach (var player in PlayerManager.Players)
//    {
//        // playerName:#:deck:GuildName
//        result.Append(player.KongName + ":0:");

//        // Commander
//        var commander = CardManager.GetBestCommander(player.Cards);

//        result.Append(commander.Name);
//        if (commander.Fusion_Level == 2) result.Append("-1");
//        result.Append(",");

//        // Cards
//        var cards = player.Cards.Where(x => x.Key.CardType == CardType.Assault.ToString() || x.Key.CardType == CardType.Structure.ToString()).OrderByDescending(x => x.Key.Power).Take(15);
//        var cardsAdded = 0;
//        foreach (var c in cards)
//        {
//            result.Append(c.Key.Name);

//            var cardsToAdd = Math.Min(c.Value, 1);

//            if (cardsToAdd > 1) result.Append("#" + Math.Min(cardsToAdd, 15 - cardsAdded)); //Basically, add the max unless you go over 15
//            result.Append(",");

//            cardsAdded += cardsToAdd;
//            if (cardsAdded >= 15) break;
//        }

//        if (cardsAdded < 15)
//        {
//            var weakCards = player.WeakCards.OrderByDescending(x => x.Key.Power).Take(15);
//            foreach (var c in weakCards)
//            {
//                result.Append(c.Key.Name);
//                if (c.Value > 1) result.Append("#" + Math.Min(c.Value, 15 - cardsAdded)); //Basically, add the max unless you go over 15
//                result.Append(",");

//                cardsAdded += c.Value;
//                if (cardsAdded >= 15) break;
//            }
//        }

//        // Guild sign
//        result.Remove(result.Length - 1, 1);
//        result.Append(":" + player.Guild);
//        result.AppendLine();
//    }

//    toolsOutputTextBox.Text = result.ToString();
//}


/// <summary>
/// Upsert card collection to mongodb
/// </summary>
//public static async Task UpsertCards()
//{
//    try
//    {
//        if (LoggedIn && CONFIG.role == "level3")
//        {
//            // Build the card database from XML files instead of Mongo to apply any new XML changes
//            await CardManager.BuildCardDatabase();

//            var collection = db.GetCollection<BsonDocument>("cards");
//            var models = new WriteModel<BsonDocument>[CardManager.CardTable.Count];
//            var i = 0;

//            // use ReplaceOneModel with property IsUpsert set to true to upsert whole documents
//            foreach (var c in CardManager.CardTable)
//            {
//                var card = c.Value;
//                var bsonDoc = card.ToBsonDocument();
//                models[i] = new ReplaceOneModel<BsonDocument>(
//                    new BsonDocument("Name", card.Name), bsonDoc)
//                { IsUpsert = true };
//                i++;
//            };

//            await collection.BulkWriteAsync(models);
//        }
//    }
//    catch (Exception ex)
//    {
//        Helper.OutputLogMessage("DbManager.UpsertCards(): " + ex);
//    }
//}


/// <summary>
/// Pull cards, except the latest section
/// </summary>
//public static async Task DownloadCards()
//{
//    try
//    {
//        if (LoggedIn)
//        {
//            var collection = db.GetCollection<BsonDocument>("cards");

//            var items = await collection.FindAsync(new BsonDocument());

//            foreach (var item in items.ToList())
//            {
//                var card = BsonSerializer.Deserialize<Card>(item);

//                if (card.Section == 11) continue;

//                var cardName = card.Name.ToLower().Replace(" ", "");
//                CardManager.CardTable.Add(cardName, card);
//            }
//        }
//    }
//    catch (Exception ex)
//    {
//        Helper.OutputLogMessage("DbManager.DownloadCards(): " + ex);
//    }
//}


//#region Live Sim - Simple

///// <summary>
///// Create a simple live sim
///// </summary>
//public static Sim BuildOldLiveSim(MainForm form, int id)
//{
//    bool useTuoX86 = false;

//    try
//    {

//        // Turns
//        if (!int.TryParse(form.rumBarrelTurnTextBox.Text, out int turns)) turns = 0;

//        // Surge or Battle mode
//        bool surgeMode = true;
//        if (form.rumBarrelGameModeLabel.Text == "Battle") surgeMode = false;

//        // How many cards to freeze. t/2 will work for battle and surge
//        int cardsToFreeze = surgeMode ? ((turns - 1) / 2) : (turns / 2);

//        // Cards in play/hand/deck
//        string myCardsInPlay = form.rumBarrelMyDeckTextBox.Text.Replace("\r\n", ", ");
//        string myCardsInHand = form.rumBarrelPlayerHandTextBox.Text.Replace("\r\n", ", ");
//        string enemyDeck = form.rumBarrelEnemyDeckTextBox.Text.Replace("\r\n", ", ").TrimEnd(',');
//        string enemyPossibleCards = form.rumBarrelEnemyDeckPossibleRemainingCardsTextBox.Text.Replace("\r\n", ",").TrimEnd(',');

//        Deck finalMyDeck = new Deck(myCardsInPlay + ", " + myCardsInHand);
//        string finalEnemyDeck = enemyDeck; //+ ", " + enemyPossibleCards;

//        Sim sim = new Sim
//        {
//            Id = id,
//            Mode = SimMode.LIVESIM_BASIC,
//            Player = form.rumBarrelKongNameLabel.Text,
//            Guild = "",

//            // Usually tuo
//            TuoExecutable = "tuo",
//            UseX86 = useTuoX86,

//            // Decks
//            MyDeck = finalMyDeck,
//            EnemyDeck = finalEnemyDeck,

//            // Towers
//            MyDominion = form.rumBarrelMyDominionTextBox.Text,
//            EnemyDominion = form.rumBarrelEnemyDominionTextBox.Text,
//            MyForts = form.rumBarrelMyFortTextBox.Text,
//            EnemyForts = form.rumBarrelEnemyFortTextBox.Text,

//            // Bges
//            Bge = form.rumBarrelBgeTextBox.Text,
//            MyBge = form.rumBarrelMyBgeTextBox.Text,
//            EnemyBge = form.rumBarrelEnemyBgeTextBox.Text,

//            // GameMode / Operation
//            GameMode = surgeMode ? "surge ordered" : "pvp ordered",
//            Operation = "climb 1000",
//            Iterations = 1000,

//            Freeze = cardsToFreeze,
//            Hand = myCardsInHand,

//            ExtraTuoFlags = "enemy:exact-ordered",

//        };

//        // Try to find Guild/Player
//        //BuildSim_FindGuildAndPlayer(sim);

//        return sim;
//    }
//    catch (Exception ex)
//    {
//        return new Sim { StatusMessage = "Syntax error in BuildSim_LiveSimple(): " + ex };
//    }
//}

///// <summary>
///// Run a simple live sim
///// </summary>
//public static void RunLiveSim_Simple(MainForm form, Sim sim)
//{
//    // cmd.exe or powershell
//    string commandLine = "cmd.exe";
//    string simString = sim.SimToString();
//    List<string> tuoOutput = new List<string>();

//    form.rumBarrelOutputTextBox.AppendText(simString + "\r\n");

//    // 5 second timeout
//    int timeout = 5000;

//    try
//    {
//        // Create a process to run TUO
//        var process = new Process();
//        var processStartInfo = new ProcessStartInfo
//        {
//            FileName = commandLine,
//            Arguments = "/c " + simString + "",
//            UseShellExecute = false,
//            CreateNoWindow = true,
//            RedirectStandardOutput = true,
//        };
//        process.StartInfo = processStartInfo;


//        // Capture TUO console window output to simResult
//        process.OutputDataReceived += new DataReceivedEventHandler
//        (
//            delegate (object sender, DataReceivedEventArgs e)
//            {
//                        // append the new data to the data already read-in
//                        if (e.Data != null)
//                    tuoOutput.Add(e.Data);
//            }
//        );


//        // Run the process until waitTime (ms)
//        process.Start();
//        process.BeginOutputReadLine();
//        var processId = process.Id;
//        process.WaitForExit(timeout);

//        KillProcessAndChildren(processId);

//        // Handle and write the result of the sim
//        RunLiveSim_GetResult(form, sim, tuoOutput);
//        WriteLiveSim_Simple(form, sim);

//    }
//    catch (Exception ex)
//    {
//        Helper.OutputWindowMessage(form, "RunSimStrings(): Error on sim: " + ex);
//    }
//}

///// <summary>
///// Write a LiveSim result to the output window
///// </summary>
//public static void WriteLiveSim_Simple(MainForm form, Sim sim)
//{
//    // WinRate 
//    // FrozenCards, [Next three], Remaining cards
//    form.rumBarrelOutputTextBox.AppendText("\r\n" + "Win Rate: " + sim.WinPercent.ToString() + "\r\n");

//    // A really terrible way to separate cards
//    string modifiedPlayerDeck = "";
//    List<string> playerCards = sim.ResultDeck.Split(',').ToList();
//    List<string> playerCardsUncompressed = TextCleaner.UncompressDeck(playerCards);

//    for (int i = 0; i < playerCardsUncompressed.Count; i++)
//    {
//        var card = playerCardsUncompressed[i];

//        //+Commander/Dominion
//        if (i < sim.Freeze + 2)
//        {
//            modifiedPlayerDeck += card + ", ";
//        }
//        else
//        {
//            modifiedPlayerDeck += "\r\n[" + card + "]";
//        }
//    }

//    form.rumBarrelOutputTextBox.AppendText(modifiedPlayerDeck + "\r\n");
//}

//#endregion


///// <summary>
///// Attempts to find the player/guild this sim belongs to
///// </summary>
//public static void BuildSim_FindGuildAndPlayer(Sim sim)
//{
//    var simString = sim.SimToString();
//    var regex = new Regex(@"data\/cards\/..?_(.*?)\.txt");
//    var match = regex.Match(simString);

//    // Try to find player
//    if (match.Success)
//    {
//        sim.Player = match.Groups[1].Value.Replace(".txt ", "");
//    }

//    // Try to find guild
//    if (simString.Contains("cards/DT_")) sim.Guild = "DT";
//    if (simString.Contains("cards/WH_")) sim.Guild = "WH";
//    if (simString.Contains("cards/TW_")) sim.Guild = "TW";
//    if (simString.Contains("cards/WT_")) sim.Guild = "WT";
//    if (simString.Contains("cards/LK_")) sim.Guild = "LK";
//    if (simString.Contains("cards/FAP_")) sim.Guild = "FAP";
//}


///// <summary>
///// After running batch sims, handle the output
///// </summary>
//private static void ProcessResults(MainForm form, TextBox outputTextBox, List<SimResult> simResults, bool navigatorMetrics = false)
//{
//    var resultOutput = new StringBuilder();

//    if (simResults.Count > 0)
//    {
//        var averageWinPercent = simResults.Average(x => x.WinPercent); //Average win
//        var worstDecks = simResults.Where(x => x.WinPercent < 50).OrderBy(x => x.WinPercent).ToList(); //Navigator sim: Decks < 50%

//        resultOutput.AppendLine("\n----");
//        resultOutput.AppendLine(simResults.Count + " results");
//        resultOutput.AppendLine("Average win(%): " + Math.Round(averageWinPercent, 1));
//        //if (outputFormat == "Synthesis")

//        if (navigatorMetrics == true)
//        {
//            resultOutput.AppendLine("These 5 decks were the hardest");
//            foreach (var deck in simResults)
//            {
//                resultOutput.AppendLine(Math.Round(deck.WinPercent, 1) + ": " + deck.EnemyDeck);
//            }
//        }
//    }
//    else
//    {
//        ControlExtensions.InvokeEx(outputTextBox, x => outputTextBox.AppendText("No sim results seen"));
//    }

//    ControlExtensions.InvokeEx(outputTextBox, x => outputTextBox.AppendText(resultOutput.ToString()));

//}

