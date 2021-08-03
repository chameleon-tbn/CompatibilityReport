using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CompatibilityReport.DataTypes;
using CompatibilityReport.Util;


namespace CompatibilityReport.Updater
{
    internal static class CatalogUpdater
    {
        // Dictionaries to collect info from the Steam Workshop (AutoUpdater) and the CSV files (ManualUpdater)
        internal static Dictionary<ulong, Mod> CollectedModInfo { get; private set; } = new Dictionary<ulong, Mod>();
        internal static Dictionary<ulong, Author> CollectedAuthorIDs { get; private set; } = new Dictionary<ulong, Author>();
        internal static Dictionary<string, Author> CollectedAuthorURLs { get; private set; } = new Dictionary<string, Author>();
        internal static Dictionary<ulong, Group> CollectedGroupInfo { get; private set; } = new Dictionary<ulong, Group>();
        internal static List<Compatibility> CollectedCompatibilities { get; private set; } = new List<Compatibility>();
        internal static List<ulong> CollectedRemovals { get; private set; } = new List<ulong>();

        // List of author custom URLs to remove from the catalog; these are collected first and removed later to avoid 'author not found' issues
        private static readonly List<string> AuthorURLsToRemove = new List<string>();

        // Note for the new catalog
        private static string CatalogNote;

        // Date and time of this update
        private static DateTime UpdateDate;

        // Change notes, separate parts and combined
        private static StringBuilder ChangeNotesNewMods;
        private static StringBuilder ChangeNotesUpdatedMods;
        private static StringBuilder ChangeNotesRemovedMods;
        private static StringBuilder ChangeNotesNewAuthors;
        private static StringBuilder ChangeNotesUpdatedAuthors;
        private static StringBuilder ChangeNotesRemovedAuthors;
        private static string ChangeNotes;

        // Calling process
        private static bool AutoUpdater;


        // Initialize all variables
        internal static void Init()
        {
            CollectedModInfo.Clear();
            CollectedAuthorIDs.Clear();
            CollectedAuthorURLs.Clear();
            CollectedGroupInfo.Clear();
            CollectedCompatibilities.Clear();
            CollectedRemovals.Clear();

            AuthorURLsToRemove.Clear();

            // Setting the note to null instead of empty to avoid accidentally clearing the note when this field is never set
            CatalogNote = null;

            ChangeNotesNewMods = new StringBuilder();
            ChangeNotesUpdatedMods = new StringBuilder();
            ChangeNotesRemovedMods = new StringBuilder();
            ChangeNotesNewAuthors = new StringBuilder();
            ChangeNotesUpdatedAuthors = new StringBuilder();
            ChangeNotesRemovedAuthors = new StringBuilder();
            ChangeNotes = "";

            UpdateDate = DateTime.Now;
        }


