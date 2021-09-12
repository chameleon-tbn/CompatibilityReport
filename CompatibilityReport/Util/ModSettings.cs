using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ColossalFramework.IO;

namespace CompatibilityReport.Util
{
    public static class ModSettings
    {
        // Todo 0.7 Reduce number of static fields.

        // Mod properties.
        public const string ModName = "Compatibility Report";
        public const string InternalName = "CompatibilityReport";

        public const string ModDescription = "Checks your subscribed mods for compatibility and missing dependencies.";
        public const string ModAuthor = "Finwickle";
        public const ulong OurOwnSteamID = LowestLocalModID;    // Todo 0.5 Our own Steam ID

        public const string Version = "0.4.0";
        public const string Build = "236";
        public const string FullVersion = Version + "." + Build;
        public const string ReleaseType = "alpha";
        public const int CurrentCatalogStructureVersion = 1;


        // Fake Steam IDs.
        public static Dictionary<string, ulong> BuiltinMods { get; } = new Dictionary<string, ulong>
        {
            { "Hard Mode", 1 },
            { "Unlimited Money", 2 },
            { "Unlimited Oil And Ore", 3 },
            { "Unlimited Soil", 4 },
            { "Unlock All", 5 }
        };

        public const ulong FakeAuthorIDforColossalOrder = 101;
        public const ulong LowestLocalModID =            1001;
        public const ulong HighestLocalModID =           9999;
        public const ulong LowestGroupID =              10001;
        public const ulong HighestGroupID =             99999;
        public const ulong HighestFakeID =             999999;


        // Filenames and paths.
        // Standard paths on Windows:
        //      DataLocation.applicationBase        = ...\Steam Games\steamapps\common\Cities_Skylines
        //      Application.dataPath                = .../Steam Games/steamapps/common/Cities_Skylines/Cities_Data
        //      DataLocation.gameContentPath        = ...\Steam Games\steamapps\common\Cities_Skylines\Files
        //      DataLocation.localApplicationData   = %LocalAppData%\Colossal Order\Cities_Skylines                     // Contains the Windows username.
        //      DataLocation.modsPath               = %localappdata%\Colossal Order\Cities_Skylines\Addons\Mods         // Contains the Windows username.
        //      DataLocation.assemblyDirectory      = ...\Steam Games\steamapps\workshop\content\255710\<mod-steamid>   // Throws "Invalid Path" exception for local mods.

        public const string ReportTextFileName = ModName + ".txt";
        public const string ReportHtmlFileName = ModName + ".html";
        public static string DefaultTextReportFullPath { get; } = Path.Combine(ReportPath, ReportTextFileName);
        public static string DefaultHtmlReportFullPath { get; } = Path.Combine(ReportPath, ReportTextFileName);

        public static string SettingsFileFullPath { get; } = Path.Combine(UpdaterPath, $"{ InternalName }_Settings.xml");
        public static string LogfileFullPath { get; } = Path.Combine(Application.dataPath, $"{ InternalName }.log");
        
        public static string BundledCatalogFullPath
        { 
            get {
                try
                {
                    // This will work if we are a Steam Workshop mod, but not if we are a local mod.
                    return Path.Combine(DataLocation.assemblyDirectory, $"{ InternalName }_Catalog.xml");
                }
                catch
                {
                    return Path.Combine(Path.Combine(DataLocation.modsPath, InternalName), $"{ InternalName }_Catalog.xml");
                }
            }
        }

        // .NET 3.5 only support TSL 1.2 with registry edits, which we can't rely on for mod users. So for a download location we
        // either need an 'unsafe' webserver that still support TLS 1.1, or a HTTP only site. Or switch to .NET 4.5+.
        public const string CatalogURL = "https://drive.google.com/uc?export=download&id=1oUT2U_PhLfW-KGWOyShHL2GvU6kyE4a2";
        public static string DownloadedCatalogFullPath { get; } = Path.Combine(DataLocation.localApplicationData, $"{ InternalName }_Downloaded_Catalog.xml");


        // Report and log properties.
        public const string ReportName = ModName;

        public const int MinimalTextReportWidth = 90;

        public const string Bullet1 = " - ";
        public const string Indent1 = "   ";
        public const string Bullet2 = "     * ";
        public const string Indent2 = "       ";
        public const string Bullet3 = "         - ";
        public const string Indent3 = "           ";

