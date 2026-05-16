using System.Threading.Tasks;
using ProjectTallify.Models;

namespace ProjectTallify.Services
{
    public interface IEmailSender
    {
        Task SendEmailConfirmationAsync(User user, string confirmationLink);
        Task SendPasswordResetAsync(User user, string resetLink);
        Task SendJudgeInvitationAsync(Judge judge, string inviteLink, string eventName, string eventCode);
        Task SendJudgeVerificationLinkAsync(Judge judge, string verificationLink, string eventName);
    }
}
