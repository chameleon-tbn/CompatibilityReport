using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ColossalFramework.PlatformServices;
using CompatibilityReport.CatalogData;
using CompatibilityReport.Util;

namespace CompatibilityReport.Reporter
{
    class TextReport
    {
        // Strings to collect the review text for all mods
        private static StringBuilder reviewedModsText = new StringBuilder();

        private static StringBuilder nonReviewedModsText = new StringBuilder();

        // Catalog instance to store the active catalog reference
        private static Catalog ActiveCatalog;


        internal static bool Create(Catalog activeCatalog)
        {
            ActiveCatalog = activeCatalog;

            StringBuilder TextReport = new StringBuilder(512);

            DateTime reportCreationTime = DateTime.Now;

            TextReport.AppendLine(Toolkit.WordWrap($"{ ModSettings.modName }, created on { reportCreationTime:D}, { reportCreationTime:t}.\n"));

            TextReport.AppendLine(Toolkit.WordWrap($"Version { ModSettings.shortVersion } with catalog { ActiveCatalog.VersionString() }. " +
                $"The catalog contains { ActiveCatalog.ReviewedModCount } reviewed mods and " +
                $"{ ActiveCatalog.ModCount - ActiveCatalog.ReviewedModCount } mods with basic information. " +
                $"Your game has { Report.AllSubscriptions.Count } mods.\n"));

            if (!string.IsNullOrEmpty(ActiveCatalog.Note))
            {
                TextReport.AppendLine(Toolkit.WordWrap(ActiveCatalog.Note) + "\n");
            }

            if (Toolkit.CurrentGameVersion != ActiveCatalog.CompatibleGameVersion)
            {
                TextReport.AppendLine(Toolkit.WordWrap($"WARNING: The review catalog is made for game version " +
                    Toolkit.ConvertGameVersionToString(ActiveCatalog.CompatibleGameVersion) +
                    $". Your game is { (Toolkit.CurrentGameVersion < ActiveCatalog.CompatibleGameVersion ? "older" : "newer") }. Results may not be accurate.\n", 
                    indent: new string(' ', "WARNING: ".Length)));
            }

            TextReport.AppendLine(ModSettings.separatorDouble + "\n");

            TextReport.AppendLine(Toolkit.WordWrap(string.IsNullOrEmpty(ActiveCatalog.ReportHeaderText) ? ModSettings.defaultHeaderText : ActiveCatalog.ReportHeaderText, 
                indent: ModSettings.noBullet, indentAfterNewLine: "") + "\n");

            uint modsWithOnlyRemarks = 0;

            // Gather all mod detail texts
            if (ModSettings.ReportSortByName)
            {
                // Sorted by name
                foreach (string name in Report.AllSubscriptionNames)
                {
                    // Get the Steam ID(s) for this mod name; could be multiple (which will be sorted by Steam ID)
                    foreach (ulong steamID in Report.AllSubscriptionNamesAndIDs[name])
                    {
                        // Get the mod text, and increase the counter if it was a mod without review but with remarks
                        if (GetModText(steamID, nameFirst: true))
                        {
                            modsWithOnlyRemarks++;
                        }
                    }
                }
            }
            else
            {
                // Sorted by Steam ID
                foreach (ulong steamID in Report.AllSubscriptionSteamIDs)
                {
                    // Get the mod text, and increase the counter if it was a mod without review but with remarks
                    if (GetModText(steamID, nameFirst: false))
                    {
                        modsWithOnlyRemarks++;
                    }
                }
            }

            // Log detail of reviewed mods and other mods with issues
            if (reviewedModsText.Length > 0)
            {
                TextReport.AppendLine(ModSettings.separatorDouble);

                TextReport.AppendLine($"REVIEWED MODS ({ Report.TotalReviewedSubscriptions })" +
                    (modsWithOnlyRemarks == 0 ? ":" : $" AND OTHER MODS WITH REMARKS ({ modsWithOnlyRemarks }): "));

                TextReport.AppendLine(reviewedModsText.ToString());
            }

            // Log details of non-reviewed mods
            if (nonReviewedModsText.Length > 0)
            {
                TextReport.AppendLine(ModSettings.separatorDouble);

                TextReport.AppendLine($"MODS NOT REVIEWED YET ({ Report.AllSubscriptions.Count - Report.TotalReviewedSubscriptions - modsWithOnlyRemarks }):");

                TextReport.AppendLine(nonReviewedModsText.ToString());
            }

            TextReport.AppendLine(ModSettings.separatorDouble + "\n");

            TextReport.AppendLine(Toolkit.WordWrap(string.IsNullOrEmpty(ActiveCatalog.ReportFooterText) ? ModSettings.defaultFooterText :
                ActiveCatalog.ReportFooterText));

            Toolkit.SaveToFile(TextReport.ToString(), ModSettings.ReportTextFullPath, createBackup: true);

            // Log the report location
            Logger.Log($"Text report ready at \"{ Toolkit.PrivacyPath(ModSettings.ReportTextFullPath) }\".", duplicateToGameLog: true);

            // Clean up memory
            ActiveCatalog = null;

            reviewedModsText = new StringBuilder();

            nonReviewedModsText = new StringBuilder();

            return true;
        }


