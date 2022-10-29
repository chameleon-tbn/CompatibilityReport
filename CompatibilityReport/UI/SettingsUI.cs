using System;
using System.Diagnostics;
using ColossalFramework.PlatformServices;
using ColossalFramework.Plugins;
using ColossalFramework.UI;
using CompatibilityReport.Reporter;
using CompatibilityReport.Settings;
using CompatibilityReport.Settings.ConfigData;
using CompatibilityReport.Util;
using ICities;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Logger = CompatibilityReport.Util.Logger;

namespace CompatibilityReport.UI
{
    internal class SettingsUI
    {
        private static readonly string[] ReportTypes = new string[] { "text", "html", "text and html" };
        private static readonly string[] DownloadOptions = new string[] { "Once a week", "Never (on-demand only) - not recommended!" };

        private UIHelperBase settingsUIHelper;
        private UIScrollablePanel optionsPanel;
        private UITextField reportPathTextField;
        private UILabel catalogVersionLabel;
        
        // make GlobalConfig Observable in the future?
        private event Action<GlobalConfig> eventGlobalOptionsUpdated;

        private static string ReportPathText => $"<color {ModSettings.SettingsUIColor}>{Toolkit.Privacy(GlobalConfig.Instance.GeneralConfig.ReportPath)}</color>";

        private SettingsUI(UIHelperBase helper)
        {
            settingsUIHelper = helper;
            optionsPanel = (helper as UIHelper).self as UIScrollablePanel;
            optionsPanel.gameObject.AddComponent<GameObjectObserver>().eventGameObjectDestroyed += Dispose;
            BuildUI();
        }

        internal static SettingsUI Create(UIHelperBase helper)
        {
            return new SettingsUI(helper);
        }

        private void Dispose()
        {
            eventGlobalOptionsUpdated = null;
            SettingsManager.CleanupEvents();
            catalogVersionLabel = null;
            optionsPanel = null;
            reportPathTextField = null;
            settingsUIHelper = null;
        }

        private void BuildUI()
        {
            string scene = SceneManager.GetActiveScene().name;
            Logger.Log($"OnSettingsUI called in scene {scene}.", Logger.Debug);

            GlobalConfig config = GlobalConfig.Instance;

            BuildRecordGroupUI(out bool canContinue);

            if (!canContinue)
            {
                return;
            }
            
            BuildCatalogOptionsUI();

            BuildAdvancedOptionsUI();
            
            if (Toolkit.IsMainMenuScene(scene))
            {
                if (ModSettings.UpdaterAvailable)
                {
                    BuildUpdaterGroupUI();
                }

                if (config.AdvancedConfig.ScanBeforeMainMenu)
                {
                    Report.Create(scene);
                }
            }
            
        }

        private void BuildCatalogOptionsUI()
        {
            GeneralConfig config = GlobalConfig.Instance.GeneralConfig;
            UIHelperBase catalogGroup = settingsUIHelper.AddGroup("Catalog options:");
            UIPanel catalogPanel = (catalogGroup as UIHelper).self as UIPanel;
            catalogVersionLabel = catalogPanel.AddUIComponent<UILabel>();
            catalogVersionLabel.processMarkup = true;
            catalogVersionLabel.text = $"Catalog version: {CatalogData.Catalog.SettingsUIText}";
            catalogVersionLabel.textScale = 1.1f;
            SettingsManager.eventCatalogUpdated += () => catalogVersionLabel.text = $"Catalog version: {CatalogData.Catalog.SettingsUIText}";
            catalogGroup.AddSpace(10);

#if CATALOG_DOWNLOAD
            UIDropDown downloadFrequencyDropdown = catalogGroup.AddDropdown($"{T("SET_BCO_DFD")}: ", DownloadOptions.ToArray(), config.DownloadFrequency, SettingsManager.OnDownloadOptionChanged) as UIDropDown;
            downloadFrequencyDropdown.width = 290f;
            eventGlobalOptionsUpdated += c => downloadFrequencyDropdown.selectedIndex = c.GeneralConfig.DownloadFrequency;

            UIComponent reportTypeContainer = downloadFrequencyDropdown.parent;
            UIPanel downloadPanel = reportTypeContainer.AddUIComponent<UIPanel>();
            downloadPanel.width = reportTypeContainer.width;
            downloadPanel.height = downloadFrequencyDropdown.height;

            UIButton download = catalogGroup.AddButton("Download now", SettingsManager.OnDownloadCatalog) as UIButton;
            download.AlignTo(downloadPanel, UIAlignAnchor.TopLeft);
            download.relativePosition += new UnityEngine.Vector3(downloadFrequencyDropdown.width + 30, -download.height);
#endif


            var textWidthField = catalogGroup.AddTextfield("Text Report width", config.TextReportWidth.ToString(), _ => { }, text => {
                if (TryGetNumber(text, out int number) && number >= 30 && number <= 500)
                {
                    GeneralConfig c = GlobalConfig.Instance.GeneralConfig;
                    if (c.TextReportWidth != number)
                    {
                        c.TextReportWidth = number;
                        GlobalConfig.WriteConfig();
                    }
                } else { Logger.Log($"Incorrect Text Report width: {text}. Value should be between 30 and 500.");}
            }) as UITextField;
            textWidthField.submitOnFocusLost = false;
            eventGlobalOptionsUpdated += c => textWidthField.text = c.GeneralConfig.TextReportWidth.ToString();
        }

