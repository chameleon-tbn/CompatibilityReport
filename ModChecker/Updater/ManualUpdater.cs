using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ModChecker.DataTypes;
using ModChecker.Util;


// Manual Updater updates the catalog with information from CSV files in de updater folder. See separate guide for details.


namespace ModChecker.Updater
{
    internal static class ManualUpdater
    {
        // Stringbuilder to gather the combined CSVs, to be saved with the new catalog
        private static StringBuilder CSVCombined;


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

            CSVCombined = new StringBuilder();

            // Read all the CSVs
            ReadCSVs();

            if (CSVCombined.Length == 0)
            {
                Logger.UpdaterLog("No CSV files found. No new catalog created.");
            }
            else
            {
                // Update the catalog with the new info and save it to a new version [Todo 0.3] Prevent 'author no longer has mods'
                string partialPath = CatalogUpdater.Start(autoUpdater: false);

                // Save the combined CSVs to one file, next to the catalog
                if (!string.IsNullOrEmpty(partialPath))
                {
                    Toolkit.SaveToFile(CSVCombined.ToString(), partialPath + "_ManualUpdates.txt");
                }
            }

            // Clean up
            CSVCombined = null;

            Logger.UpdaterLog("Manual Updater has shutdown.", extraLine: true, duplicateToRegularLog: true);
        }


        // Read all CSV files
        private static void ReadCSVs()
        {
            // Get all CSV filenames
            List<string> CSVfiles = Directory.GetFiles(ModSettings.updaterPath, $"*.csv").ToList();

            // Exit if we have no CSVs to read
            if (!CSVfiles.Any())
            {
                return;
            }

            // Sort the list
            CSVfiles.Sort();

            bool overallSuccess = true;
            uint numberOfFiles = 0;

            string line;

            // Process all CSV files
            foreach (string CSVfile in CSVfiles)
            {
                Logger.UpdaterLog($"Processing \"{ CSVfile }\".");

                numberOfFiles++;

                bool singleFileSuccess = true;
                
                // Read a single CSV file
                using (StreamReader reader = File.OpenText(CSVfile))
                {
                    // Read each line and process the command
                    while ((line = reader.ReadLine()) != null)
                    {
                        // Process a line
                        if (ProcessLine(line))
                        {
                            // Add this line to the complete list
                            CSVCombined.AppendLine(line);
                        }
                        else
                        {
                            // Add the failed line with a comment to the complete list
                            CSVCombined.AppendLine("# [NOT PROCESSED] " + line);

                            singleFileSuccess = false;

                            overallSuccess = false;
                        }
                    }
                }

                // Rename the processed CSV file
                string newFileName = CSVfile + (singleFileSuccess ? "_processed.txt" : "_processes_partially.txt");

                if (!Toolkit.MoveFile(CSVfile, newFileName))
                {
                    Logger.UpdaterLog($"Could not rename \"{ CSVfile }\". Rename or delete it manually to avoid processing it again.", Logger.error);
                }

                // Log if we couldn't process all lines
                if (!singleFileSuccess)
                {
                    Logger.UpdaterLog($"\"{ newFileName }\" not processed (completely).", Logger.warning);
                }
            }

            // Log number of processed files to updater log
            Logger.UpdaterLog($"{ numberOfFiles } CSV file{ (numberOfFiles == 1 ? "" : "s") } processed{ (overallSuccess ? "" : ", with some errors" ) }.");
            
            // Log to regular log
            if (overallSuccess)
            {
                Logger.Log($"Manual Updater processed { numberOfFiles } CSV file{ (numberOfFiles == 1 ? "" : "s") }. Check logfile for details.");
            }
            else
            {
                Logger.Log($"Manual Updater processed { numberOfFiles } CSV file{ (numberOfFiles == 1 ? "" : "s") }, with some errors. Check logfile for details.", 
                    Logger.warning);
            }
        }


