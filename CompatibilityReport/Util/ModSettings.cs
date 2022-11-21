using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ColossalFramework.IO;
using CompatibilityReport.Settings;

namespace CompatibilityReport.Util
{
    public static class ModSettings
    {
        // Mod properties.
        public const string Version = "2.0.6";
#if DEBUG
        // allow for hot-swapping the mod - rebuild only if it's in the main menu, game will detect and reload the mod
        public const string Build = "*";
#else
        public const string Build = "439";
#endif
        public const string ReleaseType = "";
        public const int CurrentCatalogStructureVersion = 6;

        public const string ModName = "Compatibility Report";
        public const string InternalName = "CompatibilityReport";
        public const string IUserModName = ModName + " v" + Version + ReleaseType;
        public const string IUserModDescription = "Checks your subscribed mods for compatibility and missing dependencies.";
        public const string ModAuthor = "Chamëleon TBN";
        public const string FullVersion = Version + "." + Build + ReleaseType;
        public const string CopyrightYear = "2022";
        public const ulong OurOwnSteamID = 2881031511;


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
        public const ulong LowestLocalModID = 1001;
        public const ulong HighestLocalModID = 9999;
        public const ulong LowestGroupID = 10001;
        public const ulong HighestGroupID = 99999;
        public const ulong HighestFakeID = 999999;


        // Filenames and paths.

        // Standard paths on Windows:
        //      DataLocation.applicationBase        = ...\Steam Games\steamapps\common\Cities_Skylines
        //      Application.dataPath                = .../Steam Games/steamapps/common/Cities_Skylines/Cities_Data
        //      DataLocation.gameContentPath        = ...\Steam Games\steamapps\common\Cities_Skylines\Files
        //      DataLocation.localApplicationData   = %LocalAppData%\Colossal Order\Cities_Skylines                     // Contains the Windows username.
        //      DataLocation.modsPath               = %localappdata%\Colossal Order\Cities_Skylines\Addons\Mods         // Contains the Windows username.
        //      DataLocation.assemblyDirectory      = Invalid Path exception (should be mod folder)

        public static string DefaultReportPath { get; } = Application.dataPath;
        public static string AlternativeReportPath { get; } = DataLocation.localApplicationData;
        public const string ReportTextFileName = InternalName + ".txt";
        public const string ReportHtmlFileName = InternalName + ".html";

        public static string DebugLogPath { get; } = Path.Combine(Application.dataPath, "Logs");
        public const string LogFileName = InternalName + ".log";

        public static string WorkPath { get; } = DataLocation.localApplicationData;
        public static string DownloadedCatalogFileName { get; } = $"{ InternalName }_Downloaded_Catalog";
        public const string OldDownloadedCatalogFileName = InternalName + "_Downloaded_Catalog.xml";

        public static string BundledCatalogFullPath
        {
            get
            {
                try
                {
                    return Path.Combine(Toolkit.GetModPath(), $"{ InternalName }_Catalog.xml.gz");
                }
                catch
                {
                    // Local mod path.
                    return Path.Combine(Path.Combine(DataLocation.modsPath, InternalName), $"{ InternalName }_Catalog.xml.gz");
                }
            }
        }


        // Download properties.

        // The default timezone for Steam downloads seems to be UTC-7 (PDT) in summer and UTC-8 (PST) in winter,
        // meaning half the mod publish and update times and author last seen dates will be off by an hour half the time.
        // DON'T CHANGE THIS! Changing this will result in a new update for EVERY mod.
        public const string DefaultSteamTimezone = "-07:00";

        // .NET 3.5 only support TSL 1.2 with registry edits, which we can't rely on for mod users. So for a download location we
        // either need an 'unsafe' webserver that still support TLS 1.1, or a HTTP only site. Or switch to .NET 4.5+ or
        // use UnityWebRequest and use a Coroutine setup on a MonoBehaviour.
        public const string CatalogUrl = "https://drive.google.com/uc?export=download&id=1P8hakzFi2ydlXGybx5aatuggSW7RpBYG";

