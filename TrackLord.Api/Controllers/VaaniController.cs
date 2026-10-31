using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrackLord.Api.Authorization;
using TrackLord.Domain.Repositories;

namespace TrackLord.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class VaaniController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly IUserRepository _userRepository;

    public VaaniController(
        HttpClient httpClient,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        IUserRepository userRepository)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _environment = environment;
        _userRepository = userRepository;
    }

    [HttpPost("webrtc-session")]
    public async Task<IActionResult> CreateWebRtcSession([FromBody] CreateWebRtcSessionRequest request)
    {
        if (!Guid.TryParse(request.AgentId, out _))
        {
            return BadRequest(new { message = "A valid Vaani agent UUID is required." });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(userId, out var userGuid))
        {
            return Unauthorized(new { message = "A valid TrackLord user identity is required." });
        }

        var user = await _userRepository.GetByIdAsync(userGuid);
        if (user == null || !user.IsActive)
        {
            return Forbid();
        }

        var voiceToken = VaaniVoiceAccessToken.Create(
            _configuration,
            user.Id.ToString(),
            user.PhoneNumber,
            user.TenantId);

        var apiKey = _configuration["Vaani:ApiKey"]
            ?? Environment.GetEnvironmentVariable("VAANI_API_KEY")
            ?? ReadDevelopmentApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Vaani is not configured",
                detail: "Set the VAANI_API_KEY environment variable or Vaani:ApiKey configuration.");
        }

        using var vaaniRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.vaanivoice.ai/api/trigger-call/");
        vaaniRequest.Headers.TryAddWithoutValidation("X-API-Key", apiKey);
        vaaniRequest.Content = JsonContent.Create(new
        {
            agent_id = request.AgentId,
            medium = "webrtc",
            metadata = new
            {
                tracklord_voice_token = voiceToken
            },
            modify_agent = new
            {
                persona = new
                {
                    identity = new
                    {
                        system_prompt = TrackLordVoicePrompt
                    }
                }
            }
        });

        using var response = await _httpClient.SendAsync(
            vaaniRequest,
            HttpCompletionOption.ResponseHeadersRead,
            HttpContext.RequestAborted);
        var responseBody = await response.Content.ReadAsStringAsync(HttpContext.RequestAborted);

        return new ContentResult
        {
            Content = responseBody,
            ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
            StatusCode = (int)response.StatusCode
        };
    }

    private const string TrackLordVoicePrompt = """
        You are TrackLord Voice Assistant, a concise and careful read-only assistant for the signed-in TrackLord user.
        You can answer questions using only the TrackLord read-only tools configured for this agent.
        For any question about current TrackLord records, call the relevant tool and base your answer only on its result.
        Never invent records, quantities, statuses, client details, or history. If a tool denies access or returns no result, explain that clearly.
        Never claim to change, create, delete, import, reject, move, consume, reverse, or administer anything. This agent is strictly read-only.
        Ask a short clarification when a lot, step, client, or material is ambiguous.
        Every tool call must include this exact voice session token: {{tracklord_voice_token}}.
        Do not reveal, repeat, or discuss the voice session token. Use it only as the voiceToken field required by a TrackLord tool.
        """;

    private string? ReadDevelopmentApiKey()
    {
        if (!_environment.IsDevelopment())
        {
            return null;
        }

        var envFilePath = Path.GetFullPath(Path.Combine(
            _environment.ContentRootPath,
            "..",
            "tracklord-web",
            ".env"));
        if (!System.IO.File.Exists(envFilePath))
        {
            return null;
        }

        foreach (var line in System.IO.File.ReadLines(envFilePath))
        {
            var trimmedLine = line.Trim();
            if (!trimmedLine.StartsWith("VAANI_API_KEY=", StringComparison.Ordinal))
            {
                continue;
            }

            var value = trimmedLine["VAANI_API_KEY=".Length..].Trim();
            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            {
                value = value[1..^1];
            }
            else if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
            {
                value = value[1..^1];
            }

            return value;
        }

        return null;
    }
}

public sealed record CreateWebRtcSessionRequest(
    [Required, StringLength(64)] string AgentId);
