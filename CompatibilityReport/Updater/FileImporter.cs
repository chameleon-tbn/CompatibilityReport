using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CompatibilityReport.CatalogData;
using CompatibilityReport.Util;


// FileImporter gathers update information from CSV files in de Updater folder, and updates the catalog with this. See Updater Guide for details.


namespace CompatibilityReport.Updater
{
    internal static class FileImporter
    {
        internal static void Start(Catalog catalog)
        {
            CatalogUpdater.SetReviewDate(DateTime.Today);

            Logger.UpdaterLog("Updater started the CSV import.");

            List<string> CSVfilenames = Directory.GetFiles(ModSettings.updaterPath, "*.csv").ToList();

            if (!CSVfilenames.Any())
            {
                Logger.UpdaterLog("No CSV files found.");

                return;
            }

            Stopwatch timer = Stopwatch.StartNew();

            // Sort the filenames, so we get a predictable processing order
            CSVfilenames.Sort();

            bool withoutErrors = true;

            foreach (string CSVfilename in CSVfilenames)
            {
                withoutErrors = withoutErrors && ReadCSV(catalog, CSVfilename);
            }

            timer.Stop();

            Logger.UpdaterLog($"{ CSVfilenames.Count } CSV files processed in { Toolkit.ElapsedTime(timer.ElapsedMilliseconds) }" + 
                (withoutErrors ? "." : ", with errors."), withoutErrors ? Logger.info : Logger.warning);

            Logger.Log("Updater found errors while processing CSV files. See separate log for details.", Logger.warning);
        }


        // Read a CSV file. Returns false on errors.
        private static bool ReadCSV(Catalog catalog, string CSVfileFullPath)
        {
            string filename = Toolkit.GetFileName(CSVfileFullPath);

            bool withoutErrors = true;

            Logger.UpdaterLog($"Processing \"{ filename }\".");

            CatalogUpdater.CSVCombined.AppendLine("###################################################");
            CatalogUpdater.CSVCombined.AppendLine($"#### FILE: { filename }");
            CatalogUpdater.CSVCombined.AppendLine("###################################################\n");

            using (StreamReader reader = File.OpenText(CSVfileFullPath))
            {
                string line;

                uint lineNumber = 0;

                // Read each line in the CSV file
                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;

                    string errorMessage = ReadLine(catalog, line);

                    if (string.IsNullOrEmpty(errorMessage))
                    {
                        CatalogUpdater.CSVCombined.AppendLine(line);
                    }
                    else
                    {
                        Logger.UpdaterLog(errorMessage + $" Line #{ lineNumber }: " + line, Logger.error);

                        // Add the failed line with a comment to the combined CSV
                        CatalogUpdater.CSVCombined.AppendLine("# [ERROR] " + line);

                        withoutErrors = false;
                    }
                }
            }

            CatalogUpdater.CSVCombined.AppendLine("\n");

            if (ModSettings.DebugMode)
            {
                Logger.UpdaterLog($"\"{ filename }\" not renamed because of debug mode. Rename or delete it manually to avoid processing it again.", Logger.warning);
            }
            else
            {
                string newFileName = CSVfileFullPath + (withoutErrors ? ".processed.txt" : ".partially_processed.txt");

                if (!Toolkit.MoveFile(CSVfileFullPath, newFileName))
                {
                    Logger.UpdaterLog($"Could not rename \"{ filename }\". Rename or delete it manually to avoid processing it again.", Logger.error);
                }
            }

            return withoutErrors;
        }


