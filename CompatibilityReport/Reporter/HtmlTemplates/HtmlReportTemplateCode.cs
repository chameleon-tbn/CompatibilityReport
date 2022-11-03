using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ColossalFramework.HTTP;
using ColossalFramework.PlatformServices;
using CompatibilityReport.CatalogData;
using CompatibilityReport.Settings;
using CompatibilityReport.Translations;
using CompatibilityReport.Util;

namespace CompatibilityReport.Reporter.HtmlTemplates
{
    internal partial class HtmlReportTemplate
    {
        private const string isCameraScriptMessage = "This is a cinematic camera script, which technically is a mod and thus listed here.";
        private const string noKnownIssuesMessage = "No known issues or incompatibilities with your other mods.";
        private const string cannotReviewMessage = "Can't review local mods.";
        private readonly List<ModInfo> unsubscribe, majorIssues, minorIssues, remarks, nothingToReport;
        private readonly DateTime reportCreationTime;
        private readonly Catalog catalog;

        private bool IsDifferentVersion => Toolkit.CurrentGameVersion() != catalog.GameVersion();
        private bool IsOlder => Toolkit.CurrentGameVersion() < catalog.GameVersion();
        private bool ShowOutdatedWarning => Catalog.DownloadStarted && !Catalog.DownloadSuccessful;
        private int NonReviewedSubscriptions => catalog.SubscriptionCount() - catalog.ReviewedSubscriptionCount - catalog.LocalSubscriptionCount;
        private string CatalogGameVersion => Toolkit.ConvertGameVersionToString(catalog.GameVersion());
        private string CurrentGameVersion => Toolkit.ConvertGameVersionToString(Toolkit.CurrentGameVersion());

        internal const string APPEND_VALUE_ID = "APPEND_VALUE"; 
        internal const string APPEND_TRANSLATED_VALUE_ID = "APPEND_TRANSLATED_VALUE"; 
        internal const string FLAG_XX = "<path fill=\"#fff\" fill-rule=\"evenodd\" stroke=\"#adb5bd\" stroke-width=\"1.1\" d=\"M.5.5h638.9v478.9H.5z\"/> <path fill=\"none\" stroke=\"#adb5bd\" stroke-width=\"1.1\" d=\"m.5.5 639 479M639.5.5l-639 479\"/>";

        internal List<string> AvailableLanguages = new List<string>();
        
