using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using ModChecker.Util;


// This is a template for older structure versions. This class is not actually used in the mod because it is not needed for V0.
// This is needed when the catalog structure changed enough to disrupt the XML (de)serializer. Only the fields exist that existed in the old structure.


namespace ModChecker.DataTypes
{
    [XmlRoot(ModSettings.xmlRoot)]
    public class V0_Catalog                                                  // Needs to be public for XML serialization
    {
        // StructureVersion is also used for logging
        private static uint StructureVersion { get; set; } = 0;

        // Catalog version and date
        public uint Version { get; private set; }

        public DateTime UpdateDate { get; private set; }

        // The actual data in four lists
        public List<Mod> Mods { get; private set; } = new List<Mod>();

        public List<ModCompatibility> ModCompatibilities { get; private set; } = new List<ModCompatibility>();

        public List<ModGroup> ModGroups { get; private set; } = new List<ModGroup>();

        public List<ModAuthor> ModAuthors { get; private set; } = new List<ModAuthor>();


        // Load an old catalog from disk and convert it to a new catalog
        internal static Catalog LoadAndConvert(string fullPath)
        {
            V0_Catalog V0_catalog = new V0_Catalog();

            // Load the old catalog from disk
            if (File.Exists(fullPath))
            {
                try
                {
                    // Load and deserialize catalog from disk
                    XmlSerializer serializer = new XmlSerializer(typeof(V0_Catalog));

                    using (TextReader reader = new StreamReader(fullPath))
                    {
                        V0_catalog = (V0_Catalog)serializer.Deserialize(reader);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Can't load catalog (V{ StructureVersion }) \"{ Tools.PrivacyPath(fullPath) }\".");

                    Logger.Exception(ex, debugOnly: true);

                    return null;
                }
            }
            else
            {
                Logger.Log($"Can't load nonexistent catalog (V{ StructureVersion }) \"{ Tools.PrivacyPath(fullPath) }\".");

                return null;
            }

            // Conversion

            // compatible game version didn't exist in V0; assume the mods compatible game version
            Version compatibleGameVersionV0 = ModSettings.CompatibleGameVersion;

            // ... Whatever more work is needed should be done here, for instance a Mods.Convert() if Mods changed too much

            // Create and return the new catalog
            Catalog catalog = new Catalog(V0_catalog.Version, V0_catalog.UpdateDate, compatibleGameVersionV0, note: "", reportIntroText: ModSettings.DefaultIntroText, 
                reportFooterText: ModSettings.DefaultFooterText, V0_catalog.Mods, V0_catalog.ModCompatibilities, V0_catalog.ModGroups, V0_catalog.ModAuthors);

            return catalog;
        }        
    }
}
