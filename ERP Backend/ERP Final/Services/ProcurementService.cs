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
/// Represents the procurement service domain model.
/// </summary>
public class ProcurementService : IProcurementService
{
    private readonly ERPDbContext _context;

    public ProcurementService(ERPDbContext context)
    {
        _context = context;
    }
    public async Task<Result<PagedResult<PurchaseOrderDto>>> SearchPurchaseOrdersAsync(
    PurchaseOrderSearchRequest search,
    PaginationRequest pagination)
    {
        try
        {
            var query = _context.PurchaseOrders
                .Include(po => po.Supplier)
                .AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(search.SearchTerm))
            {
                var searchLower = search.SearchTerm.ToLower();
                query = query.Where(po =>
                    po.OrderNumber.ToLower().Contains(searchLower) ||
                    po.Supplier.Name.ToLower().Contains(searchLower));
            }

            // Apply supplier filter
            if (search.SupplierId.HasValue)
            {
                query = query.Where(po => po.SupplierId == search.SupplierId.Value);
            }

            // Apply status filter
            if (search.Status.HasValue)
            {
                query = query.Where(po => po.Status == search.Status.Value);
            }

            // Apply order date filters
            if (search.OrderDateFrom.HasValue)
            {
                query = query.Where(po => po.OrderDate >= search.OrderDateFrom.Value);
            }

            if (search.OrderDateTo.HasValue)
            {
                query = query.Where(po => po.OrderDate <= search.OrderDateTo.Value);
            }

            // Apply amount filters
            if (search.MinAmount.HasValue)
            {
                query = query.Where(po => po.TotalAmount >= search.MinAmount.Value);
            }

            if (search.MaxAmount.HasValue)
            {
                query = query.Where(po => po.TotalAmount <= search.MaxAmount.Value);
            }

            // Apply sorting
            query = query.ApplySorting(pagination.SortBy ?? "OrderDate", pagination.SortDescending ?? false);

            // Get total count
            var totalCount = await query.CountAsync();

            // Apply pagination and project to DTO
            var orders = await query
                .Skip(pagination.Skip)
                .Take(pagination.Take)
                .Select(po => new PurchaseOrderDto(
                    po.Id,
                    po.OrderNumber,
                    po.OrderDate,
                    po.Supplier.Name,
                    po.TotalAmount,
                    po.Status,
                    new List<PurchaseOrderItemDto>()
                ))
                .ToListAsync();

            var pagedResult = new PagedResult<PurchaseOrderDto>
            {
                Items = orders,
                TotalCount = totalCount,
                PageNumber = pagination.Page,
                PageSize = pagination.PageSize
            };

            return Result<PagedResult<PurchaseOrderDto>>.Success(pagedResult);
        }
        catch (Exception ex)
        {
            return Result<PagedResult<PurchaseOrderDto>>.Failure($"Error searching purchase orders: {ex.Message}");
        }
    }
    public async Task<Result<SupplierDto>> CreateSupplierAsync(CreateSupplierRequest request)
    {
        if (await _context.Suppliers.AnyAsync(s => s.SupplierCode == request.SupplierCode))
        {
            return Result<SupplierDto>.Failure("Supplier code already exists");
        }

        var supplier = new Supplier
        {
            SupplierCode = request.SupplierCode,
            Name = request.Name,
            ContactPerson = request.ContactPerson,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            Address = request.Address,
            PaymentTerms = request.PaymentTerms,
            IsActive = true
        };

        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync();

        var dto = new SupplierDto(supplier.Id, supplier.SupplierCode, supplier.Name,
            supplier.ContactPerson, supplier.Email, supplier.PhoneNumber, supplier.IsActive);

        return Result<SupplierDto>.Success(dto);
    }

    public async Task<Result<List<SupplierDto>>> GetAllSuppliersAsync()
    {
        var suppliers = await _context.Suppliers
            .Where(s => s.IsActive)
            .ToListAsync();

        var dtos = suppliers.Select(s => new SupplierDto(
            s.Id, s.SupplierCode, s.Name, s.ContactPerson, s.Email, s.PhoneNumber, s.IsActive
        )).ToList();

        return Result<List<SupplierDto>>.Success(dtos);
    }

    public async Task<Result<PurchaseOrderDto>> CreatePurchaseOrderAsync(CreatePurchaseOrderRequest request)
    {
        var supplier = await _context.Suppliers.FindAsync(request.SupplierId);
        if (supplier == null)
        {
            return Result<PurchaseOrderDto>.Failure("Supplier not found");
        }

        var orderNumber = $"PO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}";

        var purchaseOrder = new PurchaseOrder
        {
            OrderNumber = orderNumber,
            OrderDate = request.OrderDate,
            ExpectedDeliveryDate = request.ExpectedDeliveryDate,
            SupplierId = request.SupplierId,
            WarehouseId = request.WarehouseId,
            Notes = request.Notes,
            Status = PurchaseOrderStatus.Draft
        };

        decimal subTotal = 0;
        decimal taxAmount = 0;

        foreach (var item in request.Items)
        {
            var product = await _context.Products.FindAsync(item.ProductId);
            if (product == null)
            {
                return Result<PurchaseOrderDto>.Failure($"Product not found: {item.ProductId}");
            }

            var lineTotal = item.Quantity * item.UnitPrice;
            var lineTax = lineTotal * item.TaxRate / 100;

            var poItem = new PurchaseOrderItem
            {
                PurchaseOrderId = purchaseOrder.Id,
                ProductId = item.ProductId,
                Description = item.Description,
                Quantity = item.Quantity,
                ReceivedQuantity = 0,
                UnitPrice = item.UnitPrice,
                TaxRate = item.TaxRate,
                LineTotal = lineTotal + lineTax
            };

            purchaseOrder.Items.Add(poItem);
            subTotal += lineTotal;
            taxAmount += lineTax;
        }

        purchaseOrder.SubTotal = subTotal;
        purchaseOrder.TaxAmount = taxAmount;
        purchaseOrder.ShippingCost = 0;
        purchaseOrder.TotalAmount = subTotal + taxAmount;

        _context.PurchaseOrders.Add(purchaseOrder);
        await _context.SaveChangesAsync();

        var itemDtos = purchaseOrder.Items.Select(i => new PurchaseOrderItemDto(
            i.Id,
            i.ProductId,
            i.Product.ProductCode,
            i.Product.Name,
            i.Description,
            i.Quantity,
            i.ReceivedQuantity,
            i.UnitPrice,
            i.TaxRate,
            i.LineTotal
        )).ToList();

        var dto = new PurchaseOrderDto(purchaseOrder.Id, purchaseOrder.OrderNumber,
            purchaseOrder.OrderDate, supplier.Name, purchaseOrder.TotalAmount, purchaseOrder.Status, itemDtos);

        return Result<PurchaseOrderDto>.Success(dto);
    }

    public async Task<Result<List<PurchaseOrderDto>>> GetAllPurchaseOrdersAsync(PurchaseOrderStatus? status = null)
    {
        var query = _context.PurchaseOrders.Include(po => po.Supplier).AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(po => po.Status == status.Value);
        }

        var orders = await query.ToListAsync();

        var dtos = orders.Select(po => new PurchaseOrderDto(
            po.Id, po.OrderNumber, po.OrderDate, po.Supplier.Name, po.TotalAmount, po.Status, new List<PurchaseOrderItemDto>()
        )).ToList();

        return Result<List<PurchaseOrderDto>>.Success(dtos);
    }

    public async Task<Result> ApprovePurchaseOrderAsync(Guid orderId, Guid approvedBy)
    {
        var order = await _context.PurchaseOrders.FindAsync(orderId);
        if (order == null)
        {
            return Result.Failure("Purchase order not found");
        }

        if (order.Status != PurchaseOrderStatus.Draft && order.Status != PurchaseOrderStatus.Submitted)
        {
            return Result.Failure("Only draft or submitted orders can be approved");
        }

        order.Status = PurchaseOrderStatus.Approved;
        order.ApprovedBy = approvedBy;
        order.ApprovedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result<GoodsReceiptDto>> CreateGoodsReceiptAsync(Guid purchaseOrderId, Guid warehouseId, List<GoodsReceiptItemRequest> items, string? receivedBy)
    {
        // ? NEW: Validate input
        if (items == null || items.Count == 0)
        {
            return Result<GoodsReceiptDto>.Failure("At least one goods receipt item is required");
        }

        var purchaseOrder = await _context.PurchaseOrders
            .Include(po => po.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(po => po.Id == purchaseOrderId);

        if (purchaseOrder == null)
        {
            return Result<GoodsReceiptDto>.Failure("Purchase order not found");
        }

        // ? NEW: Check if deleted
        if (purchaseOrder.IsDeleted)
        {
            return Result<GoodsReceiptDto>.Failure("Cannot create goods receipt for deleted purchase order");
        }

        if (purchaseOrder.Status != PurchaseOrderStatus.Approved)
        {
            return Result<GoodsReceiptDto>.Failure("Purchase order must be approved before receiving goods");
        }

        var warehouse = await _context.Warehouses.FindAsync(warehouseId);
        if (warehouse == null)
        {
            return Result<GoodsReceiptDto>.Failure("Warehouse not found");
        }

        // ? NEW: Check if warehouse is active
        if (!warehouse.IsActive)
        {
            return Result<GoodsReceiptDto>.Failure("Cannot receive goods into inactive warehouse");
        }

        // ? NEW: Use transaction for data integrity
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var receiptNumber = $"GRN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}";

            var goodsReceipt = new GoodsReceipt
            {
                ReceiptNumber = receiptNumber,
                ReceiptDate = DateTime.UtcNow,
                PurchaseOrderId = purchaseOrderId,
                WarehouseId = warehouseId,
                ReceivedBy = receivedBy
            };

            foreach (var item in items)
            {
                var poItem = purchaseOrder.Items.FirstOrDefault(i => i.Id == item.PurchaseOrderItemId);
                if (poItem == null)
                {
                    await transaction.RollbackAsync();
                    return Result<GoodsReceiptDto>.Failure($"Purchase order item not found: {item.PurchaseOrderItemId}");
                }

                // ? NEW: Validate quantities
                if (item.ReceivedQuantity <= 0)
                {
                    await transaction.RollbackAsync();
                    return Result<GoodsReceiptDto>.Failure($"Received quantity must be greater than zero for item {item.PurchaseOrderItemId}");
                }

                if (item.AcceptedQuantity < 0 || item.RejectedQuantity < 0)
                {
                    await transaction.RollbackAsync();
                    return Result<GoodsReceiptDto>.Failure("Accepted and rejected quantities cannot be negative");
                }

                if (item.AcceptedQuantity + item.RejectedQuantity != item.ReceivedQuantity)
                {
                    await transaction.RollbackAsync();
                    return Result<GoodsReceiptDto>.Failure(
                        $"Accepted ({item.AcceptedQuantity}) + Rejected ({item.RejectedQuantity}) must equal Received ({item.ReceivedQuantity})");
                }

                // ? NEW: Check if receiving more than ordered
                var remainingQuantity = poItem.Quantity - poItem.ReceivedQuantity;
                if (item.AcceptedQuantity > remainingQuantity)
                {
                    await transaction.RollbackAsync();
                    return Result<GoodsReceiptDto>.Failure(
                        $"Cannot receive {item.AcceptedQuantity} units. Only {remainingQuantity} units remaining for item {item.PurchaseOrderItemId}");
                }

                var grItem = new GoodsReceiptItem
                {
                    GoodsReceiptId = goodsReceipt.Id,
                    PurchaseOrderItemId = item.PurchaseOrderItemId,
                    ProductId = item.ProductId,
                    ReceivedQuantity = item.ReceivedQuantity,
                    AcceptedQuantity = item.AcceptedQuantity,
                    RejectedQuantity = item.RejectedQuantity,
                    Notes = item.Notes
                };

                goodsReceipt.Items.Add(grItem);

                // Update purchase order item received quantity
                poItem.ReceivedQuantity += item.AcceptedQuantity;

                // Update stock
                var stock = await _context.Stocks
                    .FirstOrDefaultAsync(s => s.ProductId == item.ProductId && s.WarehouseId == warehouseId);

                if (stock == null)
                {
                    stock = new Stock
                    {
                        ProductId = item.ProductId,
                        WarehouseId = warehouseId,
                        QuantityOnHand = 0,
                        QuantityReserved = 0,
                        QuantityAvailable = 0
                    };
                    _context.Stocks.Add(stock);
                }

                stock.QuantityOnHand += item.AcceptedQuantity;
                stock.QuantityAvailable = stock.QuantityOnHand - stock.QuantityReserved;

                // ? NEW: Update stock timestamp
                stock.UpdatedAt = DateTime.UtcNow;

                // Record stock movement (only for accepted quantity)
                var movement = new StockMovement
                {
                    ProductId = item.ProductId,
                    WarehouseId = warehouseId,
                    MovementNumber = receiptNumber,
                    MovementDate = DateTime.UtcNow,
                    Type = StockMovementType.Purchase,
                    Quantity = item.AcceptedQuantity,
                    UnitCost = poItem.UnitPrice,
                    TotalCost = item.AcceptedQuantity * poItem.UnitPrice,
                    Reference = $"IN-{purchaseOrder.OrderNumber}", // ? NEW: Better tracking
                    Notes = $"Goods receipt {receiptNumber} for PO {purchaseOrder.OrderNumber}"
                };

                _context.StockMovements.Add(movement);
            }

            // Check if all items are fully received
            var allReceived = purchaseOrder.Items.All(i => i.ReceivedQuantity >= i.Quantity);
            if (allReceived)
            {
                purchaseOrder.Status = PurchaseOrderStatus.Received;
            }

            // ? NEW: Update purchase order timestamp
            purchaseOrder.UpdatedAt = DateTime.UtcNow;

            _context.GoodsReceipts.Add(goodsReceipt);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var dto = new GoodsReceiptDto(goodsReceipt.Id, goodsReceipt.ReceiptNumber,
                goodsReceipt.ReceiptDate, purchaseOrder.OrderNumber, goodsReceipt.ReceivedBy);

            return Result<GoodsReceiptDto>.Success(dto);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            // ? NEW: Log error (uncomment if you have logger)
            // _logger.LogError(ex, "Error creating goods receipt for PO {PurchaseOrderId}", purchaseOrderId);
            return Result<GoodsReceiptDto>.Failure($"An error occurred while creating goods receipt: {ex.Message}");
        }
    }

    public async Task<Result<List<GoodsReceiptDto>>> GetGoodsReceiptsAsync(Guid? purchaseOrderId = null)
    {
        var query = _context.GoodsReceipts.Include(gr => gr.PurchaseOrder).AsQueryable();

        if (purchaseOrderId.HasValue)
        {
            query = query.Where(gr => gr.PurchaseOrderId == purchaseOrderId.Value);
        }

        var receipts = await query.ToListAsync();

        var dtos = receipts.Select(gr => new GoodsReceiptDto(
            gr.Id, gr.ReceiptNumber, gr.ReceiptDate, gr.PurchaseOrder.OrderNumber, gr.ReceivedBy
        )).ToList();

        return Result<List<GoodsReceiptDto>>.Success(dtos);
    }
    public async Task<Result<SupplierDto>> GetSupplierByIdAsync(Guid id)
    {
        var supplier = await _context.Suppliers.FindAsync(id);
        if (supplier == null)
            return Result<SupplierDto>.Failure("Supplier not found");

        var dto = new SupplierDto(supplier.Id, supplier.SupplierCode, supplier.Name,
            supplier.ContactPerson, supplier.Email, supplier.PhoneNumber, supplier.IsActive);

        return Result<SupplierDto>.Success(dto);
    }

    public async Task<Result<SupplierDto>> UpdateSupplierAsync(Guid id, UpdateSupplierRequest request)
    {
        var supplier = await _context.Suppliers.FindAsync(id);
        if (supplier == null)
            return Result<SupplierDto>.Failure("Supplier not found");

        if (request.Name != null) supplier.Name = request.Name;
        if (request.ContactPerson != null) supplier.ContactPerson = request.ContactPerson;
        if (request.Email != null) supplier.Email = request.Email;
        if (request.PhoneNumber != null) supplier.PhoneNumber = request.PhoneNumber;
        if (request.Address != null) supplier.Address = request.Address;
        if (request.PaymentTerms != null) supplier.PaymentTerms = request.PaymentTerms;
        if (request.IsActive.HasValue) supplier.IsActive = request.IsActive.Value;

        supplier.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var dto = new SupplierDto(supplier.Id, supplier.SupplierCode, supplier.Name,
            supplier.ContactPerson, supplier.Email, supplier.PhoneNumber, supplier.IsActive);

        return Result<SupplierDto>.Success(dto);
    }

    public async Task<Result> DeactivateSupplierAsync(Guid id)
    {
        var supplier = await _context.Suppliers.FindAsync(id);
        if (supplier == null)
            return Result.Failure("Supplier not found");

        // Check for pending purchase orders
        var hasPendingOrders = await _context.PurchaseOrders
            .AnyAsync(po => po.SupplierId == id &&
                (po.Status == PurchaseOrderStatus.Draft || po.Status == PurchaseOrderStatus.Approved));

        if (hasPendingOrders)
            return Result.Failure("Cannot deactivate supplier with pending purchase orders");

        supplier.IsActive = false;
        supplier.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<PurchaseOrderDto>> GetPurchaseOrderByIdAsync(Guid id)
    {
        var order = await _context.PurchaseOrders
            .Include(po => po.Supplier)
            .Include(po => po.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(po => po.Id == id);

        if (order == null)
            return Result<PurchaseOrderDto>.Failure("Purchase order not found");

        var itemDtos = order.Items.Select(i => new PurchaseOrderItemDto(
            i.Id,
            i.ProductId,
            i.Product.ProductCode,
            i.Product.Name,
            i.Description,
            i.Quantity,
            i.ReceivedQuantity,
            i.UnitPrice,
            i.TaxRate,
            i.LineTotal
        )).ToList();

        var dto = new PurchaseOrderDto(order.Id, order.OrderNumber,
            order.OrderDate, order.Supplier.Name, order.TotalAmount, order.Status, itemDtos);

        return Result<PurchaseOrderDto>.Success(dto);
    }

    public async Task<Result<PurchaseOrderDto>> UpdatePurchaseOrderAsync(Guid id, UpdatePurchaseOrderRequest request)
    {
        var order = await _context.PurchaseOrders
            .Include(po => po.Supplier)
            .FirstOrDefaultAsync(po => po.Id == id);

        if (order == null)
            return Result<PurchaseOrderDto>.Failure("Purchase order not found");

        if (order.Status != PurchaseOrderStatus.Draft)
            return Result<PurchaseOrderDto>.Failure("Only draft purchase orders can be updated");

        if (request.ExpectedDeliveryDate.HasValue)
            order.ExpectedDeliveryDate = request.ExpectedDeliveryDate.Value;
        if (request.Notes != null)
            order.Notes = request.Notes;

        order.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var dto = new PurchaseOrderDto(order.Id, order.OrderNumber,
            order.OrderDate, order.Supplier.Name, order.TotalAmount, order.Status, new List<PurchaseOrderItemDto>());

        return Result<PurchaseOrderDto>.Success(dto);
    }

    public async Task<Result> DeletePurchaseOrderAsync(Guid id)
    {
        var order = await _context.PurchaseOrders.FindAsync(id);
        if (order == null)
            return Result.Failure("Purchase order not found");

        if (order.Status != PurchaseOrderStatus.Draft)
            return Result.Failure("Only draft purchase orders can be deleted");

        _context.PurchaseOrders.Remove(order);
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result> CancelPurchaseOrderAsync(Guid id)
    {
        var order = await _context.PurchaseOrders.FindAsync(id);
        if (order == null)
            return Result.Failure("Purchase order not found");

        if (order.Status == PurchaseOrderStatus.Received)
            return Result.Failure("Cannot cancel received purchase orders");

        if (order.Status == PurchaseOrderStatus.Cancelled)
            return Result.Failure("Purchase order is already cancelled");

        order.Status = PurchaseOrderStatus.Cancelled;
        order.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<GoodsReceiptDto>> GetGoodsReceiptByIdAsync(Guid id)
    {
        var receipt = await _context.GoodsReceipts
            .Include(gr => gr.PurchaseOrder)
            .FirstOrDefaultAsync(gr => gr.Id == id);

        if (receipt == null)
            return Result<GoodsReceiptDto>.Failure("Goods receipt not found");

        var dto = new GoodsReceiptDto(receipt.Id, receipt.ReceiptNumber,
            receipt.ReceiptDate, receipt.PurchaseOrder.OrderNumber, receipt.ReceivedBy);

        return Result<GoodsReceiptDto>.Success(dto);
    }

    public async Task<Result> DeleteGoodsReceiptAsync(Guid id)
    {
        var receipt = await _context.GoodsReceipts
            .Include(gr => gr.Items)
            .ThenInclude(gri => gri.PurchaseOrderItem)
            .FirstOrDefaultAsync(gr => gr.Id == id);

        if (receipt == null)
            return Result.Failure("Goods receipt not found");

        // Reverse stock movements
        var movements = await _context.StockMovements
            .Where(sm => sm.MovementNumber == receipt.ReceiptNumber)
            .ToListAsync();

        foreach (var movement in movements)
        {
            var stock = await _context.Stocks
                .FirstOrDefaultAsync(s => s.ProductId == movement.ProductId && s.WarehouseId == movement.WarehouseId);

            if (stock != null)
            {
                stock.QuantityOnHand -= movement.Quantity;
                stock.QuantityAvailable = stock.QuantityOnHand - stock.QuantityReserved;
            }
        }

        // Reverse purchase order item received quantities
        foreach (var item in receipt.Items)
        {
            item.PurchaseOrderItem.ReceivedQuantity -= item.AcceptedQuantity;
        }

        _context.StockMovements.RemoveRange(movements);
        _context.GoodsReceipts.Remove(receipt);
        await _context.SaveChangesAsync();

        return Result.Success();
    }
}

