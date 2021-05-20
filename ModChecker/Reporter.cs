using System;
using System.Collections.Generic;
using System.Linq;
using ColossalFramework.PlatformServices;
using ModChecker.DataTypes;
using ModChecker.Util;


namespace ModChecker
{
    internal static class Reporter
    {
        // Create the report(s)
        internal static void Create()
        {
            // Create the html report if selected in settings               // Unfinished: change into header, body (per mod data) and footer part, and combine text en html
            if (ModSettings.HtmlReport)
            {
                if (CreateHtml())
                {
                    Logger.Log($"Scan complete. HTML report ready at \"{ Tools.PrivacyPath(ModSettings.ReportHtmlFullPath) }\".", gameLog: true);
                }
                else
                {
                    Logger.Log("Could not create the HTML report.", Logger.error, gameLog: true);
                }
            }

            // Create the text report if selected in settings, or if somehow no report was selected in options
            if (ModSettings.TextReport || !ModSettings.HtmlReport)
            {
                if (CreateText())
                {
                    Logger.Log($"{ (ModSettings.HtmlReport ? "" : "Scan complete. ") }Text report ready at \"{ Tools.PrivacyPath(ModSettings.ReportTextFullPath) }\".",
                        gameLog: true);
                }
                else
                {
                    Logger.Log("Could not create the text report.", Logger.error, gameLog: true);
                }
            }
        }


        // Create html report
        private static bool CreateHtml()
        {
            bool completed = false;

            DateTime createTime = DateTime.Now;

            // Unfinished: Work to be done here

            return completed;
        }


