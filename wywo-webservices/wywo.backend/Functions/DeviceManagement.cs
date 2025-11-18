using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using wywo.backend.AzureDataTables;
using wywo.shared.DTOs;

namespace wywo.backend.Functions;

public class DeviceManagement
{
    private const string DevicesTable = "Devices";
    private const string AckSigningKeyEnv = "DeviceEnrollmentAckSigningKey"; // HS256 secret (base64 or plain)

    private readonly ILogger<DeviceManagement> _logger;

    public DeviceManagement(ILogger<DeviceManagement> logger) => _logger = logger;

    // Enrollment: store device public key and return short-lived backend ACK (for device to exit config mode).
    [Function("RegisterDeviceKey")]
    public async Task<HttpResponseData> RegisterDeviceKey(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "devices/register")] HttpRequestData req)
    {
        string body = await new StreamReader(req.Body).ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            var bad = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Request body is empty.");
            return bad;
        }

        DeviceEnrollmentDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<DeviceEnrollmentDto>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException)
        {
            var bad = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Invalid JSON payload.");
            return bad;
        }

        if (dto is null ||
            dto.V <= 0 ||
            string.IsNullOrWhiteSpace(dto.DeviceId) ||
            string.IsNullOrWhiteSpace(dto.Algo) ||
            string.IsNullOrWhiteSpace(dto.Jwk) ||
            string.IsNullOrWhiteSpace(dto.Nonce))
        {
            var bad = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Missing or invalid required fields.");
            return bad;
        }

        var alg = dto.Algo.Trim().ToUpperInvariant();
        if (alg is not "ES256") // keep tight initially; extend to EdDSA later if needed
        {
            var bad = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Unsupported algorithm. Use ES256.");
            return bad;
        }

        // Basic JWK sanity check for ES256
        try
        {
            using var jwkDoc = JsonDocument.Parse(dto.Jwk);
            var root = jwkDoc.RootElement;
            if (!root.TryGetProperty("kty", out var kty) || kty.GetString() != "EC" ||
                !root.TryGetProperty("crv", out var crv) || crv.GetString() != "P-256" ||
                !root.TryGetProperty("x", out _) || !root.TryGetProperty("y", out _))
            {
                throw new InvalidOperationException("JWK must contain EC P-256 public key with x,y.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid JWK.");
            var bad = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Invalid JWK payload for ES256.");
            return bad;
        }

        var storage = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        if (string.IsNullOrWhiteSpace(storage))
        {
            var error = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            _logger.LogError("AzureWebJobsStorage is not configured.");
            await error.WriteStringAsync("Storage configuration error.");
            return error;
        }

        var ackKey = Environment.GetEnvironmentVariable(AckSigningKeyEnv);
        if (string.IsNullOrEmpty(ackKey))
        {
            var error = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            _logger.LogError("{EnvVar} is not configured.", AckSigningKeyEnv);
            await error.WriteStringAsync("Backend ack signing key not configured.");
            return error;
        }

        var service = new TableServiceClient(storage);
        var devices = service.GetTableClient(DevicesTable);
        await devices.CreateIfNotExistsAsync();

        var partitionKey = "devices";
        var rowKey = dto.DeviceId.Trim();

        DeviceEntity entity;
        bool exists = false;
        try
        {
            var maybe = await devices.GetEntityIfExistsAsync<DeviceEntity>(partitionKey, rowKey);
            if (maybe.HasValue)
            {
                entity = maybe.Value;
                exists = true;
            }
            else
            {
                entity = new DeviceEntity
                {
                    PartitionKey = partitionKey,
                    RowKey = rowKey,
                    DeviceId = rowKey,
                    CreatedUtc = DateTimeOffset.UtcNow
                };
            }
        }
        catch (RequestFailedException)
        {
            entity = new DeviceEntity
            {
                PartitionKey = partitionKey,
                RowKey = rowKey,
                DeviceId = rowKey,
                CreatedUtc = DateTimeOffset.UtcNow
            };
        }

        entity.DeviceName = dto.DeviceName ?? entity.DeviceName;
        entity.Status = "Active";
        entity.KeyAlgorithm = alg;
        entity.KeyId = dto.Kid ?? entity.KeyId ?? "k1";
        entity.PublicKeyJwk = dto.Jwk;
        entity.LastEnrollmentNonce = dto.Nonce;

        await devices.UpsertEntityAsync(entity, TableUpdateMode.Replace);

        // Build short-lived backend ACK JWT echoing the nonce (so device can exit config mode)
        var now = DateTimeOffset.UtcNow;
        var exp = now.AddMinutes(10);
        var ackJwt = BuildHs256Jwt(
            issuer: "wywo-backend",
            subject: $"device:{entity.DeviceId}",
            audience: "wywo-device", // fixed audience for devices, could be used for tenant in future?
            expires: exp,
            issuedAt: now,
            signingKey: ackKey,
            kid: "ack-v1",
            ("nonce", dto.Nonce),
            ("alg", alg),
            ("deviceId", entity.DeviceId),
            ("tenantId", partitionKey)
        );

        var result = new DeviceEnrollmentResultDto(entity.DeviceId, ackJwt, exp);
        var status = exists ? System.Net.HttpStatusCode.OK : System.Net.HttpStatusCode.Created;

        var resp = req.CreateResponse(status);
        resp.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await resp.WriteStringAsync(JsonSerializer.Serialize(result));
        _logger.LogInformation("Device {DeviceId} {Action} in tenant {TenantId}.",
            entity.DeviceId, exists ? "updated" : "registered", partitionKey);
        return resp;
    }

    private static string BuildHs256Jwt(
        string issuer,
        string subject,
        string audience,
        DateTimeOffset expires,
        DateTimeOffset issuedAt,
        string signingKey,
        string kid,
        params (string Name, string Value)[] extraClaims)
    {
        string headerJson = JsonSerializer.Serialize(new { alg = "HS256", typ = "JWT", kid });
        long ToUnix(DateTimeOffset t) => (long)Math.Floor((t - DateTimeOffset.UnixEpoch).TotalSeconds);

        using var payloadMem = new MemoryStream();
        using (var writer = new Utf8JsonWriter(payloadMem))
        {
            writer.WriteStartObject();
            writer.WriteString("iss", issuer);
            writer.WriteString("sub", subject);
            writer.WriteString("aud", audience);
            writer.WriteNumber("iat", ToUnix(issuedAt));
            writer.WriteNumber("exp", ToUnix(expires));
            writer.WriteString("jti", Guid.NewGuid().ToString("n"));
            foreach (var (n, v) in extraClaims) writer.WriteString(n, v);
            writer.WriteEndObject();
        }

        var headerB64 = ToB64Url(Encoding.UTF8.GetBytes(headerJson));
        var payloadB64 = ToB64Url(payloadMem.ToArray());
        var signingInput = $"{headerB64}.{payloadB64}";
        var keyBytes = TryBase64(signingKey) ?? Encoding.UTF8.GetBytes(signingKey);

        using var hmac = new HMACSHA256(keyBytes);
        var sig = hmac.ComputeHash(Encoding.ASCII.GetBytes(signingInput));
        var sigB64 = ToB64Url(sig);
        return $"{signingInput}.{sigB64}";
    }

    private static string ToB64Url(ReadOnlySpan<byte> data)
        => Convert.ToBase64String(data).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static byte[]? TryBase64(string input)
    {
        try { return Convert.FromBase64String(input); } catch { return null; }
    }
}