        // Get report text for one mod; not reported: SourceURL, Updated, Downloaded
        // Return value indicates whether we found a mod without a review in the catalog, but with remarks to report
        private static bool GetModText(ulong steamID, bool nameFirst)
        {
            // Exit if the Steam ID is 0 (meaning we ran out of fake IDs for local or builtin mods)
            if (steamID == 0)
            {
                return false;
            }

            // Get the mod
            Subscription subscription = Report.AllSubscriptions[steamID];

            // Start with a separator
            string modHeader = ModSettings.separator + "\n\n";

            // Mod name and Steam ID
            string modName = subscription.ToString(nameFirst, hideFakeID: true);

            // Authorname
            if (string.IsNullOrEmpty(subscription.AuthorName))
            {
                // Author unknown
                modHeader += modName + "\n";
            }
            else
            {
                if (modName.Length + 4 + subscription.AuthorName.Length <= ModSettings.ReportWidth)
                {
                    // Author on the same line as mod name and Steam ID
                    modHeader += modName + " by " + subscription.AuthorName + "\n";
                }
                else
                {
                    // Author right aligned on a new line under mod name and Steam ID
                    modHeader += modName + "\n" + $"by { subscription.AuthorName }".PadLeft(ModSettings.ReportWidth) + "\n";
                }
            }

            // Gather the review text; [Todo 0.4] Rethink which review texts to include in 'somethingToReport'; combine author retired and mod abandoned into one line
            StringBuilder modReview = new StringBuilder();

            modReview.Append(ThisMod(subscription));
            modReview.Append(Stability(subscription));
            modReview.Append(StabilityNote(subscription));
            modReview.Append(RequiredDLC(subscription));
            modReview.Append(RequiredMods(subscription));
            modReview.Append(Compatibilities(subscription));
            modReview.Append(Statuses(subscription));
            modReview.Append(DependencyMod(subscription));
            modReview.Append(Disabled(subscription));
            modReview.Append(RetiredAuthor(subscription));
            modReview.Append(GameVersionCompatible(subscription));
            modReview.Append(Successors(subscription));
            modReview.Append(Alternatives(subscription));
            modReview.Append(CameraScript(subscription));
            modReview.Append(GenericNote(subscription));

            // If the review text is not empty, then we found something to report
            bool somethingToReport = modReview.Length > 0;

            // Insert the 'not reviewed' text at the start of the text; we do this after the above boolean has been set
            modReview.Insert(0, NotReviewed(subscription));

            // Report that we didn't find any incompatibilities [Todo 0.4] We should keep better track of issues; now this text doesn't show if the author is retired
            if (modReview.Length == 0)
            {
                modReview.Append(ReviewLine("No known issues or incompatibilities with your other mods."));
            }

            // Workshop url for Workshop mods
            modReview.Append((steamID > ModSettings.highestFakeID) ? ReviewLine("Steam Workshop page: " + Toolkit.GetWorkshopURL(steamID)) : "");

            // Add the text for this subscription to the reviewed or nonreviewed text
            if (subscription.IsReviewed)
            {
                // Mod with a review in the catalog
                reviewedModsText.AppendLine(modHeader + modReview);
            }
            else if (somethingToReport)
            {
                // Mod without a review in the catalog, but with a remark anyway
                reviewedModsText.AppendLine(modHeader + modReview);
            }
            else
            {
                // Mod without review in the catalog and without a remark
                nonReviewedModsText.AppendLine(modHeader + modReview);
            }

            // Indicate whether we found a mod without a review in the catalog, but with remarks to report
            return !subscription.IsReviewed && somethingToReport;
        }


