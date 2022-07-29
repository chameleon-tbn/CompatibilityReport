using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ColossalFramework.PlatformServices;
using CompatibilityReport.CatalogData;
using CompatibilityReport.Settings;
using CompatibilityReport.Util;

namespace CompatibilityReport.Reporter.HtmlTemplates
{
    internal partial class HtmlReportTemplate
    {
        private const string isCameraScriptMessage = "This is a cinematic camera script, which technically is a mod and thus listed here.";
        private const string noKnownIssuesMessage = "No known issues or incompatibilities with your other mods.";
        private const string cannotReviewMessage = "Can't review local mods.";
        private readonly List<ModInfo> unsubscribe, majorIssues, minorIssues, remarks, nothingToReport;
        private readonly DateTime reportCreationTime;
        private readonly Catalog catalog;

        private bool IsDifferentVersion => Toolkit.CurrentGameVersion() != catalog.GameVersion();
        private bool IsOlder => Toolkit.CurrentGameVersion() < catalog.GameVersion();
        private bool ShowOutdatedWarning => Catalog.DownloadStarted && !Catalog.DownloadSuccessful;
        private int NonReviewedSubscriptions => catalog.SubscriptionCount() - catalog.ReviewedSubscriptionCount - catalog.LocalSubscriptionCount;
        private string CatalogGameVersion => Toolkit.ConvertGameVersionToString(catalog.GameVersion());
        private string CurrentGameVersion => Toolkit.ConvertGameVersionToString(Toolkit.CurrentGameVersion());

        public HtmlReportTemplate(Catalog catalog)
        {
            this.catalog = catalog;
            reportCreationTime = DateTime.Now;
            unsubscribe = new List<ModInfo>();
            majorIssues = new List<ModInfo>();
            minorIssues = new List<ModInfo>();
            remarks = new List<ModInfo>();
            nothingToReport = new List<ModInfo>();
            GenerateModInfo();
        }

        private string OptionalUrlLink(string url, bool isLink) => isLink ? HtmlExtensions.A(url) : url;
        
        // todo 0.8 or 0.9 identify if necessary to be read from catalog 
        private string CatalogHeaderText => catalog.ReportHeaderText;
        
        private InstalledModInfo[] AllModList()
        {
            List<InstalledModInfo> items = new List<InstalledModInfo>();
            foreach (string subscriptionName in catalog.GetSubscriptionNames())
            {
                foreach (ulong steamID in catalog.GetSubscriptionIDsByName(subscriptionName))
                {
                    Mod catalogMod = catalog.GetMod(steamID);
                    string disabled = (catalogMod.IsDisabled ? "Yes" : "");
                    bool isSteam = steamID > ModSettings.HighestFakeID;
                    string type = (isSteam ? "Steam" : steamID < ModSettings.LowestLocalModID ? "Built-in" : "Local");
                    string url = steamID > ModSettings.HighestFakeID ? Toolkit.GetWorkshopUrl(steamID) : $"{Toolkit.Privacy(catalogMod.ModPath)}";

                    items.Add(new InstalledModInfo(subscriptionName, disabled, type, isSteam, url));
                }
            }
            return items.ToArray();
        }

        private void GenerateModInfo()
        {
            if (GlobalConfig.Instance.GeneralConfig.ReportSortByName)
            {
                // Report mods sorted by name.
                List<string> AllSubscriptionNames = catalog.GetSubscriptionNames();

                foreach (string name in AllSubscriptionNames)
                {
                    // Get the Steam ID(s) for this mod name. There could be multiple IDs for mods with the same name.
                    foreach (ulong steamID in catalog.GetSubscriptionIDsByName(name))
                    {
                        CreateModText(steamID);
                    }
                }
            }
            else
            {
                // Report mods sorted by Steam ID.
                foreach (ulong steamID in catalog.GetSubscriptionIDs())
                {
                    CreateModText(steamID);
                }
            }
        }

