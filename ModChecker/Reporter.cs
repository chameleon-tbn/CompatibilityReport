using ColossalFramework.PlatformServices;
using ModChecker.DataTypes;
using ModChecker.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace ModChecker
{
    internal static class Reporter
    {
        // Strings to collect the review text for all mods
        private static StringBuilder reviewedModsText;
        private static StringBuilder nonReviewedModsText;


        // Create the report(s)
        internal static void Create()
        {
            // Initiate the strings
            reviewedModsText = new StringBuilder();
            nonReviewedModsText = new StringBuilder();

            // Date and time the reports are created
            DateTime createTime = DateTime.Now;

            // Create the HTML report if selected in settings
            if (ModSettings.HtmlReport)
            {
                CreateHtml(createTime);
            }

            // Reset the strings for re-use
            reviewedModsText = new StringBuilder();
            nonReviewedModsText = new StringBuilder();

            // Create the text report if selected in settings, or if somehow no report was selected in options
            if (ModSettings.TextReport || !ModSettings.HtmlReport)
            {
                CreateText(createTime);
            }

            // Clean up memory
            reviewedModsText = null;
            nonReviewedModsText = null;
        }


        // Create HTML report
        private static void CreateHtml(DateTime createTime)
        {
            // [Todo 0.6]

            // Logger.Log($"HTML report ready at \"{ Tools.PrivacyPath(ModSettings.ReportHtmlFullPath) }\".", duplicateToGameLog: true);
        }


        // Create text report
        private static void CreateText(DateTime createTime)
        {
            // Keep track of the number of mods without a review in the catalog, but with remarks to report
            uint modsWithRemarks = 0;

            // Mod name and report date
            Logger.TextReport($"{ ModSettings.modName } report, created on { createTime:D}, { createTime:t}.", extraLine: true);

            // Mod version, catalog version and number of mods in the catalog and in game
            Logger.TextReport($"Version { ModSettings.shortVersion } with catalog { ActiveCatalog.Instance.VersionString() }. The catalog contains " + 
                $"{ ActiveCatalog.Instance.ReviewCount } reviewed mods and " + $"{ ActiveCatalog.Instance.Count - ActiveCatalog.Instance.ReviewCount } mods\n" +
                $"with basic information. Your game has { Subscription.AllSubscriptions.Count } mods.", extraLine: true);

            // Generic note from the catalog
            Logger.TextReport(ActiveCatalog.Instance.Note, extraLine: true);

            // Special note about specific game versions; will be empty for almost everyone
            Logger.TextReport(GameVersion.SpecialNote, extraLine: true);

            // Warning about game version mismatch
            if (GameVersion.Current != ActiveCatalog.Instance.CompatibleGameVersion)
            {
                string olderNewer = (GameVersion.Current < ActiveCatalog.Instance.CompatibleGameVersion) ? "older" : "newer";
                string catalogGameVersion = GameVersion.Formatted(ActiveCatalog.Instance.CompatibleGameVersion);

                Logger.TextReport($"WARNING: The review catalog is made for game version { catalogGameVersion }. Your game is { olderNewer }.\n" + 
                               "         Results may not be accurate.", extraLine: true);
            }

            Logger.TextReport(ModSettings.separatorDouble, extraLine: true);

            // Intro text with generic remarks
            Logger.TextReport(string.IsNullOrEmpty(ActiveCatalog.Instance.ReportIntroText) ? 
                ModSettings.defaultIntroText : 
                ActiveCatalog.Instance.ReportIntroText, extraLine: true);

            // Gather all mod detail texts
            if (ModSettings.ReportSortByName)
            {
                // Sort by name
                foreach (string name in Subscription.AllNames)
                {
                    // Get the Steam ID(s) for this mod name; could be multiple (which will be sorted by Steam ID)
                    foreach (ulong steamID in Subscription.AllNamesAndIDs[name])
                    {
                        // Get the mod text, and increase the counter if it was a mod without review but with remarks
                        if (GetModText(steamID, nameFirst: true))
                        {
                            modsWithRemarks++;
                        }
                    }
                }
            }
            else
            {
                // Sort by Steam ID
                foreach (ulong steamID in Subscription.AllSteamIDs)
                {
                    // Get the mod text, and increase the counter if it was a mod without review but with remarks
                    if (GetModText(steamID, nameFirst: false))
                    {
                        modsWithRemarks++;
                    }
                }
            }

             // Log detail of reviewed mods and other mods with issues
            if (reviewedModsText.Length > 0)
            {
                Logger.TextReport(ModSettings.separatorDouble);

                Logger.TextReport($"REVIEWED MODS ({ Subscription.TotalReviewed })" + 
                    (modsWithRemarks == 0 ? ":" : $" AND OTHER MODS WITH REMARKS ({ modsWithRemarks }): "));

                Logger.TextReport(reviewedModsText.ToString());
            }

            // Log details of non-reviewed mods
            if (nonReviewedModsText.Length > 0)
            {
                Logger.TextReport(ModSettings.separatorDouble);

                Logger.TextReport($"MODS NOT REVIEWED YET ({ Subscription.AllSubscriptions.Count - Subscription.TotalReviewed - modsWithRemarks }):");

                Logger.TextReport(nonReviewedModsText.ToString());
            }

            Logger.TextReport(ModSettings.separatorDouble, extraLine: true);

            // Footer text
            Logger.TextReport(string.IsNullOrEmpty(ActiveCatalog.Instance.ReportFooterText) ? ModSettings.defaultFooterText : ActiveCatalog.Instance.ReportFooterText);

            // Log the report location
            Logger.Log($"Text report ready at \"{ Tools.PrivacyPath(ModSettings.ReportTextFullPath) }\".", duplicateToGameLog: true);
        }


        // Get report text for one mod; not reported: SourceURL, Updated, Downloaded
        // Return value indicates whether we found a mod without a review in the catalog, but with remarks to report
        private static bool GetModText(ulong steamID,
                                       bool nameFirst)
        {
            // Exit if the Steam ID is 0 (meaning we ran out of fake IDs for local or builtin mods)
            if (steamID == 0)
            {
                return false;
            }

            // Get the mod
            Subscription subscription = Subscription.AllSubscriptions[steamID];

            // Start with a separator
            string modHeader = ModSettings.separator + "\n\n";

            // Mod name and Steam ID
            string modName = subscription.ToString(nameFirst, showFakeID: false);

            // Authorname
            if (string.IsNullOrEmpty(subscription.AuthorName))
            {
                // Author unknown
                modHeader += modName + "\n";
            }
            else
            {
                if (modName.Length + 4 + subscription.AuthorName.Length <= ModSettings.maxReportWidth)
                {
                    // Author on the same line as mod name and Steam ID
                    modHeader += modName + " by " + subscription.AuthorName + "\n";
                }
                else
                {
                    // Author right aligned on a new line under mod name and Steam ID
                    modHeader += modName + "\n" + $"by { subscription.AuthorName }".PadLeft(ModSettings.maxReportWidth) + "\n";
                }
            }

            // Gather the review text
            StringBuilder modReview = new StringBuilder();

            modReview.Append(ThisMod(subscription));
            modReview.Append(RequiredDLC(subscription));
            modReview.Append(RequiredMods(subscription));
            modReview.Append(OnlyNeededFor(subscription));
            modReview.Append(Compatibilities(subscription));
            modReview.Append(Statuses(subscription));
            modReview.Append(Disabled(subscription));
            modReview.Append(RetiredAuthor(subscription));
            modReview.Append(GameVersionCompatible(subscription));
            modReview.Append(Successors(subscription));
            modReview.Append(Alternatives(subscription));
            modReview.Append(CameraScript(subscription));
            modReview.Append(Note(subscription));

            // If the review text is not empty, then we found something to report
            bool somethingToReport = modReview.Length > 0;

            // Insert the 'not reviewed' text at the start of the text; we do this after the above boolean has been set
            modReview.Insert(0, NotReviewed(subscription));

            // Make sure every mod has some text to display
            if (modReview.Length == 0)
            {
                modReview.Append(ReviewLine("Nothing to report", htmlReport: false));
            }
            
            // Workshop url for Workshop mods
            modReview.Append((steamID > ModSettings.highestFakeID) ? ReviewLine("Steam Workshop page: " + Tools.GetWorkshopURL(steamID), htmlReport: false) : "");

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


        // Format one line for the text or html review; including bullets, indenting and max. width
        // [Todo 0.6] Change for html with unordered list for bullets, etc.
        private static string ReviewLine(string message,
                                         bool htmlReport,
                                         string bullet = null,
                                         bool cutOff = true)
        {
            if (string.IsNullOrEmpty(message))
            {
                return "";
            }
            
            bullet = bullet ?? ModSettings.bullet;

            if (cutOff && ((bullet + message).Length > ModSettings.maxReportWidth))
            {
                // Cut off the message, so the 'bulleted' message stays within maximum width
                message = message.Substring(0, ModSettings.maxReportWidth - bullet.Length - 3) + "...";

                Logger.Log($"Report line too long: " + message, Logger.debug);
            }

            // Return 'bulleted' message
            if (htmlReport)
            {
                return bullet + message + "<br>";
            }
            else
            {
                return bullet + message + "\n";
            }            
        }


        // Say hi to ourselves
        private static string ThisMod(Subscription subscription,
                                      bool htmlReport = false)
        {
            if (subscription.SteamID != ModSettings.modCheckerSteamID)
            {
                return "";
            }

            return ReviewLine("This mod.", htmlReport);
        }


        // Not reviewed; local mods get a slightly different text
        private static string NotReviewed(Subscription subscription,
                                          bool htmlReport = false)
        {
            if (subscription.IsReviewed)
            {
                return "";
            }

            return subscription.IsLocal && !subscription.IsBuiltin ? ReviewLine("Can't review local mods (yet).", htmlReport) : ReviewLine("Not reviewed yet.", htmlReport);
        }


        // Cinematic camera script
        private static string CameraScript(Subscription subscription,
                                           bool htmlReport = false)
        {
            if (!subscription.IsCameraScript)
            {
                return "";
            }
            
            return ReviewLine("This is a cinematic camera script, which technically is a mod and thus listed here.", htmlReport);
        }


        // General note for this mod
        private static string Note(Subscription subscription,
                                   bool htmlReport = false)
        {
            return ReviewLine(subscription.Note, htmlReport);
        }


        // Game version compatible, only listed for current version
        private static string GameVersionCompatible(Subscription subscription,
                                                    bool htmlReport = false)
        {
            if (subscription.GameVersionCompatible != GameVersion.Current)
            {
                return "";
            }

            return ReviewLine($"This mod was created or updated for the current game version, { GameVersion.Formatted(GameVersion.Current) }.", htmlReport);
        }


        // Disabled mod
        private static string Disabled(Subscription subscription,
                                       bool htmlReport = false)
        {
            if (subscription.IsEnabled)
            {
                return "";
            }

            return ReviewLine("Mod is disabled. Unsubscribe it if not used. Disabled mods can still cause issues.", htmlReport);
        }


        // Author is retired
        private static string RetiredAuthor(Subscription subscription,
                                            bool htmlReport = false)
        {
            if (!subscription.AuthorIsRetired)
            {
                return "";
            }

            return ReviewLine("The author seems to be retired. Updates are unlikely.", htmlReport);
        }


        // Unneeded dependency mod
        private static string OnlyNeededFor(Subscription subscription,
                                            bool htmlReport = false)
        {
            if (subscription.NeededFor?.Any() != true)
            {
                return "";
            }

            // Check if any of the mods that need this is actually subscribed; we don't care if it's enabled or not
            foreach (ulong id in subscription.NeededFor)
            {
                if (Subscription.AllSubscriptions.ContainsKey(id))
                {
                    // Found a subscribed mod that needs this; nothing to report
                    return "";
                }
            }

            return ReviewLine("You can probably unsubscribe. This is only needed for mods you don't seem to have.", htmlReport);
        }            


        // Successor(s)
        private static string Successors(Subscription subscription,
                                         bool htmlReport = false)
        {            
            if (subscription.SucceededBy?.Any() != true)
            {
                return "";
            }

            string text = (subscription.SucceededBy.Count == 1) ? ReviewLine("This is succeeded by:", htmlReport) : 
                ReviewLine("This is succeeded by any of the following (pick one, not all):", htmlReport);
            
            // List all successor mods
            foreach (ulong id in subscription.SucceededBy)
            {
                if (ActiveCatalog.Instance.ModDictionary.ContainsKey(id))
                {
                    // Mod found in the catalog, list Steam ID and name
                    text += ReviewLine(ActiveCatalog.Instance.ModDictionary[id].ToString(showFakeID: false), htmlReport, ModSettings.bullet2);
                }
                else
                {
                    // Mod not found in the catalog, which should not happen unless manually editing the catalog
                    text += ReviewLine($"[Steam ID { id, 10 }] { subscription.Name }", htmlReport, ModSettings.bullet2);

                    Logger.Log($"Successor mod { id } not found in catalog.", Logger.warning);
                }
            }

            return text;
        }


        // Alternative mods
        private static string Alternatives(Subscription subscription,
                                           bool htmlReport = false)
        {
            if (subscription.Alternatives?.Any() != true)
            {
                return "";
            }

            string text = subscription.Alternatives.Count == 1 ? ReviewLine("An alternative you could use:", htmlReport) : 
                ReviewLine("Some alternatives for this are (pick one, not all):", htmlReport);

            // List all alternative mods
            foreach (ulong id in subscription.Alternatives)
            {
                if (ActiveCatalog.Instance.ModDictionary.ContainsKey(id))
                {
                    // Mod found in the catalog, list Steam ID and name
                    text += ReviewLine(ActiveCatalog.Instance.ModDictionary[id].ToString(showFakeID: false), htmlReport, ModSettings.bullet2);
                }
                else
                {
                    // Mod not found in the catalog, which should not happen unless manually editing the catalog
                    text += ReviewLine($"[Steam ID { id,10 }] { subscription.Name }", htmlReport, ModSettings.bullet2);

                    Logger.Log($"Alternative mod { id } not found in catalog.", Logger.warning);
                }
            }
                
            return text;
        }


        // Required DLC
        private static string RequiredDLC(Subscription subscription,
                                          bool htmlReport = false)
        {          
            if (subscription.RequiredDLC?.Any() != true)
            {
                return "";
            }
            
            string dlcs = "";

            // Check every required DLC against installed DLC
            foreach (Enums.DLC dlc in subscription.RequiredDLC)
            {
                if (!PlatformService.IsDlcInstalled((uint) dlc))
                {
                    // Add the missing dlc, replacing the underscores in the DLC enum name with spaces and semicolons
                    dlcs += ReviewLine(dlc.ToString().Replace("__", ": ").Replace('_', ' '), htmlReport, ModSettings.bullet2);
                }
            }

            if (string.IsNullOrEmpty(dlcs))
            {
                return "";
            }

            return ReviewLine($"Unsubscribe. This requires DLC you don't have:", htmlReport) + dlcs;
        }


        // Required mods, including the use of mod groups
        private static string RequiredMods(Subscription subscription,
                                           bool htmlReport = false)
        {
            if (subscription.RequiredMods?.Any() != true)
            {
                return "";
            }

            // Strings to collect the text
            string header = ReviewLine("This mod requires other mods you don't have, or which are not enabled:", htmlReport);
            string text = "";

            // Check every required mod
            foreach (ulong id in subscription.RequiredMods)
            {
                // Check if it's a regular mod or a mod group
                if ((id < ModSettings.lowestModGroupID) || (id > ModSettings.highestModGroupID))
                {
                    // Regular mod. Try to find it in the list of subscribed mods
                    if (Subscription.AllSubscriptions.ContainsKey(id))
                    {
                        // Mod is subscribed
                        if (!Subscription.AllSubscriptions[id].IsEnabled)
                        {
                            // Mod is subscribed, but not enabled
                            text += ReviewLine(Subscription.AllSubscriptions[id].ToString(showFakeID: false, showDisabled: true), htmlReport, ModSettings.bullet2);
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
                        if (ActiveCatalog.Instance.ModDictionary.ContainsKey(id))
                        {
                            // Mod found in the catalog
                            text += ReviewLine(ActiveCatalog.Instance.ModDictionary[id].ToString(showFakeID: false), htmlReport, ModSettings.bullet2);
                        }
                        else
                        {
                            // Mod not found in the catalog, which should not happen unless manually editing the catalog
                            text += ReviewLine($"[Steam ID { id,10 }] { subscription.Name }", htmlReport, ModSettings.bullet2);

                            Logger.Log($"Required mod { id } not found in catalog.", Logger.warning);
                        }

                        // List the workshop page for easy subscribing
                        text += ReviewLine("Workshop page: " + Tools.GetWorkshopURL(id), htmlReport, ModSettings.noBullet2);

                        continue;   // To the next required mod
                    }
                }
                else
                {
                    // Mod group. We have to dig a little deeper. First some error checks
                    if (!ActiveCatalog.Instance.ModGroupDictionary.ContainsKey(id))
                    {
                        // Group not found in catalog, which should not happen unless manually editing the catalog
                        text += ReviewLine("one of the following mods: <missing information in catalog>", htmlReport, ModSettings.bullet2);

                        Logger.Log($"Group { id } not found in catalog.", Logger.error);

                        continue;   // To the next required mod
                    }
                    else if (ActiveCatalog.Instance.ModGroupDictionary[id].SteamIDs?.Any() != true)
                    {
                        // Group contains no Steam IDs, which should not happen unless manually editing the catalog
                        text += ReviewLine("one of the following mods: <missing information in catalog>", htmlReport, ModSettings.bullet2);

                        Logger.Log($"Group { id } is empty in catalog.", Logger.error);

                        continue;   // To the next required mod
                    }

                    // Get the mod group from the catalog
                    ModGroup group = ActiveCatalog.Instance.ModGroupDictionary[id];

                    // Some vars to keep track of all mods in the group, and check if at least one group member is subscribed and enabled
                    uint subscriptionsFound = 0;
                    bool EnabledSubscriptionFound = false;
                    string disabledModsText = "";
                    string missingModsText = "";

                    // Check each mod in the group, and see if they are subscribed and enabled
                    foreach (ulong modID in group.SteamIDs)
                    {
                        if (Subscription.AllSubscriptions.ContainsKey(modID))
                        {
                            // Mod is subscribed
                            subscriptionsFound++;

                            if (Subscription.AllSubscriptions[modID].IsEnabled)
                            {
                                // Enabled mod found, no need to look any further in this group
                                EnabledSubscriptionFound = true;

                                break;   // out of the group member foreach
                            }
                            else
                            {
                                // Disabled mod
                                disabledModsText += ReviewLine(Subscription.AllSubscriptions[modID].ToString(showFakeID: false, showDisabled: true), 
                                    htmlReport, ModSettings.bullet3);
                            }
                        }
                        else
                        {
                            // Mod is not subscribed, find it in the catalog
                            if (ActiveCatalog.Instance.ModDictionary.ContainsKey(modID))
                            {
                                // Mod found in the catalog
                                missingModsText += ReviewLine(ActiveCatalog.Instance.ModDictionary[modID].ToString(showFakeID: false), htmlReport, ModSettings.bullet3);
                            }
                            else
                            {
                                // Mod not found in the catalog
                                missingModsText += ReviewLine($"[Steam ID { modID,10 }] { subscription.Name }", htmlReport, ModSettings.bullet3);

                                Logger.Log($"Mod { modID } from mod group { id } not found in catalog.", Logger.warning);
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
                        text += ReviewLine("one of the following mods:", htmlReport, ModSettings.bullet2);
                        text += missingModsText;
                    }
                    else if (subscriptionsFound == 1)
                    {
                        // One mod is subscribed but disabled; use the 'disabledText', first stripped from bullet3 and the line end
                        int indent = disabledModsText.IndexOf('[');

                        text += ReviewLine(disabledModsText.Substring(indent).Replace('\n', ' '), htmlReport, ModSettings.bullet2);
                    }
                    else
                    {
                        // More than one mod subscribed, but not enabled
                        text += ReviewLine("one of the following mods should be enabled:", htmlReport, ModSettings.bullet2);
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


        // Mod statuses; not reported: UnconfirmedIssues, SourceBundled, SourceObfuscated
        private static string Statuses(Subscription subscription,
                                       bool htmlReport = false)
        {
            if (subscription.Statuses?.Any() != true)
            {
                return "";
            }

            string text = "";

            // Obsolete
            if (subscription.Statuses.Contains(Enums.ModStatus.NoLongerNeeded))
            {
                text += ReviewLine("Unsubscribe. This is no longer needed.", htmlReport);
            }

            // Gamebreaking issues
            if (subscription.Statuses.Contains(Enums.ModStatus.IncompatibleAccordingToWorkshop))
            {
                text += ReviewLine("Unsubscribe! This is totally incompatible with current game version.", htmlReport);
            }
            else if (subscription.Statuses.Contains(Enums.ModStatus.GameBreaking))
            {
                text += ReviewLine("Unsubscribe! This breaks the game.", htmlReport);
            }
            else
            {
                // Issues, but not gamebreaking
                if (subscription.Statuses.Contains(Enums.ModStatus.MajorIssues))
                {
                    text += ReviewLine("Unsubscribe would be wise. This has major issues.", htmlReport);
                }
                else if (subscription.Statuses.Contains(Enums.ModStatus.MinorIssues))
                {
                    text += ReviewLine("This has minor issues. Check its Workshop page for details.", htmlReport);
                }

                // Several statuses only listed if there are no gamebreaking issues

                // Abandoned
                if (subscription.Statuses.Contains(Enums.ModStatus.Abandoned))
                {
                    text += ReviewLine("This seems to be abandoned and probably won't be updated anymore.", htmlReport);
                }

                // Editors
                if (subscription.Statuses.Contains(Enums.ModStatus.BreaksEditors))
                {
                    text += ReviewLine("This gives major issues in the asset editor and/or map editor.", htmlReport);
                }

                // Performance
                if (subscription.Statuses.Contains(Enums.ModStatus.PerformanceImpact))
                {
                    text += ReviewLine("This might negatively impact game performance.", htmlReport);
                }

                // Loading time
                if (subscription.Statuses.Contains(Enums.ModStatus.LoadingTimeImpact))
                {
                    text += ReviewLine("This might increase loading time.", htmlReport);
                }
            }

            // Several statuses listed even with gamebreaking issues

            // Removed from the Steam Workshop
            if (subscription.Statuses.Contains(Enums.ModStatus.RemovedFromWorkshop))
            {
                text += ReviewLine("Unsubscribe is wise. This is no longer (publicly) available on the Workshop.", htmlReport);

                // Archive workshop page
                text += string.IsNullOrEmpty(subscription.ArchiveURL) ? "" :
                    ReviewLine("Old Workshop page:\n" + subscription.ArchiveURL, htmlReport, ModSettings.noBullet, cutOff: false);
            }

            // Savegame affecting
            if (subscription.Statuses.Contains(Enums.ModStatus.SavesCantLoadWithout))
            {
                text += ReviewLine("Caution. After using this mod, savegames won't easily load without it anymore.", htmlReport);
            }

            // Source code
            if (subscription.Statuses.Contains(Enums.ModStatus.SourceUnavailable))
            {
                text += ReviewLine("No public source code found, making it hard to continue if this gets abandoned.", htmlReport);
            }
            else if (subscription.Statuses.Contains(Enums.ModStatus.SourceNotUpdated))
            {
                text += ReviewLine("Published source seems out of date, making it hard to continue if this gets abandoned.", htmlReport);
            }

            // Music
            if (subscription.Statuses.Contains(Enums.ModStatus.CopyrightFreeMusic))
            {
                text += ReviewLine("This includes copyrighted music and should not be used for streaming.", htmlReport);
            }
            else if (subscription.Statuses.Contains(Enums.ModStatus.CopyrightedMusic))
            {
                text += ReviewLine("The included music is said to be copyright-free and safe for streaming.", htmlReport);
            }
            else if (subscription.Statuses.Contains(Enums.ModStatus.CopyrightUnknownMusic))
            {
                text += ReviewLine("This includes music with unknown copyright status. Safer not to use it for streaming.", htmlReport);
            }

            return text;
        }


        // Compatibilities with other mods; result could be multiple mods and also multiple statuses for each mod
        // Not reported: NewerVersionOfTheSameMod, FunctionalityCoveredByThisMod, RequiresSpecificConfigForOtherMod, CompatibleAccordingToAuthor
        private static string Compatibilities(Subscription subscription,
                                              bool htmlReport = false)
        {
            if (subscription.Compatibilities?.Any() != true)
            {
                return "";
            }

            string text = "";

            foreach (KeyValuePair<ulong, List<Enums.CompatibilityStatus>> compatibility in subscription.Compatibilities)
            {
                // Skip if not subscribed
                if (!Subscription.AllSubscriptions.ContainsKey(compatibility.Key))
                {
                    continue;   // To the next compatibility
                }

                // Get the list of compatibility statuses
                List<Enums.CompatibilityStatus> statuses = compatibility.Value;

                // Get a formatted text with the name of the other mod and the corresponding compatibility note
                string otherModText = 
                    ReviewLine(Subscription.AllSubscriptions[compatibility.Key].ToString(showFakeID: false, showDisabled: true), htmlReport, ModSettings.bullet2) + 
                    ReviewLine(subscription.ModNotes[compatibility.Key], htmlReport, ModSettings.bullet3);
                
                // Different versions, releases or mod with the same functionality
                if (statuses.Contains(Enums.CompatibilityStatus.OlderVersionOfTheSame))
                {
                    text += ReviewLine("Unsubscribe. You're already subscribed to a newer version:", htmlReport) + otherModText;
                }
                else if (statuses.Contains(Enums.CompatibilityStatus.SameModDifferentReleaseType))
                {
                    text += ReviewLine("Unsubscribe either this one or the other release of the same mod:", htmlReport) + otherModText;
                }
                else if (statuses.Contains(Enums.CompatibilityStatus.FunctionalityCoveredByOther))
                {
                    text += ReviewLine("Unsubscribe. It's functionality is already covered by:", htmlReport) + otherModText;
                }

                // Incompatible or minor issues
                if (statuses.Contains(Enums.CompatibilityStatus.IncompatibleAccordingToAuthor))
                {
                    text += ReviewLine("This is incompatible with (unsubscribe either one):", htmlReport) + otherModText;
                }
                else if (statuses.Contains(Enums.CompatibilityStatus.IncompatibleAccordingToUsers))
                {
                    text += ReviewLine("Said to be incompatible with (best to unsubscribe one):", htmlReport) + otherModText;
                }
                else if (statuses.Contains(Enums.CompatibilityStatus.MinorIssues))
                {
                    text += ReviewLine("This has reported issues with:", htmlReport) + otherModText;
                }

                // Specific config
                if (statuses.Contains(Enums.CompatibilityStatus.RequiresSpecificConfigForThis))
                {
                    text += ReviewLine("This requires specific configuration to work together with:", htmlReport) + otherModText;
                }
            }

            return text;
        }        
    }
}
