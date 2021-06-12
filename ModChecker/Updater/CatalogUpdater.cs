using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ModChecker.DataTypes;
using ModChecker.Util;


namespace ModChecker.Updater
{
    internal static class CatalogUpdater
    {
        // Dictionaries to collect info from the Steam Workshop
        internal static Dictionary<ulong, Mod> CollectedModInfo { get; private set; } = new Dictionary<ulong, Mod>();
        internal static Dictionary<ulong, Author> CollectedAuthorIDs { get; private set; } = new Dictionary<ulong, Author>();
        internal static Dictionary<string, Author> CollectedAuthorURLs { get; private set; } = new Dictionary<string, Author>();

        // Change notes, separate parts and combined
        private static StringBuilder ChangeNotesNew;
        private static StringBuilder ChangeNotesUpdated;
        private static StringBuilder ChangeNotesRemoved;
        private static string ChangeNotes;

        // Date and time of this update
        private static DateTime UpdateDate;


        internal static void Init()
        {
            CollectedModInfo.Clear();
            CollectedAuthorIDs.Clear();
            CollectedAuthorURLs.Clear();

            ChangeNotesNew = new StringBuilder();
            ChangeNotesUpdated = new StringBuilder();
            ChangeNotesRemoved = new StringBuilder();
            ChangeNotes = "";
        }


        // [Todo 0.2.1] Add check for exclusions; also add exclusions dictionary in Catalog class; Exclusions needed for:
        // * source url: many, including 2414618415, 2139980554

