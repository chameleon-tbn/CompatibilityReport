using System;
using System.Collections.Generic;
using System.Linq;
using ColossalFramework.PlatformServices;
using ModChecker.DataTypes;
using static ModChecker.DataTypes.Subscription;
using ModChecker.Util;
using static ModChecker.Util.ModSettings;


namespace ModChecker
{
    internal static class Reporter
    {
        private static DateTime createTime;


        // Create the report(s)
        internal static void Create()
        {
            // Initialize the report logger
            Logger.InitReport();

            createTime = DateTime.Now;

            // Create the HTML report if selected in settings               // Unfinished: change into header, body (per mod data) and footer part, and combine text en html
            if (HtmlReport)
            {
                if (CreateHtml())
                {
                    Logger.Log($"Scan complete. HTML report ready at \"{ Tools.PrivacyPath(ReportHtmlFullPath) }\".", gameLog: true);
                }
                else
                {
                    Logger.Log("Could not create the HTML report.", Logger.error, gameLog: true);
                }
            }

            // Create the text report if selected in settings, or if somehow no report was selected in options
            if (TextReport || !HtmlReport)
            {
                if (CreateText())
                {
                    Logger.Log($"{ (HtmlReport ? "" : "Scan complete. ") }Text report ready at \"{ Tools.PrivacyPath(ReportTextFullPath) }\".",
                        gameLog: true);
                }
                else
                {
                    Logger.Log("Could not create the text report.", Logger.error, gameLog: true);
                }
            }
        }


        // Create HTML report
        private static bool CreateHtml()
        {
            bool completed = false;

            // Unfinished: Work to be done here

            return completed;
        }


        // Create text report
        private static bool CreateText()
        {
            bool completed = false;            

            // Mod name and report date
            Logger.Report($"{ ModSettings.name } report, created on { createTime:D}, { createTime:t}.\n");

            // Mod version, catalog version and number of mods in the catalog and in game
            Logger.Report($"Version { ModSettings.shortVersion } with catalog version { Catalog.Active.VersionString() }. " + 
                $"The catalog contains { Catalog.Active.CountReviewed } reviewed mods\n" +
                $"and { Catalog.Active.Count - Catalog.Active.CountReviewed } mods with basic information. " + 
                $"Your game has { AllSubscriptions.Count } mods, of which { TotalSubscriptionsReviewed } were reviewed.");

            // Generic note from the catalog
            Logger.Report(string.IsNullOrEmpty(Catalog.Active.Note) ? "" : "\n" + Catalog.Active.Note);

            // Special note about special game versions; will be empty for most
            Logger.Report(string.IsNullOrEmpty(GameVersion.SpecialNote) ? "" : "\n" + GameVersion.SpecialNote);

            // Warn about game version mismatch
            if (GameVersion.Current != Catalog.Active.CompatibleGameVersion)
            {
                string olderNewer = (GameVersion.Current < Catalog.Active.CompatibleGameVersion) ? "older" : "newer";

                Logger.Report($"\nWARNING: The catalog is made for game version { GameVersion.Formatted(Catalog.Active.CompatibleGameVersion) }. Your game is { olderNewer }.\n" + 
                                 "         Results may not be accurate.");
            }

            // Intro text with generic notes
            Logger.Report("\n" + separatorDouble + "\n");

            Logger.Report(string.IsNullOrEmpty(Catalog.Active.ReportIntroText) ? DefaultIntroText : Catalog.Active.ReportIntroText);

            Logger.Report("\n" + separatorDouble);

            // Details per mod, starting with the reviewed mods
            Logger.Report($"REVIEWED MODS ({ TotalSubscriptionsReviewed }):");

            string nonReviewedModsText = "";

            try
            {
                if (ReportSortByName)
                {
                    // Report the info of each reviewed mod, sorted by name; gather the info of all non-reviewed mods, also sorted
                    foreach (string name in Subscription.AllNames)
                    {
                        // Get the Steam ID
                        ulong steamID = Subscription.NameToSteamID(name);

                        if (steamID != 0)
                        {
                            Logger.Report(ModText(steamID, nameFirst: true, ref nonReviewedModsText));
                        }
                    }
                }
                else
                {
                    // Report the info of each mod, sorted by Steam ID; gather the info of all non-reviewed mods, also sorted
                    foreach (ulong steamID in Subscription.AllSteamIDs)
                    {
                        Logger.Report(ModText(steamID, nameFirst: false, ref nonReviewedModsText));
                    }
                }

                completed = true;
            }
            catch (Exception ex)
            {
                Logger.Log("Can't report (all) the found mods.", Logger.error, gameLog: true);

                Logger.Exception(ex);
            }

            Logger.Report(separatorDouble);

            // Log the non-reviewed mods
            if (nonReviewedModsText != "")
            {
                Logger.Report($"MODS WITHOUT REVIEW ({ AllSubscriptions.Count - TotalSubscriptionsReviewed }):");

                Logger.Report(nonReviewedModsText + separatorDouble);
            }

            // Footer text
            Logger.Report(string.IsNullOrEmpty(Catalog.Active.ReportFooterText) ? "\n" + DefaultFooterText : "\n" + Catalog.Active.ReportFooterText);

            return completed;
        }


