using System.Collections.Generic;

namespace ProjectTallify.Models
{
    // ============================
    // Shared helper view models
    // ============================

    public class JudgeContestantViewModel
    {
        public string ContestantId { get; set; } = "";   // e.g. C001
        public string FullName { get; set; } = "";
        public string Subtitle { get; set; } = "";       // school / department
        public string? PhotoPath { get; set; }
    }

    public class CriteriaRowViewModel
    {
        public string Name { get; set; } = "";
        public decimal WeightPercentage { get; set; }
        public decimal MinPoints { get; set; }
        public decimal MaxPoints { get; set; }
        public decimal? Score { get; set; }              // null = no score yet
    }

    public class CriteriaGroupViewModel
    {
        public string GroupName { get; set; } = "";
        public List<CriteriaRowViewModel> Criteria { get; set; } = new();
    }

    // ============================
    // JUDGE SCREEN
    // ============================

    public class JudgeViewModel
    {
        public string EventTitle { get; set; } = "";
        public string EventCode { get; set; } = "";
        public string RoundName { get; set; } = "";
        public int RoundId { get; set; } // Need RoundId for submission
        public bool IsRoundActive { get; set; } // Indicates if scoring is allowed
        public bool IsSubmitted { get; set; } // Check if judge already submitted

        
        // Added for Event Standby State
        public string EventStatus { get; set; } = "preparing";
        public DateTime Schedule { get; set; }

        public string? SelectedContestantId { get; set; }

        // New properties for dynamic dashboard
        public string? ThemeColor { get; set; }
        public string? HeaderImage { get; set; }
        public string JudgeName { get; set; } = "";
        public string OrganizationName { get; set; } = "";
        public string OrganizationSubtitle { get; set; } = "";
        public string? OrganizationPhotoPath { get; set; }
        public string? Venue { get; set; }

        public List<JudgeContestantViewModel> Contestants { get; set; } = new();
        public List<CriteriaGroupViewModel> Groups { get; set; } = new();
    }

    // ============================
    // REPORTING
    // ============================

    public class ReportViewModel
    {
        public Event Event { get; set; } = null!;
        public List<string> SelectedReports { get; set; } = new();

        // Overall Tally Data
        public List<OverallScoreRow> OverallScores { get; set; } = new();

        // Winners Data (Top N from Overall)
        public List<OverallScoreRow> Winners { get; set; } = new();

        // Score Sheet Data (Complex)
        public List<ScoreSheetRound> ScoreSheetRounds { get; set; } = new();
    }

    public class OverallScoreRow
    {
        public decimal Rank { get; set; }
        public string ContestantName { get; set; } = "";
        public string Organization { get; set; } = "";
        public decimal TotalScore { get; set; }
    }

    public class ScoreSheetRound
    {
        public string RoundName { get; set; } = "";
        public List<string> Judges { get; set; } = new(); // Judge Names
        public List<ScoreSheetRow> Rows { get; set; } = new();
    }

    public class ScoreSheetRow
    {
        public string ContestantName { get; set; } = "";
        public List<decimal> JudgeScores { get; set; } = new(); // Aligned with Judges list
        public decimal AverageScore { get; set; } // Or Total depending on system
        public decimal Rank { get; set; }
    }

    // ============================
    // LIVE TALLY (MANAGE PAGE)
    // ============================

    public class LiveRoundTallyViewModel
    {
        public int RoundId { get; set; }
        public string RoundName { get; set; } = "";
        public string Status { get; set; } = "";
        public int Order { get; set; }
        public bool IsComplete { get; set; }
        public List<SimpleJudgeViewModel> Judges { get; set; } = new();
        public List<LiveTallyRow> Rows { get; set; } = new();
        public List<LiveSummaryRow> SummaryRows { get; set; } = new();
        
        // New for Detailed Breakdown
        public List<SimpleCriteriaViewModel> CriteriaColumns { get; set; } = new();
        public List<CriteriaDetailTableViewModel> DetailedTables { get; set; } = new();
        public string? ThemeColor { get; set; } // Added for dynamic styling
    }

    public class SimpleJudgeViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class LiveTallyRow
    {
        public string ContestantName { get; set; } = "";
        public string ContestantCode { get; set; } = ""; // Added
        public string Organization { get; set; } = ""; // Added
        public string? PhotoPath { get; set; } // Added
        public Dictionary<int, string> JudgeScores { get; set; } = new Dictionary<int, string>(); // JudgeId -> Score/Status
    }

    public class LiveSummaryRow
    {
        public decimal Rank { get; set; }
        public string ContestantName { get; set; } = "";
        public string Organization { get; set; } = ""; 
        public string? PhotoPath { get; set; } 
        public decimal AverageScore { get; set; }
        public decimal TotalScore { get; set; } // Weighted/Accumulated
        public Dictionary<int, decimal> CriteriaScores { get; set; } = new Dictionary<int, decimal>(); // CriteriaId -> Weighted Score
    }

    public class SimpleCriteriaViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public decimal Weight { get; set; }
        public bool IsDerived { get; set; } // Added for derived criteria
    }

    public class CriteriaDetailTableViewModel
    {
        public int CriteriaId { get; set; }
        public string CriteriaName { get; set; } = "";
        public decimal Weight { get; set; }
        public List<CriteriaDetailRowViewModel> Rows { get; set; } = new();
    }

    public class CriteriaDetailRowViewModel
    {
        public decimal Rank { get; set; }
        public string ContestantName { get; set; } = "";
        public string ContestantCode { get; set; } = ""; // Added
        public string Organization { get; set; } = ""; // Added
        public string? PhotoPath { get; set; } // Added
        public Dictionary<int, decimal> JudgeRawScores { get; set; } = new Dictionary<int, decimal>(); // JudgeId -> Raw Score
        public decimal Average { get; set; }
        public decimal Weighted { get; set; }
    }
}