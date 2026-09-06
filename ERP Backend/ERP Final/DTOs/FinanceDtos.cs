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
/// Represents the account dto data contract.
/// </summary>
public record AccountDto(Guid Id, string AccountCode, string AccountName, AccountType Type, decimal Balance, bool IsActive);
/// <summary>
/// Represents the create account request data contract.
/// </summary>
public record CreateAccountRequest(string AccountCode, string AccountName, AccountType Type, Guid? ParentAccountId, string? Description, decimal? InitialBalance);
/// <summary>
/// Represents the journal entry dto data contract.
/// </summary>
public record JournalEntryDto(Guid Id, string EntryNumber, DateTime EntryDate, string Description, bool IsPosted);
/// <summary>
/// Represents the journal entry detail dto data contract.
/// </summary>
public record JournalEntryDetailDto(Guid Id, string EntryNumber, DateTime EntryDate, string Description, bool IsPosted, decimal TotalAmount, List<JournalLineDetailDto> Lines);
/// <summary>
/// Represents the journal line detail dto data contract.
/// </summary>
public record JournalLineDetailDto(Guid AccountId, string? AccountCode, string? AccountName, decimal DebitAmount, decimal CreditAmount, string? Description);
/// <summary>
/// Represents the create journal entry request data contract.
/// </summary>
public record CreateJournalEntryRequest(DateTime EntryDate, string Description, string? Reference, List<JournalLineRequest> Lines);
/// <summary>
/// Represents the journal line request data contract.
/// </summary>
public record JournalLineRequest(Guid AccountId, decimal DebitAmount, decimal CreditAmount, string? Description);
/// <summary>
/// Represents the invoice dto data contract.
/// </summary>
public record InvoiceDto(Guid Id, string InvoiceNumber, DateTime InvoiceDate, DateTime DueDate, string? CustomerName, decimal TotalAmount, decimal BalanceAmount, InvoiceStatus Status);
/// <summary>
/// Represents the invoice item request data contract.
/// </summary>
public record InvoiceItemRequest(Guid? ProductId, string Description, decimal Quantity, decimal UnitPrice, decimal TaxRate);
/// <summary>
/// Represents the create invoice request data contract.
/// </summary>
public record CreateInvoiceRequest(Guid CustomerId, DateTime InvoiceDate, DateTime DueDate, List<InvoiceItemRequest> Items, string? Notes, string? Terms);

/// <summary>
/// Represents the payment dto data contract.
/// </summary>
public record PaymentDto(Guid Id, string PaymentNumber, DateTime PaymentDate, decimal Amount, PaymentMethod Method, PaymentStatus Status);
/// <summary>
/// Represents the record payment request data contract.
/// </summary>
public record RecordPaymentRequest(Guid InvoiceId, decimal Amount, PaymentMethod Method, DateTime PaymentDate, string? ReferenceNumber, string? Notes, List<double>? TransactionFeatures);
/// <summary>
/// Represents the fraud analysis request data contract.
/// </summary>
public record FraudAnalysisRequest(Guid PaymentId, Guid InvoiceId, List<double> TransactionFeatures);

// Fraud Detection UX - Dropdown DTOs
/// <summary>
/// Represents the invoice for fraud detection dto data contract.
/// </summary>
public record InvoiceForFraudDetectionDto(
    Guid Id,
    string InvoiceNumber,
    DateTime InvoiceDate,
    DateTime DueDate,
    string? CustomerName,
    decimal TotalAmount,
    decimal BalanceAmount,
    string Status
);

/// <summary>
/// Represents the payment for dropdown dto data contract.
/// </summary>
public record PaymentForDropdownDto(
    Guid Id,
    string PaymentNumber,
    DateTime PaymentDate,
    decimal Amount,
    string Method
);

/// <summary>
/// Represents the invoice with payments dto data contract.
/// </summary>
public record InvoiceWithPaymentsDto(
    Guid Id,
    string InvoiceNumber,
    DateTime InvoiceDate,
    DateTime DueDate,
    string? CustomerName,
    decimal TotalAmount,
    decimal BalanceAmount,
    string Status,
    List<PaymentForDropdownDto> Payments
);

/// <summary>
/// Represents the update account request data contract.
/// </summary>
public record UpdateAccountRequest(string AccountName, string? Description, bool IsActive);
/// <summary>
/// Represents the update invoice request data contract.
/// </summary>
public record UpdateInvoiceRequest(DateTime? DueDate, string? Notes, string? Terms);

