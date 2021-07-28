using System;
using System.Collections.Generic;
using System.Diagnostics;
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

            // If the current active catalog is version 1 or 2, we're (re)building the catalog from scratch; wait with manual updates until version 3 is done
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

            // Read all the CSVs and import them into the updater collections
            ImportCSVs();

            if (CSVCombined.Length == 0)
            {
                Logger.UpdaterLog("No CSV files found. No new catalog created.");
            }
            else
            {
                // Update the catalog with the new info and save it to a new version
                string partialPath = CatalogUpdater.Start(autoUpdater: false);

                // Save the combined CSVs, next to the catalog
                if (!string.IsNullOrEmpty(partialPath))
                {
                    Toolkit.SaveToFile(CSVCombined.ToString(), partialPath + "_ManualUpdates.txt");
                }
            }

            // Clean up
            CSVCombined = null;

            Logger.UpdaterLog("Manual Updater has shutdown.", extraLine: true, duplicateToRegularLog: true);
        }


        // Read all the CSVs and import them into the updater collections
        private static void ImportCSVs()
        {
            // Get all CSV filenames
            List<string> CSVfiles = Directory.GetFiles(ModSettings.updaterPath, "*.csv").ToList();

            // Exit if we have no CSVs to read
            if (!CSVfiles.Any())
            {
                return;
            }

            // Time the update process
            Stopwatch timer = Stopwatch.StartNew();

            // Sort the list
            CSVfiles.Sort();

            bool overallSuccess = true;
            uint numberOfFiles = 0;

            // Process all CSV files
            foreach (string CSVfile in CSVfiles)
            {
                Logger.UpdaterLog($"Processing \"{ Toolkit.GetFileName(CSVfile) }\".");

                // Add filename to the combined CSV file
                CSVCombined.AppendLine($"###################################################");
                CSVCombined.AppendLine($"#### FILE: { Toolkit.GetFileName(CSVfile) }");
                CSVCombined.AppendLine($"###################################################");
                CSVCombined.AppendLine("");

                numberOfFiles++;

                bool singleFileSuccess = true;

                // Read a single CSV file
                using (StreamReader reader = File.OpenText(CSVfile))
                {
                    string line;

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

                // Add some space to the combined CSV file
                CSVCombined.AppendLine("");
                CSVCombined.AppendLine("");

                // Rename the processed CSV file
                string newFileName = CSVfile + (singleFileSuccess ? ".processed.txt" : ".processed_partially.txt");

                /* [Todo 0.3] Re-enable this
                if (!Toolkit.MoveFile(CSVfile, newFileName))
                {
                    Logger.UpdaterLog($"Could not rename \"{ Toolkit.GetFileName(CSVfile) }\". Rename or delete it manually to avoid processing it again.", Logger.error);
                }
                */
            }

            timer.Stop();

            string s = numberOfFiles == 1 ? "" : "s";

            // Log number of processed files and elapsed time to updater log, also to regular log
            Logger.UpdaterLog($"{ numberOfFiles } CSV file{ s } processed in { Toolkit.ElapsedTime(timer.ElapsedMilliseconds) }" + 
                (overallSuccess ? "." : ", with some errors."));
            
            Logger.Log($"Manual Updater processed { numberOfFiles } CSV file{ s } in { Toolkit.ElapsedTime(timer.ElapsedMilliseconds) }" +
                $"{ (overallSuccess ? "" : ", with some errors. Check separate logfile for details") }.", overallSuccess ? Logger.info : Logger.warning);
        }


        // Process a line from a CSV file
        private static bool ProcessLine(string line)
        {
            // Skip empty lines, without returning an error
            if (string.IsNullOrEmpty(line))
            {
                return true;
            }

            // Skip comments starting with a '#', without returning an error
            if (line[0] == '#')
            {
                return true;
            }

            // Split the line
            string[] lineFragments = line.Split(',');

            // Get the action
            string action = lineFragments[0].Trim().ToLower();

            // Get the id as number (Steam ID or group ID) and as string (author custom url, exclusion category, game version string, catalog note)
            string idString = lineFragments.Length < 2 ? "" : lineFragments[1].Trim();
            ulong id = Toolkit.ConvertToUlong(idString);

            // Get the rest of the data, if present
            string extraData = "";
            ulong secondID = 0;

            if (lineFragments.Length > 2)
            {
                extraData = lineFragments[2].Trim();

                secondID = Toolkit.ConvertToUlong(extraData);
                
                // Set extraData to the 3rd element if that isn't numeric (secondID = 0), otherwise to the 4th element if available, otherwise to an empty string
                extraData = secondID == 0 ? extraData : (lineFragments.Length > 3 ? lineFragments[3].Trim() : "");
            }

            string result;

            // Act on the action found with the additional data  [Todo 0.3] Check if all actions are updated in CatalogUpdater
            switch (action)
            {
                case "add_mod":
                    // Join the lineFragments to allow for commas in the mod name
                    string modName = secondID == 0 ?
                        (lineFragments.Length < 4 ? "" : string.Join(", ", lineFragments, 3, lineFragments.Length - 3)).Trim() :
                        (lineFragments.Length < 4 ? extraData : string.Join(", ", lineFragments, 3, lineFragments.Length - 3)).Trim();

                    // Get the author URL if no author ID was found
                    string authorURL = secondID == 0 ? extraData : "";

                    result = AddMod(id, authorID: secondID, authorURL, modName);
                    break;

                case "remove_mod":
                    result = RemoveMod(id);
                    break;

                case "add_archiveurl":
                case "add_sourceurl":
                case "add_gameversion":
                case "add_requireddlc":
                case "add_status":
                case "remove_requireddlc":
                case "remove_status":
                    result = ChangeModItem(id, action, extraData);
                    break;

                case "add_note":
                    // Join the lineFragments to allow for commas in the note
                    result = ChangeModItem(id, action, string.Join(", ", lineFragments, 2, lineFragments.Length - 2)).Trim();
                    break;

                case "add_reviewdate":
                case "remove_archiveurl":
                case "remove_sourceurl":
                case "remove_gameversion":
                case "remove_note":
                    result = ChangeModItem(id, action, "");
                    break;

                case "add_requiredmod":
                case "remove_requiredmod":
                    result = ChangeRequiredMod(id, action, listMember: secondID);
                    break;

                case "add_successor":
                case "add_alternative":
                case "remove_successor":
                case "remove_alternative":
                    result = ChangeModItem(id, action, listMember: secondID);
                    break;

                case "add_compatibility":
                case "remove_compatibility":
                    if (lineFragments.Length < 4)
                    {
                        result = "Not enough parameters.";
                    }
                    else
                    {
                        // Get the note, if available
                        string note = lineFragments.Length > 4 ? string.Join(", ", lineFragments, 4, lineFragments.Length - 4).Trim() : "";

                        // If the note starts with a '#', assume a comment instead of an actual note
                        if (!string.IsNullOrEmpty(note) && note[0] == '#')
                        {
                            note = "";
                        }

                        result = AddRemoveCompatibility(action, id, secondID, compatibilityString: extraData, note);
                    }
                    break;

                case "add_compatibilitiesforone":
                case "add_compatibilitiesforall":
                    if (lineFragments.Length < 5)
                    {
                        result = "Not enough parameters.";
                    }
                    else
                    {
                        // Get all linefragments to get all steam IDs as strings
                        List<string> steamIDs = lineFragments.ToList();

                        // Remove the first two or three elements: action, [first mod id,] compatibility
                        steamIDs.RemoveRange(0, action == "add_compatibilitiesforone" ? 3 : 2);

                        // Remove the list element if it starts with a '#', assuming a comment
                        if (steamIDs.Last().Trim()[0] == '#' && steamIDs.Count > 2) 
                        {
                            steamIDs.RemoveAt(steamIDs.Count - 1);
                        }

                        if (action == "add_compatibilitiesforone")
                        {
                            result = AddCompatibilitiesForOne(id, compatibilityString: extraData, steamIDs);
                        }
                        else
                        {
                            result = AddCompatibilitiesForAll(compatibilityString: idString, steamIDs);
                        }                        
                    }
                    break;

                case "add_group":
                    if (lineFragments.Length < 4)
                    {
                        result = "Not enough parameters.";
                    }
                    else
                    {
                        // Get all linefragments to get all steam IDs as strings; remove the first two elements: action and group name
                        List<string> groupMembers = lineFragments.ToList();

                        groupMembers.RemoveRange(0, 2);

                        result = AddGroup(groupName: idString, groupMembers);
                    }
                    break;

                case "remove_group":
                    // Remove the group after changing it to a replacement mod as 'required mod' for all affected mods
                    result = RemoveGroup(groupID: id, replacementMod: secondID);
                    break;

                case "add_groupmember":
                    result = AddGroupMember(groupID: id, groupMember: secondID);
                    break;

                case "remove_groupmember":
                    result = RemoveGroupMember(groupID: id, groupMember: secondID);
                    break;

                case "add_authorid":
                case "add_authorurl":
                case "add_lastseen":
                case "add_retired":
                case "remove_authorurl":
                case "remove_retired":
                    if (id == 0)
                    {
                        result = ChangeAuthorItem(authorURL: idString, action, extraData, newAuthorID: secondID);
                    }
                    else
                    {
                        result = ChangeAuthorItem(authorID: id, action, extraData);
                    }                    
                    break;

                case "remove_exclusion":
                    result = RemoveExclusion(steamID: id, subitem: secondID, categoryString: extraData);
                    break;

                case "add_cataloggameversion":
                    result = SetCatalogGameVersion(gameVersionString: idString);
                    break;

                case "add_catalognote":
                    // Join the lineFragments to allow for commas in the note
                    CatalogUpdater.SetNote(string.Join(", ", lineFragments, 1, lineFragments.Length - 1).Trim());
                    result = "";
                    break;

                case "remove_catalognote":
                    CatalogUpdater.SetNote("");
                    result = "";
                    break;

                case "updatedate":
                    result = CatalogUpdater.SetUpdateDate(idString) ? "" : "Invalid date.";
                    break;

                default:
                    result = "Line not processed, invalid action.";
                    break;
            }

            if (!string.IsNullOrEmpty(result))
            {
                Logger.UpdaterLog(result + " Line: " + line, Logger.error);
            }
            
            return string.IsNullOrEmpty(result);
        }


        // Add unlisted mod
        private static string AddMod(ulong steamID, ulong authorID, string authorURL, string modName)
        {
            // Exit if Steam ID is not valid
            if (steamID <= ModSettings.highestFakeID)
            {
                return $"Invalid Steam ID { steamID }.";
            }

            // Exit if Steam ID already exists in the active catalog or in the collected mods
            if (ActiveCatalog.Instance.ModDictionary.ContainsKey(steamID) || CatalogUpdater.CollectedModInfo.ContainsKey(steamID))
            {
                return "Mod already exists.";
            }

            // Exit if both the author ID and URL are empty
            if (authorID == 0 && string.IsNullOrEmpty(authorURL))
            {
                return "Invalid author.";
            }

            // Create a new mod
            Mod newMod = new Mod(steamID, modName, authorID, authorURL);

            // Add unlisted status
            newMod.Statuses.Add(Enums.ModStatus.UnlistedInWorkshop);

            // Add mod to collected mods
            CatalogUpdater.CollectedModInfo.Add(steamID, newMod);

            return "";
        }


        // Remove an unlisted or removed mod from the catalog
        private static string RemoveMod(ulong steamID)
        {
            // Exit if Steam ID is not valid, doesn't exist in the active catalog or does exist in the collected mods dictionary or removals list
            if (steamID <= ModSettings.highestFakeID || !ActiveCatalog.Instance.ModDictionary.ContainsKey(steamID) || 
                CatalogUpdater.CollectedModInfo.ContainsKey(steamID) || CatalogUpdater.CollectedRemovals.Contains(steamID))
            {
                return $"Invalid Steam ID { steamID }.";
            }

            // Exit if it is listed as required, successor or alternative mod anywhere, or if it is a group member
            Mod catalogMod = ActiveCatalog.Instance.Mods.FirstOrDefault(x => x.RequiredMods.Contains(steamID) || 
                                                                             x.Successors.Contains(steamID) || 
                                                                             x.Alternatives.Contains(steamID));
            Mod collectedMod = CatalogUpdater.CollectedModInfo.FirstOrDefault(x => x.Value.RequiredMods.Contains(steamID) || 
                                                                                   x.Value.Successors.Contains(steamID) || 
                                                                                   x.Value.Alternatives.Contains(steamID)).Value;
            ModGroup catalogGroup = ActiveCatalog.Instance.ModGroups.FirstOrDefault(x => x.SteamIDs.Contains(steamID));
            ModGroup collectedGroup = CatalogUpdater.CollectedModGroupInfo.FirstOrDefault(x => x.Value.SteamIDs.Contains(steamID)).Value;

            if (catalogMod != default || collectedMod != default || catalogGroup != default || collectedGroup != default)
            {
                return $"Mod { steamID } can't be removed because it is still referenced by mods or groups.";
            }

            // Get the mod from the active catalog
            Mod mod = ActiveCatalog.Instance.ModDictionary[steamID];

            // Exit if it does not have the unlisted or removed status
            if (!mod.Statuses.Contains(Enums.ModStatus.UnlistedInWorkshop) && !mod.Statuses.Contains(Enums.ModStatus.RemovedFromWorkshop))
            {
                return "Mod can't be removed because it isn't unlisted or removed.";
            }

            // Add mod to the removals list
            CatalogUpdater.CollectedRemovals.Add(steamID);

            return "";
        }


        // Change a mod item with a string value
        private static string ChangeModItem(ulong steamID, string action, string itemData)
        {
            // Exit if itemData is empty for actions that should have a non-empty string
            if (string.IsNullOrEmpty(itemData) && action != "add_reviewdate" && action != "remove_note")
            {
                return $"Invalid parameter \"{ itemData }\".";
            }

            // Do the actual change
            return ChangeModItem(steamID, action, itemData, 0);
        }


        // Change a mod list item
        private static string ChangeModItem(ulong steamID, string action, ulong listMember)
        {
            // Exit if the listMember is not a valid ID or is not in the active catalog and not in the collected mod dictionary
            if ((listMember >= ModSettings.lowestFakeID && listMember <= ModSettings.highestFakeID) || 
                (!ActiveCatalog.Instance.ModDictionary.ContainsKey(listMember) && !CatalogUpdater.CollectedModInfo.ContainsKey(listMember)))
            {
                return $"Invalid Steam ID { listMember }.";
            }

            // Do the actual change
            return ChangeModItem(steamID, action, "", listMember);
        }


        // Change a mod's required mod
        private static string ChangeRequiredMod(ulong steamID, string action, ulong listMember)
        {
            // Exit if the listMember is not a valid ID or is not in the active catalog as mod or group, and not in the collected mod or group dictionary
            if ((listMember >= ModSettings.lowestFakeID && listMember < ModSettings.lowestModGroupID) || 
                (listMember > ModSettings.highestModGroupID && listMember <= ModSettings.highestFakeID) || 
                (!ActiveCatalog.Instance.ModDictionary.ContainsKey(listMember) && !ActiveCatalog.Instance.ModGroupDictionary.ContainsKey(listMember) && 
                !CatalogUpdater.CollectedModInfo.ContainsKey(listMember) && !CatalogUpdater.CollectedModGroupInfo.ContainsKey(listMember)))
            {
                return $"Invalid Steam ID { listMember }.";
            }

            // Do the actual change
            return ChangeModItem(steamID, action, "", listMember);
        }


        // Change a mod item, with a string value or a list member
        private static string ChangeModItem(ulong steamID, string action, string itemData, ulong listMember)
        {
            // Exit if the Steam ID is invalid (allow for builtin mods) or not in the active catalog or the collected mod dictionary
            if ((steamID >= ModSettings.lowestFakeID && steamID <= ModSettings.highestFakeID) || 
                (!ActiveCatalog.Instance.ModDictionary.ContainsKey(steamID) && !CatalogUpdater.CollectedModInfo.ContainsKey(steamID)))
            {
                return $"Invalid Steam ID { steamID }.";
            }

            // Get a copy of the catalog mod from the collected mods dictionary or create a new copy
            Mod newMod= CatalogUpdater.CollectedModInfo.ContainsKey(steamID) ? CatalogUpdater.CollectedModInfo[steamID] : 
                Mod.Copy(ActiveCatalog.Instance.ModDictionary[steamID]);

            // Update the copied mod [Todo 0.3] Needs action in CatalogUpdater
            if (action == "add_archiveurl" || action == "remove_archiveurl")
            {
                newMod.Update(archiveURL: itemData);
            }
            else if (action == "add_sourceurl")
            {
                newMod.Update(sourceURL: itemData);

                ActiveCatalog.Instance.AddExclusion(steamID, Enums.ExclusionCategory.SourceURL);
            }
            else if (action == "remove_sourceurl")
            {
                newMod.Update(sourceURL: "");

                ActiveCatalog.Instance.RemoveExclusion(steamID, Enums.ExclusionCategory.SourceURL);
            }
            else if (action == "add_gameversion" || action == "remove_gameversion")
            {
                // Convert the itemData string to gameversion and back to string, to make sure we have a correctly formatted gameversion string
                string newGameVersionString = GameVersion.Formatted(Toolkit.ConvertToGameVersion(itemData));

                if (newGameVersionString == GameVersion.Formatted(GameVersion.Unknown))
                {
                    return $"Incorrect gameversion.";
                }

                // Update the new mod
                newMod.Update(compatibleGameVersionString: newGameVersionString);

                // Add or remove exclusion
                if (action[0] == 'a')
                {
                    Mod catalogMod = ActiveCatalog.Instance.ModDictionary[steamID];

                    // Add exclusion only if the mod has a different, known gameversion now
                    if (catalogMod.CompatibleGameVersionString != newGameVersionString && !string.IsNullOrEmpty(catalogMod.CompatibleGameVersionString) &&
                        catalogMod.CompatibleGameVersionString != GameVersion.Formatted(GameVersion.Unknown))
                    {
                        ActiveCatalog.Instance.AddExclusion(steamID, Enums.ExclusionCategory.GameVersion);
                    }
                }
                else
                {
                    ActiveCatalog.Instance.RemoveExclusion(steamID, Enums.ExclusionCategory.GameVersion);
                }
            }
            else if (action == "add_requireddlc" || action == "remove_requireddlc")
            {
                // Convert the DLC string to enum
                Enums.DLC dlc = Toolkit.ConvertToEnum<Enums.DLC>(itemData);

                if (dlc == default)
                {
                    return "Incorrect DLC.";
                }

                if (action[0] == 'a')
                {
                    // Add the required DLC if the mod doesn't already have it
                    if (newMod.RequiredDLC.Contains(dlc))
                    {
                        return "DLC was already required.";
                    }

                    newMod.RequiredDLC.Add(dlc);

                    // Add exclusion
                    ActiveCatalog.Instance.AddExclusion(steamID, Enums.ExclusionCategory.RequiredDLC, (uint)dlc);
                }
                else
                {
                    // Remove the required DLC if the mod has it
                    if (!newMod.RequiredDLC.Contains(dlc))
                    {
                        return "DLC was not required.";
                    }

                    newMod.RequiredDLC.Remove(dlc);

                    // Remove exclusion if it exists
                    ActiveCatalog.Instance.RemoveExclusion(steamID, Enums.ExclusionCategory.RequiredDLC, (uint)dlc);
                }
            }
            else if (action == "add_status" || action == "remove_status")  // [Todo 0.3] Needs action in CatalogUpdater for additional statuses
            {
                // Convert the status string to enum
                Enums.ModStatus status = Toolkit.ConvertToEnum<Enums.ModStatus>(itemData);

                if (status == default)
                {
                    return "Invalid status.";
                }

                if (action[0] == 'a')
                {
                    // Add the status if the mod doesn't already have it
                    if (newMod.Statuses.Contains(status))
                    {
                        return "Status was already active.";
                    }
                    
                    newMod.Statuses.Add(status);

                    // Add exclusion for some unlisted
                    if (status == Enums.ModStatus.UnlistedInWorkshop)
                    {
                        ActiveCatalog.Instance.AddExclusion(steamID, Enums.ExclusionCategory.Unlisted);
                    }
                }
                else
                {
                    // Remove the status if the mod has it
                    if (!newMod.Statuses.Contains(status))
                    {
                        return "Status not found for this mod.";
                    }

                    newMod.Statuses.Remove(status);

                    // Remove exclusion for unlisted, if it exists
                    if (status == Enums.ModStatus.UnlistedInWorkshop)
                    {
                        ActiveCatalog.Instance.RemoveExclusion(steamID, Enums.ExclusionCategory.Unlisted);
                    }
                }
            }
            else if (action == "add_note" || action == "remove_note") // [Todo 0.3] Needs action in CatalogUpdater
            {
                // Add the new note (empty for 'remove')
                newMod.Update(note: itemData);
            }
            else if (action == "add_reviewdate")
            {
                // Nothing to do here, date will be changed below
            }
            else if (action == "add_requiredmod")
            {
                // Exit if the new required mod is already in the list
                if (newMod.RequiredMods.Contains(listMember))
                {
                    return "Mod was already required.";
                }

                newMod.RequiredMods.Add(listMember);

                // Add exclusion
                ActiveCatalog.Instance.AddExclusion(steamID, Enums.ExclusionCategory.RequiredMod, listMember);
            }
            else if (action == "remove_requiredmod")
            {
                // Exit if the to-be-removed mod is not in the list
                if (!newMod.RequiredMods.Contains(listMember))
                {
                    return "Mod was not required.";
                }

                newMod.RequiredMods.Remove(listMember);

                // Remove exclusion if it exists
                ActiveCatalog.Instance.RemoveExclusion(steamID, Enums.ExclusionCategory.RequiredMod, listMember);
            }
            else if (action == "add_successor") // [Todo 0.3] Needs action in CatalogUpdater
            {
                // Exit if the new member is not in the list
                if (newMod.Successors.Contains(listMember))
                {
                    return "Already a successor.";
                }

                newMod.Successors.Add(listMember);
            }
            else if (action == "remove_successor") // [Todo 0.3] Needs action in CatalogUpdater
            {
                // Exit if the to-be-removed member is not in the list
                if (!newMod.Successors.Contains(listMember))
                {
                    return "Successor not found.";
                }

                newMod.Successors.Remove(listMember);
            }
            else if (action == "add_alternative") // [Todo 0.3] Needs action in CatalogUpdater
            {
                // Exit if the new member is already in the list
                if (newMod.Alternatives.Contains(listMember))
                {
                    return "Already an alternative.";
                }

                newMod.Alternatives.Add(listMember);
            }
            else if (action == "remove_alternative") // [Todo 0.3] Needs action in CatalogUpdater
            {
                // Exit if the to-be-removed member is not in the list
                if (!newMod.Alternatives.Contains(listMember))
                {
                    return "Alternative not found.";
                }

                newMod.Alternatives.Remove(listMember);
            }
            else
            {
                return "Invalid Action.";
            }

            // Set the review date to anything. It is only to indicate it should be updated by the CatalogUpdater, which uses its own update date.
            newMod.Update(reviewUpdated: DateTime.Now);

            // Add the copied mod to the collected mods dictionary if the change was successful
            if (!CatalogUpdater.CollectedModInfo.ContainsKey(steamID))
            {
                CatalogUpdater.CollectedModInfo.Add(newMod.SteamID, newMod);
            }

            return "";
        }


        // Add an author item, by author profile ID
        private static string ChangeAuthorItem(ulong authorID, string action, string itemData)
        {
            // Exit if the author ID is invalid or not in the active catalog
            if (authorID == 0 || !ActiveCatalog.Instance.AuthorIDDictionary.ContainsKey(authorID))
            {
                return "Invalid author ID.";
            }

            // Get a copy of the catalog author from the collected authors dictionary or create a new copy
            Author newAuthor = CatalogUpdater.CollectedAuthorIDs.ContainsKey(authorID) ? CatalogUpdater.CollectedAuthorIDs[authorID] : 
                Author.Copy(ActiveCatalog.Instance.AuthorIDDictionary[authorID]);
            
            // Update the author
            string result = ChangeAuthorItem(newAuthor, action, itemData, 0);

            // Add the copied author to the collected mods dictionary if the change was successful
            if (string.IsNullOrEmpty(result) && !CatalogUpdater.CollectedAuthorIDs.ContainsKey(authorID))
            {
                CatalogUpdater.CollectedAuthorIDs.Add(authorID, newAuthor);
            }

            return result;
        }


        // Add an author item, by author custom URL
        private static string ChangeAuthorItem(string authorURL, string action, string itemData, ulong newAuthorID = 0)
        {
            // Exit if the author custom URL is empty or not in the active catalog
            if (string.IsNullOrEmpty(authorURL) || !ActiveCatalog.Instance.AuthorURLDictionary.ContainsKey(authorURL))
            {
                return "Invalid author custom URL.";
            }

            // Get a copy of the catalog author from the collected authors dictionary or create a new copy
            Author newAuthor = CatalogUpdater.CollectedAuthorURLs.ContainsKey(authorURL) ? CatalogUpdater.CollectedAuthorURLs[authorURL] : 
                Author.Copy(ActiveCatalog.Instance.AuthorURLDictionary[authorURL]);

            // Update the author
            string result = ChangeAuthorItem(newAuthor, action, itemData, newAuthorID);

            // Add the copied author to the collected mods dictionary if the change was successful
            if (string.IsNullOrEmpty(result) && !CatalogUpdater.CollectedAuthorURLs.ContainsKey(authorURL))
            {
                CatalogUpdater.CollectedAuthorURLs.Add(authorURL, newAuthor);
            }

            return result;
        }


        // Add an author item, by author type
        private static string ChangeAuthorItem(Author newAuthor, string action, string itemData, ulong newAuthorID)
        {
            if (newAuthor == null)
            {
                return "Author not found.";
            }

            // Update the author item
            if (action == "add_authorid")
            {
                // Exit if the author already has an ID or if the new ID is zero
                if (newAuthor.ProfileID != 0)
                {
                    return "Author already has an author ID.";
                }

                if (newAuthorID == 0)
                {
                    return "Invalid Author ID.";
                }

                newAuthor.Update(profileID: newAuthorID);
            }
            else if (action == "add_authorurl")
            {
                // Exit if the new URL is empty or the same as the current URL
                if (string.IsNullOrEmpty(itemData))
                {
                    return "Invalid custom URL.";
                }

                if (newAuthor.CustomURL == itemData)
                {
                    return "This custom URL is already active.";
                }

                newAuthor.Update(customURL: itemData);
            }
            else if (action == "remove_authorurl")
            {
                // Exit if the author doesn't have a custom URL
                if (string.IsNullOrEmpty(newAuthor.CustomURL))
                {
                    return "No custom URL active.";
                }

                newAuthor.Update(customURL: "");
            }
            else if (action == "add_lastseen")
            {
                DateTime lastSeen;

                // Check if we have a valid date that is more recent than the current author last seen date
                try
                {
                    lastSeen = DateTime.ParseExact(itemData, "yyyy-MM-dd", new CultureInfo("en-GB"));

                    if (lastSeen < newAuthor.LastSeen)
                    {
                        return $"Last Seen date lower than current date in catalog ({ newAuthor.LastSeen }).";
                    }
                }
                catch
                {
                    return "Invalid date.";
                }

                newAuthor.Update(lastSeen: lastSeen);
            }
            else if (action == "add_retired")
            {
                // Exit if the author is already retired
                if (newAuthor.Retired)
                {
                    return "Author already retired.";
                }

                newAuthor.Update(retired: true);
            }
            else if (action == "remove_retired")
            {
                // Exit if the author is not retired now
                if (!newAuthor.Retired)
                {
                    return "Author was not retired.";
                }

                newAuthor.Update(retired: false);
            }
            else
            {
                return "Invalid action.";
            }            

            return "";
        }


        // Add a group
        private static string AddGroup(string groupName, List<string> groupMembers)
        {
            // Exit if name or group members is empty
            if (string.IsNullOrEmpty(groupName))
            {
                return "Invalid group name.";
            }

            if (groupMembers == null)
            {
                return "Not enough parameters.";
            }

            // Convert the members from string to steam IDs
            List<ulong> members = new List<ulong>();

            foreach (string memberString in groupMembers)
            {
                ulong member = Toolkit.ConvertToUlong(memberString);

                // Exit if a memberString can't be converted to numeric
                if (member == 0)
                {
                    return $"Invalid Steam ID { memberString }.";
                }

                members.Add(member);
            }

            // Exit if the group has less than two members
            if (members.Count < 2)
            {
                return $"Not enough parameters.";
            }

            // [Todo 0.3] Find a way to add this to the catalogupdater
            //ModGroup newGroup = new ModGroup(0, groupName, members);
            //CatalogUpdater.CollectedModGroupInfo.Add(newGroup.GroupID, newGroup);

            return "";
        }


        // Remove a group
        private static string RemoveGroup(ulong groupID, ulong replacementMod)
        {
            // Exit if the group doesn't exist or is already in the collected removals
            if (!ActiveCatalog.Instance.ModGroupDictionary.ContainsKey(groupID) || CatalogUpdater.CollectedRemovals.Contains(groupID))
            {
                return "Invalid group ID.";
            }

            // [Todo 0.3] replace group by replacement mod

            // Add group to the removals list
            CatalogUpdater.CollectedRemovals.Add(groupID);

            return "Not implemented yet.";
        }


        // Add a group member
        private static string AddGroupMember(ulong groupID, ulong groupMember)
        {
            // Exit if the group does not exist in the catalog or the collected dictionaries
            if (!ActiveCatalog.Instance.ModGroupDictionary.ContainsKey(groupID) && !CatalogUpdater.CollectedModGroupInfo.ContainsKey(groupID))
            {
                return $"Invalid group ID { groupID }.";
            }

            // Exit if the groupMember does not exist in the catalog or the collected dictionaries
            if (!ActiveCatalog.Instance.ModDictionary.ContainsKey(groupMember) && !CatalogUpdater.CollectedModInfo.ContainsKey(groupMember))
            {
                return $"Invalid Steam ID { groupMember }.";
            }

            // Exit if the group member is already in a group in the catalog or the collected dictionary
            if (ActiveCatalog.Instance.ModGroups.Find(x => x.SteamIDs.Contains(groupMember)) != null ||
                !CatalogUpdater.CollectedModGroupInfo.FirstOrDefault(x => x.Value.SteamIDs.Contains(groupMember)).Equals(default(KeyValuePair<ulong, ModGroup>)))
            {
                return $"Mod { groupMember } is already a member of another group.";
            }

            // Get a copy of the catalog group from the collected groups dictionary or create a new copy
            ModGroup group = CatalogUpdater.CollectedModGroupInfo.ContainsKey(groupID) ? CatalogUpdater.CollectedModGroupInfo[groupID] :
                ModGroup.Copy(ActiveCatalog.Instance.ModGroupDictionary[groupID]);

            // Add the new group member
            group.SteamIDs.Add(groupMember);

            // Add the copied group to the collected groups dictionary
            if (!CatalogUpdater.CollectedModGroupInfo.ContainsKey(groupID))
            {
                CatalogUpdater.CollectedModGroupInfo.Add(group.GroupID, group);
            }

            return "";
        }


        // Remove a group member
        private static string RemoveGroupMember(ulong groupID, ulong groupMember)
        {
            // Exit if the group does not exist in the catalog or the collected dictionaries
            if (!ActiveCatalog.Instance.ModGroupDictionary.ContainsKey(groupID) && !CatalogUpdater.CollectedModGroupInfo.ContainsKey(groupID))
            {
                return $"Invalid group ID { groupID }.";
            }

            // Exit if the groupMember does not exist in the catalog or the collected dictionaries
            if (!ActiveCatalog.Instance.ModDictionary.ContainsKey(groupMember) && !CatalogUpdater.CollectedModInfo.ContainsKey(groupMember))
            {
                return $"Invalid Steam ID { groupMember }.";
            }

            // Get a copy of the catalog group from the collected groups dictionary or create a new copy
            ModGroup group = CatalogUpdater.CollectedModGroupInfo.ContainsKey(groupID) ? CatalogUpdater.CollectedModGroupInfo[groupID] :
                ModGroup.Copy(ActiveCatalog.Instance.ModGroupDictionary[groupID]);

            // Exit is the group member is not actually a member of this group
            if (!group.SteamIDs.Contains(groupMember))
            {
                return $"Mod { groupMember } is not a member of this group.";
            }

            // Exit if the group will not have at least 2 members after removal
            if (group.SteamIDs.Count < 3)
            {
                return "Group does not have enough members to remove one. A group should always have at least two members.";
            }

            // Remove the group member
            string result = group.SteamIDs.Remove(groupMember) ? "" : $"Could not remove { groupMember } from group.";

            // Add the copied group to the collected groups dictionary if the change was successful
            if (string.IsNullOrEmpty(result) && !CatalogUpdater.CollectedModGroupInfo.ContainsKey(groupID))
            {
                CatalogUpdater.CollectedModGroupInfo.Add(group.GroupID, group);
            }

            return result;
        }


        // Add a set of compatibilities between the first mod and each of the other mods
        private static string AddCompatibilitiesForOne(ulong steamID1, string compatibilityString, List<string> steamIDs)
        {
            string result = "";

            // Add a compatibility between the first mod and each mod from the list
            foreach (string steamID2String in steamIDs)
            {
                // Add the compatibility
                result = AddRemoveCompatibility("add_compatibility", steamID1, steamID2: Toolkit.ConvertToUlong(steamID2String), compatibilityString, note: "");

                if (!string.IsNullOrEmpty(result))
                {
                    // Stop if this compatibility could not be added, without processing more compatibilities
                    break;
                }
            }

            return result;
        }


        // Add a set of compatibilities between each of the mods
        private static string AddCompatibilitiesForAll(string compatibilityString, List<string> steamIDs)
        {
            string result = "";

            // Loop from the first to the second to last
            for (int index1 = 0; index1 < steamIDs.Count - 1; index1++)
            {
                ulong steamID1 = Toolkit.ConvertToUlong(steamIDs[index1]);

                // Loop from the one after index1 to the last
                for (int index2 = index1 + 1; index2 < steamIDs.Count; index2++)
                {
                    ulong steamID2 = Toolkit.ConvertToUlong(steamIDs[index2]);

                    // Add a compatibility between the list items at index1 and index2
                    result = AddRemoveCompatibility("add_compatibility", steamID1, steamID2, compatibilityString, note: "");

                    if (!string.IsNullOrEmpty(result))
                    {
                        // Stop if this compatibility could not be added, without processing more compatibilities
                        return result;
                    }
                }
            }

            return result;
        }


        // Add or remove a compatibility
        private static string AddRemoveCompatibility(string action, ulong steamID1, ulong steamID2, string compatibilityString, string note)
        {
            // Exit if SteamID1 is invalid (allow for builtin mods) or not in the active catalog or the collected mod dictionary
            if ((steamID1 >= ModSettings.lowestFakeID && steamID1 <= ModSettings.highestFakeID) ||
                (!ActiveCatalog.Instance.ModDictionary.ContainsKey(steamID1) && !CatalogUpdater.CollectedModInfo.ContainsKey(steamID1)))
            {
                return $"Invalid Steam ID { steamID1 }.";
            }

            // Exit if SteamID2 is invalid (allow for builtin mods) or not in the active catalog or the collected mod dictionary
            if ((steamID2 >= ModSettings.lowestFakeID && steamID2 <= ModSettings.highestFakeID) ||
                (!ActiveCatalog.Instance.ModDictionary.ContainsKey(steamID2) && !CatalogUpdater.CollectedModInfo.ContainsKey(steamID2)))
            {
                return $"Invalid Steam ID { steamID2 }.";
            }

            // Get the compatibility status as enum
            Enums.CompatibilityStatus compatibilityStatus = Toolkit.ConvertToEnum<Enums.CompatibilityStatus>(compatibilityString);

            // Exit if the compatibility status is unknown
            if (compatibilityStatus == default)
            {
                return "Invalid compatibility status.";
            }

            // Check if a compatibility exists for these steam IDs and this compatibility status
            bool compatibilityExists = ActiveCatalog.Instance.Compatibilities.Find(x => x.SteamID1 == steamID1 && x.SteamID2 == steamID2 && 
                x.Statuses.Contains(compatibilityStatus)) != default;

            if (action == "add_compatibility")
            {
                // Add a compatibility to the collected list
                if (compatibilityExists)
                {
                    return $"Compatibility already exists.";
                }

                CatalogUpdater.CollectedCompatibilities.Add(new Compatibility(steamID1, steamID2, new List<Enums.CompatibilityStatus> { compatibilityStatus }, note));
            }
            else if (action == "remove_compatibility")
            {
                // Add a compatibility to the collected removal list
                if (!compatibilityExists)
                {
                    return "Compatibility does not exists.";
                }

                // [Todo 0.3] Add to removals
                return "Not implemented yet.";
            }
            else
            {
                return "Invalid action.";
            }

            return "";
        }


        // Remove an exclusion
        private static string RemoveExclusion(ulong steamID, ulong subitem, string categoryString)
        {
            // Exit if no valid Steam ID
            if (steamID <= ModSettings.highestFakeID)
            {
                return $"Invalid Steam ID { steamID }.";
            }

            // Exit if the category is null
            if (string.IsNullOrEmpty(categoryString))
            {
                return "Invalid category.";
            }

            // Convert the category string to enum
            Enums.ExclusionCategory category = Toolkit.ConvertToEnum<Enums.ExclusionCategory>(categoryString);

            // Exit on incorrect exclusion
            if (category == default)
            {
                return "Incorrect category.";
            }

            // Remove the exclusion; will return false if the exclusion didn't exist
            return ActiveCatalog.Instance.RemoveExclusion(steamID, category, subitem) ? "" : "Could not remove exclusion. It probably didn't exist.";
        }


        // Set the compatible game version for the catalog
        private static string SetCatalogGameVersion(string gameVersionString)
        {
            // Convert the gameversion string to gameversion
            Version newGameVersion = Toolkit.ConvertToGameVersion(gameVersionString);

            // Exit on incorrect game version
            if (newGameVersion == GameVersion.Unknown)
            {
                return "Incorrect gameversion.";
            }

            // Update the active catalog directly
            string result = ActiveCatalog.Instance.UpdateGameVersion(newGameVersion) ? "" : "Could not update gameversion.";

            if (string.IsNullOrEmpty(result))
            {
                // Abuse the 'removals' collection to indicate we have a new gameversion
                CatalogUpdater.CollectedRemovals.Add(1);
            }

            return result;
        }
    }
}
