using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace ProjectTallify.Hubs
{
    public class NotificationHub : Hub
    {
        // This hub can be used for real-time communication.
        // We can map users to connections here if needed, 
        // or rely on Groups.

        public override async Task OnConnectedAsync()
        {
            var httpContext = Context.GetHttpContext();
            if (httpContext != null)
            {
                // If we have a logged-in user in session
                var userId = httpContext.Session.GetInt32("UserId");
                if (userId.HasValue)
                {
                    // Add to a group named "User_{Id}"
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId.Value}");
                }
                
                // Also support "Event_{Id}" groups for judges/scorers if needed
                var eventId = httpContext.Session.GetInt32("EventId");
                if (eventId.HasValue)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"Event_{eventId.Value}");
                }
            }
            
            await base.OnConnectedAsync();
        }
    }
}
