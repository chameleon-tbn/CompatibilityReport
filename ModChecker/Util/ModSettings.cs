using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ColossalFramework.IO;
using ModChecker.DataTypes;


/// Paths on Windows:
///   DataLocation.applicationBase      = ...\Steam Games\steamapps\common\Cities_Skylines
///   Application.dataPath              = .../Steam Games/steamapps/common/Cities_Skylines/Cities_Data
///   DataLocation.gameContentPath      = ...\Steam Games\steamapps\common\Cities_Skylines\Files
///   DataLocation.localApplicationData = %LocalAppData%\Colossal Order\Cities_Skylines                     // contains Windows username, clean with Util.Tools.PrivacyPath
///   DataLocation.modsPath             = %localappdata%\Colossal Order\Cities_Skylines\Addons\Mods         // contains Windows username, clean with Util.Tools.PrivacyPath
///   DataLocation.assemblyDirectory    = ...\Steam Games\steamapps\workshop\content\255710\<mod-steamid>
///                                     = exception (Invalid Path) for local mods
///   DataLocation.currentDirectory     = DataLocation.applicationBase

/// .NET 3.5 now supports TLS 1.2 after a MS patch, but only with registry edits which we can't rely on for mod users. 
///   So for any download location we either need an 'unsafe' webserver that still support TLS 1.1 or worse, or a HTTP only site. Both are getting more rare.
///   Or switch to .NET 4.5+, see https://blogs.perficient.com/2016/04/28/tsl-1-2-and-net-support/
///   Sites with TLS 1.1 and 1.2 : Steam Workshop, Google Drive
///   Sites with only TLS 1.2+   : GitHub


namespace ModChecker.Util
{
    internal static class ModSettings
    {
        // Constructor; don't use Logger here because that needs ModSettings
        static ModSettings()
        {
            // Get the mod path for the bundled catalog
            try
            {
                // This will work if we are a Steam Workshop mod, otherwise it throws an exception
                bundledCatalogFullPath = Path.Combine(DataLocation.assemblyDirectory, $"{ internalName }Catalog.xml");
            }
            catch
            {
                try
                {
                    // Get the mod path again, now for if we are a local mod
                    bundledCatalogFullPath = Path.Combine(Path.Combine(DataLocation.modsPath, internalName), $"{ internalName }Catalog.xml");
                }
                catch
                {
                    // Weirdly, we couldn't get the mod path; just set it to the game folder so we have a valid path
                    bundledCatalogFullPath = Path.Combine(DataLocation.applicationBase, $"{ internalName }Catalog.xml");
                }

            }
        }


        /// Hardcoded settings that can't be changed by users

        // The version of this mod, split and combined; used in AssemblyInfo, must be a constant
        internal const string shortVersion = "0.2";
        internal const string revision = "0";
        internal const string build = "114";
        internal const string version = shortVersion + "." + revision;
        internal const string fullVersion = version + "." + build;

        // Release type: alpha, beta, test or "" (production); used in AssemblyInfo, must be a constant
        internal const string releaseType = "alpha";

        // The game version this mod is updated for; the catalog overrules this
        internal static readonly Version compatibleGameVersion = GameVersion.Patch_1_13_3_f9;

        // Mod names, shown in the report from this mod and in the game Options window and Content Manager; used in AssemblyInfo, must be a constant
        internal const string modName = "Mod Checker";                                                      // used in report filename, reporting and logging
        internal const string displayName = modName + " " + version + " " + releaseType;                    // used in game options, Content Manager and AssemblyInfo
        internal const string internalName = "ModChecker";                                                  // used in filenames, xmlRoot and game log

        // Mod description, shown in Content Manager; used in AssemblyInfo, must be a constant
        internal const string modDescription = "Checks your subscribed mods for compatibility. Version " + version + " " + releaseType;

        // Author name; used in AssemblyInfo; used in AssemblyInfo, must be a constant
        internal const string modAuthor = "Finwickle";

        // The XML root of the Catalog; must be constant
        internal const string xmlRoot = internalName + "Catalog";

        // This mods own Steam ID; [Todo 0.4]
        internal static readonly ulong modCheckerSteamID = 101;

        // The current catalog structure version
        internal static readonly uint currentCatalogStructureVersion = 1;

