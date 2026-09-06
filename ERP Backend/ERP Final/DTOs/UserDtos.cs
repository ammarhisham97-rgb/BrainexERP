using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IdentityModel.Tokens.Jwt;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ERPSystem.Services.Chatbot;
using ERPSystem.Services.HRChatbot;
using ERPSystem.Services.ResumeMatching;

// DTOs
/// <summary>
/// Represents the login request data contract.
/// </summary>
public record LoginRequest(string Email, string Password);
/// <summary>
/// Represents the register request data contract.
/// </summary>
public record RegisterRequest(string Username, string Email, string Password, string? FirstName, string? LastName);
/// <summary>
/// Represents the update user request data contract.
/// </summary>
public record UpdateUserRequest(
    string? Username,      // Add this
    string? Email,         // Add this
    string? FirstName,
    string? LastName,
    string? PhoneNumber,
    UserStatus? Status
);
/// <summary>
/// Represents the update role request data contract.
/// </summary>
public record UpdateRoleRequest(string Name, string? Description, List<string> Permissions);
/// <summary>
/// Represents the change password request data contract.
/// </summary>
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
/// <summary>
/// Represents the user dto data contract.
/// </summary>
public record UserDto(Guid Id, string Username, string Email, string? FirstName, string? LastName, UserStatus Status, List<string> Roles);
/// <summary>
/// Represents the user with permissions dto data contract.
/// </summary>
public record UserWithPermissionsDto(Guid Id, string Username, string Email, string? FirstName, string? LastName, UserStatus Status, List<string> Roles, List<string> Permissions);
/// <summary>
/// Represents the role dto data contract.
/// </summary>
public record RoleDto(Guid Id, string Name, string? Description, List<string> Permissions);
/// <summary>
/// Represents the token response data contract.
/// </summary>
public record TokenResponse(string Token, DateTime ExpiresAt, UserDto User);
/// <summary>
/// Represents the create user admin request data contract.
/// </summary>
public record CreateUserAdminRequest(string Username, string Email, string Password, string? FirstName, string? LastName);
/// <summary>
/// Represents the create role request data contract.
/// </summary>
public record CreateRoleRequest(string Name, string? Description, List<string> Permissions);

// Chatbot Request
/// <summary>
/// Represents the chatbot ask request data contract.
/// </summary>
public record ChatbotAskRequest(string Question);


// User Search Request
/// <summary>
/// Represents the user search request data contract.
/// </summary>
public record UserSearchRequest
{
    public string? SearchTerm { get; init; }
    public UserStatus? Status { get; init; }
    public Guid? RoleId { get; init; }
    public DateTime? CreatedAfter { get; init; }
    public DateTime? CreatedBefore { get; init; }
}