        // Format one line for the text or html review; including bullets, indenting and max. width; [Todo 0.4] Change cutoff = true to a false default value [Todo 1.1] Change for html with unordered list for bullets, etc.
        private static string ReviewLine(string message, string bullet = null, bool cutOff = false)
        {
            if (string.IsNullOrEmpty(message))
            {
                return "";
            }

            bullet = bullet ?? ModSettings.bullet;

            if (bullet.Length + message.Length > ModSettings.ReportWidth)
            {
                if (cutOff)
                {
                    // Cut off the message, so the 'bulleted' message stays within maximum width
                    message = message.Substring(0, ModSettings.ReportWidth - bullet.Length - 3) + "...";

                    Logger.Log("Report line too long: " + message, Logger.debug);
                }
                else
                {
                    // Word wrap the message
                    message = Toolkit.WordWrap(message, ModSettings.ReportWidth - bullet.Length, indent: new string(' ', bullet.Length));
                }
            }

            // Return 'bulleted' message
            return bullet + message + "\n";
        }


        // Say hi to ourselves
        private static string ThisMod(Subscription subscription)
        {
            if (subscription.SteamID != ModSettings.OurOwnSteamID)
            {
                return "";
            }

            return ReviewLine("This mod.");
        }


        // Not reviewed; local mods get a slightly different text
        private static string NotReviewed(Subscription subscription)
        {
            if (subscription.IsReviewed)
            {
                return "";
            }

            return subscription.IsLocal && !subscription.IsBuiltin ? ReviewLine("Can't review local mods (yet).") : ReviewLine("Not reviewed yet.");
        }


        // Cinematic camera script
        private static string CameraScript(Subscription subscription)
        {
            if (!subscription.IsCameraScript)
            {
                return "";
            }

            return ReviewLine("This is a cinematic camera script, which technically is a mod and thus listed here.");
        }


        // Generic note for this mod
        private static string GenericNote(Subscription subscription)
        {
            return ReviewLine(subscription.GenericNote);
        }


        // Game version compatible, only listed for current version
        private static string GameVersionCompatible(Subscription subscription)
        {
            if (subscription.GameVersionCompatible < Toolkit.CurrentGameMajorVersion)
            {
                return "";
            }

            string currentOrNot = subscription.GameVersionCompatible == Toolkit.CurrentGameVersion ? "current " : "";

            return ReviewLine($"Created or updated for { currentOrNot }game version { Toolkit.ConvertGameVersionToString(Toolkit.CurrentGameVersion) }. " +
                "Less likely to have issues.");
        }


        // Disabled mod
        private static string Disabled(Subscription subscription)
        {
            if (subscription.IsEnabled)
            {
                return "";
            }

            return ReviewLine("Mod is disabled. Unsubscribe it if not used. Disabled mods can still cause issues.");
        }


        // Author is retired
        private static string RetiredAuthor(Subscription subscription)
        {
            if (!subscription.AuthorIsRetired)
            {
                return "";
            }

            return ReviewLine("The author seems to be retired. Updates are unlikely.");
        }


