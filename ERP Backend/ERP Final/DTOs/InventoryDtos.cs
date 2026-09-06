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
/// Represents the product dto data contract.
/// </summary>
public record ProductDto(Guid Id, string ProductCode, string Name, string? CategoryName, decimal UnitPrice, decimal QuantityOnHand, bool IsActive);
/// <summary>
/// Represents the create product request data contract.
/// </summary>
public record CreateProductRequest(string ProductCode, string Name, string? Description, Guid? CategoryId, string? SKU, decimal UnitPrice, decimal CostPrice, string? Unit, decimal ReorderLevel);
/// <summary>
/// Represents the category dto data contract.
/// </summary>
public record CategoryDto(Guid Id, string Name, string? Description);
/// <summary>
/// Represents the create category request data contract.
/// </summary>
public record CreateCategoryRequest(string Name, string? Description, Guid? ParentCategoryId);
/// <summary>
/// Represents the warehouse dto data contract.
/// </summary>
public record WarehouseDto(Guid Id, string Code, string Name, string? Address, bool IsActive);
/// <summary>
/// Represents the create warehouse request data contract.
/// </summary>
public record CreateWarehouseRequest(string Code, string Name, string? Address);
/// <summary>
/// Represents the stock dto data contract.
/// </summary>
public record StockDto(Guid Id, Guid ProductId, string ProductName, Guid WarehouseId, string WarehouseName, decimal QuantityOnHand, decimal QuantityAvailable);
/// <summary>
/// Represents the stock movement dto data contract.
/// </summary>
public record StockMovementDto(Guid Id, string MovementNumber, DateTime MovementDate, string ProductName, StockMovementType Type, decimal Quantity);
/// <summary>
/// Represents the stock adjustment dto data contract.
/// </summary>
public record StockAdjustmentDto(Guid Id, string MovementNumber, DateTime AdjustmentDate, Guid ProductId, string ProductCode, string ProductName, Guid WarehouseId, string WarehouseName, decimal QuantityChange, string Direction, string Reason, string? CreatedBy);
/// <summary>
/// Represents the create stock adjustment request data contract.
/// </summary>
public record CreateStockAdjustmentRequest(Guid ProductId, Guid WarehouseId, decimal Quantity, string Reason);
/// <summary>
/// Represents the stock transfer request data contract.
/// </summary>
public record StockTransferRequest(Guid ProductId, Guid FromWarehouseId, Guid ToWarehouseId, decimal Quantity);
/// <summary>
/// Represents the update product request data contract.
/// </summary>
public record UpdateProductRequest(string? Name, string? Description, Guid? CategoryId, decimal? UnitPrice, decimal? CostPrice, string? Unit, decimal? ReorderLevel, bool? IsActive);
/// <summary>
/// Represents the update category request data contract.
/// </summary>
public record UpdateCategoryRequest(string Name, string? Description);
/// <summary>
/// Represents the update warehouse request data contract.
/// </summary>
public record UpdateWarehouseRequest(string Name, string? Address, string? City, string? PostalCode, string? Country, string? PhoneNumber, bool IsActive);

