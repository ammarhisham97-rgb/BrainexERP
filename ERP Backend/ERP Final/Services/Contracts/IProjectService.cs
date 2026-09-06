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
/// Defines the i project service service contract.
/// </summary>
public interface IProjectService
{
    Task<Result<ProjectDto>> CreateProjectAsync(CreateProjectRequest request);
    Task<Result<List<ProjectDto>>> GetAllProjectsAsync(ProjectStatus? status = null);
    Task<Result<ProjectDto>> GetProjectByIdAsync(Guid id);
    Task<Result> UpdateProjectStatusAsync(Guid projectId, ProjectStatus status);
    Task<Result> AssignProjectMemberAsync(Guid projectId, Guid employeeId, string role, decimal hourlyRate);
    Task<Result<ProjectTaskDto>> CreateTaskAsync(CreateTaskRequest request);
    Task<Result<List<ProjectTaskDto>>> GetProjectTasksAsync(Guid projectId);
    Task<Result> UpdateTaskStatusAsync(Guid taskId, TaskStatus status);
    Task<Result> UpdateTaskProgressAsync(Guid taskId, decimal progressPercentage);
    Task<Result<TimeEntryDto>> LogTimeEntryAsync(CreateTimeEntryRequest request);
    Task<Result<List<TimeEntryDto>>> GetTimeEntriesAsync(Guid projectId, DateTime? startDate = null, DateTime? endDate = null);
    Task<Result<Dictionary<string, object>>> GetProjectSummaryAsync(Guid projectId);
    Task<Result<PagedResult<ProjectDto>>> SearchProjectsAsync(ProjectSearchRequest search, PaginationRequest pagination);
    Task<Result<ProjectDto>> UpdateProjectAsync(Guid id, UpdateProjectRequest request);
    Task<Result> DeleteProjectAsync(Guid id);
    Task<Result<List<ProjectMemberDto>>> GetProjectMembersAsync(Guid projectId);
    Task<Result> RemoveProjectMemberAsync(Guid projectId, Guid memberId);
    Task<Result<ProjectTaskDto>> GetTaskByIdAsync(Guid id);
    Task<Result<ProjectTaskDto>> UpdateTaskAsync(Guid id, UpdateTaskRequest request);
    Task<Result> DeleteTaskAsync(Guid id);
    Task<Result<TimeEntryDto>> GetTimeEntryByIdAsync(Guid id);
    Task<Result<TimeEntryDto>> UpdateTimeEntryAsync(Guid id, UpdateTimeEntryRequest request);
    Task<Result> DeleteTimeEntryAsync(Guid id);
    Task<Result> ApproveTimeEntryAsync(Guid id, Guid approvedBy);
}



