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
        }


        /// Hardcoded settings that can't be changed by users

        // The version of this mod, split and combined; used in AssemblyInfo, must be a constant
        internal const string shortVersion = "0.2";
        internal const string revision = "0";
        internal const string build = "88";
        internal const string version = shortVersion + "." + revision + "." + build;

        // Release type: alpha, beta, test or "" (production); used in AssemblyInfo, must be a constant
        internal const string releaseType = "alpha";

        // Mod names, shown in the report from this mod and in the game Options window and Content Manager; used in AssemblyInfo, must be a constant
        internal const string name = "Mod Checker";                                             // used in report filename, reporting and logging
        internal const string displayName = name + " " + shortVersion + " " + releaseType;      // used in game options, Content Manager and AssemblyInfo
        internal const string internalName = "ModChecker";                                      // used in filenames, xmlRoot and game log

        // Mod description, shown in Content Manager; used in AssemblyInfo, must be a constant
        internal const string description = "Checks your subscribed mods for compatibility. Version " + version + " " + releaseType;

        // Author name; used in AssemblyInfo; used in AssemblyInfo, must be a constant
        internal const string author = "Finwickle";

        // The XML root of the Catalog; must be constant
        internal const string xmlRoot = internalName + "Catalog";

        // This mods own Steam ID
        internal static readonly ulong SteamID = 101;                                           // Unfinished

        // The current catalog structure version
        internal static readonly uint CurrentCatalogStructureVersion = 1;

        // Logfile location
        internal static readonly string LogfileFullPath = Path.Combine(Application.dataPath, $"{ internalName }.log");

        // Updater logfile location
        internal static readonly string UpdaterLogfileFullPath = Path.Combine(Application.dataPath, $"{ internalName }Updater.log");

        // Report filename, without path
        internal static readonly string ReportTextFileName = $"{ name } Report.txt";
        internal static readonly string ReportHtmlFileName = $"{ name } Report.html";

        // Downloaded catalog url
        internal static readonly string CatalogURL = "https://surfdrive.surf.nl/files/index.php/s/OwBdunIj4BDc8Jb/download";

        // Downloaded Catalog local location
        internal static readonly string DownloadedCatalogFullPath = Path.Combine(DataLocation.localApplicationData, $"{ internalName }Catalog.xml");

        // Bundled Catalog location: in the same location as the mod itself
        internal static readonly string BundledCatalogFullPath;                                 // Set in constructor

        // 'Please report' text to include in logs when something odd happens
        internal static readonly string PleaseReportText = $"Please report this on the Workshop page for { name }: { Tools.GetWorkshopURL(SteamID) } ";

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


        /// Settings that come from the catalog; defaults for creating a catalog

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
            Tools.GetWorkshopURL(SteamID);

        // Default HTML report intro and footer
        internal static readonly string DefaultIntroHtml = "";                                  // Unfinished

        internal static readonly string DefaultFooterHtml = "";                                 // Unfinished



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
        internal static long LogMaxSize { get; private set; } = 100 * 1024;                         // 100 KB

        // Which scenes to run the scanner: "IntroScreen" and/or "Game"
        internal static List<string> ScannerScenes { get; private set; } = new List<string>() { "IntroScreen" };

        // Number of retries on failed downloads; must be a constant
        internal const uint downloadRetries = 1;



        /// Settings for the updater only

        // Steam Workshop mods listing url without page number
        internal static readonly string SteamModsListingURL =
            "https://steamcommunity.com/workshop/browse/?appid=255710&requiredtags%5B0%5D=Mod&actualsort=mostrecent&browsesort=mostrecent";

        // Temporary download location for Steam Workshop pages
        internal static readonly string SteamWebpageFullPath = Path.Combine(DataLocation.localApplicationData, $"{ internalName }SteamPage.html");

        // Max. number of Steam Workshop pages to download, to limit the time spend and to avoid downloading for eternity
        internal static uint SteamMaxPages = 100;

        // Max. number of download errors before giving up
        internal static uint SteamDownloadRetries = 3;

        // Delay between downloading individual mod pages, to avoid being marked suspicious by Steam or their CDN; not used for mod listing pages
        internal static uint SteamDownloadDelayInMilliseconds = 250;

        // String to recognize a line in Steam webpages with mod information
        internal static string SteamHtmlSearchMod = "<div class=\"workshopItemTitle ellipsis\">";

        // String to recognize for pages without mods
        internal static string SteamHtmlNoMoreFound = "No items matching your search criteria were found";

        // Strings to use for finding the mod and author info in the HTML lines
        internal static string SteamHtmlBeforeModID = "steamcommunity.com/sharedfiles/filedetails/?id=";
        internal static string SteamHtmlAfterModID = "&searchtext";
        internal static string SteamHtmlBeforeModName = "workshopItemTitle ellipsis\">";
        internal static string SteamHtmlAfterModName = "</div>";
        internal static string SteamHtmlBeforeAuthorID = "steamcommunity.com/id/";
        internal static string SteamHtmlBeforeAuthorProfile = "steamcommunity.com/profiles/";
        internal static string SteamHtmlAfterAuthorID = "/myworkshopfiles";
        internal static string SteamHtmlBeforeAuthorName = "/myworkshopfiles/?appid=255710\">";
        internal static string SteamHtmlAfterAuthorName = "</a>";
    }
}
