using ColossalFramework;
using CompatibilityReport.Settings;
using CompatibilityReport.Translations;
using CompatibilityReport.UI;
using CompatibilityReport.Util;
using ICities;
using JetBrains.Annotations;

// This class uses code snippets from the Settings class from Loading Screen Mod by thale5:
// https://github.com/thale5/LSM/blob/master/LoadingScreenMod/Settings.cs

namespace CompatibilityReport
{
    [UsedImplicitly]
    public class CompatibilityReport : IUserMod
    {
        public string Name { get; } = ModSettings.IUserModName;
        public string Description { get; } = ModSettings.IUserModDescription;

        [UsedImplicitly]
        public void OnEnabled()
        {
            GlobalConfig.Ensure();
            SingletonLite<Translation>.Ensure();
            Translation.instance.LoadAll();
        }

        [UsedImplicitly]
        public void OnDisabled()
        {
            SettingsManager.CleanupEvents();
            Logger.CloseDebugLog();
            Translation.instance.Dispose();
        }

        /// <summary>Start the Updater when enabled and the Reporter when called in the correct scene. Also opens the settings UI.</summary>
        /// <remarks>Called at the end of loading the game to the main menu (scene IntroScreen), when all subscriptions will be available. 
        ///          Called again when loading a map (scene Game), and presumably when opening the mod options.</remarks>
        [UsedImplicitly]
        public void OnSettingsUI(UIHelperBase helper)
        {
            SettingsUI.Create(helper);
        }
    }
}