        // Process a line from a CSV file
        private static bool ProcessLine(string line)
        {
            // Skip empty lines
            if (string.IsNullOrEmpty(line))
            {
                return true;
            }

            // Skip comments starting with a '#'
            if (line[0] == '#')
            {
                return true;
            }

            // Split the line
            string[] lineFragments = line.Split(',');

            // Get the action
            string action = lineFragments[0].Trim().ToLower();

            // Get the id as number (Steam ID or group ID) and as string (author custom url, exclusion type, game version string, catalog note)
            string idString = lineFragments.Length < 2 ? "" : lineFragments[1].Trim();
            ulong id;

            try
            {
                id = Convert.ToUInt64(idString);
            }
            catch
            {
                id = 0;
            }

            // Get the rest of the data, if present
            ulong secondID = 0;
            string extraData = "";
            
            if (lineFragments.Length > 2)
            {
                extraData = lineFragments[2].Trim();

                try
                {
                    secondID = Convert.ToUInt64(extraData);
                }
                catch
                {
                    secondID = 0;
                }

                // Set extraData to the 3rd element if that isn't numeric, otherwise to the 4th element if available, otherwise to an empty string
                extraData = secondID == 0 ? extraData : (lineFragments.Length > 3 ? lineFragments[3].Trim() : "");
            }

            bool success;

            // Act on the action found
            switch (action)
            {
                case "add_mod":
                    success = AddMod(id, extraData.ToLower() != "removed");
                    break;

                case "remove_mod":
                    success = RemoveMod(id);
                    break;

                case "add_archiveurl":
                case "add_sourceurl":
                case "add_gameversion":
                case "add_requireddlc":
                case "add_status":
                    success = AddModItem(id, action, extraData);
                    break;

                case "add_note":
                    success = AddModItem(id, action, string.Join(", ", lineFragments, 2, lineFragments.Length - 2));
                    break;

                case "add_requiredmod":
                case "add_neededfor":
                case "add_successor":
                case "add_alternative":
                    success = AddModItem(id, action, listMember: secondID);
                    break;

                case "remove_archiveurl":
                case "remove_sourceurl":
                case "remove_gameversion":
                case "remove_note":
                    success = RemoveModItem(id, action);
                    break;

                case "remove_requireddlc":
                case "remove_status":
                    success = RemoveModItem(id, action, extraData);
                    break;

                case "remove_requiredmod":
                case "remove_neededfor":
                case "remove_successor":
                case "remove_alternative":
                    success = RemoveModItem(id, action, listMember: secondID);
                    break;

                case "add_authorid":
                    success = AddAuthorItem(authorURL: idString, newAuthorID: secondID);
                    break;

                case "add_authorurl":
                case "add_lastseen":
                case "add_retired":
                    if (id == 0)
                    {
                        success = AddAuthorItem(authorURL: idString, action, extraData);
                    }
                    else
                    {
                        success = AddAuthorItem(authorID: id, action, extraData);
                    }                    
                    break;

                case "remove_authorurl":
                case "remove_lastseen":
                case "remove_retired":
                    if (id == 0)
                    {
                        success = RemoveAuthorItem(authorURL: idString, action);
                    }
                    else
                    {
                        success = RemoveAuthorItem(authorID: id, action);
                    }
                    break;

                case "add_group":
                case "add_modgroup":
                    if (lineFragments.Length < 4)
                    {
                        success = false;
                    }
                    else
                    {
                        List<string> groupMembers = lineFragments.ToList();

                        groupMembers.RemoveRange(0, 2);

                        success = AddGroup(name: idString, groupMembers);
                    }
                    break;

                case "remove_group":
                case "remove_modgroup":
                    success = RemoveGroup(id, replacementMod: secondID);
                    break;

                case "add_groupmember":
                    success = AddGroupMember(id, groupMember: secondID);
                    break;

                case "remove_groupmember":
                    success = RemoveGroupMember(id, groupMember: secondID);
                    break;

                case "add_compatibility":
                    success = AddCompatibility(steamID1: id, steamID2: secondID, extraData);
                    break;

                case "remove_compatibility":
                    success = RemoveCompatibility(steamID1: id, steamID2: secondID, extraData);
                    break;

                case "remove_exclusion":
                    success = RemoveExclusion(name: idString, category: extraData, subitem: secondID);
                    break;

                case "add_cataloggameversion":
                    success = SetCatalogGameVersion(gameVersionString: idString);
                    break;

                case "add_catalognote":
                    CatalogUpdater.SetNote(string.Join(", ", lineFragments, 2, lineFragments.Length - 2).Trim());
                    success = true;
                    break;

                case "remove_catalognote":
                    CatalogUpdater.SetNote("");
                    success = true;
                    break;

                default:
                    Logger.UpdaterLog($"Line not processed, invalid action. Line: { line }", Logger.warning);
                    success = false;
                    break;
            }

            return success;
        }