        private void BuildAdvancedOptionsUI()
        {
            AdvancedConfig config = GlobalConfig.Instance.AdvancedConfig;
            UIHelperBase advancedGroup = settingsUIHelper.AddGroup("Advanced options:");
            UIPanel advGroupPanel = (advancedGroup as UIHelper).self as UIPanel;

            UILabel modVersionLabel = advGroupPanel.AddUIComponent<UILabel>();
            modVersionLabel.processMarkup = true;
            modVersionLabel.text = $"Mod version: <color {ModSettings.SettingsUIColor}>{ModSettings.FullVersion}</color>";
            modVersionLabel.textScale = 1.1f;

            
            // Todo 0.9 Activate checkboxes and button
            // group.AddCheckbox("Show source URL in report", false, (bool _) => { });
            // group.AddCheckbox("Show download date/time of mods in report", false, (bool _) => { });
            // group.AddCheckbox("Debug logging", false, (bool _) => { });
            advancedGroup.AddSpace(10);

#if CATALOG_DOWNLOAD
            var maxRetries = advancedGroup.AddTextfield($"{T("SET_BAO_MR")}", config.DownloadRetries.ToString(), _ => { }, text => {
                if (TryGetNumber(text, out int number) && number >=0 && number <= 10)
                {
                    AdvancedConfig advConfig = GlobalConfig.Instance.AdvancedConfig;
                    if (advConfig.DownloadRetries != number)
                    {
                        advConfig.DownloadRetries = number;
                        GlobalConfig.WriteConfig();
                    }
                } else { Logger.Log($"Incorrect Number of download retries: {text}. Value should be between 0 and 10.");}
            }) as UITextField;
            maxRetries.submitOnFocusLost = false;
            maxRetries.eventTextCancelled += (component, value) => (component as UITextField).text = GlobalConfig.Instance.AdvancedConfig.DownloadRetries.ToString(); 
            eventGlobalOptionsUpdated += c => maxRetries.text = c.AdvancedConfig.DownloadRetries.ToString();
#endif

            var maxLogSize = advancedGroup.AddTextfield($"{T("SET_BAO_MLS")}", (config.LogMaxSize / 1024).ToString(), _ => { }, text => {
                if (TryGetNumber(text, out int number) && number >= 10 && number <= 10000)
                {
                    AdvancedConfig advConfig = GlobalConfig.Instance.AdvancedConfig;
                    int newSize = number * 1024;
                    if (advConfig.LogMaxSize != newSize)
                    {
                        advConfig.LogMaxSize = newSize;
                        GlobalConfig.WriteConfig();
                    }
                } else { Logger.Log($"Incorrect Max log size: {text}. Value should be between 10 and 10 000 (kb).");}
            }) as UITextField;
            maxLogSize.submitOnFocusLost = false;
            maxLogSize.eventTextCancelled += (component, value) => (component as UITextField).text = (GlobalConfig.Instance.AdvancedConfig.LogMaxSize / 1024).ToString();
            
            var scanBeforeMenu = advancedGroup.AddCheckbox("Scan before entering Main Menu", config.ScanBeforeMainMenu, value => {
                AdvancedConfig advConfig = GlobalConfig.Instance.AdvancedConfig;
                if (advConfig.ScanBeforeMainMenu != value)
                {
                    advConfig.ScanBeforeMainMenu = value;
                    GlobalConfig.WriteConfig();
                }
            }) as UICheckBox;
            
            var debugMode = advancedGroup.AddCheckbox("Debug Mode", config.DebugMode, value => {
                AdvancedConfig advConfig = GlobalConfig.Instance.AdvancedConfig;
                if (advConfig.DebugMode != value)
                {
                    advConfig.DebugMode = value;
                    GlobalConfig.WriteConfig();
                }
            }) as UICheckBox;

            eventGlobalOptionsUpdated += c => maxRetries.text = c.AdvancedConfig.DownloadRetries.ToString();
            eventGlobalOptionsUpdated += c => maxLogSize.text = (c.AdvancedConfig.LogMaxSize / 1024).ToString();
            eventGlobalOptionsUpdated += c => scanBeforeMenu.isChecked = c.AdvancedConfig.ScanBeforeMainMenu;
            eventGlobalOptionsUpdated += c => debugMode.isChecked = c.AdvancedConfig.DebugMode;

            // group.AddButton("Ignore selected incompatibilities", () => dummy++);

            advancedGroup.AddButton("Reset all settings", () => {
                SettingsManager.OnResetAllSettings();
                eventGlobalOptionsUpdated?.Invoke(GlobalConfig.Instance);
            });
        }

