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
/// Represents the supplier dto data contract.
/// </summary>
public record SupplierDto(Guid Id, string SupplierCode, string Name, string? ContactPerson, string? Email, string? PhoneNumber, bool IsActive);
/// <summary>
/// Represents the create supplier request data contract.
/// </summary>
public record CreateSupplierRequest(string SupplierCode, string Name, string? ContactPerson, string? Email, string? PhoneNumber, string? Address, string? PaymentTerms);
/// <summary>
/// Represents the purchase order item dto data contract.
/// </summary>
public record PurchaseOrderItemDto(Guid Id, Guid ProductId, string ProductCode, string ProductName, string Description, decimal Quantity, decimal ReceivedQuantity, decimal UnitPrice, decimal TaxRate, decimal LineTotal);
/// <summary>
/// Represents the purchase order dto data contract.
/// </summary>
public record PurchaseOrderDto(Guid Id, string OrderNumber, DateTime OrderDate, string SupplierName, decimal TotalAmount, PurchaseOrderStatus Status, List<PurchaseOrderItemDto> Items);
/// <summary>
/// Represents the create purchase order request data contract.
/// </summary>
public record CreatePurchaseOrderRequest(Guid SupplierId, DateTime OrderDate, DateTime ExpectedDeliveryDate, Guid? WarehouseId, List<PurchaseOrderItemRequest> Items, string? Notes);
/// <summary>
/// Represents the purchase order item request data contract.
/// </summary>
public record PurchaseOrderItemRequest(Guid ProductId, string Description, decimal Quantity, decimal UnitPrice, decimal TaxRate);
/// <summary>
/// Represents the goods receipt item request data contract.
/// </summary>
public record GoodsReceiptItemRequest(Guid PurchaseOrderItemId, Guid ProductId, decimal ReceivedQuantity, decimal AcceptedQuantity, decimal RejectedQuantity, string? Notes);

/// <summary>
/// Represents the goods receipt dto data contract.
/// </summary>
public record GoodsReceiptDto(Guid Id, string ReceiptNumber, DateTime ReceiptDate, string PurchaseOrderNumber, string? ReceivedBy);
/// <summary>
/// Represents the update supplier request data contract.
/// </summary>
public record UpdateSupplierRequest(string? Name, string? ContactPerson, string? Email, string? PhoneNumber, string? Address, string? PaymentTerms, bool? IsActive);
/// <summary>
/// Represents the update purchase order request data contract.
/// </summary>
public record UpdatePurchaseOrderRequest(DateTime? ExpectedDeliveryDate, string? Notes);


// Customer Search Request
/// <summary>
/// Represents the customer search request data contract.
/// </summary>
public record CustomerSearchRequest
{
    public string? SearchTerm { get; init; }
    public bool? IsActive { get; init; }
    public decimal? MinCreditLimit { get; init; }
    public decimal? MaxCreditLimit { get; init; }
    public string? City { get; init; }
    public string? Country { get; init; }
}

// Sales Order Search Request
/// <summary>
/// Represents the sales order search request data contract.
/// </summary>
public record SalesOrderSearchRequest
{
    public string? SearchTerm { get; init; }
    public Guid? CustomerId { get; init; }
    public SalesOrderStatus? Status { get; init; }
    public DateTime? OrderDateFrom { get; init; }
    public DateTime? OrderDateTo { get; init; }
    public decimal? MinAmount { get; init; }
    public decimal? MaxAmount { get; init; }
}


