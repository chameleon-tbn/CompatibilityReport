using System;
using System.Collections.Generic;
using System.Diagnostics;
using ColossalFramework.PlatformServices;
using ColossalFramework.Plugins;
using ColossalFramework.UI;
using CompatibilityReport.Reporter;
using CompatibilityReport.Settings;
using CompatibilityReport.Settings.ConfigData;
using CompatibilityReport.Translations;
using CompatibilityReport.Util;
using ICities;
using UnityEngine;
using UnityEngine.SceneManagement;
using Logger = CompatibilityReport.Util.Logger;

namespace CompatibilityReport.UI
{
    internal class SettingsUI
    {
        private static readonly FastList<string> ReportTypes = new FastList<string>();
        private static readonly FastList<string> AvailableLangs = new FastList<string>();
        private static readonly FastList<string> DownloadOptions = new FastList<string>();

        private UIHelperBase settingsUIHelper;
        private UIScrollablePanel optionsPanel;
        private UITextField reportPathTextField;
        private UILabel catalogVersionLabel;
        
        // make GlobalConfig Observable in the future?
        private event Action<GlobalConfig> eventGlobalOptionsUpdated;

        private static string ReportPathText => $"<color {ModSettings.SettingsUIColor}>{Toolkit.Privacy(GlobalConfig.Instance.GeneralConfig.ReportPath)}</color>";
        private Translator translator;

        private SettingsUI(UIHelperBase helper) {
            Translation.instance.SetCurrentLanguage();
            translator = Translation.instance.Current;
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

            BuildTranslatedDataSources();
            
            BuildRecordGroupUI(out bool canContinue);

            if (!canContinue)
            {
                return;
            }

            BuildUsefulLinksOptionsUI();
            
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

        private void BuildTranslatedDataSources() {
            AvailableLangs.Clear();
            AvailableLangs.Add("Game Language");

            var langs = Translation.instance.AvailableLangs;
            foreach (KeyValuePair<string,string> langCodeTranslationPair in langs)
            {
                AvailableLangs.Add(langCodeTranslationPair.Value);
            }
            
            DownloadOptions.Clear();
            DownloadOptions.Add(T("SET_DO_OAW"));
            DownloadOptions.Add(T("SET_DO_NEV"));
            
            ReportTypes.Clear();
            ReportTypes.Add(T("SET_RT_HTM"));
            ReportTypes.Add(T("SET_RT_TXT"));
            ReportTypes.Add(T("SET_RT_HAT"));
        }

        private void BuildUsefulLinksOptionsUI() {
            UIHelperBase linksGroup = settingsUIHelper.AddGroup("Useful links:");
            UIPanel linksPanel = (linksGroup as UIHelper).self as UIPanel;
            linksPanel.autoLayoutDirection = LayoutDirection.Horizontal;
            linksPanel.autoLayoutPadding = new RectOffset(5, 5, 0, 0);
            UIHelper helper = new UIHelper(linksPanel);
            helper.AddButton($"{T("SET_BUL_RC")}", () => Process.Start("https://forms.gle/PvezwfpgS1V1DHqA9"));

            helper.AddButton($"{T("SET_BUL_HWT")}", () => Process.Start("https://crowdin.com/project/compatibility-report"));
            helper.AddButton($"{T("SET_BUL_SOD")}", () => Process.Start("https://discord.gg/7kV9frCc6u"));

            helper.AddButton($"{T("SET_BUL_BML")}", () => Process.Start("https://pdxint.at/BrokenModCS"));
            helper.AddButton($"{T("SET_BUL_RML")}", () => Process.Start("https://bit.ly/3VA9NxC"));
        }

        private void BuildCatalogOptionsUI()
        {
            GeneralConfig config = GlobalConfig.Instance.GeneralConfig;
            UIHelperBase catalogGroup = settingsUIHelper.AddGroup($"{T("SET_BCO_CG")}:");
            UIPanel catalogPanel = (catalogGroup as UIHelper).self as UIPanel;
            catalogVersionLabel = catalogPanel.AddUIComponent<UILabel>();
            catalogVersionLabel.processMarkup = true;
            catalogVersionLabel.text = $"{T("SET_BCO_SUIT")}: {CatalogData.Catalog.SettingsUIText}";
            catalogVersionLabel.textScale = 1.1f;
            SettingsManager.eventCatalogUpdated += () => catalogVersionLabel.text = $"{T("SET_BCO_SUIT")}: {CatalogData.Catalog.SettingsUIText}";
            catalogGroup.AddSpace(10);

#if CATALOG_DOWNLOAD
            UIDropDown downloadFrequencyDropdown = catalogGroup.AddDropdown($"{T("SET_BCO_DFD")}: ", DownloadOptions.ToArray(), config.DownloadFrequency, SettingsManager.OnDownloadOptionChanged) as UIDropDown;
            downloadFrequencyDropdown.width = 290f;
            eventGlobalOptionsUpdated += c => downloadFrequencyDropdown.selectedIndex = c.GeneralConfig.DownloadFrequency;

            UIComponent reportTypeContainer = downloadFrequencyDropdown.parent;
            UIPanel downloadPanel = reportTypeContainer.AddUIComponent<UIPanel>();
            downloadPanel.width = reportTypeContainer.width;
            downloadPanel.height = downloadFrequencyDropdown.height;

            UIButton download = catalogGroup.AddButton($"{T("SET_BCO_UIBD")}", SettingsManager.OnDownloadCatalog) as UIButton;
            download.AlignTo(downloadPanel, UIAlignAnchor.TopLeft);
            download.relativePosition += new UnityEngine.Vector3(downloadFrequencyDropdown.width + 30, -download.height);
#endif

            var textWidthField = catalogGroup.AddTextfield($"{T("SET_BCO_TWF")}", config.TextReportWidth.ToString(), _ => { }, text => {
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
            UIHelperBase advancedGroup = settingsUIHelper.AddGroup($"{T("SET_BAO_AG")}");
            UIPanel advGroupPanel = (advancedGroup as UIHelper).self as UIPanel;

            UILabel modVersionLabel = advGroupPanel.AddUIComponent<UILabel>();
            modVersionLabel.processMarkup = true;
            modVersionLabel.text = $"{T("SET_BAO_MVL")}: <color {ModSettings.SettingsUIColor}>{ModSettings.FullVersion}</color>";
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
                } else { 
                    Logger.Log($"Incorrect Max log size: {text}. Value should be between 10 and 10 000 (kb).");
                    // SOMETHING == "Incorrect Max log size: {0}. Value should be between 10 and 10 000 (kb)."
                    Logger.Log($"{T("SOMETHING", "0" , text)}");
                }
            }) as UITextField;
            maxLogSize.submitOnFocusLost = false;
            maxLogSize.eventTextCancelled += (component, value) => (component as UITextField).text = (GlobalConfig.Instance.AdvancedConfig.LogMaxSize / 1024).ToString();
            
            var scanBeforeMenu = advancedGroup.AddCheckbox($"{T("SET_BAO_SBM")}", config.ScanBeforeMainMenu, value => {
                AdvancedConfig advConfig = GlobalConfig.Instance.AdvancedConfig;
                if (advConfig.ScanBeforeMainMenu != value)
                {
                    advConfig.ScanBeforeMainMenu = value;
                    GlobalConfig.WriteConfig();
                }
            }) as UICheckBox;
            
            var debugMode = advancedGroup.AddCheckbox($"{T("SET_BAO_DM")}", config.DebugMode, value => {
                AdvancedConfig advConfig = GlobalConfig.Instance.AdvancedConfig;
                if (advConfig.DebugMode != value)
                {
                    advConfig.DebugMode = value;
                    GlobalConfig.WriteConfig();
                }
            }) as UICheckBox;

            eventGlobalOptionsUpdated += c => maxLogSize.text = (c.AdvancedConfig.LogMaxSize / 1024).ToString();
            eventGlobalOptionsUpdated += c => scanBeforeMenu.isChecked = c.AdvancedConfig.ScanBeforeMainMenu;
            eventGlobalOptionsUpdated += c => debugMode.isChecked = c.AdvancedConfig.DebugMode;

            // group.AddButton("Ignore selected incompatibilities", () => dummy++);

            advancedGroup.AddButton($"{T("SET_BAO_ORAS")}", () => {
                SettingsManager.OnResetAllSettings();
                eventGlobalOptionsUpdated?.Invoke(GlobalConfig.Instance);
            });
        }

