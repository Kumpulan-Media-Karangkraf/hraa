using Microsoft.AspNetCore.SignalR;

namespace HRAnalysis.Services
{
    
    public class ProgressHub : Hub
    {
        public async Task SendProgress(string connectionId, int percentage, string message)
        {
            await Clients.Client(connectionId).SendAsync("ReceiveProgress", percentage, message);
        }
    }


}
