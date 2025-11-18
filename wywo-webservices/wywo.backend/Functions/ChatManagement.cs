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

        // Extract the token from the body, and convert to JWT
        // Validate that it is a well-formed JWT

        // Extract the calling user information from the body and validate the payload (Name, Initial Message, and if
        // they wanted to be notified via phone the included phone number)

        // Validate the request came from a registered device
        // We'll get the iss claim from the JWT (passed as the token in the request body) and check against the devices table
        // If we have a valid device, now we can pull the public key for that device and validate the JWT signature
        // If the JWT is valid, we can proceed to create the chat room, otherwise return 401 Unauthorized
#if DEBUG
        // In debug mode, we'll allow chat creation with a fake POST body, but must reference a registered device ID
#elif RELEASE
        // In release mode, we must have a valid JWT in the POST body
#endif

        // Now that we've validated the request came from a registered device, we can create the chat room
        // Create a new chat ID and do the needful in Redis to set up the chat room
        // As part of the Redis setup, we'll need to create an access token for the anonymous user (the one who created the room)
        
        // Get user communication preferences from Users table
        // Send notifications to all registered users that a new chat is open (Azure Communication Services)
        // If the calling user wanted to receive the link to the chat, send a message with a direct link (including access token) to the user via ACS (delay by ~30 seconds)

        // Return to front end the link above (including access token) so that the app can redirect to the chat room

        // Placeholder
        return req.CreateResponse(System.Net.HttpStatusCode.NotImplemented);
    }

    // Function JoinChat

    // Function Negotiate

    // Function CloseChat
}