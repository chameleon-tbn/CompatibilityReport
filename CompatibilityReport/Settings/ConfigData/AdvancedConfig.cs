using System;

namespace CompatibilityReport.Settings.ConfigData
{
    [Serializable]
    public class AdvancedConfig
    {
        public int DownloadRetries { get; set; } = 4;
        public bool ScanBeforeMainMenu { get; set; } = true;
        public bool DebugMode { get; set; } = false;
        public long LogMaxSize { get; set; } = 100 * 1024;
    }
}