        // Unneeded dependency mod      [Todo 0.4] Add remark if we have local mods
        private static string DependencyMod(Subscription subscription)
        {
            if (!subscription.Statuses.Contains(Enums.ModStatus.DependencyMod))
            {
                return "";
            }

            // Check if any of the mods that need this is actually subscribed; we don't care if it's enabled or not
            List<Mod> ModsRequiringThis = ActiveCatalog.Mods.FindAll(x => x.RequiredMods.Contains(subscription.SteamID));

            // Check the same again for a group this is a member of and add it to the above list
            Group group = ActiveCatalog.GetGroup(subscription.SteamID);

            if (group != default)
            {
                ModsRequiringThis.AddRange(ActiveCatalog.Mods.FindAll(x => x.RequiredMods.Contains(group.GroupID)));
            }

            // Check if any of these mods is subscribed
            foreach (Mod mod in ModsRequiringThis)
            {
                if (Report.AllSubscriptions.ContainsKey(mod.SteamID))
                {
                    // Found a subscribed mod that needs this; nothing to report
                    return "";
                }
            }

            return ReviewLine("You can probably unsubscribe. This is only needed for mods you don't seem to have.");
        }


        // Successor(s)
        private static string Successors(Subscription subscription)
        {
            if (subscription.Successors?.Any() != true)
            {
                return "";
            }

            string text = (subscription.Successors.Count == 1) ? ReviewLine("This is succeeded by:") :
                ReviewLine("This is succeeded by any of the following (pick one, not all):");

            // List all successor mods
            foreach (ulong id in subscription.Successors)
            {
                if (ActiveCatalog.ModDictionary.ContainsKey(id))
                {
                    // Mod found in the catalog, list Steam ID and name
                    text += ReviewLine(ActiveCatalog.ModDictionary[id].ToString(hideFakeID: true), ModSettings.bullet2, cutOff: true);
                }
                else
                {
                    // Mod not found in the catalog, which should not happen unless manually editing the catalog
                    text += ReviewLine($"[Steam ID { id,10 }] { subscription.Name }", ModSettings.bullet2, cutOff: true);

                    Logger.Log($"Successor mod { id } not found in catalog.", Logger.warning);
                }
            }

            return text;
        }


        // Alternative mods
        private static string Alternatives(Subscription subscription)
        {
            if (subscription.Alternatives?.Any() != true)
            {
                return "";
            }

            string text = subscription.Alternatives.Count == 1 ? ReviewLine("An alternative you could use:") :
                ReviewLine("Some alternatives for this are (pick one, not all):");

            // List all alternative mods
            foreach (ulong id in subscription.Alternatives)
            {
                if (ActiveCatalog.ModDictionary.ContainsKey(id))
                {
                    // Mod found in the catalog, list Steam ID and name
                    text += ReviewLine(ActiveCatalog.ModDictionary[id].ToString(hideFakeID: true), ModSettings.bullet2, cutOff: true);
                }
                else
                {
                    // Mod not found in the catalog, which should not happen unless manually editing the catalog
                    text += ReviewLine($"[Steam ID { id,10 }] { subscription.Name }", ModSettings.bullet2, cutOff: true);

                    Logger.Log($"Alternative mod { id } not found in catalog.", Logger.warning);
                }
            }

            return text;
        }


        // Required DLC
        private static string RequiredDLC(Subscription subscription)
        {
            if (subscription.RequiredDLC?.Any() != true)
            {
                return "";
            }

            string dlcs = "";

            // Check every required DLC against installed DLC
            foreach (Enums.DLC dlc in subscription.RequiredDLC)
            {
                if (!PlatformService.IsDlcInstalled((uint)dlc))
                {
                    // Add the missing dlc, replacing the underscores in the DLC enum name with spaces and semicolons
                    dlcs += ReviewLine(Toolkit.ConvertDLCtoString(dlc), ModSettings.bullet2);
                }
            }

            if (string.IsNullOrEmpty(dlcs))
            {
                return "";
            }

            return ReviewLine("Unsubscribe. This requires DLC you don't have:") + dlcs;
        }


