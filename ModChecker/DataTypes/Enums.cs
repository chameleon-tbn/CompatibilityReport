namespace ModChecker.DataTypes
{
    // Needs to be public for XML serialization
    public static class Enums
    {
        // Status of a mod; can be none, one or more [Todo 0.4] Not all are used (yet)
        public enum ModStatus
        {
            Unknown,                                // Only used by the Updater; should not appear in the catalog
            IncompatibleAccordingToWorkshop,        // \    The Workshop has an indication for seriously broken mods; incompatible with the game itself
            GameBreaking,                           //  \
            MajorIssues,                            //   }  Use only one of these four
            MinorIssues,                            //  /
            UnconfirmedIssues,                      // Unused, only included to keep track for ourself
            PerformanceImpact,
            LoadingTimeImpact,
            BreaksEditors,
            SavesCantLoadWithout,
            Abandoned,
            UnlistedInWorkshop,
            RemovedFromWorkshop,
            NoLongerNeeded,
            NoDescription,                          // For mods without (real) description
            SourceUnavailable,
            SourceNotUpdated,
            SourceObfuscated,
            SourceBundled,
            CopyrightFreeMusic,                     // \
            CopyrightedMusic,                       //  }  Use only one of these three, and only for mods that include music
            CopyrightUnknownMusic                   // /
        }


        // Compatibility status between two mods; can be one or more
        public enum CompatibilityStatus
        {
            Unknown,                                // Only used as placeholder when errors occur; overrules all other statuses
            NewerVersionOfTheSame,                  // \
            OlderVersionOfTheSame,                  //  \
            SameModDifferentReleaseType,            //   }  Max. one of the newer/older/same/func.covered statuses
            FunctionalityCoveredByThis,             //  /
            FunctionalityCoveredByOther,            // /
            IncompatibleAccordingToAuthor,          //   \
            IncompatibleAccordingToUsers,           //    }  Max. one of the (in)compatible statuses
            CompatibleAccordingToAuthor,            //   /
            MinorIssues,
            RequiresSpecificConfigForThis,
            RequiresSpecificConfigForOther
        }


        // DLCs; the names are used in the report, with double underscores replaced by colon+space, and single underscores replaced by a space
        // Numbers are the AppIDs, as seen in the url of every DLC in the Steam shop (https://store.steampowered.com/app/255710/Cities_Skylines/)
        public enum DLC : uint
        {
            None = 0,                               // Unused
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


        // LogLevel to differentiate between log messages
        internal enum LogLevel
        {
            Info,
            Warning,
            Error,
            Debug
        }
    }
}
