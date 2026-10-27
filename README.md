# Edutech Cash Management System (CMS)

[![.NET Version](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-MVC-blue?logo=dotnet)](https://dotnet.microsoft.com/apps/aspnet)
[![Entity Framework Core](https://img.shields.io/badge/EF%20Core-SQL%20Server-purple?logo=microsoft-sql-server&logoColor=white)](https://learn.microsoft.com/ef/core/)
[![Database](https://img.shields.io/badge/Database-SQL%20Server%20LocalDB-CC292B?logo=microsoftsqlserver)](https://learn.microsoft.com/sql/database-engine/configure-windows/sql-server-express-localdb)

A comprehensive, multi-tenant financial management and operational cash tracking platform tailored for educational institutions, school networks, and multi-unit organizations. Built with **ASP.NET Core 10.0 MVC**, **Entity Framework Core**, and **SQL Server**.

---

## 🌟 Key Features

### 🏢 1. Multi-Tenant Organization Structure
- **Multi-Institute Support**: Host and administer distinct educational institutions or commercial organizations within a single installation.
- **Organizational Units**: Segment finances across schools, campuses, colleges, departments, and operational centers.
- **Institute Isolation**: Strict data scoping guarantees that each organization's accounts, bills, and transactions remain completely isolated from other institutes.

### 👥 2. Role-Based Access Control (RBAC)
- **Super Admin**: High-level platform administration, onboarding new institutes, managing organization owners/admins, and monitoring system-wide health.
- **Organization Admin / Owner**: Complete governance over their institution—managing units, staff users (accountants & operators), approving transaction deletion requests, and accessing organization-wide financial reports.
- **Accountant**: Day-to-day transaction recording (revenues, expenses, bill payments, bank transfers), submitting deletion requests with justification, and viewing unit ledgers.
- **User / Operational Staff**: Unit-specific financial read and tracking privileges.

### 💰 3. Financial Ledger & Cash Flow Management
- **Revenue Tracking**: Record fee collections, tuition, subsidies, donations, and auxiliary income with categorization and unit assignment.
- **Expense Tracking**: Manage operating expenses, utilities, payroll, and maintenance costs against specific payment accounts.
- **Bank & Cash In Hand Accounts**: Track liquidity in real-time across physical cash registers and bank accounts with automatic ledger balancing.
- **Fund Transfers**: Transfer funds between accounts with auto-generated dual debit/credit entries and unique transfer reference codes.

### 📄 4. Bills & Invoices Management
- **Bill Tracking**: Create recurring or one-time bills for institutional expenses (e.g., campus electricity, rent, supplier invoices).
- **Payment Schedules**: Record partial or full payments directly linked to ledger expense entries.
- **Overdue Detection**: Automated status computation (`Paid`, `Pending`, `Overdue`) with overdue alerts.

### 🔔 5. Role-Scoped Notifications & Audit Trail
- **Super Admin Notifications**: Exclusively receives security alerts (login tracking) and notifications when new organization administrator accounts are created.
- **Organization Admin Notifications**: Contextual alerts for institutional activities, overdue bills, accountant deletion requests, and payment logs.
- **Two-Step Deletion Workflow**: Accountants cannot directly delete posted financial records. They must submit a deletion request with mandatory justification for admin approval or rejection.

### 📊 6. Analytics & Financial Reporting
- **Overview Dashboard**: High-level KPI cards for total cash balance, revenue, expenses, net balance, and outstanding obligations.
- **Trend Charts**: 6-month monthly cash flow visualizations.
- **Unit Comparison Breakdown**: Financial performance and net margins broken down by organizational unit.

---

## 🏗️ Technology Stack

| Layer | Technology |
| :--- | :--- |
| **Framework** | .NET 10.0 (ASP.NET Core MVC) |
| **ORM / Data Access** | Entity Framework Core 10.0 (Code-First / DbContext) |
| **Database** | Microsoft SQL Server (LocalDB `(localdb)\mssqllocaldb` / SQL Server Express) |
| **Frontend UI** | HTML5, Vanilla CSS, Bootstrap 5, Custom Responsive Design |
| **Client Scripting** | Vanilla JavaScript (Async/Await Fetch API, Modal Handlers) |
| **Session & State** | In-Memory Distributed Cache & HttpContext Session with Secure Cookies |

---

## 📁 Project Structure

```text
CashManagementSystem/
├── Controllers/                 # MVC Controllers & REST API endpoints
│   ├── AccountController.cs       # Authentication, session initialization, login notifications
│   ├── BankAccountsController.cs  # Bank accounts, balances, and transfer operations
│   ├── BillsController.cs         # Bill creation, partial payments, status computation
│   ├── HomeController.cs          # Dashboard summary, analytics & cash flow APIs
│   ├── NotificationsController.cs # Role-scoped notifications, mark-read, deletion approvals
│   ├── ReportsController.cs       # Financial reporting and export endpoints
│   ├── SettingsController.cs      # Financial categories, organization settings
│   ├── TransactionsController.cs  # Revenue/expense transactions, deletion workflow
│   ├── UnitsController.cs         # Organizational units administration
│   └── UsersController.cs         # Multi-tenant user & organization admin management
├── Data/                        # Database context and initializers
│   ├── ApplicationDbContext.cs    # EF Core DbContext with model relations & precision rules
│   └── DbInitializer.cs          # Startup database creation & column verification
├── Filters/                     # Action filters & authorization attributes
│   └── AuthorizeSessionAttribute.cs # Session & role validation attribute
├── Models/                      # Entity models and database tables
│   ├── BankAccount.cs             # Bank & cash accounts
│   ├── Bill.cs                    # Bills and invoice records
│   ├── FinancialCategory.cs       # Revenue & expense categories
│   ├── Institute.cs               # Organizations / Institutes & unit relations
│   ├── NotificationItem.cs        # Role-scoped notification records
│   ├── OrganizationalUnit.cs      # Campuses, schools, and business units
│   ├── SystemSetting.cs           # Tenant configurations
│   ├── Transaction.cs             # Core financial journal entries
│   └── User.cs                    # Platform and organization users
├── Views/                       # Razor Views (UI pages)
│   ├── Account/                   # Login screen
│   ├── BankAccounts/              # Bank accounts list and modal dialogs
│   ├── Bills/                     # Bill payment registry and tracking
│   ├── Home/                      # Central executive dashboard
│   ├── Notifications/             # Notification center and approval queue
│   ├── Reports/                   # Financial analytics and breakdowns
│   ├── Settings/                  # Category and system preferences
│   ├── Shared/                    # Layout, navigation bar, alerts
│   ├── Transactions/              # Revenue and expense ledger views
│   ├── Units/                     # Units management
│   └── Users/                     # User administration and institute setup
├── wwwroot/                     # Static client assets (CSS, JS, Icons)
│   ├── css/site.css               # Design system & theme styles
│   └── js/site.js                 # Global client interactions
├── appsettings.json             # Database connection strings and logging config
├── Program.cs                   # Application entry point, middleware pipeline
└── README.md                    # Project documentation
```

---

## 🚀 Getting Started

### Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or higher.
- [Microsoft SQL Server LocalDB](https://learn.microsoft.com/sql/database-engine/configure-windows/sql-server-express-localdb) (included with Visual Studio or standalone) or [SQL Server Express](https://www.microsoft.com/sql-server/sql-server-downloads).
- Optional: [SQL Server Management Studio (SSMS)](https://learn.microsoft.com/sql/ssms/download-sql-server-management-studio-ssms) or Azure Data Studio for viewing database tables.

### 1. Clone the Repository
```bash
git clone https://github.com/HafsaMasood92/CASHMANAGEMENTSYTEM.git
cd CASHMANAGEMENTSYTEM
```

### 2. Configure the Database Connection
Open `appsettings.json` and adjust the connection string if needed. By default, it connects to SQL Server LocalDB:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=CashManagementDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
  }
}
```

### 3. Build & Run
Restore dependencies, compile, and run the application:
```bash
# Restore packages and compile
dotnet build

# Run the development server
dotnet run
```
Or run with auto-reload:
```bash
dotnet watch run
```

The application will start and listen at:
- **HTTP**: `http://localhost:5203`
- **HTTPS**: `https://localhost:7243`

---

## 🔐 Default Access & Roles

For local evaluation, the following pre-configured roles are available:

| Role | Email | Password | Scope & Responsibilities |
| :--- | :--- | :--- | :--- |
| **Super Admin** | `admin@edutech.com` | `Demo@123` | Platform oversight, onboarding new organizations, managing Organization Admins. |
| **Organization Admin** | `owner@edutech.com` | `Demo@123` | Full administration of Edutech Solutions, units, accounts, and deletion approval. |

---

## 🗄️ Viewing the Database in SSMS

To view populated tables and records in SQL Server Management Studio:
1. Open **SSMS**.
2. Connect to:
   - **Server Type**: `Database Engine`
   - **Server Name**: `(localdb)\mssqllocaldb`
   - **Authentication**: `Windows Authentication`
3. Expand **Databases** ➔ **`CashManagementDb`** ➔ **Tables**.
4. You will see all 11 application tables (`BankAccounts`, `Bills`, `FinancialCategories`, `Institutes`, `Notifications`, `OrganizationalUnits`, `Transactions`, `Users`, etc.).

---

## 🛡️ Security & Privacy
- **Environment Isolation**: Production credentials, local databases, and temporary build outputs (`bin/`, `obj/`, `*.db`, `*.mdf`, `secrets.json`) are excluded from version control via `.gitignore`.
- **Anti-Forgery Protection**: State-changing POST endpoints are protected by ASP.NET Core Anti-Forgery Tokens (`[ValidateAntiForgeryToken]`).
- **Session Validation**: All controller endpoints enforce session integrity and role validation via `[AuthorizeSession]`.

---

## 📄 License
This project is proprietary software for Edutech Cash Management System. All rights reserved.