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
/// Represents the role templates domain model.
/// </summary>
public static class RoleTemplates
{
    public static readonly Dictionary<string, (string Description, string[] Permissions)> Templates = new()
    {
        ["Administrator"] = (
            "Full system access with all permissions",
            new[] { Permissions.All }
        ),
        ["Manager"] = (
            "Department manager with approval rights",
            new[]
            {
                Permissions.UsersView, Permissions.RolesView,
                Permissions.HRViewEmployees, Permissions.HRApproveLeave,
                Permissions.FinanceViewInvoices, Permissions.FinanceApproveExpenses,
                Permissions.InventoryViewItems, Permissions.ProcurementApprovePurchases,
                Permissions.SalesViewOrders, Permissions.ProjectsView,
                Permissions.ReportsView, Permissions.ExpensesApprove
            }
        ),
        ["HR Officer"] = (
            "Human resources management",
            new[]
            {
                Permissions.HRViewEmployees, Permissions.HRCreateEmployees, Permissions.HREditEmployees,
                Permissions.HRViewAttendance, Permissions.HRManageAttendance,
                Permissions.HRViewLeave, Permissions.HRApproveLeave,
                Permissions.HRViewPayroll, Permissions.HRViewPerformance, Permissions.HRManagePerformance
            }
        ),
        ["Accountant"] = (
            "Financial management and accounting",
            new[]
            {
                Permissions.FinanceViewInvoices, Permissions.FinanceCreateInvoices, Permissions.FinanceEditInvoices,
                Permissions.FinanceViewPayments, Permissions.FinanceProcessPayments,
                Permissions.FinanceViewExpenses, Permissions.FinanceViewReports, Permissions.FinanceManageAccounts,
                Permissions.ExpensesView, Permissions.ExpensesApprove
            }
        ),
        ["Warehouse Manager"] = (
            "Inventory and warehouse operations",
            new[]
            {
                Permissions.InventoryViewItems, Permissions.InventoryCreateItems, Permissions.InventoryEditItems,
                Permissions.InventoryManageStock, Permissions.InventoryViewWarehouses, Permissions.InventoryManageWarehouses,
                Permissions.ProcurementViewPurchases, Permissions.ProcurementViewSuppliers
            }
        ),
        ["Sales Representative"] = (
            "Sales and customer relationship management",
            new[]
            {
                Permissions.SalesViewOrders, Permissions.SalesCreateOrders, Permissions.SalesEditOrders,
                Permissions.SalesViewCustomers, Permissions.SalesManageCustomers,
                Permissions.SalesViewQuotations, Permissions.SalesCreateQuotations
            }
        ),
        ["Project Manager"] = (
            "Project and team management",
            new[]
            {
                Permissions.ProjectsView, Permissions.ProjectsCreate, Permissions.ProjectsEdit,
                Permissions.ProjectsManageTasks, Permissions.ProjectsManageTeam, Permissions.ProjectsViewReports
            }
        ),
        ["Employee"] = (
            "Basic employee access",
            new[]
            {
                Permissions.HRViewEmployees, // Self only
                Permissions.HRViewAttendance, // Self only
                Permissions.HRViewLeave, // Self only
                Permissions.ExpensesView, // Self only
                Permissions.ExpensesSubmit
            }
        )
    };
}