        // Update the active catalog with the found information; returns the partial path of the new catalog
        // [Todo 0.3] Add exclusion checks in AutoUpdater and ManualUpdater
        internal static string Start(bool autoUpdater)
        {
            // Exit if the updater is not enabled in settings
            if (!ModSettings.UpdaterEnabled)
            {
                return "";
            }

            AutoUpdater = autoUpdater;

            // Add or update all found mods
            UpdateAndAddMods();

            // Add or update all found authors
            UpdateAndAddAuthors();

            if (AutoUpdater)
            {
                // Retire authors that have no mods on the Steam Workshop anymore; only when running AutoUpdater, otherwise we won't have gathered all mods from Workshop
                RetireFormerAuthors();
            }
            else
            {
                // Add or update all collected groups; only used in ManualUpdater
                UpdateAndAddGroups();
                
                // Add or update all collected compatibilities; only used in ManualUpdater
                UpdateAndAddCompatibilities();

                // Remove all items from the collected removal list; only used in ManualUpdater
                Removals();
            }

            // Only continue with catalog update if we found any changes to update the catalog
            if (ChangeNotesNewMods.Length + ChangeNotesUpdatedMods.Length + ChangeNotesRemovedMods.Length + 
                ChangeNotesNewAuthors.Length + ChangeNotesUpdatedAuthors.Length + ChangeNotesRemovedAuthors.Length == 0)
            {
                Logger.UpdaterLog("No changes or new additions found. No new catalog created.");

                // Empty the dictionaries and change notes to free memory
                Init();

                // Exit
                return "";
            }

            // Increase the catalog version and update date
            ActiveCatalog.Instance.NewVersion(UpdateDate);

            // Set a new catalog note; not changed if null
            ActiveCatalog.Instance.Update(note: CatalogNote);

            // Combine the change notes
            ChangeNotes = $"Change Notes for Catalog { ActiveCatalog.Instance.VersionString() }\n" +
                "-------------------------------\n" +
                $"{ UpdateDate:D}, { UpdateDate:t}\n" +
                "\n" +
                (ChangeNotesNewMods.Length + ChangeNotesNewAuthors.Length == 0 ? "" : 
                    "*** ADDED: ***\n" +
                    ChangeNotesNewMods.ToString() +
                    ChangeNotesNewAuthors.ToString() +
                    "\n") +
                (ChangeNotesUpdatedMods.Length + ChangeNotesUpdatedAuthors.Length == 0 ? "" : 
                    "*** UPDATED: ***\n" +
                    ChangeNotesUpdatedMods.ToString() +
                    ChangeNotesUpdatedAuthors.ToString() +
                    "\n") +
                (ChangeNotesRemovedMods.Length + ChangeNotesRemovedAuthors.Length == 0 ? "" : 
                    "*** REMOVED: ***\n" +
                    ChangeNotesRemovedMods.ToString() +
                    ChangeNotesRemovedAuthors.ToString() +
                    "\n") +
                    "\n" +
                "*** The change notes were automatically created by the " + (AutoUpdater ? "Auto" : "Manual") + "Updater process ***";

            // The filename for the new catalog and related files ('CompatibilityReportCatalog_v1.0001')
            string partialPath = Path.Combine(ModSettings.updaterPath, $"{ ModSettings.internalName }Catalog_v{ ActiveCatalog.Instance.VersionString() }");

            // Save the new catalog
            if (ActiveCatalog.Instance.Save(partialPath + ".xml"))
            {
                // Save change notes, in the same folder as the new catalog
                Toolkit.SaveToFile(ChangeNotes.ToString(), partialPath + "_ChangeNotes.txt");

                Logger.UpdaterLog($"New catalog { ActiveCatalog.Instance.VersionString() } created and change notes saved.");

                // Copy the updater logfile to the same folder as the new catalog
                Toolkit.CopyFile(ModSettings.updaterLogfileFullPath, partialPath + "_Updater.log");
            }
            else
            {
                // Clear partialPath to indicate the catalog wasn't saved
                partialPath = "";

                Logger.UpdaterLog($"Could not save the new catalog. All updates were lost.", Logger.error);
            }

            // Close and reopen the active catalog, because we made changes to it
            Logger.Log("Closing and reopening the active catalog.");

            ActiveCatalog.Close();

            ActiveCatalog.Init();

            // Empty the dictionaries and change notes to free memory
            Init();

            return partialPath;
        }


        // Update or add all found mods
        internal static void UpdateAndAddMods()
        {
            foreach (ulong steamID in CollectedModInfo.Keys)
            {
                // Get the found mod
                Mod collectedMod = CollectedModInfo[steamID];

                // Clean assets from the required mods list
                List<ulong> removalList = new List<ulong>();

                foreach (ulong requiredID in collectedMod.RequiredMods)
                {
                    // Remove the required ID if it's not in the catalog and we didn't find it on the Workshop; ignore builtin required mods
                    if (!CollectedModInfo.ContainsKey(requiredID) && !ActiveCatalog.Instance.ModDictionary.ContainsKey(requiredID) 
                        && (requiredID > ModSettings.highestFakeID))
                    {
                        // We can't remove it here directly, because the RequiredMods list is used in the foreach loop, so we just collect here and (re)move below
                        removalList.Add(requiredID);

                        // Don't log if it's a known asset to ignore
                        if (!ModSettings.requiredIDsToIgnore.Contains(requiredID))
                        {
                            Logger.UpdaterLog($"Required item [Steam ID { requiredID,10 }] not found, probably an asset. For { collectedMod.ToString(cutOff: false) }.");
                        }
                    }
                }

                // Now really remove the asset IDs and add them to the asset list
                foreach (ulong requiredID in removalList)
                {
                    collectedMod.RequiredMods.Remove(requiredID);

                    collectedMod.RequiredAssets.Add(requiredID);
                }

                // Clean up compatible gameversion
                if (collectedMod.CompatibleGameVersionString == null)
                {
                    collectedMod.Update(compatibleGameVersionString: GameVersion.Formatted(GameVersion.Unknown));
                }

                // Clean up statuses
                if (collectedMod.Statuses.Any())
                {
                    if (collectedMod.Statuses.Contains(Enums.ModStatus.UnlistedInWorkshop) && collectedMod.Statuses.Contains(Enums.ModStatus.RemovedFromWorkshop))
                    {
                        collectedMod.Statuses.Remove(Enums.ModStatus.UnlistedInWorkshop);
                    }
                }

                // Add or update the mod in the catalog
                if (!ActiveCatalog.Instance.ModDictionary.ContainsKey(steamID))
                {
                    // New mod; add to the catalog
                    AddMod(steamID);
                }
                else
                {
                    // Known mod; update all info
                    UpdateMod(steamID);
                }
            }
        }