        // Return report text for one mod
        private static string ModText(ulong steamID, bool nameFirst, ref string nonReviewedModsText)
        {
            Subscription subscription = AllSubscriptions[steamID];

            // Create a header for this mod information; start with a separator
            string modHeader = separator + "\n\n";

            // Mod name and Steam ID
            string modName = subscription.ToString(nameFirst, showFakeID: false);

            modHeader += modName;

            // Authorname (if known), on a new line if it doesn't fit anymore
            if (!string.IsNullOrEmpty(subscription.AuthorName))
            {
                if (modName.Length + 4 + subscription.AuthorName.Length <= MaxReportWidth)
                {
                    // On the same line behind name and Steam ID
                    modHeader += " by " + subscription.AuthorName;
                }
                else
                {
                    // Doesn't fit on the same line, so right align on a new line
                    modHeader += "\n" + $"by { subscription.AuthorName }".PadLeft(MaxReportWidth);
                }
            }

            modHeader += "\n";

            // Create the review text for this mod
            string modReview = "";

            // Say hi when we find ourselves
            if (steamID == ModSettings.SteamID)
            {
                modReview += ReviewText("This mod.");
            }

            // Camera Script
            if (subscription.IsCameraScript)
            {
                modReview += ReviewText("This is a camera script.");
            }
            
            // Builtin and local mods
            if (subscription.IsBuiltin)
            {
                // Builtin mod
                if (BuiltinMods.ContainsValue(steamID))
                {
                    // Recognized builtin mod that is enabled; just continue
                }
                else
                {
                    // Unknown builtin mod; can't review and nothing useful to say, so exit
                    modReview += ReviewText("Not reviewed yet.");

                    nonReviewedModsText += modHeader + modReview + "\n";

                    return "";
                }
            }
            else if (subscription.IsLocal)
            {
                // Local mod; can't review and nothing useful to say, so exit
                if (!subscription.IsEnabled)
                {
                    modReview += ReviewText("Mod is disabled. If not used, it should be removed. Disabled mods are still");
                    modReview += ReviewText("partially loaded at game startup and can still cause issues.", NoBullet);
                }

                modReview += ReviewText("Can't review local mods (yet).");

                nonReviewedModsText += modHeader + modReview + "\n";

                return "";                
            }

            // From here on we only have Steam Workshop mods and enabled known builtin mods

            // Disabled Steam Workshop mod
            if (!subscription.IsEnabled)
            {
                modReview += ReviewText("Mod is disabled. Unsubscribe it if not used. Disabled mods can still cause issues.");
            }

            // Mod is removed from the Steam Workshop
            if (subscription.IsRemoved)
            {
                modReview += ReviewText("Unsubscribe is wise. This is no longer (publicly) available on the Workshop.");

                // Archive workshop page
                modReview += string.IsNullOrEmpty(subscription.ArchiveURL) ? "" : 
                    ReviewText("Old Workshop page:\n" + subscription.ArchiveURL, NoBullet, cutOff: false);
            }
            
            // Author is retired
            if (subscription.AuthorIsRetired)
            {
                modReview += ReviewText("The author seems to be retired. Updates are unlikely.");
            }

            // Mod is not reviewed
            if (!subscription.IsReviewed)
            {
                modReview += ReviewText("Not reviewed yet.");
            }

            // Required DLC
            if (subscription.RequiredDLC?.Any() == true)
            {
                string dlcs = "";

                // Check every required DLC against installed DLC
                foreach (Enums.DLC dlc in subscription.RequiredDLC)
                {
                    if (!PlatformService.IsDlcInstalled((uint)dlc))
                    {
                        dlcs += (dlcs == "" ? "" : ", ") + $"{ dlc }";
                    }
                }

                modReview += ReviewText($"Unsubscribe. This requires DLC you don't have: { dlcs }.");
            }

            // Required mods and mod groups
            if (subscription.RequiredMods?.Any() == true)
            {
                modReview += ReviewText("This mod requires other mods you don't have, or which are not enabled:");

                foreach (ulong id in subscription.RequiredMods)
                {
                    // Check if it's a regular mod or a mod group
                    if ((id < lowestModGroupID) || (id > highestModGroupID))
                    {
                        // Regular mod. Try to find it in the list of subscribed mods
                        if (AllSubscriptions.ContainsKey(id))
                        {
                            // Mod is subscribed
                            if (!AllSubscriptions[id].IsEnabled)
                            {
                                // Mod is subscribed, but not enabled
                                modReview += ReviewText(AllSubscriptions[id].ToString(showFakeID: false, showDisabled: true), Bullet2);
                            }

                            continue;   // To the next required mod
                        }
                        else
                        {
                            // Mod is not subscribed, try to find it in the catalog
                            if (Catalog.Active.ModDictionary.ContainsKey(id))
                            {
                                // Mod found in the catalog, list Steam ID and name
                                modReview += ReviewText(Catalog.Active.ModDictionary[id].ToString(showFakeID: false), Bullet2);
                            }
                            else
                            {
                                // Mod not found in the catalog
                                modReview += ReviewText($"[Steam ID { id, 10 }] <name unknown>", Bullet2);

                                Logger.Log($"Required mod { id } not found in catalog.", Logger.debug);
                            }

                            // List the workshop page for easy subscribing
                            modReview += ReviewText("Workshop page: " + Tools.GetWorkshopURL(id), NoBullet2);
                        }

                        continue;   // To the next required mod
                    }
                    else
                    {
                        // Mod group. We have to dig a little deeper. First some error check
                        if (!Catalog.Active.ModGroupDictionary.ContainsKey(id))
                        {
                            // Group not found in catalog, this should not happen
                            Logger.Log($"Group { id } not found in catalog.", Logger.error);

                            modReview += ReviewText("one of the following mods: <missing information in catalog>", Bullet2);

                            continue;   // To the next required mod
                        }

                        // Get the mod group from the catalog
                        ModGroup group = Catalog.Active.ModGroupDictionary[id];

                        // Check if group contains any Steam IDs
                        if (group.SteamIDs?.Any() != true)
                        {
                            // Group is empty, this should not happen
                            Logger.Log($"Group { id } is empty.", Logger.error);

                            modReview += ReviewText("one of the following mods: <missing information in catalog>", Bullet2);

                            continue;   // To the next required mod
                        }

                        // Group found in catalog; quick check if one of it's members is subscribed and enabled
                        uint modFound = 0;
                        bool modEnabled = false;

                        // Temporary text to keep track of all mods in the group
                        string disabledText = "";
                        string missingText = "";

                        // Check each mods and see if they are subscribed and enabled
                        foreach (ulong modID in group.SteamIDs)
                        {
                            if (AllSubscriptions.ContainsKey(modID))
                            {
                                // Mod is subscribed
                                modFound++;

                                if (AllSubscriptions[modID].IsEnabled)
                                {
                                    // Enabled mod found, no need to look any further
                                    modEnabled = true;

                                    continue;   // To the next mod in the group
                                }
                                else
                                {
                                    // Disabled mod
                                    disabledText += ReviewText(AllSubscriptions[modID].ToString(showFakeID: false, showDisabled: true), Bullet3);
                                }
                            }
                            else
                            {
                                // Mod is not subscribed, find it in the catalog
                                if (Catalog.Active.ModDictionary.ContainsKey(modID))
                                {
                                    missingText += ReviewText(Catalog.Active.ModDictionary[modID].ToString(showFakeID: false), Bullet3);
                                }
                                else
                                {
                                    missingText += ReviewText($"[Steam ID { modID, 10 }] <name unknown>", Bullet3);

                                    Logger.Log($"Mod { modID } from mod group { id } not found in catalog.", Logger.debug);
                                }
                            }
                        }

                        // If a group member is subscribed and enabled, then there is nothing to report
                        if (modEnabled) 
                        { 
                            continue;   // To the next required mod
                        }

                        // We can finally list the group members
                        if (modFound == 0)
                        {
                            // None of the group members is subscribed
                            modReview += ReviewText("one of the following mods:", Bullet2);

                            modReview += missingText;
                        }
                        else if (modFound == 1)
                        {
                            // One mod is subscribed, but not enabled; use the 'disabledText' without the indent characters
                            int indent = disabledText.IndexOf('[');

                            modReview += ReviewText(disabledText.Substring(indent), Bullet2);
                        }
                        else
                        {
                            // More than one mod subscribed, but not enabled
                            modReview += ReviewText("one of the following mods should be enabled:", Bullet2);

                            modReview += disabledText;
                        }
                    }
                }
            }

            // Unneeded dependency mod
            if (subscription.NeededFor?.Any() == true)
            {
                bool modFound = false;

                // Check if any of the mods that need this is actually subscribed; we don't check for enabled this time
                foreach (ulong id in subscription.NeededFor)
                {
                    if (AllSubscriptions.ContainsKey(id))
                    {
                        // Found a mod that needs this, no need to look any further
                        modFound = true;

                        break;  // out of the foreach
                    }
                }

                if (!modFound)
                {
                    modReview += ReviewText("You can probably unsubscribe. This is only needed for mods you don't seem to have.");
                }
            }

            // Successor(s)
            if (subscription.SucceededBy?.Any() == true)
            {
                if (subscription.SucceededBy.Count == 1)
                {
                    modReview += ReviewText("This is succeeded by:");
                }
                else
                {
                    modReview += ReviewText("This is succeeded by any of the following:");
                }

                foreach (ulong id in subscription.SucceededBy)
                {
                    if (Catalog.Active.ModDictionary.ContainsKey(id))
                    {
                        // Mod found in the catalog, list Steam ID and name
                        modReview += ReviewText(Catalog.Active.ModDictionary[id].ToString(showFakeID: false), Bullet2);
                    }
                    else
                    {
                        // Mod not found in the catalog
                        modReview += ReviewText($"[Steam ID { id, 10 }] <name unknown>", Bullet2);

                        Logger.Log($"Successor mod { id } not found in catalog.", Logger.debug);
                    }
                }
            }

            // Alternative mods
            if (subscription.Alternatives?.Any() == true)
            {
                if (subscription.Alternatives.Count == 1)
                {
                    modReview += ReviewText("An alternative you could use:");
                }
                else
                {
                    modReview += ReviewText("Some alternatives for this are (pick one, not all):");
                }

                foreach (ulong id in subscription.Alternatives)
                {
                    if (Catalog.Active.ModDictionary.ContainsKey(id))
                    {
                        // Mod found in the catalog, list Steam ID and name
                        modReview += ReviewText(Catalog.Active.ModDictionary[id].ToString(showFakeID: false), Bullet2);
                    }
                    else
                    {
                        // Mod not found in the catalog
                        modReview += ReviewText($"[Steam ID { id, 10 }] <name unknown>", Bullet2);

                        Logger.Log($"Alternative mod { id } not found in catalog.", Logger.debug);
                    }
                }
            }

            // Game version compatible, only listed for current version
            if (subscription.GameVersionCompatible == GameVersion.Current)
            {
                modReview += ReviewText($"This mod was created or updated for the current game version, { GameVersion.Formatted(GameVersion.Current) }.");
            }

            // Mod statuses
            if (subscription.Statuses?.Any() == true)
            {
                // Obsolete
                if (subscription.Statuses.Contains(Enums.ModStatus.NoLongerNeeded))
                {
                    modReview += ReviewText("Unsubscribe. This is no longer needed.");
                }

                // Gamebreaking issues
                if (subscription.Statuses.Contains(Enums.ModStatus.IncompatibleAccordingToWorkshop))
                {
                    modReview += ReviewText("Unsubscribe! This is totally incompatible with current game version.");
                }
                else if (subscription.Statuses.Contains(Enums.ModStatus.GameBreaking))
                {
                    modReview += ReviewText("Unsubscribe! This breaks the game.");
                }
                else
                {
                    // Issues, but not gamebreaking
                    if (subscription.Statuses.Contains(Enums.ModStatus.MajorIssues))
                    {
                        modReview += ReviewText("Unsubscribe would be wise. This has major issues.");
                    }
                    else if (subscription.Statuses.Contains(Enums.ModStatus.MinorIssues))
                    {
                        modReview += ReviewText("This has minor issues. Check its Workshop page for details.");
                    }

                    // Several statuses only listed if there are no gamebreaking issues

                    // Abandoned
                    if (subscription.Statuses.Contains(Enums.ModStatus.Abandoned))
                    {
                        modReview += ReviewText("This seems to be abandoned and probably won't be updated anymore.");
                    }

                    // Editors
                    if (subscription.Statuses.Contains(Enums.ModStatus.BreaksEditors))
                    {
                        modReview += ReviewText("This gives major issues in the asset editor and/or map editor.");
                    }

                    // Performance
                    if (subscription.Statuses.Contains(Enums.ModStatus.PerformanceImpact))
                    {
                        modReview += ReviewText("This might negatively impact game performance.");
                    }

                    // Loading time
                    if (subscription.Statuses.Contains(Enums.ModStatus.LoadingTimeImpact))
                    {
                        modReview += ReviewText("This might increase loading time.");
                    }
                }

                // Several statuses listed even with gamebreaking issues

                // Savegame affecting
                if (subscription.Statuses.Contains(Enums.ModStatus.SavesCantLoadWithout))
                {
                    modReview += ReviewText("Caution. After using this mod, savegames won't easily load without it anymore.");
                }
                
                // Source code
                if (subscription.Statuses.Contains(Enums.ModStatus.SourceUnavailable))
                {
                    modReview += ReviewText("No public source code found, making it hard to continue if this gets abandoned.");
                }
                else if (subscription.Statuses.Contains(Enums.ModStatus.SourceNotUpdated))
                {
                    modReview += ReviewText("Published source seems out of date, making it hard to continue if this gets abandoned.");
                }

                // Music
                if (subscription.Statuses.Contains(Enums.ModStatus.CopyrightFreeMusic))
                {
                    modReview += ReviewText("This includes copyrighted music and should not be used for streaming.");
                }

                if (subscription.Statuses.Contains(Enums.ModStatus.CopyrightedMusic))
                {
                    modReview += ReviewText("The included music is said to be copyright-free and safe for streaming.");
                }
            }

            // Compatibilities with other mods
            if (subscription.Compatibilities?.Any() == true)
            {
                foreach (KeyValuePair<ulong, List<Enums.CompatibilityStatus>> compatibility in subscription.Compatibilities)
                {
                    // Skip if not subscribed
                    if (!AllSubscriptions.ContainsKey(compatibility.Key))
                    {
                        continue;   // To the next compatibility
                    }

                    string otherModText = ReviewText(AllSubscriptions[compatibility.Key].ToString(showFakeID: false, showDisabled: true), Bullet2);
                    string compatibilityNote = ReviewText(subscription.ModNotes[compatibility.Key], Bullet3);

                    List<Enums.CompatibilityStatus> statuses = compatibility.Value;

                    // Different versions, releases or mod with the same functionality
                    if (statuses.Contains(Enums.CompatibilityStatus.OlderVersionOfTheSameMod))              // NewerVersionOfTheSameMod skipped
                    {
                        modReview += ReviewText("Unsubscribe. You're already subscribed to a newer version:");
                        modReview += otherModText + compatibilityNote;
                    }
                    else if (statuses.Contains(Enums.CompatibilityStatus.SameModDifferentReleaseType))
                    {
                        modReview += ReviewText("Unsubscribe either this one or the other release of the same mod you have:");
                        modReview += otherModText + compatibilityNote;
                    }
                    else if (statuses.Contains(Enums.CompatibilityStatus.FunctionalityCoveredByOtherMod))    // FunctionalityCoveredByThisMod skipped
                    {
                        modReview += ReviewText("Unsubscribe. It's functionality is already covered by:");
                        modReview += otherModText + compatibilityNote;
                    }

                    // Incompatible or minor issues
                    if (statuses.Contains(Enums.CompatibilityStatus.IncompatibleAccordingToAuthor))
                    {
                        modReview += ReviewText("This is incompatible with (unsubscribe either one):");
                        modReview += otherModText + compatibilityNote;
                    }
                    else if (statuses.Contains(Enums.CompatibilityStatus.IncompatibleAccordingToUsers))
                    {
                        modReview += ReviewText("Said to be incompatible with (best to unsubscribe one):");
                        modReview += otherModText + compatibilityNote;
                    } else if (statuses.Contains(Enums.CompatibilityStatus.MinorIssues))
                    {
                        modReview += ReviewText("This has reported issues with:");
                        modReview += otherModText + compatibilityNote;
                    }

                    // Specific config
                    if (statuses.Contains(Enums.CompatibilityStatus.RequiresSpecificConfigForThisMod))      // RequiresSpecificConfigForOtherMod skipped
                    {
                        modReview += ReviewText("This requires specific configuration to work together with:");
                        modReview += otherModText + compatibilityNote;
                    }
                }
            }

            // General note for this mod
            modReview += ReviewText(subscription.Note);

            // Make sure at least something is displayed for every mod
            if (string.IsNullOrEmpty(modReview))
            {
                modReview = ReviewText("Nothing to report");
            }

            // Workshop url for Workshop mods
            modReview += (steamID > HighestFakeID) ? ReviewText("Steam Workshop page: " + Tools.GetWorkshopURL(steamID)) : "";

            // Unreported: regular properties:      SourceURL, Updated, Downloaded, Recommendations
            //             mod statuses:            SourceBundled, SourceObfuscated, UnconfirmedIssues
            //             compatibility statuses:  CompatibleAccordingToAuthor

            // Return the text for this subscription if it was reviewed, else add it to the non reviewed mods text
            if (subscription.IsReviewed)
            {                
                return modHeader + modReview;
            }
            else
            {
                // Needs an extra newline because it is concatenated with other reviews before being reported all at once
                nonReviewedModsText += modHeader + modReview + "\n";

                return "";
            }
        }


        // Format the review text
        private static string ReviewText(string message, string bullet = "", bool cutOff = true)
        {
            if (string.IsNullOrEmpty(message))
            {
                return "";
            }
            else
            {
                bullet = (bullet == "") ? Bullet : bullet;

                if (cutOff && ((bullet + message).Length > MaxReportWidth))
                {
                    // Cut off the message, so the 'bulleted' message stays within maximum width
                    message = message.Substring(0, MaxReportWidth - bullet.Length - 3) + "...";

                    Logger.Log($"Report line too long: " + message, Logger.debug);
                }

                // Return 'bulleted' message
                return bullet + message + "\n";
            }                
        }
    }
}
