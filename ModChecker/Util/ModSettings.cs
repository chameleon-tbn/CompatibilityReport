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
///   Sites with TLS 1.1 and 1.2: Steamcommunity, Imgur.com, Wordpress.com
///   Sites with only TLS 1.2+  : GitHub, Surfdrive


namespace ModChecker.Util
{
    internal static class ModSettings
    {
        // Constructor; don't use Logger here because that one needs ModSettings
        static ModSettings()
        {
            try
            {
                // Get the mod path for the bundled catalog
                BundledCatalogFullPath = Path.Combine(DataLocation.assemblyDirectory, $"{ internalName }Catalog.xml");                          // Steam Workshop mod
            }
            catch
            {
                try
                {
                    BundledCatalogFullPath = Path.Combine(Path.Combine(DataLocation.modsPath, internalName), $"{ internalName }Catalog.xml");   // Local mod
                }
                catch
                {
                    BundledCatalogFullPath = "";
                }
                
            }

            try
            {
                // Create the directory for updated catalogs
                Directory.CreateDirectory(UpdatedCatalogPath);
            }
            catch
            {
                // Ignore
            }
        }


        /// Hardcoded settings that can't be changed by users

        // The version of this mod, split and combined; used in AssemblyInfo, must be a constant
        internal const string shortVersion = "0.2";
        internal const string revision = "0";
        internal const string build = "89";
        internal const string version = shortVersion + "." + revision + "." + build;

        // Release type: alpha, beta, test or "" (production); used in AssemblyInfo, must be a constant
        internal const string releaseType = "alpha";

        // Mod names, shown in the report from this mod and in the game Options window and Content Manager; used in AssemblyInfo, must be a constant
        internal const string modName = "Mod Checker";                                                         // used in report filename, reporting and logging
        internal const string displayName = modName + " " + shortVersion + " " + releaseType;                  // used in game options, Content Manager and AssemblyInfo
        internal const string internalName = "ModChecker";                                                  // used in filenames, xmlRoot and game log

        // Mod description, shown in Content Manager; used in AssemblyInfo, must be a constant
        internal const string modDescription = "Checks your subscribed mods for compatibility. Version " + version + " " + releaseType;

        // Author name; used in AssemblyInfo; used in AssemblyInfo, must be a constant
        internal const string modAuthor = "Finwickle";

        // The XML root of the Catalog; must be constant
        internal const string xmlRoot = internalName + "Catalog";

        // This mods own Steam ID
        internal static readonly ulong ModCheckerSteamID = 101;                                                       // Unfinished

        // The current catalog structure version
        internal static readonly uint CurrentCatalogStructureVersion = 1;

        // Logfile location
        internal static readonly string LogfileFullPath = Path.Combine(Application.dataPath, $"{ internalName }.log");

        // Updater logfile location
        internal static readonly string UpdaterLogfileFullPath = Path.Combine(Application.dataPath, $"{ internalName }Updater.log");

        // Report filename, without path
        internal static readonly string ReportTextFileName = $"{ modName } Report.txt";
        internal static readonly string ReportHtmlFileName = $"{ modName } Report.html";

        // Downloaded catalog url
        internal static readonly string CatalogURL = "https://surfdrive.surf.nl/files/index.php/s/OwBdunIj4BDc8Jb/download";

        // Downloaded Catalog local location
        internal static readonly string DownloadedCatalogFullPath = Path.Combine(DataLocation.localApplicationData, $"{ internalName }Catalog.xml");

        // Bundled Catalog location: in the same location as the mod itself
        internal static readonly string BundledCatalogFullPath;                                             // Set in constructor

        // Number of retries on failed downloads; must be a constant
        internal const uint downloadRetries = 1;

        // 'Please report' text to include in logs when something odd happens
        internal static readonly string PleaseReportText = $"Please report this on the Workshop page for { modName }: { Tools.GetWorkshopURL(ModCheckerSteamID) } ";

        // Max width of the TXT report: 
        internal static readonly int MaxReportWidth = 89;

        // Separators used in the logfile and TXT report
        internal static readonly string separator       = new string('-', MaxReportWidth + 1);
        internal static readonly string separatorDouble = new string('=', MaxReportWidth + 1);

        // Separator to use in logfiles when appending
        internal static readonly string sessionSeparator = "\n\n" + separatorDouble + "\n\n";

        // Bullets used in the TXT report:
        internal static readonly string Bullet    = " - ";
        internal static readonly string NoBullet  = new string(' ', Bullet.Length);
        internal static readonly string Bullet2   = NoBullet  + "  * ";
        internal static readonly string NoBullet2 = new string(' ', Bullet2.Length);
        internal static readonly string Bullet3   = NoBullet2 + "  - ";

