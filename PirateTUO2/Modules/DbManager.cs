using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using PirateTUO2.Classes;
using PirateTUO2.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
// nuget Install-Package MongoDB.Driver


namespace PirateTUO2.Modules
{
    /// <summary>
    /// Manages connecting to mongo
    /// </summary>
    class DbManager
    {
        private static MongoClient client;
        private static IMongoDatabase db;

        /// <summary>
        /// Class mappings
        /// </summary>
        public static void Init()
        {
            BsonClassMap.RegisterClassMap<AdminData>(cm => { cm.AutoMap(); });
            BsonClassMap.RegisterClassMap<Card>(cm => { cm.AutoMap(); }); //cm.SetIgnoreExtraElements(true);
        }

        /// <summary>
        /// Login 
        /// </summary>
        public static bool Login(MainForm form)
        {
            string user = CONFIG.userName;
            string password = CONFIG.password;
            string uri = "mongodb://" + user + ":" + password + "@ds013486.mlab.com:13486/bootybase";

            try {

                // Attempt to login, and test it by connecting to a simple collection
                client = new MongoClient(uri);
                db = client.GetDatabase("bootybase");

                CheckAuthentication(user, password);
                
                // Get role
                SetRole(form);

                // Record successful login
                RecordLogin();
                

                CONFIG.LoggedIn = true;
                return true;
            }
            catch(Exception ex)
            {
                ControlExtensions.InvokeEx(form.outputTextBox, x => form.outputTextBox.AppendText("MongoDb login failed: " + ex.Message));
                ControlExtensions.InvokeEx(form.outputTextBox, x => form.outputTextBox.AppendText("DbManager.Login(): " + ex));

                CONFIG.LoggedIn = false;
                return false;
            }
        }

        public static void CheckAuthentication(string user, string password)
        {
            try
            {
                MongoCredential credential = MongoCredential.CreateCredential("bootybase", user, password);
                MongoServerAddress server = new MongoServerAddress("ds013486.mlab.com", 13486);

                MongoClientSettings clientSettings = new MongoClientSettings()
                {
                    Credentials = new[] { credential },                    
                    WaitQueueTimeout = new TimeSpan(0, 0, 0, 90),
                    ConnectTimeout = new TimeSpan(0, 0, 0, 90),
                    Server = server,
                    ClusterConfigurator = builder =>
                    {
                        //The "Normal" Timeout settings are for something different. This one here really is relevant when it is about
                        //how long it takes until we stop, when we cannot connect to the MongoDB Instance
                        //https://jira.mongodb.org/browse/CSHARP-1018, https://jira.mongodb.org/browse/CSHARP-1231
                        builder.ConfigureCluster(
                            settings => settings.With(serverSelectionTimeout: TimeSpan.FromSeconds(90)));
                    }
                };

                var testMongoClient = new MongoClient(clientSettings);
                var testDatabase = testMongoClient.GetDatabase("bootybase");
                var testCmd = new BsonDocument("usersInfo", CONFIG.userName);
                var queryResult = testDatabase.RunCommand<BsonDocument>(testCmd);

            }
            catch (TimeoutException ex)
            {
                if (ex.Message.Contains("Authentication failed"))
                {
                    Console.WriteLine("CheckAuthentication(): Authentication failed - " + ex.Message);
                    throw new Exception("Authentication failed: " + ex.Message);
                }
                Console.WriteLine("CheckAuthentication(): " + ex.Message);
                throw new Exception("CheckAuthentication(): " + ex.Message);
            }
        }

        #region Download

