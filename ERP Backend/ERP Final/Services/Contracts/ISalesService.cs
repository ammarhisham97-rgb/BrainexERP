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
/// Defines the i sales service service contract.
/// </summary>
public interface ISalesService
{
    Task<Result<CustomerDto>> CreateCustomerAsync(CreateCustomerRequest request);
    Task<Result<List<CustomerDto>>> GetAllCustomersAsync();
    Task<Result<LeadDto>> CreateLeadAsync(string name, string? company, string? email, string? phoneNumber, string? source);
    Task<Result> ConvertLeadToCustomerAsync(Guid leadId, CreateCustomerRequest customerRequest);
    Task<Result<OpportunityDto>> CreateOpportunityAsync(Guid customerId, string name, decimal estimatedValue, DateTime expectedCloseDate);
    Task<Result<SalesOrderDto>> CreateSalesOrderAsync(CreateSalesOrderRequest request);
    Task<Result<List<SalesOrderDto>>> GetAllSalesOrdersAsync(SalesOrderStatus? status = null);
    Task<Result> ConfirmSalesOrderAsync(Guid orderId);
    Task<Result<DeliveryDto>> CreateDeliveryAsync(Guid salesOrderId, Guid warehouseId, List<DeliveryItemRequest> items);
    Task<Result<PagedResult<CustomerDto>>> SearchCustomersAsync(CustomerSearchRequest search, PaginationRequest pagination);
    Task<Result<PagedResult<SalesOrderDto>>> SearchSalesOrdersAsync(SalesOrderSearchRequest search, PaginationRequest pagination);
    Task<Result<CustomerDto>> GetCustomerByIdAsync(Guid id);
    Task<Result<CustomerDto>> UpdateCustomerAsync(Guid id, UpdateCustomerRequest request);
    Task<Result> DeactivateCustomerAsync(Guid id);
    Task<Result<LeadDto>> GetLeadByIdAsync(Guid id);
    Task<Result<List<LeadDto>>> GetAllLeadsAsync(string? status = null);
    Task<Result<LeadDto>> UpdateLeadAsync(Guid id, UpdateLeadRequest request);
    Task<Result> DeleteLeadAsync(Guid id);
    Task<Result<OpportunityDto>> GetOpportunityByIdAsync(Guid id);
    Task<Result<List<OpportunityDto>>> GetAllOpportunitiesAsync(Guid? customerId = null, string? stage = null);
    Task<Result<OpportunityDto>> UpdateOpportunityAsync(Guid id, UpdateOpportunityRequest request);
    Task<Result> DeleteOpportunityAsync(Guid id);
    Task<Result<SalesOrderDto>> GetSalesOrderByIdAsync(Guid id);
    Task<Result<SalesOrderDto>> UpdateSalesOrderAsync(Guid id, UpdateSalesOrderRequest request);
    Task<Result> DeleteSalesOrderAsync(Guid id);
    Task<Result> CancelSalesOrderAsync(Guid id);
    Task<Result<DeliveryDto>> GetDeliveryByIdAsync(Guid id);
    Task<Result<List<DeliveryDto>>> GetAllDeliveriesAsync(Guid? salesOrderId = null);
    Task<Result> DeleteDeliveryAsync(Guid id);

    // Sales Quotation Methods
    Task<Result<SalesQuotationDto>> CreateSalesQuotationAsync(CreateSalesQuotationRequest request);
    Task<Result<SalesQuotationDto>> GetSalesQuotationByIdAsync(Guid id);
    Task<Result<List<SalesQuotationDto>>> GetAllSalesQuotationsAsync(string? status = null);
    Task<Result<PagedResult<SalesQuotationDto>>> SearchSalesQuotationsAsync(SalesQuotationSearchRequest search, PaginationRequest pagination);
    Task<Result<SalesQuotationDto>> UpdateSalesQuotationAsync(Guid id, UpdateSalesQuotationRequest request);
    Task<Result> DeleteSalesQuotationAsync(Guid id);
    Task<Result> ApproveSalesQuotationAsync(Guid id);
    Task<Result> RejectSalesQuotationAsync(Guid id);
    Task<Result> ConvertQuotationToSalesOrderAsync(Guid quotationId, ConvertQuotationToOrderRequest orderRequest);
}




