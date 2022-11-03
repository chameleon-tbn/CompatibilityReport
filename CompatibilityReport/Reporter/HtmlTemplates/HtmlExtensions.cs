using System;
using System.Collections.Generic;
using CompatibilityReport.CatalogData;
using CompatibilityReport.Util;

namespace CompatibilityReport.Reporter.HtmlTemplates
{
    /// <summary>
    /// Set of extensions to wrap text in html tags
    /// </summary>
    public static class HtmlExtensions
    {

        public static string Attribute(this string name, string content)
        {
            return string.IsNullOrEmpty(content) ? string.Empty : $"{name}=\"{content}\"";
        }

        public static string Classes(this string classes)
        {
            return string.IsNullOrEmpty(classes) ? string.Empty : "class".Attribute(classes);
        }

        public static string T(this string key) {
            return string.IsNullOrEmpty(key) ? string.Empty : "data-i18n".Attribute(key);
        }
        
        public static string TVars(this string vars) {
            return string.IsNullOrEmpty(vars) ? string.Empty : "data-i18n-vars".Attribute(vars);
        }
        
        public static string TVal(this string value) {
            return string.IsNullOrEmpty(value) ? string.Empty : "data-i18n-value".Attribute(value);
        }
        
        public static string PrefixVal(this string value) {
            return string.IsNullOrEmpty(value) ? string.Empty : "data-i18n-prefix-value".Attribute(value);
        }
        
        public static string Tag(this string name, string content, string classes = null, string localeId = null, string localeVars = null, string localePrefix = null, string localeValue = null)
        {
            return string.IsNullOrEmpty(content) ? string.Empty : $"<{name} {classes.Classes()} {T(localeId)} {TVars(localeVars)} {PrefixVal(localePrefix)} {TVal(localeValue)}>{content}</{name}>";
        }
        
        public static string A(this string url, string text= null, string classes = null, bool newTab = false)
        {
            return string.IsNullOrEmpty(url) ? string.Empty : $"<a {"href".Attribute(url)} {classes.Classes()} {"target".Attribute(newTab ? "_blank" : null)}>{text ?? url}</a>";
        }
        
        public static string TagConditional(this string name, bool shouldRender, string content, string classes = null, string localeId = null)
        {
            return string.IsNullOrEmpty(content) || !shouldRender ? string.Empty : $"<{name} {classes.Classes()} {T(localeId)}>{content}</{name}>";
        }

        internal static string NestedLi(this List<Message> items)
        {
            if (items != null)
            {
                string list = "";
                foreach (Message listItem in items)
                {
                    list += "li".Tag( "span".Tag(listItem.message, "message", localeId: listItem.messageLocaleId, localeVars: listItem.localeIdVariables) + "ul".Tag("li".Tag(listItem.details, "details", localeId: listItem.detailsLocaleId, localeValue:listItem.detailsValue, localePrefix: listItem.detailsLocalized)));
                }
                return list;
            }
            return string.Empty;
        }
        
        public static string AsLink(this string link, string text = null, string classes = null, bool newTab = false)
        {
            return A(link, text, classes, newTab);
        }
        
        public static string NameWithIDAsLink(this Mod mod, bool fakeId = true, bool idFirst = false)
        {
            return idFirst 
                ? $"{ mod.IdString(fakeId)} {AsLink(Toolkit.GetWorkshopUrl(mod.SteamID), mod.Name, newTab: true) }"
                : $"{ A(Toolkit.GetWorkshopUrl(mod.SteamID), mod.Name, newTab: true)} {mod.IdString(fakeId) }";
        }

        public static string NameAuthorWithIDAsLink(string name, string author, string url, string idString)
        {
            return $"{ (string.IsNullOrEmpty(url) ? "span".Tag(name, "modName") : url.AsLink(name, "modName", true)) } {"span".Tag("by", localeId: "HE_NAWIDAL_S")} " +
                $"{ "span".Tag(author, "author") }&nbsp;{ "span".Tag(idString, "steamId") }";
        }
    }
}
