using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace wywo.backend.Functions;

public class ChatManagement
{
    private readonly ILogger<ChatManagement> _logger;

    public ChatManagement(ILogger<ChatManagement> logger)
    {
        _logger = logger;
    }

    // Function CreateChat
    [Function("CreateChat")]
    public async Task<HttpResponseData> CreateChat(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "chats")] HttpRequestData req)
    {
        _logger.LogInformation("Request made to create a new chat.");

        // Validate req body is valid
        string body = await new StreamReader(req.Body).ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(body))
        {
            var bad = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Request body is empty.");
            return bad;
        }

        // Placeholder
        return req.CreateResponse(System.Net.HttpStatusCode.NotImplemented);

        // Validate the request came from a registered device
#if DEBUG
        // In debug mode, we'll allow chat creation with a fake POST body, but must reference a registered device ID
#endif
    }

    // Function JoinChat

    // Function Negotiate

    // Function CloseChat
}