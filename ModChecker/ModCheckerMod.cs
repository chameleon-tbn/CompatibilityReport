using System;
using UnityEngine.SceneManagement;
using ICities;
using ModChecker.Util;


/// This mod is inspired by & based on the Mod Compatibility Checker mod by aubergine18 / aubergine10:
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



namespace ModChecker
{
    public class ModCheckerMod : IUserMod
    {
        // The name and description of the mod, as seen in Content Manager and Options window
        public string Name => ModSettings.displayName;
        public string Description => ModSettings.description;


        // Initialize the scanner; subscriptions are not available at this stage yet
        public void OnEnabled()
        {
            Logger.Log("OnEnabled called.", Logger.debug);

            Scanner.Init();

            // Unfinished: should get a different starting trigger (file on disk?)
            if (ModSettings.DebugMode)
            {
                if (!Updater.Start())
                {
                    Logger.Log("Updater failed.", Logger.debug);
                }
            }
        }


        // OnSettingsUI is called when the game needs the Settings UI, including at the end of loading the game to the main menu (IntroScreen) 
        //      and again when loading a map (Game); and presumably when opening the mod options
        // Start the scan and create the report
        public void OnSettingsUI(UIHelperBase helper)
        {
            string scene = SceneManager.GetActiveScene().name;

            Logger.Log($"OnSettingsUI called in scene { scene }.", Logger.debug);

            Scanner.Scan(scene);

            try
            {
                // Unfinished
                //SettingsUI.Render(helper, scene);
            }
            catch (Exception ex)
            {
                Logger.Log($"Error creating SettingsUI while in scene { scene }.", Logger.error);

                Logger.Exception(ex);
            }
        }


        // OnDisabled is called when the mod is disabled in the Content Manager, or when the mod is updated while the game is running
        // Clean up
        public void OnDisabled()
        {
            Logger.Log("OnDisabled called.", Logger.debug);

            Scanner.Close();
        }
    }
}