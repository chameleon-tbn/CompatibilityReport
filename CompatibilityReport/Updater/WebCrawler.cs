using System;
using System.Diagnostics;
using System.IO;
using CompatibilityReport.DataTypes;
using CompatibilityReport.Util;


// WebCrawler gathers information from the Steam Workshop pages for all mods and updates the catalog with this. This process takes quite some time (roughly 15 minutes).
// The following information is gathered:
// * Mod: name, author, publish and update dates, source url (GitHub only), compatible game version (from tag only), required DLC, required mods, incompatible stability
//        statuses: removed from workshop, unlisted in workshop, no description, no source available (remove only, when a source url is found)
// * Author: name, profile ID and custom url, last seen date (based on mod updates, not on comments), retired status (no mod update in x months; removed on new mod update)


namespace CompatibilityReport.Updater
{
    internal static class WebCrawler
    {
        // Start the WebCrawler. Download Steam webpages for all mods and updates the catalog with found information.
        internal static void Start(Catalog ActiveCatalog)
        {
            CatalogUpdater.SetReviewDate(DateTime.Now);

            // Get basic mod and author information from the Steam Workshop 'mod listing' pages
            if (GetBasicInfo())
            {
                // Get more details from the individual mod pages
                GetDetails(ActiveCatalog);
            }
        }
        

        // Get mod and author names and IDs from the Steam Workshop 'mod listing' pages and removes unlisted/removed statuses. Returns true if we found at least one mod.
        private static bool GetBasicInfo()
        {
            Stopwatch timer = Stopwatch.StartNew();

            Logger.UpdaterLog("Updater started downloading Steam Workshop 'mod listing' pages. This should take less than 1 minute.");

            uint totalMods = 0;
            
            // Go through the different mod listings: mods and camera scripts, both regular and incompatible
            foreach (string steamURL in ModSettings.steamModListingURLs)
            {
                Logger.UpdaterLog($"Starting downloads from { steamURL }");
                
                uint pageNumber = 0;

                // Download and read pages until we find no more mods, or we reach a maximum number of pages (to avoid missing the mark and continuing for eternity)
                while (pageNumber < ModSettings.steamMaxModListingPages)
                {
                    pageNumber++;

                    string url = $"{ steamURL }&p={ pageNumber }";

                    Exception ex = Toolkit.Download(url, ModSettings.steamDownloadedPageFullPath);

                    if (ex != null)
                    {
                        Logger.UpdaterLog($"Download process interrupted due to permanent error while downloading { url }", Logger.error);

                        Logger.Exception(ex, toUpdaterLog: true);

                        // Decrease the pageNumber to the last succesful page
                        pageNumber--;

                        break;
                    }

                    // Extract mod and author info from the downloaded page
                    uint modsFoundThisPage = ReadModListingPage(steamURL.Contains("incompatible"));

                    if (modsFoundThisPage == 0)
                    {
                        // No mods found on this page; decrease the page number to the last succesful page
                        pageNumber--;

                        // Log something if no mods were found at all
                        if (pageNumber == 0)
                        {
                            Logger.UpdaterLog("Found no mods on page 1.");
                        }

                        break;
                    }

                    totalMods += modsFoundThisPage;

                    Logger.UpdaterLog($"Found { modsFoundThisPage } mods on page { pageNumber }.");
                }
            }

            // Delete the temporary file
            Toolkit.DeleteFile(ModSettings.steamDownloadedPageFullPath);

            // Note: about 75% of the total time is downloading, the other 25% is processing
            timer.Stop();

            Logger.UpdaterLog($"Updater finished checking Steam Workshop 'mod listing' pages in { Toolkit.ElapsedTime(timer.ElapsedMilliseconds) }. " +
                $"{ totalMods } mods found.", duplicateToRegularLog: true);

            return totalMods > 0;
        }


