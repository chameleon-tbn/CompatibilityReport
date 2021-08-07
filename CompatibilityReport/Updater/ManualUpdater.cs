using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using CompatibilityReport.DataTypes;
using CompatibilityReport.Util;


// ManualUpdater gathers update information from CSV files in de updater folder. CatalogUpdater will then update the catalog with this. See Updater Guide for details.


namespace CompatibilityReport.Updater
{
    internal static class ManualUpdater
    {
        // Fake group ID to assign to the next new group. This will be replaced with a real group ID by the CatalogUpdater when adding the group to the catalog
        private static ulong fakeGroupID;


        // Start the manual updater. Should only be called from CatalogUpdater.
        internal static void Start()
        {
            // If the current active catalog is version 1 or 2, we're (re)building the catalog from scratch. Wait with manual updates until version 3 is done.
            if (ActiveCatalog.Instance.Version < 3)
            {
                Logger.UpdaterLog("Updater skipped importing CSV files until catalog version 3 is created.");

                return;
            }

            Logger.UpdaterLog("Updater started the CSV import.", duplicateToRegularLog: true);

            fakeGroupID = ModSettings.highestGroupID;

            // Read all the CSVs and import them into the CatalogUpdater collections
            ImportCSVs();

            if (CatalogUpdater.CSVCombined.Length == 0)
            {
                Logger.UpdaterLog("No CSV files found.", duplicateToRegularLog: true);
            }
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

            // Track overall success and number of CSV files processed (succesful or not)
            bool overallSuccess = true;
            uint numberOfFiles = 0;

            // Sort the filenames
            CSVfiles.Sort();

            // Process all CSV files
            foreach (string CSVfile in CSVfiles)
            {
                Logger.UpdaterLog($"Processing \"{ Toolkit.GetFileName(CSVfile) }\".");

                // Add the filename to the combined CSV and increase the file counter
                CatalogUpdater.CSVCombined.AppendLine($"###################################################");
                CatalogUpdater.CSVCombined.AppendLine($"#### FILE: { Toolkit.GetFileName(CSVfile) }");
                CatalogUpdater.CSVCombined.AppendLine($"###################################################");
                CatalogUpdater.CSVCombined.AppendLine("");

                numberOfFiles++;

                // assume success until proven otherwise
                bool singleFileSuccess = true;

                // Read a single CSV file
                using (StreamReader reader = File.OpenText(CSVfile))
                {
                    string line;

                    // Read each line
                    while ((line = reader.ReadLine()) != null)
                    {
                        // Process the line
                        if (ProcessLine(line))
                        {
                            // Add this line to the combined CSV
                            CatalogUpdater.CSVCombined.AppendLine(line);
                        }
                        else
                        {
                            // Add the failed line with a comment to the combined CSV
                            CatalogUpdater.CSVCombined.AppendLine("# [NOT PROCESSED] " + line);

                            // Tag this file and the overall process as not fully successful
                            singleFileSuccess = false;

                            overallSuccess = false;
                        }
                    }
                }

                // Add some space to the combined CSV
                CatalogUpdater.CSVCombined.AppendLine("");
                CatalogUpdater.CSVCombined.AppendLine("");

                // Rename the processed CSV file to avoid processing it again next time; don't rename in debug mode
                if (ModSettings.DebugMode)
                {
                    Logger.UpdaterLog($"\"{ Toolkit.GetFileName(CSVfile) }\" not renamed due to debug mode. Rename or delete it manually to avoid processing it again.");
                }
                else
                {
                    string newFileName = CSVfile + (singleFileSuccess ? ".processed.txt" : ".partially_processed.txt");

                    if (!Toolkit.MoveFile(CSVfile, newFileName))
                    {
                        Logger.UpdaterLog($"Could not rename \"{ Toolkit.GetFileName(CSVfile) }\". Rename or delete it manually to avoid processing it again.", Logger.error);
                    }
                }
            }

            timer.Stop();

            // Log number of processed files and elapsed time to updater log and regular log
            string s = numberOfFiles == 1 ? "" : "s";

            string logText = $"{ numberOfFiles } CSV file{ s } processed in { Toolkit.ElapsedTime(timer.ElapsedMilliseconds) }" +
                (overallSuccess ? "." : ", with some errors.");

            Logger.UpdaterLog(logText);

            Logger.Log($"{ logText }" + (overallSuccess ? "" : " Check separate logfile for details."), overallSuccess ? Logger.info : Logger.warning);
        }


