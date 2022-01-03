using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using CompatibilityReport.CatalogData;
using CompatibilityReport.Util;

namespace CompatibilityReport.Updater
{
    /// <summary>WebCrawler gathers information from the Steam Workshop pages for all mods and updates the catalog with this.</summary>
    /// <remarks>This process takes roughly 15 minutes. The following information is gathered:<list type="bullet">
    /// <item>Mod: name, author, publish and update dates, source URL (GitHub links only), compatible game version (from tag), required DLCs, required mods,
    ///            incompatible stability, removed from or unlisted in the Steam Workshop status, no description status</item>
    /// <item>Author: name, Steam ID or Custom URL, last seen date (based on mod updates, not on comments), retired status (based on last seen date)</item></list></remarks>
    public static class WebCrawler
    {
        /// <summary>Starts the WebCrawler. Downloads Steam webpages for all mods and update the catalog with found information.</summary>
        public static void Start(Catalog catalog)
        {
            CatalogUpdater.SetReviewDate(DateTime.Now);

            if (GetBasicInfo(catalog))
            {
                GetDetails(catalog);

                GetMapThemes(catalog);

                // Todo 0.8 This can be removed after updater settings are implemented. Make disable-after-run an option in the settings file.
                Toolkit.MoveFile(Path.Combine(ModSettings.UpdaterPath, $"{ ModSettings.InternalName }_WebCrawler.enabled"), 
                    Path.Combine(ModSettings.UpdaterPath, $"{ ModSettings.InternalName }_WebCrawler.disabled"));
            }
        }


        /// <summary>Downloads 'mod listing' pages from the Steam Workshop to get mod names and IDs for all available mods.</summary>
        /// <returns>True if at least one mod was found, false otherwise.</returns>
        private static bool GetBasicInfo(Catalog catalog)
        {
            Logger.UpdaterLog("Updater started downloading Steam Workshop 'mod listing' pages. This should take less than 1 minute.");

            int totalMods = 0;
            int totalPages = 0;
            
            // Go through the different mod listings: mods and camera scripts, both regular and incompatible.
            foreach (string steamUrl in ModSettings.SteamModListingUrls)
            {
                Logger.UpdaterLog($"Starting downloads from { steamUrl }");
                
                int pageNumber = 0;

                // Download and read pages until we find no more mods, or we reach a maximum number of pages, to avoid missing the mark and continuing for eternity.
                while (pageNumber < ModSettings.SteamMaxListingPages)
                {
                    pageNumber++;
                    string url = $"{ steamUrl }&p={ pageNumber }";

                    if (!Toolkit.Download(url, ModSettings.TempDownloadFullPath))
                    {
                        pageNumber--;

                        Logger.UpdaterLog($"Download process interrupted due to a permanent error while downloading { url }", Logger.Error);
                        break;
                    }

                    int modsFoundThisPage = ReadModListingPage(catalog, incompatibleMods: steamUrl.Contains("incompatible"));

                    if (modsFoundThisPage == 0)
                    {
                        pageNumber--;

                        if (pageNumber == 0)
                        {
                            Logger.UpdaterLog("Found no mods on page 1.");
                        }

                        break;
                    }

                    totalMods += modsFoundThisPage;
                    Logger.UpdaterLog($"Found { modsFoundThisPage } mods on page { pageNumber }.");
                }

                totalPages += pageNumber;
            }

            Toolkit.DeleteFile(ModSettings.TempDownloadFullPath);

            // Note: about 75% of the total time is downloading, the other 25% is processing.
            Logger.UpdaterLog($"Updater finished downloading { totalPages } Steam Workshop 'mod listing' pages and found { totalMods } mods.");

            return totalMods > 0;
        }