        public const string DefaultHeaderText = "Basic information about mods:\n" +
            Bullet1 + "Always exit to desktop and restart the game when loading another save! Exiting to main menu and " +
                "loading another savegame (called 'second loading') gives lots of mod issues.\n" +
            Bullet1 + "Never (un)subscribe to anything while the game is running! This resets some mods.\n" +
            Bullet1 + "Always unsubscribe mods you're not using. Disabling often isn't good enough.\n" +
            Bullet1 + "Mods not updated for a while might still work fine. Check their Workshop page.\n" +
            "\n" +
            "Some remarks about incompatibilities:\n" +
            Bullet1 + "Mods that do the same thing are generally incompatible with each other.\n" +
            Bullet1 + "Some issues are a conflict between more than two mods or a loading order issue, making it hard to find the real culprit. " + 
                "This can lead to users blaming the wrong mod for an error. Don't believe everything you read about mod conflicts.\n" +
            Bullet1 + "Savegame not loading? Use the optimization and safe mode options from Loading Screen: " +
                "https://steamcommunity.com/sharedfiles/filedetails/?id=667342976 \n" +
            Bullet1 + "Getting errors despite all your mods being compatible? Try the Loading Order Mod: " +
                "https://steamcommunity.com/sharedfiles/filedetails/?id=2448824112 \n" +
            "\n" +
            "Disclaimer:\n" +
            Bullet1 + "We try to include reliable, researched information about incompatibilities and highly value the words of mod authors in this. " + 
                "However, we will occasionally get it wrong or miss an update. Found a mistake? Please comment on the Workshop.";   // Todo 0.5 Add our Workshop URL.

        public const string DefaultFooterText = "Did this help? Do you miss anything? Leave a comment at the Workshop page.";       // Todo 0.5 Add our Workshop URL.

        public const string FirstCatalogNote = "This first catalog only contains the builtin mods.";
        public const string SecondCatalogNote = "This catalog contains basic information about all Steam Workshop mods. No reviews yet.";

        public const string PleaseReportText = "Please report this on the Steam Workshop page for " + ModName + ".";                // Todo 0.5 Add our Workshop URL.


        // Todo 0.7 Defaults for settings that will be available to users through mod options within the game.
        public static string ReportPath { get; private set; } = DataLocation.applicationBase;
        public static string TextReportFullPath { get; private set; } = DefaultTextReportFullPath;
        public static string HtmlReportFullPath { get; private set; } = DefaultHtmlReportFullPath;
        public static int TextReportWidth { get; private set; } = MinimalTextReportWidth;
        public static bool ReportSortByName { get; private set; } = true;
        public static bool HtmlReport { get; private set; } = false;
        public static bool TextReport { get; private set; } = true;
        public static bool AllowOnDemandScanning { get; private set; } = false;

        // Calculated from above settings.
        public static string Separator { get; private set; } = new string('-', TextReportWidth);
        public static string SeparatorDouble { get; private set; } = new string('=', TextReportWidth);
        public static string SessionSeparator { get; private set; } = $"\n\n{ SeparatorDouble }\n\n";


        // Todo 0.7 Defaults for settings that will be available in a settings xml file.
        public static int DownloadRetries { get; private set; } = 2;
        public static bool ScanBeforeMainMenu { get; private set; } = true;
        public static bool DebugMode { get; private set; } = true;
        public static bool LogAppend { get; private set; } = false || DebugMode;
        public static long LogMaxSize { get; private set; } = 100 * 1024;


        // Updater properties.
        public static string UpdaterPath { get; } = Path.Combine(DataLocation.localApplicationData, $"{ InternalName }Updater");
        public static bool UpdaterAvailable { get; } = Directory.Exists(UpdaterPath);
        public static string UpdaterSettingsFileFullPath { get; } = Path.Combine(UpdaterPath, $"{ InternalName }_UpdaterSettings.xml");
        public static string UpdaterLogfileFullPath { get; } = Path.Combine(UpdaterPath, $"{ InternalName }_Updater.log");
        public static string DataDumpFullPath { get; } = Path.Combine(UpdaterPath, $"{ InternalName }_DataDump.txt");
        public static string TempDownloadFullPath { get; } = Path.Combine(UpdaterPath, $"{ InternalName }_Download.tmp");
        public static string TempCsvCombinedFullPath { get; } = Path.Combine(UpdaterPath, $"{ InternalName }_CSVCombined.tmp");

