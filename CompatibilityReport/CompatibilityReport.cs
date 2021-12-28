using UnityEngine.SceneManagement;
using ICities;
using CompatibilityReport.Util;
using CompatibilityReport.Reporter;

namespace CompatibilityReport
{
    public class CompatibilityReport : IUserMod
    {
        public string Name { get; } = ModSettings.IUserModName;
        public string Description { get; } = ModSettings.IUserModDescription;


        /// <summary>Start the Updater when enabled and the Reporter when called in the correct scene. Also opens the settings UI.</summary>
        /// <remarks>Called at the end of loading the game to the main menu (scene IntroScreen), when all subscriptions will be available. 
        ///          Called again when loading a map (scene Game), and presumably when opening the mod options.</remarks>
        public void OnSettingsUI(UIHelperBase helper)
        {
            string scene = SceneManager.GetActiveScene().name;
            Logger.Log($"OnSettingsUI called in scene { scene }.", Logger.Debug);

            if (ModSettings.UpdaterAvailable)
            {
                Updater.CatalogUpdater.Start();
            }

            Report.Create(scene);

            UIHelperBase modOptions = helper.AddGroup(ModSettings.ModName);

            modOptions.AddGroup("The mod options are not implemented yet.");
            //Todo 0.8 Create Settings UI.
        }
    }
}