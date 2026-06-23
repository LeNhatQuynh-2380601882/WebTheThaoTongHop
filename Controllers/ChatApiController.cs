using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using TamThaiTuSport.Data;
using TamThaiTuSport.Models.Domain;

namespace TamThaiTuSport.Controllers
{
    [ApiController]
    [Route("api/chat")]
    public class ChatApiController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ChatApiController(AppDbContext db)
        {
            _db = db;
        }

        // GET /api/chat/history?roomId=...
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory(string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId))
                return BadRequest("RoomId is required");

            var messages = await _db.ChatMessages
                .Where(m => m.RoomId == roomId)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new
                {
                    m.Id,
                    m.RoomId,
                    m.SenderName,
                    m.Message,
                    CreatedAt = m.CreatedAt.ToString("o"),
                    m.IsAdminSender
                })
                .ToListAsync();

            return Ok(messages);
        }

        // GET /api/chat/rooms (Admin & Staff only)
        [HttpGet("rooms")]
        [Authorize(Roles = "Admin,NhanVien")]
        public async Task<IActionResult> GetActiveRooms()
        {
            // Group messages by RoomId and find the last message details for each room
            var messages = await _db.ChatMessages.ToListAsync();
            
            var rooms = messages
                .GroupBy(m => m.RoomId)
                .Select(g => new
                {
                    RoomId = g.Key,
                    LastMessage = g.OrderByDescending(m => m.CreatedAt).FirstOrDefault()
                })
                .Where(r => r.LastMessage != null)
                .OrderByDescending(r => r.LastMessage!.CreatedAt)
                .Select(r => new
                {
                    r.RoomId,
                    LastMessageText = r.LastMessage!.Message,
                    SenderName = r.LastMessage.SenderName,
                    IsAdminSender = r.LastMessage.IsAdminSender,
                    CreatedAt = r.LastMessage.CreatedAt.ToString("o")
                })
                .ToList();

            return Ok(rooms);
        }
    }
}
