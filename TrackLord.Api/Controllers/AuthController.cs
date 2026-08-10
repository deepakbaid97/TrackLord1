using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrackLord.Domain.Aggregates.UserAggregate;
using TrackLord.Domain.Common;
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
    private readonly ITenantContext _tenantContext;

    public AuthController(
        IFirebaseTokenVerifier firebaseTokenVerifier,
        IJwtTokenService jwtTokenService,
        IUserRepository userRepository,
        ITenantContext tenantContext)
    {
        _firebaseTokenVerifier = firebaseTokenVerifier;
        _jwtTokenService = jwtTokenService;
        _userRepository = userRepository;
        _tenantContext = tenantContext;
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
    /// Gets users in the tenant. Admins/Supervisors receive all tenant users; non-admins receive only themselves.
    /// </summary>
    [HttpGet("users")]
    [Authorize]
    public async Task<IActionResult> GetAllUsers()
    {
        var currentUserIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var currentUserPhone = User.FindFirstValue("phone_number") ?? User.FindFirstValue(ClaimTypes.MobilePhone);

        User? currentUser = null;
        if (Guid.TryParse(currentUserIdStr, out var currentGuid))
        {
            currentUser = await _userRepository.GetByIdAsync(currentGuid);
        }
        if (currentUser == null && !string.IsNullOrWhiteSpace(currentUserPhone))
        {
            currentUser = await _userRepository.GetByPhoneNumberAsync(currentUserPhone);
        }

        var allUsers = await _userRepository.GetAllAsync();

        // Non-admin / non-supervisor users can only see their own account
        if (currentUser != null && currentUser.Role != "Admin" && currentUser.Role != "Supervisor")
        {
            allUsers = allUsers.Where(u => u.Id == currentUser.Id || u.PhoneNumber == currentUser.PhoneNumber).ToList();
        }

        return Ok(allUsers.Select(u => new
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

    /// <summary>
    /// Creates a new user within the current tenant (Admin / Supervisor only).
    /// </summary>
    [HttpPost("users")]
    [Authorize]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return BadRequest(new { message = "Phone number is required." });
        }

        var currentUserIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var currentUserPhone = User.FindFirstValue("phone_number") ?? User.FindFirstValue(ClaimTypes.MobilePhone);

        User? currentUser = null;
        if (Guid.TryParse(currentUserIdStr, out var currentGuid))
        {
            currentUser = await _userRepository.GetByIdAsync(currentGuid);
        }
        if (currentUser == null && !string.IsNullOrWhiteSpace(currentUserPhone))
        {
            currentUser = await _userRepository.GetByPhoneNumberAsync(currentUserPhone);
        }

        if (currentUser != null && currentUser.Role != "Admin" && currentUser.Role != "Supervisor")
        {
            return StatusCode(403, new { message = "Only Admin users can add new team members." });
        }

        var normalizedPhone = request.PhoneNumber.Trim();
        var existing = await _userRepository.GetByPhoneNumberAsync(normalizedPhone);
        if (existing != null)
        {
            return Conflict(new { message = $"User with phone number '{normalizedPhone}' already exists in this tenant." });
        }

        var activeTenantId = currentUser?.TenantId ?? _tenantContext.TenantId;
        var newUser = new User(
            Guid.NewGuid(),
            normalizedPhone,
            name: request.Name,
            role: string.IsNullOrWhiteSpace(request.Role) ? "Operator" : request.Role,
            tenantId: activeTenantId
        );

        await _userRepository.AddAsync(newUser);

        return CreatedAtAction(nameof(GetMe), new { id = newUser.Id }, new
        {
            id = newUser.Id,
            tenantId = newUser.TenantId,
            phoneNumber = newUser.PhoneNumber,
            name = newUser.Name,
            role = newUser.Role,
            createdAtUtc = newUser.CreatedAtUtc,
            isActive = newUser.IsActive
        });
    }

    /// <summary>
    /// Updates a user's details (Name / Role). Users can update their own profile, Admins can update any user.
    /// </summary>
    [HttpPut("users/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
        var targetUser = await _userRepository.GetByIdAsync(id);
        if (targetUser == null)
        {
            return NotFound(new { message = $"User with ID {id} not found." });
        }

        var currentUserIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var currentUserPhone = User.FindFirstValue("phone_number") ?? User.FindFirstValue(ClaimTypes.MobilePhone);

        User? currentUser = null;
        if (Guid.TryParse(currentUserIdStr, out var currentGuid))
        {
            currentUser = await _userRepository.GetByIdAsync(currentGuid);
        }
        if (currentUser == null && !string.IsNullOrWhiteSpace(currentUserPhone))
        {
            currentUser = await _userRepository.GetByPhoneNumberAsync(currentUserPhone);
        }

        var isSelf = currentUser != null && currentUser.Id == id;
        var isAdmin = currentUser != null && (currentUser.Role == "Admin" || currentUser.Role == "Supervisor");

        if (!isSelf && !isAdmin)
        {
            return StatusCode(403, new { message = "You can only update your own profile unless you are an Admin." });
        }

        var newRole = isAdmin && !string.IsNullOrWhiteSpace(request.Role) ? request.Role : targetUser.Role;

        targetUser.UpdateProfile(request.Name, newRole);
        await _userRepository.UpdateAsync(targetUser);

        return Ok(new
        {
            id = targetUser.Id,
            tenantId = targetUser.TenantId,
            phoneNumber = targetUser.PhoneNumber,
            name = targetUser.Name,
            role = targetUser.Role,
            createdAtUtc = targetUser.CreatedAtUtc,
            lastLoginAtUtc = targetUser.LastLoginAtUtc,
            isActive = targetUser.IsActive
        });
    }
}

public record VerifyOtpRequest(string IdToken, string? PhoneNumber);
public record CreateUserRequest(string PhoneNumber, string? Name, string? Role);
public record UpdateUserRequest(string? Name, string? Role);
