using System;
using System.Collections.Generic;
using System.Linq;
using CompatibilityReport.Util;


namespace CompatibilityReport.DataTypes
{
    // Needs to be public for XML serialization
    [Serializable] public class Compatibility
    {
        // Steam IDs of two mods
        public ulong SteamID1 { get; private set; }

        public ulong SteamID2 { get; private set; }

        // Compatibility status of these two mods, from the perspective of ID1 ('this mod')
        public Enums.CompatibilityStatus Status { get; private set; }

        // Note about this compatibility
        public string Note { get; private set; }

        // Default constructor
        public Compatibility()
        {
            // Nothing to do here
        }


        // Constructor with all parameters
        internal Compatibility(ulong steamID1, ulong steamID2, Enums.CompatibilityStatus status, string note)
        {
            if (steamID1 == steamID2)
            {
                Logger.Log($"Found compatibility with two identical Steam IDs: { SteamID1 }.", Logger.error);

                // Use fake values to avoid weird reporting of a mod being incompatible with itself
                steamID1 = steamID2 = 1;

                status = default;
            }

            SteamID1 = steamID1;

            SteamID2 = steamID2;

            Status = status;

            Note = note ?? "";
        }
    }
}
