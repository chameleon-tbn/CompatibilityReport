using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using CompatibilityReport.CatalogData;
using CompatibilityReport.Util;

namespace CompatibilityReport.Updater
{
    /// <summary>FileImporter gathers update information from CSV files in de Updater folder, and updates the catalog with this.</summary>
    /// <remarks>See Updater Guide for details.</remarks>
    public static class FileImporter
    {
        /// <summary>Starts the FileImporter. Reads all CSV files and updates the catalog with the found information.</summary>
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

            int errors = 0;

            foreach (string CsvFilename in CsvFilenames)
            {
                errors += ReadCsv(catalog, CsvFilename);
            }

            timer.Stop();

            if (errors == 0)
            {
                Logger.UpdaterLog($"Updater processed { CsvFilenames.Count } CSV files in { Toolkit.TimeString(timer.ElapsedMilliseconds) }.");
                Logger.Log($"Updater processed { CsvFilenames.Count } CSV files.");
            }
            else
            {
                Logger.UpdaterLog($"Updater processed { CsvFilenames.Count } CSV files in { Toolkit.TimeString(timer.ElapsedMilliseconds) }, with { errors } errors.", 
                    Logger.Warning);

                Logger.Log($"Updater processed { CsvFilenames.Count } CSV files and encountered { errors } errors. See separate log for details.", Logger.Warning);
            }
        }


        /// <summary>Reads one CSV file.</summary>
        /// <returns>The number of errors encountered while processing the CSV file.</returns>
        private static int ReadCsv(Catalog catalog, string CsvFileFullPath)
        {
            StringBuilder processedCsvLines = new StringBuilder();
            string filename = Toolkit.GetFileName(CsvFileFullPath);
            int errors = 0;

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
                        processedCsvLines.AppendLine($"# [ERROR] { line }");
                        errors++;
                        Logger.UpdaterLog($"{ errorMessage } Line #{ lineNumber }: { line }", Logger.Error);
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
                if (!Toolkit.MoveFile(CsvFileFullPath, $"{ CsvFileFullPath }.{ (errors == 0 ? "" : "partially_") }processed.txt"))
                {
                    Logger.UpdaterLog($"Could not rename \"{ filename }\". Rename or delete it manually to avoid processing it again.", Logger.Error);
                }
            }

