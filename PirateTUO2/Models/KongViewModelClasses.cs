using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// Support classes for KongViewModel
/// </summary>
namespace PirateTUO2.Models
{
    #region User related

    /// <summary>
    /// Player stats
    /// API format is
    /// user_data
    /// {
    ///     money: x, stamina: x, energy: x, tokens (wb): x, etc
    /// }
    /// </summary>
    public class UserData
    {
        public int Gold { get; set; }
        // Mission energy
        public int Energy { get; set; }
        public int MaxEnergy { get; set; }
        public int Stamina { get; set; }
        public int MaxStamina { get; set; }
        public int Salvage { get; set; }
        public int MaxSalvage { get; set; }
        public int BattleEnergy { get; set; }
        public int MaxBattleEnergy { get; set; }
        public int Warbonds { get; set; }

        // caps: {max_salvage: 12000, max_cards: 3350}
        public int Inventory { get; set; }
        public int MaxInventory { get; set; }

        public string MaxDecks { get; set; }

        public string ActiveDeck { get; set; }
        public string DefenseDeck { get; set; }
    }

    /// <summary>
    /// Player decks
    /// API format is
    /// 1: (1..6)
    /// {
    ///     cards: {cardId: count, cardId: count, ...}, commander_id: cardId, deck_id: (1-6) dominion_id: cardId, name: null
    /// }
    /// </summary>
    public class UserDeck
    {
        public Dictionary<Card, int> Cards { get; set; }
        public Card Commander { get; set; }
        public Card Dominion { get; set; }
        public string Id { get; set; }

        public UserDeck()
        {
            Cards = new Dictionary<Card, int>();
        }

        /// <summary>
        /// Print deck to a string
        /// </summary>
        public string DeckToString(bool groupCards = true)
        {
            StringBuilder deck = new StringBuilder();
            if (Commander != null)
            {
                deck.Append(Commander.Name);
                deck.Append(", ");
            }
            else
            {
                Console.WriteLine("Did not recognize commander in this deck. " + Id);
                deck.Append("Darius Caporegime, ");
            }
            if (Dominion != null)
            {
                deck.Append(Dominion.Name);
                deck.Append(", ");
            }

            foreach (var cards in Cards)
            {
                if (groupCards)
                {
                    deck.Append(cards.Key.Name);
                    if (cards.Value > 1)
                    {
                        deck.Append("#");
                        deck.Append(cards.Value);
                    }

                    deck.Append(", ");
                }
                else
                {
                    for (int i = 0; i < cards.Value; i++)
                    {
                        deck.Append(cards.Key.Name);
                        deck.Append(", ");
                    }
                }
            }

            if (deck.Length > 2) deck.Remove(deck.Length - 2, 2);

            return deck.ToString();
        }
    }

    #endregion

    #region Event Related

    /// <summary>
    /// Brawl data
    /// </summary>
    public class BrawlData
    {
        public int CurrentRank { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int Energy { get; set; }
        public int Points { get; set; }
        public int Seeding { get; set; }
        public int WinStreak { get; set; }
        public double PointsPerWin { get; set; }
    }

    /// <summary>
    /// Local leaderboard for guild brawl
    /// API format is 
    /// brawl_leaderboard: [
    /// {
    ///     user_id, name, points, points_rank, stat
    /// }]
    /// </summary>
    public class BrawlLeaderboard
    {
        public string UserId { get; set; }
        public string Name { get; set; }
        public string Points { get; set; }
        public string PointsRank { get; set; }
    }

    /// <summary>
    /// Conquest data
    /// </summary>
    public class ConquestData
    {
        public int Energy { get; set; }
        public int Influence { get; set; } // Personal score
        public List<CqZoneData> ConquestZones { get; set; } // List of each zone's influence

        public string FactionId { get; set; } // conquest_data.user_conquest.faction_id - Need this to match the current user's guild with CqZoneData guild

        public ConquestData()
        {
            ConquestZones = new List<CqZoneData>();
        }
    }
    // One zone in CQ, with guild rankings
    public class CqZoneData
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int MyGuildInfluence { get; set; } // Most accurate guild score
        public int Tier { get; set; } // For display purposes. T3=Nexus,Phobos - T2, T1
        public List<CqZoneDataRanking> Rankings { get; set; } // //Sometimes the point value is not a number

        public CqZoneData()
        {
            Rankings = new List<CqZoneDataRanking>();
            MyGuildInfluence = -1;
        }
    }

