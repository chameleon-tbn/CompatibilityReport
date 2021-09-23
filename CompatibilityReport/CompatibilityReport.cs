using UnityEngine.SceneManagement;
using ICities;
using CompatibilityReport.Util;
using CompatibilityReport.Reporter;

// This mod is inspired by & partially based on the Mod Compatibility Checker mod by aubergine18 / aubergine10: https://github.com/CitiesSkylinesMods/AutoRepair
// It also uses code snippets from:
//    * Enhanced District Services by Tim / chronofanz: https://github.com/chronofanz/EnhancedDistrictServices
//    * Change Loading Screen 2 by bloodypenguin: https://github.com/bloodypenguin/ChangeLoadingImage

namespace CompatibilityReport
{
    public class CompatibilityReport : IUserMod
    {
        public string Name { get; } = $"{ ModSettings.ModName } v{ ModSettings.Version }" + 
            (string.IsNullOrEmpty(ModSettings.ReleaseType) ? "" : $" { ModSettings.ReleaseType}");

        public string Description { get; } = ModSettings.ModDescription;


        /// <summary>Start the Updater when enabled and the Reporter when called in the correct scene. Also opens the settings UI.</summary>
        /// <remarks>Called at the end of loading the game to the main menu (scene IntroScreen), when all subscriptions will be available. 
        ///          Called again when loading a map (scene Game), and presumably when opening the mod options.</remarks>
        public void OnSettingsUI(UIHelperBase helper)
        {
            string scene = SceneManager.GetActiveScene().name;

            // Todo 0.9 Remove this debug log
            Logger.Log($"OnSettingsUI called in scene { scene }.", Logger.Debug);

            // Todo 0.8 Move CatalogUpdater to standalone tool.
            if (ModSettings.UpdaterAvailable)
            {
                Updater.CatalogUpdater.Start();
            }

            Report.Create(scene);

            UIHelperBase modOptions = helper.AddGroup(ModSettings.ModName);

            modOptions.AddGroup(ModSettings.ModName);
            //Todo 0.7 Create Settings UI.
        }
    }
}