        // Builtin mod fake IDs, keyed by name. These IDs are always the same, so they can be used for mod compatibility.
        internal static Dictionary<string, ulong> BuiltinMods { get; } = new Dictionary<string, ulong>
        {
            { "Hard Mode", 1 },
            { "Unlimited Money", 2 },
            { "Unlimited Oil And Ore", 3 },
            { "Unlimited Soil", 4 },
            { "Unlock All", 5 }
        };

        // Lowest and highest fake Steam ID to use; should not overlap, be higher than BuiltinMods above and lower than real Steam IDs; mod group IDs are used in catalog
        internal static readonly ulong lowestUnknownBuiltinModID  = 11;
        internal static readonly ulong highestUnknownBuiltinModID = 99;
        internal static readonly ulong lowestLocalModID          = 101;
        internal static readonly ulong highestLocalModID        = 9999;
        internal static readonly ulong lowestModGroupID        = 10001;
        internal static readonly ulong highestModGroupID      = 999999;
        internal static readonly ulong highestFakeID = Math.Max(Math.Max(highestUnknownBuiltinModID, highestLocalModID), highestModGroupID);
        
        // Logfile location (Cities_Data)
        internal static readonly string logfileFullPath = Path.Combine(Application.dataPath, $"{ internalName }.log");

        // Report filename (with spaces), without path
        internal static readonly string reportTextFileName = $"{ modName } Report.txt";
        internal static readonly string reportHtmlFileName = $"{ modName } Report.html";

        // Bundled Catalog location: in the same location as the mod itself (set in constructor)
        internal static readonly string bundledCatalogFullPath;

        // Downloaded catalog url
        internal static readonly string catalogURL = "https://drive.google.com/uc?export=download&id=1oUT2U_PhLfW-KGWOyShHL2GvU6kyE4a2";

        // Downloaded Catalog local location
        internal static readonly string downloadedCatalogFullPath = Path.Combine(DataLocation.localApplicationData, $"{ internalName }Catalog.xml");

        // Number of retries on failed downloads; used as default parameter, must be a constant
        internal const uint downloadRetries = 2;

        // 'Please report' text to include in logs when something odd happens
        internal static readonly string pleaseReportText = $"Please report this on the Workshop page for { modName }: { Toolkit.GetWorkshopURL(modCheckerSteamID) } ";

        // Max width of the text report
        internal static readonly int maxReportWidth = 90;

        // Separators used in the logfile and text report
        internal static readonly string separator = new string('-', maxReportWidth);
        internal static readonly string separatorDouble = new string('=', maxReportWidth);

        // Separator to use in logfiles when appending
        internal static readonly string sessionSeparator = "\n\n" + separatorDouble + "\n\n";

        // Bullets used in the text report
        internal static readonly string bullet = " - ";
        internal static readonly string noBullet = new string(' ', bullet.Length);
        internal static readonly string bullet2 = noBullet + "  * ";
        internal static readonly string noBullet2 = new string(' ', bullet2.Length);
        internal static readonly string bullet3 = noBullet2 + "  - ";


        /// Hardcoded defaults for data that comes from the catalog

        // Default text report intro and footer
        internal static readonly string defaultIntroText =
                       "Basic information about mods:\n" +
            bullet + "Always exit to desktop before loading another save! (no 'Second Loading')\n" +
            bullet + "Never (un)subscribe to anything while the game is running! This resets some mods.\n" +
            bullet + "Always unsubscribe mods you're not using. Disabling isn't always good enough.\n" +
            bullet + "Mods not updated for a while might still work fine. Check their Workshop page.\n" +
            bullet + "Savegame not loading? Use the optimization and safe mode options from Loading Screen:\n" +
            noBullet + Toolkit.GetWorkshopURL(667342976) + "\n" +
            "\n" +
                       "Some remarks about incompatibilities:\n" +
            bullet + "Mods that do the same thing are generally incompatible with each other.\n" +
            bullet + "Some issues are a conflict between more than two mods or a loading order issue,\n" +
            noBullet + "making it hard to find the real culprit. This can lead to users blaming the\n" +
            noBullet + "wrong mod for an error. Don't believe everything you read about mod conflicts.\n" +
            "\n" +
                       "Disclaimer:\n" +
            bullet + "We try to include only facts about incompatibilities and highly value the words of\n" +
            noBullet + "mod creators in this. However, we will occasionally get it wrong or miss an update.\n" +
            bullet + "Found a mistake? Please comment on the Workshop, especially as the creator of a mod.";