        // Read and process one CSV line. Returns an error message, if any.
        private static string ReadLine(Catalog catalog, string line)
        {
            // Skip empty lines and lines starting with a '#' (comments), without returning an error
            if (string.IsNullOrEmpty(line) || line.Trim()[0] == '#')
            {
                return "";
            }

            string[] lineElements = line.Split(',');

            // First element of the line: action
            string action = lineElements[0].Trim().ToLower();

            // Second element - numeric: mod, group, asset or author ID
            //               and string: review date, author custom url, compatibility status, group name, game version, catalog note, header/footer text
            string stringSecond = lineElements.Length < 2 ? "" : lineElements[1].Trim();
            ulong numericSecond = Toolkit.ConvertToUlong(stringSecond);

            // Third element - numeric: required mod or group ID, successor/alternative/recommended mod ID, mod ID for compatibility/group, author ID, asset ID
            //              and string: author custom url, source/archive url, game version, dlc string, stability, stability/generic note, mod/compatibility status,
            //                          exclusion category, author name, last seen date
            string stringThird = lineElements.Length < 3 ? "" : lineElements[2].Trim();
            ulong numericThird = Toolkit.ConvertToUlong(stringThird); ;

            // Fourth element - numeric: additional mod or asset ID, dlc appid
            //               and string: mod name, compatibility status
            string stringFourth = lineElements.Length < 4 ? "" : lineElements[3].Trim();
            ulong numericFourth = Toolkit.ConvertToUlong(stringFourth);

            // Act on the action found
            switch (action)
            {
                case "reviewdate":
                    return CatalogUpdater.SetReviewDate(Toolkit.ConvertDate(stringSecond));

                case "add_mod":
                    // Use the author URL only if no author ID was found. Join the lineFragments for the mod name, to allow for commas
                    return AddMod(catalog, modID: numericSecond, status: stringThird.ToLower(), authorID: numericFourth, 
                        authorURL: numericFourth == 0 ?  stringFourth : "", 
                        modName: lineElements.Length < 5 ? "" : string.Join(",", lineElements, 4, lineElements.Length - 4).Trim());

                case "remove_mod":
                    return RemoveMod(catalog, modID: numericSecond); 

                case "set_sourceurl":
                case "set_gameversion":
                case "add_requireddlc":
                case "set_stability":
                case "add_status":
                case "remove_requireddlc":
                case "remove_status":
                    return string.IsNullOrEmpty(stringThird) ? "Not enough parameters." : ChangeModItem(catalog, action, modID: numericSecond, stringThird); 

                case "set_stabilitynote":
                case "set_genericnote":
                    // Join the lineFragments for the note, to allow for commas. Replace "\n" in CSV text by real '\n' characters
                    return lineElements.Length < 3 ? "Not enough parameters." :
                        ChangeModItem(catalog, action, modID: numericSecond, string.Join(",", lineElements, 2, lineElements.Length - 2).Trim().Replace("\\n", "\n"));

                case "update_review":
                case "remove_sourceurl":
                case "remove_gameversion":
                case "remove_stabilitynote":
                case "remove_genericnote":
                    return ChangeModItem(catalog, action, modID: numericSecond);

                case "add_requiredmod":
                case "add_successor":
                case "add_alternative":
                case "add_recommendation":
                case "remove_requiredmod":
                case "remove_successor":
                case "remove_alternative":
                case "remove_recommendation":
                    return !catalog.IsValidID(numericThird) ? $"Invalid mod ID { numericThird }." :
                        ChangeModItem(catalog, action, modID: numericSecond, listMember: numericThird);

                case "remove_exclusion":
                    return RemoveExclusion(catalog, modID: numericSecond, categoryString: stringThird.ToLower(), dlcString: stringFourth, requiredID: numericFourth);

                case "add_compatibility":
                case "remove_compatibility":
                    // Get the note, if available. If the note starts with a '#', it is a comment instead of a note.
                    string compatibilityNote = lineElements.Length < 5 ? "" : lineElements[4].Trim()[0] == '#' ? "" : 
                        string.Join(",", lineElements, 4, lineElements.Length - 4).Trim().Replace("\\n", "\n");

                    return lineElements.Length < 4 ? "Not enough parameters." : 
                        AddRemoveCompatibility(catalog, action, firstModID: numericSecond, secondModID: numericThird, 
                            compatibilityString: stringFourth.ToLower(), compatibilityNote);

                case "add_compatibilitiesforone":
                case "add_compatibilitiesforall":
                case "add_group":
                case "add_requiredassets":
                case "remove_requiredassets":
                    if ((action.Contains("compatibilities") && lineElements.Length < 5) || (action.Contains("group") && lineElements.Length < 4) || lineElements.Length < 2)
                    {
                        return "Not enough parameters.";
                    }

                    // Get all line fragments as a list, converted to ulong
                    List<ulong> modIDs = Toolkit.ConvertToUlong(lineElements.ToList());

                    // Remove the first one to three elements: action, first mod id, compatibility  /  action, compatibility  /  action, group name  /  action
                    modIDs.RemoveRange(0, action == "add_compatibilitiesforone" ? 3 : action.Contains("requiredassets") ? 1 : 2);

                    // Remove the last element if it starts with a '#' (comment)
                    if (lineElements.Last().Trim()[0] == '#') 
                    {
                        modIDs.RemoveAt(modIDs.Count - 1);

                        // Exit if we don't have enough IDs left in the group: 3 for compatibilitiesforall, 1 for requiredassets, 2 for the others
                        if (modIDs.Count < (action == "add_compatibilitiesforall" ? 3 : action.Contains("requiredassets") ? 1 : 2))
                        {
                            return "Not enough parameters.";
                        }
                    }

                    return action == "add_compatibilitiesforone" ? AddCompatibilitiesForOne(catalog, firstModID: numericSecond, compatibilityStatusString: stringThird.ToLower(), modIDs) :
                           action == "add_compatibilitiesforall" ? AddCompatibilitiesForAll(catalog, compatibilityStatusString: stringSecond.ToLower(), modIDs) :
                           action == "add_group" ? AddGroup(catalog, groupName: stringSecond, groupMembers: modIDs) :
                           action == "add_requiredassets" ? AddRequiredAssets(catalog, modIDs) : RemoveRequiredAssets(catalog, modIDs);

                case "remove_group":
                    return RemoveGroup(catalog, groupID: numericSecond);

                case "add_groupmember":
                case "remove_groupmember":
                    return AddRemoveGroupMember(catalog, action, groupID: numericSecond, groupMember: numericThird);

                case "add_author":
                    // Use author url only if author ID was not found
                    return AddAuthor(catalog, authorID: numericSecond, authorURL: numericSecond == 0 ? stringSecond : "", authorName: stringThird);

                case "set_authorid":
                case "set_authorurl":
                case "remove_authorurl":
                case "set_lastseen":
                case "set_retired":
                case "remove_retired":
                    return ChangeAuthorItem(catalog, action, authorID: numericSecond, authorURL: stringSecond, itemData: stringThird, newAuthorID: numericThird);

                case "set_cataloggameversion":
                    return SetCatalogGameVersion(catalog, newGameVersion: Toolkit.ConvertToGameVersion(stringSecond));

                case "set_catalognote":
                case "set_catalogheadertext":
                case "set_catalogfootertext":
                    // Join the lineFragments for the note, to allow for commas. Replace "\n" in CSV text by real '\n' characters
                    return lineElements.Length < 2 ? "Not enough parameters." : 
                        ChangeCatalogText(catalog, action, string.Join(",", lineElements, 1, lineElements.Length - 1).Trim().Replace("\\n", "\n"));

                case "remove_catalognote":
                case "remove_catalogheadertext":
                case "remove_catalogfootertext":
                    return ChangeCatalogText(catalog, action, "");

                default:
                    return "Invalid action.";
            }
        }


