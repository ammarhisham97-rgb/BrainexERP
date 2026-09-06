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
/// Represents the inventory service domain model.
/// </summary>
public class InventoryService : IInventoryService
{
    private readonly ERPDbContext _context;

    public InventoryService(ERPDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ProductDto>> CreateProductAsync(CreateProductRequest request)
    {
        if (await _context.Products.AnyAsync(p => p.ProductCode == request.ProductCode))
        {
            return Result<ProductDto>.Failure("Product code already exists");
        }

        var product = new Product
        {
            ProductCode = request.ProductCode,
            Name = request.Name,
            Description = request.Description,
            CategoryId = request.CategoryId,
            SKU = request.SKU,
            UnitPrice = request.UnitPrice,
            CostPrice = request.CostPrice,
            Unit = request.Unit,
            ReorderLevel = request.ReorderLevel,
            IsActive = true,
            TrackInventory = true
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var category = request.CategoryId.HasValue ? await _context.Categories.FindAsync(request.CategoryId.Value) : null;
        var dto = new ProductDto(product.Id, product.ProductCode, product.Name, category?.Name, product.UnitPrice, 0, product.IsActive);

        return Result<ProductDto>.Success(dto);
    }
    public async Task<Result<PagedResult<ProductDto>>> SearchProductsAsync(
    ProductSearchRequest search,
    PaginationRequest pagination)
    {
        try
        {
            var query = _context.Products
                .Include(p => p.Category)
                .Include(p => p.Stocks)
                .Where(p => !p.IsDeleted)
                .AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(search.SearchTerm))
            {
                var searchLower = search.SearchTerm.ToLower();
                query = query.Where(p =>
                    p.Name.ToLower().Contains(searchLower) ||
                    p.ProductCode.ToLower().Contains(searchLower) ||
                    (p.SKU != null && p.SKU.ToLower().Contains(searchLower)) ||
                    (p.Description != null && p.Description.ToLower().Contains(searchLower)));
            }

            // Apply category filter
            if (search.CategoryId.HasValue)
            {
                query = query.Where(p => p.CategoryId == search.CategoryId.Value);
            }

            // Apply price filters
            if (search.MinPrice.HasValue)
            {
                query = query.Where(p => p.UnitPrice >= search.MinPrice.Value);
            }

            if (search.MaxPrice.HasValue)
            {
                query = query.Where(p => p.UnitPrice <= search.MaxPrice.Value);
            }

            // Apply stock filter
            if (search.InStock == true)
            {
                query = query.Where(p => p.Stocks.Any(s => s.QuantityOnHand > 0));
            }
            else if (search.InStock == false)
            {
                query = query.Where(p => !p.Stocks.Any(s => s.QuantityOnHand > 0));
            }

            // Apply active filter
            if (search.IsActive.HasValue)
            {
                query = query.Where(p => p.IsActive == search.IsActive.Value);
            }

            // Apply reorder level filter
            if (search.BelowReorderLevel.HasValue)
            {
                query = query.Where(p => p.Stocks.Sum(s => s.QuantityOnHand) <= p.ReorderLevel);
            }

            // Apply sorting
            query = query.ApplySorting(pagination.SortBy ?? "Name", pagination.SortDescending ?? false);

            // Get total count
            var totalCount = await query.CountAsync();

            // Apply pagination and project to DTO
            var products = await query
                .Skip(pagination.Skip)
                .Take(pagination.Take)
                .Select(p => new ProductDto(
                    p.Id,
                    p.ProductCode,
                    p.Name,
                    p.Category != null ? p.Category.Name : null,
                    p.UnitPrice,
                    p.Stocks.Sum(s => s.QuantityOnHand),
                    p.IsActive
                ))
                .ToListAsync();

            var pagedResult = new PagedResult<ProductDto>
            {
                Items = products,
                TotalCount = totalCount,
                PageNumber = pagination.Page,
                PageSize = pagination.PageSize
            };

            return Result<PagedResult<ProductDto>>.Success(pagedResult);
        }
        catch (Exception ex)
        {
            return Result<PagedResult<ProductDto>>.Failure($"Error searching products: {ex.Message}");
        }
    }
    public async Task<Result<PagedResult<StockDto>>> SearchStockAsync(
    StockFilterRequest filter,
    PaginationRequest pagination)
    {
        try
        {
            var query = _context.Stocks
                .Include(s => s.Product)
                .Include(s => s.Warehouse)
                .AsQueryable();

            // Apply warehouse filter
            if (filter.WarehouseId.HasValue)
            {
                query = query.Where(s => s.WarehouseId == filter.WarehouseId.Value);
            }

            // Apply product filter
            if (filter.ProductId.HasValue)
            {
                query = query.Where(s => s.ProductId == filter.ProductId.Value);
            }

            // Apply low stock filter
            if (filter.LowStock == true)
            {
                query = query.Where(s => s.QuantityOnHand <= s.Product.ReorderLevel);
            }

            // Apply quantity filters
            if (filter.MinQuantity.HasValue)
            {
                query = query.Where(s => s.QuantityOnHand >= filter.MinQuantity.Value);
            }

            if (filter.MaxQuantity.HasValue)
            {
                query = query.Where(s => s.QuantityOnHand <= filter.MaxQuantity.Value);
            }

            // Apply sorting
            query = query.ApplySorting(pagination.SortBy ?? "QuantityOnHand", pagination.SortDescending ?? false);

            // Get total count
            var totalCount = await query.CountAsync();

            // Apply pagination and project to DTO
            var stocks = await query
                .Skip(pagination.Skip)
                .Take(pagination.Take)
                .Select(s => new StockDto(
                    s.Id,
                    s.ProductId,
                    s.Product.Name,
                    s.WarehouseId,
                    s.Warehouse.Name,
                    s.QuantityOnHand,
                    s.QuantityAvailable
                ))
                .ToListAsync();

            var pagedResult = new PagedResult<StockDto>
            {
                Items = stocks,
                TotalCount = totalCount,
                PageNumber = pagination.Page,
                PageSize = pagination.PageSize
            };

            return Result<PagedResult<StockDto>>.Success(pagedResult);
        }
        catch (Exception ex)
        {
            return Result<PagedResult<StockDto>>.Failure($"Error searching stock: {ex.Message}");
        }
    }

    public async Task<Result<List<ProductDto>>> GetAllProductsAsync()
    {
        var products = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Stocks)
            .Where(p => p.IsActive)
            .ToListAsync();

        var dtos = products.Select(p => new ProductDto(
            p.Id,
            p.ProductCode,
            p.Name,
            p.Category?.Name,
            p.UnitPrice,
            p.Stocks.Sum(s => s.QuantityOnHand),
            p.IsActive
        )).ToList();

        return Result<List<ProductDto>>.Success(dtos);
    }

