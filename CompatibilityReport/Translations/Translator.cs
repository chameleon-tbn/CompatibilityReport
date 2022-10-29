#if DEBUG
#define TEST_READING
#endif
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CompatibilityReport.Util;
using UnityEngine;
using Logger = CompatibilityReport.Util.Logger;

namespace CompatibilityReport.Translations
{
    public class Translator
    {
        private string _localeCode;
        private Dictionary<string, string> _translations = new Dictionary<string, string>();
        private Dictionary<string, string> _translationsHtml = new Dictionary<string, string>();

        public int TranslationCount => _translations.Count;
        public string Code => _localeCode;

        public Hashtable HtmlTranslations => new Hashtable(_translationsHtml);
        
        public Translator(string localeCode) {
            _localeCode = localeCode;
            Load();
        }

        private void Load() {
            Logger.Log($"Loading translations for {_localeCode}");
            string translationsPath = Path.Combine(Toolkit.GetModPath(), "Translations");
            if (!Directory.Exists(translationsPath))
            {
                Logger.Log("Translations directory not found!", Logger.LogLevel.Error);
                return;
            }

            string filePath = Path.Combine(translationsPath, $"{_localeCode}.csv");
            if (!File.Exists(filePath))
            {
                Logger.Log($"Translation file for language {_localeCode} not found!", Logger.LogLevel.Error);
                return;
            }

            var translationData = File.ReadAllText(filePath, Encoding.UTF8);
            //HEADERS: Identifier,"Source String",Translation,Context,Labels,"Max. Length"
            using (var sr = new StringReader(translationData))
            {
                sr.ReadLine();//skip headers line
                while (true)
                {
                    string key = ReadCsvCell(sr, "id"); // Identifier
                    if (key.Length == 0)
                    {
                        break;
                    }
                    ReadCsvCell(sr, "sourceString"); // Source String (ignore)
                    string value = ReadCsvCell(sr, "translation");
                    ReadCsvCell(sr, "context"); // Context (ignore)
                    ReadCsvCell(sr, "labels"); // Labels (ignore)
                    ReadCsvCell(sr, "maxLength"); // max lenght(ignore)
                    if (!_translations.ContainsKey(key))
                    {
                        _translations.Add(key, value);
                        if (IsHtmlKey(key))
                        {
                            _translationsHtml.Add(key, value);
                        }
                    }
                    else
                    {
                        Logger.Log($"Translation key: {key} already exists! Skipping...", Logger.LogLevel.Warning);
                    }
                }
            }
            Logger.Log($"Loaded {_translations.Count} translations for {_localeCode} language");
        }

        private static bool IsHtmlKey(string key) {
            switch (key)
            {
                case string s when s.StartsWith("HRT_"):
                case string s2 when s2.StartsWith("HRTC_"):
                case string s3 when s3.StartsWith("REPORT_NOTE_"):
                    return true;
            }
            return false;
        }

        public string T(string key) {
            if (_translations.TryGetValue(key, out string translation))
            {
                return translation;
            }
            Logger.Log($"Translation [{key}] not found in language [{_localeCode}]");
            return key;
        }

        public string T(string key, string variableName, string value) {
            string translated = T(key);
            // replace {variableName} with value
            return translated.Replace($"{{{variableName}}}", value);
        }
        
        
        public string T_HTML(string key) {
            if (_translationsHtml.TryGetValue(key, out string translation))
            {
                return translation;
            }
            Logger.Log($"HTML Translation [{key}] not found in language [{_localeCode}]");
            return key;
        }
        
        /// <summary>
        /// Given a stringReader, read a CSV cell which can be a string until next comma, or quoted
        /// string (in this case double quotes are decoded to a quote character) and respects
        /// newlines \n too.
        /// </summary>
        /// <param name="sr">Source for reading CSV</param>
        /// <returns>Cell contents</returns>
        private static string ReadCsvCell(StringReader sr, string debugId) {
            var sb = new StringBuilder();
            if (sr.Peek() == '"') {
                sr.Read(); // skip the leading \"

                // The cell begins with a \" character, special reading rules apply
                while (true) {
                    int next = sr.Read();
                    if (next == -1) {
                        break; // end of the line
                    }

                    switch (next) {
                        case '\\': {
                            int special = sr.Read();
                            if (special == 'n') {
                                // Recognized a new line
                                sb.Append("\n");
                            } else {
                                // Not recognized, append as is
                                sb.Append("\\");
                                sb.Append((char)special, 1);
                            }

                            break;
                        }
                        case '\"': {
                            // Found a '""', or a '",', or a '"/r', or a '"/r'
                            int peek = sr.Peek();
                            switch (peek) {
                                case '\"': {
                                    sr.Read(); // consume the double quote
                                    sb.Append("\"");
                                    break;
                                }
                                case '\r':
                                    //Followed by a \r then \n or just \n - end-of-string
                                    sr.Read(); // consume double quote
                                    sr.Read(); // consume \r
                                    if (sr.Peek() == '\n') {
                                        sr.Read(); // consume \n
                                    }
#if !TEST_READING
                                    string text1 = sb.ToString();
                                    Logger.Log($"Cell content ({debugId}): [{text1}]");
                                    return text1;
#else
                                    return sb.ToString();
#endif
                                case '\n':
                                case ',':
                                case -1: {
                                    // Followed by a comma or end-of-string
                                    sr.Read(); // Consume the comma or newLine(LF)
#if !TEST_READING
                                    string text2 = sb.ToString();
                                    Logger.Log($"Cell content ({debugId}): [{text2}]");
                                    return text2;
#else
                                    return sb.ToString();
#endif
                                }
                                default: {
                                    // Followed by a non-comma, non-end-of-string
                                    sb.Append("\"");
                                    break;
                                }
                            }
                            break;
                        }
                        default: {
                            sb.Append((char)next, 1);
                            break;
                        }
                    }
                }
            } else {
                // Simple reading rules apply, read to the next comma, LF sequence or end-of-string
                while (true) {
                    int next = sr.Read();
                    if (next == -1 || next == ',' || next == '\n') {
                        break; // end-of-string, a newLine or a comma
                    }
                    if (next == '\r' && sr.Peek() == '\n') {
                        sr.Read(); //consume LF(\n) to complete CRLF(\r\n) newLine escape sequence
                        break;
                    }

                    sb.Append((char)next, 1);
                }
            }
            
#if !TEST_READING
            string text3 = sb.ToString();
            Logger.Log($"Cell content ({debugId}): [{text3}]");
            return text3;
#else
            return sb.ToString();
#endif
        }
    }
}
