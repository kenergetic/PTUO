using PirateTUO2.Classes;
using PirateTUO2.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PirateTUO2.Modules
{

    /// <summary>
    /// Determins a relative power of a card
    /// </summary>
    public static class CardPower
    {
        static double delayMultiplier;

        static StringBuilder result; //For deconstructing power
        static StringBuilder skillPowerString;


        /// <summary>
        /// 
        /// Some skills will scale base off a multiplier of the current XML section number
        static int SECTION = CONSTANTS.NEWEST_SECTION;

        
        /// ------------------------------------------
        /// Cards from newer sections gain a little bonus power
        /// * Unfortunately box buffs don't get dragged in
        /// ------------------------------------------
        static int SECTION_NEWEST = 0;
        static int SECTION_PREVIOUS = 0;
        static int SECTION_OLDER = 0;

        /// ---------------------
        /// Set power
        /// ---------------------
        static int SET_BOX_LEGEND = 0;
        static int SET_BOX_VINDI = 1;
        static int SET_BOX_MYTHIC = 3;
        static int SET_CHANCE = 2;
        static int SET_PVE_VINDI = 2;
        static int SET_OTHER_VIND = 1;
        static int SET_OTHER_MYTHIC = 2;


        /// ---------------------
        /// Health power
        /// ---------------------
        /// Use health to guess how above or below the HP curve a card is
        /// If its HP is over or under an average, modify its power
        /// 
        /// * Assault: Base HP is 100/130/160/190 for 1/2/3/4 delay
        /// * Structure: Ignore
        static int BASE_HEALTH = CONFIG.CardSkillPower?["BASE_HEALTH"] ?? 100;
        static int BASE_HEALTH_DELAY_MULTIPLIER = CONFIG.CardSkillPower?["BASE_HEALTH_DELAY_MULTIPLIER"] ?? 30;
        static int BASE_HEALTH_BONUS = CONFIG.CardSkillPower?["BASE_HEALTH_BONUS"] ?? 30;



        /// Delay bonus
        /// Raw power for having low delay
        static int DELAY_ZERO_BONUS = CONFIG.CardSkillPower?["DELAY_ZERO_BONUS"] ?? 2;
        static int DELAY_ONE_BONUS = CONFIG.CardSkillPower?["DELAY_ONE_BONUS"] ?? 1;
        static int DELAY_TWO_BONUS = CONFIG.CardSkillPower?["DELAY_TWO_BONUS"] ?? 0;
        static int DELAY_THREE_BONUS = CONFIG.CardSkillPower?["DELAY_THREE_BONUS"] ?? 0;
        static int DELAY_FOUR_BONUS = CONFIG.CardSkillPower?["DELAY_FOUR_BONUS"] ?? 0;

        /// ---------------------
        /// Skill Power
        /// ---------------------
        /// Cards get the majority of their power from their skills and the magnitude of those skills

        static double DELAY_MULTIPLIER_ZERO = 1.0;
        static double DELAY_MULTIPLIER_ONE = 1.0;
        static double DELAY_MULTIPLIER_TWO = 1.0;
        static double DELAY_MULTIPLIER_THREE = 1.25;
        static double DELAY_MULTIPLIER_FOUR = 1.5;

        //static double EVADE_MULTIPLIER = 1.2;

        /// Flurry increases power but also reduces skill multipliers
        // static double FLURRY_MULTIPLIER_SMALL = 0.0;
        // static double FLURRY_MULTIPLIER = 0.0;
        // static double FLURRY_MULTIPLIER_LARGE = 0.1;

        /// 
        /// Power math (X skills)
        /// [Base]      - This card gains [BasePower] power if it has X of a skill
        ///               Example: Refresh Base=15. If a card has 15+ Refresh, it gains power
        /// 
        /// [BasePower] - By default, it's 1. The power this card gains if it has [Base] of a skill
        ///               Example: Counter BasePower=2
        ///               
        /// [Extra]     - This card gains additional power for every [Extra] points over base
        ///               Example: Heal All Base=8, Extra=3
        ///                        A 1 delay would with Heal All 12 would have 4 "extra" Heal (12 - 8) extra Heal All, and gain 1 [Extra] power
        ///                        
        /// [ExtraMaximum] - Maximum [Extra] power gained from a skill. Default is 10
        ///                Example: Evade has ExtraMaximum=1. A card with 99 Evade would gain 1 [Extra] power
        ///                
        /// [Faction]   - If this skill is faction (Heal All Raider), then this card gains FactionPower instead of NormalPower
        /// 
        /// [Weightless] - Don't apply the weight from Flurry or Delay to [Base] or [Extra]
        /// 
        /// [SkillAll] - Distinguishes grading Strike from Strike All
        /// 
        /// ---------------------
        /// Defense
        /// ---------------------
        /// Evade
        static int EVADE_BASE = CONFIG.CardSkillPower?["EVADE_BASE"] ?? 5;
        static int EVADE_BASE_POWER = CONFIG.CardSkillPower?["EVADE_BASE_POWER"] ?? 2;
        static int EVADE_EXTRA = CONFIG.CardSkillPower?["EVADE_EXTRA"] ?? 2;
        static bool EVADE_WEIGHTLESS = true;
        /// Armor
        static int ARMOR_BASE = CONFIG.CardSkillPower?["ARMOR_BASE"] ?? 100;
        static int ARMOR_EXTRA = CONFIG.CardSkillPower?["ARMOR_EXTRA"] ?? 30;
        static bool ARMOR_WEIGHTLESS = true;
        /// Heal
        static int HEAL_BASE = CONFIG.CardSkillPower?["HEAL_BASE"] ?? 100;
        static int HEAL_EXTRA = CONFIG.CardSkillPower?["HEAL_EXTRA"] ?? 20;
        static int HEAL_SINGLE = CONFIG.CardSkillPower?["HEAL_SINGLE"] ?? 200;
        /// Protect	:
        static int PROTECT_BASE = CONFIG.CardSkillPower?["PROTECT_BASE"] ?? 20;
        static int PROTECT_EXTRA = CONFIG.CardSkillPower?["PROTECT_EXTRA"] ?? 10;
        static int PROTECT_SINGLE = CONFIG.CardSkillPower?["PROTECT_SINGLE"] ?? 100;
        /// Foritify: 
        static int FORTIFY_BASE = CONFIG.CardSkillPower?["FORTIFY_BASE"] ?? 60;
        static int FORTIFY_EXTRA = CONFIG.CardSkillPower?["FORTIFY_EXTRA"] ?? 20;
        /// Counter	: 
        static int COUNTER_BASE = CONFIG.CardSkillPower?["COUNTER_BASE"] ?? 100;
        static int COUNTER_EXTRA = CONFIG.CardSkillPower?["COUNTER_EXTRA"] ?? 10;
        static bool COUNTER_WEIGHTLESS = true;
        /// Entrap: 
        static int ENTRAP_BASE = CONFIG.CardSkillPower?["ENTRAP_BASE"] ?? 80;
        static int ENTRAP_EXTRA = CONFIG.CardSkillPower?["ENTRAP_EXTRA"] ?? 20;
        static int ENTRAP_SINGLE = CONFIG.CardSkillPower?["ENTRAP_SINGLE"] ?? 20;
        /// Weaken All: 
        static int WEAKEN_BASE = CONFIG.CardSkillPower?["WEAKEN_BASE"] ?? 50;
        static int WEAKEN_EXTRA = CONFIG.CardSkillPower?["WEAKEN_EXTRA"] ?? 20;
        /// Sunder	: 
        static int SUNDER_BASE = CONFIG.CardSkillPower?["SUNDER_BASE"] ?? 50;
        static int SUNDER_EXTRA = CONFIG.CardSkillPower?["SUNDER_EXTRA"] ?? 30;
        /// Refresh	: 
        static int REFRESH_BASE = CONFIG.CardSkillPower?["REFRESH_BASE"] ?? 100;
        static int REFRESH_EXTRA = CONFIG.CardSkillPower?["REFRESH_EXTRA"] ?? 30;
        static bool REFRESH_WEIGHTLESS = true;
        /// Absorb	: 
        static int ABSORB_BASE = CONFIG.CardSkillPower?["ABSORB_BASE"] ?? 100;
        static int ABSORB_EXTRA = CONFIG.CardSkillPower?["ABSORB_EXTRA"] ?? 30;
        static bool ABSORB_WEIGHTLESS = true;
        /// Scavenge:
        static int SCAVENGE_BASE = CONFIG.CardSkillPower?["SCAVENGE_BASE"] ?? 30;
        static int SCAVENGE_EXTRA = CONFIG.CardSkillPower?["SCAVENGE_EXTRA"] ?? 10;
        static bool SCAVENGE_WEIGHTLESS = true;
        /// Avenge	:
        static int AVENGE_BASE = CONFIG.CardSkillPower?["AVENGE_BASE"] ?? 30;
        static int AVENGE_EXTRA = CONFIG.CardSkillPower?["AVENGE_EXTRA"] ?? 10;
        static bool AVENGE_WEIGHTLESS = true;
        /// Corrosive: 
        static int CORROSIVE_BASE = CONFIG.CardSkillPower?["CORROSIVE_BASE"] ?? 100;
        static int CORROSIVE_EXTRA = CONFIG.CardSkillPower?["CORROSIVE_EXTRA"] ?? 50;
        static bool CORROSIVE_WEIGHTLESS = true;
        /// Subdue: 
        static int SUBDUE_BASE = CONFIG.CardSkillPower?["SUBDUE_BASE"] ?? 100;
        static int SUBDUE_EXTRA = CONFIG.CardSkillPower?["SUBDUE_EXTRA"] ?? 50;
        static bool SUBDUE_WEIGHTLESS = true;
        /// Barrier	: 
        static int BARRIER_BASE = CONFIG.CardSkillPower?["BARRIER_BASE"] ?? 100;
        static int BARRIER_EXTRA = CONFIG.CardSkillPower?["BARRIER_EXTRA"] ?? 30;
        static bool BARRIER_WEIGHTLESS = true;
        /// Stasis	: 
        static int STASIS_BASE = CONFIG.CardSkillPower?["STASIS_BASE"] ?? 40;
        static int STASIS_EXTRA = CONFIG.CardSkillPower?["STASIS_EXTRA"] ?? 10;
        static bool STASIS_WEIGHTLESS = true;
        static int STASIS_FACTION_BONUS = 2;
        /// Revenge	: 
        static int REVENGE_BASE = CONFIG.CardSkillPower?["REVENGE_BASE"] ?? 5; // Value doesn't matter, but some old cards shouldn't get power
        static bool REVENGE_WEIGHTLESS = true;
        /// 
        /// -------
        /// Offense 
        /// -------
        /// Rally
        static int RALLY_BASE = CONFIG.CardSkillPower?["RALLY_BASE"] ?? 40;
        static int RALLY_EXTRA = CONFIG.CardSkillPower?["RALLY_EXTRA"] ?? 20;
        static int RALLY_SINGLE = CONFIG.CardSkillPower?["RALLY_SINGLE"] ?? 100;
        /// Strike
        static int STRIKE_BASE = CONFIG.CardSkillPower?["STRIKE_BASE"] ?? 50;
        static int STRIKE_EXTRA = CONFIG.CardSkillPower?["STRIKE_EXTRA"] ?? 20;
        static int STRIKE_SINGLE = CONFIG.CardSkillPower?["STRIKE_SINGLE"] ?? 200;
        /// Enfeeble
        static int ENFEEBLE_BASE = CONFIG.CardSkillPower?["ENFEEBLE_BASE"] ?? 30;
        static int ENFEEBLE_EXTRA = CONFIG.CardSkillPower?["ENFEEBLE_EXTRA"] ?? 10;
        static int ENFEEBLE_SINGLE = CONFIG.CardSkillPower?["ENFEEBLE_SINGLE"] ?? 70;
        /// Swipe   :
        static int SWIPE_BASE = CONFIG.CardSkillPower?["SWIPE_BASE"] ?? 100;
        static int SWIPE_EXTRA = CONFIG.CardSkillPower?["SWIPE_EXTRA"] ?? 20;
        /// Drain	: 
        static int DRAIN_BASE = CONFIG.CardSkillPower?["DRAIN_BASE"] ?? 50;
        static int DRAIN_EXTRA = CONFIG.CardSkillPower?["DRAIN_EXTRA"] ?? 20;
        /// Berserk	: 
        static int BERSERK_BASE = CONFIG.CardSkillPower?["BERSERK_BASE"] ?? 50;
        static int BERSERK_EXTRA = CONFIG.CardSkillPower?["BERSERK_EXTRA"] ?? 20;
        /// Rally
        static int ENRAGE_BASE = CONFIG.CardSkillPower?["ENRAGE_BASE"] ?? 30;
        static int ENRAGE_EXTRA = CONFIG.CardSkillPower?["ENRAGE_EXTRA"] ?? 20;
        static int ENRAGE_SINGLE = CONFIG.CardSkillPower?["ENRAGE_SINGLE"] ?? 100;
        /// Pierce	: 
        static int PIERCE_BASE = CONFIG.CardSkillPower?["PIERCE_BASE"] ?? 150;
        static int PIERCE_EXTRA = CONFIG.CardSkillPower?["PIERCE_EXTRA"] ?? 50;
        static bool PIERCE_WEIGHTLESS = true;
        /// Venom	: 
        static int VENOM_BASE = CONFIG.CardSkillPower?["VENOM_BASE"] ?? 80;
        static int VENOM_EXTRA = CONFIG.CardSkillPower?["VENOM_EXTRA"] ?? 20;
        /// Mark	: 
        static int MARK_BASE = CONFIG.CardSkillPower?["MARK_BASE"] ?? 80;
        static int MARK_EXTRA = CONFIG.CardSkillPower?["MARK_EXTRA"] ?? 20;
        /// Hunt:
        static int HUNT_BASE = CONFIG.CardSkillPower?["HUNT_BASE"] ?? 80;
        static int HUNT_EXTRA = CONFIG.CardSkillPower?["HUNT_EXTRA"] ?? 20;
        /// Bravery	
        static int BRAVERY_BASE = CONFIG.CardSkillPower?["BRAVERY_BASE"] ?? 40;
        static int BRAVERY_EXTRA = CONFIG.CardSkillPower?["BRAVERY_EXTRA"] ?? 20;
        /// Legion	    : 
        static int LEGION_BASE = CONFIG.CardSkillPower?["LEGION_BASE"] ?? 40;
        static int LEGION_EXTRA = CONFIG.CardSkillPower?["LEGION_EXTRA"] ?? 10;
        static bool LEGION_FACTION_ONLY = true;
        static int LEGION_NONFACTION_PENALTY = CONFIG.CardSkillPower?["LEGION_NONFACTION_PENALTY"] ?? 2;
        /// Allegiance  : 
        static int ALLEGIANCE_BASE = CONFIG.CardSkillPower?["ALLEGIANCE_BASE"] ?? 30;
        static int ALLEGIANCE_EXTRA = CONFIG.CardSkillPower?["ALLEGIANCE_EXTRA"] ?? 10;
        static bool ALLEGIANCE_FACTION_ONLY = true;
        static int ALLEGIANCE_NONFACTION_PENALTY = CONFIG.CardSkillPower?["ALLEGIANCE_NONFACTION_PENALTY"] ?? 2;
        static bool ALLEGIANCE_WEIGHTLESS = true;
        /// Coalition: 
        static int COALITION_BASE = CONFIG.CardSkillPower?["COALITION_BASE"] ?? 40;
        static int COALITION_EXTRA = CONFIG.CardSkillPower?["COALITION_EXTRA"] ?? 20;
        static int COALITION_FACTION_PENALTY = CONFIG.CardSkillPower?["COALITION_FACTION_PENALTY"] ?? 2;


        /// -------
        /// Utility
        /// -------
        /// OnAttacked: Negate the delay penalty of a skill (ex: On Attack: Rally all 15 on a 4 delay would have the same power as a 1 delay with Rally all 15)
        /// 
        /// Summon: Percentage of the summoned units power
        static double SUMMON_PERCENT_ONPLAY = 1;
        static double SUMMON_PERCENT_1D = 0.75;
        static double SUMMON_PERCENT_2D = 0.50;
        static double SUMMON_PERCENT_3D = 0.25;
        static double SUMMON_PERCENT_4D = 0.25;
        static double SUMMON_PERCENT_ON_DEATH = 0.33;
        static double SUMMON_PERCENT_ON_ATTACKED = 2;
        /// 
        /// Mortar : 
        static int MORTAR_BASE = CONFIG.CardSkillPower?["MORTAR_BASE"] ?? 60;
        static int MORTAR_EXTRA = CONFIG.CardSkillPower?["MORTAR_EXTRA"] ?? 20;
        static int MORTAR_SINGLE_BASE = CONFIG.CardSkillPower?["MORTAR_SINGLE_BASE"] ?? 120;
        /// Mimic: 
        static int MIMIC_BASE = CONFIG.CardSkillPower?["MIMIC_BASE"] ?? 60;
        static int MIMIC_EXTRA = CONFIG.CardSkillPower?["MIMIC_EXTRA"] ?? 20;
        /// Tribute: 
        static int TRIBUTE_BASE = CONFIG.CardSkillPower?["TRIBUTE_BASE"] ?? 5;
        /// Inhibit: 
        static int INHIBIT_BASE = CONFIG.CardSkillPower?["INHIBIT_BASE"] ?? 5;
        /// Sabotage: 
        static int SABOTAGE_BASE = CONFIG.CardSkillPower?["SABOTAGE_BASE"] ?? 100;
        /// Disease
        static int DISEASE_BASE = CONFIG.CardSkillPower?["DISEASE_BASE"] ?? 150;
        static int DISEASE_EXTRA = CONFIG.CardSkillPower?["DISEASE_EXTRA"] ?? 150;

        /// Evolve Rupture: BasePower: 3
        /// Evolve Sunder: BasePower: 1
        /// Evolve other: 0
        /// Wall: 1
        /// 
        /// Overload:
        /// Delay=0-2 - OL2: 2, OL3: 3, OL4+: 4
        /// Delay=3-4 - OL2: 0, OL3: 1, OL4+: 2
        /// 
        /// Jam:
        /// OnPlay: Jam 1/2/3/4 = 1/2/3/5
        /// Delay=0: Jam 1/2/3/4+ = 2/4/7/10
        /// Delay=1: Jam 1/2/3/4+ = 1/2/6/10
        /// Delay=2: Jam 1/2/3/4+ = 0/1/2/5
        /// Delay=3: Jam 1/2/3/4+ = 0/1/2/5
        /// Jam every 2/1: 2/3
        /// Penalty: -2 for an assault with 1+ delay and no evade (mimic liability) 
        /// 
        /// Flurry:
        /// Flurry x (1/2/3/4): 1/4/5/10
        /// Flurry every c (1/2/X): 3/2/1
        /// FlurryPower reduced by delay: Delay 2/3/4+ = -1/-2/half
        /// Structure flurry: -1
        /// 
        /// -------
        /// Unused
        /// -------
        /// Rush: -0-
        /// Enhance: -0-
        /// 
        /// ---------------------
        /// Fusion
        /// ---------------------
        /// * Dual: 1/2 power. Mostly mythics - some explicit dual vinds from CONSTANTS.whitelist
        /// 
        /// 
        /// Mend	: --Killed off--
        /// Payback	: --Killed off--
        /// Leech	: --Killed off--
        /// Poison	: --Killed off--
        /// Rupture	: --Killed off--
        /// Valor	: --Killed off--
        /// Siege   : --Killed off on player cards--
        /// </summary>
        public static string CalculatePower(Card card)
        {

            // Reset static variables
            delayMultiplier = 0;
            skillPowerString = new StringBuilder(); //saves the power on skills to be put in result later
            result = new StringBuilder(); // string to return that summarizes power stuff            
            card.Power = 0; //reset card's power in case its a summon (it may be calculated twice)
            card.FactionPower = 0;


            if (card.Set != (int)CardSet.Summon)
            {
                // Section
                if (card.Section >= CONSTANTS.NEWEST_SECTION) card.SectionPower = SECTION_NEWEST;
                else if (card.Section == CONSTANTS.NEWEST_SECTION - 1) card.SectionPower = SECTION_PREVIOUS;
                else card.SectionPower = SECTION_OLDER;

                // Rarity
                if (card.Subset == CardSubset.Box.ToString() || card.Subset == CardSubset.Cache.ToString())
                {
                    if (card.Rarity == 4) card.SetPower += SET_BOX_LEGEND;
                    else if (card.Rarity == 5) card.SetPower += SET_BOX_VINDI;
                    else if (card.Rarity == 6) card.SetPower += SET_BOX_MYTHIC;
                }
                else if (card.Subset == CardSubset.Chance.ToString())
                {
                    card.SetPower += SET_CHANCE;
                }
                else if (card.Rarity == (int)Rarity.Vindicator &&
                         card.Subset == CardSubset.PvE_Reward.ToString())
                {
                    card.SetPower += SET_PVE_VINDI;
                }
                else
                {
                    if (card.Rarity == 5) card.SetPower += SET_OTHER_VIND;
                    else if (card.Rarity == 6) card.SetPower += SET_OTHER_MYTHIC;
                }
            }

            // Delay multiplier
            if (card.Delay == 0) delayMultiplier = DELAY_MULTIPLIER_ZERO;
            else if (card.Delay == 1) delayMultiplier = DELAY_MULTIPLIER_ONE;
            else if (card.Delay == 2) delayMultiplier = DELAY_MULTIPLIER_TWO;
            else if (card.Delay == 3) delayMultiplier = DELAY_MULTIPLIER_THREE;
            else delayMultiplier = DELAY_MULTIPLIER_FOUR;

            // Does this have flurry? Flurry reduces skill power thresholds
            //if (card.s3.id == "Flurry")
            //{
            //    // Flurry 2 +
            //    if (card.s3.x >= 2) 
            //        delayMultiplier -= FLURRY_MULTIPLIER_LARGE;
            //    // Flurry 1 times - reduces skill power thresholds
            //    else
            //        delayMultiplier -= FLURRY_MULTIPLIER_LARGE;

            //    // Flurry every 1
            //    if (card.s3.c == 1)
            //        delayMultiplier -= FLURRY_MULTIPLIER_LARGE;
            //    // Flurry every 2
            //    else if (card.s3.c == 2)
            //        delayMultiplier -= FLURRY_MULTIPLIER;
            //    // Flurry every 3+
            //    else
            //        delayMultiplier -= FLURRY_MULTIPLIER_SMALL;
            //}

            // ---------------------------------------------------------------
            // Add or remove power from specific cards (see appsettings.txt)
            // ---------------------------------------------------------------
            if (CONSTANTS.CARD_POWER_ADJUSTMENTS.ContainsKey(card.Name))
            {
                card.PowerModifier = CONSTANTS.CARD_POWER_ADJUSTMENTS[card.Name];
            }
            if (CONSTANTS.CARD_FACTIONPOWER_ADJUSTMENTS.ContainsKey(card.Name))
            {
                card.FactionPowerModifier = CONSTANTS.CARD_FACTIONPOWER_ADJUSTMENTS[card.Name];
            }

            // ---------------------
            // DelayPower
            // ---------------------
            switch (card.Delay)
            {
                case 0:
                    card.DelayPower = DELAY_ZERO_BONUS;
                    break;
                case 1:
                    card.DelayPower = DELAY_ONE_BONUS;
                    break;
                case 2:
                    card.DelayPower = DELAY_TWO_BONUS;
                    break;
                case 3:
                    card.DelayPower = DELAY_THREE_BONUS;
                    break;
                case 4:
                    card.DelayPower = DELAY_FOUR_BONUS;
                    break;
            }

            // ---------------------
            // HealthPower
            // ---------------------
            int delay = Math.Max(card.Delay, 0);
            int healthThreshold = BASE_HEALTH + (delay * BASE_HEALTH_DELAY_MULTIPLIER);

            

            if (card.CardType == CardType.Assault.ToString())
            {
                card.HealthPower = (int)(Math.Ceiling((double)card.Health - healthThreshold) / BASE_HEALTH_BONUS);
            }
            else
            {
                //if (card.s1.id == "Wall" || card.s2.id == "Wall" || card.s3.id == "Wall")
                //    card.HealthPower = 0;
                //else
                //    card.HealthPower = (int)(Math.Ceiling((double)card.Health - healthThreshold) / BASE_HEALTH_POWER_BONUS);
            }
            


            // ---------------------
            // SkillPower
            // ---------------------
            var totalSkillPower = 0;
            var totalSkillFactionPower = 0;

            for (int i = 0; i < 3; i++)
            {
                var skill = card.s1;
                if (i == 1) skill = card.s2;
                if (i == 2) skill = card.s3;

                if (skill == null)
                {
                    skillPowerString.Append("0\t");
                    continue;
                }

                CalculateSkillPower(card, skill);
                totalSkillPower += skill.skillPower;
                totalSkillFactionPower += skill.factionSkillPower;

            }//skills

            // ---------------------
            // Evade Power
            // ---------------------
            //if (card.s1.id == "Evade" || card.s2.id == "Evade" || card.s3.id == "Evade")
            //{
            //    card.Power = (int)(card.Power * EVADE_MULTIPLIER);
            //}


            // Calculate power and set subpower stats
            var power = card.SectionPower + card.SetPower + card.HealthPower + totalSkillPower + card.PowerModifier;
            var factionPower = power + totalSkillFactionPower + card.FactionPowerModifier;

            if (power < 1) power = 1;
            if (factionPower < 1) factionPower = 1;

            // Set power 
            if (card.Fusion_Level == 2 || 
                (card.Fusion_Level == 1 && card.Rarity >= 6))
            {
                card.Power = power;
                card.FactionPower = factionPower;
            }
            // 50% power for non-mythic duals
            else if (card.Fusion_Level == 1)
            {
                card.Power = power / 2;
                card.FactionPower = factionPower / 2;
            }
            else
            {
                card.Power = -1;
                card.FactionPower = -1;
            }


            // Create a string for the card's power and stats
            WriteCardStats(card);

            return result.ToString();
        }


        /// <summary>
        /// Calculates the power of a card skill. Based on its magnitude and the card delay
        /// </summary>
        private static void CalculateSkillPower(Card card, CardSkill skill)
        {
            skill.skillPower = 0;
            skill.factionSkillPower = 0;

            switch (skill.id)
            {
                // --------------
                // Defense
                // --------------
                case "Evade":
                    GetSkillPowerX(card, skill, EVADE_BASE, EVADE_EXTRA, xBasePower:EVADE_BASE_POWER, weightLess: EVADE_WEIGHTLESS, xBaseExtraPowerCap:1);
                    break;
                case "Armored":
                    GetSkillPowerX(card, skill, ARMOR_BASE, ARMOR_EXTRA, weightLess: ARMOR_WEIGHTLESS);
                    break;
                case "Heal":
                    if (skill.all)
                        GetSkillPowerX(card, skill, HEAL_BASE, HEAL_EXTRA, skillAll: true);
                    else
                        GetSkillPowerX(card, skill, HEAL_SINGLE, HEAL_EXTRA);
                    break;
                case "Protect":
                    if (skill.all)
                        GetSkillPowerX(card, skill, PROTECT_BASE, PROTECT_EXTRA, skillAll: true);
                    else
                        GetSkillPowerX(card, skill, PROTECT_SINGLE, PROTECT_EXTRA * 2);
                    break;
                case "Fortify":
                    GetSkillPowerX(card, skill, FORTIFY_BASE, FORTIFY_EXTRA);
                    break;
                case "Counter":
                    GetSkillPowerX(card, skill, COUNTER_BASE, COUNTER_EXTRA, weightLess: COUNTER_WEIGHTLESS); 
                    break;
                case "Entrap":
                    if (skill.all)
                        GetSkillPowerX(card, skill, ENTRAP_BASE, ENTRAP_EXTRA, skillAll: true);
                    else
                        GetSkillPowerX(card, skill, ENTRAP_SINGLE, ENTRAP_EXTRA);
                    break;
                case "Weaken":
                    GetSkillPowerX(card, skill, WEAKEN_BASE, WEAKEN_EXTRA, skillAll: true);
                    break;
                case "Sunder":
                    if (skill.all) 
                        GetSkillPowerX(card, skill, 1, xBasePower: 5, skillAll: true);
                    else
                        GetSkillPowerX(card, skill, SUNDER_BASE, SUNDER_EXTRA);
                    break;
                case "Refresh":
                    GetSkillPowerX(card, skill, REFRESH_BASE, REFRESH_EXTRA, weightLess: REFRESH_WEIGHTLESS);
                    break;
                case "Absorb":
                    GetSkillPowerX(card, skill, ABSORB_BASE, ABSORB_EXTRA, weightLess:ABSORB_WEIGHTLESS);
                    break;
                case "Scavenge":
                    GetSkillPowerX(card, skill, SCAVENGE_BASE, SCAVENGE_EXTRA, weightLess: SCAVENGE_WEIGHTLESS);
                    break;
                case "Corrosive":
                    GetSkillPowerX(card, skill, CORROSIVE_BASE, CORROSIVE_EXTRA, weightLess: CORROSIVE_WEIGHTLESS);
                    break;
                case "Subdue":
                    GetSkillPowerX(card, skill, SUBDUE_BASE, SUBDUE_EXTRA, weightLess: SUBDUE_WEIGHTLESS);
                    break;
                case "Stasis":
                    GetSkillPowerX(card, skill, STASIS_BASE, STASIS_EXTRA, weightLess: STASIS_WEIGHTLESS);
                    // Bonus for faction
                    skill.factionSkillPower += STASIS_FACTION_BONUS; 
                    break;
                case "Revenge":
                    // Bonus for revenge on 0-1d
                    if (card.Delay >= 2) GetSkillPowerX(card, skill, REVENGE_BASE, weightLess: REVENGE_WEIGHTLESS);
                    break;
                case "Avenge":
                    GetSkillPowerX(card, skill, AVENGE_BASE, AVENGE_EXTRA, weightLess: AVENGE_WEIGHTLESS);
                    break;
                case "Barrier":
                    GetSkillPowerX(card, skill, BARRIER_BASE, BARRIER_EXTRA, weightLess: BARRIER_WEIGHTLESS);
                    break;
                //case "Mend":
                //    GetSkillPowerX(card, skill, 30, 3);
                //    break;
                //case "Leech":
                //    GetSkillPowerX(card, skill, 25, 5);
                //    break;
                //case "Payback":
                //    GetSkillPowerX(card, skill, 6, xBasePower: 1);
                //    break;

                // --------------
                // Offense
                // --------------
                case "Rally":
                    if (skill.all)
                        GetSkillPowerX(card, skill, RALLY_BASE, RALLY_EXTRA, skillAll: true);
                    else
                        GetSkillPowerX(card, skill, RALLY_SINGLE, RALLY_EXTRA * 2);
                    break;
                case "Enrage":
                    if (skill.all)
                        GetSkillPowerX(card, skill, ENRAGE_BASE, ENRAGE_EXTRA, skillAll: true);
                    else
                        GetSkillPowerX(card, skill, ENRAGE_SINGLE);
                    break;
                case "Strike":
                    if (skill.all)
                        GetSkillPowerX(card, skill, STRIKE_BASE, STRIKE_EXTRA, skillAll: true);
                    else
                        GetSkillPowerX(card, skill, STRIKE_SINGLE, STRIKE_EXTRA * 2);
                    break;
                case "Enfeeble":
                    if (skill.all)
                        GetSkillPowerX(card, skill, ENFEEBLE_BASE, ENFEEBLE_EXTRA, skillAll: true);
                    else
                        GetSkillPowerX(card, skill, ENFEEBLE_SINGLE, ENFEEBLE_EXTRA * 2);
                    break;
                case "Swipe":
                    GetSkillPowerX(card, skill, SWIPE_BASE, SWIPE_EXTRA);
                    break;
                case "Drain":
                    GetSkillPowerX(card, skill, DRAIN_BASE, DRAIN_EXTRA);
                    break;
                case "Berserk":
                    GetSkillPowerX(card, skill, BERSERK_BASE, BERSERK_EXTRA);
                    break;
                case "Pierce":
                    GetSkillPowerX(card, skill, PIERCE_BASE, PIERCE_EXTRA, weightLess: PIERCE_WEIGHTLESS);
                    break;
                case "Venom":
                    GetSkillPowerX(card, skill, VENOM_BASE, VENOM_EXTRA);
                    break;
                case "Mark":
                    GetSkillPowerX(card, skill, MARK_BASE, MARK_EXTRA);
                    break;
                case "Hunt":
                    GetSkillPowerX(card, skill, HUNT_BASE, HUNT_EXTRA);
                    break;
                case "Bravery":
                    GetSkillPowerX(card, skill, BRAVERY_BASE, BRAVERY_EXTRA);
                    break;
                case "Legion":
                    GetSkillPowerX(card, skill, LEGION_BASE, LEGION_EXTRA, factionOnly: LEGION_FACTION_ONLY);
                    skill.skillPower -= LEGION_NONFACTION_PENALTY; // Extra penalty for nonfaction, to keep it out of their sims
                    break;
                case "Allegiance":
                    GetSkillPowerX(card, skill, ALLEGIANCE_BASE, ALLEGIANCE_EXTRA, factionOnly: ALLEGIANCE_FACTION_ONLY, weightLess:ALLEGIANCE_WEIGHTLESS);
                    skill.skillPower -= ALLEGIANCE_NONFACTION_PENALTY; // Extra penalty for nonfaction, to keep it out of their sims
                    break;
                case "Coalition":
                    GetSkillPowerX(card, skill, COALITION_BASE, COALITION_EXTRA);
                    skill.factionSkillPower -= COALITION_FACTION_PENALTY; // Penalty for faction, to push it out of their sims
                    break;

                // --------------
                // Utility
                // --------------
                case "Summon":
                    int summonedCardPower = 0;

                    // What does this summon?
                    Card summonedCard = CardManager.GetById(skill.summon);
                    if (summonedCard != null)
                    {
                        // If the summoned card wasn't assessed, get the summoned card's power
                        if (summonedCard.Power <= 0)
                        {
                            // Hotfix: Don't assess summons that summon. Eden's Assembly created an infinite loop
                            if (card.Set != (int)CardSet.Summon)
                                CalculatePower(summonedCard);
                        }

                        summonedCardPower = summonedCard.Power;
                    }

                    // When does it summon
                    if (skill.trigger != null)
                    {
                        if (skill.trigger.ToLower() == "play" || card.Delay == 0)
                        {
                            summonedCardPower = (int)(summonedCardPower * SUMMON_PERCENT_ONPLAY);                            
                        }
                        else if (skill.trigger.ToLower() == "death")
                        {
                            summonedCardPower = (int)(summonedCardPower * SUMMON_PERCENT_ON_DEATH);
                        }
                        else if (skill.trigger.ToLower() == "attacked")
                        {
                            summonedCardPower = (int)(summonedCardPower * SUMMON_PERCENT_ON_ATTACKED);
                        }
                        else if (card.Delay == 1)
                        {
                            summonedCardPower = (int)(summonedCardPower * SUMMON_PERCENT_1D);
                        }
                        else if (card.Delay == 2)
                        {
                            summonedCardPower = (int)(summonedCardPower * SUMMON_PERCENT_2D);
                        }
                        else if (card.Delay == 3)
                        {
                            summonedCardPower = (int)(summonedCardPower * SUMMON_PERCENT_3D);
                        }
                        else if (card.Delay == 4)
                        {
                            summonedCardPower = (int)(summonedCardPower * SUMMON_PERCENT_4D);
                        }

                        if (summonedCardPower <= 0) summonedCardPower = 1;
                    }

                    skill.skillPower += summonedCardPower;
                    skillPowerString.Append(summonedCardPower);
                    skillPowerString.Append("\t");

                    break;
                case "Tribute":
                    GetSkillPowerX(card, skill, TRIBUTE_BASE);
                    break;
                case "Inhibit":
                    GetSkillPowerX(card, skill, INHIBIT_BASE);
                    break;
                case "Sabotage":
                    GetSkillPowerX(card, skill, SABOTAGE_BASE);
                    break;
                //case "Siege":
                //    if (skill.all)
                //        GetSkillPowerX(card, skill, 20, 5, skillAll: true, xBaseExtraPowerCap: 3);
                //    else
                //        GetSkillPowerX(card, skill, 25, 5);
                //    break;
                case "Besiege": //Mortar
                    if (skill.all)
                        GetSkillPowerX(card, skill, MORTAR_BASE, MORTAR_EXTRA, skillAll: true);
                    else
                        GetSkillPowerX(card, skill, MORTAR_SINGLE_BASE, MORTAR_EXTRA * 2);
                    break;
                case "Mimic":
                    GetSkillPowerX(card, skill, MIMIC_BASE, MIMIC_EXTRA);
                    break;
                case "Disease":
                    GetSkillPowerX(card, skill, DISEASE_BASE, DISEASE_EXTRA);
                    break;
                case "Evolve":
                    var evolvePower = 0;
                    if (skill.skill2 == "Rupture")
                        evolvePower = 3;
                    else if (skill.skill2 == "Sunder")
                        evolvePower = 1;
                    else
                        evolvePower = 1;

                    skill.skillPower += evolvePower;
                    skillPowerString.Append(evolvePower);
                    skillPowerString.Append("\t");
                    break;
                case "Overload":
                    var overloadPower = 0;

                    if (skill.all || skill.n >= 4) overloadPower += 4;
                    else if (skill.n == 3) overloadPower += 3;
                    else if (skill.n == 2) overloadPower += 2;
                    else overloadPower += 1;

                    //if (card.Delay == 2) overloadPower-= 1;
                    if (card.Delay >= 3) overloadPower-= 1;
                    

                    if (skill.y <= 0) skill.skillPower += overloadPower;
                    else skill.factionSkillPower += overloadPower;

                    skillPowerString.Append(overloadPower);
                    skillPowerString.Append("\t");

                    break;
                case "Jam":
                    var jamPower = 0;
                    bool jamOnPlay = false; 

                    if (skill.trigger == "Play")
                    {
                        jamOnPlay = true;
                        if (skill.all || skill.n >= 4) jamPower += 4;
                        else if (skill.n <= 1) jamPower += 1;
                        else if (skill.n == 2) jamPower += 2;
                        else if (skill.n == 3) jamPower += 3;
                    }
                    else if (card.Delay == 0)
                    {
                        if (skill.all || skill.n >= 4) jamPower += 10;
                        else if (skill.n <= 1) jamPower += 1;
                        else if (skill.n == 2) jamPower += 3;
                        else if (skill.n == 3) jamPower += 5;
                    }
                    else if (card.Delay == 1)
                    {
                        if (skill.all || skill.n >= 4) jamPower += 7;
                        else if (skill.n <= 1) jamPower += 1;
                        else if (skill.n == 2) jamPower += 2;
                        else if (skill.n == 3) jamPower += 5;
                    }
                    else if (card.Delay == 2)
                    {
                        if (skill.all || skill.n >= 4) jamPower += 5;
                        else if (skill.n <= 1) jamPower += 0;
                        else if (skill.n == 2) jamPower += 1;
                        else if (skill.n == 3) jamPower += 2;
                    }
                    else if (card.Delay >= 3)
                    {
                        if (skill.all || skill.n >= 4) jamPower += 5;
                        else if (skill.n <= 1) jamPower += 0;
                        else if (skill.n == 2) jamPower += 1;
                        else if (skill.n == 3) jamPower += 2;
                    }

                    if (skill.c == 1) jamPower += 3;
                    else if (skill.c == 2) jamPower += 1;

                    //Penalty: Jam without evade gets -1 unless its on play
                    if (!jamOnPlay &&
                        card.s1.id != "Evade" && card.s2.id != "Evade" && card.s3.id != "Evade" &&
                        card.CardType == CardType.Assault.ToString())
                    {
                        jamPower -= 1;
                    }

                    skill.skillPower += jamPower;
                    skillPowerString.Append(jamPower);
                    skillPowerString.Append("\t");

                    break;
                case "Flurry":
                    var flurryPower = 0;

                    // Number of flurries
                    if (skill.x >= 4) flurryPower += 7;
                    else if (skill.x == 3) flurryPower += 5; //may change if flurry3s come out and suck
                    else if (skill.x == 2) flurryPower += 2;
                    else flurryPower += 1;

                    // Cooldown
                    if (skill.c == 1) flurryPower += 1;
                    else if (skill.c == 2) flurryPower += 1;
                    else if (skill.c >= 3) flurryPower += 0;

                    // Penalty for delay
                    if (card.Delay == 2) flurryPower -= 1;
                    else if (card.Delay == 3) flurryPower -= 1;
                    else if (card.Delay == 4) flurryPower /= 2;

                    // Structure flurry reduced
                    if (card.CardType == CardType.Structure.ToString()) flurryPower -= 2;

                    if (flurryPower <= 0) flurryPower = 1;

                    skill.skillPower += flurryPower;
                    skillPowerString.Append(flurryPower);
                    skillPowerString.Append("\t");

                    break;
                case "Wall":
                    skill.skillPower += 1;
                    skillPowerString.Append("1\t");
                    break;
                default:
                    skillPowerString.Append("0\t");
                    break;
            }
        }

        /// <summary>
        /// Card - the card being assessed
        /// Skill - the skill being assessed
        /// 
        /// xBase - the minimum amount this skill needs to be to count (ex: Rally All 5 or better). xThreshold is weighted by unit delay
        /// xBaseExtra - every X points over the xThreshold gives additional power. This caps
        /// 
        /// xBasePower - the amount of power passing the base gives (1)
        /// xBaseExtraPowerCap - the max amount of power given
        /// skillAll - only apply if skill is "all"
        /// weightLess - don't apply delay weight (Stasis)
        /// factionOnly - only apply factionBonus (Allegiance, Legion)
        /// 
        /// writeSkillToString - add this skill to the result stringbuilder. The only time we don't want to do this is if a skill is being considered twice like Stasis
        /// </summary>
        private static void GetSkillPowerX(Card c, CardSkill skill, 
            int xBase, int xBaseExtra = -1, int xBasePower = 1, int xBaseExtraPowerCap = 5, 
            bool skillAll = false, bool weightLess = false, bool factionOnly=false, bool writeSkillToString=true)
        {
            int basePower = 0;
            int extraPower = 0;


            // Multiply the X by delay penalty
            if (!weightLess && skill.trigger != "attacked")
            {
                xBase = (int)(Math.Ceiling(xBase * delayMultiplier));
                //xBaseExtra = (int)(Math.Ceiling(xBaseExtra * delayMultiplier));
            }

            // If a skill requires all, like "Strike All 7"
            if (skillAll == true && skill.all == false ||
                skillAll == false && skill.all == true)
            {
                return;
            }

            //  base and extra power
            if (skill.x >= xBase)
            {
                basePower += xBasePower;

                if (xBaseExtra > 0)
                    extraPower = Math.Min((skill.x - xBase) / xBaseExtra, xBaseExtraPowerCap);
            }

            // if y is set, then only adjust faction power
            if (skill.y == 0 && !factionOnly)
            {
                skill.skillPower += basePower + extraPower;
            }
            else
            {
                skill.factionSkillPower += basePower + extraPower;
            }

            // OnDeath: reduce power by 1/2
            if (skill.trigger == "death")
            {
                skill.skillPower /= 2;
            }
            // OnPlay: Add 1 power (for now)
            if (skill.trigger == "play")
            {
                skill.skillPower += 1;
            }

            // Note how much power this adds
            if (writeSkillToString)
            {
                int totalPower = basePower + extraPower;
                skillPowerString.Append(totalPower + "(" + basePower + "+" + extraPower + ")");
            }

        }




        /// <summary>
        /// Create a single tab-separated line for a card's stats
        /// </summary>
        private static void WriteCardStats(Card card)
        {
            //Name  TotalPower  FactionTotalPower   Section     Faction Rarity  Subset  Attack  Health  Delay   Skill1  Skill2  Skill3
            //SectionPower  SetPower    RarityPower ModifiedPower   SkillPower  
            result.Append(card.Name);
            result.Append("\t");
            result.Append(card.Power);
            result.Append("\t");
            result.Append(card.FactionPower);
            result.Append("\t");
            result.Append(card.Section);
            result.Append("\t");
            result.Append(((Faction)card.Faction).ToString());
            result.Append("\t");
            result.Append(((Rarity)card.Rarity).ToString());
            result.Append("\t");
            result.Append(card.Subset);
            result.Append("\t");
            //result.Append(card.Fusion_Level == 1 ? "Dual" : "");
            //result.Append("\t");
            result.Append(card.Attack);
            result.Append("\t");
            result.Append(card.Health);
            result.Append("\t");
            result.Append(card.Delay);
            result.Append("\t");

            // Skills
            if (!String.IsNullOrEmpty(card.s1.id))
            {
                result.Append(card.s1.id + " ");
                if (card.s1.all)
                    result.Append("All ");
                if (card.s1.n > 0)
                    result.Append(card.s1.n + " ");

                if (card.s1.y > 0)
                    result.Append((Faction)card.s1.y + " ");
                if (card.s1.x > 0)
                    result.Append(card.s1.x + " ");
                if (card.s1.c > 0)
                    result.Append("every " + card.s1.c + " ");
                if (!String.IsNullOrEmpty(card.s1.skill2))
                    result.Append(card.s1.skill2 + " ");
            }
            result.Append("\t");
            if (!String.IsNullOrEmpty(card.s2.id))
            {
                result.Append(card.s2.id + " ");
                if (card.s2.all)
                    result.Append("All ");
                if (card.s2.n > 0)
                    result.Append(card.s2.n + " ");

                if (card.s2.y > 0)
                    result.Append((Faction)card.s2.y + " ");
                if (card.s2.x > 0)
                    result.Append(card.s2.x + " ");
                if (card.s2.c > 0)
                    result.Append("every " + card.s2.c + " ");
                if (!String.IsNullOrEmpty(card.s2.skill2))
                    result.Append(card.s2.skill2 + " ");
            }
            result.Append("\t");
            if (!String.IsNullOrEmpty(card.s3.id))
            {
                result.Append(card.s3.id + " ");
                if (card.s3.all)
                    result.Append("All ");
                if (card.s3.n > 0)
                    result.Append(card.s3.n + " ");

                if (card.s3.y > 0)
                    result.Append((Faction)card.s3.y + " ");
                if (card.s3.x > 0)
                    result.Append(card.s3.x + " ");
                if (card.s3.c > 0)
                    result.Append("every " + card.s3.c + " ");
                if (!String.IsNullOrEmpty(card.s3.skill2))
                    result.Append(card.s3.skill2 + " ");
            }
            result.Append("\t");
            result.Append(card.SectionPower);
            result.Append("\t");
            result.Append(card.SetPower);
            result.Append("\t");
            result.Append(card.HealthPower);
            result.Append("\t");
            result.Append(card.PowerModifier);
            result.Append("\t");
            result.Append(card.s1.skillPower + " (" + card.s1.factionSkillPower + ")");
            result.Append("\t");
            result.Append(card.s2.skillPower + " (" + card.s2.factionSkillPower + ")");
            result.Append("\t");
            result.Append(card.s3.skillPower + " (" + card.s3.factionSkillPower + ")");
            result.Append("\t");
            result.Append(skillPowerString);
        }

    }
}
