using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MvcSample.Hubs;

namespace MvcSample.Controllers
{
    public class MessagesController : Controller
    {
        private NotificationsMessageHub NotificationsMessageHub { get; set; }

        public MessagesController(NotificationsMessageHub notificationsMessageHub)
        {
            NotificationsMessageHub = notificationsMessageHub;
        }

        [HttpGet]
        public async Task SendMessage([FromQueryAttribute]string message)
        {
            await NotificationsMessageHub.Clients.All.InvokeAsync("receiveMessage", message);
        }
    }
}