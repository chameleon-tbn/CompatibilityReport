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
    // The report is split into five categories:
    //  - Unsubscribe:       broken/gamebreaking, incompatible according to workshop or author, obsolete, disabled, reuploads, missing dlc, unneeded dependency.
    //  - Major Issues:      major issues (mod and compatibility), incompatible according to users, removed, deprecated, missing required mods.
    //  - Minor Issues:      minor issues (mod and compatibility), users report issues, no description, no comment section, successors.
    //  - Remarks:           not enough information, retired author, abandoned, breaks editors, other statuses, mod note, game version, alternatives, recommendations,
    //                       specific settings, compatible (with compatibility note).
    //  - Nothing to Report: stable, not reviewed.
    //          
    // Currently not reported: SourceURL (no-source and source-not-updated are reported), SourceBundled, Updated, Downloaded.

    public class TextReport
    {
        // The complete report text.
        private readonly StringBuilder reportText = new StringBuilder(512);

        // The different mod report categories and their mod counters.
        private readonly StringBuilder unsubscribe = new StringBuilder();
        private readonly StringBuilder majorIssues = new StringBuilder();
        private readonly StringBuilder minorIssues = new StringBuilder();
        private readonly StringBuilder remarks = new StringBuilder();
        private readonly StringBuilder nothingToReport = new StringBuilder();
        int unsubscribeCount, majorCount, minorCount, remarksCount, nothingCount;

        private readonly Catalog catalog;
        private readonly string separatorDouble = new string('=', ModSettings.TextReportWidth);


        /// <summary>Default constructor.</summary>
        public TextReport(Catalog currentCatalog)
        {
            catalog = currentCatalog;
        }


        /// <summary>Creates the text report.</summary>
        /// <remarks>If the report can't be saved in the report path selected by the user, try the default path.</remarks>
        public void Create()
        {
            AddHeader();

            AddAllMods();

            AddModList();

            AddFooter();

            string TextReportFullPath = Path.Combine(ModSettings.ReportPath, ModSettings.ReportTextFileName);
            Toolkit.DeleteFile($"{ TextReportFullPath }.old");

            if (Toolkit.SaveToFile(reportText.ToString(), TextReportFullPath, createBackup: ModSettings.DebugMode))
            {
                Logger.Log($"Text Report ready at \"{ Toolkit.Privacy(TextReportFullPath) }\".");
            }
            else
            {
                TextReportFullPath = Path.Combine(ModSettings.DefaultReportPath, ModSettings.ReportTextFileName);

                if ((ModSettings.ReportPath != ModSettings.DefaultReportPath) && Toolkit.SaveToFile(reportText.ToString(), TextReportFullPath))
                {
                    Logger.Log($"Text Report could not be saved at the location set in the options. It is instead saved as \"{ Toolkit.Privacy(TextReportFullPath) }\".",
                        Logger.Warning);
                }
                else
                {
                    Logger.Log("Text Report could not be saved.", Logger.Error);
                }
            }
        }


        /// <summary>Adds header text to the report.</summary>
        private void AddHeader()
        {
            DateTime reportCreationTime = DateTime.Now;
            reportText.AppendLine(Toolkit.WordWrap($"{ ModSettings.ModName }, created on { reportCreationTime:D}, { reportCreationTime:t}.\n"));

            reportText.AppendLine(Toolkit.WordWrap($"Version { ModSettings.FullVersion } with catalog { catalog.VersionString() }. " +
                $"The catalog contains { catalog.ReviewedModCount } reviewed mods with { catalog.Compatibilities.Count } compatibilities, and " +
                $"{ catalog.Mods.Count - catalog.ReviewedModCount - catalog.LocalSubscriptionCount } mods with basic information. " +
                $"Your game has { catalog.SubscriptionCount() } mods.\n"));

            if (!string.IsNullOrEmpty(ModSettings.ReportTextForThisModVersion))
            {
                reportText.AppendLine(Toolkit.WordWrap($"{ ModSettings.ReportTextForThisModVersion }\n"));
            }

            if (!string.IsNullOrEmpty(catalog.Note))
            {
                reportText.AppendLine(Toolkit.WordWrap($"{ catalog.Note }\n"));
            }

            if (Toolkit.CurrentGameVersion() != catalog.GameVersion())
            {
                reportText.AppendLine(Toolkit.WordWrap($"WARNING: The review catalog is made for game version " +
                    Toolkit.ConvertGameVersionToString(catalog.GameVersion()) +
                    $". Your game is { (Toolkit.CurrentGameVersion() < catalog.GameVersion() ? "older" : "newer") }. Results might not be accurate.\n",
                    indent: new string(' ', "WARNING: ".Length)));
            }

            if (catalog.LocalSubscriptionCount != 0)
            {
                reportText.AppendLine(Toolkit.WordWrap($"NOTE: You have { catalog.LocalSubscriptionCount } local mod{ (catalog.LocalSubscriptionCount == 1 ? "" : "s") }" +
                    ", which we can't review. The report does not check for incompatibilities with these. Results might not be completely accurate.\n" +
                    "Use mods as Workshop subscription whenever possible. Mods copied to the local mod folder don't always work and often cannot " +
                    "be detected correctly by other mods.\n", indent: new string(' ', "NOTE: ".Length)));
            }

            int nonReviewedSubscriptions = catalog.SubscriptionCount() - catalog.ReviewedSubscriptionCount - catalog.LocalSubscriptionCount;

            if (nonReviewedSubscriptions != 0)
            {
                reportText.AppendLine(Toolkit.WordWrap($"NOTE: { nonReviewedSubscriptions } of your mods have not been reviewed yet. " +
                    "Some incompatibilities or warnings might be missing in the report due to this.\n", indent: new string(' ', "NOTE: ".Length)));
            }

            if (!string.IsNullOrEmpty(catalog.ReportHeaderText))
            {
                reportText.AppendLine($"{ separatorDouble }\n");
                reportText.AppendLine(Toolkit.WordWrap($"{ catalog.ReportHeaderText }\n", indent: ModSettings.Indent1, indentAfterNewLine: ""));
            }

            reportText.AppendLine();
        }


        /// <summary>Adds a list of all mods to the report, sorted by name.</summary>
        /// <remarks>Built-in mods that are disabled are not included.</remarks>
        private void AddModList()
        {
            reportText.AppendLine($"{ separatorDouble }\n");
            reportText.AppendLine(Toolkit.WordWrap("This is the end of the report. Below you find a summary of all your subscribed mods.\n"));

            List<string> AllSubscriptionNames = catalog.GetSubscriptionNames();

            foreach (string name in AllSubscriptionNames)
            {
                // Get the Steam ID(s) for this mod name. There could be multiple IDs for mods with the same name.
                foreach (ulong steamID in catalog.GetSubscriptionIDsByName(name))
                {
                    string disabled = catalog.GetMod(steamID).IsDisabled ? " [disabled]" : "";

                    string url = steamID > ModSettings.HighestFakeID ? $", { Toolkit.GetWorkshopUrl(steamID) }" :
                        steamID < ModSettings.LowestLocalModID ? " [built-in]" : " [local]";

                    reportText.AppendLine($"{ name }{ disabled }{ url }");
                }
            }

            reportText.AppendLine();
            reportText.AppendLine();
        }


        /// <summary>Adds footer text to the report.</summary>
        private void AddFooter()
        {
            if (!string.IsNullOrEmpty(catalog.ReportFooterText))
            {
                reportText.AppendLine($"{ separatorDouble }\n");
                reportText.AppendLine(Toolkit.WordWrap(catalog.ReportFooterText));
            }
        }


        /// <summary>Adds the report text for all mods to the report.</summary>
        private void AddAllMods()
        {
            if (ModSettings.ReportSortByName)
            {
                // Report mods sorted by name.
                List<string> AllSubscriptionNames = catalog.GetSubscriptionNames();

                foreach (string name in AllSubscriptionNames)
                {
                    // Get the Steam ID(s) for this mod name. There could be multiple IDs for mods with the same name.
                    foreach (ulong steamID in catalog.GetSubscriptionIDsByName(name))
                    {
                        CreateModText(steamID, nameFirst: true);
                    }
                }
            }
            else
            {
                // Report mods sorted by Steam ID.
                foreach (ulong steamID in catalog.GetSubscriptionIDs())
                {
                    CreateModText(steamID, nameFirst: false);
                }
            }

            if (unsubscribeCount > 0)
            {
                reportText.AppendLine(separatorDouble);
                reportText.AppendLine($"{ unsubscribeCount } { (unsubscribeCount == 1 ? "MOD" : "MODS") } COULD OR SHOULD BE UNSUBSCRIBED:");
                reportText.AppendLine(unsubscribe.ToString());
            }
            if (majorCount > 0)
            {
                reportText.AppendLine(separatorDouble);
                reportText.AppendLine($"{ majorCount } { (majorCount == 1 ? "MOD HAS" : "MODS HAVE") } MAJOR ISSUES:");
                reportText.AppendLine(majorIssues.ToString());
            }
            if (minorCount > 0)
            {
                reportText.AppendLine(separatorDouble);
                reportText.AppendLine($"{ minorCount } { (minorCount == 1 ? "MOD HAS" : "MODS HAVE") } MINOR ISSUES:");
                reportText.AppendLine(minorIssues.ToString());
            }
            if (remarksCount > 0)
            {
                reportText.AppendLine(separatorDouble);
                reportText.AppendLine($"{ remarksCount } { (remarksCount == 1 ? "MOD" : "MODS") } WITH REMARKS:");
                reportText.AppendLine(remarks.ToString());
            }
            if (nothingCount > 0)
            {
                reportText.AppendLine(separatorDouble);
                reportText.AppendLine($"{ nothingCount } { (nothingCount == 1 ? "MOD" : "MODS") } WITH NOTHING TO REPORT:");
                reportText.AppendLine(nothingToReport.ToString());
            }
        }


        /// <summary>Creates the report text for a single mod and increase the counter.</summary>
        /// <remarks>The text is added to the relevant StringBuilder, not directly to the report.</remarks>
        private void CreateModText(ulong steamID, bool nameFirst)
        {
            Mod subscribedMod = catalog.GetMod(steamID);
            string modName = subscribedMod.ToString(hideFakeID: true, nameFirst, cutOff: true);
            Author subscriptionAuthor = catalog.GetAuthor(subscribedMod.AuthorID, subscribedMod.AuthorUrl);
            string authorName = subscriptionAuthor == null ? "" : subscriptionAuthor.Name;

            StringBuilder modText = new StringBuilder($"{ new string('-', ModSettings.TextReportWidth) }\n\n");

            modText.AppendLine(string.IsNullOrEmpty(authorName) ? $"{ modName }\n" :
                ((modName.Length + 4 + authorName.Length <= ModSettings.TextReportWidth) ? $"{ modName } by { authorName }\n" :
                $"{ modName }\n{ $"by { authorName}".PadLeft(ModSettings.TextReportWidth) }"));

            if (subscribedMod.SteamID >= ModSettings.LowestLocalModID && subscribedMod.SteamID <= ModSettings.HighestLocalModID)
            {
                modText.Append(Format("Can't review local mods."));
                modText.Append(subscribedMod.IsCameraScript ? Format("This is a cinematic camera script, which technically is a mod and thus listed here.") : "");
                modText.AppendLine();
            }
            else
            {
                modText.Append(Instability(subscribedMod));
                modText.Append(RequiredDlc(subscribedMod));
                modText.Append(UnneededDependencyMod(subscribedMod));
                modText.Append(Disabled(subscribedMod));
                modText.Append(Successors(subscribedMod));
                modText.Append(Stability(subscribedMod));
                modText.Append(Compatibilities(subscribedMod));
                modText.Append(RequiredMods(subscribedMod));
                modText.Append(Statuses(subscribedMod, authorRetired: (subscriptionAuthor != null && subscriptionAuthor.Retired)));
                modText.Append(ModNote(subscribedMod));
                modText.Append(Alternatives(subscribedMod));
                modText.Append(subscribedMod.ReportSeverity <= Enums.ReportSeverity.MajorIssues ? Recommendations(subscribedMod) : "");
                modText.Append(Format(subscribedMod.ReportSeverity == Enums.ReportSeverity.NothingToReport && subscribedMod.Stability > Enums.Stability.NotReviewed ? 
                    "No known issues or incompatibilities with your other mods." : ""));
                modText.Append(subscribedMod.IsCameraScript ? Format("This is a cinematic camera script, which technically is a mod and thus listed here.") : "");
                modText.Append(subscribedMod.SteamID <= ModSettings.HighestFakeID ? "" : $"\nSteam Workshop page: { Toolkit.GetWorkshopUrl(steamID) }\n");
                modText.AppendLine();
            }

            if (subscribedMod.ReportSeverity == Enums.ReportSeverity.Unsubscribe)
            {
                unsubscribe.Append(modText);
                unsubscribeCount++;
            }
            else if (subscribedMod.ReportSeverity == Enums.ReportSeverity.MajorIssues)
            {
                majorIssues.Append(modText);
                majorCount++;
            }
            else if (subscribedMod.ReportSeverity == Enums.ReportSeverity.MinorIssues)
            {
                minorIssues.Append(modText);
                minorCount++;
            }
            else if (subscribedMod.ReportSeverity == Enums.ReportSeverity.Remarks)
            {
                remarks.Append(modText);
                remarksCount++;
            }
            else
            {
                nothingToReport.Append(modText);
                nothingCount++;
            }
        }


        /// <summary>Formats a text for the report, including optional bullet and indenting.</summary>
        /// <remarks>The text will be wordwrapped, or optionally cutoff, at report width and end with a newline character.</remarks>
        /// <returns>A formatted string for the report.</returns>
        private string Format(string message, string bullet = ModSettings.Bullet1, bool cutOff = false)
        {
            message = $"{ bullet }{ message }";

            if (message.Length > ModSettings.TextReportWidth || message.Contains("\n"))
            {
                message = cutOff ? Toolkit.CutOff(message, ModSettings.TextReportWidth) : 
                    Toolkit.WordWrap(message, indent: new string(' ', bullet.Length), indentAfterNewLine: bullet);
            }

            return (message == bullet) ? "" : $"{ message }\n";
        }


        /// <summary>Creates report text for stability issues of a mod and increases the report severity for the mod if appropriate.</summary>
        /// <remarks>Only reports major issues and worse.</remarks>
        /// <returns>Formatted text.</returns>
        private string Instability(Mod subscribedMod)
        {
            string note = Format(subscribedMod.StabilityNote, ModSettings.Bullet2);

            switch (subscribedMod.Stability)
            {
                case Enums.Stability.IncompatibleAccordingToWorkshop:
                    subscribedMod.SetReportSeverity(Enums.ReportSeverity.Unsubscribe);
                    return Format("UNSUBSCRIBE! This mod is totally incompatible with the current game version.") + note;

                case Enums.Stability.RequiresIncompatibleMod:
                    subscribedMod.SetReportSeverity(Enums.ReportSeverity.Unsubscribe);
                    return Format("UNSUBSCRIBE! This requires a mod that is totally incompatible with the current game version.") + note;

                case Enums.Stability.GameBreaking:
                    subscribedMod.SetReportSeverity(Enums.ReportSeverity.Unsubscribe);
                    return Format("UNSUBSCRIBE! This mod breaks the game.") + note;

                case Enums.Stability.Broken:
                    subscribedMod.SetReportSeverity(Enums.ReportSeverity.Unsubscribe);
                    return Format("Unsubscribe! This mod is broken.") + note;

                case Enums.Stability.MajorIssues:
                    subscribedMod.SetReportSeverity(Enums.ReportSeverity.MajorIssues);
                    return Format($"Unsubscribe would be wise. This has major issues{ (string.IsNullOrEmpty(note) ? ". Check its Workshop page for details." : ":") }") + 
                        note;

                default:
                    return "";
            }
        }


        /// <summary>Creates report text for the stability of a mod and increases the report severity for the mod if appropriate.</summary>
        /// <remarks>Only reports minor issues and better.</remarks>
        /// <returns>Formatted text.</returns>
        private string Stability(Mod subscribedMod)
        {
            string updatedText = subscribedMod.GameVersion() < Toolkit.CurrentMajorGameVersion() ? "." :
                subscribedMod.GameVersion() == Toolkit.CurrentGameVersion() ? ", but it was updated for the current game version." :
                $", but it was updated for game version { subscribedMod.GameVersion().ToString(2) }.";

            string note = Format(subscribedMod.StabilityNote, ModSettings.Bullet2);

            switch (subscribedMod.Stability)
            {
                case Enums.Stability.MinorIssues:
                    subscribedMod.SetReportSeverity(Enums.ReportSeverity.MinorIssues);
                    return Format($"This has minor issues{ (string.IsNullOrEmpty(note) ? ". Check its Workshop page for details." : ":") }") + note;

                case Enums.Stability.UsersReportIssues:
                    subscribedMod.SetReportSeverity(Enums.ReportSeverity.MinorIssues);
                    return Format($"Users are reporting issues{ (string.IsNullOrEmpty(note) ? ". Check its Workshop page for details." : ": ") }") + note;

                case Enums.Stability.NotEnoughInformation:
                    subscribedMod.SetReportSeverity(Enums.ReportSeverity.Remarks);
                    return Format($"There is not enough information about this mod to know if it works well{ updatedText }") + note;

                case Enums.Stability.Stable:
                    subscribedMod.SetReportSeverity(string.IsNullOrEmpty(note) ? Enums.ReportSeverity.NothingToReport : Enums.ReportSeverity.Remarks);
                    return Format($"This is compatible with the current game version.") + note;

                case Enums.Stability.NotReviewed:
                case Enums.Stability.Undefined:
                    return Format($"This mod has not been reviewed yet{ updatedText }");

                default:
                    return "";
            }
        }


        /// <summary>Creates report text for the statuses of a mod and increases the report severity for the mod if appropriate.</summary>
        /// <remarks>Also reported: retired author. DependencyMod has its own method. Not reported: UnlistedInWorkshop, SourceObfuscated.</remarks>
        /// <returns>Formatted text, or an empty string if no reported status found.</returns>
        private string Statuses(Mod subscribedMod, bool authorRetired)
        {
            string text = "";

            if (subscribedMod.Statuses.Contains(Enums.Status.Obsolete))
            {
                subscribedMod.SetReportSeverity(Enums.ReportSeverity.Unsubscribe);
                text += Format("Unsubscribe this. It is no longer needed.");
            }
            else if (subscribedMod.Statuses.Contains(Enums.Status.RemovedFromWorkshop))
            {
                subscribedMod.SetReportSeverity(Enums.ReportSeverity.MajorIssues);
                text += Format("Unsubscribe would be wise. This is no longer available on the Steam Workshop.");
            }
            else if (subscribedMod.Statuses.Contains(Enums.Status.Deprecated))
            {
                subscribedMod.SetReportSeverity(Enums.ReportSeverity.MajorIssues);
                text += Format("Unsubscribe would be wise. This is deprecated and no longer supported by the author.");
            }
            else if (subscribedMod.Statuses.Contains(Enums.Status.Abandoned))
            {
                text += authorRetired ? Format("This seems to be abandoned and the author seems retired. Future updates are unlikely.") :
                    Format("This seems to be abandoned. Future updates are unlikely.");
            }
            else if (authorRetired)
            {
                text += Format("The author seems to be retired. Future updates are unlikely.");
            }

            if (subscribedMod.ReportSeverity < Enums.ReportSeverity.Unsubscribe)
            {
                // Several statuses only listed if there are no breaking issues.
                if (subscribedMod.Statuses.Contains(Enums.Status.Reupload))
                {
                    subscribedMod.SetReportSeverity(Enums.ReportSeverity.Unsubscribe);
                    text += Format("Unsubscribe this. It is a re-upload of another mod, use that one instead (or its successor).");
                }

                if (subscribedMod.Statuses.Contains(Enums.Status.NoDescription))
                {
                    subscribedMod.SetReportSeverity(Enums.ReportSeverity.MinorIssues);
                    text += Format("This has no description on the Steam Workshop. Support from the author is unlikely.");
                }

                if (subscribedMod.Statuses.Contains(Enums.Status.NoCommentSection))
                {
                    subscribedMod.SetReportSeverity(Enums.ReportSeverity.MinorIssues);
                    text += Format("This mod has the comment section disabled on the Steam Workshop, making it hard to see if other users are experiencing issues. " +
                        "Use with caution.");
                }

                if (subscribedMod.Statuses.Contains(Enums.Status.BreaksEditors))
                {
                    subscribedMod.SetReportSeverity(Enums.ReportSeverity.Remarks);
                    text += Format("If you use the asset editor and/or map editor, this may give serious issues.");
                }

                if (subscribedMod.Statuses.Contains(Enums.Status.ModForModders))
                {
                    text += Format("This is only needed for modders. Regular users don't need this one.");
                }

                if (subscribedMod.Statuses.Contains(Enums.Status.TestVersion))
                {
                    text += Format("This is a test version" + 
                        (subscribedMod.Alternatives.Any() ? ". If you don't have a specific reason to use it, you'd better use the stable version instead." :
                        subscribedMod.Stability == Enums.Stability.Stable ? ", but is considered quite stable.": "."));
                }

                if (subscribedMod.Statuses.Contains(Enums.Status.MusicCopyrightFree))
                {
                    text += Format("The included music is said to be copyright-free and safe for streaming. Some restrictions might still apply though.");
                }
                else if (subscribedMod.Statuses.Contains(Enums.Status.MusicCopyrighted))
                {
                    text += Format("This includes copyrighted music and should not be used for streaming.");
                }
                else if (subscribedMod.Statuses.Contains(Enums.Status.MusicCopyrightUnknown))
                {
                    text += Format("This includes music with unknown copyright status. Safer not to use it for streaming.");
                }
            }

            if (subscribedMod.Statuses.Contains(Enums.Status.SavesCantLoadWithout))
            {
                text += Format("NOTE: After using this mod, savegames won't (easily) load without it anymore.");
            }

            bool abandoned = subscribedMod.Statuses.Contains(Enums.Status.Obsolete) || subscribedMod.Statuses.Contains(Enums.Status.Deprecated) ||
                subscribedMod.Statuses.Contains(Enums.Status.RemovedFromWorkshop) || subscribedMod.Statuses.Contains(Enums.Status.Abandoned) ||
                (subscribedMod.Stability == Enums.Stability.IncompatibleAccordingToWorkshop) || authorRetired;

            if (abandoned && string.IsNullOrEmpty(subscribedMod.SourceUrl) && !subscribedMod.Statuses.Contains(Enums.Status.SourceBundled))
            {
                text += Format($"No public source code found, making it hard to continue by another modder.");
            }
            else if (abandoned && subscribedMod.Statuses.Contains(Enums.Status.SourceNotUpdated))
            {
                text += Format($"Published source seems out of date, making it hard to continue by another modder.");
            }

            if (!string.IsNullOrEmpty(text))
            {
                subscribedMod.SetReportSeverity(Enums.ReportSeverity.Remarks);
            }

            return text;
        }


        /// <summary>Creates report text for an unneeded dependency mod and increases the report severity for the mod if appropriate.</summary>
        /// <remarks>If this mod is a member of a group, all group members are considered for this check.</remarks>
        /// <returns>Formatted text, or an empty string if this is not a dependency mod or if another subscription has this mod as required.</returns>
        private string UnneededDependencyMod(Mod subscribedMod)
        {
            if (!subscribedMod.Statuses.Contains(Enums.Status.DependencyMod))
            {
                return "";
            }

            // Check if any of the mods that need this is actually subscribed, enabled or not. If this is a member of a group, check all group members. Exit if any is needed.
            if (catalog.IsGroupMember(subscribedMod.SteamID))
            {
                foreach (ulong groupMemberID in catalog.GetThisModsGroup(subscribedMod.SteamID).GroupMembers)
                {
                    if (IsModNeeded(groupMemberID))
                    {
                        // Group member is needed. No need to check other group members.
                        return "";
                    }
                }
            }
            else if (IsModNeeded(subscribedMod.SteamID))
            {
                return "";
            }

            if (catalog.IsValidID(ModSettings.LowestLocalModID))
            {
                subscribedMod.SetReportSeverity(Enums.ReportSeverity.Remarks);

                return Format("Unsubscribe this unless it's needed for one of your local mods. " +
                    "None of your Steam Workshop mods need this, and it doesn't provide any functionality on its own.");
            }
            else
            {
                subscribedMod.SetReportSeverity(Enums.ReportSeverity.Unsubscribe);

                return Format("Unsubscribe this. It is only needed for mods you don't have, and it doesn't provide any functionality on its own.");
            }
        }


        /// <summary>Checks if any of the mods that need this is actually subscribed, enabled or not.</summary>
        /// <returns>True if a mod needs this, otherwise false.</returns>
        private bool IsModNeeded(ulong SteamID)
        {
            // Check if any of the mods that need this is actually subscribed, enabled or not.
            List<Mod> ModsRequiringThis = catalog.Mods.FindAll(x => x.RequiredMods.Contains(SteamID));

            foreach (Mod mod in ModsRequiringThis)
            {
                if (catalog.GetSubscription(mod.SteamID) != null)
                {
                    // Found a subscribed mod that needs this.
                    return true;
                }
            }

            return false;
        }


        /// <summary>Creates report text for a disabled mod and increases the report severity for the mod if appropriate.</summary>
        /// <returns>Formatted text, or an empty string if not disabled.</returns>
        private string Disabled(Mod subscribedMod)
        {
            if (!subscribedMod.IsDisabled)
            {
                return "";
            }

            subscribedMod.SetReportSeverity(Enums.ReportSeverity.Unsubscribe);

            return Format("Unsubscribe this, or enable it if you want to use it. Disabled mods can still cause issues.");
        }


        /// <summary>Creates report text for a mod not and increases the report severity for the mod if appropriate.</summary>
        /// <returns>Formatted text, or an empty string if no mod note exists.</returns>
        private string ModNote(Mod subscribedMod)
        {
            if (string.IsNullOrEmpty(subscribedMod.Note))
            {
                return "";
            }

            subscribedMod.SetReportSeverity(Enums.ReportSeverity.Remarks);

            return Format(subscribedMod.Note);
        }
        

        /// <summary>Creates report text for missing DLCs for a mod and increases the report severity for the mod if appropriate.</summary>
        /// <returns>Formatted text, or an empty string if no DLC is required or if all required DLCs are installed.</returns>
        private string RequiredDlc(Mod subscribedMod)
        {
            string dlcs = "";

            foreach (Enums.Dlc dlc in subscribedMod.RequiredDlcs)
            {
                if (!PlatformService.IsDlcInstalled((uint)dlc))
                {
                    // Add the missing DLC.
                    dlcs += Format(Toolkit.ConvertDlcToString(dlc), ModSettings.Bullet2);
                }
            }

            if (string.IsNullOrEmpty(dlcs))
            {
                return "";
            }

            subscribedMod.SetReportSeverity(Enums.ReportSeverity.Unsubscribe);

            return Format("Unsubscribe this. It requires DLC you don't have:") + dlcs;
        }


        /// <summary>Creates report text for missing 'required mods' for a mod and increases the report severity for the mod if appropriate.</summary>
        /// <remarks>If a required mod is not subscribed but in a group, the other group members are checked. 
        ///          Required mods that are disabled are mentioned as such.</remarks>
        /// <returns>Formatted text, or an empty string if this requires no other mods or all required mods are subscribed and enabled.</returns>
        private string RequiredMods(Mod subscribedMod)
        {
            string text = "";

            foreach (ulong steamID in subscribedMod.RequiredMods)
            {
                if (catalog.IsValidID(steamID))
                {
                    text += CheckModAndGroup(steamID);
                }
                else
                {
                    // Mod not found in the catalog, which should not happen unless bugs or a manual edit of the catalog.
                    Logger.Log($"Required mod { steamID } not found in catalog.", Logger.Debug);

                    text += Format($"[Steam ID { steamID, 10 }]", ModSettings.Bullet2);
                }
            }

            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            subscribedMod.SetReportSeverity(Enums.ReportSeverity.MajorIssues);

            return Format("This mod requires other mods you don't have, or which are not enabled:") + text;
        }


        /// <summary>Creates report text for recommended mods for a mod and increases the report severity for the mod if appropriate.</summary>
        /// <remarks>If a recommended mod is not subscribed but in a group, the other group members are checked. 
        ///          Recommended mods that are disabled are mentioned as such.</remarks>
        /// <returns>Formatted text, or an empty string if this mod has no recommendations or all recommended mods are subscribed and enabled.</returns>
        private string Recommendations(Mod subscribedMod)
        {
            string text = "";

            foreach (ulong steamID in subscribedMod.Recommendations)
            {
                if (catalog.IsValidID(steamID))
                {
                    text += CheckModAndGroup(steamID);
                }
                else
                {
                    // Mod not found in the catalog, which should not happen unless bugs or a manual edit of the catalog.
                    Logger.Log($"Recommended mod { steamID } not found in catalog.", Logger.Debug);

                    text += Format($"[Steam ID { steamID,10 }]", ModSettings.Bullet2);
                }
            }

            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            subscribedMod.SetReportSeverity(Enums.ReportSeverity.Remarks);

            return Format("The author of this mod recommends using the following as well:") + text;
        }


        /// <summary>Checks if a mod or any member of its group is subscribed and enabled.</summary>
        /// <returns>A string with text for the report, or an empty string if the mod or another group member is subscribed and enabled.</returns>
        private string CheckModAndGroup(ulong steamID)
        {
            if (catalog.GetSubscription(steamID) != null && !catalog.GetSubscription(steamID).IsDisabled)
            {
                // Mod is subscribed and enabled. Don't report.
                return "";
            }
            else if (catalog.GetMod(steamID).IsDisabled)
            {
                // Mod is subscribed and disabled. Report as "missing", without Workshop page.
                return Format(catalog.GetMod(steamID).ToString(hideFakeID: true), ModSettings.Bullet2, cutOff: true);
            }
            else if (!catalog.IsGroupMember(steamID))
            {
                // Mod is not subscribed and not in a group. Report as missing with Workshop page.
                return Format(catalog.GetMod(steamID).ToString(hideFakeID: true), ModSettings.Bullet2, cutOff: true) +
                    Format($"Workshop page: { Toolkit.GetWorkshopUrl(steamID) }", ModSettings.Indent2);
            }
            else
            {
                // Mod is not subscribed but in a group. Check if another group member is subscribed.
                foreach (ulong groupMemberID in catalog.GetThisModsGroup(steamID).GroupMembers)
                {
                    if (catalog.GetSubscription(groupMemberID) != null)
                    {
                        // Group member is subscribed. No need to check other group members, but report as "missing" if disabled.
                        return !catalog.GetMod(groupMemberID).IsDisabled ? "" : 
                            Format(catalog.GetMod(groupMemberID).ToString(hideFakeID: true), ModSettings.Bullet2, cutOff: true);
                    }
                }

                // No group member is subscribed. Report original mod as missing.
                return Format(catalog.GetMod(steamID).ToString(hideFakeID: true), ModSettings.Bullet2, cutOff: true) +
                    Format($"Workshop page: { Toolkit.GetWorkshopUrl(steamID) }", ModSettings.Indent2);
            }
        }


        /// <summary>Creates report text for successors of a mod and increases the report severity for the mod if appropriate.</summary>
        /// <returns>Formatted text, or an empty string if this mod has no successors.</returns>
        private string Successors(Mod subscribedMod)
        {
            if (!subscribedMod.Successors.Any())
            {
                return "";
            }

            string text = (subscribedMod.Successors.Count == 1) ? Format("This is succeeded by:") : Format("This is succeeded by any of the following (pick one, not all):");

            foreach (ulong steamID in subscribedMod.Successors)
            {
                if (catalog.IsValidID(steamID))
                {
                    if (catalog.GetSubscription(steamID) != null)
                    {
                        subscribedMod.SetReportSeverity(Enums.ReportSeverity.Unsubscribe);

                        return Format("Unsubscribe this. It is succeeded by a mod you already have:") +
                            Format(catalog.GetMod(steamID).ToString(hideFakeID: true), ModSettings.Bullet2, cutOff: true);
                    }

                    text += Format(catalog.GetMod(steamID).ToString(hideFakeID: true), ModSettings.Bullet2, cutOff: true) +
                        Format($"Workshop page: { Toolkit.GetWorkshopUrl(steamID) }", ModSettings.Indent2);
                }
                else
                {
                    // Mod not found in the catalog, which should not happen unless bugs or a manual edit of the catalog.
                    Logger.Log($"Successor mod { steamID } not found in catalog.", Logger.Debug);

                    text += Format($"[Steam ID { steamID, 10 }]", ModSettings.Bullet2);
                }
            }

            subscribedMod.SetReportSeverity(Enums.ReportSeverity.MinorIssues);

            return text;
        }


        /// <summary>Creates report text for alternatives for a mod and increases the report severity for the mod if appropriate.</summary>
        /// <returns>Formatted text, or an empty string if this mod has no alternatives.</returns>
        private string Alternatives(Mod subscribedMod)
        {
            if (!subscribedMod.Alternatives.Any())
            {
                return "";
            }

            string text = (subscribedMod.Alternatives.Count == 1) ? Format("An alternative you could use:") : Format("Some alternatives for this are (pick one, not all):");

            foreach (ulong steamID in subscribedMod.Alternatives)
            {
                if (catalog.GetSubscription(steamID) != null && !catalog.GetSubscription(steamID).IsDisabled)
                {
                    // Already subscribed, don't report any.
                    return "";
                }

                if (catalog.IsValidID(steamID))
                {
                    text += Format(catalog.GetMod(steamID).ToString(hideFakeID: true), ModSettings.Bullet2, cutOff: true) +
                        Format($"Workshop page: { Toolkit.GetWorkshopUrl(steamID) }", ModSettings.Indent2);
                }
                else
                {
                    // Mod not found in the catalog, which should not happen unless bugs or a manual edit of the catalog.
                    Logger.Log($"Alternative mod { steamID } not found in catalog.", Logger.Debug);

                    text += Format($"[Steam ID { steamID, 10 }]", ModSettings.Bullet2);
                }
            }

            subscribedMod.SetReportSeverity(Enums.ReportSeverity.Remarks);

            return text;
        }


        /// <summary>Creates report text for compatibility issues with other subscribed mods, and increases the report severity for the mod if appropriate.</summary>
        /// <remarks>Result could be multiple mods with multiple statuses. Not reported: CompatibleAccordingToAuthor.</remarks>
        /// <returns>Formatted text, or an empty string if there are no known compatibility issues.</returns>
        private string Compatibilities(Mod subscribedMod)
        {
            string text = "";

            foreach (Compatibility compatibility in catalog.GetSubscriptionCompatibilities(subscribedMod.SteamID))
            {
                ulong otherModID = (subscribedMod.SteamID == compatibility.FirstModID) ? compatibility.SecondModID : compatibility.FirstModID;
                string otherMod = Format(catalog.GetMod(otherModID).ToString(hideFakeID: true), ModSettings.Bullet2, cutOff: true);
                string workshopUrl = Format($"Workshop page: { Toolkit.GetWorkshopUrl(otherModID) }", ModSettings.Indent2);

                string note = Format(compatibility.Note, ModSettings.Indent2);

                switch (compatibility.Status)
                {
                    case Enums.CompatibilityStatus.SameModDifferentReleaseType:
                        // This status is only reported for the second mod (the 'test' mod) in the compatibility.
                        if (subscribedMod.SteamID == compatibility.SecondModID)
                        {
                            subscribedMod.SetReportSeverity(Enums.ReportSeverity.Unsubscribe);
                            text += Format("Unsubscribe this. You're already subscribe to another edition of the same mod:") + otherMod + note;
                        }
                        break;

                    case Enums.CompatibilityStatus.SameFunctionality:
                        subscribedMod.SetReportSeverity(Enums.ReportSeverity.Unsubscribe);
                        text += Format("Unsubscribe either this one or the following mod with the same functionality:") + otherMod + workshopUrl + note;
                        break;

                    case Enums.CompatibilityStatus.IncompatibleAccordingToAuthor:
                        subscribedMod.SetReportSeverity(Enums.ReportSeverity.Unsubscribe);
                        text += Format("Unsubscribe either this one or the following mod it's incompatible with:") + otherMod + workshopUrl + note;
                        break;

                    case Enums.CompatibilityStatus.IncompatibleAccordingToUsers:
                        subscribedMod.SetReportSeverity(Enums.ReportSeverity.MajorIssues);
                        text += Format("Users report an incompatibility with:") + otherMod + workshopUrl + note;
                        break;

                    case Enums.CompatibilityStatus.MajorIssues:
                        subscribedMod.SetReportSeverity(Enums.ReportSeverity.MajorIssues);
                        text += Format("This has major issues with:") + otherMod + workshopUrl + note;
                        break;

                    case Enums.CompatibilityStatus.MinorIssues:
                        subscribedMod.SetReportSeverity(Enums.ReportSeverity.MinorIssues);
                        text += Format("This has minor issues with:") + otherMod + workshopUrl + note;
                        break;

                    case Enums.CompatibilityStatus.RequiresSpecificSettings:
                        subscribedMod.SetReportSeverity(Enums.ReportSeverity.Remarks);
                        text += Format("This requires specific configuration to work together with:") + otherMod + workshopUrl + note;
                        break;

                    case Enums.CompatibilityStatus.SameFunctionalityCompatible:
                        if (subscribedMod.Successors.Contains(otherModID))
                        {
                            // Don't mention this if the other mod is the successor, to avoid duplicate mentions of that mod.
                            break;
                        }
                        subscribedMod.SetReportSeverity(Enums.ReportSeverity.Remarks);
                        text += Format("This has very similar functionality, but is still compatible with (do you need both?):") + otherMod + workshopUrl + note;
                        break;

                    case Enums.CompatibilityStatus.CompatibleAccordingToAuthor:
                        if (!string.IsNullOrEmpty(note))
                        {
                            subscribedMod.SetReportSeverity(Enums.ReportSeverity.Remarks);
                            text += Format("This is compatible with:") + otherMod + workshopUrl + note;
                        }
                        break;

                    default:
                        break;
                }
            }

            return text;
        }
    }
}
