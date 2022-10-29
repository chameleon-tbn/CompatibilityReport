using System;
using System.Diagnostics;
using System.IO;
using ColossalFramework.PlatformServices;
using CompatibilityReport.CatalogData;
using CompatibilityReport.Reporter;
using CompatibilityReport.Settings.ConfigData;
using CompatibilityReport.UI;
using CompatibilityReport.Updater;
using CompatibilityReport.Util;
using UnityEngine.SceneManagement;

namespace CompatibilityReport.Settings
{
    internal static class SettingsManager
    {

        public static event Action eventCatalogUpdated;
        private static bool SteamOverlayAvailable
            => PlatformService.platformType == PlatformType.Steam &&
                PlatformService.IsOverlayEnabled();

        private static void OpenURL(string url) {
            if (string.IsNullOrEmpty(url)) return;

            bool useSteamOverlay =
                SteamOverlayAvailable &&
                GlobalConfig.Instance.GeneralConfig.OpenHtmlReportInSteamOverlay;

            if (useSteamOverlay) {
                PlatformService.ActivateGameOverlayToWebPage(url);
            } else {
                Process.Start(url);
            }
        }

        internal static void OnDownloadOptionChanged(int sel)
        {           
            GeneralConfig config = GlobalConfig.Instance.GeneralConfig;
            if (config.DownloadFrequency != sel)
            {
                config.DownloadFrequency = sel;
                GlobalConfig.WriteConfig();
            }
        }

        /// <summary>
        /// Notifies subscribers about updated catalog (mostly UI)
        /// </summary>
        internal static void NotifyCatalogUpdated()
        {
            eventCatalogUpdated?.Invoke();
        }

        internal static void OnDownloadCatalog()
        {
            Catalog.Load(true);
        }

        internal static void OnOpenReports()
        {
            GeneralConfig config = GlobalConfig.Instance.GeneralConfig;
            if (config.HtmlReport)
            {
                OpenURL(Path.Combine(config.ReportPath, ModSettings.ReportHtmlFileName));
            }
            if (config.TextReport)
            {
                OpenURL(Path.Combine(config.ReportPath, ModSettings.ReportTextFileName));
            }
        }

        internal static void OnReportTypeChange(int sel)
        {
            GeneralConfig config = GlobalConfig.Instance.GeneralConfig;
            config.ReportType = sel;
            switch (sel)
            {
                case 0:
                    config.TextReport = false;
                    config.HtmlReport = true;
                    break;
                case 1:
                    config.TextReport = true;
                    config.HtmlReport = false;
                    break;
                case 2:
                    config.TextReport = true;
                    config.HtmlReport = true;
                    break;
                default:
                    config.TextReport = true;
                    config.HtmlReport = false;
                    break;
            }
            GlobalConfig.WriteConfig();
        }

        internal static void OnChangeReportPath(string text)
        {
            string currentPath = GlobalConfig.Instance.GeneralConfig.ReportPath;
            if (currentPath != text)
            {
                GlobalConfig.Instance.GeneralConfig.ReportPath = text;
                GlobalConfig.WriteConfig();
            }
        }

        internal static void OnResetReportPath()
        {
            GlobalConfig.Instance.GeneralConfig.ReportPath = GeneralConfig.DefaultReportPath;
            GlobalConfig.WriteConfig();
        }

        internal static void OnGenerateReports()
        {
            Report.Create(SceneManager.GetActiveScene().name, force: true);
        }

        internal static void OnResetAllSettings()
        {
            GlobalConfig.Reset();
        }

        public static void OnReloadCatalogFromDisk()
        {
            
        }
#if CATALOG_DOWNLOAD
        internal static void OnUploadCatalog(string username, string password, Action<bool> onCompleted) {
            onCompleted(Toolkit.Upload(ModSettings.CatalogUrl, Path.Combine(ModSettings.UpdaterPath, ModSettings.UploadCatalogFileName), username, password));
        }
#endif

        public static void OnOpenHtmlReportInSteamChanged(bool ischecked)
        {
            GeneralConfig config = GlobalConfig.Instance.GeneralConfig;
            if (config.OpenHtmlReportInSteamOverlay != ischecked)
            {
                config.OpenHtmlReportInSteamOverlay = ischecked;
                GlobalConfig.WriteConfig();
            }
        }

        public static void OnStartWebCrawler(ProgressMonitorUI progressUI, bool quick = false)
        {
            ProgressMonitor monitor = new ProgressMonitor(progressUI);
            progressUI.Title = "Web Crawler progress";
            monitor.PushMessage("Web Crawler: Loading Catalog...");
            monitor.eventDisposed += () => {
                progressUI.Progress = 1f;
                progressUI.ProgressText = "Processing done.";
                progressUI.ForceClose();
            };
            progressUI.StartCoroutine(WebCrawler.StartWithProgress(Catalog.Load(true), monitor, quick));
        }

        public static void OnStartUpdater(ProgressMonitorUI progressUI)
        {
            ProgressMonitor monitor = new ProgressMonitor(progressUI);
            monitor.eventDisposed += () => {
                progressUI.Progress = 1f;
                progressUI.ProgressText = "Processing done.";
                progressUI.ForceClose();
            };
            progressUI.Title = "Updater progress";
            progressUI.StartCoroutine(CatalogUpdater.StartWithProgress(monitor));
        }

        public static void OnOneTimeAction(ProgressMonitorUI progressUI)
        {
            ProgressMonitor monitor = new ProgressMonitor(progressUI);
            monitor.eventDisposed += () => {
                progressUI.Progress = 1f;
                progressUI.ProgressText = "Processing done.";
                progressUI.ForceClose();
            };
            progressUI.Title = "One-Time-Action progress";
            progressUI.StartCoroutine(OneTimeAction.Start(monitor));
        }

        internal static void CleanupEvents()
        {
            eventCatalogUpdated = null;
        }
    }
}
