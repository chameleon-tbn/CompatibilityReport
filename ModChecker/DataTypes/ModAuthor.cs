using System;


namespace ModChecker.DataTypes
{
    [Serializable]
    public class ModAuthor                          // Needs to be public for XML serialization
    {
        public string Tag { get; private set; }

        public string Name { get; private set; }

        public DateTime? LastSeen { get; private set; }

        public bool Retired { get; private set; } = false;


        // Default constructor
        public ModAuthor()
        {
            // Nothing to do here
        }


        // Constructor with one to all parameters
        public ModAuthor(string tag,
                         string name = "",
                         DateTime? lastSeen = null,
                         bool retired = false)
        {
            Tag = tag;

            Name = name;

            LastSeen = lastSeen;
            
            Retired = retired;
        }
    }
}
