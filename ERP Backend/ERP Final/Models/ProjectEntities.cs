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
/// Represents the project domain model.
/// </summary>
public class Project : BaseEntity
{
    [Required, MaxLength(50)]
    public string ProjectCode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? ActualEndDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BudgetAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ActualCost { get; set; }

    public ProjectStatus Status { get; set; } = ProjectStatus.Planning;

    public Guid? ProjectManagerId { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal CompletionPercentage { get; set; }

    [MaxLength(100)]
    public string? Priority { get; set; }

    // Relationships
    public List<ProjectTask> Tasks { get; set; } = new();
    public List<ProjectMember> ProjectMembers { get; set; } = new();
    public List<TimeEntry> TimeEntries { get; set; } = new();
}

/// <summary>
/// Represents the project member domain model.
/// </summary>
public class ProjectMember : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    [MaxLength(100)]
    public string Role { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal HourlyRate { get; set; }

    public DateTime AssignedDate { get; set; }
    public DateTime? RemovedDate { get; set; }
}

/// <summary>
/// Represents the project task domain model.
/// </summary>
public class ProjectTask : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public Guid? AssignedToId { get; set; }
    public Employee? AssignedTo { get; set; }

    public Guid? ParentTaskId { get; set; }
    public ProjectTask? ParentTask { get; set; }

    public TaskStatus Status { get; set; } = TaskStatus.Todo;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimatedHours { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ActualHours { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal ProgressPercentage { get; set; }

    // Relationships
    public List<ProjectTask> SubTasks { get; set; } = new();
    public List<TimeEntry> TimeEntries { get; set; } = new();
    public List<TaskComment> Comments { get; set; } = new();
}

/// <summary>
/// Represents the time entry domain model.
/// </summary>
public class TimeEntry : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public Guid? TaskId { get; set; }
    public ProjectTask? Task { get; set; }

    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public DateTime Date { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Hours { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsBillable { get; set; } = true;

    [Column(TypeName = "decimal(18,2)")]
    public decimal HourlyRate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCost { get; set; }

    public bool IsApproved { get; set; } = false;
    public Guid? ApprovedBy { get; set; }
}

/// <summary>
/// Represents the task comment domain model.
/// </summary>
public class TaskComment : BaseEntity
{
    public Guid TaskId { get; set; }
    public ProjectTask Task { get; set; } = null!;

    public Guid UserId { get; set; }

    [Required, MaxLength(2000)]
    public string Comment { get; set; } = string.Empty;

    public Guid? ParentCommentId { get; set; }
    public TaskComment? ParentComment { get; set; }

    // Relationships
    public List<TaskComment> Replies { get; set; } = new();
}

