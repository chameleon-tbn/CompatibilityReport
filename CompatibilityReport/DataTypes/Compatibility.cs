using System;
using CompatibilityReport.Util;


namespace CompatibilityReport.DataTypes
{
    // Needs to be public for XML serialization
    [Serializable] public class Compatibility
    {
        public ulong FirstModID { get; private set; }

        // The mod names are only for catalog readability, they are not used anywhere else
        public string FirstModName { get; private set; }

        public ulong SecondModID { get; private set; }

        public string SecondModName { get; private set; }

        // Compatibility status of these two mods, from the perspective of the first mod
        public Enums.CompatibilityStatus Status { get; private set; }

        public string Note { get; private set; }


        // Default constructor
        public Compatibility()
        {
            // Nothing to do here
        }


        // Constructor with all parameters
        internal Compatibility(ulong firstModID, string firstModName, ulong secondModID, string secondModName, Enums.CompatibilityStatus status, string note)
        {
            if (firstModID == secondModID)
            {
                Logger.Log($"Found compatibility with two identical Steam IDs: { FirstModID }.", Logger.error);

                // Use fake values to avoid weird reporting of a mod being incompatible with itself
                firstModID = secondModID = 1;

                status = default;
            }

            FirstModID = firstModID;

            FirstModName = firstModName;

            SecondModID = secondModID;

            SecondModName = secondModName;

            Status = status;

            Note = note ?? "";
        }
    }
}
