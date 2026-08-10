using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrackLord.Domain.Aggregates.UserAggregate;
using TrackLord.Domain.Repositories;
using TrackLord.Infrastructure.Services;

namespace TrackLord.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IFirebaseTokenVerifier _firebaseTokenVerifier;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IUserRepository _userRepository;

    public AuthController(
        IFirebaseTokenVerifier firebaseTokenVerifier,
        IJwtTokenService jwtTokenService,
        IUserRepository userRepository)
    {
        _firebaseTokenVerifier = firebaseTokenVerifier;
        _jwtTokenService = jwtTokenService;
        _userRepository = userRepository;
    }

    /// <summary>
    /// Verifies Firebase ID Token from Phone OTP authentication and generates custom backend JWT token.
    /// Persists/updates User table in database.
    /// </summary>
    [HttpPost("verify-otp")]
    [HttpPost("firebase-verify")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
        {
            return BadRequest(new { message = "Firebase ID token is required." });
        }

        var verificationResult = await _firebaseTokenVerifier.VerifyIdTokenAsync(request.IdToken, request.PhoneNumber);
        if (!verificationResult.IsValid)
        {
            return BadRequest(new { message = verificationResult.ErrorMessage ?? "Invalid Firebase token." });
        }

        var phone = verificationResult.PhoneNumber!;
        var firebaseUid = verificationResult.UserId!;

        // Persist / update User in Database
        var user = await _userRepository.GetByPhoneNumberAsync(phone);
        if (user == null)
        {
            user = new User(Guid.NewGuid(), phone, name: null, role: "Operator", firebaseUid: firebaseUid);
            await _userRepository.AddAsync(user);
        }
        else
        {
            user.SetFirebaseUid(firebaseUid);
            user.RecordLogin();
            await _userRepository.UpdateAsync(user);
        }

        var tokenResult = _jwtTokenService.GenerateToken(user.Id.ToString(), user.PhoneNumber, user.TenantId);

        return Ok(new
        {
            token = tokenResult.Token,
            expiresAt = tokenResult.ExpiresAt,
            user = new
            {
                id = user.Id,
                tenantId = user.TenantId,
                phoneNumber = user.PhoneNumber,
                name = user.Name,
                role = user.Role,
                createdAtUtc = user.CreatedAtUtc,
                lastLoginAtUtc = user.LastLoginAtUtc
            }
        });
    }

    /// <summary>
    /// Gets current authenticated user details from JWT token and User table.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMe()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var phoneNumber = User.FindFirstValue("phone_number") ?? User.FindFirstValue(ClaimTypes.MobilePhone);

        User? user = null;
        if (Guid.TryParse(userIdStr, out var userGuid))
        {
            user = await _userRepository.GetByIdAsync(userGuid);
        }

        if (user == null && !string.IsNullOrWhiteSpace(phoneNumber))
        {
            user = await _userRepository.GetByPhoneNumberAsync(phoneNumber);
        }

        return Ok(new
        {
            id = user?.Id.ToString() ?? userIdStr,
            tenantId = user?.TenantId,
            phoneNumber = user?.PhoneNumber ?? phoneNumber ?? "Unknown",
            name = user?.Name,
            role = user?.Role ?? "Operator",
            isAuthenticated = true
        });
    }

    /// <summary>
    /// Gets all users in the system.
    /// </summary>
    [HttpGet("users")]
    [Authorize]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _userRepository.GetAllAsync();
        return Ok(users.Select(u => new
        {
            u.Id,
            u.TenantId,
            u.PhoneNumber,
            u.Name,
            u.Role,
            u.FirebaseUid,
            u.CreatedAtUtc,
            u.LastLoginAtUtc,
            u.IsActive
        }));
    }
}

public record VerifyOtpRequest(string IdToken, string? PhoneNumber);
