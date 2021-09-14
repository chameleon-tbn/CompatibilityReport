using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using CompatibilityReport.CatalogData;
using CompatibilityReport.Util;

// FileImporter gathers update information from CSV files in de Updater folder, and updates the catalog with this. See Updater Guide for details.

namespace CompatibilityReport.Updater
{
    public static class FileImporter
    {
        // Start the FileImporter. Read all CSV files and update the catalog with found information.
        public static void Start(Catalog catalog)
        {
            Logger.UpdaterLog("Updater started the CSV import.");
            Stopwatch timer = Stopwatch.StartNew();

            CatalogUpdater.SetReviewDate(DateTime.Now);

            List<string> CsvFilenames = Directory.GetFiles(ModSettings.UpdaterPath, "*.csv").ToList();
            CsvFilenames.Sort();

            if (!CsvFilenames.Any())
            {
                Logger.UpdaterLog("No CSV files found.");
                return;
            }

            int errorCounter = 0;

            foreach (string CsvFilename in CsvFilenames)
            {
                if (!ReadCsv(catalog, CsvFilename))
                {
                    errorCounter++;
                }
            }

            timer.Stop();

            if (errorCounter == 0)
            {
                Logger.UpdaterLog($"Updater processed { CsvFilenames.Count } CSV files in { Toolkit.TimeString(timer.ElapsedMilliseconds) }.");
                Logger.Log($"Updater processed { CsvFilenames.Count } CSV files.");
            }
            else
            {
                Logger.UpdaterLog($"Updater processed { CsvFilenames.Count } CSV files in { Toolkit.TimeString(timer.ElapsedMilliseconds) }, " +
                    $"with { errorCounter } errors.", Logger.Warning);

                Logger.Log($"Updater processed { CsvFilenames.Count } CSV files and encountered { errorCounter } errors. See separate log for details.", Logger.Warning);
            }
        }


        // Read one CSV file. Returns false on errors.
        private static bool ReadCsv(Catalog catalog, string CsvFileFullPath)
        {
            StringBuilder processedCsvLines = new StringBuilder();
            string filename = Toolkit.GetFileName(CsvFileFullPath);
            bool withoutErrors = true;

            Logger.UpdaterLog($"Processing \"{ filename }\".");

            processedCsvLines.AppendLine("###################################################");
            processedCsvLines.AppendLine($"#### FILE: { filename }");
            processedCsvLines.AppendLine("###################################################\n");

            using (StreamReader reader = File.OpenText(CsvFileFullPath))
            {
                string line;
                int lineNumber = 0;

                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;

                    string errorMessage = ReadCsvLine(catalog, line);

                    if (string.IsNullOrEmpty(errorMessage))
                    {
                        processedCsvLines.AppendLine(line);
                    }
                    else
                    {
                        processedCsvLines.AppendLine("# [ERROR] " + line);

                        withoutErrors = false;
                        Logger.UpdaterLog(errorMessage + $" Line #{ lineNumber }: { line }", Logger.Error);
                    }
                }
            }

            processedCsvLines.AppendLine("\n");

            Toolkit.SaveToFile(processedCsvLines.ToString(), Path.Combine(ModSettings.WorkPath, ModSettings.TempCsvCombinedFileName), append: true);

            if (ModSettings.DebugMode)
            {
                Logger.UpdaterLog($"\"{ filename }\" not renamed because of debug mode. Rename or delete it manually to avoid processing it again.", Logger.Warning);
            }
            else
            {
                if (!Toolkit.MoveFile(CsvFileFullPath, $"{ CsvFileFullPath }.{ (withoutErrors ? "" : "partially_") }processed.txt"))
                {
                    Logger.UpdaterLog($"Could not rename \"{ filename }\". Rename or delete it manually to avoid processing it again.", Logger.Error);
                }
            }

            return withoutErrors;
        }