        public const string Bullet1 = " - ";
        public const string Indent1 = "   ";
        public const string Bullet2 = "    * ";
        public const string Indent2 = "      ";
        public const string Bullet3 = "        - ";
        public const string Indent3 = "          ";

        public const string ReportTextForThisModVersion = "";

        public static string PleaseReportText { get; } = $"Please report this on the Steam Workshop page: { Toolkit.GetWorkshopUrl(OurOwnSteamID) }.";


        // Miscellaneous settings
        public static string SettingsUIColor = "#FF8C00";

        public static bool DebugMode =>
#if DEBUG
            true;
#else
            GlobalConfig.Instance?.AdvancedConfig?.DebugMode ?? false;
#endif
        public static int TextReportWidth => GlobalConfig.Instance.GeneralConfig.TextReportWidth;

        // Updater properties.
        public static string UpdaterPath { get; } = Path.Combine(WorkPath, $"{ InternalName }Updater");
        public static bool UpdaterAvailable { get; } = Directory.Exists(UpdaterPath);
        public static bool UploadFileAvailable => File.Exists(Path.Combine(UpdaterPath, UploadCatalogFileName));
        public const string UpdaterSettingsFileName = InternalName + "_UpdaterSettings.xml";
        public const string UpdaterLogFileName = InternalName + "_Updater.log";
        public const string DataDumpFileName = InternalName + "_DataDump.txt";
        public const string CatalogDumpFileName = InternalName + "_WebCrawlerDump.xml";
        public const string OneTimeActionFileName = InternalName + "_OneTimeAction.txt";
        public const string UploadCatalogFileName = "Catalog for download.txt";

        public static string FakeSubscriptionsFileFullPath { get; } = Path.Combine(WorkPath, $"{ InternalName }_FakeSubscriptions.txt");

        public static string TempDownloadFullPath { get; } = Path.Combine(WorkPath, $"{ InternalName }_Download.tmp");
        public const string TempCsvCombinedFileName = InternalName + "_CSVCombined.tmp";

        public static List<string> SteamModListingUrls { get; } = new List<string> {
            "https://steamcommunity.com/workshop/browse/?appid=255710&browsesort=mostrecent&requiredtags[]=Mod",
            "https://steamcommunity.com/workshop/browse/?appid=255710&browsesort=mostrecent&requiredtags[]=Cinematic+Cameras",
            "https://steamcommunity.com/workshop/browse/?appid=255710&browsesort=mostrecent&requiredtags[]=Mod&requiredflags[]=incompatible",
            "https://steamcommunity.com/workshop/browse/?appid=255710&browsesort=mostrecent&requiredtags[]=Cinematic+Cameras&requiredflags[]=incompatible" };

        public const string SteamMapThemesListingUrl = "https://steamcommunity.com/workshop/browse/?appid=255710&browsesort=mostrecent&requiredtags[]=Map+Theme";

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
        public const string SearchDescriptionNextLine = "\t\t\t</div>";
        public const string SearchDescriptionNextSection = "<script>";
        public const string SearchSteamUrlFilter = "https://steamcommunity.com/linkfilter/?url=";
        public static readonly List<string> SearchSourceUrlSites = new List<string>                                      // All must end in a slash.
        { 
            "https://github.com/",
            "http://github.com/",
            "https://gist.github.com/",
            "http://gist.github.com/"
        };
        public const string SearchSourceUrlRight = "\" ";

        // Source URLs to ignore completely.
        public static readonly List<string> CommonSourceUrlsToIgnore = new List<string>
        {
            "https://github.com/pardeike",
            "http://github.com/pardeike",
            "https://github.com/sschoener/cities-skylines-detour",
            "http://github.com/sschoener/cities-skylines-detour"
        };

        // Source URLs (parts) to discard if another source URL was already found.
        public static readonly List<string> sourceUrlsToDiscard = new List<string>
        {
            "issue",
            "wiki",
            "documentation",
            "readme",
            "guide",
            "translation",
            "blob"
        };

        public const string FeedbackFormUrl = "https://forms.gle/PvezwfpgS1V1DHqA9";

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
