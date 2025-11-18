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
    internal sealed class DeviceEntity: ITableEntity
    {
        public string PartitionKey { get; set; } = default!;
        public string RowKey { get; set; } = default!;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public string DeviceName { get; set; }
        public string DeviceId { get; set; }
        public DateTimeOffset CreatedUtc { get; set; }
        public string KeyAlgorithm { get; set; }
        public string Status { get; set; }
        public string KeyId { get; set; }
        public string PublicKeyJwk { get; set; }
        public string LastEnrollmentNonce { get; set; }
    }
}