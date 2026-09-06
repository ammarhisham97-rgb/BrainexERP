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
/// Defines the i inventory service service contract.
/// </summary>
public interface IInventoryService
{
    Task<Result<ProductDto>> CreateProductAsync(CreateProductRequest request);
    Task<Result<List<ProductDto>>> GetAllProductsAsync();
    Task<Result<ProductDto>> GetProductByIdAsync(Guid id);
    Task<Result<CategoryDto>> CreateCategoryAsync(string name, string? description, Guid? parentCategoryId);
    Task<Result<List<CategoryDto>>> GetAllCategoriesAsync();
    Task<Result<WarehouseDto>> CreateWarehouseAsync(string code, string name, string? address);
    Task<Result<List<WarehouseDto>>> GetAllWarehousesAsync();
    Task<Result<StockDto>> GetStockAsync(Guid productId, Guid warehouseId);
    Task<Result<List<StockDto>>> GetAllStocksByWarehouseAsync(Guid warehouseId);
    Task<Result<List<StockDto>>> GetLowStockProductsAsync();
    Task<Result> AdjustStockAsync(Guid productId, Guid warehouseId, decimal quantity, string reason);
    Task<Result> TransferStockAsync(Guid productId, Guid fromWarehouseId, Guid toWarehouseId, decimal quantity);
    Task<Result<PagedResult<ProductDto>>> SearchProductsAsync(ProductSearchRequest search, PaginationRequest pagination);
    Task<Result<PagedResult<StockDto>>> SearchStockAsync(StockFilterRequest filter, PaginationRequest pagination);
    Task<Result<ProductDto>> UpdateProductAsync(Guid id, UpdateProductRequest request);
    Task<Result> DeactivateProductAsync(Guid id);
    Task<Result<CategoryDto>> GetCategoryByIdAsync(Guid id);
    Task<Result<CategoryDto>> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request);
    Task<Result> DeleteCategoryAsync(Guid id);
    Task<Result<WarehouseDto>> GetWarehouseByIdAsync(Guid id);
    Task<Result<WarehouseDto>> UpdateWarehouseAsync(Guid id, UpdateWarehouseRequest request);
    Task<Result> DeactivateWarehouseAsync(Guid id);
    Task<Result<List<StockMovementDto>>> GetStockMovementsAsync(Guid? productId = null, Guid? warehouseId = null, DateTime? startDate = null, DateTime? endDate = null);
}


