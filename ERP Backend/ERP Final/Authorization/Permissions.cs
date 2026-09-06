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
/// Represents the permissions domain model.
/// </summary>
public static class Permissions
{
    // Administrator
    public const string All = "all";

    // User Management
    public const string UsersView = "users.view";
    public const string UsersCreate = "users.create";
    public const string UsersEdit = "users.edit";
    public const string UsersDelete = "users.delete";
    public const string UsersManageRoles = "users.manage_roles";
    public const string UsersChangePassword = "users.change_password";

    // Role Management
    public const string RolesView = "roles.view";
    public const string RolesCreate = "roles.create";
    public const string RolesEdit = "roles.edit";
    public const string RolesDelete = "roles.delete";
    public const string RolesAssignPermissions = "roles.assign_permissions";

    // HR & Payroll
    public const string HRViewEmployees = "hr.view_employees";
    public const string HRCreateEmployees = "hr.create_employees";
    public const string HREditEmployees = "hr.edit_employees";
    public const string HRDeleteEmployees = "hr.delete_employees";
    public const string HRViewAttendance = "hr.view_attendance";
    public const string HRManageAttendance = "hr.manage_attendance";
    public const string HRViewLeave = "hr.view_leave";
    public const string HRApproveLeave = "hr.approve_leave";
    public const string HRViewPayroll = "hr.view_payroll";
    public const string HRProcessPayroll = "hr.process_payroll";
    public const string HRViewPerformance = "hr.view_performance";
    public const string HRManagePerformance = "hr.manage_performance";

    // Finance
    public const string FinanceViewInvoices = "finance.view_invoices";
    public const string FinanceCreateInvoices = "finance.create_invoices";
    public const string FinanceEditInvoices = "finance.edit_invoices";
    public const string FinanceDeleteInvoices = "finance.delete_invoices";
    public const string FinanceViewPayments = "finance.view_payments";
    public const string FinanceProcessPayments = "finance.process_payments";
    public const string FinanceViewExpenses = "finance.view_expenses";
    public const string FinanceApproveExpenses = "finance.approve_expenses";
    public const string FinanceViewReports = "finance.view_reports";
    public const string FinanceManageAccounts = "finance.manage_accounts";

    // Inventory
    public const string InventoryViewItems = "inventory.view_items";
    public const string InventoryCreateItems = "inventory.create_items";
    public const string InventoryEditItems = "inventory.edit_items";
    public const string InventoryDeleteItems = "inventory.delete_items";
    public const string InventoryManageStock = "inventory.manage_stock";
    public const string InventoryViewWarehouses = "inventory.view_warehouses";
    public const string InventoryManageWarehouses = "inventory.manage_warehouses";

    // Procurement
    public const string ProcurementViewPurchases = "procurement.view_purchases";
    public const string ProcurementCreatePurchases = "procurement.create_purchases";
    public const string ProcurementEditPurchases = "procurement.edit_purchases";
    public const string ProcurementDeletePurchases = "procurement.delete_purchases";
    public const string ProcurementViewSuppliers = "procurement.view_suppliers";
    public const string ProcurementManageSuppliers = "procurement.manage_suppliers";
    public const string ProcurementApprovePurchases = "procurement.approve_purchases";

    // Sales & CRM
    public const string SalesViewOrders = "sales.view_orders";
    public const string SalesCreateOrders = "sales.create_orders";
    public const string SalesEditOrders = "sales.edit_orders";
    public const string SalesDeleteOrders = "sales.delete_orders";
    public const string SalesViewCustomers = "sales.view_customers";
    public const string SalesManageCustomers = "sales.manage_customers";
    public const string SalesViewQuotations = "sales.view_quotations";
    public const string SalesCreateQuotations = "sales.create_quotations";

    // Projects
    public const string ProjectsView = "projects.view";
    public const string ProjectsCreate = "projects.create";
    public const string ProjectsEdit = "projects.edit";
    public const string ProjectsDelete = "projects.delete";
    public const string ProjectsManageTasks = "projects.manage_tasks";
    public const string ProjectsManageTeam = "projects.manage_team";
    public const string ProjectsViewReports = "projects.view_reports";

    // Reports
    public const string ReportsView = "reports.view";
    public const string ReportsCreate = "reports.create";
    public const string ReportsEdit = "reports.edit";
    public const string ReportsDelete = "reports.delete";
    public const string ReportsExport = "reports.export";
    public const string ReportsSchedule = "reports.schedule";

    // Expenses
    public const string ExpensesView = "expenses.view";
    public const string ExpensesSubmit = "expenses.submit";
    public const string ExpensesApprove = "expenses.approve";
    public const string ExpensesReject = "expenses.reject";

    /// <summary>
    /// All available permissions in the system
    /// </summary>
    public static readonly string[] AllPermissions = new[]
    {
        All,
        // Users
        UsersView, UsersCreate, UsersEdit, UsersDelete, UsersManageRoles, UsersChangePassword,
        // Roles
        RolesView, RolesCreate, RolesEdit, RolesDelete, RolesAssignPermissions,
        // HR
        HRViewEmployees, HRCreateEmployees, HREditEmployees, HRDeleteEmployees,
        HRViewAttendance, HRManageAttendance, HRViewLeave, HRApproveLeave,
        HRViewPayroll, HRProcessPayroll, HRViewPerformance, HRManagePerformance,
        // Finance
        FinanceViewInvoices, FinanceCreateInvoices, FinanceEditInvoices, FinanceDeleteInvoices,
        FinanceViewPayments, FinanceProcessPayments, FinanceViewExpenses, FinanceApproveExpenses,
        FinanceViewReports, FinanceManageAccounts,
        // Inventory
        InventoryViewItems, InventoryCreateItems, InventoryEditItems, InventoryDeleteItems,
        InventoryManageStock, InventoryViewWarehouses, InventoryManageWarehouses,
        // Procurement
        ProcurementViewPurchases, ProcurementCreatePurchases, ProcurementEditPurchases, ProcurementDeletePurchases,
        ProcurementViewSuppliers, ProcurementManageSuppliers, ProcurementApprovePurchases,
        // Sales
        SalesViewOrders, SalesCreateOrders, SalesEditOrders, SalesDeleteOrders,
        SalesViewCustomers, SalesManageCustomers, SalesViewQuotations, SalesCreateQuotations,
        // Projects
        ProjectsView, ProjectsCreate, ProjectsEdit, ProjectsDelete,
        ProjectsManageTasks, ProjectsManageTeam, ProjectsViewReports,
        // Reports
        ReportsView, ReportsCreate, ReportsEdit, ReportsDelete, ReportsExport, ReportsSchedule,
        // Expenses
        ExpensesView, ExpensesSubmit, ExpensesApprove, ExpensesReject
    };
}

/// <summary>
/// Predefined role templates with standard permissions
/// </summary>
