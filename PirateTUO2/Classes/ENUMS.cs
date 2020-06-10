using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PirateTUO2
{
    public enum BatchSimShortcuts
    {
        Brawl,
        Brawl_Defense,
        War,
        War_Defense,
        CQ,
        CQ_Defense,
        Raid,
        PvP_Attack,
        PvP_Defense
    }

    // Card enums
    public enum Faction
    {
        Rainbow = 0,
        Imperial = 1,
        Raider = 2,
        Bloodthirsty = 3,
        Xeno = 4,
        Righteous = 5,
        Progen = 6
    }

    public enum CardType
    {
        Assault,
        Structure,
        Commander,

        Dominion,
        Fortress
    }
    public enum Rarity
    {
        Common = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4,
        Vindicator = 5,
        Mythic = 6
    }

    public enum FusionLevel
    {
        Unfused = -1,
        Nofuse = 0,
        Dual = 1,
        Quad = 2,
        Octo = 3
    }

    public enum CardSet
    {
        BoxOrReward = 2000,
        Fusion = 2500,
        GameOnly = 6000,
        Commander = 7000,
        Fortress = 8000,
        Dominion = 8500,
        Summon = 9500,
        Unknown = 9999,
    }

    public enum CardSubset
    {
        Box,
        Cache,
        Chance,
        Fusion,
        PvE_Reward,
        PvP_Reward,
        PvE_PvP_Reward,
        Commander,
        Summon,
        GameOnly,
        Dominion,
        Unknown
    }

    // Sim modes
    public enum SimMode
    {
        // One sim
        BASIC,
        // Batch of sims
        BATCH,
        // Special sims
        LIVESIM_BASIC,
        LIVESIM_COMPLEX,
        NAVIGATOR
    }
}
