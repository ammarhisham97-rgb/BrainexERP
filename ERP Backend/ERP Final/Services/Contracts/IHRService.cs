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
/// Defines the i h r service service contract.
/// </summary>
public interface IHRService
{
    Task<Result<EmployeeDto>> CreateEmployeeAsync(CreateEmployeeRequest request);
    Task<Result<List<EmployeeDto>>> GetAllEmployeesAsync();
    Task<Result<EmployeeDto>> GetEmployeeByIdAsync(Guid id);
    Task<Result> MarkAttendanceAsync(Guid employeeId, DateTime date, TimeSpan checkIn, TimeSpan? checkOut, AttendanceStatus status);
    Task<Result<List<AttendanceDto>>> GetEmployeeAttendanceAsync(Guid employeeId, DateTime startDate, DateTime endDate);
    Task<Result<LeaveDto>> RequestLeaveAsync(Guid employeeId, LeaveType type, DateTime startDate, DateTime endDate, string? reason);
    Task<Result> ApproveLeaveAsync(Guid leaveId, Guid approvedBy, string? notes);
    Task<Result> RejectLeaveAsync(Guid leaveId, string? notes);
    Task<Result<PayrollDto>> GeneratePayrollAsync(Guid employeeId, int month, int year);
    Task<Result<List<PayrollDto>>> GetPayrollsAsync(int month, int year);
    Task<Result<PagedResult<EmployeeDto>>> SearchEmployeesAsync(EmployeeSearchRequest search, PaginationRequest pagination);
    Task<Result<PagedResult<LeaveDto>>> SearchLeavesAsync(LeaveFilterRequest filter, PaginationRequest pagination);
    Task<Result<EmployeeDto>> UpdateEmployeeAsync(Guid id, UpdateEmployeeRequest request);
    Task<Result> DeactivateEmployeeAsync(Guid id);
    Task<Result> UpdateAttendanceAsync(Guid id, UpdateAttendanceRequest request);
    Task<Result> DeleteAttendanceAsync(Guid id);
    Task<Result<LeaveDto>> GetLeaveByIdAsync(Guid id);
    Task<Result<LeaveDto>> UpdateLeaveAsync(Guid id, UpdateLeaveRequest request);
    Task<Result> DeleteLeaveAsync(Guid id);
    Task<Result<PayrollDto>> GetPayrollByIdAsync(Guid id);
    Task<Result<PayrollDto>> UpdatePayrollAsync(Guid id, UpdatePayrollRequest request);
    Task<Result> DeletePayrollAsync(Guid id);
    Task<Result> ProcessPayrollAsync(Guid id);

    Task<Result<JobPostingDto>> CreateJobPostingAsync(CreateJobPostingRequest request);
    Task<Result<List<JobPostingDto>>> GetAllJobPostingsAsync();
    Task<Result<CandidateDto>> ApplyForJobAsync(ApplyForJobRequest request);
    Task<Result<List<CandidateDto>>> GetCandidatesByJobAsync(Guid jobPostingId);
    Task<Result<InterviewDto>> ScheduleInterviewAsync(ScheduleInterviewRequest request);
    Task<Result<JobPostingDto>> GetJobPostingByIdAsync(Guid id);
    Task<Result<JobPostingDto>> UpdateJobPostingAsync(Guid id, UpdateJobPostingRequest request);
    Task<Result> UpdateJobPostingStatusAsync(Guid id, JobPostingStatus status);
    Task<Result> DeleteJobPostingAsync(Guid id);
    Task<Result<CandidateDto>> GetCandidateByIdAsync(Guid id);
    Task<Result> UpdateCandidateStatusAsync(Guid id, CandidateStatus status);
    Task<Result> DeleteCandidateAsync(Guid id);
    Task<Result> UpdateInterviewFeedbackAsync(Guid id, int rating, string feedback);
    Task<Result> CancelInterviewAsync(Guid id);
    Task<Result> DeleteInterviewAsync(Guid id);

    Task<Result<HRAttritionResponse>> PredictEmployeeAttritionAsync(Guid employeeId, List<double> features);
    Task<bool> CheckAttritionServiceHealthAsync();

    Task<Result<bool>> SaveResumeScreeningResultsAsync(
        Guid jobPostingId, 
        List<CandidateMatchResult> matches,
        string? notes);

    Task<Result<List<ResumeMatchResultDto>>> GetResumeMatchResultsAsync(Guid jobPostingId);

    Task<Result<List<ResumeMatchResultDto>>> GetCandidateMatchHistoryAsync(Guid candidateId);

    Task<Result<List<ResumeScreeningBatchDto>>> GetScreeningBatchesAsync(Guid jobPostingId);

    Task<Result<bool>> DeleteScreeningBatchAsync(Guid batchId);
}
