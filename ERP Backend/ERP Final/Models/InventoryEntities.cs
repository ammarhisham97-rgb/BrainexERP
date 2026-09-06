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
/// Represents the product domain model.
/// </summary>
public class Product : BaseEntity
{
    [Required, MaxLength(50)]
    public string ProductCode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }

    [MaxLength(50)]
    public string? SKU { get; set; }

    [MaxLength(50)]
    public string? Barcode { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CostPrice { get; set; }

    [MaxLength(50)]
    public string? Unit { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal ReorderLevel { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal ReorderQuantity { get; set; }

    public bool IsActive { get; set; } = true;
    public bool TrackInventory { get; set; } = true;

    // Relationships
    public List<Stock> Stocks { get; set; } = new();
    public List<StockMovement> StockMovements { get; set; } = new();
}

/// <summary>
/// Represents the category domain model.
/// </summary>
public class Category : BaseEntity
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public Guid? ParentCategoryId { get; set; }
    public Category? ParentCategory { get; set; }

    // Relationships
    public List<Category> SubCategories { get; set; } = new();
    public List<Product> Products { get; set; } = new();
}

/// <summary>
/// Represents the warehouse domain model.
/// </summary>
public class Warehouse : BaseEntity
{
    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(50)]
    public string? PostalCode { get; set; }

    [MaxLength(100)]
    public string? Country { get; set; }

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    public bool IsActive { get; set; } = true;

    // Relationships
    public List<Stock> Stocks { get; set; } = new();
}

/// <summary>
/// Represents the stock domain model.
/// </summary>
public class Stock : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    [Column(TypeName = "decimal(18,3)")]
    public decimal QuantityOnHand { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal QuantityReserved { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal QuantityAvailable { get; set; }

    [MaxLength(50)]
    public string? Location { get; set; }

    [MaxLength(50)]
    public string? BatchNumber { get; set; }

    [MaxLength(50)]
    public string? SerialNumber { get; set; }

    public DateTime? ExpiryDate { get; set; }
}

/// <summary>
/// Represents the stock movement domain model.
/// </summary>
public class StockMovement : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public Guid? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    [Required, MaxLength(50)]
    public string MovementNumber { get; set; } = string.Empty;

    public DateTime MovementDate { get; set; }

    public StockMovementType Type { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitCost { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCost { get; set; }

    [MaxLength(500)]
    public string? Reference { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public Guid? RelatedDocumentId { get; set; }
}

