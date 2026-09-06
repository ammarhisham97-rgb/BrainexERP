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
/// Represents the supplier domain model.
/// </summary>
public class Supplier : BaseEntity
{
    [Required, MaxLength(50)]
    public string SupplierCode { get; set; } = string.Empty;

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

    [MaxLength(500)]
    public string? PaymentTerms { get; set; }

    public bool IsActive { get; set; } = true;

    // Relationships
    public List<PurchaseOrder> PurchaseOrders { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
}

/// <summary>
/// Represents the purchase requisition domain model.
/// </summary>
public class PurchaseRequisition : BaseEntity
{
    [Required, MaxLength(50)]
    public string RequisitionNumber { get; set; } = string.Empty;

    public DateTime RequisitionDate { get; set; }

    public Guid RequestedBy { get; set; }

    [Required, MaxLength(100)]
    public string Department { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Purpose { get; set; }

    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }

    // Relationships
    public List<PurchaseRequisitionItem> Items { get; set; } = new();
}

/// <summary>
/// Represents the purchase requisition item domain model.
/// </summary>
public class PurchaseRequisitionItem : BaseEntity
{
    public Guid PurchaseRequisitionId { get; set; }
    public PurchaseRequisition PurchaseRequisition { get; set; } = null!;

    public Guid? ProductId { get; set; }
    public Product? Product { get; set; }

    [Required, MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,3)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimatedPrice { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// Represents the purchase order domain model.
/// </summary>
public class PurchaseOrder : BaseEntity
{
    [Required, MaxLength(50)]
    public string OrderNumber { get; set; } = string.Empty;

    public DateTime OrderDate { get; set; }
    public DateTime ExpectedDeliveryDate { get; set; }

    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;

    public Guid? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SubTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ShippingCost { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public Guid? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }

    // Relationships
    public List<PurchaseOrderItem> Items { get; set; } = new();
    public List<GoodsReceipt> GoodsReceipts { get; set; } = new();
}

/// <summary>
/// Represents the purchase order item domain model.
/// </summary>
public class PurchaseOrderItem : BaseEntity
{
    public Guid PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,3)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal ReceivedQuantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxRate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineTotal { get; set; }
}

/// <summary>
/// Represents the goods receipt domain model.
/// </summary>
public class GoodsReceipt : BaseEntity
{
    [Required, MaxLength(50)]
    public string ReceiptNumber { get; set; } = string.Empty;

    public DateTime ReceiptDate { get; set; }

    public Guid PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;

    public Guid? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    [MaxLength(200)]
    public string? ReceivedBy { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [MaxLength(200)]
    public string? DeliveryNoteNumber { get; set; }

    // Relationships
    public List<GoodsReceiptItem> Items { get; set; } = new();
}

/// <summary>
/// Represents the goods receipt item domain model.
/// </summary>
public class GoodsReceiptItem : BaseEntity
{
    public Guid GoodsReceiptId { get; set; }
    public GoodsReceipt GoodsReceipt { get; set; } = null!;

    public Guid PurchaseOrderItemId { get; set; }
    public PurchaseOrderItem PurchaseOrderItem { get; set; } = null!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    [Column(TypeName = "decimal(18,3)")]
    public decimal ReceivedQuantity { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal AcceptedQuantity { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal RejectedQuantity { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