        // Add an unlisted or removed mod
        private static string AddMod(Catalog catalog, ulong modID, string status, ulong authorID, string authorURL, string modName)
        {
            if (!catalog.IsValidID(modID, shouldExist: false))
            {
                return "Invalid Steam ID or mod already exists.";
            }

            if (status != "unlisted" && status != "removed")
            {
                return "Invalid status, must be 'unlisted' or 'removed'.";
            }

            Mod newMod = CatalogUpdater.GetOrAddMod(catalog, modID, modName == "" ? null : modName, unlisted: status == "unlisted", removed: status == "removed");

            CatalogUpdater.UpdateMod(catalog, newMod, authorID: authorID, authorURL: authorURL, alwaysUpdateReviewDate: true);

            return "";
        }


        // Remove a removed mod from the catalog
        private static string RemoveMod(Catalog catalog, ulong modID)
        {
            if (!catalog.IsValidID(modID, allowBuiltin: false))
            {
                return "Invalid Steam ID or mod does not exist.";
            }

            Mod catalogMod = catalog.ModDictionary[modID];

            if (!catalogMod.Statuses.Contains(Enums.ModStatus.RemovedFromWorkshop))
            {
                return "Mod can't be removed because it is not removed from the Steam Workshop.";
            }

            if (catalog.IsGroupMember(modID))
            {
                return "Mod can't be removed because it is still in a group.";
            }

            // Check if the mod is listed as required, successor, alternative or recommended mod anywhere
            if (catalog.Mods.FirstOrDefault(x => x.RequiredMods.Contains(modID) || x.Successors.Contains(modID) ||
                                                                x.Alternatives.Contains(modID) || x.Recommendations.Contains(modID)) != default)
            {
                return "Mod can't be removed because it is still referenced by other mods (required, successor, alternative or recommendation).";
            }

            CatalogUpdater.AddRemovedModChangeNote(catalogMod);

            catalog.Mods.Remove(catalogMod);     // [Todo 0.4] Move to Catalog class as RemoveMod?

            catalog.ModDictionary.Remove(modID);

            return "";
        }


