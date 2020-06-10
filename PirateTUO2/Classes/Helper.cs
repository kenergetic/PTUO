using PirateTUO2.Modules;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace PirateTUO2
{
    /// <summary>
    /// Helper methods to Pirate forms
    /// </summary>
    public static class Helper
    {

        // Stupid class to increment a fake ID for sim objects
        private static int Id = 0;
        public static int GetNewId()
        {
            Id++;
            return Id;
        }

        /// <summary>
        /// Randomize a list
        /// </summary>
        public static void Shuffle<T>(this IList<T> list)
        {
            RNGCryptoServiceProvider provider = new RNGCryptoServiceProvider();
            int n = list.Count;
            while (n > 1)
            {
                byte[] box = new byte[1];
                do provider.GetBytes(box);
                while (!(box[0] < n * (Byte.MaxValue / n)));
                int k = (box[0] % n);
                n--;
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        /// <summary>
        /// Deep copies an object
        /// </summary>
        public static T DeepCopy<T>(T other)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(ms, other);
                ms.Position = 0;
                return (T)formatter.Deserialize(ms);
            }
        }

        /// <summary>
        /// Open a popup
        /// </summary>
        public static void Popup(MainForm mainForm, string text = "Default message", string title = "Avast ye pirate!")
        {
            var form = new PopupForm();
            form.Text = title;
            form.popupTextBox.Text = text;
            form.Show(mainForm);
        }

        /// <summary>
        /// Output error to sim tab 1
        /// </summary>
        public static void OutputWindowMessage(MainForm mainForm, string text = "Default message", string title = "Avast ye pirate!")
        {
            mainForm.outputTextBox.AppendText("---\r\n" + title + "\r\n" + text + "\r\n---\r\n");
        }

        /// <summary>
        /// Translate a Conquest Zone ID to a PTUO Zone string
        /// * Deprecated - Conquest event is no longer active
        /// </summary>
        public static int GetConquestZoneId(string zoneName)
        {
            int zoneId = 0;
            switch (zoneName)
            {
                case "T2 - NEXUS":
                    zoneId = 12;
                    break;

                case "T2 - Jotun":
                    zoneId = 3;
                    break;
                case "T2 - Ashrock":
                    zoneId = 21;
                    break;
                case "T2 - Baron":
                    zoneId = 20;
                    break;
                case "T2 - SkyCom":
                case "T2 - Skycom":
                    zoneId = 7;
                    break;
                case "T2 - Spire":
                    zoneId = 1;
                    break;
                case "T2 - Borean":
                    zoneId = 16;
                    break;
                case "T2 - RedMaw":
                    zoneId = 8;
                    break;
                case "T2 - Brood":
                    zoneId = 6;
                    break;
                case "T2 - Magma":
                    zoneId = 13;
                    break;
                case "T2 - Andar":
                    zoneId = 18;
                    break;


                case "T1 - PHOBOS":
                    zoneId = 11;
                    break;
                case "T1 - Elder Port":
                    zoneId = 19;
                    break;
                case "T1 - Mech Graveyard":
                    zoneId = 9;
                    break;
                case "T1 - Infested":
                    zoneId = 5;
                    break;
                case "T1 - Norhaven":
                    zoneId = 2;
                    break;
                case "T1 - Enclave":
                    zoneId = 17;
                    break;
                case "T1 - Cleave":
                    zoneId = 14;
                    break;
                case "T1 - Malort's":
                    zoneId = 15;
                    break;
                case "T1 - Seismic":
                    zoneId = 10;
                    break;
                case "T1 - Tyrolian":
                    zoneId = 4;
                    break;
                case "T1 - Colonial":
                    zoneId = 22;
                    break;
            }
            return zoneId;
        }

        /// <summary>
        /// Output log to file
        /// </summary>
        //public static void OutputLogMessage(string message, ErrorType errorType = ErrorType.ERROR)
        //{
        //    using (var writer = new StreamWriter(CONSTANTS.logPath, true))
        //    {
        //        writer.WriteLine(errorType.ToString() + ": " + message);
        //    }
        //}

        /// <summary>
        /// Get the text or first selected item from a control
        /// </summary>
        public static string GetControlText(MainForm form, string control)
        {
            try
            {
                var result = "";
                var c = form.Controls.Find(control, true).FirstOrDefault();
                if (c != null)
                {
                    if (c is TextBox)
                    {
                        result = ((TextBox)c).Text;
                    }
                    else if (c is ComboBox)
                    {
                        result = ((ComboBox)c).Text;
                    }
                    else if (c is CheckBox)
                    {
                        result = ((CheckBox)c).Checked.ToString();
                    }
                    else if (c is ListBox)
                    {
                        var item = ((ListBox)c).SelectedItem;
                        if (item != null)
                            result = item.ToString();
                    }
                    else if (c is ListView)
                    {
                        var items = ((ListView)c).SelectedItems;
                        if (items != null)
                            result = items[0].ToString();
                    }
                }

                return result.Trim();
            }
            catch { return ""; }
        }

        public static ComboBox GetComboBox(MainForm form, string control)
        {
            return ((ComboBox)form.Controls.Find(control, true).FirstOrDefault());
        }
        public static TextBox GetTextBox(MainForm form, string control)
        {
            return ((TextBox)form.Controls.Find(control, true).FirstOrDefault());
        }
        public static CheckBox GetCheckBox(MainForm form, string control)
        {
            return ((CheckBox)form.Controls.Find(control, true).FirstOrDefault());
        }
        public static ListBox GetListBox(MainForm form, string control)
        {
            return ((ListBox)form.Controls.Find(control, true).FirstOrDefault());
        }

        /// <summary>
        /// Returns the tuo.exe version 
        /// </summary>
        public static string GetTuoVersion()
        {
            string result = "";
            try
            {
                List<string> tuoOutput = new List<string>();

                // Create a process to run TUO
                var process = new Process();
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c tuo -version",
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
                        if (e.Data != null)
                            tuoOutput.Add(e.Data);
                    }
                );


                // Run the process until waitTime (ms)
                process.Start();
                process.BeginOutputReadLine();
                process.WaitForExit(1100);

                foreach (var output in tuoOutput)
                    result += output;

            }
            catch (Exception ex)
            {
                result += "Ran an error trying to get TUO Version: " + ex.Message;
            }

            return result;
        }

        public static string GetLatestFusionRecipe()
        {
            string result = "";

            try
            {
                // Open the reader, looking for the first comment
                var reader = XmlReader.Create("data/fusion_recipes_cj2.xml");
                while (reader.Read())
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Comment:
                            result = reader.Value;
                            break;
                    }

                    if (result != "") break;
                }

                // Close the reader
                reader.Close();
            }
            catch(Exception ex)
            {
                Console.WriteLine("GetLatestFusionRecipe() threw an error: " + ex.Message);
            }

            return result;
        }


        public static async Task AsyncDelay(int milliseconds)
        {
            await Task.Delay(milliseconds);
        }

        /// <summary>
        /// Most events happen Friday - Monday. Returns true if its Friday-Monday
        /// * Time could be narrowed down, but this is sufficient
        /// 
        /// Note: War placement one-day brawl occurs Thursday
        /// </summary>
        public static bool IsEventWeekend()
        {
            DateTime now = DateTime.UtcNow;
            if (now.DayOfWeek == DayOfWeek.Friday || now.DayOfWeek == DayOfWeek.Saturday || 
                now.DayOfWeek == DayOfWeek.Sunday || now.DayOfWeek == DayOfWeek.Monday)
            {
                return true;
            }

            return false;
        }


        /// <summary>
        /// If modifying the salvage list on the fly, repull it
        /// </summary>
        public static void RefreshSalvageList(MainForm form)
        {
            List<string> settings = new List<string>();
            string setting;
            string targetSetting;

            List<string> salvageRewards = new List<string>();
            List<string> salvageQuads = new List<string>();
            List<string> salvageAggressive = new List<string>();

            try
            {
                settings = FileIO.SimpleRead(form, "config/appsettings.txt", returnCommentedLines: true);

                targetSetting = "rewardsToSalvage:";
                setting = settings.FirstOrDefault(x => x.StartsWith(targetSetting));
                if (setting != null)
                {
                    salvageRewards = setting.Replace(targetSetting, "").Split(',').Select(x => x.Trim()).ToList();
                }

                List<string> targetSettings = new List<string>
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

                foreach(string ts in targetSettings)
                {
                    setting = settings.FirstOrDefault(x => x.StartsWith(ts));
                    if (setting != null)
                    {
                        salvageQuads.AddRange(setting.Replace(ts, "").Split(',').Select(x => x.Trim()).ToList());
                    }
                }

                CONSTANTS.SALVAGE_REWARDS = salvageRewards;
                CONSTANTS.SALVAGE_AGGRESSIVE = salvageQuads;
            }
            catch (Exception ex)
            {
                Helper.OutputWindowMessage(form, ex.Message, "ReadFromAppSettings() error: Could not open config/appsettings.txt.\r\n" + ex.Message);
            }
        }
    }
}
