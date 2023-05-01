using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using CompatibilityReport.CatalogData;
using CompatibilityReport.Settings;
using CompatibilityReport.Settings.ConfigData;
using CompatibilityReport.UI;
using CompatibilityReport.Util;
using UnityEngine;
using Logger = CompatibilityReport.Util.Logger;

namespace CompatibilityReport.Updater
{
    /// <summary>WebCrawler gathers information from the Steam Workshop pages for all mods and updates the catalog with this.</summary>
    /// <remarks>This process takes roughly 15 minutes. The following information is gathered:<list type="bullet">
    /// <item>Mod: name, author, publish and update dates, source URL (GitHub links only), compatible game version (from tag), required DLCs, required mods,
    ///            incompatible stability, removed from or unlisted in the Steam Workshop status, no description status</item>
    /// <item>Author: name, Steam ID or Custom URL, last seen date (based on mod updates, not on comments), retired status (based on last seen date)</item></list></remarks>
    public static class WebCrawler
    {
        
        /// <summary>Starts the WebCrawler, reports progress. Downloads Steam webpages for all mods and update the catalog with found information.</summary>
        public static IEnumerator StartWithProgress(Catalog catalog, ProgressMonitor progressMonitor, bool quick = false)
        {
            CatalogUpdater.SetReviewDate(DateTime.Now);

            progressMonitor.PushMessage("Web Crawler processing Catalog...");
            bool shouldContinue = false;
            HashSet<ulong> detectedModIds = new HashSet<ulong>();
            yield return GetBasicInfoWithProgress(catalog, progressMonitor, (result, mods) => {
                shouldContinue = result;
                detectedModIds = new HashSet<ulong>(mods);
            });
            
            if (shouldContinue)
            {
                progressMonitor.PushMessage("Web Crawler fetching mod details...");
                yield return GetDetailsWithProgress(catalog, progressMonitor, detectedModIds, quick);

                progressMonitor.PushMessage("Web Crawler fetching map themes...");
                yield return GetMapThemesWithProgress(catalog, progressMonitor);
            }

            progressMonitor.UpdateStage(7, 8);
            progressMonitor.PushMessage("Web Crawler finished.");
            yield return new WaitForSeconds(1);
            progressMonitor.UpdateStage(8, 8);
            progressMonitor.PushMessage("Data dumper started...");
            
            DataDumper.Start(catalog, isUpdater: false);
            
            progressMonitor.PushMessage("Data dumper finished.");
            yield return new WaitForSeconds(1);
            progressMonitor.PushMessage("Catalog dumper started...");

            string dumpPath = Path.Combine(ModSettings.UpdaterPath, ModSettings.CatalogDumpFileName);
            bool dumpSuccess = catalog.Save(dumpPath, createBackup: true);
            progressMonitor.PushMessage($"Catalog dumper finished. {(dumpSuccess ? "<color #00ff00>Sucess</color>" : "<color #ff0000>Failure</color>")}!\n See: {dumpPath}");
            
            yield return new WaitForSeconds(1);
            // NO_NEED - additionally dump as downloaded catalog
            // string dumpPathAsDownloaded = Path.Combine(ModSettings.WorkPath, ModSettings.DownloadedCatalogFileName);
            // bool dump2Success = catalog.Save(dumpPathAsDownloaded, createBackup: true, skipCompressed: true);
            // progressMonitor.PushMessage($"Dumped as downloaded catalog. {(dump2Success ? "<color #00ff00>Sucess</color>" : "<color #ff0000>Failure</color>")}!\n See: {dumpPathAsDownloaded}");
    
            yield return new WaitForSeconds(3);
            progressMonitor.Dispose();
        }


        /// <summary>Downloads 'mod listing' pages from the Steam Workshop to get mod names and IDs for all available mods.</summary>
        /// <returns>Enumerator, invokes onCompleted(bool) with status True if at least one mod was found, false otherwise.</returns>
        private static IEnumerator GetBasicInfoWithProgress(Catalog catalog, ProgressMonitor monitor, Action<bool, List<ulong>> onCompleted)
        {
            Logger.UpdaterLog("Updater started downloading Steam Workshop 'mod listing' pages. This should take less than 1 minute.");

            int totalMods = 0;
            int totalPages = 0;
            UpdaterConfig updaterConfig = GlobalConfig.Instance.UpdaterConfig;
            int maxListingPages = updaterConfig.SteamMaxListingPages;
            int index = 0;
            List<ulong> detectedModIds = new List<ulong>();
            // Go through the different mod listings: mods and camera scripts, both regular and incompatible.
            foreach (string steamUrl in ModSettings.SteamModListingUrls)
            {
                monitor.UpdateStage(1 + index++, 8);
                Logger.UpdaterLog($"Starting downloads from { steamUrl }");
                
                int pageNumber = 0;
                monitor.StartProgress(maxListingPages, $"Starting downloads from url: { steamUrl.Split(new string[] {"requiredtags"}, StringSplitOptions.None)[1].Substring(2) }", true);
                monitor.PushMessage($"Starting downloads from url: <color #FFFF00>{ steamUrl.Split(new string[] {"requiredtags"}, StringSplitOptions.None)[1].Substring(2) }</color>");
                // Download and read pages until we find no more mods, or we reach a maximum number of pages, to avoid missing the mark and continuing for eternity.
                while (pageNumber < maxListingPages)
                {
                    pageNumber++;
                    string url = $"{ steamUrl }&p={ pageNumber }";

                    KeyValuePair<bool, string> statusWithText = new KeyValuePair<bool, string>();
                    yield return Toolkit.DownloadPageText(url, result => statusWithText = result);

                    monitor.ReportProgress(pageNumber, $"Downloading <color #00ff00>{pageNumber}</color> of <color #16a085>{maxListingPages}</color>");
                    if (!statusWithText.Key)
                    {
                        pageNumber--;

                        monitor.PushMessage($"<color #ff0000>Download process interrupted due to a permanent error while downloading</color>\n { url }");
                        Logger.UpdaterLog($"Download process interrupted due to a permanent error while downloading { url }", Logger.Error);
                        monitor.Abort();
                        break;
                    }

                    int modsFoundThisPage = ReadModListingTextPage(catalog, statusWithText.Value, incompatibleMods: steamUrl.Contains("incompatible"), detectedModIds);
                        //ReadModListingPage(catalog, incompatibleMods: steamUrl.Contains("incompatible"));

                    if (modsFoundThisPage == 0)
                    {
                        pageNumber--;

                        if (pageNumber == 0)
                        {
                            Logger.UpdaterLog("Found no mods on page 1.");
                        }

                        // todo fix: it may break before reaching maxListingPage if no mods on page.
                        break;
                    }

                    totalMods += modsFoundThisPage;
                    monitor.PushMessage($"Found <color #00ff00>{modsFoundThisPage}</color>, total: <color #16a085>{totalMods}</color>");
                    Logger.UpdaterLog($"Found { modsFoundThisPage } mods on page { pageNumber }.");
                }

                totalPages += pageNumber;
            }

            // Toolkit.DeleteFile(ModSettings.TempDownloadFullPath);

            // Note: about 75% of the total time is downloading, the other 25% is processing.
            Logger.UpdaterLog($"Updater finished downloading { totalPages } Steam Workshop 'mod listing' pages and found { totalMods } mods.");
            
            monitor.ReportProgress(maxListingPages, $"Updater finished downloading <color #00ff00>{ totalPages }</color>, found: <color #00ff00>{totalMods}</color> mods");
            monitor.PushMessage($"Updater finished downloading <color #00ff00>{ totalPages }</color> 'mod listing' pages, found: <color #00ff00>{totalMods}</color> mods.");
            
            yield return new WaitForSeconds(2);
            onCompleted(totalMods > 0, detectedModIds);
        }
     

        /// <summary>Extracts Steam IDs and mod names for all mods from a downloaded mod listing page and adds/updates this in the catalog.</summary>
        /// <remarks>Sets the auto review date, (re)sets 'incompatible according to workshop' stability and removes unlisted and 'removed from workshop' statuses.</remarks>
        /// <returns>The number of mods found on this page.</returns>
        private static int ReadModListingTextPage(Catalog catalog, string page, bool incompatibleMods, List<ulong> detectedModIds)
        {
            int modsFoundThisPage = 0;
            string line;
            int index = 0;
            string[] lines = page.Split('\n');

            while (index < lines.Length)
            {
                line = lines[index++];
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

                detectedModIds.Add(steamID);
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
                    CatalogUpdater.UpdateMod(catalog, catalogMod, stability: Enums.Stability.IncompatibleAccordingToWorkshop, stabilityNote: new ElementWithId());
                }
                else if (!incompatibleMods && catalogMod.Stability == Enums.Stability.IncompatibleAccordingToWorkshop)
                {
                    CatalogUpdater.UpdateMod(catalog, catalogMod, stability: Enums.Stability.NotReviewed, stabilityNote: new ElementWithId());
                }

                // Update the name and the auto review date.
                CatalogUpdater.UpdateMod(catalog, catalogMod, modName);

                // Author info can be found on the next line, but skip it here and get it later on the mod page.
            }

            return modsFoundThisPage;
        }
        
        
        /// <summary>Downloads 'Map Theme listing' pages from the Steam Workshop to get Steam IDs for all available map themes.</summary>
        /// <remarks>The Steam IDs will be added to the list of Map Themes.</remarks>
        private static IEnumerator GetMapThemesWithProgress(Catalog catalog, ProgressMonitor progressMonitor)
        {
            Logger.UpdaterLog("Updater started downloading Steam Workshop 'map theme listing' pages. This should take less than 2 minutes.");

            string steamUrl = ModSettings.SteamMapThemesListingUrl;
            int pageNumber = 0;
            int newMapThemes = 0;

            int maxListingPages = GlobalConfig.Instance.UpdaterConfig.SteamMaxListingPages;
            
            progressMonitor.StartProgress(maxListingPages, $"Starting Map Themes download, num of pages {maxListingPages}", true);
            progressMonitor.UpdateStage(6, 8);
            progressMonitor.PushMessage($"Starting Map Themes download, number of pages <color #00ff00>{maxListingPages}</color>");
            
            // Download and read pages until we find no more map themes, or we reach a maximum number of pages, to avoid missing the mark and continuing for eternity.
            while (pageNumber < maxListingPages)
            {
                pageNumber++;
                int mapThemesFoundThisPage = 0;
                string url = $"{ steamUrl }&p={ pageNumber }";

                progressMonitor.ReportProgress(pageNumber, $"Downloading page {pageNumber} of {maxListingPages}");
                KeyValuePair<bool, string> stateWithText = new KeyValuePair<bool, string>();
                yield return Toolkit.DownloadPageText(url, result => stateWithText = result);
                
                if (!stateWithText.Key)
                {
                    Logger.UpdaterLog($"Download process interrupted due to a permanent error while downloading { url }", Logger.Error);

                    pageNumber--;
                    progressMonitor.Abort();
                    break;
                }

                string line;
                int index = 0;
                string[] lines = stateWithText.Value.Split('\n');

                while (index < lines.Length)
                {
                    line = lines[index++];
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

                if (mapThemesFoundThisPage == 0)
                {
                    pageNumber--;
                    break;
                }
            }

            Logger.UpdaterLog($"Updater finished downloading { pageNumber } Steam Workshop 'map themes listing' pages and found { newMapThemes } new map themes.");
            
            progressMonitor.PushMessage($"Finished downloading <color #00ff00>{pageNumber}</color> Steam Workshop 'map themes listing' pages and found <color #00ff00>{newMapThemes}</color> new map themes.");
            progressMonitor.ReportProgress(maxListingPages, $"Finished downloading <color #00ff00>{ pageNumber }</color> 'map themes listing' pages");
            yield return null;
        }
        

        /// <summary>Downloads individual mod pages from the Steam Workshop to get detailed mod information for all mods in the catalog.</summary>
        /// <remarks>Known unlisted mods are included. Removed mods are checked, to catch reappearing mods.</remarks>
        private static IEnumerator GetDetailsWithProgress(Catalog catalog, ProgressMonitor monitor, HashSet<ulong> detectedModIds, bool quick = false)
        {
            UpdaterConfig updaterConfig = GlobalConfig.Instance.UpdaterConfig;
            Stopwatch timer = Stopwatch.StartNew();
            
            DateTime yesterday = DateTime.Today.Subtract(TimeSpan.FromDays(1));
            List<Mod> catalogMods = quick ? catalog.Mods.Where(mod => mod.AutoReviewDate >= yesterday || detectedModIds.Contains(mod.SteamID)).ToList() : catalog.Mods;
            int numberOfMods = quick ? catalogMods.Count : catalog.Mods.Count - ModSettings.BuiltinMods.Count;
            
            long estimatedMilliseconds = updaterConfig.EstimatedMillisecondsPerModPage * numberOfMods;
    
            monitor.StartProgress(numberOfMods, $"Started downloading <color #00FF00>{ numberOfMods }</color> Workshop mod pages", true);
            monitor.UpdateStage(5, 8);
            monitor.PushMessage($"Started downloading <color #00ff00>{ numberOfMods }</color> Workshop mod pages.\n" +
                $"This should take about <color #00ff00>{ Toolkit.TimeString(estimatedMilliseconds) }</color>" +
                $" and should be ready around <color #00ff00>{ DateTime.Now.AddMilliseconds(estimatedMilliseconds + 30*1000):HH:mm}</color>.");
            
            Logger.UpdaterLog($"Updater started downloading { numberOfMods } individual Steam Workshop mod pages. This should take about " +
                $"{ Toolkit.TimeString(estimatedMilliseconds) } and should be ready around { DateTime.Now.AddMilliseconds(estimatedMilliseconds + 30*1000):HH:mm}.");
    
            int modsDownloaded = 0;
            int failedDownloads = 0;
            
            foreach (Mod catalogMod in catalogMods)
            {
                if (!catalog.IsValidID(catalogMod.SteamID, allowBuiltin: false))
                {
                    // Skip built-in mods.
                    continue;
                }

                KeyValuePair<bool, string> statusWithText = new KeyValuePair<bool, string>();
                yield return Toolkit.DownloadPageText(Toolkit.GetWorkshopUrl(catalogMod.SteamID), result => statusWithText = result);
                
                if (!statusWithText.Key)
                {
                    failedDownloads++;
    
                    Logger.UpdaterLog($"Permanent error while downloading Steam Workshop page for { catalogMod.ToString() }. Details not updated.", Logger.Error);
    
                    if (failedDownloads <= updaterConfig.SteamMaxFailedPages)
                    {
                        monitor.ReportProgress(modsDownloaded, $"Downloaded <color #00ff00>{ modsDownloaded }</color> of <color #16a085>{ numberOfMods }</color>, <color #ff0000>{ failedDownloads }</color> failed");
                        continue;
                    }
                    else
                    {
                        Logger.UpdaterLog("Download process stopped prematurely.", Logger.Error);
                        monitor.Abort();
                        break;
                    }
                }
    
                modsDownloaded++;
                monitor.ReportProgress(modsDownloaded, $"Downloaded <color #00ff00>{modsDownloaded}</color> of <color #16a085>{numberOfMods}</color>, <color #ff0000>{failedDownloads}</color> failed");
    
                if (modsDownloaded % 100 == 0)
                {
                    Logger.UpdaterLog($"{ modsDownloaded }/{ numberOfMods } mod pages downloaded.");
                }
    
                if (!ReadModPageText(catalog, catalogMod, statusWithText.Value))
                {
                    // Redownload and try one more time, to work around cut-off downloads.
                    monitor.PushMessage("<color #ffc107>Could not read mod page, retrying...</color>");
                    yield return Toolkit.DownloadPageText(Toolkit.GetWorkshopUrl(catalogMod.SteamID), result => statusWithText = result);
                    
                    if (!ReadModPageText(catalog, catalogMod, statusWithText.Value))
                    {
                        failedDownloads++;
                        monitor.PushMessage($"<color #ff0000>Mod info not updated for { catalogMod.ToString() }.</color>");
                        Logger.UpdaterLog($"Mod info not updated for { catalogMod.ToString() }.", Logger.Error);
                    }
                    else
                    {
                        Logger.UpdaterLog($"Steam page correctly read on retry for { catalogMod.ToString() }. Mod info updated.");
                    }
                }
            }
    
            // Note: about 90% of the total time is downloading, the other 10% is processing.
            Logger.UpdaterLog($"Updater finished downloading { modsDownloaded } individual Steam Workshop mod pages in " + 
                $"{ Toolkit.TimeString(timer.ElapsedMilliseconds, alwaysShowSeconds: true) }.");
            
            monitor.ReportProgress(numberOfMods, $"Finished downloading <color #00ff00>{ modsDownloaded }</color> Steam Workshop mod pages");
            monitor.PushMessage($"Updater finished downloading <color #00ff00>{ modsDownloaded }</color> individual Steam Workshop mod pages\n" +
                $"in { Toolkit.TimeString(timer.ElapsedMilliseconds, alwaysShowSeconds: true) }.");
        }


        /// <summary>Extracts detailed mod information from a downloaded mod page and updates the catalog.</summary>
        /// <returns>True if successful, false if there was an error with the mod page.</returns>
        public static bool ReadModPageText(Catalog catalog, Mod catalogMod, string pageText)
        {
            try
            {
                List<Enums.Dlc> RequiredDlcsToRemove = new List<Enums.Dlc>(catalogMod.RequiredDlcs);
                bool steamIDmatched = false;
                string line;
                int lineIndex = 0;
                string[] lines = pageText.Split('\n');

                while (lineIndex++ < lines.Length)
                {
                    line = lines[lineIndex];

                    if (!steamIDmatched)
                    {
                        steamIDmatched = line.Contains($"{ModSettings.SearchSteamID}{catalogMod.SteamID}");

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
                                Logger.UpdaterLog($"We found this mod, but can't read the Steam page for {catalogMod.ToString()}.", Logger.Warning);
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

                    if (line.Contains(ModSettings.SearchServerError) || line.Contains(ModSettings.SearchSomethingWentWrongError))
                    {
                        File.WriteAllText(Path.Combine(ModSettings.UpdaterPath, $"{catalogMod.SteamID} - communication error.html"), pageText);

                        Logger.UpdaterLog($"Server communication problem while accessing mod page {catalogMod.ToString()}. Downloaded page saved.", Logger.Warning);
                        return false;
                    }

                    // Author Steam ID, Custom URL and author name.
                    if (line.Contains(ModSettings.SearchAuthorLeft))
                    {
                        // Only get the author URL if the author ID was not found, to prevent updating the author URL to an empty string.
                        ulong authorID = Toolkit.ConvertToUlong(Toolkit.MidString(line, $"{ModSettings.SearchAuthorLeft}profiles/", ModSettings.SearchAuthorMid));
                        string authorUrl = authorID != 0 ? null : Toolkit.MidString(line, $"{ModSettings.SearchAuthorLeft}id/", ModSettings.SearchAuthorMid);

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
                                Logger.UpdaterLog($"Author found without a name: {catalogAuthor.ToString()}.", Logger.Error);
                            }

                            // Don't update the name to an empty string.
                            authorName = null;
                        }
                        else if (authorName == authorID.ToString() && authorID != 0 && (authorName != catalogAuthor.Name || catalogAuthor.AddedThisSession) &&
                            !catalog.SuppressedWarnings.Contains(authorID))
                        {
                            // An author name equal to the author ID is a common Steam error, although some authors really have their ID as name (ofcourse they do).
                            if (string.IsNullOrEmpty(catalogAuthor.Name) || catalogAuthor.AddedThisSession)
                            {
                                Logger.UpdaterLog($"Author found with Steam ID as name: {authorName}. Some authors do this, but it could be a Steam error.",
                                    Logger.Warning);
                            }
                            else
                            {
                                Logger.UpdaterLog($"Author found with Steam ID as name: {authorName}. Old name still used: {catalogAuthor.Name}.");
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
                            Logger.UpdaterLog($"Mod name not found for {catalogMod.SteamID}. This could be an actual unnamed mod, or a Steam error.", Logger.Warning);
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
                        lineIndex += 2;
                        line = lines[lineIndex];
                        // line = reader.ReadLine();
                        // line = reader.ReadLine();
                        DateTime published = Toolkit.ConvertWorkshopDateTime(Toolkit.MidString(line, ModSettings.SearchDatesLeft, ModSettings.SearchDatesRight));

                        line = lines[++lineIndex];
                        // line = reader.ReadLine();
                        DateTime updated = Toolkit.ConvertWorkshopDateTime(Toolkit.MidString(line, ModSettings.SearchDatesLeft, ModSettings.SearchDatesRight));

                        CatalogUpdater.UpdateMod(catalog, catalogMod, published: published, updated: updated);
                    }

                    // Required DLC. This line can be found multiple times.
                    else if (line.Contains(ModSettings.SearchRequiredDlc))
                    {
                        line = lines[++lineIndex];
                        // line = reader.ReadLine();
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
                            line = lines[++lineIndex];
                            // line = reader.ReadLine();
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
                            lineIndex += 3;
                            line = lines[lineIndex];

                            // line = reader.ReadLine();
                            // line = reader.ReadLine();
                            // line = reader.ReadLine();
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
                        line = lines[lineIndex];
                        // We can't search for the right part, because it might exist inside the description.
                        StringBuilder descriptionSB = new StringBuilder(Toolkit.MidString($"{line}\n", ModSettings.SearchDescriptionLeft, "\n"));

                        // Usually, the complete description is on one line. However, it might be split when using certain bbcode. Combine up to 30 lines.
                        for (var i = 1; i <= 30; i++)
                        {
                            line = lines[++lineIndex];
                            // line = reader.ReadLine();

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

                if (!steamIDmatched && !catalogMod.Statuses.Contains(Enums.Status.RemovedFromWorkshop))
                {
                    // We didn't find a Steam ID on the page, but no error page either. Must be a download issue or another Steam error.
                    File.WriteAllText(Path.Combine(ModSettings.UpdaterPath, $"{catalogMod.SteamID} - error.html"), pageText);

                    Logger.UpdaterLog($"Can't find the Steam ID on downloaded page for {catalogMod.ToString()}. Downloaded page saved.", Logger.Warning);
                    return false;
                }

                foreach (Enums.Dlc oldRequiredDlc in RequiredDlcsToRemove)
                {
                    CatalogUpdater.RemoveRequiredDlc(catalog, catalogMod, oldRequiredDlc);
                }

                return true;
            }
            catch (Exception ex) { Logger.Exception(ex); }

            return false;
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