        private void BuildUpdaterGroupUI()
        {
            UIHelperBase updaterGroup = settingsUIHelper.AddGroup($"{T("SET_BUG_UG")}");
#if CATALOG_DOWNLOAD
            UpdaterConfig config = GlobalConfig.Instance.UpdaterConfig;

            var steamMaxFailedPages = updaterGroup.AddTextfield("Steam max failed pages", config.SteamMaxFailedPages.ToString(), _ => { }, text => {
                if (TryGetNumber(text, out int number))
                {
                    UpdaterConfig updaterConfig = GlobalConfig.Instance.UpdaterConfig;
                    if (updaterConfig.SteamMaxFailedPages != number)
                    {
                        updaterConfig.SteamMaxFailedPages = number;
                        GlobalConfig.WriteConfig();
                    }
                }
            }) as UITextField;
            steamMaxFailedPages.submitOnFocusLost = false;
            steamMaxFailedPages.eventTextCancelled += (component, value) => (component as UITextField).text = GlobalConfig.Instance.UpdaterConfig.SteamMaxFailedPages.ToString();

            var steamMaxListingPages = updaterGroup.AddTextfield("Steam max listing pages", config.SteamMaxListingPages.ToString(), _ => { }, text => {
                if (TryGetNumber(text, out int number))
                {
                    UpdaterConfig updaterConfig = GlobalConfig.Instance.UpdaterConfig;
                    if (updaterConfig.SteamMaxListingPages != number)
                    {
                        updaterConfig.SteamMaxListingPages = number;
                        GlobalConfig.WriteConfig();
                    }
                }
            }) as UITextField;
            steamMaxListingPages.submitOnFocusLost = false;
            steamMaxListingPages.eventTextCancelled += (component, value) => (component as UITextField).text = GlobalConfig.Instance.UpdaterConfig.SteamMaxListingPages.ToString();

            var msPerModPage = updaterGroup.AddTextfield("Estimated milliseconds per mod page", config.EstimatedMillisecondsPerModPage.ToString(), _ => { }, text => {
                if (TryGetNumber(text, out int number))
                {
                    UpdaterConfig updaterConfig = GlobalConfig.Instance.UpdaterConfig;
                    if (updaterConfig.EstimatedMillisecondsPerModPage != number)
                    {
                        updaterConfig.EstimatedMillisecondsPerModPage = number;
                        GlobalConfig.WriteConfig();
                    }
                }
            }) as UITextField;
            msPerModPage.submitOnFocusLost = false;
            msPerModPage.eventTextCancelled += (component, value) => (component as UITextField).text = GlobalConfig.Instance.UpdaterConfig.EstimatedMillisecondsPerModPage.ToString();

            var daysOfInactivity = updaterGroup.AddTextfield("Days of inactivity to retire author", config.DaysOfInactivityToRetireAuthor.ToString(), _ => { }, text => {
                if (TryGetNumber(text, out int number))
                {
                    UpdaterConfig updaterConfig = GlobalConfig.Instance.UpdaterConfig;
                    if (updaterConfig.DaysOfInactivityToRetireAuthor != number)
                    {
                        updaterConfig.DaysOfInactivityToRetireAuthor = number;
                        GlobalConfig.WriteConfig();
                    }
                }
            }) as UITextField;
            daysOfInactivity.submitOnFocusLost = false;
            daysOfInactivity.eventTextCancelled += (component, value) => (component as UITextField).text = GlobalConfig.Instance.UpdaterConfig.DaysOfInactivityToRetireAuthor.ToString();

            var weeksForRetired = updaterGroup.AddTextfield("Weeks for soon retired", config.WeeksForSoonRetired.ToString(), _ => { }, text => {
                if (TryGetNumber(text, out int number))
                {
                    UpdaterConfig updaterConfig = GlobalConfig.Instance.UpdaterConfig;
                    if (updaterConfig.WeeksForSoonRetired != number)
                    {
                        updaterConfig.WeeksForSoonRetired = number;
                        GlobalConfig.WriteConfig();
                    }
                }
            }) as UITextField;
            weeksForRetired.submitOnFocusLost = false;
            weeksForRetired.eventTextCancelled += (component, value) => (component as UITextField).text = GlobalConfig.Instance.UpdaterConfig.WeeksForSoonRetired.ToString();
#endif
            UIPanel updaterPanel = HelperToUIPanel(updaterGroup);
            UIPanel panel = updaterPanel.AddUIComponent<UIPanel>();
            panel.relativePosition = Vector3.zero;
            panel.width = 650;
            panel.height = 40;
            var helper = new UIHelper(panel);
            helper.AddButton("Run Web Crawler", () => SettingsManager.OnStartWebCrawler(OpenProgressModal()));
            helper.AddButton("Run Web Crawler (quick)", () => SettingsManager.OnStartWebCrawler(OpenProgressModal(), quick: true));
            helper.AddButton("Run Updater", () => SettingsManager.OnStartUpdater(OpenProgressModal()));
            panel.autoLayoutDirection = LayoutDirection.Horizontal;
            panel.autoLayoutPadding = new RectOffset(0, 10, 0, 0);
            panel.autoLayout = true;
            updaterGroup.AddButton($"{T("SET_BUG_OOTA")}", () => SettingsManager.OnOneTimeAction(OpenProgressModal()));
#if CATALOG_DOWNLOAD
            updaterGroup.AddButton("Upload Catalog", () => OpenUploadCatalogModal());

            eventGlobalOptionsUpdated += (c) => steamMaxFailedPages.text = c.UpdaterConfig.SteamMaxFailedPages.ToString();
            eventGlobalOptionsUpdated += (c) => steamMaxListingPages.text = c.UpdaterConfig.SteamMaxListingPages.ToString();
            eventGlobalOptionsUpdated += (c) => msPerModPage.text = c.UpdaterConfig.EstimatedMillisecondsPerModPage.ToString();
            eventGlobalOptionsUpdated += (c) => daysOfInactivity.text = c.UpdaterConfig.DaysOfInactivityToRetireAuthor.ToString();
            eventGlobalOptionsUpdated += (c) => weeksForRetired.text = c.UpdaterConfig.WeeksForSoonRetired.ToString();
#endif
        }

