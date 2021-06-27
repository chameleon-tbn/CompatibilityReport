using System;
using System.Collections.Generic;
using System.Globalization;
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

            // If the current active catalog is version 1 or 2, we're (re)building the catalog from scratch; wait with manual updates until version 3
            if (ActiveCatalog.Instance.Version < 3)
            {
                Logger.UpdaterLog($"ManualUpdater skipped.", extraLine: true);

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
                // Update the catalog with the new info and save it to a new version
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
                    // Add filename to the combined CSV file
                    CSVCombined.AppendLine($"#####################################################################");
                    CSVCombined.AppendLine($"#### { Toolkit.PrivacyPath(CSVfile) }");
                    CSVCombined.AppendLine("");

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

                    // Add some space to the combined CSV file
                    CSVCombined.AppendLine("");
                }

                // Rename the processed CSV file
                string newFileName = CSVfile + (singleFileSuccess ? ".processed.txt" : ".processes_partially.txt");

                /* [Todo 0.3]
                if (!Toolkit.MoveFile(CSVfile, newFileName))
                {
                    Logger.UpdaterLog($"Could not rename \"{ Toolkit.PrivacyPath(CSVfile) }\". Rename or delete it manually to avoid processing it again.", Logger.error);
                }
                */

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
                    success = AddMod(id, authorID: secondID, 
                        modName: lineFragments.Length < 5 ? extraData : string.Join(", ", lineFragments, 3, lineFragments.Length - 3));
                    break;

                case "remove_mod":
                    success = RemoveMod(id);
                    break;

                case "add_archiveurl":
                case "add_sourceurl":
                case "add_gameversion":
                case "add_requireddlc":
                case "add_status":
                case "remove_requireddlc":
                case "remove_status":
                    success = ChangeModItem(id, action, extraData);
                    break;

                case "add_note":
                    success = ChangeModItem(id, action, string.Join(", ", lineFragments, 2, lineFragments.Length - 2));
                    break;

                case "remove_archiveurl":
                case "remove_sourceurl":
                case "remove_gameversion":
                case "remove_note":
                    success = ChangeModItem(id, action, "", 0);
                    break;

                case "add_requiredmod":
                case "add_neededfor":
                case "add_successor":
                case "add_alternative":
                case "remove_requiredmod":
                case "remove_neededfor":
                case "remove_successor":
                case "remove_alternative":
                    success = ChangeModItem(id, action, listMember: secondID);
                    break;

                case "add_authorid":
                    success = AddAuthorID(authorURL: idString, newAuthorID: secondID);
                    break;

                case "add_authorurl":
                case "add_lastseen":
                case "add_retired":
                case "remove_authorurl":
                case "remove_retired":
                    if (id == 0)
                    {
                        success = ChangeAuthorItem(authorURL: idString, action, extraData);
                    }
                    else
                    {
                        success = ChangeAuthorItem(authorID: id, action, extraData);
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


        // Add unlisted mod
        private static bool AddMod(ulong steamID, ulong authorID, string modName)
        {
            // Check if the Steam ID is valid and not already in the active catalog
            if (steamID == 0 || ActiveCatalog.Instance.ModDictionary.ContainsKey(steamID))
            {
                return false;
            }

            Mod newMod = new Mod(steamID, modName, authorID, "");

            newMod.Statuses.Add(Enums.ModStatus.UnlistedInWorkshop);

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

            Mod mod = ActiveCatalog.Instance.ModDictionary[steamID];

            // Check if it is unlisted or removed
            if (!mod.Statuses.Contains(Enums.ModStatus.UnlistedInWorkshop) && !mod.Statuses.Contains(Enums.ModStatus.RemovedFromWorkshop))
            {
                return false;
            }

            // [Todo 0.3] Add mod to 'to-be-removed' list
            return false;
        }


        // 
        private static bool ChangeModItem(ulong steamID, string action, string itemData)
        {
            // Check if the listMember is in the active catalog (as mod or group) or collected mod dictionary [Todo 0.3] Add collected group info
            if (string.IsNullOrEmpty(itemData))
            {
                return false;
            }

            return ChangeModItem(steamID, action, itemData, 0);
        }


        // 
        private static bool ChangeModItem(ulong steamID, string action, ulong listMember)
        {
            // Check if the listMember is in the active catalog (as mod or group) or collected mod dictionary [Todo 0.3] Add collected group info
            if (listMember == 0 || (!ActiveCatalog.Instance.ModDictionary.ContainsKey(listMember) && !ActiveCatalog.Instance.ModGroupDictionary.ContainsKey(listMember) &&
                !CatalogUpdater.CollectedModInfo.ContainsKey(listMember)))
            {
                return false;
            }

            return ChangeModItem(steamID, action, "", listMember);
        }


        // 
        private static bool ChangeModItem(ulong steamID, string action, string itemData, ulong listMember)
        {
            // Check if the Steam ID is in the active catalog or the collected mod dictionary
            if (steamID == 0 || (!ActiveCatalog.Instance.ModDictionary.ContainsKey(steamID) && !CatalogUpdater.CollectedModInfo.ContainsKey(steamID)))
            {
                return false;
            }
            
            // Get the mod
            Mod newMod;

            if (CatalogUpdater.CollectedModInfo.ContainsKey(steamID))
            {
                // Get existing collected mod
                newMod = CatalogUpdater.CollectedModInfo[steamID];
            }
            else
            {
                // Create a new copy of the catalog mod
                newMod = Mod.CopyMod(ActiveCatalog.Instance.ModDictionary[steamID]);
            }

            // Update the new mod
            if (action == "add_archiveurl" || action == "remove_archiveurl")
            {
                newMod.Update(archiveURL: itemData);
            }
            else if (action == "add_sourceurl" || action == "remove_sourceurl")
            {
                newMod.Update(sourceURL: itemData);

                // [Todo 0.3] Add or remove exclusion
            }
            else if (action == "add_gameversion")
            {
                // [Todo 0.3]
                return false;

                // [Todo 0.3] Add exclusion
            }
            else if (action == "remove_gameversion")
            {
                // [Todo 0.3]
                return false;

                // [Todo 0.3] Remove exclusion if it exists
            }
            else if (action == "add_requireddlc" || action == "remove_requireddlc")
            {
                Enums.DLC dlc;

                // Convert the DLC string to enum
                try
                {
                    dlc = (Enums.DLC)Enum.Parse(typeof(Enums.DLC), itemData, ignoreCase: true);
                }
                catch
                {
                    Logger.UpdaterLog($"DLC \"{ itemData }\" could not be converted.", Logger.debug);

                    return false;
                }

                if (action[0] == 'a')
                {
                    // Add the required DLC if the mod doesn't already have it
                    if (newMod.RequiredDLC.Contains(dlc))
                    {
                        return false;
                    }

                    newMod.RequiredDLC.Add(dlc);

                    // [Todo 0.3] Add exclusion
                }
                else
                {
                    // Remove the required DLC if the mod has it
                    if (!newMod.RequiredDLC.Contains(dlc))
                    {
                        return false;
                    }

                    newMod.RequiredDLC.Remove(dlc);

                    // [Todo 0.3] Remove exclusion if it exists
                }
            }
            else if (action == "add_status" || action == "remove_status")
            {
                Enums.ModStatus status;

                // Convert the status string to enum
                try
                {
                    status = (Enums.ModStatus)Enum.Parse(typeof(Enums.ModStatus), itemData, ignoreCase: true);
                }
                catch
                {
                    Logger.UpdaterLog($"Status \"{ itemData }\" could not be converted.", Logger.debug);

                    return false;
                }

                if (action[0] == 'a')
                {
                    // Add the status if the mod doesn't already have it
                    if (newMod.Statuses.Contains(status))
                    {
                        return false;
                    }
                    
                    newMod.Statuses.Add(status);

                    // [Todo 0.3] Add exclusion for some
                }
                else
                {
                    // Remove the status if the mod has it
                    if (!newMod.Statuses.Contains(status))
                    {
                        return false;
                    }

                    newMod.Statuses.Remove(status);

                    // [Todo 0.3] Remove exclusion for some, if it exists
                }
                // Check if the mod already has this status
                
            }
            else if (action == "add_note" || action == "remove_note")
            {
                newMod.Update(note: itemData);
            }
            else if (action == "add_requiredmod")
            {
                if (newMod.RequiredMods.Contains(listMember))
                {
                    return false;
                }

                newMod.RequiredMods.Add(listMember);

                // [Todo 0.3] Add exclusion
            }
            else if (action == "remove_requiredmod")
            {
                if (!newMod.RequiredMods.Contains(listMember))
                {
                    return false;
                }

                newMod.RequiredMods.Remove(listMember);

                // [Todo 0.3] Remove exclusion if it exists
            }
            else if (action == "add_neededfor")
            {
                if (newMod.NeededFor.Contains(listMember))
                {
                    return false;
                }

                newMod.NeededFor.Add(listMember);
            }
            else if (action == "remove_neededfor")
            {
                if (!newMod.NeededFor.Contains(listMember))
                {
                    return false;
                }

                newMod.NeededFor.Remove(listMember);
            }
            else if (action == "add_successor")
            {
                if (newMod.SucceededBy.Contains(listMember))
                {
                    return false;
                }

                newMod.SucceededBy.Add(listMember);
            }
            else if (action == "remove_successor")
            {
                if (!newMod.SucceededBy.Contains(listMember))
                {
                    return false;
                }

                newMod.SucceededBy.Remove(listMember);
            }
            else if (action == "add_alternative")
            {
                if (newMod.Alternatives.Contains(listMember))
                {
                    return false;
                }

                newMod.Alternatives.Add(listMember);
            }
            else if (action == "remove_alternative")
            {
                if (!newMod.Alternatives.Contains(listMember))
                {
                    return false;
                }

                newMod.Alternatives.Remove(listMember);
            }
            else
            {
                return false;
            }
            
            // Add the copied mod to the collected mods dictionary
            if (!CatalogUpdater.CollectedModInfo.ContainsKey(steamID))
            {
                CatalogUpdater.CollectedModInfo.Add(newMod.SteamID, newMod);
            }

            return true;
        }


        // Add a profile ID to an author
        private static bool AddAuthorID(string authorURL, ulong newAuthorID)
        {
            // Check if the author custom URL is in the active catalog and if the new author ID is not zero
            if (string.IsNullOrEmpty(authorURL) || newAuthorID == 0 || !ActiveCatalog.Instance.AuthorURLDictionary.ContainsKey(authorURL))
            {
                return false;
            }

            // [Todo 0.3] copy the author and add to collected authors
            Author catalogAuthor = ActiveCatalog.Instance.AuthorURLDictionary[authorURL];

            // Check if the author already has an ID
            if (catalogAuthor.ProfileID != 0)
            {
                return false;
            }

            // Update the author
            catalogAuthor.Update(newAuthorID);

            return true;
        }


        // Add an author item, by author profile ID
        private static bool ChangeAuthorItem(ulong authorID, string action, string itemData)
        {
            // Check if the author ID is in the active catalog
            if (authorID == 0 || !ActiveCatalog.Instance.AuthorIDDictionary.ContainsKey(authorID))
            {
                return false;
            }

            // Get the author and add the item
            return ChangeAuthorItem(ActiveCatalog.Instance.AuthorIDDictionary[authorID], action, itemData);
        }


        // Add an author item, by author custom URL
        private static bool ChangeAuthorItem(string authorURL, string action, string itemData)
        {
            // Check if the author custom URL is in the active catalog
            if (string.IsNullOrEmpty(authorURL) || !ActiveCatalog.Instance.AuthorURLDictionary.ContainsKey(authorURL))
            {
                return false;
            }

            // Get the author and add the item
            return ChangeAuthorItem(ActiveCatalog.Instance.AuthorURLDictionary[authorURL], action, itemData);
        }


        // Add an author item, by author type
        private static bool ChangeAuthorItem(Author author, string action, string itemData)
        {
            if (author == null)
            {
                return false;
            }

            // [Todo 0.3] copy the author and add to collected authors

            if (action == "add_authorurl")
            {
                if (string.IsNullOrEmpty(itemData))
                {
                    return false;
                }

                author.Update(customURL: itemData);
            }
            else if (action == "remove_authorurl")
            {
                if (string.IsNullOrEmpty(author.CustomURL))
                {
                    return false;
                }

                author.Update(customURL: "");
            }
            else if (action == "add_lastseen")
            {
                DateTime lastSeen;

                // Check if we have a valid date that is more recent than the current author last seen date
                try
                {
                    lastSeen = DateTime.ParseExact(itemData, "yyyy-MM-dd", new CultureInfo("en-GB"));

                    if (lastSeen < author.LastSeen)
                    {
                        return false;
                    }
                }
                catch
                {
                    return false;
                }

                author.Update(lastSeen: lastSeen);
            }
            else if (action == "add_retired")
            {
                if (author.Retired)
                {
                    return false;
                }

                author.Update(retired: true);
            }
            else if (action == "remove_retired")
            {
                if (!author.Retired)
                {
                    return false;
                }

                author.Update(retired: false);
            }
            else
            {
                return false;
            }            

            return true;
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
