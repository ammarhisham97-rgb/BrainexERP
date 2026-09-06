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
/// Represents the sales service domain model.
/// </summary>
public class SalesService : ISalesService
{
    private readonly ERPDbContext _context;

    public SalesService(ERPDbContext context)
    {
        _context = context;
    }
    private async Task CreateCOGSJournalEntry(DeliveryItem item, Product product, string deliveryNumber, ERPDbContext context)
    {
        var cogsAmount = item.DeliveredQuantity * product.CostPrice;

        // Find required accounts
        var cogsAccount = await context.Accounts
            .FirstOrDefaultAsync(a => a.AccountCode == "5000"); // COGS

        var inventoryAccount = await context.Accounts
            .FirstOrDefaultAsync(a => a.AccountCode == "1300"); // Inventory

        if (cogsAccount == null || inventoryAccount == null)
        {
            return;
        }

        var entry = new JournalEntry
        {
            EntryNumber = $"COGS-{deliveryNumber}",
            EntryDate = DateTime.UtcNow,
            Description = $"COGS for delivery {deliveryNumber} - {product.Name}",
            IsPosted = true,
            PostedAt = DateTime.UtcNow
        };

        entry.Lines.Add(new JournalEntryLine
        {
            JournalEntryId = entry.Id,
            AccountId = cogsAccount.Id,
            DebitAmount = cogsAmount,
            CreditAmount = 0,
            Description = $"COGS - {product.Name}"
        });

        entry.Lines.Add(new JournalEntryLine
        {
            JournalEntryId = entry.Id,
            AccountId = inventoryAccount.Id,
            DebitAmount = 0,
            CreditAmount = cogsAmount,
            Description = $"Reduce inventory - {product.Name}"
        });

        context.JournalEntries.Add(entry);

        cogsAccount.Balance += cogsAmount;
        inventoryAccount.Balance -= cogsAmount;
    }
    public async Task<Result<PagedResult<CustomerDto>>> SearchCustomersAsync(
    CustomerSearchRequest search,
    PaginationRequest pagination)
    {
        try
        {
            var query = _context.Customers.AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(search.SearchTerm))
            {
                var searchLower = search.SearchTerm.ToLower();
                query = query.Where(c =>
                    c.Name.ToLower().Contains(searchLower) ||
                    c.CustomerCode.ToLower().Contains(searchLower) ||
                    (c.Email != null && c.Email.ToLower().Contains(searchLower)) ||
                    (c.ContactPerson != null && c.ContactPerson.ToLower().Contains(searchLower)));
            }

            // Apply active filter
            if (search.IsActive.HasValue)
            {
                query = query.Where(c => c.IsActive == search.IsActive.Value);
            }

            // Apply credit limit filters
            if (search.MinCreditLimit.HasValue)
            {
                query = query.Where(c => c.CreditLimit >= search.MinCreditLimit.Value);
            }

            if (search.MaxCreditLimit.HasValue)
            {
                query = query.Where(c => c.CreditLimit <= search.MaxCreditLimit.Value);
            }

            // Apply location filters
            if (!string.IsNullOrWhiteSpace(search.City))
            {
                query = query.Where(c => c.City == search.City);
            }

            if (!string.IsNullOrWhiteSpace(search.Country))
            {
                query = query.Where(c => c.Country == search.Country);
            }

            // Apply sorting
            query = query.ApplySorting(pagination.SortBy ?? "Name", pagination.SortDescending ?? false);

            // Get total count
            var totalCount = await query.CountAsync();

            // Apply pagination and project to DTO
            var customers = await query
                .Skip(pagination.Skip)
                .Take(pagination.Take)
                .Select(c => new CustomerDto(
                    c.Id,
                    c.CustomerCode,
                    c.Name,
                    c.Email,
                    c.PhoneNumber,
                    c.CreditLimit,
                    c.IsActive
                ))
                .ToListAsync();

            var pagedResult = new PagedResult<CustomerDto>
            {
                Items = customers,
                TotalCount = totalCount,
                PageNumber = pagination.Page,
                PageSize = pagination.PageSize
            };

            return Result<PagedResult<CustomerDto>>.Success(pagedResult);
        }
        catch (Exception ex)
        {
            return Result<PagedResult<CustomerDto>>.Failure($"Error searching customers: {ex.Message}");
        }
    }

    public async Task<Result<PagedResult<SalesOrderDto>>> SearchSalesOrdersAsync(
        SalesOrderSearchRequest search,
        PaginationRequest pagination)
    {
        try
        {
            var query = _context.SalesOrders
                .Include(so => so.Customer)
                .AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(search.SearchTerm))
            {
                var searchLower = search.SearchTerm.ToLower();
                query = query.Where(so =>
                    so.OrderNumber.ToLower().Contains(searchLower) ||
                    so.Customer.Name.ToLower().Contains(searchLower));
            }

            // Apply customer filter
            if (search.CustomerId.HasValue)
            {
                query = query.Where(so => so.CustomerId == search.CustomerId.Value);
            }

            // Apply status filter
            if (search.Status.HasValue)
            {
                query = query.Where(so => so.Status == search.Status.Value);
            }

            // Apply date filters
            if (search.OrderDateFrom.HasValue)
            {
                query = query.Where(so => so.OrderDate >= search.OrderDateFrom.Value);
            }

            if (search.OrderDateTo.HasValue)
            {
                query = query.Where(so => so.OrderDate <= search.OrderDateTo.Value);
            }

            // Apply amount filters
            if (search.MinAmount.HasValue)
            {
                query = query.Where(so => so.TotalAmount >= search.MinAmount.Value);
            }

            if (search.MaxAmount.HasValue)
            {
                query = query.Where(so => so.TotalAmount <= search.MaxAmount.Value);
            }

            // Apply sorting
            query = query.ApplySorting(pagination.SortBy ?? "OrderDate", pagination.SortDescending ?? false);

            // Get total count
            var totalCount = await query.CountAsync();

            // Apply pagination and project to DTO
            var orders = await query
                .Skip(pagination.Skip)
                .Take(pagination.Take)
                .Select(so => new SalesOrderDto(
                    so.Id,
                    so.OrderNumber,
                    so.OrderDate,
                    so.Customer.Name,
                    so.TotalAmount,
                    so.Status,
                    new List<SalesOrderItemDto>() // Empty list for search results
                ))
                .ToListAsync();

            var pagedResult = new PagedResult<SalesOrderDto>
            {
                Items = orders,
                TotalCount = totalCount,
                PageNumber = pagination.Page,
                PageSize = pagination.PageSize
            };

            return Result<PagedResult<SalesOrderDto>>.Success(pagedResult);
        }
        catch (Exception ex)
        {
            return Result<PagedResult<SalesOrderDto>>.Failure($"Error searching sales orders: {ex.Message}");
        }
    }
    public async Task<Result<CustomerDto>> CreateCustomerAsync(CreateCustomerRequest request)
    {
        if (await _context.Customers.AnyAsync(c => c.CustomerCode == request.CustomerCode))
        {
            return Result<CustomerDto>.Failure("Customer code already exists");
        }

        var customer = new Customer
        {
            CustomerCode = request.CustomerCode,
            Name = request.Name,
            ContactPerson = request.ContactPerson,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            Address = request.Address,
            CreditLimit = request.CreditLimit,
            IsActive = true
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();

        var dto = new CustomerDto(customer.Id, customer.CustomerCode, customer.Name,
            customer.Email, customer.PhoneNumber, customer.CreditLimit, customer.IsActive);

        return Result<CustomerDto>.Success(dto);
    }

    public async Task<Result<List<CustomerDto>>> GetAllCustomersAsync()
    {
        var customers = await _context.Customers
            .Where(c => c.IsActive)
            .ToListAsync();

        var dtos = customers.Select(c => new CustomerDto(
            c.Id, c.CustomerCode, c.Name, c.Email, c.PhoneNumber, c.CreditLimit, c.IsActive
        )).ToList();

        return Result<List<CustomerDto>>.Success(dtos);
    }

    public async Task<Result<LeadDto>> CreateLeadAsync(string name, string? company, string? email, string? phoneNumber, string? source)
    {
        var lead = new Lead
        {
            Name = name,
            Company = company,
            Email = email,
            PhoneNumber = phoneNumber,
            Source = source,
            Status = "New"
        };

        _context.Leads.Add(lead);
        await _context.SaveChangesAsync();

        var dto = new LeadDto(lead.Id, lead.Name, lead.Company, lead.Email, lead.Status);
        return Result<LeadDto>.Success(dto);
    }

    public async Task<Result> ConvertLeadToCustomerAsync(Guid leadId, CreateCustomerRequest customerRequest)
    {
        var lead = await _context.Leads.FindAsync(leadId);
        if (lead == null)
        {
            return Result.Failure("Lead not found");
        }

        if (lead.CustomerId.HasValue)
        {
            return Result.Failure("Lead already converted to customer");
        }

        var customerResult = await CreateCustomerAsync(customerRequest);
        if (!customerResult.IsSuccess)
        {
            return Result.Failure(customerResult.ErrorMessage ?? "Failed to create customer");
        }

        lead.CustomerId = customerResult.Data!.Id;
        lead.Status = "Converted";
        lead.ConvertedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result<OpportunityDto>> CreateOpportunityAsync(Guid customerId, string name, decimal estimatedValue, DateTime expectedCloseDate)
    {
        var customer = await _context.Customers.FindAsync(customerId);
        if (customer == null)
        {
            return Result<OpportunityDto>.Failure("Customer not found");
        }

        var opportunity = new Opportunity
        {
            Name = name,
            CustomerId = customerId,
            EstimatedValue = estimatedValue,
            Probability = 50,
            ExpectedCloseDate = expectedCloseDate,
            Stage = "Prospecting"
        };

        _context.Opportunities.Add(opportunity);
        await _context.SaveChangesAsync();

        var dto = new OpportunityDto(opportunity.Id, opportunity.Name, customer.Name,
            opportunity.EstimatedValue, opportunity.Probability, opportunity.Stage);

        return Result<OpportunityDto>.Success(dto);
    }

    public async Task<Result<SalesOrderDto>> CreateSalesOrderAsync(CreateSalesOrderRequest request)
    {
        var customer = await _context.Customers.FindAsync(request.CustomerId);
        if (customer == null)
        {
            return Result<SalesOrderDto>.Failure("Customer not found");
        }

        var orderNumber = $"SO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}";

        var salesOrder = new SalesOrder
        {
            OrderNumber = orderNumber,
            OrderDate = request.OrderDate,
            ExpectedDeliveryDate = request.ExpectedDeliveryDate,
            CustomerId = request.CustomerId,
            ShippingAddress = request.ShippingAddress,
            Notes = request.Notes,
            Status = SalesOrderStatus.Draft
        };

        decimal subTotal = 0;
        decimal taxAmount = 0;

        foreach (var item in request.Items)
        {
            var product = await _context.Products.FindAsync(item.ProductId);
            if (product == null)
            {
                return Result<SalesOrderDto>.Failure($"Product not found: {item.ProductId}");
            }

            var lineTotal = item.Quantity * item.UnitPrice;
            var lineTax = lineTotal * item.TaxRate / 100;

            var soItem = new SalesOrderItem
            {
                SalesOrderId = salesOrder.Id,
                ProductId = item.ProductId,
                Description = item.Description,
                Quantity = item.Quantity,
                DeliveredQuantity = 0,
                UnitPrice = item.UnitPrice,
                TaxRate = item.TaxRate,
                LineTotal = lineTotal + lineTax
            };

            salesOrder.Items.Add(soItem);
            subTotal += lineTotal;
            taxAmount += lineTax;
        }

        salesOrder.SubTotal = subTotal;
        salesOrder.TaxAmount = taxAmount;
        salesOrder.ShippingCost = 0;
        salesOrder.TotalAmount = subTotal + taxAmount;

        _context.SalesOrders.Add(salesOrder);
        await _context.SaveChangesAsync();

        var itemDtos = salesOrder.Items.Select(i => new SalesOrderItemDto(
            i.Id,
            i.ProductId,
            i.Product.Name,
            i.Description,
            i.Quantity,
            i.UnitPrice,
            i.TaxRate
        )).ToList();

        var dto = new SalesOrderDto(salesOrder.Id, salesOrder.OrderNumber, salesOrder.OrderDate,
            customer.Name, salesOrder.TotalAmount, salesOrder.Status, itemDtos);

        return Result<SalesOrderDto>.Success(dto);
    }

    public async Task<Result<List<SalesOrderDto>>> GetAllSalesOrdersAsync(SalesOrderStatus? status = null)
    {
        var query = _context.SalesOrders
            .Include(so => so.Customer)
            .Include(so => so.Items)
                .ThenInclude(i => i.Product)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(so => so.Status == status.Value);
        }

        var orders = await query.ToListAsync();

        var dtos = orders.Select(so => new SalesOrderDto(
            so.Id, so.OrderNumber, so.OrderDate, so.Customer.Name, so.TotalAmount, so.Status,
            so.Items.Select(i => new SalesOrderItemDto(
                i.Id,
                i.ProductId,
                i.Product.Name,
                i.Description,
                i.Quantity,
                i.UnitPrice,
                i.TaxRate
            )).ToList()
        )).ToList();

        return Result<List<SalesOrderDto>>.Success(dtos);
    }

    public async Task<Result> ConfirmSalesOrderAsync(Guid orderId)
    {
        var order = await _context.SalesOrders
            .Include(so => so.Items)
            .ThenInclude(i => i.Product)
            .Include(so => so.Customer)
            .FirstOrDefaultAsync(so => so.Id == orderId);

        if (order == null)
        {
            return Result.Failure("Sales order not found");
        }

        if (order.IsDeleted)
        {
            return Result.Failure("Cannot confirm deleted sales order");
        }

        if (order.Status != SalesOrderStatus.Draft)
        {
            return Result.Failure("Only draft orders can be confirmed");
        }

        // ? FEATURE 2: Check customer credit limit
         var customer = order.Customer ?? await _context.Customers.FindAsync(order.CustomerId);
         if (customer != null && customer.CreditLimit > 0)
         {
             var outstandingBalance = await _context.Invoices
                 .Where(i => i.CustomerId == customer.Id &&
                            i.Status != InvoiceStatus.Paid &&
                            i.Status != InvoiceStatus.Cancelled)
                 .SumAsync(i => i.BalanceAmount);

             if (outstandingBalance + order.TotalAmount > customer.CreditLimit)
             {
                 return Result.Failure(
                     $"Order exceeds customer credit limit. " +
                     $"Credit Limit: {customer.CreditLimit:C}, " +
                     $"Outstanding: {outstandingBalance:C}, " +
                     $"This Order: {order.TotalAmount:C}, " +
                     $"Total Would Be: {(outstandingBalance + order.TotalAmount):C}");
             }
         }

        // ? Use transaction for data integrity
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // ? FEATURE 1: Check stock availability AND reserve stock
            foreach (var item in order.Items)
            {
                // Sum total available stock across all warehouses
                var stocks = await _context.Stocks
                    .Where(s => s.ProductId == item.ProductId)
                    .ToListAsync();

                var totalAvailable = stocks.Sum(s => s.QuantityAvailable);

                if (totalAvailable < item.Quantity)
                {
                    await transaction.RollbackAsync();
                    return Result.Failure(
                        $"Insufficient stock for product '{item.Product.Name}'. " +
                        $"Available: {totalAvailable}, Required: {item.Quantity}");
                }

                // ? Reserve stock (FIFO - First In First Out by warehouse)
                var remainingToReserve = item.Quantity;

                foreach (var stock in stocks.OrderBy(s => s.CreatedAt))
                {
                    if (remainingToReserve <= 0) break;

                    var canReserve = Math.Min(stock.QuantityAvailable, remainingToReserve);

                    if (canReserve > 0)
                    {
                        stock.QuantityReserved += canReserve;
                        stock.QuantityAvailable = stock.QuantityOnHand - stock.QuantityReserved;
                        stock.UpdatedAt = DateTime.UtcNow;

                        remainingToReserve -= canReserve;
                    }
                }

                if (remainingToReserve > 0)
                {
                    await transaction.RollbackAsync();
                    return Result.Failure($"Failed to reserve stock for product '{item.Product.Name}'");
                }
            }

            order.Status = SalesOrderStatus.Confirmed;
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Result.Success();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return Result.Failure($"Error confirming sales order: {ex.Message}");
        }
    }

    public async Task<Result<DeliveryDto>> CreateDeliveryAsync(Guid salesOrderId, Guid warehouseId, List<DeliveryItemRequest> items)
    {
        // ? NEW: Validate input parameters
        if (items == null || items.Count == 0)
        {
            return Result<DeliveryDto>.Failure("At least one delivery item is required");
        }

        var salesOrder = await _context.SalesOrders
            .Include(so => so.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(so => so.Id == salesOrderId);

        if (salesOrder == null)
        {
            return Result<DeliveryDto>.Failure("Sales order not found");
        }

        // ? NEW: Check if deleted
        if (salesOrder.IsDeleted)
        {
            return Result<DeliveryDto>.Failure("Cannot create delivery for deleted sales order");
        }

        if (salesOrder.Status != SalesOrderStatus.Confirmed)
        {
            return Result<DeliveryDto>.Failure("Sales order must be confirmed before delivery");
        }

        var warehouse = await _context.Warehouses.FindAsync(warehouseId);
        if (warehouse == null)
        {
            return Result<DeliveryDto>.Failure("Warehouse not found");
        }

        // ? NEW: Check if warehouse is active
        if (!warehouse.IsActive)
        {
            return Result<DeliveryDto>.Failure("Cannot create delivery from inactive warehouse");
        }

        // ? NEW: Use transaction for data integrity
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var deliveryNumber = $"DEL-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}";

            var delivery = new Delivery
            {
                DeliveryNumber = deliveryNumber,
                DeliveryDate = DateTime.UtcNow,
                SalesOrderId = salesOrderId,
                WarehouseId = warehouseId,
                ShippingAddress = salesOrder.ShippingAddress
            };

            foreach (var item in items)
            {
                var soItem = salesOrder.Items.FirstOrDefault(i => i.Id == item.SalesOrderItemId);
                if (soItem == null)
                {
                    await transaction.RollbackAsync();
                    return Result<DeliveryDto>.Failure($"Sales order item not found: {item.SalesOrderItemId}");
                }

                // ? NEW: Validate delivery quantity
                if (item.DeliveredQuantity <= 0)
                {
                    await transaction.RollbackAsync();
                    return Result<DeliveryDto>.Failure($"Delivery quantity must be greater than zero for item {item.SalesOrderItemId}");
                }

                // ? NEW: Check if delivering more than ordered
                var remainingQuantity = soItem.Quantity - soItem.DeliveredQuantity;
                if (item.DeliveredQuantity > remainingQuantity)
                {
                    await transaction.RollbackAsync();
                    return Result<DeliveryDto>.Failure(
                        $"Cannot deliver {item.DeliveredQuantity} units. Only {remainingQuantity} units remaining for item {item.SalesOrderItemId}");
                }

                // Check stock
                var stock = await _context.Stocks
                    .FirstOrDefaultAsync(s => s.ProductId == item.ProductId && s.WarehouseId == warehouseId);

                if (stock == null || stock.QuantityAvailable < item.DeliveredQuantity)
                {
                    await transaction.RollbackAsync();
                    return Result<DeliveryDto>.Failure(
                        $"Insufficient stock for product {item.ProductId}. Available: {stock?.QuantityAvailable ?? 0}, Required: {item.DeliveredQuantity}");
                }

                var delItem = new DeliveryItem
                {
                    DeliveryId = delivery.Id,
                    SalesOrderItemId = item.SalesOrderItemId,
                    ProductId = item.ProductId,
                    DeliveredQuantity = item.DeliveredQuantity
                };

                delivery.Items.Add(delItem);

                // Update sales order item delivered quantity
                soItem.DeliveredQuantity += item.DeliveredQuantity;

                // Update stock
                // ? FEATURE 3: Clear reservation and reduce stock
                stock.QuantityReserved -= item.DeliveredQuantity; // Unreserve
                stock.QuantityOnHand -= item.DeliveredQuantity;   // Reduce actual stock
                stock.QuantityAvailable = stock.QuantityOnHand - stock.QuantityReserved;

                // ? Validate stock doesn't go negative
                if (stock.QuantityReserved < 0 || stock.QuantityOnHand < 0)
                {
                    await transaction.RollbackAsync();
                    return Result<DeliveryDto>.Failure(
                        $"Stock integrity error for product {item.ProductId}. " +
                        $"Reserved: {stock.QuantityReserved + item.DeliveredQuantity}, " +
                        $"OnHand: {stock.QuantityOnHand + item.DeliveredQuantity}");
                }

                // ? FIX: Use positive quantity with direction indicator in reference
                var movement = new StockMovement
                {
                    ProductId = item.ProductId,
                    WarehouseId = warehouseId,
                    MovementNumber = deliveryNumber,
                    MovementDate = DateTime.UtcNow,
                    Type = StockMovementType.Sale,
                    Quantity = item.DeliveredQuantity, // ? FIX: Positive quantity
                    UnitCost = soItem.Product.CostPrice,
                    TotalCost = item.DeliveredQuantity * soItem.Product.CostPrice, // ? FIX: Positive cost
                    Reference = $"OUT-{salesOrder.OrderNumber}", // ? NEW: Better tracking
                    Notes = $"Delivery {deliveryNumber} for sales order {salesOrder.OrderNumber}"
                };

                _context.StockMovements.Add(movement);

                // ? NEW: Update stock timestamp
                stock.UpdatedAt = DateTime.UtcNow;

                // ? CREATE COGS JOURNAL ENTRY
                await CreateCOGSJournalEntry(delItem, soItem.Product, deliveryNumber, _context);
            }

            // Check if all items are fully delivered
            var allDelivered = salesOrder.Items.All(i => i.DeliveredQuantity >= i.Quantity);
            if (allDelivered)
            {
                salesOrder.Status = SalesOrderStatus.Delivered;
            }
            else
            {
                salesOrder.Status = SalesOrderStatus.Shipped;
            }

            // ? NEW: Update sales order timestamp
            salesOrder.UpdatedAt = DateTime.UtcNow;

            _context.Deliveries.Add(delivery);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var dto = new DeliveryDto(delivery.Id, delivery.DeliveryNumber, delivery.DeliveryDate, salesOrder.OrderNumber);
            return Result<DeliveryDto>.Success(dto);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            // ? NEW: Log error (uncomment if you have logger)
            // _logger.LogError(ex, "Error creating delivery for sales order {SalesOrderId}", salesOrderId);
            return Result<DeliveryDto>.Failure($"An error occurred while creating delivery: {ex.Message}");
        }
    }
    public async Task<Result<CustomerDto>> GetCustomerByIdAsync(Guid id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
            return Result<CustomerDto>.Failure("Customer not found");

        var dto = new CustomerDto(customer.Id, customer.CustomerCode, customer.Name,
            customer.Email, customer.PhoneNumber, customer.CreditLimit, customer.IsActive);

        return Result<CustomerDto>.Success(dto);
    }

    public async Task<Result<CustomerDto>> UpdateCustomerAsync(Guid id, UpdateCustomerRequest request)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
            return Result<CustomerDto>.Failure("Customer not found");

        if (request.Name != null) customer.Name = request.Name;
        if (request.ContactPerson != null) customer.ContactPerson = request.ContactPerson;
        if (request.Email != null) customer.Email = request.Email;
        if (request.PhoneNumber != null) customer.PhoneNumber = request.PhoneNumber;
        if (request.Address != null) customer.Address = request.Address;
        if (request.CreditLimit.HasValue) customer.CreditLimit = request.CreditLimit.Value;
        if (request.IsActive.HasValue) customer.IsActive = request.IsActive.Value;

        customer.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var dto = new CustomerDto(customer.Id, customer.CustomerCode, customer.Name,
            customer.Email, customer.PhoneNumber, customer.CreditLimit, customer.IsActive);

        return Result<CustomerDto>.Success(dto);
    }

    public async Task<Result> DeactivateCustomerAsync(Guid id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
            return Result.Failure("Customer not found");

        // Check for pending orders
        var hasPendingOrders = await _context.SalesOrders
            .AnyAsync(so => so.CustomerId == id &&
                (so.Status == SalesOrderStatus.Draft || so.Status == SalesOrderStatus.Confirmed));

        if (hasPendingOrders)
            return Result.Failure("Cannot deactivate customer with pending sales orders");

        customer.IsActive = false;
        customer.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<LeadDto>> GetLeadByIdAsync(Guid id)
    {
        var lead = await _context.Leads.FindAsync(id);
        if (lead == null)
            return Result<LeadDto>.Failure("Lead not found");

        var dto = new LeadDto(lead.Id, lead.Name, lead.Company, lead.Email, lead.Status);
        return Result<LeadDto>.Success(dto);
    }

    public async Task<Result<List<LeadDto>>> GetAllLeadsAsync(string? status = null)
    {
        var query = _context.Leads.AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(l => l.Status == status);

        var leads = await query.OrderByDescending(l => l.CreatedAt).ToListAsync();

        var dtos = leads.Select(l => new LeadDto(
            l.Id, l.Name, l.Company, l.Email, l.Status
        )).ToList();

        return Result<List<LeadDto>>.Success(dtos);
    }

    public async Task<Result<LeadDto>> UpdateLeadAsync(Guid id, UpdateLeadRequest request)
    {
        var lead = await _context.Leads.FindAsync(id);
        if (lead == null)
            return Result<LeadDto>.Failure("Lead not found");

        if (request.Name != null) lead.Name = request.Name;
        if (request.Company != null) lead.Company = request.Company;
        if (request.Email != null) lead.Email = request.Email;
        if (request.PhoneNumber != null) lead.PhoneNumber = request.PhoneNumber;
        if (request.Status != null) lead.Status = request.Status;
        if (request.Notes != null) lead.Notes = request.Notes;

        lead.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var dto = new LeadDto(lead.Id, lead.Name, lead.Company, lead.Email, lead.Status);
        return Result<LeadDto>.Success(dto);
    }

    public async Task<Result> DeleteLeadAsync(Guid id)
    {
        var lead = await _context.Leads.FindAsync(id);
        if (lead == null)
            return Result.Failure("Lead not found");

        if (lead.CustomerId.HasValue)
            return Result.Failure("Cannot delete converted leads");

        _context.Leads.Remove(lead);
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<OpportunityDto>> GetOpportunityByIdAsync(Guid id)
    {
        var opportunity = await _context.Opportunities
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (opportunity == null)
            return Result<OpportunityDto>.Failure("Opportunity not found");

        var dto = new OpportunityDto(opportunity.Id, opportunity.Name, opportunity.Customer.Name,
            opportunity.EstimatedValue, opportunity.Probability, opportunity.Stage);

        return Result<OpportunityDto>.Success(dto);
    }

    public async Task<Result<List<OpportunityDto>>> GetAllOpportunitiesAsync(Guid? customerId = null, string? stage = null)
    {
        var query = _context.Opportunities
            .Include(o => o.Customer)
            .AsQueryable();

        if (customerId.HasValue)
            query = query.Where(o => o.CustomerId == customerId.Value);

        if (!string.IsNullOrWhiteSpace(stage))
            query = query.Where(o => o.Stage == stage);

        var opportunities = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();

        var dtos = opportunities.Select(o => new OpportunityDto(
            o.Id, o.Name, o.Customer.Name, o.EstimatedValue, o.Probability, o.Stage
        )).ToList();

        return Result<List<OpportunityDto>>.Success(dtos);
    }

    public async Task<Result<OpportunityDto>> UpdateOpportunityAsync(Guid id, UpdateOpportunityRequest request)
    {
        var opportunity = await _context.Opportunities
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (opportunity == null)
            return Result<OpportunityDto>.Failure("Opportunity not found");

        if (request.Name != null) opportunity.Name = request.Name;
        if (request.EstimatedValue.HasValue) opportunity.EstimatedValue = request.EstimatedValue.Value;
        if (request.Probability.HasValue) opportunity.Probability = request.Probability.Value;
        if (request.ExpectedCloseDate.HasValue) opportunity.ExpectedCloseDate = request.ExpectedCloseDate.Value;
        if (request.Stage != null) opportunity.Stage = request.Stage;
        if (request.Description != null) opportunity.Description = request.Description;

        opportunity.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var dto = new OpportunityDto(opportunity.Id, opportunity.Name, opportunity.Customer.Name,
            opportunity.EstimatedValue, opportunity.Probability, opportunity.Stage);

        return Result<OpportunityDto>.Success(dto);
    }

    public async Task<Result> DeleteOpportunityAsync(Guid id)
    {
        var opportunity = await _context.Opportunities.FindAsync(id);
        if (opportunity == null)
            return Result.Failure("Opportunity not found");

        if (opportunity.IsWon)
            return Result.Failure("Cannot delete won opportunities");

        _context.Opportunities.Remove(opportunity);
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<SalesOrderDto>> GetSalesOrderByIdAsync(Guid id)
    {
        var order = await _context.SalesOrders
            .Include(so => so.Customer)
            .Include(so => so.Items)              // Add this
                .ThenInclude(i => i.Product)      // Add this
            .FirstOrDefaultAsync(so => so.Id == id);

        if (order == null)
            return Result<SalesOrderDto>.Failure("Sales order not found");

        var dto = new SalesOrderDto(
            order.Id,
            order.OrderNumber,
            order.OrderDate,
            order.Customer.Name,
            order.TotalAmount,
            order.Status,
            order.Items.Select(i => new SalesOrderItemDto(    // Add this mapping
                i.Id,
                i.ProductId,
                i.Product.Name,
                i.Description,
                i.Quantity,
                i.UnitPrice,
                i.TaxRate
            )).ToList()
        );

        return Result<SalesOrderDto>.Success(dto);
    }

    public async Task<Result<SalesOrderDto>> UpdateSalesOrderAsync(Guid id, UpdateSalesOrderRequest request)
    {
        var order = await _context.SalesOrders
            .Include(so => so.Customer)
            .FirstOrDefaultAsync(so => so.Id == id);

        if (order == null)
            return Result<SalesOrderDto>.Failure("Sales order not found");

        if (order.Status != SalesOrderStatus.Draft)
            return Result<SalesOrderDto>.Failure("Only draft sales orders can be updated");

        if (request.ExpectedDeliveryDate.HasValue)
            order.ExpectedDeliveryDate = request.ExpectedDeliveryDate.Value;
        if (request.ShippingAddress != null)
            order.ShippingAddress = request.ShippingAddress;
        if (request.Notes != null)
            order.Notes = request.Notes;

        order.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var dto = new SalesOrderDto(order.Id, order.OrderNumber, order.OrderDate,
            order.Customer.Name, order.TotalAmount, order.Status,
            new List<SalesOrderItemDto>() // Empty list, items not needed for update response
        );

        return Result<SalesOrderDto>.Success(dto);
    }

    public async Task<Result> DeleteSalesOrderAsync(Guid id)
    {
        var order = await _context.SalesOrders.FindAsync(id);
        if (order == null)
            return Result.Failure("Sales order not found");

        if (order.Status != SalesOrderStatus.Draft)
            return Result.Failure("Only draft sales orders can be deleted");

        _context.SalesOrders.Remove(order);
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result> CancelSalesOrderAsync(Guid id)
    {
        var order = await _context.SalesOrders
            .Include(so => so.Items)
            .FirstOrDefaultAsync(so => so.Id == id);

        if (order == null)
            return Result.Failure("Sales order not found");

        if (order.Status == SalesOrderStatus.Delivered)
            return Result.Failure("Cannot cancel delivered sales orders");

        if (order.Status == SalesOrderStatus.Cancelled)
            return Result.Failure("Sales order is already cancelled");

        // ? FEATURE 3: Release reserved stock for confirmed orders
        if (order.Status == SalesOrderStatus.Confirmed || order.Status == SalesOrderStatus.Shipped)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                foreach (var item in order.Items)
                {
                    // Calculate how much is still reserved (not yet delivered)
                    var remainingReserved = item.Quantity - item.DeliveredQuantity;

                    if (remainingReserved > 0)
                    {
                        // Release reserved stock (FIFO)
                        var stocks = await _context.Stocks
                            .Where(s => s.ProductId == item.ProductId && s.QuantityReserved > 0)
                            .OrderBy(s => s.CreatedAt)
                            .ToListAsync();

                        var remainingToRelease = remainingReserved;

                        foreach (var stock in stocks)
                        {
                            if (remainingToRelease <= 0) break;

                            var canRelease = Math.Min(stock.QuantityReserved, remainingToRelease);

                            if (canRelease > 0)
                            {
                                stock.QuantityReserved -= canRelease;
                                stock.QuantityAvailable = stock.QuantityOnHand - stock.QuantityReserved;
                                stock.UpdatedAt = DateTime.UtcNow;

                                remainingToRelease -= canRelease;
                            }
                        }
                    }
                }

                order.Status = SalesOrderStatus.Cancelled;
                order.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Result.Success();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Result.Failure($"Error cancelling sales order: {ex.Message}");
            }
        }
        else
        {
            // For draft orders, just cancel without stock changes
            order.Status = SalesOrderStatus.Cancelled;
            order.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Result.Success();
        }
    }

    public async Task<Result<DeliveryDto>> GetDeliveryByIdAsync(Guid id)
    {
        var delivery = await _context.Deliveries
            .Include(d => d.SalesOrder)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (delivery == null)
            return Result<DeliveryDto>.Failure("Delivery not found");

        var dto = new DeliveryDto(delivery.Id, delivery.DeliveryNumber,
            delivery.DeliveryDate, delivery.SalesOrder.OrderNumber);

        return Result<DeliveryDto>.Success(dto);
    }

    public async Task<Result<List<DeliveryDto>>> GetAllDeliveriesAsync(Guid? salesOrderId = null)
    {
        var query = _context.Deliveries
            .Include(d => d.SalesOrder)
            .AsQueryable();

        if (salesOrderId.HasValue)
            query = query.Where(d => d.SalesOrderId == salesOrderId.Value);

        var deliveries = await query.OrderByDescending(d => d.DeliveryDate).ToListAsync();

        var dtos = deliveries.Select(d => new DeliveryDto(
            d.Id, d.DeliveryNumber, d.DeliveryDate, d.SalesOrder.OrderNumber
        )).ToList();

        return Result<List<DeliveryDto>>.Success(dtos);
    }

    public async Task<Result> DeleteDeliveryAsync(Guid id)
    {
        var delivery = await _context.Deliveries
            .Include(d => d.Items)
            .ThenInclude(di => di.SalesOrderItem)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (delivery == null)
            return Result.Failure("Delivery not found");

        // Reverse stock movements
        var movements = await _context.StockMovements
            .Where(sm => sm.MovementNumber == delivery.DeliveryNumber)
            .ToListAsync();

        foreach (var movement in movements)
        {
            var stock = await _context.Stocks
                .FirstOrDefaultAsync(s => s.ProductId == movement.ProductId && s.WarehouseId == movement.WarehouseId);

            if (stock != null)
            {
                stock.QuantityOnHand += Math.Abs(movement.Quantity);
                stock.QuantityAvailable = stock.QuantityOnHand - stock.QuantityReserved;
            }
        }

        // Reverse sales order item delivered quantities
        foreach (var item in delivery.Items)
        {
            item.SalesOrderItem.DeliveredQuantity -= item.DeliveredQuantity;
        }

        _context.StockMovements.RemoveRange(movements);
        _context.Deliveries.Remove(delivery);
        await _context.SaveChangesAsync();

        return Result.Success();
    }


    public async Task<Result<SalesQuotationDto>> CreateSalesQuotationAsync(CreateSalesQuotationRequest request)
    {
        try
        {
            var customer = await _context.Customers.FindAsync(request.CustomerId);
            if (customer == null)
                return Result<SalesQuotationDto>.Failure("Customer not found");

            // Validate items
            if (request.Items == null || request.Items.Count == 0)
                return Result<SalesQuotationDto>.Failure("Quotation must have at least one item");

            // Generate quotation number
            var lastQuotation = await _context.SalesQuotations
                .OrderByDescending(q => q.CreatedAt)
                .FirstOrDefaultAsync();

            var quotationNumber = GenerateQuotationNumber(lastQuotation?.QuotationNumber);

            var quotation = new SalesQuotation
            {
                Id = Guid.NewGuid(),
                QuotationNumber = quotationNumber,
                QuotationDate = request.QuotationDate,
                ValidUntil = request.ValidUntil,
                CustomerId = request.CustomerId,
                OpportunityId = request.OpportunityId,
                Notes = request.Notes,
                Terms = request.Terms,
                Status = "Draft",
                SubTotal = 0,
                TaxAmount = 0,
                DiscountAmount = 0,
                TotalAmount = 0
            };

            decimal subTotal = 0;
            decimal totalTax = 0;
            decimal totalDiscount = 0;

            foreach (var item in request.Items)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product == null)
                    return Result<SalesQuotationDto>.Failure($"Product {item.ProductId} not found");

                var lineDiscount = item.Quantity * item.UnitPrice * (item.DiscountPercent / 100);
                var lineBeforeTax = (item.Quantity * item.UnitPrice) - lineDiscount;
                var lineTax = lineBeforeTax * (item.TaxRate / 100);
                var lineTotal = lineBeforeTax + lineTax;

                subTotal += item.Quantity * item.UnitPrice;
                totalDiscount += lineDiscount;
                totalTax += lineTax;

                quotation.Items.Add(new SalesQuotationItem
                {
                    Id = Guid.NewGuid(),
                    SalesQuotationId = quotation.Id,
                    ProductId = item.ProductId,
                    Description = item.Description,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    DiscountPercent = item.DiscountPercent,
                    TaxRate = item.TaxRate,
                    LineTotal = lineTotal,
                    CreatedAt = DateTime.UtcNow
                });
            }

            quotation.SubTotal = subTotal;
            quotation.DiscountAmount = totalDiscount;
            quotation.TaxAmount = totalTax;
            quotation.TotalAmount = subTotal - totalDiscount + totalTax;

            _context.SalesQuotations.Add(quotation);
            await _context.SaveChangesAsync();

            return Result<SalesQuotationDto>.Success(MapQuotationToDto(quotation, customer));
        }
        catch (Exception ex)
        {
            return Result<SalesQuotationDto>.Failure($"Error creating quotation: {ex.Message}");
        }
    }

    public async Task<Result<SalesQuotationDto>> GetSalesQuotationByIdAsync(Guid id)
    {
        var quotation = await _context.SalesQuotations
            .Include(q => q.Customer)
            .Include(q => q.Opportunity)
            .Include(q => q.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(q => q.Id == id);

        if (quotation == null)
            return Result<SalesQuotationDto>.Failure("Quotation not found");

        return Result<SalesQuotationDto>.Success(MapQuotationToDto(quotation, quotation.Customer));
    }

    public async Task<Result<List<SalesQuotationDto>>> GetAllSalesQuotationsAsync(string? status = null)
    {
        var query = _context.SalesQuotations
            .Include(q => q.Customer)
            .Include(q => q.Opportunity)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(q => q.Status == status);

        var quotations = await query.OrderByDescending(q => q.CreatedAt).ToListAsync();

        return Result<List<SalesQuotationDto>>.Success(
            quotations.Select(q => MapQuotationToDto(q, q.Customer)).ToList());
    }

    public async Task<Result<PagedResult<SalesQuotationDto>>> SearchSalesQuotationsAsync(
        SalesQuotationSearchRequest search,
        PaginationRequest pagination)
    {
        var query = _context.SalesQuotations
            .Include(q => q.Customer)
            .Include(q => q.Opportunity)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search.SearchTerm))
            query = query.Where(q => q.QuotationNumber.Contains(search.SearchTerm) ||
                                    q.Customer.Name.Contains(search.SearchTerm));

        if (search.CustomerId.HasValue)
            query = query.Where(q => q.CustomerId == search.CustomerId);

        if (search.OpportunityId.HasValue)
            query = query.Where(q => q.OpportunityId == search.OpportunityId);

        if (!string.IsNullOrEmpty(search.Status))
            query = query.Where(q => q.Status == search.Status);

        if (search.DateFrom.HasValue)
            query = query.Where(q => q.QuotationDate >= search.DateFrom);

        if (search.DateTo.HasValue)
            query = query.Where(q => q.QuotationDate <= search.DateTo);

        var result = await query.OrderByDescending(q => q.CreatedAt)
            .ToPagedResultAsync(pagination.Page, pagination.PageSize);

        return Result<PagedResult<SalesQuotationDto>>.Success(new PagedResult<SalesQuotationDto>
        {
            Items = result.Items.Select(q => MapQuotationToDto(q, q.Customer)).ToList(),
            TotalCount = result.TotalCount,
            PageNumber = result.PageNumber,
            PageSize = result.PageSize
        });
    }

    public async Task<Result<SalesQuotationDto>> UpdateSalesQuotationAsync(Guid id, UpdateSalesQuotationRequest request)
    {
        var quotation = await _context.SalesQuotations
            .Include(q => q.Customer)
            .Include(q => q.Items)
            .FirstOrDefaultAsync(q => q.Id == id);

        if (quotation == null)
            return Result<SalesQuotationDto>.Failure("Quotation not found");

        // Only allow updates if status is Draft
        if (quotation.Status != "Draft")
            return Result<SalesQuotationDto>.Failure("Can only update quotations with Draft status");

        if (request.ValidUntil.HasValue)
            quotation.ValidUntil = request.ValidUntil.Value;

        if (request.Items != null && request.Items.Count > 0)
        {
            // Remove old items
            _context.SalesQuotationItems.RemoveRange(quotation.Items);
            quotation.Items.Clear();

            // Add new items
            decimal subTotal = 0;
            decimal totalTax = 0;
            decimal totalDiscount = 0;

            foreach (var item in request.Items)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product == null)
                    return Result<SalesQuotationDto>.Failure($"Product {item.ProductId} not found");

                var lineDiscount = item.Quantity * item.UnitPrice * (item.DiscountPercent / 100);
                var lineBeforeTax = (item.Quantity * item.UnitPrice) - lineDiscount;
                var lineTax = lineBeforeTax * (item.TaxRate / 100);
                var lineTotal = lineBeforeTax + lineTax;

                subTotal += item.Quantity * item.UnitPrice;
                totalDiscount += lineDiscount;
                totalTax += lineTax;

                quotation.Items.Add(new SalesQuotationItem
                {
                    Id = Guid.NewGuid(),
                    SalesQuotationId = quotation.Id,
                    ProductId = item.ProductId,
                    Description = item.Description,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    DiscountPercent = item.DiscountPercent,
                    TaxRate = item.TaxRate,
                    LineTotal = lineTotal,
                    CreatedAt = DateTime.UtcNow
                });
            }

            quotation.SubTotal = subTotal;
            quotation.DiscountAmount = totalDiscount;
            quotation.TaxAmount = totalTax;
            quotation.TotalAmount = subTotal - totalDiscount + totalTax;
        }

        if (!string.IsNullOrEmpty(request.Notes))
            quotation.Notes = request.Notes;

        if (!string.IsNullOrEmpty(request.Terms))
            quotation.Terms = request.Terms;

        quotation.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Result<SalesQuotationDto>.Success(MapQuotationToDto(quotation, quotation.Customer));
    }

    public async Task<Result> DeleteSalesQuotationAsync(Guid id)
    {
        var quotation = await _context.SalesQuotations
            .Include(q => q.Items)
            .FirstOrDefaultAsync(q => q.Id == id);

        if (quotation == null)
            return Result.Failure("Quotation not found");

        // Only allow deletion if status is Draft or Rejected
        if (quotation.Status != "Draft" && quotation.Status != "Rejected")
            return Result.Failure($"Cannot delete quotation with {quotation.Status} status");

        _context.SalesQuotationItems.RemoveRange(quotation.Items);
        _context.SalesQuotations.Remove(quotation);
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result> ApproveSalesQuotationAsync(Guid id)
    {
        var quotation = await _context.SalesQuotations.FindAsync(id);

        if (quotation == null)
            return Result.Failure("Quotation not found");

        if (quotation.Status != "Draft")
            return Result.Failure("Only draft quotations can be approved");

        quotation.Status = "Approved";
        quotation.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result> RejectSalesQuotationAsync(Guid id)
    {
        var quotation = await _context.SalesQuotations.FindAsync(id);

        if (quotation == null)
            return Result.Failure("Quotation not found");

        if (quotation.Status == "Converted" || quotation.Status == "Rejected")
            return Result.Failure($"Cannot reject a {quotation.Status} quotation");

        quotation.Status = "Rejected";
        quotation.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result> ConvertQuotationToSalesOrderAsync(Guid quotationId, ConvertQuotationToOrderRequest orderRequest)
    {
        // Fetch the quotation first
        var quotation = await _context.SalesQuotations
            .Include(q => q.Items)
            .FirstOrDefaultAsync(q => q.Id == quotationId && !q.IsDeleted);

        if (quotation == null)
            return Result.Failure("Quotation not found");

        if (quotation.Status != "Approved")
            return Result.Failure("Only approved quotations can be converted to sales orders");

        // Extract CustomerId from the quotation
        var customerId = quotation.CustomerId;

        // Create sales order from quotation using the quotation's CustomerId
        var salesOrderRequest = new CreateSalesOrderRequest(
            customerId,
            orderRequest.OrderDate,
            orderRequest.ExpectedDeliveryDate,
            orderRequest.Items,
            orderRequest.ShippingAddress,
            orderRequest.Notes
        );

        var result = await CreateSalesOrderAsync(salesOrderRequest);

        if (result.IsSuccess)
        {
            quotation.Status = "Converted";
            quotation.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return result.IsSuccess ? Result.Success() : Result.Failure(result.ErrorMessage);

    }

    private string GenerateQuotationNumber(string? lastQuotationNumber)
    {
        if (string.IsNullOrEmpty(lastQuotationNumber))
            return $"QT-{DateTime.UtcNow:yyyyMM}-0001";

        // Extract the number from format QT-YYYYMM-XXXX
        var parts = lastQuotationNumber.Split('-');
        if (parts.Length == 3 && int.TryParse(parts[2], out var number))
        {
            var yearMonth = DateTime.UtcNow.ToString("yyyyMM");
            if (parts[1] == yearMonth)
            {
                return $"QT-{yearMonth}-{(number + 1):D4}";
            }
        }

        return $"QT-{DateTime.UtcNow:yyyyMM}-0001";
    }

    private SalesQuotationDto MapQuotationToDto(SalesQuotation quotation, Customer customer)
    {
        return new SalesQuotationDto(
            quotation.Id,
            quotation.QuotationNumber,
            quotation.QuotationDate,
            quotation.ValidUntil,
            customer.Name,
            quotation.Opportunity?.Name,
            quotation.SubTotal,
            quotation.TaxAmount,
            quotation.DiscountAmount,
            quotation.TotalAmount,
            quotation.Status);
    }
}