        private bool TryGetNumber(string text, out int number)
        {
            return int.TryParse(text, out number);
        }

        private UIPanel HelperToUIPanel(UIHelperBase helperBase)
        {
            // ReSharper disable once PossibleNullReferenceException
            return (helperBase as UIHelper).self as UIPanel;
        }

        private ProgressMonitorUI OpenProgressModal()
        {
            ProgressMonitorUI ui = (ProgressMonitorUI)UIView.GetAView().AddUIComponent(typeof(ProgressMonitorUI));
            ui.Initialize();
            ui.Show();
            ui.BringToFront();
            UIView.PushModal(ui);
            return ui;
        }
        
#if CATALOG_DOWNLOAD
        private UploadCatalogUI OpenUploadCatalogModal()
        {
            UploadCatalogUI ui = (UploadCatalogUI)UIView.GetAView().AddUIComponent(typeof(UploadCatalogUI));
            ui.Initialize();
            ui.Show();
            ui.BringToFront();
            UIView.PushModal(ui);
            return ui;
        }
#endif
        
        private void BuildRecordGroupUI(out bool shouldBuildOptions)
        {
            shouldBuildOptions = true;
            if (PluginManager.noWorkshop || PlatformService.platformType != PlatformType.Steam)
            {
                UIPanel reportPanel = HelperToUIPanel(settingsUIHelper.AddGroup("Mod Errors"));
                UILabel platformError = reportPanel.AddUIComponent<UILabel>();
                platformError.text = PluginManager.noWorkshop 
                    ? "'--noWorkshop' launch option detected. No report was generated" 
                    : $"Platform not supported ({PlatformService.platformType})!\n" +
                    "Access to the Steam Workshop is required for the mod to work properly.";
                platformError.textColor = Color.red;
                
                // skip building the rest of options - won't be usable anyways 
                shouldBuildOptions = false;
                return;
            }
            
            GeneralConfig config = GlobalConfig.Instance.GeneralConfig;
            UIHelperBase reportGroup = settingsUIHelper.AddGroup("Report options:");

            reportPathTextField = reportGroup.AddTextfield(
                text: "Report path:",
                defaultContent: config.ReportPath,
                eventChangedCallback: _ => { },
                eventSubmittedCallback: SettingsManager.OnChangeReportPath
            ) as UITextField;
            reportPathTextField.processMarkup = true;
            reportPathTextField.width = 650f;
            reportPathTextField.submitOnFocusLost = false;
            reportPathTextField.eventTextCancelled += (component, value) => (component as UITextField).text = GlobalConfig.Instance.GeneralConfig.ReportPath;
            eventGlobalOptionsUpdated += c => reportPathTextField.text = c.GeneralConfig.ReportPath;

            reportGroup.AddSpace(5);

            UIButton changePathBtn = reportGroup.AddButton("Change path", OnChangeReportPathClicked) as UIButton;
            UIComponent pathContainer = changePathBtn.parent;
            UIPanel pathPanel = pathContainer.AddUIComponent<UIPanel>();
            pathPanel.width = pathContainer.width;
            pathPanel.height = changePathBtn.height;
            changePathBtn.AlignTo(pathPanel, UIAlignAnchor.TopLeft);

            UIButton resetPathBtn = reportGroup.AddButton("Reset path to default", OnResetReportPathClicked) as UIButton;
            resetPathBtn.AlignTo(pathPanel, UIAlignAnchor.TopLeft);
            resetPathBtn.relativePosition += new UnityEngine.Vector3(changePathBtn.width + 10, 0);

            reportGroup.AddSpace(20);

            UIDropDown reportTypeDropdown = reportGroup.AddDropdown("Report type:", ReportTypes, config.ReportType, SettingsManager.OnReportTypeChange) as UIDropDown;
            reportTypeDropdown.width = 180f;
            eventGlobalOptionsUpdated += c => reportTypeDropdown.selectedIndex = c.GeneralConfig.ReportType;
            UIComponent reportTypeContainer = reportTypeDropdown.parent;
            UIPanel reportGenPanel = reportTypeContainer.AddUIComponent<UIPanel>();
            reportGenPanel.width = reportTypeContainer.width;
            reportGenPanel.height = reportTypeDropdown.height;

            reportGroup.AddCheckbox("Use Steam Overlay to open html report if available", config.OpenHtmlReportInSteamOverlay, SettingsManager.OnOpenHtmlReportInSteamChanged);

            UIButton openReportBtn = reportGroup.AddButton("Open report(s)", SettingsManager.OnOpenReports) as UIButton;

            UIButton generateReportBtn = reportGroup.AddButton("Generate report(s)", SettingsManager.OnGenerateReports) as UIButton;
            openReportBtn.AlignTo(reportGenPanel, UIAlignAnchor.TopLeft);
            openReportBtn.relativePosition += new UnityEngine.Vector3(reportTypeDropdown.width + 30, -openReportBtn.height);
            generateReportBtn.AlignTo(reportGenPanel, UIAlignAnchor.TopLeft);
            generateReportBtn.relativePosition += new UnityEngine.Vector3(reportTypeDropdown.width + 30 + openReportBtn.width + 10, -openReportBtn.height);
        }

        private void OnResetReportPathClicked()
        {
            SettingsManager.OnResetReportPath();
            reportPathTextField.text = ReportPathText;
        }

        private void OnChangeReportPathClicked()
        {
            SettingsManager.OnChangeReportPath(reportPathTextField.text);
        }
    }
}
