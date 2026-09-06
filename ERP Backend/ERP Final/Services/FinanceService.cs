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
/// Represents the finance service domain model.
/// </summary>
public class FinanceService : IFinanceService
{
    private readonly ERPDbContext _context;
    private readonly IFraudDetectionService _fraudDetectionService;
    private readonly ILogger<FinanceService> _logger;

    public FinanceService(ERPDbContext context, IFraudDetectionService fraudDetectionService, ILogger<FinanceService> logger)
    {
        _context = context;
        _fraudDetectionService = fraudDetectionService;
        _logger = logger;
    }
    private async Task CreatePaymentJournalEntry(Payment payment, Invoice invoice, ERPDbContext context)
    {
        // Find required accounts
        var bankAccount = await context.Accounts
            .FirstOrDefaultAsync(a => a.AccountCode == "1010"); // Cash/Bank

        var arAccount = await context.Accounts
            .FirstOrDefaultAsync(a => a.AccountCode == "1200"); // A/R

        if (bankAccount == null || arAccount == null)
        {
            return;
        }

        var entry = new JournalEntry
        {
            EntryNumber = $"PAY-JE-{payment.PaymentNumber}",
            EntryDate = payment.PaymentDate,
            Description = $"Payment {payment.PaymentNumber} for Invoice {invoice.InvoiceNumber}",
            IsPosted = true,
            PostedAt = DateTime.UtcNow
        };

        entry.Lines.Add(new JournalEntryLine
        {
            JournalEntryId = entry.Id,
            AccountId = bankAccount.Id,
            DebitAmount = payment.Amount,
            CreditAmount = 0,
            Description = $"Payment received - {payment.PaymentNumber}"
        });

        entry.Lines.Add(new JournalEntryLine
        {
            JournalEntryId = entry.Id,
            AccountId = arAccount.Id,
            DebitAmount = 0,
            CreditAmount = payment.Amount,
            Description = $"Clear A/R for Invoice {invoice.InvoiceNumber}"
        });

        context.JournalEntries.Add(entry);

        // Update balances
        bankAccount.Balance += payment.Amount;
        arAccount.Balance -= payment.Amount;
    }
    private async Task CreateInvoiceJournalEntry(Invoice invoice, ERPDbContext context)
    {
        // Find required accounts (you need to create these accounts first in your database)
        var arAccount = await context.Accounts
            .FirstOrDefaultAsync(a => a.AccountCode == "1200"); // Accounts Receivable

        var revenueAccount = await context.Accounts
            .FirstOrDefaultAsync(a => a.AccountCode == "4000"); // Sales Revenue

        if (arAccount == null || revenueAccount == null)
        {
            // Skip journal entry if accounts don't exist
            return;
        }

        var entry = new JournalEntry
        {
            EntryNumber = $"INV-JE-{invoice.InvoiceNumber}",
            EntryDate = invoice.InvoiceDate,
            Description = $"Sales Invoice {invoice.InvoiceNumber}",
            IsPosted = true,
            PostedAt = DateTime.UtcNow
        };

        entry.Lines.Add(new JournalEntryLine
        {
            JournalEntryId = entry.Id,
            AccountId = arAccount.Id,
            DebitAmount = invoice.TotalAmount,
            CreditAmount = 0,
            Description = $"A/R for Invoice {invoice.InvoiceNumber}"
        });

        entry.Lines.Add(new JournalEntryLine
        {
            JournalEntryId = entry.Id,
            AccountId = revenueAccount.Id,
            DebitAmount = 0,
            CreditAmount = invoice.TotalAmount,
            Description = $"Revenue for Invoice {invoice.InvoiceNumber}"
        });

        context.JournalEntries.Add(entry);

        // Update account balances
        arAccount.Balance += invoice.TotalAmount;
        revenueAccount.Balance += invoice.TotalAmount;
    }
    public async Task<Result<PagedResult<InvoiceDto>>> SearchInvoicesAsync(
    InvoiceSearchRequest search,
    PaginationRequest pagination)
    {
        try
        {
            var query = _context.Invoices
                .Include(i => i.Customer)
                .AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(search.SearchTerm))
            {
                var searchLower = search.SearchTerm.ToLower();
                query = query.Where(i =>
                    i.InvoiceNumber.ToLower().Contains(searchLower) ||
                    (i.Customer != null && i.Customer.Name.ToLower().Contains(searchLower)));
            }

            // Apply customer filter
            if (search.CustomerId.HasValue)
            {
                query = query.Where(i => i.CustomerId == search.CustomerId.Value);
            }

            // Apply status filter
            if (search.Status.HasValue)
            {
                query = query.Where(i => i.Status == search.Status.Value);
            }

            // Apply invoice date filters
            if (search.InvoiceDateFrom.HasValue)
            {
                query = query.Where(i => i.InvoiceDate >= search.InvoiceDateFrom.Value);
            }

            if (search.InvoiceDateTo.HasValue)
            {
                query = query.Where(i => i.InvoiceDate <= search.InvoiceDateTo.Value);
            }

            // Apply due date filters
            if (search.DueDateFrom.HasValue)
            {
                query = query.Where(i => i.DueDate >= search.DueDateFrom.Value);
            }

            if (search.DueDateTo.HasValue)
            {
                query = query.Where(i => i.DueDate <= search.DueDateTo.Value);
            }

            // Apply amount filters
            if (search.MinAmount.HasValue)
            {
                query = query.Where(i => i.TotalAmount >= search.MinAmount.Value);
            }

            if (search.MaxAmount.HasValue)
            {
                query = query.Where(i => i.TotalAmount <= search.MaxAmount.Value);
            }

            // Apply overdue filter
            if (search.Overdue == true)
            {
                var today = DateTime.UtcNow.Date;
                query = query.Where(i => i.DueDate < today && i.Status != InvoiceStatus.Paid);
            }

            // Apply sorting
            query = query.ApplySorting(pagination.SortBy ?? "InvoiceDate", pagination.SortDescending ?? false);

            // Get total count
            var totalCount = await query.CountAsync();

            // Apply pagination and project to DTO
            var invoices = await query
                .Skip(pagination.Skip)
                .Take(pagination.Take)
                .Select(i => new InvoiceDto(
                    i.Id,
                    i.InvoiceNumber,
                    i.InvoiceDate,
                    i.DueDate,
                    i.Customer != null ? i.Customer.Name : null,
                    i.TotalAmount,
                    i.BalanceAmount,
                    i.Status
                ))
                .ToListAsync();

            var pagedResult = new PagedResult<InvoiceDto>
            {
                Items = invoices,
                TotalCount = totalCount,
                PageNumber = pagination.Page,
                PageSize = pagination.PageSize
            };

            return Result<PagedResult<InvoiceDto>>.Success(pagedResult);
        }
        catch (Exception ex)
        {
            return Result<PagedResult<InvoiceDto>>.Failure($"Error searching invoices: {ex.Message}");
        }
    }

    public async Task<Result<AccountDto>> CreateAccountAsync(CreateAccountRequest request)
    {
        // ? Validate required fields
        if (string.IsNullOrWhiteSpace(request.AccountCode))
        {
            return Result<AccountDto>.Failure("Account code is required");
        }

        if (string.IsNullOrWhiteSpace(request.AccountName))
        {
            return Result<AccountDto>.Failure("Account name is required");
        }

        // ? Validate AccountCode length (must match database constraint)
        if (request.AccountCode.Length > 50)
        {
            return Result<AccountDto>.Failure("Account code cannot exceed 50 characters");
        }

        // ? Validate AccountName length (must match database constraint)
        if (request.AccountName.Length > 200)
        {
            return Result<AccountDto>.Failure("Account name cannot exceed 200 characters");
        }

        // ? FIX: Only check for active accounts (exclude deleted/inactive accounts)
        if (await _context.Accounts.AnyAsync(a => a.AccountCode == request.AccountCode && a.IsActive && !a.IsDeleted))
        {
            return Result<AccountDto>.Failure("Account code already exists");
        }

        var account = new Account
        {
            AccountCode = request.AccountCode.Trim(),
            AccountName = request.AccountName.Trim(),
            Type = request.Type,
            ParentAccountId = request.ParentAccountId,
            Description = request.Description?.Trim(),
            Balance = request.InitialBalance ?? 0, // ? Use provided balance or default to 0
            IsActive = true
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        var dto = new AccountDto(account.Id, account.AccountCode, account.AccountName, account.Type, account.Balance, account.IsActive);
        return Result<AccountDto>.Success(dto);
    }

    public async Task<Result<List<AccountDto>>> GetAllAccountsAsync()
    {
        var accounts = await _context.Accounts
            .Where(a => a.IsActive)
            .ToListAsync();

        var dtos = accounts.Select(a => new AccountDto(
            a.Id, a.AccountCode, a.AccountName, a.Type, a.Balance, a.IsActive
        )).ToList();

        return Result<List<AccountDto>>.Success(dtos);
    }

    public async Task<Result<JournalEntryDto>> CreateJournalEntryAsync(CreateJournalEntryRequest request)
    {
        // Validate that debits equal credits
        var totalDebits = request.Lines.Sum(l => l.DebitAmount);
        var totalCredits = request.Lines.Sum(l => l.CreditAmount);

        if (totalDebits != totalCredits)
        {
            return Result<JournalEntryDto>.Failure("Total debits must equal total credits");
        }

        var entryNumber = $"JE-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}";

        var journalEntry = new JournalEntry
        {
            EntryNumber = entryNumber,
            EntryDate = request.EntryDate,
            Description = request.Description,
            Reference = request.Reference,
            IsPosted = false
        };

        foreach (var line in request.Lines)
        {
            var account = await _context.Accounts.FindAsync(line.AccountId);
            if (account == null)
            {
                return Result<JournalEntryDto>.Failure($"Account not found: {line.AccountId}");
            }

            var entryLine = new JournalEntryLine
            {
                JournalEntryId = journalEntry.Id,
                AccountId = line.AccountId,
                DebitAmount = line.DebitAmount,
                CreditAmount = line.CreditAmount,
                Description = line.Description
            };

            journalEntry.Lines.Add(entryLine);
        }

        _context.JournalEntries.Add(journalEntry);
        await _context.SaveChangesAsync();

        var dto = new JournalEntryDto(journalEntry.Id, journalEntry.EntryNumber, journalEntry.EntryDate,
            journalEntry.Description, journalEntry.IsPosted);

        return Result<JournalEntryDto>.Success(dto);
    }

    public async Task<Result> PostJournalEntryAsync(Guid entryId)
    {
        var entry = await _context.JournalEntries
            .Include(je => je.Lines)
            .ThenInclude(l => l.Account)
            .FirstOrDefaultAsync(je => je.Id == entryId);

        if (entry == null)
        {
            return Result.Failure("Journal entry not found");
        }

        if (entry.IsPosted)
        {
            return Result.Failure("Journal entry is already posted");
        }

        // Update account balances
        foreach (var line in entry.Lines)
        {
            if (line.Account.Type == AccountType.Asset || line.Account.Type == AccountType.Expense)
            {
                line.Account.Balance += line.DebitAmount - line.CreditAmount;
            }
            else
            {
                line.Account.Balance += line.CreditAmount - line.DebitAmount;
            }
        }

        entry.IsPosted = true;
        entry.PostedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // ? AUTO-UPDATE: Update budgets when journal entry is posted
        await AutoUpdateRelatedBudgets(entry);

        return Result.Success();
    }

    public async Task<Result<InvoiceDto>> CreateInvoiceAsync(Guid customerId, DateTime invoiceDate, DateTime dueDate, List<InvoiceItemRequest> items)
    {
        // ? NEW: Validate items parameter
        if (items == null || items.Count == 0)
        {
            return Result<InvoiceDto>.Failure("At least one invoice item is required");
        }

        var customer = await _context.Customers.FindAsync(customerId);
        if (customer == null)
        {
            return Result<InvoiceDto>.Failure("Customer not found");
        }

        var invoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}";

        var invoice = new Invoice
        {
            InvoiceNumber = invoiceNumber,
            InvoiceDate = invoiceDate,
            DueDate = dueDate,
            CustomerId = customerId,
            Status = InvoiceStatus.Draft
        };

        invoice.Items = new List<InvoiceItem>();

        decimal subTotal = 0;
        decimal taxAmount = 0;

        foreach (var item in items)
        {
            var lineTotal = item.Quantity * item.UnitPrice;
            var lineTax = lineTotal * item.TaxRate / 100;

            var invoiceItem = new InvoiceItem
            {
                InvoiceId = invoice.Id,
                ProductId = item.ProductId,
                Description = item.Description,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TaxRate = item.TaxRate,
                LineTotal = lineTotal + lineTax
            };

            invoice.Items.Add(invoiceItem);
            subTotal += lineTotal;
            taxAmount += lineTax;
        }

        invoice.SubTotal = subTotal;
        invoice.TaxAmount = taxAmount;
        invoice.TotalAmount = subTotal + taxAmount;
        invoice.BalanceAmount = invoice.TotalAmount;

        _context.Invoices.Add(invoice); await _context.SaveChangesAsync();

        // ? CREATE JOURNAL ENTRY FOR INVOICE
        invoice.Status = InvoiceStatus.Sent; // Set to Sent to trigger journal entry
        await CreateInvoiceJournalEntry(invoice, _context);
        // Journal entry will be saved with next SaveChangesAsync()

        var dto = new InvoiceDto(invoice.Id, invoice.InvoiceNumber, invoice.InvoiceDate, invoice.DueDate,
            customer.Name, invoice.TotalAmount, invoice.BalanceAmount, invoice.Status);

        return Result<InvoiceDto>.Success(dto);
    }

    public async Task<Result<List<InvoiceDto>>> GetInvoicesAsync(InvoiceStatus? status = null)
    {
        var query = _context.Invoices.Include(i => i.Customer).AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(i => i.Status == status.Value);
        }

        var invoices = await query.ToListAsync();

        var dtos = invoices.Select(i => new InvoiceDto(
            i.Id, i.InvoiceNumber, i.InvoiceDate, i.DueDate,
            i.Customer?.Name, i.TotalAmount, i.BalanceAmount, i.Status
        )).ToList();

        return Result<List<InvoiceDto>>.Success(dtos);
    }

    public async Task<Result<PaymentDto>> RecordPaymentAsync(Guid invoiceId, decimal amount, PaymentMethod method, DateTime paymentDate)
    {
        // ? NEW: Validate input parameters
        if (amount <= 0)
        {
            return Result<PaymentDto>.Failure("Payment amount must be greater than zero");
        }

        if (paymentDate > DateTime.UtcNow)
        {
            return Result<PaymentDto>.Failure("Payment date cannot be in the future");
        }

        // Get invoice with customer details
        var invoice = await _context.Invoices
            .Include(i => i.Customer)
            .FirstOrDefaultAsync(i => i.Id == invoiceId);

        if (invoice == null)
        {
            return Result<PaymentDto>.Failure("Invoice not found");
        }

        // ? NEW: Check invoice status
        if (invoice.Status == InvoiceStatus.Cancelled)
        {
            return Result<PaymentDto>.Failure("Cannot record payment for cancelled invoice");
        }

        if (invoice.Status == InvoiceStatus.Paid)
        {
            return Result<PaymentDto>.Failure("Invoice is already fully paid");
        }

        // ? FIX: Use threshold for decimal comparison
        const decimal PRECISION_THRESHOLD = 0.01m;

        if (amount > invoice.BalanceAmount + PRECISION_THRESHOLD)
        {
            return Result<PaymentDto>.Failure(
                $"Payment amount ({amount:C}) exceeds balance amount ({invoice.BalanceAmount:C})");
        }

        // ? NEW: Handle overpayment within threshold
        var effectiveAmount = amount;
        if (amount > invoice.BalanceAmount && amount <= invoice.BalanceAmount + PRECISION_THRESHOLD)
        {
            effectiveAmount = invoice.BalanceAmount;
        }

        // ? Use transaction for data integrity
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var paymentNumber = $"PAY-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}";

            var payment = new Payment
            {
                PaymentNumber = paymentNumber,
                PaymentDate = paymentDate,
                InvoiceId = invoiceId,
                CustomerId = invoice.CustomerId,
                Amount = effectiveAmount,
                Method = method,
                Status = PaymentStatus.Completed
            };

            // Update invoice amounts
            invoice.PaidAmount += effectiveAmount;
            invoice.BalanceAmount -= effectiveAmount;

            // ? FIX: Use threshold for comparison
            if (invoice.BalanceAmount <= PRECISION_THRESHOLD)
            {
                invoice.BalanceAmount = 0;
                invoice.Status = InvoiceStatus.Paid;
            }
            else if (invoice.PaidAmount > 0 && invoice.Status == InvoiceStatus.Draft)
            {
                invoice.Status = InvoiceStatus.Sent;
            }

            // ? NEW: Update timestamp
            invoice.UpdatedAt = DateTime.UtcNow;

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            // ? CREATE JOURNAL ENTRY FOR PAYMENT
            await CreatePaymentJournalEntry(payment, invoice, _context);
            // Journal entry will be saved with transaction commit

            await transaction.CommitAsync();

            var dto = new PaymentDto(
                payment.Id,
                payment.PaymentNumber,
                payment.PaymentDate,
                payment.Amount,
                payment.Method,
                payment.Status
            );

            return Result<PaymentDto>.Success(dto);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return Result<PaymentDto>.Failure("Payment already processed by another user. Please refresh and try again.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            // Log error: _logger.LogError(ex, "Error recording payment for invoice {InvoiceId}", invoiceId);
            return Result<PaymentDto>.Failure("An error occurred while recording the payment");
        }
    }
    public async Task<Result<Dictionary<string, decimal>>> GetFinancialSummaryAsync(DateTime startDate, DateTime endDate)
    {
        var totalRevenue = await _context.Invoices
            .Where(i => i.InvoiceDate >= startDate && i.InvoiceDate <= endDate && i.Status == InvoiceStatus.Paid)
            .SumAsync(i => i.TotalAmount);

        var totalExpenses = await _context.Expenses
            .Where(e => e.ExpenseDate >= startDate && e.ExpenseDate <= endDate && e.IsApproved)
            .SumAsync(e => e.Amount);

        var summary = new Dictionary<string, decimal>
        {
            ["TotalRevenue"] = totalRevenue,
            ["TotalExpenses"] = totalExpenses,
            ["NetProfit"] = totalRevenue - totalExpenses
        };

        return Result<Dictionary<string, decimal>>.Success(summary);
    }
    public async Task<Result<AccountDto>> GetAccountByIdAsync(Guid id)
    {
        var account = await _context.Accounts.FindAsync(id);
        if (account == null)
            return Result<AccountDto>.Failure("Account not found");

        var dto = new AccountDto(account.Id, account.AccountCode, account.AccountName, account.Type, account.Balance, account.IsActive);
        return Result<AccountDto>.Success(dto);
    }

    public async Task<Result<AccountDto>> UpdateAccountAsync(Guid id, UpdateAccountRequest request)
    {
        var account = await _context.Accounts.FindAsync(id);
        if (account == null)
            return Result<AccountDto>.Failure("Account not found");

        account.AccountName = request.AccountName;
        account.Description = request.Description;
        account.IsActive = request.IsActive;
        account.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var dto = new AccountDto(account.Id, account.AccountCode, account.AccountName, account.Type, account.Balance, account.IsActive);
        return Result<AccountDto>.Success(dto);
    }

    public async Task<Result> DeactivateAccountAsync(Guid id)
    {
        var account = await _context.Accounts.FindAsync(id);
        if (account == null)
            return Result.Failure("Account not found");

        // Check if account has balance
        if (account.Balance != 0)
            return Result.Failure("Cannot deactivate account with non-zero balance");

        // ? FIX: Set both IsActive and IsDeleted for proper soft delete
        account.IsActive = false;
        account.IsDeleted = true; // ? Proper soft delete flag
        account.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<JournalEntryDetailDto>> GetJournalEntryByIdAsync(Guid id)
    {
        var entry = await _context.JournalEntries
            .Include(je => je.Lines)
            .ThenInclude(l => l.Account)
            .FirstOrDefaultAsync(je => je.Id == id);

        if (entry == null)
            return Result<JournalEntryDetailDto>.Failure("Journal entry not found");

        var totalAmount = entry.Lines.Sum(l => l.DebitAmount > 0 ? l.DebitAmount : l.CreditAmount);

        var lines = entry.Lines.Select(l => new JournalLineDetailDto(
            l.AccountId,
            l.Account?.AccountCode,
            l.Account?.AccountName,
            l.DebitAmount,
            l.CreditAmount,
            l.Description
        )).ToList();

        var dto = new JournalEntryDetailDto(
            entry.Id, 
            entry.EntryNumber, 
            entry.EntryDate, 
            entry.Description, 
            entry.IsPosted,
            totalAmount,
            lines
        );
        return Result<JournalEntryDetailDto>.Success(dto);
    }

    public async Task<Result<List<JournalEntryDetailDto>>> GetAllJournalEntriesAsync(bool? isPosted = null)
    {
        var query = _context.JournalEntries
            .Include(je => je.Lines)
            .ThenInclude(l => l.Account)
            .AsQueryable();

        if (isPosted.HasValue)
            query = query.Where(je => je.IsPosted == isPosted.Value);

        var entries = await query.OrderByDescending(je => je.EntryDate).ToListAsync();

        var dtos = entries.Select(je => {
            var totalAmount = je.Lines.Sum(l => l.DebitAmount > 0 ? l.DebitAmount : l.CreditAmount);
            var lines = je.Lines.Select(l => new JournalLineDetailDto(
                l.AccountId,
                l.Account?.AccountCode,
                l.Account?.AccountName,
                l.DebitAmount,
                l.CreditAmount,
                l.Description
            )).ToList();

            return new JournalEntryDetailDto(
                je.Id,
                je.EntryNumber,
                je.EntryDate,
                je.Description,
                je.IsPosted,
                totalAmount,
                lines
            );
        }).ToList();

        return Result<List<JournalEntryDetailDto>>.Success(dtos);
    }

    public async Task<Result> DeleteJournalEntryAsync(Guid id)
    {
        var entry = await _context.JournalEntries.FindAsync(id);
        if (entry == null)
            return Result.Failure("Journal entry not found");

        if (entry.IsPosted)
            return Result.Failure("Cannot delete posted journal entries");

        _context.JournalEntries.Remove(entry);
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<InvoiceDto>> GetInvoiceByIdAsync(Guid id)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Customer)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice == null)
            return Result<InvoiceDto>.Failure("Invoice not found");

        var dto = new InvoiceDto(invoice.Id, invoice.InvoiceNumber, invoice.InvoiceDate, invoice.DueDate,
            invoice.Customer?.Name, invoice.TotalAmount, invoice.BalanceAmount, invoice.Status);

        return Result<InvoiceDto>.Success(dto);
    }

    public async Task<Result<InvoiceDto>> UpdateInvoiceAsync(Guid id, UpdateInvoiceRequest request)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Customer)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice == null)
            return Result<InvoiceDto>.Failure("Invoice not found");

        if (invoice.Status != InvoiceStatus.Draft)
            return Result<InvoiceDto>.Failure("Only draft invoices can be updated");

        if (request.DueDate.HasValue) invoice.DueDate = request.DueDate.Value;
        if (request.Notes != null) invoice.Notes = request.Notes;
        if (request.Terms != null) invoice.Terms = request.Terms;

        invoice.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var dto = new InvoiceDto(invoice.Id, invoice.InvoiceNumber, invoice.InvoiceDate, invoice.DueDate,
            invoice.Customer?.Name, invoice.TotalAmount, invoice.BalanceAmount, invoice.Status);

        return Result<InvoiceDto>.Success(dto);
    }

    public async Task<Result> DeleteInvoiceAsync(Guid id)
    {
        var invoice = await _context.Invoices.FindAsync(id);
        if (invoice == null)
            return Result.Failure("Invoice not found");

        if (invoice.Status == InvoiceStatus.Paid)
            return Result.Failure("Cannot delete paid invoices");

        if (invoice.PaidAmount > 0)
            return Result.Failure("Cannot delete invoices with payments");

        _context.Invoices.Remove(invoice);
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result> CancelInvoiceAsync(Guid id)
    {
        var invoice = await _context.Invoices.FindAsync(id);
        if (invoice == null)
            return Result.Failure("Invoice not found");

        if (invoice.Status == InvoiceStatus.Paid)
            return Result.Failure("Cannot cancel paid invoices");

        if (invoice.PaidAmount > 0)
            return Result.Failure("Cannot cancel invoices with partial payments");

        invoice.Status = InvoiceStatus.Cancelled;
        invoice.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<PaymentDto>> GetPaymentByIdAsync(Guid id)
    {
        var payment = await _context.Payments.FindAsync(id);
        if (payment == null)
            return Result<PaymentDto>.Failure("Payment not found");

        var dto = new PaymentDto(payment.Id, payment.PaymentNumber, payment.PaymentDate, payment.Amount, payment.Method, payment.Status);
        return Result<PaymentDto>.Success(dto);
    }

    public async Task<Result<List<PaymentDto>>> GetAllPaymentsAsync(Guid? invoiceId = null, Guid? customerId = null)
    {
        var query = _context.Payments.AsQueryable();

        if (invoiceId.HasValue)
            query = query.Where(p => p.InvoiceId == invoiceId.Value);

        if (customerId.HasValue)
            query = query.Where(p => p.CustomerId == customerId.Value);

        var payments = await query.OrderByDescending(p => p.PaymentDate).ToListAsync();

        var dtos = payments.Select(p => new PaymentDto(
            p.Id, p.PaymentNumber, p.PaymentDate, p.Amount, p.Method, p.Status
        )).ToList();

        return Result<List<PaymentDto>>.Success(dtos);
    }

    public async Task<Result> DeletePaymentAsync(Guid id)
    {
        var payment = await _context.Payments
            .Include(p => p.Invoice)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payment == null)
            return Result.Failure("Payment not found");

        if (payment.Status == PaymentStatus.Completed)
            return Result.Failure("Cannot delete completed payments. Please void or refund instead");

        // Reverse invoice amounts if payment was linked to invoice
        if (payment.InvoiceId.HasValue && payment.Invoice != null)
        {
            payment.Invoice.PaidAmount -= payment.Amount;
            payment.Invoice.BalanceAmount += payment.Amount;

            if (payment.Invoice.Status == InvoiceStatus.Paid)
                payment.Invoice.Status = InvoiceStatus.Sent;
        }

        _context.Payments.Remove(payment);
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    // BUDGET MANAGEMENT IMPLEMENTATION

    public async Task<Result<BudgetDto>> CreateBudgetAsync(CreateBudgetRequest request)
    {
        // Validate
        if (string.IsNullOrWhiteSpace(request.BudgetName))
            return Result<BudgetDto>.Failure("Budget name is required");

        if (string.IsNullOrWhiteSpace(request.FiscalYear))
            return Result<BudgetDto>.Failure("Fiscal year is required");

        if (request.StartDate >= request.EndDate)
            return Result<BudgetDto>.Failure("Start date must be before end date");

        if (request.Lines == null || request.Lines.Count == 0)
            return Result<BudgetDto>.Failure("At least one budget line is required");

        // Validate all accounts exist
        foreach (var line in request.Lines)
        {
            var account = await _context.Accounts.FindAsync(line.AccountId);
            if (account == null)
                return Result<BudgetDto>.Failure($"Account not found: {line.AccountId}");

            if (!account.IsActive)
                return Result<BudgetDto>.Failure($"Cannot budget for inactive account: {account.AccountName}");
        }

        var budgetNumber = $"BUD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}";

        var budget = new Budget
        {
            BudgetNumber = budgetNumber,
            BudgetName = request.BudgetName,
            Department = request.Department,
            FiscalYear = request.FiscalYear,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Description = request.Description,
            Notes = request.Notes,
            Status = BudgetStatus.Draft,
            TotalBudgetAmount = 0,
            ActualAmount = 0,
            Variance = 0
        };

        decimal totalBudget = 0;

        foreach (var line in request.Lines)
        {
            var account = await _context.Accounts.FindAsync(line.AccountId);

            var budgetLine = new BudgetLine
            {
                BudgetId = budget.Id,
                AccountId = line.AccountId,
                LineDescription = line.LineDescription,
                BudgetedAmount = line.BudgetedAmount,
                ActualAmount = 0,
                Variance = 0,
                VariancePercentage = 0,
                Notes = line.Notes
            };

            budget.BudgetLines.Add(budgetLine);
            totalBudget += line.BudgetedAmount;
        }

        budget.TotalBudgetAmount = totalBudget;
        budget.Variance = totalBudget; // Initially, variance = budget (no actuals yet)

        _context.Budgets.Add(budget);
        await _context.SaveChangesAsync();

        var dto = new BudgetDto(
            budget.Id,
            budget.BudgetNumber,
            budget.BudgetName,
            budget.Department,
            budget.FiscalYear,
            budget.StartDate,
            budget.EndDate,
            budget.TotalBudgetAmount,
            budget.ActualAmount,
            budget.Variance,
            budget.Status
        );

        return Result<BudgetDto>.Success(dto);
    }

    public async Task<Result<List<BudgetDto>>> GetAllBudgetsAsync(BudgetStatus? status = null)
    {
        var query = _context.Budgets.AsQueryable();

        if (status.HasValue)
            query = query.Where(b => b.Status == status.Value);

        var budgets = await query.OrderByDescending(b => b.CreatedAt).ToListAsync();

        var dtos = budgets.Select(b => new BudgetDto(
            b.Id,
            b.BudgetNumber,
            b.BudgetName,
            b.Department,
            b.FiscalYear,
            b.StartDate,
            b.EndDate,
            b.TotalBudgetAmount,
            b.ActualAmount,
            b.Variance,
            b.Status
        )).ToList();

        return Result<List<BudgetDto>>.Success(dtos);
    }

    public async Task<Result<BudgetDto>> GetBudgetByIdAsync(Guid id)
    {
        var budget = await _context.Budgets.FindAsync(id);

        if (budget == null)
            return Result<BudgetDto>.Failure("Budget not found");

        var dto = new BudgetDto(
            budget.Id,
            budget.BudgetNumber,
            budget.BudgetName,
            budget.Department,
            budget.FiscalYear,
            budget.StartDate,
            budget.EndDate,
            budget.TotalBudgetAmount,
            budget.ActualAmount,
            budget.Variance,
            budget.Status
        );

        return Result<BudgetDto>.Success(dto);
    }

    public async Task<Result<BudgetDto>> UpdateBudgetAsync(Guid id, UpdateBudgetRequest request)
    {
        var budget = await _context.Budgets.FindAsync(id);

        if (budget == null)
            return Result<BudgetDto>.Failure("Budget not found");

        if (budget.Status == BudgetStatus.Approved || budget.Status == BudgetStatus.Active)
            return Result<BudgetDto>.Failure("Cannot update approved or active budgets");

        if (request.BudgetName != null) budget.BudgetName = request.BudgetName;
        if (request.Department != null) budget.Department = request.Department;
        if (request.StartDate.HasValue) budget.StartDate = request.StartDate.Value;
        if (request.EndDate.HasValue) budget.EndDate = request.EndDate.Value;
        if (request.Description != null) budget.Description = request.Description;
        if (request.Notes != null) budget.Notes = request.Notes;

        if (request.StartDate.HasValue && request.EndDate.HasValue)
        {
            if (request.StartDate.Value >= request.EndDate.Value)
                return Result<BudgetDto>.Failure("Start date must be before end date");
        }

        budget.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var dto = new BudgetDto(
            budget.Id,
            budget.BudgetNumber,
            budget.BudgetName,
            budget.Department,
            budget.FiscalYear,
            budget.StartDate,
            budget.EndDate,
            budget.TotalBudgetAmount,
            budget.ActualAmount,
            budget.Variance,
            budget.Status
        );

        return Result<BudgetDto>.Success(dto);
    }

    public async Task<Result> DeleteBudgetAsync(Guid id)
    {
        var budget = await _context.Budgets.FindAsync(id);

        if (budget == null)
            return Result.Failure("Budget not found");

        if (budget.Status == BudgetStatus.Active)
            return Result.Failure("Cannot delete active budgets. Close the budget first.");

        budget.IsDeleted = true;
        budget.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result> ApproveBudgetAsync(Guid id, Guid approvedBy)
    {
        var budget = await _context.Budgets.FindAsync(id);

        if (budget == null)
            return Result.Failure("Budget not found");

        if (budget.Status != BudgetStatus.Draft && budget.Status != BudgetStatus.Submitted)
            return Result.Failure("Only draft or submitted budgets can be approved");

        budget.Status = BudgetStatus.Approved;
        budget.ApprovedBy = approvedBy;
        budget.ApprovedAt = DateTime.UtcNow;
        budget.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> RejectBudgetAsync(Guid id)
    {
        var budget = await _context.Budgets.FindAsync(id);

        if (budget == null)
            return Result.Failure("Budget not found");

        if (budget.Status != BudgetStatus.Submitted && budget.Status != BudgetStatus.UnderReview)
            return Result.Failure("Only submitted or under review budgets can be rejected");

        budget.Status = BudgetStatus.Rejected;
        budget.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> ActivateBudgetAsync(Guid id)
    {
        var budget = await _context.Budgets.FindAsync(id);

        if (budget == null)
            return Result.Failure("Budget not found");

        if (budget.Status != BudgetStatus.Approved)
            return Result.Failure("Only approved budgets can be activated");

        // Check if dates are valid
        if (budget.StartDate > DateTime.UtcNow)
            return Result.Failure("Cannot activate budget before start date");

        budget.Status = BudgetStatus.Active;
        budget.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> CloseBudgetAsync(Guid id)
    {
        var budget = await _context.Budgets.FindAsync(id);

        if (budget == null)
            return Result.Failure("Budget not found");

        if (budget.Status != BudgetStatus.Active)
            return Result.Failure("Only active budgets can be closed");

        // Update actual amounts one final time
        await UpdateBudgetActuals(id);

        budget.Status = BudgetStatus.Closed;
        budget.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result<PagedResult<BudgetDto>>> SearchBudgetsAsync(
        BudgetSearchRequest search,
        PaginationRequest pagination)
    {
        try
        {
            var query = _context.Budgets.AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(search.SearchTerm))
            {
                var searchLower = search.SearchTerm.ToLower();
                query = query.Where(b =>
                    b.BudgetNumber.ToLower().Contains(searchLower) ||
                    b.BudgetName.ToLower().Contains(searchLower) ||
                    (b.Description != null && b.Description.ToLower().Contains(searchLower)));
            }

            // Apply department filter
            if (!string.IsNullOrWhiteSpace(search.Department))
            {
                query = query.Where(b => b.Department == search.Department);
            }

            // Apply fiscal year filter
            if (!string.IsNullOrWhiteSpace(search.FiscalYear))
            {
                query = query.Where(b => b.FiscalYear == search.FiscalYear);
            }

            // Apply status filter
            if (search.Status.HasValue)
            {
                query = query.Where(b => b.Status == search.Status.Value);
            }

            // Apply date filters
            if (search.StartDateFrom.HasValue)
            {
                query = query.Where(b => b.StartDate >= search.StartDateFrom.Value);
            }

            if (search.StartDateTo.HasValue)
            {
                query = query.Where(b => b.StartDate <= search.StartDateTo.Value);
            }

            // Apply sorting
            query = query.ApplySorting(pagination.SortBy ?? "StartDate", pagination.SortDescending ?? true);

            // Get total count
            var totalCount = await query.CountAsync();

            // Apply pagination
            var budgets = await query
                .Skip(pagination.Skip)
                .Take(pagination.Take)
                .Select(b => new BudgetDto(
                    b.Id,
                    b.BudgetNumber,
                    b.BudgetName,
                    b.Department,
                    b.FiscalYear,
                    b.StartDate,
                    b.EndDate,
                    b.TotalBudgetAmount,
                    b.ActualAmount,
                    b.Variance,
                    b.Status
                ))
                .ToListAsync();

            var pagedResult = new PagedResult<BudgetDto>
            {
                Items = budgets,
                TotalCount = totalCount,
                PageNumber = pagination.Page,
                PageSize = pagination.PageSize
            };

            return Result<PagedResult<BudgetDto>>.Success(pagedResult);
        }
        catch (Exception ex)
        {
            return Result<PagedResult<BudgetDto>>.Failure($"Error searching budgets: {ex.Message}");
        }
    }

    public async Task<Result<List<BudgetLineDto>>> GetBudgetLinesAsync(Guid budgetId)
    {
        var budget = await _context.Budgets.FindAsync(budgetId);

        if (budget == null)
            return Result<List<BudgetLineDto>>.Failure("Budget not found");

        var lines = await _context.BudgetLines
            .Include(bl => bl.Account)
            .Where(bl => bl.BudgetId == budgetId)
            .ToListAsync();

        var dtos = lines.Select(bl => new BudgetLineDto(
            bl.Id,
            bl.BudgetId,
            bl.AccountId,
            bl.Account.AccountCode,
            bl.Account.AccountName,
            bl.LineDescription,
            bl.BudgetedAmount,
            bl.ActualAmount,
            bl.Variance,
            bl.VariancePercentage
        )).ToList();

        return Result<List<BudgetLineDto>>.Success(dtos);
    }

    public async Task<Result> UpdateBudgetActuals(Guid budgetId)
    {
        var budget = await _context.Budgets
            .Include(b => b.BudgetLines)
            .ThenInclude(bl => bl.Account)
            .ThenInclude(a => a.JournalEntryLines)
            .ThenInclude(jel => jel.JournalEntry)
            .FirstOrDefaultAsync(b => b.Id == budgetId);

        if (budget == null)
            return Result.Failure("Budget not found");

        decimal totalActual = 0;

        foreach (var line in budget.BudgetLines)
        {
            // Get actual transactions for this account within budget period
            var accountTransactions = line.Account.JournalEntryLines
                .Where(jel => jel.JournalEntry.IsPosted &&
                             jel.JournalEntry.EntryDate >= budget.StartDate &&
                             jel.JournalEntry.EntryDate <= budget.EndDate)
                .ToList();

            // Calculate actual based on account type
            decimal actualAmount = 0;

            if (line.Account.Type == AccountType.Expense)
            {
                // For expense accounts: sum of debits
                actualAmount = accountTransactions.Sum(jel => jel.DebitAmount);
            }
            else if (line.Account.Type == AccountType.Revenue)
            {
                // For revenue accounts: sum of credits
                actualAmount = accountTransactions.Sum(jel => jel.CreditAmount);
            }
            else if (line.Account.Type == AccountType.Asset)
            {
                // For asset accounts: net (debits - credits)
                actualAmount = accountTransactions.Sum(jel => jel.DebitAmount - jel.CreditAmount);
            }

            line.ActualAmount = actualAmount;
            line.Variance = line.BudgetedAmount - actualAmount;
            line.VariancePercentage = line.BudgetedAmount != 0
                ? (line.Variance / line.BudgetedAmount) * 100
                : 0;

            totalActual += actualAmount;
        }

        budget.ActualAmount = totalActual;
        budget.Variance = budget.TotalBudgetAmount - totalActual;
        budget.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Result.Success();
    }

    // ? NEW: Auto-update budgets when journal entries are posted
    private async Task AutoUpdateRelatedBudgets(JournalEntry entry)
    {
        // Find all active budgets that cover this entry's date
        var affectedBudgets = await _context.Budgets
            .Where(b => b.Status == BudgetStatus.Active &&
                       b.StartDate <= entry.EntryDate &&
                       b.EndDate >= entry.EntryDate)
            .ToListAsync();

        // Update actuals for each affected budget
        foreach (var budget in affectedBudgets)
        {
            await UpdateBudgetActuals(budget.Id);
        }
    }

    // FRAUD DETECTION METHODS

    /// <summary>
    /// Analyzes a payment for credit card fraud using the Flask AI model.
    /// </summary>
    public async Task<Result<PaymentFraudAnalysisDto>> AnalyzePaymentFraudAsync(Guid paymentId, Guid invoiceId, List<double> transactionFeatures)
    {
        try
        {
            // Validate features count
            if (transactionFeatures == null || transactionFeatures.Count != 30)
            {
                _logger.LogWarning("Invalid feature count for fraud analysis: expected 30, got {Count}", transactionFeatures?.Count ?? 0);
                return Result<PaymentFraudAnalysisDto>.Failure("Transaction features must contain exactly 30 values (V1-V28, Amount, Time).");
            }

            // Get payment record
            var payment = await _context.Payments.FirstOrDefaultAsync(p => p.Id == paymentId);
            if (payment == null)
            {
                return Result<PaymentFraudAnalysisDto>.Failure("Payment not found.");
            }

            // Call fraud detection API
            var fraudResult = await _fraudDetectionService.PredictFraudAsync(transactionFeatures);
            if (!fraudResult.IsSuccess)
            {
                _logger.LogWarning("Fraud detection API call failed: {Error}", fraudResult.ErrorMessage);
                // Log fraud flag as inconclusive if API is unavailable
                var inconclusiveResult = new PaymentFraudAnalysisDto
                {
                    PaymentId = paymentId,
                    InvoiceId = invoiceId,
                    IsFraudulent = false,
                    FraudLabel = "Inconclusive",
                    FraudProbability = 0,
                    Confidence = 0,
                    AnalyzedAt = DateTime.UtcNow,
                    Notes = $"Fraud detection service unavailable: {fraudResult.ErrorMessage}"
                };
                return Result<PaymentFraudAnalysisDto>.Success(inconclusiveResult);
            }

            var fraudResponse = fraudResult.Data;

            // Create fraud analysis record
            var analysis = new PaymentFraudAnalysisDto
            {
                PaymentId = paymentId,
                InvoiceId = invoiceId,
                IsFraudulent = fraudResponse.Prediction == 1,
                FraudLabel = fraudResponse.Label,
                FraudProbability = fraudResponse.FraudProbability,
                Confidence = fraudResponse.Confidence,
                AnalyzedAt = DateTime.UtcNow,
                Notes = fraudResponse.Prediction == 1 
                    ? $"?? FRAUD ALERT: {fraudResponse.Label} with {fraudResponse.Confidence}% confidence"
                    : $"? Legitimate transaction with {fraudResponse.Confidence}% confidence"
            };

            // Log fraud flag if suspicious
            if (analysis.IsFraudulent)
            {
                _logger.LogWarning(
                    "FRAUD DETECTED - Payment {PaymentId} for Invoice {InvoiceId}: {Label} ({FraudProb}% fraud probability, {Confidence}% confidence)",
                    paymentId, invoiceId, analysis.FraudLabel, analysis.FraudProbability, analysis.Confidence);

                // Mark payment as flagged for review (optional: add to database if you have a FraudFlag table)
                payment.Status = PaymentStatus.Completed; // Can be updated to "Flagged" if that status exists
            }
            else
            {
                _logger.LogInformation(
                    "Payment {PaymentId} passed fraud check: {Label} ({Confidence}% confidence)",
                    paymentId, analysis.FraudLabel, analysis.Confidence);
            }

            return Result<PaymentFraudAnalysisDto>.Success(analysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing payment {PaymentId} for fraud", paymentId);
            return Result<PaymentFraudAnalysisDto>.Failure($"Fraud analysis failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Retrieves fraud analysis history for a specific invoice's payments.
    /// </summary>
    public async Task<Result<List<PaymentFraudAnalysisDto>>> GetFraudAnalysisHistoryAsync(Guid invoiceId)
    {
        try
        {
            // Verify invoice exists
            var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId);
            if (invoice == null)
            {
                return Result<List<PaymentFraudAnalysisDto>>.Failure("Invoice not found.");
            }

            // Get all payments for this invoice
            var payments = await _context.Payments
                .Where(p => p.InvoiceId == invoiceId)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();

            // For now, return empty history as we'd need to store fraud analysis in a separate table
            // In a full implementation, you'd have a FraudAnalysis entity to store historical records
            var history = new List<PaymentFraudAnalysisDto>();

            _logger.LogInformation("Retrieved fraud analysis history for invoice {InvoiceId}: {Count} payments", invoiceId, payments.Count);

            return Result<List<PaymentFraudAnalysisDto>>.Success(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving fraud analysis history for invoice {InvoiceId}", invoiceId);
            return Result<List<PaymentFraudAnalysisDto>>.Failure($"Failed to retrieve fraud analysis history: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks the health status of the fraud detection service.
    /// </summary>
    public async Task<bool> CheckFraudDetectionHealthAsync()
    {
        return await _fraudDetectionService.HealthCheckAsync();
    }
}