        internal static readonly string defaultFooterText =
            "Did this help? Do you miss anything? Leave a rating/comment at the workshop page.\n" +
            Toolkit.GetWorkshopURL(modCheckerSteamID);

        // Default HTML report intro and footer; [Todo 0.6]
        internal static readonly string defaultIntroHtml = "";

        internal static readonly string defaultFooterHtml = "";

        // Catalog notes for the first few catalogs
        internal static readonly string firstCatalogNote  = "This first catalog only contains the builtin mods.";
        internal static readonly string secondCatalogNote = "This second catalog only contains some basic info for all mods.";
        internal static readonly string thirdCatalogNote  = "This third catalog is the first one with all basic info about mods. No reviews yet.";


        /// Defaults for settings that will be available to users through mod options within the game [Todo 0.7]

        // Sort report by Name or Steam ID
        internal static bool ReportSortByName { get; private set; } = true;

        // Which report(s) to create; will create text report if none; [Todo 0.6] Set HTML to true
        internal static bool HtmlReport { get; private set; } = false;
        internal static bool TextReport { get; private set; } = true | !HtmlReport;

        // Report path (game folder); filename is not user-changeable and is set in another variable
        internal static string ReportPath { get; private set; } = DataLocation.applicationBase;

        // Report location, generated from the path and filename
        internal static string ReportTextFullPath { get; private set; } = Path.Combine(ReportPath, reportTextFileName);
        internal static string ReportHtmlFullPath { get; private set; } = Path.Combine(ReportPath, reportHtmlFileName);

        // Run the scanner before the main menu or later during map loading
        internal static bool ScanBeforeMainMenu { get; private set; } = true;

        // Allow on-demand scanning; this will increase memory usage because the catalog stays loaded
        internal static bool AllowOnDemandScanning { get; private set; } = false;



        /// Defaults for settings that will be available in a settings xml file [Todo 0.7]

        // Debug mode; this enables debug logging and logfile append
        internal static bool DebugMode { get; private set; } = true;

        // Append (default) or overwrite the log file for normal mode; always append for debug mode
        internal static bool LogAppend { get; private set; } = false || DebugMode;

        // Maximum log file size before starting with new log file; only applicable when appending; default is 100 KB
        internal static long LogMaxSize { get; private set; } = 100 * 1024;



        /// Hardcoded updater settings

        // Updater path
        internal static readonly string updaterPath = Path.Combine(DataLocation.localApplicationData, $"{ internalName }Updater");

        // Updater settings file
        internal static readonly string updaterSettingsXml = Path.Combine(updaterPath, $"{ internalName }_UpdaterSettings.xml");

        // Updater logfile location (Cities_Data)
        internal static readonly string updaterLogfileFullPath = Path.Combine(updaterPath, $"{ internalName }_Updater.log");

        // Temporary download location for Steam Workshop pages
        internal static readonly string steamDownloadedPageFullPath = Path.Combine(updaterPath, $"{ internalName }_Download.tmp");

        // Max. number of Steam Workshop mod listing pages to download (per category), to avoid downloading for eternity; should be high enough to include all pages
        internal static readonly uint steamMaxModListingPages = 200;

        // Steam Workshop mod listing urls, without page number ("&p=1")
        internal static readonly List<string> steamModListingURLs = new List<string> {
            "https://steamcommunity.com/workshop/browse/?appid=255710&browsesort=mostrecent&requiredtags[]=Mod",
            "https://steamcommunity.com/workshop/browse/?appid=255710&browsesort=mostrecent&requiredtags[]=Cinematic+Cameras",
            "https://steamcommunity.com/workshop/browse/?appid=255710&browsesort=mostrecent&requiredtags[]=Mod&requiredflags[]=incompatible",
            "https://steamcommunity.com/workshop/browse/?appid=255710&browsesort=mostrecent&requiredtags[]=Cinematic+Cameras&requiredflags[]=incompatible" };

        // Search string for Mod info in mod Listing files
        internal static readonly string steamModListingModFind = "<div class=\"workshopItemTitle ellipsis\">";
        
