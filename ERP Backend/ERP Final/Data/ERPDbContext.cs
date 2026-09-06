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
/// Represents the e r p db context domain model.
/// </summary>
public class ERPDbContext : DbContext
{
    public ERPDbContext(DbContextOptions<ERPDbContext> options) : base(options) { }

    // Module 1: Users & Roles
    public DbSet<User> Users { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<UserRole> UserRoles { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }

    // Module 2: HR & Payroll
    public DbSet<Employee> Employees { get; set; }
    public DbSet<Attendance> Attendances { get; set; }
    public DbSet<Leave> Leaves { get; set; }
    public DbSet<Payroll> Payrolls { get; set; }
    public DbSet<Document> Documents { get; set; }

    // Module 2.1: ATS Recruitment
    public DbSet<JobPosting> JobPostings { get; set; }
    public DbSet<Candidate> Candidates { get; set; }
    public DbSet<Interview> Interviews { get; set; }
    public DbSet<ResumeMatchResult> ResumeMatchResults { get; set; }
    public DbSet<ResumeScreeningBatch> ResumeScreeningBatches { get; set; }

    // Module 3: Finance & Accounting
    public DbSet<Account> Accounts { get; set; }
    public DbSet<JournalEntry> JournalEntries { get; set; }
    public DbSet<JournalEntryLine> JournalEntryLines { get; set; }
    public DbSet<Invoice> Invoices { get; set; }
    public DbSet<InvoiceItem> InvoiceItems { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<Expense> Expenses { get; set; }
    public DbSet<Budget> Budgets { get; set; }
    public DbSet<BudgetLine> BudgetLines { get; set; }

    // Module 4: Inventory & Stock
    public DbSet<Product> Products { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Warehouse> Warehouses { get; set; }
    public DbSet<Stock> Stocks { get; set; }
    public DbSet<StockMovement> StockMovements { get; set; }

    // Module 5: Procurement
    public DbSet<Supplier> Suppliers { get; set; }
    public DbSet<PurchaseRequisition> PurchaseRequisitions { get; set; }
    public DbSet<PurchaseRequisitionItem> PurchaseRequisitionItems { get; set; }
    public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
    public DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }
    public DbSet<GoodsReceipt> GoodsReceipts { get; set; }
    public DbSet<GoodsReceiptItem> GoodsReceiptItems { get; set; }

    // Module 6: Sales & CRM
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Lead> Leads { get; set; }
    public DbSet<Opportunity> Opportunities { get; set; }
    public DbSet<SalesQuotation> SalesQuotations { get; set; }
    public DbSet<SalesQuotationItem> SalesQuotationItems { get; set; }
    public DbSet<SalesOrder> SalesOrders { get; set; }
    public DbSet<SalesOrderItem> SalesOrderItems { get; set; }
    public DbSet<Delivery> Deliveries { get; set; }
    public DbSet<DeliveryItem> DeliveryItems { get; set; }

    // Module 7: Project Management
    public DbSet<Project> Projects { get; set; }
    public DbSet<ProjectMember> ProjectMembers { get; set; }
    public DbSet<ProjectTask> ProjectTasks { get; set; }
    public DbSet<TimeEntry> TimeEntries { get; set; }
    public DbSet<TaskComment> TaskComments { get; set; }

    // Module 8: Reporting
    public DbSet<Report> Reports { get; set; }
    public DbSet<Dashboard> Dashboards { get; set; }
    public DbSet<DashboardWidget> DashboardWidgets { get; set; }
    public DbSet<ReportSchedule> ReportSchedules { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // In OnModelCreating:

        // ? Invoice search optimization
        modelBuilder.Entity<Invoice>()
            .HasIndex(i => new { i.CustomerId, i.Status, i.InvoiceDate });

        // ? Sales Order search optimization
        modelBuilder.Entity<SalesOrder>()
            .HasIndex(so => new { so.CustomerId, so.Status, so.OrderDate });

        // ? Purchase Order search optimization
        modelBuilder.Entity<PurchaseOrder>()
            .HasIndex(po => new { po.SupplierId, po.Status, po.OrderDate });

        // ? Attendance lookup optimization
        modelBuilder.Entity<Attendance>()
            .HasIndex(a => new { a.EmployeeId, a.Date });

        // ? Leave query optimization
        modelBuilder.Entity<Leave>()
            .HasIndex(l => new { l.EmployeeId, l.Status, l.StartDate });

        // ? Payment lookup optimization
        modelBuilder.Entity<Payment>()
            .HasIndex(p => new { p.InvoiceId, p.Status, p.PaymentDate });

        // ? Time entry reports optimization
        modelBuilder.Entity<TimeEntry>()
            .HasIndex(te => new { te.ProjectId, te.EmployeeId, te.Date });

        // ? Stock movement tracking
        modelBuilder.Entity<StockMovement>()
            .HasIndex(sm => new { sm.ProductId, sm.WarehouseId, sm.MovementDate });
        // Configure indexes and relationships
        // ? ADD THESE UNIQUE CONSTRAINTS:
        modelBuilder.Entity<Invoice>()
            .HasIndex(i => i.InvoiceNumber)
            .IsUnique();

        modelBuilder.Entity<SalesOrder>()
            .HasIndex(so => so.OrderNumber)
            .IsUnique();

        modelBuilder.Entity<PurchaseOrder>()
            .HasIndex(po => po.OrderNumber)
            .IsUnique();

        modelBuilder.Entity<Payment>()
            .HasIndex(p => p.PaymentNumber)
            .IsUnique();

        modelBuilder.Entity<Delivery>()
            .HasIndex(d => d.DeliveryNumber)
            .IsUnique();

        modelBuilder.Entity<GoodsReceipt>()
            .HasIndex(gr => gr.ReceiptNumber)
            .IsUnique();

        modelBuilder.Entity<StockMovement>()
            .HasIndex(sm => sm.MovementNumber)
            .IsUnique();

        // Users
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        // Employees
        modelBuilder.Entity<Employee>()
            .HasIndex(e => e.EmployeeCode)
            .IsUnique();

        // Products
        modelBuilder.Entity<Product>()
            .HasIndex(p => p.ProductCode)
            .IsUnique();

        modelBuilder.Entity<Product>()
            .HasIndex(p => p.SKU);

        // Accounts
        // ? REMOVED unique constraint to allow reusing codes after soft delete
        // If you need uniqueness, add a composite unique index on (AccountCode, IsDeleted)
        modelBuilder.Entity<Account>()
            .HasIndex(a => a.AccountCode);  // Non-unique index for performance

        // Suppliers
        modelBuilder.Entity<Supplier>()
            .HasIndex(s => s.SupplierCode)
            .IsUnique();

        // Customers
        modelBuilder.Entity<Customer>()
            .HasIndex(c => c.CustomerCode)
            .IsUnique();

        // Warehouses
        modelBuilder.Entity<Warehouse>()
            .HasIndex(w => w.Code)
            .IsUnique();

        // Projects
        modelBuilder.Entity<Project>()
            .HasIndex(p => p.ProjectCode)
            .IsUnique();

        // Stock composite index
        modelBuilder.Entity<Stock>()
            .HasIndex(s => new { s.ProductId, s.WarehouseId })
            .IsUnique();

        // Budget unique index
        modelBuilder.Entity<Budget>()
            .HasIndex(b => b.BudgetNumber)
            .IsUnique();

        // Budget lines relationship
        modelBuilder.Entity<BudgetLine>()
            .HasOne(bl => bl.Budget)
            .WithMany(b => b.BudgetLines)
            .HasForeignKey(bl => bl.BudgetId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BudgetLine>()
            .HasOne(bl => bl.Account)
            .WithMany()
            .HasForeignKey(bl => bl.AccountId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<GoodsReceiptItem>()
            .HasOne(gri => gri.GoodsReceipt)
            .WithMany(gr => gr.Items)
            .HasForeignKey(gri => gri.GoodsReceiptId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GoodsReceiptItem>()
            .HasOne(gri => gri.PurchaseOrderItem)
            .WithMany()
            .HasForeignKey(gri => gri.PurchaseOrderItemId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<GoodsReceiptItem>()
            .HasOne(gri => gri.Product)
            .WithMany()
            .HasForeignKey(gri => gri.ProductId)
            .OnDelete(DeleteBehavior.NoAction);

        // Configure cascade delete behavior for DeliveryItem
        modelBuilder.Entity<DeliveryItem>()
            .HasOne(di => di.Delivery)
            .WithMany(d => d.Items)
            .HasForeignKey(di => di.DeliveryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DeliveryItem>()
            .HasOne(di => di.SalesOrderItem)
            .WithMany()
            .HasForeignKey(di => di.SalesOrderItemId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<DeliveryItem>()
            .HasOne(di => di.Product)
            .WithMany()
            .HasForeignKey(di => di.ProductId)
            .OnDelete(DeleteBehavior.NoAction);

        // Configure cascade delete behavior for InvoiceItem
        modelBuilder.Entity<InvoiceItem>()
            .HasOne(ii => ii.Invoice)
            .WithMany(i => i.Items)
            .HasForeignKey(ii => ii.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<InvoiceItem>()
            .HasOne(ii => ii.Product)
            .WithMany()
            .HasForeignKey(ii => ii.ProductId)
            .OnDelete(DeleteBehavior.NoAction);

        // Configure cascade delete behavior for SalesOrderItem
        modelBuilder.Entity<SalesOrderItem>()
            .HasOne(soi => soi.SalesOrder)
            .WithMany(so => so.Items)
            .HasForeignKey(soi => soi.SalesOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SalesOrderItem>()
            .HasOne(soi => soi.Product)
            .WithMany()
            .HasForeignKey(soi => soi.ProductId)
            .OnDelete(DeleteBehavior.NoAction);

        // Configure cascade delete behavior for PurchaseOrderItem
        modelBuilder.Entity<PurchaseOrderItem>()
            .HasOne(poi => poi.PurchaseOrder)
            .WithMany(po => po.Items)
            .HasForeignKey(poi => poi.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PurchaseOrderItem>()
            .HasOne(poi => poi.Product)
            .WithMany()
            .HasForeignKey(poi => poi.ProductId)
            .OnDelete(DeleteBehavior.NoAction);


        // Configure decimal precision globally
        foreach (var property in modelBuilder.Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            if (property.GetColumnType() == null)
            {
                property.SetColumnType("decimal(18,2)");
            }
        }

        // Soft delete filter
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .HasQueryFilter(GenerateSoftDeleteFilter(entityType.ClrType));
            }
        }
    }

    private static LambdaExpression GenerateSoftDeleteFilter(Type entityType)
    {
        var parameter = Expression.Parameter(entityType, "e");
        var property = Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
        var condition = Expression.Equal(property, Expression.Constant(false));
        return Expression.Lambda(condition, parameter);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries<BaseEntity>();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}




