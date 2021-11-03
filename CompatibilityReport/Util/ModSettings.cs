using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ColossalFramework.IO;

namespace CompatibilityReport.Util
{
    public static class ModSettings
    {
        // Mod properties.
        public const string ModName = "Compatibility Report";
        public const string InternalName = "CompatibilityReport";

        public const string Version = "0.6.0";
        public const string Build = "361";
        public const string ReleaseType = " beta";
        public const string FullVersion = Version + "." + Build + ReleaseType;
        public const int CurrentCatalogStructureVersion = 1;

        public const string IUserModName = ModName + " v" + Version + ReleaseType;
        public const string IUserModDescription = "Checks your subscribed mods for compatibility and missing dependencies.";
        public const string ModAuthor = "Finwickle";
        public const ulong OurOwnSteamID = 2633433869;


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
        //      DataLocation.assemblyDirectory      = Invalid Path exception (should be mod folder)

        public static string DefaultReportPath { get; } = Application.dataPath;
        public const string ReportTextFileName = InternalName + ".txt";
        public const string ReportHtmlFileName = InternalName + ".html";

        public static string SettingsPath { get; } = DataLocation.applicationBase;
        public const string SettingsFileName = InternalName + "_Settings.xml";

        public static string DebugLogPath { get; } = Path.Combine(Application.dataPath, "Logs");
        public const string LogFileName = InternalName + ".log";

        public static string WorkPath { get; } = DataLocation.localApplicationData;
        public const string DownloadedCatalogFileName = InternalName + "_Downloaded_Catalog.xml";

        public static string BundledCatalogFullPath
        { 
            get 
            {
                try
                {
                    // Steam Workshop mod path. This only works if the workshop is in the same "steamapps" folder as the game, which will be true for most users.
                    // Todo 0.8 Find a more robust way of getting the mods own folder. See https://github.com/kianzarrin/UnifiedUI/blob/e77391479c0ab36c228402b898771a509535e846/UnifiedUILib/Helpers/UUIHelpers.cs#L376
                    char slash = Path.DirectorySeparatorChar;
                    string wsModPath = $"{ DataLocation.applicationBase }{ slash }..{ slash }..{ slash }workshop{ slash }content{ slash }255710{ slash }{ OurOwnSteamID }";
                    
                    if (!Directory.Exists(wsModPath))
                    {
                        throw new DirectoryNotFoundException();
                    }

                    return Path.Combine(wsModPath, $"{ InternalName }_Catalog.xml");
                }
                catch
                {
                    // Local mod path.
                    return Path.Combine(Path.Combine(DataLocation.modsPath, InternalName), $"{ InternalName }_Catalog.xml");
                }
            }
        }


        // Download properties.

        // The default timezone for Steam downloads seems to be UTC-7 (PDT) in summer and UTC-8 (PST) in winter,
        // meaning half the mod publish and update times and author last seen dates will be off by an hour half the time.
        public const string DefaultSteamTimezone = "-07:00";

        // .NET 3.5 only support TSL 1.2 with registry edits, which we can't rely on for mod users. So for a download location we
        // either need an 'unsafe' webserver that still support TLS 1.1, or a HTTP only site. Or switch to .NET 4.5+.
        public const string CatalogUrl = "https://drive.google.com/uc?export=download&id=1oUT2U_PhLfW-KGWOyShHL2GvU6kyE4a2";


        // Report and log properties.
        public const int MinimalTextReportWidth = 90;

        public const string Bullet1 = " - ";
        public const string Indent1 = "   ";
        public const string Bullet2 = "    * ";
        public const string Indent2 = "      ";
        public const string Bullet3 = "        - ";
        public const string Indent3 = "          ";

        public const string ReportTextForThisModVersion = "This is a BETA version of the mod, not thoroughly tested yet.";

        public static string PleaseReportText { get; } = $"Please report this on the Steam Workshop page: { Toolkit.GetWorkshopUrl(OurOwnSteamID) }.";


        // Todo 0.7 Settings that will be available to users through mod options within the game.
        public static string ReportPath { get; private set; } = DefaultReportPath;
        public static int TextReportWidth { get; private set; } = MinimalTextReportWidth;
        public static bool ReportSortByName { get; private set; } = true;
        public static bool HtmlReport { get; private set; } = false;
        public static bool TextReport { get; private set; } = true;
        public static bool AllowOnDemandScanning { get; private set; } = false;


        // Todo 0.7 Settings that will be available in a settings xml file.
        public static int DownloadRetries { get; private set; } = 4;
        public static bool ScanBeforeMainMenu { get; private set; } = true;
        public static bool DebugMode { get; private set; } = File.Exists(Path.Combine(WorkPath, $"{ InternalName }_Debug.enabled"));
        public static long LogMaxSize { get; private set; } = 100 * 1024;