        // Search strings for mod and author info in the mod listing files
        internal static readonly string steamModListingModIDLeft = "steamcommunity.com/sharedfiles/filedetails/?id=";
        internal static readonly string steamModListingModIDRight = "&searchtext";
        internal static readonly string steamModListingModNameLeft = "workshopItemTitle ellipsis\">";
        internal static readonly string steamModListingModNameRight = "</div>";
        internal static readonly string steamModListingAuthorURLLeft = "steamcommunity.com/id/";
        internal static readonly string steamModListingAuthorIDLeft = "steamcommunity.com/profiles/";
        internal static readonly string steamModListingAuthorRight = "/myworkshopfiles";
        internal static readonly string steamModListingAuthorNameLeft = "/myworkshopfiles/?appid=255710\">";
        internal static readonly string steamModListingAuthorNameRight = "</a>";

        // Search strings for individual mod pages
        internal static readonly string steamModPageSteamID = "var publishedfileid = '";                                                    // Followed by the Steam ID
        internal static readonly string steamModPageAuthorFind = "&gt;&nbsp;<a href=\"https://steamcommunity.com/";                         // Followed by 'id' or 'profiles'
        internal static readonly string steamModPageAuthorMid = "/myworkshopfiles/?appid=255710\">";                                        // Sits between ID/URL and name
        internal static readonly string steamModPageAuthorRight = "'s Workshop</a>";
        internal static readonly string steamModPageNameLeft = "<div class=\"workshopItemTitle\">";
        internal static readonly string steamModPageNameRight = "</div>";
        internal static readonly string steamModPageVersionTagFind = "<span class=\"workshopTagsTitle\">Tags:";                             // Can appear multiple times
        internal static readonly string steamModPageVersionTagLeft = "-compatible\">";
        internal static readonly string steamModPageVersionTagRight = "-compatible";
        internal static readonly string steamModPageDatesFind = "<div class=\"detailsStatsContainerRight\">";
        internal static readonly string steamModPageDatesLeft = "detailsStatRight\">";                                                      // Two/three lines below Find
        internal static readonly string steamModPageDatesRight = "</div>";
        internal static readonly string steamModPageRequiredDLCFind = "<div class=\"requiredDLCItem\">";                                    // Can appear multiple times
        internal static readonly string steamModPageRequiredDLCLeft = "https://store.steampowered.com/app/";                                // One line below Find text
        internal static readonly string steamModPageRequiredDLCRight = "\">";
        internal static readonly string steamModPageRequiredModFind = "<div class=\"requiredItemsContainer\" id=\"RequiredItems\">";        // Can appear multiple times
        internal static readonly string steamModPageRequiredModLeft = "https://steamcommunity.com/workshop/filedetails/?id=";               // One line below Find text
        internal static readonly string steamModPageRequiredModRight = "\" ";
        internal static readonly string steamModPageDescriptionFind = "<div class=\"workshopItemDescriptionTitle\">Description</div>";
        internal static readonly string steamModPageDescriptionLeft = "workshopItemDescription\" id=\"highlightContent\">";                 // One line below Find text
        internal static readonly string steamModPageDescriptionRight = "</div>";
        internal static readonly string steamModPageSourceURLLeft = "https://steamcommunity.com/linkfilter/?url=https://github.com/";       // Only GitHub is recognized
        internal static readonly string steamModPageSourceURLRight = "\" ";

        // Steam IDs of assets to ignore as required items; [Todo 0.3] Move to catalog
        internal static readonly List<ulong> requiredIDsToIgnore = new List<ulong> { 1145223801, 2228643473, 1083162158, 1787627069, 2088550283, 2071058348, 
            2071057587, 2013521990, 1866391549, 2128887855, 1568009578, 1779369015, 2008960441, 1899911682, 678303408, 1544460160, 1386088603, 816325876,
            698391006, 697218602, 597034783, 574281911, 548154065, 562337133, 545841631, 452680862, 482681213, 484657860, 489046338, 413776855, 2442974192};



        /// Defaults for updater settings that will be available in an updater settings xml file [Todo 0.7]

        // Updater enabled? Updater will only be enabled if the updater settings xml file exists
        internal static bool UpdaterEnabled { get; private set; } = true && File.Exists(updaterSettingsXml);

        // Max. number of individual mod pages to download for known mods, to limit the time spend
        internal const uint SteamMaxKnownModDownloads = 100;

        // Max. number of failed downloads for individual pages before giving up altogether
        internal static uint SteamMaxFailedPages { get; private set; } = 4;
    }
}
