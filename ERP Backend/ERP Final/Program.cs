using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IdentityModel.Tokens.Jwt;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Serilog;
using Amazon.IdentityManagement.Model;
using System.Text.Json;
using ERPSystem.Services.Chatbot;
using ERPSystem.Services.HRChatbot;
using ERPSystem.Services.ResumeMatching;


var builder = WebApplication.CreateBuilder(args);

// Register database and application services.
builder.Services.AddDbContext<ERPDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection") ??
        "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=master;Integrated Security=True"));

builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<ILoggerService, LoggerService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IHRService, HRService>();
builder.Services.AddScoped<IFinanceService, FinanceService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IProcurementService, ProcurementService>();
builder.Services.AddScoped<ISalesService, SalesService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IReportingService, ReportingService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();

// Register external AI integrations.
builder.Services.AddHttpClient<IFraudDetectionService, FraudDetectionService>()
    .ConfigureHttpClient(client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["FraudDetection:FlaskApiUrl"] ?? "http://localhost:5003");
        client.Timeout = TimeSpan.FromSeconds(30);
    });

builder.Services.AddHttpClient<IHRAttritionService, HRAttritionService>()
    .ConfigureHttpClient(client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["HRAttrition:FlaskApiUrl"] ?? "http://localhost:5001");
        client.Timeout = TimeSpan.FromSeconds(30);
    });

builder.Services.AddHttpClient<ERPSystem.Services.Chatbot.IChatbotService, ERPSystem.Services.Chatbot.ChatbotService>()
    .ConfigureHttpClient(client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["Chatbot:FlaskApiUrl"] ?? "http://localhost:5005");
        client.Timeout = TimeSpan.FromSeconds(120);
    });

builder.Services.AddHttpClient<ERPSystem.Services.HRChatbot.IHRChatbotService, ERPSystem.Services.HRChatbot.HRChatbotService>()
    .ConfigureHttpClient(client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["HRChatbot:FlaskApiUrl"] ?? "http://localhost:5004");
        client.Timeout = TimeSpan.FromSeconds(120);
    });

builder.Services.AddHttpClient<IResumeMatchingService, ResumeMatchingService>()
    .ConfigureHttpClient(client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["ResumeMatching:FlaskApiUrl"] ?? "http://localhost:5006");
        client.Timeout = TimeSpan.FromSeconds(60);
     });

// Configure authentication and authorization.
var jwtKey = builder.Configuration["Jwt:Key"] ?? "YourSuperSecretKeyForJWTTokenGeneration12345";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ERPSystem";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ERPSystemUsers";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in Permissions.AllPermissions)
    {
        options.AddPolicy(permission, policy =>
            policy.Requirements.Add(new PermissionRequirement(permission)));
    }
});

// Configure cross-origin requests and JSON serialization.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ERP System API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseMiddleware<ExceptionLoggingMiddleware>();

app.UseMiddleware<RequestResponseLoggingMiddleware>();
//app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();



app.MapPost("/api/auth/login", async (LoginRequest request, IUserService userService) =>
{
    var result = await userService.LoginAsync(request);
    return result.IsSuccess
        ? Results.Ok(result.Data)
        : Results.BadRequest(new { error = result.ErrorMessage, success = false });
}).AllowAnonymous();

app.MapPost("/api/auth/register", async (RegisterRequest request, IUserService userService) =>
{
    var result = await userService.RegisterAsync(request);
    return result.IsSuccess
        ? Results.Ok(new { data = result.Data, success = true, message = "Registration successful" })
        : Results.BadRequest(new { error = result.ErrorMessage, success = false });
}).AllowAnonymous();

// GET Current User with Permissions
app.MapGet("/api/auth/me", async (HttpContext httpContext, IUserService userService) =>
{
    var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
    {
        return Results.Unauthorized();
    }

    var result = await userService.GetCurrentUserAsync(userId);
    return result.IsSuccess
        ? Results.Ok(result.Data)
        : Results.NotFound(new { error = result.ErrorMessage });
}).RequireAuthorization();

// GET User Permissions by ID
app.MapGet("/api/users/{id:guid}/permissions", async (Guid id, IUserService userService) =>
{
    var result = await userService.GetUserPermissionsAsync(id);
    return result.IsSuccess
        ? Results.Ok(new { permissions = result.Data })
        : Results.NotFound(new { error = result.ErrorMessage });
}).RequireAuthorization();

// GET All Available Permissions
app.MapGet("/api/permissions", () =>
{
    return Results.Ok(new { permissions = Permissions.AllPermissions });
}).RequireAuthorization();

// GET Role Templates
app.MapGet("/api/roles/templates", () =>
{
    var templates = RoleTemplates.Templates.Select(t => new
    {
        name = t.Key,
        description = t.Value.Description,
        permissions = t.Value.Permissions
    });
    return Results.Ok(templates);
}).RequireAuthorization();