        // Update or add all collected groups
        internal static void UpdateAndAddGroups()
        {
            foreach (Group group in CollectedGroupInfo.Values)
            {
                if (!ActiveCatalog.Instance.GroupDictionary.ContainsKey(group.GroupID))
                {
                    // Add new group; group ID will be automatically assigned
                    ActiveCatalog.Instance.AddGroup(group.Name, group.SteamIDs);

                    // [Todo 0.3] replace required mod by new group in catalog

                    ChangeNotesNewMods.AppendLine($"New group { group.ToString() }");
                }
                else
                {
                    // Update existing group [Todo 0.3]
                    //UpdateGroup(groupID);
                }
            }
        }


        // Update or add all found authors
        internal static void UpdateAndAddAuthors()
        {
            // Add or update all found authors, by author ID
            foreach (ulong authorID in CollectedAuthorIDs.Keys)
            {
                Author collectedAuthor = CollectedAuthorIDs[authorID];

                if (!ActiveCatalog.Instance.AuthorIDDictionary.ContainsKey(authorID))
                {
                    // New author; add to the catalog
                    AddAuthor(collectedAuthor);
                }
                else
                {
                    // Known author
                    Author catalogAuthor = ActiveCatalog.Instance.AuthorIDDictionary[authorID];

                    // Update all info
                    UpdateAuthor(catalogAuthor, collectedAuthor);
                }
            }

            // Add or update all found authors, by author custom URL
            foreach (string authorURL in CollectedAuthorURLs.Keys)
            {
                Author collectedAuthor = CollectedAuthorURLs[authorURL];

                if (!ActiveCatalog.Instance.AuthorURLDictionary.ContainsKey(authorURL))
                {
                    // New author; add to the catalog
                    AddAuthor(collectedAuthor);
                }
                else
                {
                    // Known author
                    Author catalogAuthor = ActiveCatalog.Instance.AuthorURLDictionary[authorURL];

                    // Update all info
                    UpdateAuthor(catalogAuthor, collectedAuthor);
                }
            }

            // Remove all old author custom URLs that no longer exist
            foreach (string oldURL in AuthorURLsToRemove)
            {
                ActiveCatalog.Instance.AuthorURLDictionary.Remove(oldURL);
            }
        }


        // Set retired status for authors in the catalog that we didn't find any mods for (anymore) in the Steam Workshop
        internal static void RetireFormerAuthors()
        {
            // Find authors by author ID
            foreach (ulong authorID in ActiveCatalog.Instance.AuthorIDDictionary.Keys)
            {
                Author catalogAuthor = ActiveCatalog.Instance.AuthorIDDictionary[authorID];

                // Ignore authors that we found by either ID or URL, and authors that already have the 'retired' status
                if (!CollectedAuthorIDs.ContainsKey(authorID) && !CollectedAuthorURLs.ContainsKey(catalogAuthor.CustomURL) && !catalogAuthor.Retired)
                {
                    catalogAuthor.Update(retired: true,
                        changeNotes: $"{ (AutoUpdater ? "Auto" : "") }Updated as retired on { Toolkit.DateString(UpdateDate) }.");

                    ChangeNotesRemovedAuthors.AppendLine($"Author no longer has mods on the workshop: { ActiveCatalog.Instance.AuthorIDDictionary[authorID].ToString() }");
                }
            }

            // Find authors by custom URL
            foreach (string authorURL in ActiveCatalog.Instance.AuthorURLDictionary.Keys)
            {
                Author catalogAuthor = ActiveCatalog.Instance.AuthorURLDictionary[authorURL];

                // Ignore authors that we found by either ID or URL, and authors that already have the 'retired' status
                if (!CollectedAuthorURLs.ContainsKey(authorURL) && !CollectedAuthorIDs.ContainsKey(catalogAuthor.ProfileID) && !catalogAuthor.Retired)
                {
                    catalogAuthor.Update(retired: true,
                        changeNotes: $"{ (AutoUpdater ? "Auto" : "") }Updated as retired on { Toolkit.DateString(UpdateDate) }.");

                    ChangeNotesRemovedAuthors.AppendLine($"Author no longer has mods on the workshop: { ActiveCatalog.Instance.AuthorURLDictionary[authorURL].ToString() }");
                }
            }
        }


        // Update or add all collected groups
        internal static void UpdateAndAddCompatibilities()
        {
            // foreach (Compatibility compatibility in CollectedCompatibilities)
            {
                // [Todo 0.3] Add new compatibility or add status to existing one
            }
        }


