using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wywo.shared.Models
{
    public class ChatRoom
    {
        public string Id { get; init; } = default!;
        public string Name { get; init; } = default!;
        public List<ChatMessage> Messages { get; init; } = new();
    }

    public record ChatMessage(
        long Id,
        string? SenderId,
        string Content,
        DateTimeOffset Timestamp
    );
}