        // Process a line from a CSV file
        private static bool ProcessLine(string line)
        {
            // Skip empty lines and lines starting with a '#' (comments), without returning an error
            if (string.IsNullOrEmpty(line) || line.Trim()[0] == '#')
            {
                return true;
            }

            // Split the line
            string[] lineFragments = line.Split(',');

            // First element: action
            string action = lineFragments[0].Trim().ToLower();

            // Second element: ID as number (Steam ID or group ID) and as string (author custom url, exclusion category, game version string, catalog note)
            string idString = lineFragments.Length < 2 ? "" : lineFragments[1].Trim();
            ulong id = Toolkit.ConvertToUlong(idString);

            // Third element: second ID or extraData string
            string extraData = "";
            ulong secondID = 0;

            if (lineFragments.Length > 2)
            {
                extraData = lineFragments[2].Trim();

                secondID = Toolkit.ConvertToUlong(extraData);
                
                // Fourth element (if a numeric second ID was found): extraData string
                extraData = secondID == 0 ? extraData : (lineFragments.Length > 3 ? lineFragments[3].Trim() : "");
            }

            string result;

            // Act on the action found  [Todo 0.3] Check if all actions are updated in CatalogUpdater
            switch (action)
            {
                case "add_mod":
                    // Get the author URL if no author ID was found
                    string authorURL = secondID == 0 ? extraData : "";

                    // Join the lineFragments for the mod name, to allow for commas
                    string modName = lineFragments.Length < 4 ? "" : string.Join(",", lineFragments, 3, lineFragments.Length - 3).Trim();

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
                    result = ChangeModItem(action, id, extraData);
                    break;

                case "add_note":
                    // Join the lineFragments for the note, to allow for commas
                    result = lineFragments.Length < 2 ? "Not enough parameters." :
                        ChangeModItem(action, id, string.Join(",", lineFragments, 1, lineFragments.Length - 1).Trim());
                    break;

                case "add_requiredmod":
                case "add_successor":
                case "add_alternative":
                case "add_recommendation":
                case "remove_requiredmod":
                case "remove_successor":
                case "remove_alternative":
                case "remove_recommendation":
                    result = ChangeLinkedMod(action, id, listMember: secondID);
                    break;

                case "add_reviewdate":
                case "remove_archiveurl":
                case "remove_sourceurl":
                case "remove_gameversion":
                case "remove_note":
                    result = ChangeModItem(action, id, "");
                    break;

                case "add_compatibility":
                case "remove_compatibility":
                    // Get the note, if available; if the note starts with a '#', assume a comment instead of an actual note
                    string note = lineFragments.Length < 5 ? "" : 
                        lineFragments[4][0] == '#' ? "" : string.Join(",", lineFragments, 4, lineFragments.Length - 4).Trim();

                    result = lineFragments.Length < 4 ? "Not enough parameters." : 
                        AddRemoveCompatibility(action, id, secondID, compatibilityString: extraData, note);
                    break;

                case "add_compatibilitiesforone":
                case "add_compatibilitiesforall":
                case "add_group":
                case "add_requiredassets":
                case "remove_requiredassets":
                    if ((action.Contains("requiredassets") && lineFragments.Length < 2) || (!action.Contains("assets") && lineFragments.Length < 4))
                    {
                        result = "Not enough parameters.";
                        break;
                    }

                    // Get all line fragments as a list, converted to ulong
                    List<ulong> steamIDs = Toolkit.ConvertToUlong(lineFragments.ToList());

                    // Remove the first one to three elements: action, first mod id, compatibility  /  action, compatibility  /  action, group name  /  action
                    steamIDs.RemoveRange(0, action == "add_compatibilitiesforone" ? 3 : action.Contains("requiredassets") ? 1 : 2);

                    // Remove the last element if it starts with a '#', assuming a comment
                    if (lineFragments.Last().Trim()[0] == '#') 
                    {
                        steamIDs.RemoveAt(steamIDs.Count - 1);
                    }

                    // Exit if we don't have enough Steam IDs: 3 for compatibilitiesforall, 1 for requiredassets, 2 for the others
                    if (steamIDs.Count < (action == "add_compatibilitiesforall" ? 3 : action.Contains("requiredassets") ? 1 : 2))
                    {
                        result = "Not enough parameters.";
                        break;
                    }

                    result = action == "add_compatibilitiesforone" ? AddCompatibilitiesForOne(id, compatibilityString: extraData, steamIDs) :
                             action == "add_compatibilitiesforall" ? AddCompatibilitiesForAll(compatibilityString: idString, steamIDs) :
                             action == "add_group" ? AddGroup(groupName: idString, groupMembers: steamIDs) :
                             action == "add_requiredassets" ? AddRequiredAssets(steamIDs) : RemoveRequiredAssets(steamIDs);
                    break;

                case "remove_group":
                    result = RemoveGroup(groupID: id, replacementModID: secondID);
                    break;

                case "add_groupmember":
                case "remove_groupmember":
                    result = AddRemoveGroupMember(action, groupID: id, groupMember: secondID);
                    break;

                case "add_author":
                    result = AddAuthor(authorID: id, authorURL: idString, name: extraData);
                    break;

                case "add_authorid":
                case "add_authorurl":
                case "add_lastseen":
                case "add_retired":
                case "remove_authorurl":
                case "remove_retired":
                    result = id == 0 ? ChangeAuthorItem(action, authorURL: idString, extraData, newAuthorID: secondID) :
                        ChangeAuthorItem(action, authorID: id, extraData);
                    break;

                case "remove_exclusion":
                    result = RemoveExclusion(steamID: id, subitem: secondID, categoryString: extraData);
                    break;

                case "add_cataloggameversion":
                    result = SetCatalogGameVersion(gameVersionString: idString);
                    break;

                case "add_catalognote":
                    // Join the lineFragments to allow for commas in the note
                    if (lineFragments.Length < 2)
                    {
                        result = "Not enough parameters.";
                    }
                    else
                    {
                        CatalogUpdater.SetNote(string.Join(",", lineFragments, 1, lineFragments.Length - 1).Trim());
                        result = "";
                    }
                    break;

                case "remove_catalognote":
                    CatalogUpdater.SetNote("");
                    result = "";
                    break;

                case "updatedate":
                    result = CatalogUpdater.SetCatalogUpdateDate(idString) ? "" : "Invalid date.";
                    break;

                default:
                    result = "Invalid action.";
                    break;
            }

            if (!string.IsNullOrEmpty(result))
            {
                Logger.UpdaterLog(result + " Line: " + line, Logger.error);

                return false;
            }
            
            return true;
        }


