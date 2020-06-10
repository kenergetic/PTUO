using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PirateTUO2.Models
{
    /// <summary>
    /// Ogre Deck object - used in Ogrelicious for card pulling
    /// </summary>
    public class OgreliciousDeck
    {
        public DateTime Date { get; set; }
        public string Player { get; set; }
        public string Guild { get; set; }
        public string DeckList { get; set; }
        public string[] DeckParts { get; set; }
        public int DeckSize { get; set; }

        public OgreliciousDeck(string player, string deck, string guild, DateTime date)
        {
            Player = player;
            DeckList = deck.Replace(",,", ",").Replace(", ,", ",").Trim();
            Guild = guild;
            Date = date;
            DeckParts = DeckList.Split(',');
            DeckSize = DeckParts.Length - 2;


            // Sort cards, except commander/dominion
            if (DeckSize > 2)            
                Array.Sort(DeckParts, 2, DeckSize);
        }
    }
}