        private void BuildUpdaterGroupUI()
        {
            UIHelperBase updaterGroup = settingsUIHelper.AddGroup($"{T("SET_BUG_UG")}");
#if CATALOG_DOWNLOAD
            UpdaterConfig config = GlobalConfig.Instance.UpdaterConfig;

            var steamMaxFailedPages = updaterGroup.AddTextfield($"{T("SET_BUG_SMFP")}", config.SteamMaxFailedPages.ToString(), _ => { }, text => {
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

            var steamMaxListingPages = updaterGroup.AddTextfield($"{T("SET_BUG_SMLP")}", config.SteamMaxListingPages.ToString(), _ => { }, text => {
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

            var msPerModPage = updaterGroup.AddTextfield($"{T("SET_BUG_MSPMP")}", config.EstimatedMillisecondsPerModPage.ToString(), _ => { }, text => {
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

            var daysOfInactivity = updaterGroup.AddTextfield($"{T("SET_BUG_DOI")}", config.DaysOfInactivityToRetireAuthor.ToString(), _ => { }, text => {
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

            var weeksForRetired = updaterGroup.AddTextfield($"{T("SET_BUG_WFR")}", config.WeeksForSoonRetired.ToString(), _ => { }, text => {
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
            helper.AddButton($"{T("SET_BUG_OSWC")}", () => SettingsManager.OnStartWebCrawler(OpenProgressModal()));
            helper.AddButton($"{T("SET_BUG_OSWCQ")}", () => SettingsManager.OnStartWebCrawler(OpenProgressModal(), quick: true));
            helper.AddButton($"{T("SET_BUG_OSU")}", () => SettingsManager.OnStartUpdater(OpenProgressModal()));
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

        internal static UIPanel HelperToUIPanel(UIHelperBase helperBase)
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
            GeneralConfig config = GlobalConfig.Instance.GeneralConfig;
            UIDropDown langsDropdown = settingsUIHelper.AddDropdown(
                translator.T("LANGUAGE"),
                AvailableLangs.ToArray(),
                FindLangIndex(config.Language),
                (index) => {
                    optionsPanel.StartCoroutine(Translation.instance.ChangeLanguageByIndex_Deferred(index));
                }) as UIDropDown;
            langsDropdown.width = 300f;
            
            shouldBuildOptions = true;
            if (PluginManager.noWorkshop || PlatformService.platformType != PlatformType.Steam)
            {
                UIPanel reportPanel = HelperToUIPanel(settingsUIHelper.AddGroup($"{T("SET_BRG_RP")}"));
                UILabel platformError = reportPanel.AddUIComponent<UILabel>();
                platformError.text = PluginManager.noWorkshop 
                    ? $"{T("SET_BRG_NW")}" 
                    : $"{T("SET_BRG_PT")} ({PlatformService.platformType})!\n" +
                    $"{T("SET_BRG_PE")}";
                platformError.textColor = Color.red;
                
                // skip building the rest of options - won't be usable anyways 
                shouldBuildOptions = false;
                return;
            }

            UIHelperBase reportGroup = settingsUIHelper.AddGroup($"{T("SET_BRG_RG")}:");

            reportPathTextField = reportGroup.AddTextfield(
                text: $"{T("SET_BRG_RPTF")}:",
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

            UIButton changePathBtn = reportGroup.AddButton($"{T("SET_BRG_CPB")}", OnChangeReportPathClicked) as UIButton;
            UIComponent pathContainer = changePathBtn.parent;
            UIPanel pathPanel = pathContainer.AddUIComponent<UIPanel>();
            pathPanel.width = pathContainer.width;
            pathPanel.height = changePathBtn.height;
            changePathBtn.AlignTo(pathPanel, UIAlignAnchor.TopLeft);

            UIButton resetPathBtn = reportGroup.AddButton($"{T("SET_BRG_RPB")}", OnResetReportPathClicked) as UIButton;
            resetPathBtn.AlignTo(pathPanel, UIAlignAnchor.TopLeft);
            resetPathBtn.relativePosition += new UnityEngine.Vector3(changePathBtn.width + 10, 0);

            reportGroup.AddSpace(20);

            UIDropDown reportTypeDropdown = reportGroup.AddDropdown($"{T("SET_BRG_RTD")}:", ReportTypes.ToArray(), config.ReportType, SettingsManager.OnReportTypeChange) as UIDropDown;
            reportTypeDropdown.width = 180f;
            eventGlobalOptionsUpdated += c => reportTypeDropdown.selectedIndex = c.GeneralConfig.ReportType;
            UIComponent reportTypeContainer = reportTypeDropdown.parent;
            UIPanel reportGenPanel = reportTypeContainer.AddUIComponent<UIPanel>();
            reportGenPanel.width = reportTypeContainer.width;
            reportGenPanel.height = reportTypeDropdown.height;

            reportGroup.AddCheckbox($"{T("SET_BRG_OHRISO")}", config.OpenHtmlReportInSteamOverlay, SettingsManager.OnOpenHtmlReportInSteamChanged);

            UIButton openReportBtn = reportGroup.AddButton($"{T("SET_BRG_OOR")}", SettingsManager.OnOpenReports) as UIButton;

            UIButton generateReportBtn = reportGroup.AddButton($"{T("SET_BRG_OGR")}", SettingsManager.OnGenerateReports) as UIButton;
            openReportBtn.AlignTo(reportGenPanel, UIAlignAnchor.TopLeft);
            openReportBtn.relativePosition += new UnityEngine.Vector3(reportTypeDropdown.width + 30, -openReportBtn.height);
            generateReportBtn.AlignTo(reportGenPanel, UIAlignAnchor.TopLeft);
            generateReportBtn.relativePosition += new UnityEngine.Vector3(reportTypeDropdown.width + 30 + openReportBtn.width + 10, -openReportBtn.height);
        }

        private int FindLangIndex(string language) {
            var langs = Translation.instance.AvailableLangs;
            int index = langs.FindIndex(kv => kv.Key.Equals(language));
            return index > -1 ? index + 1 : 0;
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

        /// <summary>
        /// Helper method for translating keys
        /// </summary>
        /// <param name="key">Translation identifier</param>
        /// <returns></returns>
        private string T(string key) {
            return translator.T(key);
        }

        /// <summary>
        /// Helper method for translating keys
        /// </summary>
        /// <param name="key">Translation identifier</param>
        /// <param name="variableName">Search key to be replaced, without {}</param>
        /// <param name="value">Value for replacement</param>
        /// <returns></returns>
        private string T(string key, string variableName, string value) {
            return translator.T(key, variableName, value);
        }
    }
}