        // Change a mod item
        private static string ChangeModItem(Catalog catalog, string action, ulong modID, string itemData = "", ulong listMember = 0)
        {
            if (!catalog.IsValidID(modID))
            {
                return $"Invalid mod ID { modID }.";
            }

            Mod catalogMod = catalog.ModDictionary[modID];

            // Act on the action
            if (action == "set_sourceurl")
            {
                if (!itemData.StartsWith("http://") && !itemData.StartsWith("https://"))
                {
                    return "Invalid URL.";
                }

                CatalogUpdater.UpdateMod(catalog, catalogMod, sourceURL: itemData);

                CatalogUpdater.RemoveStatus(catalogMod, Enums.ModStatus.SourceUnavailable);
            }
            else if (action == "remove_sourceurl")
            {
                if (string.IsNullOrEmpty(catalogMod.SourceURL))
                {
                    return "No source URL to remove.";
                }

                CatalogUpdater.UpdateMod(catalog, catalogMod, sourceURL: "");
            }
            else if (action == "set_gameversion")
            {
                // Convert the itemData string to gameversion and back to string, to make sure we have a consistently formatted gameversion string
                string newGameVersionString = Toolkit.ConvertGameVersionToString(Toolkit.ConvertToGameVersion(itemData));

                if (newGameVersionString == Toolkit.ConvertGameVersionToString(Toolkit.UnknownVersion))
                {
                    return "Invalid gameversion.";
                }

                CatalogUpdater.UpdateMod(catalog, catalogMod, compatibleGameVersionString: newGameVersionString);
            }
            else if (action == "remove_gameversion")
            {
                if (!catalogMod.ExclusionForGameVersion)
                {
                    return "Cannot remove compatible gameversion because it was not manually added.";
                }

                // Remove the game version and the exclusion
                CatalogUpdater.UpdateMod(catalog, catalogMod, compatibleGameVersionString: "");
            }
            else if (action == "add_requireddlc")
            {
                Enums.DLC requiredDLC = Toolkit.ConvertToEnum<Enums.DLC>(itemData);

                if (requiredDLC == default)
                {
                    return "Invalid DLC.";
                }

                if (catalogMod.RequiredDLC.Contains(requiredDLC))
                {
                    return "DLC is already required.";
                }

                CatalogUpdater.AddRequiredDLC(catalogMod, requiredDLC);
            }
            else if (action == "remove_requireddlc")
            {
                Enums.DLC requiredDLC = Toolkit.ConvertToEnum<Enums.DLC>(itemData);

                if (requiredDLC == default)
                {
                    return "Invalid DLC.";
                }

                if (!catalogMod.RequiredDLC.Contains(requiredDLC))
                {
                    return "DLC is not required.";
                }

                if (!catalogMod.ExclusionForRequiredDLC.Contains(requiredDLC))
                {
                    return "Cannot remove required DLC because it was not manually added.";
                }

                CatalogUpdater.RemoveRequiredDLC(catalogMod, requiredDLC);
            }
            else if (action == "set_stability")
            {
                Enums.ModStability stability = Toolkit.ConvertToEnum<Enums.ModStability>(itemData);

                if (stability == default)
                {
                    return "Invalid stability.";
                }

                if (catalogMod.Stability == stability)
                {
                    return "Mod already has this stability.";
                }

                if (stability == Enums.ModStability.IncompatibleAccordingToWorkshop && !catalogMod.Statuses.Contains(Enums.ModStatus.RemovedFromWorkshop))
                {
                    return "The Incompatible stability can only be set on a removed mod.";
                }
                else if (catalogMod.Stability == Enums.ModStability.IncompatibleAccordingToWorkshop && !catalogMod.Statuses.Contains(Enums.ModStatus.RemovedFromWorkshop))
                {
                    return "Mod has the Incompatible stability and that can only be changed for a removed mod.";
                }

                CatalogUpdater.UpdateMod(catalog, catalogMod, stability: stability);
            }
            else if (action == "add_status")
            {
                Enums.ModStatus status = Toolkit.ConvertToEnum<Enums.ModStatus>(itemData);

                if (status == default)
                {
                    return "Invalid status.";
                }
                
                if ((status == Enums.ModStatus.NoCommentSectionOnWorkshop || status == Enums.ModStatus.NoDescription) && 
                    catalogMod.Statuses.Contains(Enums.ModStatus.RemovedFromWorkshop))
                {
                    return "Status cannot be combined with existing 'RemovedFromWorkshop' status.";
                }
                else if ((status == Enums.ModStatus.Abandoned || status == Enums.ModStatus.Deprecated) && catalogMod.Statuses.Contains(Enums.ModStatus.NoLongerNeeded))
                {
                    return "Status cannot be combined with existing 'NoLongerNeeded' status.";
                }
                else if (status == Enums.ModStatus.Abandoned && catalogMod.Statuses.Contains(Enums.ModStatus.Deprecated))
                {
                    return "Status cannot be combined with existing 'Deprecated' status.";
                }

                if (catalogMod.Statuses.Contains(status))
                {
                    return "Mod already has this status.";
                }

                CatalogUpdater.AddStatus(catalog, catalogMod, status);
            }
            else if (action == "remove_status")
            {
                Enums.ModStatus status = Toolkit.ConvertToEnum<Enums.ModStatus>(itemData);

                if (status == Enums.ModStatus.UnlistedInWorkshop)
                {
                    return "The 'Unlisted' status cannot be manually removed. Adding a 'removed' status will remove the 'unlisted' status automatically.";
                }

                if (!CatalogUpdater.RemoveStatus(catalogMod, status))
                {
                    return "Status not found for this mod.";
                }
            }
            else if (action == "set_stabilitynote")
            {
                if (!string.IsNullOrEmpty(catalogMod.StabilityNote) && catalogMod.StabilityNote == itemData)
                {
                    return "Note already added.";
                }

                CatalogUpdater.UpdateMod(catalog, catalogMod, stabilityNote: itemData);
            }
            else if (action == "set_genericnote")
            {
                if (!string.IsNullOrEmpty(catalogMod.GenericNote) && catalogMod.GenericNote == itemData)
                {
                    return "Note already added.";
                }

                CatalogUpdater.UpdateMod(catalog, catalogMod, genericNote: itemData);
            }
            else if (action == "remove_stabilitynote")
            {
                if (string.IsNullOrEmpty(catalogMod.StabilityNote))
                {
                    return "Note already empty.";
                }

                CatalogUpdater.UpdateMod(catalog, catalogMod, stabilityNote: "");
            }
            else if (action == "remove_genericnote")
            {
                if (string.IsNullOrEmpty(catalogMod.GenericNote))
                {
                    return "Note already empty.";
                }

                CatalogUpdater.UpdateMod(catalog, catalogMod, genericNote: "");
            }
            else if (action == "update_review")
            {
                CatalogUpdater.UpdateMod(catalog, catalogMod, alwaysUpdateReviewDate: true);
            }
            else if (action == "add_requiredmod")
            {
                if (catalogMod.RequiredMods.Contains(listMember))
                {
                    return "Mod is already required.";
                }

                CatalogUpdater.AddRequiredMod(catalog, catalogMod, listMember);
            }
            else if (action == "remove_requiredmod")
            {
                if (!catalogMod.RequiredMods.Contains(listMember))
                {
                    return "Mod is not required.";
                }

                CatalogUpdater.RemoveRequiredMod(catalog, catalogMod, listMember);
            }
            else if (action == "add_successor")
            {
                if (catalogMod.Successors.Contains(listMember))
                {
                    return "Already a successor.";
                }

                CatalogUpdater.AddSuccessor(catalogMod, listMember);
            }
            else if (action == "remove_successor")
            {
                if (!catalogMod.Successors.Contains(listMember))
                {
                    return "Successor not found.";
                }

                CatalogUpdater.RemoveSuccessor(catalogMod, listMember);
            }
            else if (action == "add_alternative")
            {
                if (catalogMod.Alternatives.Contains(listMember))
                {
                    return "Already an alternative mod.";
                }

                CatalogUpdater.AddAlternative(catalogMod, listMember);
            }
            else if (action == "remove_alternative")
            {
                if (!catalogMod.Alternatives.Contains(listMember))
                {
                    return "Alternative mod not found.";
                }

                CatalogUpdater.RemoveAlternative(catalogMod, listMember);
            }
            else if (action == "add_recommendation")
            {
                if (catalogMod.Recommendations.Contains(listMember))
                {
                    return "Already an recommended mod.";
                }

                CatalogUpdater.AddRecommendation(catalogMod, listMember);
            }
            else if (action == "remove_recommendation")
            {
                if (!catalogMod.Recommendations.Contains(listMember))
                {
                    return "Recommended mod not found.";
                }

                CatalogUpdater.RemoveRecommendation(catalogMod, listMember);
            }
            else
            {
                // Throw an error for when an extra action is later added but not implemented here yet
                return "Not implemented yet.";
            }

            return "";
        }