        // Remove all items from the collected removal list
        private static void Removals()
        {
            foreach (ulong id in CollectedRemovals)
            {
                // Mod
                if (id > ModSettings.highestFakeID)
                {
                    // Get the mod
                    Mod mod = ActiveCatalog.Instance.ModDictionary[id];

                    // This has already been checked for existence in other lists; can remove immediately
                    ActiveCatalog.Instance.ModDictionary.Remove(id);

                    ActiveCatalog.Instance.Mods.Remove(mod);

                    ChangeNotesRemovedMods.AppendLine($"Mod \"{ mod.ToString(cutOff: false) }\" was removed from the catalog.");

                    // Remove any exclusions for this mod
                    List<Exclusion> ExclusionsToRemove = ActiveCatalog.Instance.Exclusions.FindAll(x => x.SteamID == id);

                    foreach (Exclusion exclusion in ExclusionsToRemove)
                    {
                        ActiveCatalog.Instance.Exclusions.Remove(exclusion);
                    }
                }

                // Group
                else if (id >= ModSettings.lowestGroupID && id <= ModSettings.highestGroupID)
                {
                    // Get the group
                    Group group = ActiveCatalog.Instance.GroupDictionary[id];

                    // This has already been checked for existence and has been replaced in all required mods lists; can remove immediately
                    ActiveCatalog.Instance.GroupDictionary.Remove(id);

                    ActiveCatalog.Instance.Groups.Remove(group);

                    // Add to the 'authors' change notes, so it will go to the end of the changes notes
                    ChangeNotesRemovedAuthors.AppendLine($"Group \"{ group.Name }\" was removed from the catalog.");
                }

                // Catalog compatible game version
                else if (id == 1)
                {
                    // Update the change notes; the update itself was already done by the ManualUpdater
                    ChangeNotesUpdatedAuthors.AppendLine("Catalog was updated to a new game version: " + ActiveCatalog.Instance.CompatibleGameVersionString);
                }
            }
        }


        // Add a new mod to the active catalog
        private static void AddMod(ulong steamID)
        {
            Mod collectedMod = CollectedModInfo[steamID];

            Mod catalogMod = ActiveCatalog.Instance.AddMod(steamID);

            catalogMod.Update(collectedMod.Name, collectedMod.AuthorID, collectedMod.AuthorURL, collectedMod.Published, collectedMod.Updated, archiveURL: null,
                collectedMod.SourceURL, collectedMod.CompatibleGameVersionString, collectedMod.RequiredDLC, collectedMod.RequiredMods, collectedMod.RequiredAssets, 
                successors: null, alternatives: null, collectedMod.Statuses, note: null, reviewUpdated: null, UpdateDate,
                changeNotes: $"Added by { (AutoUpdater ? "Auto" : "Manual") }Updater on { Toolkit.DateString(UpdateDate) }.");

            // Change notes
            string modType = catalogMod.Statuses.Contains(Enums.ModStatus.UnlistedInWorkshop) ? "unlisted " : 
                (catalogMod.Statuses.Contains(Enums.ModStatus.RemovedFromWorkshop) ? "removed " : "");

            ChangeNotesNewMods.AppendLine($"New { modType }mod { catalogMod.ToString(cutOff: false) }");
        }


