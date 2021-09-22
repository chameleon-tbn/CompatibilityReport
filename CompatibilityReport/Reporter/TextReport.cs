using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ColossalFramework.PlatformServices;
using CompatibilityReport.CatalogData;
using CompatibilityReport.Util;

namespace CompatibilityReport.Reporter
{
    public static class TextReport
    {
        // Todo 0.4.1 When we detect local mods, warn that the warning in this report might not be accurate.
        // Todo 0.4.1 What to do with the static stringbuilders? Change TextReport into non-static for better garbage collection?
        // Todo 0.4.1 Split report in four: major issues (stability, missing mods/dlc, ...), minor issues (stability), remarks (alternatives, recommended), not reviewed
        // Todo 0.4.1 Review needed for all texts that appear in the report.
        // Todo 0.4.1 This class needs XML documentation tagged comments.
        private static StringBuilder reviewedModsText;
        private static StringBuilder nonReviewedModsText;


        /// <summary>Creates the text report.</summary>
        public static void Create(Catalog catalog)
        {
            reviewedModsText = new StringBuilder();
            nonReviewedModsText = new StringBuilder();

            StringBuilder TextReport = new StringBuilder(512);
            DateTime reportCreationTime = DateTime.Now;
            string separatorDouble = new string('=', ModSettings.TextReportWidth);

            TextReport.AppendLine(Toolkit.WordWrap($"{ ModSettings.ReportName }, created on { reportCreationTime:D}, { reportCreationTime:t}.\n"));

            TextReport.AppendLine(Toolkit.WordWrap($"Version { ModSettings.FullVersion } with catalog { catalog.VersionString() }. " +
                $"The catalog contains { catalog.ReviewedModCount } reviewed mods and { catalog.Mods.Count - catalog.ReviewedModCount } mods with basic information. " +
                $"Your game has { catalog.SubscriptionCount() } mods.\n"));

            if (!string.IsNullOrEmpty(catalog.Note))
            {
                TextReport.AppendLine(Toolkit.WordWrap($"{ catalog.Note }\n"));
            }

            if (Toolkit.CurrentGameVersion() != catalog.GameVersion())
            {
                TextReport.AppendLine(Toolkit.WordWrap($"WARNING: The review catalog is made for game version " +
                    Toolkit.ConvertGameVersionToString(catalog.GameVersion()) +
                    $". Your game is { (Toolkit.CurrentGameVersion() < catalog.GameVersion() ? "older" : "newer") }. Results may not be accurate.\n", 
                    indent: new string(' ', "WARNING: ".Length)));
            }

            TextReport.AppendLine($"{ separatorDouble }\n");

            if (!string.IsNullOrEmpty(catalog.ReportHeaderText))
            {
                TextReport.AppendLine(Toolkit.WordWrap($"{ catalog.ReportHeaderText }\n", indent: ModSettings.Indent1, indentAfterNewLine: ""));
            }

            int modsWithOnlyRemarks = 0;

            if (ModSettings.ReportSortByName)
            {
                // Report mods sorted by name.
                List<string> AllSubscriptionNames = catalog.GetSubscriptionNames();

                foreach (string name in AllSubscriptionNames)
                {
                    // Get the Steam ID(s) for this mod name. There could be multiple IDs for mods with the same name.
                    foreach (ulong steamID in catalog.GetSubscriptionIDsByName(name))
                    {
                        if (GetModText(catalog, steamID, nameFirst: true))
                        {
                            modsWithOnlyRemarks++;
                        }
                    }
                }
            }
            else
            {
                // Report mods sorted by Steam ID.
                foreach (ulong steamID in catalog.GetSubscriptionIDs())
                {
                    if (GetModText(catalog, steamID, nameFirst: false))
                    {
                        modsWithOnlyRemarks++;
                    }
                }
            }

            if (reviewedModsText.Length > 0)
            {
                TextReport.AppendLine(separatorDouble);

                TextReport.AppendLine($"REVIEWED MODS ({ catalog.ReviewedSubscriptionCount })" +
                    (modsWithOnlyRemarks == 0 ? ":" : $" AND OTHER MODS WITH REMARKS ({ modsWithOnlyRemarks }): "));

                TextReport.AppendLine(reviewedModsText.ToString());
            }

            if (nonReviewedModsText.Length > 0)
            {
                TextReport.AppendLine(separatorDouble);

                TextReport.AppendLine($"MODS NOT REVIEWED YET ({ catalog.SubscriptionCount() - catalog.ReviewedSubscriptionCount - modsWithOnlyRemarks }):");

                TextReport.AppendLine(nonReviewedModsText.ToString());
            }

            TextReport.AppendLine($"{ separatorDouble }\n");
            TextReport.AppendLine(Toolkit.WordWrap(catalog.ReportFooterText));

            reviewedModsText = null;
            nonReviewedModsText = null;

            string TextReportFullPath = Path.Combine(ModSettings.ReportPath, ModSettings.ReportTextFileName);
            if (Toolkit.SaveToFile(TextReport.ToString(), TextReportFullPath, createBackup: ModSettings.DebugMode))
            {
                Logger.Log($"Text Report ready at \"{ Toolkit.Privacy(TextReportFullPath) }\".", duplicateToGameLog: true);
            }
            else
            {
                TextReportFullPath = Path.Combine(ModSettings.DefaultReportPath, ModSettings.ReportTextFileName);
                if ((ModSettings.ReportPath != ModSettings.DefaultReportPath) && Toolkit.SaveToFile(TextReport.ToString(), TextReportFullPath))
                {
                    Logger.Log($"Text Report could not be saved at the location set in the options. It is instead saved as \"{ Toolkit.Privacy(TextReportFullPath) }\".", 
                        Logger.Warning, duplicateToGameLog: true);
                }
                else
                {
                    Logger.Log("Text Report could not be saved.", Logger.Error, duplicateToGameLog: true);
                }
            }
        }


