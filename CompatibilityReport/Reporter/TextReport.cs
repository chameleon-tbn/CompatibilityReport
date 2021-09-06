using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ColossalFramework.PlatformServices;
using CompatibilityReport.CatalogData;
using CompatibilityReport.Util;

namespace CompatibilityReport.Reporter
{
    internal static class TextReport
    {
        // Strings to collect the review text for all mods      [Todo 0.4] what to do with the static stringbuilders?
        private static StringBuilder reviewedModsText;

        private static StringBuilder nonReviewedModsText;


        internal static bool Create(Catalog catalog)
        {
            reviewedModsText = new StringBuilder();

            nonReviewedModsText = new StringBuilder();

            StringBuilder TextReport = new StringBuilder(512);

            DateTime reportCreationTime = DateTime.Now;

            TextReport.AppendLine(Toolkit.WordWrap($"{ ModSettings.modName }, created on { reportCreationTime:D}, { reportCreationTime:t}.\n"));

            TextReport.AppendLine(Toolkit.WordWrap($"Version { ModSettings.fullVersion } with catalog { catalog.VersionString() }. " +
                $"The catalog contains { catalog.ReviewedModCount } reviewed mods and " +
                $"{ catalog.Mods.Count - catalog.ReviewedModCount } mods with basic information. " +
                $"Your game has { catalog.SubscriptionIDIndex.Count } mods.\n"));

            if (!string.IsNullOrEmpty(catalog.Note))
            {
                TextReport.AppendLine(Toolkit.WordWrap(catalog.Note) + "\n");
            }

            if (Toolkit.CurrentGameVersion != catalog.GameVersion())
            {
                TextReport.AppendLine(Toolkit.WordWrap($"WARNING: The review catalog is made for game version " +
                    Toolkit.ConvertGameVersionToString(catalog.GameVersion()) +
                    $". Your game is { (Toolkit.CurrentGameVersion < catalog.GameVersion() ? "older" : "newer") }. Results may not be accurate.\n", 
                    indent: new string(' ', "WARNING: ".Length)));
            }

            TextReport.AppendLine(ModSettings.separatorDouble + "\n");

            TextReport.AppendLine(Toolkit.WordWrap(string.IsNullOrEmpty(catalog.ReportHeaderText) ? ModSettings.defaultHeaderText : catalog.ReportHeaderText, 
                indent: ModSettings.noBullet, indentAfterNewLine: "") + "\n");

            int modsWithOnlyRemarks = 0;

            // Gather all mod detail texts
            if (ModSettings.ReportSortByName)
            {
                // Sorted by name
                List<string> AllSubscriptionNames = new List<string>(catalog.SubscriptionNameIndex.Keys);

                AllSubscriptionNames.Sort();

                foreach (string name in AllSubscriptionNames)
                {
                    // Get the Steam ID(s) for this mod name; could be multiple (which will be sorted by Steam ID)
                    foreach (ulong steamID in catalog.SubscriptionNameIndex[name])
                    {
                        // Get the mod text, and increase the counter if it was a mod without review but with remarks
                        if (GetModText(catalog, steamID, nameFirst: true))
                        {
                            modsWithOnlyRemarks++;
                        }
                    }
                }
            }
            else
            {
                // Sorted by Steam ID
                catalog.SubscriptionIDIndex.Sort();

                foreach (ulong steamID in catalog.SubscriptionIDIndex)
                {
                    // Get the mod text, and increase the counter if it was a mod without review but with remarks
                    if (GetModText(catalog, steamID, nameFirst: false))
                    {
                        modsWithOnlyRemarks++;
                    }
                }
            }

            // Log detail of reviewed mods and other mods with issues, and free memory
            if (reviewedModsText.Length > 0)
            {
                TextReport.AppendLine(ModSettings.separatorDouble);

                TextReport.AppendLine($"REVIEWED MODS ({ catalog.ReviewedSubscriptionCount })" +
                    (modsWithOnlyRemarks == 0 ? ":" : $" AND OTHER MODS WITH REMARKS ({ modsWithOnlyRemarks }): "));

                TextReport.AppendLine(reviewedModsText.ToString());

                reviewedModsText = null;
            }

            // Log details of non-reviewed mods, and free memory
            if (nonReviewedModsText.Length > 0)
            {
                TextReport.AppendLine(ModSettings.separatorDouble);

                TextReport.AppendLine($"MODS NOT REVIEWED YET ({ catalog.SubscriptionIDIndex.Count - catalog.ReviewedSubscriptionCount - modsWithOnlyRemarks }):");

                TextReport.AppendLine(nonReviewedModsText.ToString());

                nonReviewedModsText = null;
            }

            TextReport.AppendLine(ModSettings.separatorDouble + "\n");

            TextReport.AppendLine(Toolkit.WordWrap(string.IsNullOrEmpty(catalog.ReportFooterText) ? ModSettings.defaultFooterText :
                catalog.ReportFooterText));

            bool reportCreated = Toolkit.SaveToFile(TextReport.ToString(), ModSettings.ReportTextFullPath, createBackup: true);

            if (reportCreated)
            {
                Logger.Log($"Text report ready at \"{ Toolkit.PrivacyPath(ModSettings.ReportTextFullPath) }\".", duplicateToGameLog: true);
            }
            else
            {
                Logger.Log("Text report could not be created.", Logger.error, duplicateToGameLog: true);    // [Todo 0.7] Try to save to default location if saving fails
            }

            return reportCreated;
        }


