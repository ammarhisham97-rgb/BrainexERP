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
/// Represents the reporting service domain model.
/// </summary>
public class ReportingService : IReportingService
{
    private readonly ERPDbContext _context;

    public ReportingService(ERPDbContext context)
    {
        _context = context;
    }

    public async Task<Result<DashboardSummary>> GetDashboardSummaryAsync()
    {
        var totalUsers = await _context.Users.CountAsync();
        var totalEmployees = await _context.Employees.CountAsync(e => e.IsActive);
        var totalCustomers = await _context.Customers.CountAsync(c => c.IsActive);
        var totalSuppliers = await _context.Suppliers.CountAsync(s => s.IsActive);
        var totalProducts = await _context.Products.CountAsync(p => p.IsActive);

        var totalRevenue = await _context.Invoices
            .Where(i => i.Status == InvoiceStatus.Paid)
            .SumAsync(i => i.TotalAmount);

        var totalExpenses = await _context.Expenses
            .Where(e => e.IsApproved)
            .SumAsync(e => e.Amount);

        var netProfit = totalRevenue - totalExpenses;

        var activeProjects = await _context.Projects
            .CountAsync(p => p.Status == ProjectStatus.InProgress);

        var pendingOrders = await _context.SalesOrders
            .CountAsync(so => so.Status == SalesOrderStatus.Draft || so.Status == SalesOrderStatus.Confirmed);

        var summary = new DashboardSummary(
            totalUsers,
            totalEmployees,
            totalCustomers,
            totalSuppliers,
            totalProducts,
            totalRevenue,
            totalExpenses,
            netProfit,
            activeProjects,
            pendingOrders
        );

        return Result<DashboardSummary>.Success(summary);
    }

    public async Task<Result<List<SalesReportData>>> GetSalesReportAsync(DateTime startDate, DateTime endDate, string groupBy = "day")
    {
        var orders = await _context.SalesOrders
            .Where(so => so.OrderDate >= startDate && so.OrderDate <= endDate && so.Status == SalesOrderStatus.Delivered)
            .ToListAsync();

        IEnumerable<IGrouping<DateTime, SalesOrder>> grouped = groupBy.ToLower() switch
        {
            "day" => orders.GroupBy(o => o.OrderDate.Date),
            "week" => orders.GroupBy(o => o.OrderDate.Date.AddDays(-(int)o.OrderDate.DayOfWeek)),
            "month" => orders.GroupBy(o => new DateTime(o.OrderDate.Year, o.OrderDate.Month, 1)),
            _ => orders.GroupBy(o => o.OrderDate.Date)
        };

        var reportData = grouped.Select(g => new SalesReportData(
            g.Key,
            g.Sum(o => o.TotalAmount),
            g.Count(),
            g.Average(o => o.TotalAmount)
        )).OrderBy(r => r.Period).ToList();

        return Result<List<SalesReportData>>.Success(reportData);
    }

    public async Task<Result<List<InventoryReportData>>> GetInventoryReportAsync(bool lowStockOnly = false)
    {
        var query = _context.Products
            .Include(p => p.Stocks)
            .Where(p => p.IsActive && p.TrackInventory)
            .AsQueryable();

        var products = await query.ToListAsync();

        var reportData = products.Select(p =>
        {
            var totalStock = p.Stocks.Sum(s => s.QuantityOnHand);
            var totalReserved = p.Stocks.Sum(s => s.QuantityReserved);
            var needsReorder = totalStock <= p.ReorderLevel;

            return new InventoryReportData(
                p.Id,
                p.ProductCode,
                p.Name,
                totalStock,
                totalReserved,
                p.ReorderLevel,
                needsReorder
            );
        }).ToList();

        if (lowStockOnly)
        {
            reportData = reportData.Where(r => r.NeedsReorder).ToList();
        }

        return Result<List<InventoryReportData>>.Success(reportData);
    }

    public async Task<Result<List<FinancialReportData>>> GetFinancialReportAsync(DateTime startDate, DateTime endDate, AccountType? accountType = null)
    {
        var query = _context.Accounts
            .Include(a => a.JournalEntryLines)
            .ThenInclude(l => l.JournalEntry)
            .Where(a => a.IsActive)
            .AsQueryable();

        if (accountType.HasValue)
        {
            query = query.Where(a => a.Type == accountType.Value);
        }

        var accounts = await query.ToListAsync();

        var reportData = accounts.Select(a =>
        {
            var relevantLines = a.JournalEntryLines
                .Where(l => l.JournalEntry.IsPosted && l.JournalEntry.EntryDate >= startDate && l.JournalEntry.EntryDate <= endDate)
                .ToList();

            var totalDebit = relevantLines.Sum(l => l.DebitAmount);
            var totalCredit = relevantLines.Sum(l => l.CreditAmount);

            return new FinancialReportData(
                a.AccountName,
                a.Type,
                totalDebit,
                totalCredit,
                a.Balance
            );
        }).ToList();

        return Result<List<FinancialReportData>>.Success(reportData);
    }

