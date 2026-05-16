using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using ProjectTallify.Models;
using System.Text.Json; // For JsonSerializerOptions

namespace ProjectTallify.Services
{
    /// <summary>
    /// Service responsible for processing score submissions, calculating round tallies, and computing overall rankings.
    /// Handles both Criteria-based and Pointing-based (ORW) scoring systems.
    /// </summary>
    public interface IScoringService
    {
        /// <summary>
        /// Processes a single judge's score submission for a specific contestant in a round.
        /// </summary>
        Task ProcessJudgeScoreSubmission(int eventId, int judgeId, int roundId, string contestantCode, List<ScoreInput> submittedScores);

        /// <summary>
        /// Processes a bulk submission of scores from a judge for multiple contestants in a round.
        /// Typically used when a judge submits all scores at once for a round.
        /// </summary>
        Task ProcessJudgeRoundSubmission(int eventId, int judgeId, int roundId, List<ContestantScoreSubmission> submissions); 

        /// <summary>
        /// Computes the overall tally for the entire event, aggregating scores from all rounds.
        /// </summary>
        Task<OverallTallyReport> ComputeOverallTally(int eventId);

        /// <summary>
        /// Computes the tally for a specific round, calculating weighted scores, ranks, and derived criteria.
        /// </summary>
        Task<RoundTallyReport> ComputeRoundTally(int eventId, int roundId);
    }

    /// <summary>
    /// Implementation of the scoring logic for the Tallify application.
    /// </summary>
    public class ScoringService : IScoringService
    {
        private readonly TallifyDbContext _db;

