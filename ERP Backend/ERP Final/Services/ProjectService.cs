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
/// Represents the project service domain model.
/// </summary>
public class ProjectService : IProjectService
{
    private readonly ERPDbContext _context;

    public ProjectService(ERPDbContext context)
    {
        _context = context;
    }
    public async Task<Result<PagedResult<ProjectDto>>> SearchProjectsAsync(
    ProjectSearchRequest search,
    PaginationRequest pagination)
    {
        try
        {
            var query = _context.Projects
                .Include(p => p.Customer)
                .AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(search.SearchTerm))
            {
                var searchLower = search.SearchTerm.ToLower();
                query = query.Where(p =>
                    p.ProjectCode.ToLower().Contains(searchLower) ||
                    p.Name.ToLower().Contains(searchLower) ||
                    (p.Description != null && p.Description.ToLower().Contains(searchLower)));
            }

            // Apply customer filter
            if (search.CustomerId.HasValue)
            {
                query = query.Where(p => p.CustomerId == search.CustomerId.Value);
            }

            // Apply status filter
            if (search.Status.HasValue)
            {
                query = query.Where(p => p.Status == search.Status.Value);
            }

            // Apply project manager filter
            if (search.ProjectManagerId.HasValue)
            {
                query = query.Where(p => p.ProjectManagerId == search.ProjectManagerId.Value);
            }

            // Apply start date filters
            if (search.StartDateFrom.HasValue)
            {
                query = query.Where(p => p.StartDate >= search.StartDateFrom.Value);
            }

            if (search.StartDateTo.HasValue)
            {
                query = query.Where(p => p.StartDate <= search.StartDateTo.Value);
            }


            if (search.MinBudget.HasValue)
            {
                query = query.Where(p => p.BudgetAmount >= search.MinBudget.Value);
            }

            if (search.MaxBudget.HasValue)
            {
                query = query.Where(p => p.BudgetAmount <= search.MaxBudget.Value);
            }


            query = query.ApplySorting(pagination.SortBy ?? "StartDate", pagination.SortDescending ?? false);


            var totalCount = await query.CountAsync();


            var projects = await query
                .Skip(pagination.Skip)
                .Take(pagination.Take)
                .Select(p => new ProjectDto(
                    p.Id,
                    p.ProjectCode,
                    p.Name,
                    p.Customer != null ? p.Customer.Name : null,
                    p.StartDate,
                    p.EndDate,
                    p.BudgetAmount,
                    p.ActualCost,
                    p.Status,
                    p.CompletionPercentage
                ))
                .ToListAsync();

            var pagedResult = new PagedResult<ProjectDto>
            {
                Items = projects,
                TotalCount = totalCount,
                PageNumber = pagination.Page,
                PageSize = pagination.PageSize
            };

            return Result<PagedResult<ProjectDto>>.Success(pagedResult);
        }
        catch (Exception ex)
        {
            return Result<PagedResult<ProjectDto>>.Failure($"Error searching projects: {ex.Message}");
        }
    }
    public async Task<Result<ProjectDto>> CreateProjectAsync(CreateProjectRequest request)
    {
        if (await _context.Projects.AnyAsync(p => p.ProjectCode == request.ProjectCode))
        {
            return Result<ProjectDto>.Failure("Project code already exists");
        }

        var project = new Project
        {
            ProjectCode = request.ProjectCode,
            Name = request.Name,
            Description = request.Description,
            CustomerId = request.CustomerId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            BudgetAmount = request.BudgetAmount,
            ActualCost = 0,
            ProjectManagerId = request.ProjectManagerId,
            Status = ProjectStatus.Planning,
            CompletionPercentage = 0
        };

        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        var customer = request.CustomerId.HasValue ? await _context.Customers.FindAsync(request.CustomerId.Value) : null;
        var dto = new ProjectDto(project.Id, project.ProjectCode, project.Name, customer?.Name,
            project.StartDate, project.EndDate, project.BudgetAmount, project.ActualCost,
            project.Status, project.CompletionPercentage);

        return Result<ProjectDto>.Success(dto);
    }

    public async Task<Result<List<ProjectDto>>> GetAllProjectsAsync(ProjectStatus? status = null)
    {
        var query = _context.Projects.Include(p => p.Customer).AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        var projects = await query.ToListAsync();

        var dtos = projects.Select(p => new ProjectDto(
            p.Id, p.ProjectCode, p.Name, p.Customer?.Name, p.StartDate, p.EndDate,
            p.BudgetAmount, p.ActualCost, p.Status, p.CompletionPercentage
        )).ToList();

        return Result<List<ProjectDto>>.Success(dtos);
    }

    public async Task<Result<ProjectDto>> GetProjectByIdAsync(Guid id)
    {
        var project = await _context.Projects
            .Include(p => p.Customer)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project == null)
        {
            return Result<ProjectDto>.Failure("Project not found");
        }

        var dto = new ProjectDto(project.Id, project.ProjectCode, project.Name, project.Customer?.Name,
            project.StartDate, project.EndDate, project.BudgetAmount, project.ActualCost,
            project.Status, project.CompletionPercentage);

        return Result<ProjectDto>.Success(dto);
    }

    public async Task<Result> UpdateProjectStatusAsync(Guid projectId, ProjectStatus status)
    {
        var project = await _context.Projects.FindAsync(projectId);
        if (project == null)
        {
            return Result.Failure("Project not found");
        }

        project.Status = status;

        if (status == ProjectStatus.Completed)
        {
            project.ActualEndDate = DateTime.UtcNow;
            project.CompletionPercentage = 100;
        }

        await _context.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> AssignProjectMemberAsync(Guid projectId, Guid employeeId, string role, decimal hourlyRate)
    {
        var project = await _context.Projects.FindAsync(projectId);
        if (project == null)
        {
            return Result.Failure("Project not found");
        }

        var employee = await _context.Employees.FindAsync(employeeId);
        if (employee == null)
        {
            return Result.Failure("Employee not found");
        }

        if (await _context.ProjectMembers.AnyAsync(pm => pm.ProjectId == projectId && pm.EmployeeId == employeeId && pm.RemovedDate == null))
        {
            return Result.Failure("Employee is already assigned to this project");
        }

        var projectMember = new ProjectMember
        {
            ProjectId = projectId,
            EmployeeId = employeeId,
            Role = role,
            HourlyRate = hourlyRate,
            AssignedDate = DateTime.UtcNow
        };

        _context.ProjectMembers.Add(projectMember);
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<ProjectTaskDto>> CreateTaskAsync(CreateTaskRequest request)
    {
        var project = await _context.Projects.FindAsync(request.ProjectId);
        if (project == null)
        {
            return Result<ProjectTaskDto>.Failure("Project not found");
        }

        var task = new ProjectTask
        {
            ProjectId = request.ProjectId,
            Title = request.Title,
            Description = request.Description,
            AssignedToId = request.AssignedToId,
            ParentTaskId = request.ParentTaskId,
            Status = TaskStatus.Todo,
            Priority = request.Priority,
            StartDate = request.StartDate,
            DueDate = request.DueDate,
            EstimatedHours = request.EstimatedHours,
            ActualHours = 0,
            ProgressPercentage = 0
        };

        _context.ProjectTasks.Add(task);
        await _context.SaveChangesAsync();

        var assignedTo = request.AssignedToId.HasValue ? await _context.Employees.FindAsync(request.AssignedToId.Value) : null;
        var dto = new ProjectTaskDto(task.Id, task.Title, assignedTo != null ? $"{assignedTo.FirstName} {assignedTo.LastName}" : null,
            task.Status, task.Priority, task.DueDate, task.EstimatedHours, task.ActualHours, task.ProgressPercentage);

        return Result<ProjectTaskDto>.Success(dto);
    }

    public async Task<Result<List<ProjectTaskDto>>> GetProjectTasksAsync(Guid projectId)
    {
        var tasks = await _context.ProjectTasks
            .Include(t => t.AssignedTo)
            .Where(t => t.ProjectId == projectId)
            .ToListAsync();

        var dtos = tasks.Select(t => new ProjectTaskDto(
            t.Id, t.Title, t.AssignedTo != null ? $"{t.AssignedTo.FirstName} {t.AssignedTo.LastName}" : null,
            t.Status, t.Priority, t.DueDate, t.EstimatedHours, t.ActualHours, t.ProgressPercentage
        )).ToList();

        return Result<List<ProjectTaskDto>>.Success(dtos);
    }

    public async Task<Result> UpdateTaskStatusAsync(Guid taskId, TaskStatus status)
    {
        var task = await _context.ProjectTasks.FindAsync(taskId);
        if (task == null)
        {
            return Result.Failure("Task not found");
        }

        task.Status = status;

        if (status == TaskStatus.Done)
        {
            task.CompletedAt = DateTime.UtcNow;
            task.ProgressPercentage = 100;
        }

        await _context.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> UpdateTaskProgressAsync(Guid taskId, decimal progressPercentage)
    {
        var task = await _context.ProjectTasks.FindAsync(taskId);
        if (task == null)
        {
            return Result.Failure("Task not found");
        }

        task.ProgressPercentage = progressPercentage;

        if (progressPercentage == 100 && task.Status != TaskStatus.Done)
        {
            task.Status = TaskStatus.Done;
            task.CompletedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result<TimeEntryDto>> LogTimeEntryAsync(CreateTimeEntryRequest request)
    {
        var project = await _context.Projects.FindAsync(request.ProjectId);
        if (project == null)
        {
            return Result<TimeEntryDto>.Failure("Project not found");
        }

        var employee = await _context.Employees.FindAsync(request.EmployeeId);
        if (employee == null)
        {
            return Result<TimeEntryDto>.Failure("Employee not found");
        }

        var projectMember = await _context.ProjectMembers
            .FirstOrDefaultAsync(pm => pm.ProjectId == request.ProjectId && pm.EmployeeId == request.EmployeeId && pm.RemovedDate == null);

        var hourlyRate = projectMember?.HourlyRate ?? 0;

        var timeEntry = new TimeEntry
        {
            ProjectId = request.ProjectId,
            TaskId = request.TaskId,
            EmployeeId = request.EmployeeId,
            Date = request.Date,
            Hours = request.Hours,
            Description = request.Description,
            IsBillable = request.IsBillable,
            HourlyRate = hourlyRate,
            TotalCost = request.Hours * hourlyRate,
            IsApproved = false
        };

        _context.TimeEntries.Add(timeEntry);


        if (request.TaskId.HasValue)
        {
            var task = await _context.ProjectTasks.FindAsync(request.TaskId.Value);
            if (task != null)
            {
                task.ActualHours += request.Hours;
            }
        }


        project.ActualCost += timeEntry.TotalCost;

        await _context.SaveChangesAsync();

        var task2 = request.TaskId.HasValue ? await _context.ProjectTasks.FindAsync(request.TaskId.Value) : null;
        var dto = new TimeEntryDto(timeEntry.Id, timeEntry.ProjectId, project.Name, timeEntry.TaskId,
            task2?.Title, timeEntry.Date, timeEntry.Hours, timeEntry.Description, timeEntry.IsBillable);

        return Result<TimeEntryDto>.Success(dto);
    }

    public async Task<Result<List<TimeEntryDto>>> GetTimeEntriesAsync(Guid projectId, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _context.TimeEntries
            .Include(te => te.Project)
            .Include(te => te.Task)
            .Where(te => te.ProjectId == projectId);

        if (startDate.HasValue)
        {
            query = query.Where(te => te.Date >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(te => te.Date <= endDate.Value);
        }

        var timeEntries = await query.ToListAsync();

        var dtos = timeEntries.Select(te => new TimeEntryDto(
            te.Id, te.ProjectId, te.Project.Name, te.TaskId, te.Task?.Title,
            te.Date, te.Hours, te.Description, te.IsBillable
        )).ToList();

        return Result<List<TimeEntryDto>>.Success(dtos);
    }

    public async Task<Result<Dictionary<string, object>>> GetProjectSummaryAsync(Guid projectId)
    {
        var project = await _context.Projects
            .Include(p => p.Tasks)
            .Include(p => p.TimeEntries)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null)
        {
            return Result<Dictionary<string, object>>.Failure("Project not found");
        }

        var totalTasks = project.Tasks.Count;
        var completedTasks = project.Tasks.Count(t => t.Status == TaskStatus.Done);
        var totalHours = project.TimeEntries.Sum(te => te.Hours);
        var totalCost = project.ActualCost;
        var budgetVariance = project.BudgetAmount - totalCost;

        var summary = new Dictionary<string, object>
        {
            ["ProjectName"] = project.Name,
            ["Status"] = project.Status.ToString(),
            ["TotalTasks"] = totalTasks,
            ["CompletedTasks"] = completedTasks,
            ["CompletionPercentage"] = project.CompletionPercentage,
            ["TotalHours"] = totalHours,
            ["BudgetAmount"] = project.BudgetAmount,
            ["ActualCost"] = totalCost,
            ["BudgetVariance"] = budgetVariance
        };

        return Result<Dictionary<string, object>>.Success(summary);
    }
    public async Task<Result<ProjectDto>> UpdateProjectAsync(Guid id, UpdateProjectRequest request)
    {
        var project = await _context.Projects
            .Include(p => p.Customer)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project == null)
            return Result<ProjectDto>.Failure("Project not found");

        if (request.Name != null) project.Name = request.Name;
        if (request.Description != null) project.Description = request.Description;
        if (request.StartDate.HasValue) project.StartDate = request.StartDate.Value;
        if (request.EndDate.HasValue) project.EndDate = request.EndDate;
        if (request.BudgetAmount.HasValue) project.BudgetAmount = request.BudgetAmount.Value;
        if (request.ProjectManagerId.HasValue) project.ProjectManagerId = request.ProjectManagerId;
        if (request.Priority != null) project.Priority = request.Priority;

        project.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var dto = new ProjectDto(project.Id, project.ProjectCode, project.Name, project.Customer?.Name,
            project.StartDate, project.EndDate, project.BudgetAmount, project.ActualCost,
            project.Status, project.CompletionPercentage);

        return Result<ProjectDto>.Success(dto);
    }

    public async Task<Result> DeleteProjectAsync(Guid id)
    {
        var project = await _context.Projects.FindAsync(id);
        if (project == null)
            return Result.Failure("Project not found");

        if (project.Status == ProjectStatus.InProgress)
            return Result.Failure("Cannot delete projects in progress");

        project.IsDeleted = true;
        project.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<List<ProjectMemberDto>>> GetProjectMembersAsync(Guid projectId)
    {
        var members = await _context.ProjectMembers
            .Include(pm => pm.Employee)
            .Where(pm => pm.ProjectId == projectId && pm.RemovedDate == null)
            .ToListAsync();

        var dtos = members.Select(pm => new ProjectMemberDto(
            pm.Id,
            pm.EmployeeId,
            $"{pm.Employee.FirstName} {pm.Employee.LastName}",
            pm.Role,
            pm.HourlyRate,
            pm.AssignedDate
        )).ToList();

        return Result<List<ProjectMemberDto>>.Success(dtos);
    }

    public async Task<Result> RemoveProjectMemberAsync(Guid projectId, Guid memberId)
    {
        var member = await _context.ProjectMembers
            .FirstOrDefaultAsync(pm => pm.Id == memberId && pm.ProjectId == projectId);

        if (member == null)
            return Result.Failure("Project member not found");

        member.RemovedDate = DateTime.UtcNow;
        member.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<ProjectTaskDto>> GetTaskByIdAsync(Guid id)
    {
        var task = await _context.ProjectTasks
            .Include(t => t.AssignedTo)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null)
            return Result<ProjectTaskDto>.Failure("Task not found");

        var dto = new ProjectTaskDto(task.Id, task.Title,
            task.AssignedTo != null ? $"{task.AssignedTo.FirstName} {task.AssignedTo.LastName}" : null,
            task.Status, task.Priority, task.DueDate, task.EstimatedHours, task.ActualHours, task.ProgressPercentage);

        return Result<ProjectTaskDto>.Success(dto);
    }

    public async Task<Result<ProjectTaskDto>> UpdateTaskAsync(Guid id, UpdateTaskRequest request)
    {
        var task = await _context.ProjectTasks
            .Include(t => t.AssignedTo)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null)
            return Result<ProjectTaskDto>.Failure("Task not found");

        if (request.Title != null) task.Title = request.Title;
        if (request.Description != null) task.Description = request.Description;
        if (request.AssignedToId.HasValue) task.AssignedToId = request.AssignedToId;
        if (request.Priority.HasValue) task.Priority = request.Priority.Value;
        if (request.StartDate.HasValue) task.StartDate = request.StartDate;
        if (request.DueDate.HasValue) task.DueDate = request.DueDate;
        if (request.EstimatedHours.HasValue) task.EstimatedHours = request.EstimatedHours.Value;

        task.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var dto = new ProjectTaskDto(task.Id, task.Title,
            task.AssignedTo != null ? $"{task.AssignedTo.FirstName} {task.AssignedTo.LastName}" : null,
            task.Status, task.Priority, task.DueDate, task.EstimatedHours, task.ActualHours, task.ProgressPercentage);

        return Result<ProjectTaskDto>.Success(dto);
    }

    public async Task<Result> DeleteTaskAsync(Guid id)
    {
        var task = await _context.ProjectTasks.FindAsync(id);
        if (task == null)
            return Result.Failure("Task not found");

        if (task.Status == TaskStatus.Done)
            return Result.Failure("Cannot delete completed tasks");

        task.IsDeleted = true;
        task.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<TimeEntryDto>> GetTimeEntryByIdAsync(Guid id)
    {
        var timeEntry = await _context.TimeEntries
            .Include(te => te.Project)
            .Include(te => te.Task)
            .FirstOrDefaultAsync(te => te.Id == id);

        if (timeEntry == null)
            return Result<TimeEntryDto>.Failure("Time entry not found");

        var dto = new TimeEntryDto(timeEntry.Id, timeEntry.ProjectId, timeEntry.Project.Name,
            timeEntry.TaskId, timeEntry.Task?.Title, timeEntry.Date, timeEntry.Hours,
            timeEntry.Description, timeEntry.IsBillable);

        return Result<TimeEntryDto>.Success(dto);
    }

    public async Task<Result<TimeEntryDto>> UpdateTimeEntryAsync(Guid id, UpdateTimeEntryRequest request)
    {
        var timeEntry = await _context.TimeEntries
            .Include(te => te.Project)
            .Include(te => te.Task)
            .FirstOrDefaultAsync(te => te.Id == id);

        if (timeEntry == null)
            return Result<TimeEntryDto>.Failure("Time entry not found");

        if (timeEntry.IsApproved)
            return Result<TimeEntryDto>.Failure("Cannot update approved time entries");

        var oldHours = timeEntry.Hours;

        if (request.Date.HasValue) timeEntry.Date = request.Date.Value;
        if (request.Hours.HasValue) timeEntry.Hours = request.Hours.Value;
        if (request.Description != null) timeEntry.Description = request.Description;
        if (request.IsBillable.HasValue) timeEntry.IsBillable = request.IsBillable.Value;


        timeEntry.TotalCost = timeEntry.Hours * timeEntry.HourlyRate;


        if (request.Hours.HasValue && timeEntry.TaskId.HasValue)
        {
            var task = await _context.ProjectTasks.FindAsync(timeEntry.TaskId.Value);
            if (task != null)
            {
                task.ActualHours = task.ActualHours - oldHours + timeEntry.Hours;
            }
        }


        var project = await _context.Projects.FindAsync(timeEntry.ProjectId);
        if (project != null)
        {
            project.ActualCost = project.ActualCost - (oldHours * timeEntry.HourlyRate) + timeEntry.TotalCost;
        }

        timeEntry.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var dto = new TimeEntryDto(timeEntry.Id, timeEntry.ProjectId, timeEntry.Project.Name,
            timeEntry.TaskId, timeEntry.Task?.Title, timeEntry.Date, timeEntry.Hours,
            timeEntry.Description, timeEntry.IsBillable);

        return Result<TimeEntryDto>.Success(dto);
    }

    public async Task<Result> DeleteTimeEntryAsync(Guid id)
    {
        var timeEntry = await _context.TimeEntries
            .Include(te => te.Task)
            .Include(te => te.Project)
            .FirstOrDefaultAsync(te => te.Id == id);

        if (timeEntry == null)
            return Result.Failure("Time entry not found");

        if (timeEntry.IsApproved)
            return Result.Failure("Cannot delete approved time entries");


        if (timeEntry.TaskId.HasValue && timeEntry.Task != null)
        {
            timeEntry.Task.ActualHours -= timeEntry.Hours;
        }


        if (timeEntry.Project != null)
        {
            timeEntry.Project.ActualCost -= timeEntry.TotalCost;
        }

        _context.TimeEntries.Remove(timeEntry);
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result> ApproveTimeEntryAsync(Guid id, Guid approvedBy)
    {
        var timeEntry = await _context.TimeEntries.FindAsync(id);
        if (timeEntry == null)
            return Result.Failure("Time entry not found");

        if (timeEntry.IsApproved)
            return Result.Failure("Time entry is already approved");

        timeEntry.IsApproved = true;
        timeEntry.ApprovedBy = approvedBy;
        timeEntry.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Result.Success();
    }
}