        /// <summary>Extracts Steam IDs and mod names for all mods from a downloaded mod listing page and adds/updates this in the catalog.</summary>
        /// <remarks>Sets the auto review date, (re)sets 'incompatible according to workshop' stability and removes unlisted and 'removed from workshop' statuses.</remarks>
        /// <returns>The number of mods found on this page.</returns>
        private static int ReadModListingPage(Catalog catalog, bool incompatibleMods)
        {
            int modsFoundThisPage = 0;
            string line;

            using (StreamReader reader = File.OpenText(ModSettings.TempDownloadFullPath))
            {
                while ((line = reader.ReadLine()) != null)
                {
                    // Search for the identifying string for the next mod; continue with next line if not found.
                    if (!line.Contains(ModSettings.SearchModStart))
                    {
                        continue;
                    }

                    ulong steamID = Toolkit.ConvertToUlong(Toolkit.MidString(line, ModSettings.SearchSteamIDLeft, ModSettings.SearchSteamIDRight));

                    if (steamID == 0) 
                    {
                        Logger.UpdaterLog($"Steam ID not recognized on HTML line: { line }", Logger.Error);
                        continue;
                    }

                    modsFoundThisPage++;

                    string modName = Toolkit.CleanHtml(Toolkit.MidString(line, ModSettings.SearchListingModNameLeft, ModSettings.SearchListingModNameRight));

                    if (string.IsNullOrEmpty(modName) && !catalog.SuppressedWarnings.Contains(steamID))
                    {
                        // An empty mod name might be an error, although there is a Steam Workshop mod without a name (ofcourse there is).
                        Logger.UpdaterLog($"Mod name not found for { steamID }. This could be an actual unnamed mod, or a Steam error.", Logger.Warning);
                    }

                    Mod catalogMod = catalog.GetMod(steamID) ?? CatalogUpdater.AddMod(catalog, steamID, modName, incompatibleMods);

                    CatalogUpdater.RemoveStatus(catalog, catalogMod, Enums.Status.RemovedFromWorkshop);
                    CatalogUpdater.RemoveStatus(catalog, catalogMod, Enums.Status.UnlistedInWorkshop);

                    if (incompatibleMods && catalogMod.Stability != Enums.Stability.IncompatibleAccordingToWorkshop)
                    {
                        CatalogUpdater.UpdateMod(catalog, catalogMod, stability: Enums.Stability.IncompatibleAccordingToWorkshop, stabilityNote: "");
                    }
                    else if (!incompatibleMods && catalogMod.Stability == Enums.Stability.IncompatibleAccordingToWorkshop)
                    {
                        CatalogUpdater.UpdateMod(catalog, catalogMod, stability: Enums.Stability.NotReviewed, stabilityNote: "");
                    }

                    // Update the name and the auto review date.
                    CatalogUpdater.UpdateMod(catalog, catalogMod, modName);

                    // Author info can be found on the next line, but skip it here and get it later on the mod page.
                }
            }

            return modsFoundThisPage;
        }


        /// <summary>Downloads 'Map Theme listing' pages from the Steam Workshop to get Steam IDs for all available map themes.</summary>
        /// <remarks>The Steam IDs will be added to the list of Map Themes.</remarks>
        private static void GetMapThemes(Catalog catalog)
        {
            Logger.UpdaterLog("Updater started downloading Steam Workshop 'map theme listing' pages. This should take less than 2 minutes.");

            string steamUrl = ModSettings.SteamMapThemesListingUrl;
            int pageNumber = 0;
            int newMapThemes = 0;

            // Download and read pages until we find no more map themes, or we reach a maximum number of pages, to avoid missing the mark and continuing for eternity.
            while (pageNumber < ModSettings.SteamMaxListingPages)
            {
                pageNumber++;
                int mapThemesFoundThisPage = 0;
                string url = $"{ steamUrl }&p={ pageNumber }";

                if (!Toolkit.Download(url, ModSettings.TempDownloadFullPath))
                {
                    Logger.UpdaterLog($"Download process interrupted due to a permanent error while downloading { url }", Logger.Error);

                    pageNumber--;
                    break;
                }

                using (StreamReader reader = File.OpenText(ModSettings.TempDownloadFullPath))
                {
                    string line;

                    while ((line = reader.ReadLine()) != null)
                    {
                        // Search for the identifying string for the next map theme; continue with next line if not found.
                        if (!line.Contains(ModSettings.SearchModStart))
                        {
                            continue;
                        }

                        ulong steamID = Toolkit.ConvertToUlong(Toolkit.MidString(line, ModSettings.SearchSteamIDLeft, ModSettings.SearchSteamIDRight));

                        if (steamID == 0)
                        {
                            Logger.UpdaterLog($"Steam ID not recognized on HTML line: { line }", Logger.Error);
                            continue;
                        }

                        // Add the found map theme Steam ID only if that Steam ID is not a valid mod in the catalog.
                        if (catalog.IsValidID(steamID, shouldExist: false) && catalog.AddMapTheme(steamID))
                        {
                            newMapThemes++;
                        }

                        mapThemesFoundThisPage++;
                    }
                }

                if (mapThemesFoundThisPage == 0)
                {
                    pageNumber--;
                    break;
                }
            }

            Toolkit.DeleteFile(ModSettings.TempDownloadFullPath);
            Logger.UpdaterLog($"Updater finished downloading { pageNumber } Steam Workshop 'map themes listing' pages and found { newMapThemes } new map themes.");
        }