        // Get report text for one mod; not reported: SourceURL, Updated, Downloaded
        // Return value indicates whether we found a mod without a review in the catalog, but with remarks to report    [Todo 0.4] Change this logic
        private static bool GetModText(Catalog catalog, ulong steamID, bool nameFirst)
        {
            // Exit if the Steam ID is 0 (meaning we ran out of fake IDs for local or builtin mods)
            if (steamID == 0)
            {
                return false;
            }

            // Get the mod
            Mod subscribedMod = catalog.GetMod(steamID);

            Author subscriptionAuthor = catalog.GetAuthor(subscribedMod.AuthorID, subscribedMod.AuthorURL);

            string AuthorName = subscriptionAuthor == null ? "" : subscriptionAuthor.Name;

            // Start with a separator
            string modHeader = ModSettings.separator + "\n\n";

            // Mod name and Steam ID
            string modName = subscribedMod.ToString(hideFakeID: true, nameFirst);

            // Authorname
            if (string.IsNullOrEmpty(AuthorName))
            {
                // Author unknown
                modHeader += modName + "\n";
            }
            else
            {
                if (modName.Length + 4 + AuthorName.Length <= ModSettings.ReportWidth)
                {
                    // Author on the same line as mod name and Steam ID
                    modHeader += modName + " by " + AuthorName + "\n";
                }
                else
                {
                    // Author right aligned on a new line under mod name and Steam ID
                    modHeader += modName + "\n" + $"by { AuthorName }".PadLeft(ModSettings.ReportWidth) + "\n";
                }
            }

            // Gather the review text; [Todo 0.4] Rethink which review texts to include in 'somethingToReport'; combine author retired and mod abandoned into one line
            StringBuilder modReview = new StringBuilder();

            modReview.Append(ThisMod(subscribedMod));
            modReview.Append(Stability(subscribedMod));
            modReview.Append(RequiredDLC(subscribedMod));
            modReview.Append(RequiredMods(catalog, subscribedMod));
            modReview.Append(Compatibilities(catalog, subscribedMod));
            modReview.Append(Statuses(subscribedMod));
            modReview.Append(DependencyMod(catalog, subscribedMod));
            modReview.Append(Disabled(subscribedMod));
            modReview.Append(RetiredAuthor(subscriptionAuthor));
            modReview.Append(GameVersionCompatible(subscribedMod));
            modReview.Append(Successors(catalog, subscribedMod));
            modReview.Append(Alternatives(catalog, subscribedMod));
            // [Todo 0.4] Add recommendations
            modReview.Append(CameraScript(subscribedMod));
            modReview.Append(GenericNote(subscribedMod));

            // If the review text is not empty, then we found something to report
            bool somethingToReport = modReview.Length > 0;

            // Insert the 'not reviewed' text at the start of the text; we do this after the above boolean has been set
            modReview.Insert(0, NotReviewed(subscribedMod));

            // Report that we didn't find any incompatibilities [Todo 0.4] We should keep better track of issues; now this text doesn't show if the author is retired
            if (modReview.Length == 0)
            {
                modReview.Append(ReviewLine("No known issues or incompatibilities with your other mods."));
            }

            // Workshop url for Workshop mods
            modReview.Append((steamID > ModSettings.highestFakeID) ? ReviewLine("Steam Workshop page: " + Toolkit.GetWorkshopURL(steamID)) : "");

            // Add the text for this subscription to the reviewed or nonreviewed text
            if (subscribedMod.ReviewDate != default)
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
            return subscribedMod.ReviewDate == default && somethingToReport;
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
        private static string ThisMod(Mod subscribedMod)
        {
            if (subscribedMod.SteamID != ModSettings.OurOwnSteamID)
            {
                return "";
            }

            return ReviewLine("This mod.");
        }


        // Not reviewed; local mods get a slightly different text
        private static string NotReviewed(Mod subscribedMod)
        {
            if (subscribedMod.ReviewDate != default)
            {
                return "";
            }

            bool IsLocal = subscribedMod.SteamID >= ModSettings.lowestLocalModID && subscribedMod.SteamID <= ModSettings.highestLocalModID;

            return IsLocal ? ReviewLine("Can't review local mods (yet).") : ReviewLine("Not reviewed yet.");
        }


        // Cinematic camera script
        private static string CameraScript(Mod subscribedMod)
        {
            if (!subscribedMod.IsCameraScript)
            {
                return "";
            }

            return ReviewLine("This is a cinematic camera script, which technically is a mod and thus listed here.");
        }


        // Generic note for this mod
        private static string GenericNote(Mod subscribedMod)
        {
            return ReviewLine(subscribedMod.GenericNote);
        }


        // Game version compatible, only listed for current version
        private static string GameVersionCompatible(Mod subscribedMod)
        {
            Version modCompatibleGameVersion = Toolkit.ConvertToGameVersion(subscribedMod.CompatibleGameVersionString);

            if (modCompatibleGameVersion < Toolkit.CurrentGameMajorVersion)
            {
                return "";
            }

            string currentOrNot = modCompatibleGameVersion == Toolkit.CurrentGameVersion ? "current " : "";

            return ReviewLine($"Created or updated for { currentOrNot }game version { Toolkit.ConvertGameVersionToString(Toolkit.CurrentGameVersion) }. " +
                "Less likely to have issues.");
        }


        // Disabled mod
        private static string Disabled(Mod subscribedMod)
        {
            if (!subscribedMod.IsDisabled)
            {
                return "";
            }

            return ReviewLine("Mod is disabled. Unsubscribe it if not used. Disabled mods can still cause issues.");
        }


        // Author is retired
        private static string RetiredAuthor(Author subscriptionAuthor)
        {
            if (subscriptionAuthor == null || !subscriptionAuthor.Retired)
            {
                return "";
            }

            return ReviewLine("The author seems to be retired. Updates are unlikely.");
        }


        // Unneeded dependency mod      [Todo 0.4] Add remark if we have local mods
        private static string DependencyMod(Catalog catalog, Mod subscribedMod)
        {
            if (!subscribedMod.Statuses.Contains(Enums.ModStatus.DependencyMod))
            {
                return "";
            }

            // Check if any of the mods that need this is actually subscribed; we don't care if it's enabled or not
            List<Mod> ModsRequiringThis = catalog.Mods.FindAll(x => x.RequiredMods.Contains(subscribedMod.SteamID));

            // Check the same again for a group this is a member of and add it to the above list
            Group group = catalog.GetGroup(subscribedMod.SteamID);

            if (group != default)
            {
                ModsRequiringThis.AddRange(catalog.Mods.FindAll(x => x.RequiredMods.Contains(group.GroupID)));
            }

            // Check if any of these mods is subscribed
            foreach (Mod mod in ModsRequiringThis)
            {
                if (catalog.SubscriptionIDIndex.Contains(mod.SteamID))
                {
                    // Found a subscribed mod that needs this; nothing to report
                    return "";
                }
            }

            return ReviewLine("You can probably unsubscribe. This is only needed for mods you don't seem to have.");
        }


        // Successor(s)
        private static string Successors(Catalog catalog, Mod subscribedMod)
        {
            if (subscribedMod.Successors?.Any() != true)
            {
                return "";
            }

            string text = (subscribedMod.Successors.Count == 1) ? ReviewLine("This is succeeded by:") :
                ReviewLine("This is succeeded by any of the following (pick one, not all):");

            // List all successor mods
            foreach (ulong id in subscribedMod.Successors)
            {
                if (catalog.ModIndex.ContainsKey(id))
                {
                    // Mod found in the catalog, list Steam ID and name
                    text += ReviewLine(catalog.ModIndex[id].ToString(hideFakeID: true), ModSettings.bullet2, cutOff: true);
                }
                else
                {
                    // Mod not found in the catalog, which should not happen unless manually editing the catalog
                    text += ReviewLine($"[Steam ID { id,10 }] { subscribedMod.Name }", ModSettings.bullet2, cutOff: true);

                    Logger.Log($"Successor mod { id } not found in catalog.", Logger.warning);
                }
            }

            return text;
        }


        // Alternative mods
        private static string Alternatives(Catalog catalog, Mod subscribedMod)
        {
            if (subscribedMod.Alternatives?.Any() != true)
            {
                return "";
            }

            string text = subscribedMod.Alternatives.Count == 1 ? ReviewLine("An alternative you could use:") :
                ReviewLine("Some alternatives for this are (pick one, not all):");

            // List all alternative mods
            foreach (ulong id in subscribedMod.Alternatives)
            {
                if (catalog.ModIndex.ContainsKey(id))
                {
                    // Mod found in the catalog, list Steam ID and name
                    text += ReviewLine(catalog.ModIndex[id].ToString(hideFakeID: true), ModSettings.bullet2, cutOff: true);
                }
                else
                {
                    // Mod not found in the catalog, which should not happen unless manually editing the catalog
                    text += ReviewLine($"[Steam ID { id,10 }] { subscribedMod.Name }", ModSettings.bullet2, cutOff: true);

                    Logger.Log($"Alternative mod { id } not found in catalog.", Logger.warning);
                }
            }

            return text;
        }


        // Required DLC
        private static string RequiredDLC(Mod subscribedMod)
        {
            if (subscribedMod.RequiredDLC?.Any() != true)
            {
                return "";
            }

            string dlcs = "";

            // Check every required DLC against installed DLC
            foreach (Enums.DLC dlc in subscribedMod.RequiredDLC)
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
        private static string RequiredMods(Catalog catalog, Mod subscribedMod)
        {
            if (subscribedMod.RequiredMods?.Any() != true)
            {
                return "";
            }

            // Strings to collect the text
            string header = ReviewLine("This mod requires other mods you don't have, or which are not enabled:");
            string text = "";

            // Check every required mod
            foreach (ulong id in subscribedMod.RequiredMods)
            {
                // Check if it's a regular mod or a group
                if ((id < ModSettings.lowestGroupID) || (id > ModSettings.highestGroupID))
                {
                    // Regular mod. Try to find it in the list of subscribed mods
                    if (catalog.SubscriptionIDIndex.Contains(id))
                    {
                        // Mod is subscribed
                        if (catalog.ModIndex[id].IsDisabled)
                        {
                            // Mod is subscribed, but not enabled
                            text += ReviewLine(catalog.ModIndex[id].ToString(hideFakeID: true), ModSettings.bullet2, cutOff: true);
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
                        if (catalog.ModIndex.ContainsKey(id))
                        {
                            // Mod found in the catalog
                            text += ReviewLine(catalog.ModIndex[id].ToString(hideFakeID: true), ModSettings.bullet2, cutOff: true);
                        }
                        else
                        {
                            // Mod not found in the catalog, which should not happen unless manually editing the catalog
                            text += ReviewLine($"[Steam ID { id,10 }] { subscribedMod.Name }", ModSettings.bullet2, cutOff: true);

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
                    if (!catalog.GroupIndex.ContainsKey(id))
                    {
                        // Group not found in catalog, which should not happen unless manually editing the catalog
                        text += ReviewLine("one of the following mods: <missing information in catalog>", ModSettings.bullet2);

                        Logger.Log($"Group { id } not found in catalog.", Logger.error);

                        continue;   // To the next required mod
                    }
                    else if (catalog.GroupIndex[id].GroupMembers?.Any() != true)
                    {
                        // Group contains no Steam IDs, which should not happen unless manually editing the catalog
                        text += ReviewLine("one of the following mods: <missing information in catalog>", ModSettings.bullet2);

                        Logger.Log($"Group { id } is empty in catalog.", Logger.error);

                        continue;   // To the next required mod
                    }

                    // Get the group from the catalog
                    Group group = catalog.GroupIndex[id];

                    // Some vars to keep track of all mods in the group, and check if at least one group member is subscribed and enabled
                    int subscriptionsFound = 0;
                    bool EnabledSubscriptionFound = false;
                    string disabledModsText = "";
                    string missingModsText = "";

                    // Check each mod in the group, and see if they are subscribed and enabled
                    foreach (ulong modID in group.GroupMembers)
                    {
                        if (catalog.SubscriptionIDIndex.Contains(modID))
                        {
                            // Mod is subscribed
                            subscriptionsFound++;

                            if (!catalog.ModIndex[modID].IsDisabled)
                            {
                                // Enabled mod found, no need to look any further in this group
                                EnabledSubscriptionFound = true;

                                break;   // out of the group member foreach
                            }
                            else
                            {
                                // Disabled mod
                                disabledModsText += ReviewLine(catalog.ModIndex[modID].ToString(hideFakeID: true), ModSettings.bullet3, cutOff: true);
                            }
                        }
                        else
                        {
                            // Mod is not subscribed, find it in the catalog
                            if (catalog.ModIndex.ContainsKey(modID))
                            {
                                // Mod found in the catalog
                                missingModsText += ReviewLine(catalog.ModIndex[modID].ToString(hideFakeID: true), ModSettings.bullet3, cutOff: true);
                            }
                            else
                            {
                                // Mod not found in the catalog
                                missingModsText += ReviewLine($"[Steam ID { modID,10 }] { subscribedMod.Name }", ModSettings.bullet3, cutOff: true);

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
        private static string Stability(Mod subscribedMod)
        {
            string note = ReviewLine(subscribedMod.StabilityNote, ModSettings.bullet2);

            switch (subscribedMod.Stability)
            {
                case Enums.ModStability.IncompatibleAccordingToWorkshop:
                    return ReviewLine("UNSUBSCRIBE! This is totally incompatible with the current game version.") + note;

                case Enums.ModStability.RequiresIncompatibleMod:
                    return ReviewLine("UNSUBSCRIBE! This requires a mod that is totally incompatible with the current game version.") + note;

                case Enums.ModStability.GameBreaking:
                    return ReviewLine("UNSUBSCRIBE! This breaks the game.") + note;

                case Enums.ModStability.Broken:
                    return ReviewLine("Unsubscribe! This mod is broken.") + note;

                case Enums.ModStability.MajorIssues:
                    return ReviewLine($"Unsubscribe would be wise. This has major issues{ (string.IsNullOrEmpty(note) ? "." : ":") }") + note;

                case Enums.ModStability.MinorIssues:
                    return ReviewLine($"This has minor issues{ (string.IsNullOrEmpty(note) ? ". Check its Workshop page for details." : ":") }") + note;

                case Enums.ModStability.UsersReportIssues:
                    return ReviewLine("Stability is uncertain. Some users are reporting issues" +
                        (string.IsNullOrEmpty(note) ? ". Check its Workshop page for details." : ": ")) + note;

                case Enums.ModStability.Stable:
                    bool isBuiltin = subscribedMod.SteamID <= ModSettings.BuiltinMods.Values.Max();
                    return ReviewLine($"This { (isBuiltin ? "is" : "should be") } compatible with the current game version.") + note;

                case Enums.ModStability.NotEnoughInformation:
                    return ReviewLine("There is not enough information about this mod to know if it is compatible with the current game version.") + note;

                case Enums.ModStability.NotReviewed:
                default:
                    return "";
            }
        }


        // Mod statuses; not reported: UsersReportIssues, UnlistedInWorkshop, SourceBundled, SourceObfuscated, and more  [Todo 0.4] add all statuses
        private static string Statuses(Mod subscribedMod)
        {
            if (subscribedMod.Statuses?.Any() != true)
            {
                return "";
            }

            string text = "";

            // Obsolete
            if (subscribedMod.Statuses.Contains(Enums.ModStatus.NoLongerNeeded))
            {
                text += ReviewLine("Unsubscribe. This is no longer needed.");
            }
            else if (subscribedMod.Statuses.Contains(Enums.ModStatus.Reupload))
            {
                text += ReviewLine("Unsubscribe. This is a re-upload of another mod, use that one instead.");
            }

            if (subscribedMod.Stability != Enums.ModStability.IncompatibleAccordingToWorkshop && subscribedMod.Stability != Enums.ModStability.RequiresIncompatibleMod &&
                subscribedMod.Stability != Enums.ModStability.GameBreaking && subscribedMod.Stability != Enums.ModStability.Broken)
            {
                // Several statuses only listed if there are no gamebreaking issues

                // Abandoned
                if (subscribedMod.Statuses.Contains(Enums.ModStatus.Abandoned))
                {
                    text += ReviewLine("This seems to be abandoned and probably won't be updated anymore.");
                }

                // Editors
                if (subscribedMod.Statuses.Contains(Enums.ModStatus.BreaksEditors))
                {
                    text += ReviewLine("This gives major issues in the asset editor and/or map editor.");
                }
            }

            // Several statuses listed even with gamebreaking issues

            // Removed from the Steam Workshop
            if (subscribedMod.Statuses.Contains(Enums.ModStatus.RemovedFromWorkshop))
            {
                text += ReviewLine("Unsubscribe is wise. This is no longer available on the Workshop.");
            }

            // Savegame affecting
            if (subscribedMod.Statuses.Contains(Enums.ModStatus.SavesCantLoadWithout))
            {
                text += ReviewLine("NOTE: After using this mod, savegames won't easily load without it anymore.");
            }

            // Source code
            if (!subscribedMod.Statuses.Contains(Enums.ModStatus.SourceBundled) && string.IsNullOrEmpty(subscribedMod.SourceURL))
            {
                text += ReviewLine("No public source code found, making it hard to continue if this gets abandoned.");
            }
            else if (subscribedMod.Statuses.Contains(Enums.ModStatus.SourceNotUpdated))
            {
                text += ReviewLine("Published source seems out of date, making it hard to continue if this gets abandoned.");
            }

            // Music
            if (subscribedMod.Statuses.Contains(Enums.ModStatus.MusicCopyrightFree))
            {
                text += ReviewLine(Toolkit.WordWrap("The included music is said to be copyright-free and safe for streaming. " +
                    "Some restrictions might still apply though."));
            }
            else if (subscribedMod.Statuses.Contains(Enums.ModStatus.MusicCopyrighted))
            {
                text += ReviewLine("This includes copyrighted music and should not be used for streaming.");
            }
            else if (subscribedMod.Statuses.Contains(Enums.ModStatus.MusicCopyrightUnknown))
            {
                text += ReviewLine("This includes music with unknown copyright status. Safer not to use it for streaming.");
            }

            return text;
        }


        // Compatibilities with other mods. Result could be multiple mods with multiple statuses. Not reported: CompatibleAccordingToAuthor
        private static string Compatibilities(Catalog catalog, Mod subscribedMod)
        {
            string text = "";

            foreach (Compatibility compatibility in catalog.SubscriptionCompatibilityIndex[subscribedMod.SteamID])
            {
                string firstMod = ReviewLine(catalog.ModIndex[compatibility.FirstModSteamID].ToString(hideFakeID: true), ModSettings.bullet2, cutOff: true);

                string secondMod = ReviewLine(catalog.ModIndex[compatibility.SecondModSteamID].ToString(hideFakeID: true), ModSettings.bullet2, cutOff: true);

                string otherMod = subscribedMod.SteamID == compatibility.FirstModSteamID ? secondMod : firstMod;

                string note = ReviewLine(compatibility.Note, ModSettings.bullet3);

                switch (compatibility.Status)
                {
                    case Enums.CompatibilityStatus.NewerVersion:
                        // Only reported for the older mod
                        if (subscribedMod.SteamID == compatibility.SecondModSteamID)
                        {
                            text += ReviewLine("Unsubscribe. You're already subscribed to a newer version:") + firstMod + note;
                        }
                        break;

                    case Enums.CompatibilityStatus.FunctionalityCovered:
                        // Only reported for the mod with less functionality
                        if (subscribedMod.SteamID == compatibility.SecondModSteamID)
                        {
                            text += ReviewLine("Unsubscribe. You're already subscribed to a mod that has all functionality:") + firstMod + note;
                        }
                        break;

                    case Enums.CompatibilityStatus.SameModDifferentReleaseType:
                        // Only reported for the test/beta mod
                        if (subscribedMod.SteamID == compatibility.SecondModSteamID)
                        {
                            text += ReviewLine("Unsubscribe. You're already subscribe to another edition of the same mod:") + firstMod + note;
                        }
                        break;

                    // The statuses below are reported for both mods
                    case Enums.CompatibilityStatus.SameFunctionality:
                        text += ReviewLine("Unsubscribe either this one or the other mod with the same functionality:") + otherMod + note;
                        break;

                    case Enums.CompatibilityStatus.RequiresSpecificSettings:
                        text += ReviewLine("This requires specific configuration to work together with:") + otherMod + note;
                        break;

                    case Enums.CompatibilityStatus.MinorIssues:
                        text += ReviewLine("This has minor issues with:") + otherMod + note;
                        break;

                    case Enums.CompatibilityStatus.IncompatibleAccordingToAuthor:
                        text += ReviewLine("This is incompatible with (unsubscribe either one):") + otherMod + note;
                        break;

                    case Enums.CompatibilityStatus.IncompatibleAccordingToUsers:
                        text += ReviewLine("Users report incompatibility with (best to unsubscribe one):") + otherMod + note;
                        break;

                    default:
                        break;
                }
            }

            return text;
        }
    }
}
