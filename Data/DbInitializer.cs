using CashManagementSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CashManagementSystem.Data
{
    public static class DbInitializer
    {
        public static void Initialize(AppDbContext context)
        {
            context.Database.EnsureCreated();

            // Ensure InstituteId columns exist on FinancialCategories and BankAccounts
            try
            {
                context.Database.ExecuteSqlRaw(
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='FinancialCategories' AND COLUMN_NAME='InstituteId') " +
                    "ALTER TABLE FinancialCategories ADD InstituteId INT NULL;"
                );
                context.Database.ExecuteSqlRaw(
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='BankAccounts' AND COLUMN_NAME='InstituteId') " +
                    "ALTER TABLE BankAccounts ADD InstituteId INT NULL;"
                );

                context.Database.ExecuteSqlRaw(
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Transactions' AND COLUMN_NAME='InstituteId') " +
                    "ALTER TABLE Transactions ADD InstituteId INT NULL;"
                );
                context.Database.ExecuteSqlRaw(
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Bills' AND COLUMN_NAME='InstituteId') " +
                    "ALTER TABLE Bills ADD InstituteId INT NULL;"
                );
                context.Database.ExecuteSqlRaw(
                    "IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Notifications' AND COLUMN_NAME='InstituteId') " +
                    "ALTER TABLE Notifications ADD InstituteId INT NULL;"
                );

                // Backfill InstituteId = 1 for any legacy records
                context.Database.ExecuteSqlRaw("UPDATE FinancialCategories SET InstituteId = 1 WHERE InstituteId IS NULL;");
                context.Database.ExecuteSqlRaw("UPDATE BankAccounts SET InstituteId = 1 WHERE InstituteId IS NULL;");
                context.Database.ExecuteSqlRaw("UPDATE Transactions SET InstituteId = 1 WHERE InstituteId IS NULL;");
                context.Database.ExecuteSqlRaw("UPDATE Bills SET InstituteId = 1 WHERE InstituteId IS NULL;");
                context.Database.ExecuteSqlRaw("UPDATE Notifications SET InstituteId = 1 WHERE InstituteId IS NULL;");
            }
            catch
            {
                // Suppress if provider doesn't support raw SQL
            }

            // DEMO SEEDING REMOVED per user requirement:
            // Do not automatically add or restore sample transactions, bills, bank accounts, or categories on startup.
        }
    }
}