// Budget DTOs
/// <summary>
/// Represents the budget dto data contract.
/// </summary>
public record BudgetDto(
    Guid Id,
    string BudgetNumber,
    string BudgetName,
    string? Department,
    string FiscalYear,
    DateTime StartDate,
    DateTime EndDate,
    decimal TotalBudgetAmount,
    decimal ActualAmount,
    decimal Variance,
    BudgetStatus Status
);

/// <summary>
/// Represents the budget line dto data contract.
/// </summary>
public record BudgetLineDto(
    Guid Id,
    Guid BudgetId,
    Guid AccountId,
    string AccountCode,
    string AccountName,
    string LineDescription,
    decimal BudgetedAmount,
    decimal ActualAmount,
    decimal Variance,
    decimal VariancePercentage
);

/// <summary>
/// Represents the create budget request data contract.
/// </summary>
public record CreateBudgetRequest(
    string BudgetName,
    string? Department,
    string FiscalYear,
    DateTime StartDate,
    DateTime EndDate,
    List<BudgetLineItemRequest> Lines,
    string? Description,
    string? Notes
);

/// <summary>
/// Represents the budget line item request data contract.
/// </summary>
public record BudgetLineItemRequest(
    Guid AccountId,
    string LineDescription,
    decimal BudgetedAmount,
    string? Notes
);

/// <summary>
/// Represents the update budget request data contract.
/// </summary>
public record UpdateBudgetRequest(
    string? BudgetName,
    string? Department,
    DateTime? StartDate,
    DateTime? EndDate,
    string? Description,
    string? Notes
);

/// <summary>
/// Represents the budget search request data contract.
/// </summary>
public record BudgetSearchRequest
{
    public string? SearchTerm { get; init; }
    public string? Department { get; init; }
    public string? FiscalYear { get; init; }
    public BudgetStatus? Status { get; init; }
    public DateTime? StartDateFrom { get; init; }
    public DateTime? StartDateTo { get; init; }
}

// FRAUD DETECTION DTOs

/// <summary>
/// Request model for fraud detection prediction.
/// Expects exactly 30 features: V1-V28 (PCA components), Amount, Time
/// </summary>
public record FraudDetectionRequest
{
    [JsonPropertyName("features")]
    public List<double> Features { get; init; } = new();
}

/// <summary>
/// Response model from fraud detection API.
/// Includes prediction, confidence score, and fraud probability.
/// </summary>
public record FraudDetectionResponse
{
    [JsonPropertyName("prediction")]
    public int Prediction { get; init; }

    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;

    [JsonPropertyName("fraud_probability")]
    public double FraudProbability { get; init; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; init; }
}

/// <summary>
/// DTO for capturing fraud detection result with transaction context.
/// Used internally to log and track fraud flags.
/// </summary>
public record PaymentFraudAnalysisDto
{
    public Guid PaymentId { get; init; }
    public Guid InvoiceId { get; init; }
    public bool IsFraudulent { get; init; }
    public string FraudLabel { get; init; } = string.Empty;
    public double FraudProbability { get; init; }
    public double Confidence { get; init; }
    public DateTime AnalyzedAt { get; init; }
    public string? Notes { get; init; }
}

// HR ATTRITION PREDICTION MODELS

/// <summary>
/// DTO for HR Attrition prediction request
/// </summary>
public record HRAttritionRequest
{
    [JsonPropertyName("features")]
    public List<double> Features { get; init; } = new();
}

/// <summary>
/// DTO for HR Attrition prediction response from Flask API
/// </summary>
public record HRAttritionResponse
{
    [JsonPropertyName("prediction")]
    public int Prediction { get; init; }

    [JsonPropertyName("risk_level")]
    public string RiskLevel { get; init; } = string.Empty;

    [JsonPropertyName("confidence")]
    public double Confidence { get; init; }

    [JsonPropertyName("attrition_probability")]
    public double AttritionProbability { get; init; }
}


// Product Search Request
/// <summary>
/// Represents the product search request data contract.
/// </summary>
public record ProductSearchRequest
{
    public string? SearchTerm { get; init; }
    public Guid? CategoryId { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public bool? InStock { get; init; }
    public bool? IsActive { get; init; }
    public decimal? BelowReorderLevel { get; init; }
}

// Stock Filter Request
/// <summary>
/// Represents the stock filter request data contract.
/// </summary>
public record StockFilterRequest
{
    public Guid? WarehouseId { get; init; }
    public Guid? ProductId { get; init; }
    public bool? LowStock { get; init; }
    public decimal? MinQuantity { get; init; }
    public decimal? MaxQuantity { get; init; }
}


