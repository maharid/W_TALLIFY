using Microsoft.EntityFrameworkCore;
using ProjectTallify.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ProjectTallify.Services
{
    /// <summary>
    /// Service for generating PDF reports using QuestPDF.
    /// Handles various report types including Score Sheets, Overall Tally, Winners List, and Round Results.
    /// </summary>
    public class ReportService : IReportService
    {
        private readonly TallifyDbContext _db;

        public ReportService(TallifyDbContext db)
        {
            _db = db;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        /// <summary>
        /// Generates a PDF report document as a byte array based on the requested report types.
        /// Supports multiple report sections in a single document.
        /// </summary>
        /// <param name="eventId">The ID of the event to generate the report for.</param>
        /// <param name="reportTypes">A list of strings indicating which sections to include (e.g., "scoresheet", "overall", "winners").</param>
        /// <returns>A byte array representing the PDF file.</returns>
        public async Task<byte[]> GeneratePdfReportAsync(int eventId, List<string> reportTypes)
        {
            var reportData = await FetchAndPrepareDataAsync(eventId);

            var document = Document.Create(container =>
            {
                // GLOBAL SETTINGS
                
                // OPTION A: Judge's Score (Audit Cards)
                if (reportTypes.Contains("scoresheet") || reportTypes.Contains("judges")) 
                {
                    // "Judge's Score" logic: One section per judge
                    foreach (var judge in reportData.Judges)
                    {
                        container.Page(page =>
                        {
                            page.Margin(1, Unit.Centimetre);
                            page.Size(PageSizes.A4);
                            page.DefaultTextStyle(x => x.FontSize(10));

                            ComposeHeader(page.Header(), reportData.Event, "JUDGE'S SCORECARD");

                            page.Content().PaddingVertical(10).Column(col =>
                            {
                                col.Item().Text($"JUDGE: {judge.Name.ToUpper()}").Bold().FontSize(12).Underline();
                                col.Item().PaddingBottom(10);

                                foreach (var round in reportData.Rounds)
                                {
                                    ComposeJudgeRoundTable(col, round, judge.Id);
                                    col.Item().PaddingBottom(15);
                                }
                                
                                // Signature Line
                                col.Item().ExtendVertical().AlignBottom().Column(sigCol => 
                                {
                                    sigCol.Item().Text("___________________________________").AlignCenter();
                                    sigCol.Item().Text(judge.Name.ToUpper()).Bold().AlignCenter();
                                    sigCol.Item().Text("Signature over Printed Name").FontSize(8).Italic().AlignCenter();
                                });
                            });

                            ComposeFooter(page.Footer());
                        });
                    }
                }

                // OPTION B & C: Consolidated / Result Per Round
                if (reportTypes.Contains("overall") || reportTypes.Contains("resultperround"))
                {
                    bool isConsolidated = reportTypes.Contains("overall");
                    string title = isConsolidated ? "CONSOLIDATED MASTER SHEET" : "RESULT PER ROUND";

                    container.Page(page =>
                    {
                        page.Margin(1, Unit.Centimetre);
                        page.Size(PageSizes.A4.Landscape()); // Landscape for wide tables
                        page.DefaultTextStyle(x => x.FontSize(9));

                        ComposeHeader(page.Header(), reportData.Event, title);

                        page.Content().PaddingVertical(10).Column(col =>
                        {
                            for (int i = 0; i < reportData.Rounds.Count; i++)
                            {
                                var round = reportData.Rounds[i];

                                col.Item().Background(Colors.Grey.Lighten3).Padding(5).Text($"ROUND: {round.RoundName.ToUpper()}").Bold().FontSize(14);
                                col.Item().PaddingBottom(5);

                                // 1. Overall Tally (Score-Based)
                                col.Item().Text("OVERALL TALLY (GAINED SCORE)").Bold();
                                ComposeOverallScoreTable(col, round);
                                col.Item().PaddingBottom(10);

                                // 2. Overall Tally (Rank-Based)
                                col.Item().Text("OVERALL TALLY (RANK SUM / CONSENSUS)").Bold();
                                ComposeRankSumTable(col, round);
                                col.Item().PaddingBottom(10);

                                // 3. Detailed Criteria Tables (Only for Consolidated)
                                if (isConsolidated)
                                {
                                    foreach (var crit in round.Criteria)
                                    {
                                        col.Item().Text($"CRITERIA: {crit.Name.ToUpper()}").Bold();
                                        ComposeCriteriaDetailTable(col, round, crit.Id, reportData.Judges, crit);
                                        col.Item().PaddingBottom(10);
                                    }
                                }

                                if (i < reportData.Rounds.Count - 1)
                                    col.Item().PageBreak();
                            }
                        });

                        ComposeFooter(page.Footer());
                    });
                }

                // OPTION D: Winners
                if (reportTypes.Contains("winners"))
                {
                    container.Page(page =>
                    {
                        page.Margin(1, Unit.Centimetre);
                        page.Size(PageSizes.A4);
                        page.DefaultTextStyle(x => x.FontSize(11));

                        ComposeHeader(page.Header(), reportData.Event, "LIST OF WINNERS");

                        page.Content().PaddingVertical(10).Column(col =>
                        {
                            for (int i = 0; i < reportData.Rounds.Count; i++)
                            {
                                var round = reportData.Rounds[i];
                                
                                col.Item().Background(Colors.Grey.Lighten3).Padding(5).Text(round.RoundName.ToUpper()).Bold().FontSize(14);
                                col.Item().PaddingBottom(5);

                                col.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(2); // Award
                                        columns.RelativeColumn(3); // Winner Name
                                        columns.RelativeColumn(2); // Organization
                                        columns.RelativeColumn(1); // Score
                                    });

                                    table.Header(header =>
                                    {
                                        header.Cell().Element(BlockHeader).Text("Award / Criterion");
                                        header.Cell().Element(BlockHeader).Text("Organization");
                                        header.Cell().Element(BlockHeader).Text("Winner Name");
                                        header.Cell().Element(BlockHeader).AlignRight().Text("Score");
                                    });

                                    // 1. Overall Winner(s)
                                    if (round.RoundWinners.Any())
                                    {
                                        foreach(var winner in round.RoundWinners)
                                        {
                                            table.Cell().Element(BlockCell).Text("ROUND CHAMPION").Bold();
                                            table.Cell().Element(BlockCell).Text(winner.Organization);
                                            table.Cell().Element(BlockCell).Text(winner.Name).Bold();
                                            table.Cell().Element(BlockCell).AlignRight().Text(winner.Score.ToString("F2"));
                                        }

                                        // Tie-Breaker Note
                                        if (round.RoundWinners.Count > 1)
                                        {
                                            table.Cell().ColumnSpan(4).Element(BlockCell).Text("    Note: A tie has occurred. Please consult the Head Judge if a single winner is required to break the tie.")
                                                .FontColor(Colors.Red.Medium).Italic().FontSize(8);
                                        }
                                    }

                                    // 2. Criteria Winners
                                    foreach (var crit in round.Criteria)
                                    {
                                        if (round.CriteriaWinners.TryGetValue(crit.Id, out var winners) && winners != null && winners.Any())
                                        {
                                            foreach(var winner in winners)
                                            {
                                                table.Cell().Element(BlockCell).Text($"Best in {crit.Name}");
                                                table.Cell().Element(BlockCell).Text(winner.Organization);
                                                table.Cell().Element(BlockCell).Text(winner.Name);
                                                table.Cell().Element(BlockCell).AlignRight().Text(winner.Score.ToString("F2"));
                                            }

                                            // Tie-Breaker Note
                                            if (winners.Count > 1)
                                            {
                                                table.Cell().ColumnSpan(4).Element(BlockCell).Text("Note: A tie has occurred. Please consult the Head Judge if a single winner is required to break the tie.")
                                                    .FontColor(Colors.Red.Medium).Italic().FontSize(8);
                                            }
                                        }
                                    }
                                });

                                col.Item().PaddingBottom(20);
                            }
                        });

                        ComposeFooter(page.Footer());
                    });
                }
            });

            return document.GeneratePdf();
        }

        // ==========================================
        // UI COMPOSERS (QuestPDF)
        // ==========================================

        private void ComposeHeader(IContainer container, Event ev, string reportTitle)
        {
            container.Column(col =>
            {
                col.Item().Text(ev.Name).FontSize(20).Bold().AlignCenter();
                if (!string.IsNullOrWhiteSpace(ev.Venue)) col.Item().Text(ev.Venue).FontSize(10).AlignCenter();
                col.Item().Text(ev.StartDateTime.ToString("MMMM dd, yyyy h:mm tt")).FontSize(10).AlignCenter();
                if (!string.IsNullOrWhiteSpace(ev.Description)) col.Item().Text(ev.Description).FontSize(9).Italic().AlignCenter();
                
                col.Item().PaddingTop(10).Text(reportTitle).FontSize(16).Bold().Underline().AlignCenter();
                col.Item().PaddingBottom(10).LineHorizontal(1);
            });
        }

        private void ComposeFooter(IContainer container)
        {
            container.Column(col => 
            {
                col.Item().LineHorizontal(1);
                col.Item().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text(x => {
                        x.Span("Generated via Tallify | ");
                        x.Span(DateTime.Now.ToString("g"));
                    });
                    row.RelativeItem().AlignRight().Text(x => {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" of ");
                        x.TotalPages();
                    });
                });
            });
        }

        private void ComposeJudgeRoundTable(ColumnDescriptor col, ReportRoundData round, int judgeId)
        {
            col.Item().Column(c => 
            {
                c.Item().Text($"Round: {round.RoundName}").Bold();
            
                c.Item().Table(table =>
                {
                    // Columns: Organization + Contestant Name + 1 col per Criteria
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2); // Org
                        columns.RelativeColumn(3); // Name
                        foreach(var c in round.Criteria) columns.RelativeColumn();
                    });

                    // Header
                    table.Header(header =>
                    {
                        header.Cell().BorderBottom(1).Padding(2).Text("Organization").Bold();
                        header.Cell().BorderBottom(1).Padding(2).Text("Contestant Name").Bold();
                        foreach(var c in round.Criteria) 
                            header.Cell().BorderBottom(1).Padding(2).AlignCenter().Text(c.Name).Bold().FontSize(8);
                    });

                    // Rows (Use Filtered Participants)
                    foreach(var contestant in round.Participants)
                    {
                        table.Cell().Padding(2).Text(contestant.Name); // Org
                        table.Cell().Padding(2).Text(contestant.Organization); // Name

                        foreach(var c in round.Criteria)
                        {
                            string val = "-";
                            if (round.RawScores.TryGetValue(judgeId, out var jScores) && 
                                jScores.TryGetValue(contestant.Id, out var cScores) && 
                                cScores.TryGetValue(c.Id, out var score))
                            {
                                val = score.ToString("0.##");
                            }
                            
                            table.Cell().Padding(2).AlignCenter().Text(val);
                        }
                    }
                });
            });
        }

        private void ComposeOverallScoreTable(ColumnDescriptor col, ReportRoundData round)
        {
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(30); // Rank
                    columns.RelativeColumn(3); // Name
                    columns.RelativeColumn(2); // Org
                    // Dynamic Columns for Criteria Breakdown
                    foreach(var c in round.Criteria) columns.RelativeColumn();
                    columns.RelativeColumn(); // Total Score
                });

                table.Header(header =>
                {
                    header.Cell().Element(BlockHeader).Text("Rank");
                    header.Cell().Element(BlockHeader).Text("Organization");
                    header.Cell().Element(BlockHeader).Text("Contestant Name");
                    // Dynamic Headers
                    foreach(var c in round.Criteria) 
                        header.Cell().Element(BlockHeader).AlignRight().Text(c.Name).FontSize(7);
                    
                    header.Cell().Element(BlockHeader).AlignRight().Text("Weighted Score");
                });

                foreach(var item in round.OverallScores.OrderBy(x => x.Rank))
                {
                    table.Cell().Element(BlockCell).Text(item.Rank.ToString("0.##")).Bold();
                    table.Cell().Element(BlockCell).Text(item.Name);
                    table.Cell().Element(BlockCell).Text(item.Organization);

                    // Dynamic Values
                    foreach(var c in round.Criteria)
                    {
                         string val = "-";
                         if (item.CriteriaBreakdown.TryGetValue(c.Id, out var s)) val = s.ToString("F2");
                         table.Cell().Element(BlockCell).AlignRight().Text(val);
                    }

                    table.Cell().Element(BlockCell).AlignRight().Text(item.Score.ToString("F4"));
                }
            });
        }

        private void ComposeRankSumTable(ColumnDescriptor col, ReportRoundData round)
        {
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(30); // Rank
                    columns.RelativeColumn(3); // Name
                    columns.RelativeColumn(2); // Org
                    // Dynamic Columns for Criteria Breakdown (SAME AS TABLE A)
                    foreach(var c in round.Criteria) columns.RelativeColumn();
                    columns.RelativeColumn(); // Sum of Ranks
                });

                table.Header(header =>
                {
                    header.Cell().Element(BlockHeader).Text("Rank");
                    header.Cell().Element(BlockHeader).Text("Organization");
                    header.Cell().Element(BlockHeader).Text("Contestant Name");
                    // Dynamic Headers
                    foreach(var c in round.Criteria) 
                        header.Cell().Element(BlockHeader).AlignRight().Text(c.Name).FontSize(7);
                    
                    header.Cell().Element(BlockHeader).AlignRight().Text("Rank Sum");
                });

                foreach(var item in round.RankSumScores.OrderBy(x => x.Rank))
                {
                    table.Cell().Element(BlockCell).Text(item.Rank.ToString("0.##")).Bold();
                    table.Cell().Element(BlockCell).Text(item.Name);
                    table.Cell().Element(BlockCell).Text(item.Organization);

                    // Dynamic Values (These are RANKS now)
                    foreach(var c in round.Criteria)
                    {
                         string val = "-";
                         if (item.CriteriaBreakdown.TryGetValue(c.Id, out var r)) val = r.ToString("0.##");
                         table.Cell().Element(BlockCell).AlignRight().Text(val);
                    }

                    table.Cell().Element(BlockCell).AlignRight().Text(item.Score.ToString("0.##")); 
                }
            });
        }

        private void ComposeCriteriaDetailTable(ColumnDescriptor col, ReportRoundData round, int criteriaId, List<Judge> judges, Criteria crit)
        {
            if (!round.CriteriaDetails.ContainsKey(criteriaId)) return;

            var rows = round.CriteriaDetails[criteriaId];

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(30); // Rank
                    columns.RelativeColumn(2); // Org
                    columns.RelativeColumn(3); // Name
                    // Dynamic Judge Columns
                    foreach(var j in judges) columns.RelativeColumn();
                    
                    columns.RelativeColumn(); // Average / Score
                    columns.RelativeColumn(); // Weighted
                });

                table.Header(header =>
                {
                    header.Cell().Element(BlockHeader).Text("Rank");
                    header.Cell().Element(BlockHeader).Text("Organization");
                    header.Cell().Element(BlockHeader).Text("Contestant Name");
                    foreach(var j in judges) header.Cell().Element(BlockHeader).AlignCenter().Text(j.Name).FontSize(6); // Small font for judges
                    
                    // Dynamic Header Label
                    string avgLabel = (crit.IsDerived || crit.MinPoints == -1) ? "Total" : "Avg";
                    header.Cell().Element(BlockHeader).AlignRight().Text(avgLabel);
                    
                    header.Cell().Element(BlockHeader).AlignRight().Text("W.Score");
                });

                foreach(var row in rows.OrderBy(x => x.Rank))
                {
                    table.Cell().Element(BlockCell).Text(row.Rank.ToString("0.##")).Bold();
                    table.Cell().Element(BlockCell).Text(row.Name);
                    table.Cell().Element(BlockCell).Text(row.Organization);
                    
                    foreach(var j in judges)
                    {
                        string val = "-";
                        if (row.JudgeRawScores.TryGetValue(j.Id, out var s)) val = s.ToString("0.##");
                        table.Cell().Element(BlockCell).AlignCenter().Text(val);
                    }

                    table.Cell().Element(BlockCell).AlignRight().Text(row.AverageScore.ToString("F2"));
                    table.Cell().Element(BlockCell).AlignRight().Text(row.WeightedScore.ToString("F2"));
                }
            });
        }

        // Style Helpers
        static IContainer BlockHeader(IContainer container)
        {
            return container.BorderBottom(1).Background(Colors.Grey.Lighten4).Padding(2).DefaultTextStyle(x => x.Bold().FontSize(8));
        }
        
        static IContainer BlockCell(IContainer container)
        {
            return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten4).Padding(2).DefaultTextStyle(x => x.FontSize(8));
        }

        // ==========================================
        // DATA FETCHING & LOGIC
        // ==========================================

        /// <summary>
        /// Fetches all necessary data (Event, Judges, Scores, Computed Scores) from the database
        /// and organizes it into a ReportData structure for easy consumption by the PDF composer.
        /// Performs complex logic to link derived scores, calculate ranks, and determine winners.
        /// </summary>
        private async Task<ReportData> FetchAndPrepareDataAsync(int eventId)
        {
            var ev = await _db.Events
                .Include(e => e.Contestants)
                .Include(e => e.Rounds).ThenInclude(r => r.Criterias)
                .FirstOrDefaultAsync(e => e.Id == eventId);

            if (ev == null) throw new ArgumentException("Event not found");

            var judges = await _db.Judges.Where(j => j.EventId == eventId).ToListAsync();
            var scores = await _db.Scores.Where(s => s.EventId == eventId).ToListAsync();
            var computedScores = await _db.ComputedRoundScores.Where(s => s.EventId == eventId).ToListAsync();

            var data = new ReportData
            {
                Event = ev,
                Judges = judges,
                Contestants = ev.Contestants.OrderBy(c => c.Code).ToList(), 
                Rounds = new List<ReportRoundData>()
            };

            foreach (var r in ev.Rounds.OrderBy(x => x.Order))
            {
                var rData = new ReportRoundData
                {
                    RoundId = r.Id,
                    RoundName = r.Name,
                    Order = r.Order,
                    Criteria = r.Criterias.OrderBy(c => c.DisplayOrder).ToList()
                };

                // A. DETERMINE PARTICIPANTS FOR THIS ROUND
                // Strict Filter: Only include contestants who have actual Raw Scores recorded for this round.
                // This filters out eliminated contestants who might exist in the DB but have no activity in this specific round.
                var participantsIds = scores
                    .Where(s => s.RoundId == r.Id)
                    .Select(s => s.ContestantId)
                    .Distinct()
                    .ToList();
                
                // Fallback: If Raw Scores are empty (e.g. Derived Round with no judges, or data initialization), 
                // check Computed Scores. This handles cases where a round is purely calculated from previous rounds.
                if (!participantsIds.Any())
                {
                     participantsIds = computedScores
                        .Where(crs => crs.RoundId == r.Id && crs.CriteriaId == null)
                        .Select(crs => crs.ContestantId)
                        .Distinct()
                        .ToList();
                }

                rData.Participants = data.Contestants
                    .Where(c => participantsIds.Contains(c.Id))
                    .OrderBy(c => c.Code)
                    .ToList();
                
                // 1. Raw Scores Population
                foreach (var j in judges)
                {
                    rData.RawScores[j.Id] = new Dictionary<int, Dictionary<int, decimal>>();
                    foreach(var c in rData.Participants) // Only for Participants
                    {
                        rData.RawScores[j.Id][c.Id] = new Dictionary<int, decimal>();
                    }
                }

                var roundRawScores = scores.Where(s => s.RoundId == r.Id).ToList();
                foreach (var s in roundRawScores)
                {
                    if (s.JudgeId == null) continue;
                    int jId = s.JudgeId.Value;

                    // Ensure key exists (in case a score exists for a non-participant - rare but possible if disqualified later)
                    if (!rData.Participants.Any(p => p.Id == s.ContestantId)) continue; 

                    if (!rData.RawScores.ContainsKey(jId)) rData.RawScores[jId] = new Dictionary<int, Dictionary<int, decimal>>();
                    if (!rData.RawScores[jId].ContainsKey(s.ContestantId)) rData.RawScores[jId][s.ContestantId] = new Dictionary<int, decimal>();
                    
                    rData.RawScores[jId][s.ContestantId][s.CriteriaId] = s.Value;
                }

                // 2. Overall Scores (Score-Based)
                // Filter computed scores for Participants only
                var roundOverall = computedScores
                    .Where(x => x.RoundId == r.Id && x.CriteriaId == null && participantsIds.Contains(x.ContestantId))
                    .ToList();

                // Fetch Criteria Breakdown Computed Scores
                var roundCriteriaComputed = computedScores
                    .Where(x => x.RoundId == r.Id && x.CriteriaId != null && participantsIds.Contains(x.ContestantId))
                    .ToList();

                foreach(var os in roundOverall)
                {
                    var c = data.Contestants.FirstOrDefault(x => x.Id == os.ContestantId);
                    if (c == null) continue;
                    
                    var summary = new ContestantScoreSummary
                    {
                        ContestantId = c.Id,
                        Name = c.Name,
                        Organization = c.Organization,
                        Score = os.Score,
                        Rank = os.Rank
                    };

                    // Populate Breakdown (Weighted Scores)
                    foreach(var crit in rData.Criteria)
                    {
                        var critScore = roundCriteriaComputed.FirstOrDefault(x => x.ContestantId == c.Id && x.CriteriaId == crit.Id);
                        if (critScore != null)
                        {
                            summary.CriteriaBreakdown[crit.Id] = critScore.Score;
                        }
                    }

                    rData.OverallScores.Add(summary);

                } // End of roundOverall loop

                // Store Round Winners (Rank 1) - Supports Ties (e.g. Rank 1.5)
                if (rData.OverallScores.Any())
                {
                    decimal minRank = rData.OverallScores.Min(x => x.Rank);
                    rData.RoundWinners = rData.OverallScores.Where(x => x.Rank == minRank).ToList();
                }

                // 3. Overall Scores (Rank-Based / Consensus)
                // New Logic: Criteria Ranks
                rData.RankSumScores = CalculateRankSumScores(rData.Participants, rData.Criteria, roundCriteriaComputed);

                // 4. Detailed Criteria Tables
                foreach (var crit in rData.Criteria)
                {
                    var rows = new List<CriteriaTableDetailRow>();
                    var critComputed = computedScores.Where(x => x.RoundId == r.Id && x.CriteriaId == crit.Id).ToList();

                    foreach (var c in rData.Participants)
                    {
                        var comp = critComputed.FirstOrDefault(x => x.ContestantId == c.Id);
                        
                        var row = new CriteriaTableDetailRow
                        {
                            ContestantId = c.Id,
                            Name = c.Name,
                            Organization = c.Organization ?? "",
                            WeightedScore = comp?.Score ?? 0,
                        };

                        // Logic for "AverageScore" Column
                        if (crit.IsDerived || crit.MinPoints == -1)
                        {
                            if (crit.WeightPercent > 0)
                            {
                                row.AverageScore = row.WeightedScore / (crit.WeightPercent / 100M);
                            }
                            else
                            {
                                row.AverageScore = row.WeightedScore; // Fallback
                            }
                        }
                        else
                        {
                            // Standard: Average of Judge Raw Scores
                            decimal sumRaw = 0;
                            int countRaw = 0;
                            foreach(var j in judges)
                            {
                                if (rData.RawScores.TryGetValue(j.Id, out var cMap) && 
                                    cMap.TryGetValue(c.Id, out var crMap) && 
                                    crMap.TryGetValue(crit.Id, out var val))
                                {
                                    row.JudgeRawScores[j.Id] = val;
                                    sumRaw += val;
                                    countRaw++;
                                }
                            }
                            row.AverageScore = countRaw > 0 ? sumRaw / countRaw : 0;
                        }
                        
                        rows.Add(row);
                    }

                    // Compute Ranks
                    var sortedRows = rows.OrderByDescending(x => x.WeightedScore).ToList();
                    RankList(sortedRows, x => x.WeightedScore, (x, rank) => x.Rank = rank);
                    
                    rData.CriteriaDetails[crit.Id] = sortedRows;

                    // Store Winner(s) - Supports Ties (e.g. Rank 1.5)
                    var winnersRows = new List<CriteriaTableDetailRow>();
                    if (sortedRows.Any())
                    {
                        decimal minRank = sortedRows.Min(x => x.Rank);
                        winnersRows = sortedRows.Where(x => x.Rank == minRank).ToList();
                    }
                    
                    var winnersList = new List<ContestantScoreSummary>();
                    
                    foreach(var winnerRow in winnersRows)
                    {
                        var cWinner = rData.Participants.FirstOrDefault(p => p.Id == winnerRow.ContestantId);
                        winnersList.Add(new ContestantScoreSummary
                        {
                            ContestantId = winnerRow.ContestantId,
                            Name = winnerRow.Name,
                            Organization = cWinner?.Organization ?? "",
                            Score = winnerRow.WeightedScore
                        });
                    }
                    
                    if (winnersList.Any())
                    {
                        rData.CriteriaWinners[crit.Id] = winnersList;
                    }
                }

                data.Rounds.Add(rData);
            }

            return data;
        }

        // UPDATED CONSENSUS LOGIC
        private List<ContestantScoreSummary> CalculateRankSumScores(List<Contestant> participants, List<Criteria> criteria, List<ComputedRoundScore> criteriaComputedScores)
        {
            var result = new List<ContestantScoreSummary>();

            // Initialize Summaries
            foreach(var p in participants)
            {
                result.Add(new ContestantScoreSummary
                {
                    ContestantId = p.Id,
                    Name = p.Name,
                    Organization = p.Organization ?? "",
                    Score = 0, // Will hold Rank Sum
                    CriteriaBreakdown = new Dictionary<int, decimal>() // Will hold Ranks
                });
            }

            // Loop Criteria and Rank Participants
            foreach(var crit in criteria)
            {
                // Get Scores for this Criteria
                var scores = new List<(int ContestantId, decimal Score)>();
                foreach(var p in participants)
                {
                    var s = criteriaComputedScores.FirstOrDefault(x => x.ContestantId == p.Id && x.CriteriaId == crit.Id);
                    scores.Add((p.Id, s?.Score ?? 0));
                }

                // Sort Descending (Higher Score = Better)
                var sorted = scores.OrderByDescending(x => x.Score).ToList();

                // Assign Ranks
                for (int i = 0; i < sorted.Count; )
                {
                    int j = i;
                    while (j < sorted.Count - 1 && sorted[j + 1].Score == sorted[i].Score) j++;
                    
                    decimal avgRank = (decimal)(i + 1 + j + 1) / 2.0m;
                    
                    for (int k = i; k <= j; k++)
                    {
                        // Assign this rank to the Contestant
                        var summary = result.FirstOrDefault(r => r.ContestantId == sorted[k].ContestantId);
                        if (summary != null)
                        {
                            summary.CriteriaBreakdown[crit.Id] = avgRank;
                        }
                    }
                    i = j + 1;
                }
            }

            // Sum Ranks
            foreach(var res in result)
            {
                decimal rankSum = 0;
                foreach(var crit in criteria)
                {
                    if (res.CriteriaBreakdown.TryGetValue(crit.Id, out var r))
                        rankSum += r;
                }
                res.Score = rankSum;
            }

            // Final Ranking (Lower Rank Sum is Better)
            var sortedFinal = result.OrderBy(x => x.Score).ToList();
            RankList(sortedFinal, x => -x.Score, (x, rank) => x.Rank = rank); // -x.Score because Helper does Descending

            return result;
        }

        // Generic Rank Helper
        private void RankList<T>(List<T> items, Func<T, decimal> valueSelector, Action<T, decimal> rankSetter)
        {
            for (int i = 0; i < items.Count; )
            {
                int j = i;
                while (j < items.Count - 1 && valueSelector(items[j + 1]) == valueSelector(items[i])) j++;
                
                decimal avgRank = (decimal)(i + 1 + j + 1) / 2.0m;
                for (int k = i; k <= j; k++) rankSetter(items[k], avgRank);
                i = j + 1;
            }
        }
    }
}
