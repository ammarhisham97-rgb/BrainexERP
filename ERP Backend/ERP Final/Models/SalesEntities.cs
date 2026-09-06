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
/// Represents the customer domain model.
/// </summary>
public class Customer : BaseEntity
{
    [Required, MaxLength(50)]
    public string CustomerCode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? ContactPerson { get; set; }

    [EmailAddress, MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(50)]
    public string? PostalCode { get; set; }

    [MaxLength(100)]
    public string? Country { get; set; }

    [MaxLength(50)]
    public string? TaxNumber { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CreditLimit { get; set; }

    [MaxLength(500)]
    public string? PaymentTerms { get; set; }

    public bool IsActive { get; set; } = true;

    // Relationships
    public List<Lead> Leads { get; set; } = new();
    public List<Opportunity> Opportunities { get; set; } = new();
    public List<SalesOrder> SalesOrders { get; set; } = new();
    public List<Invoice> Invoices { get; set; } = new();
}

/// <summary>
/// Represents the lead domain model.
/// </summary>
public class Lead : BaseEntity
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Company { get; set; }

    [EmailAddress, MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    [MaxLength(100)]
    public string? Source { get; set; }

    [MaxLength(100)]
    public string? Status { get; set; }

    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public Guid? AssignedTo { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime? ConvertedAt { get; set; }
}

/// <summary>
/// Represents the opportunity domain model.
/// </summary>
public class Opportunity : BaseEntity
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimatedValue { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal Probability { get; set; }

    public DateTime ExpectedCloseDate { get; set; }

    [MaxLength(100)]
    public string Stage { get; set; } = "Prospecting";

    public Guid? AssignedTo { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsWon { get; set; } = false;
    public DateTime? ClosedAt { get; set; }
}

/// <summary>
/// Represents the sales quotation domain model.
/// </summary>
public class SalesQuotation : BaseEntity
{
    [Required, MaxLength(50)]
    public string QuotationNumber { get; set; } = string.Empty;

    public DateTime QuotationDate { get; set; }
    public DateTime ValidUntil { get; set; }

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public Guid? OpportunityId { get; set; }
    public Opportunity? Opportunity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SubTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [MaxLength(100)]
    public string Status { get; set; } = "Draft";

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [MaxLength(500)]
    public string? Terms { get; set; }

    // Relationships
    public List<SalesQuotationItem> Items { get; set; } = new();
}

/// <summary>
/// Represents the sales quotation item domain model.
/// </summary>
public class SalesQuotationItem : BaseEntity
{
    public Guid SalesQuotationId { get; set; }
    public SalesQuotation SalesQuotation { get; set; } = null!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,3)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountPercent { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxRate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineTotal { get; set; }
}

/// <summary>
/// Represents the sales order domain model.
/// </summary>
public class SalesOrder : BaseEntity
{
    [Required, MaxLength(50)]
    public string OrderNumber { get; set; } = string.Empty;

    public DateTime OrderDate { get; set; }
    public DateTime ExpectedDeliveryDate { get; set; }

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public Guid? QuotationId { get; set; }
    public SalesQuotation? Quotation { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SubTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ShippingCost { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    public SalesOrderStatus Status { get; set; } = SalesOrderStatus.Draft;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [MaxLength(500)]
    public string? ShippingAddress { get; set; }

    // Relationships
    public List<SalesOrderItem> Items { get; set; } = new();
    public List<Delivery> Deliveries { get; set; } = new();
}

/// <summary>
/// Represents the sales order item domain model.
/// </summary>
public class SalesOrderItem : BaseEntity
{
    public Guid SalesOrderId { get; set; }
    public SalesOrder SalesOrder { get; set; } = null!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,3)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal DeliveredQuantity { get; set; }

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
/// Represents the delivery domain model.
/// </summary>
public class Delivery : BaseEntity
{
    [Required, MaxLength(50)]
    public string DeliveryNumber { get; set; } = string.Empty;

    public DateTime DeliveryDate { get; set; }

    public Guid SalesOrderId { get; set; }
    public SalesOrder SalesOrder { get; set; } = null!;

    public Guid? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    [MaxLength(200)]
    public string? DeliveredBy { get; set; }

    [MaxLength(500)]
    public string? ShippingAddress { get; set; }

    [MaxLength(200)]
    public string? TrackingNumber { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Relationships
    public List<DeliveryItem> Items { get; set; } = new();
}

/// <summary>
/// Represents the delivery item domain model.
/// </summary>
public class DeliveryItem : BaseEntity
{
    public Guid DeliveryId { get; set; }
    public Delivery Delivery { get; set; } = null!;

    public Guid SalesOrderItemId { get; set; }
    public SalesOrderItem SalesOrderItem { get; set; } = null!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    [Column(TypeName = "decimal(18,3)")]
    public decimal DeliveredQuantity { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

