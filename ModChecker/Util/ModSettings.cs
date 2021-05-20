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
        // The version of this mod, split and combined; used in AssemblyInfo, must be a constant
        internal const string shortVersion = "0.1";
        internal const string revision = "0";
        internal const string build = "80";
        internal const string version = shortVersion + "." + revision + "." + build;

        // Release type: alpha, beta, test or "" (production)
        internal const string releaseType = "alpha";


        // Constructor
        static ModSettings()
        {
            try
            {
                BundledCatalogFullPath = Path.Combine(DataLocation.assemblyDirectory, $"{ internalName }Catalog.xml");                      // Steam Workshop mod
            }
            catch
            {
                BundledCatalogFullPath = Path.Combine(Path.Combine(DataLocation.modsPath, internalName), $"{ internalName }Catalog.xml");   // Local mod
            }
        }


        /// Hardcoded settings that can't be changed by users

        // Mod names, shown in the report from this mod and in the game Options window and Content Manager; used in AssemblyInfo, must be a constant
        internal const string name = "Mod Checker";                                                     // used in report filename, reporting and logging
        internal const string displayName = name + " " + shortVersion + " " + releaseType;              // used in game options, Content Manager and AssemblyInfo
        internal const string internalName = "ModChecker";                                              // used in filenames, xmlRoot, game log, ...

        // Mod description, shown in Content Manager; used in AssemblyInfo, must be a constant
        internal const string description = "Checks your subscribed mods for compatibility. Version " + version + " " + releaseType;

        // Author name; used in AssemblyInfo, must be a constant
        internal const string author = "Finwickle";

        // This mods own Steam ID
        internal static ulong SteamID { get; private set; } = 101;                                      // Unfinished

        // The XML root of the Catalog; must be constant
        internal const string xmlRoot = internalName + "Catalog";

        // The current catalog structure version
        internal const uint CurrentCatalogStructureVersion = 1;


        // Logfile location
        internal static string LogfileFullPath { get; private set; } = Path.Combine(Application.dataPath, $"{ internalName }.log");

        // Downloaded catalog url
        internal static string CatalogURL { get; private set; } = "https://surfdrive.surf.nl/files/index.php/s/OwBdunIj4BDc8Jb/download";

        // Report filename, without path
        internal static string ReportTextFileName { get; private set; } = $"{ name } Report.txt";
        internal static string ReportHtmlFileName { get; private set; } = $"{ name } Report.html";

        // Downloaded Catalog local location
        internal static string DownloadedCatalogFullPath { get; private set; } = Path.Combine(DataLocation.localApplicationData, $"{ internalName }Catalog.xml");

        // Bundled Catalog location: in the same location as the mod itself
        internal static string BundledCatalogFullPath { get; private set; }                            // Set in constructor


        // 'Please report' text to include in logs when something odd happens
        internal static string PleaseReportText { get; private set; } = $"Please report this on the Workshop page for { name }: { Tools.GetWorkshopURL(SteamID) } ";

        // Separators used in the logfile and TXT report
        internal const string separator       = "------------------------------------------------------------------------------------------";
        internal const string separatorDouble = "==========================================================================================";

        // Separator to use in logfiles when appending
        internal const string sessionSeparator = "\n\n" + separatorDouble + "\n\n";

        // Max width of the TXT report: 
        internal static int MaxReportWidth { get; private set; } = separator.Length - 1;

        // Bullets used in the TXT report:
        internal static string Bullet    { get; private set; } = " - ";
        internal static string NoBullet  { get; private set; } = "".PadLeft(Bullet.Length);         // Unfinished: any more efficient way to do this?
        internal static string Bullet2   { get; private set; } = NoBullet  + "  * ";
        internal static string NoBullet2 { get; private set; } = "".PadLeft(Bullet2.Length);
        internal static string Bullet3   { get; private set; } = NoBullet2 + "  - ";


        // Builtin mod fake IDs, keyed by name. These IDs are always the same, so they can be used for mod compatibility.
        internal static Dictionary<string, ulong> BuiltinMods { get; private set; } = new Dictionary<string, ulong>
        {
            { "Hard Mode", 1 },
            { "Unlimited Money", 2 },
            { "Unlimited Oil And Ore", 3 },
            { "Unlimited Soil", 4 },
            { "Unlock All", 5 }
        };

        // Lowest and highest fake Steam ID for unknown builtin mods, local mods and mod groups
        // These should be in this order, not overlap, be higher than above BuiltinMods IDs, and all be lower than real Steam IDs
        internal const ulong lowestUnknownBuiltinModID  = 11;
        internal const ulong highestUnknownBuiltinModID = 99;
        internal const ulong lowestLocalModID          = 101;
        internal const ulong highestLocalModID        = 9999;
        internal const ulong lowestModGroupID        = 10001;
        internal const ulong highestModGroupID      = 999999;
        internal static ulong HighestFakeID { get; private set; } = Math.Max(Math.Max(highestUnknownBuiltinModID, highestLocalModID), highestModGroupID);
        
        
        /// Settings that come from the catalog; defaults for creating a catalog
        
        // The game version this mod is updated for; the catalog can overrule this
        internal static Version CompatibleGameVersion { get; private set; } = GameVersion.Patch_1_13_1_f1;

        // Default report intro text for creating new catalogs; should end with \n
        internal static string DefaultIntroText { get; private set; } =
                       "Basic information about mods:\n" +
            Bullet +   "Always exit to desktop before loading another save! (no 'Second Loading')\n" +
            Bullet +   "Always unsubscribe mods you're not using! Disabled mods are still partially loaded.\n" +
            Bullet +   "Never (un)subscribe to anything while the game is running! This resets some mods.\n" +
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

        // Default report footer text for creating new catalogs
        internal static string DefaultFooterText { get; private set; } =
            "Did this help? Do you miss anything? Leave a rating/comment at the workshop page.\n" + 
            Tools.GetWorkshopURL(SteamID);

        // Unfinished: defaults for Html needed


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

        // Overwrite (default) or append the log file for normal mode; always append for debug mode
        internal static bool LogOverwrite { get; private set; } = true && !DebugMode;

        // Maximum log file size before starting with new log file; only applicable when appending
        internal static long LogMaxSize { get; private set; } = 100 * 1024;                         // 100 KB

        // Example catalog location                                                                 // Unfinished: temporary location (previously downloaded)
        internal static string ExampleCatalogFullPath { get; private set; } = Path.Combine(DataLocation.localApplicationData, internalName + "Catalog.xml");

        // Which scenes to run the scanner: "IntroScreen" and/or "Game"
        internal static List<string> ScannerScenes { get; private set; } = new List<string>() { "IntroScreen" };
    }
}
