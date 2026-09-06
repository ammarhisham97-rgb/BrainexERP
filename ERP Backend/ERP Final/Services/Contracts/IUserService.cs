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

/// <summary>
/// Defines the i user service service contract.
/// </summary>
public interface IUserService
{
    Task<Result<TokenResponse>> LoginAsync(LoginRequest request);
    Task<Result<UserDto>> RegisterAsync(RegisterRequest request);
    Task<Result<UserDto>> CreateUserAdminAsync(CreateUserAdminRequest request);
    Task<Result<UserDto>> GetUserByIdAsync(Guid id);
    Task<Result<UserDto>> UpdateUserAsync(Guid id, UpdateUserRequest request);
    Task<Result> DeactivateUserAsync(Guid id);
    Task<Result<RoleDto>> UpdateRoleAsync(Guid id, UpdateRoleRequest request);
    Task<Result> DeleteRoleAsync(Guid id);
    Task<Result<RoleDto>> GetRoleByIdAsync(Guid id);
    Task<Result<List<UserDto>>> GetAllUsersAsync();
    Task<Result> ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
    Task<Result> AssignRoleAsync(Guid userId, Guid roleId);
    Task<Result> RemoveRoleAsync(Guid userId, Guid roleId);
    Task<Result<List<RoleDto>>> GetAllRolesAsync();
    Task<Result<RoleDto>> CreateRoleAsync(string name, string? description, List<string> permissions);
    Task LogAuditAsync(Guid userId, string action, string? entityType = null, Guid? entityId = null);
    Task<Result<PagedResult<UserDto>>> SearchUsersAsync(UserSearchRequest search, PaginationRequest pagination);
    Task<Result<UserWithPermissionsDto>> GetCurrentUserAsync(Guid userId);
    Task<Result<List<string>>> GetUserPermissionsAsync(Guid userId);
}