    public async Task<Result<List<HRReportData>>> GetHRReportAsync(int month, int year)
    {
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        var employees = await _context.Employees
            .Include(e => e.Attendances)
            .Include(e => e.Leaves)
            .Include(e => e.Payrolls)
            .Where(e => e.IsActive)
            .ToListAsync();

        var reportData = employees.Select(e =>
        {
            var attendances = e.Attendances.Where(a => a.Date >= startDate && a.Date <= endDate).ToList();
            var presentDays = attendances.Count(a => a.Status == AttendanceStatus.Present);
            var absentDays = attendances.Count(a => a.Status == AttendanceStatus.Absent);

            var leaves = e.Leaves
                .Where(l => l.Status == LeaveStatus.Approved && l.StartDate <= endDate && l.EndDate >= startDate)
                .ToList();
            var leaveDays = leaves.Sum(l => l.TotalDays);

            var payroll = e.Payrolls.FirstOrDefault(p => p.Month == month && p.Year == year);
            var totalSalary = payroll?.NetSalary ?? 0;

            return new HRReportData(
                e.Id,
                $"{e.FirstName} {e.LastName}",
                e.Department ?? "N/A",
                presentDays,
                absentDays,
                leaveDays,
                totalSalary
            );
        }).ToList();

        return Result<List<HRReportData>>.Success(reportData);
    }

    public async Task<Result<byte[]>> ExportReportToPdfAsync(string reportType, Dictionary<string, object> parameters)
    {

        await Task.CompletedTask;
        return Result<byte[]>.Failure("PDF export not implemented");
    }

    public async Task<Result<byte[]>> ExportReportToExcelAsync(string reportType, Dictionary<string, object> parameters)
    {

        await Task.CompletedTask;
        return Result<byte[]>.Failure("Excel export not implemented");
    }
    public async Task<Result<List<DashboardDto>>> GetAllDashboardsAsync(Guid? userId = null)
    {
        var query = _context.Dashboards.AsQueryable();

        if (userId.HasValue)
            query = query.Where(d => d.UserId == userId.Value);

        var dashboards = await query.ToListAsync();

        var dtos = dashboards.Select(d => new DashboardDto(
            d.Id, d.Name, d.Description, d.UserId, d.IsDefault
        )).ToList();

        return Result<List<DashboardDto>>.Success(dtos);
    }

    public async Task<Result<DashboardDto>> GetDashboardByIdAsync(Guid id)
    {
        var dashboard = await _context.Dashboards.FindAsync(id);
        if (dashboard == null)
            return Result<DashboardDto>.Failure("Dashboard not found");

        var dto = new DashboardDto(dashboard.Id, dashboard.Name, dashboard.Description,
            dashboard.UserId, dashboard.IsDefault);

        return Result<DashboardDto>.Success(dto);
    }

    public async Task<Result<DashboardDto>> CreateDashboardAsync(CreateDashboardRequest request)
    {

        if (request.IsDefault)
        {
            var existingDefaults = await _context.Dashboards
                .Where(d => d.UserId == request.UserId && d.IsDefault)
                .ToListAsync();

            foreach (var dash in existingDefaults)
            {
                dash.IsDefault = false;
            }
        }

        var dashboard = new Dashboard
        {
            Name = request.Name,
            Description = request.Description,
            UserId = request.UserId,
            IsDefault = request.IsDefault
        };

        _context.Dashboards.Add(dashboard);
        await _context.SaveChangesAsync();

        var dto = new DashboardDto(dashboard.Id, dashboard.Name, dashboard.Description,
            dashboard.UserId, dashboard.IsDefault);

        return Result<DashboardDto>.Success(dto);
    }

