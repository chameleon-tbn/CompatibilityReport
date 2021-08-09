namespace CompatibilityReport.DataTypes
{
    // Needs to be public for XML serialization
    public static class Enums
    {
        // Statuses of a mod, can be none, one or more. Not all can be combined, for instance use only one of the 'issues'. [Todo 0.4] Not all are used (yet)
        public enum ModStatus
        {
            Unknown,                                // [Only used by the Updater; should not appear in the catalog]
            IncompatibleAccordingToWorkshop,        // The Workshop has an indication for seriously broken mods; incompatible with the game itself
            GameBreaking,                           // Broken and also crashes or otherwise disrupts the game
            Broken,                                 // Broken, as in doesn't really function
            MajorIssues,                            // Will function (at least partially), but with some serious issues
            MinorIssues,                            // Will function but you might run into minor issues
            UnconfirmedIssues,                      // [Only used by the updater to keep track of possible issues]
            PerformanceImpact,                      // Negatively impacts performance: fps drops, stuttering, etc.
            LoadingTimeImpact,                      // Increases loading time for the game, but should not have an impact after
            BreaksEditors,                          // Gives serious issues in the map and/or asset editor, or prevents them from loading
            SavesCantLoadWithout,                   // This mod is needed to successfully load a savegame where it was previously used
            TestVersion,                            // This is a test/beta/alpha/experimental version; use only when a stable version exists, to differentiate between them
            Deprecated,                             // No longer supported and should not be used anymore
            Abandoned,                              // No longer maintained and might give issues or break with future game updates
            NoDescription,                          // For mods without a (real) description in the Workshop, which indicates a sparsely supported mod
            NoCommentSectionOnWorkshop,             // This mods Workshop page has the comment section disabled, making it hard to see if people are experiencing issues
            UnlistedInWorkshop,                     // Available in the Workshop, but not listed anywhere or returned in searches; can only be found with a direct link
            RemovedFromWorkshop,                    // Once available in the Workshop, but no more; better not to use anymore
            NoLongerNeeded,                         // Obsolete, because whatever it did is now done by the game itself or by another mod it was a patch/addon for
            DependencyMod,                          // This is only a dependency mod and adds no functionality on its own
            ModForModders,                          // Only needed for modders, to help in creating mods or assets; no use for regular players
            MusicCopyrightFree,                     // This mod uses music, but only music that is copyright-free. Safe for videos and streaming
            MusicCopyrighted,                       // This mod uses music with copyright. Should not be used in videos and streaming
            MusicCopyrightUnknown,                  // This mod uses music, but it's unclear whether that music has copyright on it or not. Not safe for videos or streaming
            SourceUnavailable,                      // No source files available; making it hard for other modders to support compatibility, or take over when abandoned
            SourceNotUpdated,                       // Source files are not updated; making it hard for other modders to support compatibility, or take over when abandoned
            SourceObfuscated,                       // The author has deliberately hidden the mod code from other modders; somewhat suspicious
            SourceBundled                           // The source files are bundled with the mod and can be found in the mods folder
        }


        // Compatibility statuses between two mods, can be one or more. Not all can be combined, for instance use only one of the 'newer/same/covered' statuses.
        // All compatibilities are from the perspective of the first mod mentioned in the compatibility. Don't create 'mirrored' compatibilities, the mod handles this.
        public enum CompatibilityStatus
        {
            Unknown,                                // [Only used by the Updater; should not appear in the catalog]
            NewerVersion,                           // The first mod is a newer version of the second
            SameModDifferentReleaseType,            // Both mods are different release types ('stable' vs. 'beta', etc.) of the same mod; first should be the 'stable'
            SameFunctionality,                      // Both mods do the same thing, for instance different versions or similar mods from different authors
            FunctionalityCovered,                   // The first mod has all functionality of the second (and maybe more), so no need to have the second
            IncompatibleAccordingToAuthor,          // These mods are incompatible according to the author of the first mod
            IncompatibleAccordingToUsers,           // These mods are incompatible according to users of the first mod; should only be used on 'clear cases', not on a whim
            CompatibleAccordingToAuthor,            // These mods are fully compatible according to the author of the first mod
            MinorIssues,                            // These mods have minor issues when used together; use the compatibility note to clarify
            RequiresSpecificSettings,               // These mods require specific settings when used together; use the compatibility note to clarify
            OlderVersion,                           // [Only used by the Updater; use the 'NewerVersion' status]
            FunctionalityCoveredByOther,            // [Only used by the Updater; use the 'FunctionalityCovered' status]
        }


        // DLCs; the names are used in the report, with double underscores replacing colon+space, and single underscores replacing a space
        // Numbers are the AppIDs, as seen in the url of every DLC in the Steam shop (https://store.steampowered.com/app/255710/Cities_Skylines/)
        public enum DLC : uint
        {
            Unknown = 0,                            // Unused
            Deluxe_Edition = 346791,
            After_Dark = 369150,
            Snowfall = 420610,
            Match_Day = 456200,
            Content_Creator_Pack__Art_Deco = 515190,
            Natural_Disasters = 515191,
            Stadiums_Europe = 536610,
            Content_Creator_Pack__High_Tech_Buildings = 547500,
            Relaxation_Station = 547501,
            Mass_Transit = 547502,
            Pearls_From_the_East = 563850,
            Green_Cities = 614580,
            Concerts = 614581,
            Rock_City_Radio = 614582,
            Content_Creator_Pack__European_Suburbia = 715190,
            Park_Life = 715191,
            Carols_Candles_and_Candy = 715192,
            All_That_Jazz = 715193,
            Industries = 715194,
            Country_Road_Radio = 815380,
            Synthetic_Dawn_Radio = 944070,
            Campus = 944071,
            Content_Creator_Pack__University_City = 1059820,
            Deep_Focus_Radio = 1065490,
            Campus_Radio = 1065491,
            Sunset_Harbor = 1146930,
            Content_Creator_Pack__Modern_City_Center = 1148020,
            Downtown_Radio = 1148021,
            Content_Creator_Pack__Modern_Japan = 1148022,
            Coast_to_Coast_Radio = 1196100,
            Rail_Hawk_Radio = 1531472,
            Sunny_Breeze_Radio = 1531473,
            Content_Creator_Pack__Train_Station = 1531470,
            Content_Creator_Pack__Bridges_and_Piers = 1531471
        }
    }
}
