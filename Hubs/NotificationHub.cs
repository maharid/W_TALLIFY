using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using System.Security.Claims;
using System.Linq;

namespace ProjectTallify.Hubs
{
    public class NotificationHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var user = Context.User;
            if (user != null && user.Identity != null && user.Identity.IsAuthenticated)
            {
                // Identification via Claims (modern way)
                var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null)
                {
                    // For Organizers: "User_123"
                    // For Judges:    "User_Judge_456"
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userIdClaim.Value}");
                }

                // Event Context
                var eventIdClaim = user.FindFirst("EventId");
                if (eventIdClaim != null)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"Event_{eventIdClaim.Value}");
                }
            }
            
            await base.OnConnectedAsync();
        }
    }
}
