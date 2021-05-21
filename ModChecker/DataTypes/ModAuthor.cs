using System;


namespace ModChecker.DataTypes
{
    [Serializable]
    public class ModAuthor                          // Needs to be public for XML serialization
    {
        // Author ID, which can be either a profile ID (string) or a steam user number
        public string ID { get; private set; }
        public bool IDIsProfile { get; private set; }

        public string Name { get; private set; }

        public DateTime? LastSeen { get; private set; }

        public bool Retired { get; private set; } = false;


        // Default constructor
        public ModAuthor()
        {
            // Nothing to do here
        }


        // Constructor with one to all parameters
        public ModAuthor(string id,
                         bool idIsProfile,
                         string name = "",
                         DateTime? lastSeen = null,
                         bool retired = false)
        {
            ID = id;

            IDIsProfile = idIsProfile;

            if (string.IsNullOrEmpty(name))
            {
                Name = id;
            }
            else
            {
                Name = name;
            }            

            LastSeen = lastSeen;
            
            Retired = retired;
        }
    }
}
