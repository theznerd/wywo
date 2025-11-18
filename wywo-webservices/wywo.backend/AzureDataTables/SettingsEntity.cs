using Azure;
using Azure.Data.Tables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using wywo.shared.DTOs;

namespace wywo.backend.AzureDataTables
{
    internal sealed class SettingsEntity: ITableEntity
    {
        public string PartitionKey { get; set; } = default!;
        public string RowKey { get; set; } = default!;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public string? DataJson { get; set; }

        [IgnoreDataMember]
        public AppSettingsDto Settings
        {
            get => string.IsNullOrEmpty(DataJson)
                ? new AppSettingsDto(string.Empty)
                : System.Text.Json.JsonSerializer.Deserialize<AppSettingsDto>(DataJson)!;
            set
            {
                DataJson = System.Text.Json.JsonSerializer.Serialize(value);
            }
        }
    }
}