        // Add unlisted mod
        private static string AddMod(ulong steamID, ulong authorID, string authorURL, string modName)
        {
            // Exit if Steam ID is not valid or exists in the catalog or collected modinfo
            if (!IsValidID(steamID, allowBuiltin: false, shouldNotExist: true))
            {
                return $"Invalid Steam ID or mod already exists.";
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
            // Exit if Steam ID is not valid or does not exist in the catalog or collected modinfo
            if (!IsValidID(steamID, allowBuiltin: false))
            {
                return $"Invalid Steam ID or mod does not exist.";
            }

            // Exit if it is listed as required, successor or alternative mod anywhere, or if it is a group member
            Mod catalogMod = ActiveCatalog.Instance.Mods.FirstOrDefault(x => x.RequiredMods.Contains(steamID) || 
                                                                             x.Successors.Contains(steamID) || 
                                                                             x.Alternatives.Contains(steamID));
            Mod collectedMod = CatalogUpdater.CollectedModInfo.FirstOrDefault(x => x.Value.RequiredMods.Contains(steamID) || 
                                                                                   x.Value.Successors.Contains(steamID) || 
                                                                                   x.Value.Alternatives.Contains(steamID)).Value;
            Group catalogGroup = ActiveCatalog.Instance.GetGroup(steamID);
            Group collectedGroup = CatalogUpdater.CollectedGroupInfo.FirstOrDefault(x => x.Value.SteamIDs.Contains(steamID)).Value;

            if (catalogMod != default || collectedMod != default || catalogGroup != default || collectedGroup != default)
            {
                return $"Mod can't be removed because it is still referenced by mods or groups.";
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


        // Change a mod list item
        private static string ChangeLinkedMod(string action, ulong steamID, ulong listMember)
        {
            // Exit if the listMember is not a valid ID or does not exists in the catalog or collected modinfo
            if (!IsValidID(listMember, allowGroup: action.Contains("requiredmod")))
            {
                return $"Invalid Steam { (action.Contains("requiredmod") ? "or group " : "") }ID { listMember }.";
            }

            // Do the actual change
            return ChangeModItem(action, steamID, "", listMember);
        }


        // Change a mod item, with a string value or a list member
        private static string ChangeModItem(string action, ulong steamID, string itemData, ulong listMember = 0)
        {
            // Exit if the Steam ID is not valid or does not exists in the catalog or collected modinfo
            if (!IsValidID(steamID))
            {
                return $"Invalid Steam ID { steamID }.";
            }

            // Exit if itemData is empty and listmember is zero, except for actions that don't need a third parameter
            if (string.IsNullOrEmpty(itemData) && listMember == 0 && 
                action != "add_reviewdate" && action != "remove_archiveurl" && action != "remove_sourceurl" && action != "remove_gameversion" && action != "remove_note")
            {
                return $"Not enough parameters.";
            }

            // Get a copy of the catalog mod from the collected mods dictionary or create a new copy
            Mod newMod = CatalogUpdater.CollectedModInfo.ContainsKey(steamID) ? CatalogUpdater.CollectedModInfo[steamID] : 
                Mod.Copy(ActiveCatalog.Instance.ModDictionary[steamID]);

            // Update the mod [Todo 0.3] Needs actions in CatalogUpdater
            if (action == "add_archiveurl" || action == "remove_archiveurl")
            {
                if (!string.IsNullOrEmpty(itemData) && !itemData.StartsWith("http://") && !itemData.StartsWith("https://"))
                {
                    return "Invalid archive URL.";
                }

                newMod.Update(archiveURL: itemData);
            }
            else if (action == "add_sourceurl")
            {
                if (!itemData.StartsWith("http://") && !itemData.StartsWith("https://"))
                {
                    return "Invalid source URL.";
                }

                newMod.Update(sourceURL: itemData);
                
                // Remove the SourceUnavailable status if it was present
                newMod.Statuses.Remove(Enums.ModStatus.SourceUnavailable);

                ActiveCatalog.Instance.AddExclusion(steamID, Enums.ExclusionCategory.SourceURL);
            }
            else if (action == "remove_sourceurl")
            {
                if (!ActiveCatalog.Instance.ExclusionExists(steamID, Enums.ExclusionCategory.SourceURL))
                {
                    return "Cannot remove source URL because it was not manually added.";
                }

                newMod.Update(sourceURL: "");

                ActiveCatalog.Instance.RemoveExclusion(steamID, Enums.ExclusionCategory.SourceURL);
            }
            else if (action == "add_gameversion")
            {
                // Convert the itemData string to gameversion and back to string, to make sure we have a correctly formatted gameversion string
                string newGameVersionString = GameVersion.Formatted(Toolkit.ConvertToGameVersion(itemData));

                if (newGameVersionString == GameVersion.Formatted(GameVersion.Unknown))
                {
                    return $"Invalid gameversion.";
                }

                newMod.Update(compatibleGameVersionString: newGameVersionString);

                ActiveCatalog.Instance.AddExclusion(steamID, Enums.ExclusionCategory.GameVersion);
            }
            else if (action == "remove_gameversion")
            {
                if (!ActiveCatalog.Instance.ExclusionExists(steamID, Enums.ExclusionCategory.GameVersion))
                {
                    return "Cannot remove compatible gameversion because it was not manually added.";
                }

                newMod.Update(compatibleGameVersionString: "");

                ActiveCatalog.Instance.RemoveExclusion(steamID, Enums.ExclusionCategory.GameVersion);
            }
            else if (action == "add_requireddlc")
            {
                // Convert the DLC string to enum
                Enums.DLC dlc = Toolkit.ConvertToEnum<Enums.DLC>(itemData);

                if (dlc == default)
                {
                    return "Invalid DLC.";
                }

                if (newMod.RequiredDLC.Contains(dlc))
                {
                    return "DLC was already required.";
                }

                newMod.RequiredDLC.Add(dlc);

                ActiveCatalog.Instance.AddExclusion(steamID, Enums.ExclusionCategory.RequiredDLC, (uint)dlc);
            }
            else if (action == "remove_requireddlc")
            {
                // Convert the DLC string to enum
                Enums.DLC dlc = Toolkit.ConvertToEnum<Enums.DLC>(itemData);

                if (dlc == default)
                {
                    return "Invalid DLC.";
                }

                if (!newMod.RequiredDLC.Contains(dlc))
                {
                    return "DLC was not required.";
                }

                if (!ActiveCatalog.Instance.ExclusionExists(steamID, Enums.ExclusionCategory.RequiredDLC, (uint)dlc))
                {
                    return "Cannot remove required DLC because it was not manually added.";
                }

                if (!newMod.RequiredDLC.Remove(dlc))
                {
                    return "Could not removed DLC.";
                }

                ActiveCatalog.Instance.RemoveExclusion(steamID, Enums.ExclusionCategory.RequiredDLC, (uint)dlc);
            }
            else if (action == "add_status")  // [Todo 0.3] Needs action in CatalogUpdater for additional statuses
            {
                // Convert the status string to enum
                Enums.ModStatus status = Toolkit.ConvertToEnum<Enums.ModStatus>(itemData);

                // Status IncompatibleAccordingToWorkshop cannot be manually added
                if (status == default || status == Enums.ModStatus.IncompatibleAccordingToWorkshop)
                {
                    return "Invalid status.";
                }

                if (newMod.Statuses.Contains(status))
                {
                    return "Mod already has this status.";
                }
                    
                newMod.Statuses.Add(status);

                // Add exclusion for some statuses
                if (status == Enums.ModStatus.NoDescription)
                {
                    ActiveCatalog.Instance.AddExclusion(steamID, Enums.ExclusionCategory.NoDescription);
                }
                else if (status == Enums.ModStatus.SourceUnavailable)
                {
                    ActiveCatalog.Instance.AddExclusion(steamID, Enums.ExclusionCategory.SourceURL);

                    // Remove source URL if it was present
                    newMod.Update(sourceURL: "");
                }
            }
            else if (action == "remove_status")
            {
                // Convert the status string to enum
                Enums.ModStatus status = Toolkit.ConvertToEnum<Enums.ModStatus>(itemData);

                // Status IncompatibleAccordingToWorkshop cannot be manually removed
                if (status == default || status == Enums.ModStatus.IncompatibleAccordingToWorkshop)
                {
                    return "Invalid status.";
                }

                if (!newMod.Statuses.Contains(status))
                {
                    return "Status not found for this mod.";
                }

                newMod.Statuses.Remove(status);

                // Remove exclusion for some statuses
                if (status == Enums.ModStatus.NoDescription)
                {
                    ActiveCatalog.Instance.RemoveExclusion(steamID, Enums.ExclusionCategory.NoDescription);
                }
                else if (status == Enums.ModStatus.SourceUnavailable)
                {
                    ActiveCatalog.Instance.RemoveExclusion(steamID, Enums.ExclusionCategory.SourceURL);
                }
            }
            else if (action == "add_note")
            {
                string newNote = (string.IsNullOrEmpty(newMod.Note) ? "" : newMod.Note + "\n") + itemData;

                newMod.Update(note: newNote);
            }
            else if (action == "remove_note")
            {
                newMod.Update(note: "");
            }
            else if (action == "add_reviewdate")
            {
                // Nothing to do here, date will be changed below
            }
            else if (action == "add_requiredmod")
            {
                if (newMod.RequiredMods.Contains(listMember))
                {
                    return "Mod is already required.";
                }

                if (ActiveCatalog.Instance.IsGroupMember(listMember))
                {
                    return "Mod is in a group. Add the group as required mod instead.";
                }

                newMod.RequiredMods.Add(listMember);

                // Add exclusion
                ActiveCatalog.Instance.AddExclusion(steamID, Enums.ExclusionCategory.RequiredMod, listMember);
            }
            else if (action == "remove_requiredmod")
            {
                if (!newMod.RequiredMods.Contains(listMember))
                {
                    if (ActiveCatalog.Instance.IsGroupMember(listMember))
                    {
                        return "Mod is in a group. Try removing the group as required mod instead.";
                    }
                    else
                    {
                        return "Mod is not required.";
                    }
                }

                newMod.RequiredMods.Remove(listMember);

                if (ActiveCatalog.Instance.ExclusionExists(steamID, Enums.ExclusionCategory.RequiredMod, listMember))
                {
                    // Remove the existing exclusion; this required mod was manually added before
                    ActiveCatalog.Instance.RemoveExclusion(steamID, Enums.ExclusionCategory.RequiredMod, listMember);
                }
                else
                {
                    // Add an exclusion for not requiring this mod; this required mod was found by the AutoUpdater  [Todo 0.3] requires exclusion check in autoupdater/catalogupdater
                    ActiveCatalog.Instance.AddExclusion(steamID, Enums.ExclusionCategory.NotRequiredMod, listMember);
                }
            }
            else if (action == "add_successor")
            {
                if (newMod.Successors.Contains(listMember))
                {
                    return "Already a successor.";
                }

                newMod.Successors.Add(listMember);
            }
            else if (action == "remove_successor")
            {
                if (!newMod.Successors.Contains(listMember))
                {
                    return "Successor not found.";
                }

                newMod.Successors.Remove(listMember);
            }
            else if (action == "add_alternative")
            {
                if (newMod.Alternatives.Contains(listMember))
                {
                    return "Already an alternative mod.";
                }

                newMod.Alternatives.Add(listMember);
            }
            else if (action == "remove_alternative")
            {
                if (!newMod.Alternatives.Contains(listMember))
                {
                    return "Alternative mod not found.";
                }

                newMod.Alternatives.Remove(listMember);
            }
            else if (action == "add_recommendation")
            {
                if (newMod.Recommendations.Contains(listMember))
                {
                    return "Already an recommended mod.";
                }

                newMod.Recommendations.Add(listMember);
            }
            else if (action == "remove_recommendation")
            {
                if (!newMod.Recommendations.Contains(listMember))
                {
                    return "Recommended mod not found.";
                }

                newMod.Recommendations.Remove(listMember);
            }
            else
            {
                // For when an extra action is added but not implemented here yet
                return "Not implemented yet.";
            }

            // Set the review date to anything. It is only to indicate it should be updated by the CatalogUpdater, which uses its own update date.
            newMod.Update(reviewUpdated: DateTime.Now);

            // Add the copied mod to the collected mods dictionary
            if (!CatalogUpdater.CollectedModInfo.ContainsKey(steamID))
            {
                CatalogUpdater.CollectedModInfo.Add(newMod.SteamID, newMod);
            }

            return "";
        }


        // Add an author
        private static string AddAuthor(ulong authorID, string authorURL, string name)
        {
            // Exit on an empty author ID/URL
            if (authorID == 0 && string.IsNullOrEmpty(authorURL))
            {
                return "Invalid author ID/URL.";
            }

            // Exit if the name is empty
            if (string.IsNullOrEmpty(name))
            {
                return "Invalid author name.";
            }

            // Empty the URL if the ID is not zero
            authorURL = authorID == 0 ? authorURL : "";

            // Exit if the author already exists in the catalog or collection
            if (ActiveCatalog.Instance.AuthorIDDictionary.ContainsKey(authorID) || CatalogUpdater.CollectedAuthorIDs.ContainsKey(authorID) ||
                ActiveCatalog.Instance.AuthorURLDictionary.ContainsKey(authorURL) || CatalogUpdater.CollectedAuthorURLs.ContainsKey(authorURL))
            {
                return "Author already exists.";
            }

            if (authorID != 0)
            {
                CatalogUpdater.CollectedAuthorIDs.Add(authorID, new Author(authorID, authorURL, name, retired: true));
            }
            else
            {
                CatalogUpdater.CollectedAuthorURLs.Add(authorURL, new Author(authorID, authorURL, name, retired: true));
            }
            
            return "";
        }


        // Change an author item, by author profile ID
        private static string ChangeAuthorItem(string action, ulong authorID, string itemData)
        {
            // Exit if the author ID is invalid or does not exist in the active catalog
            if (authorID == 0 || !ActiveCatalog.Instance.AuthorIDDictionary.ContainsKey(authorID))
            {
                return "Invalid author ID.";
            }

            // Get a copy of the catalog author from the collected authors dictionary or create a new copy
            Author newAuthor = CatalogUpdater.CollectedAuthorIDs.ContainsKey(authorID) ? CatalogUpdater.CollectedAuthorIDs[authorID] :
                Author.Copy(ActiveCatalog.Instance.AuthorIDDictionary[authorID]);

            // Update the author
            string result = ChangeAuthorItem(action, newAuthor, itemData, 0);

            // Add the copied author to the collected mods dictionary if the change was successful
            if (string.IsNullOrEmpty(result) && !CatalogUpdater.CollectedAuthorIDs.ContainsKey(authorID))
            {
                CatalogUpdater.CollectedAuthorIDs.Add(authorID, newAuthor);
            }

            return result;
        }


        // Change an author item, by author custom URL
        private static string ChangeAuthorItem(string action, string authorURL, string itemData, ulong newAuthorID = 0)
        {
            // Exit if the author custom URL is empty or does not exist in the active catalog
            if (string.IsNullOrEmpty(authorURL) || !ActiveCatalog.Instance.AuthorURLDictionary.ContainsKey(authorURL))
            {
                return "Invalid author custom URL.";
            }

            // Get a copy of the catalog author from the collected authors dictionary or create a new copy
            Author newAuthor = CatalogUpdater.CollectedAuthorURLs.ContainsKey(authorURL) ? CatalogUpdater.CollectedAuthorURLs[authorURL] : 
                Author.Copy(ActiveCatalog.Instance.AuthorURLDictionary[authorURL]);

            // Update the author
            string result = ChangeAuthorItem(action, newAuthor, itemData, newAuthorID);

            // Add the copied author to the collected mods dictionary if the change was successful
            if (string.IsNullOrEmpty(result) && !CatalogUpdater.CollectedAuthorURLs.ContainsKey(authorURL))
            {
                CatalogUpdater.CollectedAuthorURLs.Add(authorURL, newAuthor);
            }

            return result;
        }


        // Change an author item, by author type
        private static string ChangeAuthorItem(string action, Author newAuthor, string itemData, ulong newAuthorID)
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

            // Indicate that all fields for this author should be updated by the CatalogUpdater.
            newAuthor.Update(manuallyUpdated: true);

            return "";
        }


        // Add a group
        private static string AddGroup(string groupName, List<ulong> groupMembers)
        {
            // Exit if name is empty or not enough group members
            if (string.IsNullOrEmpty(groupName) || groupMembers == null || groupMembers.Count < 2)
            {
                return "Not enough parameters.";
            }

            // Check if all group members are valid
            foreach (ulong steamID in groupMembers)
            {
                // Exit if a memberString can't be converted to numeric
                if (!IsValidID(steamID))
                {
                    return $"Invalid Steam ID { steamID }.";
                }

                // Exit if the new group member is already a member of another group
                if (ActiveCatalog.Instance.IsGroupMember(steamID) || 
                    CatalogUpdater.CollectedGroupInfo.FirstOrDefault(x => x.Value.SteamIDs.Contains(steamID)).Value != default)
                {
                    return $"Mod { steamID } is already in a group.";
                }
            }

            // Add the group to the collection with a fake group ID which will be replaced by the CatalogUpdater
            Group newGroup = new Group(fakeGroupID, groupName, groupMembers);

            CatalogUpdater.CollectedGroupInfo.Add(newGroup.GroupID, newGroup);

            // Lower the fake group ID so they remain unique
            fakeGroupID--;

            return "";
        }


        // Remove a group
        private static string RemoveGroup(ulong groupID, ulong replacementModID)
        {
            // Exit if the group doesn't exist or is already in the collected removals
            if (!ActiveCatalog.Instance.GroupDictionary.ContainsKey(groupID) || CatalogUpdater.CollectedRemovals.Contains(groupID))
            {
                return "Invalid group ID.";
            }

            // Get all mods that have this group as required mod
            List<Mod> mods = ActiveCatalog.Instance.Mods.FindAll(x => x.RequiredMods.Contains(groupID));

            // Extra checks are only needed if the group is still in use. If it isn't, then replacement mod ID is irrelevant and can be zero.
            if (mods.Count > 0)
            {
                // Exit if the replacement mod isn't valid
                if (IsValidID(replacementModID))
                {
                    return "Invalid replacement mod.";
                }

                // Exit if the replacement mod is a member of any group
                if (ActiveCatalog.Instance.IsGroupMember(replacementModID))
                {
                    return "Replacement mod is a group member and shouldn't be.";
                }

                foreach (Mod mod in mods)
                {
                    // Add a copy of the catalog mod to the collection if it isn't there yet
                    if (!CatalogUpdater.CollectedModInfo.ContainsKey(mod.SteamID))
                    {
                        CatalogUpdater.CollectedModInfo.Add(mod.SteamID, Mod.Copy(mod));
                    }

                    // Remove the group as required mod
                    CatalogUpdater.CollectedModInfo[mod.SteamID].RequiredMods.Remove(groupID);

                    // Add the replacement mod as required mod, if it isn't there already
                    if (!CatalogUpdater.CollectedModInfo[mod.SteamID].RequiredMods.Contains(replacementModID))
                    {
                        CatalogUpdater.CollectedModInfo[mod.SteamID].RequiredMods.Add(replacementModID);
                    }

                    // [Todo 0.3] Update exclusions
                }
            }

            // Add the group to the removals list
            CatalogUpdater.CollectedRemovals.Add(groupID);

            return "";
        }


        // Add a group member
        private static string AddRemoveGroupMember(string action, ulong groupID, ulong groupMember)
        {
            // Exit if the group does not exist in the catalog
            if (!ActiveCatalog.Instance.GroupDictionary.ContainsKey(groupID))
            {
                return "Invalid group ID.";
            }

            // Exit if the group member is not a valid ID or does not exists in the catalog or collected modinfo
            if (!IsValidID(groupMember))
            {
                return $"Invalid Steam ID { groupMember }.";
            }

            // Exit if the group member to add is already in a group in the catalog or the collected dictionary
            if (action == "add_groupmember" && (ActiveCatalog.Instance.IsGroupMember(groupMember) ||
                !CatalogUpdater.CollectedGroupInfo.FirstOrDefault(x => x.Value.SteamIDs.Contains(groupMember)).Equals(default(KeyValuePair<ulong, Group>))))
            {
                return $"Mod { groupMember } is already a member of a group.";
            }

            // Get the catalog group from the collected groups dictionary or create a new copy
            Group group = CatalogUpdater.CollectedGroupInfo.ContainsKey(groupID) ? CatalogUpdater.CollectedGroupInfo[groupID] :
                Group.Copy(ActiveCatalog.Instance.GroupDictionary[groupID]);

            string result = "";

            if (action == "add_groupmember")
            {
                // Add the new group member
                group.SteamIDs.Add(groupMember);
            }
            else
            {
                // Exit is the group member to remove is not actually a member of this group
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
                result = group.SteamIDs.Remove(groupMember) ? "" : $"Could not remove { groupMember } from group.";
            }

            // Add the copied group to the collected groups dictionary if the change was successful
            if (string.IsNullOrEmpty(result) && !CatalogUpdater.CollectedGroupInfo.ContainsKey(groupID))
            {
                CatalogUpdater.CollectedGroupInfo.Add(group.GroupID, group);
            }

            return result;
        }


        // Add a set of compatibilities between the first mod and each of the other mods
        private static string AddCompatibilitiesForOne(ulong steamID1, string compatibilityString, List<ulong> steamIDs)
        {
            // Sort the steamIDs
            steamIDs.Sort();

            ulong previousID = 0;

            // Add a compatibility between the first mod and each mod from the list  [Todo 0.3] Change to convert string-list to ulong-list and check all before adding anything
            foreach (ulong steamID2 in steamIDs)
            {
                // Check if the ID is the same as the previous
                if (steamID2 == previousID)
                {
                    return "Duplicate Steam ID.";
                }

                previousID = steamID2;

                // Add the compatibility
                string result = AddRemoveCompatibility("add_compatibility", steamID1, steamID2, compatibilityString, note: "");

                if (!string.IsNullOrEmpty(result))
                {
                    // Stop if this compatibility could not be added, without processing more compatibilities
                    return result;
                }
            }

            return "";
        }


        // Add a set of compatibilities between each of the mods  [Todo 0.3] change this to use AddCompatibilitiesForOne
        private static string AddCompatibilitiesForAll(string compatibilityString, List<ulong> steamIDs)
        {
            // Sort the steamIDs
            steamIDs.Sort();

            // Loop from the first to the second-to-last  [Todo 0.3] Change to convert string-list to ulong-list and check all before adding anything
            for (int index1 = 0; index1 < steamIDs.Count - 1; index1++)
            {
                ulong steamID1 = steamIDs[index1];

                ulong previousID = steamID1;

                // Loop from the one after index1 to the last
                for (int index2 = index1 + 1; index2 < steamIDs.Count; index2++)
                {
                    ulong steamID2 = steamIDs[index2];

                    // Check if the ID is the same as the previous
                    if (steamID2 == previousID)
                    {
                        return "Duplicate Steam ID.";
                    }

                    previousID = steamID2;

                    // Add a compatibility between the list items at index1 and index2
                    string result = AddRemoveCompatibility("add_compatibility", steamID1, steamID2, compatibilityString, note: "");

                    if (!string.IsNullOrEmpty(result))
                    {
                        // Stop if this compatibility could not be added, without processing more compatibilities
                        return result;
                    }
                }
            }

            return "";
        }


        // Add or remove a compatibility
        private static string AddRemoveCompatibility(string action, ulong steamID1, ulong steamID2, string compatibilityString, string note)
        {
            // Exit if SteamID1 is invalid or does not exist in the active catalog or the collected modinfo
            if (!IsValidID(steamID1))
            {
                return $"Invalid Steam ID { steamID1 }.";
            }

            // Exit if SteamID2 is invalid or does not exist in the active catalog or the collected modinfo
            if (!IsValidID(steamID2))
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
            else
            {
                // Add a compatibility to the collected removal list
                if (!compatibilityExists)
                {
                    return "Compatibility does not exists.";
                }

                // [Todo 0.3] Add to removals
                return "Not implemented yet.";
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


        // Add required assets to the catalog list of assets
        private static string AddRequiredAssets(List<ulong> steamIDs)
        {
            foreach (ulong steamID in steamIDs)
            {
                if (!IsValidID(steamID, allowBuiltin: false, shouldNotExist: true))
                {
                    return $"Invalid Steam ID { steamID }.";
                }

                // If the asset is already in the list, then just ignore it without error
                if (!ActiveCatalog.Instance.Assets.Contains(steamID))
                {
                    // Add the asset directly to the catalog. No need to show this in the change notes.
                    ActiveCatalog.Instance.Assets.Add(steamID);
                }
            }

            return "";
        }


        // Remove required assets from the catalog list of assets
        private static string RemoveRequiredAssets(List<ulong> steamIDs)
        {
            foreach (ulong steamID in steamIDs)
            {
                // Remove the asset directly from the catalog. If the asset is not in the list, then just ignore it without error
                ActiveCatalog.Instance.Assets.Remove(steamID);
            }

            return "";
        }


        // Check if the ID is valid, and if it exists or not (if asked)
        private static bool IsValidID(ulong id,
                                      bool allowBuiltin = true, 
                                      bool allowGroup = false,
                                      bool shouldExist = true,
                                      bool shouldNotExist = false)
        {
            // 'ShouldNotExist' overrules 'shouldExist'
            shouldExist = !shouldNotExist && shouldExist;

            // Check if the ID a valid mod or group ID
            bool valid = id > ModSettings.highestFakeID || 
                         (allowBuiltin && ModSettings.BuiltinMods.ContainsValue(id)) ||
                         (allowGroup && id >= ModSettings.lowestGroupID && id <= ModSettings.highestGroupID);

            // If the ID is valid, do further checks if it should (not) exist
            if (valid && (shouldExist || shouldNotExist))
            {
                // Check if the mod or group already exists and is not in removal collection
                bool exists = (ActiveCatalog.Instance.ModDictionary.ContainsKey(id) || ActiveCatalog.Instance.GroupDictionary.ContainsKey(id) || 
                    CatalogUpdater.CollectedModInfo.ContainsKey(id) || CatalogUpdater.CollectedGroupInfo.ContainsKey(id)) && 
                    !CatalogUpdater.CollectedRemovals.Contains(id);

                // Check if the mod existence is correct
                valid = shouldExist ? exists : !exists;
            }

            return valid;
        }
    }
}