        // Required mods, including the use of groups
        private static string RequiredMods(Subscription subscription)
        {
            if (subscription.RequiredMods?.Any() != true)
            {
                return "";
            }

            // Strings to collect the text
            string header = ReviewLine("This mod requires other mods you don't have, or which are not enabled:");
            string text = "";

            // Check every required mod
            foreach (ulong id in subscription.RequiredMods)
            {
                // Check if it's a regular mod or a group
                if ((id < ModSettings.lowestGroupID) || (id > ModSettings.highestGroupID))
                {
                    // Regular mod. Try to find it in the list of subscribed mods
                    if (Report.AllSubscriptions.ContainsKey(id))
                    {
                        // Mod is subscribed
                        if (!Report.AllSubscriptions[id].IsEnabled)
                        {
                            // Mod is subscribed, but not enabled
                            text += ReviewLine(Report.AllSubscriptions[id].ToString(hideFakeID: true), ModSettings.bullet2, cutOff: true);
                        }
                        else
                        {
                            // Enabled mod, nothing to report
                        }

                        continue;   // To the next required mod
                    }
                    else
                    {
                        // Mod is not subscribed, try to find it in the catalog
                        if (ActiveCatalog.ModDictionary.ContainsKey(id))
                        {
                            // Mod found in the catalog
                            text += ReviewLine(ActiveCatalog.ModDictionary[id].ToString(hideFakeID: true), ModSettings.bullet2, cutOff: true);
                        }
                        else
                        {
                            // Mod not found in the catalog, which should not happen unless manually editing the catalog
                            text += ReviewLine($"[Steam ID { id,10 }] { subscription.Name }", ModSettings.bullet2, cutOff: true);

                            Logger.Log($"Required mod { id } not found in catalog.", Logger.warning);
                        }

                        // List the workshop page for easy subscribing
                        text += ReviewLine("Workshop page: " + Toolkit.GetWorkshopURL(id), ModSettings.noBullet2);

                        continue;   // To the next required mod
                    }
                }
                else
                {
                    // Group. We have to dig a little deeper. First some error checks
                    if (!ActiveCatalog.GroupDictionary.ContainsKey(id))
                    {
                        // Group not found in catalog, which should not happen unless manually editing the catalog
                        text += ReviewLine("one of the following mods: <missing information in catalog>", ModSettings.bullet2);

                        Logger.Log($"Group { id } not found in catalog.", Logger.error);

                        continue;   // To the next required mod
                    }
                    else if (ActiveCatalog.GroupDictionary[id].GroupMembers?.Any() != true)
                    {
                        // Group contains no Steam IDs, which should not happen unless manually editing the catalog
                        text += ReviewLine("one of the following mods: <missing information in catalog>", ModSettings.bullet2);

                        Logger.Log($"Group { id } is empty in catalog.", Logger.error);

                        continue;   // To the next required mod
                    }

                    // Get the group from the catalog
                    Group group = ActiveCatalog.GroupDictionary[id];

                    // Some vars to keep track of all mods in the group, and check if at least one group member is subscribed and enabled
                    uint subscriptionsFound = 0;
                    bool EnabledSubscriptionFound = false;
                    string disabledModsText = "";
                    string missingModsText = "";

                    // Check each mod in the group, and see if they are subscribed and enabled
                    foreach (ulong modID in group.GroupMembers)
                    {
                        if (Report.AllSubscriptions.ContainsKey(modID))
                        {
                            // Mod is subscribed
                            subscriptionsFound++;

                            if (Report.AllSubscriptions[modID].IsEnabled)
                            {
                                // Enabled mod found, no need to look any further in this group
                                EnabledSubscriptionFound = true;

                                break;   // out of the group member foreach
                            }
                            else
                            {
                                // Disabled mod
                                disabledModsText += ReviewLine(Report.AllSubscriptions[modID].ToString(hideFakeID: true), ModSettings.bullet3, cutOff: true);
                            }
                        }
                        else
                        {
                            // Mod is not subscribed, find it in the catalog
                            if (ActiveCatalog.ModDictionary.ContainsKey(modID))
                            {
                                // Mod found in the catalog
                                missingModsText += ReviewLine(ActiveCatalog.ModDictionary[modID].ToString(hideFakeID: true), ModSettings.bullet3, cutOff: true);
                            }
                            else
                            {
                                // Mod not found in the catalog
                                missingModsText += ReviewLine($"[Steam ID { modID,10 }] { subscription.Name }", ModSettings.bullet3, cutOff: true);

                                Logger.Log($"Mod { modID } from group { id } not found in catalog.", Logger.warning);
                            }
                        }
                    }

                    // If a group member is subscribed and enabled, then there is nothing to report
                    if (EnabledSubscriptionFound)
                    {
                        continue;   // To the next required mod
                    }

                    // Time to list the group members
                    if (subscriptionsFound == 0)
                    {
                        // None of the group members is subscribed; this will look weird if the group only has one member, but that shouldn't happen anyway
                        text += ReviewLine("one of the following mods:", ModSettings.bullet2);
                        text += missingModsText;
                    }
                    else if (subscriptionsFound == 1)
                    {
                        // One mod is subscribed but disabled; use the 'disabledText', first stripped from bullet3 and the line end
                        int indent = disabledModsText.IndexOf('[');

                        text += ReviewLine(disabledModsText.Substring(indent).Replace('\n', ' '), ModSettings.bullet2);
                    }
                    else
                    {
                        // More than one mod subscribed, but not enabled
                        text += ReviewLine("one of the following mods should be enabled:", ModSettings.bullet2);
                        text += disabledModsText;
                    }
                }
            }

            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            return header + text;
        }


