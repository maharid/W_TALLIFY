using ProjectTallify.Models;

namespace ProjectTallify.Models
{
    public class ReportData
    {
        public Event Event { get; set; } = null!;
        public List<ReportRoundData> Rounds { get; set; } = new();
        public List<Judge> Judges { get; set; } = new();
        public List<Contestant> Contestants { get; set; } = new();
    }

    public class ReportRoundData
    {
        public int RoundId { get; set; }
        public string RoundName { get; set; } = "";
        public int Order { get; set; }
        public List<Criteria> Criteria { get; set; } = new();
        public List<Contestant> Participants { get; set; } = new(); // NEW: Contestants specific to this round

        // Data for "Consolidated" & "Result per Round"
        // Key: ContestantId
        public List<ContestantScoreSummary> OverallScores { get; set; } = new(); // Score-based (Weighted Average)
        public List<ContestantScoreSummary> RankSumScores { get; set; } = new(); // Rank-based (Sum of Ranks)
        
        // Data for "Judge's Score" (Raw scores)
        // JudgeId -> ContestantId -> CriteriaId -> Score Value
        public Dictionary<int, Dictionary<int, Dictionary<int, decimal>>> RawScores { get; set; } = new();

        // Data for "Consolidated" - Detailed Criteria Tables
        // CriteriaId -> List of Rows
        public Dictionary<int, List<CriteriaTableDetailRow>> CriteriaDetails { get; set; } = new();

        // Data for "Winners" (Top 1s)
        public List<ContestantScoreSummary> RoundWinners { get; set; } = new();
        public Dictionary<int, List<ContestantScoreSummary>> CriteriaWinners { get; set; } = new(); // CriteriaId -> Winners List
    }

    public class ContestantScoreSummary
    {
        public int ContestantId { get; set; }
        public string Name { get; set; } = "";
        public string Organization { get; set; } = "";
        public decimal Score { get; set; } // Total Score or Rank Sum
        public decimal Rank { get; set; }
        public Dictionary<int, decimal> CriteriaBreakdown { get; set; } = new(); // NEW: CriteriaId -> Weighted Score
    }

    public class CriteriaTableDetailRow
    {
        public int ContestantId { get; set; }
        public string Name { get; set; } = "";
        public string Organization { get; set; } = "";
        public decimal Rank { get; set; }
        public decimal WeightedScore { get; set; } // The final weighted value used for the round sum
        public decimal AverageScore { get; set; } // Average of raw scores (Standard) or Source Total (Derived)
        public Dictionary<int, decimal> JudgeRawScores { get; set; } = new(); // JudgeId -> Raw
    }
}