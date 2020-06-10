using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PirateTUO2.Models
{
    /// <summary>
    /// Stores the kong input needed to call the API, and various data structures to store the output
    /// </summary>
    public class KongViewModel
    {
        // -------------------------------
        // Request - Input 
        // -------------------------------
        public string UserId { get; set; }
        public string KongId { get; set; }
        public string KongToken { get; set; }
        public string KongName { get; set; } //optional
        public string Password { get; set; } //optional
        public string Syncode { get; set; }
        public string Guild { get; set; } // Metadata - not needed, just for grouping

        // This tells the api what to do
        public string Message { get; set; }

        // Some api requests will take parameters
        // &target_user_id=3072159&var2=x...
        public string Params { get; set; }

        // I think this is where the request came from
        public string ApiStatName { get; set; }



        // -------------------------------
        // Response - General
        // -------------------------------

        // If a result object comes back, there's usually an error
        public string Result { get; set; }
        public string ResultMessage { get; set; }

        // If result returns something
        public string ResultNewCardId { get; set; }


        // -------------------------------
        // Response - Init
        // -------------------------------
        public Dictionary<Card, int> PlayerCards { get; set; }
        public Dictionary<Card, int> RestoreCards { get; set; }
        public Dictionary<string, int> Items { get; set; } //2100: Commander Respec, 2000: Epic SHard, 2001: Legendary Shard

        // Init: battle_to_resume if there's a previous battle (=1)
        public bool BattleToResume { get; set; }

        // User decks
        public List<UserDeck> UserDecks { get; set; }

        // User stats
        public UserData UserData { get; set; }

        // Store promos
        public List<StorePromo> StorePromos { get; set; }

        // Daily bonus time
        public bool DailyBonusAvailable { get; set; }
        //public DateTimeOffset DailyBonusTime { get; set; }

        // -------------------------------
        // Response - Missions
        // -------------------------------
        public List<MissionCompletion> MissionCompletions { get; set; }
        public List<Quest> Quests { get; set; }

        // -------------------------------
        // Response - PVP
        // -------------------------------
        // GetBattleData - if the user is currently fighting
        public BattleData BattleData { get; set; }

        // GetHuntingTargets - if the user is pvping
        public List<HuntingTarget> HuntingTargets { get; set; }

        // Player flags - technically under UserData
        public bool AutoPilot { get; set; }

        // -------------------------------
        // Response - Guild 
        // -------------------------------
        public Faction Faction { get; set; }
        public List<PlayerInfo> PlayerInfo { get; set; }

        // -------------------------------
        // Response - Events
        // -------------------------------
        public BrawlData BrawlData { get; set; }
        public List<BrawlLeaderboard> BrawlLeaderboard { get; set; }

        public ConquestData ConquestData { get; set; }
        public List<CqInfluenceLeaderboard> CqInfluenceLeaderboard { get; set; }

        public RaidData RaidData { get; set; }

        public WarData WarData { get; set; }

        public CampaignData CampaignData { get; set; }

        // -------------------------------
        // Metadata - not part of API 
        // -------------------------------
        public bool GetCardsFromInit { get; set; } // Do we process the cards from init(). It's expensive

        public bool ConquestActive { get; set; }
        public bool ConquestRewardsActive { get; set; }
        public bool ClaimedConquestRewards { get; set; }
        public DateTimeOffset ConquestStartTime { get; set; }
        public DateTimeOffset ConquestEndTime { get; set; }

        public bool BrawlActive { get; set; }
        public bool BrawlRewardsActive { get; set; }
        public bool ClaimedBrawlRewards { get; set; }
        public DateTimeOffset BrawlStartTime { get; set; }
        public DateTimeOffset BrawlEndTime { get; set; }

        public bool RaidActive { get; set; }
        public bool RaidRewardsActive { get; set; }
        public bool ClaimedRaidRewards { get; set; }
        public DateTimeOffset RaidStartTime { get; set; }
        public DateTimeOffset RaidEndTime { get; set; }

        public bool WarActive { get; set; }
        public bool WarRewardsActive { get; set; }
        public bool ClaimedWarRewards { get; set; }
        public DateTimeOffset WarStartTime { get; set; }
        public DateTimeOffset WarEndTime { get; set; }

        public bool CampaignActive { get; set; }
        public DateTimeOffset CampaignStartTime { get; set; }
        public DateTimeOffset CampaignEndTime { get; set; }

        public string PtuoMessage { get; set; } // Sometimes we want to inject a status message into complex calls
        public string EnemyDeckSource { get; set; } // If guessing the enemy deck, where did it come from?

        // -------------------------------
        // NewCards
        // -------------------------------
        public StorePromoNewCards StorePromoNewCards { get; set; }

        // message: Current commmand
        // apiStatName: Previous command
        public KongViewModel(string message, string apiStatName, string apiParams = "")
        {
            // Input default
            ApiStatName = apiStatName;
            Message = message;
            Params = apiParams;
            ResultMessage = "";
            PtuoMessage = "";

            // Output default
            Faction = new Faction();
            StorePromos = new List<StorePromo>();
            PlayerCards = new Dictionary<Card, int>();
            RestoreCards = new Dictionary<Card, int>();
            Items = new Dictionary<string, int>();

            HuntingTargets = new List<HuntingTarget>();
            UserData = new UserData();
            UserDecks = new List<UserDeck>();
            PlayerInfo = new List<PlayerInfo>();
            Quests = new List<Quest>();
            MissionCompletions = new List<MissionCompletion>();

            // Event data
            BrawlData = new BrawlData();
            ConquestData = new ConquestData();
            RaidData = new RaidData();
            WarData = new WarData();
            CampaignData = new CampaignData();
            BrawlLeaderboard = new List<BrawlLeaderboard>();
            CqInfluenceLeaderboard = new List<CqInfluenceLeaderboard>();

            BattleData = new BattleData { IsAttacker = true };

            // When buying cards
            StorePromoNewCards = new StorePromoNewCards();
        }

        public KongViewModel() : this("getUserAccount", "", "") { }
        public KongViewModel(string message, string apiParams="") : this(message, message, apiParams) { }

        /// <summary>
        /// Returns resultMessage with the kongName and the brackets and newlines clipped off
        /// </summary>
        public string GetResultMessage()
        {
            string resultMessage = ResultMessage.Replace("\r\n", "").Replace("[", "").Replace("]", "");
            resultMessage = KongName + " - " + resultMessage + "\r\n";
            return resultMessage;
        }
    }

}