        // Update the active catalog with the found information
        internal static bool Start()
        {
            UpdateDate = DateTime.Now;

            // Add or update all found mods
            foreach (ulong steamID in CollectedModInfo.Keys)
            {
                // Get the found mod
                Mod collectedMod = CollectedModInfo[steamID];

                // Clean out assets from the required mods list
                List<ulong> removalList = new List<ulong>();

                foreach (ulong requiredID in collectedMod.RequiredMods)
                {
                    // Remove the required ID if we didn't find it on the Workshop; ignore builtin required mods
                    if (!CollectedModInfo.ContainsKey(requiredID) && (requiredID > ModSettings.highestFakeID))
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

                foreach (ulong requiredID in removalList)
                {
                    // Now move the above collected IDs to the asset list
                    collectedMod.RequiredMods.Remove(requiredID);

                    collectedMod.RequiredAssets.Add(requiredID);
                }

                // Clean up compatible gameversion
                if (collectedMod.CompatibleGameVersionString == null)
                {
                    collectedMod.Update(compatibleGameVersionString: GameVersion.Formatted(GameVersion.Unknown));
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

            // Add or update all found authors, by author ID
            foreach (ulong authorID in CollectedAuthorIDs.Keys)
            {
                if (!ActiveCatalog.Instance.AuthorIDDictionary.ContainsKey(authorID))
                {
                    // New author; add to the catalog
                    Author collectedAuthor = CollectedAuthorIDs[authorID];

                    AddAuthor(collectedAuthor);
                }
                else
                {
                    // Known author
                    Author catalogAuthor = ActiveCatalog.Instance.AuthorIDDictionary[authorID];

                    Author collectedAuthor = CollectedAuthorIDs[authorID];

                    // Update all info
                    UpdateAuthor(catalogAuthor, collectedAuthor);
                }
            }

            // Add or update all found authors, by author custom URL
            foreach (string authorURL in CollectedAuthorURLs.Keys)
            {
                if (!ActiveCatalog.Instance.AuthorURLDictionary.ContainsKey(authorURL))
                {
                    // New author; add to the catalog
                    Author collectedAuthor = CollectedAuthorURLs[authorURL];

                    AddAuthor(collectedAuthor);
                }
                else
                {
                    // Known author
                    Author catalogAuthor = ActiveCatalog.Instance.AuthorURLDictionary[authorURL];

                    Author collectedAuthor = CollectedAuthorURLs[authorURL];

                    // Update all info
                    UpdateAuthor(catalogAuthor, collectedAuthor);
                }
            }

            // Mods no longer available in the Steam Workshop
            foreach (ulong steamID in ActiveCatalog.Instance.ModDictionary.Keys)
            {
                Mod catalogMod = ActiveCatalog.Instance.ModDictionary[steamID];

                // Ignore mods we just found, and local and builtin mods, and mods that already have the 'removed' status
                if (!CollectedModInfo.ContainsKey(steamID) && (steamID > ModSettings.highestFakeID) && !catalogMod.Statuses.Contains(Enums.ModStatus.RemovedFromWorkshop))
                {
                    catalogMod.Statuses.Add(Enums.ModStatus.RemovedFromWorkshop);

                    catalogMod.Update(catalogRemark: $"AutoUpdated as removed on { UpdateDate.ToShortDateString() }.");

                    ChangeNotesRemoved.AppendLine("Mod no longer available on the workshop: " + catalogMod.ToString(cutOff: false));
                }
            }

            /* [Todo 0.2] Rethink this. It doesn't work well, since Steam sometimes randomly gives the ID link instead of the URL link in mod listing pages

            // Authors no longer available in the Steam Workshop, by author ID
            foreach (ulong authorID in ActiveCatalog.Instance.AuthorIDDictionary.Keys)
            {
                // Ignore authors that already have the 'retired' status
                if (!collectedAuthorIDs.ContainsKey(authorID) && !ActiveCatalog.Instance.AuthorIDDictionary[authorID].Retired)
                {
                    Author catalogAuthor = ActiveCatalog.Instance.AuthorIDDictionary[authorID];
                    
                    catalogAuthor.Update(retired: true, catalogRemark: $"AutoUpdated as retired on { UpdateDate.ToShortDateString() }.");

                    ChangeNotesRemoved.AppendLine($"Author no longer has mods on the workshop: { ActiveCatalog.Instance.AuthorIDDictionary[authorID].ToString() }");
                }
            }

            // Authors no longer available in the Steam Workshop, by author custom URL
            foreach (string authorURL in ActiveCatalog.Instance.AuthorURLDictionary.Keys)
            {
                // Ignore authors that already have the 'retired' status
                if (!collectedAuthorURLs.ContainsKey(authorURL) && !ActiveCatalog.Instance.AuthorURLDictionary[authorURL].Retired)
                {
                    Author catalogAuthor = ActiveCatalog.Instance.AuthorURLDictionary[authorURL];

                    catalogAuthor.Update(retired: true, catalogRemark: $"AutoUpdated as retired on { UpdateDate.ToShortDateString() }.");

                    ChangeNotesRemoved.AppendLine($"Author no longer has mods on the workshop: { ActiveCatalog.Instance.AuthorURLDictionary[authorURL].ToString() }");
                }
            }
            */

            bool success = false;

            // Did we find any changes?
            if (ChangeNotesNew.Length == 0 && ChangeNotesUpdated.Length == 0 && ChangeNotesRemoved.Length == 0)
            {
                // Nothing changed
                Logger.UpdaterLog("No changed or new mods detected on the Steam Workshop.");

                success = true;
            }
            else
            {
                // Increase the catalog version and update date
                ActiveCatalog.Instance.NewVersion(UpdateDate);

                // Combine the change notes
                ChangeNotes = $"Change Notes for Catalog { ActiveCatalog.Instance.VersionString() }\n" +
                    "-------------------------------\n" +
                    $"{ UpdateDate:D}, { UpdateDate:t}\n\n" +
                    "*** ADDED: ***\n" +
                    ChangeNotesNew.ToString() + "\n" +
                    "*** UPDATED: ***\n" +
                    ChangeNotesUpdated.ToString() + "\n" +
                    "*** REMOVED: ***\n" +
                    ChangeNotesRemoved.ToString();

                // The filename for the new catalog and related files ('ModCheckerCatalog_v1.0001')
                string partialPath = Path.Combine(ModSettings.updaterPath, $"{ ModSettings.internalName }Catalog_v{ ActiveCatalog.Instance.VersionString() }");

                // Save the new catalog
                if (ActiveCatalog.Instance.Save(partialPath + ".xml"))
                {
                    // Save change notes, in the same folder as the new catalog
                    Tools.SaveToFile(ChangeNotes.ToString(), partialPath + "_ChangeNotes.txt");

                    // Copy the updater logfile to the same folder as the new catalog
                    Tools.CopyFile(ModSettings.updaterLogfileFullPath, partialPath + ".log");

                    success = true;
                }

                // Close and reopen the active catalog, because we made changes to it
                Logger.Log("Closing and reopening the active catalog.");

                ActiveCatalog.Close();

                ActiveCatalog.Init();
            }

            // Empty the dictionaries and change notes to free memory
            Init();

            return success;
        }


        // Add a new author to the catalog
        private static void AddAuthor(Author collectedAuthor)
        {
            ActiveCatalog.Instance.AddAuthor(collectedAuthor.ProfileID, collectedAuthor.CustomURL, collectedAuthor.Name, collectedAuthor.LastSeen, retired: false,
                catalogRemark: $"Added by AutoUpdater on { UpdateDate.ToShortDateString() }.");

            // Change notes
            ChangeNotesNew.AppendLine($"New author { collectedAuthor.ToString() }");
        }


        // Update changed info for an author; return the change notes text
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

            // Last seen and retired
            if (catalogAuthor.LastSeen < collectedAuthor.LastSeen)
            {
                if (catalogAuthor.Retired)
                {
                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "no longer retired";
                }

                catalogAuthor.Update(lastSeen: collectedAuthor.LastSeen, retired: false);

                changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "'last seen' date updated";
            }

            // Change notes
            if (!string.IsNullOrEmpty(changes))
            {
                catalogAuthor.Update(catalogRemark: $"AutoUpdated on { UpdateDate.ToShortDateString() }: { changes }.");

                ChangeNotesUpdated.AppendLine($"Author { catalogAuthor.ToString() }: " + changes);
            }
        }


        // Add a new mod to the active catalog
        private static void AddMod(ulong steamID)
        {
            Mod collectedMod = CollectedModInfo[steamID];

            Mod catalogMod = ActiveCatalog.Instance.AddMod(steamID);

            catalogMod.Update(collectedMod.Name, collectedMod.AuthorID, collectedMod.AuthorURL, collectedMod.Published, collectedMod.Updated, archiveURL: null,
                collectedMod.SourceURL, collectedMod.CompatibleGameVersionString, collectedMod.RequiredDLC, collectedMod.RequiredMods, onlyNeededFor: null,
                succeededBy: null, alternatives: null, collectedMod.RequiredAssets, collectedMod.Statuses, note: null, reviewUpdated: null, UpdateDate,
                catalogRemark: $"Added by AutoUpdater on { UpdateDate.ToShortDateString() }.");

            // Change notes
            ChangeNotesNew.AppendLine($"New mod { catalogMod.ToString(cutOff: false) }");
        }


        // Update a mod in the catalog with new info
        private static void UpdateMod(ulong steamID)
        {
            // Get a reference to the mod in the catalog and to the mod with the collected info
            Mod catalogMod = ActiveCatalog.Instance.ModDictionary[steamID];

            Mod collectedMod = CollectedModInfo[catalogMod.SteamID];

            // Did we check details for this mod?
            bool detailsChecked = collectedMod.CatalogRemark == "Details checked";

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
                // Author profile ID found where there was none before
                catalogMod.Update(authorID: collectedMod.AuthorID);

                Logger.UpdaterLog($"Author profile ID found for { catalogMod.ToString(cutOff: false) }");

                // [Todo 0.2] Add author id to other mods with the same Author URL (elsewhere in the updater after all collected mods are processed)
                // [Todo 0.2] Add author id to author (link through author URL)
            }

            // Author URL
            if (string.IsNullOrEmpty(catalogMod.AuthorURL) && !string.IsNullOrEmpty(collectedMod.AuthorURL))
            {
                // Author URL found where there was none before
                catalogMod.Update(authorURL: collectedMod.AuthorURL);

                Logger.UpdaterLog($"Author custom URL found for { catalogMod.ToString(cutOff: false) }");
            }
            else if (catalogMod.AuthorURL != collectedMod.AuthorURL)
            {
                // Author URL has changed or was no longer found

                // [Todo 0.2] Finish this; use this to merge the two authors with old and new url (link through author ID if known)
                // also change author URL for every mod that has it; but how certain are we that this change is real? What if another author now uses this URL?

                // changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "author URL changed";

                Logger.UpdaterLog($"Author custom URL change detected (but not updated in the catalog) for { catalogMod.ToString(cutOff: false) }; " +
                    $"from \"{ catalogMod.AuthorURL }\" to \"{ collectedMod.AuthorURL }\".", Logger.debug);
            }

            // Published (only if details for this mod were checked)
            if (catalogMod.Published < collectedMod.Published && detailsChecked)
            {
                // No mention in the change notes, but log if the publish date was already a valid date
                if (catalogMod.Published != DateTime.MinValue)
                {
                    Logger.UpdaterLog($"Published date changed from { catalogMod.Published.ToShortDateString() } to { collectedMod.Published.ToShortDateString() }. " +
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

            // Source URL (only if details for this mod were checked)
            if (catalogMod.SourceURL != collectedMod.SourceURL && detailsChecked)
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
            if (catalogMod.RequiredDLC.ToString() != collectedMod.RequiredDLC.ToString() && detailsChecked)
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
                        catalogMod.RequiredDLC.Remove(dlc);

                        changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + $"required dlc \"{ dlc }\" removed";
                    }
                }
            }

            // Required mods (only if details for this mod were checked), including updating existing NeededFor lists; [Todo 0.5] simplify (or split) this
            if (catalogMod.RequiredMods.ToString() != collectedMod.RequiredMods.ToString() && detailsChecked)
            {
                // Remove no longer needed mods and groups from the required list
                foreach (ulong requiredID in catalogMod.RequiredMods)
                {
                    // Check if it's a mod or a group
                    if (requiredID >= ModSettings.lowestModGroupID && requiredID <= ModSettings.highestModGroupID)
                    {
                        // ID is a group; check if this is still required
                        bool stillRequired = false;

                        foreach (ulong modID in ActiveCatalog.Instance.ModGroupDictionary[requiredID].SteamIDs)
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
                            catalogMod.RequiredMods.Remove(requiredID);

                            changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + $"required mod group { requiredID } removed";
                        }
                    }
                    else if (ActiveCatalog.Instance.ModGroups.Find(x => x.SteamIDs.Contains(requiredID)) != null)
                    {
                        // ID is a mod that is a group member, so remove it; the group will be added below if still needed
                        catalogMod.RequiredMods.Remove(requiredID);

                        changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + $"required mod { requiredID } removed";
                    }
                    else if (!collectedMod.RequiredMods.Contains(requiredID))
                    {
                        // ID is a mod that is not in any group, and it's not required anymore, so remove it
                        catalogMod.RequiredMods.Remove(requiredID);

                        changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + $"required mod { requiredID } removed";

                        // Remove the updated mod from the 'only needed for' list of the previously required mod
                        if (ActiveCatalog.Instance.ModDictionary.ContainsKey(requiredID))
                        {
                            Mod requiredMod = ActiveCatalog.Instance.ModDictionary[requiredID];

                            if (requiredMod.NeededFor.Contains(steamID))
                            {
                                requiredMod.NeededFor.Remove(steamID);

                                ChangeNotesUpdated.AppendLine($"Mod { requiredMod.ToString(cutOff: false) }: removed { steamID } from 'only needed for' list");
                            }
                        }
                    }
                }

                // Add new required mods, as mod or group
                foreach (ulong requiredModID in collectedMod.RequiredMods)
                {
                    // Check if this required mod is part of a group
                    ModGroup group = ActiveCatalog.Instance.ModGroups.Find(x => x.SteamIDs.Contains(requiredModID));

                    if (group == null)
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

                            changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + $"required mod group { group.GroupID } added (instead of mod { requiredModID })";
                        }
                    }
                }
            }