        /// <summary>
        /// Based on the user's role, downloads files from the server
        /// 
        /// * CSV files for card inventories
        /// * Gauntlets
        /// * Config files
        /// Returns "success", or errors 
        /// </summary>
        public static async Task<string> DownloadFiles(MainForm form)
        {
            IMongoCollection<BsonDocument> collection;
            IAsyncCursor<BsonDocument> documents;

            string result = "";

            // Check Login and Role
            if (!CONFIG.LoggedIn)
            {
                return "Error: DownloadFiles(): Not logged into the database";
            }
            if (CONFIG.overrideNormalLogin)
            {
                return "Warning: Overrode login: Not pulling any new files";
            }

            if (CONFIG.role != "level0" && CONFIG.role != "level1" && CONFIG.role != "level2" && CONFIG.role != "level3" &&
                CONFIG.role != "newLevel0" && CONFIG.role != "newLevel1" && CONFIG.role != "newLevel2" && CONFIG.role != "newLevel3")
            {
                return "Warning - Current user does not have permissions";
            }

            // DEBUG
            //CONFIG.role = "level2"; 

            // Level 0 files
            try
            {
                if (CONFIG.role == "level0" || CONFIG.role == "level1" || CONFIG.role == "level2" || CONFIG.role == "level3" ||
                    CONFIG.role == "newLevel0" || CONFIG.role == "newLevel1" || CONFIG.role == "newLevel2" || CONFIG.role == "newLevel3")
                {
                    collection = db.GetCollection<BsonDocument>("level0");
                    documents = await collection.FindAsync(new BsonDocument());

                    foreach (var document in documents.ToList())
                    {
                        var docName = document["Name"].ToString();
                        var content = document["Content"].ToString();

                        switch (docName)
                        {
                            // Whitelist files
                            case "_boxes.txt":
                            case "_commanders.txt":
                            case "_fusions.txt":
                            case "_singles.txt":
                                FileIO.SimpleWrite(form, "config/card-addons", docName, content);
                                break;
                            // Config items
                            case "appsettings.txt":
                            case "cardpower.txt":
                            case "changelog.txt":
                            case "help.txt":
                            case "customcards1.txt":
                            case "customcards2.txt":
                            case "customcards3.txt":
                            case "customcards4.txt":
                            case "customcards5.txt":
                            case "customcards6.txt":
                                FileIO.SimpleWrite(form, "config", docName, content);
                                break;

                            case "seeds-general.txt":
                            case "seeds-tw.txt":
                            case "seeds-mj.txt":
                            case "seeds-wh.txt":
                            case "seeds-tfk.txt":
                                FileIO.SimpleWrite(form, "config", docName, content);

                                // Save player seeds
                                List<string> seeds = FileIO.SimpleRead(form, "config/" + docName, returnCommentedLines: false);
                                foreach (var seed in seeds)
                                {
                                    string[] playerSeed = seed.Split(':');
                                    if (playerSeed.Length != 3) continue;

                                    string name = playerSeed[0].Trim();
                                    string seedType = playerSeed[1].Trim();
                                    string deck = playerSeed[2].Trim();

                                    CONFIG.PlayerConfigSeeds.Add(new Tuple<string, string, string>(name, seedType, deck));
                                }
                                break;

                            case "level1.txt":
                            case "level2.txt":
                            case "level3.txt":
                            case "level2reverse.txt":
                            case "level3reverse.txt":
                                FileIO.SimpleWrite(form, "config/whitelist", docName, content);
                                break;

                            // Gauntlets + Data files
                            case "bges.txt":
                            case "cardabbrs.txt":
                            case "raids.xml":
                            case "customdecks_brawl.txt":
                            case "customdecks_cq.txt":
                            case "customdecks_campaign.txt":
                            case "customdecks_pvp.txt":
                            case "customdecks_war.txt":
                                FileIO.SimpleWrite(form, "data", docName, content);
                                break;

                            // Manually uploaded enemy logs
                            case "hardlogs.txt":
                                FileIO.SimpleWrite(form, "config", docName, content);
                                break;

                            // Card csvs - process later
                            //case "cards_ForActivePlayers":
                            //case "cards_LadyKillerz":
                            //case "cards_LethalHamsters":
                            //case "cards_WarHungry":
                            //case "cards_WarThirsty":
                            case "cards_TidalWave":
                                CONFIG.playerCsvURLs.Add(content);
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result += "DownloadFiles(): Level 0 files - Failed on downloading or writing a file - " + ex + "\r\n";
            }

            // Level 1 files
            try
            {
                if (CONFIG.role == "level1" || CONFIG.role == "level2" || CONFIG.role == "level3" ||
                    CONFIG.role == "newLevel1" || CONFIG.role == "newLevel2" || CONFIG.role == "newLevel3")
                {
                    collection = db.GetCollection<BsonDocument>("level1");
                    documents = await collection.FindAsync(new BsonDocument());


                    foreach (var document in documents.ToList())
                    {
                        var docName = document["Name"].ToString();
                        var content = document["Content"].ToString();

                        switch (docName)
                        {
                            default:
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result += "DownloadFiles(): Level 1 files - Failed on downloading or writing a file - " + ex + "\r\n";
            }

            // Level 2 files
            try
            {
                if (CONFIG.role == "level2" || CONFIG.role == "level3" || 
                    CONFIG.role == "newLevel2" || CONFIG.role == "newLevel3")
                {
                    collection = db.GetCollection<BsonDocument>("level2");
                    documents = await collection.FindAsync(new BsonDocument());


                    foreach (var document in documents.ToList())
                    {
                        var docName = document["Name"].ToString();
                        var content = document["Content"].ToString();

                        switch (docName)
                        {
                            // Config items
                            case "seeds-dt.txt":
                                FileIO.SimpleWrite(form, "config", docName, content);

                                // Save player seeds
                                List<string> seeds = FileIO.SimpleRead(form, "config/seeds-dt.txt", returnCommentedLines: false);
                                foreach (var seed in seeds)
                                {
                                    var playerSeed = seed.Split(':');
                                    if (playerSeed.Length != 3) continue;

                                    var name = playerSeed[0].Trim();
                                    var seedType = playerSeed[1].Trim();
                                    var deck = playerSeed[2].Trim();

                                    CONFIG.PlayerConfigSeeds.Add(new Tuple<string, string, string>(name, seedType, deck));
                                }
                                break;

                            // Gauntlets + Data files
                            case "customdecks_warbig.txt":
                                FileIO.SimpleWrite(form, "data", docName, content);
                                break;

                            // Recent logs
                            case "recentlogs":
                                // Open the remote csv file
                                var request = (HttpWebRequest)WebRequest.Create(content);
                                var response = await request.GetResponseAsync();
                                var stream = response.GetResponseStream();
                                var output = new StreamReader(stream);

                                var contents = output.ReadToEnd();
                                FileIO.SimpleWrite(form, "./config", "recentlogs.txt", contents);
                                CONFIG.playerCsvs.Add(contents);

                                break;

                            // Card csvs - process later
                            case "cards_DireTide":
                                CONFIG.playerCsvURLs.Add(content);
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result += "DownloadFiles(): Level 2 files - Failed on downloading or writing a file - " + ex + "\r\n";
            }

            // Level 3 files
            try
            {
                if (CONFIG.role == "level3" || CONFIG.role == "newLevel3")
                {
                    collection = db.GetCollection<BsonDocument>("level3");
                    documents = await collection.FindAsync(new BsonDocument());


                    foreach (var document in documents.ToList())
                    {
                        var docName = document["Name"].ToString();
                        var content = document["Content"].ToString();

                        switch (docName)
                        {
                            // Gauntlets + Data files
                            case "customdecks_c3.txt":
                            case "customdecks_cryo.txt":
                            case "customdecks_reck.txt":
                                FileIO.SimpleWrite(form, "data", docName, content);
                                break;

                            // Card 
                            case "officer-sniffer":
                                CONFIG.DeckSnifferUrl = content;
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result += "DownloadFiles(): Level 3 files - Failed on downloading or writing a file - " + ex + "\r\n";
            }

            // Dowload all csv files to turn into player inventories later
            await DownloadCsvs();

            // If no file downloads threw an error
            if (result == "") result = "Success";

            // Return the status of this
            return result;
        }


        /// <summary>
        /// Download just decklogs
        /// </summary>
        public static string DownloadLogs(MainForm form)
        {
            string result = "";

            // Check Login and Role
            if (!CONFIG.LoggedIn)
            {
                return "Error: DownloadLogs(): Not logged into the database";
            }
            if (CONFIG.overrideNormalLogin)
            {
                return "Warning: Overrode login: Not pulling any new files";
            }

            if (CONFIG.role != "level0" && CONFIG.role != "level1" && CONFIG.role != "level2" && CONFIG.role != "level3" && 
                CONFIG.role != "newLevel1" && CONFIG.role != "newLevel2" && CONFIG.role != "newLevel3")
            {
                return "Warning - Current user does not have permissions";
            }

            // DEBUG
            //CONFIG.role = "level2"; 
            
            // Level 2 files
            try
            {
                if (CONFIG.role == "level2" || CONFIG.role == "level3" ||
                    CONFIG.role == "newLevel2" || CONFIG.role == "newLevel3")
                {
                    var collection = db.GetCollection<BsonDocument>("level2");
                  
                    var documents = collection.Find(new BsonDocument());

                    foreach (var document in documents.ToList())
                    {
                        var docName = document["Name"].ToString();
                        var content = document["Content"].ToString();

                        switch (docName)
                        {
                            // Recent logs
                            case "recentlogs":
                                // Open the remote csv file
                                var request = (HttpWebRequest)WebRequest.Create(content);
                                var response = request.GetResponse();
                                var stream = response.GetResponseStream();
                                var output = new StreamReader(stream);

                                var contents = output.ReadToEnd();
                                FileIO.SimpleWrite(form, "./config", "recentlogs.txt", contents);
                                CONFIG.playerCsvs.Add(contents);
                                return "Log files updated from server. Existing data erased.\r\n";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result += "DownloadFiles(): Level 2 files - Failed on downloading or writing a file - " + ex + "\r\n";
            }

            // If no file downloads threw an error
            if (result == "") result = "Success";

            // Return the status of this
            return result;
        }

        /// <summary>
        /// Download card csvs 
        /// **TODO**: This is from Google Drive. Get from the ogre
        /// </summary>
        private static async Task<string> DownloadCsvs()
        {
            string result = "";

            foreach (var url in CONFIG.playerCsvURLs)
            {
                try
                {
                    // Open the remote csv file
                    var request = (HttpWebRequest)WebRequest.Create(url);
                    var response = await request.GetResponseAsync();
                    var stream = response.GetResponseStream();
                    var output = new StreamReader(stream);

                    var contents = output.ReadToEnd();
                    CONFIG.playerCsvs.Add(contents);
                }
                catch (Exception ex)
                {
                    result += "DownloadCsvs(): Failed on " + url + " - " + ex;
                }
            }
            return "";
        }

        #endregion

        #region Upload

        /// <summary>
        /// Define files to upload to the database
        /// </summary>
        public static async Task<string> UploadFiles(MainForm form)
        {
            StringBuilder result = new StringBuilder();

            try
            {

                // --------------------------------
                // Level 0 - Config files
                // --------------------------------
                if (form.adminAppSettingsCheckBox.Checked)
                    result.AppendLine(await UploadOneFile("config", "appsettings.txt", "level0"));

                if (form.adminUploadBgesCheckBox.Checked)
                    result.AppendLine(await UploadOneFile("data", "bges.txt", "level0"));

                if (form.adminUploadCardAbbrsCheckBox.Checked)
                    result.AppendLine(await UploadOneFile("data", "cardabbrs.txt", "level0"));

                if (form.adminUploadRaidsCheckBox.Checked)
                    result.AppendLine(await UploadOneFile("data", "raids.xml", "level0"));

                if (form.adminUploadHelpCheckbox.Checked)
                    result.AppendLine(await UploadOneFile("config", "help.txt", "level0"));

                if (form.adminCardpowerCheckBox.Checked)
                    result.AppendLine(await UploadOneFile("config", "cardpower.txt", "level0"));


                if (form.adminUploadCustomCards1CheckBox.Checked)
                    result.AppendLine(await UploadOneFile("config", "customcards1.txt", "level0"));
                if (form.adminUploadCustomCards2CheckBox.Checked)
                    result.AppendLine(await UploadOneFile("config", "customcards2.txt", "level0"));
                if (form.adminUploadCustomCards3CheckBox.Checked)
                    result.AppendLine(await UploadOneFile("config", "customcards3.txt", "level0"));
                if (form.adminUploadCustomCards4CheckBox.Checked)
                    result.AppendLine(await UploadOneFile("config", "customcards4.txt", "level0"));
                if (form.adminUploadCustomCards5CheckBox.Checked)
                    result.AppendLine(await UploadOneFile("config", "customcards5.txt", "level0"));
                if (form.adminUploadCustomCards6CheckBox.Checked)
                    result.AppendLine(await UploadOneFile("config", "customcards6.txt", "level0"));

                // --------------------------------
                // Level 0 - Box/Extracards
                // --------------------------------
                if (form.adminUploadBoxesCheckBox.Checked)
                    result.AppendLine(await UploadOneFile("config/card-addons", "_boxes.txt", "level0"));

                if (form.adminUploadCommandersCheckBox.Checked)
                    result.AppendLine(await UploadOneFile("config/card-addons", "_commanders.txt", "level0"));

                if (form.adminUploadFusionsCheckBox.Checked)
                    result.AppendLine(await UploadOneFile("config/card-addons", "_fusions.txt", "level0"));

                if (form.adminUploadSinglesCheckBox.Checked)
                    result.AppendLine(await UploadOneFile("config/card-addons", "_singles.txt", "level0"));


                // --------------------------------
                // Level 0 - Card Whitelist
                // --------------------------------
                if (form.adminUploadLevel1Checkbox.Checked)
                    result.AppendLine(await UploadOneFile("config/whitelist", "level1.txt", "level0"));

                if (form.adminUploadLevel2Checkbox.Checked)
                    result.AppendLine(await UploadOneFile("config/whitelist", "level2.txt", "level0"));

                if (form.adminUploadLevel2ReverseCheckbox.Checked)
                    result.AppendLine(await UploadOneFile("config/whitelist", "level2reverse.txt", "level0"));

                if (form.adminUploadLevel3Checkbox.Checked)
                    result.AppendLine(await UploadOneFile("config/whitelist", "level3.txt", "level0"));

                if (form.adminUploadLevel3ReverseCheckbox.Checked)
                    result.AppendLine(await UploadOneFile("config/whitelist", "level3reverse.txt", "level0"));


                // --------------------------------
                // Level 0 - Gauntlets
                // --------------------------------

                if (form.adminUploadBrawlCheckBox.Checked)
                    result.AppendLine(await UploadOneFile("data", "customdecks_brawl.txt", "level0"));

                if (form.adminUploadCqCheckBox.Checked)
                    result.AppendLine(await UploadOneFile("data", "customdecks_cq.txt", "level0"));

                if (form.adminUploadCampaignCheckBox.Checked)
                    result.AppendLine(await UploadOneFile("data", "customdecks_campaign.txt", "level0"));

                if (form.adminUploadPvpCheckBox.Checked)
                    result.AppendLine(await UploadOneFile("data", "customdecks_pvp.txt", "level0"));

                if (form.adminUploadWarCheckBox.Checked)
                    result.AppendLine(await UploadOneFile("data", "customdecks_war.txt", "level0"));

                if (form.adminUploadChangelogCheckbox.Checked)
                    result.AppendLine(await UploadOneFile("config", "changelog.txt", "level0"));

                // --------------------------------
                // Level 0 - Seeds
                // --------------------------------
                if (form.adminUploadSeedsGeneralCheckBox.Checked)
                    result.AppendLine(await UploadOneFile("config", "seeds-general.txt", "level0"));
                if (form.adminUploadSeedsTwCheckBox.Checked)
                    result.AppendLine(await UploadOneFile("config", "seeds-tw.txt", "level0"));
                if (form.adminUploadSeedsTfkCheckBox.Checked)
                    result.AppendLine(await UploadOneFile("config", "seeds-tfk.txt", "level0"));
                if (form.adminUploadSeedsWhCheckBox.Checked)
                    result.AppendLine(await UploadOneFile("config", "seeds-wh.txt", "level0"));
                if (form.adminUploadSeedsMjCheckBox.Checked)
                    result.AppendLine(await UploadOneFile("config", "seeds-mj.txt", "level0"));

                // --------------------------------
                // Level 0 - Logs
                // --------------------------------
                if (form.adminHardLogsCheckBox.Checked)
                    result.AppendLine(await UploadOneFile("config", "hardlogs.txt", "level0"));


                // --------------------------------
                // Level 1 - Protected WH data
                // --------------------------------

                // --------------------------------
                // Level 2 - Protected DT data
                // --------------------------------
                if (form.adminUploadSeedsDtCheckBox.Checked)
                    result.AppendLine(await UploadOneFile("config", "seeds-dt.txt", "level2"));

                if (form.adminUploadWarbigCheckBox.Checked)
                    result.AppendLine(await UploadOneFile("data", "customdecks_warbig.txt", "level2"));


                // --------------------------------
                // Level 3 - Protected officer data
                // --------------------------------
                if (form.adminUploadC3CheckBox.Checked)
                    result.AppendLine(await UploadOneFile("data", "customdecks_c3.txt", "level3"));
                
                if (form.adminUploadCryoCheckBox.Checked)
                    result.AppendLine(await UploadOneFile("data", "customdecks_cryo.txt", "level3"));

                if (form.adminUploadReckCheckBox.Checked)
                    result.AppendLine(await UploadOneFile("data", "customdecks_reck.txt", "level3"));
                
                return result.ToString();
            }
            catch(Exception ex)
            {
                return "Avast! Error on upload - " + ex.ToString();
            }
        }


        /// <summary>
        /// Upload target file to target collection
        /// </summary>
        private static async Task<string> UploadOneFile(string filePath, string fileName, string collectionName)
        {
            // Mongo objects
            var mongoCollection = db.GetCollection<BsonDocument>(collectionName);
            var models = new WriteModel<BsonDocument>[1];
            
            // If this file exists, attempt to upload it
            if (File.Exists(filePath + "/" + fileName))
            {
                using (var reader = new StreamReader(filePath + "/" + fileName))
                {
                    // Create an item for the collection
                    var item = new MongoBsonDoc
                    {
                        Name = fileName,
                        UploadDate = DateTime.Now.ToString(),
                        Content = reader.ReadToEnd()
                    };

                    // Convert the item to a bsondoc
                    var bsonDoc = item.ToBsonDocument();

                    // Instruct the model to upsert
                    models[0] = new ReplaceOneModel<BsonDocument>(new BsonDocument("Name", item.Name), bsonDoc) { IsUpsert = true };
                }

                // Upsert file
                await mongoCollection.BulkWriteAsync(models);
            }
            else
            {
                return DateTime.Now.ToShortTimeString() + ": " + fileName + " does not exist";
            }

            return DateTime.Now.ToShortTimeString() + ": Uploaded " + fileName;
        }

        #endregion

        #region Helpers


        /// <summary>
        /// Record a login
        /// </summary>
        public static async void RecordLogin()
        {
            // Ignored user, don't record
            if (CONSTANTS.DATABASE_LOGINS_TO_IGNORE.Contains(CONFIG.userName)) return;

            
            // Get IP address
            string ipAddress = "";
            try
            {
                ipAddress = (new WebClient()).DownloadString("http://checkip.dyndns.org/");
                ipAddress = (new Regex(@"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}")).Matches(ipAddress)[0].ToString();
            }
            catch { ipAddress = "NoIP"; }

            var loginRecord = new
            {
                Name = CONFIG.userName,
                //Date = DateTimeOffset.UtcNow.ToString("MM/dd/yy H:mm"),
                Date = DateTimeOffset.UtcNow.ToString("MM/dd/yy H:mm", CultureInfo.InvariantCulture),
                IpAddress = ipAddress,
                ApiData = "LOGIN"
            };

            // Write - permission to upsert is borked
            try
            {
                var collection = db.GetCollection<BsonDocument>("login");
                //var models = new WriteModel<BsonDocument>[1];
                var document = loginRecord.ToBsonDocument();

                await collection.InsertOneAsync(document);
                //models[0] = new ReplaceOneModel<BsonDocument>(new BsonDocument("IpAddress", loginRecord.IpAddress), bsonDoc) { IsUpsert = true };
                //await collection.BulkWriteAsync(models);

                //var collection = db.GetCollection<BsonDocument>("login");
                //var models = new WriteModel<BsonDocument>[1];
                //var document = loginRecord.ToBsonDocument();

                //await collection.InsertOneAsync(document);
                //models[0] = new ReplaceOneModel<BsonDocument>(new BsonDocument("IpAddress", loginRecord.IpAddress), document) { IsUpsert = true };
                //await collection.BulkWriteAsync(models);
            }
            catch (Exception ex)
            {
                Console.WriteLine("PostLogin() failed:" + ex);
            }
        }

        /// <summary>
        /// Record an action
        /// </summary>
        public static async void RecordAction(string apiData = "", string userId = "")
        {
            // Ignore actions without userid
            if (string.IsNullOrWhiteSpace(userId)) return;
            userId = userId.Replace("=", "");

            // Ignored user, don't record
            if (CONSTANTS.DATABASE_API_CALLS_TO_IGNORE.Contains(CONFIG.userName)) return;

            
            string ipAddress = "";

            // Get IP address
            try
            {
                ipAddress = (new WebClient()).DownloadString("http://checkip.dyndns.org/");
                ipAddress = (new Regex(@"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}")).Matches(ipAddress)[0].ToString();
            }
            catch { ipAddress = "NoIP"; }


            //TODO: Replace some of the excess data in ApiData

            var loginRecord = new
            {
                Name = CONFIG.userName,
                //Date = DateTimeOffset.Now.ToString("MM/dd/yy H:mm"),
                Date = DateTimeOffset.UtcNow.ToString("MM/dd/yy H:mm", CultureInfo.InvariantCulture),
                IpAddress = ipAddress,
                ApiData = userId + ": " + apiData
            };

            try
            {
                var collection = db.GetCollection<BsonDocument>("login");
                //var models = new WriteModel<BsonDocument>[1];
                var document = loginRecord.ToBsonDocument();

                await collection.InsertOneAsync(document);
                //models[0] = new ReplaceOneModel<BsonDocument>(new BsonDocument("IpAddress", loginRecord.IpAddress), bsonDoc) { IsUpsert = true };
                //await collection.BulkWriteAsync(models);
            }
            catch (Exception ex)
            {
                Console.WriteLine("LogLog() failed:" + ex);
            }
        }
        
        // After logging in, sets the user's role
        private static void SetRole(MainForm form)
        {
            try
            {
                // Get user role
                var cmd = new BsonDocument("usersInfo", CONFIG.userName);
                var queryResult = db.RunCommand<BsonDocument>(cmd);
                var roles = (BsonArray)queryResult[0][0]["roles"];
                var result = from roleDetail in roles select new { Role = roleDetail["role"].AsBsonValue.ToString(), RoleDB = roleDetail["db"].AsBsonValue.ToString() };

                CONFIG.role = result.FirstOrDefault()?.Role;

            }
            catch(Exception ex)
            {
                ControlExtensions.InvokeEx(form.outputTextBox, x => form.outputTextBox.AppendText("SetRole() Could not find role for user! " + ex.Message));
                CONFIG.role = "level1";
            }
        }

        #endregion



    }
}
