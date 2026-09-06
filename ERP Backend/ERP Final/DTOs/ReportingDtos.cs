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
/// Represents the dashboard summary data contract.
/// </summary>
public record DashboardSummary(
    int TotalUsers,
    int TotalEmployees,
    int TotalCustomers,
    int TotalSuppliers,
    int TotalProducts,
    decimal TotalRevenue,
    decimal TotalExpenses,
    decimal NetProfit,
    int ActiveProjects,
    int PendingOrders
);

/// <summary>
/// Represents the sales report data data contract.
/// </summary>
public record SalesReportData(
    DateTime Period,
    decimal TotalSales,
    int OrderCount,
    decimal AverageOrderValue
);

/// <summary>
/// Represents the inventory report data data contract.
/// </summary>
public record InventoryReportData(
    Guid ProductId,
    string ProductCode,
    string ProductName,
    decimal QuantityOnHand,
    decimal QuantityReserved,
    decimal ReorderLevel,
    bool NeedsReorder
);

/// <summary>
/// Represents the financial report data data contract.
/// </summary>
public record FinancialReportData(
    string AccountName,
    AccountType AccountType,
    decimal Debit,
    decimal Credit,
    decimal Balance
);

/// <summary>
/// Represents the h r report data data contract.
/// </summary>
public record HRReportData(
    Guid EmployeeId,
    string EmployeeName,
    string Department,
    int PresentDays,
    int AbsentDays,
    int LeaveDays,
    decimal TotalSalary
);
/// <summary>
/// Represents the dashboard dto data contract.
/// </summary>
public record DashboardDto(Guid Id, string Name, string? Description, Guid UserId, bool IsDefault);
/// <summary>
/// Represents the create dashboard request data contract.
/// </summary>
public record CreateDashboardRequest(string Name, string? Description, Guid UserId, bool IsDefault);
/// <summary>
/// Represents the update dashboard request data contract.
/// </summary>
public record UpdateDashboardRequest(string? Name, string? Description, bool? IsDefault);
/// <summary>
/// Represents the report dto data contract.
/// </summary>
public record ReportDto(Guid Id, string Name, string? Description, string Module, string ReportType, bool IsPublic);
/// <summary>
/// Represents the create report request data contract.
/// </summary>
public record CreateReportRequest(string Name, string? Description, string Module, string ReportType, string? QueryDefinition, bool IsPublic, Guid? CreatedByUserId);
/// <summary>
/// Represents the update report request data contract.
/// </summary>
public record UpdateReportRequest(string? Name, string? Description, string? QueryDefinition, bool? IsPublic);

// Dashboard Widgets DTOs
/// <summary>
/// Represents the dashboard widget dto data contract.
/// </summary>
public record DashboardWidgetDto(
    Guid Id,
    Guid DashboardId,
    string Title,
    string WidgetType,
    string? DataSource,
    string? Configuration,
    int PositionX,
    int PositionY,
    int Width,
    int Height,
    int RefreshInterval
);

/// <summary>
/// Represents the create dashboard widget request data contract.
/// </summary>
public record CreateDashboardWidgetRequest(
    string Title,
    string WidgetType,
    string? DataSource,
    string? Configuration,
    int PositionX,
    int PositionY,
    int Width,
    int Height,
    int RefreshInterval
);

/// <summary>
/// Represents the update dashboard widget request data contract.
/// </summary>
public record UpdateDashboardWidgetRequest(
    string? Title,
    string? WidgetType,
    string? DataSource,
    string? Configuration,
    int? PositionX,
    int? PositionY,
    int? Width,
    int? Height,
    int? RefreshInterval
);

// Report Scheduling DTOs
/// <summary>
/// Represents the report schedule dto data contract.
/// </summary>
public record ReportScheduleDto(
    Guid Id,
    Guid ReportId,
    string ReportName,
    string Name,
    string Frequency,
    string? CronExpression,
    List<string> Recipients,
    string Format,
    DateTime? LastRunAt,
    DateTime? NextRunAt,
    bool IsActive
);

/// <summary>
/// Represents the create report schedule request data contract.
/// </summary>
public record CreateReportScheduleRequest(
    string Name,
    string Frequency,
    string? CronExpression,
    List<string> Recipients,
    string Format
);

/// <summary>
/// Represents the update report schedule request data contract.
/// </summary>
public record UpdateReportScheduleRequest(
    string? Name,
    string? Frequency,
    string? CronExpression,
    List<string>? Recipients,
    string? Format,
    bool? IsActive
);

// Projects Report DTOs
/// <summary>
/// Represents the project report data data contract.
/// </summary>
public record ProjectReportData(
    Guid ProjectId,
    string ProjectCode,
    string ProjectName,
    string? CustomerName,
    ProjectStatus Status,
    DateTime StartDate,
    DateTime? EndDate,
    decimal BudgetAmount,
    decimal ActualCost,
    decimal BudgetVariance,
    decimal CompletionPercentage,
    int TotalTasks,
    int CompletedTasks,
    decimal TotalHours,
    int TeamMemberCount
);

// Invoice Search Request
/// <summary>
/// Represents the invoice search request data contract.
/// </summary>
public record InvoiceSearchRequest
{
    public string? SearchTerm { get; init; }
    public Guid? CustomerId { get; init; }
    public InvoiceStatus? Status { get; init; }
    public DateTime? InvoiceDateFrom { get; init; }
    public DateTime? InvoiceDateTo { get; init; }
    public DateTime? DueDateFrom { get; init; }
    public DateTime? DueDateTo { get; init; }
    public decimal? MinAmount { get; init; }
    public decimal? MaxAmount { get; init; }
    public bool? Overdue { get; init; }
}

// Purchase Order Search Request
/// <summary>
/// Represents the purchase order search request data contract.
/// </summary>
public record PurchaseOrderSearchRequest
{
    public string? SearchTerm { get; init; }
    public Guid? SupplierId { get; init; }
    public PurchaseOrderStatus? Status { get; init; }
    public DateTime? OrderDateFrom { get; init; }
    public DateTime? OrderDateTo { get; init; }
    public decimal? MinAmount { get; init; }
    public decimal? MaxAmount { get; init; }
}

// DTOs
/// <summary>
/// Represents the expense dto data contract.
/// </summary>
public record ExpenseDto(
    Guid Id,
    string ExpenseNumber,
    DateTime ExpenseDate,
    string Category,
    decimal Amount,
    string? SupplierName,
    bool IsApproved,
    string? ApprovedByName
);

/// <summary>
/// Represents the create expense request data contract.
/// </summary>
public record CreateExpenseRequest(
    DateTime ExpenseDate,
    string Category,
    decimal Amount,
    Guid? SupplierId,
    string? Description,
    string? ReceiptNumber
);

/// <summary>
/// Represents the update expense request data contract.
/// </summary>
public record UpdateExpenseRequest(
    DateTime? ExpenseDate,
    string? Category,
    decimal? Amount,
    string? Description,
    string? ReceiptNumber
);

/// <summary>
/// Represents the expense search request data contract.
/// </summary>
public record ExpenseSearchRequest
{
    public string? SearchTerm { get; init; }
    public string? Category { get; init; }
    public Guid? SupplierId { get; init; }
    public bool? IsApproved { get; init; }
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
    public decimal? MinAmount { get; init; }
    public decimal? MaxAmount { get; init; }
}