        /// <summary>Gets the report text for one mod.</summary>
        /// <returns>True if this mod has no review in the catalog but has remarks to report nonetheless, false otherwise.</returns>
        // Todo 0.4.1 Change GetModText(), which is now quite cumbersome.
        // Todo 0.4.1 Currently not reported: SourceURL, Updated, Downloaded.
        private static bool GetModText(Catalog catalog, ulong steamID, bool nameFirst)
        {
            Mod subscribedMod = catalog.GetMod(steamID);
            Author subscriptionAuthor = catalog.GetAuthor(subscribedMod.AuthorID, subscribedMod.AuthorUrl);
            string AuthorName = subscriptionAuthor == null ? "" : subscriptionAuthor.Name;
            string modName = subscribedMod.ToString(hideFakeID: true, nameFirst, cutOff: true);

            StringBuilder modReview = new StringBuilder();
            string modHeader = $"{ new string('-', ModSettings.TextReportWidth) }\n\n{ modName }";

            modHeader += string.IsNullOrEmpty(AuthorName) ? "\n" : 
                ((modName.Length + 4 + AuthorName.Length <= ModSettings.TextReportWidth) ? $" by { AuthorName }\n" :
                $"\n{ $"by { AuthorName}".PadLeft(ModSettings.TextReportWidth) }\n");

            // Todo 0.4.1 Rethink which review texts to include in 'somethingToReport'. Combine author retired and mod abandoned into one line.
            modReview.Append(ThisMod(subscribedMod));
            modReview.Append(Stability(subscribedMod));
            modReview.Append(RequiredDlc(subscribedMod));
            modReview.Append(RequiredMods(catalog, subscribedMod));
            modReview.Append(Compatibilities(catalog, subscribedMod));
            modReview.Append(Statuses(subscribedMod));
            modReview.Append(DependencyMod(catalog, subscribedMod));
            modReview.Append(Disabled(subscribedMod));
            modReview.Append(RetiredAuthor(subscriptionAuthor));
            modReview.Append(GameVersionCompatible(subscribedMod));
            modReview.Append(Successors(catalog, subscribedMod));
            modReview.Append(Alternatives(catalog, subscribedMod));
            modReview.Append(Recommendations(catalog, subscribedMod));
            modReview.Append(CameraScript(subscribedMod));
            modReview.Append(GenericNote(subscribedMod));

            bool somethingToReport = modReview.Length > 0;

            // Insert the 'not reviewed' text at the start of the text. This does not count towards somethingToReport.
            modReview.Insert(0, NotReviewed(subscribedMod));

            // Todo 0.4.1 Keep better track of issues. Now the "nothing found" text doesn't show if the author is retired.
            if (modReview.Length == 0)
            {
                modReview.Append(ReviewLine("No known issues or incompatibilities with your other mods."));
            }

            modReview.Append((steamID > ModSettings.HighestFakeID) ? ReviewLine($"Steam Workshop page: { Toolkit.GetWorkshopUrl(steamID) }"): "");

            if ((subscribedMod.ReviewDate != default) || somethingToReport)
            {
                reviewedModsText.AppendLine(modHeader + modReview);
            }
            else
            {
                nonReviewedModsText.AppendLine(modHeader + modReview);
            }

            // Indicate whether we found a mod without a review in the catalog, but with remarks to report.
            return subscribedMod.ReviewDate == default && somethingToReport;
        }