        public ScoringService(TallifyDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Processes a bulk submission of scores from a judge for a specific round.
        /// Clears existing scores for this judge/round before adding new ones to ensure data integrity.
        /// Triggers re-computation of round and overall tallies.
        /// </summary>
        /// <param name="eventId">The ID of the event.</param>
        /// <param name="judgeId">The ID of the judge submitting scores.</param>
        /// <param name="roundId">The ID of the round being scored.</param>
        /// <param name="submissions">A list of submissions, each containing a contestant code and their scores.</param>
        public async Task ProcessJudgeRoundSubmission(int eventId, int judgeId, int roundId, List<ContestantScoreSubmission> submissions)
        {
            // 1. Retrieve Entities
            var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == eventId);
            var round = await _db.Rounds.Include(r => r.Criterias).FirstOrDefaultAsync(r => r.Id == roundId);
            var judge = await _db.Judges.FirstOrDefaultAsync(j => j.Id == judgeId);

            if (ev == null || round == null || judge == null)
            {
                throw new ArgumentException("Invalid event, round, or judge ID provided.");
            }

            // 2. Clear ALL Existing Scores for this judge and round
            var existingScores = await _db.Scores
                .Where(s => s.EventId == eventId && s.RoundId == roundId && s.JudgeId == judgeId)
                .ToListAsync();
            _db.Scores.RemoveRange(existingScores);

            // 3. Loop through submissions and add new scores
            foreach(var submission in submissions)
            {
                var contestant = await _db.Contestants.FirstOrDefaultAsync(c => c.EventId == eventId && c.Code == submission.ContestantCode);
                if (contestant == null) continue; // Skip if invalid contestant code

                foreach (var scoreInput in submission.Scores)
                {
                    var criteria = round.Criterias.FirstOrDefault(c => c.Name == scoreInput.CriteriaName);
                    if (criteria == null) continue; // Skip if unknown criteria

                    _db.Scores.Add(new Score
                    {
                        EventId = eventId,
                        RoundId = roundId,
                        ContestantId = contestant.Id,
                        JudgeId = judgeId,
                        CriteriaId = criteria.Id,
                        Value = scoreInput.Score,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            // 4. Save Changes
            await _db.SaveChangesAsync();

            // 5. Trigger Tally Re-computation
            await ComputeRoundTally(eventId, roundId); 
            await ComputeOverallTally(eventId); 
        }

        /// <summary>
        /// Processes a single score submission for one contestant from a judge.
        /// Useful for real-time saving or single-contestant scoring interfaces.
        /// </summary>
        public async Task ProcessJudgeScoreSubmission(int eventId, int judgeId, int roundId, string contestantCode, List<ScoreInput> submittedScores)
        {
            // 1. Retrieve Entities
            var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == eventId);
            var round = await _db.Rounds.Include(r => r.Criterias).FirstOrDefaultAsync(r => r.Id == roundId);
            var contestant = await _db.Contestants.FirstOrDefaultAsync(c => c.EventId == eventId && c.Code == contestantCode);
            var judge = await _db.Judges.FirstOrDefaultAsync(j => j.Id == judgeId);

            if (ev == null || round == null || contestant == null || judge == null)
            {
                // Handle invalid IDs, maybe throw an exception or log an error
                throw new ArgumentException("Invalid event, round, contestant, or judge ID provided.");
            }

            // 2. Clear Existing Scores for this judge, round, and contestant
            var existingScores = await _db.Scores
                .Where(s => s.EventId == eventId && s.RoundId == roundId && s.ContestantId == contestant.Id && s.JudgeId == judgeId)
                .ToListAsync();
            _db.Scores.RemoveRange(existingScores);
            
            // 3. Create and Add New Scores
            foreach (var scoreInput in submittedScores)
            {
                var criteria = round.Criterias.FirstOrDefault(c => c.Name == scoreInput.CriteriaName);
                if (criteria == null)
                {
                    // Handle unknown criteria, maybe throw an exception or log an error
                    throw new ArgumentException($"Criteria '{scoreInput.CriteriaName}' not found in round {round.Name}.");
                }

                _db.Scores.Add(new Score
                {
                    EventId = eventId,
                    RoundId = roundId,
                    ContestantId = contestant.Id,
                    JudgeId = judgeId,
                    CriteriaId = criteria.Id,
                    Value = scoreInput.Score,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // 4. Save Changes
            await _db.SaveChangesAsync();

            // 5. Trigger Tally Re-computation
            await ComputeRoundTally(eventId, roundId); // Recompute for this round
            await ComputeOverallTally(eventId); // Recompute overall
        }

        /// <summary>
        /// Computes the overall results for the event.
        /// Sums up the computed scores from all rounds for each contestant and calculates final ranks.
        /// </summary>
        public async Task<OverallTallyReport> ComputeOverallTally(int eventId)
        {
            var report = new OverallTallyReport();

            var ev = await _db.Events.Include(e => e.Contestants).FirstOrDefaultAsync(e => e.Id == eventId);
            if (ev == null)
            {
                // Log error or throw
                return report;
            }

            // Clear existing overall scores for this event
            var existingOverallScores = await _db.OverallScores
                .Where(os => os.EventId == eventId)
                .ToListAsync();
            _db.OverallScores.RemoveRange(existingOverallScores);
            await _db.SaveChangesAsync();

            var contestantOverallScores = new List<(Contestant Contestant, decimal OverallScore)>();

            foreach (var contestant in ev.Contestants)
            {
                decimal overallTotalScore = 0;

                // Fetch all computed round scores for this contestant across all rounds
                var computedScoresForContestant = await _db.ComputedRoundScores
                    .Where(crs => crs.EventId == eventId && crs.ContestantId == contestant.Id && crs.CriteriaId == null) // Assuming CriteriaId == null for round totals
                    .ToListAsync();
                
                // Sum all round total scores to get the overall score
                overallTotalScore = computedScoresForContestant.Sum(crs => crs.Score);

                // TODO: Handle overall derived criteria if any (e.g., "Preliminary" in "Top 3 Round" is derived from Preliminary's total score)
                // This would be more complex and might involve re-weighting or specific logic based on the event setup.
                // For now, it's a direct sum of round totals.

                contestantOverallScores.Add((Contestant: contestant, OverallScore: overallTotalScore));
            }

            // Store OverallScore and Compute Ranks
            var allOverallScores = contestantOverallScores.Select(cos => cos.OverallScore).ToList();
            foreach (var cos in contestantOverallScores)
            {
                var rank = CalculateRankAvg(allOverallScores, cos.OverallScore);

                _db.OverallScores.Add(new OverallScore
                {
                    EventId = eventId,
                    ContestantId = cos.Contestant.Id,
                    Score = cos.OverallScore,
                    Rank = rank, // Store as int
                    ComputedAt = DateTime.UtcNow
                });
            }
            await _db.SaveChangesAsync();



            // Re-fetch ranks from the newly saved OverallScores to ensure consistency
            var finalOverallScoresWithRanks = await _db.OverallScores
                .Where(os => os.EventId == eventId)
                .OrderBy(os => os.Rank)
                .ToListAsync();

            report.Scores = finalOverallScoresWithRanks.Select(os => new TallyRow
            {
                ContestantName = ev.Contestants.FirstOrDefault(c => c.Id == os.ContestantId)?.Name ?? "Unknown",
                ContestantCode = ev.Contestants.FirstOrDefault(c => c.Id == os.ContestantId)?.Code ?? "Unknown",
                TotalScore = os.Score,
                Rank = os.Rank
            }).ToList();


            return report;
        }

        /// <summary>
        /// Computes the tally for a specific round.
        /// Handles:
        /// 1. Weighted Averages for Criteria events.
        /// 2. Simple summation for ORW events.
        /// 3. Derived criteria logic (carrying scores from previous rounds).
        /// 4. Rank calculation (Standard Competition Ranking with fractional support for ties).
        /// </summary>
        public async Task<RoundTallyReport> ComputeRoundTally(int eventId, int roundId)
        {
            var report = new RoundTallyReport { RoundName = "Unknown Round" };

            // 1. Fetch Event with all necessary hierarchy
            var ev = await _db.Events
                .Include(e => e.Contestants)
                .Include(e => e.Rounds)
                    .ThenInclude(r => r.Criterias)
                .FirstOrDefaultAsync(e => e.Id == eventId);

            var currentRound = ev?.Rounds.FirstOrDefault(r => r.Id == roundId);

            if (ev == null || currentRound == null)
            {
                return report;
            }

            report.RoundName = currentRound.Name;

            // 2. Clear existing computed scores for this round to start fresh
            var existingComputedScores = await _db.ComputedRoundScores
                .Where(crs => crs.EventId == eventId && crs.RoundId == roundId)
                .ToListAsync();
            _db.ComputedRoundScores.RemoveRange(existingComputedScores);
            await _db.SaveChangesAsync();

            // 3. Prepare data structures
            var contestantScores = new List<(Contestant Contestant, decimal TotalScore, Dictionary<int, decimal> CriteriaScores)>();
            
            // Optimization: Fetch all raw scores for this round at once
            var allRawScores = await _db.Scores
                .Where(s => s.EventId == eventId && s.RoundId == roundId)
                .ToListAsync();

            // Optimization: Fetch all computed scores from POTENTIAL previous rounds (for derived logic)
            var allPriorComputedScores = await _db.ComputedRoundScores
                .Where(crs => crs.EventId == eventId && crs.CriteriaId == null) // Round totals only
                .ToListAsync();

            // 4. Compute for each contestant
            foreach (var contestant in ev.Contestants)
            {
                decimal roundTotalScore = 0;
                var currentContestantCriteriaScores = new Dictionary<int, decimal>(); // CriteriaId -> Weighted Score

                // Filter raw scores for this contestant
                var contestantRawScores = allRawScores.Where(s => s.ContestantId == contestant.Id).ToList();

                // FIX: Only compute if contestant has raw scores in this round OR is marked active.
                // This prevents eliminated contestants from getting "ghost" scores in later rounds (e.g. via derived criteria)
                // just because they exist in the event.
                if (!contestantRawScores.Any() && !contestant.IsActive)
                {
                    continue;
                }

                if (ev.EventType == "criteria") // Weighted Average System
                {
                    foreach (var criteria in currentRound.Criterias.OrderBy(c => c.DisplayOrder))
                    {
                        decimal weightedScore = 0;

                        // --- STEP B: DERIVED CRITERION ---
                        // Check if this criteria is derived from a previous round
                        // Prioritize explicit IsDerived flag, fallback to MinPoints == -1 convention
                        if (criteria.IsDerived || criteria.MinPoints == -1 || criteria.Name.IndexOf("DERIVED FROM", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Round? sourceRound = null;

                            // Strategy 0: Explicit DB Link (Best)
                            if (criteria.DerivedFromRoundId.HasValue)
                            {
                                sourceRound = ev.Rounds.FirstOrDefault(r => r.Id == criteria.DerivedFromRoundId.Value);
                            }

                            // Strategy 1: Look for round name match in the string (Legacy/Fallback)
                            if (sourceRound == null)
                            {
                                sourceRound = ev.Rounds
                                    .Where(r => r.Id != currentRound.Id && criteria.Name.IndexOf(r.Name, StringComparison.OrdinalIgnoreCase) >= 0)
                                    .OrderByDescending(r => r.Order)
                                    .FirstOrDefault();
                            }

                            // Strategy 2 (Fallback): If MinPoints == -1 but no name match, assume immediate previous round
                            if (sourceRound == null && (criteria.MinPoints == -1 || criteria.IsDerived))
                            {
                                sourceRound = ev.Rounds
                                    .Where(r => r.Order < currentRound.Order)
                                    .OrderByDescending(r => r.Order)
                                    .FirstOrDefault();
                            }

                            if (sourceRound != null)
                            {
                                // Fetch the total score of that round for this contestant
                                var prevRoundScore = allPriorComputedScores
                                    .FirstOrDefault(crs => crs.RoundId == sourceRound.Id && crs.ContestantId == contestant.Id);

                                if (prevRoundScore != null)
                                {
                                    // Formula: Previous Round Total * Weight
                                    weightedScore = prevRoundScore.Score * (criteria.WeightPercent / 100M);
                                }
                            }
                        }
                        // --- STEP A: STANDARD CRITERION ---
                        else
                        {
                            // Fetch judge scores for this criteria
                            var criteriaRawValues = contestantRawScores
                                .Where(s => s.CriteriaId == criteria.Id)
                                .Select(s => s.Value)
                                .ToList();

                            if (criteriaRawValues.Any())
                            {
                                // Formula: (Sum of Scores / Count of Judges) * Weight
                                decimal averageScore = criteriaRawValues.Average();
                                weightedScore = averageScore * (criteria.WeightPercent / 100M);
                            }
                        }

                        currentContestantCriteriaScores.Add(criteria.Id, weightedScore);
                        
                        // --- STEP C: ROUND TOTAL ---
                        roundTotalScore += weightedScore;
                    }
                }
                else if (ev.EventType == "orw") // Pointing System (Objective Right/Wrong)
                {
                    // Sum all raw points directly. Weight is ignored or implicitly 100% of the raw value.
                    roundTotalScore = contestantRawScores.Sum(s => s.Value);
                }

                contestantScores.Add((Contestant: contestant, TotalScore: roundTotalScore, CriteriaScores: currentContestantCriteriaScores));
            }

            // 5. Save Computed Results & Calculate Ranks
            var scoresForRanking = contestantScores.Select(cs => cs.TotalScore).ToList();
            
            foreach (var cs in contestantScores)
            {
                var rank = CalculateRankAvg(scoresForRanking, cs.TotalScore);

                // A. Save Round Total
                _db.ComputedRoundScores.Add(new ComputedRoundScore
                {
                    EventId = eventId,
                    RoundId = roundId,
                    ContestantId = cs.Contestant.Id,
                    Score = cs.TotalScore,
                    Rank = rank,
                    ComputedAt = DateTime.UtcNow,
                    CriteriaId = null // Indicates Round Total
                });

                // B. Save Criteria Breakdown (Optional but good for reports)
                foreach (var kvp in cs.CriteriaScores)
                {
                    _db.ComputedRoundScores.Add(new ComputedRoundScore
                    {
                        EventId = eventId,
                        RoundId = roundId,
                        ContestantId = cs.Contestant.Id,
                        Score = kvp.Value,
                        Rank = 0, // No specific rank for criteria breakdown usually
                        ComputedAt = DateTime.UtcNow,
                        CriteriaId = kvp.Key
                    });
                }
            }
            
            await _db.SaveChangesAsync();

            // 6. Generate Report DTO
            var finalScores = await _db.ComputedRoundScores
                .Where(crs => crs.EventId == eventId && crs.RoundId == roundId && crs.CriteriaId == null)
                .OrderBy(crs => crs.Rank)
                .ToListAsync();

            report.Scores = finalScores.Select(crs => new TallyRow
            {
                ContestantName = ev.Contestants.FirstOrDefault(c => c.Id == crs.ContestantId)?.Name ?? "Unknown",
                ContestantCode = ev.Contestants.FirstOrDefault(c => c.Id == crs.ContestantId)?.Code ?? "Unknown",
                TotalScore = crs.Score,
                Rank = crs.Rank
            }).ToList();

            return report;
        }
    
        // --- Helper methods for scoring logic ---

        private decimal CalculateRankAvg(List<decimal> scores, decimal currentScore)
        {
            if (scores == null || !scores.Any())
            {
                return 0; // No scores to rank against
            }

            // Sort scores in descending order (higher score is better)
            var sortedScores = scores.OrderByDescending(s => s).ToList();

            int firstPosition = -1;
            int lastPosition = -1;

            for (int i = 0; i < sortedScores.Count; i++)
            {
                if (sortedScores[i] == currentScore)
                {
                    if (firstPosition == -1)
                    {
                        firstPosition = i + 1; // 1-based rank
                    }
                    lastPosition = i + 1; // 1-based rank
                }
            }

            if (firstPosition == -1)
            {
                // currentScore not found in the list. This scenario should ideally not happen
                // if currentScore is a score from the list being ranked.
                return 0; // Indicate non-rankable or error state
            }

            // If there are ties, calculate the average of their ranks
            if (firstPosition != lastPosition)
            {
                return (decimal)(firstPosition + lastPosition) / 2;
            }
            else
            {
                return (decimal)firstPosition; // No ties, rank is simply its position
            }
        }        
    }
    
    // --- DTOs for score input ---
    public class ScoreInput
    {
        public string CriteriaName { get; set; } = "";
        public decimal Score { get; set; }
    }

    public class ContestantScoreSubmission
    {
        public string ContestantCode { get; set; } = "";
        public List<ScoreInput> Scores { get; set; } = new();
    }
    
    // --- DTOs for reporting ---
    public class TallyRow
    {
        public string ContestantName { get; set; } = "";
        public string ContestantCode { get; set; } = "";
        public Dictionary<string, decimal?> JudgeScores { get; set; } = new Dictionary<string, decimal?>(); // JudgeName -> Score
        public decimal TotalScore { get; set; }
        public decimal Rank { get; set; }
    }

    public class RoundTallyReport
    {
        public string RoundName { get; set; } = "";
        public List<TallyRow> Scores { get; set; } = new List<TallyRow>();
        public List<TallyRow> Ranks { get; set; } = new List<TallyRow>(); // For Judge ranking for each contestant
    }

    public class OverallTallyReport
    {
        public List<TallyRow> Scores { get; set; } = new List<TallyRow>();
        public List<TallyRow> Ranks { get; set; } = new List<TallyRow>(); // For Judge ranking for each contestant
    }
}