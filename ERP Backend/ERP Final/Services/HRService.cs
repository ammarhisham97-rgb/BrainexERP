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
/// Represents the h r service domain model.
/// </summary>
public class HRService : IHRService
{
    private readonly ERPDbContext _context;
    private readonly IHRAttritionService _hrAttritionService;

    public HRService(ERPDbContext context, IHRAttritionService hrAttritionService)
    {
        _context = context;
        _hrAttritionService = hrAttritionService;
    }
    public async Task<Result<PagedResult<EmployeeDto>>> SearchEmployeesAsync(
       EmployeeSearchRequest search,
       PaginationRequest pagination)
    {
        try
        {
            // ? Add IsDeleted filter at the start
            var query = _context.Employees
                .Where(e => !e.IsDeleted)
                .AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(search.SearchTerm))
            {
                var searchLower = search.SearchTerm.ToLower();
                query = query.Where(e =>
                    e.EmployeeCode.ToLower().Contains(searchLower) ||
                    e.FirstName.ToLower().Contains(searchLower) ||
                    e.LastName.ToLower().Contains(searchLower) ||
                    (e.Email != null && e.Email.ToLower().Contains(searchLower)));
            }

            // Apply department filter
            if (!string.IsNullOrWhiteSpace(search.Department))
            {
                query = query.Where(e => e.Department == search.Department);
            }

            // Apply position filter
            if (!string.IsNullOrWhiteSpace(search.Position))
            {
                query = query.Where(e => e.Position == search.Position);
            }

            // Apply active filter
            if (search.IsActive.HasValue)
            {
                query = query.Where(e => e.IsActive == search.IsActive.Value);
            }

            // Apply join date filters
            if (search.JoinedAfter.HasValue)
            {
                query = query.Where(e => e.JoinDate >= search.JoinedAfter.Value);
            }

            if (search.JoinedBefore.HasValue)
            {
                query = query.Where(e => e.JoinDate <= search.JoinedBefore.Value);
            }

            // Apply salary filters
            if (search.MinSalary.HasValue)
            {
                query = query.Where(e => e.BaseSalary >= search.MinSalary.Value);
            }

            if (search.MaxSalary.HasValue)
            {
                query = query.Where(e => e.BaseSalary <= search.MaxSalary.Value);
            }

            // Apply sorting
            var sortDescending = pagination.SortDescending ?? false;
            query = query.ApplySorting(pagination.SortBy ?? "EmployeeCode", sortDescending);

            // Get total count
            var totalCount = await query.CountAsync();

            // Apply pagination and project to DTO
            var employees = await query
                .Skip(pagination.Skip)
                .Take(pagination.Take)
                .Select(e => new EmployeeDto(
                    e.Id,
                    e.EmployeeCode,
                    e.FirstName,
                    e.LastName,
                    e.Email,
                    e.Department,
                    e.Position,
                    e.BaseSalary,
                    e.IsActive
                ))
                .ToListAsync();

            var pagedResult = new PagedResult<EmployeeDto>
            {
                Items = employees,
                TotalCount = totalCount,
                PageNumber = pagination.Page,
                PageSize = pagination.PageSize
            };

            return Result<PagedResult<EmployeeDto>>.Success(pagedResult);
        }
        catch (Exception ex)
        {
            return Result<PagedResult<EmployeeDto>>.Failure($"Error searching employees: {ex.Message}");
        }
    }
    public async Task<Result<EmployeeDto>> UpdateEmployeeAsync(Guid id, UpdateEmployeeRequest request)
    {
        var employee = await _context.Employees.FindAsync(id);
        if (employee == null)
            return Result<EmployeeDto>.Failure("Employee not found");

        // ? Check if deleted
        if (employee.IsDeleted)
            return Result<EmployeeDto>.Failure("Cannot update deleted employee");

        if (request.FirstName != null) employee.FirstName = request.FirstName;
        if (request.LastName != null) employee.LastName = request.LastName;
        if (request.Email != null) employee.Email = request.Email;
        if (request.PhoneNumber != null) employee.PhoneNumber = request.PhoneNumber;
        if (request.Department != null) employee.Department = request.Department;
        if (request.Position != null) employee.Position = request.Position;
        if (request.BaseSalary.HasValue) employee.BaseSalary = request.BaseSalary.Value;
        if (request.Address != null) employee.Address = request.Address;

        employee.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            return Result<EmployeeDto>.Failure($"Error updating employee: {ex.Message}");
        }

        var dto = new EmployeeDto(employee.Id, employee.EmployeeCode, employee.FirstName, employee.LastName,
            employee.Email, employee.Department, employee.Position, employee.BaseSalary, employee.IsActive);

        return Result<EmployeeDto>.Success(dto);
    }

    public async Task<Result> DeactivateEmployeeAsync(Guid id)
    {
        var employee = await _context.Employees.FindAsync(id);
        if (employee == null)
            return Result.Failure("Employee not found");

        // ? Set soft delete flags
        employee.IsDeleted = true;
        employee.IsActive = false;
        employee.LeaveDate = DateTime.UtcNow;
        employee.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result> UpdateAttendanceAsync(Guid id, UpdateAttendanceRequest request)
    {
        var attendance = await _context.Attendances.FindAsync(id);
        if (attendance == null)
            return Result.Failure("Attendance record not found");

        attendance.Date = request.Date;

        // ? Parse time strings to TimeSpan
        if (!string.IsNullOrEmpty(request.CheckInTime))
        {
            if (!TimeSpan.TryParse(request.CheckInTime, out var checkIn))
                return Result.Failure("Invalid check-in time format");
            attendance.CheckInTime = checkIn;
        }

        if (!string.IsNullOrEmpty(request.CheckOutTime))
        {
            if (!TimeSpan.TryParse(request.CheckOutTime, out var checkOut))
                return Result.Failure("Invalid check-out time format");
            attendance.CheckOutTime = checkOut;
        }

        attendance.Status = request.Status;
        attendance.Notes = request.Notes;

        if (attendance.CheckInTime.HasValue && attendance.CheckOutTime.HasValue)
        {
            var workHours = attendance.CheckOutTime.Value - attendance.CheckInTime.Value;
            attendance.WorkHours = workHours;
            if (workHours.TotalHours > 8)
            {
                attendance.OvertimeHours = TimeSpan.FromHours(workHours.TotalHours - 8);
            }
        }

        attendance.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result> DeleteAttendanceAsync(Guid id)
    {
        var attendance = await _context.Attendances.FindAsync(id);
        if (attendance == null)
            return Result.Failure("Attendance record not found");

        // ? Soft delete instead of hard delete
        attendance.IsDeleted = true;
        attendance.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<LeaveDto>> GetLeaveByIdAsync(Guid id)
    {
        var leave = await _context.Leaves
            .AsNoTracking()
            .Include(l => l.Employee)
            .FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted);

        if (leave == null)
            return Result<LeaveDto>.Failure("Leave request not found");

        var dto = new LeaveDto(
            leave.Id, 
            leave.EmployeeId, 
            $"{leave.Employee.FirstName} {leave.Employee.LastName}",
            leave.Employee.EmployeeCode,
            leave.Type, 
            leave.StartDate, 
            leave.EndDate, 
            leave.TotalDays, 
            leave.Reason,
            leave.Status,
            leave.ApprovedBy,
            leave.ApprovedAt,
            leave.ApprovalNotes
        );
        return Result<LeaveDto>.Success(dto);
    }

    public async Task<Result<LeaveDto>> UpdateLeaveAsync(Guid id, UpdateLeaveRequest request)
    {
        var leave = await _context.Leaves
            .Include(l => l.Employee)
            .FirstOrDefaultAsync(l => l.Id == id && !l.IsDeleted);

        if (leave == null)
            return Result<LeaveDto>.Failure("Leave request not found");

        if (leave.Status != LeaveStatus.Pending)
            return Result<LeaveDto>.Failure("Only pending leave requests can be updated");

        leave.Type = request.Type;
        leave.StartDate = request.StartDate;
        leave.EndDate = request.EndDate;
        leave.TotalDays = (request.EndDate.Date - request.StartDate.Date).Days + 1;
        leave.Reason = request.Reason;
        leave.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var dto = new LeaveDto(
            leave.Id, 
            leave.EmployeeId, 
            $"{leave.Employee.FirstName} {leave.Employee.LastName}",
            leave.Employee.EmployeeCode,
            leave.Type, 
            leave.StartDate, 
            leave.EndDate, 
            leave.TotalDays, 
            leave.Reason,
            leave.Status,
            leave.ApprovedBy,
            leave.ApprovedAt,
            leave.ApprovalNotes
        );
        return Result<LeaveDto>.Success(dto);
    }

    public async Task<Result> DeleteLeaveAsync(Guid id)
    {
        var leave = await _context.Leaves.FindAsync(id);
        if (leave == null)
            return Result.Failure("Leave request not found");

        // Only pending leaves can be deleted
        if (leave.Status == LeaveStatus.Approved)
            return Result.Failure("Cannot delete approved leave requests. Only pending leave requests can be deleted. Contact HR to modify approved requests.");

        if (leave.Status == LeaveStatus.Rejected)
            return Result.Failure("Cannot delete rejected leave requests. Only pending leave requests can be deleted.");

        // Soft delete instead of hard delete
        leave.IsDeleted = true;
        leave.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Result.Success();
    }


    public async Task<Result<PayrollDto>> GetPayrollByIdAsync(Guid id)
    {
        var payroll = await _context.Payrolls
            .Include(p => p.Employee)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payroll == null)
            return Result<PayrollDto>.Failure("Payroll not found");

        var dto = new PayrollDto(payroll.Id, payroll.EmployeeId, $"{payroll.Employee.FirstName} {payroll.Employee.LastName}",
            payroll.Month, payroll.Year, payroll.BaseSalary, payroll.NetSalary, payroll.Status);

        return Result<PayrollDto>.Success(dto);
    }

    public async Task<Result<PayrollDto>> UpdatePayrollAsync(Guid id, UpdatePayrollRequest request)
    {
        var payroll = await _context.Payrolls
            .Include(p => p.Employee)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payroll == null)
            return Result<PayrollDto>.Failure("Payroll not found");

        if (payroll.Status != PayrollStatus.Draft)
            return Result<PayrollDto>.Failure("Only draft payrolls can be updated");

        payroll.Allowances = request.Allowances;
        payroll.Deductions = request.Deductions;
        payroll.Tax = request.Tax;
        payroll.Notes = request.Notes;

        // Recalculate net salary
        var grossSalary = payroll.BaseSalary + payroll.Allowances + payroll.OvertimePay;
        payroll.NetSalary = grossSalary - payroll.Tax - payroll.Deductions;

        payroll.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var dto = new PayrollDto(payroll.Id, payroll.EmployeeId, $"{payroll.Employee.FirstName} {payroll.Employee.LastName}",
            payroll.Month, payroll.Year, payroll.BaseSalary, payroll.NetSalary, payroll.Status);

        return Result<PayrollDto>.Success(dto);
    }

    public async Task<Result> DeletePayrollAsync(Guid id)
    {
        var payroll = await _context.Payrolls.FindAsync(id);
        if (payroll == null)
            return Result.Failure("Payroll not found");

        if (payroll.Status == PayrollStatus.Paid)
            return Result.Failure("Cannot delete paid payrolls");

        // ? Soft delete instead of hard delete
        payroll.IsDeleted = true;
        payroll.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Result.Success();
    }


    public async Task<Result> ProcessPayrollAsync(Guid id)
    {
        var payroll = await _context.Payrolls.FindAsync(id);
        if (payroll == null)
            return Result.Failure("Payroll not found");

        if (payroll.Status != PayrollStatus.Draft)
            return Result.Failure("Only draft payrolls can be processed");

        payroll.Status = PayrollStatus.Processed;
        payroll.ProcessedAt = DateTime.UtcNow;
        payroll.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Result.Success();
    }
    public async Task<Result<PagedResult<LeaveDto>>> SearchLeavesAsync(
        LeaveFilterRequest filter,
        PaginationRequest pagination)
    {
        try
        {
            // ? Add IsDeleted filter and Include Employee
            var query = _context.Leaves
                .AsNoTracking()
                .Include(l => l.Employee)
                .Where(l => !l.IsDeleted)
                .AsQueryable();

            // Apply employee filter
            if (filter.EmployeeId.HasValue)
            {
                query = query.Where(l => l.EmployeeId == filter.EmployeeId.Value);
            }

            // Apply type filter
            if (filter.Type.HasValue)
            {
                query = query.Where(l => l.Type == filter.Type.Value);
            }

            // Apply status filter
            if (filter.Status.HasValue)
            {
                query = query.Where(l => l.Status == filter.Status.Value);
            }

            // Apply date filters
            if (filter.StartDateFrom.HasValue)
            {
                query = query.Where(l => l.StartDate >= filter.StartDateFrom.Value);
            }

            if (filter.StartDateTo.HasValue)
            {
                query = query.Where(l => l.StartDate <= filter.StartDateTo.Value);
            }

            // Apply sorting
            var sortDescending = pagination.SortDescending ?? false;
            query = query.ApplySorting(pagination.SortBy ?? "StartDate", sortDescending);

            // Get total count
            var totalCount = await query.CountAsync();

            // Apply pagination and project to DTO
            var leaves = await query
                .Skip(pagination.Skip)
                .Take(pagination.Take)
                .Select(l => new LeaveDto(
                    l.Id,
                    l.EmployeeId,
                    $"{l.Employee.FirstName} {l.Employee.LastName}",
                    l.Employee.EmployeeCode,
                    l.Type,
                    l.StartDate,
                    l.EndDate,
                    l.TotalDays,
                    l.Reason,
                    l.Status,
                    l.ApprovedBy,
                    l.ApprovedAt,
                    l.ApprovalNotes
                ))
                .ToListAsync();

            var pagedResult = new PagedResult<LeaveDto>
            {
                Items = leaves,
                TotalCount = totalCount,
                PageNumber = pagination.Page,
                PageSize = pagination.PageSize
            };

            return Result<PagedResult<LeaveDto>>.Success(pagedResult);
        }
        catch (Exception ex)
        {
            return Result<PagedResult<LeaveDto>>.Failure($"Error searching leaves: {ex.Message}");
        }
    }

    public async Task<Result<EmployeeDto>> CreateEmployeeAsync(CreateEmployeeRequest request)
    {
        if (await _context.Employees.AnyAsync(e => e.EmployeeCode == request.EmployeeCode))
        {
            return Result<EmployeeDto>.Failure("Employee code already exists");
        }

        var employee = new Employee
        {
            EmployeeCode = request.EmployeeCode,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            DateOfBirth = request.DateOfBirth ?? DateTime.UtcNow.AddYears(-25),
            JoinDate = request.JoinDate,
            Department = request.Department,
            Position = request.Position,
            BaseSalary = request.BaseSalary,
            IsActive = true
        };

        _context.Employees.Add(employee);
        await _context.SaveChangesAsync();

        var dto = new EmployeeDto(employee.Id, employee.EmployeeCode, employee.FirstName, employee.LastName,
            employee.Email, employee.Department, employee.Position, employee.BaseSalary, employee.IsActive);

        return Result<EmployeeDto>.Success(dto);
    }


    public async Task<Result<List<EmployeeDto>>> GetAllEmployeesAsync()
    {
        var employees = await _context.Employees
            .Where(e => !e.IsDeleted && e.IsActive)  // ? Add IsDeleted check
            .ToListAsync();

        var dtos = employees.Select(e => new EmployeeDto(
            e.Id, e.EmployeeCode, e.FirstName, e.LastName, e.Email,
            e.Department, e.Position, e.BaseSalary, e.IsActive
        )).ToList();

        return Result<List<EmployeeDto>>.Success(dtos);
    }

    public async Task<Result<EmployeeDto>> GetEmployeeByIdAsync(Guid id)
    {
        var employee = await _context.Employees.FindAsync(id);

        if (employee == null)
        {
            return Result<EmployeeDto>.Failure("Employee not found");
        }

        var dto = new EmployeeDto(employee.Id, employee.EmployeeCode, employee.FirstName, employee.LastName,
            employee.Email, employee.Department, employee.Position, employee.BaseSalary, employee.IsActive);

        return Result<EmployeeDto>.Success(dto);
    }

    public async Task<Result> MarkAttendanceAsync(Guid employeeId, DateTime date, TimeSpan checkIn, TimeSpan? checkOut, AttendanceStatus status)
    {
        var employee = await _context.Employees.FindAsync(employeeId);
        if (employee == null)
        {
            return Result.Failure("Employee not found");
        }

        var existingAttendance = await _context.Attendances
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.Date.Date == date.Date && !a.IsDeleted);

        if (existingAttendance != null)
        {
            existingAttendance.CheckInTime = checkIn;
            existingAttendance.CheckOutTime = checkOut;
            existingAttendance.Status = status;

            if (checkOut.HasValue)
            {
                var workHours = checkOut.Value - checkIn;
                existingAttendance.WorkHours = workHours;
                if (workHours.TotalHours > 8)
                {
                    existingAttendance.OvertimeHours = TimeSpan.FromHours(workHours.TotalHours - 8);
                }
                else
                {
                    existingAttendance.OvertimeHours = TimeSpan.Zero;
                }
            }

            // ? FIX: Set UpdatedAt timestamp when updating existing attendance
            existingAttendance.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var attendance = new Attendance
            {
                EmployeeId = employeeId,
                Date = date.Date,
                CheckInTime = checkIn,
                CheckOutTime = checkOut,
                Status = status
            };

            if (checkOut.HasValue)
            {
                var workHours = checkOut.Value - checkIn;
                attendance.WorkHours = workHours;
                if (workHours.TotalHours > 8)
                {
                    attendance.OvertimeHours = TimeSpan.FromHours(workHours.TotalHours - 8);
                }
            }

            _context.Attendances.Add(attendance);
        }

        await _context.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result<List<AttendanceDto>>> GetEmployeeAttendanceAsync(Guid employeeId, DateTime startDate, DateTime endDate)
    {
        var attendances = await _context.Attendances
            .Include(a => a.Employee)
            .Where(a => a.EmployeeId == employeeId && a.Date >= startDate && a.Date <= endDate && !a.IsDeleted)
            .OrderByDescending(a => a.Date)
            .ToListAsync();

        var dtos = attendances.Select(a => new AttendanceDto(
            a.Id,
            a.EmployeeId,
            $"{a.Employee.FirstName} {a.Employee.LastName}",
            a.Date,
            a.CheckInTime,
            a.CheckOutTime,
            a.Status
        )).ToList();

        return Result<List<AttendanceDto>>.Success(dtos);
    }

    public async Task<Result<LeaveDto>> RequestLeaveAsync(Guid employeeId, LeaveType type, DateTime startDate, DateTime endDate, string? reason)
    {
        // ? NEW: Validate dates
        if (startDate.Date > endDate.Date)
        {
            return Result<LeaveDto>.Failure("Start date cannot be after end date");
        }

        if (startDate.Date < DateTime.UtcNow.Date)
        {
            return Result<LeaveDto>.Failure("Cannot request leave for past dates");
        }

        var employee = await _context.Employees.FindAsync(employeeId);
        if (employee == null)
        {
            return Result<LeaveDto>.Failure("Employee not found");
        }

        // ? NEW: Check if employee is active
        if (!employee.IsActive)
        {
            return Result<LeaveDto>.Failure("Cannot request leave for inactive employee");
        }


        // ? FIX #1: Calculate working days only (exclude weekends)
        var totalDays = CalculateWorkingDays(startDate.Date, endDate.Date);

        if (totalDays <= 0)
        {
            return Result<LeaveDto>.Failure("Leave request must include at least one working day");
        }

        // ? NEW: Check for overlapping leave requests
        var hasOverlap = await _context.Leaves
            .AnyAsync(l => l.EmployeeId == employeeId &&
                          l.Status != LeaveStatus.Rejected &&
                          !l.IsDeleted &&
                          ((startDate.Date >= l.StartDate.Date && startDate.Date <= l.EndDate.Date) ||
                           (endDate.Date >= l.StartDate.Date && endDate.Date <= l.EndDate.Date) ||
                           (startDate.Date <= l.StartDate.Date && endDate.Date >= l.EndDate.Date)));

        if (hasOverlap)
        {
            return Result<LeaveDto>.Failure("Leave request overlaps with an existing leave request");
        }

        // ? NEW: Check leave balance (optional - depends on your business rules)
        var leaveBalance = await GetEmployeeLeaveBalance(employeeId, type);
        if (leaveBalance < totalDays)
        {
            return Result<LeaveDto>.Failure(
                $"Insufficient leave balance. Available: {leaveBalance} days, Requested: {totalDays} days");
        }

        var leave = new Leave
        {
            EmployeeId = employeeId,
            Type = type,
            StartDate = startDate.Date, // ? Use Date only (remove time component)
            EndDate = endDate.Date,     // ? Use Date only
            TotalDays = totalDays,
            Reason = reason,
            Status = LeaveStatus.Pending
        };

        _context.Leaves.Add(leave);
        await _context.SaveChangesAsync();

        // Reload with Employee included
        leave = await _context.Leaves
            .Include(l => l.Employee)
            .FirstAsync(l => l.Id == leave.Id);

        var dto = new LeaveDto(
            leave.Id,
            leave.EmployeeId,
            $"{leave.Employee.FirstName} {leave.Employee.LastName}",
            leave.Employee.EmployeeCode,
            leave.Type,
            leave.StartDate,
            leave.EndDate,
            leave.TotalDays,
            leave.Reason,
            leave.Status,
            leave.ApprovedBy,
            leave.ApprovedAt,
            leave.ApprovalNotes
        );

        return Result<LeaveDto>.Success(dto);
    }

    // ? NEW: Helper method to calculate working days (excluding weekends)
    private int CalculateWorkingDays(DateTime startDate, DateTime endDate)
    {
        int workingDays = 0;

        for (DateTime date = startDate; date <= endDate; date = date.AddDays(1))
        {
            // Skip Saturdays and Sundays
            if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
            {
                workingDays++;
            }
        }

        return workingDays;
    }

    // ? NEW: Helper method to get employee leave balance
    private async Task<int> GetEmployeeLeaveBalance(Guid employeeId, LeaveType leaveType)
    {
        // Define annual leave entitlements per type
        var annualEntitlement = leaveType switch
        {
            LeaveType.Vacation => 21,      // 21 days vacation per year
            LeaveType.Sick => 10,          // 10 days sick leave per year
            LeaveType.Personal => 5,       // 5 days personal leave per year
            LeaveType.Unpaid => int.MaxValue, // Unlimited unpaid leave
            _ => 0
        };

        // Get current year
        var currentYear = DateTime.UtcNow.Year;
        var yearStart = new DateTime(currentYear, 1, 1);
        var yearEnd = new DateTime(currentYear, 12, 31);

        // Calculate used leave days for this year
        var usedDays = await _context.Leaves
            .Where(l => l.EmployeeId == employeeId &&
                       l.Type == leaveType &&
                       l.Status == LeaveStatus.Approved &&
                       l.StartDate >= yearStart &&
                       l.EndDate <= yearEnd)
            .SumAsync(l => l.TotalDays);

        return annualEntitlement - usedDays;
    }

    // ? OPTIONAL: Advanced version that also excludes public holidays
    private int CalculateWorkingDaysWithHolidays(DateTime startDate, DateTime endDate)
    {
        // Define public holidays (you could also load these from database)
        var publicHolidays = GetPublicHolidays(startDate.Year, endDate.Year);

        int workingDays = 0;

        for (DateTime date = startDate; date <= endDate; date = date.AddDays(1))
        {
            // Skip weekends
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                continue;

            // Skip public holidays
            if (publicHolidays.Contains(date.Date))
                continue;

            workingDays++;
        }

        return workingDays;
    }

    // ? OPTIONAL: Get public holidays for given years
    private List<DateTime> GetPublicHolidays(int startYear, int endYear)
    {
        var holidays = new List<DateTime>();

        for (int year = startYear; year <= endYear; year++)
        {
            // Example holidays (customize based on your country)
            holidays.Add(new DateTime(year, 1, 1));   // New Year's Day
            holidays.Add(new DateTime(year, 12, 25)); // Christmas
                                                      // Add more holidays as needed
        }

        return holidays;
    }

    public async Task<Result> ApproveLeaveAsync(Guid leaveId, Guid approvedBy, string? notes)
    {
        var leave = await _context.Leaves
            .Include(l => l.Employee)
            .FirstOrDefaultAsync(l => l.Id == leaveId && !l.IsDeleted);

        if (leave == null)
        {
            return Result.Failure("Leave request not found");
        }

        // Validate that only pending leaves can be approved
        if (leave.Status != LeaveStatus.Pending)
        {
            return Result.Failure($"Cannot approve leave with status '{leave.Status}'. Only pending leave requests can be approved.");
        }

        leave.Status = LeaveStatus.Approved;
        leave.ApprovedBy = approvedBy;
        leave.ApprovedAt = DateTime.UtcNow;
        leave.ApprovalNotes = notes;
        leave.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> RejectLeaveAsync(Guid leaveId, string? notes)
    {
        var leave = await _context.Leaves
            .Include(l => l.Employee)
            .FirstOrDefaultAsync(l => l.Id == leaveId && !l.IsDeleted);

        if (leave == null)
        {
            return Result.Failure("Leave request not found");
        }

        // Validate that only pending leaves can be rejected
        if (leave.Status != LeaveStatus.Pending)
        {
            return Result.Failure($"Cannot reject leave with status '{leave.Status}'. Only pending leave requests can be rejected.");
        }

        leave.Status = LeaveStatus.Rejected;
        leave.ApprovalNotes = notes;
        leave.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result<PayrollDto>> GeneratePayrollAsync(Guid employeeId, int month, int year)
    {
        // Validate input
        if (month < 1 || month > 12)
        {
            return Result<PayrollDto>.Failure("Invalid month. Must be between 1 and 12");
        }

        if (year < 2000 || year > 2100)
        {
            return Result<PayrollDto>.Failure("Invalid year");
        }

        var employee = await _context.Employees.FindAsync(employeeId);
        if (employee == null)
        {
            return Result<PayrollDto>.Failure("Employee not found");
        }

        if (!employee.IsActive)
        {
            return Result<PayrollDto>.Failure("Cannot generate payroll for inactive employee");
        }

        // Check for existing payroll
        var existingPayroll = await _context.Payrolls
            .FirstOrDefaultAsync(p => p.EmployeeId == employeeId && p.Month == month && p.Year == year);

        if (existingPayroll != null)
        {
            return Result<PayrollDto>.Failure("Payroll already exists for this period");
        }

        // ? FIX: Calculate date range properly
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        // Get attendance records for the month
        var attendances = await _context.Attendances
            .Where(a => a.EmployeeId == employeeId && a.Date >= startDate && a.Date <= endDate)
            .ToListAsync();

        // ? FIX #1: Calculate actual work hours and overtime separately
        var totalWorkHours = attendances
            .Where(a => a.WorkHours.HasValue)
            .Sum(a => a.WorkHours!.Value.TotalHours);

        var totalOvertimeHours = attendances
            .Where(a => a.OvertimeHours.HasValue)
            .Sum(a => a.OvertimeHours!.Value.TotalHours);

        // ? FIX #2: Overtime rate should be 0.5x (the premium only), not 1.5x
        // Because BaseSalary already covers regular hours worked
        // Overtime pay = overtime hours + hourly rate + 0.5 (the 50% premium)
        var hourlyRate = employee.BaseSalary / 160; // Assuming 160 work hours per month
        var overtimePay = (decimal)totalOvertimeHours * hourlyRate * 0.5m; // ? Changed from 1.5m to 0.5m

        // ? ALTERNATIVE FIX (if overtime should be paid at 1.5x total):
        // In this case, you need to deduct the regular pay for those hours first
        // var overtimePay = (decimal)totalOvertimeHours * hourlyRate * 1.5m;
        // var regularPayForOvertimeHours = (decimal)totalOvertimeHours * hourlyRate;
        // var netOvertimePremium = overtimePay - regularPayForOvertimeHours; // This gives 0.5x

        // ? NEW: Calculate allowances based on business rules
        var allowances = CalculateAllowances(employee, attendances);

        // ? NEW: Calculate deductions based on business rules
        var deductions = CalculateDeductions(employee, attendances);

        // ? FIX #3: Gross salary calculation is now correct
        // BaseSalary covers regular hours (up to 160 hours/month)
        // OvertimePay is the premium (50%) for extra hours
        var grossSalary = employee.BaseSalary + allowances + overtimePay;

        // ? IMPROVED: Progressive tax calculation (optional enhancement)
        var tax = CalculateTax(grossSalary);

        var netSalary = grossSalary - tax - deductions;

        // ? NEW: Validate net salary is not negative
        if (netSalary < 0)
        {
            return Result<PayrollDto>.Failure($"Calculated net salary is negative ({netSalary:C}). Please review deductions and tax");
        }

        var payroll = new Payroll
        {
            EmployeeId = employeeId,
            Month = month,
            Year = year,
            BaseSalary = employee.BaseSalary,
            Allowances = allowances,
            Deductions = deductions,
            OvertimePay = overtimePay,
            Tax = tax,
            NetSalary = netSalary,
            Status = PayrollStatus.Draft
        };

        _context.Payrolls.Add(payroll);
        await _context.SaveChangesAsync();

        var dto = new PayrollDto(
            payroll.Id,
            payroll.EmployeeId,
            $"{employee.FirstName} {employee.LastName}",
            payroll.Month,
            payroll.Year,
            payroll.BaseSalary,
            payroll.NetSalary,
            payroll.Status
        );

        return Result<PayrollDto>.Success(dto);
    }

    // ? NEW: Helper method for allowances
    private decimal CalculateAllowances(Employee employee, List<Attendance> attendances)
    {
        decimal allowances = 0m;

        // Transportation allowance (example: $100/month)
        allowances += 100m;

        // Perfect attendance bonus (example: $50 if no absences)
        var hasAbsences = attendances.Any(a => a.Status == AttendanceStatus.Absent);
        if (!hasAbsences && attendances.Count >= 20) // At least 20 working days
        {
            allowances += 50m;
        }

        // Position-based allowance (example)
        if (employee.Position?.Contains("Manager") == true)
        {
            allowances += 200m;
        }

        return allowances;
    }

    // ? NEW: Helper method for deductions
    private decimal CalculateDeductions(Employee employee, List<Attendance> attendances)
    {
        decimal deductions = 0m;

        // Late deductions (example: $5 per late attendance)
        var lateCount = attendances.Count(a => a.Status == AttendanceStatus.Late);
        deductions += lateCount * 5m;

        // Absence deductions (example: daily rate + absent days)
        var absentCount = attendances.Count(a => a.Status == AttendanceStatus.Absent);
        var dailyRate = employee.BaseSalary / 22; // Assuming 22 working days/month
        deductions += absentCount * dailyRate;

        // Insurance (example: 2% of base salary)
        deductions += employee.BaseSalary * 0.02m;

        return deductions;
    }

    // ? NEW: Helper method for progressive tax calculation
    private decimal CalculateTax(decimal grossSalary)
    {
        // Simple progressive tax example
        // 0-5000: 0%
        // 5000-10000: 5%
        // 10000+: 10%

        if (grossSalary <= 5000)
        {
            return 0m;
        }
        else if (grossSalary <= 10000)
        {
            return (grossSalary - 5000) * 0.05m;
        }
        else
        {
            return (5000 * 0.05m) + ((grossSalary - 10000) * 0.10m);
        }
    }
    public async Task<Result<List<PayrollDto>>> GetPayrollsAsync(int month, int year)
    {
        var payrolls = await _context.Payrolls
            .Include(p => p.Employee)
            .Where(p => p.Month == month && p.Year == year)
            .ToListAsync();

        var dtos = payrolls.Select(p => new PayrollDto(
            p.Id, p.EmployeeId, $"{p.Employee.FirstName} {p.Employee.LastName}",
            p.Month, p.Year, p.BaseSalary, p.NetSalary, p.Status
        )).ToList();

        return Result<List<PayrollDto>>.Success(dtos);
    }

    public async Task<Result<JobPostingDto>> CreateJobPostingAsync(CreateJobPostingRequest request)
    {
        var job = new JobPosting
        {
            Title = request.Title,
            Department = request.Department,
            Location = request.Location,
            Description = request.Description,
            Requirements = request.Requirements,
            MinSalary = request.MinSalary,
            MaxSalary = request.MaxSalary,
            Status = JobPostingStatus.Draft
        };
        _context.JobPostings.Add(job);
        await _context.SaveChangesAsync();

        return Result<JobPostingDto>.Success(new JobPostingDto(job.Id, job.Title, job.Department, job.Location, job.Status, job.PublishedAt, job.ClosedAt));
    }

    public async Task<Result<List<JobPostingDto>>> GetAllJobPostingsAsync()
    {
        var jobs = await _context.JobPostings.Where(j => !j.IsDeleted).ToListAsync();
        var dtos = jobs.Select(j => new JobPostingDto(j.Id, j.Title, j.Department, j.Location, j.Status, j.PublishedAt, j.ClosedAt)).ToList();
        return Result<List<JobPostingDto>>.Success(dtos);
    }

    public async Task<Result<CandidateDto>> ApplyForJobAsync(ApplyForJobRequest request)
    {
        var job = await _context.JobPostings.FirstOrDefaultAsync(j => j.Id == request.JobPostingId && !j.IsDeleted);
        if (job == null) return Result<CandidateDto>.Failure("Job posting not found");

        var candidate = new Candidate
        {
            JobPostingId = request.JobPostingId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            ResumeUrl = request.ResumeUrl,
            ResumeContent = request.ResumeContent,
            CoverLetter = request.CoverLetter,
            Status = CandidateStatus.Applied,
            AppliedAt = DateTime.UtcNow
        };
        _context.Candidates.Add(candidate);
        await _context.SaveChangesAsync();

        return Result<CandidateDto>.Success(new CandidateDto(candidate.Id, candidate.JobPostingId, candidate.FirstName, candidate.LastName, candidate.Email, candidate.PhoneNumber, candidate.ResumeUrl, candidate.ResumeContent, candidate.Status, candidate.AppliedAt));
    }

    public async Task<Result<List<CandidateDto>>> GetCandidatesByJobAsync(Guid jobPostingId)
    {
        var candidates = await _context.Candidates.Where(c => c.JobPostingId == jobPostingId && !c.IsDeleted).ToListAsync();
        var dtos = candidates.Select(c => new CandidateDto(c.Id, c.JobPostingId, c.FirstName, c.LastName, c.Email, c.PhoneNumber, c.ResumeUrl, c.ResumeContent, c.Status, c.AppliedAt)).ToList();
        return Result<List<CandidateDto>>.Success(dtos);
    }

    public async Task<Result<InterviewDto>> ScheduleInterviewAsync(ScheduleInterviewRequest request)
    {
        var candidate = await _context.Candidates.Include(c => c.JobPosting).FirstOrDefaultAsync(c => c.Id == request.CandidateId && !c.IsDeleted);
        if (candidate == null) return Result<InterviewDto>.Failure("Candidate not found");

        var interviewer = await _context.Employees.FirstOrDefaultAsync(e => e.Id == request.InterviewerId && !e.IsDeleted);
        if (interviewer == null) return Result<InterviewDto>.Failure("Interviewer not found");

        var interview = new Interview
        {
            CandidateId = request.CandidateId,
            InterviewerId = request.InterviewerId,
            ScheduledAt = request.ScheduledAt,
            DurationMinutes = request.DurationMinutes,
            InterviewType = request.InterviewType,
            Status = InterviewStatus.Scheduled
        };
        _context.Interviews.Add(interview);

        candidate.Status = CandidateStatus.Interviewing;
        await _context.SaveChangesAsync();

        return Result<InterviewDto>.Success(new InterviewDto(interview.Id, candidate.Id, candidate.FirstName + " " + candidate.LastName, interviewer.Id, interviewer.FirstName + " " + interviewer.LastName, interview.ScheduledAt, interview.DurationMinutes, interview.InterviewType, interview.Status));
    }

    public async Task<Result<JobPostingDto>> GetJobPostingByIdAsync(Guid id)
    {
        var job = await _context.JobPostings.FirstOrDefaultAsync(j => j.Id == id && !j.IsDeleted);
        if (job == null) return Result<JobPostingDto>.Failure("Job posting not found");
        return Result<JobPostingDto>.Success(new JobPostingDto(job.Id, job.Title, job.Department, job.Location, job.Status, job.PublishedAt, job.ClosedAt));
    }

    public async Task<Result<JobPostingDto>> UpdateJobPostingAsync(Guid id, UpdateJobPostingRequest request)
    {
        var job = await _context.JobPostings.FirstOrDefaultAsync(j => j.Id == id && !j.IsDeleted);
        if (job == null) return Result<JobPostingDto>.Failure("Job posting not found");

        job.Title = request.Title;
        job.Department = request.Department;
        job.Location = request.Location;
        job.Description = request.Description;
        job.Requirements = request.Requirements;
        job.MinSalary = request.MinSalary;
        job.MaxSalary = request.MaxSalary;
        job.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Result<JobPostingDto>.Success(new JobPostingDto(job.Id, job.Title, job.Department, job.Location, job.Status, job.PublishedAt, job.ClosedAt));
    }

    public async Task<Result> UpdateJobPostingStatusAsync(Guid id, JobPostingStatus status)
    {
        var job = await _context.JobPostings.FirstOrDefaultAsync(j => j.Id == id && !j.IsDeleted);
        if (job == null) return Result.Failure("Job posting not found");

        job.Status = status;
        if (status == JobPostingStatus.Published) job.PublishedAt = DateTime.UtcNow;
        if (status == JobPostingStatus.Closed) job.ClosedAt = DateTime.UtcNow;
        job.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> DeleteJobPostingAsync(Guid id)
    {
        var job = await _context.JobPostings.FirstOrDefaultAsync(j => j.Id == id && !j.IsDeleted);
        if (job == null) return Result.Failure("Job posting not found");

        job.IsDeleted = true;
        job.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result<CandidateDto>> GetCandidateByIdAsync(Guid id)
    {
        var cand = await _context.Candidates.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        if (cand == null) return Result<CandidateDto>.Failure("Candidate not found");
        return Result<CandidateDto>.Success(new CandidateDto(cand.Id, cand.JobPostingId, cand.FirstName, cand.LastName, cand.Email, cand.PhoneNumber, cand.ResumeUrl, cand.ResumeContent, cand.Status, cand.AppliedAt));
    }

    public async Task<Result> UpdateCandidateStatusAsync(Guid id, CandidateStatus status)
    {
        var cand = await _context.Candidates.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        if (cand == null) return Result.Failure("Candidate not found");

        cand.Status = status;
        cand.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> DeleteCandidateAsync(Guid id)
    {
        var cand = await _context.Candidates.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        if (cand == null) return Result.Failure("Candidate not found");

        cand.IsDeleted = true;
        cand.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> UpdateInterviewFeedbackAsync(Guid id, int rating, string feedback)
    {
        var interview = await _context.Interviews.FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
        if (interview == null) return Result.Failure("Interview not found");

        interview.Rating = rating;
        interview.Feedback = feedback;
        interview.Status = InterviewStatus.Completed;
        interview.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> CancelInterviewAsync(Guid id)
    {
        var interview = await _context.Interviews.FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
        if (interview == null) return Result.Failure("Interview not found");

        interview.Status = InterviewStatus.Cancelled;
        interview.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> DeleteInterviewAsync(Guid id)
    {
        var interview = await _context.Interviews.FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
        if (interview == null) return Result.Failure("Interview not found");

        interview.IsDeleted = true;
        interview.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Result.Success();
    }

    /// <summary>
    /// Predicts whether an employee is at risk of attrition using ML model.
    /// </summary>
    public async Task<Result<HRAttritionResponse>> PredictEmployeeAttritionAsync(Guid employeeId, List<double> features)
    {
        try
        {
            // Verify employee exists
            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == employeeId && !e.IsDeleted);
            if (employee == null)
                return Result<HRAttritionResponse>.Failure("Employee not found");

            // Call the attrition service
            var result = await _hrAttritionService.PredictAttritionAsync(features);

            return result;
        }
        catch (Exception ex)
        {
            return Result<HRAttritionResponse>.Failure($"Error predicting attrition: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if the HR Attrition service is healthy and accessible.
    /// </summary>
    public async Task<bool> CheckAttritionServiceHealthAsync()
    {
        return await _hrAttritionService.HealthCheckAsync();
    }

    // RESUME SCREENING & MATCHING IMPLEMENTATIONS

    /// <summary>
    /// Save resume matching results to database
    /// </summary>
    public async Task<Result<bool>> SaveResumeScreeningResultsAsync(
        Guid jobPostingId, 
        List<CandidateMatchResult> matches,
        string? notes)
    {
        try
        {
            var batch = new ResumeScreeningBatch
            {
                JobPostingId = jobPostingId,
                TotalCandidatesMatched = matches.Count,
                Notes = notes,
                ScreenedAt = DateTime.UtcNow,
                MatchResults = matches.Select((m, idx) => new ResumeMatchResult
                {
                    CandidateId = m.CandidateId,
                    JobPostingId = jobPostingId,
                    SimilarityScore = (decimal)m.SimilarityScore,
                    Rank = m.Rank,
                    Domain = m.Category,
                    MatchedAt = DateTime.UtcNow
                }).ToList()
            };

            _context.ResumeScreeningBatches.Add(batch);
            await _context.SaveChangesAsync();

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure($"Error saving screening results: {ex.Message}");
        }
    }

    /// <summary>
    /// Get resume match results for a specific job posting
    /// </summary>
    public async Task<Result<List<ResumeMatchResultDto>>> GetResumeMatchResultsAsync(Guid jobPostingId)
    {
        try
        {
            var results = await _context.ResumeMatchResults
                .Where(r => r.JobPostingId == jobPostingId)
                .Include(r => r.Candidate)
                .OrderByDescending(r => r.SimilarityScore)
                .Select(r => new ResumeMatchResultDto(
                    r.Id,
                    r.JobPostingId,
                    r.CandidateId,
                    $"{r.Candidate.FirstName} {r.Candidate.LastName}",
                    r.SimilarityScore,
                    r.Rank,
                    r.Domain,
                    r.MatchedAt
                ))
                .ToListAsync();

            return Result<List<ResumeMatchResultDto>>.Success(results);
        }
        catch (Exception ex)
        {
            return Result<List<ResumeMatchResultDto>>.Failure($"Error retrieving results: {ex.Message}");
        }
    }

    /// <summary>
    /// Get match history for a specific candidate across all applications
    /// </summary>
    public async Task<Result<List<ResumeMatchResultDto>>> GetCandidateMatchHistoryAsync(Guid candidateId)
    {
        try
        {
            var results = await _context.ResumeMatchResults
                .Where(r => r.CandidateId == candidateId)
                .Include(r => r.JobPosting)
                .Include(r => r.Candidate)
                .OrderByDescending(r => r.MatchedAt)
                .Select(r => new ResumeMatchResultDto(
                    r.Id,
                    r.JobPostingId,
                    r.CandidateId,
                    $"{r.Candidate.FirstName} {r.Candidate.LastName}",
                    r.SimilarityScore,
                    r.Rank,
                    r.Domain,
                    r.MatchedAt
                ))
                .ToListAsync();

            return Result<List<ResumeMatchResultDto>>.Success(results);
        }
        catch (Exception ex)
        {
            return Result<List<ResumeMatchResultDto>>.Failure($"Error retrieving history: {ex.Message}");
        }
    }

    /// <summary>
    /// Get all screening batches for a job posting
    /// </summary>
    public async Task<Result<List<ResumeScreeningBatchDto>>> GetScreeningBatchesAsync(Guid jobPostingId)
    {
        try
        {
            var batches = await _context.ResumeScreeningBatches
                .Where(b => b.JobPostingId == jobPostingId)
                .Include(b => b.MatchResults)
                .ThenInclude(m => m.Candidate)
                .OrderByDescending(b => b.ScreenedAt)
                .Select(b => new ResumeScreeningBatchDto(
                    b.Id,
                    b.JobPostingId,
                    b.TotalCandidatesMatched,
                    b.Notes,
                    b.ScreenedAt,
                    b.MatchResults.Select(m => new ResumeMatchResultDto(
                        m.Id,
                        m.JobPostingId,
                        m.CandidateId,
                        $"{m.Candidate.FirstName} {m.Candidate.LastName}",
                        m.SimilarityScore,
                        m.Rank,
                        m.Domain,
                        m.MatchedAt
                    )).ToList()
                ))
                .ToListAsync();

            return Result<List<ResumeScreeningBatchDto>>.Success(batches);
        }
        catch (Exception ex)
        {
            return Result<List<ResumeScreeningBatchDto>>.Failure($"Error retrieving batches: {ex.Message}");
        }
    }

    /// <summary>
    /// Delete a screening batch and its results
    /// </summary>
    public async Task<Result<bool>> DeleteScreeningBatchAsync(Guid batchId)
    {
        try
        {
            var batch = await _context.ResumeScreeningBatches
                .Include(b => b.MatchResults)
                .FirstOrDefaultAsync(b => b.Id == batchId && !b.IsDeleted);

            if (batch == null)
                return Result<bool>.Failure("Screening batch not found");

            batch.IsDeleted = true;
            foreach (var result in batch.MatchResults)
            {
                result.IsDeleted = true;
            }

            await _context.SaveChangesAsync();
            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure($"Error deleting batch: {ex.Message}");
        }
    }
}

