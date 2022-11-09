using System;
using System.IO;
using System.Xml.Serialization;
using ColossalFramework.IO;
using CompatibilityReport.Settings.ConfigData;
using CompatibilityReport.Util;

namespace CompatibilityReport.Settings
{
    public class GlobalConfig
    {
        private const string SettingsFileName = ModSettings.InternalName + "_Settings.xml";
        private static string SettingsPath { get; } = DataLocation.applicationBase;
        internal const int CONFIG_VERSION = 1;
        public static GlobalConfig Instance { get; private set; }

        public GeneralConfig GeneralConfig = new GeneralConfig();
        public UpdaterConfig UpdaterConfig = new UpdaterConfig();
        public AdvancedConfig AdvancedConfig = new AdvancedConfig();
        
        static GlobalConfig()
        {
            Reload();
        }
        
        internal static void Ensure()
        {
            Validate();
        }

        internal static void WriteConfig()
        {
            WriteConfig(Instance);
        }

        internal static void Reload()
        {
            Instance = Load();
        }

        internal static void Reset()
        {
            GlobalConfig config = new GlobalConfig();
            WriteConfig(config);
            Instance = config;
        }

        private static GlobalConfig Load()
        {
            try
            {
                // use game log explicitly since mod logger is accessing GlobalConfig - not available at first load
                UnityEngine.Debug.Log($"[{ModSettings.InternalName}] Loading mod config from file '{SettingsFileName}'...");
                using (FileStream fs = new FileStream(Path.Combine(SettingsPath, SettingsFileName), FileMode.Open))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(GlobalConfig));
                    UnityEngine.Debug.Log($"[{ModSettings.InternalName}] Mod config loaded.");
                    GlobalConfig conf = (GlobalConfig)serializer.Deserialize(fs);
                    if (conf.GeneralConfig == null)
                    {
                        conf.GeneralConfig = new GeneralConfig();
                    }

                    if (conf.UpdaterConfig == null)
                    {
                        conf.UpdaterConfig = new UpdaterConfig();
                    }
                    
                    if (conf.AdvancedConfig == null)
                    {
                        conf.AdvancedConfig = new AdvancedConfig();
                    }
                    
                    return conf;
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[{ModSettings.InternalName}] Could not load global config: {e} Generating default config.");
                GlobalConfig config = new GlobalConfig();
                WriteConfig(config);
                return config;
            }
        }

        private static void Validate() {
#if DEBUG
         Logger.Log("Validating global config...");   
#endif
            GlobalConfig conf = Instance;
            bool requireUpdate = false;
            if (conf.GeneralConfig.Version < CONFIG_VERSION)
            {
                conf.GeneralConfig.Version = CONFIG_VERSION;
                conf.GeneralConfig.ReportType = 0;
                conf.GeneralConfig.TextReport = false;
                conf.GeneralConfig.HtmlReport = true;
                requireUpdate = true;
            }
#if DEBUG
            Logger.Log($"Global config validated successfully. Require update? {requireUpdate}");   
#endif
            if (requireUpdate)
            {
                WriteConfig();
            }
        }

        private static void WriteConfig(GlobalConfig config)
        {
            try
            {
                Logger.Log($"Writing global config to file '{SettingsFileName}'...");
                XmlSerializer serializer = new XmlSerializer(typeof(GlobalConfig));
                using (TextWriter writer = new StreamWriter(Path.Combine(SettingsPath, SettingsFileName)))
                {
                    serializer.Serialize(writer, config);
                }
            }
            catch (Exception e)
            {
                Logger.Log($"Could not write global config: {e}", Logger.LogLevel.Error);
            }
        }
    }
}