        /// <summary>Creates the report object for a single mod.</summary>
        /// <remarks>The object is added to the relevant collection.</remarks>
        private void CreateModText(ulong steamID)
        {
            Mod subscribedMod = catalog.GetMod(steamID);
            Author subscriptionAuthor = catalog.GetAuthor(subscribedMod.AuthorID, subscribedMod.AuthorUrl);
            string authorName = subscriptionAuthor == null ? "" : subscriptionAuthor.Name;

            ModInfo modInfo = new ModInfo();
            modInfo.authorName = authorName;
            modInfo.modName = subscribedMod.Name;
            modInfo.idString = subscribedMod.IdString(true);
            modInfo.isDisabled = subscribedMod.IsDisabled;
            modInfo.isLocal = subscribedMod.SteamID >= ModSettings.LowestLocalModID && subscribedMod.SteamID <= ModSettings.HighestLocalModID;
            modInfo.isCameraScript = subscribedMod.IsCameraScript;
            if (!modInfo.isLocal)
            {
                modInfo.instability = Instability(subscribedMod);
                modInfo.requiredDlc = RequiredDlc(subscribedMod);
                modInfo.unneededDependencyMod = UnneededDependencyMod2(subscribedMod);
                modInfo.disabled = Disabled(subscribedMod);
                modInfo.successors = Successors(subscribedMod);
                modInfo.stability = Stability(subscribedMod);
                modInfo.compatibilities = Compatibilities(subscribedMod);
                modInfo.requiredMods = RequiredMods(subscribedMod);
                modInfo.statuses = Statuses(subscribedMod, authorRetired: (subscriptionAuthor != null && subscriptionAuthor.Retired));
                modInfo.note = ModNote(subscribedMod);
                modInfo.alternatives = Alternatives(subscribedMod);
                modInfo.reportSeverity = subscribedMod.ReportSeverity;
                modInfo.recommendations = subscribedMod.ReportSeverity <= Enums.ReportSeverity.MajorIssues ? Recommendations2(subscribedMod) : null;
                modInfo.anyIssues = subscribedMod.ReportSeverity == Enums.ReportSeverity.NothingToReport && subscribedMod.Stability > Enums.Stability.NotReviewed;
                modInfo.isCameraScript = subscribedMod.IsCameraScript;
                modInfo.steamUrl = subscribedMod.SteamID > ModSettings.HighestFakeID ? Toolkit.GetWorkshopUrl(steamID) : null;
            }
         
            switch (subscribedMod.ReportSeverity)
            {
                case Enums.ReportSeverity.Unsubscribe:
                    unsubscribe.Add(modInfo);
                    break;
                case Enums.ReportSeverity.MajorIssues:
                    majorIssues.Add(modInfo);
                    break;
                case Enums.ReportSeverity.MinorIssues:
                    minorIssues.Add(modInfo);
                    break;
                case Enums.ReportSeverity.Remarks:
                    remarks.Add(modInfo);
                    break;
                default:
                    nothingToReport.Add(modInfo);
                    break;
            }
        }

        /// <summary>Creates report message for stability issues of a mod and increases the report severity for the mod if appropriate.</summary>
        /// <remarks>Only reports major issues and worse.</remarks>
        /// <returns>Text wrapped in Message object.</returns>
        private Message Instability(Mod subscribedMod)
        {
            switch (subscribedMod.Stability)
            {
                case Enums.Stability.IncompatibleAccordingToWorkshop:
                    subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Unsubscribe);
                    return new Message(){message = "UNSUBSCRIBE! This mod is totally incompatible with the current game version.", details = subscribedMod.StabilityNote};

                case Enums.Stability.RequiresIncompatibleMod:
                    subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Unsubscribe);
                    return new Message(){message = "UNSUBSCRIBE! This requires a mod that is totally incompatible with the current game version.", details = subscribedMod.StabilityNote};

                case Enums.Stability.GameBreaking:
                    subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Unsubscribe);
                    return new Message(){message = "UNSUBSCRIBE! This mod breaks the game.", details = subscribedMod.StabilityNote};

