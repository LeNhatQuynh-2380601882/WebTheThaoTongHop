using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;
using TamThaiTuSport.Data;
using TamThaiTuSport.Models.Domain;

namespace TamThaiTuSport.Hubs
{
    public class ChatHub : Hub
    {
        private readonly AppDbContext _db;

        public ChatHub(AppDbContext db)
        {
            _db = db;
        }

        // Customer joins their own chat room
        public async Task JoinRoom(string roomId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        }

        // Admin joins the general Admin notification group
        public async Task JoinAdminGroup()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
        }

        // Admin joins a specific customer room
        public async Task JoinCustomerRoom(string roomId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
        }

        // Send a message
        public async Task SendMessage(string roomId, string senderName, string message, bool isAdmin)
        {
            var msg = new ChatMessage
            {
                RoomId = roomId,
                SenderName = senderName,
                Message = message,
                IsAdminSender = isAdmin,
                CreatedAt = DateTime.UtcNow
            };

            // Associate SenderId if user is authenticated
            if (Context.User?.Identity?.IsAuthenticated == true)
            {
                // Try to get UserId from Claims
                var userIdClaim = Context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim != null)
                {
                    msg.SenderId = userIdClaim.Value;
                }
            }

            _db.ChatMessages.Add(msg);
            await _db.SaveChangesAsync();

            // Broadcast message to everyone in the room (the customer and any admins viewing the room)
            await Clients.Group(roomId).SendAsync("ReceiveMessage", roomId, senderName, message, msg.CreatedAt.ToString("o"), isAdmin);

            // If it's a customer sending the message, notify the Admin group
            if (!isAdmin)
            {
                await Clients.Group("Admins").SendAsync("NotifyNewMessage", roomId, senderName, message);
            }
        }
    }
}