        // Update a mod in the catalog with new info
        private static void UpdateMod(ulong steamID)
        {
            // Get a reference to the mod in the catalog and to the mod with the collected info
            Mod catalogMod = ActiveCatalog.Instance.ModDictionary[steamID];

            Mod collectedMod = CollectedModInfo[catalogMod.SteamID];

            // Did we check details for this mod?
            bool detailsChecked = collectedMod.ChangeNotes == "Details checked";

            // Keep track of changes
            string changes = "";

            // Name
            if ((catalogMod.Name != collectedMod.Name) && !string.IsNullOrEmpty(collectedMod.Name))
            {
                catalogMod.Update(name: collectedMod.Name);

                changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "name changed";
            }

            // Author ID; only update if it was unknown; author ID can never changed and a mod can't change primary owner, so don't remove if we didn't find it anymore
            if ((catalogMod.AuthorID == 0) && (collectedMod.AuthorID != 0))
            {
                // Add author ID to the mod
                catalogMod.Update(authorID: collectedMod.AuthorID);

                changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "author profile ID found";

                // Update the author ID for the author; this ensures that when adding/updating authors later, the author is recognized and not mistakenly seen as new
                if (!string.IsNullOrEmpty(catalogMod.AuthorURL) && ActiveCatalog.Instance.AuthorURLDictionary.ContainsKey(catalogMod.AuthorURL))
                {
                    // Only update the ID if it wasn't updated already from a previous mod
                    if (!ActiveCatalog.Instance.AuthorIDDictionary.ContainsKey(catalogMod.AuthorID))
                    {
                        Author catalogAuthor = ActiveCatalog.Instance.AuthorURLDictionary[catalogMod.AuthorURL];

                        // Add author ID to author
                        catalogAuthor.Update(profileID: catalogMod.AuthorID, 
                            changeNotes: $"{ (AutoUpdater ? "Auto" : "") }Updated on { Toolkit.DateString(UpdateDate) }: Profile ID found");

                        // Add author to author ID dictionary in the active catalog
                        ActiveCatalog.Instance.AuthorIDDictionary.Add(catalogAuthor.ProfileID, catalogAuthor);

                        // Change notes
                        ChangeNotesUpdatedAuthors.AppendLine($"Author { catalogAuthor.ToString() }: author profile ID found");

                        Logger.UpdaterLog($"Author { catalogAuthor.ToString() }: profile ID { catalogAuthor.ProfileID } linked to custom URL \"{ catalogAuthor.CustomURL }\".");
                    }
                }
                else
                {
                    Logger.UpdaterLog($"Could not add author profile ID { catalogMod.AuthorID } to author with custom URL \"{ catalogMod.AuthorURL }\", " + 
                        "because the URL can't be found anymore.");
                }
            }

            // Author URL; only update if different and not empty; sometimes we get an author by ID while it still has an url, so don't remove an url
            if (catalogMod.AuthorURL != collectedMod.AuthorURL && !string.IsNullOrEmpty(collectedMod.AuthorURL))
            {
                // Author URL found, removed (not used currently) or changed
                string change = string.IsNullOrEmpty(catalogMod.AuthorURL)   ? "found" :
                                string.IsNullOrEmpty(collectedMod.AuthorURL) ? "removed" :
                                "changed";

                // Collect the old URL before we change it
                string oldURL = catalogMod.AuthorURL ?? "";

                // Add author URL to the mod; change null into empty string for if we decide to allow removal of author URL
                catalogMod.Update(authorURL: collectedMod.AuthorURL ?? "");

                changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "author custom url " + change;

                // Update the author URL for the author; this ensures that when adding/updating authors later, the author is recognized and not mistakenly seen as new
                Author catalogAuthor = null;

                // Get the author by ID
                if (catalogMod.AuthorID != 0 && ActiveCatalog.Instance.AuthorIDDictionary.ContainsKey(catalogMod.AuthorID))
                {
                    catalogAuthor = ActiveCatalog.Instance.AuthorIDDictionary[catalogMod.AuthorID];
                }
                // Or get the author by old URL
                else if (!string.IsNullOrEmpty(oldURL) && ActiveCatalog.Instance.AuthorURLDictionary.ContainsKey(oldURL))
                {
                    catalogAuthor = ActiveCatalog.Instance.AuthorURLDictionary[oldURL];
                }

                // Update the custom URL for the author, but only if it wasn't updated already from a previous mod
                if (catalogAuthor != null && !ActiveCatalog.Instance.AuthorURLDictionary.ContainsKey(catalogMod.AuthorURL))
                {
                    // Add/update URL for author
                    catalogAuthor.Update(customURL: catalogMod.AuthorURL, 
                        changeNotes: $"{ (AutoUpdater ? "Auto" : "") }Updated on { Toolkit.DateString(UpdateDate) }: custom URL { change }");

                    // Add author to author URL dictionary in the active catalog
                    ActiveCatalog.Instance.AuthorURLDictionary.Add(catalogAuthor.CustomURL, catalogAuthor);

                    // Mark the old URL for removal
                    if (!string.IsNullOrEmpty(oldURL) && !AuthorURLsToRemove.Contains(oldURL))
                    {
                        AuthorURLsToRemove.Add(oldURL);
                    }

                    // Change notes
                    ChangeNotesUpdatedAuthors.AppendLine($"Author { catalogAuthor.ToString() }: custom URL { change }");

                    Logger.UpdaterLog($"Author { catalogAuthor.ToString() }: new custom URL \"{ catalogAuthor.CustomURL }\"" +
                        (catalogAuthor.ProfileID == 0 ? "." : $" linked to profile ID { catalogAuthor.ProfileID }.") +
                        (string.IsNullOrEmpty(oldURL) ? "" : $" Old URL: { oldURL }."));
                }
                // If the catalog contains the new URL, then the author was already updated from an earlier mod; otherwise log an error
                else if (!ActiveCatalog.Instance.AuthorURLDictionary.ContainsKey(catalogMod.AuthorURL))
                {
                    Logger.UpdaterLog($"Could not update author custom URL \"{ catalogMod.AuthorURL }\" to author with profile ID { catalogMod.AuthorID }, " +
                        "because the ID or the old URL can't be found.", Logger.error);
                }
            }

            // Author URL no longer found
            if (!string.IsNullOrEmpty(catalogMod.AuthorURL) && string.IsNullOrEmpty(collectedMod.AuthorURL))
            {
                // We don't know if the custom URL was removed by the author, or if Steam decided to give us the profile ID instead; do nothing with it for now
                // [Todo 0.3] Not removing means incorrect custom URLs in the catalog, with potential weird results if someone else starts using it; test URL by downloading?

                /*
                if (catalogMod.AuthorID != 0 && ActiveCatalog.Instance.AuthorIDDictionary.ContainsKey(catalogMod.AuthorID)) 
                {
                    Author author = ActiveCatalog.Instance.AuthorIDDictionary[catalogMod.AuthorID];

                    Logger.UpdaterLog($"[Not updated in catalog] Author URL no longer found for author { author.ToString() }");
                }
                else
                {
                    Logger.UpdaterLog($"[Not updated in catalog] Mod { catalogMod.ToString() }: author URL no longer found");
                }
                */
            }

            // Published (only if details for this mod were checked)
            if (catalogMod.Published < collectedMod.Published && detailsChecked)
            {
                // No mention in the change notes, but log if the publish date was already a valid date
                if (catalogMod.Published != DateTime.MinValue)
                {
                    Logger.UpdaterLog($"Published date changed from { Toolkit.DateString(catalogMod.Published) } to { Toolkit.DateString(collectedMod.Published) }. " +
                        $"This should not happen. Mod { catalogMod.ToString(cutOff: false) }", Logger.warning);
                }

                catalogMod.Update(published: collectedMod.Published);
            }

            // Updated (only if details for this mod were checked)
            if (catalogMod.Updated < collectedMod.Updated && detailsChecked)
            {
                catalogMod.Update(updated: collectedMod.Updated);

                // Only mention in the change notes if it was really an update (and not a copy of the published date)
                if (catalogMod.Updated != catalogMod.Published)
                {
                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "new update";
                }
            }

            // Source URL (only if details for this mod were checked or ManualUpdater is running)
            if (catalogMod.SourceURL != collectedMod.SourceURL && (detailsChecked || !AutoUpdater))
            {
                if (string.IsNullOrEmpty(catalogMod.SourceURL) && !string.IsNullOrEmpty(collectedMod.SourceURL))
                {
                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "source url found";

                    // Remove 'source unavailable' status
                    if (catalogMod.Statuses.Contains(Enums.ModStatus.SourceUnavailable))
                    {
                        catalogMod.Statuses.Remove(Enums.ModStatus.SourceUnavailable);

                        changes += " ('source unavailable' status removed)";
                    }
                }
                else if (string.IsNullOrEmpty(collectedMod.SourceURL) && !string.IsNullOrEmpty(catalogMod.SourceURL))
                {
                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "source url removed";
                }
                else if (!string.IsNullOrEmpty(collectedMod.SourceURL) && !string.IsNullOrEmpty(catalogMod.SourceURL))
                {
                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "source url changed";
                }

                catalogMod.Update(sourceURL: collectedMod.SourceURL);
            }

            // Compatible game version (only if details for this mod were checked)
            if (catalogMod.CompatibleGameVersionString != collectedMod.CompatibleGameVersionString && detailsChecked)
            {
                string unknown = GameVersion.Unknown.ToString();

                if (catalogMod.CompatibleGameVersionString == unknown && collectedMod.CompatibleGameVersionString != unknown)
                {
                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "compatible game version tag found";
                }
                else if (catalogMod.CompatibleGameVersionString != unknown && collectedMod.CompatibleGameVersionString == unknown)
                {
                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "compatible game version tag removed";
                }
                else if (catalogMod.CompatibleGameVersionString != unknown && collectedMod.CompatibleGameVersionString != unknown)
                {
                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "compatible game version tag changed";
                }

                catalogMod.Update(compatibleGameVersionString: collectedMod.CompatibleGameVersionString);
            }

            // Required DLC (only if details for this mod were checked)
            if (catalogMod.RequiredDLC.Count + collectedMod.RequiredDLC.Count != 0 && detailsChecked)
            {
                // Add new required dlc
                foreach (Enums.DLC dlc in collectedMod.RequiredDLC)
                {
                    if (!catalogMod.RequiredDLC.Contains(dlc))
                    {
                        catalogMod.RequiredDLC.Add(dlc);

                        changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + $"required dlc \"{ dlc }\" added";
                    }
                }

                // Remove no longer required dlc
                foreach (Enums.DLC dlc in catalogMod.RequiredDLC)
                {
                    if (!collectedMod.RequiredDLC.Contains(dlc))
                    {
                        //catalogMod.RequiredDLC.Remove(dlc);     // [Todo 0.3] Illegal operation, cannot change within foreach

                        changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + $"required dlc \"{ dlc }\" removed";
                    }
                }
            }

            // Required mods (only if details for this mod were checked), including updating existing NeededFor lists;
            // [Todo 0.3] Check exclusions before replacing a mod with a group  [Todo 0.5] simplify (or split) this
            if (catalogMod.RequiredMods.Count + collectedMod.RequiredMods.Count != 0 && detailsChecked)
            {
                // Remove no longer needed mods and groups from the required list
                foreach (ulong requiredID in catalogMod.RequiredMods)
                {
                    // Check if it's a mod or a group
                    if (requiredID >= ModSettings.lowestGroupID && requiredID <= ModSettings.highestGroupID)
                    {
                        // ID is a group; check if this is still required
                        bool stillRequired = false;

                        foreach (ulong modID in ActiveCatalog.Instance.GroupDictionary[requiredID].SteamIDs)
                        {
                            if (collectedMod.RequiredMods.Contains(modID))
                            {
                                // A group member is still required, so the group is still required
                                stillRequired = true;

                                break;
                            }
                        }

                        if (!stillRequired)
                        {
                            // No longer required; remove the group
                            //catalogMod.RequiredMods.Remove(requiredID);     // [Todo 0.3] Illegal operation, cannot change within foreach

                            changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + $"required group { requiredID } removed";
                        }
                    }
                    else if (ActiveCatalog.Instance.Groups.Find(x => x.SteamIDs.Contains(requiredID)) != null)
                    {
                        // ID is a mod that is a group member, so remove it; the group will be added below if still needed
                        //catalogMod.RequiredMods.Remove(requiredID);     // [Todo 0.3] Illegal operation, cannot change within foreach

                        changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + $"required mod { requiredID } removed";
                    }
                    else if (!collectedMod.RequiredMods.Contains(requiredID))
                    {
                        // ID is a mod that is not in any group, and it's not required anymore, so remove it
                        //catalogMod.RequiredMods.Remove(requiredID);     // [Todo 0.3] Illegal operation, cannot change within foreach

                        changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + $"required mod { requiredID } removed";
                    }
                }

                // Add new required mods, as mod or group
                foreach (ulong requiredModID in collectedMod.RequiredMods)
                {
                    // Check if this required mod is part of a group
                    Group group = ActiveCatalog.Instance.GetGroup(requiredModID);

                    if (group == default)
                    {
                        // Add the required mod to the catalog mod's required list, if it isn't there already
                        if (!catalogMod.RequiredMods.Contains(requiredModID))
                        {
                            catalogMod.RequiredMods.Add(requiredModID);

                            changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + $"required mod { requiredModID } added";
                        }
                    }
                    else
                    {
                        // Add the group (instead of the required mod) to the catalog mod's required list, if it isn't there already
                        if (!catalogMod.RequiredMods.Contains(group.GroupID))
                        {
                            catalogMod.RequiredMods.Add(group.GroupID);

                            changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + $"required group { group.GroupID } added (instead of mod { requiredModID })";
                        }
                    }
                }
            }

            // Required assets (only if details for this mod were checked)
            if (catalogMod.RequiredAssets.Count + collectedMod.RequiredAssets.Count != 0 && detailsChecked)
            {
                // We're not really interested in these; just replace the list
                catalogMod.Update(requiredAssets: collectedMod.RequiredAssets);
            }

            // Add new Statuses: incompatible, no description*, unlisted in workshop, removed from workshop (* = only if details for this mod were checked)
            if (collectedMod.Statuses.Count > 0)
            {
                if (collectedMod.Statuses.Contains(Enums.ModStatus.IncompatibleAccordingToWorkshop) &&
                    !catalogMod.Statuses.Contains(Enums.ModStatus.IncompatibleAccordingToWorkshop))
                {
                    catalogMod.Statuses.Add(Enums.ModStatus.IncompatibleAccordingToWorkshop);

                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "'IncompatibleAccordingToWorkshop' status added";
                }

                if (collectedMod.Statuses.Contains(Enums.ModStatus.NoDescription) && 
                    !catalogMod.Statuses.Contains(Enums.ModStatus.NoDescription) && detailsChecked)
                {
                    catalogMod.Statuses.Add(Enums.ModStatus.NoDescription);

                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "'NoDescription' status added";
                }

                if (collectedMod.Statuses.Contains(Enums.ModStatus.UnlistedInWorkshop) && 
                    !catalogMod.Statuses.Contains(Enums.ModStatus.UnlistedInWorkshop))
                {
                    catalogMod.Statuses.Add(Enums.ModStatus.UnlistedInWorkshop);

                    // Remove removed status, if needed
                    catalogMod.Statuses.Remove(Enums.ModStatus.RemovedFromWorkshop);

                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "'UnlistedInWorkshop' status added";
                }

                if (collectedMod.Statuses.Contains(Enums.ModStatus.RemovedFromWorkshop) &&
                    !catalogMod.Statuses.Contains(Enums.ModStatus.RemovedFromWorkshop))
                {
                    catalogMod.Statuses.Add(Enums.ModStatus.RemovedFromWorkshop);

                    // Remove unlisted status, if needed
                    catalogMod.Statuses.Remove(Enums.ModStatus.UnlistedInWorkshop);

                    // Gives this its own line in the change notes
                    ChangeNotesNewMods.AppendLine($"Mod no longer available on the workshop: { catalogMod.ToString(cutOff: false) }");
                }
            }

            // Remove Statuses: incompatible, no description*, unlisted in workshop, removed from workshop (* = only if details for this mod were checked)
            if (catalogMod.Statuses.Count > 0)
            {
                if (catalogMod.Statuses.Contains(Enums.ModStatus.IncompatibleAccordingToWorkshop) &&
                    !collectedMod.Statuses.Contains(Enums.ModStatus.IncompatibleAccordingToWorkshop))
                {
                    catalogMod.Statuses.Remove(Enums.ModStatus.IncompatibleAccordingToWorkshop);

                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "'IncompatibleAccordingToWorkshop' status removed";
                }

                if (catalogMod.Statuses.Contains(Enums.ModStatus.NoDescription) && 
                    !collectedMod.Statuses.Contains(Enums.ModStatus.NoDescription) && detailsChecked)
                {
                    catalogMod.Statuses.Remove(Enums.ModStatus.NoDescription);

                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "'NoDescription' status removed";
                }

                if (catalogMod.Statuses.Contains(Enums.ModStatus.UnlistedInWorkshop) &&
                    !collectedMod.Statuses.Contains(Enums.ModStatus.UnlistedInWorkshop))
                {
                    catalogMod.Statuses.Remove(Enums.ModStatus.UnlistedInWorkshop);

                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "'UnlistedInWorkshop' status removed";
                }

                if (catalogMod.Statuses.Contains(Enums.ModStatus.RemovedFromWorkshop) &&
                    !collectedMod.Statuses.Contains(Enums.ModStatus.RemovedFromWorkshop))
                {
                    catalogMod.Statuses.Remove(Enums.ModStatus.RemovedFromWorkshop);

                    // Gives this its own line in the change notes
                    ChangeNotesRemovedMods.AppendLine($"Mod reappeared on the workshop: { catalogMod.ToString(cutOff: false) }");
                }
            }

            // Auto review update date and change notes
            if (!string.IsNullOrEmpty(changes))
            {
                catalogMod.Update(autoReviewUpdated: UpdateDate, 
                    changeNotes: $"{ (AutoUpdater ? "Auto" : "") }Updated on { Toolkit.DateString(UpdateDate) }: { changes }.");

                ChangeNotesUpdatedMods.AppendLine($"Mod { catalogMod.ToString(cutOff: false) }: { changes }");
            }
        }