        // Extract mod and author info from the downloaded mod listing page. Returns the number of mods found on this page.
        private static uint ReadModListingPage(bool incompatibleMods)
        {
            uint modsFoundThisPage = 0;

            string line;

            // Read the downloaded file
            using (StreamReader reader = File.OpenText(ModSettings.steamDownloadedPageFullPath))
            {
                // Read all the lines until the end of the file
                while ((line = reader.ReadLine()) != null)
                {
                    // Search for the identifying string for the next mod; continue with next line if not found
                    if (!line.Contains(ModSettings.steamModListingModFind))
                    {
                        continue;
                    }

                    // Found the identifying string; get the Steam ID
                    ulong steamID = Toolkit.ConvertToUlong(Toolkit.MidString(line, ModSettings.steamModListingModIDLeft, ModSettings.steamModListingModIDRight));

                    if (steamID == 0) 
                    {
                        // Steam ID was not recognized. This should not happen. Continue with the next line.
                        Logger.UpdaterLog("Steam ID not recognized on HTML line: " + line, Logger.error);

                        continue;
                    }

                    modsFoundThisPage++;

                    string name = Toolkit.CleanHtmlString(Toolkit.MidString(line, ModSettings.steamModListingModNameLeft, ModSettings.steamModListingModNameRight));

                    Mod catalogMod = CatalogUpdater.GetOrAddMod(steamID, name, incompatibleMods);

                    // (Re)set incompatible stability on existing mods, if it changed in the Steam Workshop
                    if (incompatibleMods && catalogMod.Stability != Enums.ModStability.IncompatibleAccordingToWorkshop)
                    {
                        CatalogUpdater.UpdateMod(catalogMod, stability: Enums.ModStability.IncompatibleAccordingToWorkshop, updatedByWebCrawler: true);
                    }
                    else if (!incompatibleMods && catalogMod.Stability == Enums.ModStability.IncompatibleAccordingToWorkshop)
                    {
                        CatalogUpdater.UpdateMod(catalogMod, stability: Enums.ModStability.Undefined, updatedByWebCrawler: true);
                    }

                    // Skip one line
                    line = reader.ReadLine();

                    // Get the author ID or custom URL. One will be found, the other will be zero / empty   [Todo 0.4] Add a check for author URL changes, to prevent creating a new author
                    ulong authorID = Toolkit.ConvertToUlong(Toolkit.MidString(line, ModSettings.steamModListingAuthorIDLeft, ModSettings.steamModListingAuthorRight));

                    string authorURL = Toolkit.MidString(line, ModSettings.steamModListingAuthorURLLeft, ModSettings.steamModListingAuthorRight);

                    // Remove the removed and unlisted statuses, if they exist
                    CatalogUpdater.RemoveStatus(catalogMod, Enums.ModStatus.RemovedFromWorkshop);

                    CatalogUpdater.RemoveStatus(catalogMod, Enums.ModStatus.UnlistedInWorkshop);

                    // Update the mod. This will also set the UpdatedThisSession, which is used in GetDetails()
                    CatalogUpdater.UpdateMod(catalogMod, name, authorID: authorID, authorURL: authorURL, alwaysUpdateReviewDate: true, updatedByWebCrawler: true);

                    string authorName = Toolkit.CleanHtmlString(Toolkit.MidString(line, ModSettings.steamModListingAuthorNameLeft, 
                        ModSettings.steamModListingAuthorNameRight));

                    CatalogUpdater.GetOrAddAuthor(authorID, authorURL, authorName);
                }
            }

            return modsFoundThisPage;
        }


        // Get mod information from the individual mod pages on the Steam Workshop
        private static void GetDetails(Catalog ActiveCatalog)
        {
            Stopwatch timer = Stopwatch.StartNew();

            int numberOfMods = ActiveCatalog.Mods.Count - ModSettings.BuiltinMods.Count;

            // Estimated time is about half a second (500 milliseconds) per download. Note: 90+% of the total time is download, less than 10% is processing
            long estimated = 500 * numberOfMods;

            Logger.UpdaterLog($"Updater started checking { numberOfMods } individual Steam Workshop mod pages. Estimated time: { Toolkit.ElapsedTime(estimated) }.", 
                duplicateToRegularLog: true);

            uint modsDownloaded = 0;

            uint failedDownloads = 0;

            foreach (Mod catalogMod in ActiveCatalog.Mods)
            {
                if (!ActiveCatalog.IsValidID(catalogMod.SteamID, allowBuiltin: false))
                {
                    // Skip builtin mods
                    continue;
                }

                // Download the Steam Workshop page for this mod
                if (Toolkit.Download(Toolkit.GetWorkshopURL(catalogMod.SteamID), ModSettings.steamDownloadedPageFullPath) != null)
                {
                    // Download error
                    failedDownloads++;

                    if (failedDownloads <= ModSettings.SteamMaxFailedPages)
                    {
                        // Download error might be mod specific. Go to the next mod.
                        Logger.UpdaterLog("Permanent error while downloading Steam Workshop page for " + 
                            $"{ catalogMod.ToString() }. Will continue with next mod.", Logger.error);

                        continue;
                    }
                    else
                    {
                        // Too many failed downloads. Stop downloading
                        Logger.UpdaterLog("Permanent error while downloading Steam Workshop page for " + 
                            $"{ catalogMod.ToString() }. Download process stopped.", Logger.error);

                        break;
                    }
                }
                modsDownloaded++;

                // Log a sign of life every 100 mods
                if (modsDownloaded % 100 == 0)
                {
                    Logger.UpdaterLog($"{ modsDownloaded }/{ numberOfMods } mods checked.");
                }

                // Extract detailed info from the downloaded page
                if (!ReadModPage(catalogMod))
                {
                    // Redownload and try again, to work around cut-off downloads
                    Toolkit.Download(Toolkit.GetWorkshopURL(catalogMod.SteamID), ModSettings.steamDownloadedPageFullPath);

                    ReadModPage(catalogMod);
                }
            }

            // Delete the temporary file
            Toolkit.DeleteFile(ModSettings.steamDownloadedPageFullPath);

            // Note: about 90% of the total time is downloading, the other 10% is processing
            timer.Stop();

            Logger.UpdaterLog($"Updater finished downloading { modsDownloaded } individual Steam Workshop mod pages in " + 
                $"{ Toolkit.ElapsedTime(timer.ElapsedMilliseconds, alwaysShowSeconds: true) }.", duplicateToRegularLog: true);
        }