        public static List<string> SteamModListingURLs { get; } = new List<string> {
            "https://steamcommunity.com/workshop/browse/?appid=255710&browsesort=mostrecent&requiredtags[]=Mod",
            "https://steamcommunity.com/workshop/browse/?appid=255710&browsesort=mostrecent&requiredtags[]=Cinematic+Cameras",
            "https://steamcommunity.com/workshop/browse/?appid=255710&browsesort=mostrecent&requiredtags[]=Mod&requiredflags[]=incompatible",
            "https://steamcommunity.com/workshop/browse/?appid=255710&browsesort=mostrecent&requiredtags[]=Cinematic+Cameras&requiredflags[]=incompatible" };

        public const int MonthsOfInactivityToRetireAuthor = 12;
        public const int SteamMaxModListingPages = 200;

        // Search strings for mod and author info in the mod listing files.
        public const string SearchModStart = "<div class=\"workshopItemTitle ellipsis\">";
        public const string SearchSteamIDLeft = "steamcommunity.com/sharedfiles/filedetails/?id=";
        public const string SearchSteamIDRight = "&searchtext";
        public const string SearchListingModNameLeft = "workshopItemTitle ellipsis\">";
        public const string SearchListingModNameRight = "</div>";
        // public const string SearchAuthorURLLeft = "steamcommunity.com/id/";
        // public const string SearchAuthorIDLeft = "steamcommunity.com/profiles/";
        // public const string SearchListingAuthorRight = "/myworkshopfiles";
        // public const string SearchAuthorNameLeft = "/myworkshopfiles/?appid=255710\">";
        // public const string SearchAuthorNameRight = "</a>";

        // Search strings for individual mod pages.
        public const string SearchItemNotFound = "There was a problem accessing the item. Please try again.";
        public const string SearchSteamID = "var publishedfileid = '";                                                  // Followed by the Steam ID.
        public const string SearchAuthorLeft = "&gt;&nbsp;<a href=\"https://steamcommunity.com/";                       // Followed by 'id' or 'profiles'.
        public const string SearchAuthorMid = "/myworkshopfiles/?appid=255710\">";                                      // Sits between ID/URL and name.
        public const string SearchAuthorRight = "'s Workshop</a>";
        public const string SearchModNameLeft = "<div class=\"workshopItemTitle\">";
        public const string SearchModNameRight = "</div>";
        public const string SearchVersionTag = "<span class=\"workshopTagsTitle\">Tags:";                               // Can appear multiple times.
        public const string SearchVersionTagLeft = "-compatible\">";
        public const string SearchVersionTagRight = "-compatible";
        public const string SearchDates = "<div class=\"detailsStatsContainerRight\">";
        public const string SearchDatesLeft = "detailsStatRight\">";                                                    // Two/three lines below 'Find'.
        public const string SearchDatesRight = "</div>";
        public const string SearchRequiredDLC = "<div class=\"requiredDLCItem\">";                                      // Can appear multiple times.
        public const string SearchRequiredDLCLeft = "https://store.steampowered.com/app/";                              // One line below 'Find' text.
        public const string SearchRequiredDLCRight = "\">";
        public const string SearchRequiredMod = "<div class=\"requiredItemsContainer\" id=\"RequiredItems\">";          // Can appear multiple times.
        public const string SearchRequiredModLeft = "https://steamcommunity.com/workshop/filedetails/?id=";             // One line below 'Find' text.
        public const string SearchRequiredModRight = "\" ";
        public const string SearchDescription = "<div class=\"workshopItemDescriptionTitle\">Description</div>";
        public const string SearchDescriptionLeft = "workshopItemDescription\" id=\"highlightContent\">";               // One line below 'Find' text.
        public const string SearchDescriptionRight = "</div>";
        public const string SearchSourceURLLeft = "https://steamcommunity.com/linkfilter/?url=https://github.com/";     // Only GitHub is recognized.
        public const string SearchSourceURLRight = "\" ";


        // Todo 0.7 Defaults for updater settings that will be available in an updater settings xml file.
        public static bool UpdaterEnabled { get; private set; } = true;
        public static bool WebCrawlerEnabled { get; private set; } = !File.Exists(Path.Combine(UpdaterPath, $"{ InternalName }_WebCrawler.disabled"));
        public static int SteamMaxFailedPages { get; private set; } = 4;
    }
}