        // Add a new author to the catalog
        private static void AddAuthor(Author collectedAuthor)
        {
            ActiveCatalog.Instance.AddAuthor(collectedAuthor.ProfileID, collectedAuthor.CustomURL, collectedAuthor.Name, collectedAuthor.LastSeen, retired: false,
                changeNotes: $"Added by { (AutoUpdater ? "Auto" : "Manual") }Updater on { Toolkit.DateString(UpdateDate) }.");

            // Change notes
            ChangeNotesNewAuthors.AppendLine($"New author { collectedAuthor.ToString() }");
        }


        // Update changed info for an author (profile ID and custom URL changes are updated together with mod updates)
        private static void UpdateAuthor(Author catalogAuthor, Author collectedAuthor)
        {
            // Keep track of changes
            string changes = "";

            // Name
            if ((catalogAuthor.Name != collectedAuthor.Name) && !string.IsNullOrEmpty(collectedAuthor.Name))
            {
                catalogAuthor.Update(name: collectedAuthor.Name);

                changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "name changed";
            }

            // Last seen; also resets retired 
            if (catalogAuthor.LastSeen < collectedAuthor.LastSeen)
            {
                if (catalogAuthor.Retired)
                {
                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "no longer retired";
                }

                catalogAuthor.Update(lastSeen: collectedAuthor.LastSeen, retired: false);

                changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "'last seen' date updated";
            }
            // Retired
            else if (!catalogAuthor.Retired && collectedAuthor.Retired)
            {
                catalogAuthor.Update(retired: true);

                changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "set as retired";
            }
            // No longer retired
            else if (catalogAuthor.Retired && !collectedAuthor.Retired)
            {
                // [Todo 0.3] How to differentiate between Remove_Retired action and the default false value?
                // catalogAuthor.Update(retired: false);

                // changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "no longer retired";
            }

            // Change notes
            if (!string.IsNullOrEmpty(changes))
            {
                catalogAuthor.Update(changeNotes: $"{ (AutoUpdater ? "Auto" : "") }Updated on { Toolkit.DateString(UpdateDate) }: { changes }.");

                ChangeNotesUpdatedAuthors.AppendLine($"Author { catalogAuthor.ToString() }: " + changes);
            }
        }


        // Set a new note for the catalog
        internal static void SetNote(string catalogNote) => CatalogNote = catalogNote;


        // Set an update date
        internal static bool SetUpdateDate(string updateDate)
        {
            try
            {
                DateTime convertedDate = DateTime.ParseExact(updateDate, "yyyy-MM-dd", new CultureInfo("en-GB"));

                UpdateDate = convertedDate;

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
