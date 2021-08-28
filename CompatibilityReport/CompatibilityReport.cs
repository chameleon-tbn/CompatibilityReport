using UnityEngine.SceneManagement;
using ICities;
using CompatibilityReport.Util;


/// This mod is inspired by & partially based on the Mod Compatibility Checker mod by aubergine18 / aubergine10:
///     https://steamcommunity.com/sharedfiles/filedetails/?id=2034713132
///     https://github.com/CitiesSkylinesMods/AutoRepair
///
/// It also uses (or used) code snippets from:
///
///   * Enhanced District Services by Tim / chronofanz
///     https://github.com/chronofanz/EnhancedDistrictServices
///     https://steamcommunity.com/sharedfiles/filedetails/?id=2303997489
///
///   * Customize It Extended by C# / Celisuis
///     https://github.com/Celisuis/CustomizeItExtended
///     https://steamcommunity.com/sharedfiles/filedetails/?id=1806759255
///     
///   * Change Loading Screen 2 by bloodypenguin
///     https://github.com/bloodypenguin/ChangeLoadingImage
///     https://steamcommunity.com/sharedfiles/filedetails/?id=1818482110


namespace CompatibilityReport
{
    public class CompatibilityReport : IUserMod
    {
        // The name and description of the mod, as seen in Content Manager and Options window
        public string Name => ModSettings.displayName;
        public string Description => ModSettings.modDescription;


        // OnSettingsUI is called at the end of loading the game to the main menu (scene IntroScreen), when all subscriptions will be available.
        // Called again when loading a map (scene Game), and presumably when opening the mod options.
        public void OnSettingsUI(UIHelperBase helper)
        {
            // Check in which phase of game loading we are
            string scene = SceneManager.GetActiveScene().name;

            // Debug message
            Logger.Log($"OnSettingsUI called in scene { scene }.", Logger.debug);

            // Start the updater; will only run if the updater is enabled, and only on the first call
            Updater.CatalogUpdater.Start();

            // Create a report; will only be done once and only in the allowed scene
            Reporter.Start(scene);

            // Get the settings on the screen [Todo 0.7]
            UIHelperBase modOptions = helper.AddGroup(ModSettings.modName);

            // modOptions.Add...
        }
    }
}