        // Extract detailed mod information from the downloaded mod page; return false if there was an error with the mod page
        private static bool ReadModPage(Mod catalogMod)
        {
            bool steamIDmatched = false;

            // Read the downloaded page back from file
            using (StreamReader reader = File.OpenText(ModSettings.steamDownloadedPageFullPath))
            {
                string line;

                // Read all the lines until the end of the file
                while ((line = reader.ReadLine()) != null)
                {
                    // First find the correct Steam ID on this page; it appears before all other info
                    if (!steamIDmatched)
                    {
                        if (line.Contains(ModSettings.steamModPageItemNotFound))
                        {
                            // Steam says it can't find the mod, stop processing the page further
                            if (catalogMod.UpdatedThisSession)
                            {
                                // We found the mod in the mod listing, but not now. Must be a Steam error.
                                Logger.UpdaterLog($"We found this, but can't read the Steam page for { catalogMod.ToString() }. Mod info not updated.", Logger.warning);

                                // Return false to trigger a retry on the download
                                return false;
                            }
                            else
                            {
                                // Change the mod to removed
                                CatalogUpdater.AddStatus(catalogMod, Enums.ModStatus.RemovedFromWorkshop, updatedByWebCrawler: true);

                                // Return true because no retry on download is needed
                                return true;
                            }
                        }

                        steamIDmatched = line.Contains(ModSettings.steamModPageSteamID + catalogMod.SteamID.ToString());

                        // Keep trying to find the Steam ID before anything else
                        continue;
                    }

                    // Update removed and unlisted statuses: no longer removed and only unlisted if not found during GetBasicInfo()
                    CatalogUpdater.RemoveStatus(catalogMod, Enums.ModStatus.RemovedFromWorkshop);

                    if (catalogMod.UpdatedThisSession)
                    {
                        CatalogUpdater.RemoveStatus(catalogMod, Enums.ModStatus.UnlistedInWorkshop, updatedByWebCrawler: true);
                    }
                    else
                    {
                        CatalogUpdater.AddStatus(catalogMod, Enums.ModStatus.UnlistedInWorkshop, updatedByWebCrawler: true);
                    }

                    // Try to find data on this line of the mod page

                    // Author profile ID, custom URL and author name; only for unlisted mods (we have this info for other mods already)  [Todo 0.4] Add a check for author URL changes, to prevent creating a new author
                    if (line.Contains(ModSettings.steamModPageAuthorFind) && catalogMod.Statuses.Contains(Enums.ModStatus.UnlistedInWorkshop))
                    {
                        ulong authorID = Toolkit.ConvertToUlong(Toolkit.MidString(line, ModSettings.steamModPageAuthorFind + "profiles/",
                            ModSettings.steamModPageAuthorMid));

                        string authorURL = Toolkit.MidString(line, ModSettings.steamModPageAuthorFind + "id/", ModSettings.steamModPageAuthorMid);

                        // Empty the author custom URL if author ID was found or if custom URL was not found, preventing updating the custom URL to an empty string
                        authorURL = authorID != 0 || string.IsNullOrEmpty(authorURL) ? null : authorURL;

                        string authorName = Toolkit.CleanHtmlString(Toolkit.MidString(line, ModSettings.steamModPageAuthorMid, ModSettings.steamModPageAuthorRight));

                        catalogMod.Update(authorID: authorID, authorURL: authorURL);

                        CatalogUpdater.GetOrAddAuthor(authorID, authorURL, authorName);
                    }

                    // Mod name; only for unlisted mods (we have this info for other mods already)
                    else if (line.Contains(ModSettings.steamModPageNameLeft) && catalogMod.Statuses.Contains(Enums.ModStatus.UnlistedInWorkshop))
                    {
                        CatalogUpdater.UpdateMod(catalogMod, name: Toolkit.CleanHtmlString(
                            Toolkit.MidString(line, ModSettings.steamModPageNameLeft, ModSettings.steamModPageNameRight)), updatedByWebCrawler: true);
                    }

                    // Compatible game version tag
                    else if (line.Contains(ModSettings.steamModPageVersionTagFind))
                    {
                        // Convert the found tag to a game version and back to a formatted game version string, so we have a consistently formatted string
                        string gameVersionString = Toolkit.MidString(line, ModSettings.steamModPageVersionTagLeft, ModSettings.steamModPageVersionTagRight);

                        Version gameVersion = Toolkit.ConvertToGameVersion(gameVersionString);

                        gameVersionString = GameVersion.Formatted(gameVersion);

                        // Update the mod, unless an exclusion exists and the found gameversion is lower than in the catalog. Remove the exclusion on update.
                        if (!catalogMod.ExclusionForGameVersion || gameVersion > Toolkit.ConvertToGameVersion(catalogMod.CompatibleGameVersionString))
                        {
                            CatalogUpdater.UpdateMod(catalogMod, compatibleGameVersionString: gameVersionString, updatedByWebCrawler: true);

                            catalogMod.Update(exclusionForGameVersion: false);
                        }
                    }

                    // Publish and update dates. Also update author last seen date.
                    else if (line.Contains(ModSettings.steamModPageDatesFind))
                    {
                        // Skip two lines for the published data, then one more for the update date (if available)
                        line = reader.ReadLine();
                        line = reader.ReadLine();

                        DateTime published = Toolkit.ConvertWorkshopDateTime(Toolkit.MidString(line, ModSettings.steamModPageDatesLeft, ModSettings.steamModPageDatesRight));

                        line = reader.ReadLine();

                        DateTime updated = Toolkit.ConvertWorkshopDateTime(Toolkit.MidString(line, ModSettings.steamModPageDatesLeft, ModSettings.steamModPageDatesRight));

                        CatalogUpdater.UpdateMod(catalogMod, published: published, updated: updated, updatedByWebCrawler: true);
                    }

                    // Required DLC. This line can be found multiple times.
                    else if (line.Contains(ModSettings.steamModPageRequiredDLCFind))
                    {
                        // Skip one line
                        line = reader.ReadLine();

                        Enums.DLC dlc = Toolkit.ConvertToEnum<Enums.DLC>(
                            Toolkit.MidString(line, ModSettings.steamModPageRequiredDLCLeft, ModSettings.steamModPageRequiredDLCRight));

                        if (!catalogMod.ExclusionForRequiredDLC.Contains(dlc))
                        {
                            CatalogUpdater.AddRequiredDLC(catalogMod, dlc);
                        }
                    }

                    // Required mods and assets. The 'find' string is a container with all required items on the next lines.
                    else if (line.Contains(ModSettings.steamModPageRequiredModFind))
                    {
                        // Get all required items from the next lines, until we find no more. Max. 50 times to avoid an infinite loop.
                        for (uint tries = 1; tries <= 50; tries++)
                        {
                            // Skip one line, and three more at the end
                            line = reader.ReadLine();

                            ulong requiredID = Toolkit.ConvertToUlong(
                                Toolkit.MidString(line, ModSettings.steamModPageRequiredModLeft, ModSettings.steamModPageRequiredModRight));

                            // Exit the for loop if no more Steam ID is found
                            if (requiredID == 0)
                            {
                                break;
                            }

                            // Add the required mod (or asset) if it wasn't added already and no exclusion exists for this ID
                            if (!catalogMod.ExclusionForRequiredMods.Contains(requiredID))
                            {
                                CatalogUpdater.AddRequiredMod(catalogMod, requiredID);
                            }

                            line = reader.ReadLine();
                            line = reader.ReadLine();
                            line = reader.ReadLine();
                        }
                    }

                    // Description for 'no description' status and for source url
                    else if (line.Contains(ModSettings.steamModPageDescriptionFind))
                    {
                        // Skip one line; the complete description is on the next line
                        line = reader.ReadLine();

                        int descriptionLength = line.Length - line.IndexOf(ModSettings.steamModPageDescriptionLeft) -
                            ModSettings.steamModPageDescriptionLeft.Length - ModSettings.steamModPageDescriptionRight.Length;

                        // A 'no description' status is when the description is not at least a few characters longer than the mod name.
                        if (descriptionLength < catalogMod.Name.Length + 5 && !catalogMod.ExclusionForNoDescription)
                        {
                            CatalogUpdater.AddStatus(catalogMod, Enums.ModStatus.NoDescription, updatedByWebCrawler: true);
                        }
                        else if (descriptionLength > catalogMod.Name.Length + 5 && !catalogMod.ExclusionForNoDescription)
                        {
                            CatalogUpdater.RemoveStatus(catalogMod, Enums.ModStatus.NoDescription, updatedByWebCrawler: true);
                        }

                        // Try to get the source url, unless there is an exclusion.
                        if (line.Contains(ModSettings.steamModPageSourceURLLeft) && !catalogMod.ExclusionForSourceURL)
                        {
                            CatalogUpdater.UpdateMod(catalogMod, sourceURL: GetSourceURL(line, catalogMod), updatedByWebCrawler: true);
                        }

                        // Description is the last info we need from the page, so break out of the while loop
                        break;
                    }
                }
            }

            if (!steamIDmatched)
            {
                // We didn't find a Steam ID on the page, but no error page either. Must be a download issue or other Steam error.
                Logger.UpdaterLog($"Can't find the Steam ID on downloaded page for { catalogMod.ToString() }. Mod info not updated.", Logger.error);
            }

            return true;
        }


