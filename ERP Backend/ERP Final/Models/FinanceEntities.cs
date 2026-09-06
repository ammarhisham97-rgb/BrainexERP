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


// Domain Models
/// <summary>
/// Represents the account domain model.
/// </summary>
public class Account : BaseEntity
{
    [Required, MaxLength(50)]
    public string AccountCode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string AccountName { get; set; } = string.Empty;

    public AccountType Type { get; set; }

    public Guid? ParentAccountId { get; set; }
    public Account? ParentAccount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Balance { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    // Relationships
    public List<Account> SubAccounts { get; set; } = new();
    public List<JournalEntryLine> JournalEntryLines { get; set; } = new();
}

/// <summary>
/// Represents the journal entry domain model.
/// </summary>
public class JournalEntry : BaseEntity
{
    [Required, MaxLength(50)]
    public string EntryNumber { get; set; } = string.Empty;

    public DateTime EntryDate { get; set; }

    [Required, MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Reference { get; set; }

    public bool IsPosted { get; set; } = false;
    public DateTime? PostedAt { get; set; }

    // Relationships
    public List<JournalEntryLine> Lines { get; set; } = new();
}

/// <summary>
/// Represents the journal entry line domain model.
/// </summary>
public class JournalEntryLine : BaseEntity
{
    public Guid JournalEntryId { get; set; }
    public JournalEntry JournalEntry { get; set; } = null!;

    public Guid AccountId { get; set; }
    public Account Account { get; set; } = null!;

    [Column(TypeName = "decimal(18,2)")]
    public decimal DebitAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CreditAmount { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }
}

/// <summary>
/// Represents the invoice domain model.
/// </summary>
public class Invoice : BaseEntity
{
    [Required, MaxLength(50)]
    public string InvoiceNumber { get; set; } = string.Empty;

    public DateTime InvoiceDate { get; set; }
    public DateTime DueDate { get; set; }

    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SubTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PaidAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BalanceAmount { get; set; }

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [MaxLength(500)]
    public string? Terms { get; set; }
    [Timestamp] // ? ADD THIS
    public byte[] RowVersion { get; set; }

    // Relationships
    public List<InvoiceItem> Items { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
}

/// <summary>
/// Represents the invoice item domain model.
/// </summary>
public class InvoiceItem : BaseEntity
{
    public Guid InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    public Guid? ProductId { get; set; }
    public Product? Product { get; set; }

    [Required, MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,3)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxRate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineTotal { get; set; }
}

/// <summary>
/// Represents the payment domain model.
/// </summary>
public class Payment : BaseEntity
{
    [Required, MaxLength(50)]
    public string PaymentNumber { get; set; } = string.Empty;

    public DateTime PaymentDate { get; set; }

    public Guid? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public PaymentMethod Method { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    [MaxLength(200)]
    public string? ReferenceNumber { get; set; }
    [Timestamp] // ? ADD THIS
    public byte[] RowVersion { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// Represents the expense domain model.
/// </summary>
public class Expense : BaseEntity
{
    [Required, MaxLength(50)]
    public string ExpenseNumber { get; set; } = string.Empty;

    public DateTime ExpenseDate { get; set; }

    [Required, MaxLength(100)]
    public string Category { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(200)]
    public string? ReceiptNumber { get; set; }

    public bool IsApproved { get; set; } = false;
    public Guid? ApprovedBy { get; set; }
}

/// <summary>
/// Represents the budget domain model.
/// </summary>
public class Budget : BaseEntity
{
    [Required, MaxLength(50)]
    public string BudgetNumber { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string BudgetName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Department { get; set; }

    [MaxLength(100)]
    public string FiscalYear { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalBudgetAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ActualAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Variance { get; set; }

    public BudgetStatus Status { get; set; } = BudgetStatus.Draft;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }

    // Relationships
    public List<BudgetLine> BudgetLines { get; set; } = new();
}

/// <summary>
/// Represents the budget line domain model.
/// </summary>
public class BudgetLine : BaseEntity
{
    public Guid BudgetId { get; set; }
    public Budget Budget { get; set; } = null!;

    public Guid AccountId { get; set; }
    public Account Account { get; set; } = null!;

    [Required, MaxLength(200)]
    public string LineDescription { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal BudgetedAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ActualAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Variance { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal VariancePercentage { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

