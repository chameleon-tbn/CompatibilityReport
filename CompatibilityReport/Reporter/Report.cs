using ColossalFramework.PlatformServices;
using ColossalFramework.Plugins;
using CompatibilityReport.CatalogData;
using CompatibilityReport.Util;

namespace CompatibilityReport.Reporter
{
    public static class Report
    {
        private static bool hasRun;


        /// <summary>Starts the reporter to create a text and/or HTML report.</summary>
        public static void Create(string scene)
        {
            if (hasRun)
            {
                return;
            }
            
            hasRun = true;

            // Todo 0.5 Remove this one-time only cleanup action.
            try
            {
                System.IO.File.Delete(System.IO.Path.Combine(ColossalFramework.IO.DataLocation.applicationBase, "Compatibility Report.txt"));
                System.IO.File.Delete(System.IO.Path.Combine(ColossalFramework.IO.DataLocation.applicationBase, "Compatibility Report.txt.old"));
                System.IO.File.Delete(System.IO.Path.Combine(UnityEngine.Application.dataPath, "CompatibilityReport.txt.old"));
                System.IO.File.Delete(System.IO.Path.Combine(UnityEngine.Application.dataPath, "CompatibilityReport.log"));
                System.IO.File.Delete(System.IO.Path.Combine(UnityEngine.Application.dataPath, "CompatibilityReport.log.old"));
            }
            catch { }

            // Remove the debug logfile from a previous session, if it exists.
            if (!ModSettings.DebugMode)
            {
                Toolkit.DeleteFile(System.IO.Path.Combine(ModSettings.DebugLogPath, ModSettings.LogFileName));
            }

            Logger.Log($"{ ModSettings.ModName } version { ModSettings.FullVersion }. Game version { Toolkit.ConvertGameVersionToString(Toolkit.CurrentGameVersion()) }.");
            Logger.Log($"Reporter started during scene { scene }.", Logger.Debug);

            if (PlatformService.platformType != PlatformType.Steam)
            {
                Logger.Log("Your game has no access to the Steam Workshop, and this mod requires that. No report was generated.", Logger.Error);
                return;
            }
            if (PluginManager.noWorkshop)
            {
                Logger.Log("The game can't access the Steam Workshop because of the '--noWorkshop' launch option. No report was generated.", Logger.Warning);
                return;
            }

            Catalog catalog = Catalog.Load();

            if (catalog == null)
            {
                Logger.Log("Can't load bundled catalog and can't download a new catalog. No report was generated.", Logger.Error);
                return;
            }

            if (Toolkit.CurrentGameVersion() != catalog.GameVersion())
            {
                Logger.Log($"The catalog was updated for game version { Toolkit.ConvertGameVersionToString(catalog.GameVersion()) }. You're using " +
                    $"{ (Toolkit.CurrentGameVersion() < catalog.GameVersion() ? "an older" : "a newer") } version of the game. Results might not be accurate.", 
                    Logger.Warning);
            }

            catalog.ScanSubscriptions();
            Logger.Log($"Reviewed { catalog.ReviewedSubscriptionCount } of your { catalog.SubscriptionCount() } mods.", Logger.Debug);

            if (ModSettings.HtmlReport)
            {
                // Todo 1.1 Create HTML report.
            }

            // Always create the text report if the HTML report is disabled, so at least one report is created.
            if (ModSettings.TextReport || !ModSettings.HtmlReport)
            {
                TextReport textReport = new TextReport(catalog);
                textReport.Create();
            }
        }
    }
}