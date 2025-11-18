using Azure;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using wywo.backend.AzureDataTables;
using wywo.shared.DTOs;

namespace wywo.backend.Functions;

public class SettingsManagement
{
    private const string TableName = "Settings";
    private const string Partition = "settings";
    private const string Row = "app";

    private readonly ILogger<SettingsManagement> _logger;

    public SettingsManagement(ILogger<SettingsManagement> logger)
    {
        _logger = logger;
    }

    [Function("GetAllSettings")]
    public async Task<HttpResponseData> GetAllSettings(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "settings")] HttpRequestData req)
    {
        _logger.LogInformation("Request made for application settings.");

        var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            _logger.LogError("Failed to get settings because the Azure Storage connection string is not configured.");
            await errorResponse.WriteStringAsync("Storage configuration error.");
            return errorResponse;
        }

        var serviceClient = new TableServiceClient(connectionString);
        var tableClient = serviceClient.GetTableClient(TableName);

        AppSettingsDto settings = new(string.Empty);

        try
        {
            var result = await tableClient.GetEntityIfExistsAsync<SettingsEntity>(Partition, Row);
            if (result.HasValue)
            {
                settings = result.Value.Settings;
            }
            else
            {
                _logger.LogInformation("Settings entity not found. Returning default settings.");
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Settings table was not found. Returning default settings.");
        }

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(settings));
        return response;
    }

    [Function("UpsertSettings")]
    public async Task<HttpResponseData> UpsertSettings(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "put", Route = "settings")] HttpRequestData req)
    {
        _logger.LogInformation("Request made to upsert application settings.");

        var body = await new StreamReader(req.Body).ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            var bad = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            _logger.LogWarning("Upsert settings request body was empty.");
            await bad.WriteStringAsync("Request body is empty.");
            return bad;
        }

        AppSettingsDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<AppSettingsDto>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException)
        {
            var bad = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            _logger.LogWarning("Upsert settings request contained invalid JSON.");
            await bad.WriteStringAsync("Invalid JSON payload.");
            return bad;
        }

        if (dto is null)
        {
            var bad = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            _logger.LogWarning("Upsert settings request deserialized to null.");
            await bad.WriteStringAsync("Invalid settings payload.");
            return bad;
        }

        var connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            _logger.LogError("Failed to upsert settings because the Azure Storage connection string is not configured.");
            await errorResponse.WriteStringAsync("Storage configuration error.");
            return errorResponse;
        }

        var serviceClient = new TableServiceClient(connectionString);
        var tableClient = serviceClient.GetTableClient(TableName);
        await tableClient.CreateIfNotExistsAsync();

        bool existed = false;
        try
        {
            var existing = await tableClient.GetEntityIfExistsAsync<SettingsEntity>(Partition, Row);
            existed = existing.HasValue;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Table not found prior to CreateIfNotExists or entity not found; treat as non-existent.
            existed = false;
        }

        var entity = new SettingsEntity
        {
            PartitionKey = Partition,
            RowKey = Row,
            Settings = dto
        };

        await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace);

        var status = existed ? System.Net.HttpStatusCode.OK : System.Net.HttpStatusCode.Created;
        var response = req.CreateResponse(status);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        if (!existed)
        {
            response.Headers.Add("Location", "/api/settings");
        }
        await response.WriteStringAsync(JsonSerializer.Serialize(dto));
        _logger.LogInformation("Application settings {Action}.", existed ? "updated" : "created");

        return response;
    }
}