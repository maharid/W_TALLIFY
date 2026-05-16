using ProjectTallify.Models;

namespace ProjectTallify.Services
{
    public interface IReportService
    {
        Task<byte[]> GeneratePdfReportAsync(int eventId, List<string> reportTypes);
    }
}