app.MapGet("/api/users/search", async (
    [AsParameters] UserSearchRequest search,
    [AsParameters] PaginationRequest pagination,
    IUserService userService,
    ILoggerService logger) =>
{
    logger.LogInformation("Searching users with term: {SearchTerm}, page: {Page}",
        search.SearchTerm, pagination.Page);

    var result = await userService.SearchUsersAsync(search, pagination);

    if (result.IsSuccess)
    {
        logger.LogInformation("Found {Count} users, page {Page} of {TotalPages}",
            result.Data!.Items.Count, result.Data.PageNumber, result.Data.TotalPages);
    }

    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.UsersView);
app.MapGet("/api/users", async (IUserService userService) =>
{
    var result = await userService.GetAllUsersAsync();
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.UsersView);
app.MapPost("/api/users", async (CreateUserAdminRequest request, IUserService userService) =>
{
    var result = await userService.CreateUserAdminAsync(request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.UsersCreate);
app.MapGet("/api/users/{id:guid}", async (Guid id, IUserService userService) =>
{
    var result = await userService.GetUserByIdAsync(id);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result.ErrorMessage);
}).RequireAuthorization(Permissions.UsersView);

app.MapPost("/api/users/{userId:guid}/change-password", async (Guid userId, ChangePasswordRequest request, IUserService userService) =>
{
    var result = await userService.ChangePasswordAsync(userId, request);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.UsersChangePassword);

app.MapPost("/api/users/{userId:guid}/roles/{roleId:guid}", async (Guid userId, Guid roleId, IUserService userService) =>
{
    var result = await userService.AssignRoleAsync(userId, roleId);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.UsersManageRoles);

app.MapPost("/api/users/{userId:guid}/roles/{roleName}", async (Guid userId, string roleName, IUserService userService, ERPDbContext context) =>
{
    // Look up role by name
    var role = await context.Roles.FirstOrDefaultAsync(r => r.Name.ToLower() == roleName.ToLower());

    if (role == null)
    {
        return Results.BadRequest(new { error = $"Role '{roleName}' not found", success = false });
    }

    var result = await userService.AssignRoleAsync(userId, role.Id);
    return result.IsSuccess
        ? Results.Ok(new { success = true, message = $"Role '{roleName}' assigned successfully" })
        : Results.BadRequest(new { error = result.ErrorMessage, success = false });
}).RequireAuthorization(Permissions.UsersManageRoles);

app.MapDelete("/api/users/{userId:guid}/roles/{roleId:guid}", async (Guid userId, Guid roleId, IUserService userService) =>
{
    var result = await userService.RemoveRoleAsync(userId, roleId);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.UsersManageRoles);

app.MapDelete("/api/users/{userId:guid}/roles/{roleName}", async (Guid userId, string roleName, IUserService userService, ERPDbContext context) =>
{
    var role = await context.Roles.FirstOrDefaultAsync(r => r.Name.ToLower() == roleName.ToLower());

    if (role == null)
    {
        return Results.BadRequest(new { error = $"Role '{roleName}' not found", success = false });
    }

    var result = await userService.RemoveRoleAsync(userId, role.Id);
    return result.IsSuccess
        ? Results.Ok(new { success = true, message = $"Role '{roleName}' removed successfully" })
        : Results.BadRequest(new { error = result.ErrorMessage, success = false });
}).RequireAuthorization(Permissions.UsersManageRoles);

app.MapGet("/api/roles", async (IUserService userService) =>
{
    var result = await userService.GetAllRolesAsync();
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.RolesView);

app.MapPost("/api/roles", async (CreateRoleRequest request, IUserService userService) =>
{
    var result = await userService.CreateRoleAsync(request.Name, request.Description, request.Permissions);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.RolesCreate);
app.MapPut("/api/users/{id:guid}", async (Guid id, UpdateUserRequest request, IUserService userService) =>
{
    var result = await userService.UpdateUserAsync(id, request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.UsersEdit);

app.MapDelete("/api/users/{id:guid}", async (Guid id, IUserService userService) =>
{
    var result = await userService.DeactivateUserAsync(id);
    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.UsersDelete);

app.MapPut("/api/roles/{id:guid}", async (Guid id, UpdateRoleRequest request, IUserService userService) =>
{
    var result = await userService.UpdateRoleAsync(id, request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.RolesEdit);

app.MapDelete("/api/roles/{id:guid}", async (Guid id, IUserService userService) =>
{
    var result = await userService.DeleteRoleAsync(id);
    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.RolesDelete);

app.MapGet("/api/roles/{id:guid}", async (Guid id, IUserService userService) =>
{
    var result = await userService.GetRoleByIdAsync(id);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result.ErrorMessage);
}).RequireAuthorization(Permissions.RolesView);




app.MapPost("/api/employees", async (CreateEmployeeRequest request, IHRService hrService) =>
{
    var result = await hrService.CreateEmployeeAsync(request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.HRCreateEmployees);

app.MapGet("/api/employees", async (IHRService hrService) =>
{
    var result = await hrService.GetAllEmployeesAsync();
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.HRViewEmployees);


app.MapGet("/api/employees/search", async (
    [AsParameters] EmployeeSearchRequest search,
    [AsParameters] PaginationRequest pagination,
    IHRService hrService,
    ILoggerService logger) =>
{
    logger.LogInformation("Searching employees with term: {SearchTerm}, department: {Department}",
        search.SearchTerm, search.Department);

    var result = await hrService.SearchEmployeesAsync(search, pagination);

    if (result.IsSuccess)
    {
        logger.LogInformation("Found {Count} employees, page {Page} of {TotalPages}",
            result.Data!.Items.Count, result.Data.PageNumber, result.Data.TotalPages);
    }

    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.HRViewEmployees);

app.MapGet("/api/leaves/search", async (
    [AsParameters] LeaveFilterRequest filter,
    [AsParameters] PaginationRequest pagination,
    IHRService hrService,
    ILoggerService logger) =>
{
    logger.LogInformation("Searching leaves with employee: {EmployeeId}, status: {Status}",
        filter.EmployeeId, filter.Status);

    var result = await hrService.SearchLeavesAsync(filter, pagination);

    if (result.IsSuccess)
    {
        logger.LogInformation("Found {Count} leave requests", result.Data!.Items.Count);
    }

    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.HRViewLeave);

app.MapGet("/api/employees/{id:guid}", async (Guid id, IHRService hrService) =>
{
    var result = await hrService.GetEmployeeByIdAsync(id);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result.ErrorMessage);
}).RequireAuthorization(Permissions.HRViewEmployees);

app.MapPost("/api/attendance", async (CreateAttendanceRequest request, IHRService hrService) =>
{
    // ? Parse time strings - support both ISO datetime and time-only formats
    TimeSpan checkIn;

    // Try parsing as DateTime first (for ISO format like "2026-02-05T09:00:00")
    if (DateTime.TryParse(request.CheckInTime, out var checkInDt))
    {
        checkIn = checkInDt.TimeOfDay;
    }
    // Fall back to time-only format (like "09:00" or "09:00:00")
    else if (!TimeSpan.TryParse(request.CheckInTime, out checkIn))
    {
        return Results.BadRequest(new { error = "Invalid check-in time format", success = false });
    }

    TimeSpan? checkOut = null;
    if (!string.IsNullOrEmpty(request.CheckOutTime))
    {
        // Try parsing as DateTime first (for ISO format)
        if (DateTime.TryParse(request.CheckOutTime, out var checkOutDt))
        {
            checkOut = checkOutDt.TimeOfDay;
        }
        // Fall back to time-only format
        else if (!TimeSpan.TryParse(request.CheckOutTime, out var parsed))
        {
            return Results.BadRequest(new { error = "Invalid check-out time format", success = false });
        }
        else
        {
            checkOut = parsed;
        }
    }

    var result = await hrService.MarkAttendanceAsync(
        request.EmployeeId,
        request.Date,
        checkIn,
        checkOut,
        request.Status
    );
    return result.IsSuccess ? Results.Ok(new { success = true, message = "Attendance marked successfully" }) : Results.BadRequest(new { error = result.ErrorMessage, success = false });
}).RequireAuthorization(Permissions.HRManageAttendance);

app.MapGet("/api/attendance/{employeeId:guid}", async (
    Guid employeeId,
    DateTime startDate,
    DateTime endDate,
    IHRService hrService) =>
{
    var result = await hrService.GetEmployeeAttendanceAsync(employeeId, startDate, endDate);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.HRViewAttendance);

app.MapPost("/api/leaves", async (CreateLeaveRequest request, IHRService hrService) =>
{
    var result = await hrService.RequestLeaveAsync(
        request.EmployeeId,
        request.Type,
        request.StartDate,
        request.EndDate,
        request.Reason
    );
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.HRViewLeave);

app.MapPost("/api/leaves/{leaveId:guid}/approve", async (
    Guid leaveId,
    ApproveLeaveRequest request,
    IHRService hrService) =>
{
    var result = await hrService.ApproveLeaveAsync(leaveId, request.ApprovedBy, request.Notes);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.HRApproveLeave);

app.MapPost("/api/leaves/{leaveId:guid}/reject", async (
    Guid leaveId,
    RejectLeaveRequest request,
    IHRService hrService) =>
{
    var result = await hrService.RejectLeaveAsync(leaveId, request.Notes);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.HRApproveLeave);

app.MapPost("/api/payroll/generate", async (
    GeneratePayrollRequest request,
    IHRService hrService) =>
{
    var result = await hrService.GeneratePayrollAsync(request.EmployeeId, request.Month, request.Year);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.HRProcessPayroll);

app.MapGet("/api/payroll", async (int month, int year, IHRService hrService) =>
{
    var result = await hrService.GetPayrollsAsync(month, year);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.HRViewPayroll);

app.MapPut("/api/employees/{id:guid}", async (Guid id, UpdateEmployeeRequest request, IHRService hrService) =>
{
    var result = await hrService.UpdateEmployeeAsync(id, request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.HREditEmployees);

app.MapDelete("/api/employees/{id:guid}", async (Guid id, IHRService hrService) =>
{
    var result = await hrService.DeactivateEmployeeAsync(id);
    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.HRDeleteEmployees);

app.MapPut("/api/attendance/{id:guid}", async (Guid id, UpdateAttendanceRequest request, IHRService hrService) =>
{
    var result = await hrService.UpdateAttendanceAsync(id, request);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapDelete("/api/attendance/{id:guid}", async (Guid id, IHRService hrService) =>
{
    var result = await hrService.DeleteAttendanceAsync(id);
    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapGet("/api/leaves/{id:guid}", async (Guid id, IHRService hrService) =>
{
    var result = await hrService.GetLeaveByIdAsync(id);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result.ErrorMessage);
}).RequireAuthorization();

app.MapPut("/api/leaves/{id:guid}", async (Guid id, UpdateLeaveRequest request, IHRService hrService) =>
{
    var result = await hrService.UpdateLeaveAsync(id, request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapDelete("/api/leaves/{id:guid}", async (Guid id, IHRService hrService) =>
{
    var result = await hrService.DeleteLeaveAsync(id);
    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapGet("/api/payroll/{id:guid}", async (Guid id, IHRService hrService) =>
{
    var result = await hrService.GetPayrollByIdAsync(id);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result.ErrorMessage);
}).RequireAuthorization();

app.MapPut("/api/payroll/{id:guid}", async (Guid id, UpdatePayrollRequest request, IHRService hrService) =>
{
    var result = await hrService.UpdatePayrollAsync(id, request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapDelete("/api/payroll/{id:guid}", async (Guid id, IHRService hrService) =>
{
    var result = await hrService.DeletePayrollAsync(id);
    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapPost("/api/payroll/{id:guid}/process", async (Guid id, IHRService hrService) =>
{
    var result = await hrService.ProcessPayrollAsync(id);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();


app.MapPost("/api/jobs", async (CreateJobPostingRequest request, IHRService hrService) =>
{
    var result = await hrService.CreateJobPostingAsync(request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.HRCreateEmployees);

app.MapGet("/api/jobs", async (IHRService hrService) =>
{
    var result = await hrService.GetAllJobPostingsAsync();
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.HRViewEmployees);

app.MapPost("/api/jobs/apply", async (ApplyForJobRequest request, IHRService hrService) =>
{
    var result = await hrService.ApplyForJobAsync(request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}); // Public endpoint

app.MapGet("/api/jobs/{jobId:guid}/candidates", async (Guid jobId, IHRService hrService) =>
{
    var result = await hrService.GetCandidatesByJobAsync(jobId);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.HRViewEmployees);

app.MapPost("/api/interviews/schedule", async (ScheduleInterviewRequest request, IHRService hrService) =>
{
    var result = await hrService.ScheduleInterviewAsync(request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.HRCreateEmployees);

app.MapGet("/api/jobs/{id:guid}", async (Guid id, IHRService hrService) =>
{
    var result = await hrService.GetJobPostingByIdAsync(id);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result.ErrorMessage);
}).RequireAuthorization(Permissions.HRViewEmployees);

app.MapPut("/api/jobs/{id:guid}", async (Guid id, UpdateJobPostingRequest request, IHRService hrService) =>
{
    var result = await hrService.UpdateJobPostingAsync(id, request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.HRCreateEmployees);

app.MapPatch("/api/jobs/{id:guid}/status", async (Guid id, JobPostingStatus status, IHRService hrService) =>
{
    var result = await hrService.UpdateJobPostingStatusAsync(id, status);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.HRCreateEmployees);

app.MapDelete("/api/jobs/{id:guid}", async (Guid id, IHRService hrService) =>
{
    var result = await hrService.DeleteJobPostingAsync(id);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.HRDeleteEmployees);

app.MapGet("/api/candidates/{id:guid}", async (Guid id, IHRService hrService) =>
{
    var result = await hrService.GetCandidateByIdAsync(id);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result.ErrorMessage);
}).RequireAuthorization(Permissions.HRViewEmployees);

app.MapPatch("/api/candidates/{id:guid}/status", async (Guid id, CandidateStatus status, IHRService hrService) =>
{
    var result = await hrService.UpdateCandidateStatusAsync(id, status);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.HRCreateEmployees);

app.MapDelete("/api/candidates/{id:guid}", async (Guid id, IHRService hrService) =>
{
    var result = await hrService.DeleteCandidateAsync(id);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.HRDeleteEmployees);

app.MapPatch("/api/interviews/{id:guid}/feedback", async (Guid id, UpdateFeedbackRequest request, IHRService hrService) =>
{
    var result = await hrService.UpdateInterviewFeedbackAsync(id, request.Rating, request.Feedback);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.HREditEmployees);

app.MapPatch("/api/interviews/{id:guid}/cancel", async (Guid id, IHRService hrService) =>
{
    var result = await hrService.CancelInterviewAsync(id);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.HREditEmployees);

app.MapDelete("/api/interviews/{id:guid}", async (Guid id, IHRService hrService) =>
{
    var result = await hrService.DeleteInterviewAsync(id);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.HRDeleteEmployees);

// POST: Match resumes for candidates against job posting
app.MapPost("/api/candidates/match-resumes", async (MatchCandidatesRequest request, IHRService hrService, IResumeMatchingService resumeMatchingService, ILogger<Program> logger) =>
{
    try
    {
        // Get job posting details
        var jobResult = await hrService.GetJobPostingByIdAsync(request.JobPostingId);
        if (!jobResult.IsSuccess)
        {
            return Results.NotFound("Job posting not found");
        }

        var jobPosting = jobResult.Data;
        // Note: Need to fetch full entity to get Description and Requirements
        // Using title and job ID as fallback
        var jobDescription = jobPosting?.Title ?? "Job Posting";

        // Get candidate details with resume text
        var candidatesToMatch = new List<(Guid Id, string Name, string Text, string Category)>();

        foreach (var candidateId in request.CandidateIds)
        {
            var candidateResult = await hrService.GetCandidateByIdAsync(candidateId);
            if (candidateResult.IsSuccess && candidateResult.Data != null)
            {
                var candidate = candidateResult.Data;
                // Prioritize ResumeContent, fall back to ResumeUrl or use email as last resort
                var resumeText = candidate.ResumeContent ?? candidate.ResumeUrl ?? "";
                var category = jobPosting?.Department ?? "General";

                candidatesToMatch.Add((candidateId, $"{candidate.FirstName} {candidate.LastName}", resumeText, category));
            }
        }

        if (candidatesToMatch.Count == 0)
        {
            return Results.BadRequest("No valid candidates found");
        }

        // Call resume matching service
        var matchingResult = await resumeMatchingService.MatchCandidatesForJobAsync(
            request.JobPostingId,
            jobDescription,
            candidatesToMatch
        );

        return Results.Ok(matchingResult);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error in match-resumes endpoint");
        return Results.BadRequest($"Resume matching failed: {ex.Message}");
    }
}).RequireAuthorization(Permissions.HREditEmployees);

// POST: Direct resume matching for custom resumes
app.MapPost("/api/candidates/smart-match", async (ResumeMatchingRequest request, IResumeMatchingService resumeMatchingService) =>
{
    try
    {
        var result = await resumeMatchingService.MatchResumesAsync(request.JobDescription, request.Resumes);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.BadRequest($"Resume matching failed: {ex.Message}");
    }
}).RequireAuthorization(Permissions.HREditEmployees);

// GET: Check resume matching service health
app.MapGet("/api/candidates/health/resume-service", async (IResumeMatchingService resumeMatchingService) =>
{
    var isHealthy = await resumeMatchingService.HealthCheckAsync();
    return isHealthy 
        ? Results.Ok(new { status = "healthy", message = "Resume Matching Service is running" }) 
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
});

// RESUME SCREENING ENDPOINTS

// POST: Save match results to database
app.MapPost("/api/candidates/save-screening-results", 
    async (SaveResumeScreeningRequest request, 
           IHRService hrService, 
           IResumeMatchingService resumeMatchingService,
           ILogger<Program> logger) =>
{
    try
    {
        var jobResult = await hrService.GetJobPostingByIdAsync(request.JobPostingId);
        if (!jobResult.IsSuccess)
            return Results.NotFound("Job posting not found");

        var candidatesToMatch = new List<(Guid Id, string Name, string Text, string Category)>();

        foreach (var candidateId in request.CandidateIds)
        {
            var candidateResult = await hrService.GetCandidateByIdAsync(candidateId);
            if (candidateResult.IsSuccess && candidateResult.Data != null)
            {
                var candidate = candidateResult.Data;
                var jobPosting = jobResult.Data;
                candidatesToMatch.Add((
                    candidateId,
                    $"{candidate.FirstName} {candidate.LastName}",
                    candidate.ResumeContent ?? candidate.ResumeUrl ?? "",
                    jobPosting?.Department ?? "General"
                ));
            }
        }

        if (candidatesToMatch.Count == 0)
            return Results.BadRequest("No valid candidates found");

        var matchingResult = await resumeMatchingService.MatchCandidatesForJobAsync(
            request.JobPostingId,
            jobResult.Data?.Title ?? "Job",
            candidatesToMatch
        );

        // SAVE results to database
        var saveResult = await hrService.SaveResumeScreeningResultsAsync(
            request.JobPostingId,
            matchingResult.Results,
            request.Notes
        );

        if (!saveResult.IsSuccess)
            return Results.BadRequest("Failed to save results");

        return Results.Ok(matchingResult);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error in save-screening-results endpoint");
        return Results.BadRequest($"Screening failed: {ex.Message}");
    }
}).RequireAuthorization(Permissions.HREditEmployees);

// GET: Retrieve saved match results for a job
app.MapGet("/api/jobs/{jobId:guid}/match-results", 
    async (Guid jobId, IHRService hrService) =>
{
    var result = await hrService.GetResumeMatchResultsAsync(jobId);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.HRViewEmployees);

// GET: Retrieve match history for a candidate
app.MapGet("/api/candidates/{candidateId:guid}/match-history", 
    async (Guid candidateId, IHRService hrService) =>
{
    var result = await hrService.GetCandidateMatchHistoryAsync(candidateId);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.HRViewEmployees);

// GET: Get all screening batches for a job
app.MapGet("/api/jobs/{jobId:guid}/screening-batches", 
    async (Guid jobId, IHRService hrService) =>
{
    var result = await hrService.GetScreeningBatchesAsync(jobId);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.HRViewEmployees);

// DELETE: Delete a screening batch
app.MapDelete("/api/screening-batches/{batchId:guid}", 
    async (Guid batchId, IHRService hrService) =>
{
    var result = await hrService.DeleteScreeningBatchAsync(batchId);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.HRDeleteEmployees);




app.MapPost("/api/accounts", async (CreateAccountRequest request, IFinanceService financeService, ILogger<Program> logger) =>
{
    // ? DEBUG: Log what we receive from frontend
    logger.LogInformation("Creating account - Code: {Code}, Name: {Name}, Type: {Type}",
        request.AccountCode ?? "NULL",
        request.AccountName ?? "NULL",
        request.Type);

    var result = await financeService.CreateAccountAsync(request);
    return result.IsSuccess
        ? Results.Ok(new { data = result.Data, success = true, message = "Account created successfully" })
        : Results.BadRequest(new { error = result.ErrorMessage, success = false });
}).RequireAuthorization(Permissions.FinanceManageAccounts);
app.MapGet("/api/invoices/search", async (
    [AsParameters] InvoiceSearchRequest search,
    [AsParameters] PaginationRequest pagination,
    IFinanceService financeService,
    ILoggerService logger) =>
{
    logger.LogInformation("Searching invoices with term: {SearchTerm}, status: {Status}",
        search.SearchTerm, search.Status);

    var result = await financeService.SearchInvoicesAsync(search, pagination);

    if (result.IsSuccess)
    {
        logger.LogInformation("Found {Count} invoices, page {Page} of {TotalPages}",
            result.Data!.Items.Count, result.Data.PageNumber, result.Data.TotalPages);
    }

    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.FinanceViewInvoices);

app.MapGet("/api/invoices/overdue", async (
    [AsParameters] PaginationRequest pagination,
    IFinanceService financeService,
    ILoggerService logger) =>
{
    var search = new InvoiceSearchRequest { Overdue = true };

    logger.LogInformation("Retrieving overdue invoices");

    var result = await financeService.SearchInvoicesAsync(search, pagination);

    if (result.IsSuccess)
    {
        logger.LogWarning("Found {Count} overdue invoices", result.Data!.Items.Count);
    }

    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.FinanceViewInvoices);
app.MapGet("/api/accounts", async (IFinanceService financeService) =>
{
    var result = await financeService.GetAllAccountsAsync();
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.FinanceManageAccounts);

app.MapPost("/api/journal-entries", async (CreateJournalEntryRequest request, IFinanceService financeService) =>
{
    var result = await financeService.CreateJournalEntryAsync(request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.FinanceManageAccounts);

app.MapPost("/api/journal-entries/{entryId:guid}/post", async (Guid entryId, IFinanceService financeService) =>
{
    var result = await financeService.PostJournalEntryAsync(entryId);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.FinanceManageAccounts);

app.MapPost("/api/invoices", async (CreateInvoiceRequest request, IFinanceService financeService) =>
{
    // ? NEW: Validate request before processing
    if (request.Items == null || !request.Items.Any())
    {
        return Results.BadRequest(new { error = "Invoice must have at least one item", success = false });
    }

    var result = await financeService.CreateInvoiceAsync(request.CustomerId, request.InvoiceDate, request.DueDate, request.Items);
    return result.IsSuccess
        ? Results.Ok(new { data = result.Data, success = true, message = "Invoice created successfully" })
        : Results.BadRequest(new { error = result.ErrorMessage, success = false });
}).RequireAuthorization(Permissions.FinanceCreateInvoices);

app.MapGet("/api/invoices", async (InvoiceStatus? status, IFinanceService financeService) =>
{
    var result = await financeService.GetInvoicesAsync(status);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.FinanceViewInvoices);

app.MapPost("/api/payments", async (RecordPaymentRequest request, IFinanceService financeService) =>
{
    var result = await financeService.RecordPaymentAsync(request.InvoiceId, request.Amount, request.Method, request.PaymentDate);
    return result.IsSuccess
        ? Results.Ok(new { data = result.Data, success = true, message = "Payment recorded successfully" })
        : Results.BadRequest(new { error = result.ErrorMessage, success = false });
}).RequireAuthorization(Permissions.FinanceProcessPayments);

app.MapGet("/api/finance/summary", async (DateTime startDate, DateTime endDate, IFinanceService financeService) =>
{
    var result = await financeService.GetFinancialSummaryAsync(startDate, endDate);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.FinanceViewReports);
// GET Account by ID
app.MapGet("/api/accounts/{id:guid}", async (Guid id, IFinanceService financeService) =>
{
    var result = await financeService.GetAccountByIdAsync(id);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result.ErrorMessage);
}).RequireAuthorization();

// UPDATE Account
app.MapPut("/api/accounts/{id:guid}", async (Guid id, UpdateAccountRequest request, IFinanceService financeService) =>
{
    var result = await financeService.UpdateAccountAsync(id, request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// DELETE/Deactivate Account
app.MapDelete("/api/accounts/{id:guid}", async (Guid id, IFinanceService financeService) =>
{
    var result = await financeService.DeactivateAccountAsync(id);
    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// GET Journal Entry by ID
app.MapGet("/api/journal-entries/{id:guid}", async (Guid id, IFinanceService financeService) =>
{
    var result = await financeService.GetJournalEntryByIdAsync(id);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result.ErrorMessage);
}).RequireAuthorization();

// GET All Journal Entries
app.MapGet("/api/journal-entries", async (bool? isPosted, IFinanceService financeService) =>
{
    var result = await financeService.GetAllJournalEntriesAsync(isPosted);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// DELETE Journal Entry
app.MapDelete("/api/journal-entries/{id:guid}", async (Guid id, IFinanceService financeService) =>
{
    var result = await financeService.DeleteJournalEntryAsync(id);
    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// GET Invoice by ID
app.MapGet("/api/invoices/{id:guid}", async (Guid id, IFinanceService financeService) =>
{
    var result = await financeService.GetInvoiceByIdAsync(id);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result.ErrorMessage);
}).RequireAuthorization();

// UPDATE Invoice
app.MapPut("/api/invoices/{id:guid}", async (Guid id, UpdateInvoiceRequest request, IFinanceService financeService) =>
{
    var result = await financeService.UpdateInvoiceAsync(id, request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.FinanceEditInvoices);

// DELETE Invoice
app.MapDelete("/api/invoices/{id:guid}", async (Guid id, IFinanceService financeService) =>
{
    var result = await financeService.DeleteInvoiceAsync(id);
    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.FinanceDeleteInvoices);

// Cancel Invoice
app.MapPost("/api/invoices/{id:guid}/cancel", async (Guid id, IFinanceService financeService) =>
{
    var result = await financeService.CancelInvoiceAsync(id);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// GET Payment by ID
app.MapGet("/api/payments/{id:guid}", async (Guid id, IFinanceService financeService) =>
{
    var result = await financeService.GetPaymentByIdAsync(id);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result.ErrorMessage);
}).RequireAuthorization();

// GET All Payments
app.MapGet("/api/payments", async (Guid? invoiceId, Guid? customerId, IFinanceService financeService) =>
{
    var result = await financeService.GetAllPaymentsAsync(invoiceId, customerId);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// DELETE Payment
app.MapDelete("/api/payments/{id:guid}", async (Guid id, IFinanceService financeService) =>
{
    var result = await financeService.DeletePaymentAsync(id);
    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// FRAUD DETECTION ENDPOINTS

// POST /api/finance/payments/record - Record payment with fraud detection
app.MapPost("/api/finance/payments/record", async (RecordPaymentRequest request, IFinanceService financeService) =>
{
    // Record payment first
    var paymentResult = await financeService.RecordPaymentAsync(request.InvoiceId, request.Amount, request.Method, request.PaymentDate);

    if (!paymentResult.IsSuccess)
    {
        return Results.BadRequest(new { error = paymentResult.ErrorMessage, success = false });
    }

    // Perform fraud detection if transaction features are provided
    PaymentFraudAnalysisDto? fraudAnalysis = null;
    if (request.TransactionFeatures != null && request.TransactionFeatures.Count == 30)
    {
        var fraudResult = await financeService.AnalyzePaymentFraudAsync(
            paymentResult.Data.Id,
            request.InvoiceId,
            request.TransactionFeatures
        );

        if (fraudResult.IsSuccess)
        {
            fraudAnalysis = fraudResult.Data;
        }
    }

    return Results.Ok(new
    {
        success = true,
        message = "Payment recorded successfully",
        data = new
        {
            payment = paymentResult.Data,
            fraudAnalysis = fraudAnalysis
        }
    });
}).RequireAuthorization(Permissions.FinanceProcessPayments);

// GET /api/finance/fraud-detection/health - Health check for fraud detection service
app.MapGet("/api/finance/fraud-detection/health", async (IFinanceService financeService) =>
{
    var result = await financeService.CheckFraudDetectionHealthAsync();
    return Results.Ok(new
    {
        success = true,
        status = result ? "healthy" : "unhealthy",
        service = "Fraud Detection API"
    });
}).RequireAuthorization();

// POST /api/finance/fraud-detection/analyze - Analyze payment for fraud
app.MapPost("/api/finance/fraud-detection/analyze", async (FraudAnalysisRequest request, IFinanceService financeService) =>
{
    var result = await financeService.AnalyzePaymentFraudAsync(
        request.PaymentId,
        request.InvoiceId,
        request.TransactionFeatures
    );

    return result.IsSuccess
        ? Results.Ok(new { success = true, data = result.Data })
        : Results.BadRequest(new { error = result.ErrorMessage, success = false });
}).RequireAuthorization(Permissions.FinanceProcessPayments);

// GET /api/finance/fraud-detection/history/{invoiceId} - Get fraud analysis history
app.MapGet("/api/finance/fraud-detection/history/{invoiceId:guid}", async (Guid invoiceId, IFinanceService financeService) =>
{
    var result = await financeService.GetFraudAnalysisHistoryAsync(invoiceId);

    return result.IsSuccess
        ? Results.Ok(new { success = true, data = result.Data })
        : Results.NotFound(new { error = result.ErrorMessage, success = false });
}).RequireAuthorization(Permissions.FinanceViewReports);

// GET /api/finance/invoices/for-fraud-detection - Get invoices for fraud detection dropdown
app.MapGet("/api/finance/invoices/for-fraud-detection", async (IFinanceService financeService, ERPDbContext context) =>
{
    try
    {
        // Get all invoices (for fraud detection - any invoice can be processed)
        var invoices = await context.Invoices
            .Where(i => !i.IsDeleted)
            .OrderByDescending(i => i.InvoiceDate)
            .Select(i => new InvoiceForFraudDetectionDto(
                i.Id,
                i.InvoiceNumber,
                i.InvoiceDate,
                i.DueDate,
                i.Customer != null ? i.Customer.Name : null,
                i.Items.Sum(it => (it.Quantity * it.UnitPrice) * (1 + it.TaxRate / 100)),
                i.BalanceAmount,
                i.Status.ToString()
            ))
            .ToListAsync();

        return Results.Ok(new
        {
            success = true,
            data = invoices,
            count = invoices.Count
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message, success = false });
    }
}).RequireAuthorization(Permissions.FinanceViewInvoices);

app.MapPost("/api/finance/invoices/fix-test-data", async (ERPDbContext context) =>
{
    try
    {
        // Find any invoice and update it to have outstanding balance for testing
        var invoice = await context.Invoices
            .FirstOrDefaultAsync(i => i.InvoiceNumber == "INV-20260504-50f60bc5");

        if (invoice != null)
        {
            invoice.Status = InvoiceStatus.Sent;  // Change from Cancelled to Sent
            invoice.BalanceAmount = 5000;
            await context.SaveChangesAsync();

            return Results.Ok(new
            {
                success = true,
                message = "Test data fixed! Invoice updated with Sent status and balance of 5000",
                invoice = new
                {
                    invoice.InvoiceNumber,
                    Status = invoice.Status.ToString(),
                    invoice.BalanceAmount
                }
            });
        }

        return Results.NotFound(new { success = false, message = "Test invoice not found" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});


app.MapGet("/api/finance/invoices/debug", async (ERPDbContext context) =>
{
    try
    {
        var allInvoices = await context.Invoices
            .Select(i => new
            {
                i.Id,
                i.InvoiceNumber,
                Status = i.Status.ToString(),
                i.IsDeleted,
                i.InvoiceDate,
                i.DueDate,
                CustomerName = i.Customer != null ? i.Customer.Name : "No Customer",
                BalanceAmount = i.BalanceAmount
            })
            .ToListAsync();

        var sentCount = allInvoices.Count(i => i.Status == "Sent");
        var overdueCount = allInvoices.Count(i => i.Status == "Overdue");

        return Results.Ok(new
        {
            totalInvoices = allInvoices.Count,
            sentCount = sentCount,
            overdueCount = overdueCount,
            invoices = allInvoices,
            message = $"Total: {allInvoices.Count}, Sent: {sentCount}, Overdue: {overdueCount} - These are the only ones that will show in dropdown"
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// GET /api/finance/payments/for-invoice/{invoiceId} - Get payments for an invoice dropdown
app.MapGet("/api/finance/payments/for-invoice/{invoiceId:guid}", async (Guid invoiceId, ERPDbContext context) =>
{
    try
    {
        // Verify invoice exists
        var invoice = await context.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice == null)
        {
            return Results.NotFound(new { error = "Invoice not found", success = false });
        }

        // Get all payments for this invoice
        var payments = await context.Payments
            .Where(p => p.InvoiceId == invoiceId && !p.IsDeleted)
            .OrderByDescending(p => p.PaymentDate)
            .Select(p => new PaymentForDropdownDto(
                p.Id,
                p.PaymentNumber,
                p.PaymentDate,
                p.Amount,
                p.Method.ToString()
            ))
            .ToListAsync();

        return Results.Ok(new
        {
            success = true,
            data = payments,
            count = payments.Count
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message, success = false });
    }
}).RequireAuthorization(Permissions.FinanceViewInvoices);

// GET /api/finance/fraud-detection/lookup - Get invoice with its payments for fraud detection form
app.MapGet("/api/finance/fraud-detection/lookup/{invoiceId:guid}", async (Guid invoiceId, ERPDbContext context) =>
{
    try
    {
        var invoice = await context.Invoices
            .Include(i => i.Customer)
            .Include(i => i.Items)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.Id == invoiceId && !i.IsDeleted);

        if (invoice == null)
        {
            return Results.NotFound(new { error = "Invoice not found", success = false });
        }

        var payments = invoice.Payments
            .Where(p => !p.IsDeleted)
            .OrderByDescending(p => p.PaymentDate)
            .Select(p => new PaymentForDropdownDto(
                p.Id,
                p.PaymentNumber,
                p.PaymentDate,
                p.Amount,
                p.Method.ToString()
            ))
            .ToList();

        var result = new InvoiceWithPaymentsDto(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.InvoiceDate,
            invoice.DueDate,
            invoice.Customer?.Name,
            invoice.Items.Sum(it => (it.Quantity * it.UnitPrice) * (1 + it.TaxRate / 100)),
            invoice.BalanceAmount,
            invoice.Status.ToString(),
            payments
        );

        return Results.Ok(new
        {
            success = true,
            data = result
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message, success = false });
    }
}).RequireAuthorization(Permissions.FinanceViewInvoices);

// TRANSACTIONS ENDPOINTS (integrated with Journal Entries)
app.MapGet("/api/transactions", async (IFinanceService financeService, ERPDbContext context) =>
{
    var result = await financeService.GetAllJournalEntriesAsync();

    if (!result.IsSuccess)
    {
        return Results.BadRequest(new { error = result.ErrorMessage, success = false });
    }

    // Map JournalEntry to transaction format for frontend
    var journalEntries = await context.JournalEntries
        .Include(je => je.Lines)
        .ThenInclude(l => l.Account)
        .Where(je => !je.IsDeleted)
        .OrderByDescending(je => je.EntryDate)
        .ToListAsync();

    var transactions = journalEntries.SelectMany(je =>
        je.Lines.Select(line => new
        {
            id = je.Id.ToString(),
            transactionDate = je.EntryDate,
            accountName = line.Account.AccountName,
            description = je.Description,
            amount = line.DebitAmount > 0 ? line.DebitAmount : -line.CreditAmount,
            transactionType = line.DebitAmount > 0 ? "Debit" : "Credit",
            reference = je.Reference,
            isPosted = je.IsPosted
        })
    ).ToList();

    return Results.Ok(transactions);
}).RequireAuthorization();

app.MapGet("/api/transactions/{id:guid}", async (Guid id, IFinanceService financeService, ERPDbContext context) =>
{
    var result = await financeService.GetJournalEntryByIdAsync(id);

    if (!result.IsSuccess)
    {
        return Results.NotFound(new { error = result.ErrorMessage, success = false });
    }

    // Get full journal entry with lines
    var journalEntry = await context.JournalEntries
        .Include(je => je.Lines)
        .ThenInclude(l => l.Account)
        .FirstOrDefaultAsync(je => je.Id == id);

    if (journalEntry == null)
    {
        return Results.NotFound(new { error = "Transaction not found", success = false });
    }

    var transaction = new
    {
        id = journalEntry.Id.ToString(),
        transactionDate = journalEntry.EntryDate,
        description = journalEntry.Description,
        reference = journalEntry.Reference,
        isPosted = journalEntry.IsPosted,
        lines = journalEntry.Lines.Select(line => new
        {
            accountId = line.AccountId,
            accountName = line.Account.AccountName,
            debitAmount = line.DebitAmount,
            creditAmount = line.CreditAmount,
            description = line.Description
        }).ToList()
    };

    return Results.Ok(transaction);
}).RequireAuthorization();

app.MapPost("/api/transactions", async (CreateJournalEntryRequest request, IFinanceService financeService, ILoggerService logger) =>
{
    logger.LogInformation("Creating transaction - Date: {Date}, Description: {Description}, Lines: {LineCount}",
        request.EntryDate, request.Description, request.Lines?.Count ?? 0);

    // Create journal entry as DRAFT (not auto-posted)
    var result = await financeService.CreateJournalEntryAsync(request);

    if (!result.IsSuccess)
    {
        logger.LogWarning("Transaction creation failed: {Error}", result.ErrorMessage);
        return Results.BadRequest(new { error = result.ErrorMessage, success = false });
    }

    logger.LogInformation("Transaction created as draft: {EntryNumber}", result.Data.EntryNumber);

    return Results.Ok(new
    {
        data = result.Data,
        success = true,
        message = "Transaction created successfully"
    });
}).RequireAuthorization();

app.MapPut("/api/transactions/{id:guid}", async (Guid id, CreateJournalEntryRequest request, IFinanceService financeService, ERPDbContext context) =>
{
    var journalEntry = await context.JournalEntries
        .Include(je => je.Lines)
        .FirstOrDefaultAsync(je => je.Id == id);

    if (journalEntry == null)
    {
        return Results.NotFound(new { error = "Transaction not found", success = false });
    }

    if (journalEntry.IsPosted)
    {
        return Results.BadRequest(new { error = "Cannot update posted transactions", success = false });
    }

    // Validate debits equal credits
    var totalDebits = request.Lines.Sum(l => l.DebitAmount);
    var totalCredits = request.Lines.Sum(l => l.CreditAmount);

    if (totalDebits != totalCredits)
    {
        return Results.BadRequest(new { error = "Total debits must equal total credits", success = false });
    }

    // Update journal entry
    journalEntry.EntryDate = request.EntryDate;
    journalEntry.Description = request.Description;
    journalEntry.Reference = request.Reference;
    journalEntry.UpdatedAt = DateTime.UtcNow;

    // Remove old lines
    context.JournalEntryLines.RemoveRange(journalEntry.Lines);

    foreach (var line in request.Lines)
    {
        var account = await context.Accounts.FindAsync(line.AccountId);
        if (account == null)
        {
            return Results.BadRequest(new { error = $"Account not found: {line.AccountId}", success = false });
        }

        var entryLine = new JournalEntryLine
        {
            JournalEntryId = journalEntry.Id,
            AccountId = line.AccountId,
            DebitAmount = line.DebitAmount,
            CreditAmount = line.CreditAmount,
            Description = line.Description
        };

        journalEntry.Lines.Add(entryLine);
    }

    await context.SaveChangesAsync();

    return Results.Ok(new { success = true, message = "Transaction updated successfully" });
}).RequireAuthorization();

app.MapDelete("/api/transactions/{id:guid}", async (Guid id, IFinanceService financeService) =>
{
    var result = await financeService.DeleteJournalEntryAsync(id);

    if (!result.IsSuccess)
    {
        return Results.BadRequest(new { error = result.ErrorMessage, success = false });
    }

    return Results.Ok(new { success = true, message = "Transaction deleted successfully" });
}).RequireAuthorization();

// POST Transaction (Post/Lock Entry)
app.MapPost("/api/transactions/{id:guid}/post", async (Guid id, IFinanceService financeService, ILoggerService logger) =>
{
    logger.LogInformation("Posting transaction: {TransactionId}", id);

    var result = await financeService.PostJournalEntryAsync(id);

    if (!result.IsSuccess)
    {
        logger.LogWarning("Transaction posting failed: {Error}", result.ErrorMessage);
        return Results.BadRequest(new { error = result.ErrorMessage, success = false });
    }

    logger.LogInformation("Transaction posted successfully: {TransactionId}", id);

    return Results.Ok(new
    {
        success = true,
        message = "Transaction posted and locked successfully"
    });
}).RequireAuthorization();

// BUDGETS ENDPOINTS
app.MapGet("/api/budgets", async (BudgetStatus? status, IFinanceService financeService) =>
{
    var result = await financeService.GetAllBudgetsAsync(status);
    return result.IsSuccess
        ? Results.Ok(result.Data)
        : Results.BadRequest(new { error = result.ErrorMessage, success = false });
}).RequireAuthorization();

app.MapGet("/api/budgets/search", async (
    [AsParameters] BudgetSearchRequest search,
    [AsParameters] PaginationRequest pagination,
    IFinanceService financeService,
    ILoggerService logger) =>
{
    logger.LogInformation("Searching budgets with term: {SearchTerm}, fiscal year: {FiscalYear}",
        search.SearchTerm, search.FiscalYear);

    var result = await financeService.SearchBudgetsAsync(search, pagination);

    if (result.IsSuccess)
    {
        logger.LogInformation("Found {Count} budgets, page {Page} of {TotalPages}",
            result.Data!.Items.Count, result.Data.PageNumber, result.Data.TotalPages);
    }

    return result.IsSuccess
        ? Results.Ok(result.Data)
        : Results.BadRequest(new { error = result.ErrorMessage, success = false });
}).RequireAuthorization();

app.MapGet("/api/budgets/{id:guid}", async (Guid id, IFinanceService financeService) =>
{
    var result = await financeService.GetBudgetByIdAsync(id);
    return result.IsSuccess
        ? Results.Ok(result.Data)
        : Results.NotFound(new { error = result.ErrorMessage, success = false });
}).RequireAuthorization();

app.MapGet("/api/budgets/{id:guid}/lines", async (Guid id, IFinanceService financeService) =>
{
    var result = await financeService.GetBudgetLinesAsync(id);
    return result.IsSuccess
        ? Results.Ok(result.Data)
        : Results.BadRequest(new { error = result.ErrorMessage, success = false });
}).RequireAuthorization();

app.MapPost("/api/budgets", async (CreateBudgetRequest request, IFinanceService financeService) =>
{
    var result = await financeService.CreateBudgetAsync(request);
    return result.IsSuccess
        ? Results.Ok(new { data = result.Data, success = true, message = "Budget created successfully" })
        : Results.BadRequest(new { error = result.ErrorMessage, success = false });
}).RequireAuthorization();

app.MapPut("/api/budgets/{id:guid}", async (Guid id, UpdateBudgetRequest request, IFinanceService financeService) =>
{
    var result = await financeService.UpdateBudgetAsync(id, request);
    return result.IsSuccess
        ? Results.Ok(new { data = result.Data, success = true, message = "Budget updated successfully" })
        : Results.BadRequest(new { error = result.ErrorMessage, success = false });
}).RequireAuthorization();

app.MapDelete("/api/budgets/{id:guid}", async (Guid id, IFinanceService financeService) =>
{
    var result = await financeService.DeleteBudgetAsync(id);
    return result.IsSuccess
        ? Results.Ok(new { success = true, message = "Budget deleted successfully" })
        : Results.BadRequest(new { error = result.ErrorMessage, success = false });
}).RequireAuthorization();

app.MapPost("/api/budgets/{id:guid}/approve", async (Guid id, Guid approvedBy, IFinanceService financeService) =>
{
    var result = await financeService.ApproveBudgetAsync(id, approvedBy);
    return result.IsSuccess
        ? Results.Ok(new { success = true, message = "Budget approved successfully" })
        : Results.BadRequest(new { error = result.ErrorMessage, success = false });
}).RequireAuthorization();

app.MapPost("/api/budgets/{id:guid}/reject", async (Guid id, IFinanceService financeService) =>
{
    var result = await financeService.RejectBudgetAsync(id);
    return result.IsSuccess
        ? Results.Ok(new { success = true, message = "Budget rejected" })
        : Results.BadRequest(new { error = result.ErrorMessage, success = false });
}).RequireAuthorization();

app.MapPost("/api/budgets/{id:guid}/activate", async (Guid id, IFinanceService financeService) =>
{
    var result = await financeService.ActivateBudgetAsync(id);
    return result.IsSuccess
        ? Results.Ok(new { success = true, message = "Budget activated successfully" })
        : Results.BadRequest(new { error = result.ErrorMessage, success = false });
}).RequireAuthorization();

app.MapPost("/api/budgets/{id:guid}/close", async (Guid id, IFinanceService financeService) =>
{
    var result = await financeService.CloseBudgetAsync(id);
    return result.IsSuccess
        ? Results.Ok(new { success = true, message = "Budget closed successfully" })
        : Results.BadRequest(new { error = result.ErrorMessage, success = false });
}).RequireAuthorization();

app.MapPost("/api/budgets/{id:guid}/update-actuals", async (Guid id, IFinanceService financeService) =>
{
    var result = await financeService.UpdateBudgetActuals(id);
    return result.IsSuccess
        ? Results.Ok(new { success = true, message = "Budget actuals updated successfully" })
        : Results.BadRequest(new { error = result.ErrorMessage, success = false });
}).RequireAuthorization();




app.MapPost("/api/products", async (CreateProductRequest request, IInventoryService inventoryService) =>
{
    var result = await inventoryService.CreateProductAsync(request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapGet("/api/products", async (IInventoryService inventoryService) =>
{
    var result = await inventoryService.GetAllProductsAsync();
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();
app.MapGet("/api/products/search", async (
    [AsParameters] ProductSearchRequest search,
    [AsParameters] PaginationRequest pagination,
    IInventoryService inventoryService,
    ILoggerService logger) =>
{
    logger.LogInformation("Searching products with term: {SearchTerm}, page: {Page}",
        search.SearchTerm, pagination.Page);

    var result = await inventoryService.SearchProductsAsync(search, pagination);

    if (result.IsSuccess)
    {
        logger.LogInformation("Found {Count} products, page {Page} of {TotalPages}",
            result.Data!.Items.Count, result.Data.PageNumber, result.Data.TotalPages);
    }

    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapGet("/api/products/{id:guid}", async (Guid id, IInventoryService inventoryService) =>
{
    var result = await inventoryService.GetProductByIdAsync(id);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result.ErrorMessage);
}).RequireAuthorization();

app.MapPost("/api/categories", async (CreateCategoryRequest request, IInventoryService inventoryService) =>
{
    var result = await inventoryService.CreateCategoryAsync(request.Name, request.Description, request.ParentCategoryId);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapGet("/api/categories", async (IInventoryService inventoryService) =>
{
    var result = await inventoryService.GetAllCategoriesAsync();
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapPost("/api/warehouses", async (CreateWarehouseRequest request, IInventoryService inventoryService) =>
{
    var result = await inventoryService.CreateWarehouseAsync(request.Code, request.Name, request.Address);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapGet("/api/warehouses", async (IInventoryService inventoryService) =>
{
    var result = await inventoryService.GetAllWarehousesAsync();
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapGet("/api/stock/search", async (
    [AsParameters] StockFilterRequest filter,
    [AsParameters] PaginationRequest pagination,
    IInventoryService inventoryService,
    ILoggerService logger) =>
{
    logger.LogInformation("Searching stock with warehouse: {WarehouseId}, page: {Page}",
        filter.WarehouseId, pagination.Page);

    var result = await inventoryService.SearchStockAsync(filter, pagination);

    if (result.IsSuccess)
    {
        logger.LogInformation("Found {Count} stock items", result.Data!.Items.Count);
    }

    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapGet("/api/stock/{productId:guid}/{warehouseId:guid}", async (Guid productId, Guid warehouseId, IInventoryService inventoryService) =>
{
    var result = await inventoryService.GetStockAsync(productId, warehouseId);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result.ErrorMessage);
}).RequireAuthorization();

app.MapGet("/api/stock/warehouse/{warehouseId:guid}", async (Guid warehouseId, IInventoryService inventoryService) =>
{
    var result = await inventoryService.GetAllStocksByWarehouseAsync(warehouseId);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapGet("/api/stock/low-stock", async (IInventoryService inventoryService) =>
{
    var result = await inventoryService.GetLowStockProductsAsync();
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapPost("/api/stock/adjust", async (CreateStockAdjustmentRequest request, IInventoryService inventoryService) =>
{
    var result = await inventoryService.AdjustStockAsync(request.ProductId, request.WarehouseId, request.Quantity, request.Reason);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapPost("/api/stock/transfer", async (StockTransferRequest request, IInventoryService inventoryService) =>
{
    var result = await inventoryService.TransferStockAsync(request.ProductId, request.FromWarehouseId, request.ToWarehouseId, request.Quantity);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();
// UPDATE Product
app.MapPut("/api/products/{id:guid}", async (Guid id, UpdateProductRequest request, IInventoryService inventoryService) =>
{
    var result = await inventoryService.UpdateProductAsync(id, request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// DELETE/Deactivate Product
app.MapDelete("/api/products/{id:guid}", async (Guid id, IInventoryService inventoryService) =>
{
    var result = await inventoryService.DeactivateProductAsync(id);
    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// GET Category by ID
app.MapGet("/api/categories/{id:guid}", async (Guid id, IInventoryService inventoryService) =>
{
    var result = await inventoryService.GetCategoryByIdAsync(id);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result.ErrorMessage);
}).RequireAuthorization();

// UPDATE Category
app.MapPut("/api/categories/{id:guid}", async (Guid id, UpdateCategoryRequest request, IInventoryService inventoryService) =>
{
    var result = await inventoryService.UpdateCategoryAsync(id, request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// DELETE Category
app.MapDelete("/api/categories/{id:guid}", async (Guid id, IInventoryService inventoryService) =>
{
    var result = await inventoryService.DeleteCategoryAsync(id);
    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// GET Warehouse by ID
app.MapGet("/api/warehouses/{id:guid}", async (Guid id, IInventoryService inventoryService) =>
{
    var result = await inventoryService.GetWarehouseByIdAsync(id);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result.ErrorMessage);
}).RequireAuthorization();

// UPDATE Warehouse
app.MapPut("/api/warehouses/{id:guid}", async (Guid id, UpdateWarehouseRequest request, IInventoryService inventoryService) =>
{
    var result = await inventoryService.UpdateWarehouseAsync(id, request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// DELETE/Deactivate Warehouse
app.MapDelete("/api/warehouses/{id:guid}", async (Guid id, IInventoryService inventoryService) =>
{
    var result = await inventoryService.DeactivateWarehouseAsync(id);
    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// GET Stock Movement History
app.MapGet("/api/stock-movements", async (Guid? productId, Guid? warehouseId, DateTime? startDate, DateTime? endDate, IInventoryService inventoryService) =>
{
    var result = await inventoryService.GetStockMovementsAsync(productId, warehouseId, startDate, endDate);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();
// STOCK ADJUSTMENTS ENDPOINTS (Using StockMovement records)

// GET all stock adjustments
app.MapGet("/api/stock-adjustments", async (
    Guid? productId,
    Guid? warehouseId,
    DateTime? startDate,
    DateTime? endDate,
    ERPDbContext context) =>
{
    try
    {
        var query = context.StockMovements
            .Include(sm => sm.Product)
            .Include(sm => sm.Warehouse)
            .Where(sm => sm.Type == StockMovementType.Adjustment && !sm.IsDeleted)
            .AsQueryable();

        // Apply filters
        if (productId.HasValue)
            query = query.Where(sm => sm.ProductId == productId.Value);

        if (warehouseId.HasValue)
            query = query.Where(sm => sm.WarehouseId == warehouseId.Value);

        if (startDate.HasValue)
            query = query.Where(sm => sm.MovementDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(sm => sm.MovementDate <= endDate.Value);

        var movements = await query
            .OrderByDescending(sm => sm.MovementDate)
            .ToListAsync();

        var adjustments = movements.Select(sm => new StockAdjustmentDto(
            sm.Id,
            sm.MovementNumber,
            sm.MovementDate,
            sm.ProductId,
            sm.Product.ProductCode,
            sm.Product.Name,
            sm.WarehouseId ?? Guid.Empty,
            sm.Warehouse?.Name ?? "N/A",
            sm.Quantity,
            sm.Reference?.StartsWith("INCREASE") == true ? "Increase" : "Decrease",
            sm.Notes ?? "No reason provided",
            sm.CreatedBy
        )).ToList();

        return Results.Ok(adjustments);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = $"Error retrieving stock adjustments: {ex.Message}", success = false });
    }
}).RequireAuthorization();

// GET stock adjustment by ID
app.MapGet("/api/stock-adjustments/{id:guid}", async (Guid id, ERPDbContext context) =>
{
    try
    {
        var movement = await context.StockMovements
            .Include(sm => sm.Product)
            .Include(sm => sm.Warehouse)
            .FirstOrDefaultAsync(sm => sm.Id == id && sm.Type == StockMovementType.Adjustment);

        if (movement == null)
            return Results.NotFound(new { error = "Stock adjustment not found", success = false });

        var adjustment = new StockAdjustmentDto(
            movement.Id,
            movement.MovementNumber,
            movement.MovementDate,
            movement.ProductId,
            movement.Product.ProductCode,
            movement.Product.Name,
            movement.WarehouseId ?? Guid.Empty,
            movement.Warehouse?.Name ?? "N/A",
            movement.Quantity,
            movement.Reference?.StartsWith("INCREASE") == true ? "Increase" : "Decrease",
            movement.Notes ?? "No reason provided",
            movement.CreatedBy
        );

        return Results.Ok(adjustment);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = $"Error retrieving stock adjustment: {ex.Message}", success = false });
    }
}).RequireAuthorization();

// CREATE stock adjustment (uses existing AdjustStockAsync)
app.MapPost("/api/stock-adjustments", async (
    CreateStockAdjustmentRequest request,
    IInventoryService inventoryService,
    ILoggerService logger) =>
{
    try
    {
        logger.LogInformation("Creating stock adjustment - Product: {ProductId}, Warehouse: {WarehouseId}, Quantity: {Quantity}",
            request.ProductId, request.WarehouseId, request.Quantity);

        var result = await inventoryService.AdjustStockAsync(
            request.ProductId,
            request.WarehouseId,
            request.Quantity,
            request.Reason
        );

        if (!result.IsSuccess)
        {
            logger.LogWarning("Stock adjustment failed: {Error}", result.ErrorMessage);
            return Results.BadRequest(new { error = result.ErrorMessage, success = false });
        }

        logger.LogInformation("Stock adjustment created successfully");

        return Results.Ok(new
        {
            success = true,
            message = "Stock adjustment created successfully"
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error creating stock adjustment");
        return Results.BadRequest(new { error = $"Error creating stock adjustment: {ex.Message}", success = false });
    }
}).RequireAuthorization();

// UPDATE stock adjustment - Only allow updating reason/notes
app.MapPut("/api/stock-adjustments/{id:guid}", async (
    Guid id,
    string reason,
    ERPDbContext context,
    ILoggerService logger) =>
{
    try
    {
        var movement = await context.StockMovements
            .FirstOrDefaultAsync(sm => sm.Id == id && sm.Type == StockMovementType.Adjustment);

        if (movement == null)
            return Results.NotFound(new { error = "Stock adjustment not found", success = false });

        // Only allow updating notes/reason for adjustment records
        movement.Notes = reason;
        movement.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        logger.LogInformation("Stock adjustment {Id} updated successfully", id);

        return Results.Ok(new
        {
            success = true,
            message = "Stock adjustment updated successfully"
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error updating stock adjustment {Id}", id);
        return Results.BadRequest(new { error = $"Error updating stock adjustment: {ex.Message}", success = false });
    }
}).RequireAuthorization();

// DELETE stock adjustment - Soft delete only
app.MapDelete("/api/stock-adjustments/{id:guid}", async (
    Guid id,
    ERPDbContext context,
    ILoggerService logger) =>
{
    try
    {
        var movement = await context.StockMovements
            .FirstOrDefaultAsync(sm => sm.Id == id && sm.Type == StockMovementType.Adjustment);

        if (movement == null)
            return Results.NotFound(new { error = "Stock adjustment not found", success = false });

        // Soft delete
        movement.IsDeleted = true;
        movement.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        logger.LogWarning("Stock adjustment {Id} deleted (soft delete)", id);

        return Results.Ok(new
        {
            success = true,
            message = "Stock adjustment deleted successfully"
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error deleting stock adjustment {Id}", id);
        return Results.BadRequest(new { error = $"Error deleting stock adjustment: {ex.Message}", success = false });
    }
}).RequireAuthorization();



app.MapPost("/api/suppliers", async (CreateSupplierRequest request, IProcurementService procurementService) =>
{
    var result = await procurementService.CreateSupplierAsync(request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapGet("/api/suppliers", async (IProcurementService procurementService) =>
{
    var result = await procurementService.GetAllSuppliersAsync();
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapGet("/api/purchase-orders/search", async (
    [AsParameters] PurchaseOrderSearchRequest search,
    [AsParameters] PaginationRequest pagination,
    IProcurementService procurementService,
    ILoggerService logger) =>
{
    logger.LogInformation("Searching purchase orders with term: {SearchTerm}, status: {Status}",
        search.SearchTerm, search.Status);

    var result = await procurementService.SearchPurchaseOrdersAsync(search, pagination);

    if (result.IsSuccess)
    {
        logger.LogInformation("Found {Count} purchase orders, page {Page} of {TotalPages}",
            result.Data!.Items.Count, result.Data.PageNumber, result.Data.TotalPages);
    }

    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapPost("/api/purchase-orders", async (CreatePurchaseOrderRequest request, IProcurementService procurementService) =>
{
    var result = await procurementService.CreatePurchaseOrderAsync(request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapGet("/api/purchase-orders", async (PurchaseOrderStatus? status, IProcurementService procurementService) =>
{
    var result = await procurementService.GetAllPurchaseOrdersAsync(status);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapPost("/api/purchase-orders/{orderId:guid}/approve", async (Guid orderId, Guid approvedBy, IProcurementService procurementService) =>
{
    var result = await procurementService.ApprovePurchaseOrderAsync(orderId, approvedBy);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapPost("/api/goods-receipts", async (Guid purchaseOrderId, Guid warehouseId, List<GoodsReceiptItemRequest> items, string? receivedBy, IProcurementService procurementService) =>
{
    var result = await procurementService.CreateGoodsReceiptAsync(purchaseOrderId, warehouseId, items, receivedBy);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapGet("/api/goods-receipts", async (Guid? purchaseOrderId, IProcurementService procurementService) =>
{
    var result = await procurementService.GetGoodsReceiptsAsync(purchaseOrderId);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();
// GET Supplier by ID
app.MapGet("/api/suppliers/{id:guid}", async (Guid id, IProcurementService procurementService) =>
{
    var result = await procurementService.GetSupplierByIdAsync(id);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result.ErrorMessage);
}).RequireAuthorization();

// UPDATE Supplier
app.MapPut("/api/suppliers/{id:guid}", async (Guid id, UpdateSupplierRequest request, IProcurementService procurementService) =>
{
    var result = await procurementService.UpdateSupplierAsync(id, request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// DELETE/Deactivate Supplier
app.MapDelete("/api/suppliers/{id:guid}", async (Guid id, IProcurementService procurementService) =>
{
    var result = await procurementService.DeactivateSupplierAsync(id);
    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// GET Purchase Order by ID
app.MapGet("/api/purchase-orders/{id:guid}", async (Guid id, IProcurementService procurementService) =>
{
    var result = await procurementService.GetPurchaseOrderByIdAsync(id);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result.ErrorMessage);
}).RequireAuthorization();

// UPDATE Purchase Order
app.MapPut("/api/purchase-orders/{id:guid}", async (Guid id, UpdatePurchaseOrderRequest request, IProcurementService procurementService) =>
{
    var result = await procurementService.UpdatePurchaseOrderAsync(id, request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// DELETE Purchase Order
app.MapDelete("/api/purchase-orders/{id:guid}", async (Guid id, IProcurementService procurementService) =>
{
    var result = await procurementService.DeletePurchaseOrderAsync(id);
    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// Cancel Purchase Order
app.MapPost("/api/purchase-orders/{id:guid}/cancel", async (Guid id, IProcurementService procurementService) =>
{
    var result = await procurementService.CancelPurchaseOrderAsync(id);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// GET Goods Receipt by ID
app.MapGet("/api/goods-receipts/{id:guid}", async (Guid id, IProcurementService procurementService) =>
{
    var result = await procurementService.GetGoodsReceiptByIdAsync(id);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result.ErrorMessage);
}).RequireAuthorization();

// DELETE Goods Receipt
app.MapDelete("/api/goods-receipts/{id:guid}", async (Guid id, IProcurementService procurementService) =>
{
    var result = await procurementService.DeleteGoodsReceiptAsync(id);
    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();



app.MapGet("/api/customers/search", async (
    [AsParameters] CustomerSearchRequest search,
    [AsParameters] PaginationRequest pagination,
    ISalesService salesService,
    ILoggerService logger) =>
{
    logger.LogInformation("Searching customers with term: {SearchTerm}, city: {City}",
        search.SearchTerm, search.City);

    var result = await salesService.SearchCustomersAsync(search, pagination);

    if (result.IsSuccess)
    {
        logger.LogInformation("Found {Count} customers, page {Page} of {TotalPages}",
            result.Data!.Items.Count, result.Data.PageNumber, result.Data.TotalPages);
    }

    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapGet("/api/sales-orders/search", async (
    [AsParameters] SalesOrderSearchRequest search,
    [AsParameters] PaginationRequest pagination,
    ISalesService salesService,
    ILoggerService logger) =>
{
    logger.LogInformation("Searching sales orders with term: {SearchTerm}, status: {Status}",
        search.SearchTerm, search.Status);

    var result = await salesService.SearchSalesOrdersAsync(search, pagination);

    if (result.IsSuccess)
    {
        logger.LogInformation("Found {Count} sales orders, page {Page} of {TotalPages}",
            result.Data!.Items.Count, result.Data.PageNumber, result.Data.TotalPages);
    }

    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapPost("/api/customers", async (CreateCustomerRequest request, ISalesService salesService) =>
{
    var result = await salesService.CreateCustomerAsync(request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapGet("/api/customers", async (ISalesService salesService) =>
{
    var result = await salesService.GetAllCustomersAsync();
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapPost("/api/leads", async (CreateLeadRequest request, ISalesService salesService) =>
{
    var result = await salesService.CreateLeadAsync(request.Name, request.Company, request.Email, request.PhoneNumber, request.Source);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapPost("/api/leads/{leadId:guid}/convert", async (Guid leadId, CreateCustomerRequest customerRequest, ISalesService salesService) =>
{
    var result = await salesService.ConvertLeadToCustomerAsync(leadId, customerRequest);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapPost("/api/opportunities", async (CreateOpportunityRequest request, ISalesService salesService) =>
{
    var result = await salesService.CreateOpportunityAsync(request.CustomerId, request.Name, request.EstimatedValue, request.ExpectedCloseDate);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapPost("/api/sales-orders", async (CreateSalesOrderRequest request, ISalesService salesService) =>
{
    var result = await salesService.CreateSalesOrderAsync(request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapGet("/api/sales-orders", async (SalesOrderStatus? status, ISalesService salesService) =>
{
    var result = await salesService.GetAllSalesOrdersAsync(status);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapPost("/api/sales-orders/{orderId:guid}/confirm", async (Guid orderId, ISalesService salesService) =>
{
    var result = await salesService.ConfirmSalesOrderAsync(orderId);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapPost("/api/deliveries", async (CreateDeliveryRequest request, ISalesService salesService) =>
{
    var result = await salesService.CreateDeliveryAsync(request.SalesOrderId, request.WarehouseId, request.Items);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();
// GET Customer by ID
app.MapGet("/api/customers/{id:guid}", async (Guid id, ISalesService salesService) =>
{
    var result = await salesService.GetCustomerByIdAsync(id);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result.ErrorMessage);
}).RequireAuthorization();

// UPDATE Customer
app.MapPut("/api/customers/{id:guid}", async (Guid id, UpdateCustomerRequest request, ISalesService salesService) =>
{
    var result = await salesService.UpdateCustomerAsync(id, request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// DELETE/Deactivate Customer
app.MapDelete("/api/customers/{id:guid}", async (Guid id, ISalesService salesService) =>
{
    var result = await salesService.DeactivateCustomerAsync(id);
    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// GET Lead by ID
app.MapGet("/api/leads/{id:guid}", async (Guid id, ISalesService salesService) =>
{
    var result = await salesService.GetLeadByIdAsync(id);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result.ErrorMessage);
}).RequireAuthorization();

// GET All Leads
app.MapGet("/api/leads", async (string? status, ISalesService salesService) =>
{
    var result = await salesService.GetAllLeadsAsync(status);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// UPDATE Lead
app.MapPut("/api/leads/{id:guid}", async (Guid id, UpdateLeadRequest request, ISalesService salesService) =>
{
    var result = await salesService.UpdateLeadAsync(id, request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// DELETE Lead
app.MapDelete("/api/leads/{id:guid}", async (Guid id, ISalesService salesService) =>
{
    var result = await salesService.DeleteLeadAsync(id);
    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// GET Opportunity by ID
app.MapGet("/api/opportunities/{id:guid}", async (Guid id, ISalesService salesService) =>
{
    var result = await salesService.GetOpportunityByIdAsync(id);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result.ErrorMessage);
}).RequireAuthorization();

// GET All Opportunities
app.MapGet("/api/opportunities", async (Guid? customerId, string? stage, ISalesService salesService) =>
{
    var result = await salesService.GetAllOpportunitiesAsync(customerId, stage);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// UPDATE Opportunity
app.MapPut("/api/opportunities/{id:guid}", async (Guid id, UpdateOpportunityRequest request, ISalesService salesService) =>
{
    var result = await salesService.UpdateOpportunityAsync(id, request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// DELETE Opportunity
app.MapDelete("/api/opportunities/{id:guid}", async (Guid id, ISalesService salesService) =>
{
    var result = await salesService.DeleteOpportunityAsync(id);
    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();
// GET Sales Order by ID
app.MapGet("/api/sales-orders/{id:guid}", async (Guid id, ISalesService salesService) =>
{
    var result = await salesService.GetSalesOrderByIdAsync(id);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result.ErrorMessage);
}).RequireAuthorization();

// UPDATE Sales Order
app.MapPut("/api/sales-orders/{id:guid}", async (Guid id, UpdateSalesOrderRequest request, ISalesService salesService) =>
{
    var result = await salesService.UpdateSalesOrderAsync(id, request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// DELETE Sales Order
app.MapDelete("/api/sales-orders/{id:guid}", async (Guid id, ISalesService salesService) =>
{
    var result = await salesService.DeleteSalesOrderAsync(id);
    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// Cancel Sales Order
app.MapPost("/api/sales-orders/{id:guid}/cancel", async (Guid id, ISalesService salesService) =>
{
    var result = await salesService.CancelSalesOrderAsync(id);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// GET Delivery by ID
app.MapGet("/api/deliveries/{id:guid}", async (Guid id, ISalesService salesService) =>
{
    var result = await salesService.GetDeliveryByIdAsync(id);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result.ErrorMessage);
}).RequireAuthorization();

// GET All Deliveries
app.MapGet("/api/deliveries", async (Guid? salesOrderId, ISalesService salesService) =>
{
    var result = await salesService.GetAllDeliveriesAsync(salesOrderId);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// DELETE Delivery
app.MapDelete("/api/deliveries/{id:guid}", async (Guid id, ISalesService salesService) =>
{
    var result = await salesService.DeleteDeliveryAsync(id);
    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();


// POST Create Sales Quotation
app.MapPost("/api/sales-quotations", async (CreateSalesQuotationRequest request, ISalesService salesService) =>
{
    var result = await salesService.CreateSalesQuotationAsync(request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.SalesCreateQuotations);

// GET All Sales Quotations
app.MapGet("/api/sales-quotations", async (string? status, ISalesService salesService) =>
{
    var result = await salesService.GetAllSalesQuotationsAsync(status);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.SalesViewQuotations);

// GET Sales Quotations with Search
app.MapGet("/api/sales-quotations/search", async (
    [AsParameters] SalesQuotationSearchRequest search,
    [AsParameters] PaginationRequest pagination,
    ISalesService salesService,
    ILoggerService logger) =>
{
    logger.LogInformation("Searching sales quotations with term: {SearchTerm}, status: {Status}",
        search.SearchTerm, search.Status);

    var result = await salesService.SearchSalesQuotationsAsync(search, pagination);

    if (result.IsSuccess)
    {
        logger.LogInformation("Found {Count} quotations, page {Page} of {TotalPages}",
            result.Data!.Items.Count, result.Data.PageNumber, result.Data.TotalPages);
    }

    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.SalesViewQuotations);

// GET Sales Quotation by ID
app.MapGet("/api/sales-quotations/{id:guid}", async (Guid id, ISalesService salesService) =>
{
    var result = await salesService.GetSalesQuotationByIdAsync(id);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result.ErrorMessage);
}).RequireAuthorization(Permissions.SalesViewQuotations);

// PUT Update Sales Quotation
app.MapPut("/api/sales-quotations/{id:guid}", async (Guid id, UpdateSalesQuotationRequest request, ISalesService salesService) =>
{
    var result = await salesService.UpdateSalesQuotationAsync(id, request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.SalesCreateQuotations);

// DELETE Sales Quotation
app.MapDelete("/api/sales-quotations/{id:guid}", async (Guid id, ISalesService salesService) =>
{
    var result = await salesService.DeleteSalesQuotationAsync(id);
    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.SalesCreateQuotations);

// POST Approve Sales Quotation
app.MapPost("/api/sales-quotations/{id:guid}/approve", async (Guid id, ISalesService salesService) =>
{
    var result = await salesService.ApproveSalesQuotationAsync(id);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.SalesCreateQuotations);

// POST Reject Sales Quotation
app.MapPost("/api/sales-quotations/{id:guid}/reject", async (Guid id, ISalesService salesService) =>
{
    var result = await salesService.RejectSalesQuotationAsync(id);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.SalesCreateQuotations);

// POST Convert Sales Quotation to Sales Order
app.MapPost("/api/sales-quotations/{id:guid}/convert", async (Guid id, ConvertQuotationToOrderRequest orderRequest, ISalesService salesService) =>
{
    var result = await salesService.ConvertQuotationToSalesOrderAsync(id, orderRequest);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization(Permissions.SalesCreateOrders);






app.MapGet("/api/projects/search", async (
    [AsParameters] ProjectSearchRequest search,
    [AsParameters] PaginationRequest pagination,
    IProjectService projectService,
    ILoggerService logger) =>
{
    logger.LogInformation("Searching projects with term: {SearchTerm}, status: {Status}",
        search.SearchTerm, search.Status);

    var result = await projectService.SearchProjectsAsync(search, pagination);

    if (result.IsSuccess)
    {
        logger.LogInformation("Found {Count} projects, page {Page} of {TotalPages}",
            result.Data!.Items.Count, result.Data.PageNumber, result.Data.TotalPages);
    }

    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapPost("/api/projects", async (CreateProjectRequest request, IProjectService projectService) =>
{
    var result = await projectService.CreateProjectAsync(request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapGet("/api/projects", async (ProjectStatus? status, IProjectService projectService) =>
{
    var result = await projectService.GetAllProjectsAsync(status);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapGet("/api/projects/{id:guid}", async (Guid id, IProjectService projectService) =>
{
    var result = await projectService.GetProjectByIdAsync(id);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result.ErrorMessage);
}).RequireAuthorization();

app.MapPut("/api/projects/{projectId:guid}/status", async (Guid projectId, ProjectStatus status, IProjectService projectService) =>
{
    var result = await projectService.UpdateProjectStatusAsync(projectId, status);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapPost("/api/projects/{projectId:guid}/members", async (Guid projectId, Guid employeeId, string role, decimal hourlyRate, IProjectService projectService) =>
{
    var result = await projectService.AssignProjectMemberAsync(projectId, employeeId, role, hourlyRate);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapPost("/api/tasks", async (CreateTaskRequest request, IProjectService projectService) =>
{
    var result = await projectService.CreateTaskAsync(request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapGet("/api/projects/{projectId:guid}/tasks", async (Guid projectId, IProjectService projectService) =>
{
    var result = await projectService.GetProjectTasksAsync(projectId);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapPut("/api/tasks/{taskId:guid}/status", async (Guid taskId, TaskStatus status, IProjectService projectService) =>
{
    var result = await projectService.UpdateTaskStatusAsync(taskId, status);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapPut("/api/tasks/{taskId:guid}/progress", async (Guid taskId, decimal progressPercentage, IProjectService projectService) =>
{
    var result = await projectService.UpdateTaskProgressAsync(taskId, progressPercentage);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapPost("/api/time-entries", async (CreateTimeEntryRequest request, IProjectService projectService) =>
{
    var result = await projectService.LogTimeEntryAsync(request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapGet("/api/projects/{projectId:guid}/time-entries", async (Guid projectId, DateTime? startDate, DateTime? endDate, IProjectService projectService) =>
{
    var result = await projectService.GetTimeEntriesAsync(projectId, startDate, endDate);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapGet("/api/projects/{projectId:guid}/summary", async (Guid projectId, IProjectService projectService) =>
{
    var result = await projectService.GetProjectSummaryAsync(projectId);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();
// UPDATE Project
app.MapPut("/api/projects/{id:guid}", async (Guid id, UpdateProjectRequest request, IProjectService projectService) =>
{
    var result = await projectService.UpdateProjectAsync(id, request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// DELETE Project
app.MapDelete("/api/projects/{id:guid}", async (Guid id, IProjectService projectService) =>
{
    var result = await projectService.DeleteProjectAsync(id);
    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// GET Project Members
app.MapGet("/api/projects/{projectId:guid}/members", async (Guid projectId, IProjectService projectService) =>
{
    var result = await projectService.GetProjectMembersAsync(projectId);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// Remove Project Member
app.MapDelete("/api/projects/{projectId:guid}/members/{memberId:guid}", async (Guid projectId, Guid memberId, IProjectService projectService) =>
{
    var result = await projectService.RemoveProjectMemberAsync(projectId, memberId);
    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// GET Task by ID
app.MapGet("/api/tasks/{id:guid}", async (Guid id, IProjectService projectService) =>
{
    var result = await projectService.GetTaskByIdAsync(id);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result.ErrorMessage);
}).RequireAuthorization();

// UPDATE Task
app.MapPut("/api/tasks/{id:guid}", async (Guid id, UpdateTaskRequest request, IProjectService projectService) =>
{
    var result = await projectService.UpdateTaskAsync(id, request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// DELETE Task
app.MapDelete("/api/tasks/{id:guid}", async (Guid id, IProjectService projectService) =>
{
    var result = await projectService.DeleteTaskAsync(id);
    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// GET Time Entry by ID
app.MapGet("/api/time-entries/{id:guid}", async (Guid id, IProjectService projectService) =>
{
    var result = await projectService.GetTimeEntryByIdAsync(id);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result.ErrorMessage);
}).RequireAuthorization();

// UPDATE Time Entry
app.MapPut("/api/time-entries/{id:guid}", async (Guid id, UpdateTimeEntryRequest request, IProjectService projectService) =>
{
    var result = await projectService.UpdateTimeEntryAsync(id, request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// DELETE Time Entry
app.MapDelete("/api/time-entries/{id:guid}", async (Guid id, IProjectService projectService) =>
{
    var result = await projectService.DeleteTimeEntryAsync(id);
    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// Approve Time Entry
app.MapPost("/api/time-entries/{id:guid}/approve", async (Guid id, Guid approvedBy, IProjectService projectService) =>
{
    var result = await projectService.ApproveTimeEntryAsync(id, approvedBy);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();




app.MapGet("/api/dashboard/summary", async (IReportingService reportingService) =>
{
    var result = await reportingService.GetDashboardSummaryAsync();
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// SIMPLIFIED REPORTS ENDPOINTS (for frontend without parameters)

// Simplified endpoint for frontend - Financial Report
app.MapGet("/api/reports/financial", async (
    DateTime? startDate,
    DateTime? endDate,
    AccountType? accountType,
    IReportingService reportingService) =>
{
    // Use defaults if parameters not provided
    var start = startDate ?? DateTime.Now.AddMonths(-3);
    var end = endDate ?? DateTime.Now;

    var result = await reportingService.GetFinancialReportAsync(start, end, accountType);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// Simplified endpoint for frontend - Sales Report
app.MapGet("/api/reports/sales", async (
    DateTime? startDate,
    DateTime? endDate,
    string? groupBy,
    IReportingService reportingService) =>
{
    // Use defaults if parameters not provided
    var start = startDate ?? DateTime.Now.AddMonths(-3);
    var end = endDate ?? DateTime.Now;
    var group = groupBy ?? "month";

    var result = await reportingService.GetSalesReportAsync(start, end, group);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// Simplified endpoint for frontend - Inventory Report
app.MapGet("/api/reports/inventory", async (
    bool? lowStockOnly,
    IReportingService reportingService) =>
{
    var result = await reportingService.GetInventoryReportAsync(lowStockOnly ?? false);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// Simplified endpoint for frontend - HR Report
app.MapGet("/api/reports/hr", async (
    int? month,
    int? year,
    IReportingService reportingService) =>
{
    // Use current month/year as defaults
    var currentMonth = month ?? DateTime.Now.Month;
    var currentYear = year ?? DateTime.Now.Year;

    var result = await reportingService.GetHRReportAsync(currentMonth, currentYear);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// NEW: Projects Report endpoint
app.MapGet("/api/reports/projects", async (
    DateTime? startDate,
    DateTime? endDate,
    ProjectStatus? status,
    IReportingService reportingService) =>
{
    var result = await reportingService.GetProjectsReportAsync(startDate, endDate, status);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// DASHBOARD WIDGETS ENDPOINTS

// CREATE Dashboard Widget
app.MapPost("/api/dashboards/{dashboardId:guid}/widgets", async (
    Guid dashboardId,
    CreateDashboardWidgetRequest request,
    IReportingService reportingService) =>
{
    var result = await reportingService.CreateDashboardWidgetAsync(dashboardId, request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// GET Widgets for Dashboard
app.MapGet("/api/dashboards/{dashboardId:guid}/widgets", async (
    Guid dashboardId,
    IReportingService reportingService) =>
{
    var result = await reportingService.GetDashboardWidgetsAsync(dashboardId);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// GET Dashboard Widget by ID
app.MapGet("/api/dashboard-widgets/{id:guid}", async (
    Guid id,
    IReportingService reportingService) =>
{
    var result = await reportingService.GetDashboardWidgetByIdAsync(id);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result.ErrorMessage);
}).RequireAuthorization();

// UPDATE Dashboard Widget
app.MapPut("/api/dashboard-widgets/{id:guid}", async (
    Guid id,
    UpdateDashboardWidgetRequest request,
    IReportingService reportingService) =>
{
    var result = await reportingService.UpdateDashboardWidgetAsync(id, request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// DELETE Dashboard Widget
app.MapDelete("/api/dashboard-widgets/{id:guid}", async (
    Guid id,
    IReportingService reportingService) =>
{
    var result = await reportingService.DeleteDashboardWidgetAsync(id);
    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// GET Widget Data (real-time data for widget)
app.MapGet("/api/dashboard-widgets/{id:guid}/data", async (
    Guid id,
    IReportingService reportingService) =>
{
    var result = await reportingService.GetWidgetDataAsync(id);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// REPORT SCHEDULING ENDPOINTS

// CREATE Report Schedule
app.MapPost("/api/reports/{reportId:guid}/schedules", async (
    Guid reportId,
    CreateReportScheduleRequest request,
    IReportingService reportingService) =>
{
    var result = await reportingService.CreateReportScheduleAsync(reportId, request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// GET All Report Schedules
app.MapGet("/api/report-schedules", async (
    bool? activeOnly,
    IReportingService reportingService) =>
{
    var result = await reportingService.GetAllReportSchedulesAsync(activeOnly);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// GET Report Schedule by ID
app.MapGet("/api/report-schedules/{id:guid}", async (
    Guid id,
    IReportingService reportingService) =>
{
    var result = await reportingService.GetReportScheduleByIdAsync(id);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result.ErrorMessage);
}).RequireAuthorization();

// UPDATE Report Schedule
app.MapPut("/api/report-schedules/{id:guid}", async (
    Guid id,
    UpdateReportScheduleRequest request,
    IReportingService reportingService) =>
{
    var result = await reportingService.UpdateReportScheduleAsync(id, request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// DELETE Report Schedule
app.MapDelete("/api/report-schedules/{id:guid}", async (
    Guid id,
    IReportingService reportingService) =>
{
    var result = await reportingService.DeleteReportScheduleAsync(id);
    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// RUN Report Schedule Manually
app.MapPost("/api/report-schedules/{id:guid}/run", async (
    Guid id,
    IReportingService reportingService) =>
{
    var result = await reportingService.RunReportScheduleAsync(id);
    return result.IsSuccess ? Results.Ok(new { message = "Report schedule executed successfully" }) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapGet("/api/utilities/departments", async (ERPDbContext context) =>
{
    var departments = await context.Employees
        .Where(e => e.Department != null && e.IsActive)
        .Select(e => e.Department)
        .Distinct()
        .OrderBy(d => d)
        .ToListAsync();

    return Results.Ok(departments);
}).RequireAuthorization();

app.MapGet("/api/utilities/positions", async (ERPDbContext context) =>
{
    var positions = await context.Employees
        .Where(e => e.Position != null && e.IsActive)
        .Select(e => e.Position)
        .Distinct()
        .OrderBy(p => p)
        .ToListAsync();

    return Results.Ok(positions);
}).RequireAuthorization();

app.MapGet("/api/utilities/cities", async (ERPDbContext context) =>
{
    var cities = await context.Customers
        .Where(c => c.City != null && c.IsActive)
        .Select(c => c.City)
        .Distinct()
        .OrderBy(c => c)
        .ToListAsync();

    return Results.Ok(cities);
}).RequireAuthorization();

app.MapGet("/api/utilities/countries", async (ERPDbContext context) =>
{
    var countries = await context.Customers
        .Where(c => c.Country != null && c.IsActive)
        .Select(c => c.Country)
        .Distinct()
        .OrderBy(c => c)
        .ToListAsync();

    return Results.Ok(countries);
}).RequireAuthorization();
// GET All Dashboards
app.MapGet("/api/dashboards", async (Guid? userId, IReportingService reportingService) =>
{
    var result = await reportingService.GetAllDashboardsAsync(userId);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// GET Dashboard by ID
app.MapGet("/api/dashboards/{id:guid}", async (Guid id, IReportingService reportingService) =>
{
    var result = await reportingService.GetDashboardByIdAsync(id);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result.ErrorMessage);
}).RequireAuthorization();

// CREATE Dashboard
app.MapPost("/api/dashboards", async (CreateDashboardRequest request, IReportingService reportingService) =>
{
    var result = await reportingService.CreateDashboardAsync(request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// UPDATE Dashboard
app.MapPut("/api/dashboards/{id:guid}", async (Guid id, UpdateDashboardRequest request, IReportingService reportingService) =>
{
    var result = await reportingService.UpdateDashboardAsync(id, request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// DELETE Dashboard
app.MapDelete("/api/dashboards/{id:guid}", async (Guid id, IReportingService reportingService) =>
{
    var result = await reportingService.DeleteDashboardAsync(id);
    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// GET All Reports
app.MapGet("/api/reports", async (string? module, IReportingService reportingService) =>
{
    var result = await reportingService.GetAllReportsAsync(module);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// GET Report by ID
app.MapGet("/api/reports/{id:guid}", async (Guid id, IReportingService reportingService) =>
{
    var result = await reportingService.GetReportByIdAsync(id);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result.ErrorMessage);
}).RequireAuthorization();

// CREATE Report
app.MapPost("/api/reports", async (CreateReportRequest request, IReportingService reportingService) =>
{
    var result = await reportingService.CreateReportAsync(request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// UPDATE Report
app.MapPut("/api/reports/{id:guid}", async (Guid id, UpdateReportRequest request, IReportingService reportingService) =>
{
    var result = await reportingService.UpdateReportAsync(id, request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// DELETE Report
app.MapDelete("/api/reports/{id:guid}", async (Guid id, IReportingService reportingService) =>
{
    var result = await reportingService.DeleteReportAsync(id);
    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

// EXPENSE MANAGEMENT API ENDPOINTS


app.MapPost("/api/expenses", async (CreateExpenseRequest request, IExpenseService expenseService) =>
{
    var result = await expenseService.CreateExpenseAsync(request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapGet("/api/expenses", async (bool? isApproved, IExpenseService expenseService) =>
{
    var result = await expenseService.GetAllExpensesAsync(isApproved);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapGet("/api/expenses/search", async (
    [AsParameters] ExpenseSearchRequest search,
    [AsParameters] PaginationRequest pagination,
    IExpenseService expenseService,
    ILoggerService logger) =>
{
    logger.LogInformation("Searching expenses with term: {SearchTerm}, category: {Category}",
        search.SearchTerm, search.Category);

    var result = await expenseService.SearchExpensesAsync(search, pagination);

    if (result.IsSuccess)
    {
        logger.LogInformation("Found {Count} expenses, page {Page} of {TotalPages}",
            result.Data!.Items.Count, result.Data.PageNumber, result.Data.TotalPages);
    }

    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapGet("/api/expenses/{id:guid}", async (Guid id, IExpenseService expenseService) =>
{
    var result = await expenseService.GetExpenseByIdAsync(id);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result.ErrorMessage);
}).RequireAuthorization();

app.MapPut("/api/expenses/{id:guid}", async (Guid id, UpdateExpenseRequest request, IExpenseService expenseService) =>
{
    var result = await expenseService.UpdateExpenseAsync(id, request);
    return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapDelete("/api/expenses/{id:guid}", async (Guid id, IExpenseService expenseService) =>
{
    var result = await expenseService.DeleteExpenseAsync(id);
    return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapPost("/api/expenses/{id:guid}/approve", async (Guid id, Guid approvedBy, IExpenseService expenseService) =>
{
    var result = await expenseService.ApproveExpenseAsync(id, approvedBy);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapPost("/api/expenses/{id:guid}/reject", async (Guid id, IExpenseService expenseService) =>
{
    var result = await expenseService.RejectExpenseAsync(id);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();

app.MapGet("/api/expenses/total", async (DateTime startDate, DateTime endDate, string? category, IExpenseService expenseService) =>
{
    var result = await expenseService.GetTotalExpensesAsync(startDate, endDate, category);
    return result.IsSuccess ? Results.Ok(new { Total = result.Data }) : Results.BadRequest(result.ErrorMessage);
}).RequireAuthorization();





using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ERPDbContext>();

    // Ensure database is created
    context.Database.EnsureCreated();

    // Seed default data if needed
    if (!context.Users.Any())
    {
        var adminUser = new User
        {
            Username = "admin",
            Email = "admin@erp.com",
            PasswordHash = PasswordHasher.HashPassword("Admin@123"),
            FirstName = "System",
            LastName = "Administrator",
            Status = UserStatus.Active
        };

        var adminRole = new Role
        {
            Name = "Administrator",
            Description = "Full system access",
            Permissions = new List<string> { "all" }
        };

        context.Users.Add(adminUser);
        context.Roles.Add(adminRole);
        context.SaveChanges();

        context.UserRoles.Add(new UserRole
        {
            UserId = adminUser.Id,
            RoleId = adminRole.Id
        });

        context.SaveChanges();

        Console.WriteLine("? Default admin user created:");
        Console.WriteLine("   Email: admin@erp.com");
        Console.WriteLine("   Password: Admin@123");
    }
}




// POST Ask HR Chatbot a question
app.MapPost("/api/hr-chatbot/ask", async (ChatbotAskRequest request, IHRChatbotService hrChatbotService, ILoggerService logger) =>
{
    if (string.IsNullOrWhiteSpace(request.Question))
    {
        logger.LogWarning("Empty HR chatbot question received");
        return Results.BadRequest(new { success = false, error = "Question cannot be empty" });
    }

    logger.LogInformation($"?? HR Chatbot Query: {request.Question}");

    try
    {
        var result = await hrChatbotService.AskAsync(request.Question);

        if (!result.Success)
        {
            logger.LogWarning($"?? HR Chatbot failed: {result.Error}");
            return Results.BadRequest(new { success = false, error = result.Error });
        }

        logger.LogInformation($"? HR Chatbot answer generated (processing time: {result.ProcessingTimeMs}ms)");

        return Results.Ok(new
        {
            success = result.Success,
            answer = result.Answer,
            sources = result.Sources,
            processingTimeMs = result.ProcessingTimeMs,
            documentsRetrieved = result.DocumentsRetrieved
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "HR Chatbot processing error");
        return Results.Json(new { success = false, error = "Internal server error" }, statusCode: StatusCodes.Status500InternalServerError);
    }
}).AllowAnonymous();

// GET Check HR Chatbot health
app.MapGet("/api/hr-chatbot/health", async (IHRChatbotService hrChatbotService, ILoggerService logger) =>
{
    logger.LogInformation("?? Checking HR Chatbot health...");

    try
    {
        var health = await hrChatbotService.GetHealthAsync();
        return Results.Ok(new
        {
            status = health.Status,
            timestamp = health.Timestamp,
            chatbotReady = health.ChatbotReady
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "HR Chatbot health check failed");
        return Results.Json(new { status = "error", chatbotReady = false }, statusCode: StatusCodes.Status500InternalServerError);
    }
}).AllowAnonymous();

// GET Get HR Chatbot configuration
app.MapGet("/api/hr-chatbot/config", async (IHRChatbotService hrChatbotService, ILoggerService logger) =>
{
    logger.LogInformation("?? Getting HR Chatbot configuration...");

    try
    {
        var config = await hrChatbotService.GetConfigAsync();
        return Results.Ok(new
        {
            embeddingModel = config.EmbeddingModel,
            chunkSize = config.ChunkSize,
            retrievalK = config.RetrievalK,
            llmModel = config.LlmModel,
            temperature = config.Temperature,
            pdfFile = config.PdfFile
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "HR Chatbot config retrieval failed");
        return Results.Json(new { error = "Failed to retrieve configuration" }, statusCode: StatusCodes.Status500InternalServerError);
    }
}).AllowAnonymous();


app.Run();