    public class CqZoneDataRanking
    {
        public int Rank { get; set; }
        public string FactionId { get; set; }
        public string Name { get; set; }
        public int Influence { get; set; } //stat: and this metric sometimes has a string, so we need to check for this

        //Influence is accurate within ~5 minutes. The Cq servers are really weird and different guilds will see other guilds scores change at different intervals
    }

    // In-guild scoreboard for cq zones
    public class CqInfluenceLeaderboard
    {
        public string UserId { get; set; }
        public string Name { get; set; }
        public string Influence { get; set; }
    }


    /// <summary>
    /// Raid data
    /// </summary>
    public class RaidData
    {
        public int Id { get; set; }
        public int Energy { get; set; }

        // Boss Stats
        public int Level { get; set; }
        public int Health { get; set; }
        public int MaxHealth { get; set; }
        public DateTimeOffset LevelEnd { get; set; }
        public TimeSpan TimeLeft { get; set; }

        public List<RaidLeaderboard> RaidLeaderboard { get; set; }

        public RaidData()
        {
            RaidLeaderboard = new List<RaidLeaderboard>();
        }
    }

    // Scoreboard for raid
    public class RaidLeaderboard
    {
        public string UserId { get; set; }
        public string Name { get; set; }
        public string Damage { get; set; }
    }

    /// <summary>
    /// War data
    /// TODO: Currently not used
    /// </summary>
    public class WarData
    {
        public string AttackerFactionName { get; set; }
        public string DefenderFactionName { get; set; }

        public string AttackerBGE { get; set; } // defender_faction_battleground or attacker_faction_battleground
        public string DefenderBGE { get; set; }

        public int Energy { get; set; }

        public int AttackerScore { get; set; } // attacker_faction_points
        public int DefenderScore { get; set; } // defender_faction_points

        // defender_faction_fortress_slots: { 11: {faction_id, slot_id, card_id, health})
        // always 11, 12, 21, 22
        public List<WarFortress> AttackerFortresses { get; set; }
        public List<WarFortress> DefenderFortresses { get; set; }
        public List<WarLeaderboard> AttackerLeaderboard { get; set; }
        public List<WarLeaderboard> DefenderLeaderboard { get; set; }

        public WarData()
        {
            AttackerFortresses = new List<WarFortress>();
            DefenderFortresses = new List<WarFortress>();
            AttackerLeaderboard = new List<WarLeaderboard>();
            DefenderLeaderboard = new List<WarLeaderboard>();
        }
    }

    public class CampaignData
    {
        public int Id { get; set; }
        public int LevelCompleted { get; set; } // Highest difficulty beaten
        public int MaxProgress { get; set; } // Number of levels?

        // Number of rewards to collect on each level
        //rewards: {
        //    1: {
        //        card: {63452: {amount: "1", collected: "0"}, 63578: {amount: "1", collected: "0"},…}
        //        63452: {amount: "1", collected: "0"}
        //        63578: {amount: "1", collected: "0"}
        //        63614: {amount: "2", collected: "1"}
        //        mini_pack: {94: {amount: "1", collected: "0", name: "Epic PvE Reward",…}}
        //        pack: {1000: {amount: "1", collected: "0"}},
        //    2: {}

        public bool NormalRewardsToCollect = true;
        public bool HeroicRewardsToCollect = true;
        public bool MythicRewardsToCollect = true;


        public CampaignData() { }
    }

    /// <summary>
    /// War fortress data
    /// </summary>
    public class WarFortress
    {
        public string CardId { get; set; }
        public string Name { get; set; }
        public string Health { get; set; }
    }

    /// <summary>
    /// Current War scoreboard
    /// </summary>
    public class WarLeaderboard
    {
        public string UserId { get; set; }
        public string Name { get; set; }
        public int Score { get; set; }
        public int TotalScore { get; set; }
        public int Energy { get; set; }

        public int Wins { get; set; }
        public int Losses { get; set; }
        public int DefenseWins { get; set; }
        public int DefenseLosses { get; set; }

        public string AttackDeck { get; set; }
        public string DefenseDeck { get; set; }

        public int Winstreak { get; set; }
    }

    #endregion

    #region Faction Related

    /// <summary>
    /// Guild data
    /// </summary>
    public class Faction
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<FactionMember> Members { get; set; }
        public string Message { get; set; } // MOTD

        public int GuildSupplies { get; set; } // Supplies - for GBGEs
        public int GuildPoints { get; set; } // Gems - for towers

        // 11, 12 = Defense. 21, 22 = offense
        public WarFortress Fort11 { get; set; }
        public WarFortress Fort12 { get; set; }
        public WarFortress Fort21 { get; set; }
        public WarFortress Fort22 { get; set; }


