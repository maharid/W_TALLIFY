namespace ProjectTallify.Models
{
    public class SimpleContestant
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Organization { get; set; }
        public string? PhotoPath { get; set; }
    }

    public class SimpleAccessAccount
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Assigned { get; set; }  // email or contestant IDs
        public string? Pin { get; set; }
        public bool IsVerified { get; set; } // Add this property
    }

    public class SimpleRoundWithCriteria
    {
        public int Id { get; set; } // Add Round Id
        public int Order { get; set; } // Add Round Order
        public string? RoundName { get; set; }
        public string? Status { get; set; } // Add Status
        public List<SimpleCriteriaDetails>? Criteria { get; set; }
    }

    public class SimpleCriteriaDetails
    {
        public string? Name { get; set; }
        public decimal Weight { get; set; }
        public decimal MinPoints { get; set; }
        public decimal MaxPoints { get; set; }
        public bool IsDerived { get; set; } // Added
        public int? DerivedFromRoundIndex { get; set; } // NEW: Index (1-based) of the source round from the wizard
    }

    public class CreateEventRequest
    {
        public int?   EventId           { get; set; } 

        public string? EventName        { get; set; }
        public string? EventVenue       { get; set; }
        public string? EventDescription { get; set; }

        public string? EventStartDate   { get; set; }
        public string? EventStartTime   { get; set; }
        
        public string? ScoringLogic     { get; set; } 
        public string? AccessCode       { get; set; }

        // Structured Lists for Atomic Save (Prompt Requirement)
        public List<SimpleContestant>? Contestants { get; set; } = new();
        public List<SimpleAccessAccount>? AccessUsers { get; set; } = new();

        // Complex nested configs still as JSON for now (or can be structured)
        public string? RoundsJson         { get; set; }
        public string? CriteriaType       { get; set; } // "WeightedAverage" or "PointBased"

        public string? ThemeColor  { get; set; }
        public string? HeaderImage { get; set; }

        public bool ShouldSendJudgeInvites { get; set; }
        public bool IsPublishing { get; set; }
    }

    public class EventActionRequest
    {
        public string? AccessCode { get; set; }
    }

    public class EndRoundRequest
    {
        public int RoundId { get; set; }
        public string? AccessCode { get; set; }
    }

    public class StartRoundRequest
    {
        public int EventId { get; set; }
        public int RoundId { get; set; }
        public List<string>? ContestantIds { get; set; }
    }
}