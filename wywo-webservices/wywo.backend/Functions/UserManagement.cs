using Azure;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Threading.Tasks;
using wywo.backend.AzureDataTables;
using wywo.shared.DTOs;

namespace wywo.backend.Functions;

public class UserManagement
{
    private readonly ILogger<UserManagement> _logger;

    public UserManagement(ILogger<UserManagement> logger)
    {
        _logger = logger;
    }

    // TODO: This route should be protected, only accessible by registered users.
    //       A specific route for getting a list of chat participants for anonymous users (chats/{chatId}/users})
    //       should be created separately.
    [Function("GetAllUsers")]
    public async Task<HttpResponseData> GetAllUsers([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "users")] HttpRequestData req)
    {
        _logger.LogInformation("Request made for all registered users.");

        var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            _logger.LogError("Failed to get all users because the Azure Storage connection string is not configured.");
            await errorResponse.WriteStringAsync("Storage configuration error.");
            return errorResponse;
        }

        var serviceClient = new TableServiceClient(connectionString);
        var tableClient = serviceClient.GetTableClient("Users");

        var users = new List<object>();
        const string partitionKey = "users";

        try
        {
            await foreach (var entity in tableClient.QueryAsync<UserEntity>(u => u.PartitionKey == partitionKey))
            {
                users.Add(new
                {
                    Id = entity.Id,
                    Email = entity.Email,
                    DisplayName = entity.DisplayName,
                    Logins = entity.Logins
                });
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Users table was not found. Returning empty list.");
        }

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(users));
        return response;
    }

    // Upsert semantics with immutability for Email (and thus RowKey) and Id uniqueness.
    // RowKey and Email are always identical (normalized email) and NEVER changed after creation.
    [Function("UpsertUser")]
    public async Task<HttpResponseData> UpsertUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "put", Route = "users")] HttpRequestData req)
    {
        _logger.LogInformation("Request made to upsert a user.");

        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        if (string.IsNullOrEmpty(requestBody))
        {
            var badResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            _logger.LogWarning("Upsert request body was empty.");
            await badResponse.WriteStringAsync("Request body is empty.");
            return badResponse;
        }

        CreateUserDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<CreateUserDto>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException)
        {
            var badResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            _logger.LogWarning("Upsert request contained invalid JSON.");
            await badResponse.WriteStringAsync("Invalid JSON payload.");
            return badResponse;
        }

        if (dto is null || string.IsNullOrWhiteSpace(dto.Email))
        {
            var badResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            _logger.LogWarning("Upsert request had invalid or missing email.");
            await badResponse.WriteStringAsync("Email is required.");
            return badResponse;
        }

        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            _logger.LogError("Failed to upsert user because the Azure Storage connection string is not configured.");
            await errorResponse.WriteStringAsync("Storage configuration error.");
            return errorResponse;
        }

        var serviceClient = new TableServiceClient(connectionString);
        var tableClient = serviceClient.GetTableClient("Users");
        await tableClient.CreateIfNotExistsAsync();

        const string partitionKey = "users";
        bool isUpdate = false;

        // Try to get existing user by immutable key (PartitionKey + RowKey).
        var existing = await tableClient.GetEntityIfExistsAsync<UserEntity>(partitionKey, normalizedEmail);

        UserEntity entity;
        if (existing.HasValue)
        {
            entity = existing.Value;
            isUpdate = true;

            // Email/RowKey immutability: If payload email differs from stored email (shouldn't), reject.
            if (entity.Email != normalizedEmail || entity.RowKey != normalizedEmail)
            {
                var conflict = req.CreateResponse(System.Net.HttpStatusCode.Conflict);
                _logger.LogWarning("Attempt to change immutable email/rowkey for user Id {Id}.", entity.Id);
                await conflict.WriteStringAsync("Email cannot be changed.");
                return conflict;
            }

            // Id immutability check (cannot change Id for existing email).
            if (!string.IsNullOrWhiteSpace(dto.Id) && dto.Id.Trim() != entity.Id)
            {
                var conflict = req.CreateResponse(System.Net.HttpStatusCode.Conflict);
                _logger.LogWarning("Attempt to change immutable Id for email {Email}. Existing Id {ExistingId}, supplied Id {SuppliedId}.",
                    normalizedEmail, entity.Id, dto.Id);
                await conflict.WriteStringAsync("Cannot change user Id for existing email.");
                return conflict;
            }

            // Update only mutable, non-null fields.
            if (dto.DisplayName is not null)
                entity.DisplayName = dto.DisplayName;
            if (dto.AvatarUrl is not null)
                entity.AvatarUrl = dto.AvatarUrl;
        }
        else
        {
            // Creating new user; enforce Id uniqueness across all emails.
            var newId = string.IsNullOrWhiteSpace(dto.Id) ? Guid.NewGuid().ToString("n") : dto.Id.Trim();

            bool idExists = false;
            await foreach (var _ in tableClient.QueryAsync<UserEntity>(e => e.PartitionKey == partitionKey && e.Id == newId))
            {
                idExists = true;
                break;
            }

            if (idExists)
            {
                var conflict = req.CreateResponse(System.Net.HttpStatusCode.Conflict);
                _logger.LogWarning("Attempt to create a user with an Id already used by a different email. Id: {Id}, Email: {Email}.", newId, normalizedEmail);
                await conflict.WriteStringAsync("User Id already exists.");
                return conflict;
            }

            entity = new UserEntity
            {
                PartitionKey = partitionKey,
                RowKey = normalizedEmail,
                Email = normalizedEmail,
                Id = newId,
                DisplayName = dto.DisplayName,
                AvatarUrl = dto.AvatarUrl,
                LoginsJson = "[]"
            };
        }

        // Upsert: Replace ensures full snapshot is stored while retaining immutables.
        await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace);

        var statusCode = isUpdate
            ? System.Net.HttpStatusCode.OK
            : System.Net.HttpStatusCode.Created;

        var response = req.CreateResponse(statusCode);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        response.Headers.Add("Location", $"/api/users/{entity.Id}");

        var payload = new
        {
            Id = entity.Id,
            Email = entity.Email,
            DisplayName = entity.DisplayName,
            AvatarUrl = entity.AvatarUrl,
            Logins = entity.Logins
        };

        await response.WriteStringAsync(JsonSerializer.Serialize(payload));
        _logger.LogInformation("User {Action} (Email: {Email}, Id: {Id}).",
            isUpdate ? "updated" : "created", entity.Email, entity.Id);

        return response;
    }
}