        /// <summary>Formats a text for the report, including optional bullet and indenting.</summary>
        /// <remarks>The text will be wordwrapped, or optionally cutoff, at report width.</remarks>
        /// <returns>A formatted string for the report.</returns>
        private static string ReviewLine(string message, string bullet = ModSettings.Bullet1, bool cutOff = false)
        {
            message = $"{ bullet }{ message }";

            if (message.Length > ModSettings.TextReportWidth)
            {
                if (cutOff)
                {
                    message = $"{ message.Substring(0, ModSettings.TextReportWidth - 3) }...";

                    Logger.Log($"Report line cut off: { message }", Logger.Debug);
                }
                else
                {
                    message = Toolkit.WordWrap($"{ message }", indent: new string(' ', bullet.Length));
                }
            }

            return string.IsNullOrEmpty(message) || (message == bullet) ? "" : $"{ message }\n";
        }


        // Say hi to ourselves.
        private static string ThisMod(Mod subscribedMod)
        {
            return (subscribedMod.SteamID != ModSettings.OurOwnSteamID) ? "" : ReviewLine("This mod.");
        }


        // Mod not reviewed, Local mods get a slightly different text.
        private static string NotReviewed(Mod subscribedMod)
        {
            bool IsLocal = subscribedMod.SteamID >= ModSettings.LowestLocalModID && subscribedMod.SteamID <= ModSettings.HighestLocalModID;

            return IsLocal ? ReviewLine("Can't review local mods (yet).") : (subscribedMod.ReviewDate != default) ? "" : ReviewLine("Not reviewed yet.");
        }


        // Cinematic camera script.
        private static string CameraScript(Mod subscribedMod)
        {
            return !subscribedMod.IsCameraScript ? "" : ReviewLine("This is a cinematic camera script, which technically is a mod and thus listed here.");
        }


        // Generic note.
        private static string GenericNote(Mod subscribedMod)
        {
            return ReviewLine(subscribedMod.GenericNote ?? "");
        }


        // Game version compatible, only listed for current major game version.
        private static string GameVersionCompatible(Mod subscribedMod)
        {
            string currentOrNot = subscribedMod.GameVersion() == Toolkit.CurrentGameVersion() ? "current " : "";
            return (subscribedMod.GameVersion() < Toolkit.CurrentMajorGameVersion()) ? "" : ReviewLine("Created or updated for " +
                $"{ currentOrNot }game version { Toolkit.ConvertGameVersionToString(Toolkit.CurrentGameVersion()) }. Less likely to have issues with the game.");
        }


        // Disabled mod.
        private static string Disabled(Mod subscribedMod)
        {
            return !subscribedMod.IsDisabled ? "" : ReviewLine("Mod is disabled. Unsubscribe it if not used. Disabled mods can still cause issues.");
        }


        // Retired author.
        private static string RetiredAuthor(Author subscriptionAuthor)
        {
            return (subscriptionAuthor == null || !subscriptionAuthor.Retired) ? "" : ReviewLine("The author seems to be retired. Updates are unlikely.");
        }


