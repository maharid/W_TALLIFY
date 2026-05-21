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
                // Identification via Claims
                var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
                var roleClaim = user.FindFirst(ClaimTypes.Role);

                if (userIdClaim != null && roleClaim != null)
                {
                    if (roleClaim.Value == "Organizer")
                    {
                        // Group: Organizer_123
                        await Groups.AddToGroupAsync(Context.ConnectionId, $"Organizer_{userIdClaim.Value}");
                    }
                    else if (roleClaim.Value == "Judge")
                    {
                        // Group: Judge_456
                        await Groups.AddToGroupAsync(Context.ConnectionId, userIdClaim.Value);
                    }
                }

                // Event Context (everyone watching an event)
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
