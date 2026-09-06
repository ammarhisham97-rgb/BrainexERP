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
/// Provides dashboard, reporting, export, and report scheduling operations.
/// </summary>
public interface IReportingService
{
    Task<Result<DashboardSummary>> GetDashboardSummaryAsync();
    Task<Result<List<SalesReportData>>> GetSalesReportAsync(DateTime startDate, DateTime endDate, string groupBy = "day");
    Task<Result<List<InventoryReportData>>> GetInventoryReportAsync(bool lowStockOnly = false);
    Task<Result<List<FinancialReportData>>> GetFinancialReportAsync(DateTime startDate, DateTime endDate, AccountType? accountType = null);
    Task<Result<List<HRReportData>>> GetHRReportAsync(int month, int year);
    Task<Result<List<ProjectReportData>>> GetProjectsReportAsync(DateTime? startDate = null, DateTime? endDate = null, ProjectStatus? status = null);
    Task<Result<byte[]>> ExportReportToPdfAsync(string reportType, Dictionary<string, object> parameters);
    Task<Result<byte[]>> ExportReportToExcelAsync(string reportType, Dictionary<string, object> parameters);

    Task<Result<List<DashboardDto>>> GetAllDashboardsAsync(Guid? userId = null);
    Task<Result<DashboardDto>> GetDashboardByIdAsync(Guid id);
    Task<Result<DashboardDto>> CreateDashboardAsync(CreateDashboardRequest request);
    Task<Result<DashboardDto>> UpdateDashboardAsync(Guid id, UpdateDashboardRequest request);
    Task<Result> DeleteDashboardAsync(Guid id);

    Task<Result<DashboardWidgetDto>> CreateDashboardWidgetAsync(Guid dashboardId, CreateDashboardWidgetRequest request);
    Task<Result<List<DashboardWidgetDto>>> GetDashboardWidgetsAsync(Guid dashboardId);
    Task<Result<DashboardWidgetDto>> GetDashboardWidgetByIdAsync(Guid id);
    Task<Result<DashboardWidgetDto>> UpdateDashboardWidgetAsync(Guid id, UpdateDashboardWidgetRequest request);
    Task<Result> DeleteDashboardWidgetAsync(Guid id);
    Task<Result<Dictionary<string, object>>> GetWidgetDataAsync(Guid widgetId);

    Task<Result<List<ReportDto>>> GetAllReportsAsync(string? module = null);
    Task<Result<ReportDto>> GetReportByIdAsync(Guid id);
    Task<Result<ReportDto>> CreateReportAsync(CreateReportRequest request);
    Task<Result<ReportDto>> UpdateReportAsync(Guid id, UpdateReportRequest request);
    Task<Result> DeleteReportAsync(Guid id);

    Task<Result<ReportScheduleDto>> CreateReportScheduleAsync(Guid reportId, CreateReportScheduleRequest request);
    Task<Result<List<ReportScheduleDto>>> GetAllReportSchedulesAsync(bool? activeOnly = null);
    Task<Result<ReportScheduleDto>> GetReportScheduleByIdAsync(Guid id);
    Task<Result<ReportScheduleDto>> UpdateReportScheduleAsync(Guid id, UpdateReportScheduleRequest request);
    Task<Result> DeleteReportScheduleAsync(Guid id);
    Task<Result> RunReportScheduleAsync(Guid id);
}
/// <summary>
/// Provides expense creation, review, approval, and reporting operations.
/// </summary>
public interface IExpenseService
{
    Task<Result<ExpenseDto>> CreateExpenseAsync(CreateExpenseRequest request);
    Task<Result<List<ExpenseDto>>> GetAllExpensesAsync(bool? isApproved = null);
    Task<Result<ExpenseDto>> GetExpenseByIdAsync(Guid id);
    Task<Result<ExpenseDto>> UpdateExpenseAsync(Guid id, UpdateExpenseRequest request);
    Task<Result> DeleteExpenseAsync(Guid id);
    Task<Result> ApproveExpenseAsync(Guid id, Guid approvedBy);
    Task<Result> RejectExpenseAsync(Guid id);
    Task<Result<PagedResult<ExpenseDto>>> SearchExpensesAsync(ExpenseSearchRequest search, PaginationRequest pagination);
    Task<Result<decimal>> GetTotalExpensesAsync(DateTime startDate, DateTime endDate, string? category = null);
}



