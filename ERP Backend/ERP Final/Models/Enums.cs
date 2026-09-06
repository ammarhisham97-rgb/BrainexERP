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
/// Defines the supported user status values.
/// </summary>
public enum UserStatus { Active, Inactive, Suspended }
/// <summary>
/// Defines the supported leave type values.
/// </summary>
public enum LeaveType { Sick, Vacation, Personal, Unpaid }
/// <summary>
/// Defines the supported leave status values.
/// </summary>
public enum LeaveStatus { Pending, Approved, Rejected }
/// <summary>
/// Defines the supported attendance status values.
/// </summary>
public enum AttendanceStatus { Present, Absent, Late, HalfDay }
/// <summary>
/// Defines the supported payroll status values.
/// </summary>
public enum PayrollStatus { Draft, Processed, Paid }
/// <summary>
/// Defines the supported account type values.
/// </summary>
public enum AccountType { Asset, Liability, Equity, Revenue, Expense }
/// <summary>
/// Defines the supported transaction type values.
/// </summary>
public enum TransactionType { Debit, Credit }
/// <summary>
/// Defines the supported invoice status values.
/// </summary>
public enum InvoiceStatus { Draft, Sent, Paid, Overdue, Cancelled }
/// <summary>
/// Defines the supported payment status values.
/// </summary>
public enum PaymentStatus { Pending, Completed, Failed, Refunded }
/// <summary>
/// Defines the supported payment method values.
/// </summary>
public enum PaymentMethod { Cash, BankTransfer, CreditCard, Cheque }
/// <summary>
/// Defines the supported stock movement type values.
/// </summary>
public enum StockMovementType { Purchase, Sale, Transfer, Adjustment, Return }
/// <summary>
/// Defines the supported purchase order status values.
/// </summary>
public enum PurchaseOrderStatus { Draft, Submitted, Approved, Received, Cancelled }
/// <summary>
/// Defines the supported sales order status values.
/// </summary>
public enum SalesOrderStatus { Draft, Confirmed, Shipped, Delivered, Cancelled }
/// <summary>
/// Defines the supported project status values.
/// </summary>
public enum ProjectStatus { Planning, InProgress, OnHold, Completed, Cancelled }
/// <summary>
/// Defines the supported task status values.
/// </summary>
public enum TaskStatus { Todo, InProgress, Review, Done }
/// <summary>
/// Defines the supported task priority values.
/// </summary>
public enum TaskPriority { Low, Medium, High, Critical }
/// <summary>
/// Defines the supported budget status values.
/// </summary>
public enum BudgetStatus { Draft, Submitted, UnderReview, Approved, Rejected, Active, Closed }

// ATS Enums
/// <summary>
/// Defines the supported job posting status values.
/// </summary>
public enum JobPostingStatus { Draft, Published, Closed, Cancelled }
/// <summary>
/// Defines the supported candidate status values.
/// </summary>
public enum CandidateStatus { Applied, Screening, Interviewing, Offered, Hired, Rejected, Withdrawn }
/// <summary>
/// Defines the supported interview status values.
/// </summary>
public enum InterviewStatus { Scheduled, Completed, Cancelled, NoShow }