        // Unneeded dependency mod.
        private static string DependencyMod(Catalog catalog, Mod subscribedMod)
        {
            if (!subscribedMod.Statuses.Contains(Enums.Status.DependencyMod))
            {
                return "";
            }

            // Check if any of the mods that need this is actually subscribed, enabled or not.
            List<Mod> ModsRequiringThis = catalog.Mods.FindAll(x => x.RequiredMods.Contains(subscribedMod.SteamID));

            foreach (Mod mod in ModsRequiringThis)
            {
                if (catalog.GetSubscription(mod.SteamID) != null)
                {
                    // Found a subscribed mod that needs this. Nothing to report.
                    return "";
                }
            }

            return catalog.IsValidID(ModSettings.LowestLocalModID) ? ReviewLine("You should unsubscribe this, unless it is needed for one of your local mods. " +
                "None of your Steam Workshop mods need this, and it doesn't provide any functionality on its own.") : 
                ReviewLine("You should unsubscribe this. It is only needed for mods you don't have.");
        }


        // Successor(s)
        private static string Successors(Catalog catalog, Mod subscribedMod)
        {
            string text = (subscribedMod.Successors.Count == 0) ? "" : (subscribedMod.Successors.Count == 1) ? ReviewLine("This is succeeded by:") :
                ReviewLine("This is succeeded by any of the following (pick one, not all):");

            foreach (ulong steamID in subscribedMod.Successors)
            {
                if (catalog.IsValidID(steamID))
                {
                    if (catalog.GetSubscription(steamID) != null)
                    {
                        return ReviewLine("Unsubscribe. This is succeeded by a mod you already have:") + 
                            ReviewLine(catalog.GetMod(steamID).ToString(hideFakeID: true), ModSettings.Bullet2, cutOff: true);
                    }

                    text += ReviewLine(catalog.GetMod(steamID).ToString(hideFakeID: true), ModSettings.Bullet2, cutOff: true);
                }
                else
                {
                    // Mod not found in the catalog, which should not happen unless bugs or a manual edit of the catalog.
                    text += ReviewLine($"[Steam ID { steamID,10 }]", ModSettings.Bullet2);

                    Logger.Log($"Successor mod { steamID } not found in catalog.", Logger.Warning);
                }
            }

            return text;
        }


        // Alternative mods
        private static string Alternatives(Catalog catalog, Mod subscribedMod)
        {
            string text = (subscribedMod.Alternatives.Count == 0) ? "" : (subscribedMod.Alternatives.Count == 1) ? ReviewLine("An alternative you could use:") :
                ReviewLine("Some alternatives for this are (pick one, not all):");

            foreach (ulong steamID in subscribedMod.Alternatives)
            {
                if (catalog.IsValidID(steamID))
                {
                    text += ReviewLine($"{ (catalog.GetSubscription(steamID) == null ? "" : "[Subscribed] ") }{ catalog.GetMod(steamID).ToString(hideFakeID: true) }", 
                        ModSettings.Bullet2, cutOff: true);
                }
                else
                {
                    // Mod not found in the catalog, which should not happen unless bugs or a manual edit of the catalog.
                    text += ReviewLine($"[Steam ID { steamID,10 }]", ModSettings.Bullet2);

                    Logger.Log($"Alternative mod { steamID } not found in catalog.", Logger.Warning);
                }
            }

            return text;
        }


        // Required DLC
        private static string RequiredDlc(Mod subscribedMod)
        {
            string dlcs = "";

            foreach (Enums.Dlc dlc in subscribedMod.RequiredDlcs)
            {
                if (!PlatformService.IsDlcInstalled((uint)dlc))
                {
                    // Add the missing DLC, replacing the underscores in the DLC enum name with spaces and semicolons
                    dlcs += ReviewLine(Toolkit.ConvertDlcToString(dlc), ModSettings.Bullet2);
                }
            }

            return string.IsNullOrEmpty(dlcs) ? "" : ReviewLine("Unsubscribe. This requires DLC you don't have:") + dlcs;
        }


