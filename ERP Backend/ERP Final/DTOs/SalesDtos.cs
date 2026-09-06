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
/// Represents the customer dto data contract.
/// </summary>
public record CustomerDto(Guid Id, string CustomerCode, string Name, string? Email, string? PhoneNumber, decimal CreditLimit, bool IsActive);
/// <summary>
/// Represents the create customer request data contract.
/// </summary>
public record CreateCustomerRequest(string CustomerCode, string Name, string? ContactPerson, string? Email, string? PhoneNumber, string? Address, decimal CreditLimit);
/// <summary>
/// Represents the lead dto data contract.
/// </summary>
public record LeadDto(Guid Id, string Name, string? Company, string? Email, string? Status);
/// <summary>
/// Represents the create lead request data contract.
/// </summary>
public record CreateLeadRequest(string Name, string? Company, string? Email, string? PhoneNumber, string? Source);
/// <summary>
/// Represents the opportunity dto data contract.
/// </summary>
public record OpportunityDto(Guid Id, string Name, string CustomerName, decimal EstimatedValue, decimal Probability, string Stage);
/// <summary>
/// Represents the create opportunity request data contract.
/// </summary>
public record CreateOpportunityRequest(Guid CustomerId, string Name, decimal EstimatedValue, DateTime ExpectedCloseDate);
/// <summary>
/// Represents the sales order dto data contract.
/// </summary>
public record SalesOrderDto(
    Guid Id,
    string OrderNumber,
    DateTime OrderDate,
    string CustomerName,
    decimal TotalAmount,
    SalesOrderStatus Status,
    List<SalesOrderItemDto> Items  // Add this parameter
);
/// <summary>
/// Represents the sales order item dto data contract.
/// </summary>
public record SalesOrderItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal TaxRate
);
/// <summary>
/// Represents the create sales order request data contract.
/// </summary>
public record CreateSalesOrderRequest(Guid CustomerId, DateTime OrderDate, DateTime ExpectedDeliveryDate, List<SalesOrderItemRequest> Items, string? ShippingAddress, string? Notes);
/// <summary>
/// Represents the convert quotation to order request data contract.
/// </summary>
public record ConvertQuotationToOrderRequest(DateTime OrderDate, DateTime ExpectedDeliveryDate, List<SalesOrderItemRequest> Items, string? ShippingAddress, string? Notes);
/// <summary>
/// Represents the delivery dto data contract.
/// </summary>
public record DeliveryDto(Guid Id, string DeliveryNumber, DateTime DeliveryDate, string SalesOrderNumber);
/// <summary>
/// Represents the delivery item request data contract.
/// </summary>
public record DeliveryItemRequest(Guid SalesOrderItemId, Guid ProductId, decimal DeliveredQuantity);
/// <summary>
/// Represents the create delivery request data contract.
/// </summary>
public record CreateDeliveryRequest(Guid SalesOrderId, Guid WarehouseId, List<DeliveryItemRequest> Items);
/// <summary>
/// Represents the sales order item request data contract.
/// </summary>
public record SalesOrderItemRequest(Guid ProductId, string Description, decimal Quantity, decimal UnitPrice, decimal TaxRate);
/// <summary>
/// Represents the update customer request data contract.
/// </summary>
public record UpdateCustomerRequest(string? Name, string? ContactPerson, string? Email, string? PhoneNumber, string? Address, decimal? CreditLimit, bool? IsActive);
/// <summary>
/// Represents the update lead request data contract.
/// </summary>
public record UpdateLeadRequest(string? Name, string? Company, string? Email, string? PhoneNumber, string? Status, string? Notes);
/// <summary>
/// Represents the update opportunity request data contract.
/// </summary>
public record UpdateOpportunityRequest(string? Name, decimal? EstimatedValue, decimal? Probability, DateTime? ExpectedCloseDate, string? Stage, string? Description);
/// <summary>
/// Represents the update sales order request data contract.
/// </summary>
public record UpdateSalesOrderRequest(DateTime? ExpectedDeliveryDate, string? ShippingAddress, string? Notes);

// Sales Quotation DTOs and Requests
/// <summary>
/// Represents the sales quotation dto data contract.
/// </summary>
public record SalesQuotationDto(
    Guid Id,
    string QuotationNumber,
    DateTime QuotationDate,
    DateTime ValidUntil,
    string CustomerName,
    string? OpportunityName,
    decimal SubTotal,
    decimal TaxAmount,
    decimal DiscountAmount,
    decimal TotalAmount,
    string Status);

/// <summary>
/// Represents the sales quotation item dto data contract.
/// </summary>
public record SalesQuotationItemDto(
    Guid Id,
    string ProductName,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TaxRate,
    decimal LineTotal);

/// <summary>
/// Represents the create sales quotation request data contract.
/// </summary>
public record CreateSalesQuotationRequest(
    Guid CustomerId,
    Guid? OpportunityId,
    DateTime QuotationDate,
    DateTime ValidUntil,
    List<CreateSalesQuotationItemRequest> Items,
    string? Notes,
    string? Terms);

/// <summary>
/// Represents the create sales quotation item request data contract.
/// </summary>
public record CreateSalesQuotationItemRequest(
    Guid ProductId,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal TaxRate);

/// <summary>
/// Represents the update sales quotation request data contract.
/// </summary>
public record UpdateSalesQuotationRequest(
    DateTime? ValidUntil,
    List<CreateSalesQuotationItemRequest>? Items,
    string? Notes,
    string? Terms);

/// <summary>
/// Represents the sales quotation search request data contract.
/// </summary>
public record SalesQuotationSearchRequest
{
    public string? SearchTerm { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? OpportunityId { get; init; }
    public string? Status { get; init; }
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
}