        // Mod stability
        private static string Stability(Subscription subscription)
        {
            switch (subscription.Stability)
            {
                case Enums.ModStability.IncompatibleAccordingToWorkshop:
                    return ReviewLine("UNSUBSCRIBE! This is totally incompatible with the current game version.");

                case Enums.ModStability.RequiresIncompatibleMod:
                    return ReviewLine("UNSUBSCRIBE! This requires a mod that is totally incompatible with the current game version.");

                case Enums.ModStability.GameBreaking:
                    return ReviewLine("UNSUBSCRIBE! This breaks the game.");

                case Enums.ModStability.Broken:
                    return ReviewLine("Unsubscribe! This mod is broken.");

                case Enums.ModStability.MajorIssues:
                    return ReviewLine("Unsubscribe would be wise. This has major issues.");

                case Enums.ModStability.MinorIssues:
                    return ReviewLine("This has minor issues. Check its Workshop page for details.");

                case Enums.ModStability.UsersReportIssues:
                    return ReviewLine("Stability is uncertain. Some users are reporting issues. Check its Workshop page for details.");

                case Enums.ModStability.Stable:
                    return ReviewLine("This should be compatible with the current game version.");

                case Enums.ModStability.NotEnoughInformation:
                    return ReviewLine("There is not enough information about this mod to know if it is compatible with the current game version.");

                case Enums.ModStability.NotReviewed:
                default:
                    return "";
            }
        }


        // Stability note for this mod
        private static string StabilityNote(Subscription subscription)
        {
            return ReviewLine(subscription.StabilityNote);
        }


        // Mod statuses; not reported: UsersReportIssues, UnlistedInWorkshop, SourceBundled, SourceObfuscated, and more  [Todo 0.4] add all statuses
        private static string Statuses(Subscription subscription)
        {
            if (subscription.Statuses?.Any() != true)
            {
                return "";
            }

            string text = "";

            // Obsolete
            if (subscription.Statuses.Contains(Enums.ModStatus.NoLongerNeeded))
            {
                text += ReviewLine("Unsubscribe. This is no longer needed.");
            }
            else if (subscription.Statuses.Contains(Enums.ModStatus.Reupload))
            {
                text += ReviewLine("Unsubscribe. This is a re-upload of another mod, use that one instead.");
            }

            if (subscription.Stability != Enums.ModStability.IncompatibleAccordingToWorkshop && subscription.Stability != Enums.ModStability.RequiresIncompatibleMod &&
                subscription.Stability != Enums.ModStability.GameBreaking && subscription.Stability != Enums.ModStability.Broken)
            {
                // Several statuses only listed if there are no gamebreaking issues

                // Abandoned
                if (subscription.Statuses.Contains(Enums.ModStatus.Abandoned))
                {
                    text += ReviewLine("This seems to be abandoned and probably won't be updated anymore.");
                }

                // Editors
                if (subscription.Statuses.Contains(Enums.ModStatus.BreaksEditors))
                {
                    text += ReviewLine("This gives major issues in the asset editor and/or map editor.");
                }
            }

            // Several statuses listed even with gamebreaking issues

            // Removed from the Steam Workshop
            if (subscription.Statuses.Contains(Enums.ModStatus.RemovedFromWorkshop))
            {
                text += ReviewLine("Unsubscribe is wise. This is no longer available on the Workshop.");
            }

            // Savegame affecting
            if (subscription.Statuses.Contains(Enums.ModStatus.SavesCantLoadWithout))
            {
                text += ReviewLine("Caution. After using this mod, savegames won't easily load without it anymore.");
            }

            // Source code
            if (subscription.Statuses.Contains(Enums.ModStatus.SourceUnavailable))
            {
                text += ReviewLine("No public source code found, making it hard to continue if this gets abandoned.");
            }
            else if (subscription.Statuses.Contains(Enums.ModStatus.SourceNotUpdated))
            {
                text += ReviewLine("Published source seems out of date, making it hard to continue if this gets abandoned.");
            }

            // Music
            if (subscription.Statuses.Contains(Enums.ModStatus.MusicCopyrightFree))
            {
                text += ReviewLine(Toolkit.WordWrap("The included music is said to be copyright-free and safe for streaming. " +
                    "Some restrictions might still apply though."));
            }
            else if (subscription.Statuses.Contains(Enums.ModStatus.MusicCopyrighted))
            {
                text += ReviewLine("This includes copyrighted music and should not be used for streaming.");
            }
            else if (subscription.Statuses.Contains(Enums.ModStatus.MusicCopyrightUnknown))
            {
                text += ReviewLine("This includes music with unknown copyright status. Safer not to use it for streaming.");
            }

            return text;
        }