    /// <summary>Downloads individual mod pages from the Steam Workshop to get detailed mod information for all mods in the catalog.</summary>
    /// <remarks>Known unlisted mods are included. Removed mods are checked, to catch reappearing mods.</remarks>
    private static void GetDetails(Catalog catalog)
        {
            Stopwatch timer = Stopwatch.StartNew();
            int numberOfMods = catalog.Mods.Count - ModSettings.BuiltinMods.Count;
            long estimatedMilliseconds = ModSettings.EstimatedMillisecondsPerModPage * numberOfMods;

            Logger.UpdaterLog($"Updater started downloading { numberOfMods } individual Steam Workshop mod pages. This should take about " +
                $"{ Toolkit.TimeString(estimatedMilliseconds) } and should be ready around { DateTime.Now.AddMilliseconds(estimatedMilliseconds + 30*1000):HH:mm}.");

            int modsDownloaded = 0;
            int failedDownloads = 0;

            foreach (Mod catalogMod in catalog.Mods)
            {
                if (!catalog.IsValidID(catalogMod.SteamID, allowBuiltin: false))
                {
                    // Skip built-in mods.
                    continue;
                }

                if (!Toolkit.Download(Toolkit.GetWorkshopUrl(catalogMod.SteamID), ModSettings.TempDownloadFullPath))
                {
                    failedDownloads++;

                    Logger.UpdaterLog($"Permanent error while downloading Steam Workshop page for { catalogMod.ToString() }. Details not updated.", Logger.Error);

                    if (failedDownloads <= ModSettings.SteamMaxFailedPages)
                    {
                        continue;
                    }
                    else
                    {
                        Logger.UpdaterLog("Download process stopped prematurely.", Logger.Error);
                        break;
                    }
                }

                modsDownloaded++;

                if (modsDownloaded % 100 == 0)
                {
                    Logger.UpdaterLog($"{ modsDownloaded }/{ numberOfMods } mod pages downloaded.");
                }

                if (!ReadModPage(catalog, catalogMod))
                {
                    // Redownload and try one more time, to work around cut-off downloads.
                    Toolkit.Download(Toolkit.GetWorkshopUrl(catalogMod.SteamID), ModSettings.TempDownloadFullPath);
                    if (!ReadModPage(catalog, catalogMod))
                    {
                        Logger.UpdaterLog($"Mod info not updated for { catalogMod.ToString() }.", Logger.Error);
                    }
                    else
                    {
                        Logger.UpdaterLog($"Steam page correctly read on retry for { catalogMod.ToString() }. Mod info updated.");
                    }
                }
            }

            Toolkit.DeleteFile(ModSettings.TempDownloadFullPath);

            // Note: about 90% of the total time is downloading, the other 10% is processing.
            Logger.UpdaterLog($"Updater finished downloading { modsDownloaded } individual Steam Workshop mod pages in " + 
                $"{ Toolkit.TimeString(timer.ElapsedMilliseconds, alwaysShowSeconds: true) }.");
        }


