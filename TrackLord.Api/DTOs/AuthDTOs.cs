using System;

namespace TrackLord.Api.DTOs;

public record VerifyOtpRequest(string IdToken, string? PhoneNumber);
public record CreateUserRequest(string PhoneNumber, string? Name, string? Role, Guid? RoleId = null);
public record UpdateUserRequest(string? Name, string? Role, Guid? RoleId = null);