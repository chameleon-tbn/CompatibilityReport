using UnityEngine.SceneManagement;
using ICities;
using ColossalFramework.UI;
using CompatibilityReport.Util;
using CompatibilityReport.Reporter;

// This class uses code snippets from the Settings class from Loading Screen Mod by thale5:
// https://github.com/thale5/LSM/blob/master/LoadingScreenMod/Settings.cs

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

            // Todo 0.8 Create Mod Options UI and xml file. Remove dummy vars.
            int dummy = 0;
            bool settingsVisible = false;

            UIHelperBase group;
            UIComponent container;
            UIPanel panel;
            UIButton button1, button2;
            UIDropDown dropdown;
            UILabel label;

            group = helper.AddGroup("Report options:");
            panel = (group as UIHelper).self as UIPanel;

            // Todo 0.8 Remove temporary 'not implemented text.
            label = panel.AddUIComponent<UILabel>();
            label.processMarkup = true;
            label.text = "<color #FF0000>THE MOD OPTIONS ARE NOT FULLY IMPLEMENTED YET.</color>";
            group.AddSpace(20);

            label = panel.AddUIComponent<UILabel>();
            label.text = "Report path:";
            label.textScale = 1.1f;
            label = panel.AddUIComponent<UILabel>();
            label.processMarkup = true;
            label.text = $"<color { ModSettings.SettingsUIColor }>{ Toolkit.Privacy(ModSettings.ReportPath) }</color>";
            label.textScale = 0.9f;
            group.AddSpace(5);

            button1 = group.AddButton("Change path", () => dummy++) as UIButton;
            button1.isVisible = settingsVisible;    // Todo 0.8 Remove this.
            button2 = group.AddButton("Reset path to default", () => dummy++) as UIButton;
            button2.isVisible = settingsVisible;    // Todo 0.8 Remove this.
            container = button1.parent;
            panel = container.AddUIComponent<UIPanel>();
            panel.width = container.width;
            panel.height = button1.height;
            button1.AlignTo(panel, UIAlignAnchor.TopLeft);
            button2.AlignTo(panel, UIAlignAnchor.TopLeft);
            button2.relativePosition += new UnityEngine.Vector3(button1.width + 10, 0);
            group.AddSpace(10);

            dropdown = group.AddDropdown("Report type: ", new string[] { "text" /*, "html", "text and html" */ }, 0, (int _) => { }) as UIDropDown; // Todo 0.8 Remove /* */
            dropdown.width = 180f;
            container = dropdown.parent;
            panel = container.AddUIComponent<UIPanel>();
            panel.width = container.width;
            panel.height = dropdown.height;
            button1 = group.AddButton("Open report(s)", () => dummy++) as UIButton;
            button1.isVisible = settingsVisible;    // Todo 0.8 Remove this.
            button2 = group.AddButton("Generate report(s)", () => dummy++) as UIButton;
            button2.isVisible = settingsVisible;    // Todo 0.8 Remove this.
            button1.AlignTo(panel, UIAlignAnchor.TopLeft);
            button1.relativePosition += new UnityEngine.Vector3(dropdown.width + 30, -button1.height);
            button2.AlignTo(panel, UIAlignAnchor.TopLeft);
            button2.relativePosition += new UnityEngine.Vector3(dropdown.width + 30 + button1.width + 10, -button1.height);

            group = helper.AddGroup("Catalog options:");
            panel = (group as UIHelper).self as UIPanel;

            label = panel.AddUIComponent<UILabel>();
            label.processMarkup = true;
            label.text = $"Catalog version: { CatalogData.Catalog.SettingsUIText }";
            label.textScale = 1.1f;
            group.AddSpace(10);
            
            dropdown = group.AddDropdown("Download: ",  // Todo 0.8 Remove /* */
                new string[] { "Every game start", /* "Once per day", "Once a week", "Never (on-demand only) - not recommended!" */ }, 0, (int _) => { }) as UIDropDown;
            dropdown.width = 290f;
            container = dropdown.parent;
            panel = container.AddUIComponent<UIPanel>();
            panel.width = container.width;
            panel.height = dropdown.height;
            button1 = group.AddButton("Download now", () => dummy++) as UIButton;
            button1.isVisible = settingsVisible;    // Todo 0.8 Remove this.
            button1.AlignTo(panel, UIAlignAnchor.TopLeft);
            button1.relativePosition += new UnityEngine.Vector3(dropdown.width + 30, -button1.height);

            group = helper.AddGroup("Advanced options:");
            panel = (group as UIHelper).self as UIPanel;

            label = panel.AddUIComponent<UILabel>();
            label.processMarkup = true;
            label.text = $"Mod version: <color { ModSettings.SettingsUIColor }>{ ModSettings.FullVersion }</color>";
            label.textScale = 1.1f;

            // Todo 0.8 Activate checkboxes and button
            // group.AddCheckbox("Show source URL in report", false, (bool _) => { });
            // group.AddCheckbox("Show download date/time of mods in report", false, (bool _) => { });
            // group.AddCheckbox("Debug logging", false, (bool _) => { });
            group.AddSpace(10);
            
            // group.AddButton("Ignore selected incompatibilities", () => dummy++);

            helper.AddButton("Reset all settings", () => dummy++);

            /* OLD FIELDS, NO LONGER USED:

                UITextField textField = group.AddTextfield("Report path:", Toolkit.Privacy(ModSettings.ReportPath), (string _) => { }) as UITextField;
                textField.width = 650f;
                textField.textScale = 0.8f;
                textField.readOnly = true;

                UIHelper subGroup = group.AddGroup(" ") as UIHelper;
                var panel = subGroup.self as UIPanel;
                panel.autoLayoutDirection = LayoutDirection.Horizontal;
                panel.autoLayoutPadding = new UnityEngine.RectOffset(5, 5, 0, 0);
                subGroup.AddButton("Open text report", () => dummy++);
                subGroup.AddButton("Open HTML report", () => dummy++);
                subGroup.AddButton("Generate new report", () => dummy++);
            */
        }
    }
}