        /// <summary>Extracts detailed mod information from a downloaded mod page and updates the catalog.</summary>
        /// <returns>True if succesful, false if there was an error with the mod page.</returns>
        public static bool ReadModPage(Catalog catalog, Mod catalogMod)
        {
            List<Enums.Dlc> RequiredDlcsToRemove = new List<Enums.Dlc>(catalogMod.RequiredDlcs);
            bool steamIDmatched = false;
            string line;

            using (StreamReader reader = File.OpenText(ModSettings.TempDownloadFullPath))
            {
                while ((line = reader.ReadLine()) != null)
                {
                    if (!steamIDmatched)
                    {
                        steamIDmatched = line.Contains($"{ ModSettings.SearchSteamID }{catalogMod.SteamID}");

                        if (steamIDmatched)
                        {
                            CatalogUpdater.RemoveStatus(catalog, catalogMod, Enums.Status.RemovedFromWorkshop);

                            if (!catalogMod.UpdatedThisSession)
                            {
                                // Set the unlisted status. Also update the auto review date, which is already done for all listed mods.
                                CatalogUpdater.AddStatus(catalog, catalogMod, Enums.Status.UnlistedInWorkshop);
                                CatalogUpdater.UpdateMod(catalog, catalogMod);
                            }
                        }

                        else if (line.Contains(ModSettings.SearchItemNotFound))
                        {
                            if (catalogMod.UpdatedThisSession)
                            {
                                Logger.UpdaterLog($"We found this mod, but can't read the Steam page for { catalogMod.ToString() }.", Logger.Warning);
                                return false;
                            }
                            else
                            {
                                CatalogUpdater.AddStatus(catalog, catalogMod, Enums.Status.RemovedFromWorkshop);
                                return true;
                            }
                        }

                        // Keep reading lines until we find the Steam ID.
                        continue;
                    }

                    // Author Steam ID, Custom URL and author name.
                    if (line.Contains(ModSettings.SearchAuthorLeft))
                    {
                        // Only get the author URL if the author ID was not found, to prevent updating the author URL to an empty string.
                        ulong authorID = Toolkit.ConvertToUlong(Toolkit.MidString(line, $"{ ModSettings.SearchAuthorLeft }profiles/", ModSettings.SearchAuthorMid));
                        string authorUrl = authorID != 0 ? null : Toolkit.MidString(line, $"{ ModSettings.SearchAuthorLeft }id/", ModSettings.SearchAuthorMid);

                        // Author name needs to be cleaned twice because of how it is presented in the HTML source.
                        string authorName = Toolkit.CleanHtml(Toolkit.CleanHtml(Toolkit.MidString(line, ModSettings.SearchAuthorMid, ModSettings.SearchAuthorRight)));

                        // Try to get the author with ID/URL from the mod, to prevent creating a new author on an ID/URL change or when Steam gives ID instead of URL.
                        // On a new mod that fails, so try the newly found ID/URL. If it still fails we have an unknown author, so create a new author.
                        // Todo 1.x This is not foolproof and we can still accidentally create a new author on new mods. Requires Steam API for better reliability.
                        Author catalogAuthor = catalog.GetAuthor(catalogMod.AuthorID, catalogMod.AuthorUrl) ?? catalog.GetAuthor(authorID, authorUrl) ??
                            CatalogUpdater.AddAuthor(catalog, authorID, authorUrl, authorName);

                        if (authorName == "")
                        {
                            if (string.IsNullOrEmpty(catalogAuthor.Name))
                            {
                                Logger.UpdaterLog($"Author found without a name: { catalogAuthor.ToString() }.", Logger.Error);
                            }

                            // Don't update the name to an empty string.
                            authorName = null;
                        }
                        else if (authorName == authorID.ToString() && authorID != 0 && (authorName != catalogAuthor.Name || catalogAuthor.AddedThisSession))
                        {
                            // An author name equal to the author ID is a common Steam error, although some authors really have their ID as name (ofcourse they do).
                            if (string.IsNullOrEmpty(catalogAuthor.Name) || catalogAuthor.AddedThisSession)
                            {
                                Logger.UpdaterLog($"Author found with Steam ID as name: { authorName }. Some authors do this, but it could be a Steam error.", 
                                    Logger.Warning);
                            }
                            else
                            {
                                Logger.UpdaterLog($"Author found with Steam ID as name: { authorName }. Old name still used: { catalogAuthor.Name }.");
                            }

                            // Don't update the name if we already know a name.
                            authorName = string.IsNullOrEmpty(catalogAuthor.Name) ? authorName : null;
                        }

                        // Update the author. All mods from the author will be updated, including this one if it already existed in the catalog.
                        CatalogUpdater.UpdateAuthor(catalog, catalogAuthor, authorID, authorUrl, authorName, updatedByImporter: false);

                        // Still need to update the mod if this is a new mod.
                        CatalogUpdater.UpdateMod(catalog, catalogMod, authorID: catalogAuthor.SteamID, authorUrl: catalogAuthor.CustomUrl);
                    }

                    // Mod name.
                    else if (line.Contains(ModSettings.SearchModNameLeft))
                    {
                        string modName = Toolkit.CleanHtml(Toolkit.MidString(line, ModSettings.SearchModNameLeft, ModSettings.SearchModNameRight));

                        if (string.IsNullOrEmpty(modName) && !catalog.SuppressedWarnings.Contains(catalogMod.SteamID))
                        {
                            // An empty mod name might be an error, although there is a Steam Workshop mod without a name (ofcourse there is).
                            Logger.UpdaterLog($"Mod name not found for { catalogMod.SteamID }. This could be an actual unnamed mod, or a Steam error.", Logger.Warning);
                        }

                        CatalogUpdater.UpdateMod(catalog, catalogMod, modName);
                    }

                    // Compatible game version tag
                    else if (line.Contains(ModSettings.SearchVersionTag))
                    {
                        // Convert the found tag to a game version and back to a formatted game version string, so we have a consistently formatted string.
                        string gameVersionString = Toolkit.MidString(line, ModSettings.SearchVersionTagLeft, ModSettings.SearchVersionTagRight);
                        Version gameVersion = Toolkit.ConvertToVersion(gameVersionString);
                        gameVersionString = Toolkit.ConvertGameVersionToString(gameVersion);

                        if (!catalogMod.ExclusionForGameVersion || gameVersion >= catalogMod.GameVersion())
                        {
                            CatalogUpdater.UpdateMod(catalog, catalogMod, gameVersionString: gameVersionString);
                            catalogMod.UpdateExclusions(exclusionForGameVersion: false);
                        }
                    }

                    // Publish and update dates.
                    else if (line.Contains(ModSettings.SearchDates))
                    {
                        line = reader.ReadLine();
                        line = reader.ReadLine();
                        DateTime published = Toolkit.ConvertWorkshopDateTime(Toolkit.MidString(line, ModSettings.SearchDatesLeft, ModSettings.SearchDatesRight));

                        line = reader.ReadLine();
                        DateTime updated = Toolkit.ConvertWorkshopDateTime(Toolkit.MidString(line, ModSettings.SearchDatesLeft, ModSettings.SearchDatesRight));

                        CatalogUpdater.UpdateMod(catalog, catalogMod, published: published, updated: updated);
                    }

                    // Required DLC. This line can be found multiple times.
                    else if (line.Contains(ModSettings.SearchRequiredDlc))
                    {
                        line = reader.ReadLine();
                        Enums.Dlc dlc = Toolkit.ConvertToEnum<Enums.Dlc>(Toolkit.MidString(line, ModSettings.SearchRequiredDlcLeft, ModSettings.SearchRequiredDlcRight));

                        if (dlc != default)
                        {
                            if (catalogMod.ExclusionForRequiredDlcs.Contains(dlc) && catalogMod.RequiredDlcs.Contains(dlc))
                            {
                                // This required DLC was manually added (thus the exclusion), but was now found on the Steam page. The exclusion is no longer needed.
                                catalogMod.RemoveExclusion(dlc);
                            }

                            // If an exclusion exists at this point, then it's about a required DLC that was manually removed. Don't add it again.
                            if (!catalogMod.ExclusionForRequiredDlcs.Contains(dlc))
                            {
                                CatalogUpdater.AddRequiredDlc(catalog, catalogMod, dlc);
                                RequiredDlcsToRemove.Remove(dlc);
                            }
                        }
                    }

                    // Required mods and assets. The search string is a container with all required items on the next lines.
                    else if (line.Contains(ModSettings.SearchRequiredMod))
                    {
                        List<ulong> RequiredModsToRemove = new List<ulong>(catalogMod.RequiredMods);

                        // Get all required items from the next lines, until we find no more. Max. 50 times to avoid an infinite loop.
                        for (var i = 1; i <= 50; i++)
                        {
                            line = reader.ReadLine();
                            ulong requiredID = Toolkit.ConvertToUlong(Toolkit.MidString(line, ModSettings.SearchRequiredModLeft, ModSettings.SearchRequiredModRight));

                            if (requiredID == 0)
                            {
                                break;
                            }

                            if (catalogMod.ExclusionForRequiredMods.Contains(requiredID) && catalogMod.RequiredMods.Contains(requiredID))
                            {
                                // This required mod was manually added (thus the exclusion), but was now found on the Steam page. The exclusion is no longer needed.
                                catalogMod.RemoveExclusion(requiredID);
                            }

                            // If an exclusion exists at this point, then it's about a required mod that was manually removed. Don't add it again.
                            if (!catalogMod.ExclusionForRequiredMods.Contains(requiredID))
                            {
                                CatalogUpdater.AddRequiredMod(catalog, catalogMod, requiredID, updatedByImporter: false);
                                RequiredModsToRemove.Remove(requiredID);
                            }

                            line = reader.ReadLine();
                            line = reader.ReadLine();
                            line = reader.ReadLine();
                        }

                        foreach (ulong oldRequiredID in RequiredModsToRemove)
                        {
                            if (!catalogMod.ExclusionForRequiredMods.Contains(oldRequiredID))
                            {
                                CatalogUpdater.RemoveRequiredMod(catalog, catalogMod, oldRequiredID, updatedByImporter: false);
                            }
                        }
                    }

                    // Description for 'no description' status and for source URL.
                    else if (line.Contains(ModSettings.SearchDescription))
                    {
                        // We can't search for the right part, because it might exist inside the description.
                        StringBuilder descriptionSB = new StringBuilder(Toolkit.MidString($"{ reader.ReadLine() }\n", ModSettings.SearchDescriptionLeft, "\n"));

                        // Usually, the complete description is on one line. However, it might be split when using certain bbcode. Combine up to 30 lines.
                        for (var i = 1; i <= 30; i++)
                        {
                            line = reader.ReadLine();

                            // Break out of the for loop when a line is found that marks the end of the description.
                            if (line == ModSettings.SearchDescriptionNextLine || line == ModSettings.SearchDescriptionNextSection)
                            {
                                break;
                            }

                            descriptionSB.Append(line);
                        }

                        // A 'no description' status is when the description is not at least a few characters longer than the mod name.
                        int noDescriptionThreshold = catalogMod.Name.Length + 3 + ModSettings.SearchDescriptionRight.Length;

                        if ((descriptionSB.Length <= noDescriptionThreshold) && !catalogMod.ExclusionForNoDescription)
                        {
                            CatalogUpdater.AddStatus(catalog, catalogMod, Enums.Status.NoDescription);
                        }
                        else
                        {
                            if ((descriptionSB.Length > noDescriptionThreshold) && !catalogMod.ExclusionForNoDescription)
                            {
                                CatalogUpdater.RemoveStatus(catalog, catalogMod, Enums.Status.NoDescription);
                            }

                            // Try to find the source URL, unless an exception exists.
                            if (!catalogMod.ExclusionForSourceUrl)
                            {
                                CatalogUpdater.UpdateMod(catalog, catalogMod, sourceUrl: GetSourceUrl(descriptionSB.ToString(), catalogMod));
                            }
                        }

                        // Description is the last info we need from the page, so break out of the while loop.
                        break;
                    }
                }
            }

            if (!steamIDmatched && !catalogMod.Statuses.Contains(Enums.Status.RemovedFromWorkshop))
            {
                // We didn't find a Steam ID on the page, but no error page either. Must be a download issue or another Steam error.
                Toolkit.CopyFile(ModSettings.TempDownloadFullPath, Path.Combine(ModSettings.UpdaterPath, $"{ catalogMod.SteamID } - error.html"));

                Logger.UpdaterLog($"Can't find the Steam ID on downloaded page for { catalogMod.ToString() }. Downloaded page saved.", Logger.Warning);
                return false;
            }

            foreach (Enums.Dlc oldRequiredDlc in RequiredDlcsToRemove)
            {
                CatalogUpdater.RemoveRequiredDlc(catalog, catalogMod, oldRequiredDlc);
            }

            return true;
        }