        // Read and process one CSV line, returning an error message, if any.
        private static string ReadCsvLine(Catalog catalog, string line)
        {
            if (string.IsNullOrEmpty(line) || line.Trim()[0] == '#')
            {
                return "";
            }

            string[] lineElements = line.Split(',');

            // First element: action
            string action = lineElements[0].Trim().ToLower();

            // Second element - numeric: mod, group, author or asset ID
            //               and string: review date, compatibility status, group name, author custom URL, game version, catalog note, header/footer text
            string stringSecond = lineElements.Length < 2 ? "" : lineElements[1].Trim();
            ulong numericSecond = Toolkit.ConvertToUlong(stringSecond);

            // Third element - numeric: required/successor/alternative mod ID, recommended mod or group ID, mod ID for compatibility/group, author ID, asset ID
            //              and string: unlisted/removed text, source URL, game version, DLC string, stability, mod/compatibility status, generic note,
            //                          exclusion category, author name, author custom URL, last seen date
            string stringThird = lineElements.Length < 3 ? "" : lineElements[2].Trim();
            ulong numericThird = Toolkit.ConvertToUlong(stringThird); ;

            // Fourth element - numeric: author ID, additional mod or asset ID, required mod ID for exclusion
            //               and string: author custom URL, stability note, DLC string, compatibility status
            string stringFourth = lineElements.Length < 4 ? "" : lineElements[3].Trim();
            ulong numericFourth = Toolkit.ConvertToUlong(stringFourth);

            // Fifth and further elements are collected through a string.Join (mod name, compatibility note) and a List (additional mod or asset IDs).

            switch (action)
            {
                case "reviewdate":
                    DateTime newDate = Toolkit.ConvertDate(stringSecond);
                    return newDate == default ? "Invalid date." : CatalogUpdater.SetReviewDate(newDate);

                case "add_mod":
                    // Join the lineFragments for the optional mod name, to allow for commas.
                    string modName = lineElements.Length < 5 ? "" : string.Join(",", lineElements, 4, lineElements.Length - 4).Trim();
                    return lineElements.Length < 3 ? "Not enough parameters." : AddMod(catalog, steamID: numericSecond, status: stringThird.ToLower(), 
                        authorID: numericFourth, authorUrl: (numericFourth == 0 ? stringFourth : ""), modName);

                case "remove_mod":
                    return lineElements.Length < 2 ? "Not enough parameters." : RemoveMod(catalog, steamID: numericSecond); 

                case "set_sourceurl":
                case "set_gameversion":
                case "add_requireddlc":
                case "add_status":
                case "remove_requireddlc":
                case "remove_status":
                    return lineElements.Length < 3 ? "Not enough parameters." : ChangeModProperty(catalog, action, steamID: numericSecond, stringThird);

                case "set_stability":
                    // Join the lineFragments for the note, to allow for commas. Replace "\n" in the CSV text by real newline characters.
                    string note = lineElements.Length < 4 ? "" : string.Join(",", lineElements, 3, lineElements.Length - 3).Trim().Replace("\\n", "\n");
                    return lineElements.Length < 3 ? "Not enough parameters." : ChangeStability(catalog, steamID: numericSecond, stabilityString: stringThird, note);

                case "set_genericnote":
                    // Join the lineFragments for the note, to allow for commas. Replace "\n" in the CSV text by real newline characters.
                    string genericNote = lineElements.Length < 3 ? "" : string.Join(",", lineElements, 2, lineElements.Length - 2).Trim().Replace("\\n", "\n");
                    return lineElements.Length < 3 ? "Not enough parameters." : ChangeModProperty(catalog, action, steamID: numericSecond, genericNote);

                case "update_review":
                case "remove_sourceurl":
                case "remove_gameversion":
                case "remove_genericnote":
                    return lineElements.Length < 2 ? "Not enough parameters." : ChangeModProperty(catalog, action, steamID: numericSecond);

                case "add_requiredmod":
                case "add_successor":
                case "add_alternative":
                case "add_recommendation":
                case "remove_requiredmod":
                case "remove_successor":
                case "remove_alternative":
                case "remove_recommendation":
                    bool allowGroup = action.Contains("recommendation");
                    return lineElements.Length < 3 ? "Not enough parameters." : 
                        !catalog.IsValidID(numericThird, allowGroup) ? $"Invalid mod { (allowGroup ? "or group " : "") }ID { numericThird }." : 
                        ChangeModProperty(catalog, action, steamID: numericSecond, listMember: numericThird);

                case "remove_exclusion":
                    // Correct number of parameters is checked at RemoveExclusion().
                    return RemoveExclusion(catalog, steamID: numericSecond, categoryString: stringThird.ToLower(), dlcString: stringFourth, requiredID: numericFourth);

                case "add_compatibility":
                case "remove_compatibility":
                    // Join the lineFragments for the optional note, to allow for commas. If the note starts with a '#', it's a comment instead of a note.
                    string compatNote = (lineElements.Length < 5 || lineElements[4].Trim()[0] == '#') ? "" : 
                        string.Join(",", lineElements, 4, lineElements.Length - 4).Trim().Replace("\\n", "\n");

                    return lineElements.Length < 4 ? "Not enough parameters." : 
                        AddRemoveCompatibility(catalog, action, firstSteamID: numericSecond, secondSteamID: numericThird, compatibilityString: stringFourth, compatNote);

                case "add_compatibilitiesforone":
                case "add_compatibilitiesforall":
                case "add_group":
                case "add_requiredassets":
                case "remove_requiredassets":
                    if ((action.Contains("compatibilities") && lineElements.Length < 5) || (action.Contains("group") && lineElements.Length < 4) || lineElements.Length < 2)
                    {
                        return "Not enough parameters.";
                    }

                    List<ulong> steamIDs = Toolkit.ConvertToUlong(lineElements.ToList());

                    // Remove the first one to three elements: action, first mod ID, compatibility  /  action, compatibility  /  action, group name  /  action
                    steamIDs.RemoveRange(0, action == "add_compatibilitiesforone" ? 3 : action.Contains("requiredassets") ? 1 : 2);

                    // Remove the last element if it starts with a '#' (comment).
                    if (lineElements.Last().Trim()[0] == '#') 
                    {
                        steamIDs.RemoveAt(steamIDs.Count - 1);

                        // Exit if we don't have enough IDs left in the group: 3 for compatibilitiesforall, 1 for requiredassets, 2 for the others.
                        if (steamIDs.Count < (action == "add_compatibilitiesforall" ? 3 : action.Contains("requiredassets") ? 1 : 2))
                        {
                            return "Not enough parameters.";
                        }
                    }

                    return action == "add_compatibilitiesforone" ? 
                                AddCompatibilitiesForOne(catalog, firstSteamID: numericSecond, compatibilityString: stringThird.ToLower(), steamIDs) :
                           action == "add_compatibilitiesforall" ? AddCompatibilitiesForAll(catalog, compatibilityString: stringSecond.ToLower(), steamIDs) :
                           action == "add_group" ? AddGroup(catalog, groupName: stringSecond, groupMembers: steamIDs) :
                           action == "add_requiredassets" ? AddRequiredAssets(catalog, steamIDs) : RemoveRequiredAssets(catalog, steamIDs);

                case "remove_group":
                    return lineElements.Length < 2 ? "Not enough parameters." : RemoveGroup(catalog, groupID: numericSecond);

                case "add_groupmember":
                case "remove_groupmember":
                    return lineElements.Length < 3 ? "Not enough parameters." : AddRemoveGroupMember(catalog, action, groupID: numericSecond, groupMember: numericThird);

                case "add_author":
                    return lineElements.Length < 3 ? "Not enough parameters." : 
                        AddAuthor(catalog, authorID: numericSecond, authorUrl: (numericSecond == 0 ? stringSecond : ""), name: stringThird);

                case "set_authorid":
                case "set_authorurl":
                case "set_lastseen":
                    return lineElements.Length < 3 ? "Not enough parameters." : ChangeAuthorProperty(catalog, action, authorID: numericSecond,
                        authorUrl: (numericSecond == 0 ? stringSecond : ""), propertyData: stringThird, newAuthorID: numericThird);

                case "set_retired":
                case "remove_authorurl":
                case "remove_retired":
                    return lineElements.Length < 2 ? "Not enough parameters." : ChangeAuthorProperty(catalog, action, authorID: numericSecond,
                        authorUrl: (numericSecond == 0 ? stringSecond : ""));

                case "set_cataloggameversion":
                    return SetCatalogGameVersion(catalog, newGameVersion: Toolkit.ConvertToGameVersion(stringSecond));

                case "set_catalognote":
                case "set_catalogheadertext":
                case "set_catalogfootertext":
                    // Join the lineFragments for the note/text, to allow for commas. Replace "\n" in CSV text by real newline characters.
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


        // Add an unlisted or removed mod.
        private static string AddMod(Catalog catalog, ulong steamID, string status, ulong authorID, string authorUrl, string modName)
        {
            if (!catalog.IsValidID(steamID, shouldExist: false))
            {
                return "Invalid Steam ID or mod already exists in the catalog.";
            }

            if (status != "unlisted" && status != "removed")
            {
                return "Invalid status, must be 'Unlisted' or 'Removed'.";
            }

            Mod newMod = CatalogUpdater.AddMod(catalog, steamID, modName, unlisted: status == "unlisted", removed: status == "removed");
            CatalogUpdater.UpdateMod(catalog, newMod, authorID: authorID, authorUrl: authorUrl, updatedByImporter: true);

            return "";
        }


        // Remove a 'removed from Workshop' mod from the catalog.
        private static string RemoveMod(Catalog catalog, ulong steamID)
        {
            if (!catalog.IsValidID(steamID, allowBuiltin: false))
            {
                return "Invalid Steam ID or mod does not exist in the catalog.";
            }

            Mod catalogMod = catalog.GetMod(steamID);

            if (!catalogMod.Statuses.Contains(Enums.Status.RemovedFromWorkshop))
            {
                return "Mod can't be removed because it is not removed from the Steam Workshop.";
            }

            if (catalog.IsGroupMember(steamID))
            {
                return "Mod can't be removed because it is in a group.";
            }

            if (catalog.Mods.FirstOrDefault(x => x.RequiredMods.Contains(steamID) || x.Successors.Contains(steamID) || 
                x.Alternatives.Contains(steamID) || x.Recommendations.Contains(steamID)) != default)
            {
                return "Mod can't be removed because it is referenced by other mods as required mod, successor, alternative or recommendation.";
            }

            catalog.ChangeNotes.RemovedMods.AppendLine($"Mod removed: { catalogMod.ToString() }");

            return catalog.RemoveMod(catalogMod) ? "" : "Mod could not be removed.";
        }


        // Change mod stability.
        private static string ChangeStability(Catalog catalog, ulong steamID, string stabilityString, string note)
        {
            Mod catalogMod = catalog.GetMod(steamID);

            if (catalogMod == null)
            {
                return $"Invalid Steam ID { steamID }.";
            }

            Enums.Stability stability = Toolkit.ConvertToEnum<Enums.Stability>(stabilityString);

            if (stability == default)
            {
                return "Invalid stability.";
            }

            if (catalogMod.Stability == stability && catalogMod.StabilityNote == note)
            {
                return "Mod already has this stability with the same note.";
            }
            if (stability == Enums.Stability.IncompatibleAccordingToWorkshop && !catalogMod.Statuses.Contains(Enums.Status.RemovedFromWorkshop))
            {
                return "The Incompatible stability can only be set on a mod that is removed from the Steam Workshop.";
            }
            if (catalogMod.Stability == Enums.Stability.IncompatibleAccordingToWorkshop && !catalogMod.Statuses.Contains(Enums.Status.RemovedFromWorkshop))
            {
                return "Mod has the Incompatible stability and that can only be changed for a mod that is removed from the Steam Workshop.";
            }

            CatalogUpdater.UpdateMod(catalog, catalogMod, stability: stability, stabilityNote: note, updatedByImporter: true);
            return "";
        }


        // Change a mod property.
        private static string ChangeModProperty(Catalog catalog, string action, ulong steamID, string propertyData = "", ulong listMember = 0)
        {
            Mod catalogMod = catalog.GetMod(steamID);

            if (catalogMod == null)
            {
                return $"Invalid Steam ID { steamID }.";
            }

            if (action == "set_sourceurl")
            {
                if (!propertyData.StartsWith("http://") && !propertyData.StartsWith("https://"))
                {
                    return "Invalid URL.";
                }

                CatalogUpdater.UpdateMod(catalog, catalogMod, sourceURL: propertyData, updatedByImporter: true);
            }
            else if (action == "remove_sourceurl")
            {
                if (string.IsNullOrEmpty(catalogMod.SourceUrl))
                {
                    return "No source URL to remove.";
                }

                CatalogUpdater.UpdateMod(catalog, catalogMod, sourceURL: "", updatedByImporter: true);
            }
            else if (action == "set_gameversion")
            {
                // Convert the itemData string to gameversion and back to string, to make sure we have a consistently formatted gameversion string.
                Version newGameVersion = Toolkit.ConvertToGameVersion(propertyData);

                if (newGameVersion == Toolkit.UnknownVersion())
                {
                    return "Invalid game version.";
                }

                CatalogUpdater.UpdateMod(catalog, catalogMod, compatibleGameVersionString: Toolkit.ConvertGameVersionToString(newGameVersion), updatedByImporter: true);
            }
            else if (action == "remove_gameversion")
            {
                if (!catalogMod.ExclusionForGameVersion)
                {
                    return "Cannot remove compatible gameversion because it was not manually added.";
                }

                CatalogUpdater.UpdateMod(catalog, catalogMod, compatibleGameVersionString: "", updatedByImporter: true);
            }
            else if (action == "add_requireddlc")
            {
                Enums.Dlc requiredDLC = Toolkit.ConvertToEnum<Enums.Dlc>(propertyData);

                if (requiredDLC == default)
                {
                    return "Invalid DLC.";
                }

                if (catalogMod.RequiredDlcs.Contains(requiredDLC))
                {
                    return "DLC is already required.";
                }

                CatalogUpdater.AddRequiredDLC(catalog, catalogMod, requiredDLC, updatedByImporter: true);
            }
            else if (action == "remove_requireddlc")
            {
                Enums.Dlc requiredDLC = Toolkit.ConvertToEnum<Enums.Dlc>(propertyData);

                if (requiredDLC == default)
                {
                    return "Invalid DLC.";
                }

                if (!catalogMod.RequiredDlcs.Contains(requiredDLC))
                {
                    return "DLC is not required.";
                }

                if (!catalogMod.ExclusionForRequiredDlc.Contains(requiredDLC))
                {
                    return "Cannot remove required DLC because it was not manually added.";
                }

                CatalogUpdater.RemoveRequiredDLC(catalog, catalogMod, requiredDLC);
            }
            else if (action == "add_status")
            {
                Enums.Status status = Toolkit.ConvertToEnum<Enums.Status>(propertyData);

                if (status == default)
                {
                    return "Invalid status.";
                }

                if (catalogMod.Statuses.Contains(status))
                {
                    return "Mod already has this status.";
                }

                if (status == Enums.Status.UnlistedInWorkshop || status == Enums.Status.RemovedFromWorkshop)
                {
                    return "This status cannot be manually added.";
                }
                if ((status == Enums.Status.NoCommentSection || status == Enums.Status.NoDescription) && 
                    catalogMod.Statuses.Contains(Enums.Status.RemovedFromWorkshop))
                {
                    return "Status cannot be combined with existing 'RemovedFromWorkshop' status.";
                }
                if ((status == Enums.Status.Abandoned || status == Enums.Status.Deprecated) && catalogMod.Statuses.Contains(Enums.Status.NoLongerNeeded))
                {
                    return "Status cannot be combined with existing 'NoLongerNeeded' status.";
                }
                if (status == Enums.Status.Abandoned && catalogMod.Statuses.Contains(Enums.Status.Deprecated))
                {
                    return "Status cannot be combined with existing 'Deprecated' status.";
                }

                CatalogUpdater.AddStatus(catalog, catalogMod, status, updatedByImporter: true);
            }
            else if (action == "remove_status")
            {
                Enums.Status status = Toolkit.ConvertToEnum<Enums.Status>(propertyData);

                if (status == default)
                {
                    return "Invalid status.";
                }

                if (status == Enums.Status.UnlistedInWorkshop || status == Enums.Status.RemovedFromWorkshop)
                {
                    return "This status cannot be manually removed.";
                }

                if (!CatalogUpdater.RemoveStatus(catalog, catalogMod, status, updatedByImporter: true))
                {
                    return "Status not found for this mod.";
                }
            }
            else if (action == "set_genericnote")
            {
                if (!string.IsNullOrEmpty(catalogMod.GenericNote) && catalogMod.GenericNote == propertyData)
                {
                    return "Note already added.";
                }

                CatalogUpdater.UpdateMod(catalog, catalogMod, genericNote: propertyData, updatedByImporter: true);
            }
            else if (action == "remove_genericnote")
            {
                if (string.IsNullOrEmpty(catalogMod.GenericNote))
                {
                    return "Note already empty.";
                }

                CatalogUpdater.UpdateMod(catalog, catalogMod, genericNote: "", updatedByImporter: true);
            }
            else if (action == "update_review")
            {
                CatalogUpdater.UpdateMod(catalog, catalogMod, alwaysUpdateReviewDate: true, updatedByImporter: true);
            }
            else if (action == "add_requiredmod")
            {
                if (catalogMod.RequiredMods.Contains(listMember))
                {
                    return "Mod is already required.";
                }

                CatalogUpdater.AddRequiredMod(catalog, catalogMod, listMember, updatedByImporter: true);
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

                CatalogUpdater.AddSuccessor(catalog, catalogMod, listMember);
            }
            else if (action == "remove_successor")
            {
                if (!catalogMod.Successors.Contains(listMember))
                {
                    return "Successor not found.";
                }

                CatalogUpdater.RemoveSuccessor(catalog, catalogMod, listMember);
            }
            else if (action == "add_alternative")
            {
                if (catalogMod.Alternatives.Contains(listMember))
                {
                    return "Already an alternative mod.";
                }

                CatalogUpdater.AddAlternative(catalog, catalogMod, listMember);
            }
            else if (action == "remove_alternative")
            {
                if (!catalogMod.Alternatives.Contains(listMember))
                {
                    return "Alternative mod not found.";
                }

                CatalogUpdater.RemoveAlternative(catalog, catalogMod, listMember);
            }
            else if (action == "add_recommendation")
            {
                if (catalogMod.Recommendations.Contains(listMember))
                {
                    return "Already an recommended mod.";
                }

                CatalogUpdater.AddRecommendation(catalog, catalogMod, listMember);
            }
            else if (action == "remove_recommendation")
            {
                if (!catalogMod.Recommendations.Contains(listMember))
                {
                    return "Recommended mod not found.";
                }

                CatalogUpdater.RemoveRecommendation(catalog, catalogMod, listMember);
            }
            else
            {
                // Throw an error for when an extra action is later added, but not implemented here yet.
                return "Action not implemented.";
            }

            return "";
        }


        // Remove an exclusion for SourceURL, GameVersion, RequiredDLC, RequiredMod or NoDescription.
        private static string RemoveExclusion(Catalog catalog, ulong steamID, string categoryString, string dlcString, ulong requiredID)
        {
            Mod catalogMod = catalog.GetMod(steamID);

            if (catalogMod == null)
            {
                return $"Invalid Steam ID { steamID }.";
            }

            if (categoryString == "sourceurl")
            {
                catalogMod.UpdateExclusions(exclusionForSourceUrl: false);
            }
            else if (categoryString == "gameversion")
            {
                catalogMod.UpdateExclusions(exclusionForGameVersion: false);
            }
            else if (categoryString == "nodescription")
            {
                catalogMod.UpdateExclusions(exclusionForNoDescription: false);
            }
            else if (categoryString == "requireddlc")
            {
                if (string.IsNullOrEmpty(dlcString))
                {
                    return "Not enough parameters.";
                }

                if (!catalogMod.ExclusionForRequiredDlc.Remove(Toolkit.ConvertToEnum<Enums.Dlc>(dlcString)))
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


        // Add compatibilities between each of the mods in a list. Only 'SameFunctionality' can be used with this.
        private static string AddCompatibilitiesForAll(Catalog catalog, string compatibilityString, List<ulong> steamIDs)
        {
            if (compatibilityString != "samefunctionality")
            {
                return "Only the 'SameFunctionality' status can be used with 'Add_CompatibilitiesForAll'.";
            }

            // Sort the steamIDs so we're able to detect duplicate Steam IDs.
            steamIDs.Sort();
            int numberOfSteamIDs = steamIDs.Count;

            for (var i = 1; i < numberOfSteamIDs; i++)
            {
                // Set the first element as first mod, and remove it from the list.
                ulong firstSteamID = steamIDs[0];
                steamIDs.RemoveAt(0);

                // Add compatibilities between this mod and each remaining mod from the list.
                string errorMessage = AddCompatibilitiesForOne(catalog, firstSteamID, compatibilityString, steamIDs);

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    // Stop if these compatibilities could not be added, without processing more compatibilities.
                    return errorMessage;
                }
            }

            return "";
        }


        // Add compatibilities between one mod and a list of others.
        private static string AddCompatibilitiesForOne(Catalog catalog, ulong firstSteamID, string compatibilityString, List<ulong> steamIDs)
        {
            if (compatibilityString == "minorissues" || compatibilityString == "requiresspecificsettings")
            {
                return "This compatibility status needs a note and cannot be used in an action with multiple compatibilities.";
            }

            // Sort the steamIDs so we're able to detect duplicate Steam IDs.
            steamIDs.Sort();
            ulong previousSecond = 1;

            // Add compatibilities between the first mod and each mod from the list.
            foreach (ulong secondSteamID in steamIDs)
            {
                if (secondSteamID == previousSecond)
                {
                    return $"Duplicate Steam ID { secondSteamID }.";
                }

                string errorMessage = AddRemoveCompatibility(catalog, "add_compatibility", firstSteamID, secondSteamID, compatibilityString, note: "");

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    // Stop if this compatibility could not be added, without processing more compatibilities.
                    return errorMessage + " Some of the compatibilities might have been added, check the change notes.";
                }

                previousSecond = secondSteamID;
            }

            return "";
        }


        // Add or remove a compatibility between two mods.
        private static string AddRemoveCompatibility(Catalog catalog, string action, ulong firstSteamID, ulong secondSteamID, string compatibilityString, string note)
        {
            if (firstSteamID == secondSteamID)
            {
                return $"Duplicate Steam ID { firstSteamID }.";
            }
            if (!catalog.IsValidID(firstSteamID))
            {
                return $"Invalid Steam ID { firstSteamID }.";
            }
            if (!catalog.IsValidID(secondSteamID))
            {
                return $"Invalid Steam ID { secondSteamID }.";
            }

            Enums.CompatibilityStatus compatibilityStatus = Toolkit.ConvertToEnum<Enums.CompatibilityStatus>(compatibilityString);

            if (compatibilityStatus == default)
            {
                return "Invalid compatibility status.";
            }

            Compatibility existingCompatibility = catalog.Compatibilities.Find(x => x.FirstSteamID == firstSteamID && x.SecondSteamID == secondSteamID &&
                x.Status == compatibilityStatus);

            if (action == "add_compatibility")
            {
                if (existingCompatibility != null)
                {
                    return "Compatibility already exists.";
                }
                if (catalog.Compatibilities.Find(x => x.FirstSteamID == secondSteamID && x.SecondSteamID == firstSteamID && x.Status == compatibilityStatus) != default)
                {
                    return $"Compatibility already exists, with { secondSteamID } as first and { firstSteamID } as second mod.";
                }

                CatalogUpdater.AddCompatibility(catalog, firstSteamID, secondSteamID, compatibilityStatus, note);
            }
            else
            {
                if (existingCompatibility == null)
                {
                    return "Compatibility does not exists.";
                }

                if (!CatalogUpdater.RemoveCompatibility(catalog, existingCompatibility))
                {
                    return "Compatibility could not be removed.";
                }
            }

            return "";
        }


        // Add a group.
        private static string AddGroup(Catalog catalog, string groupName, List<ulong> groupMembers)
        {
            if (catalog.Groups.Find(x => x.Name == groupName) != default)
            {
                return "A group with that name already exists.";
            }

            foreach (ulong groupMember in groupMembers)
            {
                if (!catalog.IsValidID(groupMember))
                {
                    return $"Invalid Steam ID { groupMember }.";
                }
                if (catalog.IsGroupMember(groupMember))
                {
                    return $"Mod { groupMember } is already in a group and a mod can only be in one.";
                }
            }

            CatalogUpdater.AddGroup(catalog, groupName, groupMembers);
            return "";
        }


        // Remove a group.
        private static string RemoveGroup(Catalog catalog, ulong groupID)
        {
            Group catalogGroup = catalog.GetGroup(groupID);

            return catalogGroup == null ? "Invalid group ID." : CatalogUpdater.RemoveGroup(catalog, catalogGroup) ? "" : "Group could not be removed.";
        }


        // Add or remove a group member.
        private static string AddRemoveGroupMember(Catalog catalog, string action, ulong groupID, ulong groupMember)
        {
            Group catalogGroup = catalog.GetGroup(groupID);

            if (catalogGroup == null)
            {
                return "Invalid group ID.";
            }

            if (!catalog.IsValidID(groupMember))
            {
                return $"Invalid Steam ID { groupMember }.";
            }

            if (action == "add_groupmember")
            {
                if (catalog.IsGroupMember(groupMember))
                {
                    return $"Mod { groupMember } is already in a group and a mod can only be in one.";
                }

                CatalogUpdater.AddGroupMember(catalog, catalogGroup, groupMember);
                return "";
            }
            else
            {
                if (!catalogGroup.GroupMembers.Contains(groupMember))
                {
                    return $"Mod { groupMember } is not a member of this group.";
                }

                return CatalogUpdater.RemoveGroupMember(catalog, catalogGroup, groupMember) ? "" : $"Could not remove { groupMember } from group.";
            }
        }


        // Add a retired author.
        private static string AddAuthor(Catalog catalog, ulong authorID, string authorUrl, string name)
        {
            if (catalog.GetAuthor(authorID, authorUrl) != null)
            {
                return "Author already exists.";
            }

            CatalogUpdater.AddAuthor(catalog, authorID, authorUrl: (authorID == 0 ? authorUrl : ""), name, retired: true);
            return "";
        }


        // Change an author property.
        private static string ChangeAuthorProperty(Catalog catalog, string action, ulong authorID, string authorUrl, string propertyData = "", ulong newAuthorID = 0)
        {
            Author catalogAuthor = catalog.GetAuthor(authorID, authorUrl);

            if (catalogAuthor == null)
            {
                return "Author not found.";
            }

            if (action == "set_authorid")
            {
                if (catalogAuthor.SteamID != 0)
                {
                    return "Author already has an author ID.";
                }
                if (newAuthorID == 0)
                {
                    return "Invalid Author ID.";
                }

                CatalogUpdater.UpdateAuthor(catalog, catalogAuthor, newAuthorID);
            }
            else if (action == "set_authorurl")
            {
                if (string.IsNullOrEmpty(propertyData))
                {
                    return "Invalid custom URL.";
                }
                if (catalogAuthor.CustomUrl == propertyData)
                {
                    return "This custom URL is already active.";
                }

                CatalogUpdater.UpdateAuthor(catalog, catalogAuthor, authorUrl: propertyData);
            }
            else if (action == "remove_authorurl")
            {
                if (string.IsNullOrEmpty(catalogAuthor.CustomUrl))
                {
                    return "No custom URL active.";
                }

                CatalogUpdater.UpdateAuthor(catalog, catalogAuthor, authorUrl: "");
            }
            else if (action == "set_lastseen")
            {
                DateTime lastSeen = Toolkit.ConvertDate(propertyData);

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
                    return "Author already has a more recent last seen date.";
                }

                CatalogUpdater.UpdateAuthor(catalog, catalogAuthor, lastSeen: lastSeen);
            }
            else if (action == "set_retired")
            {
                if (catalogAuthor.Retired)
                {
                    return "Author is already retired.";
                }

                CatalogUpdater.UpdateAuthor(catalog, catalogAuthor, retired: true);
            }
            else if (action == "remove_retired")
            {
                if (!catalogAuthor.Retired)
                {
                    return "Author is not retired.";
                }
                if (!catalogAuthor.ExclusionForRetired)
                {
                    return "Current author retirement is automatic and can only be removed by adding a recent 'last seen' date.";
                }

                CatalogUpdater.UpdateAuthor(catalog, catalogAuthor, retired: false);
            }
            else
            {
                return "Action not implemented.";
            }

            return "";
        }


        // Set the compatible game version for the catalog.
        private static string SetCatalogGameVersion(Catalog catalog, Version newGameVersion)
        {
            if (newGameVersion == Toolkit.UnknownVersion())
            {
                return "Incorrect gameversion.";
            }
            if (newGameVersion <= catalog.GameVersion())
            {
                return "New game version should be higher than the current game version.";
            }

            catalog.Update(newGameVersion);
            catalog.ChangeNotes.CatalogChanges.AppendLine($"Catalog was updated to game version { Toolkit.ConvertGameVersionToString(newGameVersion) }.");

            return "";
        }


        // Set one of the text fields on the catalog.
        private static string ChangeCatalogText(Catalog catalog, string action, string text)
        {
            if (action.Contains("note"))
            {
                if (text == catalog.Note)
                {
                    return $"Catalog already has { (string.IsNullOrEmpty(text) ? "no" : "this") } note.";
                }

                CatalogUpdater.SetNote(catalog, text);
            }
            else if (action.Contains("header"))
            {
                if (text == catalog.ReportHeaderText)
                {
                    return $"Catalog already has { (string.IsNullOrEmpty(text) ? "no" : "this") } header text.";
                }

                CatalogUpdater.SetHeaderText(catalog, text);
            }
            else
            {
                if (text == catalog.ReportFooterText)
                {
                    return $"Catalog already has { (string.IsNullOrEmpty(text) ? "no" : "this") } footer text.";
                }

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
                    catalog.RemoveUnknownAsset(assetID);
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
