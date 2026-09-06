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
/// Represents the user service domain model.
/// </summary>
public class UserService : IUserService
{
    private readonly ERPDbContext _context;
    private readonly JwtTokenService _tokenService;

    public UserService(ERPDbContext context, JwtTokenService tokenService)
    {
        _context = context;
        _tokenService = tokenService;
    }
    // Add these two methods right after the UserService constructor

    private Result ValidatePassword(string password)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(password))
            return Result.Failure("Password is required");

        if (password.Length < 8)
            errors.Add("Password must be at least 8 characters long");

        if (!password.Any(char.IsUpper))
            errors.Add("Password must contain at least one uppercase letter");

        if (!password.Any(char.IsLower))
            errors.Add("Password must contain at least one lowercase letter");

        if (!password.Any(char.IsDigit))
            errors.Add("Password must contain at least one digit");

        if (!password.Any(c => "!@#$%^&*()_+-=[]{}|;:,.<>?".Contains(c)))
            errors.Add("Password must contain at least one special character");

        if (errors.Any())
            return Result.Failure(string.Join("; ", errors));

        return Result.Success();
    }

    private Result ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Result.Failure("Email is required");

        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            if (addr.Address != email)
                return Result.Failure("Invalid email format");
        }
        catch
        {
            return Result.Failure("Invalid email format");
        }

        return Result.Success();
    }
    public async Task<Result<UserDto>> UpdateUserAsync(Guid id, UpdateUserRequest request)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
            return Result<UserDto>.Failure("User not found");

        // ? Prevent updating deleted or inactive users
        if (user.IsDeleted)
            return Result<UserDto>.Failure("Cannot update deleted user");

        if (request.Username != null) user.Username = request.Username;
        if (request.Email != null) user.Email = request.Email;
        if (request.FirstName != null) user.FirstName = request.FirstName;
        if (request.LastName != null) user.LastName = request.LastName;
        if (request.PhoneNumber != null) user.PhoneNumber = request.PhoneNumber;
        if (request.Status.HasValue) user.Status = request.Status.Value;

        user.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            return Result<UserDto>.Failure($"Error updating user: {ex.Message}");
        }

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        var userDto = new UserDto(user.Id, user.Username, user.Email, user.FirstName, user.LastName, user.Status, roles);
        return Result<UserDto>.Success(userDto);
    }

    public async Task<Result> DeactivateUserAsync(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return Result.Failure("User not found");

        // ? Implement proper soft delete
        user.IsDeleted = true;
        user.Status = UserStatus.Inactive;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Result.Success();
    }
    public async Task<Result<RoleDto>> UpdateRoleAsync(Guid id, UpdateRoleRequest request)
    {
        var role = await _context.Roles.FindAsync(id);
        if (role == null)
            return Result<RoleDto>.Failure("Role not found");

        if (await _context.Roles.AnyAsync(r => r.Name == request.Name && r.Id != id))
            return Result<RoleDto>.Failure("Role name already exists");

        role.Name = request.Name;
        role.Description = request.Description;
        role.Permissions = request.Permissions;
        role.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var roleDto = new RoleDto(role.Id, role.Name, role.Description, role.Permissions);
        return Result<RoleDto>.Success(roleDto);
    }

    public async Task<Result> DeleteRoleAsync(Guid id)
    {
        var role = await _context.Roles.FindAsync(id);
        if (role == null)
            return Result.Failure("Role not found");

        // Check if role is assigned to any users
        var hasUsers = await _context.UserRoles.AnyAsync(ur => ur.RoleId == id);
        if (hasUsers)
            return Result.Failure("Cannot delete role that is assigned to users");

        _context.Roles.Remove(role);
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<RoleDto>> GetRoleByIdAsync(Guid id)
    {
        var role = await _context.Roles.FindAsync(id);
        if (role == null)
            return Result<RoleDto>.Failure("Role not found");

        var roleDto = new RoleDto(role.Id, role.Name, role.Description, role.Permissions);
        return Result<RoleDto>.Success(roleDto);
    }

    public async Task<Result<TokenResponse>> LoginAsync(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Result<TokenResponse>.Failure("Email and password are required");

        try
        {
            // Case-insensitive email lookup
            var user = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());

            // Generic error message
            if (user == null)
                return Result<TokenResponse>.Failure("Invalid email or password");

            // Check if deleted
            if (user.IsDeleted)
                return Result<TokenResponse>.Failure("Account not found");

            // Verify password
            if (!PasswordHasher.VerifyPassword(request.Password, user.PasswordHash))
            {
                await LogAuditAsync(user.Id, "LoginFailed", "User", user.Id);
                return Result<TokenResponse>.Failure("Invalid email or password");
            }

            // Check status
            if (user.Status != UserStatus.Active)
            {
                var statusMessage = user.Status switch
                {
                    UserStatus.Inactive => "Account is inactive. Please contact support.",
                    UserStatus.Suspended => "Account is suspended. Please contact support.",
                    _ => "Account is not active"
                };
                return Result<TokenResponse>.Failure(statusMessage);
            }

            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await LogAuditAsync(user.Id, "LoginSuccess", "User", user.Id);

            // Get roles and aggregate all permissions
            var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
            var permissions = user.UserRoles
                .SelectMany(ur => ur.Role.Permissions)
                .Distinct()
                .ToList();

            var token = _tokenService.GenerateToken(user, roles, permissions);

            var userDto = new UserDto(user.Id, user.Username, user.Email, user.FirstName, user.LastName, user.Status, roles);
            var response = new TokenResponse(token, DateTime.UtcNow.AddHours(24), userDto);

            return Result<TokenResponse>.Success(response);
        }
        catch (Exception ex)
        {
            return Result<TokenResponse>.Failure("An error occurred during login. Please try again.");
        }
    }

    public async Task<Result<UserDto>> RegisterAsync(RegisterRequest request)
    {
        var emailValidation = ValidateEmail(request.Email);
        if (!emailValidation.IsSuccess)
            return Result<UserDto>.Failure(emailValidation.ErrorMessage ?? "Invalid email");

        var passwordValidation = ValidatePassword(request.Password);
        if (!passwordValidation.IsSuccess)
            return Result<UserDto>.Failure(passwordValidation.ErrorMessage ?? "Invalid password");

        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length < 3)
            return Result<UserDto>.Failure("Username must be at least 3 characters long");

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            if (await _context.Users.AnyAsync(u =>
                u.Email.ToLower() == request.Email.ToLower() ||
                u.Username.ToLower() == request.Username.ToLower()))
            {
                return Result<UserDto>.Failure("Email or username already exists");
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = request.Username.Trim(),
                Email = request.Email.Trim().ToLower(),
                PasswordHash = PasswordHasher.HashPassword(request.Password),
                FirstName = request.FirstName?.Trim(),
                LastName = request.LastName?.Trim(),
                Status = UserStatus.Active,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);

            // Add audit log to context (don't save separately)
            var auditLog = new AuditLog
            {
                UserId = user.Id,
                Action = "UserRegistered",
                EntityType = "User",
                EntityId = user.Id
            };
            _context.AuditLogs.Add(auditLog);

            // Save both user and audit log in single transaction
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var userDto = new UserDto(user.Id, user.Username, user.Email, user.FirstName, user.LastName, user.Status, new List<string>());
            return Result<UserDto>.Success(userDto);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return Result<UserDto>.Failure("An error occurred during registration. Please try again.");
        }
    }
    public async Task<Result<UserDto>> CreateUserAdminAsync(CreateUserAdminRequest request)
    {
        var emailValidation = ValidateEmail(request.Email);
        if (!emailValidation.IsSuccess)
            return Result<UserDto>.Failure(emailValidation.ErrorMessage ?? "Invalid email");

        var passwordValidation = ValidatePassword(request.Password);
        if (!passwordValidation.IsSuccess)
            return Result<UserDto>.Failure(passwordValidation.ErrorMessage ?? "Invalid password");

        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length < 3)
            return Result<UserDto>.Failure("Username must be at least 3 characters long");

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            if (await _context.Users.AnyAsync(u =>
                u.Email.ToLower() == request.Email.ToLower() ||
                u.Username.ToLower() == request.Username.ToLower()))
            {
                return Result<UserDto>.Failure("Email or username already exists");
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = request.Username.Trim(),
                Email = request.Email.Trim().ToLower(),
                PasswordHash = PasswordHasher.HashPassword(request.Password),
                FirstName = request.FirstName?.Trim(),
                LastName = request.LastName?.Trim(),
                Status = UserStatus.Active,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);

            // Add audit log to context (don't save separately)
            var auditLog = new AuditLog
            {
                UserId = user.Id,
                Action = "UserCreatedByAdmin",
                EntityType = "User",
                EntityId = user.Id
            };
            _context.AuditLogs.Add(auditLog);

            // Save both user and audit log in single transaction
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var createdUser = await _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync(u => u.Id == user.Id);

            var roles = createdUser?.UserRoles.Select(ur => ur.Role.Name).ToList() ?? new List<string>();
            var userDto = new UserDto(user.Id, user.Username, user.Email, user.FirstName, user.LastName, user.Status, roles);

            return Result<UserDto>.Success(userDto);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return Result<UserDto>.Failure("An error occurred while creating the user. Please try again.");
        }
    }
    public async Task<Result<PagedResult<UserDto>>> SearchUsersAsync(
    UserSearchRequest search,
    PaginationRequest pagination)
    {
        try
        {
            var query = _context.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(search.SearchTerm))
            {
                var searchLower = search.SearchTerm.ToLower();
                query = query.Where(u =>
                    u.Username.ToLower().Contains(searchLower) ||
                    u.Email.ToLower().Contains(searchLower) ||
                    (u.FirstName != null && u.FirstName.ToLower().Contains(searchLower)) ||
                    (u.LastName != null && u.LastName.ToLower().Contains(searchLower)));
            }

            // Apply status filter
            if (search.Status.HasValue)
            {
                query = query.Where(u => u.Status == search.Status.Value);
            }

            // Apply role filter
            if (search.RoleId.HasValue)
            {
                query = query.Where(u => u.UserRoles.Any(ur => ur.RoleId == search.RoleId.Value));
            }

            // Apply date filters
            if (search.CreatedAfter.HasValue)
            {
                query = query.Where(u => u.CreatedAt >= search.CreatedAfter.Value);
            }

            if (search.CreatedBefore.HasValue)
            {
                query = query.Where(u => u.CreatedAt <= search.CreatedBefore.Value);
            }

            // Apply sorting
            query = query.ApplySorting(pagination.SortBy ?? "CreatedAt", pagination.SortDescending ?? false);

            // Get total count before pagination
            var totalCount = await query.CountAsync();

            // Apply pagination
            var users = await query
                .Skip(pagination.Skip)
                .Take(pagination.Take)
                .ToListAsync();

            // Map to DTOs
            var userDtos = users.Select(u => new UserDto(
                u.Id,
                u.Username,
                u.Email,
                u.FirstName,
                u.LastName,
                u.Status,
                u.UserRoles.Select(ur => ur.Role.Name).ToList()
            )).ToList();

            var pagedResult = new PagedResult<UserDto>
            {
                Items = userDtos,
                TotalCount = totalCount,
                PageNumber = pagination.Page,
                PageSize = pagination.PageSize
            };

            return Result<PagedResult<UserDto>>.Success(pagedResult);
        }
        catch (Exception ex)
        {
            // Log error here
            return Result<PagedResult<UserDto>>.Failure($"Error searching users: {ex.Message}");
        }
    }

    public async Task<Result<UserDto>> GetUserByIdAsync(Guid id)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return Result<UserDto>.Failure("User not found");
        }

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        var userDto = new UserDto(user.Id, user.Username, user.Email, user.FirstName, user.LastName, user.Status, roles);

        return Result<UserDto>.Success(userDto);
    }

    public async Task<Result<List<UserDto>>> GetAllUsersAsync()
    {
        var users = await _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .ToListAsync();

        var userDtos = users.Select(u => new UserDto(
            u.Id, u.Username, u.Email, u.FirstName, u.LastName, u.Status,
            u.UserRoles.Select(ur => ur.Role.Name).ToList()
        )).ToList();

        return Result<List<UserDto>>.Success(userDtos);
    }

    public async Task<Result> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        var passwordValidation = ValidatePassword(request.NewPassword);
        if (!passwordValidation.IsSuccess)
            return Result.Failure(passwordValidation.ErrorMessage ?? "Invalid password");

        var user = await _context.Users.FindAsync(userId);
        if (user == null || user.IsDeleted)
            return Result.Failure("User not found");

        if (!PasswordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
            return Result.Failure("Current password is incorrect");

        if (request.CurrentPassword == request.NewPassword)
            return Result.Failure("New password must be different from current password");

        user.PasswordHash = PasswordHasher.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        await LogAuditAsync(userId, "PasswordChanged", "User", userId);

        return Result.Success();
    }
    public async Task<Result> AssignRoleAsync(Guid userId, Guid roleId)
    {
        if (!await _context.Users.AnyAsync(u => u.Id == userId))
        {
            return Result.Failure("User not found");
        }

        if (!await _context.Roles.AnyAsync(r => r.Id == roleId))
        {
            return Result.Failure("Role not found");
        }

        if (await _context.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId))
        {
            return Result.Failure("User already has this role");
        }

        var userRole = new UserRole { UserId = userId, RoleId = roleId };
        _context.UserRoles.Add(userRole);
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result> RemoveRoleAsync(Guid userId, Guid roleId)
    {
        var userRole = await _context.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

        if (userRole == null)
        {
            return Result.Failure("User role assignment not found");
        }

        _context.UserRoles.Remove(userRole);
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<List<RoleDto>>> GetAllRolesAsync()
    {
        var roles = await _context.Roles.ToListAsync();
        var roleDtos = roles.Select(r => new RoleDto(r.Id, r.Name, r.Description, r.Permissions)).ToList();
        return Result<List<RoleDto>>.Success(roleDtos);
    }

    public async Task<Result<RoleDto>> CreateRoleAsync(string name, string? description, List<string> permissions)
    {
        if (await _context.Roles.AnyAsync(r => r.Name == name))
        {
            return Result<RoleDto>.Failure("Role name already exists");
        }

        var role = new Role
        {
            Name = name,
            Description = description,
            Permissions = permissions
        };

        _context.Roles.Add(role);
        await _context.SaveChangesAsync();

        var roleDto = new RoleDto(role.Id, role.Name, role.Description, role.Permissions);
        return Result<RoleDto>.Success(roleDto);
    }

    public async Task LogAuditAsync(Guid userId, string action, string? entityType = null, Guid? entityId = null)
    {
        var auditLog = new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId
        };

        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();
    }

    public async Task<Result<UserWithPermissionsDto>> GetCurrentUserAsync(Guid userId)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);

        if (user == null)
            return Result<UserWithPermissionsDto>.Failure("User not found");

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role.Permissions)
            .Distinct()
            .ToList();

        var userDto = new UserWithPermissionsDto(
            user.Id,
            user.Username,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Status,
            roles,
            permissions
        );

        return Result<UserWithPermissionsDto>.Success(userDto);
    }

    public async Task<Result<List<string>>> GetUserPermissionsAsync(Guid userId)
    {
        var user = await _context.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);

        if (user == null)
            return Result<List<string>>.Failure("User not found");

        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role.Permissions)
            .Distinct()
            .ToList();

        return Result<List<string>>.Success(permissions);
    }
}