        public Faction()
        {
            Members = new List<FactionMember>();
        }
    }

    /// <summary>
    /// Guild member (from init's faction object)
    /// </summary>
    public class FactionMember
    {
        public string Name { get; set; }
        public string UserId { get; set; }
        public TimeSpan LastUpdateTime { get; set; }
        public int Level { get; set; } // pvp level, 1-25
        public int Role { get; set; } // 31=Guild Leader,21=Officer
        public int Rating { get; set; } // battle rating (old pvp metric)
    }

    /// <summary>
    /// Info for one player from the guild screen - this should have their active/defense deck
    /// </summary>
    public class PlayerInfo
    {
        public string UserId { get; set; }
        public string Name { get; set; }
        public string LastUpdateTime { get; set; } // epoch time: 1526485123
        public UserDeck ActiveDeck { get; set; }
        public UserDeck DefenseDeck { get; set; }

        public PlayerInfo()
        {
            ActiveDeck = new UserDeck();
            DefenseDeck = new UserDeck();
        }
    }

    #endregion

    #region Battle related

    /// <summary>
    /// Output for BattleData
    /// API format is
    /// battle_data: {
    ///     ...
    /// }
    /// </summary>
    public class BattleData
    {
        // BattleId (but if BattleId is 0, I think the API returns the last fight)
        public string BattleId { get; set; }

        // True: The "Attacker" is the player, and goes first (battle). False: The "Defender" is the player, and goes second
        public bool IsAttacker { get; set; }

        public string Turn { get; set; }

        // Enemy name
        public string EnemyName { get; set; }
        public string EnemyId { get; set; }

        // --------

        // Attacker/Defender commander
        public string PlayerCommander { get; set; }
        public string EnemyCommander { get; set; }

        // Final decklists 
        //public List<string> PlayerCardsPlayed { get; set; }
        //public List<string> EnemyCardsPlayed { get; set; }

        // 1-10: Attacker assaults
        // 101-110: Defender assaults
        // 51-60(?): Attacker forts (including Dominions and Guild Forts)
        // 151-160(?): Defender forts (including Dominions and Guild Forts)
        public List<MappedCard> PlayerCardsPlayed { get; set; }
        public List<MappedCard> EnemyCardsPlayed { get; set; }

        // Order of player's cards
        public List<MappedCard> PlayerDrawOrder { get; set; }

        // Map of what attacker and defender played
        // Format: { [51-59] , 15x = structures, [1-10] = player assaults, [101-110] = enemy assaults
        public List<MappedCard> CardMap { get; set; }

        // What 3 cards are in the player's hand
        public List<MappedCard> PlayerHand { get; set; }
        // What cards has the opponent played so far
        public List<string> EnemyHand { get; set; }

        // Size of player deck
        public string PlayerSize { get; set; }
        // Size of enemy deck
        public string EnemySize { get; set; }

        // Doms
        public string PlayerDominion { get; set; }
        public string EnemyDominion { get; set; }

        // Forts
        public List<string> PlayerForts { get; set; }
        public List<string> EnemyForts { get; set; }

        // BGEs
        public string BGE { get; set; }
        public string PlayerBGE { get; set; }
        public string EnemyBGE { get; set; }

        // --------

        // Not sure what these do
        public string PlayerPower { get; set; }
        public string EnemyPower { get; set; }

        public bool? Winner { get; set; }

        // --------
        // METADATA: Does not come from API
        public List<string> EnemyCardsRemaining { get; set; }
        public string EnemyGuild { get; set; }

        public BattleData()
        {
            PlayerDrawOrder = new List<MappedCard>();

            PlayerCardsPlayed = new List<MappedCard>();
            EnemyCardsPlayed = new List<MappedCard>();

            PlayerHand = new List<MappedCard>();
            EnemyHand = new List<string>();
            EnemyCardsRemaining = new List<string>();

            PlayerForts = new List<string>();
            EnemyForts = new List<string>();

            CardMap = new List<MappedCard>();
        }


        /// <summary>
        /// Gets the player deck in this format
        /// Player(10): Commander, Dominion, CardA, CardB, ...
        /// </summary>
        public string GetPlayerDeck(bool includeDominion = false)
        {
            try
            {
                StringBuilder result = new StringBuilder();

                // Commander
                result.AppendLine(PlayerCommander);

                // Dominion (if applicable)
                if (includeDominion && !string.IsNullOrWhiteSpace(PlayerDominion))
                {
                    result.Append(PlayerDominion).Append("\r\n");
                }

                // Dominion (if applicable.. it almost always is)
                foreach (var card in PlayerCardsPlayed)
                {
                    result.Append(card.Name).Append("\r\n");
                }

                return result.ToString();
            }
            catch (Exception ex)
            {
                return "Error on parsing: " + ex.Message;
            }
        }

        /// <summary>
        /// Gets the enemy deck in this format
        /// Enemy(10): Commander, Dominion, CardA, CardB, ...
        /// </summary>
        public string GetEnemyDeck(bool includePlayer = false, bool includeDominion = false, bool commaSeparated = true)
        {
            try
            {
                StringBuilder result = new StringBuilder();

                // If enemy name is specified
                if (includePlayer && !string.IsNullOrWhiteSpace(EnemyName))
                {
                    result.Append(EnemyName);

                    if (Int32.Parse(EnemySize) != EnemyCardsPlayed.Count)
                    {
                        result.Append("(").Append(EnemyCardsPlayed.Count).Append("/").Append(EnemySize).Append(")");
                    }

                    result.Append(":");
                }


                // Commander
                if (commaSeparated) result.Append(EnemyCommander).Append(", ");
                else result.AppendLine(EnemyCommander);


                // Dominion (if applicable.. it almost always is)
                if (includeDominion && !string.IsNullOrWhiteSpace(EnemyDominion))
                {
                    if (commaSeparated) result.Append(EnemyDominion).Append(", ");
                    else result.AppendLine(EnemyDominion);
                }

                // Enemy cards played
                foreach (var mappedCard in EnemyCardsPlayed)
                {
                    if (commaSeparated) result.Append(mappedCard.Name).Append(", ");
                    else result.AppendLine(mappedCard.Name);
                }

                return result.ToString();
            }
            catch (Exception ex)
            {
                return "Error on parsing: " + ex.Message;
            }
        }

    }

    /// <summary>
    /// Helper to Mapped card
    /// </summary>
    public class MappedCard
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public MappedCard(int id, string name)
        {
            Id = id;
            Name = name;
        }
    }

    #endregion

    #region PvP related

    /// <summary>
    /// Output for hunting targets
    /// API format is 
    /// hunting_targets: 
    /// {
    ///     userId: obj{ name: x, guild: x, user_id: x},
    ///     userId: ...
    /// }
    /// </summary>
    public class HuntingTarget
    {
        public string UserId { get; set; }
        public string Name { get; set; }
        public string Guild { get; set; }
    }


    #endregion

    #region Mission/Quest related

    /// <summary>
    /// What Quests does the player have - under user_achievements
    /// -1, -2, -3: Guild daily quests
    /// 0-999: ?
    /// 1000-1999: Mutants
    /// 2000-2999: Daily pvp quests
    /// 3000+: Event quests(?). These seem to have 6 digit numbers
    /// </summary>
    public class Quest
    {
        public int Id { get; set; }
        public string Name { get; set; }

        // Guild daily missions - some quests
        public int Progress { get; set; }
        public int MaxProgress { get; set; }

        // req: { mission: "1060": level: "10" } for missions
        public int MissionId { get; set; }

        // quest_id for guild quests
        public int QuestId { get; set; }

        // Notes
        // to attack a GQ - fightFactionQuest, quest_id=Id
        // mission - startMission, mission_id=Id
    }

    /// <summary>
    /// mission_completions. We may not need all the data for this
    /// 5: {number: "181", complete: "10", found_item: 1, star2: "1", star3: "1"}
    /// </summary>
    public class MissionCompletion
    {
        public int Id { get; set; }
        public int NumberOfCompletions { get; set; } // How many times did the user do this mission

        // We don't care about these
        //public int Complete { get; set; } // This is always 10
        //public bool FoundItem { get; set; } 
        //public int Star2 { get; set; } 
        //public int Star3 { get; set; } 
    }

    #endregion

    /// <summary>
    /// Any temporary store item (Box, Surge pack, etc)
    /// </summary>
    public class StorePromo
    {
        public string Name { get; set; }
        public int NumberAvailable { get; set; } // num_available this may not exist
    }

    /// <summary>
    /// When buying, cards in the result.new_cards array
    /// </summary>
    public class StorePromoNewCards
    {
        public List<int> CardIds { get; set; }
        public List<Card> Cards { get; set; }

        public StorePromoNewCards()
        {
            CardIds = new List<int>();
            Cards = new List<Card>();
        }
    }


}