        // Updater properties.
        public static string UpdaterPath { get; } = Path.Combine(WorkPath, $"{ InternalName }Updater");
        public static bool UpdaterAvailable { get; } = Directory.Exists(UpdaterPath);
        public const string UpdaterSettingsFileName = InternalName + "_UpdaterSettings.xml";
        public const string UpdaterLogFileName = InternalName + "_Updater.log";
        public const string DataDumpFileName = InternalName + "_DataDump.txt";

        public static string TempDownloadFullPath { get; } = Path.Combine(WorkPath, $"{ InternalName }_Download.tmp");
        public const string TempCsvCombinedFileName = InternalName + "_CSVCombined.tmp";

        public static List<string> SteamModListingUrls { get; } = new List<string> {
            "https://steamcommunity.com/workshop/browse/?appid=255710&browsesort=mostrecent&requiredtags[]=Mod",
            "https://steamcommunity.com/workshop/browse/?appid=255710&browsesort=mostrecent&requiredtags[]=Cinematic+Cameras",
            "https://steamcommunity.com/workshop/browse/?appid=255710&browsesort=mostrecent&requiredtags[]=Mod&requiredflags[]=incompatible",
            "https://steamcommunity.com/workshop/browse/?appid=255710&browsesort=mostrecent&requiredtags[]=Cinematic+Cameras&requiredflags[]=incompatible" };

        public const int MonthsOfInactivityToRetireAuthor = 12;
        public const int SteamMaxModListingPages = 200;
        public const int EstimatedMillisecondsPerModPage = 550;

        // Search strings for mod and author info in the mod listing files.
        public const string SearchModStart = "<div class=\"workshopItemTitle ellipsis\">";
        public const string SearchSteamIDLeft = "steamcommunity.com/sharedfiles/filedetails/?id=";
        public const string SearchSteamIDRight = "&searchtext";
        public const string SearchListingModNameLeft = "workshopItemTitle ellipsis\">";
        public const string SearchListingModNameRight = "</div>";
        // public const string SearchAuthorUrlLeft = "steamcommunity.com/id/";
        // public const string SearchAuthorIDLeft = "steamcommunity.com/profiles/";
        // public const string SearchListingAuthorRight = "/myworkshopfiles";
        // public const string SearchAuthorNameLeft = "/myworkshopfiles/?appid=255710\">";
        // public const string SearchAuthorNameRight = "</a>";

        // Search strings for individual mod pages.
        public const string SearchItemNotFound = "There was a problem accessing the item.";
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
        public const string SearchRequiredDlc = "<div class=\"requiredDLCItem\">";                                      // Can appear multiple times.
        public const string SearchRequiredDlcLeft = "https://store.steampowered.com/app/";                              // One line below 'Find' text.
        public const string SearchRequiredDlcRight = "\">";
        public const string SearchRequiredMod = "<div class=\"requiredItemsContainer\" id=\"RequiredItems\">";          // Can appear multiple times.
        public const string SearchRequiredModLeft = "https://steamcommunity.com/workshop/filedetails/?id=";             // One line below 'Find' text.
        public const string SearchRequiredModRight = "\" ";
        public const string SearchDescription = "<div class=\"workshopItemDescriptionTitle\">Description</div>";
        public const string SearchDescriptionLeft = "workshopItemDescription\" id=\"highlightContent\">";               // One line below 'Find' text.
        public const string SearchDescriptionRight = "</div>";
        public const string SearchSourceUrlLeft = "https://steamcommunity.com/linkfilter/?url=https://github.com/";     // Only GitHub is recognized.
        public const string SearchSourceUrlRight = "\" ";


        // Todo 0.7 Defaults for updater settings that will be available in an updater settings xml file.
        public static bool UpdaterEnabled { get; private set; } = true;
        public static bool WebCrawlerEnabled { get; private set; } = File.Exists(Path.Combine(UpdaterPath, $"{ InternalName }_WebCrawler.enabled"));
        public static int SteamMaxFailedPages { get; private set; } = 4;

        public const string DefaultHeaderText = "General information about using mods:\n" +
            Bullet1 + "Never (un)subscribe to anything while the game is running! This resets some mods.\n" +
            Bullet1 + "Always exit to desktop and restart the game. NEVER exit to main menu!\n" + 
            Bullet1 + "Mods not updated for a while might still work fine. Check their Workshop page.\n" +
            Bullet1 + "Mod compatible but not working? Try unsubscribe and resubscribe (while not in game).\n" +
            "\n" +
            "Found a mistake? Please comment on the Workshop.";

        public const string DefaultFooterText = "Did this help? Do you miss anything? Leave a comment at the Workshop page.";
        public const string FirstCatalogNote = "This first catalog only contains the built-in mods.";
    }
}
