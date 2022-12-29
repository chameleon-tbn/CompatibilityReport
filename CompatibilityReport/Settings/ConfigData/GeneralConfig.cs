using System;
using CompatibilityReport.Translations;

namespace CompatibilityReport.Settings.ConfigData
{
    [Serializable]
    public class GeneralConfig
    {
        private const int MinimalTextReportWidth = 90;
        internal const string GAME_DEFAULT_LANG = "game_language";
        internal static string DefaultReportPath { get; } = UnityEngine.Application.dataPath;

        public string Language { get; set; } = GAME_DEFAULT_LANG; //game default language

        public int Version { get; set; }
 
        /// <summary>
        /// Configurable path where html and text reports will be stored
        /// </summary>
        public string ReportPath { get; set; } = DefaultReportPath;
        
        public bool ReportSortByName { get; set; } = true;
        
        public int TextReportWidth { get; set; } = MinimalTextReportWidth;
        
        /// <summary>
        /// Generate html report
        /// </summary>
        public bool HtmlReport { get; set; } = false;
        /// <summary>
        /// Generate text report, will be force if html report disabled
        /// </summary>
        public bool TextReport { get; set; } = true;
        
        /// <summary>
        /// Open Html report using Steam Overlay if available
        /// </summary>
        public bool OpenHtmlReportInSteamOverlay { get; set; } = false;
        
        /// <summary>
        /// "text", "html", "text and html"
        /// </summary>
        public int ReportType { get; set; } = 0;
        
        /// <summary>
        /// 0: "Once a week", 1: "Never (on-demand only) - not recommended!"
        /// </summary>
        public int DownloadFrequency { get; set; } = 0;

        public static string DownloadFrequencyToString(int frequency)
        {
            switch (frequency)
            {
                case 0:
                    return "Once a day";
                case 1:
                    return "Once a week";
                case 2:
                    return "Never (on-demand only)";
                default:
                    return $"Not supported: {frequency}";
            }
        }
    }
}