        // Add unlisted or removed mod [Todo 0.3] Add mod name and author for removed mods
        private static bool AddMod(ulong steamID, bool unlisted)
        {
            // Check if the Steam ID is valid and not already in the active catalog
            if (steamID == 0 || ActiveCatalog.Instance.ModDictionary.ContainsKey(steamID))
            {
                return false;
            }

            Mod newMod = new Mod(steamID, "", 0, "");

            newMod.Statuses.Add(unlisted ? Enums.ModStatus.UnlistedInWorkshop : Enums.ModStatus.RemovedFromWorkshop);

            CatalogUpdater.CollectedModInfo.Add(steamID, newMod);

            return true;
        }


        // Remove an unlisted or removed mod
        private static bool RemoveMod(ulong steamID)
        {
            // Check if the Steam ID is valid and does exist in the active catalog
            if (steamID == 0 || !ActiveCatalog.Instance.ModDictionary.ContainsKey(steamID))
            {
                return false;
            }

            // [Todo 0.3] Check if it is unlisted or removed; Add mod to 'to-be-removed' list

            return false;
        }


        // 
        private static bool AddModItem(ulong steamID, string action, string itemData)
        {
            // Check if the Steam ID is in the active catalog
            if (steamID == 0 || !ActiveCatalog.Instance.ModDictionary.ContainsKey(steamID))
            {
                return false;
            }

            // [Todo 0.3]

            return false;
        }


        // 
        private static bool AddModItem(ulong steamID, string action, ulong listMember)
        {
            // Check if the Steam ID and the listMember are in the active catalog
            if (steamID == 0 || listMember == 0 || !ActiveCatalog.Instance.ModDictionary.ContainsKey(steamID) || 
                (!ActiveCatalog.Instance.ModDictionary.ContainsKey(listMember) && !ActiveCatalog.Instance.ModGroupDictionary.ContainsKey(listMember)))
            {
                return false;
            }

            Mod newMod = Mod.CopyMod(ActiveCatalog.Instance.ModDictionary[steamID]);

            if (action == "add_requiredmod")
            {
                if (newMod.RequiredMods.Contains(listMember))
                {
                    return false;
                }

                newMod.RequiredMods.Add(listMember);
            }
            else if (action == "add_neededfor")
            {
                // [Todo 0.3]
            }
            else if (action == "add_successor")
            {
                // [Todo 0.3]
            }
            else if (action == "add_alternative")
            {
                // [Todo 0.3]
            }
            else
            {
                return false;
            }

            return true;
        }


        // 
        private static bool RemoveModItem(ulong steamID, string action, string subItem = "")
        {
            // [Todo 0.3]

            return false;
        }


        // 
        private static bool RemoveModItem(ulong steamID, string action, ulong listMember)
        {
            // [Todo 0.3]

            return false;
        }


        // 
        private static bool AddAuthorItem(ulong authorID, string action, string itemData)
        {
            // [Todo 0.3]

            return false;
        }


        // 
        private static bool AddAuthorItem(string authorURL, string action, string itemData)
        {
            // [Todo 0.3]

            return false;
        }


        // 
        private static bool AddAuthorItem(string authorURL, ulong newAuthorID)
        {
            // [Todo 0.3]

            return false;
        }


        // 
        private static bool RemoveAuthorItem(string authorURL, string action)
        {
            // [Todo 0.3]

            return false;
        }


        // 
        private static bool RemoveAuthorItem(ulong authorID, string action)
        {
            // [Todo 0.3]

            return false;
        }


        // 
        private static bool AddGroup(string name, List<string> groupMembers)
        {
            // [Todo 0.3]

            return false;
        }


        // 
        private static bool RemoveGroup(ulong groupID, ulong replacementMod)
        {
            // [Todo 0.3]

            return false;
        }


        // 
        private static bool AddGroupMember(ulong groupID, ulong groupMember)
        {
            // [Todo 0.3]

            return false;
        }


        // 
        private static bool RemoveGroupMember(ulong groupID, ulong groupMember)
        {
            // [Todo 0.3]

            return false;
        }


        // 
        private static bool AddCompatibility(ulong steamID1, ulong steamID2, string compatibility)
        {
            // [Todo 0.3]

            return false;
        }


        // 
        private static bool RemoveCompatibility(ulong steamID1, ulong steamID2, string compatibility)
        {
            // [Todo 0.3]

            return false;
        }


        // 
        private static bool RemoveExclusion(string name, string category, ulong subitem)
        {
            // [Todo 0.3]

            return false;
        }


        // 
        private static bool SetCatalogGameVersion(string gameVersionString)
        {
            // [Todo 0.3]

            return false;
        }
    }
}