        // Builtin mod fake IDs, keyed by name. These IDs are always the same, so they can be used for mod compatibility.
        internal static Dictionary<string, ulong> BuiltinMods { get; } = new Dictionary<string, ulong>
        {
            { "Hard Mode", 1 },
            { "Unlimited Money", 2 },
            { "Unlimited Oil And Ore", 3 },
            { "Unlimited Soil", 4 },
            { "Unlock All", 5 }
        };

        // Lowest and highest fake Steam ID for unknown builtin mods, local mods and mod groups
        // These should be in this order, not overlap, be higher than above BuiltinMods IDs, and all be lower than real Steam IDs
        internal static readonly ulong lowestUnknownBuiltinModID  = 11;
        internal static readonly ulong highestUnknownBuiltinModID = 99;
        internal static readonly ulong lowestLocalModID          = 101;
        internal static readonly ulong highestLocalModID        = 9999;
        internal static readonly ulong lowestModGroupID        = 10001;
        internal static readonly ulong highestModGroupID      = 999999;
        internal static readonly ulong HighestFakeID = Math.Max(Math.Max(highestUnknownBuiltinModID, highestLocalModID), highestModGroupID);


        /// Defaults for settings that come from the catalog

        // The game version this mod is updated for; the catalog should overrule this
        internal static readonly Version CompatibleGameVersion = GameVersion.Patch_1_13_1_f1;

        // Default text report intro and footer
        internal static readonly string DefaultIntroText =
                       "Basic information about mods:\n" +
            Bullet +   "Always exit to desktop before loading another save! (no 'Second Loading')\n" +
            Bullet +   "Never (un)subscribe to anything while the game is running! This resets some mods.\n" +
            Bullet +   "Always unsubscribe mods you're not using. Disabling isn't always good enough.\n" +
            Bullet +   "Mods not updated for a while might still work fine. Check their Workshop page.\n" +
            Bullet +   "Savegame not loading? Use the optimization and safe mode options from Loading Screen:\n" +
            NoBullet + Tools.GetWorkshopURL(667342976) + "\n" +
            "\n" +
                       "Some remarks about incompatibilities:\n" +
            Bullet +   "Mods that do the same thing are generally incompatible with each other.\n" +
            Bullet +   "Some issues are a conflict between more than two mods or a loading order issue,\n" +
            NoBullet + "making it hard to find the real culprit. This can lead to users blaming the\n" +
            NoBullet + "wrong mod for an error. Don't believe everything you read about mod conflicts.\n" +
            "\n" +
                       "Disclaimer:\n" +
            Bullet +   "We try to include only facts about incompatibilities and highly value the words of\n" +
            NoBullet + "mod creators in this. However, we will occasionally get it wrong or miss an update.\n" +
            Bullet +   "Found a mistake? Please comment on the Workshop, especially as the creator of a mod.";

        internal static readonly string DefaultFooterText =
            "Did this help? Do you miss anything? Leave a rating/comment at the workshop page.\n" + 
            Tools.GetWorkshopURL(ModCheckerSteamID);

        // Default HTML report intro and footer
        internal static readonly string DefaultIntroHtml = "";                                              // Unfinished

        internal static readonly string DefaultFooterHtml = "";                                             // Unfinished



        /// Settings that will be available to users through mod options

        // Sort report by Name or Steam ID
        internal static bool ReportSortByName { get; private set; } = true;

        // Report type; can be both, will create Text report if none
        internal static bool TextReport { get; private set; } = true;
        internal static bool HtmlReport { get; private set; } = true;

        // Report path; filename is not changeable and is set in another variable
        internal static string ReportPath { get; private set; } = DataLocation.applicationBase;

        // Report location, generated from the path and filename
        internal static string ReportTextFullPath { get; private set; } = Path.Combine(ReportPath, ReportTextFileName);
        internal static string ReportHtmlFullPath { get; private set; } = Path.Combine(ReportPath, ReportHtmlFileName);



        /// Settings that will be available in a settings xml file

        // Debug mode; this enables debug logging and logfile append
        internal static bool DebugMode { get; private set; } = true;

        // Append (default) or overwrite the log file for normal mode; always append for debug mode
        internal static bool LogAppend { get; private set; } = true || DebugMode;

        // Maximum log file size before starting with new log file; only applicable when appending
        internal static long LogMaxSize { get; private set; } = 100 * 1024;                                 // 100 KB

        // Which scenes to run the scanner: "IntroScreen" and/or "Game"
        internal static List<string> ScannerScenes { get; private set; } = new List<string>() { "IntroScreen" };



        /// Settings for the updater only