            // Required assets (only if details for this mod were checked)
            if (catalogMod.RequiredAssets.ToString() != collectedMod.RequiredAssets.ToString() && detailsChecked)
            {
                // We're not really interested in these; just replace the list
                catalogMod.Update(requiredAssets: collectedMod.RequiredAssets);

                Logger.UpdaterLog($"Required assets changed for [Steam ID { steamID,10 }]: { collectedMod.RequiredAssets }.", Logger.debug);
            }

            // Add new Statuses: incompatible, no description (only if details for this mod were checked)
            if (collectedMod.Statuses.Count > 0)
            {
                if (collectedMod.Statuses.Contains(Enums.ModStatus.IncompatibleAccordingToWorkshop) &&
                    !catalogMod.Statuses.Contains(Enums.ModStatus.IncompatibleAccordingToWorkshop))
                {
                    catalogMod.Statuses.Add(Enums.ModStatus.IncompatibleAccordingToWorkshop);

                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "'IncompatibleAccordingToWorkshop' status added";
                }

                if (collectedMod.Statuses.Contains(Enums.ModStatus.NoDescription) && !catalogMod.Statuses.Contains(Enums.ModStatus.NoDescription) && detailsChecked)
                {
                    catalogMod.Statuses.Add(Enums.ModStatus.NoDescription);

                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "'NoDescription' status added";
                }
            }

