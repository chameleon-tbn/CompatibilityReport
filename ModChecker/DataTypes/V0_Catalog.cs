using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using ModChecker.Util;


// This is a template for older structure versions. This class is not actually used in the mod because it is not needed for V0.
// This is needed when the catalog structure changed enough to disrupt the XML (de)serializer. Only the fields exist that existed in the old structure.


namespace ModChecker.DataTypes
{
    // Needs to be public for XML serialization
    [XmlRoot(ModSettings.xmlRoot)] public class V0_Catalog
    {
        // StructureVersion didn't exist in V0, but is used for logging
        [XmlIgnore] private static uint StructureVersion { get; set; } = 0;


        // Catalog version and date
        public uint Version { get; private set; }

        public DateTime UpdateDate { get; private set; }

        // The actual data in four lists
        public List<Mod> Mods { get; private set; } = new List<Mod>();

        public List<Compatibility> ModCompatibilities { get; private set; } = new List<Compatibility>();

        public List<ModGroup> ModGroups { get; private set; } = new List<ModGroup>();

        public List<Author> ModAuthors { get; private set; } = new List<Author>();


        // Load an old catalog from disk and convert it to a new catalog
        internal static Catalog LoadAndConvert(string fullPath)
        {
            V0_Catalog v0_catalog = new V0_Catalog();

            // Load the old catalog from disk
            if (File.Exists(fullPath))
            {
                try
                {
                    // Load and deserialize catalog from disk
                    XmlSerializer serializer = new XmlSerializer(typeof(V0_Catalog));

                    using (TextReader reader = new StreamReader(fullPath))
                    {
                        v0_catalog = (V0_Catalog)serializer.Deserialize(reader);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Can't load V{ StructureVersion } catalog \"{ Tools.PrivacyPath(fullPath) }\".");

                    Logger.Exception(ex, debugOnly: true);

                    return null;
                }
            }
            else
            {
                Logger.Log($"Can't load nonexistent V{ StructureVersion } catalog \"{ Tools.PrivacyPath(fullPath) }\".");

                return null;
            }

            // Convert and/or create all properties

            // compatible game version didn't exist in V0; assume the compatible game version for this mod
            Version v0_CompatibleGameVersion = ModSettings.compatibleGameVersion;

            // ... Whatever else is needed should be done here, for instance a to-be-created V0_Mods.Convert() if mods change too much ...

            // Create and return the new catalog
            return new Catalog(v0_catalog.Version, v0_catalog.UpdateDate, v0_CompatibleGameVersion, note: "", reportIntroText: ModSettings.defaultIntroText, 
                reportFooterText: ModSettings.defaultFooterText, v0_catalog.Mods, v0_catalog.ModCompatibilities, v0_catalog.ModGroups, v0_catalog.ModAuthors, 
                updateExclusions: null);
        }        
    }
}
