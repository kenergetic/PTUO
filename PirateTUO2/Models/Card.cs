using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using PirateTUO2.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PirateTUO2.Models
{
    public class Card
    {
        public string Name { get; set; }
        public int CardId { get; set; }
        public int CardIdOrigin { get; set; } // Some cards with multiple levels' IDs aren't linear. Commanders and some old cards
        public string FormattedName { get; set; } // Formatted name is lower-case and removes all spacing. e.g. "Miasma Master" becomes "miasmamaster"

        // What level is this card - most cards have 6, mutants/raid have 1, commons have 3, rares 4
        public int Level { get; set; }
        public int MaxLevel { get; set; } // Could have a linked list to the next card instead for level traversal

        public Card CardOrigin { get; set; } // Min level of this card
        public Card CardFinal { get; set; } // Max level of this card

        // 1 = imp, 2 = raider, 3 = bt, 4 = xeno, 5 = rt, 6 = progen
        public int Faction { get; set; }

        // Assault, Structure, Commander, Dominion, EventStructure
        public string CardType { get; set; }

        public int Fusion_Level { get; set; }
        public int Attack { get; set; }
        public int Health { get; set; }
        public int Delay { get; set; }
        public int Rarity { get; set; }

        // Most cards have 3 skills, but some special cards have 5 (fake commanders with invisible wall/evade)
        public CardSkill s1 { get; set; }
        public CardSkill s2 { get; set; }
        public CardSkill s3 { get; set; }
        public CardSkill s4 { get; set; }
        public CardSkill s5 { get; set; }

        // cards_section_X
        public int Section { get; set; }

        // 2000 - Mixed 
        // 2500 - Base Fusions
        // 3000 - Box, Cache cards
        // 6000 - Mission cards
        // 9000 - Exclusive (Gamble) cards
        // 9500 - Summons
        // 9999 - Summons Old
        public int Set { get; set; }
        // Attempt to parse through set 2000
        public string Subset { get; set; }

        // Comment section this card is in
        public string Comment { get; set; }

        // Power (relative stat)
        public int Power { get; set; }
        public int FactionPower { get; set; }

        public int SectionPower { get; set; }
        public int SetPower { get; set; }
        public int HealthPower { get; set; }
        public int FactionSkillPower { get; set; }
        public int PowerModifier { get; set; } // Manual overrides of the power formula, in appsettings.txt
        public int FactionPowerModifier { get; set; }
        public int DelayPower { get; set; }


        // Fusions: What this fuses into, what this fuses from
        public Dictionary<Card, int> FusesFrom { get; set; }
        public List<Card> FusesInto { get; set; }

        public Card()
        {
            Name = "Unknown";
            CardType = "Assault";
            Comment = "";
            Subset = "";
            CardId = -1;
            CardIdOrigin = -1;
            Faction = 0;
            Fusion_Level = 0;
            Attack = -1;
            Health = -1;
            Rarity = -1;
            Delay = -1;
            Section = -1;
            Set = -1;
            FactionPower = -1;
            Power = -1;
            PowerModifier = 0;

            FusesFrom = new Dictionary<Card, int>();
            FusesInto = new List<Card>();
        }


        /// <summary>
        /// Does this card have the targeted skill
        /// </summary>
        public bool HasSkill(string skillName)
        {
            if (s1.id == skillName ||
                s2.id == skillName ||
                s3.id == skillName ||
                s4.id == skillName ||
                s5.id == skillName) return true;
            return false;
        }
    }

    [Serializable]
    public class CardSkill
    {
        public string id { get; set; }
        
        // strength  
        public int x { get; set; }

        // faction
        public int y { get; set; }
        public bool all { get; set; }

        // every c
        public int c { get; set; }

        // n units
        public int n { get; set; }
        
        // evolve s1 to s2
        public string skill1 { get; set; }
        public string skill2 { get; set; }
        
        // on play/enter
        public string trigger { get; set; }
        // summon
        public string summon { get; set; }

        // metadata: power
        public int skillPower { get; set; }
        public int factionSkillPower { get; set; }
    
        public CardSkill()
        {
        }

        // Returns a copy of a skill
        public CardSkill Copy()
        {
            return new CardSkill
            {
                id = this.id,
                x = this.x,
                y = this.y,
                all = this.all,
                c = this.c,
                n = this.n,
                skill1 = this.skill1,
                skill2 = this.skill2,
                trigger = this.trigger,
                summon = this.summon,
                skillPower = this.skillPower,
                factionSkillPower = this.factionSkillPower
            };
        }
    }
}
