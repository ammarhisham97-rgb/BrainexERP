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

// ATS DTOs
/// <summary>
/// Represents the job posting dto data contract.
/// </summary>
public record JobPostingDto(Guid Id, string Title, string? Department, string? Location, JobPostingStatus Status, DateTime? PublishedAt, DateTime? ClosedAt);
/// <summary>
/// Represents the create job posting request data contract.
/// </summary>
public record CreateJobPostingRequest(string Title, string? Department, string? Location, string? Description, string? Requirements, decimal? MinSalary, decimal? MaxSalary);
/// <summary>
/// Represents the candidate dto data contract.
/// </summary>
public record CandidateDto(Guid Id, Guid JobPostingId, string FirstName, string LastName, string Email, string? PhoneNumber, string? ResumeUrl, string? ResumeContent, CandidateStatus Status, DateTime AppliedAt);
/// <summary>
/// Represents the apply for job request data contract.
/// </summary>
public record ApplyForJobRequest(Guid JobPostingId, string FirstName, string LastName, string Email, string? PhoneNumber, string? ResumeUrl, string? ResumeContent, string? CoverLetter);
/// <summary>
/// Represents the interview dto data contract.
/// </summary>
public record InterviewDto(Guid Id, Guid CandidateId, string CandidateName, Guid InterviewerId, string InterviewerName, DateTime ScheduledAt, int DurationMinutes, string? InterviewType, InterviewStatus Status);
/// <summary>
/// Represents the schedule interview request data contract.
/// </summary>
public record ScheduleInterviewRequest(Guid CandidateId, Guid InterviewerId, DateTime ScheduledAt, int DurationMinutes, string? InterviewType);
/// <summary>
/// Represents the update job posting request data contract.
/// </summary>
public record UpdateJobPostingRequest(string Title, string? Department, string? Location, string? Description, string? Requirements, decimal? MinSalary, decimal? MaxSalary);
/// <summary>
/// Represents the update feedback request data contract.
/// </summary>
public record UpdateFeedbackRequest(int Rating, string Feedback);

// Resume Screening & Matching DTOs
/// <summary>
/// Represents the resume match result dto data contract.
/// </summary>
public record ResumeMatchResultDto(
    Guid Id,
    Guid JobPostingId,
    Guid CandidateId,
    string CandidateName,
    decimal SimilarityScore,
    int Rank,
    string Domain,
    DateTime MatchedAt
);

/// <summary>
/// Represents the resume screening batch dto data contract.
/// </summary>
public record ResumeScreeningBatchDto(
    Guid Id,
    Guid JobPostingId,
    int TotalCandidatesMatched,
    string? Notes,
    DateTime ScreenedAt,
    List<ResumeMatchResultDto>? MatchResults
);

/// <summary>
/// Represents the save resume screening request data contract.
/// </summary>
public record SaveResumeScreeningRequest(
    Guid JobPostingId,
    List<Guid> CandidateIds,
    string? Notes
);

// HR DTOs
/// <summary>
/// Represents the employee dto data contract.
/// </summary>
public record EmployeeDto(Guid Id, string EmployeeCode, string FirstName, string LastName, string? Email, string? Department, string? Position, decimal BaseSalary, bool IsActive);
/// <summary>
/// Represents the create employee request data contract.
/// </summary>
public record CreateEmployeeRequest(
    string EmployeeCode,
    string FirstName,
    string LastName,
    string? Email,
    string? PhoneNumber,
    DateTime? DateOfBirth,
    DateTime JoinDate,
    string? Department,
    string? Position,
    [property: JsonPropertyName("salary")]  // ? Accept "salary" as well
    decimal BaseSalary,
    string? Address
);
/// <summary>
/// Represents the attendance dto data contract.
/// </summary>
public record AttendanceDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    DateTime Date,
    TimeSpan? CheckInTime,
    TimeSpan? CheckOutTime,
    AttendanceStatus Status
);
/// <summary>
/// Represents the leave dto data contract.
/// </summary>
public record LeaveDto(Guid Id, Guid EmployeeId, string? EmployeeName, string? EmployeeCode, LeaveType Type, DateTime StartDate, DateTime EndDate, int TotalDays, string? Reason, LeaveStatus Status, Guid? ApprovedBy, DateTime? ApprovedAt, string? ApprovalNotes);
/// <summary>
/// Represents the payroll dto data contract.
/// </summary>
public record PayrollDto(Guid Id, Guid EmployeeId, string EmployeeName, int Month, int Year, decimal BaseSalary, decimal NetSalary, PayrollStatus Status);
/// <summary>
/// Represents the update employee request data contract.
/// </summary>
public record UpdateEmployeeRequest(string? FirstName, string? LastName, string? Email, string? PhoneNumber, string? Department, string? Position, decimal? BaseSalary, string? Address);
/// <summary>
/// Represents the update attendance request data contract.
/// </summary>
public record UpdateAttendanceRequest(DateTime Date, string? CheckInTime, string? CheckOutTime, AttendanceStatus Status, string? Notes);
/// <summary>
/// Represents the update leave request data contract.
/// </summary>
public record UpdateLeaveRequest(LeaveType Type, DateTime StartDate, DateTime EndDate, string? Reason);
/// <summary>
/// Represents the update payroll request data contract.
/// </summary>
public record UpdatePayrollRequest(decimal Allowances, decimal Deductions, decimal Tax, string? Notes);
// Missing Request DTOs
/// <summary>
/// Represents the create attendance request data contract.
/// </summary>
public record CreateAttendanceRequest(
    Guid EmployeeId,
    DateTime Date,
    string CheckInTime,      // ? Changed to string for better JSON compatibility
    string? CheckOutTime,    // ? Changed to string for better JSON compatibility
    AttendanceStatus Status,
    string? Notes
);

/// <summary>
/// Represents the create leave request data contract.
/// </summary>
public record CreateLeaveRequest(
    Guid EmployeeId,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    LeaveType Type,
    DateTime StartDate,
    DateTime EndDate,
    string? Reason
);

/// <summary>
/// Represents the approve leave request data contract.
/// </summary>
public record ApproveLeaveRequest(
    Guid ApprovedBy,
    string? Notes
);

/// <summary>
/// Represents the reject leave request data contract.
/// </summary>
public record RejectLeaveRequest(
    string? Notes
);

/// <summary>
/// Represents the generate payroll request data contract.
/// </summary>
public record GeneratePayrollRequest(
    Guid EmployeeId,
    int Month,
    int Year
);

// Search/Filter Models
/// <summary>
/// Represents the employee search request data contract.
/// </summary>
public record EmployeeSearchRequest
{
    public string? SearchTerm { get; init; }
    public string? Department { get; init; }
    public string? Position { get; init; }
    public bool? IsActive { get; init; }
    public DateTime? JoinedAfter { get; init; }
    public DateTime? JoinedBefore { get; init; }
    public decimal? MinSalary { get; init; }
    public decimal? MaxSalary { get; init; }
}

/// <summary>
/// Represents the leave filter request data contract.
/// </summary>
public record LeaveFilterRequest
{
    public Guid? EmployeeId { get; init; }
    public LeaveType? Type { get; init; }
    public LeaveStatus? Status { get; init; }
    public DateTime? StartDateFrom { get; init; }
    public DateTime? StartDateTo { get; init; }
}