        // Create text report
        private static bool CreateText()
        {
            bool completed = false;

            DateTime createTime = DateTime.Now;

            // Mod name and report date
            Logger.Report($"{ ModSettings.name } report, created on { createTime:D}, { createTime:t}.\n");

            // Mod version, catalog version and number of mods in the catalog and in game
            Logger.Report($"Version { ModSettings.version } with catalog version { Catalog.Active.StructureVersion }.{ Catalog.Active.Version:D4}. " + 
                $"The catalog contains { Catalog.Active.CountReviewed } reviewed mods\n" +
                $"and { Catalog.Active.Count - Catalog.Active.CountReviewed } mods with basic information. " + 
                $"Your game has { Subscription.AllSubscriptions.Count } mods, of which { Subscription.TotalReviewed } were reviewed.");

            // Special note about special game versions; will be empty for most
            if (!string.IsNullOrEmpty(GameVersion.SpecialNote))
            {
                Logger.Report("\n" + GameVersion.SpecialNote);
            }            

            // Warn about game version mismatch
            if (GameVersion.Current != ModSettings.CompatibleGameVersion)
            {
                string olderNewer = (GameVersion.Current < ModSettings.CompatibleGameVersion) ? "an older" : "a newer";

                Logger.Report($"\nThis mod is made for game version { GameVersion.Formatted(ModSettings.CompatibleGameVersion) }. " +
                    $"You're using { olderNewer } version of the game. Results might not be accurate.", Logger.warning);

                if (olderNewer == "a newer")
                {
                    Logger.Report($"Check this mods Steam workshop page for details or an updated version: { Tools.GetWorkshopURL(ModSettings.SteamID) }");
                }

                
            }

            // Intro text with generic notes
            Logger.Report("\n" + ModSettings.separatorDouble + "\n");

            Logger.Report(string.IsNullOrEmpty(Catalog.Active.ReportIntroText) ? ModSettings.DefaultIntroText : Catalog.Active.ReportIntroText);

            Logger.Report("\n" + ModSettings.separatorDouble);

            // Details per mod, starting with the reviewed mods
            Logger.Report($"REVIEWED MODS ({ Subscription.TotalReviewed }):");

            string nonReviewedModsText = "";

            try
            {
                if (ModSettings.ReportSortByName)
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

            Logger.Report(ModSettings.separatorDouble);

            // Log the non-reviewed mods
            if (nonReviewedModsText != "")
            {
                Logger.Report($"MODS WITHOUT REVIEW ({ Subscription.AllSubscriptions.Count - Subscription.TotalReviewed }):");

                Logger.Report(nonReviewedModsText + ModSettings.separatorDouble);
            }

            // Footer text
            Logger.Report(string.IsNullOrEmpty(Catalog.Active.ReportFooterText) ? "\n" + ModSettings.DefaultFooterText : "\n" + Catalog.Active.ReportFooterText);

            return completed;
        }


        // Return report text for one mod
        private static string ModText(ulong steamID, bool nameFirst, ref string nonReviewedModsText)
        {
            Subscription subscription = Subscription.AllSubscriptions[steamID];

            // Start with a separator
            string modText = ModSettings.separator + "\n\n";

            // Mod name and Steam ID
            string modName = subscription.ToString(nameFirst, showFakeID: false);
            
            modText += modName;

            // Authorname (if known), on the same line if it fits
            if (!string.IsNullOrEmpty(subscription.AuthorName))
            {
                if (modName.Length + 4 + subscription.AuthorName.Length <= ModSettings.MaxReportWidth)
                {
                    // On the same line behind name and Steam ID
                    modText += " by " + subscription.AuthorName;
                }
                else
                {
                    // Doesn't fit on the same line, so right align on a new line
                    modText += "\n" + $"by { subscription.AuthorName }".PadLeft(ModSettings.MaxReportWidth);
                }
            }

            modText += "\n";

            // Say hi when we find ourselves
            if (steamID == ModSettings.SteamID)
            {
                modText += ReviewText("This mod.");
            }

            // Camera Script
            if (subscription.IsCameraScript)
            {
                modText += ReviewText("This is a camera script.");
            }
            
            // Builtin and local mods
            if (subscription.IsBuiltin)
            {
                // Builtin mod
                if (ModSettings.BuiltinMods.ContainsValue(steamID))
                {
                    // Recognized builtin mod that is enabled; just continue
                }
                else
                {
                    // Unknown builtin mod; can't review and nothing useful to say, so exit
                    modText += ReviewText("Not reviewed yet.");

                    nonReviewedModsText += modText + "\n";

                    return "";
                }
            }
            else if (subscription.IsLocal)
            {
                // Local mod; can't review and nothing useful to say, so exit
                if (!subscription.IsEnabled)
                {
                    modText += ReviewText("Mod is disabled. If not used, it should be removed. Disabled mods are still");
                    modText += ReviewText("partially loaded at game startup and can still cause issues.", ModSettings.NoBullet);
                }

                modText += ReviewText("Can't review local mods (yet).");

                nonReviewedModsText += modText + "\n";

                return "";                
            }

            // From here on we only have Steam Workshop mods and enabled known builtin mods

            // Disabled Steam Workshop mod
            if (!subscription.IsEnabled)
            {
                modText += ReviewText("Mod is disabled. If not used, it should be unsubscribed. Disabled mods are still");
                modText += ReviewText("partially loaded at game startup and can still cause issues.", ModSettings.NoBullet);
            }

            // Mod is removed from the Steam Workshop
            if (subscription.IsRemoved)
            {
                modText += ReviewText("No longer (publicly) available on the Workshop. Probably wise not to use it anymore.");

                // Archive workshop page
                modText += string.IsNullOrEmpty(subscription.ArchiveURL) ? "" : 
                    ReviewText("Old Workshop page:\n" + subscription.ArchiveURL, ModSettings.NoBullet, cutOff: false);
            }
            
            // Author is retired
            if (subscription.AuthorIsRetired)
            {
                modText += ReviewText("The author seems to be retired. This mod will probably not be updated anymore.");
            }

            // Mod is not reviewed
            if (!subscription.IsReviewed)
            {
                modText += ReviewText("Not reviewed yet.");
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

                modText += ReviewText($"Requires DLC you don't have: { dlcs }.");
            }

            // Required mods and mod groups
            if (subscription.RequiredMods?.Any() == true)
            {
                modText += ReviewText("This mod requires other mods you don't have, or which are not enabled:");

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
                                modText += ReviewText(Subscription.AllSubscriptions[id].ToString(showFakeID: false, showDisabled: true), ModSettings.Bullet2);
                            }

                            break;      // To the next required mod
                        }
                        else
                        {
                            // Mod is not subscribed, try to find it in the catalog
                            if (Catalog.Active.ModDictionary.ContainsKey(id))
                            {
                                // Mod found in the catalog, list Steam ID and name
                                modText += ReviewText(Catalog.Active.ModDictionary[id].ToString(showFakeID: false), ModSettings.Bullet2);
                            }
                            else
                            {
                                // Mod not found in the catalog
                                modText += ReviewText($"[Steam ID { id, 10 }] <name unknown>", ModSettings.Bullet2);

                                Logger.Log($"Required mod { id } not found in catalog.", Logger.debug);
                            }

                            // List the workshop page for easy subscribing
                            modText += ReviewText("Workshop page: " + Tools.GetWorkshopURL(id), ModSettings.NoBullet2);
                        }

                        // To the next required mod
                    }
                    else
                    {
                        // Mod group. We have to dig a little deeper. First some error check
                        if (!Catalog.Active.ModGroupDictionary.ContainsKey(id))
                        {
                            // Group not found in catalog, this should not happen
                            Logger.Log($"Group { id } not found in catalog.", Logger.error);

                            modText += ReviewText("one of the following mods: <missing information in catalog>", ModSettings.Bullet2);

                            break;      // To the next required mod
                        }

                        // Get the mod group from the catalog
                        ModGroup group = Catalog.Active.ModGroupDictionary[id];

                        // Check if group contains any Steam IDs
                        if (group.SteamIDs?.Any() != true)
                        {
                            // Group is empty, this should not happen
                            Logger.Log($"Group { id } is empty.", Logger.error);

                            modText += ReviewText("one of the following mods: <missing information in catalog>", ModSettings.Bullet2);

                            break;      // To the next required mod
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
                            if (Subscription.AllSubscriptions.ContainsKey(modID))
                            {
                                // Mod is subscribed
                                modFound++;

                                if (Subscription.AllSubscriptions[modID].IsEnabled)
                                {
                                    // Enabled mod found, no need to look any further
                                    modEnabled = true;

                                    break;
                                }
                                else
                                {
                                    // Disabled mod
                                    disabledText += ReviewText(Subscription.AllSubscriptions[modID].ToString(showFakeID: false, showDisabled: true), ModSettings.Bullet3);
                                }
                            }
                            else
                            {
                                // Mod is not subscribed, find it in the catalog
                                if (Catalog.Active.ModDictionary.ContainsKey(modID))
                                {
                                    missingText += ReviewText(Catalog.Active.ModDictionary[modID].ToString(showFakeID: false), ModSettings.Bullet3);
                                }
                                else
                                {
                                    missingText += ReviewText($"[Steam ID { modID, 10 }] <name unknown>", ModSettings.Bullet3);

                                    Logger.Log($"Mod { modID } from mod group { id } not found in catalog.", Logger.debug);
                                }
                            }
                        }

                        // If a group member is subscribed and enabled, then there is nothing to report
                        if (modEnabled) { break; }          // To the next required mod

                        // We can finally list the group members
                        if (modFound == 0)
                        {
                            // None of the group members is subscribed
                            modText += ReviewText("one of the following mods:", ModSettings.Bullet2);

                            modText += missingText;
                        }
                        else if (modFound == 1)
                        {
                            // One mod is subscribed, but not enabled; use the 'disabledText' without the indent characters
                            int indent = disabledText.IndexOf('[');

                            modText += ReviewText(disabledText.Substring(indent), ModSettings.Bullet2);
                        }
                        else
                        {
                            // More than one mod subscribed, but not enabled
                            modText += ReviewText("one of the following mods should be enabled:", ModSettings.Bullet2);

                            modText += disabledText;
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
                    if (Subscription.AllSubscriptions.ContainsKey(id))
                    {
                        // Found a mod that needs this, no need to look any further
                        break;
                    }
                }

                if (!modFound)
                {
                    modText += ReviewText("This is only needed for mods you don't seem to have and can be unsubscribed.");
                }
            }

