using PirateTUO2.Classes;
using PirateTUO2.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PirateTUO2.Modules
{

    /// <summary>
    /// Hub for reading/writing file operations
    /// </summary>
    public static class FileIO
    {
        /// <summary>
        /// Reads a file line by line, and returns that data in a List<string>
        /// 
        /// Ignores commented lines and empty lines
        /// </summary>
        public static List<string> SimpleRead(MainForm form, string filePath, bool returnCommentedLines, bool skipWhitespace = true, bool displayError=true)
        {
            List<string> result = new List<string>();
            string line = "";
            int numberOfRetries = 3;

            for(int i=0; i<numberOfRetries; i++)
            {
                try
                {
                    using (var reader = new StreamReader(filePath))
                    {
                        while ((line = reader.ReadLine()) != null)
                        {
                            line = line.Trim();

                            // Skip whitespace if true
                            if (skipWhitespace && String.IsNullOrWhiteSpace(line)) continue;

                            // Return commented lines unless false
                            if (!returnCommentedLines && line.StartsWith("//")) continue;

                            result.Add(line);
                        }
                        break;
                    }
                }
                // File is being used by another process - try {numberOfRetry} times
                catch(IOException ex)
                {
                    if (displayError)
                    {
                        ControlExtensions.InvokeEx(form.outputTextBox, x => form.outputTextBox.AppendText("Could not read this file: " + filePath + ": " + ex.Message + "\r\n"));
                        Console.WriteLine(MethodInfo.GetCurrentMethod() + " - " + ex.Message);
                    }
                    Thread.Sleep(50);
                }
                catch (Exception ex)
                {
                    if (displayError)
                    {
                        ControlExtensions.InvokeEx(form.outputTextBox, x => form.outputTextBox.AppendText("SimpleRead(): Could not read this file: " + filePath + ": " + ex.Message + "\r\n"));
                        Console.WriteLine(MethodInfo.GetCurrentMethod() + " - " + ex.Message);
                        break;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Writes a file
        /// Returns an empty string on success, or an error string if it failed
        /// </summary>
        public static string SimpleWrite(MainForm form, string folderPath, string filename, string data, bool append = false)
        {
            string status = "";

            try
            {
                // Create the folder if it doesn't exist
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                if (!folderPath.EndsWith("/")) folderPath += "/";

                using (var writer = new StreamWriter(folderPath + filename, append))
                {
                    writer.Write(data);
                }

                Thread.Sleep(1);
            }
            catch (Exception ex)
            {
                string error = "SimpleWrite(): Could not read this file: " + folderPath + "/" + filename + ": " + ex.Message;
                Console.WriteLine(MethodInfo.GetCurrentMethod() + " - " + ex.Message);
                Console.WriteLine(error);
            }

            return status;
        }

        /// <summary>
        /// Opens a text file in notepad or notepad++
        /// </summary>
        public static void OpenFile(string name, MainForm form)
        {
            try
            {
                if (!File.Exists(name))
                {
                    using (StreamWriter w = File.AppendText(name)) { };
                }

                if (File.Exists(@"C:\Program Files (x86)\Notepad++\notepad++.exe"))
                {
                    Process.Start(@"notepad++.exe", name);
                }
                else
                {
                    Process.Start("notepad.exe", name);
                }
                //Helper.Popup(form, "We did it!");
            }
            catch (Exception ex)
            {
                Helper.Popup(form, "OpenFile() threw an exception: " + ex);
            }
        }


        /// <summary>
        /// Read from cardabbrs, mapping cardabbr:fullname and populate CONSTANTS.CARDABBRS
        /// </summary>
        public static void AddCardAbbreviations()
        {
            string line = "";

            try
            {
                // Looks for each Line in appsettings that starts with "namedSetting"
                // For each line, split each of its values by comma, trim and lowercase them, then add them to a dictionary

                using (var reader = new StreamReader("data/cardabbrs.txt"))
                {
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Contains("//")) continue;
                        var splitLine = line.Split(':');

                        if (splitLine.Length == 2)
                        {
                            if (!CONSTANTS.CARDABBRS.ContainsKey(splitLine[1].Trim()))
                                CONSTANTS.CARDABBRS.Add(splitLine[1].Trim(), splitLine[0].Trim());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("AddCardAbbreviations(): Error " + ex);
            }
        }

        /// <summary>
        /// Reads a file for a target line, "OptionX: A, B, C" and returns that line separated by commas
        /// </summary>
        public static Dictionary<int, List<string>> ReadFromAppSettings(string namedSetting)
        {
            Dictionary<int, List<string>> appLines = new Dictionary<int, List<string>>();
            string line = "";
            int x = 0;

            try
            {
                // Looks for each Line in appsettings that starts with "namedSetting"
                // For each line, split each of its values by comma, trim and lowercase them, then add them to a dictionary

                using (var reader = new StreamReader("config/appsettings.txt"))
                {
                    while ((line = reader.ReadLine()) != null)
                    {
                        line = line.Trim();

                        // Found a line
                        if (line.StartsWith(namedSetting))
                        {
                            var appLine = new List<string>();

                            // Remove namedSetting: from "namedSetting: A, B, C"
                            var settings = line.Replace(namedSetting + ":", "");

                            // Split remaining elements [A, B, C]
                            appLine = settings.Split(new char[] { ',' }).ToList();
                            
                            // ToLower and trim <a, b, c>
                            for(var i = 0; i< appLine.Count; i++)
                            {
                                // toLower() and trim() the string
                                if (!appLine[i].StartsWith("name="))
                                {
                                    appLine[i] = appLine[i].Trim().ToLower();
                                }
                                // Special case: Don't lowercase
                                else
                                {
                                    appLine[i] = appLine[i].Trim();
                                }
                            }

                            // Add to dictionary
                            appLines.Add(x, appLine);
                            x++;
                        }
                    }
                }
                
            }
            catch (Exception ex)
            {
                Console.WriteLine(MethodInfo.GetCurrentMethod() + " - " + ex.Message);
            }

            // Add empty string
            if (appLines.Count == 0) {
                appLines.Add(0, new List<string>());
            }

            return appLines;
        }

        /// <summary>
        /// Delete all files from a target directory
        /// </summary>
        public static string DeleteFromDirectory(string folderPath)
        {
            string status = "";

            try
            {
                var time = new Stopwatch();
                time.Start();

                List<string> filesInDirectory = Directory.GetFiles(folderPath).ToList();
                filesInDirectory.Remove("desktop.ini");
                filesInDirectory.Remove("customdecks.txt");

                Parallel.For(0, filesInDirectory.Count, (i) =>
                {
                    TryToDelete(filesInDirectory[i]);
                });

                //foreach (string file in filesInDirectory)
                //{
                //    // Don't delete these files
                //    if (file.Contains("desktop.ini")) continue;
                //    if (file == "customdecks.txt") continue;
                //    TryToDelete(file);
                //}

                time.Stop();
                Console.WriteLine(MethodInfo.GetCurrentMethod() + " " + time.ElapsedMilliseconds + "ms ");                
            }
            catch(Exception ex)
            {
                Console.WriteLine(MethodInfo.GetCurrentMethod() + " - " + ex.Message);
                status = "Error on DeleteFromDirectory(" + folderPath + "): " + ex.Message;
            }

            return status;
        }


        /// <summary>
        /// Try to delete a file
        /// </summary>
        public static bool TryToDelete(string f)
        {
            try
            {
                File.Delete(f);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }

        /// <summary>
        /// Looks in a directory for a file whose name contains the string
        /// </summary>
        public static string[] FilesContainingString(string folderPath, string fileName)
        {
            string[] fileNames = Directory.GetFiles(folderPath, "*" + fileName + "*.*");
            return fileNames;
        }

    }
}
