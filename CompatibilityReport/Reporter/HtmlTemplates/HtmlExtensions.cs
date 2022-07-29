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
            return string.IsNullOrEmpty(content) ? String.Empty : $"{name}=\"{content}\"";
        }

        public static string Classes(this string classes)
        {
            return string.IsNullOrEmpty(classes) ? string.Empty : "class".Attribute(classes);
        }
        
        public static string Tag(this string name, string content, string classes = null)
        {
            return string.IsNullOrEmpty(content) ? string.Empty : $"<{name} {classes.Classes()}>{content}</{name}>";
        }
        
        public static string A(this string url, string text= null, string classes = null)
        {
            return string.IsNullOrEmpty(url) ? string.Empty : $"<a {"href".Attribute(url)} {classes.Classes()}>{text ?? url}</a>";
        }
        
        public static string TagConditional(this string name, bool shouldRender, string content, string classes = null)
        {
            return string.IsNullOrEmpty(content) || !shouldRender ? string.Empty : $"<{name} {classes.Classes()}>{content}</{name}>";
        }

        internal static string NestedLi(this List<Message> items)
        {
            if (items != null)
            {
                string list = "";
                foreach (Message listItem in items)
                {
                    list += "li".Tag( listItem.message + "ul".Tag("li".Tag(listItem.details, "details")), "message");
                }
                return list;
            }
            return string.Empty;
        }
        
        public static string AsLink(this string link, string text = null, string classes = null)
        {
            return A(link, text, classes);
        }
        
        public static string NameWithIDAsLink(this Mod mod, bool fakeId = true, bool idFirst = false)
        {
            return idFirst 
                ? $"{ mod.IdString(fakeId)} {AsLink(Toolkit.GetWorkshopUrl(mod.SteamID), mod.Name) }"
                : $"{ A(Toolkit.GetWorkshopUrl(mod.SteamID), mod.Name)} {mod.IdString(fakeId) }";
        }

        public static string NameAuthorWithIDAsLink(string name, string author, string url, string idString)
        {
            return $"{ (string.IsNullOrEmpty(url) ? "span".Tag(name, "modName") : url.AsLink(name, "modName")) } by " +
                $"{ "span".Tag(author, "author") }&nbsp;{ "span".Tag(idString, "steamId") }";
        }
    }
}
