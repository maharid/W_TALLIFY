using ProjectTallify.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProjectTallify.Services
{
    public interface IScoringService
    {
        Task<bool> ProcessJudgeRoundSubmission(int eventId, int judgeId, int roundId, List<ContestantScoreSubmission> submissions);
        Task<RoundTallyReport> ComputeRoundTally(int eventId, int roundId);
    }
}