            // Successor(s)
            if (subscription.SucceededBy?.Any() == true)
            {
                if (subscription.SucceededBy.Count == 1)
                {
                    modText += ReviewText("This is succeeded by:");
                }
                else
                {
                    modText += ReviewText("This is succeeded by any of the following:");
                }

                foreach (ulong id in subscription.SucceededBy)
                {
                    if (Catalog.Active.ModDictionary.ContainsKey(id))
                    {
                        // Mod found in the catalog, list Steam ID and name
                        modText += ReviewText(Catalog.Active.ModDictionary[id].ToString(showFakeID: false), ModSettings.Bullet2);
                    }
                    else
                    {
                        // Mod not found in the catalog
                        modText += ReviewText($"[Steam ID { id, 10 }] <name unknown>", ModSettings.Bullet2);

                        Logger.Log($"Successor mod { id } not found in catalog.", Logger.debug);
                    }
                }
            }

            // Alternative mods
            if (subscription.Alternatives?.Any() == true)
            {
                if (subscription.Alternatives.Count == 1)
                {
                    modText += ReviewText("An alternative you could use:");
                }
                else
                {
                    modText += ReviewText("Some alternatives for this are:");
                }

                foreach (ulong id in subscription.Alternatives)
                {
                    if (Catalog.Active.ModDictionary.ContainsKey(id))
                    {
                        // Mod found in the catalog, list Steam ID and name
                        modText += ReviewText(Catalog.Active.ModDictionary[id].ToString(showFakeID: false), ModSettings.Bullet2);
                    }
                    else
                    {
                        // Mod not found in the catalog
                        modText += ReviewText($"[Steam ID { id, 10 }] <name unknown>", ModSettings.Bullet2);

                        Logger.Log($"Successor mod { id } not found in catalog.", Logger.debug);
                    }
                }
            }

