using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiddiFranchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeOrdinaryOperationsAccountless : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialOperations_OrdinaryFields",
                table: "FinancialOperations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialOperations_OrdinaryFields",
                table: "FinancialOperations",
                sql: "\"Kind\" NOT IN ('Income', 'Expense') OR (\"CategoryId\" IS NOT NULL AND \"AmountAmount\" IS NOT NULL AND \"AmountCurrencyCode\" IS NOT NULL)");

            // Le entrate e le uscite non hanno più un conto: si eliminano i posting
            // storici e si azzera il collegamento senza toccare importi e categorie.
            migrationBuilder.Sql(
                """
                DELETE FROM "AccountPostings" p
                USING "FinancialOperations" f
                WHERE p."FinancialOperationId" = f."Id"
                  AND f."Kind" IN ('Income', 'Expense');

                UPDATE "FinancialOperations"
                SET "AccountId" = NULL
                WHERE "Kind" IN ('Income', 'Expense');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialOperations_OrdinaryFields",
                table: "FinancialOperations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialOperations_OrdinaryFields",
                table: "FinancialOperations",
                sql: "\"Kind\" NOT IN ('Income', 'Expense') OR (\"AccountId\" IS NOT NULL AND \"CategoryId\" IS NOT NULL AND \"AmountAmount\" IS NOT NULL AND \"AmountCurrencyCode\" IS NOT NULL)");
        }
    }
}