        private Dictionary<string, string> _flags = new Dictionary<string, string>()
        {
            {"en_US", "<g fill-rule=\"evenodd\"> <g stroke-width=\"1pt\"> <path fill=\"#bd3d44\" d=\"M0 0h912v37H0zm0 73.9h912v37H0zm0 73.8h912v37H0zm0 73.8h912v37H0zm0 74h912v36.8H0zm0 73.7h912v37H0zM0 443h912V480H0z\"/> <path fill=\"#fff\" d=\"M0 37h912v36.9H0zm0 73.8h912v36.9H0zm0 73.8h912v37H0zm0 73.9h912v37H0zm0 73.8h912v37H0zm0 73.8h912v37H0z\"/> </g> <path fill=\"#192f5d\" d=\"M0 0h364.8v258.5H0z\"/> <path fill=\"#fff\" d=\"m30.4 11 3.4 10.3h10.6l-8.6 6.3 3.3 10.3-8.7-6.4-8.6 6.3L25 27.6l-8.7-6.3h10.9zm60.8 0 3.3 10.3h10.8l-8.7 6.3 3.2 10.3-8.6-6.4-8.7 6.3 3.3-10.2-8.6-6.3h10.6zm60.8 0 3.3 10.3H166l-8.6 6.3 3.3 10.3-8.7-6.4-8.7 6.3 3.3-10.2-8.7-6.3h10.8zm60.8 0 3.3 10.3h10.8l-8.7 6.3 3.3 10.3-8.7-6.4-8.7 6.3 3.4-10.2-8.8-6.3h10.7zm60.8 0 3.3 10.3h10.7l-8.6 6.3 3.3 10.3-8.7-6.4-8.7 6.3 3.3-10.2-8.6-6.3h10.7zm60.8 0 3.3 10.3h10.8l-8.8 6.3 3.4 10.3-8.7-6.4-8.7 6.3 3.4-10.2-8.8-6.3h10.8zM60.8 37l3.3 10.2H75l-8.7 6.2 3.2 10.3-8.5-6.3-8.7 6.3 3.1-10.3-8.4-6.2h10.7zm60.8 0 3.4 10.2h10.7l-8.8 6.2 3.4 10.3-8.7-6.3-8.7 6.3 3.3-10.3-8.7-6.2h10.8zm60.8 0 3.3 10.2h10.8l-8.7 6.2 3.3 10.3-8.7-6.3-8.7 6.3 3.3-10.3-8.6-6.2H179zm60.8 0 3.4 10.2h10.7l-8.8 6.2 3.4 10.3-8.7-6.3-8.6 6.3 3.2-10.3-8.7-6.2H240zm60.8 0 3.3 10.2h10.8l-8.7 6.2 3.3 10.3-8.7-6.3-8.7 6.3 3.3-10.3-8.6-6.2h10.7zM30.4 62.6l3.4 10.4h10.6l-8.6 6.3 3.3 10.2-8.7-6.3-8.6 6.3L25 79.3 16.3 73h10.9zm60.8 0L94.5 73h10.8l-8.7 6.3 3.2 10.2-8.6-6.3-8.7 6.3 3.3-10.3-8.6-6.3h10.6zm60.8 0 3.3 10.3H166l-8.6 6.3 3.3 10.2-8.7-6.3-8.7 6.3 3.3-10.3-8.7-6.3h10.8zm60.8 0 3.3 10.3h10.8l-8.7 6.3 3.3 10.2-8.7-6.3-8.7 6.3 3.4-10.3-8.8-6.3h10.7zm60.8 0 3.3 10.3h10.7l-8.6 6.3 3.3 10.2-8.7-6.3-8.7 6.3 3.3-10.3-8.6-6.3h10.7zm60.8 0 3.3 10.3h10.8l-8.8 6.3 3.4 10.2-8.7-6.3-8.7 6.3 3.4-10.3-8.8-6.3h10.8zM60.8 88.6l3.3 10.2H75l-8.7 6.3 3.3 10.3-8.7-6.4-8.7 6.3 3.3-10.2-8.6-6.3h10.7zm60.8 0 3.4 10.2h10.7l-8.8 6.3 3.4 10.3-8.7-6.4-8.7 6.3 3.3-10.2-8.7-6.3h10.8zm60.8 0 3.3 10.2h10.8l-8.7 6.3 3.3 10.3-8.7-6.4-8.7 6.3 3.3-10.2-8.6-6.3H179zm60.8 0 3.4 10.2h10.7l-8.7 6.3 3.3 10.3-8.7-6.4-8.6 6.3 3.2-10.2-8.7-6.3H240zm60.8 0 3.3 10.2h10.8l-8.7 6.3 3.3 10.3-8.7-6.4-8.7 6.3 3.3-10.2-8.6-6.3h10.7zM30.4 114.5l3.4 10.2h10.6l-8.6 6.3 3.3 10.3-8.7-6.4-8.6 6.3L25 131l-8.7-6.3h10.9zm60.8 0 3.3 10.2h10.8l-8.7 6.3 3.2 10.2-8.6-6.3-8.7 6.3 3.3-10.2-8.6-6.3h10.6zm60.8 0 3.3 10.2H166l-8.6 6.3 3.3 10.3-8.7-6.4-8.7 6.3 3.3-10.2-8.7-6.3h10.8zm60.8 0 3.3 10.2h10.8l-8.7 6.3 3.3 10.3-8.7-6.4-8.7 6.3 3.4-10.2-8.8-6.3h10.7zm60.8 0 3.3 10.2h10.7L279 131l3.3 10.3-8.7-6.4-8.7 6.3 3.3-10.2-8.6-6.3h10.7zm60.8 0 3.3 10.2h10.8l-8.8 6.3 3.4 10.3-8.7-6.4-8.7 6.3L329 131l-8.8-6.3h10.8zM60.8 140.3l3.3 10.3H75l-8.7 6.2 3.3 10.3-8.7-6.4-8.7 6.4 3.3-10.3-8.6-6.3h10.7zm60.8 0 3.4 10.3h10.7l-8.8 6.2 3.4 10.3-8.7-6.4-8.7 6.4 3.3-10.3-8.7-6.3h10.8zm60.8 0 3.3 10.3h10.8l-8.7 6.2 3.3 10.3-8.7-6.4-8.7 6.4 3.3-10.3-8.6-6.3H179zm60.8 0 3.4 10.3h10.7l-8.7 6.2 3.3 10.3-8.7-6.4-8.6 6.4 3.2-10.3-8.7-6.3H240zm60.8 0 3.3 10.3h10.8l-8.7 6.2 3.3 10.3-8.7-6.4-8.7 6.4 3.3-10.3-8.6-6.3h10.7zM30.4 166.1l3.4 10.3h10.6l-8.6 6.3 3.3 10.1-8.7-6.2-8.6 6.2 3.2-10.2-8.7-6.3h10.9zm60.8 0 3.3 10.3h10.8l-8.7 6.3 3.3 10.1-8.7-6.2-8.7 6.2 3.4-10.2-8.7-6.3h10.6zm60.8 0 3.3 10.3H166l-8.6 6.3 3.3 10.1-8.7-6.2-8.7 6.2 3.3-10.2-8.7-6.3h10.8zm60.8 0 3.3 10.3h10.8l-8.7 6.3 3.3 10.1-8.7-6.2-8.7 6.2 3.4-10.2-8.8-6.3h10.7zm60.8 0 3.3 10.3h10.7l-8.6 6.3 3.3 10.1-8.7-6.2-8.7 6.2 3.3-10.2-8.6-6.3h10.7zm60.8 0 3.3 10.3h10.8l-8.8 6.3 3.4 10.1-8.7-6.2-8.7 6.2 3.4-10.2-8.8-6.3h10.8zM60.8 192l3.3 10.2H75l-8.7 6.3 3.3 10.3-8.7-6.4-8.7 6.3 3.3-10.2-8.6-6.3h10.7zm60.8 0 3.4 10.2h10.7l-8.8 6.3 3.4 10.3-8.7-6.4-8.7 6.3 3.3-10.2-8.7-6.3h10.8zm60.8 0 3.3 10.2h10.8l-8.7 6.3 3.3 10.3-8.7-6.4-8.7 6.3 3.3-10.2-8.6-6.3H179zm60.8 0 3.4 10.2h10.7l-8.7 6.3 3.3 10.3-8.7-6.4-8.6 6.3 3.2-10.2-8.7-6.3H240zm60.8 0 3.3 10.2h10.8l-8.7 6.3 3.3 10.3-8.7-6.4-8.7 6.3 3.3-10.2-8.6-6.3h10.7zM30.4 217.9l3.4 10.2h10.6l-8.6 6.3 3.3 10.2-8.7-6.3-8.6 6.3 3.2-10.3-8.7-6.3h10.9zm60.8 0 3.3 10.2h10.8l-8.7 6.3 3.3 10.2-8.7-6.3-8.7 6.3 3.4-10.3-8.7-6.3h10.6zm60.8 0 3.3 10.2H166l-8.4 6.3 3.3 10.2-8.7-6.3-8.7 6.3 3.3-10.3-8.7-6.3h10.8zm60.8 0 3.3 10.2h10.8l-8.7 6.3 3.3 10.2-8.7-6.3-8.7 6.3 3.4-10.3-8.8-6.3h10.7zm60.8 0 3.3 10.2h10.7l-8.6 6.3 3.3 10.2-8.7-6.3-8.7 6.3 3.3-10.3-8.6-6.3h10.7zm60.8 0 3.3 10.2h10.8l-8.8 6.3 3.4 10.2-8.7-6.3-8.7 6.3 3.4-10.3-8.8-6.3h10.8z\"/> </g>"},
            {"de_DE", "<path fill=\"#ffce00\" d=\"M0 320h640v160H0z\"/> <path d=\"M0 0h640v160H0z\"/> <path fill=\"#d00\" d=\"M0 160h640v160H0z\"/>"},
            {"fr_FR", "<g fill-rule=\"evenodd\" stroke-width=\"1pt\"> <path fill=\"#fff\" d=\"M0 0h640v480H0z\"/> <path fill=\"#002654\" d=\"M0 0h213.3v480H0z\"/> <path fill=\"#ce1126\" d=\"M426.7 0H640v480H426.7z\"/> </g>"},
            {"ja_JP", "<defs> <clipPath id=\"jp\"> <path fill-opacity=\".7\" d=\"M-88 32h640v480H-88z\"/> </clipPath> </defs> <g fill-rule=\"evenodd\" stroke-width=\"1pt\" clip-path=\"url(#jp)\" transform=\"translate(88 -32)\"> <path fill=\"#fff\" d=\"M-128 32h720v480h-720z\"/> <circle cx=\"523.1\" cy=\"344.1\" r=\"194.9\" fill=\"#bc002d\" transform=\"translate(-168.4 8.6) scale(.76554)\"/> </g>"},
            {"ko_KR", "<defs> <clipPath id=\"ko\"> <path fill-opacity=\".7\" d=\"M-95.8-.4h682.7v512H-95.8z\"/> </clipPath> </defs> <g fill-rule=\"evenodd\" clip-path=\"url(#ko)\" transform=\"translate(89.8 .4) scale(.9375)\"> <path fill=\"#fff\" d=\"M-95.8-.4H587v512H-95.8Z\"/> <g transform=\"rotate(-56.3 361.6 -101.3) scale(10.66667)\"> <g id=\"c\"> <path id=\"b\" d=\"M-6-26H6v2H-6Zm0 3H6v2H-6Zm0 3H6v2H-6Z\"/> <use xlink:href=\"#b\" width=\"100%\" height=\"100%\" y=\"44\"/> </g> <path stroke=\"#fff\" d=\"M0 17v10\"/> <path fill=\"#cd2e3a\" d=\"M0-12a12 12 0 0 1 0 24Z\"/> <path fill=\"#0047a0\" d=\"M0-12a12 12 0 0 0 0 24A6 6 0 0 0 0 0Z\"/> <circle cy=\"-6\" r=\"6\" fill=\"#cd2e3a\"/> </g> <g transform=\"rotate(-123.7 191.2 62.2) scale(10.66667)\"> <use xlink:href=\"#c\" width=\"100%\" height=\"100%\"/> <path stroke=\"#fff\" d=\"M0-23.5v3M0 17v3.5m0 3v3\"/> </g> </g>"},
            {"pl_PL", "<g fill-rule=\"evenodd\"> <path fill=\"#fff\" d=\"M640 480H0V0h640z\"/> <path fill=\"#dc143c\" d=\"M640 480H0V240h640z\"/> </g>"},
            {"zh_CN", "<defs><path id=\"cn\" fill=\"#ffde00\" d=\"M-0.6 0.8 0 -1 0.6 0.8 -1 -0.3h2z\"/></defs><path fill=\"#de2910\" d=\"M0 0h640v480H0z\"/><use xlink:href=\"#cn\" width=\"30\" height=\"20\" transform=\"matrix(71.9991 0 0 72 120 120)\"/><use xlink:href=\"#cn\" width=\"30\" height=\"20\" transform=\"matrix(-12.33562 -20.5871 20.58684 -12.33577 240.3 48)\"/><use xlink:href=\"#cn\" width=\"30\" height=\"20\" transform=\"matrix(-3.38573 -23.75998 23.75968 -3.38578 288 95.8)\"/><use xlink:href=\"#cn\" width=\"30\" height=\"20\" transform=\"matrix(6.5991 -23.0749 23.0746 6.59919 288 168)\"/><use xlink:href=\"#cn\" width=\"30\" height=\"20\" transform=\"matrix(14.9991 -18.73557 18.73533 14.99929 240 216)\"/>"}
        };

