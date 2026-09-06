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
/// Represents the project dto data contract.
/// </summary>
public record ProjectDto(Guid Id, string ProjectCode, string Name, string? CustomerName, DateTime StartDate, DateTime? EndDate, decimal BudgetAmount, decimal ActualCost, ProjectStatus Status, decimal CompletionPercentage);
/// <summary>
/// Represents the create project request data contract.
/// </summary>
public record CreateProjectRequest(string ProjectCode, string Name, string? Description, Guid? CustomerId, DateTime StartDate, DateTime? EndDate, decimal BudgetAmount, Guid? ProjectManagerId);
/// <summary>
/// Represents the project task dto data contract.
/// </summary>
public record ProjectTaskDto(Guid Id, string Title, string? AssignedToName, TaskStatus Status, TaskPriority Priority, DateTime? DueDate, decimal EstimatedHours, decimal ActualHours, decimal ProgressPercentage);
/// <summary>
/// Represents the create task request data contract.
/// </summary>
public record CreateTaskRequest(Guid ProjectId, string Title, string? Description, Guid? AssignedToId, Guid? ParentTaskId, TaskPriority Priority, DateTime? StartDate, DateTime? DueDate, decimal EstimatedHours);
/// <summary>
/// Represents the time entry dto data contract.
/// </summary>
public record TimeEntryDto(Guid Id, Guid ProjectId, string ProjectName, Guid? TaskId, string? TaskTitle, DateTime Date, decimal Hours, string? Description, bool IsBillable);
/// <summary>
/// Represents the create time entry request data contract.
/// </summary>
public record CreateTimeEntryRequest(Guid ProjectId, Guid? TaskId, Guid EmployeeId, DateTime Date, decimal Hours, string? Description, bool IsBillable);
/// <summary>
/// Represents the update project request data contract.
/// </summary>
public record UpdateProjectRequest(string? Name, string? Description, DateTime? StartDate, DateTime? EndDate, decimal? BudgetAmount, Guid? ProjectManagerId, string? Priority);
/// <summary>
/// Represents the update task request data contract.
/// </summary>
public record UpdateTaskRequest(string? Title, string? Description, Guid? AssignedToId, TaskPriority? Priority, DateTime? StartDate, DateTime? DueDate, decimal? EstimatedHours);
/// <summary>
/// Represents the update time entry request data contract.
/// </summary>
public record UpdateTimeEntryRequest(DateTime? Date, decimal? Hours, string? Description, bool? IsBillable);
/// <summary>
/// Represents the project member dto data contract.
/// </summary>
public record ProjectMemberDto(Guid Id, Guid EmployeeId, string EmployeeName, string Role, decimal HourlyRate, DateTime AssignedDate);

// Employee Search Request
//public record EmployeeSearchRequest
//{
//    public string? SearchTerm { get; init; }
//    public string? Department { get; init; }
//    public string? Position { get; init; }
//    public bool? IsActive { get; init; }
//    public DateTime? JoinedAfter { get; init; }
//    public DateTime? JoinedBefore { get; init; }
//    public decimal? MinSalary { get; init; }
//    public decimal? MaxSalary { get; init; }
//}

//// Leave Filter Request
//public record LeaveFilterRequest
//{
//    public Guid? EmployeeId { get; init; }
//    public LeaveType? Type { get; init; }
//    public LeaveStatus? Status { get; init; }
//    public DateTime? StartDateFrom { get; init; }
//    public DateTime? StartDateTo { get; init; }
//}

// Project Search Request
/// <summary>
/// Represents the project search request data contract.
/// </summary>
public record ProjectSearchRequest
{
    public string? SearchTerm { get; init; }
    public Guid? CustomerId { get; init; }
    public ProjectStatus? Status { get; init; }
    public Guid? ProjectManagerId { get; init; }
    public DateTime? StartDateFrom { get; init; }
    public DateTime? StartDateTo { get; init; }
    public decimal? MinBudget { get; init; }
    public decimal? MaxBudget { get; init; }
}



