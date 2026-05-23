using ProjectTallify.Models;

namespace ProjectTallify.Services
{
    public interface IReportService
    {
        Task<byte[]> GeneratePdfReportAsync(int eventId, List<string> reportTypes);
        Task<Dictionary<string, byte[]>> GenerateReportsAsync(int eventId, List<string> reportTypes);
        Task<byte[]> GenerateJudgeScoreSheetsAsync(int eventId, int roundId);
    }
}