        // Remove an exclusion for SourceURL, GameVersion, RequiredDLC, RequiredMod or NoDescription
        private static string RemoveExclusion(Catalog catalog, ulong modID, string categoryString, string dlcString, ulong requiredID)
        {
            if (!catalog.IsValidID(modID))
            {
                return $"Invalid Steam ID { modID }.";
            }

            if (string.IsNullOrEmpty(categoryString))
            {
                return "Not enough parameters.";
            }

            Mod catalogMod = catalog.ModDictionary[modID];

            if (categoryString == "sourceurl")
            {
                catalogMod.Update(exclusionForSourceURL: false);
            }
            else if (categoryString == "gameversion")
            {
                catalogMod.Update(exclusionForGameVersion: false);
            }
            else if (categoryString == "nodescription")
            {
                catalogMod.Update(exclusionForNoDescription: false);
            }
            else if (categoryString == "requireddlc")
            {
                if (string.IsNullOrEmpty(dlcString))
                {
                    return "Not enough parameters.";
                }

                if (!catalogMod.ExclusionForRequiredDLC.Remove(Toolkit.ConvertToEnum<Enums.DLC>(dlcString)))
                {
                    return "Invalid DLC or no exclusion exists.";
                }
            }
            else if (categoryString == "requiredmod")
            {
                if (requiredID == 0)
                {
                    return "Not enough parameters.";
                }

                if (!catalogMod.ExclusionForRequiredMods.Remove(requiredID))
                {
                    return "Invalid required mod ID or no exclusion exists.";
                }
            }
            else
            {
                return "Invalid category.";
            }

            return "";
        }


