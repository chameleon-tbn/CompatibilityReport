using System;
using System.Xml.Serialization;

namespace CompatibilityReport.CatalogData
{
    [Serializable]
    public class Compatibility
    {
        // The mod names are only for catalog readability, they are not used anywhere else.
        public ulong FirstModID { get; private set; }
        public string FirstModName { get; private set; }

        public ulong SecondModID { get; private set; }
        public string SecondModName { get; private set; }

        // The compatibility status is from the perspective of the first mod.
        public Enums.CompatibilityStatus Status { get; private set; }
        [XmlElement("Note")]
        public ElementWithId Note { get; private set; }

        /// <summary>Default constructor for deserialization.</summary>
        private Compatibility()
        {
            // Nothing to do here.
        }


        /// <summary>Constructor for compatibility creation.</summary>
        public Compatibility(ulong firstModID, string firstModName, ulong secondModID, string secondModName, Enums.CompatibilityStatus status, ElementWithId note)
        {
            FirstModID = firstModID;
            FirstModName = firstModName ?? "";

            SecondModID = secondModID;
            SecondModName = secondModName ?? "";

            Status = status;
            Note = note ?? new ElementWithId();
        }


        /// <summary>Updates mod names.</summary>
        /// <remarks>The mod names are only for catalog readability, they are not used anywhere else.</remarks>
        public void UpdateModNames(string firstModName, string secondModName)
        {
            FirstModName = firstModName ?? "";
            SecondModName = secondModName ?? "";
        }
    }
}