        // Updated catalog location
        internal static readonly string UpdatedCatalogPath = Path.Combine(DataLocation.localApplicationData, "ModCheckerCatalogs");

        // Temporary download location for Steam Workshop pages
        internal static readonly string SteamWebpageFullPath = Path.Combine(DataLocation.localApplicationData, $"{ internalName }SteamPage.html");

        // Max. number of Steam Workshop pages to download, to limit the time spend and to avoid downloading for eternity
        internal static readonly uint SteamMaxModListingPages = 100;
        internal static readonly uint SteamMaxNewModDownloads = 10;     // Unfinished: should be 100
        internal static readonly uint SteamMaxKnownModDownloads = 10;   // Unfinished: should be 100

        // Max. number of download errors before giving up
        internal static readonly uint SteamDownloadRetries = 3;

        // Delay between downloading individual mod pages, to avoid being marked suspicious by Steam or their CDN; not used for mod listing pages
        internal static readonly uint SteamDownloadDelayInMilliseconds = 250;                               // Unfinished: do we need this?

        // Steam Workshop mod listing url without page number ("&p=1")
        internal static readonly string SteamModsListingURL =
            "https://steamcommunity.com/workshop/browse/?appid=255710&browsesort=mostrecent&requiredtags[]=Mod";

        internal static readonly string SteamIncompatibleModsURL =
            "https://steamcommunity.com/workshop/browse/?appid=255710&browsesort=mostrecent&requiredtags[]=Mod&requiredflags[]=incompatible";

        // Search string for Mod info in mod Listing files
        internal static readonly string SteamModListingModFind = "<div class=\"workshopItemTitle ellipsis\">";

        // Recognize a mod listing page without mods
        internal static readonly string SteamModListingNoMoreFind = "No items matching your search criteria were found.";

        // Search strings for mod and author info in the mod listing files
        internal static readonly string SteamModListingModIDLeft = "steamcommunity.com/sharedfiles/filedetails/?id=";
        internal static readonly string SteamModListingModIDRight = "&searchtext";
        internal static readonly string SteamModListingModNameLeft = "workshopItemTitle ellipsis\">";
        internal static readonly string SteamModListingModNameRight = "</div>";
        internal static readonly string SteamModListingAuthorIDLeft = "steamcommunity.com/id/";
        internal static readonly string SteamModListingAuthorProfileLeft = "steamcommunity.com/profiles/";
        internal static readonly string SteamModListingAuthorIDRight = "/myworkshopfiles";
        internal static readonly string SteamModListingAuthorNameLeft = "/myworkshopfiles/?appid=255710\">";
        internal static readonly string SteamModListingAuthorNameRight = "</a>";

        // Search strings for individual mod pages
        internal static readonly string SteamModPageSteamID = "<span onclick=\"VoteUp(";                                                    // Followed by the Steam ID
        internal static readonly string SteamModPageVersionTagFind = "<span class=\"workshopTagsTitle\">Tags:";                             // Can appear multiple times
        internal static readonly string SteamModPageVersionTagLeft = "-compatible\" >";
        internal static readonly string SteamModPageVersionTagRight = "-compatible";
        internal static readonly string SteamModPageDatesFind = "<div class=\"detailsStatsContainerRight\">";
        internal static readonly string SteamModPageDatesLeft = "detailsStatRight\">";                      // Two lines below Find text for published, three lines for updated
        internal static readonly string SteamModPageDatesRight = "</div>";                                  // Published should be found, updated not
        internal static readonly string SteamModPageRequiredDLCFind = "<div class=\"requiredDLCItem\">";                                    // Can appear multiple times
        internal static readonly string SteamModPageRequiredDLCLeft = "https://store.steampowered.com/app/";                                // One line below Find text
        internal static readonly string SteamModPageRequiredDLCRight = "\">";
        internal static readonly string SteamModPageRequiredModFind = "<div class=\"requiredItemsContainer\" id=\"RequiredItems\">";        // Can appear multiple times
        internal static readonly string SteamModPageRequiredModLeft = "https://steamcommunity.com/workshop/filedetails/?id=";               // One line below Find text
        internal static readonly string SteamModPageRequiredModRight = "\" ";
        internal static readonly string SteamModPageDescriptionFind = "<div class=\"workshopItemDescriptionTitle\">Description</div>";
        internal static readonly string SteamModPageDescriptionLeft = "workshopItemDescription\" id=\"highlightContent\">";                 // One line below Find text
        internal static readonly string SteamModPageDescriptionRight = "</div>";
        internal static readonly string SteamModPageSourceURLLeft = "https://steamcommunity.com/linkfilter/?url=https://github.com/";       // Only GitHub is recognized
        internal static readonly string SteamModPageSourceURLRight = "\" ";
    }
}
