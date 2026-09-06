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
/// Represents the employee domain model.
/// </summary>
public class Employee : BaseEntity
{
    [Required, MaxLength(50)]
    public string EmployeeCode { get; set; } = string.Empty;

    public Guid? UserId { get; set; }
    public User? User { get; set; }

    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [EmailAddress, MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    public DateTime DateOfBirth { get; set; }

    public DateTime JoinDate { get; set; }

    public DateTime? LeaveDate { get; set; }

    [MaxLength(100)]
    public string? Department { get; set; }

    [MaxLength(100)]
    public string? Position { get; set; }

    [MaxLength(100)]
    public string? ReportsTo { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BaseSalary { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    public bool IsActive { get; set; } = true;

    // Relationships
    public List<Attendance> Attendances { get; set; } = new();
    public List<Leave> Leaves { get; set; } = new();
    public List<Payroll> Payrolls { get; set; } = new();
    public List<Document> Documents { get; set; } = new();
}

/// <summary>
/// Represents the attendance domain model.
/// </summary>
public class Attendance : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public DateTime Date { get; set; }

    public TimeSpan? CheckInTime { get; set; }
    public TimeSpan? CheckOutTime { get; set; }

    public AttendanceStatus Status { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public TimeSpan? WorkHours { get; set; }
    public TimeSpan? OvertimeHours { get; set; }
}

/// <summary>
/// Represents the leave domain model.
/// </summary>
public class Leave : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public LeaveType Type { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public int TotalDays { get; set; }

    [MaxLength(1000)]
    public string? Reason { get; set; }

    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;

    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }

    [MaxLength(500)]
    public string? ApprovalNotes { get; set; }
}

/// <summary>
/// Represents the payroll domain model.
/// </summary>
public class Payroll : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public int Month { get; set; }
    public int Year { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BaseSalary { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Allowances { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Deductions { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OvertimePay { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Tax { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal NetSalary { get; set; }

    public PayrollStatus Status { get; set; } = PayrollStatus.Draft;

    public DateTime? ProcessedAt { get; set; }
    public DateTime? PaidAt { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// Represents the document domain model.
/// </summary>
public class Document : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    [Required, MaxLength(200)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string DocumentType { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? FilePath { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public long FileSize { get; set; }

    [MaxLength(50)]
    public string? ContentType { get; set; }
}

/// <summary>
/// Represents the job posting domain model.
/// </summary>
public class JobPosting : BaseEntity
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Department { get; set; }

    [MaxLength(100)]
    public string? Location { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(2000)]
    public string? Requirements { get; set; }

    public JobPostingStatus Status { get; set; } = JobPostingStatus.Draft;

    public decimal? MinSalary { get; set; }
    public decimal? MaxSalary { get; set; }

    public DateTime? PublishedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    public List<Candidate> Candidates { get; set; } = new();
}

/// <summary>
/// Represents the candidate domain model.
/// </summary>
public class Candidate : BaseEntity
{
    public Guid JobPostingId { get; set; }
    public JobPosting JobPosting { get; set; } = null!;

    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    [MaxLength(500)]
    public string? ResumeUrl { get; set; }

    [MaxLength(5000)]
    public string? ResumeContent { get; set; }

    [MaxLength(2000)]
    public string? CoverLetter { get; set; }

    public CandidateStatus Status { get; set; } = CandidateStatus.Applied;

    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

    public List<Interview> Interviews { get; set; } = new();
}

/// <summary>
/// Represents the interview domain model.
/// </summary>
public class Interview : BaseEntity
{
    public Guid CandidateId { get; set; }
    public Candidate Candidate { get; set; } = null!;

    public Guid InterviewerId { get; set; }
    public Employee Interviewer { get; set; } = null!;

    public DateTime ScheduledAt { get; set; }
    public int DurationMinutes { get; set; }

    [MaxLength(100)]
    public string? InterviewType { get; set; } // Phone, Video, In-Person

    public InterviewStatus Status { get; set; } = InterviewStatus.Scheduled;

    [MaxLength(2000)]
    public string? Feedback { get; set; }

    public int? Rating { get; set; } // e.g., 1-5
}

// RESUME SCREENING & AI MATCHING MODELS

/// <summary>
/// Represents the resume match result domain model.
/// </summary>
public class ResumeMatchResult : BaseEntity
{
    public Guid JobPostingId { get; set; }
    public JobPosting JobPosting { get; set; } = null!;

    public Guid CandidateId { get; set; }
    public Candidate Candidate { get; set; } = null!;

    [Column(TypeName = "decimal(5,4)")]
    public decimal SimilarityScore { get; set; }  // 0-1 range

    [MaxLength(100)]
    public string Domain { get; set; } = string.Empty;  // Category from Flask

    public int Rank { get; set; }  // 1-5 ranking

    public DateTime MatchedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents the resume screening batch domain model.
/// </summary>
public class ResumeScreeningBatch : BaseEntity
{
    public Guid JobPostingId { get; set; }
    public JobPosting JobPosting { get; set; } = null!;

    public int TotalCandidatesMatched { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public DateTime ScreenedAt { get; set; } = DateTime.UtcNow;

    public List<ResumeMatchResult> MatchResults { get; set; } = new();
}

