using Microsoft.EntityFrameworkCore;
using ProjectTallify.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

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

        public async Task<Dictionary<string, byte[]>> GenerateReportsAsync(int eventId, List<string> reportTypes)
        {
            var reports = new Dictionary<string, byte[]>();
            var reportData = await FetchAndPrepareDataAsync(eventId);

            // 1. Master Sheet (Renamed from Consolidated)
            if (reportTypes.Contains("overall"))
            {
                var doc = Document.Create(container => {
                    container.Page(page => {
                        page.Margin(1, Unit.Centimetre);
                        page.Size(PageSizes.A4.Landscape());
                        ComposeHeader(page.Header(), reportData.Event, "MASTER SHEET");
                        page.Content().PaddingVertical(10).Column(col => {
                            foreach (var round in reportData.Rounds) {
                                col.Item().Background(Colors.Grey.Lighten3).Padding(5).Text($"ROUND: {round.RoundName.ToUpper()}").Bold();
                                ComposeTallyTable(col, round);
                                col.Item().PaddingBottom(20);
                            }
                        });
                        ComposeFooter(page.Footer());
                    });
                });
                reports.Add("Master Sheet.pdf", doc.GeneratePdf());
            }

            // 2. Result per Round
            if (reportTypes.Contains("resultperround"))
            {
                var doc = Document.Create(container => {
                    container.Page(page => {
                        page.Margin(1, Unit.Centimetre);
                        page.Size(PageSizes.A4);
                        ComposeHeader(page.Header(), reportData.Event, "RESULT PER ROUND");
                        page.Content().PaddingVertical(10).Column(col => {
                            foreach (var round in reportData.Rounds) {
                                col.Item().Background(Colors.Grey.Lighten3).Padding(5).Text($"ROUND: {round.RoundName.ToUpper()}").Bold();
                                ComposeTallyTable(col, round);
                                col.Item().PaddingBottom(20);
                            }
                        });
                        ComposeFooter(page.Footer());
                    });
                });
                reports.Add("Result per Round.pdf", doc.GeneratePdf());
            }

            // 3. List of Winners
            if (reportTypes.Contains("winners"))
            {
                var doc = Document.Create(container => {
                    container.Page(page => {
                        page.Margin(1, Unit.Centimetre);
                        page.Size(PageSizes.A4);
                        ComposeHeader(page.Header(), reportData.Event, "OFFICIAL LIST OF WINNERS");
                        page.Content().PaddingVertical(10).Column(col => {
                            foreach (var round in reportData.Rounds) {
                                ComposeWinnersSection(col, round);
                                col.Item().PaddingBottom(20);
                            }
                        });
                        ComposeFooter(page.Footer());
                    });
                });
                reports.Add("List of Winners.pdf", doc.GeneratePdf());
            }

            // 4. Judge's Audit Cards (Combined for all judges)
            if (reportTypes.Contains("judges"))
            {
                var doc = Document.Create(container => {
                    foreach (var judge in reportData.Judges) {
                        container.Page(page => {
                            page.Margin(1, Unit.Centimetre);
                            page.Size(PageSizes.A4);
                            ComposeHeader(page.Header(), reportData.Event, "JUDGE'S SCORECARD");
                            page.Content().PaddingVertical(10).Column(col => {
                                col.Item().Text($"JUDGE: {judge.Name.ToUpper()}").Bold().Underline();
                                foreach (var round in reportData.Rounds) {
                                    ComposeJudgeRoundTable(col, round, judge.Id);
                                    col.Item().PaddingBottom(15);
                                }
                            });
                            ComposeFooter(page.Footer());
                        });
                    }
                });
                reports.Add("Judges Audit Cards.pdf", doc.GeneratePdf());
            }

            // 5. Tie-Breaking Audit
            if (reportTypes.Contains("tiebreak"))
            {
                var doc = Document.Create(container => {
                    container.Page(page => {
                        page.Margin(1, Unit.Centimetre); page.Size(PageSizes.A4);
                        ComposeHeader(page.Header(), reportData.Event, "TIE-BREAKING AUDIT");
                        page.Content().PaddingVertical(10).Column(col => {
                            col.Item().PaddingBottom(10).Text("Evidence and resolution steps for all ties.").Italic();
                            var logs = _db.AuditLogs.Where(a => a.EventId == eventId && a.Action.Contains("Tie-Break")).ToList();
                            foreach (var log in logs) {
                                col.Item().Border(0.5f).Padding(5).Column(c => {
                                    c.Item().Text(log.Action).Bold();
                                    c.Item().Text(log.Details).FontSize(9);
                                });
                                col.Item().PaddingBottom(5);
                            }
                        });
                        ComposeFooter(page.Footer());
                    });
                });
                reports.Add("Tie-Breaking Audit.pdf", doc.GeneratePdf());
            }

            // 6. Dynamic Round Judge Sheets (Informational)
            foreach (var type in reportTypes.Where(t => t.StartsWith("judgesheet-")))
            {
                if (int.TryParse(type.Split('-').Last(), out int roundId))
                {
                    var round = reportData.Rounds.FirstOrDefault(r => r.RoundId == roundId);
                    if (round != null)
                    {
                        var bytes = await GenerateJudgeScoreSheetsAsync(eventId, roundId);
                        reports.Add($"{round.RoundName}-JudgeScoreSheet.pdf", bytes);
                    }
                }
            }

            return reports;
        }

        public async Task<byte[]> GeneratePdfReportAsync(int eventId, List<string> reportTypes)
        {
            var reportData = await FetchAndPrepareDataAsync(eventId);

            var document = Document.Create(container =>
            {
                // Simple combined document for previews
                if (reportTypes.Contains("overall") || reportTypes.Contains("resultperround"))
                {
                    container.Page(page => {
                        page.Margin(1, Unit.Centimetre);
                        page.Size(PageSizes.A4.Landscape());
                        ComposeHeader(page.Header(), reportData.Event, "CONSOLIDATED REPORT");
                        page.Content().PaddingVertical(10).Column(col => {
                            foreach (var round in reportData.Rounds) {
                                col.Item().Background(Colors.Grey.Lighten3).Padding(5).Text($"ROUND: {round.RoundName.ToUpper()}").Bold();
                                ComposeTallyTable(col, round);
                                col.Item().PaddingBottom(20);
                            }
                        });
                        ComposeFooter(page.Footer());
                    });
                }
                
                if (reportTypes.Contains("winners"))
                {
                    container.Page(page => {
                        page.Margin(1, Unit.Centimetre);
                        page.Size(PageSizes.A4);
                        ComposeHeader(page.Header(), reportData.Event, "LIST OF WINNERS");
                        page.Content().PaddingVertical(10).Column(col => {
                            foreach (var round in reportData.Rounds) {
                                ComposeWinnersSection(col, round);
                                col.Item().PaddingBottom(20);
                            }
                        });
                        ComposeFooter(page.Footer());
                    });
                }

                if (reportTypes.Contains("tiebreak"))
                {
                    container.Page(page => {
                        page.Margin(1, Unit.Centimetre); page.Size(PageSizes.A4);
                        ComposeHeader(page.Header(), reportData.Event, "TIE-BREAKING AUDIT");
                        page.Content().PaddingVertical(10).Column(col => {
                            var logs = _db.AuditLogs.Where(a => a.EventId == eventId && a.Action.Contains("Tie-Break")).ToList();
                            foreach (var log in logs) {
                                col.Item().Border(0.5f).Padding(5).Column(c => {
                                    c.Item().Text(log.Action).Bold();
                                    c.Item().Text(log.Details).FontSize(9);
                                });
                                col.Item().PaddingBottom(5);
                            }
                        });
                        ComposeFooter(page.Footer());
                    });
                }
            });

            return document.GeneratePdf();
        }

        public async Task<byte[]> GenerateJudgeScoreSheetsAsync(int eventId, int roundId)
        {
            var reportData = await FetchAndPrepareDataAsync(eventId);
            var round = reportData.Rounds.FirstOrDefault(r => r.RoundId == roundId);
            if (round == null) return Array.Empty<byte>();

            var document = Document.Create(container =>
            {
                foreach (var judge in reportData.Judges)
                {
                    container.Page(page =>
                    {
                        page.Margin(1.5f, Unit.Centimetre);
                        page.Size(PageSizes.A4);
                        page.DefaultTextStyle(x => x.FontSize(11));

                        ComposeHeader(page.Header(), reportData.Event, "OFFICIAL SCORE SHEET");

                        page.Content().PaddingVertical(20).Column(col =>
                        {
                            col.Item().Row(row =>
                            {
                                row.RelativeItem().Text(x =>
                                {
                                    x.Span("ROUND: ").Bold();
                                    x.Span(round.RoundName.ToUpper());
                                });
                                row.RelativeItem().AlignRight().Text(x =>
                                {
                                    x.Span("JUDGE: ").Bold();
                                    x.Span(judge.Name.ToUpper());
                                });
                            });

                            col.Item().PaddingTop(10).PaddingBottom(20).LineHorizontal(0.5f);

                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(40); // Code
                                    columns.RelativeColumn();   // Name
                                    foreach (var crit in round.Criteria)
                                    {
                                        columns.RelativeColumn();
                                    }
                                    columns.RelativeColumn(); // Total
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(BlockHeader).Text("Code");
                                    header.Cell().Element(BlockHeader).Text("Contestant");
                                    foreach (var crit in round.Criteria)
                                    {
                                        string weightText = (crit.WeightPercent % 1 == 0) ? crit.WeightPercent.ToString("0") : crit.WeightPercent.ToString("0.##");
                                        header.Cell().Element(BlockHeader).AlignCenter().Text($"{crit.Name}\n({weightText}%)");
                                    }
                                    header.Cell().Element(BlockHeader).AlignRight().Text("TOTAL");
                                });

                                foreach (var pRow in round.Participants)
                                {
                                    table.Cell().Element(BlockCell).AlignCenter().Text(pRow.Code);
                                    table.Cell().Element(BlockCell).Text(pRow.Name);

                                    decimal judgeTotal = 0;
                                    foreach (var crit in round.Criteria)
                                    {
                                        decimal score = 0;
                                        if (round.RawScores.TryGetValue(judge.Id, out var judgeScores) && 
                                            judgeScores.TryGetValue(pRow.Id, out var cScores))
                                        {
                                            cScores.TryGetValue(crit.Id, out score);
                                        }

                                        table.Cell().Element(BlockCell).AlignCenter().Text(score.ToString("F1"));
                                        
                                        if (crit.IsDerived) judgeTotal += score;
                                        else judgeTotal += score * (crit.WeightPercent / 100M);
                                    }

                                    table.Cell().Element(BlockCell).AlignRight().Text(judgeTotal.ToString("F2")).Bold();
                                }
                            });

                            col.Item().PaddingTop(60).Column(sigCol =>
                            {
                                sigCol.Item().Text("I hereby certify that the scores provided above are true and correct to the best of my knowledge.").FontSize(10).AlignCenter();
                                sigCol.Item().PaddingTop(10).Text("Reviewed and Approved by:").FontSize(10).AlignCenter();
                                
                                sigCol.Item().PaddingTop(40).AlignCenter().Width(250).Column(inner =>
                                {
                                    inner.Item().LineHorizontal(1).LineColor(Colors.Black);
                                    inner.Item().PaddingTop(5).Text(judge.Name.ToUpper()).Bold().FontSize(12).AlignCenter();
                                });
                            });
                        });
                        
                        ComposeFooter(page.Footer());
                    });
                }
            });

            return document.GeneratePdf();
        }

        private void ComposeHeader(IContainer container, Event ev, string reportTitle)
        {
            container.Column(col =>
            {
                col.Item().Text(ev.Name).FontSize(20).Bold().AlignCenter();
                if (!string.IsNullOrWhiteSpace(ev.Venue)) col.Item().Text(ev.Venue).FontSize(10).AlignCenter();
                col.Item().Text(ev.Schedule.ToString("MMMM dd, yyyy h:mm tt")).FontSize(10).AlignCenter();
                
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
                    row.RelativeItem().Text($"Generated via Tallify | {DateTime.Now:g}");
                    row.RelativeItem().AlignRight().Text(x => {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" of ");
                        x.TotalPages();
                    });
                });
            });
        }

        private void ComposeTallyTable(ColumnDescriptor col, ReportRoundData round)
        {
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(30);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(2);
                    foreach(var c in round.Criteria) columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                table.Header(header =>
                {
                    header.Cell().Element(BlockHeader).Text("Rank");
                    header.Cell().Element(BlockHeader).Text("Organization");
                    header.Cell().Element(BlockHeader).Text("Contestant");
                    foreach(var c in round.Criteria) header.Cell().Element(BlockHeader).AlignRight().Text(c.Name).FontSize(7);
                    header.Cell().Element(BlockHeader).AlignRight().Text("Score");
                });

                foreach(var item in round.OverallScores.OrderBy(x => x.Rank))
                {
                    table.Cell().Element(BlockCell).Text(item.Rank.ToString("0.##")).Bold();
                    table.Cell().Element(BlockCell).Text(item.Name);
                    table.Cell().Element(BlockCell).Text(item.Organization);
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

        private void ComposeWinnersSection(ColumnDescriptor col, ReportRoundData round)
        {
            col.Item().Background(Colors.Grey.Lighten3).Padding(5).Text(round.RoundName.ToUpper()).Bold().FontSize(14);
            col.Item().PaddingBottom(5);

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(1);
                });

                table.Header(header =>
                {
                    header.Cell().Element(BlockHeader).Text("Award");
                    header.Cell().Element(BlockHeader).Text("Organization");
                    header.Cell().Element(BlockHeader).Text("Winner Name");
                    header.Cell().Element(BlockHeader).AlignRight().Text("Score");
                });

                if (round.RoundWinners.Any())
                {
                    foreach(var winner in round.RoundWinners)
                    {
                        table.Cell().Element(BlockCell).Text("ROUND CHAMPION").Bold();
                        table.Cell().Element(BlockCell).Text(winner.Organization);
                        table.Cell().Element(BlockCell).Text(winner.Name).Bold();
                        table.Cell().Element(BlockCell).AlignRight().Text(winner.Score.ToString("F2"));
                    }
                }
            });
        }

        private void ComposeJudgeRoundTable(ColumnDescriptor col, ReportRoundData round, int judgeId)
        {
            col.Item().Text($"Round: {round.RoundName}").Bold();
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(3);
                    foreach(var c in round.Criteria) columns.RelativeColumn();
                });

                table.Header(header =>
                {
                    header.Cell().BorderBottom(1).Text("Organization").Bold();
                    header.Cell().BorderBottom(1).Text("Contestant").Bold();
                    foreach(var c in round.Criteria) header.Cell().BorderBottom(1).AlignCenter().Text(c.Name).Bold().FontSize(8);
                });

                foreach(var contestant in round.Participants)
                {
                    table.Cell().Padding(2).Text(contestant.Name);
                    table.Cell().Padding(2).Text(contestant.Organization);
                    foreach(var c in round.Criteria)
                    {
                        string val = "-";
                        if (round.RawScores.TryGetValue(judgeId, out var jScores) && jScores.TryGetValue(contestant.Id, out var cScores) && cScores.TryGetValue(c.Id, out var score))
                            val = score.ToString("0.##");
                        table.Cell().Padding(2).AlignCenter().Text(val);
                    }
                }
            });
        }

        static IContainer BlockHeader(IContainer container) => container.BorderBottom(1).Background(Colors.Grey.Lighten4).Padding(2).DefaultTextStyle(x => x.Bold().FontSize(8));
        static IContainer BlockCell(IContainer container) => container.BorderBottom(1).BorderColor(Colors.Grey.Lighten4).Padding(2).DefaultTextStyle(x => x.FontSize(8));

        private async Task<ReportData> FetchAndPrepareDataAsync(int eventId)
        {
            var ev = await _db.Events.Include(e => e.Contestants).Include(e => e.Rounds).ThenInclude(r => r.Criterias).FirstOrDefaultAsync(e => e.Id == eventId);
            var judges = await _db.Judges.Where(j => j.EventId == eventId).OrderBy(j => j.Id).ToListAsync();
            var scores = await _db.Scores.Where(s => s.EventId == eventId).ToListAsync();
            var computedScores = await _db.ComputedRoundScores.Where(s => s.EventId == eventId).ToListAsync();

            var data = new ReportData { Event = ev, Judges = judges, Contestants = ev.Contestants.ToList(), Rounds = new List<ReportRoundData>() };

            foreach (var r in ev.Rounds.OrderBy(x => x.Order))
            {
                var rData = new ReportRoundData { RoundId = r.Id, RoundName = r.Name, Order = r.Order, Criteria = r.Criterias.ToList() };
                var pIds = scores.Where(s => s.RoundId == r.Id).Select(s => s.ContestantId).Distinct().ToList();
                rData.Participants = data.Contestants.Where(c => pIds.Contains(c.Id)).ToList();
                
                foreach (var j in judges)
                {
                    rData.RawScores[j.Id] = new Dictionary<int, Dictionary<int, decimal>>();
                    foreach(var c in rData.Participants) rData.RawScores[j.Id][c.Id] = new Dictionary<int, decimal>();
                }
                foreach (var s in scores.Where(s => s.RoundId == r.Id))
                {
                    if (s.JudgeId == null || !rData.RawScores.ContainsKey(s.JudgeId.Value)) continue;
                    if (!rData.RawScores[s.JudgeId.Value].ContainsKey(s.ContestantId)) rData.RawScores[s.JudgeId.Value][s.ContestantId] = new Dictionary<int, decimal>();
                    rData.RawScores[s.JudgeId.Value][s.ContestantId][s.CriteriaId] = s.Value;
                }

                foreach(var os in computedScores.Where(x => x.RoundId == r.Id && x.CriteriaId == null))
                {
                    var c = data.Contestants.FirstOrDefault(x => x.Id == os.ContestantId);
                    var summary = new ContestantScoreSummary { ContestantId = os.ContestantId, Name = c?.Name ?? "", Organization = c?.Organization ?? "", Score = os.Score, Rank = os.Rank };
                    foreach(var crit in rData.Criteria)
                    {
                        var critScore = computedScores.FirstOrDefault(x => x.ContestantId == os.ContestantId && x.CriteriaId == crit.Id && x.RoundId == r.Id);
                        if (critScore != null) summary.CriteriaBreakdown[crit.Id] = critScore.Score;
                    }
                    rData.OverallScores.Add(summary);
                }
                if (rData.OverallScores.Any())
                {
                    decimal minRank = rData.OverallScores.Min(x => x.Rank);
                    rData.RoundWinners = rData.OverallScores.Where(x => x.Rank == minRank).ToList();
                }
                data.Rounds.Add(rData);
            }
            return data;
        }
    }

    public class ReportData { public Event Event { get; set; } public List<Judge> Judges { get; set; } public List<Contestant> Contestants { get; set; } public List<ReportRoundData> Rounds { get; set; } }
    public class ReportRoundData { public int RoundId { get; set; } public string RoundName { get; set; } public int Order { get; set; } public List<Criteria> Criteria { get; set; } public List<Contestant> Participants { get; set; } public Dictionary<int, Dictionary<int, Dictionary<int, decimal>>> RawScores { get; set; } = new(); public List<ContestantScoreSummary> OverallScores { get; set; } = new(); public List<ContestantScoreSummary> RankSumScores { get; set; } = new(); public Dictionary<int, List<CriteriaTableDetailRow>> CriteriaDetails { get; set; } = new(); public List<ContestantScoreSummary> RoundWinners { get; set; } = new(); public Dictionary<int, List<ContestantScoreSummary>> CriteriaWinners { get; set; } = new(); }
    public class ContestantScoreSummary { public int ContestantId { get; set; } public string Name { get; set; } public string Organization { get; set; } public decimal Score { get; set; } public decimal Rank { get; set; } public Dictionary<int, decimal> CriteriaBreakdown { get; set; } = new(); }
    public class CriteriaTableDetailRow { public int ContestantId { get; set; } public string Name { get; set; } public string Organization { get; set; } public decimal AverageScore { get; set; } public decimal WeightedScore { get; set; } public decimal Rank { get; set; } public Dictionary<int, decimal> JudgeRawScores { get; set; } = new(); }
}