        // Add an author, as retired
        private static string AddAuthor(Catalog catalog, ulong authorID, string authorURL, string authorName)
        {
            if ((authorID == 0 && string.IsNullOrEmpty(authorURL)) || string.IsNullOrEmpty(authorName))
            {
                return "Not enough parameters.";
            }

            if (catalog.AuthorIDDictionary.ContainsKey(authorID) || catalog.AuthorURLDictionary.ContainsKey(authorURL))
            {
                return "Author already exists.";
            }

            Author newAuthor = CatalogUpdater.GetOrAddAuthor(catalog, authorID, authorURL, authorName);

            CatalogUpdater.UpdateAuthor(newAuthor, retired: true);
            
            return "";
        }


        // Change an author item, by author type
        private static string ChangeAuthorItem(Catalog catalog, string action, ulong authorID, string authorURL, string itemData, ulong newAuthorID)
        {
            Author catalogAuthor;

            if (authorID != 0)
            {
                if (!catalog.AuthorIDDictionary.ContainsKey(authorID))
                {
                    return "Invalid author ID.";
                }

                catalogAuthor = catalog.AuthorIDDictionary[authorID];
            }
            else
            {
                if (!catalog.AuthorURLDictionary.ContainsKey(authorURL))
                {
                    return "Invalid author custom URL.";
                }

                catalogAuthor = catalog.AuthorURLDictionary[authorURL];
            }

            if (catalogAuthor == null)
            {
                return "Author not found.";
            }

            // Act on the action
            if (action == "set_authorid")
            {
                if (catalogAuthor.ProfileID != 0)
                {
                    return "Author already has an author ID.";
                }

                if (newAuthorID == 0)
                {
                    return "Invalid Author ID.";
                }

                CatalogUpdater.UpdateAuthor(catalogAuthor, authorID);
            }
            else if (action == "set_authorurl")
            {
                if (string.IsNullOrEmpty(itemData))
                {
                    return "Invalid custom URL.";
                }

                if (catalogAuthor.CustomURL == itemData)
                {
                    return "This custom URL is already active.";
                }

                CatalogUpdater.UpdateAuthor(catalogAuthor, authorURL: itemData);
            }
            else if (action == "remove_authorurl")
            {
                if (string.IsNullOrEmpty(catalogAuthor.CustomURL))
                {
                    return "No custom URL active.";
                }

                CatalogUpdater.UpdateAuthor(catalogAuthor, authorURL: "");
            }
            else if (action == "set_lastseen")
            {
                DateTime lastSeen = Toolkit.ConvertDate(itemData);

                if (lastSeen == default)
                {
                    return "Invalid date.";
                }

                if (lastSeen == catalogAuthor.LastSeen)
                {
                    return "Author already has this last seen date.";
                }
                
                if (lastSeen < catalogAuthor.LastSeen)
                {
                    Logger.UpdaterLog($"Lowered the last seen date for { catalogAuthor.ToString() }, " +
                        $"from { Toolkit.DateString(catalogAuthor.LastSeen) } to { Toolkit.DateString(lastSeen) }.");
                }

                CatalogUpdater.UpdateAuthor(catalogAuthor, lastSeen: lastSeen);
            }
            else if (action == "set_retired")
            {
                if (catalogAuthor.Retired)
                {
                    return "Author already retired.";
                }

                CatalogUpdater.UpdateAuthor(catalogAuthor, retired: true);
            }
            else if (action == "remove_retired")
            {
                if (!catalogAuthor.Retired)
                {
                    return "Author was not retired.";
                }

                if (!catalogAuthor.ExclusionForRetired)
                {
                    return "Author retirement is automatic and cannot be removed. Try adding a recent 'last seen' date.";
                }

                CatalogUpdater.UpdateAuthor(catalogAuthor, retired: false);
            }
            else
            {
                return "Invalid action.";
            }

            return "";
        }