            // Game version compatible, only listed for current version
            if (subscription.GameVersionCompatible == GameVersion.Current)
            {
                modText += ReviewText($"This mod was created or updated for the current game version, { GameVersion.Formatted(GameVersion.Current) }.");
            }

            // Mod statuses
            if (subscription.Statuses?.Any() == true)
            {
                // Obsolete
                if (subscription.Statuses.Contains(Enums.ModStatus.NoLongerNeeded))
                {
                    modText += ReviewText("This is no longer needed. Unsubscribe.");
                }

                // Gamebreaking issues
                if (subscription.Statuses.Contains(Enums.ModStatus.IncompatibleAccordingToWorkshop))
                {
                    modText += ReviewText("Totally incompatible with current game version. Unsubscribe!");
                }
                else if (subscription.Statuses.Contains(Enums.ModStatus.GameBreaking))
                {
                    modText += ReviewText("Breaks the game and should be unsubscribed!");
                }
                else
                {
                    // Issues, but not gamebreaking
                    if (subscription.Statuses.Contains(Enums.ModStatus.MajorIssues))
                    {
                        modText += ReviewText("This has major issues. Use with caution or unsubscribe.");
                    }
                    else if (subscription.Statuses.Contains(Enums.ModStatus.MinorIssues))
                    {
                        modText += ReviewText("This has minor issues. Check its Workshop page for details.");
                    }

                    // Several statuses only listed if there are no gamebreaking issues

                    // Abandonned
                    if (subscription.Statuses.Contains(Enums.ModStatus.Abandonned))
                    {
                        modText += ReviewText("This seems abandonned and probably won't be updated anymore.");
                    }

                    // Editors
                    if (subscription.Statuses.Contains(Enums.ModStatus.BreaksEditors))
                    {
                        modText += ReviewText("This gives major issues in the asset editor and/or map editor.");
                    }

                    // Performance
                    if (subscription.Statuses.Contains(Enums.ModStatus.PerformanceImpact))
                    {
                        modText += ReviewText("This might negatively impact game performance.");
                    }

                    // Loading time
                    if (subscription.Statuses.Contains(Enums.ModStatus.LoadingTimeImpact))
                    {
                        modText += ReviewText("This might increase loading time.");
                    }
                }

                // Several statuses listed even with gamebreaking issues

                // Savegame affecting
                if (subscription.Statuses.Contains(Enums.ModStatus.SavesCantLoadWithout))
                {
                    modText += ReviewText("After using this mod, savegames won't easily load without it anymore.");
                }
                
                // Source code
                if (subscription.Statuses.Contains(Enums.ModStatus.SourceUnavailable))
                {
                    modText += ReviewText("No public source code found, making it hard to continue if this gets abandonned.");
                }
                else if (subscription.Statuses.Contains(Enums.ModStatus.SourceNotUpdated))
                {
                    modText += ReviewText("Published source seems out of date, making it hard to continue if this gets abandonned.");
                }

                // Music
                if (subscription.Statuses.Contains(Enums.ModStatus.CopyrightFreeMusic))
                {
                    modText += ReviewText("This includes copyrighted music and should not be used for streaming.");
                }

                if (subscription.Statuses.Contains(Enums.ModStatus.CopyrightedMusic))
                {
                    modText += ReviewText("The included music is said to be copyright-free and safe for streaming.");
                }
            }