        // Compatibilities with other mods; result could be multiple mods and also multiple statuses for each mod
        // Not reported: NewerVersionOfTheSameMod, FunctionalityCoveredByThisMod, RequiresSpecificConfigForOtherMod, CompatibleAccordingToAuthor
        private static string Compatibilities(Subscription subscription)
        {
            if (subscription.Compatibilities?.Any() != true)
            {
                return "";
            }

            string text = "";

            foreach (KeyValuePair<ulong, List<Enums.CompatibilityStatus>> compatibility in subscription.Compatibilities)
            {
                // Skip if not subscribed
                if (!Report.AllSubscriptions.ContainsKey(compatibility.Key))
                {
                    continue;   // To the next compatibility
                }

                // Get the list of compatibility statuses
                List<Enums.CompatibilityStatus> statuses = compatibility.Value;

                // Get a formatted text with the name of the other mod and the corresponding compatibility note
                string otherModText = ReviewLine(Report.AllSubscriptions[compatibility.Key].ToString(hideFakeID: true), ModSettings.bullet2, cutOff: true);

                if (subscription.ModNotes.ContainsKey(compatibility.Key))
                {
                    otherModText += ReviewLine(subscription.ModNotes[compatibility.Key], ModSettings.bullet3, cutOff: true);
                }

                // Different versions, releases or mod with the same functionality
                if (statuses.Contains(Enums.CompatibilityStatus.OlderVersion))
                {
                    text += ReviewLine("Unsubscribe. You're already subscribed to a newer version:") + otherModText;
                }
                else if (statuses.Contains(Enums.CompatibilityStatus.SameModDifferentReleaseType))
                {
                    text += ReviewLine("Unsubscribe either this one or the other release of the same mod:") + otherModText;
                }

                // Incompatible or minor issues
                if (statuses.Contains(Enums.CompatibilityStatus.IncompatibleAccordingToAuthor))
                {
                    text += ReviewLine("This is incompatible with (unsubscribe either one):") + otherModText;
                }
                else if (statuses.Contains(Enums.CompatibilityStatus.IncompatibleAccordingToUsers))
                {
                    text += ReviewLine("Said to be incompatible with (best to unsubscribe one):") + otherModText;
                }
                else if (statuses.Contains(Enums.CompatibilityStatus.MinorIssues))
                {
                    text += ReviewLine("This has reported issues with:") + otherModText;
                }

                // Specific config
                if (statuses.Contains(Enums.CompatibilityStatus.RequiresSpecificSettings))
                {
                    text += ReviewLine("This requires specific configuration to work together with:") + otherModText;
                }
            }

            return text;
        }
    }
}
