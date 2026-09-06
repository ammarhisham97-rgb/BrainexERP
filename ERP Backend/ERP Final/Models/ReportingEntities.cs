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


// Domain Models
/// <summary>
/// Represents the report domain model.
/// </summary>
public class Report : BaseEntity
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required, MaxLength(100)]
    public string Module { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string ReportType { get; set; } = string.Empty;

    public string? QueryDefinition { get; set; }
    public string? Parameters { get; set; }

    public bool IsPublic { get; set; } = false;

    public Guid? CreatedByUserId { get; set; }
}

/// <summary>
/// Represents the dashboard domain model.
/// </summary>
public class Dashboard : BaseEntity
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public Guid UserId { get; set; }

    public string? Layout { get; set; }
    public bool IsDefault { get; set; } = false;

    // Relationships
    public List<DashboardWidget> Widgets { get; set; } = new();
}

/// <summary>
/// Represents the dashboard widget domain model.
/// </summary>
public class DashboardWidget : BaseEntity
{
    public Guid DashboardId { get; set; }
    public Dashboard Dashboard { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string WidgetType { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? DataSource { get; set; }

    public string? Configuration { get; set; }

    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public int RefreshInterval { get; set; } = 300;
}

/// <summary>
/// Represents the report schedule domain model.
/// </summary>
public class ReportSchedule : BaseEntity
{
    public Guid ReportId { get; set; }
    public Report Report { get; set; } = null!;

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Frequency { get; set; } = string.Empty;

    public string? CronExpression { get; set; }

    public List<string> Recipients { get; set; } = new();

    [MaxLength(100)]
    public string Format { get; set; } = "PDF";

    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }

    public bool IsActive { get; set; } = true;
}

