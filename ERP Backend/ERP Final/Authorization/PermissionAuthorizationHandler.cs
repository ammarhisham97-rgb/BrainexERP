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
/// Represents a permission required to authorize an operation.
/// </summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }

    public PermissionRequirement(string permission)
    {
        Permission = permission ?? throw new ArgumentNullException(nameof(permission));
    }
}

/// <summary>
/// Authorization handler that checks if user has required permission with hierarchical checking
/// </summary>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var permissionsClaim = context.User.FindFirst("permissions")?.Value;
        if (string.IsNullOrEmpty(permissionsClaim))
            return Task.CompletedTask;

        var userPermissions = JsonSerializer.Deserialize<List<string>>(permissionsClaim) ?? new List<string>();

        if (HasPermission(userPermissions, requirement.Permission))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }

    private bool HasPermission(List<string> userPermissions, string requiredPermission)
    {
        // 1. Check "all" super permission
        if (userPermissions.Any(p => p.Equals("all", StringComparison.OrdinalIgnoreCase)))
            return true;

        // 2. Exact match
        if (userPermissions.Any(p => p.Equals(requiredPermission, StringComparison.OrdinalIgnoreCase)))
            return true;

        // Parse permission
        var parts = requiredPermission.Split('.');
        if (parts.Length < 2) return false;

        var module = parts[0];
        var fullAction = string.Join(".", parts.Skip(1));
        var actionParts = fullAction.Split('_');
        var baseAction = actionParts[0];
        var resource = actionParts.Length > 1 ? string.Join("_", actionParts.Skip(1)) : null;

        // 3. Check module.manage_resource (e.g., hr.manage_employees covers hr.view_employees)
        if (!string.IsNullOrEmpty(resource))
        {
            var managePermission = $"{module}.manage_{resource}";
            if (userPermissions.Any(p => p.Equals(managePermission, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        // 4. Check broad module.action (e.g., hr.view covers hr.view_employees, hr.view_attendance)
        var broadPermission = $"{module}.{baseAction}";
        if (userPermissions.Any(p => p.Equals(broadPermission, StringComparison.OrdinalIgnoreCase)))
            return true;

        // 5. Check read/write mapping
        var readActions = new[] { "view", "get", "list", "search", "read" };
        if (readActions.Contains(baseAction, StringComparer.OrdinalIgnoreCase))
        {
            var readPermission = $"{module}.read";
            if (userPermissions.Any(p => p.Equals(readPermission, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        var writeActions = new[] { "create", "edit", "update", "delete", "manage", "process",
                                   "approve", "reject", "change", "assign", "remove", "post",
                                   "cancel", "generate" };
        if (writeActions.Contains(baseAction, StringComparer.OrdinalIgnoreCase))
        {
            var writePermission = $"{module}.write";
            if (userPermissions.Any(p => p.Equals(writePermission, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        // 6. Check full module access
        var fullAccessPatterns = new[] { $"{module}.manage", $"{module}.full_access", $"{module}.admin" };
        if (fullAccessPatterns.Any(pattern =>
            userPermissions.Any(p => p.Equals(pattern, StringComparison.OrdinalIgnoreCase))))
            return true;

        return false;
    }
}


