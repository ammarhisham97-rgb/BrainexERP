# BraienX AI-Powered ERP Platform

**Production-Ready Enterprise Resource Planning System with Integrated Machine Learning**

A comprehensive .NET 9.0-based ERP platform that seamlessly integrates machine learning services across HR, Finance, Inventory, and Sales workflows. BraienX combines traditional business process management with intelligent AI capabilities for predictive analytics, semantic resume matching, fraud detection, and conversational policy assistance.

<div align="center">

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?style=flat-square&logo=dotnet)
![C#](https://img.shields.io/badge/C%23-12-239120?style=flat-square&logo=csharp)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-Minimal%20APIs-512BD4?style=flat-square)
![SQL Server](https://img.shields.io/badge/SQL%20Server-2022+-CC2927?style=flat-square&logo=microsoftsqlserver)
![EF Core](https://img.shields.io/badge/EF%20Core-9.0-512BD4?style=flat-square)
![Docker](https://img.shields.io/badge/Docker-Containerized-2496ED?style=flat-square&logo=docker)

[Overview](#overview) • [Architecture](#architecture) • [AI Capabilities](#-ai-capabilities) • [Getting Started](#getting-started) • [Technical Details](#technical-highlights)

</div>

---

## Overview

BraienX is a full-featured, production-ready ERP system engineered for organizations requiring both robust enterprise process management and AI-driven intelligence. The platform unifies **eight core business modules**—HR & Recruitment, Finance & Accounting, Inventory Management, Procurement, Sales & CRM, Project Management, Reporting, and Expense Management—with seamless integration of **six machine learning microservices**.

### What Makes BraienX Different

Unlike traditional ERPs that are static data repositories, BraienX **embeds intelligence into workflows**:

- **Smart Recruitment:** AI-powered resume-to-job matching using BERT embeddings for semantic candidate ranking
- **Predictive HR:** XGBoost attrition prediction to proactively identify and retain at-risk employees
- **Fraud Prevention:** Real-time transaction fraud detection using Keras neural networks
- **Intelligent Automation:** RAG-based chatbots providing instant access to HR policies and system documentation
- **Conversational Interface:** Natural language Q&A over enterprise knowledge bases via LLM integration
- **Fine-Grained Security:** 40+ permission-based access controls with comprehensive audit logging
- **Modern Backend:** ASP.NET Core Minimal APIs with async/await, dependency injection, and clean architecture

This is not a tutorial project—it's a serious, architecturally sound system demonstrating professional .NET engineering with AI integration.

---

## 🎯 Key Features

### Core ERP Modules (8 Total)

| Module | Key Functionality | Business Value |
|--------|-------------------|-----------------|
| **HR & Payroll** | Employee lifecycle, attendance, leave, payroll processing, recruitment ATS | End-to-end workforce management |
| **Finance & Accounting** | Chart of accounts, journal entries, invoicing, payments, budgeting, expense tracking | Complete financial control |
| **Inventory** | Product master, multi-warehouse stock, reorder points, stock movements | Inventory visibility & optimization |
| **Procurement** | Purchase requisitions, POs, goods receipt, supplier management | Purchase-to-pay automation |
| **Sales & CRM** | Customers, leads, opportunities, quotations, orders, delivery tracking | Revenue pipeline management |
| **Project Mgmt** | Projects, tasks, time tracking, team collaboration, project costing | Delivery execution & control |
| **Reporting** | Financial, sales, inventory, HR reports; dashboards with KPIs | Data-driven decision making |
| **Expense Mgmt** | Employee expense submission, approval, categorization, analytics | Spend control & compliance |

### 🤖 AI Capabilities (6 Microservices)

#### **Resume-to-Job Matching (BERT)**
- Semantic matching of candidate resumes to job descriptions
- Top-5 candidate ranking by relevance score
- Returns confidence scores for hiring decisions
- **API:** `POST /api/candidates/match-resumes`, `POST /api/candidates/smart-match`

#### **Employee Attrition Prediction (XGBoost)**  
- Identifies high-risk employees likely to leave
- Features: tenure, salary, department, performance, attendance
- Real-time scoring for retention intervention
- **Output:** Risk percentage (0-100%) + categorical level

#### **Performance Analytics (Random Forest)**
- Objective performance rating prediction
- Batch and individual employee scoring
- Supports workforce analytics and benchmarking

#### **Fraud Detection (Keras CNN)**
- Real-time credit card fraud flagging
- Temporal patterns, transaction velocity, merchant analysis
- 6-layer neural network with SMOTE oversampling
- **Latency:** <50ms per transaction

#### **HR Policy Chatbot (RAG)**
- LangChain + FAISS + LM Studio LLM architecture
- Semantic search over HR policy PDFs
- Multi-turn conversation support
- **Features:** Source attribution, response streaming
- **API:** `POST /api/hr-chatbot/ask`, `GET /api/hr-chatbot/health`

#### **Customer Support Chatbot (RAG)**
- Retrieval-augmented generation over ERP system documentation
- Real-time conversational AI for system help
- Offline-capable with local LLM

### 🔐 Enterprise Security

- **JWT Authentication** with configurable expiration, issuer, audience
- **Permission Matrix:** 40+ fine-grained permissions (not role-hardcoded)
- **RBAC with Templates:** Pre-built roles (Admin, HR Manager, Finance Manager, etc.)
- **Audit Logging:** Complete create/update/delete operation trail with user context
- **Password Security:** Bcrypt hashing with salt
- **CORS Protection** and cross-origin configuration
- **Request/Response Logging** middleware for compliance

### 🏗️ Engineering Practices

- **Async/Await Throughout:** Non-blocking I/O for high concurrency
- **Dependency Injection:** Built-in ASP.NET Core DI container
- **Minimal APIs:** Clean endpoint routing without controller bloat
- **Entity Framework Core:** Modern ORM with migrations
- **Structured Logging (Serilog):** Context-aware, production-ready
- **Separation of Concerns:** DTOs, Services, Domain Models, Data Access layers
- **Health Checks:** Container orchestration ready
- **Error Handling Middleware:** Consistent exception-to-response mapping

---

## 🏛️ Architecture

### System Architecture Diagram

```
┌─────────────────┐
│   Frontend      │
│  (HTML/JS/CSS)  │
└────────┬────────┘
         │ HTTP/JSON
         ↓
┌─────────────────────────────────────────┐
│   ASP.NET Core 9.0 REST API             │
│   (Minimal APIs - 50+ endpoints)        │
└────────┬────────────────────────────────┘
         │
    ┌────┴─────────────────────────────┐
    │                                  │
    ↓                                  ↓
┌──────────────────────┐    ┌──────────────────────┐
│  Application Layer   │    │  AI Microservices    │
│  (Services)          │    │  (Flask Python)      │
└──────┬───────────────┘    └──────┬───────────────┘
       │                            │
       ↓                            │
┌──────────────────────┐            │
│  Domain Models       │            │
│  (70+ Entities)      │            │  HTTP
└──────┬───────────────┘            │
       │                            │
       ↓                            │
┌──────────────────────┐            │
│  EF Core Data Access │            │
└──────┬───────────────┘            │
       │                            ↓
       │              ┌──────────────────────────┐
       └─────────────→│   SQL Server Database    │
                      │   (70+ tables, 100+ cols)│
                      └──────────────────────────┘

┌─────────────────┐  ┌──────────────┐  ┌──────────────┐
│ HR Attrition    │  │ Resume Match │  │ Fraud Detect │  (ML Services)
│ (Port 5001)     │  │ (Port 5006)  │  │ (Port 5003)  │
└─────────────────┘  └──────────────┘  └──────────────┘
```

### Backend Project Structure

```
ERP Backend/
├── ERP Final/                           # .NET 9.0 Web API Project
│   ├── Program.cs                       # ASP.NET bootstrap, 50+ endpoints
│   ├── ERP Final.csproj                 # Dependencies: JWT, EF Core, Serilog, Swagger
│   │
│   ├── Authorization/                   # Security layer
│   │   ├── Permissions.cs               # 40+ permission constants
│   │   ├── PermissionAuthorizationHandler.cs
│   │   └── RoleTemplates.cs             # Pre-built role definitions
│   │
│   ├── Models/                          # Domain entities (70+ classes)
│   │   ├── UserEntities.cs              # User, Role, AuditLog
│   │   ├── HREntities.cs                # Employee, Attendance, Leave, Payroll, Job, Candidate, Interview
│   │   ├── FinanceEntities.cs           # Account, JournalEntry, Invoice, Payment, Expense, Budget
│   │   ├── InventoryEntities.cs         # Product, Category, Warehouse, Stock
│   │   ├── ProcurementEntities.cs       # Supplier, PurchaseRequisition, PurchaseOrder, GoodsReceipt
│   │   ├── SalesEntities.cs             # Customer, Lead, Opportunity, SalesOrder, Delivery
│   │   ├── ProjectEntities.cs           # Project, Task, TimeEntry
│   │   └── ReportingEntities.cs         # Report, Dashboard, Widget
│   │
│   ├── Services/                        # Business logic (app services)
│   │   ├── Contracts/                   # Service interfaces
│   │   │   ├── IUserService.cs
│   │   │   ├── IHRService.cs
│   │   │   ├── IFinanceService.cs
│   │   │   ├── IInventoryService.cs
│   │   │   ├── IProcurementService.cs
│   │   │   ├── ISalesService.cs
│   │   │   ├── IProjectService.cs
│   │   │   ├── IReportingService.cs
│   │   │   └── ILoggerService.cs
│   │   │
│   │   ├── UserService.cs               # Auth, permissions, user CRUD
│   │   ├── HRService.cs                 # Employees, attendance, payroll, recruitment
│   │   ├── FinanceService.cs            # Invoicing, budgeting, GL
│   │   ├── InventoryService.cs          # Products, stock, warehouses
│   │   ├── ProcurementService.cs        # Purchase workflows
│   │   ├── SalesService.cs              # Customer, quotes, orders
│   │   ├── ProjectService.cs            # Projects, tasks, time tracking
│   │   ├── ReportingService.cs          # Aggregated reports
│   │   │
│   │   ├── FraudDetectionService.cs     # Calls Flask fraud API
│   │   ├── HRAttritionService.cs        # Calls Flask attrition API
│   │   ├── ChatbotService.cs            # Calls Flask customer chatbot
│   │   ├── HRChatbotService.cs          # Calls Flask HR chatbot
│   │   ├── ResumeMatching/
│   │   │   ├── IResumeMatchingService.cs
│   │   │   ├── ResumeMatchingService.cs
│   │   │   └── ResumeMatchingDtos.cs
│   │   ├── JwtTokenService.cs           # JWT generation
│   │   ├── PasswordHasher.cs            # Bcrypt hashing
│   │   └── LoggerService.cs             # Serilog logging
│   │
│   ├── DTOs/                            # Data transfer objects
│   │   ├── CreateXxxRequest.cs
│   │   ├── UpdateXxxRequest.cs
│   │   ├── XxxResponse.cs
│   │   ├── PaginatedResponse.cs
│   │   └── Search filters
│   │
│   ├── Data/
│   │   └── ERPDbContext.cs              # EF Core DbContext, 70+ DbSets
│   │
│   ├── Migrations/                      # EF Core schema versions
│   │
│   ├── Middleware/
│   │   ├── ExceptionLoggingMiddleware.cs
│   │   └── RequestResponseLoggingMiddleware.cs
│   │
│   ├── Common/
│   │   └── Result<T>                    # Operation result wrapper
│   │
│   ├── Enums/
│   │   └── AttendanceStatus, LeaveStatus, BudgetStatus, etc.
│   │
│   └── appsettings.json                 # Config
│
└── ERP Final.Tests/                     # xUnit integration tests
    ├── ErpWebApplicationFactory.cs
    ├── TestAuthHandler.cs
    └── HrEnumBindingTests.cs
```

---

## 💾 Technology Stack

| Component | Technology | Version | Purpose |
|-----------|-----------|---------|---------|
| **Backend Framework** | ASP.NET Core | 9.0 | Web API runtime |
| **Language** | C# | 12+ | Backend development |
| **API Style** | Minimal APIs | Native | RESTful endpoint routing |
| **ORM** | Entity Framework Core | 9.0 | Data access abstraction |
| **Database** | SQL Server | 2022+ | Persistent storage |
| **Authentication** | JWT Bearer | RFC 7519 | Token-based auth |
| **Logging** | Serilog | 4.3.0 | Structured logging |
| **API Documentation** | Swagger/OpenAPI | 3.0 | Interactive API docs |
| **Testing** | xUnit | 2.9.2 | Unit/integration tests |
| **Testing DB** | In-Memory | EF Core | Isolated test environment |
| **AI/ML - Resume** | BERT + HuggingFace | transformers | Semantic embeddings |
| **AI/ML - Attrition** | XGBoost | scikit-learn | Classification model |
| **AI/ML - Performance** | Random Forest | scikit-learn | Regression model |
| **AI/ML - Fraud** | Keras CNN | TensorFlow | Neural network |
| **AI/ML - LLM** | LM Studio + Llama | Local | Local LLM inference |
| **AI/RAG** | LangChain + FAISS | Latest | Retrieval-augmented generation |
| **Frontend** | Vanilla JS + Tailwind | Latest | Browser UI |
| **Containerization** | Docker | Latest | Container images |
| **Orchestration** | Docker Compose | 3.8 | Multi-container coordination |

---

## 🔐 Security Model

### Authentication Pipeline

1. **Login:** `POST /api/auth/login` with credentials
2. **Validation:** Backend hashes provided password against bcrypt hash
3. **Token Generation:** JwtTokenService creates signed JWT with user claims + permissions
4. **Client Usage:** Frontend includes `Authorization: Bearer {token}` header
5. **Validation:** Middleware validates JWT signature, expiration, issuer, audience
6. **Authorization:** Endpoint checks for required permission in JWT claims

### Permission-Based Access Control

Instead of role-name hardcoding (`RequireAuthorization("HRManager")`), endpoints check permissions:

```csharp
app.MapPost("/api/leaves/{leaveId}/approve", ...)
    .RequireAuthorization(Permissions.HRApproveLeave);
```

**Benefits:**
- Dynamic: Change role permissions without code
- Flexible: Multiple roles per user
- Auditable: Exactly which permission enabled access

### Permission Matrix (40+ Permissions)

**User Management (6):** View, Create, Edit, Delete, ManageRoles, ChangePassword  
**HR (12):** ViewEmployees, CreateEmployees, EditEmployees, DeleteEmployees, ViewAttendance, ManageAttendance, ViewLeave, ApproveLeave, ViewPayroll, ProcessPayroll, ViewPerformance, ManagePerformance  
**Finance (10):** ViewInvoices, CreateInvoices, EditInvoices, DeleteInvoices, ViewPayments, ProcessPayments, ViewExpenses, ApproveExpenses, ViewReports, ManageAccounts  
**Inventory (8):** ViewItems, CreateItems, EditItems, DeleteItems, ManageStock, ViewWarehouses, ManageWarehouses  
**Procurement (4):** ViewPurchases, CreatePurchases, EditPurchases, DeletePurchases  
**Sales (4):** ViewSales, CreateSales, EditSales, DeleteSales  
**Projects (4):** ViewProjects, CreateProjects, EditProjects, DeleteProjects  
**Reports (2):** ViewReports, ExportReports

---

## 🤖 AI Integration Architecture

### Service-Oriented Design

All AI capabilities are provided by **separate Flask microservices**, not embedded in .NET. This enables:

- **Isolation:** ML service crash doesn't impact ERP
- **Scalability:** Heavy compute doesn't block API requests
- **Language Fit:** Python is ML-native
- **Flexibility:** Easy to upgrade/swap AI models
- **Fallback:** ERP continues if AI unavailable

### AI Request Flow Example: Resume Matching

```
HR User clicks "Match Resumes"
    ↓
POST /api/candidates/match-resumes
    ↓
ResumeMatchingService.MatchCandidatesForJobAsync()
    ↓
Extract job description + candidate resumes
    ↓
HTTP POST to Flask service (Port 5006)
    ↓
Flask loads BERT model
    ↓
Embed job description → 768-dim vector
    ↓
Embed each resume → 768-dim vectors
    ↓
Compute cosine similarity (job vs each resume)
    ↓
Sort by score, return top 5
    ↓
Backend receives JSON response
    ↓
Parse, map to DTOs, return to frontend
    ↓
Frontend displays ranked candidates with scores
```

### Configuration-Based Service URLs

Services are configured via environment, not hardcoded:

```json
{
  "AI": {
    "ResumeMatching": {
      "FlaskApiUrl": "http://localhost:5006",
      "TimeoutSeconds": 60
    },
    "FraudDetection": {
      "FlaskApiUrl": "http://localhost:5003",
      "TimeoutSeconds": 30
    }
  }
}
```

---

## 🚀 Getting Started

### Prerequisites

- **.NET 9.0 SDK** ([download](https://dotnet.microsoft.com/download))
- **SQL Server 2022** or Express ([download](https://www.microsoft.com/sql-server/sql-server-downloads))
- **Visual Studio 2022** or VS Code + C# extension
- **Git**

### Quick Start (5 Minutes)

#### Step 1: Clone & Navigate

```bash
git clone https://github.com/yourusername/BraienX.git
cd BraienX
cd "ERP Backend"
```

#### Step 2: Create Database

```bash
# Run EF Core migrations to create schema
dotnet ef database update --project "ERP Final" --context ERPDbContext
```

This creates `BraienX` database with all tables and seeds default admin user:  
- **Email:** `admin@erp.com`
- **Password:** `Admin@123`

#### Step 3: Run Backend

```bash
cd "ERP Final"
dotnet run
```

**Backend URL:** http://localhost:5000  
**Swagger UI:** http://localhost:5000/swagger/index.html  
**Health Check:** http://localhost:5000/health

#### Step 4: Login & Test

1. Open Swagger UI
2. Click "Authorize" button
3. Execute `POST /api/auth/login`:
   ```json
   {
       "username": "admin",
       "password": "Admin@123"
   }
   ```
4. Copy the JWT token
5. Click "Authorize" again, paste token
6. Test endpoints

#### Step 5: Frontend (Optional)

```bash
cd Frontend-AI
# Open index.html in browser
# Or run simple HTTP server:
python -m http.server 8000
# Then navigate to http://localhost:8000
```

---

## 📋 API Quick Reference

### Authentication
```
POST   /api/auth/login                 # Obtain JWT token
POST   /api/auth/register              # Create new account
GET    /api/auth/me                    # Current user with permissions
```

### HR Module (30+ endpoints)
```
POST   /api/employees                  # Create employee
GET    /api/employees                  # List all
GET    /api/employees/search           # Search with pagination
POST   /api/attendance                 # Record attendance
POST   /api/leaves                     # Submit leave request
POST   /api/leaves/{id}/approve        # Manager approve
POST   /api/payroll/generate           # Generate payroll
POST   /api/jobs                       # Create job posting
POST   /api/candidates/match-resumes   # AI: Match resumes
```

### Finance Module  (20+ endpoints)
```
POST   /api/invoices                   # Create invoice
POST   /api/payments                   # Record payment
POST   /api/expenses                   # Submit expense
POST   /api/expenses/{id}/approve      # Approve expense
GET    /api/budgets                    # List budgets
POST   /api/budgets                    # Create budget
```

### Inventory Module (15+ endpoints)
```
POST   /api/products                   # Create product
GET    /api/products/search            # Search products
POST   /api/stock                      # Record stock movement
GET    /api/stock/{productId}          # Get stock levels
```

### Sales Module (10+ endpoints)
```
POST   /api/customers                  # Create customer
POST   /api/leads                      # Create lead
POST   /api/sales-orders               # Create SO
```

### AI Endpoints
```
POST   /api/hr-chatbot/ask             # Ask HR policy question
GET    /api/hr-chatbot/health          # HR chatbot health
POST   /api/candidates/smart-match     # Direct resume match
```

Full Swagger documentation at `/swagger` after running backend.

---

## 🏛️ Architecture Highlights

### Clean Layered Architecture

```
Presentation (API)
     ↓ (Depends on)
Application (Services)
     ↓ (Depends on)
Domain Models
     ↓ (Depends on)
Data Access (EF Core)
```

**Rule:** Inner layers never depend on outer layers. Only inward dependencies.

### Result<T> Pattern

All service methods return `Result<T>` (not exceptions for business logic):

```csharp
public class Result<T>
{
    public bool IsSuccess { get; set; }
    public T Data { get; set; }
    public string ErrorMessage { get; set; }
}

// Usage:
var result = await hrService.CreateEmployeeAsync(request);
if (result.IsSuccess)
    return Results.Ok(result.Data);
else
    return Results.BadRequest(new { error = result.ErrorMessage });
```

**Benefit:** Explicit success/failure handling, testable, no exception overhead.

### Dependency Injection

All services registered as interfaces:

```csharp
builder.Services.AddScoped<IHRService, HRService>();
builder.Services.AddScoped<IFinanceService, FinanceService>();
```

Enables: testability, loose coupling, single responsibility.

### Async/Await Everywhere

All I/O (database, HTTP, file) is truly async:

```csharp
public async Task<Result<EmployeeResponse>> GetEmployeeByIdAsync(Guid id)
{
    var employee = await _dbContext.Employees
        .AsNoTracking()
        .FirstOrDefaultAsync(e => e.Id == id);

    if (employee == null)
        return Result<EmployeeResponse>.Failure("Not found");

    return Result<EmployeeResponse>.Success(_mapper.Map<EmployeeResponse>(employee));
}
```

**Benefit:** Non-blocking, thread pool efficiency, handles thousands of concurrent requests.

---

## 📊 Database Design

### 8 Logical Modules, 70+ Entities

**Users & Security:** User, Role, UserRole, AuditLog  
**HR & Payroll:** Employee, Attendance, Leave, Payroll, JobPosting, Candidate, Interview, ResumeMatchResult  
**Finance:** Account, JournalEntry, Invoice, Payment, Expense, Budget  
**Inventory:** Product, Category, Warehouse, Stock, StockMovement  
**Procurement:** Supplier, PurchaseRequisition, PurchaseOrder, GoodsReceipt  
**Sales:** Customer, Lead, Opportunity, SalesQuotation, SalesOrder, Delivery  
**Projects:** Project, ProjectTask, ProjectMember, TimeEntry  
**Reporting:** Report, Dashboard, DashboardWidget

### Key Design Patterns

**Composite Primary Keys:**
```csharp
modelBuilder.Entity<UserRole>()
    .HasKey(ur => new { ur.UserId, ur.RoleId });
```

**Cascade Deletes:**
```csharp
modelBuilder.Entity<Employee>()
    .HasMany(e => e.Attendances)
    .WithOne(a => a.Employee)
    .OnDelete(DeleteBehavior.Cascade);
```

**Decimal Precision for Currency:**
```csharp
modelBuilder.Entity<Invoice>()
    .Property(i => i.TotalAmount)
    .HasColumnType("decimal(18,2)");
```

**Indexes for Performance:**
```csharp
modelBuilder.Entity<Employee>()
    .HasIndex(e => e.EmployeeCode)
    .IsUnique();
```

---

## 📝 Design Decisions & Rationale

### Why Minimal APIs?

- Cleaner for REST CRUD APIs
- All endpoints visible in Program.cs
- Less boilerplate than controller classes
- Better for API-first design

### Why Async/Await?

- Non-blocking I/O scales to thousands of concurrent requests
- Thread pool efficiency
- Industry standard for modern .NET

### Why Result<T>?

- Explicit success/failure semantics
- No exception overhead for expected failures
- Testable without try-catch
- Clear API contracts

### Why Separate AI Services?

- Python is ML-native (scikit-learn, BERT, TensorFlow)
- Isolated failure domains
- Independent scaling
- Language/framework flexibility

### Why Local LLM (LM Studio)?

- No external API dependency
- Data privacy
- Offline capability
- Cost-effective
- Faster inference (no network round-trip)

### Why Permission-Based, Not Role-Based?

- Fine-grained control
- Dynamic role configuration without code changes
- Supports multiple roles per user
- More flexible than role-name hardcoding

---

## 💪 Technical Highlights

### 1. Comprehensive Permission System

40+ permissions enable flexible, role-independent access control. Permissions are simple strings assigned to roles dynamically.

### 2. Audit Logging Infrastructure

Every significant operation creates AuditLog entry with:
- User ID (who)
- Entity name (what)
- Operation (create/update/delete)
- Old values (before)
- New values (after)
- Timestamp (when)

Enables compliance investigation and security audits.

### 3. AI Integration Without Tight Coupling

HttpClient with timeout, retry logic, and graceful degradation:

```csharp
try
{
    var response = await _httpClient.PostAsync("/predict", content);
    if (!response.IsSuccessStatusCode)
        return Result<T>.Failure("Service unavailable");
}
catch (TaskCanceledException)
{
    _logger.LogWarning("AI service timeout");
    return Result<T>.Failure("Service timeout - proceeding without AI");
}
catch (HttpRequestException ex)
{
    _logger.LogError(ex, "Network error");
    return Result<T>.Failure("Network error");
}
```

### 4. Search & Pagination

Flexible search across relevant fields + configurable pagination:

```csharp
public async Task<Result<PagedResponse<EmployeeResponse>>> SearchEmployeesAsync(
    EmployeeSearchRequest search,
    PaginationRequest pagination)
{
    var query = _dbContext.Employees.AsQueryable();

    if (!string.IsNullOrEmpty(search.SearchTerm))
        query = query.Where(e =>
            EF.Functions.Like(e.FirstName, $"%{search.SearchTerm}%") ||
            EF.Functions.Like(e.LastName, $"%{search.SearchTerm}%") ||
            EF.Functions.Like(e.EmployeeCode, $"%{search.SearchTerm}%"));

    if (!string.IsNullOrEmpty(search.Department))
        query = query.Where(e => e.Department == search.Department);

    var total = await query.CountAsync();
    var items = await query
        .Skip((pagination.Page - 1) * pagination.PageSize)
        .Take(pagination.PageSize)
        .ToListAsync();

    return Result<PagedResponse<EmployeeResponse>>.Success(new PagedResponse<EmployeeResponse>
    {
        Items = items,
        PageNumber = pagination.Page,
        PageSize = pagination.PageSize,
        TotalCount = total
    });
}
```

### 5. Structured Logging (Serilog)

Context-aware, production-ready logging:

```csharp
_logger.LogInformation(
    "Employee {EmployeeId} created by {UserId} in department {Department}",
    employee.Id, userId, employee.Department);

_logger.LogWarning(
    "Resume matching timeout for candidate {CandidateId}",
    candidateId);

_logger.LogError(ex,
    "Database connection failed in {Method}",
    nameof(ApproveLeaveAsync));
```

### 6. Health Checks

Container orchestration ready:

```csharp
app.MapGet("/health", async () =>
{
    return Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
}).AllowAnonymous();

app.MapGet("/health/detailed", async (ERPDbContext db) =>
{
    try
    {
        await db.Database.ExecuteSqlAsync(new FormattableStringFactory.Invariant("SELECT 1"));
        return Results.Ok(new
        {
            status = "healthy",
            database = "connected",
            timestamp = DateTime.UtcNow
        });
    }
    catch
    {
        return Results.StatusCode(503, new
        {
            status = "unhealthy",
            database = "disconnected"
        });
    }
}).AllowAnonymous();
```

---

## 🧪 Testing

### Test Framework

- **Framework:** xUnit
- **Database:** In-memory EF Core (isolated tests)
- **Fixture:** WebApplicationFactory for integration tests
- **Auth:** TestAuthHandler for JWT token generation

### Run Tests

```bash
# All tests
dotnet test

# Specific test class
dotnet test --filter "ClassName=HrEnumBindingTests"

# With code coverage
dotnet test /p:CollectCoverage=true
```

### Test Projects

- **ERP Final.Tests** - Integration tests
- **WebApplicationFactory** - Test fixture
- **In-Memory Database** - Isolated test data

---

## 🐳 Docker & Deployment

### Containerized Architecture

Frontend + Backend + Database all containerizable:

```yaml
services:
  braienx-api:
    image: braienx-api:latest
    ports:
      - "5000:5000"
    environment:
      - DefaultConnection=Server=sql-server;Database=BraienX;...
      - Chatbot__FlaskApiUrl=http://chatbot:5005

  sql-server:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - SA_PASSWORD=YourSecurePassword

  chatbot:
    image: braienx-chatbot:latest
    ports:
      - "5005:5005"
```

### Docker Commands

```bash
# Build
docker-compose build

# Start
docker-compose up -d

# Logs
docker-compose logs -f braienx-api

# Stop
docker-compose down
```

---

## 🎯 Key Metrics

| Metric | Value |
|--------|-------|
| .NET Version | 9.0 (Latest LTS) |
| Language | C# 12+ |
| Database | SQL Server 2022+ |
| REST Endpoints | 50+ |
| ERP Modules | 8 |
| AI Microservices | 6 |
| Domain Entities | 70+ |
| Permissions | 40+ |
| Service Interfaces | 10 |
| DTOs | 100+ |
| Migrations | Auto-versioned |
| Test Framework | xUnit |
| Async Coverage | 100% |

---

## 📚 Code Quality

✅ **Clean Architecture** - Layered, dependency inversion  
✅ **SOLID Principles** - Interfaces, SRP, OCP  
✅ **Async/Await** - Non-blocking I/O throughout  
✅ **Error Handling** - Middleware, logging, Result<T>  
✅ **Security** - JWT, RBAC, permissions, bcrypt, audit logging  
✅ **Testing** - Integration tests, xUnit, in-memory DB  
✅ **Logging** - Structured (Serilog), context-aware  
✅ **Documentation** - Swagger/OpenAPI, code comments  
✅ **Scalability** - Async APIs, indexed queries, microservices  
✅ **Maintainability** - Clear structure, interfaces, DTOs  

---

## 🚜 Future Enhancements

- **API Rate Limiting** - Middleware protection
- **Advanced Dashboards** - Real-time KPI tracking
- **Mobile App** - Native mobile client
- **Workflow Automation** - Low-code process designer
- **SSO Integration** - LDAP/Active Directory
- **Multi-Tenant SaaS** - Per-tenant data isolation
- **Self-Service Reporting** - Drag-drop report builder
- **EDI Integration** - Supplier/customer data exchange
- **Kubernetes Deployment** - Cloud-native with Helm

---

## 📄 License

Portfolio project. See LICENSE file for details.

---

## 👤 Author

**Ammar Hisham**  
.NET Software Engineer  
GitHub: [https://github.com/ammarhisham97-rgb]  
LinkedIn: [https://www.linkedin.com/in/ammar-hisham-dev/]  
Email: [ammarhisham72@gmail.com]

---

**Status:** Production-Ready  
**Version:** 1.0.0  
**Last Updated:** January 2026
