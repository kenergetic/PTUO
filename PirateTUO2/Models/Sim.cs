using PirateTUO2.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace PirateTUO2.Models
{
    /// <summary>
    /// The string for a TUO sim
    /// </summary>
    public class Sim
    {
        // Basic sim info
        public int Id { get; set; }
        public string Description { get; set; }
        public string Guild { get; set; } //Optional - will bestguess
        public string Player { get; set; } //Optional - will bestguess

        // TODO: Check for x86 mode
        public string TuoExecutable { get; set; }
        public bool UseX86 { get; set; }

        // Deck/Gauntlets
        public Deck MyDeck { get; set; }
        public string EnemyDeck { get; set; }

        // Dominion
        public string MyDominion { get; set; }
        public string EnemyDominion { get; set; }

        // Forts/BGEs
        public string MyForts { get; set; }
        public string EnemyForts { get; set; }
        public string Bge { get; set; }
        public string MyBge { get; set; }
        public string EnemyBge { get; set; }

        // External files
        public string OwnedCards { get; set; }
        public string GauntletFile { get; set; }

        // Sim mode
        public string GameMode { get; set; }
        public string Operation { get; set; }

        // Sim Speed
        public int Iterations { get; set; }
        public int CpuThreads { get; set; }

        // Sim options
        public bool Verbose { get; set; }
        public bool HarmonicMean { get; set; }
        public bool SimDominion { get; set; }
        public int DeckLimitLow { get; set; }
        public int DeckLimitHigh { get; set; }
        public double MinIncrementScore { get; set; }
        public int Fund { get; set; }
        public string ExtraTuoFlags { get; set; }

        // "Live sim" data
        public int Freeze { get; set; }
        public string Hand { get; set; }
        public string EnemyHand { get; set; }
        public int ExtraFreezeCards { get; set; } //how many cards are we doing "combinations" in a full sim


        // ----------------- //
        // Sim result - win rates
        // ----------------- //
        public double WinPercent { get; set; }
        public double WinScore { get; set; }
        public double ScorePerWin { get; set; }
        public double LosePercent { get; set; }
        public double StallPercent { get; set; }
        // Deck
        public string ResultDeck { get; set; }
        public List<Card> ResultDeckList { get; set; }


        // ----------------- //
        // Sim data specific to PTUO
        // ----------------- //
        public SimMode Mode { get; set; }
        public int Timeout { get; set; } //ms
        // Sim status message
        public string StatusMessage { get; set; }


        // ----------------- //
        // API data for live sim stuff
        // ----------------- //
        public List<MappedCard> ApiHand { get; set; }
        public MappedCard LiveSimNextCard { get; set; }


        public Sim()
        {
            TuoExecutable = "tuo";

            GameMode = "surge ordered";
            Operation = "climb";

            Iterations = 200;
            CpuThreads = 4;

            Verbose = false;
            HarmonicMean = false;
            SimDominion = false;
            MinIncrementScore = 0;
            Fund = 0;

            StatusMessage = "";
            Timeout = 0;

            WinScore = -1;
            ScorePerWin = -1;

            ApiHand = new List<MappedCard>();
        }

        #region Helper Methods

        /// <summary>
        /// Returns a tuo string from a sim object
        /// * replaceMyDeckIfGenetic => if doing genetic mode, and a batch sim, replace whatever seed deck is provided with (playername_genetic)
        /// </summary>
        public string SimToString(bool replaceMyDeckIfGenetic = true)
        {
            var result = new StringBuilder();

            if (UseX86)
            {
                TuoExecutable = "tuo-x86";
            }

            //MyDeck = MyDominion + ", " + MyDeck;
            //EnemyDeck = EnemyDominion + ", " + EnemyDeck;
            
            result.Append(TuoExecutable);
            result.Append(" \"");
            //result.Append(MyDeck.DeckToString());

            // Replace MyDeck when doing genetic mode
            if (!Operation.Contains("genetic") || replaceMyDeckIfGenetic == false)
            {
                result.Append(MyDeck.OriginalString);
            }
            else
            {
                string playerName = "UNKNOWN";

                // -o="data/cards/_DT_<playername>.txt" -o="config/card-addons/..."
                string[] ownedCardsSplit = OwnedCards.Split(new string[] { ".txt" }, 2, StringSplitOptions.RemoveEmptyEntries);

                // -o="data/cards/_DT_<playername>"
                string guildPlayerName = ownedCardsSplit[0].Replace("-o=\"data/cards/", "").Trim();

                if (guildPlayerName.Length >= 5)
                {
                    // _DT_<playername>
                    if (guildPlayerName[0] == '_') playerName = guildPlayerName.Substring(4);
                    // DT_<playername>
                    else if (guildPlayerName[2] == '_') playerName = guildPlayerName.Substring(3);
                }

                //string[] playerNamePart = OwnedCards.Split(new char[] { '_' }, 3);
                //string playerName = playerNamePart[playerNamePart.Length - 1].Replace(".txt", "").Replace("\"", "").Trim();

                result.Append(playerName + "-Genetic");
            }
            result.Append("\"");


            result.Append(" \"");
            result.Append(EnemyDeck);
            result.Append("\"");

            // Dominions and Forts (if specified outside of MyDeck/EnemyDeck)
            if (!String.IsNullOrWhiteSpace(MyDominion) && EnemyDominion != "[-1]")
            {
                result.Append(" ydom \"" + MyDominion + "\"");                
            }
            if (!String.IsNullOrWhiteSpace(EnemyDominion) && EnemyDominion != "[-1]")
            {
                result.Append(" edom \"" + EnemyDominion + "\"");
            }

            if (!String.IsNullOrEmpty(MyForts) && MyForts.ToLower() != "none")
            {
                result.Append(" yf \"" + MyForts + "\"");
            }
            if (!String.IsNullOrEmpty(EnemyForts) && EnemyForts.ToLower() != "none")
            {
                result.Append(" ef \"" + EnemyForts + "\"");
            }

            // BGEs
            if (!String.IsNullOrEmpty(Bge) && Bge.ToLower() != "none")
            {
                result.Append(" -e \"" + Bge + "\"");
            }
            if (!String.IsNullOrEmpty(MyBge) && MyBge.ToLower() != "none")
            {
                result.Append(" ye \"" + MyBge + "\"");
            }
            if (!String.IsNullOrEmpty(EnemyBge) && EnemyBge.ToLower() != "none")
            {
                result.Append(" ee \"" + EnemyBge + "\"");
            }

            result.Append(" " + GameMode);
            result.Append(" " + Operation);

            result.Append(" " + OwnedCards);
            result.Append(" " + GauntletFile);

            if (DeckLimitLow > 0 && DeckLimitHigh > 0)
            {
                result.Append(" -L ");
                result.Append(Math.Max(DeckLimitLow, 1));
                result.Append(" ");
                result.Append(Math.Min(DeckLimitHigh, 10));
            }

            if (CpuThreads != 4)
            {
                result.Append(" -t " + CpuThreads);
            }
            if (Verbose)
            {
                result.Append(" +v");
            }
            else
            {
                result.Append(" -v");
            }
            if (HarmonicMean)
            {
                result.Append(" +hm");
            }
            if (SimDominion)
            {
                result.Append(" dom-");
            }
            if (MinIncrementScore > 0)
            {
                result.Append(" mis " + MinIncrementScore);
            }
            if (Fund > 0)
            {
                result.Append(" fund " + Fund);
            }
            if (Freeze > 0)
            {
                result.Append(" freeze " + Freeze);
            }
            if (!String.IsNullOrWhiteSpace(Hand))
            {
                result.Append(" hand \"" + Hand + "\"");
            }
            if (!String.IsNullOrWhiteSpace(EnemyHand))
            {
                result.Append(" enemy:hand \"" + EnemyHand + "\"");
            }

            result.Append(" " + ExtraTuoFlags);

            return result.ToString();
        }

        #endregion
    }
    
    /// <summary>
    /// Sim result
    /// </summary>
    public class SimResult
    {
        // Player 
        public string Player { get; set; }
        public string Guild { get; set; }

        // Deck
        public string PlayerDeck { get; set; }
        public List<Card> PlayerDeckList { get; set; }
        public string EnemyDeck { get; set; }
        public List<Card> EnemyDeckList { get; set; }

        // Forts/Bges
        public string PlayerForts { get; set; }
        public string EnemyForts { get; set; }
        public string GlobalBGE { get; set; }
        public string PlayerBGE { get; set; }
        public string EnemyBGE { get; set; }
        public string PlayerDominion { get; set; }
        
        // Freeze count 
        public int Freeze { get; set; }
        public int ExtraFreezeCards { get; set; }
        // Turn
        public int Turn { get; set; }
        
        // Result type
        public string GameMode { get; set; } // Climb, Climbex, Sim, Reorder
        public string GameOptions { get; set; }

        // Result 
        public double WinPercent { get; set; }
        public double LosePercent { get; set; }
        public double StallPercent { get; set; }


        public SimResult()
        {

        }
    }


    /// <summary>
    /// Batch of sims
    /// </summary>
    public class BatchSim
    {
        // Basic sim info
        public int Id { get; set; }
        public string StatusMessage { get; set; }
        public string Description { get; set; }
        public SimMode Mode { get; set; }
        public ConcurrentBag<Sim> Sims { get; set; } 

        #region Constructor

        public BatchSim()
        {
            Sims = new ConcurrentBag<Sim>();
        }
        public BatchSim(int id, SimMode mode)
        {
            Sims = new ConcurrentBag<Sim>();
            Id = id;
            Description = "BatchSim:" + id + ":" + mode + ":";
            Mode = mode;
        }
        public BatchSim(int id, string description, SimMode mode)
        {
            Sims = new ConcurrentBag<Sim>();
            Id = id;
            Description = "BatchSim:" + id + ":" + mode + ":" + description;
            Mode = mode;
        }

        #endregion

        #region Helpers


        /// <summary>
        /// Return a batch sim description (which will represent the batchsim)
        /// </summary>
        public static string BatchSimToString(BatchSim batchSim)
        {
            return batchSim.Description;
        }

        #endregion
    }

}