        // Add a group
        private static string AddGroup(Catalog catalog, string groupName, List<ulong> groupMembers)
        {
            if (string.IsNullOrEmpty(groupName) || groupMembers == null || groupMembers.Count < 2)
            {
                return "Not enough parameters.";
            }

            if (catalog.Groups.Find(x => x.Name == groupName) != default)
            {
                return "A group with that name already exists.";
            }

            foreach (ulong groupMember in groupMembers)
            {
                if (!catalog.IsValidID(groupMember))
                {
                    return $"Invalid mod ID { groupMember }.";
                }

                if (catalog.IsGroupMember(groupMember))
                {
                    return $"Mod { groupMember } is already in a group and a mod can only be in one.";
                }
            }

            CatalogUpdater.AddGroup(catalog, groupName, groupMembers);

            return "";
        }


        // Remove a group
        private static string RemoveGroup(Catalog catalog, ulong groupID)
        {
            if (!catalog.GroupDictionary.ContainsKey(groupID))
            {
                return "Invalid group ID.";
            }

            CatalogUpdater.RemoveGroup(catalog, groupID);

            return "";
        }


        // Add or remove a group member
        private static string AddRemoveGroupMember(Catalog catalog, string action, ulong groupID, ulong groupMember)
        {
            if (!catalog.GroupDictionary.ContainsKey(groupID))
            {
                return "Invalid group ID.";
            }

            if (!catalog.IsValidID(groupMember))
            {
                return $"Invalid mod ID { groupMember }.";
            }

            Group group = catalog.GroupDictionary[groupID];

            if (action == "add_groupmember")
            {
                if (catalog.IsGroupMember(groupMember))
                {
                    return $"Mod { groupMember } is already in a group and a mod can only be in one.";
                }

                CatalogUpdater.AddGroupMember(catalog, group, groupMember);

                return "";
            }
            else
            {
                if (!group.GroupMembers.Contains(groupMember))
                {
                    return $"Mod { groupMember } is not a member of this group.";
                }

                return CatalogUpdater.RemoveGroupMember(catalog, group, groupMember) ? "" : $"Could not remove { groupMember } from group.";
            }
        }


        // Add a set of compatibilities between each of the mods in a list
        private static string AddCompatibilitiesForAll(Catalog catalog, string compatibilityStatusString, List<ulong> steamIDs)
        {
            if (compatibilityStatusString == "newerversion" || compatibilityStatusString == "functionalitycovered" || 
                compatibilityStatusString == "incompatibleaccordingtoauthor" || compatibilityStatusString == "incompatibleaccordingtousers" || 
                compatibilityStatusString == "compatibleaccordingtoauthor")
            {
                return "This compatibility status cannot be used for \"CompatibilitiesForAll\".";
            }

            // Sort the steamIDs so we're able to detect duplicate Steam IDs
            steamIDs.Sort();

            int numberOfSteamIDs = steamIDs.Count;

            // Loop from the first to the second-to-last
            for (int index1 = 0; index1 < numberOfSteamIDs - 1; index1++)
            {
                // Set the first element as first mod, and remove it from the list
                ulong firstModID = steamIDs[0];

                steamIDs.RemoveAt(0);

                // Add compatibilities between this mod and each mod from the list
                string result = AddCompatibilitiesForOne(catalog, firstModID, compatibilityStatusString, steamIDs);

                if (!string.IsNullOrEmpty(result))
                {
                    // Stop if these compatibilities could not be added, without processing more compatibilities
                    return result;
                }
            }

            return "";
        }


        // Add a set of compatibilities between one mod and a list of others
        private static string AddCompatibilitiesForOne(Catalog catalog, ulong firstModID, string compatibilityStatusString, List<ulong> steamIDs)
        {
            if (compatibilityStatusString == "minorissues" || compatibilityStatusString == "requiresspecificsettings")
            {
                return "This compatibility status needs a note and cannot be used in an action with multiple compatibilities.";
            }

            // Sort the steamIDs so we're able to detect duplicate Steam IDs
            steamIDs.Sort();

            ulong previousSecond = 0;

            // Add compatibilities between the first mod and each mod from the list
            foreach (ulong secondModID in steamIDs)
            {
                if (secondModID == previousSecond)
                {
                    return "Duplicate Steam ID.";
                }

                // Add the compatibility
                string result = AddRemoveCompatibility(catalog, "add_compatibility", firstModID, secondModID, compatibilityStatusString, compatibilityNote: "");

                if (!string.IsNullOrEmpty(result))
                {
                    // Stop if this compatibility could not be added, without processing more compatibilities
                    return result + " Some of the compatibilities might have been added, check the change notes.";
                }

                previousSecond = secondModID;
            }

            return "";
        }