    public async Task<Result<DashboardDto>> UpdateDashboardAsync(Guid id, UpdateDashboardRequest request)
    {
        var dashboard = await _context.Dashboards.FindAsync(id);
        if (dashboard == null)
            return Result<DashboardDto>.Failure("Dashboard not found");

        if (request.Name != null) dashboard.Name = request.Name;
        if (request.Description != null) dashboard.Description = request.Description;

        if (request.IsDefault.HasValue && request.IsDefault.Value)
        {

            var existingDefaults = await _context.Dashboards
                .Where(d => d.UserId == dashboard.UserId && d.IsDefault && d.Id != id)
                .ToListAsync();

            foreach (var dash in existingDefaults)
            {
                dash.IsDefault = false;
            }

            dashboard.IsDefault = true;
        }
        else if (request.IsDefault.HasValue)
        {
            dashboard.IsDefault = false;
        }

        dashboard.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var dto = new DashboardDto(dashboard.Id, dashboard.Name, dashboard.Description,
            dashboard.UserId, dashboard.IsDefault);

        return Result<DashboardDto>.Success(dto);
    }

    public async Task<Result> DeleteDashboardAsync(Guid id)
    {
        var dashboard = await _context.Dashboards.FindAsync(id);
        if (dashboard == null)
            return Result.Failure("Dashboard not found");

        _context.Dashboards.Remove(dashboard);
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<List<ReportDto>>> GetAllReportsAsync(string? module = null)
    {
        var query = _context.Reports.AsQueryable();

        if (!string.IsNullOrWhiteSpace(module))
            query = query.Where(r => r.Module == module);

        var reports = await query.ToListAsync();

        var dtos = reports.Select(r => new ReportDto(
            r.Id, r.Name, r.Description, r.Module, r.ReportType, r.IsPublic
        )).ToList();

        return Result<List<ReportDto>>.Success(dtos);
    }

    public async Task<Result<ReportDto>> GetReportByIdAsync(Guid id)
    {
        var report = await _context.Reports.FindAsync(id);
        if (report == null)
            return Result<ReportDto>.Failure("Report not found");

        var dto = new ReportDto(report.Id, report.Name, report.Description,
            report.Module, report.ReportType, report.IsPublic);

        return Result<ReportDto>.Success(dto);
    }

    public async Task<Result<ReportDto>> CreateReportAsync(CreateReportRequest request)
    {
        var report = new Report
        {
            Name = request.Name,
            Description = request.Description,
            Module = request.Module,
            ReportType = request.ReportType,
            QueryDefinition = request.QueryDefinition,
            IsPublic = request.IsPublic,
            CreatedByUserId = request.CreatedByUserId
        };

        _context.Reports.Add(report);
        await _context.SaveChangesAsync();

        var dto = new ReportDto(report.Id, report.Name, report.Description,
            report.Module, report.ReportType, report.IsPublic);

        return Result<ReportDto>.Success(dto);
    }

    public async Task<Result<ReportDto>> UpdateReportAsync(Guid id, UpdateReportRequest request)
    {
        var report = await _context.Reports.FindAsync(id);
        if (report == null)
            return Result<ReportDto>.Failure("Report not found");

        if (request.Name != null) report.Name = request.Name;
        if (request.Description != null) report.Description = request.Description;
        if (request.QueryDefinition != null) report.QueryDefinition = request.QueryDefinition;
        if (request.IsPublic.HasValue) report.IsPublic = request.IsPublic.Value;

        report.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var dto = new ReportDto(report.Id, report.Name, report.Description,
            report.Module, report.ReportType, report.IsPublic);

        return Result<ReportDto>.Success(dto);
    }

    public async Task<Result> DeleteReportAsync(Guid id)
    {
        var report = await _context.Reports.FindAsync(id);
        if (report == null)
            return Result.Failure("Report not found");

        _context.Reports.Remove(report);
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    // Dashboard Widgets Implementation
    public async Task<Result<DashboardWidgetDto>> CreateDashboardWidgetAsync(Guid dashboardId, CreateDashboardWidgetRequest request)
    {
        var dashboard = await _context.Dashboards.FindAsync(dashboardId);
        if (dashboard == null)
            return Result<DashboardWidgetDto>.Failure("Dashboard not found");

        var widget = new DashboardWidget
        {
            DashboardId = dashboardId,
            Title = request.Title,
            WidgetType = request.WidgetType,
            DataSource = request.DataSource,
            Configuration = request.Configuration,
            PositionX = request.PositionX,
            PositionY = request.PositionY,
            Width = request.Width,
            Height = request.Height,
            RefreshInterval = request.RefreshInterval
        };

        _context.DashboardWidgets.Add(widget);
        await _context.SaveChangesAsync();

        var dto = new DashboardWidgetDto(
            widget.Id, widget.DashboardId, widget.Title, widget.WidgetType,
            widget.DataSource, widget.Configuration, widget.PositionX, widget.PositionY,
            widget.Width, widget.Height, widget.RefreshInterval
        );

        return Result<DashboardWidgetDto>.Success(dto);
    }

    public async Task<Result<List<DashboardWidgetDto>>> GetDashboardWidgetsAsync(Guid dashboardId)
    {
        var widgets = await _context.DashboardWidgets
            .Where(w => w.DashboardId == dashboardId && !w.IsDeleted)
            .OrderBy(w => w.PositionY)
            .ThenBy(w => w.PositionX)
            .ToListAsync();

        var dtos = widgets.Select(w => new DashboardWidgetDto(
            w.Id, w.DashboardId, w.Title, w.WidgetType, w.DataSource,
            w.Configuration, w.PositionX, w.PositionY, w.Width, w.Height, w.RefreshInterval
        )).ToList();

        return Result<List<DashboardWidgetDto>>.Success(dtos);
    }

    public async Task<Result<DashboardWidgetDto>> GetDashboardWidgetByIdAsync(Guid id)
    {
        var widget = await _context.DashboardWidgets.FindAsync(id);
        if (widget == null || widget.IsDeleted)
            return Result<DashboardWidgetDto>.Failure("Widget not found");

        var dto = new DashboardWidgetDto(
            widget.Id, widget.DashboardId, widget.Title, widget.WidgetType,
            widget.DataSource, widget.Configuration, widget.PositionX, widget.PositionY,
            widget.Width, widget.Height, widget.RefreshInterval
        );

        return Result<DashboardWidgetDto>.Success(dto);
    }

    public async Task<Result<DashboardWidgetDto>> UpdateDashboardWidgetAsync(Guid id, UpdateDashboardWidgetRequest request)
    {
        var widget = await _context.DashboardWidgets.FindAsync(id);
        if (widget == null || widget.IsDeleted)
            return Result<DashboardWidgetDto>.Failure("Widget not found");

        if (request.Title != null) widget.Title = request.Title;
        if (request.WidgetType != null) widget.WidgetType = request.WidgetType;
        if (request.DataSource != null) widget.DataSource = request.DataSource;
        if (request.Configuration != null) widget.Configuration = request.Configuration;
        if (request.PositionX.HasValue) widget.PositionX = request.PositionX.Value;
        if (request.PositionY.HasValue) widget.PositionY = request.PositionY.Value;
        if (request.Width.HasValue) widget.Width = request.Width.Value;
        if (request.Height.HasValue) widget.Height = request.Height.Value;
        if (request.RefreshInterval.HasValue) widget.RefreshInterval = request.RefreshInterval.Value;

        widget.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var dto = new DashboardWidgetDto(
            widget.Id, widget.DashboardId, widget.Title, widget.WidgetType,
            widget.DataSource, widget.Configuration, widget.PositionX, widget.PositionY,
            widget.Width, widget.Height, widget.RefreshInterval
        );

        return Result<DashboardWidgetDto>.Success(dto);
    }

    public async Task<Result> DeleteDashboardWidgetAsync(Guid id)
    {
        var widget = await _context.DashboardWidgets.FindAsync(id);
        if (widget == null)
            return Result.Failure("Widget not found");

        widget.IsDeleted = true;
        widget.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<Dictionary<string, object>>> GetWidgetDataAsync(Guid widgetId)
    {
        var widget = await _context.DashboardWidgets.FindAsync(widgetId);
        if (widget == null || widget.IsDeleted)
            return Result<Dictionary<string, object>>.Failure("Widget not found");

        var data = new Dictionary<string, object>();

        // Calculate data based on widget type and data source
        switch (widget.DataSource?.ToLower())
        {
            case "sales":
                var totalSales = await _context.SalesOrders
                    .Where(so => so.Status == SalesOrderStatus.Delivered)
                    .SumAsync(so => so.TotalAmount);
                var salesCount = await _context.SalesOrders
                    .CountAsync(so => so.Status == SalesOrderStatus.Delivered);
                data["totalAmount"] = totalSales;
                data["orderCount"] = salesCount;
                data["averageOrderValue"] = salesCount > 0 ? totalSales / salesCount : 0;
                break;

            case "inventory":
                var totalProducts = await _context.Products.CountAsync(p => p.IsActive);
                var lowStockCount = await _context.Products
                    .Include(p => p.Stocks)
                    .Where(p => p.IsActive && p.TrackInventory)
                    .CountAsync(p => p.Stocks.Sum(s => s.QuantityOnHand) <= p.ReorderLevel);
                data["totalProducts"] = totalProducts;
                data["lowStockCount"] = lowStockCount;
                break;

            case "projects":
                var activeProjects = await _context.Projects.CountAsync(p => p.Status == ProjectStatus.InProgress);
                var completedProjects = await _context.Projects.CountAsync(p => p.Status == ProjectStatus.Completed);
                var totalBudget = await _context.Projects
                    .Where(p => p.Status == ProjectStatus.InProgress)
                    .SumAsync(p => p.BudgetAmount);
                var totalActualCost = await _context.Projects
                    .Where(p => p.Status == ProjectStatus.InProgress)
                    .SumAsync(p => p.ActualCost);
                data["activeProjects"] = activeProjects;
                data["completedProjects"] = completedProjects;
                data["totalBudget"] = totalBudget;
                data["totalActualCost"] = totalActualCost;
                break;

            case "finance":
                var totalRevenue = await _context.Invoices
                    .Where(i => i.Status == InvoiceStatus.Paid)
                    .SumAsync(i => i.TotalAmount);
                var totalExpenses = await _context.Expenses
                    .Where(e => e.IsApproved)
                    .SumAsync(e => e.Amount);
                data["revenue"] = totalRevenue;
                data["expenses"] = totalExpenses;
                data["profit"] = totalRevenue - totalExpenses;
                break;

            default:
                data["message"] = "No data source configured";
                break;
        }

        return Result<Dictionary<string, object>>.Success(data);
    }

    // Report Scheduling Implementation
    public async Task<Result<ReportScheduleDto>> CreateReportScheduleAsync(Guid reportId, CreateReportScheduleRequest request)
    {
        var report = await _context.Reports.FindAsync(reportId);
        if (report == null)
            return Result<ReportScheduleDto>.Failure("Report not found");

        var nextRunAt = CalculateNextRunDate(request.Frequency, request.CronExpression);

        var schedule = new ReportSchedule
        {
            ReportId = reportId,
            Name = request.Name,
            Frequency = request.Frequency,
            CronExpression = request.CronExpression,
            Recipients = request.Recipients,
            Format = request.Format,
            NextRunAt = nextRunAt,
            IsActive = true
        };

        _context.ReportSchedules.Add(schedule);
        await _context.SaveChangesAsync();

        var dto = new ReportScheduleDto(
            schedule.Id, schedule.ReportId, report.Name, schedule.Name, schedule.Frequency,
            schedule.CronExpression, schedule.Recipients, schedule.Format,
            schedule.LastRunAt, schedule.NextRunAt, schedule.IsActive
        );

        return Result<ReportScheduleDto>.Success(dto);
    }

    public async Task<Result<List<ReportScheduleDto>>> GetAllReportSchedulesAsync(bool? activeOnly = null)
    {
        var query = _context.ReportSchedules
            .Include(rs => rs.Report)
            .AsQueryable();

        if (activeOnly.HasValue)
            query = query.Where(rs => rs.IsActive == activeOnly.Value);

        var schedules = await query.ToListAsync();

        var dtos = schedules.Select(rs => new ReportScheduleDto(
            rs.Id, rs.ReportId, rs.Report.Name, rs.Name, rs.Frequency,
            rs.CronExpression, rs.Recipients, rs.Format,
            rs.LastRunAt, rs.NextRunAt, rs.IsActive
        )).ToList();

        return Result<List<ReportScheduleDto>>.Success(dtos);
    }

    public async Task<Result<ReportScheduleDto>> GetReportScheduleByIdAsync(Guid id)
    {
        var schedule = await _context.ReportSchedules
            .Include(rs => rs.Report)
            .FirstOrDefaultAsync(rs => rs.Id == id);

        if (schedule == null)
            return Result<ReportScheduleDto>.Failure("Report schedule not found");

        var dto = new ReportScheduleDto(
            schedule.Id, schedule.ReportId, schedule.Report.Name, schedule.Name, schedule.Frequency,
            schedule.CronExpression, schedule.Recipients, schedule.Format,
            schedule.LastRunAt, schedule.NextRunAt, schedule.IsActive
        );

        return Result<ReportScheduleDto>.Success(dto);
    }

    public async Task<Result<ReportScheduleDto>> UpdateReportScheduleAsync(Guid id, UpdateReportScheduleRequest request)
    {
        var schedule = await _context.ReportSchedules
            .Include(rs => rs.Report)
            .FirstOrDefaultAsync(rs => rs.Id == id);

        if (schedule == null)
            return Result<ReportScheduleDto>.Failure("Report schedule not found");

        if (request.Name != null) schedule.Name = request.Name;
        if (request.Frequency != null) schedule.Frequency = request.Frequency;
        if (request.CronExpression != null) schedule.CronExpression = request.CronExpression;
        if (request.Recipients != null) schedule.Recipients = request.Recipients;
        if (request.Format != null) schedule.Format = request.Format;
        if (request.IsActive.HasValue) schedule.IsActive = request.IsActive.Value;

        // Recalculate next run date if frequency or cron changed
        if (request.Frequency != null || request.CronExpression != null)
        {
            schedule.NextRunAt = CalculateNextRunDate(schedule.Frequency, schedule.CronExpression);
        }

        schedule.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var dto = new ReportScheduleDto(
            schedule.Id, schedule.ReportId, schedule.Report.Name, schedule.Name, schedule.Frequency,
            schedule.CronExpression, schedule.Recipients, schedule.Format,
            schedule.LastRunAt, schedule.NextRunAt, schedule.IsActive
        );

        return Result<ReportScheduleDto>.Success(dto);
    }

    public async Task<Result> DeleteReportScheduleAsync(Guid id)
    {
        var schedule = await _context.ReportSchedules.FindAsync(id);
        if (schedule == null)
            return Result.Failure("Report schedule not found");

        _context.ReportSchedules.Remove(schedule);
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result> RunReportScheduleAsync(Guid id)
    {
        var schedule = await _context.ReportSchedules
            .Include(rs => rs.Report)
            .FirstOrDefaultAsync(rs => rs.Id == id);

        if (schedule == null)
            return Result.Failure("Report schedule not found");

        if (!schedule.IsActive)
            return Result.Failure("Report schedule is not active");

        // Update execution tracking
        schedule.LastRunAt = DateTime.UtcNow;
        schedule.NextRunAt = CalculateNextRunDate(schedule.Frequency, schedule.CronExpression);
        await _context.SaveChangesAsync();

        // TODO: Implement actual report generation and email sending
        // This would typically involve:
        // 1. Generate report based on report definition
        // 2. Export to requested format (PDF/Excel)
        // 3. Send email to recipients with attachment

        return Result.Success();
    }

    // Projects Report Implementation
    public async Task<Result<List<ProjectReportData>>> GetProjectsReportAsync(
        DateTime? startDate = null, DateTime? endDate = null, ProjectStatus? status = null)
    {
        var query = _context.Projects
            .Include(p => p.Customer)
            .Include(p => p.Tasks)
            .Include(p => p.ProjectMembers)
            .Include(p => p.TimeEntries)
            .AsQueryable();

        if (startDate.HasValue)
            query = query.Where(p => p.StartDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(p => p.StartDate <= endDate.Value);

        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        var projects = await query.ToListAsync();

        var reportData = projects.Select(p =>
        {
            var totalTasks = p.Tasks.Count;
            var completedTasks = p.Tasks.Count(t => t.Status == TaskStatus.Done);
            var totalHours = p.TimeEntries.Sum(te => te.Hours);
            var activeMembers = p.ProjectMembers.Count(pm => !pm.RemovedDate.HasValue);
            var budgetVariance = p.BudgetAmount - p.ActualCost;

            return new ProjectReportData(
                p.Id,
                p.ProjectCode,
                p.Name,
                p.Customer?.Name,
                p.Status,
                p.StartDate,
                p.EndDate,
                p.BudgetAmount,
                p.ActualCost,
                budgetVariance,
                p.CompletionPercentage,
                totalTasks,
                completedTasks,
                totalHours,
                activeMembers
            );
        }).ToList();

        return Result<List<ProjectReportData>>.Success(reportData);
    }

    // Helper method to calculate next run date for report schedules
    private DateTime? CalculateNextRunDate(string frequency, string? cronExpression)
    {
        var now = DateTime.UtcNow;

        return frequency.ToLower() switch
        {
            "daily" => now.Date.AddDays(1).AddHours(9), // 9 AM next day
            "weekly" => now.Date.AddDays(7 - (int)now.DayOfWeek + 1).AddHours(9), // Next Monday 9 AM
            "monthly" => new DateTime(now.Year, now.Month, 1).AddMonths(1).AddHours(9), // 1st of next month 9 AM
            "custom" => ParseCronExpression(cronExpression),
            _ => null
        };
    }

    // Basic cron expression parser (simplified - supports "minute hour * * dayOfWeek" format)
    private DateTime? ParseCronExpression(string? cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
            return null;

        try
        {
            var parts = cronExpression.Split(' ');
            if (parts.Length < 5) return null;

            var minute = int.Parse(parts[0]);
            var hour = int.Parse(parts[1]);
            var dayOfWeek = parts[4] != "*" ? int.Parse(parts[4]) : -1;

            var now = DateTime.UtcNow;
            var nextRun = now.Date.AddHours(hour).AddMinutes(minute);

            // If time has passed today, move to next day
            if (nextRun <= now)
                nextRun = nextRun.AddDays(1);

            // Adjust for day of week if specified (0 = Sunday, 1 = Monday, etc.)
            if (dayOfWeek >= 0)
            {
                while ((int)nextRun.DayOfWeek != dayOfWeek)
                {
                    nextRun = nextRun.AddDays(1);
                }
            }

            return nextRun;
        }
        catch
        {
            return null;
        }
    }
}

// EXPENSE SERVICE IMPLEMENTATION


/// <summary>
/// Represents the expense service domain model.
/// </summary>
public class ExpenseService : IExpenseService
{
    private readonly ERPDbContext _context;

    public ExpenseService(ERPDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ExpenseDto>> CreateExpenseAsync(CreateExpenseRequest request)
    {

        if (request.Amount <= 0)
        {
            return Result<ExpenseDto>.Failure("Expense amount must be greater than zero");
        }

        if (string.IsNullOrWhiteSpace(request.Category))
        {
            return Result<ExpenseDto>.Failure("Expense category is required");
        }


        if (request.SupplierId.HasValue)
        {
            var supplierToValidate = await _context.Suppliers.FindAsync(request.SupplierId.Value);
            if (supplierToValidate == null)
            {
                return Result<ExpenseDto>.Failure("Supplier not found");
            }

            if (!supplierToValidate.IsActive)
            {
                return Result<ExpenseDto>.Failure("Cannot create expense for inactive supplier");
            }
        }

        var expenseNumber = $"EXP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}";

        var expense = new Expense
        {
            ExpenseNumber = expenseNumber,
            ExpenseDate = request.ExpenseDate,
            Category = request.Category,
            Amount = request.Amount,
            SupplierId = request.SupplierId,
            Description = request.Description,
            ReceiptNumber = request.ReceiptNumber,
            IsApproved = false
        };

        _context.Expenses.Add(expense);
        await _context.SaveChangesAsync();

        var supplier = request.SupplierId.HasValue ? await _context.Suppliers.FindAsync(request.SupplierId.Value) : null;

        var dto = new ExpenseDto(
            expense.Id,
            expense.ExpenseNumber,
            expense.ExpenseDate,
            expense.Category,
            expense.Amount,
            supplier?.Name,
            expense.IsApproved,
            null
        );

        return Result<ExpenseDto>.Success(dto);
    }

    public async Task<Result<List<ExpenseDto>>> GetAllExpensesAsync(bool? isApproved = null)
    {
        var query = _context.Expenses
            .Include(e => e.Supplier)
            .Where(e => !e.IsDeleted)
            .AsQueryable();

        if (isApproved.HasValue)
        {
            query = query.Where(e => e.IsApproved == isApproved.Value);
        }

        var expenses = await query.OrderByDescending(e => e.ExpenseDate).ToListAsync();

        var dtos = expenses.Select(e => new ExpenseDto(
            e.Id,
            e.ExpenseNumber,
            e.ExpenseDate,
            e.Category,
            e.Amount,
            e.Supplier?.Name,
            e.IsApproved,
            null
        )).ToList();

        return Result<List<ExpenseDto>>.Success(dtos);
    }

    public async Task<Result<ExpenseDto>> GetExpenseByIdAsync(Guid id)
    {
        var expense = await _context.Expenses
            .Include(e => e.Supplier)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (expense == null)
            return Result<ExpenseDto>.Failure("Expense not found");

        var dto = new ExpenseDto(
            expense.Id,
            expense.ExpenseNumber,
            expense.ExpenseDate,
            expense.Category,
            expense.Amount,
            expense.Supplier?.Name,
            expense.IsApproved,
            null
        );

        return Result<ExpenseDto>.Success(dto);
    }

    public async Task<Result<ExpenseDto>> UpdateExpenseAsync(Guid id, UpdateExpenseRequest request)
    {
        var expense = await _context.Expenses
            .Include(e => e.Supplier)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (expense == null)
            return Result<ExpenseDto>.Failure("Expense not found");

        if (expense.IsApproved)
            return Result<ExpenseDto>.Failure("Cannot update approved expenses");

        if (request.ExpenseDate.HasValue) expense.ExpenseDate = request.ExpenseDate.Value;
        if (request.Category != null) expense.Category = request.Category;
        if (request.Amount.HasValue)
        {
            if (request.Amount.Value <= 0)
                return Result<ExpenseDto>.Failure("Expense amount must be greater than zero");
            expense.Amount = request.Amount.Value;
        }
        if (request.Description != null) expense.Description = request.Description;
        if (request.ReceiptNumber != null) expense.ReceiptNumber = request.ReceiptNumber;

        expense.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var dto = new ExpenseDto(
            expense.Id,
            expense.ExpenseNumber,
            expense.ExpenseDate,
            expense.Category,
            expense.Amount,
            expense.Supplier?.Name,
            expense.IsApproved,
            null
        );

        return Result<ExpenseDto>.Success(dto);
    }

    public async Task<Result> DeleteExpenseAsync(Guid id)
    {
        var expense = await _context.Expenses.FindAsync(id);
        if (expense == null)
            return Result.Failure("Expense not found");

        if (expense.IsApproved)
            return Result.Failure("Cannot delete approved expenses");

        expense.IsDeleted = true;
        expense.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result> ApproveExpenseAsync(Guid id, Guid approvedBy)
    {
        var expense = await _context.Expenses.FindAsync(id);
        if (expense == null)
            return Result.Failure("Expense not found");

        if (expense.IsApproved)
            return Result.Failure("Expense is already approved");

        expense.IsApproved = true;
        expense.ApprovedBy = approvedBy;
        expense.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result> RejectExpenseAsync(Guid id)
    {
        var expense = await _context.Expenses.FindAsync(id);
        if (expense == null)
            return Result.Failure("Expense not found");

        if (expense.IsApproved)
            return Result.Failure("Cannot reject approved expenses. Delete or create adjustment instead.");

        expense.IsDeleted = true;
        expense.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<PagedResult<ExpenseDto>>> SearchExpensesAsync(
        ExpenseSearchRequest search,
        PaginationRequest pagination)
    {
        try
        {
            var query = _context.Expenses
                .Include(e => e.Supplier)
                .Where(e => !e.IsDeleted)
                .AsQueryable();


            if (!string.IsNullOrWhiteSpace(search.SearchTerm))
            {
                var searchLower = search.SearchTerm.ToLower();
                query = query.Where(e =>
                    e.ExpenseNumber.ToLower().Contains(searchLower) ||
                    e.Category.ToLower().Contains(searchLower) ||
                    (e.Description != null && e.Description.ToLower().Contains(searchLower)) ||
                    (e.Supplier != null && e.Supplier.Name.ToLower().Contains(searchLower)));
            }


            if (!string.IsNullOrWhiteSpace(search.Category))
            {
                query = query.Where(e => e.Category == search.Category);
            }


            if (search.SupplierId.HasValue)
            {
                query = query.Where(e => e.SupplierId == search.SupplierId.Value);
            }


            if (search.IsApproved.HasValue)
            {
                query = query.Where(e => e.IsApproved == search.IsApproved.Value);
            }


            if (search.DateFrom.HasValue)
            {
                query = query.Where(e => e.ExpenseDate >= search.DateFrom.Value);
            }

            if (search.DateTo.HasValue)
            {
                query = query.Where(e => e.ExpenseDate <= search.DateTo.Value);
            }


            if (search.MinAmount.HasValue)
            {
                query = query.Where(e => e.Amount >= search.MinAmount.Value);
            }

            if (search.MaxAmount.HasValue)
            {
                query = query.Where(e => e.Amount <= search.MaxAmount.Value);
            }


            query = query.ApplySorting(pagination.SortBy ?? "ExpenseDate", pagination.SortDescending ?? true);


            var totalCount = await query.CountAsync();


            var expenses = await query
                .Skip(pagination.Skip)
                .Take(pagination.Take)
                .Select(e => new ExpenseDto(
                    e.Id,
                    e.ExpenseNumber,
                    e.ExpenseDate,
                    e.Category,
                    e.Amount,
                    e.Supplier != null ? e.Supplier.Name : null,
                    e.IsApproved,
                    null
                ))
                .ToListAsync();

            var pagedResult = new PagedResult<ExpenseDto>
            {
                Items = expenses,
                TotalCount = totalCount,
                PageNumber = pagination.Page,
                PageSize = pagination.PageSize
            };

            return Result<PagedResult<ExpenseDto>>.Success(pagedResult);
        }
        catch (Exception ex)
        {
            return Result<PagedResult<ExpenseDto>>.Failure($"Error searching expenses: {ex.Message}");
        }
    }

    public async Task<Result<decimal>> GetTotalExpensesAsync(DateTime startDate, DateTime endDate, string? category = null)
    {
        var query = _context.Expenses
            .Where(e => e.IsApproved && e.ExpenseDate >= startDate && e.ExpenseDate <= endDate);

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(e => e.Category == category);
        }

        var total = await query.SumAsync(e => e.Amount);

        return Result<decimal>.Success(total);
    }
}