        /// <summary>Check for required mods that are not subscribed or disabled.</summary>
        /// <remarks>If a required mod is not subscribed but in a group, the other group members are checked.</remarks>
        /// <returns>A string with text for the report, or an empty string if there is no required mod or no required mod is missing.</returns>
        private static string RequiredMods(Catalog catalog, Mod subscribedMod)
        {
            string text = "";

            foreach (ulong steamID in subscribedMod.RequiredMods)
            {
                if (catalog.IsValidID(steamID))
                {
                    text += CheckModAndGroup(catalog, steamID);
                }
                else
                {
                    // Mod not found in the catalog, which should not happen unless bugs or a manual edit of the catalog.
                    text += ReviewLine($"[Steam ID { steamID,10 }]", ModSettings.Bullet2);

                    Logger.Log($"Required mod { steamID } not found in catalog.", Logger.Warning);
                }
            }

            return string.IsNullOrEmpty(text) ? "" : ReviewLine("This mod requires other mods you don't have, or which are not enabled:") + text;
        }


        /// <summary>Check for recommended mods that are not subscribed or disabled.</summary>
        /// <remarks>If a recommended mod is not subscribed but in a group, the other group members are checked.</remarks>
        /// <returns>A string with text for the report, or an empty string if there is no recommendation or no recommended mod is missing.</returns>
        private static string Recommendations(Catalog catalog, Mod subscribedMod)
        {
            string text = "";

            foreach (ulong steamID in subscribedMod.Recommendations)
            {
                if (catalog.IsValidID(steamID))
                {
                    text += CheckModAndGroup(catalog, steamID);
                }
                else
                {
                    // Mod not found in the catalog, which should not happen unless bugs or a manual edit of the catalog.
                    text += ReviewLine($"[Steam ID { steamID,10 }]", ModSettings.Bullet2);

                    Logger.Log($"Recommended mod { steamID } not found in catalog.", Logger.Warning);
                }
            }

            return string.IsNullOrEmpty(text) ? "" : ReviewLine("The author of this mod recommends using the following with this:") + text;
        }


        /// <summary>Check if a mod or any member of its group is subscribed and enabled.</summary>
        /// <returns>A string with text for the report, or an empty string if the mod or another group member is subscribed and enabled.</returns>
        private static string CheckModAndGroup(Catalog catalog, ulong steamID)
        {
            string text = "";

            if (catalog.GetSubscription(steamID) != null && !catalog.GetSubscription(steamID).IsDisabled)
            {
                // Mod is subscribed and enabled. Nothing to report.
            }
            else if (catalog.GetMod(steamID).IsDisabled)
            {
                // Mod is subscribed and disabled. Report as "missing", without Workshop page.
                text = ReviewLine(catalog.GetMod(steamID).ToString(hideFakeID: true), ModSettings.Bullet2, cutOff: true);
            }
            else if (!catalog.IsGroupMember(steamID))
            {
                // Mod is not subscribed and not in a group. Report as missing.
                text = ReviewLine(catalog.GetMod(steamID).ToString(hideFakeID: true), ModSettings.Bullet2, cutOff: true) +
                    ReviewLine($"Workshop page: { Toolkit.GetWorkshopUrl(steamID) }", ModSettings.Indent2);
            }
            else
            {
                // Mod is not subscribed but in a group. Check if another group member is subscribed.
                bool subscribedGroupMember = false;

                foreach (ulong groupMemberID in catalog.GetThisModsGroup(steamID).GroupMembers)
                {
                    if (subscribedGroupMember = catalog.GetSubscription(groupMemberID) != null)
                    {
                        // Group member is subscribed. No need to check other group members, but report as "missing" if disabled.
                        if (catalog.GetMod(groupMemberID).IsDisabled)
                        {
                            text = ReviewLine(catalog.GetMod(groupMemberID).ToString(hideFakeID: true), ModSettings.Bullet2, cutOff: true);
                        }
                        break;
                    }
                }

                if (!subscribedGroupMember)
                {
                    // No group member is subscribed. Report original mod as missing.
                    text = ReviewLine(catalog.GetMod(steamID).ToString(hideFakeID: true), ModSettings.Bullet2, cutOff: true) +
                        ReviewLine($"Workshop page: { Toolkit.GetWorkshopUrl(steamID) }", ModSettings.Indent2);
                }
            }

            return text;
        }