    public async Task<Result<ProductDto>> GetProductByIdAsync(Guid id)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Stocks)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            return Result<ProductDto>.Failure("Product not found");
        }

        var dto = new ProductDto(
            product.Id,
            product.ProductCode,
            product.Name,
            product.Category?.Name,
            product.UnitPrice,
            product.Stocks.Sum(s => s.QuantityOnHand),
            product.IsActive
        );

        return Result<ProductDto>.Success(dto);
    }

    public async Task<Result<CategoryDto>> CreateCategoryAsync(string name, string? description, Guid? parentCategoryId)
    {
        if (await _context.Categories.AnyAsync(c => c.Name == name))
        {
            return Result<CategoryDto>.Failure("Category name already exists");
        }

        var category = new Category
        {
            Name = name,
            Description = description,
            ParentCategoryId = parentCategoryId
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        var dto = new CategoryDto(category.Id, category.Name, category.Description);
        return Result<CategoryDto>.Success(dto);
    }

    public async Task<Result<List<CategoryDto>>> GetAllCategoriesAsync()
    {
        var categories = await _context.Categories.ToListAsync();
        var dtos = categories.Select(c => new CategoryDto(c.Id, c.Name, c.Description)).ToList();
        return Result<List<CategoryDto>>.Success(dtos);
    }

    public async Task<Result<WarehouseDto>> CreateWarehouseAsync(string code, string name, string? address)
    {
        if (await _context.Warehouses.AnyAsync(w => w.Code == code))
        {
            return Result<WarehouseDto>.Failure("Warehouse code already exists");
        }

        var warehouse = new Warehouse
        {
            Code = code,
            Name = name,
            Address = address,
            IsActive = true
        };

        _context.Warehouses.Add(warehouse);
        await _context.SaveChangesAsync();

        var dto = new WarehouseDto(warehouse.Id, warehouse.Code, warehouse.Name, warehouse.Address, warehouse.IsActive);
        return Result<WarehouseDto>.Success(dto);
    }

    public async Task<Result<List<WarehouseDto>>> GetAllWarehousesAsync()
    {
        var warehouses = await _context.Warehouses
            .Where(w => w.IsActive)
            .ToListAsync();

        var dtos = warehouses.Select(w => new WarehouseDto(
            w.Id, w.Code, w.Name, w.Address, w.IsActive
        )).ToList();

        return Result<List<WarehouseDto>>.Success(dtos);
    }

    public async Task<Result<StockDto>> GetStockAsync(Guid productId, Guid warehouseId)
    {
        var stock = await _context.Stocks
            .Include(s => s.Product)
            .Include(s => s.Warehouse)
            .FirstOrDefaultAsync(s => s.ProductId == productId && s.WarehouseId == warehouseId);

        if (stock == null)
        {
            return Result<StockDto>.Failure("Stock not found");
        }

        var dto = new StockDto(
            stock.Id,
            stock.ProductId,
            stock.Product.Name,
            stock.WarehouseId,
            stock.Warehouse.Name,
            stock.QuantityOnHand,
            stock.QuantityAvailable
        );

        return Result<StockDto>.Success(dto);
    }

    public async Task<Result<List<StockDto>>> GetAllStocksByWarehouseAsync(Guid warehouseId)
    {
        var stocks = await _context.Stocks
            .Include(s => s.Product)
            .Include(s => s.Warehouse)
            .Where(s => s.WarehouseId == warehouseId)
            .ToListAsync();

        var dtos = stocks.Select(s => new StockDto(
            s.Id,
            s.ProductId,
            s.Product.Name,
            s.WarehouseId,
            s.Warehouse.Name,
            s.QuantityOnHand,
            s.QuantityAvailable
        )).ToList();

        return Result<List<StockDto>>.Success(dtos);
    }

    public async Task<Result<List<StockDto>>> GetLowStockProductsAsync()
    {
        var stocks = await _context.Stocks
            .Include(s => s.Product)
            .Include(s => s.Warehouse)
            .Where(s => s.QuantityOnHand <= s.Product.ReorderLevel)
            .ToListAsync();

        var dtos = stocks.Select(s => new StockDto(
            s.Id,
            s.ProductId,
            s.Product.Name,
            s.WarehouseId,
            s.Warehouse.Name,
            s.QuantityOnHand,
            s.QuantityAvailable
        )).ToList();

        return Result<List<StockDto>>.Success(dtos);
    }

    public async Task<Result> AdjustStockAsync(Guid productId, Guid warehouseId, decimal quantity, string reason)
    {
        // ? NEW: Validate quantity is not zero
        if (quantity == 0)
        {
            return Result.Failure("Adjustment quantity cannot be zero");
        }

        // ? NEW: Validate reason is provided
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure("Adjustment reason is required");
        }

        var product = await _context.Products.FindAsync(productId);
        if (product == null)
        {
            return Result.Failure("Product not found");
        }

        // ? NEW: Check if product is active
        if (!product.IsActive)
        {
            return Result.Failure("Cannot adjust stock for inactive product");
        }

        var warehouse = await _context.Warehouses.FindAsync(warehouseId);
        if (warehouse == null)
        {
            return Result.Failure("Warehouse not found");
        }

        // ? NEW: Check if warehouse is active
        if (!warehouse.IsActive)
        {
            return Result.Failure("Cannot adjust stock in inactive warehouse");
        }

        var stock = await _context.Stocks
            .FirstOrDefaultAsync(s => s.ProductId == productId && s.WarehouseId == warehouseId);

        if (stock == null)
        {
            stock = new Stock
            {
                ProductId = productId,
                WarehouseId = warehouseId,
                QuantityOnHand = 0,
                QuantityReserved = 0,
                QuantityAvailable = 0
            };
            _context.Stocks.Add(stock);
        }

        // ? FIX #1: Validate negative adjustments won't create negative stock
        var newQuantityOnHand = stock.QuantityOnHand + quantity;

        if (newQuantityOnHand < 0)
        {
            return Result.Failure(
                $"Adjustment would result in negative stock. " +
                $"Current quantity: {stock.QuantityOnHand}, " +
                $"Adjustment: {quantity}, " +
                $"Result: {newQuantityOnHand}");
        }

        // ? FIX #2: Check if negative adjustment exceeds available (not just on-hand)
        if (quantity < 0)
        {
            var newQuantityAvailable = stock.QuantityAvailable + quantity;

            if (newQuantityAvailable < 0)
            {
                return Result.Failure(
                    $"Cannot reduce stock below reserved quantity. " +
                    $"Available quantity: {stock.QuantityAvailable}, " +
                    $"Reserved quantity: {stock.QuantityReserved}, " +
                    $"Adjustment: {quantity}");
            }
        }

        // ? NEW: Use transaction for data integrity
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // Apply adjustment
            stock.QuantityOnHand += quantity;
            stock.QuantityAvailable = stock.QuantityOnHand - stock.QuantityReserved;

            // ? NEW: Update timestamp
            stock.UpdatedAt = DateTime.UtcNow;

            // ? FIX #3: Record movement with proper direction indicator
            var movementType = quantity > 0
                ? StockMovementType.Adjustment
                : StockMovementType.Adjustment; // Could add AdjustmentIn/AdjustmentOut enum values

            var movement = new StockMovement
            {
                ProductId = productId,
                WarehouseId = warehouseId,
                MovementNumber = $"ADJ-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}",
                MovementDate = DateTime.UtcNow,
                Type = movementType,
                Quantity = Math.Abs(quantity), // ? Changed: Store absolute value
                UnitCost = product.CostPrice,
                TotalCost = Math.Abs(quantity) * product.CostPrice, // ? Changed: Absolute value
                Notes = reason,
                Reference = quantity > 0 ? "INCREASE" : "DECREASE" // ? NEW: Direction indicator
            };

            _context.StockMovements.Add(movement);

            // ? NEW: Update product cost if this is a positive adjustment (optional)
            if (quantity > 0 && product.CostPrice == 0)
            {
                // If product has no cost price set, you might want to calculate weighted average
                // This is optional based on your business logic
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Result.Success();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            // Log error: _logger.LogError(ex, "Error adjusting stock for product {ProductId}", productId);
            return Result.Failure("An error occurred while adjusting stock");
        }
    }
    public async Task<Result> TransferStockAsync(Guid productId, Guid fromWarehouseId, Guid toWarehouseId, decimal quantity)
    {
        // Validation checks
        if (fromWarehouseId == toWarehouseId)
        {
            return Result.Failure("Source and destination warehouses cannot be the same");
        }

        if (quantity <= 0)
        {
            return Result.Failure("Transfer quantity must be greater than zero");
        }

        // Verify product exists
        var product = await _context.Products.FindAsync(productId);
        if (product == null)
        {
            return Result.Failure("Product not found");
        }

        // Verify source warehouse exists and has stock
        var fromStock = await _context.Stocks
            .FirstOrDefaultAsync(s => s.ProductId == productId && s.WarehouseId == fromWarehouseId);

        if (fromStock == null)
        {
            return Result.Failure("Product not found in source warehouse");
        }

        if (fromStock.QuantityAvailable < quantity)
        {
            return Result.Failure($"Insufficient stock in source warehouse. Available: {fromStock.QuantityAvailable}, Requested: {quantity}");
        }

        // Get or create destination stock record
        var toStock = await _context.Stocks
            .FirstOrDefaultAsync(s => s.ProductId == productId && s.WarehouseId == toWarehouseId);

        if (toStock == null)
        {
            toStock = new Stock
            {
                ProductId = productId,
                WarehouseId = toWarehouseId,
                QuantityOnHand = 0,
                QuantityReserved = 0,
                QuantityAvailable = 0
            };
            _context.Stocks.Add(toStock);
        }

        // Use transaction to ensure atomicity
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // ? FIX #1: Update stock quantities correctly
            // Source warehouse: reduce both on-hand and available
            fromStock.QuantityOnHand -= quantity;
            fromStock.QuantityAvailable -= quantity; // ? Directly subtract transferred amount
                                                     // Note: Reserved stock remains untouched in source warehouse

            // Destination warehouse: increase both on-hand and available
            toStock.QuantityOnHand += quantity;
            toStock.QuantityAvailable += quantity; // ? Directly add transferred amount
                                                   // Note: Reserved stock remains untouched in destination warehouse

            // ? FIX #2: Validate final quantities are not negative
            if (fromStock.QuantityOnHand < 0 || fromStock.QuantityAvailable < 0)
            {
                await transaction.RollbackAsync();
                return Result.Failure("Transfer would result in negative stock in source warehouse");
            }

            // Generate unique transfer number
            var transferNumber = $"TRN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}";

            // ? FIX #3: Record movements with consistent positive quantities
            var outMovement = new StockMovement
            {
                ProductId = productId,
                WarehouseId = fromWarehouseId,
                MovementNumber = transferNumber,
                MovementDate = DateTime.UtcNow,
                Type = StockMovementType.Transfer,
                Quantity = quantity,  // ? Changed: Use positive quantity
                UnitCost = product.CostPrice,
                TotalCost = quantity * product.CostPrice,
                Notes = $"Transfer out to warehouse {toWarehouseId}",
                Reference = $"OUT-{transferNumber}" // ? Added: Better tracking
            };

            var inMovement = new StockMovement
            {
                ProductId = productId,
                WarehouseId = toWarehouseId,
                MovementNumber = transferNumber,
                MovementDate = DateTime.UtcNow,
                Type = StockMovementType.Transfer,
                Quantity = quantity, // ? Already positive
                UnitCost = product.CostPrice,
                TotalCost = quantity * product.CostPrice,
                Notes = $"Transfer in from warehouse {fromWarehouseId}",
                Reference = $"IN-{transferNumber}" // ? Added: Better tracking
            };

            _context.StockMovements.Add(outMovement);
            _context.StockMovements.Add(inMovement);

            // ? FIX #4: Update timestamps
            fromStock.UpdatedAt = DateTime.UtcNow;
            toStock.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Result.Success();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            // ? FIX #5: Log the error (add logger if available)
            // _logger?.LogError(ex, "Error transferring stock from {FromWarehouse} to {ToWarehouse}", 
            //     fromWarehouseId, toWarehouseId);
            return Result.Failure($"An error occurred during stock transfer: {ex.Message}");
        }
    }
    public async Task<Result<ProductDto>> UpdateProductAsync(Guid id, UpdateProductRequest request)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Stocks)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
            return Result<ProductDto>.Failure("Product not found");

        if (request.Name != null) product.Name = request.Name;
        if (request.Description != null) product.Description = request.Description;
        if (request.CategoryId.HasValue) product.CategoryId = request.CategoryId.Value;
        if (request.UnitPrice.HasValue) product.UnitPrice = request.UnitPrice.Value;
        if (request.CostPrice.HasValue) product.CostPrice = request.CostPrice.Value;
        if (request.Unit != null) product.Unit = request.Unit;
        if (request.ReorderLevel.HasValue) product.ReorderLevel = request.ReorderLevel.Value;
        if (request.IsActive.HasValue) product.IsActive = request.IsActive.Value;

        product.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var dto = new ProductDto(
            product.Id,
            product.ProductCode,
            product.Name,
            product.Category?.Name,
            product.UnitPrice,
            product.Stocks.Sum(s => s.QuantityOnHand),
            product.IsActive
        );

        return Result<ProductDto>.Success(dto);
    }

    public async Task<Result> DeactivateProductAsync(Guid id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null)
            return Result.Failure("Product not found");

        // ? ADD: Check for existing stock
        var hasStock = await _context.Stocks
            .AnyAsync(s => s.ProductId == id && s.QuantityOnHand > 0);

        if (hasStock)
            return Result.Failure("Cannot deactivate product with stock on hand");

        // ? ADD: Check for pending orders
        var hasPendingOrders = await _context.SalesOrderItems
            .Include(soi => soi.SalesOrder)
            .AnyAsync(soi => soi.ProductId == id &&
                            (soi.SalesOrder.Status == SalesOrderStatus.Draft ||
                             soi.SalesOrder.Status == SalesOrderStatus.Confirmed));

        if (hasPendingOrders)
            return Result.Failure("Cannot deactivate product with pending sales orders");

        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<CategoryDto>> GetCategoryByIdAsync(Guid id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null)
            return Result<CategoryDto>.Failure("Category not found");

        var dto = new CategoryDto(category.Id, category.Name, category.Description);
        return Result<CategoryDto>.Success(dto);
    }

    public async Task<Result<CategoryDto>> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null)
            return Result<CategoryDto>.Failure("Category not found");

        if (await _context.Categories.AnyAsync(c => c.Name == request.Name && c.Id != id))
            return Result<CategoryDto>.Failure("Category name already exists");

        category.Name = request.Name;
        category.Description = request.Description;
        category.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var dto = new CategoryDto(category.Id, category.Name, category.Description);
        return Result<CategoryDto>.Success(dto);
    }

    public async Task<Result> DeleteCategoryAsync(Guid id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null)
            return Result.Failure("Category not found");

        // Check if category has products
        var hasProducts = await _context.Products.AnyAsync(p => p.CategoryId == id);
        if (hasProducts)
            return Result.Failure("Cannot delete category with associated products");

        // Check if category has subcategories
        var hasSubCategories = await _context.Categories.AnyAsync(c => c.ParentCategoryId == id);
        if (hasSubCategories)
            return Result.Failure("Cannot delete category with subcategories");

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<WarehouseDto>> GetWarehouseByIdAsync(Guid id)
    {
        var warehouse = await _context.Warehouses.FindAsync(id);
        if (warehouse == null)
            return Result<WarehouseDto>.Failure("Warehouse not found");

        var dto = new WarehouseDto(warehouse.Id, warehouse.Code, warehouse.Name, warehouse.Address, warehouse.IsActive);
        return Result<WarehouseDto>.Success(dto);
    }

    public async Task<Result<WarehouseDto>> UpdateWarehouseAsync(Guid id, UpdateWarehouseRequest request)
    {
        var warehouse = await _context.Warehouses.FindAsync(id);
        if (warehouse == null)
            return Result<WarehouseDto>.Failure("Warehouse not found");

        warehouse.Name = request.Name;
        warehouse.Address = request.Address;
        warehouse.City = request.City;
        warehouse.PostalCode = request.PostalCode;
        warehouse.Country = request.Country;
        warehouse.PhoneNumber = request.PhoneNumber;
        warehouse.IsActive = request.IsActive;
        warehouse.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var dto = new WarehouseDto(warehouse.Id, warehouse.Code, warehouse.Name, warehouse.Address, warehouse.IsActive);
        return Result<WarehouseDto>.Success(dto);
    }

    public async Task<Result> DeactivateWarehouseAsync(Guid id)
    {
        var warehouse = await _context.Warehouses.FindAsync(id);
        if (warehouse == null)
            return Result.Failure("Warehouse not found");

        // Check if warehouse has stock
        var hasStock = await _context.Stocks.AnyAsync(s => s.WarehouseId == id && s.QuantityOnHand > 0);
        if (hasStock)
            return Result.Failure("Cannot deactivate warehouse with stock on hand");

        warehouse.IsActive = false;
        warehouse.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<List<StockMovementDto>>> GetStockMovementsAsync(Guid? productId = null, Guid? warehouseId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _context.StockMovements
            .Include(sm => sm.Product)
            .Include(sm => sm.Warehouse)
            .AsQueryable();

        if (productId.HasValue)
            query = query.Where(sm => sm.ProductId == productId.Value);

        if (warehouseId.HasValue)
            query = query.Where(sm => sm.WarehouseId == warehouseId.Value);

        if (startDate.HasValue)
            query = query.Where(sm => sm.MovementDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(sm => sm.MovementDate <= endDate.Value);

        var movements = await query.OrderByDescending(sm => sm.MovementDate).ToListAsync();

        var dtos = movements.Select(sm => new StockMovementDto(
            sm.Id,
            sm.MovementNumber,
            sm.MovementDate,
            sm.Product.Name,
            sm.Type,
            sm.Quantity
        )).ToList();

        return Result<List<StockMovementDto>>.Success(dtos);
    }
}

