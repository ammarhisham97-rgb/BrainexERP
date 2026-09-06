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
/// Defines the i finance service service contract.
/// </summary>
public interface IFinanceService
{
    Task<Result<AccountDto>> CreateAccountAsync(CreateAccountRequest request);
    Task<Result<List<AccountDto>>> GetAllAccountsAsync();
    Task<Result<JournalEntryDto>> CreateJournalEntryAsync(CreateJournalEntryRequest request);
    Task<Result> PostJournalEntryAsync(Guid entryId);
    Task<Result<InvoiceDto>> CreateInvoiceAsync(Guid customerId, DateTime invoiceDate, DateTime dueDate, List<InvoiceItemRequest> items);
    Task<Result<List<InvoiceDto>>> GetInvoicesAsync(InvoiceStatus? status = null);
    Task<Result<PaymentDto>> RecordPaymentAsync(Guid invoiceId, decimal amount, PaymentMethod method, DateTime paymentDate);
    Task<Result<Dictionary<string, decimal>>> GetFinancialSummaryAsync(DateTime startDate, DateTime endDate);
    Task<Result<PagedResult<InvoiceDto>>> SearchInvoicesAsync(InvoiceSearchRequest search, PaginationRequest pagination);
    Task<Result<AccountDto>> GetAccountByIdAsync(Guid id);
    Task<Result<AccountDto>> UpdateAccountAsync(Guid id, UpdateAccountRequest request);
    Task<Result> DeactivateAccountAsync(Guid id);
    Task<Result<JournalEntryDetailDto>> GetJournalEntryByIdAsync(Guid id);
    Task<Result<List<JournalEntryDetailDto>>> GetAllJournalEntriesAsync(bool? isPosted = null);
    Task<Result> DeleteJournalEntryAsync(Guid id);
    Task<Result<InvoiceDto>> GetInvoiceByIdAsync(Guid id);
    Task<Result<InvoiceDto>> UpdateInvoiceAsync(Guid id, UpdateInvoiceRequest request);
    Task<Result> DeleteInvoiceAsync(Guid id);
    Task<Result> CancelInvoiceAsync(Guid id);
    Task<Result<PaymentDto>> GetPaymentByIdAsync(Guid id);
    Task<Result<List<PaymentDto>>> GetAllPaymentsAsync(Guid? invoiceId = null, Guid? customerId = null);
    Task<Result> DeletePaymentAsync(Guid id);

    Task<Result<BudgetDto>> CreateBudgetAsync(CreateBudgetRequest request);
    Task<Result<List<BudgetDto>>> GetAllBudgetsAsync(BudgetStatus? status = null);
    Task<Result<BudgetDto>> GetBudgetByIdAsync(Guid id);
    Task<Result<BudgetDto>> UpdateBudgetAsync(Guid id, UpdateBudgetRequest request);
    Task<Result> DeleteBudgetAsync(Guid id);
    Task<Result> ApproveBudgetAsync(Guid id, Guid approvedBy);
    Task<Result> RejectBudgetAsync(Guid id);
    Task<Result> ActivateBudgetAsync(Guid id);
    Task<Result> CloseBudgetAsync(Guid id);
    Task<Result<PagedResult<BudgetDto>>> SearchBudgetsAsync(BudgetSearchRequest search, PaginationRequest pagination);
    Task<Result<List<BudgetLineDto>>> GetBudgetLinesAsync(Guid budgetId);
    Task<Result> UpdateBudgetActuals(Guid budgetId);

    Task<Result<PaymentFraudAnalysisDto>> AnalyzePaymentFraudAsync(Guid paymentId, Guid invoiceId, List<double> transactionFeatures);
    Task<Result<List<PaymentFraudAnalysisDto>>> GetFraudAnalysisHistoryAsync(Guid invoiceId);
    Task<bool> CheckFraudDetectionHealthAsync();
}



// FRAUD DETECTION SERVICE INTERFACE


/// <summary>
/// Defines the i fraud detection service service contract.
/// </summary>
public interface IFraudDetectionService
{
    /// <summary>
    /// Predicts whether a credit card transaction is fraudulent.
    /// </summary>
    /// <param name="features">List of 30 transaction features (V1-V28, Amount, Time)</param>
    /// <returns>Fraud detection analysis result</returns>
    Task<Result<FraudDetectionResponse>> PredictFraudAsync(List<double> features);

    /// <summary>
    /// Health check for fraud detection API.
    /// </summary>
    /// <returns>True if API is available, false otherwise</returns>
    Task<bool> HealthCheckAsync();
}

/// <summary>
/// Interface for HR Attrition prediction service integration
/// </summary>
public interface IHRAttritionService
{
    /// <summary>
    /// Predicts whether an employee is at risk of attrition.
    /// </summary>
    /// <param name="features">List of 30 employee features for prediction</param>
    /// <returns>HR Attrition prediction result with risk level and confidence</returns>
    Task<Result<HRAttritionResponse>> PredictAttritionAsync(List<double> features);

    /// <summary>
    /// Health check for HR Attrition API.
    /// </summary>
    /// <returns>True if API is available, false otherwise</returns>
    Task<bool> HealthCheckAsync();
}