        public string GetFlag(string name) {
            return _flags.TryGetValue(name, out string value) ? value : FLAG_XX;
        }

        public HtmlReportTemplate(Catalog catalog)
        {
            this.catalog = catalog;
            catalog.Save(Path.Combine(ModSettings.UpdaterPath, ModSettings.CatalogDumpFileName));
            reportCreationTime = DateTime.Now;
            unsubscribe = new List<ModInfo>();
            majorIssues = new List<ModInfo>();
            minorIssues = new List<ModInfo>();
            remarks = new List<ModInfo>();
            nothingToReport = new List<ModInfo>();
            GenerateModInfo();
        }

        private string OptionalUrlLink(string url, bool isLink) => isLink ? HtmlExtensions.A(url, newTab: true) : url;
        
        // todo 0.8 or 0.9 identify if necessary to be read from catalog 
        private string CatalogHeaderText => catalog.ReportHeaderText;

        internal string GetTranslations() {
            Hashtable langs = new Hashtable();
#if !DEBUG_TRANSLATIONS
            var all = Translation.instance.All;
            for (var i = 0; i < all.Length; i++)
            {
                AvailableLanguages.Add(all[i].Code);
                langs.Add(all[i].Code, all[i].HtmlTranslations);
            }
#else
            //check current language, if different add to the list, otherwise en_US only
            string currentLang = Translation.instance.Current.Code;
            if (!currentLang.Equals(Translation.DEFAULT_LANGUAGE_CODE))
            {
                AvailableLanguages.Add(currentLang);
                langs.Add(currentLang, Translation.instance.Current.HtmlTranslations);
            }
            AvailableLanguages.Add(Translation.DEFAULT_LANGUAGE_CODE);
            langs.Add(Translation.DEFAULT_LANGUAGE_CODE, Translation.instance.Fallback.HtmlTranslations);
#endif
            return JSON.JsonEncode(langs);
        }

        internal string GetPreferredLanguage() {
            Logger.Log($"Get lang: {Translation.instance.Current.Code}");
            return Translation.instance.Current.Code;
        }

        private InstalledModInfo[] AllModList()
        {
            List<InstalledModInfo> items = new List<InstalledModInfo>();
            foreach (string subscriptionName in catalog.GetSubscriptionNames())
            {
                foreach (ulong steamID in catalog.GetSubscriptionIDsByName(subscriptionName))
                {
                    Mod catalogMod = catalog.GetMod(steamID);
                    string disabled = (catalogMod.IsDisabled ? "Yes" : "");
                    bool isSteam = steamID > ModSettings.HighestFakeID;
                    string type = (isSteam ? "Steam" : steamID < ModSettings.LowestLocalModID ? "Built-in" : "Local");
                    string localeId = (isSteam ? "HRTC_IS_S" : steamID < ModSettings.LowestLocalModID ? "HRTC_LLMID_BI" : "HRTC_LLMID_L");
                    string url = steamID > ModSettings.HighestFakeID ? Toolkit.GetWorkshopUrl(steamID) : $"{Toolkit.Privacy(catalogMod.ModPath)}";

                    items.Add(new InstalledModInfo(subscriptionName, disabled, type, localeId, isSteam, url));
                }
            }
            return items.ToArray();
        }

        private void GenerateModInfo()
        {
            if (GlobalConfig.Instance.GeneralConfig.ReportSortByName)
            {
                // Report mods sorted by name.
                List<string> AllSubscriptionNames = catalog.GetSubscriptionNames();

                foreach (string name in AllSubscriptionNames)
                {
                    // Get the Steam ID(s) for this mod name. There could be multiple IDs for mods with the same name.
                    foreach (ulong steamID in catalog.GetSubscriptionIDsByName(name))
                    {
                        CreateModText(steamID);
                    }
                }
            }
            else
            {
                // Report mods sorted by Steam ID.
                foreach (ulong steamID in catalog.GetSubscriptionIDs())
                {
                    CreateModText(steamID);
                }
            }
        }