        // Get the source URL. If more than one is found, pick the most likely, which is far from perfect and will need a CSV update for some mods.
        private static string GetSourceURL(string line, Mod catalogMod)
        {
            string sourceURL = "https://github.com/" + Toolkit.MidString(line, ModSettings.steamModPageSourceURLLeft, ModSettings.steamModPageSourceURLRight);

            if (sourceURL == "https://github.com/")
            {
                return null;
            }

            // Some commonly listed source url's to always ignore: pardeike's Harmony and Sschoener's detour
            const string pardeike  = "https://github.com/pardeike";
            const string sschoener = "https://github.com/sschoener/cities-skylines-detour";

            string discardedURLs = "";

            uint tries = 0;

            // Keep comparing source url's until we find no more; max. 50 times to avoid infinite loops on code errors
            while (line.IndexOf(ModSettings.steamModPageSourceURLLeft) != line.LastIndexOf(ModSettings.steamModPageSourceURLLeft) && tries < 50)
            {
                tries++;

                string firstLower = sourceURL.ToLower();

                // Cut off the start of the line to just after the previous occurrence and find the next source url
                int index = line.IndexOf(ModSettings.steamModPageSourceURLLeft) + 1;

                line = line.Substring(index, line.Length - index);

                string nextSourceURL = "https://github.com/" + Toolkit.MidString(line, ModSettings.steamModPageSourceURLLeft, ModSettings.steamModPageSourceURLRight);

                string nextLower = nextSourceURL.ToLower();

                // Skip this source URL if it is empty, pardeike, sschoener or the same as the previous one
                if (nextLower == "https://github.com/" || nextLower.Contains(pardeike) || nextLower.Contains(sschoener) || nextLower == firstLower)
                {
                    continue;
                }

                // Silently discard the previous source url if it is pardeike or sschoener
                if (firstLower.Contains(pardeike) || firstLower.Contains(sschoener))
                {
                    sourceURL = nextSourceURL;
                }
                // Discard the previous url if it contains 'issue', 'wiki', 'documentation', 'readme', 'guide' or 'translation'.
                else if (firstLower.Contains("issue") || firstLower.Contains("wiki") || firstLower.Contains("documentation") 
                    || firstLower.Contains("readme") || firstLower.Contains("guide") || firstLower.Contains("translation"))
                {
                    discardedURLs += "\n                      Discarded: " + sourceURL;

                    sourceURL = nextSourceURL;
                }
                // Otherwise discard the new source url
                else
                {
                    discardedURLs += "\n                      Discarded: " + nextSourceURL;
                }
            }

            // Discard the selected source url if it is pardeike or sschoener. This can happen when that is the only github link in the description.
            if (sourceURL.Contains(pardeike) || sourceURL.Contains(sschoener))
            {
                sourceURL = null;
            }

            // Log the selected and discarded source URLs, if the selected source URL is different from the one in the catalog
            if (!string.IsNullOrEmpty(discardedURLs) && sourceURL != catalogMod.SourceURL)
            {
                Logger.UpdaterLog($"Found multiple source url's for { catalogMod.ToString() }" +
                    $"\n                      Selected:  { (string.IsNullOrEmpty(sourceURL) ? "none" : sourceURL) }{ discardedURLs }");
            }

            return sourceURL;
        }
    }
}