            // Compatibilities with other mods
            if (subscription.Compatibilities?.Any() == true)
            {
                foreach (KeyValuePair<ulong, List<Enums.CompatibilityStatus>> compatibility in subscription.Compatibilities)
                {
                    // Skip if not subscribed
                    if (!Subscription.AllSubscriptions.ContainsKey(compatibility.Key))
                    {
                        break;
                    }

                    string otherModText = ReviewText(Subscription.AllSubscriptions[compatibility.Key].ToString(showFakeID: false, showDisabled: true), ModSettings.Bullet2);
                    string compatibilityNote = ReviewText(subscription.ModNotes[compatibility.Key], ModSettings.Bullet3);

                    List<Enums.CompatibilityStatus> statuses = compatibility.Value;

                    // Different versions, releases or mod with the same functionality
                    if (statuses.Contains(Enums.CompatibilityStatus.NewerVersionOfTheSameMod))
                    {
                        modText += ReviewText("You still have an older version of the same mod subscribed. Unsubscribe that one:");
                        modText += otherModText + compatibilityNote;
                    }
                    else if (statuses.Contains(Enums.CompatibilityStatus.OlderVersionOfTheSameMod))
                    {
                        modText += ReviewText("Unsubscribe this mod. You're already subscribed to a newer version:");
                        modText += otherModText + compatibilityNote;
                    }
                    else if (statuses.Contains(Enums.CompatibilityStatus.SameModDifferentReleaseType))
                    {
                        modText += ReviewText("You have different releases of the same mod. Unsubscribe either this one or the other:");
                        modText += otherModText + compatibilityNote;
                    }
                    else if (statuses.Contains(Enums.CompatibilityStatus.FunctionalityCoveredByOtherMod))    // FunctionalityCoveredByThisMod skipped
                    {
                        modText += ReviewText("Unsubscribe this. It's functionality is already covered by:");
                        modText += otherModText + compatibilityNote;
                    }

                    // Incompatible or minor issues
                    if (statuses.Contains(Enums.CompatibilityStatus.IncompatibleAccordingToAuthor))
                    {
                        modText += ReviewText("This is incompatible with (unsubscribe either one):");
                        modText += otherModText + compatibilityNote;
                    }
                    else if (statuses.Contains(Enums.CompatibilityStatus.IncompatibleAccordingToUsers))
                    {
                        modText += ReviewText("This is said to be incompatible with (best to unsubscribe either one):");
                        modText += otherModText + compatibilityNote;
                    } else if (statuses.Contains(Enums.CompatibilityStatus.MinorIssues))
                    {
                        modText += ReviewText("This has reported issues with:");
                        modText += otherModText + compatibilityNote;
                    }

                    // Specific config
                    if (statuses.Contains(Enums.CompatibilityStatus.RequiresSpecificConfigForThisMod))      // RequiresSpecificConfigForOtherMod skipped
                    {
                        modText += ReviewText("This requires specific configuration to work together with:");
                        modText += otherModText + compatibilityNote;
                    }
                }
            }

            // General note for this mod
            modText += ReviewText(subscription.Note);

            // Workshop url for Workshop mods
            modText += (steamID > ModSettings.HighestFakeID) ? ReviewText("Steam Workshop page: " + Tools.GetWorkshopURL(steamID)) : "";

            // Unreported: regular properties:      SourceURL, Updated, Downloaded, Recommendations
            //             mod statuses:            SourceBundled, SourceObfuscated, UnconfirmedIssues
            //             compatibility statuses:  CompatibleAccordingToAuthor

            // Return the text for this subscription if it was reviewed, else add it to the non reviewed mods text
            if (subscription.IsReviewed)
            {
                return modText;
            }
            else
            {
                nonReviewedModsText += modText + "\n";

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
                bullet = (bullet == "") ? ModSettings.Bullet : bullet;

                if (cutOff && ((bullet + message).Length > ModSettings.MaxReportWidth))
                {
                    // Cut off the message, so the 'bulleted' message stays within maximum width
                    message = message.Substring(0, ModSettings.MaxReportWidth - bullet.Length - 3) + "...";

                    Logger.Log($"Report line too long: " + message, Logger.debug);
                }

                // Return 'bulleted' message
                return bullet + message + "\n";
            }                
        }
    }
}