        // Mod stability.
        private static string Stability(Mod subscribedMod)
        {
            string note = ReviewLine(subscribedMod.StabilityNote, ModSettings.Bullet2);

            switch (subscribedMod.Stability)
            {
                case Enums.Stability.IncompatibleAccordingToWorkshop:
                    return ReviewLine("UNSUBSCRIBE! This is totally incompatible with the current game version.") + note;

                case Enums.Stability.RequiresIncompatibleMod:
                    return ReviewLine("UNSUBSCRIBE! This requires a mod that is totally incompatible with the current game version.") + note;

                case Enums.Stability.GameBreaking:
                    return ReviewLine("UNSUBSCRIBE! This breaks the game.") + note;

                case Enums.Stability.Broken:
                    return ReviewLine("Unsubscribe! This mod is broken.") + note;

                case Enums.Stability.MajorIssues:
                    return ReviewLine($"Unsubscribe would be wise. This has major issues{ (string.IsNullOrEmpty(note) ? "." : ":") }") + note;

                case Enums.Stability.MinorIssues:
                    return ReviewLine($"This has minor issues{ (string.IsNullOrEmpty(note) ? ". Check its Workshop page for details." : ":") }") + note;

                case Enums.Stability.UsersReportIssues:
                    return ReviewLine("Stability is uncertain. Some users are reporting issues" +
                        (string.IsNullOrEmpty(note) ? ". Check its Workshop page for details." : ": ")) + note;

                case Enums.Stability.Stable:
                    bool isBuiltin = subscribedMod.SteamID <= ModSettings.BuiltinMods.Values.Max();
                    return ReviewLine($"This { (isBuiltin ? "is" : "should be") } compatible with the current game version.") + note;

                case Enums.Stability.NotEnoughInformation:
                    return ReviewLine("There is not enough information about this mod to know if it is compatible with the current game version.") + note;

                case Enums.Stability.NotReviewed:
                default:
                    return "";
            }
        }