        /// <summary>Gets the source URL.</summary>
        /// <remarks>If more than one is found, pick the first one that isn't on the ignore or discard list. This may occasionally require a correction by CSV.</remarks>
        /// <returns>The source URL string, or null if no source URL was found.</returns>
        private static string GetSourceUrl(string steamDescription, Mod catalogMod)
        {
            string sourceUrl = "";
            string lowerUrl = "";
            string discardedUrls = "";

            // Keep comparing source URLs until we find no more. Max. 50 times to avoid infinite loops.
            for (var i = 1; i <= 50; i++)
            {
                int index = -1;
                string newSourceSite = "";

                // Find the first source URL in the (remaining) description.
                foreach (string searchSourceUrlSite in ModSettings.SearchSourceUrlSites)
                {
                    int newIndex = steamDescription.IndexOf($"{ ModSettings.SearchSteamUrlFilter }{ searchSourceUrlSite }");

                    if (newIndex != -1 && (index == -1 || newIndex < index))
                    {
                        index = newIndex;
                        newSourceSite = searchSourceUrlSite;
                    }
                }

                string newSourceUrl = Toolkit.MidString(steamDescription, $"{ ModSettings.SearchSteamUrlFilter }{ newSourceSite }", ModSettings.SearchSourceUrlRight);

                // Break out of the for loop if no (more) source URL was found.
                if (string.IsNullOrEmpty(newSourceSite) || string.IsNullOrEmpty(newSourceUrl))
                {
                    break;
                }

                newSourceUrl = $"{ newSourceSite }{ newSourceUrl }";
                string newLowerUrl = newSourceUrl.ToLower();

                // Time to decide if we want to select this source URL.
                foreach (string ignoreUrl in ModSettings.CommonSourceUrlsToIgnore)
                {
                    if (newLowerUrl.Contains(ignoreUrl))
                    {
                        // Set newLowerUrl so it will be detected below and this source URL will be silently ignored. And break out of the foreach loop.
                        newLowerUrl = lowerUrl;
                        break;
                    }
                }

                if (newLowerUrl == lowerUrl)
                {
                    // Silently discard the newly found source URL if it is the same as the currently selected one, or if it is in the ignore list.
                }
                else if (string.IsNullOrEmpty(sourceUrl))
                {
                    // Select this source URL if it's the first (non-ignored) one we found.
                    sourceUrl = newSourceUrl;
                    lowerUrl = newLowerUrl;
                }
                else
                {
                    // Discard the previously found source URL if it is found in the discard list, and select the new one.
                    foreach (string discardUrl in ModSettings.sourceUrlsToDiscard)
                    {
                        if (lowerUrl.Contains(discardUrl))
                        {
                            discardedUrls += $"\n                      * Discarded: { sourceUrl }";

                            sourceUrl = newSourceUrl;
                            lowerUrl = newLowerUrl;

                            // Break out of the foreach loop
                            break;
                        }
                    }

                    // Discard the newly found source URL if we didn't just discard the old one.
                    if (sourceUrl != newSourceUrl)
                    {
                        discardedUrls += $"\n                      * Discarded: { newSourceUrl }";
                    }
                }

                // Cut off the first part of the description to just behind the found source URL.
                steamDescription = steamDescription.Substring(index + ModSettings.SearchSteamUrlFilter.Length + newSourceUrl.Length);
            }

            if (!string.IsNullOrEmpty(discardedUrls) && sourceUrl != catalogMod.SourceUrl)
            {
                Logger.UpdaterLog($"Found multiple source URLs for { catalogMod.ToString() }\n                      * Selected:  { sourceUrl }{ discardedUrls }");
            }

            return string.IsNullOrEmpty(sourceUrl) ? null : sourceUrl;
        }
    }
}