            return errors;
        }


        /// <summary>Reads and processes one line from a CSV file.</summary>
        /// <returns>Error message, or empty string when all was well.</returns>
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

            // Third element - numeric: required/successor/alternative mod ID, recommended mod ID, mod ID for compatibility/group, author ID, asset ID
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
                    CatalogUpdater.SetReviewDate(newDate);
                    return (newDate == default) ? "Invalid date." : "";

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
                    return lineElements.Length < 3 ? "Not enough parameters." : !catalog.IsValidID(numericThird) ? $"Invalid mod ID { numericThird }." : 
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
                    return SetCatalogGameVersion(catalog, newGameVersion: Toolkit.ConvertToVersion(stringSecond));

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


        /// <summary>Adds an unlisted or removed mod to the catalog.</summary>
        /// <returns>Error message, or empty string when all was well.</returns>
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
            CatalogUpdater.UpdateMod(catalog, newMod, authorID: authorID, authorUrl: string.IsNullOrEmpty(authorUrl) ? null : authorUrl, updatedByImporter: true);

            if (newMod.Statuses.Contains(Enums.Status.UnlistedInWorkshop))
            {
                // Download and process the Steam Workshop page for an unlisted mod to get all the details.
                Toolkit.Download(Toolkit.GetWorkshopUrl(newMod.SteamID), ModSettings.TempDownloadFullPath);
                WebCrawler.ReadModPage(catalog, newMod);

                Toolkit.DeleteFile(ModSettings.TempDownloadFullPath);
            }

            return "";
        }


        /// <summary>Removes a 'removed from Workshop' mod from the catalog.</summary>
        /// <returns>Error message, or empty string when all was well.</returns>
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

            catalog.ChangeNotes.AppendRemovedMod($"Removed mod { catalogMod.ToString() }");

            return catalog.RemoveMod(catalogMod) ? "" : "Mod could not be removed.";
        }


        /// <summary>Changes the stability for a mod.</summary>
        /// <returns>Error message, or empty string when all was well.</returns>
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


        /// <summary>Changes a mod property.</summary>
        /// <returns>Error message, or empty string when all was well.</returns>
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

                CatalogUpdater.UpdateMod(catalog, catalogMod, sourceUrl: propertyData, updatedByImporter: true);
            }
            else if (action == "remove_sourceurl")
            {
                if (string.IsNullOrEmpty(catalogMod.SourceUrl))
                {
                    return "No source URL to remove.";
                }

                CatalogUpdater.UpdateMod(catalog, catalogMod, sourceUrl: "", updatedByImporter: true);
            }
            else if (action == "set_gameversion")
            {
                // Convert the itemData string to version and back to string, to make sure we have a consistently formatted game version string.
                Version newGameVersion = Toolkit.ConvertToVersion(propertyData);

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
                    return "Cannot remove compatible game version because it was not manually added.";
                }

                CatalogUpdater.UpdateMod(catalog, catalogMod, compatibleGameVersionString: "", updatedByImporter: true);
            }
            else if (action == "add_requireddlc")
            {
                Enums.Dlc requiredDlc = Toolkit.ConvertToEnum<Enums.Dlc>(propertyData);

                if (requiredDlc == default)
                {
                    return "Invalid DLC.";
                }

                if (catalogMod.RequiredDlcs.Contains(requiredDlc))
                {
                    return "DLC is already required.";
                }

                CatalogUpdater.AddRequiredDlc(catalog, catalogMod, requiredDlc, updatedByImporter: true);
            }
            else if (action == "remove_requireddlc")
            {
                Enums.Dlc requiredDlc = Toolkit.ConvertToEnum<Enums.Dlc>(propertyData);

                if (requiredDlc == default)
                {
                    return "Invalid DLC.";
                }

                if (!catalogMod.RequiredDlcs.Contains(requiredDlc))
                {
                    return "DLC is not required.";
                }

                if (!catalogMod.ExclusionForRequiredDlcs.Contains(requiredDlc))
                {
                    return "Cannot remove required DLC because it was not manually added.";
                }

                CatalogUpdater.RemoveRequiredDlc(catalog, catalogMod, requiredDlc);
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
                CatalogUpdater.UpdateMod(catalog, catalogMod, updatedByImporter: true);
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


        /// <summary>Removes a mod exclusion.</summary>
        /// <remarks>Available exclusion categories: SourceURL, GameVersion, RequiredDLC, RequiredMod and NoDescription.</remarks>
        /// <returns>Error message, or empty string when all was well.</returns>
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

                if (!catalogMod.RemoveExclusion(Toolkit.ConvertToEnum<Enums.Dlc>(dlcString)))
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

                if (!catalogMod.RemoveExclusion(requiredID))
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


        /// <summary>Adds compatibilities between each of the mods in a given list.</summary>
        /// <remarks>Only 'SameFunctionality' can be used with this method.</remarks>
        /// <returns>Error message, or empty string when all was well.</returns>
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


        /// <summary>Adds compatibilities between one mod and a list of others.</summary>
        /// <returns>Error message, or empty string when all was well.</returns>
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
                    return $"{ errorMessage } Some of the compatibilities might have been added, check the change notes.";
                }

                previousSecond = secondSteamID;
            }

            return "";
        }


        /// <summary>Adds or remove a compatibility between two mods.</summary>
        /// <returns>Error message, or empty string when all was well.</returns>
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
            if (string.IsNullOrEmpty(note) && (compatibilityString == "minorissues" || compatibilityString == "requiresspecificsettings"))
            {
                return "A note is mandatory for this compatibility.";
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


        /// <summary>Adds a group to the catalog.</summary>
        /// <returns>Error message, or empty string when all was well.</returns>
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


        /// <summary>Removes a group from the catalog.</summary>
        /// <returns>Error message, or empty string when all was well.</returns>
        private static string RemoveGroup(Catalog catalog, ulong groupID)
        {
            Group catalogGroup = catalog.GetGroup(groupID);

            return catalogGroup == null ? "Invalid group ID." : CatalogUpdater.RemoveGroup(catalog, catalogGroup) ? "" : "Group could not be removed.";
        }


        /// <summary>Adds or removes a group member.</summary>
        /// <returns>Error message, or empty string when all was well.</returns>
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


        /// <summary>Adds a retired author to the catalog.</summary>
        /// <returns>Error message, or empty string when all was well.</returns>
        private static string AddAuthor(Catalog catalog, ulong authorID, string authorUrl, string name)
        {
            if (catalog.GetAuthor(authorID, authorUrl) != null)
            {
                return "Author already exists.";
            }

            CatalogUpdater.AddAuthor(catalog, authorID, authorUrl: (authorID == 0 ? authorUrl : ""), name, retired: true);
            return "";
        }


        /// <summary>Changes an author property.</summary>
        /// <returns>Error message, or empty string when all was well.</returns>
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


        /// <summary>Changes the compatible game version for the catalog.</summary>
        /// <returns>Error message, or empty string when all was well.</returns>
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
            catalog.ChangeNotes.AppendCatalogChange($"Catalog was updated to game version { Toolkit.ConvertGameVersionToString(newGameVersion) }.");

            return "";
        }


        /// <summary>Changes one of the text fields on the catalog.</summary>
        /// <returns>Error message, or empty string when all was well.</returns>
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


        /// <summary>Adds required assets to the catalog list of assets.</summary>
        /// <remarks>This will not be mentioned in the change notes and 'already exists' errors will not be logged.</remarks>
        /// <returns>Error message, or empty string when all was well.</returns>
        private static string AddRequiredAssets(Catalog catalog, List<ulong> assetIDs)
        {
            foreach (ulong assetID in assetIDs)
            {
                if (!catalog.IsValidID(assetID, shouldExist: false))
                {
                    return $"Invalid asset ID { assetID }.";
                }

                catalog.AddAsset(assetID);
                catalog.RemovePotentialAsset(assetID);
            }

            return "";
        }


        /// <summary>Removes required assets from the catalog list of assets.</summary>
        /// <remarks>This will not be noted in the change notes and errors will not be logged.</remarks>
        /// <returns>An empty string.</returns>
        private static string RemoveRequiredAssets(Catalog catalog, List<ulong> assetIDs)
        {
            foreach (ulong assetID in assetIDs)
            {
                catalog.RemoveAsset(assetID);
            }

            return "";
        }
    }
}
