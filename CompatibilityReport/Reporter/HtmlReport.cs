using System.IO;
using CompatibilityReport.CatalogData;
using CompatibilityReport.Reporter.HtmlTemplates;
using CompatibilityReport.Settings;
using CompatibilityReport.Settings.ConfigData;
using CompatibilityReport.Util;

namespace CompatibilityReport.Reporter
{
    public class HtmlReport
    {
        private readonly Catalog catalog;
        public HtmlReport(Catalog currentCatalog)
        {
            catalog = currentCatalog;
        }
        public void Create()
        {
            HtmlReportTemplate template = new HtmlReportTemplate(catalog);
            SaveReport(template.TransformText());
        }
        
        private void SaveReport(string reportText)
        {
            GeneralConfig config = GlobalConfig.Instance.GeneralConfig;
            string htmlReportFullPath = Path.Combine(config.ReportPath, ModSettings.ReportHtmlFileName);
            Toolkit.DeleteFile($"{ htmlReportFullPath }.old");

            if (Toolkit.SaveToFile(reportText, htmlReportFullPath, createBackup: GlobalConfig.Instance.AdvancedConfig.DebugMode))
            {
                Logger.Log($"Html Report ready at \"{ Toolkit.Privacy(htmlReportFullPath) }\".");
                return;
            }

            Logger.Log($"Html Report could not be saved to \"{ Toolkit.Privacy(htmlReportFullPath) }\". Trying an alternative location.", Logger.Warning);

            string altHtmlReportFullPath = Path.Combine(ModSettings.DefaultReportPath, ModSettings.ReportHtmlFileName);
            Toolkit.DeleteFile($"{ altHtmlReportFullPath }.old");
            if ((config.ReportPath != ModSettings.DefaultReportPath) && Toolkit.SaveToFile(reportText.ToString(), altHtmlReportFullPath))
            {
                Logger.Log($"Html Report could not be saved to the location set in the options. It is instead saved as \"{ Toolkit.Privacy(altHtmlReportFullPath) }\".", 
                    Logger.Warning);
                return;
            }

            altHtmlReportFullPath = Path.Combine(ModSettings.AlternativeReportPath, ModSettings.ReportHtmlFileName);
            Toolkit.DeleteFile($"{ altHtmlReportFullPath }.old");
            if (Toolkit.SaveToFile(reportText.ToString(), altHtmlReportFullPath))
            {
                Logger.Log($"Html Report could not be saved to the location set in the options. It is instead saved as \"{ Toolkit.Privacy(altHtmlReportFullPath) }\".", 
                    Logger.Warning);
                return;
            }

            Logger.Log($"Html Report could not be saved. No report was created.", Logger.Error);
        }
    }
}