        // Mod statuses. Not reported: UnlistedInWorkshop, SourceObfuscated.
        private static string Statuses(Mod subscribedMod)
        {
            string text = "";

            if (subscribedMod.Statuses.Contains(Enums.Status.NoLongerNeeded))
            {
                text += ReviewLine("Unsubscribe. This is no longer needed.");
            }

            if (subscribedMod.Statuses.Contains(Enums.Status.SavesCantLoadWithout))
            {
                text += ReviewLine("NOTE: After using this mod, savegames won't (easily) load without it anymore.");
            }

            if (subscribedMod.Stability <= Enums.Stability.Broken)
            {
                // Several statuses only listed if there are no breaking issues.
                if (subscribedMod.Statuses.Contains(Enums.Status.Deprecated))
                {
                    text += ReviewLine("Unsubscribe would be wise. This mod is no longer supported by the author.");
                }
                else if (subscribedMod.Statuses.Contains(Enums.Status.RemovedFromWorkshop))
                {
                    text += ReviewLine("Unsubscribe would be wise. This is no longer available on the Steam Workshop.");
                }
                else if (subscribedMod.Statuses.Contains(Enums.Status.Reupload))
                {
                    text += ReviewLine("Unsubscribe. This is a re-upload of another mod, use that one instead.");
                }
                else if (subscribedMod.Statuses.Contains(Enums.Status.Abandoned))
                {
                    text += ReviewLine("This seems to be abandoned and probably won't be updated anymore.");
                }

                if (subscribedMod.Statuses.Contains(Enums.Status.BreaksEditors))
                {
                    text += ReviewLine("If you use the asset editor and/or map editor, this might give serious issues.");
                }

                if (subscribedMod.Statuses.Contains(Enums.Status.ModForModders))
                {
                    text += ReviewLine("This is only needed for modders. Regular users don't need this one.");
                }

                if (subscribedMod.Statuses.Contains(Enums.Status.TestVersion))
                {
                    text += ReviewLine("This is a test version. If you don't have a specific reason to use it, you'd better subscribe to the stable version instead.");
                }

                if (subscribedMod.Statuses.Contains(Enums.Status.MusicCopyrightFree))
                {
                    text += ReviewLine(Toolkit.WordWrap("The included music is said to be copyright-free and safe for streaming. " +
                        "Some restrictions might still apply though."));
                }
                else if (subscribedMod.Statuses.Contains(Enums.Status.MusicCopyrighted))
                {
                    text += ReviewLine("This includes copyrighted music and should not be used for streaming.");
                }
                else if (subscribedMod.Statuses.Contains(Enums.Status.MusicCopyrightUnknown))
                {
                    text += ReviewLine("This includes music with unknown copyright status. Safer not to use it for streaming.");
                }

                if (subscribedMod.Statuses.Contains(Enums.Status.NoDescription))
                {
                    text += ReviewLine("This has no description on the Steam Workshop. Support from the author is unlikely.");
                }

                if (subscribedMod.Statuses.Contains(Enums.Status.NoCommentSection))
                {
                    text += ReviewLine("This mod has the comment section disabled on the Steam Workshop, making it hard to see if other users are experiencing issues. " +
                        "Use with caution.");
                }
            }

            bool unsupported = subscribedMod.Statuses.Contains(Enums.Status.NoLongerNeeded) || subscribedMod.Statuses.Contains(Enums.Status.Deprecated) || 
                subscribedMod.Statuses.Contains(Enums.Status.RemovedFromWorkshop) || subscribedMod.Statuses.Contains(Enums.Status.Reupload) || 
                subscribedMod.Statuses.Contains(Enums.Status.Abandoned) || (subscribedMod.Stability == Enums.Stability.IncompatibleAccordingToWorkshop);

            if (!subscribedMod.Statuses.Contains(Enums.Status.SourceBundled) && string.IsNullOrEmpty(subscribedMod.SourceUrl))
            {
                text += ReviewLine($"No public source code found, making it hard to continue by another modder{ (unsupported ? "" : " if this gets abandoned") }.");
            }
            else if (subscribedMod.Statuses.Contains(Enums.Status.SourceNotUpdated))
            {
                text += ReviewLine($"Published source seems out of date, making it hard to continue by another modder{ (unsupported ? "" : " if this gets abandoned") }.");
            }

            return text;
        }


        // Compatibilities with other mods. Result could be multiple mods with multiple statuses. Not reported: CompatibleAccordingToAuthor.
        private static string Compatibilities(Catalog catalog, Mod subscribedMod)
        {
            string text = "";

            foreach (Compatibility compatibility in catalog.GetSubscriptionCompatibilities(subscribedMod.SteamID))
            {
                string firstMod = ReviewLine(catalog.GetMod(compatibility.FirstModID).ToString(hideFakeID: true), ModSettings.Bullet2, cutOff: true);
                string secondMod = ReviewLine(catalog.GetMod(compatibility.SecondModID).ToString(hideFakeID: true), ModSettings.Bullet2, cutOff: true);
                string otherMod = subscribedMod.SteamID == compatibility.FirstModID ? secondMod : firstMod;

                string note = ReviewLine(compatibility.Note, ModSettings.Bullet3);

                switch (compatibility.Status)
                {
                    case Enums.CompatibilityStatus.NewerVersion:
                        // Only reported for the older mod.
                        if (subscribedMod.SteamID == compatibility.SecondModID)
                        {
                            text += ReviewLine("Unsubscribe. You're already subscribed to a newer version:") + firstMod + note;
                        }
                        break;

                    case Enums.CompatibilityStatus.FunctionalityCovered:
                        // Only reported for the mod with less functionality.
                        if (subscribedMod.SteamID == compatibility.SecondModID)
                        {
                            text += ReviewLine("Unsubscribe. You're already subscribed to a mod that has all functionality:") + firstMod + note;
                        }
                        break;

                    case Enums.CompatibilityStatus.SameModDifferentReleaseType:
                        // Only reported for the test mod.
                        if (subscribedMod.SteamID == compatibility.SecondModID)
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
