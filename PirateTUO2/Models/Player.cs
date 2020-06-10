using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PirateTUO2.Models
{
    public class Player
    {
        // Player info
        public string TyrantName { get; set; }  // Tyrant Unleashed Name (logging in)
        public string KongName { get; set; }    // Kongregate name (setting decks)        
        public string LineName { get; set; }    // Optional: Line identity
        public string KongStats { get; set; }   // Kong stats        
        public string Guild { get; set; }       // Guild
        //public string Canvas { get; set; }      // Canvas link
        //public string Email { get; set; }       // Email
        //public string Sms { get; set; }         // SMS

        // Settings
        //public bool SettingOnGrind { get; set; }         // Does this user grind
        //public bool SettingIsGrinding { get; set; }        // User here or away
        //public bool SettingSetDeckWhenAway { get; set; }

        // Player cards
        public List<Card> DominionCards { get; set; }
        public ConcurrentDictionary<Card, int> Cards { get; set; }
        public ConcurrentDictionary<Card, int> PossibleCards { get; set; }
        public ConcurrentDictionary<Card, int> WeakCards { get; set; }
        public ConcurrentDictionary<string, int> UnknownCards { get; set; }

        // If we include what-if cards to a player sim, this would store those
        public ConcurrentDictionary<Card, int> NewCards { get; set; }

        public string LastUpdated { get; set; }

        // Player seeds
        public List<string> ExternalSeeds { get; set; }
        public List<string> Seeds { get; set; }

        public Player()
        {
            Cards = new ConcurrentDictionary<Card, int>();
            PossibleCards = new ConcurrentDictionary<Card, int>();
            WeakCards = new ConcurrentDictionary<Card, int>();
            UnknownCards = new ConcurrentDictionary<string, int>();
            NewCards = new ConcurrentDictionary<Card, int>();

            ExternalSeeds = new List<string>();
            Seeds = new List<string>();
        }
    }
}