                case Enums.Stability.Broken:
                    subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Unsubscribe);
                    return new Message(){message = "Unsubscribe! This mod is broken.", details = subscribedMod.StabilityNote};

                case Enums.Stability.MajorIssues:
                    subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.MajorIssues);
                    return new Message(){message = $"Unsubscribe would be wise. This has major issues{(string.IsNullOrEmpty(subscribedMod.StabilityNote) ? ". Check its Workshop page for details." : ":")}", details = subscribedMod.StabilityNote};

                default:
                    return null;
            }
        }

        /// <summary>Creates report message for the stability of a mod and increases the report severity for the mod if appropriate.</summary>
        /// <remarks>Only reports minor issues and better.</remarks>
        /// <returns>Text wrapped in Message object.</returns>
        private Message Stability(Mod subscribedMod)
        {
            switch (subscribedMod.Stability)
            {
                case Enums.Stability.MinorIssues:
                    subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.MinorIssues);
                    return new Message()
                    {
                        message = $"This has minor issues{(string.IsNullOrEmpty(subscribedMod.StabilityNote) ? ". Check its Workshop page for details." : ":")}",
                        details = subscribedMod.StabilityNote,
                    };

                case Enums.Stability.UsersReportIssues:
                    subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.MinorIssues);
                    return new Message()
                    {
                        message = $"Users are reporting issues{(string.IsNullOrEmpty(subscribedMod.StabilityNote) ? ". Check its Workshop page for details." : ": ")}",
                        details = subscribedMod.StabilityNote,
                    };
                case Enums.Stability.NotEnoughInformation:
                    subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Remarks);
                    string updatedText = subscribedMod.GameVersion() < Toolkit.CurrentMajorGameVersion() 
                        ? "." 
                        : subscribedMod.GameVersion() == Toolkit.CurrentGameVersion() 
                            ? ", but it was updated for the current game version."
                            : $", but it was updated for game version {subscribedMod.GameVersion().ToString(2)}.";
                    return new Message()
                    {
                        message = $"There is not enough information about this mod to know if it works well{updatedText}",
                        details = subscribedMod.StabilityNote,
                    };
                case Enums.Stability.Stable:
                    subscribedMod.IncreaseReportSeverity(string.IsNullOrEmpty(subscribedMod.StabilityNote) ? Enums.ReportSeverity.NothingToReport : Enums.ReportSeverity.Remarks);
                    return new Message()
                    {
                        message = $"This is compatible with the current game version.",
                        details = subscribedMod.StabilityNote,
                    };
                case Enums.Stability.NotReviewed:
                case Enums.Stability.Undefined:
                    string updatedText2 = subscribedMod.GameVersion() < Toolkit.CurrentMajorGameVersion() 
                        ? "."
                        : subscribedMod.GameVersion() == Toolkit.CurrentGameVersion() 
                            ? ", but it was updated for the current game version." 
                            : $", but it was updated for game version {subscribedMod.GameVersion().ToString(2)}.";
                    return new Message() { message = $"This mod has not been reviewed yet{updatedText2}", };
                default:
                    return null;
            }
        }


        /// <summary>Creates report MessageList object for the statuses of a mod and increases the report severity for the mod if appropriate.</summary>
        /// <remarks>Also reported: retired author. DependencyMod has its own method. ModNamesDiffer is reported in the mod note (at Catalog.ScanSubscriptions()).
        ///          Not reported: UnlistedInWorkshop, SourceObfuscated.</remarks>
        /// <returns>Message list, or an empty string if no reported status found.</returns>
        private MessageList Statuses(Mod subscribedMod, bool authorRetired)
        {
            var nestedItem = new MessageList() { messages = new List<Message>() };
            
            if (subscribedMod.Statuses.Contains(Enums.Status.Obsolete))
            {
                subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Unsubscribe);
                nestedItem.title = "Unsubscribe this. It is no longer needed.";
            }
            else if (subscribedMod.Statuses.Contains(Enums.Status.RemovedFromWorkshop))
            {
                subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.MajorIssues);
                nestedItem.title = "Unsubscribe would be wise. This is no longer available on the Steam Workshop.";
            }
            else if (subscribedMod.Statuses.Contains(Enums.Status.Deprecated))
            {
                subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.MajorIssues);
                nestedItem.title = "Unsubscribe would be wise. This is deprecated and no longer supported by the author.";
            }
            else if (subscribedMod.Statuses.Contains(Enums.Status.Abandoned))
            {
                nestedItem.title = authorRetired 
                    ? "This seems to be abandoned and the author seems retired. Future updates are unlikely."
                    : "This seems to be abandoned. Future updates are unlikely.";
            }
            else if (authorRetired)
            {
                nestedItem.title = "The author seems to be retired. Future updates are unlikely.";
            }

            if (subscribedMod.ReportSeverity < Enums.ReportSeverity.Unsubscribe)
            {
                // Several statuses only listed if there are no breaking issues.
                if (subscribedMod.Statuses.Contains(Enums.Status.Reupload))
                {
                    subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Unsubscribe);
                    nestedItem.messages.Add(new Message(){message = "Unsubscribe this. It is a re-upload of another mod, use that one instead (or its successor)."});
                }

                if (subscribedMod.Statuses.Contains(Enums.Status.NoDescription))
                {
                    subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.MinorIssues);
                    nestedItem.messages.Add(new Message(){message = "This has no description on the Steam Workshop. Support from the author is unlikely."});
                }

                if (subscribedMod.Statuses.Contains(Enums.Status.NoCommentSection))
                {
                    subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.MinorIssues);
                    nestedItem.messages.Add(new Message(){message = "This mod has the comment section disabled on the Steam Workshop, making it hard to see if other users are experiencing issues. " +
                        "Use with caution."});
                }

                if (subscribedMod.Statuses.Contains(Enums.Status.BreaksEditors))
                {
                    subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Remarks);
                    nestedItem.messages.Add(new Message(){message = "If you use the asset editor and/or map editor, this may give serious issues."});
                }

                if (subscribedMod.Statuses.Contains(Enums.Status.ModForModders))
                {
                    nestedItem.messages.Add(new Message(){message = "This is only needed for modders. Regular users don't need this one."});
                }

                if (subscribedMod.Statuses.Contains(Enums.Status.TestVersion))
                {
                    nestedItem.messages.Add(new Message(){message = "This is a test version" +
                        (subscribedMod.Alternatives.Any() ? ". If you don't have a specific reason to use it, you'd better use the stable version instead." :
                        subscribedMod.Stability == Enums.Stability.Stable ? ", but is considered quite stable." : ".")});
                }

                if (subscribedMod.Statuses.Contains(Enums.Status.MusicCopyrightFree))
                {
                    nestedItem.messages.Add(new Message(){message = "The included music is said to be copyright-free and safe for streaming. Some restrictions might still apply though."});
                }
                else if (subscribedMod.Statuses.Contains(Enums.Status.MusicCopyrighted))
                {
                    nestedItem.messages.Add(new Message(){message = "This includes copyrighted music and should not be used for streaming."});
                }
                else if (subscribedMod.Statuses.Contains(Enums.Status.MusicCopyrightUnknown))
                {
                    nestedItem.messages.Add(new Message(){message = "This includes music with unknown copyright status. Safer not to use it for streaming."});
                }
            }

            if (subscribedMod.Statuses.Contains(Enums.Status.SavesCantLoadWithout))
            {
                    nestedItem.messages.Add(new Message(){message = "NOTE: After using this mod, savegames won't (easily) load without it anymore."});
            }

            bool abandoned = subscribedMod.Statuses.Contains(Enums.Status.Obsolete) || subscribedMod.Statuses.Contains(Enums.Status.Deprecated) ||
                subscribedMod.Statuses.Contains(Enums.Status.RemovedFromWorkshop) || subscribedMod.Statuses.Contains(Enums.Status.Abandoned) ||
                (subscribedMod.Stability == Enums.Stability.IncompatibleAccordingToWorkshop) || authorRetired;

            if (abandoned && string.IsNullOrEmpty(subscribedMod.SourceUrl) && !subscribedMod.Statuses.Contains(Enums.Status.SourceBundled))
            {
                nestedItem.messages.Add(new Message(){message = "No public source code found, making it hard to continue by another modder."});
            }
            else if (abandoned && subscribedMod.Statuses.Contains(Enums.Status.SourceNotUpdated))
            {
                nestedItem.messages.Add(new Message(){message = "Published source seems out of date, making it hard to continue by another modder."});
            }

            if (!string.IsNullOrEmpty(nestedItem.title) || nestedItem.messages.Count > 0)
            {
                subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Remarks);
            }

            return nestedItem;
        }

        /// <summary>Creates report text for an unneeded dependency mod and increases the report severity for the mod if appropriate.</summary>
        /// <remarks>If this mod is a member of a group, all group members are considered for this check.</remarks>
        /// <returns>Text wrapped in Message object or null if this is not a dependency mod or if another subscription has this mod as required.</returns>
        private Message UnneededDependencyMod2(Mod subscribedMod)
        {
            if (!subscribedMod.Statuses.Contains(Enums.Status.DependencyMod))
            {
                return null;
            }

            // Check if any of the mods that need this is actually subscribed, enabled or not. If this is in a group, check all group members. Exit if any is needed.
            if (catalog.IsGroupMember(subscribedMod.SteamID))
            {
                foreach (ulong groupMemberID in catalog.GetThisModsGroup(subscribedMod.SteamID).GroupMembers)
                {
                    if (IsModNeeded(groupMemberID))
                    {
                        // Group member is needed. No need to check other group members.
                        return null;
                    }
                }
            }
            else if (IsModNeeded(subscribedMod.SteamID))
            {
                return null;
            }

            if (catalog.IsValidID(ModSettings.LowestLocalModID))
            {
                subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Remarks);

                return new Message()
                {
                    message = "Unsubscribe this unless it's needed for one of your local mods. " +
                        "None of your Steam Workshop mods need this, and it doesn't provide any functionality on its own."
                };
            }
            else
            {
                subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Unsubscribe);
                return new Message() { message = "Unsubscribe this. It is only needed for mods you don't have, and it doesn't provide any functionality on its own." };
            }
        }


        /// <summary>Checks if any of the mods that need this is actually subscribed, enabled or not.</summary>
        /// <returns>True if a mod needs this, otherwise false.</returns>
        private bool IsModNeeded(ulong SteamID)
        {
            // Check if any of the mods that need this is actually subscribed, enabled or not.
            List<Mod> ModsRequiringThis = catalog.Mods.FindAll(x => x.RequiredMods.Contains(SteamID));

            foreach (Mod mod in ModsRequiringThis)
            {
                if (catalog.GetSubscription(mod.SteamID) != null)
                {
                    // Found a subscribed mod that needs this.
                    return true;
                }
            }

            return false;
        }

        /// <summary>Creates report text for a disabled mod and increases the report severity for the mod if appropriate.</summary>
        /// <returns>Text wrapped in Message object, or an empty string if not disabled or if this mod works while disabled.</returns>
        private Message Disabled(Mod subscribedMod)
        {
            if (!subscribedMod.IsDisabled || subscribedMod.Statuses.Contains(Enums.Status.WorksWhenDisabled))
            {
                return null;
            }

            subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Unsubscribe);

            return new Message() {message = "Enable this if you want to use it, or unsubscribe it. Disabled mods can cause issues."};
        }


        /// <summary>Creates report text for a mod not and increases the report severity for the mod if appropriate.</summary>
        /// <returns>Formatted text, or an empty string if no mod note exists.</returns>
        private string ModNote(Mod subscribedMod)
        {
            if (string.IsNullOrEmpty(subscribedMod.Note))
            {
                return "";
            }

            subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Remarks);

            return subscribedMod.Note;
        }


        /// <summary>Creates report text for missing DLCs for a mod and increases the report severity for the mod if appropriate.</summary>
        /// <returns>Text wrapped in Message object, or null if no DLC is required or if all required DLCs are installed.</returns>
        private MessageList RequiredDlc(Mod subscribedMod)
        {
            var dlcs = new MessageList()
            {
                title = "Unsubscribe this. It requires DLC you don't have:",
                messages = new List<Message>()
            };

            foreach (Enums.Dlc dlc in subscribedMod.RequiredDlcs)
            {
                if (!PlatformService.IsDlcInstalled((uint)dlc))
                {
                    // Add the missing DLC.
                    dlcs.messages.Add(new Message() {message = Toolkit.ConvertDlcToString(dlc)});
                }
            }

            if (dlcs.messages.Count == 0)
            {
                return null;
            }

            subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Unsubscribe);

            return dlcs;
        }
        
        /// <summary>Creates report text for missing 'required mods' for a mod and increases the report severity for the mod if appropriate.</summary>
        /// <remarks>If a required mod is not subscribed but in a group, the other group members are checked. 
        ///          Required mods that are disabled are mentioned as such.</remarks>
        /// <returns>MessageList object filled with messages, or null if this requires no other mods or all required mods are subscribed and enabled.</returns>
        private MessageList RequiredMods(Mod subscribedMod)
        {
            var item = new MessageList()
            {
                title = "This mod requires other mods you don't have, or which are not enabled:",
                messages = new List<Message>()
            };

            foreach (ulong steamID in subscribedMod.RequiredMods)
            {
                if (catalog.IsValidID(steamID))
                {
                    var mod = ModAndGroupItem(steamID);
                    if (mod != null)
                    {
                        item.messages.Add(mod);
                    }
                }
                else
                {
                    // Mod not found in the catalog, which should not happen unless bugs or a manual edit of the catalog.
                    Logger.Log($"Required mod {steamID} not found in catalog.", Logger.Debug);
                    item.messages.Add(new Message() {message = $"[Steam ID {steamID,10}]"});
                }
            }

            if (item.messages.Count == 0)
            {
                return null;
            }

            subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.MajorIssues);

            return item;
        }

        /// <summary>Creates report text for recommended mods for a mod and increases the report severity for the mod if appropriate.</summary>
        /// <remarks>If a recommended mod is not subscribed but in a group, the other group members are checked. 
        ///          Recommended mods that are disabled are mentioned as such.</remarks>
        /// <returns>MessageList object filled with messages, or null if this mod has no recommendations or all recommended mods are subscribed and enabled.</returns>
        private MessageList Recommendations2(Mod subscribedMod)
        {
            MessageList list = new MessageList
            {
                title = "The author or the users of this mod recommend using the following as well:",
                messages = new List<Message>()
            };
            
            foreach (ulong steamID in subscribedMod.Recommendations)
            {
                if (catalog.IsValidID(steamID))
                {
                    var item = ModAndGroupItem(steamID);
                    if (item != null)
                    {
                        list.messages.Add(item);
                    }
                }
                else
                {
                    // Mod not found in the catalog, which should not happen unless bugs or a manual edit of the catalog.
                    Logger.Log($"Recommended mod {steamID} not found in catalog.", Logger.Debug);

                    list.messages.Add(new Message() {message = $"[Steam ID {steamID,10}]"});
                }
            }

            if (list.messages.Count == 0)
            {
                return null;
            }

            subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Remarks);

            return list;
        }
        
        /// <summary>Checks if a mod or any member of its group is subscribed and enabled.</summary>
        /// <returns>A Message with text for the report, or null if the mod or another group member is subscribed and enabled.</returns>
        private Message ModAndGroupItem(ulong steamID)
        {
            Mod catalogMod = catalog.GetSubscription(steamID);

            if (catalogMod != null && (!catalogMod.IsDisabled || catalogMod.Statuses.Contains(Enums.Status.WorksWhenDisabled)))
            {
                // Mod is subscribed and enabled (or works when disabled). Don't report.
                return null;
            }
            catalogMod = catalog.GetMod(steamID);

            if (catalogMod.IsDisabled)
            {
                // Mod is subscribed and disabled. Report as "missing", without Workshop page.
                return new Message() { message = catalog.GetMod(steamID).ToString(hideFakeID: true) };
            }

            if (!catalog.IsGroupMember(steamID))
            {
                // Mod is not subscribed and not in a group. Report as missing with Workshop page.
                return new Message() { message = catalogMod.NameWithIDAsLink(true, false) };
            }
            
            // Mod is not subscribed but in a group. Check if another group member is subscribed.
            foreach (ulong groupMemberID in catalog.GetThisModsGroup(steamID).GroupMembers)
            {
                Mod groupMember = catalog.GetSubscription(groupMemberID);

                if (groupMember != null)
                {
                    // Group member is subscribed. No need to check other group members, but report as "missing" if disabled (unless it works when disabled).
                    if (!groupMember.IsDisabled || groupMember.Statuses.Contains(Enums.Status.WorksWhenDisabled))
                    {
                        return null;
                    }
                    return new Message() { message = groupMember.ToString(hideFakeID: true) };
                }
            }

            // No group member is subscribed. Report original mod as missing.
            return new Message() { message = catalogMod.NameWithIDAsLink(true, false) };
        }

        /// <summary>Creates report text for successors of a mod and increases the report severity for the mod if appropriate.</summary>
        /// <returns>Text wrapped in Message object, or null if this mod has no successors.</returns>
        private MessageList Successors(Mod subscribedMod)
        {
            if (!subscribedMod.Successors.Any())
            {
                return null;
            }

            MessageList successors = new MessageList();
            successors.title = (subscribedMod.Successors.Count == 1)
                ? "This is succeeded by:"
                : "This is succeeded by any of the following (pick one, not all):";

            List<Message> successorsCollection = new List<Message>();
            successors.messages = successorsCollection;

            foreach (ulong steamID in subscribedMod.Successors)
            {
                Message s = new Message();
                if (catalog.IsValidID(steamID))
                {
                    if (catalog.GetSubscription(steamID) != null)
                    {
                        subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Unsubscribe);

                        s.message = "Unsubscribe this. It is succeeded by a mod you already have:";
                        s.details = catalog.GetMod(steamID).ToString(hideFakeID: true);
                        successorsCollection.Add(s);
                        return successors;
                    }

                    s.message = catalog.GetMod(steamID).ToString(hideFakeID: true);
                    s.details = $"Workshop page: {HtmlExtensions.A(Toolkit.GetWorkshopUrl(steamID))}";
                    successorsCollection.Add(s);
                }
                else
                {
                    // Mod not found in the catalog, which should not happen unless bugs or a manual edit of the catalog.
                    Logger.Log($"Successor mod {steamID} not found in catalog.", Logger.Debug);

                    s.message = $"[Steam ID {steamID,10}]";
                    successorsCollection.Add(s);
                }
            }

            subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.MinorIssues);

            return successors;
        }
        
        /// <summary>Creates report text for alternatives for a mod and increases the report severity for the mod if appropriate.</summary>
        /// <returns>Text wrapped in Message object, or null if this mod has no alternatives.</returns>
        private MessageList Alternatives(Mod subscribedMod)
        {
            if (!subscribedMod.Alternatives.Any())
            {
                return null;
            }
            
            var result = new MessageList()
            {
                title = subscribedMod.Alternatives.Count == 1 
                    ? "An alternative you could use:"
                    : "Some alternatives for this are (pick one, not all):",
                messages = new List<Message>()
            };

            foreach (ulong steamID in subscribedMod.Alternatives)
            {
                Mod alternativeMod = catalog.GetSubscription(steamID);

                if (alternativeMod != null && (!alternativeMod.IsDisabled || alternativeMod.Statuses.Contains(Enums.Status.WorksWhenDisabled)))
                {
                    // Already subscribed, don't report any.
                    return null;
                }

                if (catalog.IsValidID(steamID))
                {
                    result.messages.Add(new Message(){message = catalog.GetMod(steamID).NameWithIDAsLink(true, false)});
                }
                else
                {
                    // Mod not found in the catalog, which should not happen unless bugs or a manual edit of the catalog.
                    Logger.Log($"Alternative mod {steamID} not found in catalog.", Logger.Debug);

                    result.messages.Add(new Message(){message = $"[Steam ID {steamID,10}]"});
                }
            }

            subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Remarks);

            return result;
        }
        
        /// <summary>Creates report text for compatibility issues with other subscribed mods, and increases the report severity for the mod if appropriate.</summary>
        /// <remarks>Result could be multiple mods with multiple statuses. Not reported: CompatibleAccordingToAuthor.</remarks>
        /// <returns>Text wrapped in Message object, or null if there are no known compatibility issues.</returns>
        private MessageList Compatibilities(Mod subscribedMod)
        {

            var result = new MessageList()
            {
                title = string.Empty,
                messages = new List<Message>()
            };
            
            foreach (Compatibility compatibility in catalog.GetSubscriptionCompatibilities(subscribedMod.SteamID))
            {
                ulong otherModID = (subscribedMod.SteamID == compatibility.FirstModID) ? compatibility.SecondModID : compatibility.FirstModID;

                Mod otherMod = catalog.GetMod(otherModID);
                if (subscribedMod.Successors.Contains(otherModID) || otherMod == null || otherMod.Successors.Contains(subscribedMod.SteamID))
                {
                    // Don't mention the incompatibility if either mod is the others successor. The succeeded mod will already be mentioned in 'Unsubscribe' severity.
                    continue;
                }

                string otherModString = catalog.GetMod(otherModID).NameWithIDAsLink(false, idFirst: false);

                Message item = new Message();
                
                switch (compatibility.Status)
                {
                    case Enums.CompatibilityStatus.SameModDifferentReleaseType:
                        subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Unsubscribe);
                        item.message = "Unsubscribe either this or the other edition of the same mod:";
                        item.details = otherModString + compatibility.Note;
                        break;

                    case Enums.CompatibilityStatus.SameFunctionality:
                        subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Unsubscribe);
                        item.message = "Unsubscribe either this or the following incompatible mod with similar functionality:";
                        item.details = otherModString + compatibility.Note;
                        break;

                    case Enums.CompatibilityStatus.IncompatibleAccordingToAuthor:
                        subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Unsubscribe);
                        item.message = "Unsubscribe either this one or the following mod it's incompatible with:";
                        item.details = otherModString + compatibility.Note;
                        break;

                    case Enums.CompatibilityStatus.IncompatibleAccordingToUsers:
                        subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.MajorIssues);
                        item.message = "Users report an incompatibility with:";
                        item.details = otherModString + compatibility.Note;
                        break;

                    case Enums.CompatibilityStatus.MajorIssues:
                        subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.MajorIssues);
                        item.message = "This has major issues with:";
                        item.details = otherModString + compatibility.Note;
                        break;

                    case Enums.CompatibilityStatus.MinorIssues:
                        subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.MinorIssues);
                        item.message = "This has minor issues with:";
                        item.details = otherModString + compatibility.Note;
                        break;

                    case Enums.CompatibilityStatus.RequiresSpecificSettings:
                        subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Remarks);
                        item.message = "This requires specific configuration to work together with:";
                        item.details = otherModString + compatibility.Note;
                        break;

                    case Enums.CompatibilityStatus.SameFunctionalityCompatible:
                        subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Remarks);
                        item.message = "This has very similar functionality, but is still compatible with (do you need both?):";
                        item.details = otherModString + compatibility.Note;
                        break;

                    case Enums.CompatibilityStatus.CompatibleAccordingToAuthor:
                        if (!string.IsNullOrEmpty(compatibility.Note))
                        {
                            subscribedMod.IncreaseReportSeverity(Enums.ReportSeverity.Remarks);
                            item.message = "This is compatible with:";
                            item.details = otherModString + compatibility.Note;
                        }
                        break;

                    default:
                        break;
                }
                
                result.messages.Add(item);
            }

            return result;
        }

        private class ModInfo
        {
            public bool isLocal;
            public string authorName;
            public string modName;
            public string idString;
            public bool isDisabled;
            public bool isCameraScript;
            public Enums.ReportSeverity reportSeverity;
            public Message instability;
            public MessageList requiredDlc;
            public Message unneededDependencyMod;
            public Message disabled;
            public MessageList successors;
            public Message stability;
            public MessageList compatibilities;
            public MessageList requiredMods;
            public MessageList statuses;
            public string note;
            public MessageList alternatives;
            public MessageList recommendations;
            public bool anyIssues;
            public string steamUrl;
        }

        private class InstalledModInfo
        {
            public string disabled;
            public bool isSteam;

            public string subscriptionName;
            public string type;
            public string url;

            internal InstalledModInfo(string subscriptionName, string disabled, string type, bool isSteam, string url)
            {
                this.subscriptionName = subscriptionName;
                this.disabled = disabled;
                this.type = type;
                this.isSteam = isSteam;
                this.url = url;
            }
        }
    }

    internal class MessageList
    {
        public string title;
        public List<Message> messages;
    }

    internal class Message
    {
        public string message;
        public string details;
    }
}
