namespace ModChecker.DataTypes
{
    public static class Enums                       // Needs to be public for XML serialization
    {
        // LogLevel to differentiate between log messages
        internal enum LogLevel
        {
            Info,
            Warning,
            Error,
            Debug
        }

        // DLCs; numbers are the AppIDs, see https://steamdb.info/search/?a=app_keynames&keyname=23&operator=3&keyvalue=Colossal+Order+Ltd.
        public enum DLC : uint
        {
            AfterDark = 369150,
            Snowfall = 420610,
            NaturalDisasters = 515191,
            MassTransit = 547502,
            GreenCities = 614580,
            ParkLife = 715191,
            Industries = 715194,
            Campus = 944071,
            SunsetHarbor = 1146930,
            Deluxe = 346791,
            MatchDay = 456200,
            Concerts = 614581,
            Carols = 715192,
            ArtDeco = 515190,
            HighTech = 547500,
            PearlsFromTheEast = 563850,
            EuropeanSuburbia = 715190,
            UniversityCity = 1059820,
            ModernCityCenter = 1148020
        }

        // Status of a mod; can be none, one or more
        public enum ModStatus
        {
            IncompatibleAccordingToWorkshop,    // The Workshop has an indication for seriously broken mods; incompatible with the game itself
            GameBreaking,
            MajorIssues,
            MinorIssues,
            UnconfirmedIssues,                  // Unused, only included to keep track for myself
            PerformanceImpact,
            LoadingTimeImpact,
            BreaksEditors,
            SavesCantLoadWithout,
            Abandonned,
            NoLongerNeeded,
            SourceUnavailable,
            SourceNotUpdated,
            SourceObfuscated,
            SourceBundled,
            CopyrightFreeMusic,                 // Only to be used for mods that include music
            CopyrightedMusic                    // Mod should not be used when streaming
        }

        // Compatibility status between two mods; can be one or more
        public enum CompatibilityStatus
        {
            Unknown,                            // Only used as placeholder when errors occur; overrules all other statuses
            NewerVersionOfTheSameMod,           //    \
            OlderVersionOfTheSameMod,           //     \
            SameModDifferentReleaseType,        //      }  Use max. one of the newer/older/same/func.covered statuses
            FunctionalityCoveredByThisMod,      //     /
            FunctionalityCoveredByOtherMod,     //    /
            IncompatibleAccordingToAuthor,      //  \
            IncompatibleAccordingToUsers,       //   }  Use max. one of the (in)compatible statuses
            CompatibleAccordingToAuthor,        //  /
            MinorIssues,
            RequiresSpecificConfigForThisMod,
            RequiresSpecificConfigForOtherMod
        }
    }
}
