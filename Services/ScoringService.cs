using Microsoft.EntityFrameworkCore;
using ProjectTallify.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ProjectTallify.Services
{
    /// <summary>
    /// Service responsible for processing score submissions, calculating round tallies, and computing overall rankings.
    /// Acts as the main logic processor for the scoring engine.
    /// </summary>
    public class ScoringService : IScoringService
    {
        private readonly TallifyDbContext _db;

        public ScoringService(TallifyDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Saves or updates score submissions from a judge for a specific contestant.
        /// Includes validation for point ranges and active status.
        /// </summary>
        public async Task<bool> ProcessJudgeRoundSubmission(int eventId, int judgeId, int roundId, List<ContestantScoreSubmission> submissions)
        {
            var ev = await _db.Events
                .Include(e => e.Rounds).ThenInclude(r => r.Criterias)
                .Include(e => e.Contestants)
                .FirstOrDefaultAsync(e => e.Id == eventId);

            if (ev == null) return false;

            var round = ev.Rounds.FirstOrDefault(r => r.Id == roundId);
            if (round == null) return false;

            // 1. Transaction to ensure atomicity
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                foreach (var submission in submissions)
                {
                    var contestant = ev.Contestants.FirstOrDefault(c => c.Code == submission.ContestantCode);
                    if (contestant == null) continue;

                    foreach (var scoreInput in submission.Scores)
                    {
                        var criteria = round.Criterias.FirstOrDefault(c => c.Name == scoreInput.CriteriaName);
                        if (criteria == null) continue;

                        // Range Validation
                        if (!criteria.IsDerived && criteria.MinPoints != -1)
                        {
                            if (scoreInput.Score < criteria.MinPoints || scoreInput.Score > criteria.MaxPoints)
                            {
                                throw new Exception($"Score for {criteria.Name} must be between {criteria.MinPoints} and {criteria.MaxPoints}.");
                            }
                        }

                        var existingScore = await _db.Scores.FirstOrDefaultAsync(s =>
                            s.EventId == eventId &&
                            s.RoundId == roundId &&
                            s.JudgeId == judgeId &&
                            s.ContestantId == contestant.Id &&
                            s.CriteriaId == criteria.Id);

                        if (existingScore != null)
                        {
                            existingScore.Value = scoreInput.Score;
                            existingScore.CreatedAt = DateTime.UtcNow;
                        }
                        else
                        {
                            _db.Scores.Add(new Score
                            {
                                EventId = eventId,
                                RoundId = roundId,
                                JudgeId = judgeId,
                                ContestantId = contestant.Id,
                                CriteriaId = criteria.Id,
                                Value = scoreInput.Score,
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                    }
                }

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"[ScoringService.ProcessJudgeRoundSubmission] Error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Computes the final tally for a round, including:
        /// 1. Individual weighted scores per criterion.
        /// 2. Total round scores per contestant.
        /// 3. Rank calculation (Standard Competition Ranking with fractional support for ties).
        /// 4. Hierarchical Tie-Breaking (Head-to-Head, Premium Criterion).
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
            var contestantsInRound = new List<(Contestant Contestant, decimal TotalScore, Dictionary<int, decimal> CriteriaScores)>();
            
            var allRawScores = await _db.Scores
                .Where(s => s.EventId == eventId && s.RoundId == roundId)
                .ToListAsync();

            var allPriorComputedScores = await _db.ComputedRoundScores
                .Where(crs => crs.EventId == eventId && crs.CriteriaId == null) 
                .ToListAsync();

            // 4. Compute for each contestant
            foreach (var contestant in ev.Contestants)
            {
                decimal roundTotalScore = 0;
                var currentContestantCriteriaScores = new Dictionary<int, decimal>(); 

                var contestantRawScores = allRawScores.Where(s => s.ContestantId == contestant.Id).ToList();

                if (!contestantRawScores.Any() && !contestant.IsActive)
                {
                    continue;
                }

                if (ev.ScoringLogic == "WeightedAverage")
                {
                    foreach (var criteria in currentRound.Criterias.OrderBy(c => c.DisplayOrder))
                    {
                        decimal weightedScore = 0;

                        if (criteria.IsDerived || criteria.MinPoints == -1)
                        {
                            Round? sourceRound = null;
                            if (criteria.DerivedFromRoundId.HasValue)
                                sourceRound = ev.Rounds.FirstOrDefault(r => r.Id == criteria.DerivedFromRoundId.Value);

                            if (sourceRound == null)
                            {
                                sourceRound = ev.Rounds
                                    .Where(r => r.Order < currentRound.Order)
                                    .OrderByDescending(r => r.Order)
                                    .FirstOrDefault();
                            }

                            if (sourceRound != null)
                            {
                                var prevRoundScore = allPriorComputedScores
                                    .FirstOrDefault(crs => crs.RoundId == sourceRound.Id && crs.ContestantId == contestant.Id);

                                if (prevRoundScore != null)
                                    weightedScore = prevRoundScore.Score * (criteria.WeightPercent / 100M);
                            }
                        }
                        else
                        {
                            var criteriaRawValues = contestantRawScores
                                .Where(s => s.CriteriaId == criteria.Id)
                                .Select(s => s.Value)
                                .ToList();

                            if (criteriaRawValues.Any())
                            {
                                decimal averageScore = criteriaRawValues.Average();
                                weightedScore = averageScore * (criteria.WeightPercent / 100M);
                            }
                        }

                        currentContestantCriteriaScores.Add(criteria.Id, weightedScore);
                        roundTotalScore += weightedScore;
                    }
                }
                else if (ev.ScoringLogic == "PointBased")
                {
                    roundTotalScore = contestantRawScores.Sum(s => s.Value);
                }

                contestantsInRound.Add((Contestant: contestant, TotalScore: roundTotalScore, CriteriaScores: currentContestantCriteriaScores));
            }

            // 5. Tie-Breaking Logic (Applied before final saving)
            var totalScoresMap = contestantsInRound.ToDictionary(x => x.Contestant.Id, x => x.TotalScore);
            var participantIds = contestantsInRound.Select(x => x.Contestant.Id).ToList();

            // detect and resolve ties
            var sortedList = totalScoresMap.OrderByDescending(x => x.Value).ToList();
            for (int i = 0; i < sortedList.Count; )
            {
                int j = i;
                while (j < sortedList.Count - 1 && sortedList[j+1].Value == sortedList[i].Value) j++;

                int tiedCount = j - i + 1;
                if (tiedCount > 1)
                {
                    var tiedParticipants = sortedList.Skip(i).Take(tiedCount).Select(p => p.Key).ToList();
                    
                    // Attempt Rule 1: Head-to-Head
                    var h2hWinnerId = await ResolveHeadToHead(eventId, roundId, tiedParticipants);
                    if (h2hWinnerId.HasValue)
                    {
                        totalScoresMap[h2hWinnerId.Value] += 0.0001M;
                        await LogTieBreak(eventId, "Head-to-Head Preference", 
                            $"Contestant '{ev.Contestants.First(c=>c.Id==h2hWinnerId).Name}' won the Head-to-Head matchup among tied participants.");
                    }
                    else
                    {
                        // Attempt Rule 2: Premium Criterion
                        var premiumWinnerId = await ResolvePremiumCriterion(eventId, roundId, tiedParticipants);
                        if (premiumWinnerId.HasValue)
                        {
                            totalScoresMap[premiumWinnerId.Value] += 0.00005M;
                            await LogTieBreak(eventId, "Premium Criterion", 
                                $"Contestant '{ev.Contestants.First(c=>c.Id==premiumWinnerId).Name}' broke the tie via higher score in the premium criterion.");
                        }
                        else
                        {
                            // Rule 3: Manual Required (Halts/Flag in logs)
                            await LogTieBreak(eventId, "Manual Resolution Required", 
                                $"Ties persist for Contestants: {string.Join(", ", tiedParticipants.Select(id => ev.Contestants.First(c=>c.Id==id).Name))}. Manual override required via official certified sheets.");
                        }
                    }
                }
                i = j + 1;
            }

            // 6. Save Results
            if (ev.ScoringLogic == "PointBased")
            {
                // SPECIAL LOGIC: Point-Based Rank Aggregation
                var judges = allRawScores.Select(s => s.JudgeId).Distinct().ToList();
                var contestantRankSums = new Dictionary<int, decimal>(); 

                foreach (var judgeId in judges)
                {
                    var judgeScores = allRawScores
                        .Where(s => s.JudgeId == judgeId)
                        .GroupBy(s => s.ContestantId)
                        .Select(g => new { ContestantId = g.Key, TotalPoints = g.Sum(s => s.Value) })
                        .ToList();

                    var pointsList = judgeScores.Select(js => js.TotalPoints).ToList();
                    foreach (var js in judgeScores)
                    {
                        var rank = CalculateRankAvg(pointsList, js.TotalPoints);
                        if (!contestantRankSums.ContainsKey(js.ContestantId)) contestantRankSums[js.ContestantId] = 0;
                        contestantRankSums[js.ContestantId] += rank;
                    }
                }

                var rankSumsList = contestantRankSums.Values.ToList();
                foreach (var cs in contestantsInRound)
                {
                    decimal rankSum = contestantRankSums.ContainsKey(cs.Contestant.Id) ? contestantRankSums[cs.Contestant.Id] : 9999;
                    var finalRank = CalculateRankLowIsBetter(rankSumsList, rankSum);

                    _db.ComputedRoundScores.Add(new ComputedRoundScore
                    {
                        EventId = eventId,
                        RoundId = roundId,
                        ContestantId = cs.Contestant.Id,
                        Score = rankSum, 
                        Rank = finalRank,
                        ComputedAt = DateTime.UtcNow,
                        CriteriaId = null
                    });
                }
            }
            else
            {
                // STANDARD LOGIC: Weighted Average
                var updatedScores = contestantsInRound.Select(cs => totalScoresMap[cs.Contestant.Id]).ToList();
                foreach (var cs in contestantsInRound)
                {
                    var finalScore = totalScoresMap[cs.Contestant.Id];
                    var rank = CalculateRankAvg(updatedScores, finalScore);

                    _db.ComputedRoundScores.Add(new ComputedRoundScore
                    {
                        EventId = eventId,
                        RoundId = roundId,
                        ContestantId = cs.Contestant.Id,
                        Score = finalScore,
                        Rank = rank,
                        ComputedAt = DateTime.UtcNow,
                        CriteriaId = null
                    });

                    foreach (var kvp in cs.CriteriaScores)
                    {
                        _db.ComputedRoundScores.Add(new ComputedRoundScore
                        {
                            EventId = eventId,
                            RoundId = roundId,
                            ContestantId = cs.Contestant.Id,
                            Score = kvp.Value,
                            Rank = 0,
                            ComputedAt = DateTime.UtcNow,
                            CriteriaId = kvp.Key
                        });
                    }
                }
            }
            
            await _db.SaveChangesAsync();

            // 7. Generate Report DTO
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

        private async Task<int?> ResolveHeadToHead(int eventId, int roundId, List<int> tiedIds)
        {
            if (tiedIds.Count != 2) return null; // Hierarchical complex ties require manual resolution

            var idA = tiedIds[0];
            var idB = tiedIds[1];

            var scores = await _db.Scores
                .Where(s => s.EventId == eventId && s.RoundId == roundId && (s.ContestantId == idA || s.ContestantId == idB))
                .ToListAsync();

            var judgeIds = scores.Select(s => s.JudgeId).Distinct().ToList();
            var criteria = await _db.Criterias.Where(c => c.RoundId == roundId).ToListAsync();

            int winsA = 0;
            int winsB = 0;

            foreach (var jId in judgeIds)
            {
                decimal totalA = 0;
                decimal totalB = 0;

                foreach (var crit in criteria)
                {
                    var sA = scores.FirstOrDefault(s => s.JudgeId == jId && s.ContestantId == idA && s.CriteriaId == crit.Id);
                    var sB = scores.FirstOrDefault(s => s.JudgeId == jId && s.ContestantId == idB && s.CriteriaId == crit.Id);
                    
                    if (crit.IsDerived) 
                    {
                        totalA += sA?.Value ?? 0;
                        totalB += sB?.Value ?? 0;
                    }
                    else
                    {
                        totalA += (sA?.Value ?? 0) * (crit.WeightPercent / 100M);
                        totalB += (sB?.Value ?? 0) * (crit.WeightPercent / 100M);
                    }
                }

                if (totalA > totalB) winsA++;
                else if (totalB > totalA) winsB++;
            }

            if (winsA > winsB) return idA;
            if (winsB > winsA) return idB;
            return null;
        }

        private async Task<int?> ResolvePremiumCriterion(int eventId, int roundId, List<int> tiedIds)
        {
            var premiumCrit = await _db.Criterias
                .Where(c => c.RoundId == roundId && !c.IsDerived)
                .OrderByDescending(c => c.WeightPercent)
                .ThenBy(c => c.DisplayOrder)
                .FirstOrDefaultAsync();

            if (premiumCrit == null) return null;

            var scores = _db.Scores
                .Where(s => s.RoundId == roundId && s.CriteriaId == premiumCrit.Id && tiedIds.Contains(s.ContestantId))
                .GroupBy(s => s.ContestantId)
                .Select(g => new { ContestantId = g.Key, AvgScore = g.Average(s => s.Value) })
                .OrderByDescending(x => x.AvgScore)
                .ToList();

            if (scores.Count >= 2 && scores[0].AvgScore > scores[1].AvgScore) return scores[0].ContestantId;
            return null;
        }

        private async Task LogTieBreak(int eventId, string rule, string details)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                EventId = eventId,
                Action = $"Tie-Break: {rule}",
                Details = details,
                UserRole = "System",
                UserName = "Tallify Auditor",
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        private decimal CalculateRankAvg(List<decimal> scores, decimal currentScore)
        {
            if (scores == null || !scores.Any()) return 0;
            var sortedScores = scores.OrderByDescending(s => s).ToList();
            int firstPosition = -1;
            int lastPosition = -1;

            for (int i = 0; i < sortedScores.Count; i++)
            {
                if (Math.Abs(sortedScores[i] - currentScore) < 0.000001M) 
                {
                    if (firstPosition == -1) firstPosition = i + 1;
                    lastPosition = i + 1;
                }
            }

            if (firstPosition == -1) return 0;
            return (firstPosition != lastPosition) ? (decimal)(firstPosition + lastPosition) / 2 : (decimal)firstPosition;
        }

        private decimal CalculateRankLowIsBetter(List<decimal> scores, decimal currentScore)
        {
            if (scores == null || !scores.Any()) return 0;
            var sortedScores = scores.OrderBy(s => s).ToList();
            int firstPosition = -1;
            int lastPosition = -1;

            for (int i = 0; i < sortedScores.Count; i++)
            {
                if (Math.Abs(sortedScores[i] - currentScore) < 0.000001M)
                {
                    if (firstPosition == -1) firstPosition = i + 1;
                    lastPosition = i + 1;
                }
            }
            if (firstPosition == -1) return 0;
            return (firstPosition != lastPosition) ? (decimal)(firstPosition + lastPosition) / 2 : (decimal)firstPosition;
        }
    }
    
    // --- DTOs ---
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
    
    public class TallyRow
    {
        public string ContestantName { get; set; } = "";
        public string ContestantCode { get; set; } = "";
        public Dictionary<string, decimal?> JudgeScores { get; set; } = new Dictionary<string, decimal?>(); 
        public decimal TotalScore { get; set; }
        public decimal Rank { get; set; }
    }

    public class RoundTallyReport
    {
        public string RoundName { get; set; } = "";
        public List<TallyRow> Scores { get; set; } = new List<TallyRow>();
        public List<TallyRow> Ranks { get; set; } = new List<TallyRow>();
    }

    public class OverallTallyReport
    {
        public List<TallyRow> Scores { get; set; } = new List<TallyRow>();
        public List<TallyRow> Ranks { get; set; } = new List<TallyRow>();
    }
}