        /// <summary>Creates the report object for a single mod.</summary>
        /// <remarks>The object is added to the relevant collection.</remarks>
        private void CreateModText(ulong steamID)
        {
            Mod subscribedMod = catalog.GetMod(steamID);
            Author subscriptionAuthor = catalog.GetAuthor(subscribedMod.AuthorID, subscribedMod.AuthorUrl);
            string authorName = subscriptionAuthor == null ? "" : subscriptionAuthor.Name;

            ModInfo modInfo = new ModInfo();
            modInfo.authorName = authorName;
            modInfo.modName = subscribedMod.Name;
            modInfo.idString = subscribedMod.IdString(true);
            modInfo.isDisabled = subscribedMod.IsDisabled;
            modInfo.isLocal = subscribedMod.SteamID >= ModSettings.LowestLocalModID && subscribedMod.SteamID <= ModSettings.HighestLocalModID;
            modInfo.isCameraScript = subscribedMod.IsCameraScript;
            if (!modInfo.isLocal)
            {
                modInfo.instability = Instability(subscribedMod);
                modInfo.requiredDlc = RequiredDlc(subscribedMod);
                modInfo.unneededDependencyMod = UnneededDependencyMod2(subscribedMod);
                modInfo.disabled = Disabled(subscribedMod);
                modInfo.successors = Successors(subscribedMod);
                modInfo.stability = Stability(subscribedMod);
                modInfo.compatibilities = Compatibilities(subscribedMod);
                modInfo.requiredMods = RequiredMods(subscribedMod);
                modInfo.statuses = Statuses(subscribedMod, authorRetired: (subscriptionAuthor != null && subscriptionAuthor.Retired));
                var note = ModNote(subscribedMod);
                modInfo.note = note.Value;
                modInfo.noteLocaleId = note.Id;
                modInfo.alternatives = Alternatives(subscribedMod);
                modInfo.reportSeverity = subscribedMod.ReportSeverity;
                modInfo.recommendations = subscribedMod.ReportSeverity <= Enums.ReportSeverity.MajorIssues ? Recommendations2(subscribedMod) : null;
                modInfo.anyIssues = subscribedMod.ReportSeverity == Enums.ReportSeverity.NothingToReport && subscribedMod.Stability > Enums.Stability.NotReviewed;
                modInfo.isCameraScript = subscribedMod.IsCameraScript;
                modInfo.steamUrl = subscribedMod.SteamID > ModSettings.HighestFakeID ? Toolkit.GetWorkshopUrl(steamID) : null;
            }
         
            switch (subscribedMod.ReportSeverity)
            {
                case Enums.ReportSeverity.Unsubscribe:
                    unsubscribe.Add(modInfo);
                    break;
                case Enums.ReportSeverity.MajorIssues:
                    majorIssues.Add(modInfo);
                    break;
                case Enums.ReportSeverity.MinorIssues:
                    minorIssues.Add(modInfo);
                    break;
                case Enums.ReportSeverity.Remarks:
                    remarks.Add(modInfo);
                    break;
                default:
                    nothingToReport.Add(modInfo);
                    break;
            }
        }

        /// <summary>Creates report message for stability issues of a mod and increases the report severity for the mod if appropriate.</summary>
        /// <remarks>Only reports major issues and worse.</remarks>
        /// <returns>Text wrapped in Message object.</returns>
        private Message Instability(Mod subscribedMod)
        {
            switch (subscribedMod.Stability)
            {
                case Enums.Stability.IncompatibleAccordingToWorkshop:
                    subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Unsubscribe);
                    return new Message(){message = "UNSUBSCRIBE! This mod is totally incompatible with the current game version.", messageLocaleId = "HRTC_I_IATW", details = subscribedMod.StabilityNote.Value, detailsLocaleId = subscribedMod.StabilityNote.Id};

                case Enums.Stability.RequiresIncompatibleMod:
                    subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Unsubscribe);
                    return new Message(){message = "UNSUBSCRIBE! This requires a mod that is totally incompatible with the current game version.", messageLocaleId = "HRTC_I_RIM", details = subscribedMod.StabilityNote.Value, detailsLocaleId = subscribedMod.StabilityNote.Id};

                case Enums.Stability.GameBreaking:
                    subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Unsubscribe);
                    return new Message(){message = "UNSUBSCRIBE! This mod breaks the game.", messageLocaleId = "HRTC_I_GB", details = subscribedMod.StabilityNote.Value, detailsLocaleId = subscribedMod.StabilityNote.Id};

