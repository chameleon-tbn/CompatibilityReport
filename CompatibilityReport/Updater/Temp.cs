using System;
using System.Collections.Generic;
using System.Linq;
using CompatibilityReport.DataTypes;
using CompatibilityReport.Util;

namespace CompatibilityReport.Updater
{
    class Temp  // Old CatalogUpdater methods, for review
    {
/*        // [Todo 0.3] Move into new UpdateMod()
        private static void UpdateMod(ulong steamID)
        {
            Mod catalogMod = ActiveCatalog.Instance.ModDictionary[steamID];
            Mod collectedMod = catalogMod; // CollectedModInfo[catalogMod.SteamID];
            bool detailedUpdate = collectedMod.AutoReviewDate != default || collectedMod.ReviewDate != default;
            string changes = "";

            // Name
            if ((catalogMod.Name != collectedMod.Name) && !string.IsNullOrEmpty(collectedMod.Name))
            {
                changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "name changed";
            }

            // Author ID; only update if it was unknown; author ID can never changed and a mod can't change primary owner, so don't remove if we didn't find it anymore
            if ((catalogMod.AuthorID == 0) && (collectedMod.AuthorID != 0))
            {
                // Add author ID to the mod
                catalogMod.Update(authorID: collectedMod.AuthorID);

                changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "profile ID added";

                // Update the author ID for the author; this ensures that when adding/updating authors later, the author is recognized and not mistakenly seen as new
                if (!string.IsNullOrEmpty(catalogMod.AuthorURL) && ActiveCatalog.Instance.AuthorURLDictionary.ContainsKey(catalogMod.AuthorURL))
                {
                    // Only update the ID if it wasn't updated already from a previous mod
                    if (!ActiveCatalog.Instance.AuthorIDDictionary.ContainsKey(catalogMod.AuthorID))
                    {
                        Author catalogAuthor = ActiveCatalog.Instance.AuthorURLDictionary[catalogMod.AuthorURL];

                        // Add author ID to author
                        catalogAuthor.Update(profileID: catalogMod.AuthorID, extraChangeNote: $"{ catalogDateString }: profile ID added");

                        // Add author to author ID dictionary in the active catalog
                        ActiveCatalog.Instance.AuthorIDDictionary.Add(catalogAuthor.ProfileID, catalogAuthor);

                        // Change notes
                        changeNotesUpdatedAuthors.AppendLine($"Author { catalogAuthor.ToString() }: profile ID added");

                        Logger.UpdaterLog($"Author { catalogAuthor.ToString() }: profile ID { catalogAuthor.ProfileID } linked to custom URL \"{ catalogAuthor.CustomURL }\".");
                    }
                }
                else
                {
                    Logger.UpdaterLog($"Could not add author profile ID { catalogMod.AuthorID } to author with custom URL \"{ catalogMod.AuthorURL }\", " +
                        "because the URL can't be found anymore.");
                }
            }


            // Author URL. If it was no longer found, it could just be Steam acting up, but safer to remove it anyway before someone else starts using it
            // [Todo 0.3] manual updates to authors might go wrong because of changed or missing url's
            if (catalogMod.AuthorURL != collectedMod.AuthorURL && (!string.IsNullOrEmpty(catalogMod.AuthorURL) || !string.IsNullOrEmpty(collectedMod.AuthorURL)))
            {
                // Added, removed (not used currently) or changed
                string change = string.IsNullOrEmpty(catalogMod.AuthorURL) ? "added" :
                                string.IsNullOrEmpty(collectedMod.AuthorURL) ? "removed" :
                                "changed";

                // Collect the old URL before we change it
                string oldURL = catalogMod.AuthorURL ?? "";

                // Add author URL to the mod, with null changed into an empty string
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
                    catalogAuthor.Update(customURL: catalogMod.AuthorURL, extraChangeNote: $"{ catalogDateString }: custom URL { change }");

                    // Add author to author URL dictionary in the active catalog
                    ActiveCatalog.Instance.AuthorURLDictionary.Add(catalogAuthor.CustomURL, catalogAuthor);

                    // Mark the old URL for removal
                    if (!string.IsNullOrEmpty(oldURL) && !CollectedAuthorURLRemovals.Contains(oldURL))
                    {
                        CollectedAuthorURLRemovals.Add(oldURL);
                    }

                    // Change notes
                    changeNotesUpdatedAuthors.AppendLine($"Author { catalogAuthor.ToString() }: custom URL { change }");

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

            // Published (only if details for this mod were checked)
            if (catalogMod.Published < collectedMod.Published && detailedUpdate)
            {
                // No mention in the change notes, but log if the publish date was already a valid date
                if (catalogMod.Published != DateTime.MinValue)
                {
                    Logger.UpdaterLog($"Published date changed from { Toolkit.DateString(catalogMod.Published) } to { Toolkit.DateString(collectedMod.Published) }. " +
                        $"This should not happen. Mod { catalogMod.ToString() }", Logger.warning);
                }

                catalogMod.Update(published: collectedMod.Published);
            }

            // Updated (only if details for this mod were checked)
            if (catalogMod.Updated < collectedMod.Updated && detailedUpdate)
            {
                catalogMod.Update(updated: collectedMod.Updated);

                // Only mention in the change notes if it was really an update (and not a copy of the published date)
                if (catalogMod.Updated != catalogMod.Published)
                {
                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "update found";
                }
            }

            // Source URL (only if details for this mod were checked or FileImporter is running)
            if (catalogMod.SourceURL != collectedMod.SourceURL && detailedUpdate)
            {
                // Added, removed or changed
                string change = string.IsNullOrEmpty(catalogMod.SourceURL) && !string.IsNullOrEmpty(collectedMod.SourceURL) ? "added" :
                                !string.IsNullOrEmpty(catalogMod.SourceURL) && string.IsNullOrEmpty(collectedMod.SourceURL) ? "removed" :
                                "changed";

                changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "source url " + change;

                // Remove 'source unavailable' status if a source url was added
                if (change == "added" && catalogMod.Statuses.Contains(Enums.ModStatus.SourceUnavailable))
                {
                    catalogMod.Statuses.Remove(Enums.ModStatus.SourceUnavailable);

                    changes += " ('source unavailable' status removed)";
                }

                catalogMod.Update(sourceURL: collectedMod.SourceURL);
            }

            // Compatible game version (only if details for this mod were checked)
            if (catalogMod.CompatibleGameVersionString != collectedMod.CompatibleGameVersionString && detailedUpdate)
            {
                string unknown = GameVersion.Unknown.ToString();

                // Added, removed or changed
                string change = catalogMod.CompatibleGameVersionString == unknown && collectedMod.CompatibleGameVersionString != unknown ? "added" :
                                catalogMod.CompatibleGameVersionString != unknown && collectedMod.CompatibleGameVersionString == unknown ? "removed" :
                                "changed";

                changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "compatible game version tag " + change;

                catalogMod.Update(compatibleGameVersionString: collectedMod.CompatibleGameVersionString);
            }

            // Required DLC (only if details for this mod were checked)
            if (catalogMod.RequiredDLC.Count + collectedMod.RequiredDLC.Count != 0 && detailedUpdate)
            {
                // Add new required dlc
                foreach (Enums.DLC dlc in collectedMod.RequiredDLC)
                {
                    if (!catalogMod.RequiredDLC.Contains(dlc))
                    {
                        catalogMod.RequiredDLC.Add(dlc);

                        changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + $"required dlc { dlc } added";
                    }
                }

                // We need to collect the required dlcs because we can't remove them inside the foreach loop
                List<Enums.DLC> removals = new List<Enums.DLC>();

                // Find no longer required dlc
                foreach (Enums.DLC dlc in catalogMod.RequiredDLC)
                {
                    if (!collectedMod.RequiredDLC.Contains(dlc))
                    {
                        // Add the dlc to the removals list
                        removals.Add(dlc);

                        changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + $"required dlc { dlc } removed";
                    }
                }

                // Remove the no longer required dlc
                foreach (Enums.DLC dlc in removals)
                {
                    catalogMod.RequiredDLC.Remove(dlc);
                }
            }

            // Required mods (only if details for this mod were checked)  [Todo 0.4] simplify (or split) this
            if (catalogMod.RequiredMods.Count + collectedMod.RequiredMods.Count != 0 && detailedUpdate)
            {
                // We need to collect the required mods and groups because we can't remove them inside the foreach loop
                List<ulong> removals = new List<ulong>();

                // Find no longer needed mods and groups from the required list
                foreach (ulong requiredID in catalogMod.RequiredMods)
                {
                    // Check if it's a mod or a group
                    if (requiredID >= ModSettings.lowestGroupID && requiredID <= ModSettings.highestGroupID)
                    {
                        // ID is a group; check if this is still required
                        bool stillRequired = false;

                        foreach (ulong modID in ActiveCatalog.Instance.GroupDictionary[requiredID].GroupMembers)
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
                            removals.Add(requiredID);

                            changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + $"required group { requiredID } removed";
                        }
                    }
                    else if (ActiveCatalog.Instance.Groups.Find(x => x.GroupMembers.Contains(requiredID)) != null)
                    {
                        // ID is a mod that is a group member, so remove it; the group will be added below if still needed
                        removals.Add(requiredID);

                        changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + $"required mod { requiredID } removed";
                    }
                    else if (!collectedMod.RequiredMods.Contains(requiredID))
                    {
                        // ID is a mod that is not in any group, and it's not required anymore, so remove it
                        removals.Add(requiredID);

                        changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + $"required mod { requiredID } removed";
                    }
                }

                // Remove the no longer required mods and groups
                foreach (ulong requiredID in removals)
                {
                    catalogMod.RequiredMods.Remove(requiredID);
                }

                // Add new required mods
                foreach (ulong requiredModID in collectedMod.RequiredMods)
                {
                    // Add the required mod to the catalog mod's required list, if it isn't there already
                    if (!catalogMod.RequiredMods.Contains(requiredModID))
                    {
                        catalogMod.RequiredMods.Add(requiredModID);

                        // Replace the required mod by its group, if it is a group member
                        if (ActiveCatalog.Instance.IsGroupMember(requiredModID))
                        {
                            ActiveCatalog.Instance.AddRequiredGroupForAllMods(requiredModID);

                            changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + $"required group { ActiveCatalog.Instance.GetGroup(requiredModID).GroupID } added";
                        }
                        else
                        {
                            changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + $"required mod { requiredModID } added";
                        }
                    }
                }
            }

            // Add new Stability and Statuses: incompatible, no description*, unlisted in workshop, removed from workshop (* = only if details for this mod were checked)
            if (collectedMod.Statuses.Count > 0)
            {
                if (collectedMod.Stability == Enums.ModStability.IncompatibleAccordingToWorkshop &&
                    catalogMod.Stability != Enums.ModStability.IncompatibleAccordingToWorkshop)
                {
                    catalogMod.Update(stability: Enums.ModStability.IncompatibleAccordingToWorkshop);

                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "Stability changed to Incompatible";
                }

                if (collectedMod.Statuses.Contains(Enums.ModStatus.NoDescription) &&
                    !catalogMod.Statuses.Contains(Enums.ModStatus.NoDescription) && detailedUpdate)
                {
                    catalogMod.Statuses.Add(Enums.ModStatus.NoDescription);

                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "NoDescription status added";
                }

                if (collectedMod.Statuses.Contains(Enums.ModStatus.UnlistedInWorkshop) &&
                    !catalogMod.Statuses.Contains(Enums.ModStatus.UnlistedInWorkshop))
                {
                    catalogMod.Statuses.Add(Enums.ModStatus.UnlistedInWorkshop);

                    // Remove removed status, if needed
                    catalogMod.Statuses.Remove(Enums.ModStatus.RemovedFromWorkshop);

                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "UnlistedInWorkshop status added";
                }

                if (collectedMod.Statuses.Contains(Enums.ModStatus.RemovedFromWorkshop) &&
                    !catalogMod.Statuses.Contains(Enums.ModStatus.RemovedFromWorkshop))
                {
                    catalogMod.Statuses.Add(Enums.ModStatus.RemovedFromWorkshop);

                    // Remove unlisted status, if needed
                    catalogMod.Statuses.Remove(Enums.ModStatus.UnlistedInWorkshop);

                    // Gives this its own line in the change notes
                    changeNotesRemovedMods.AppendLine($"Mod { catalogMod.ToString() }: removed from the workshop");
                }
            }

            // Remove Statuses: incompatible, no description*, unlisted in workshop, removed from workshop (* = only if details for this mod were checked)
            if (catalogMod.Statuses.Count > 0)
            {
                if (catalogMod.Stability == Enums.ModStability.IncompatibleAccordingToWorkshop &&
                    collectedMod.Stability != Enums.ModStability.IncompatibleAccordingToWorkshop)
                {
                    catalogMod.Update(stability: collectedMod.Stability);

                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "No longer IncompatibleAccordingToWorkshop";
                }

                if (catalogMod.Statuses.Contains(Enums.ModStatus.NoDescription) &&
                    !collectedMod.Statuses.Contains(Enums.ModStatus.NoDescription) && detailedUpdate)
                {
                    catalogMod.Statuses.Remove(Enums.ModStatus.NoDescription);

                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "NoDescription status removed";
                }

                if (catalogMod.Statuses.Contains(Enums.ModStatus.UnlistedInWorkshop) &&
                    !collectedMod.Statuses.Contains(Enums.ModStatus.UnlistedInWorkshop))
                {
                    catalogMod.Statuses.Remove(Enums.ModStatus.UnlistedInWorkshop);

                    changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "UnlistedInWorkshop status removed";
                }

                if (catalogMod.Statuses.Contains(Enums.ModStatus.RemovedFromWorkshop) &&
                    !collectedMod.Statuses.Contains(Enums.ModStatus.RemovedFromWorkshop))
                {
                    catalogMod.Statuses.Remove(Enums.ModStatus.RemovedFromWorkshop);

                    // Gives this its own line in the change notes
                    changeNotesNewMods.AppendLine($"Mod { catalogMod.ToString() }: reappeared on the workshop after being removed previously");
                }
            }

            // Review update dates and change notes
            if (!string.IsNullOrEmpty(changes))
            {
                // If the ReviewUpdated fields were filled, set the date to the local variable reviewUpdateDate, so the datetime will be the same for all mods in this update
                DateTime? modReviewUpdated = null;
                modReviewUpdated = collectedMod.ReviewDate == default ? modReviewUpdated : reviewDate;

                DateTime? modAutoReviewUpdated = null;
                modAutoReviewUpdated = collectedMod.AutoReviewDate == default ? modAutoReviewUpdated : catalogDate;

                catalogMod.Update(reviewDate: modReviewUpdated, autoReviewDate: modAutoReviewUpdated, extraChangeNote: $"{ catalogDateString }: { changes }");

                AddUpdatedModChangeNote(catalogMod, changes);

                // [Todo 0.3] changeNotesUpdatedMods.AppendLine($"Mod { catalogMod.ToString() }: { changes }");
            }
        }


        // Update changed info for an author (profile ID and custom URL changes are updated together with mod updates)
        private static void UpdateAuthor(Author catalogAuthor, Author collectedAuthor)
        {
            string changes = "";

            // Name
            if ((catalogAuthor.Name != collectedAuthor.Name) && !string.IsNullOrEmpty(collectedAuthor.Name))
            {
                catalogAuthor.Update(name: collectedAuthor.Name);

                changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "name changed";
            }

            // Last seen. Only updated if last seen is newer, or if last seen was manually updated
            if (catalogAuthor.LastSeen < collectedAuthor.LastSeen || (catalogAuthor.LastSeen != collectedAuthor.LastSeen && collectedAuthor.UpdatedByImporter))
            {
                catalogAuthor.Update(lastSeen: collectedAuthor.LastSeen);

                changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "last seen date updated";
            }

            // Update the collected author's retired status if no exclusion exists or if the last seen date was over a year ago
            if (!collectedAuthor.ExclusionForRetired || collectedAuthor.LastSeen.AddMonths(ModSettings.monthsOfInactivityToRetireAuthor) < DateTime.Today)
            {
                collectedAuthor.Update(retired: collectedAuthor.LastSeen.AddMonths(ModSettings.monthsOfInactivityToRetireAuthor) < DateTime.Today);
            }

            // Retired
            if (!catalogAuthor.Retired && collectedAuthor.Retired)
            {
                catalogAuthor.Update(retired: true, exclusionForRetired: collectedAuthor.ExclusionForRetired);

                changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "set as retired";
            }
            // No longer retired, only on manual updates and when no exclusion existed
            else if (catalogAuthor.Retired && !catalogAuthor.ExclusionForRetired && !collectedAuthor.Retired && collectedAuthor.UpdatedByImporter)
            {
                catalogAuthor.Update(retired: false);

                changes += (string.IsNullOrEmpty(changes) ? "" : ", ") + "no longer retired";
            }

            // Change notes
            if (!string.IsNullOrEmpty(changes))
            {
                catalogAuthor.Update(extraChangeNote: $"{ catalogDateString }: { changes }");

                changeNotesUpdatedAuthors.AppendLine($"Author { catalogAuthor.ToString() }: " + changes);
            }
        }
*/
    }
}
