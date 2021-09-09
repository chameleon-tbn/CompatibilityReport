using UnityEngine.SceneManagement;
using ICities;
using CompatibilityReport.Util;
using CompatibilityReport.Reporter;

// This mod is inspired by & partially based on the Mod Compatibility Checker mod by aubergine18 / aubergine10:
//      https://steamcommunity.com/sharedfiles/filedetails/?id=2034713132
//      https://github.com/CitiesSkylinesMods/AutoRepair
//
// It also uses code snippets from:
//    * Enhanced District Services by Tim / chronofanz:
//      https://github.com/chronofanz/EnhancedDistrictServices
//      https://steamcommunity.com/sharedfiles/filedetails/?id=2303997489
//
//    * Change Loading Screen 2 by bloodypenguin:
//      https://github.com/bloodypenguin/ChangeLoadingImage
//      https://steamcommunity.com/sharedfiles/filedetails/?id=1818482110

namespace CompatibilityReport
{
    public class CompatibilityReport : IUserMod
    {
        public string Name { get; } = $"{ ModSettings.ModName } v{ ModSettings.Version }" +
            (string.IsNullOrEmpty(ModSettings.ReleaseType) ? "" : $" { ModSettings.ReleaseType}");

        public string Description { get; } = ModSettings.ModDescription;


        // OnSettingsUI is called at the end of loading the game to the main menu (scene IntroScreen), when all subscriptions will be available.
        // Called again when loading a map (scene Game), and presumably when opening the mod options.
        public void OnSettingsUI(UIHelperBase helper)
        {
            string scene = SceneManager.GetActiveScene().name;

            Logger.Log($"OnSettingsUI called in scene { scene }.", Logger.Debug);

            // Todo 0.8 Move CatalogUpdater to standalone tool
            Updater.CatalogUpdater.Start();

            Report.Create(scene);

            //Todo 0.7 Get the settings on the screen
            UIHelperBase modOptions = helper.AddGroup(ModSettings.ModName);
            // modOptions.Add...
        }
    }
}