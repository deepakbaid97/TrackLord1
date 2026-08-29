using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrackLord.Api.Authorization;
using TrackLord.Api.DTOs;
using TrackLord.Domain.Aggregates.RoleAggregate;
using TrackLord.Domain.Aggregates.TenantAggregate;
using TrackLord.Domain.Aggregates.UserAggregate;
using TrackLord.Domain.Common;
using TrackLord.Domain.Repositories;
using TrackLord.Domain.ValueObjects;
using TrackLord.Infrastructure.Services;

namespace TrackLord.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IFirebaseTokenVerifier _firebaseTokenVerifier;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IUserRepository _userRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly ITenantContext _tenantContext;

    public AuthController(
        IFirebaseTokenVerifier firebaseTokenVerifier,
        IJwtTokenService jwtTokenService,
        IUserRepository userRepository,
        ITenantRepository tenantRepository,
        IRoleRepository roleRepository,
        ITenantContext tenantContext)
    {
        _firebaseTokenVerifier = firebaseTokenVerifier;
        _jwtTokenService = jwtTokenService;
        _userRepository = userRepository;
        _tenantRepository = tenantRepository;
        _roleRepository = roleRepository;
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Verifies Firebase ID Token from Phone OTP authentication and generates custom backend JWT token.
    /// Persists/updates User table in database and returns permissions dictionary.
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

        // Ensure user has valid Role entity linked
        var role = await EnsureUserRoleAsync(user, user.TenantId);

        var tokenResult = _jwtTokenService.GenerateToken(user.Id.ToString(), user.PhoneNumber, user.TenantId, user.Role, user.RoleId);
        var tenant = await _tenantRepository.GetByIdAsync(user.TenantId);
        var tenantSettings = ParseTenantSettings(tenant?.SettingsJson);

        return Ok(new
        {
            token = tokenResult.Token,
            expiresAt = tokenResult.ExpiresAt,
            user = new
            {
                id = user.Id,
                tenantId = user.TenantId,
                tenantName = tenant?.Name ?? "Default Tenant",
                tenantCode = tenant?.Code ?? "DEFAULT",
                phoneNumber = user.PhoneNumber,
                name = user.Name,
                role = user.Role,
                roleId = user.RoleId,
                permissions = role.GetPermissionsDictionary(),
                createdAtUtc = user.CreatedAtUtc,
                lastLoginAtUtc = user.LastLoginAtUtc,
                tenantSettings = new
                {
                    clientMode = FormatClientMode(tenantSettings.ClientMode),
                    featureOverrides = tenantSettings.FeatureOverrides ?? new Dictionary<string, bool>()
                }
            }
        });
    }

    /// <summary>
    /// Gets current authenticated user details, assigned role, and resolved permissions from JWT token and database.
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

        var targetTenantId = user?.TenantId ?? _tenantContext.TenantId;
        var tenant = await _tenantRepository.GetByIdAsync(targetTenantId);
        var tenantSettings = ParseTenantSettings(tenant?.SettingsJson);

        Role? role = null;
        if (user != null)
        {
            role = await EnsureUserRoleAsync(user, targetTenantId);
        }

        return Ok(new
        {
            id = user?.Id.ToString() ?? userIdStr,
            tenantId = targetTenantId,
            tenantName = tenant?.Name ?? "Default Tenant",
            tenantCode = tenant?.Code ?? "DEFAULT",
            phoneNumber = user?.PhoneNumber ?? phoneNumber ?? "Unknown",
            name = user?.Name,
            role = user?.Role ?? "Operator",
            roleId = user?.RoleId,
            permissions = role?.GetPermissionsDictionary() ?? new Dictionary<string, List<string>>(),
            isAuthenticated = true,
            tenantSettings = new
            {
                clientMode = FormatClientMode(tenantSettings.ClientMode),
                featureOverrides = tenantSettings.FeatureOverrides ?? new Dictionary<string, bool>()
            }
        });
    }

    /// <summary>
    /// Gets users in the tenant.
    /// </summary>
    [HttpGet("users")]
    [Authorize]
    [RequirePermission(PermissionFeatures.Users, AccessLevels.Read)]
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

        // Non-admin / non-supervisor users can only see their own account if they lack full user management access
        if (currentUser != null && currentUser.Role != "Admin" && currentUser.Role != "Supervisor" && currentUser.Role != "Manager")
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
            u.RoleId,
            u.FirebaseUid,
            u.CreatedAtUtc,
            u.LastLoginAtUtc,
            u.IsActive
        }));
    }

    /// <summary>
    /// Creates a new user within the current tenant and assigns their role.
    /// </summary>
    [HttpPost("users")]
    [Authorize]
    [RequirePermission(PermissionFeatures.Users, AccessLevels.Write)]
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

        var normalizedPhone = request.PhoneNumber.Trim();
        var existing = await _userRepository.GetByPhoneNumberAsync(normalizedPhone);
        if (existing != null)
        {
            return Conflict(new { message = $"User with phone number '{normalizedPhone}' already exists in this tenant." });
        }

        var activeTenantId = currentUser?.TenantId ?? _tenantContext.TenantId;

        // Resolve assigned role
        Role? targetRole = null;
        if (request.RoleId.HasValue && request.RoleId.Value != Guid.Empty)
        {
            targetRole = await _roleRepository.GetByIdAsync(request.RoleId.Value);
        }
        if (targetRole == null && !string.IsNullOrWhiteSpace(request.Role))
        {
            targetRole = await _roleRepository.GetByNameAsync(request.Role);
        }
        if (targetRole == null)
        {
            targetRole = await _roleRepository.GetByNameAsync("Operator") ?? (await EnsureTenantRolesExistAsync(activeTenantId)).First(r => r.Name == "Operator");
        }

        var newUser = new User(
            Guid.NewGuid(),
            normalizedPhone,
            name: request.Name,
            role: targetRole.Name,
            tenantId: activeTenantId,
            roleId: targetRole.Id
        );

        await _userRepository.AddAsync(newUser);

        return CreatedAtAction(nameof(GetMe), new { id = newUser.Id }, new
        {
            id = newUser.Id,
            tenantId = newUser.TenantId,
            phoneNumber = newUser.PhoneNumber,
            name = newUser.Name,
            role = newUser.Role,
            roleId = newUser.RoleId,
            createdAtUtc = newUser.CreatedAtUtc,
            isActive = newUser.IsActive
        });
    }

    /// <summary>
    /// Updates a user's details (Name / Role). Users can update their own profile, Admins/Managers can update any user.
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
        var isAdminOrManager = currentUser != null && (currentUser.Role == "Admin" || currentUser.Role == "Supervisor" || currentUser.Role == "Manager");

        if (!isSelf && !isAdminOrManager)
        {
            return StatusCode(403, new { message = "You can only update your own profile unless you have user management privileges." });
        }

        Role? targetRole = null;
        if (isAdminOrManager)
        {
            if (request.RoleId.HasValue && request.RoleId.Value != Guid.Empty)
            {
                targetRole = await _roleRepository.GetByIdAsync(request.RoleId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(request.Role))
            {
                targetRole = await _roleRepository.GetByNameAsync(request.Role);
            }
        }

        if (targetRole != null)
        {
            targetUser.AssignRole(targetRole.Id, targetRole.Name);
        }

        targetUser.UpdateProfile(request.Name, targetUser.Role, targetUser.RoleId);
        await _userRepository.UpdateAsync(targetUser);

        return Ok(new
        {
            id = targetUser.Id,
            tenantId = targetUser.TenantId,
            phoneNumber = targetUser.PhoneNumber,
            name = targetUser.Name,
            role = targetUser.Role,
            roleId = targetUser.RoleId,
            createdAtUtc = targetUser.CreatedAtUtc,
            lastLoginAtUtc = targetUser.LastLoginAtUtc,
            isActive = targetUser.IsActive
        });
    }

    private async Task<Role> EnsureUserRoleAsync(User user, Guid tenantId)
    {
        Role? role = null;
        if (user.RoleId.HasValue && user.RoleId.Value != Guid.Empty)
        {
            role = await _roleRepository.GetByIdAsync(user.RoleId.Value);
        }

        if (role == null && !string.IsNullOrWhiteSpace(user.Role))
        {
            role = await _roleRepository.GetByNameAsync(user.Role);
        }

        if (role == null)
        {
            var seededRoles = await EnsureTenantRolesExistAsync(tenantId);
            role = seededRoles.FirstOrDefault(r => string.Equals(r.Name, user.Role, StringComparison.OrdinalIgnoreCase))
                   ?? seededRoles.First(r => r.Name == "Operator");
        }

        if (user.RoleId != role.Id || user.Role != role.Name)
        {
            user.AssignRole(role.Id, role.Name);
            await _userRepository.UpdateAsync(user);
        }

        return role;
    }

    private async Task<IReadOnlyList<Role>> EnsureTenantRolesExistAsync(Guid tenantId)
    {
        var existing = await _roleRepository.GetAllAsync();
        if (existing != null && existing.Count > 0)
        {
            return existing;
        }

        var defaultRoles = Role.CreateDefaultSystemRoles(tenantId);
        await _roleRepository.AddRangeAsync(defaultRoles);
        return defaultRoles;
    }

    private static TenantSettings ParseTenantSettings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new TenantSettings();
        try
        {
            return JsonSerializer.Deserialize<TenantSettings>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new TenantSettings();
        }
        catch
        {
            return new TenantSettings();
        }
    }

    private static string FormatClientMode(ClientTrackingMode mode) => mode switch
    {
        ClientTrackingMode.LotLevel => "lot",
        ClientTrackingMode.StepLevel => "step",
        ClientTrackingMode.None => "none",
        _ => "step"
    };
}