        // Add or remove a compatibility between two mods
        private static string AddRemoveCompatibility(Catalog catalog, string action, ulong firstModID, ulong secondModID, 
            string compatibilityString, string compatibilityNote)
        {
            Enums.CompatibilityStatus compatibilityStatus = Toolkit.ConvertToEnum<Enums.CompatibilityStatus>(compatibilityString);

            if (compatibilityStatus == default)
            {
                return "Invalid compatibility status.";
            }

            if (firstModID == secondModID)
            {
                return $"Duplicate Steam ID { firstModID }.";
            }
            else if (!catalog.IsValidID(firstModID))
            {
                return $"Invalid Steam ID { firstModID }.";
            }
            else if (!catalog.IsValidID(secondModID))
            {
                return $"Invalid Steam ID { secondModID }.";
            }

            // Check if a compatibility exists for these steam IDs and this compatibility status
            bool compatibilityExists = catalog.Compatibilities.Find(x => x.FirstModID == firstModID && x.SecondModID == secondModID && 
                x.Status == compatibilityStatus) != default;

            if (action == "add_compatibility")
            {
                if (compatibilityExists)
                {
                    return "Compatibility already exists.";
                }

                // Check if a mirrored compatibility already exists; this is allowed for some statuses, but not all  [Todo 0.4] Can we allow all compatibilities mirrored?
                if (compatibilityStatus == Enums.CompatibilityStatus.SameModDifferentReleaseType || compatibilityStatus == Enums.CompatibilityStatus.SameFunctionality ||
                    compatibilityStatus == Enums.CompatibilityStatus.MinorIssues || compatibilityStatus == Enums.CompatibilityStatus.RequiresSpecificSettings)
                {
                    bool mirroredCompatibilityExists = catalog.Compatibilities.Find(x => x.FirstModID == secondModID && x.SecondModID == firstModID && 
                        x.Status == compatibilityStatus) != default;

                    if (mirroredCompatibilityExists)
                    {
                        return $"Compatibility already exists, with { secondModID } as first and { firstModID } as second mod.";
                    }
                }

                CatalogUpdater.AddCompatibility(catalog, firstModID, catalog.ModDictionary[firstModID].Name, 
                    secondModID, catalog.ModDictionary[secondModID].Name, compatibilityStatus, compatibilityNote);
            }
            else
            {
                if (!compatibilityExists)
                {
                    return "Compatibility does not exists.";
                }

                if (!CatalogUpdater.RemoveCompatibility(catalog, firstModID, secondModID, compatibilityStatus))
                {
                    return "Compatibility could not be removed.";
                }
            }

            return "";
        }


        // Set the compatible game version for the catalog
        private static string SetCatalogGameVersion(Catalog catalog, Version newGameVersion)
        {
            if (newGameVersion == Toolkit.UnknownVersion)
            {
                return "Incorrect gameversion.";
            }

            if (!catalog.UpdateGameVersion(newGameVersion))
            {
                return "Could not update game version, it should be higher than the current game version.";
            }

            CatalogUpdater.AddCatalogChangeNote($"Catalog was updated to game version { Toolkit.ConvertGameVersionToString(newGameVersion) }.");

            return "";
        }


        // Set one of the text fields on the catalog
        private static string ChangeCatalogText(Catalog catalog, string action, string text)
        {
            if (action.Contains("note"))
            {
                CatalogUpdater.SetNote(catalog, text);
            }
            else if (action.Contains("header"))
            {
                CatalogUpdater.SetHeaderText(catalog, text);
            }
            else // Footer
            {
                CatalogUpdater.SetFooterText(catalog, text);
            }
                                        
            return "";
        }


        // Add required assets to the catalog list of assets. This will not be mentioned in the change notes and 'already exists' errors will not be logged.
        private static string AddRequiredAssets(Catalog catalog, List<ulong> assetIDs)
        {
            foreach (ulong assetID in assetIDs)
            {
                if (!catalog.IsValidID(assetID, shouldExist: false))
                {
                    return $"Invalid asset ID { assetID }.";
                }

                if (!catalog.RequiredAssets.Contains(assetID))
                {
                    catalog.RequiredAssets.Add(assetID);
                }
            }

            return "";
        }


        // Remove required assets from the catalog list of assets. This will not be noted in the change notes and errors will not be logged.
        private static string RemoveRequiredAssets(Catalog catalog, List<ulong> assetIDs)
        {
            foreach (ulong assetID in assetIDs)
            {
                catalog.RequiredAssets.Remove(assetID);
            }

            return "";
        }
    }
}