            // Remove Statuses: incompatible, no description (only if details for this mod were checked), removed from workshop
            if (catalogMod.Statuses.Count > 0)
            {
                // Remove statuses
                if (catalogMod.Statuses.Contains(Enums.ModStatus.IncompatibleAccordingToWorkshop) &&
                    !collectedMod.Statuses.Contains(Enums.ModStatus.IncompatibleAccordingToWorkshop))
                {
                    catalogMod.Statuses.Remove(Enums.ModStatus.IncompatibleAccordingToWorkshop);

                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "'IncompatibleAccordingToWorkshop' status removed";
                }

                if (catalogMod.Statuses.Contains(Enums.ModStatus.NoDescription) && !collectedMod.Statuses.Contains(Enums.ModStatus.NoDescription) && detailsChecked)
                {
                    catalogMod.Statuses.Remove(Enums.ModStatus.NoDescription);

                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "'NoDescription' status removed";
                }

                if (catalogMod.Statuses.Contains(Enums.ModStatus.RemovedFromWorkshop))
                {
                    catalogMod.Statuses.Remove(Enums.ModStatus.RemovedFromWorkshop);

                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "'RemovedFromWorkshop' status removed";
                }

            }

            // Auto review update date, catalog remark and change notes
            if (!string.IsNullOrEmpty(changes))
            {
                catalogMod.Update(autoReviewUpdated: UpdateDate, catalogRemark: $"AutoUpdated on { UpdateDate.ToShortDateString() }: { changes }.");

                ChangeNotesUpdated.AppendLine($"Mod { catalogMod.ToString(cutOff: false) }: { changes }");
            }
        }
    }
}
