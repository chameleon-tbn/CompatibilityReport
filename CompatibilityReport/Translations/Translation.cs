using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ColossalFramework;
using ColossalFramework.Globalization;
using ColossalFramework.UI;
using CompatibilityReport.Settings;
using CompatibilityReport.Settings.ConfigData;
using CompatibilityReport.Util;
using UnityEngine;
using Logger = CompatibilityReport.Util.Logger;

namespace CompatibilityReport.Translations
{

    public class Translation : SingletonLite<Translation>
    {
        internal const string LANGUAGE_KEY = "LANGUAGE";
        internal const string DEFAULT_LANGUAGE_CODE = "en_US";

        // public static Translator Translator { get; set; }

        private Dictionary<string, Translator> _translators = new Dictionary<string, Translator>();
        private Translator _fallbackTranslator;
        private Translator _current;

        public Translator Current
        {
            get { return _current ?? _fallbackTranslator; }
            private set { _current = value; }
        }

        public Translator[] All => _translators.Values.ToArray();

        internal Translator Fallback => _fallbackTranslator;

        public readonly List<KeyValuePair<string, string>> AvailableLangs = new List<KeyValuePair<string, string>>();

        public Translation() {
            LocaleManager.eventLocaleChanged += OnGameLocaleChanged;
        }

        private void OnGameLocaleChanged() {
            if (!LocaleManager.exists) return;
            string currentLanguage = LocaleManager.instance.language;
            Logger.Log($"Locale Changed to: {currentLanguage}. Updating...");
        }

        public void Dispose() {
            LocaleManager.eventLocaleChanged -= OnGameLocaleChanged;
        }

        public void ChangeLanguage(string code) {
            if (Current.Code.Equals(code))
            {
                Logger.Log($"Language {code} already selected.");
                return;
            }

            if (!_translators.TryGetValue(code, out Translator translator))
            {
                Logger.Log($"Language {code} not found!", Logger.LogLevel.Warning);
                return;
            }
            GlobalConfig.Instance.GeneralConfig.Language = code;
            GlobalConfig.WriteConfig();
            
            Current = translator;
        }
        
        public void LoadAll() {
            _translators.Clear();
            AvailableLangs.Clear();

            string translationsPath = Path.Combine(Toolkit.GetModPath(), "Translations");
            if (!Directory.Exists(translationsPath))
            {
                Logger.Log("Translations directory not found!", Logger.LogLevel.Error);
                _fallbackTranslator = new Translator("en");
                return;
            }

            string[] files = Directory.GetFiles(translationsPath, "*.csv", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                string code = Path.GetFileNameWithoutExtension(file);
                if (!_translators.TryGetValue(code, out Translator translator))
                {
                    translator = new Translator(code);
                }

                if (translator != null && translator.TranslationCount > 0)
                {
                    if (translator.Code == DEFAULT_LANGUAGE_CODE)
                    {
                        _fallbackTranslator = translator;
                    }
                    
                    if (!_translators.ContainsKey(code))
                    {
                        Logger.Log($"Adding available lang: {code} display name: {translator.T(LANGUAGE_KEY)}");
                        AvailableLangs.Add(new KeyValuePair<string, string>(code, translator.T(LANGUAGE_KEY)));
                        _translators.Add(code, translator);
                    }
                }
            }
            AvailableLangs.Sort((pair, valuePair) => pair.Value.CompareTo(valuePair.Value));
        }

        public void SetCurrentLanguage() {
            var language = GlobalConfig.Instance.GeneralConfig.Language;
            if (!LocaleManager.exists) return;
            
            if (string.IsNullOrEmpty(language) || language.Equals(GeneralConfig.GAME_DEFAULT_LANG))
            {
                Logger.Log($"Attempting to apply game language: {LocaleManager.instance.language}");
                Current = FindTranslator(LocaleManager.instance.language);
            }
            else
            {
                Logger.Log($"Setting {language} language...");
                var lang = FindTranslator(language);
                if (lang == null)
                {
                    Logger.Log($"Could not apply {language} language. Fallback to {DEFAULT_LANGUAGE_CODE}");
                }
                else
                {
                    Logger.Log($"Language {language} found. Applying.");
                    Current = lang;
                }
            }
        }

        private Translator FindTranslator(string code) {
            if (_translators.TryGetValue(code, out Translator translator))
            {
                Logger.Log($"Found {code} language translator");  
                return translator;
            }

            var firstMatch = _translators.FirstOrDefault(k => k.Key.StartsWith(code.Substring(0, 2)));
            if (!string.IsNullOrEmpty(firstMatch.Key))
            {
                Logger.Log($"Found {code} language translator by short locale name");  
                return firstMatch.Value;
            }
            
            Logger.Log($"Could not find {code} language in translations");    
            return null;
        }

        private void ChangeLanguageByIndex(int index) {
            if (index <= 0)
            {
               SetCurrentLanguage();
               GlobalConfig.Instance.GeneralConfig.Language = string.Empty;
               GlobalConfig.WriteConfig();
            }
            else
            {
                string code = AvailableLangs[index - 1].Key;
                if (code == Current.Code)
                {
                    return;
                }
                GlobalConfig.Instance.GeneralConfig.Language = code;
                GlobalConfig.WriteConfig();
                ChangeLanguage(code);
            }
            RebuildOptions();
        }
        
        /// <summary>
        /// Inform the main Options window of the C:S about language change. This should rebuild the
        /// options tab for TM:PE.
        /// </summary>
        public static void RebuildOptions() {
            // Inform the Main Options Panel about locale change, recreate the categories
            MethodInfo onChangedHandler = typeof(OptionsMainPanel)
                .GetMethod(
                    "OnLocaleChanged",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            if (onChangedHandler == null) {
                Logger.Log("Cannot rebuild options panel, OnLocaleChanged handler is null", Logger.LogLevel.Error);
                return;
            }

            Logger.Log("Informing the main OptionsPanel about the locale change...", Logger.LogLevel.Debug);
            onChangedHandler.Invoke(
                UIView.library.Get<OptionsMainPanel>("OptionsPanel"),
                new object[] { });
        }

        /// <summary>
        /// Defers execution to next frame to eliminate NullReferenceException
        /// Invoked from change event, changing language forces Option panel rebuild so event propagation may encounter a destroyed component
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        internal IEnumerator ChangeLanguageByIndex_Deferred(int index) {
            yield return null;
            ChangeLanguageByIndex(index);
        }
    }
}
