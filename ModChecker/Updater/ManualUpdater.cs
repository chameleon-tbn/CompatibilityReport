using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ModChecker.DataTypes;
using ModChecker.Util;


// Manual Updater updates the catalog with information from CSV files in de updater folder. The following can be updated/added:
// * Mod:           nothing yet
// * Author:        nothing yet
// * Mod Group:     nothing yet
// * Compatibility: nothing yet
// * Catalog info:  nothing yet 
//
// CSV support is limited (comma separator only, no escaping of comma with quotes) and is in the following format:
//    Category, Command, Exclusion (true/false), ID, Other command specific data
//    example: SourceURL, Change, true, 2414618415, https://github.com/kianzarrin/AdvancedRoads
//
// Available categories are listed in the switch statement in ProcessCSV() below.
// Command can be Add, Remove or Change. Not all categories support all commands.
// Lines that start with a '#' will be ignored.


namespace ModChecker.Updater
{
    internal static class ManualUpdater
    {
        // Stringbuilder to gather the combined CSVs, to be saved with the new catalog
        internal static StringBuilder CSVComplete;


        // Start the manual updater; will load and process CSV files, update the active catalog and save it with a new version; including change notes
        internal static void Start()
        {
            // Exit if the updater is not enabled in settings
            if (!ModSettings.UpdaterEnabled)
            {
                return;
            }
            
            Logger.Log("Manual Updater started. See separate logfile for details.");
            Logger.UpdaterLog("Manual Updater started.");

            // Initialize the dictionaries we need
            CatalogUpdater.Init();

            // Read all the CSVs
            bool? success = ReadCSVs();

            if (success != null)
            {
                // Update the catalog with the new info and save it to a new version
                string partialPath = CatalogUpdater.Start("the Manual Updater process");

                // Save the combined CSVs to one file, next to the catalog
                if (!string.IsNullOrEmpty(partialPath) && CSVComplete.Length != 0)
                {
                    Toolkit.SaveToFile(CSVComplete.ToString(), partialPath + "_ManualUpdates.csv");
                }

                if (success == false)
                {
                    Logger.Log($"Manual Updater processed some but not all CSV files. Check logfile for details.", Logger.warning);
                }
            }

            // Clean up
            CSVComplete = null;

            Logger.UpdaterLog("Manual Updater has shutdown.", extraLine: true, duplicateToRegularLog: true);
        }


        // Read all CSV files; returns null if no files where found, false if not all files could be processed, or true if all is well
        private static bool? ReadCSVs()
        {
            // Get all CSV filenames
            List<string> CSVfiles = Directory.GetFiles(ModSettings.updaterPath, $"{ ModSettings.internalName }*.csv").ToList();

            // Remove all Manual Updates CSVs and sort the filenames
            CSVfiles.RemoveAll(x => x.Contains("_ManualUpdates.csv"));

            CSVfiles.Sort();

            if (!CSVfiles.Any())
            {
                return null;
            }

            bool overallSuccess = true;

            string line;

            // Process all CSV files
            foreach (string CSVfile in CSVfiles)
            {
                Logger.UpdaterLog($"Processing \"{ CSVfile }\".");

                // Read a single CSV file
                using (StreamReader reader = File.OpenText(CSVfile))
                {
                    bool singleFileSuccess = true;

                    // Read each line and process the command
                    while ((line = reader.ReadLine()) != null)
                    {
                        // Process a line
                        if (ProcessCommand(line))
                        {
                            // Add this line to the complete list
                            CSVComplete.AppendLine(line);   // [Todo 0.3] append or appendline?
                        }
                        else
                        {
                            // Add the failed line with a comment to the complete list
                            CSVComplete.AppendLine("# [NOT PROCESSED]: " + line);   // [Todo 0.3] append or appendline?

                            singleFileSuccess = false;

                            overallSuccess = false;
                        }
                    }

                    // Rename the processed CSV file
                    string newFileName = CSVfile + (singleFileSuccess ? "_processed.txt" : "_processes_partially.txt");

                    if (!Toolkit.MoveFile(CSVfile, newFileName))
                    {
                        Logger.UpdaterLog($"Could not rename \"{ CSVfile }\". Rename or delete it manually to avoid processing it again.", Logger.error);
                    }

                    if (!singleFileSuccess)
                    {
                        Logger.UpdaterLog($"\"{ newFileName }\" not processed (completely).", Logger.warning);
                    }
                }
            }

            return overallSuccess;
        }


        private static bool ProcessCommand(string line)
        {
            // Split the line
            string[] lineFragments = line.Split(',');

            string category = lineFragments[0].Trim().ToLower();

            switch (category)
            {
                case "mod": 
                    // ...
                    break;

                case "group":
                case "modgroup":
                    // ...
                    break;

                case "compatibility":
                case "modcompatibility":
                    // ...
                    break;

                case "archiveurl":
                case "sourceurl": 
                    // ...
                    break;

                default: 
                    // ...
                    break;
            }
            /* 
            Mod,
            ArchiveURL,
            SourceURL,
            CompatibleGameVersion,
            RequiredMod,
            RequiredDLC,
            NeededFor,
            Succeessor,
            Alternative,
            Status,
            Note,
            Group,
            GroupMember,
            Compatibility,
            AuthorID,
            AuthorURL,
            LastSeen,
            Retired,
            Exclusion,
            CatalogNote,
            CatalogGameVersion,
            IntroText,
            FooterText
            */

            return false;
        }
    }
}
