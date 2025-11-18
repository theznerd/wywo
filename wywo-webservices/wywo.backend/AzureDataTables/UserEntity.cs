using Azure;
using Azure.Data.Tables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using wywo.shared.Models;

namespace wywo.backend.AzureDataTables
{
    internal sealed class UserEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = default!;
        public string RowKey { get; set; } = default!;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public required string Id { get; set; }
        public required string Email { get; set; }
        public string? DisplayName { get; set; }
        public string? AvatarUrl { get; set; }
        public string? LoginsJson { get; set; }

        // This property is not mapped to a table column directly.
        // It serializes/deserializes the LoginsJson property.
        [IgnoreDataMember]
        public List<UserLogin> Logins
        {
            get => string.IsNullOrEmpty(LoginsJson)
                ? new List<UserLogin>()
                : System.Text.Json.JsonSerializer.Deserialize<List<UserLogin>>(LoginsJson)!;
            set => LoginsJson = System.Text.Json.JsonSerializer.Serialize(value);
        }
    }
}