                case Enums.Stability.Broken:
                    subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Unsubscribe);
                    return new Message(){message = "Unsubscribe! This mod is broken.", messageLocaleId = "HRTC_I_B", details = subscribedMod.StabilityNote.Value, detailsLocaleId = subscribedMod.StabilityNote.Id};

                case Enums.Stability.MajorIssues:
                    subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.MajorIssues);
                    return new Message(){message = $"Unsubscribe would be wise. This has major issues{(string.IsNullOrEmpty(subscribedMod.StabilityNote.Value) ? ". Check its Workshop page for details." : ":")}", messageLocaleId = "HRTC_I_MAI", localeIdVariables = $"StabilityNote:{subscribedMod.StabilityNote}", details = subscribedMod.StabilityNote.Value, detailsLocaleId = subscribedMod.StabilityNote.Id};

                default:
                    return null;
            }
        }

        /// <summary>Creates report message for the stability of a mod and increases the report severity for the mod if appropriate.</summary>
        /// <remarks>Only reports minor issues and better.</remarks>
        /// <returns>Text wrapped in Message object.</returns>
        private Message Stability(Mod subscribedMod)
        {
            switch (subscribedMod.Stability)
            {
                case Enums.Stability.MinorIssues:
                    subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.MinorIssues);
                    bool hasNote = string.IsNullOrEmpty(subscribedMod.StabilityNote.Value);
                    return new Message()
                    {
                        message = $"This has minor issues{(!hasNote ? ". Check its Workshop page for details." : ":")}",
                        messageLocaleId = $"{(!hasNote ? "HRTC_S_MI": "HRTC_S_MIS")}",
                        localeIdVariables = "StabilityNote: ",
                        details = subscribedMod.StabilityNote.Value,
                        detailsLocaleId = $"{(hasNote ? $"{subscribedMod.StabilityNote.Id}" :"HRTC_I_MAIC")}"
                    };

                case Enums.Stability.UsersReportIssues:
                    subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.MinorIssues);
                    bool hasNote2 = string.IsNullOrEmpty(subscribedMod.StabilityNote.Value);
                    return new Message()
                    {
                        message = $"Users are reporting issues{(!hasNote2 ? ". Check its Workshop page for details." : ": ")}",
                        messageLocaleId = $"{(!hasNote2 ? "HRTC_S_URI": "HRTC_S_URS")}",
                        localeIdVariables = $"StabilityNote: ",
                        details = subscribedMod.StabilityNote.Value,
                        detailsLocaleId = $"{(hasNote2 ? $"{subscribedMod.StabilityNote.Id}" :"HRTC_I_MAIC")}"
                    };
                case Enums.Stability.NotEnoughInformation:
                    subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Remarks);
                    string updatedText = subscribedMod.GameVersion() < Toolkit.CurrentMajorGameVersion() 
                        ? "." 
                        : subscribedMod.GameVersion() == Toolkit.CurrentGameVersion() 
                            ? ", but it was updated for the current game version."
                            : $", but it was updated for game version {subscribedMod.GameVersion().ToString(2)}.";
                    return new Message()
                    {
                        message = $"There is not enough information about this mod to know if it works well{updatedText}",
                        messageLocaleId = "HRTC_S_NEI",
                        localeIdVariables = $"{APPEND_TRANSLATED_VALUE_ID}:{(subscribedMod.GameVersion() == Toolkit.CurrentGameVersion() ? "HRTC_S_NRC" :"")}",
                        details = subscribedMod.StabilityNote.Value,
                        detailsLocaleId = subscribedMod.StabilityNote.Id
                    };
                case Enums.Stability.Stable:
                    subscribedMod.IncreaseReportSeverity(string.IsNullOrEmpty(subscribedMod.StabilityNote.Value) ? Enums.ReportSeverity.NothingToReport : Enums.ReportSeverity.Remarks);
                    return new Message()
                    {
                        message = $"This is compatible with the current game version.",
                        messageLocaleId = "HRTC_S_S",
                        details = subscribedMod.StabilityNote.Value,
                        detailsLocaleId = subscribedMod.StabilityNote.Id
                    };
                case Enums.Stability.NotReviewed:
                case Enums.Stability.Undefined:
                    string updatedText2 = subscribedMod.GameVersion() < Toolkit.CurrentMajorGameVersion() 
                        ? "."
                        : subscribedMod.GameVersion() == Toolkit.CurrentGameVersion() 
                            ? ", but it was updated for the current game version." 
                            : $", but it was updated for game version {subscribedMod.GameVersion().ToString(2)}.";
                    return new Message() { message = $"This mod has not been reviewed yet{updatedText2}", messageLocaleId = "HRTC_S_NR", localeIdVariables = ""};
                default:
                    return null;
            }
        }


        /// <summary>Creates report MessageList object for the statuses of a mod and increases the report severity for the mod if appropriate.</summary>
        /// <remarks>Also reported: retired author. DependencyMod has its own method. ModNamesDiffer is reported in the mod note (at Catalog.ScanSubscriptions()).
        ///          Not reported: UnlistedInWorkshop, SourceObfuscated.</remarks>
        /// <returns>Message list, or an empty string if no reported status found.</returns>
        private MessageList Statuses(Mod subscribedMod, bool authorRetired)
        {
            var nestedItem = new MessageList() { messages = new List<Message>() };
            
            if (subscribedMod.Statuses.Contains(Enums.Status.Obsolete))
            {
                subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Unsubscribe);
                nestedItem.title = "Unsubscribe this. It is no longer needed.";
                nestedItem.titleLocaleId = "HRTC_S_O";
            }
            else if (subscribedMod.Statuses.Contains(Enums.Status.RemovedFromWorkshop))
            {
                subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.MajorIssues);
                nestedItem.title = "Unsubscribe would be wise. This is no longer available on the Steam Workshop.";
                nestedItem.titleLocaleId = "HRTC_S_RFW";
            }
            else if (subscribedMod.Statuses.Contains(Enums.Status.Deprecated))
            {
                subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.MajorIssues);
                nestedItem.title = "Unsubscribe would be wise. This is deprecated and no longer supported by the author.";
                nestedItem.titleLocaleId = "HRTC_S_D";
            }
            else if (subscribedMod.Statuses.Contains(Enums.Status.Abandoned))
            {
                nestedItem.title = authorRetired 
                    ? "This seems to be abandoned and the author seems retired. Future updates are unlikely."
                    : "This seems to be abandoned. Future updates are unlikely.";
                nestedItem.titleLocaleId = authorRetired ? "HRTC_S_A" : "HRTC_S_NIAR";
            }
            else if (authorRetired)
            {
                nestedItem.title = "The author seems to be retired. Future updates are unlikely.";
                nestedItem.titleLocaleId = "HRTC_S_AR";
            }

            if (subscribedMod.ReportSeverity < Enums.ReportSeverity.Unsubscribe)
            {
                // Several statuses only listed if there are no breaking issues.
                if (subscribedMod.Statuses.Contains(Enums.Status.Reupload))
                {
                    subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Unsubscribe);
                    nestedItem.messages.Add(new Message(){message = "Unsubscribe this. It is a re-upload of another mod, use that one instead (or its successor).", messageLocaleId = "HRTC_RU_U"});
                }

                if (subscribedMod.Statuses.Contains(Enums.Status.NoDescription))
                {
                    subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.MinorIssues);
                    nestedItem.messages.Add(new Message(){message = "This has no description on the Steam Workshop. Support from the author is unlikely.", messageLocaleId = "HRTC_ND_MI"});
                }

                if (subscribedMod.Statuses.Contains(Enums.Status.NoCommentSection))
                {
                    subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.MinorIssues);
                    nestedItem.messages.Add(new Message(){message = "This mod has the comment section disabled on the Steam Workshop, making it hard to see if other users are experiencing issues. " +
                        "Use with caution.", messageLocaleId = "HRTC_NCS_MI"});
                }

                if (subscribedMod.Statuses.Contains(Enums.Status.BreaksEditors))
                {
                    subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Remarks);
                    nestedItem.messages.Add(new Message(){message = "If you use the asset editor and/or map editor, this may give serious issues.", messageLocaleId = "HRTC_BE_R"});
                }

                if (subscribedMod.Statuses.Contains(Enums.Status.ModForModders))
                {
                    nestedItem.messages.Add(new Message(){message = "This is only needed for modders. Regular users don't need this one.", messageLocaleId = "HRTC_S_MFM"});
                }

                if (subscribedMod.Statuses.Contains(Enums.Status.TestVersion))
                {
                    nestedItem.messages.Add(new Message(){message = "This is a test version" +
                        (subscribedMod.Alternatives.Any() ? ". If you don't have a specific reason to use it, you'd better use the stable version instead." :
                        subscribedMod.Stability == Enums.Stability.Stable ? ", but is considered quite stable." : "."), 
                        messageLocaleId = subscribedMod.Alternatives.Any() ? "HRTC_S_TVA" : subscribedMod.Stability == Enums.Stability.Stable ? "HRTC_SS_TV": string.Empty});
                }

                if (subscribedMod.Statuses.Contains(Enums.Status.MusicCopyrightFree))
                {
                    nestedItem.messages.Add(new Message(){message = "The included music is said to be copyright-free and safe for streaming. Some restrictions might still apply though.", messageLocaleId = "HRTC_S_MCF"});
                }
                else if (subscribedMod.Statuses.Contains(Enums.Status.MusicCopyrighted))
                {
                    nestedItem.messages.Add(new Message(){message = "This includes copyrighted music and should not be used for streaming.", messageLocaleId = "HRTC_S_MC"});
                }
                else if (subscribedMod.Statuses.Contains(Enums.Status.MusicCopyrightUnknown))
                {
                    nestedItem.messages.Add(new Message(){message = "This includes music with unknown copyright status. Safer not to use it for streaming.", messageLocaleId = "HRTC_S_MCU"});
                }
            }

            if (subscribedMod.Statuses.Contains(Enums.Status.SavesCantLoadWithout))
            {
                    nestedItem.messages.Add(new Message(){message = "NOTE: After using this mod, savegames won't (easily) load without it anymore.", messageLocaleId = "HRTC_S_SCLW"});
            }

            bool abandoned = subscribedMod.Statuses.Contains(Enums.Status.Obsolete) || subscribedMod.Statuses.Contains(Enums.Status.Deprecated) ||
                subscribedMod.Statuses.Contains(Enums.Status.RemovedFromWorkshop) || subscribedMod.Statuses.Contains(Enums.Status.Abandoned) ||
                (subscribedMod.Stability == Enums.Stability.IncompatibleAccordingToWorkshop) || authorRetired;

            if (abandoned && string.IsNullOrEmpty(subscribedMod.SourceUrl) && !subscribedMod.Statuses.Contains(Enums.Status.SourceBundled))
            {
                nestedItem.messages.Add(new Message(){message = "No public source code found, making it hard to continue by another modder.", messageLocaleId = "HRTC_A_SURL"});
            }
            else if (abandoned && subscribedMod.Statuses.Contains(Enums.Status.SourceNotUpdated))
            {
                nestedItem.messages.Add(new Message(){message = "Published source seems out of date, making it hard to continue by another modder.", messageLocaleId = "HRTC_A_SNU"});
            }

            if (!string.IsNullOrEmpty(nestedItem.title) || nestedItem.messages.Count > 0)
            {
                subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Remarks);
            }

            return nestedItem;
        }

        /// <summary>Creates report text for an unneeded dependency mod and increases the report severity for the mod if appropriate.</summary>
        /// <remarks>If this mod is a member of a group, all group members are considered for this check.</remarks>
        /// <returns>Text wrapped in Message object or null if this is not a dependency mod or if another subscription has this mod as required.</returns>
        private Message UnneededDependencyMod2(Mod subscribedMod)
        {
            if (!subscribedMod.Statuses.Contains(Enums.Status.DependencyMod))
            {
                return null;
            }

            // Check if any of the mods that need this is actually subscribed, enabled or not. If this is in a group, check all group members. Exit if any is needed.
            if (catalog.IsGroupMember(subscribedMod.SteamID))
            {
                foreach (ulong groupMemberID in catalog.GetThisModsGroup(subscribedMod.SteamID).GroupMembers)
                {
                    if (IsModNeeded(groupMemberID))
                    {
                        // Group member is needed. No need to check other group members.
                        return null;
                    }
                }
            }
            else if (IsModNeeded(subscribedMod.SteamID))
            {
                return null;
            }

            if (catalog.IsValidID(ModSettings.LowestLocalModID))
            {
                subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Remarks);

                return new Message()
                {
                    message = "Unsubscribe this unless it's needed for one of your local mods. " +
                        "None of your Steam Workshop mods need this, and it doesn't provide any functionality on its own.",
                    messageLocaleId = "HRTC_IMN_RL|HRTC_IMN_R"
                };
            }
            else
            {
                subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Unsubscribe);
                return new Message() { message = "Unsubscribe this. It is only needed for mods you don't have, and it doesn't provide any functionality on its own.", messageLocaleId = "HRTC_IMN_U"};
            }
        }


        /// <summary>Checks if any of the mods that need this is actually subscribed, enabled or not.</summary>
        /// <returns>True if a mod needs this, otherwise false.</returns>
        private bool IsModNeeded(ulong SteamID)
        {
            // Check if any of the mods that need this is actually subscribed, enabled or not.
            List<Mod> ModsRequiringThis = catalog.Mods.FindAll(x => x.RequiredMods.Contains(SteamID));

            foreach (Mod mod in ModsRequiringThis)
            {
                if (catalog.GetSubscription(mod.SteamID) != null)
                {
                    // Found a subscribed mod that needs this.
                    return true;
                }
            }

            return false;
        }

        /// <summary>Creates report text for a disabled mod and increases the report severity for the mod if appropriate.</summary>
        /// <returns>Text wrapped in Message object, or an empty string if not disabled or if this mod works while disabled.</returns>
        private Message Disabled(Mod subscribedMod)
        {
            if (!subscribedMod.IsDisabled || subscribedMod.Statuses.Contains(Enums.Status.WorksWhenDisabled))
            {
                return null;
            }

            subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Unsubscribe);

            return new Message() {message = "Enable this if you want to use it, or unsubscribe it. Disabled mods can cause issues.", messageLocaleId = "HRTC_ID_U"};
        }


        /// <summary>Creates report text for a mod not and increases the report severity for the mod if appropriate.</summary>
        /// <returns>Formatted text, or an empty string if no mod note exists.</returns>
        private ElementWithId ModNote(Mod subscribedMod)
        {
            if (subscribedMod.Note == null || string.IsNullOrEmpty(subscribedMod.Note.Value))
            {
                return new ElementWithId();
            }

            subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Remarks);

            return subscribedMod.Note;
        }


        /// <summary>Creates report text for missing DLCs for a mod and increases the report severity for the mod if appropriate.</summary>
        /// <returns>Text wrapped in Message object, or null if no DLC is required or if all required DLCs are installed.</returns>
        private MessageList RequiredDlc(Mod subscribedMod)
        {
            var dlcs = new MessageList()
            {
                title = "Unsubscribe this. It requires DLC you don't have:",
                titleLocaleId = "HRTC_RDLC_U",
                messages = new List<Message>()
            };

            foreach (Enums.Dlc dlc in subscribedMod.RequiredDlcs)
            {
                if (!PlatformService.IsDlcInstalled((uint)dlc))
                {
                    // Add the missing DLC.
                    dlcs.messages.Add(new Message() {message = Toolkit.ConvertDlcToString(dlc)});
                }
            }

            if (dlcs.messages.Count == 0)
            {
                return null;
            }

            subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Unsubscribe);

            return dlcs;
        }
        
        /// <summary>Creates report text for missing 'required mods' for a mod and increases the report severity for the mod if appropriate.</summary>
        /// <remarks>If a required mod is not subscribed but in a group, the other group members are checked. 
        ///          Required mods that are disabled are mentioned as such.</remarks>
        /// <returns>MessageList object filled with messages, or null if this requires no other mods or all required mods are subscribed and enabled.</returns>
        private MessageList RequiredMods(Mod subscribedMod)
        {
            var item = new MessageList()
            {
                title = "This mod requires other mods you don't have, or which are not enabled:",
                titleLocaleId = "HRTC_RM_SM",
                messages = new List<Message>()
            };

            foreach (ulong steamID in subscribedMod.RequiredMods)
            {
                if (catalog.IsValidID(steamID))
                {
                    var mod = ModAndGroupItem(steamID);
                    if (mod != null)
                    {
                        item.messages.Add(mod);
                    }
                }
                else
                {
                    // Mod not found in the catalog, which should not happen unless bugs or a manual edit of the catalog.
                    Logger.Log($"Required mod {steamID} not found in catalog.", Logger.Debug);
                    item.messages.Add(new Message() {message = $"[Steam ID {steamID,10}]"});
                }
            }

            if (item.messages.Count == 0)
            {
                return null;
            }

            subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.MajorIssues);

            return item;
        }

        /// <summary>Creates report text for recommended mods for a mod and increases the report severity for the mod if appropriate.</summary>
        /// <remarks>If a recommended mod is not subscribed but in a group, the other group members are checked. 
        ///          Recommended mods that are disabled are mentioned as such.</remarks>
        /// <returns>MessageList object filled with messages, or null if this mod has no recommendations or all recommended mods are subscribed and enabled.</returns>
        private MessageList Recommendations2(Mod subscribedMod)
        {
            MessageList list = new MessageList
            {
                title = "The author or the users of this mod recommend using the following as well:",
                titleLocaleId = "HRTC_R2_SM",
                messages = new List<Message>()
            };
            
            foreach (ulong steamID in subscribedMod.Recommendations)
            {
                if (catalog.IsValidID(steamID))
                {
                    var item = ModAndGroupItem(steamID);
                    if (item != null)
                    {
                        list.messages.Add(item);
                    }
                }
                else
                {
                    // Mod not found in the catalog, which should not happen unless bugs or a manual edit of the catalog.
                    Logger.Log($"Recommended mod {steamID} not found in catalog.", Logger.Debug);

                    list.messages.Add(new Message() {message = $"[Steam ID {steamID,10}]"});
                }
            }

            if (list.messages.Count == 0)
            {
                return null;
            }

            subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Remarks);

            return list;
        }
        
        /// <summary>Checks if a mod or any member of its group is subscribed and enabled.</summary>
        /// <returns>A Message with text for the report, or null if the mod or another group member is subscribed and enabled.</returns>
        private Message ModAndGroupItem(ulong steamID)
        {
            Mod catalogMod = catalog.GetSubscription(steamID);

            if (catalogMod != null && (!catalogMod.IsDisabled || catalogMod.Statuses.Contains(Enums.Status.WorksWhenDisabled)))
            {
                // Mod is subscribed and enabled (or works when disabled). Don't report.
                return null;
            }
            catalogMod = catalog.GetMod(steamID);

            if (catalogMod.IsDisabled)
            {
                // Mod is subscribed and disabled. Report as "missing", without Workshop page.
                return new Message() { message = catalog.GetMod(steamID).ToString(hideFakeID: true, nameFirst: true, html: true) };
            }

            if (!catalog.IsGroupMember(steamID))
            {
                // Mod is not subscribed and not in a group. Report as missing with Workshop page.
                return new Message() { message = catalogMod.NameWithIDAsLink(true, false) };
            }
            
            // Mod is not subscribed but in a group. Check if another group member is subscribed.
            foreach (ulong groupMemberID in catalog.GetThisModsGroup(steamID).GroupMembers)
            {
                Mod groupMember = catalog.GetSubscription(groupMemberID);

                if (groupMember != null)
                {
                    // Group member is subscribed. No need to check other group members, but report as "missing" if disabled (unless it works when disabled).
                    if (!groupMember.IsDisabled || groupMember.Statuses.Contains(Enums.Status.WorksWhenDisabled))
                    {
                        return null;
                    }
                    return new Message() { message = groupMember.ToString(hideFakeID: true, nameFirst: true, html: true) };
                }
            }

            // No group member is subscribed. Report original mod as missing.
            return new Message() { message = catalogMod.NameWithIDAsLink(true, false) };
        }

        /// <summary>Creates report text for successors of a mod and increases the report severity for the mod if appropriate.</summary>
        /// <returns>Text wrapped in Message object, or null if this mod has no successors.</returns>
        private MessageList Successors(Mod subscribedMod)
        {
            if (!subscribedMod.Successors.Any())
            {
                return null;
            }

            MessageList successors = new MessageList();
            successors.title = (subscribedMod.Successors.Count == 1)
                ? "The successor of this mod is:"
                : "This is succeeded by any of the following (pick one, not all):";
            successors.titleLocaleId = (subscribedMod.Successors.Count == 1) ? "HRTC_S_SMC" : "HRTC_SS_SM";

            List<Message> successorsCollection = new List<Message>();
            successors.messages = successorsCollection;

            foreach (ulong steamID in subscribedMod.Successors)
            {
                Message s = new Message();
                if (catalog.IsValidID(steamID))
                {
                    if (catalog.GetSubscription(steamID) != null)
                    {
                        subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Unsubscribe);

                        s.message = "Unsubscribe this. It is succeeded by a mod you already have:";
                        s.messageLocaleId = "HRTC_S_U";
                        s.details = catalog.GetMod(steamID).ToString(hideFakeID: true);
                        s.detailsLocaleId = " ";
                        s.detailsLocalized = Toolkit.EscapeHtml(catalog.GetMod(steamID).ToString(hideFakeID: true, html: true));
                        successorsCollection.Add(s);
                        return successors;
                    }

                    s.message = catalog.GetMod(steamID).ToString(hideFakeID: true);
                    s.localeIdVariables = $"{Toolkit.EscapeHtml(HtmlExtensions.A(Toolkit.GetWorkshopUrl(steamID), newTab: true))}";
                    s.details = $"Workshop page: {HtmlExtensions.A(Toolkit.GetWorkshopUrl(steamID), newTab: true)}";
                    s.detailsLocaleId = "HRTC_S_WSP";
                    s.detailsValue = Toolkit.EscapeHtml(HtmlExtensions.A(Toolkit.GetWorkshopUrl(steamID), newTab: true));
                    successorsCollection.Add(s);
                }
                else
                {
                    // Mod not found in the catalog, which should not happen unless bugs or a manual edit of the catalog.
                    Logger.Log($"Successor mod {steamID} not found in catalog.", Logger.Debug);

                    s.message = $"[Steam ID {steamID,10}]";
                    successorsCollection.Add(s);
                }
            }

            subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.MinorIssues);

            return successors;
        }
        
        /// <summary>Creates report text for alternatives for a mod and increases the report severity for the mod if appropriate.</summary>
        /// <returns>Text wrapped in Message object, or null if this mod has no alternatives.</returns>
        private MessageList Alternatives(Mod subscribedMod)
        {
            if (!subscribedMod.Alternatives.Any())
            {
                return null;
            }
            
            var result = new MessageList()
            {
                title = subscribedMod.Alternatives.Count == 1 
                    ? "An alternative you could use:"
                    : "Some alternatives for this are (pick one, not all):",
                titleLocaleId = subscribedMod.Alternatives.Count == 1 ? "HRTC_A_SMC": "HRTC_A_SM",
                messages = new List<Message>()
            };

            foreach (ulong steamID in subscribedMod.Alternatives)
            {
                Mod alternativeMod = catalog.GetSubscription(steamID);

                if (alternativeMod != null && (!alternativeMod.IsDisabled || alternativeMod.Statuses.Contains(Enums.Status.WorksWhenDisabled)))
                {
                    // Already subscribed, don't report any.
                    return null;
                }

                if (catalog.IsValidID(steamID))
                {
                    result.messages.Add(new Message(){message = catalog.GetMod(steamID).NameWithIDAsLink(true, false)});
                }
                else
                {
                    // Mod not found in the catalog, which should not happen unless bugs or a manual edit of the catalog.
                    Logger.Log($"Alternative mod {steamID} not found in catalog.", Logger.Debug);

                    result.messages.Add(new Message(){message = $"[Steam ID {steamID,10}]"});
                }
            }

            subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Remarks);

            return result;
        }
        
        /// <summary>Creates report text for compatibility issues with other subscribed mods, and increases the report severity for the mod if appropriate.</summary>
        /// <remarks>Result could be multiple mods with multiple statuses. Not reported: CompatibleAccordingToAuthor.</remarks>
        /// <returns>Text wrapped in Message object, or null if there are no known compatibility issues.</returns>
        private MessageList Compatibilities(Mod subscribedMod)
        {

            var result = new MessageList()
            {
                title = string.Empty,
                messages = new List<Message>()
            };
            
            foreach (Compatibility compatibility in catalog.GetSubscriptionCompatibilities(subscribedMod.SteamID))
            {
                ulong otherModID = (subscribedMod.SteamID == compatibility.FirstModID) ? compatibility.SecondModID : compatibility.FirstModID;

                Mod otherMod = catalog.GetMod(otherModID);
                if (subscribedMod.Successors.Contains(otherModID) || otherMod == null || otherMod.Successors.Contains(subscribedMod.SteamID))
                {
                    // Don't mention the incompatibility if either mod is the others successor. The succeeded mod will already be mentioned in 'Unsubscribe' severity.
                    continue;
                }

                string otherModString = catalog.GetMod(otherModID).NameWithIDAsLink(false, idFirst: false);
                string escapedModString = Toolkit.EscapeHtml(otherModString);

                Message item = new Message();
                switch (compatibility.Status)
                {
                    case Enums.CompatibilityStatus.SameModDifferentReleaseType:
                        subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Unsubscribe);
                        item.message = "Unsubscribe either this or the other edition of the same mod:";
                        item.messageLocaleId = "HRTC_IRS_SMDRT";
                        item.details = $"{otherModString} {compatibility.Note}";
                        item.detailsLocalized =  $"{escapedModString} {(string.IsNullOrEmpty(compatibility.Note.Id) ? compatibility.Note.Value: string.Empty)}";
                        item.detailsLocaleId = compatibility.Note.Id;
                        break;

                    case Enums.CompatibilityStatus.SameFunctionality:
                        subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Unsubscribe);
                        item.message = "Unsubscribe either this or the following incompatible mod with similar functionality:";
                        item.messageLocaleId = "HRTC_IRS_SF";
                        item.details = $"{otherModString} {compatibility.Note}";
                        item.detailsLocalized = $"{escapedModString} {(string.IsNullOrEmpty(compatibility.Note.Id) ? compatibility.Note.Value: string.Empty)}";
                        item.detailsLocaleId = compatibility.Note.Id;
                        break;

                    case Enums.CompatibilityStatus.IncompatibleAccordingToAuthor:
                        subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Unsubscribe);
                        item.message = "Unsubscribe either this one or the following mod it's incompatible with:";
                        item.messageLocaleId = "HRTC_IRS_IATA";
                        item.details = $"{otherModString} {compatibility.Note}";
                        item.detailsLocalized = $"{escapedModString} {(string.IsNullOrEmpty(compatibility.Note.Id) ? compatibility.Note.Value: string.Empty)}";
                        item.detailsLocaleId = compatibility.Note.Id;
                        break;

                    case Enums.CompatibilityStatus.IncompatibleAccordingToUsers:
                        subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.MajorIssues);
                        item.message = "Users report an incompatibility with:";
                        item.messageLocaleId = "HRTC_IRS_IATU";
                        item.details = $"{otherModString} {compatibility.Note}";
                        item.detailsLocalized = $"{escapedModString} {(string.IsNullOrEmpty(compatibility.Note.Id) ? compatibility.Note.Value: string.Empty)}";
                        item.detailsLocaleId = $"{compatibility.Note.Id}";
                        break;

                    case Enums.CompatibilityStatus.MajorIssues:
                        subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.MajorIssues);
                        item.message = "This has major issues with:";
                        item.messageLocaleId = "HRTC_IRS_MAI";
                        item.details = $"{otherModString} {compatibility.Note}";
                        item.detailsLocalized = $"{escapedModString} {(string.IsNullOrEmpty(compatibility.Note.Id) ? compatibility.Note.Value: string.Empty)}";
                        item.detailsLocaleId = compatibility.Note.Id;
                        break;

                    case Enums.CompatibilityStatus.MinorIssues:
                        subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.MinorIssues);
                        item.message = "This has minor issues with:";
                        item.messageLocaleId = "HRTC_IRS_MI";
                        item.details = $"{otherModString} {compatibility.Note}";
                        item.detailsLocalized = $"{escapedModString} {(string.IsNullOrEmpty(compatibility.Note.Id) ? compatibility.Note.Value: string.Empty)}";
                        item.detailsLocaleId = compatibility.Note.Id;
                        break;

                    case Enums.CompatibilityStatus.RequiresSpecificSettings:
                        subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Remarks);
                        item.message = "This requires specific configuration to work together with:";
                        item.messageLocaleId = "HRTC_IRS_RSS";
                        item.details = $"{otherModString} {compatibility.Note}";
                        item.detailsLocalized = $"{escapedModString} {(string.IsNullOrEmpty(compatibility.Note.Id) ? compatibility.Note.Value: string.Empty)}";
                        item.detailsLocaleId = compatibility.Note.Id;
                        break;

                    case Enums.CompatibilityStatus.SameFunctionalityCompatible:
                        subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Remarks);
                        item.message = "This has very similar functionality, but is still compatible with (do you need both?):";
                        item.messageLocaleId = "HRTC_IRS_SFC";
                        item.details = $"{otherModString} {compatibility.Note}";
                        item.detailsLocalized = $"{escapedModString} {(string.IsNullOrEmpty(compatibility.Note.Id) ? compatibility.Note.Value: string.Empty)}";
                        item.detailsLocaleId = compatibility.Note.Id;
                        break;

                    case Enums.CompatibilityStatus.CompatibleAccordingToAuthor:
                        if (compatibility.Note != null && !string.IsNullOrEmpty(compatibility.Note.Value))
                        {
                            subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Remarks);
                            item.message = "This is compatible with:";
                            item.messageLocaleId = "HRTC_IRS_CATA";
                            item.details = $"{otherModString} {compatibility.Note}";
                            item.detailsLocalized = $"{escapedModString} {(string.IsNullOrEmpty(compatibility.Note.Id) ? compatibility.Note.Value: string.Empty)}";
                            item.detailsLocaleId = $"{compatibility.Note.Id}";
                        }
                        break;

                    default:
                        break;
                }
                
                result.messages.Add(item);
            }

            return result;
        }

        private class ModInfo
        {
            public bool isLocal;
            public string authorName;
            public string modName;
            public string idString;
            public bool isDisabled;
            public bool isCameraScript;
            public Enums.ReportSeverity reportSeverity;
            public Message instability;
            public MessageList requiredDlc;
            public Message unneededDependencyMod;
            public Message disabled;
            public MessageList successors;
            public Message stability;
            public MessageList compatibilities;
            public MessageList requiredMods;
            public MessageList statuses;
            public string note;
            public string noteLocaleId;
            public MessageList alternatives;
            public MessageList recommendations;
            public bool anyIssues;
            public string steamUrl;
        }

        private class InstalledModInfo
        {
            public string disabled;
            public bool isSteam;

            public string subscriptionName;
            public string type;
            public string typeLocaleID;
            public string url;

            internal InstalledModInfo(string subscriptionName, string disabled, string type, string typeLocaleID, bool isSteam, string url)
            {
                this.subscriptionName = subscriptionName;
                this.disabled = disabled;
                this.type = type;
                this.typeLocaleID = typeLocaleID;
                this.isSteam = isSteam;
                this.url = url;
            }
        }
    }

    internal class MessageList
    {
        public string title;
        public string titleLocaleId;
        public List<Message> messages;
    }

    internal class Message
    {
        public string message;
        public string messageLocaleId;
        public string messageLocalized;
        public string localeIdVariables;
        public string details;
        public string detailsLocaleId;
        public string detailsLocalized;
        public string detailsValue;
    }
}
