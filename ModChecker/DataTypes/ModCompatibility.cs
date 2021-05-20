using System;
using System.Collections.Generic;
using System.Linq;
using ModChecker.Util;


namespace ModChecker.DataTypes
{
    [Serializable]
    public class ModCompatibility                   // Needs to be public for XML serialization
    {
        public ulong SteamID1 { get; private set; }

        public ulong SteamID2 { get; private set; }

        public List<Enums.CompatibilityStatus> Statuses { get; private set; } = new List<Enums.CompatibilityStatus>();
                                                                        // One or more

        public string NoteMod1 { get; private set; }                    // Note to display when reporting compatibility with Steam ID 1

        public string NoteMod2 { get; private set; }                    // Note to display when reporting compatibility with Steam ID 2


        // Default constructor
        public ModCompatibility()
        {
            // Nothing to do here
        }


        // Constructor with 3 to all parameters
        public ModCompatibility(ulong steamID1,
                                ulong steamID2,
                                List<Enums.CompatibilityStatus> statuses,
                                string note1 = "",
                                string note2 = "")
        {
            if (steamID1 == steamID2)
            {
                Logger.Log($"Found ModCompatibility object with two identical Steam IDs: { SteamID1 }.", Logger.warning);

                // Overwrite status to avoid weird reporting of a mod being incompatible with itself
                statuses = new List<Enums.CompatibilityStatus> { Enums.CompatibilityStatus.Unknown };
            }

            SteamID1 = steamID1;

            SteamID2 = steamID2;

            // If no status is indicated, add an Unknown so we have at least one
            if (statuses?.Any() != true)
            {
                Logger.Log($"Found ModCompatibility object with no status, Steam IDs: { steamID1 } and { steamID2 }.", Logger.warning);

                Statuses = new List<Enums.CompatibilityStatus> { Enums.CompatibilityStatus.Unknown };
            }
            else
            {
                Statuses = statuses;
            }

            NoteMod1 = note1;

            NoteMod2 = note2;
        }
    }
}
