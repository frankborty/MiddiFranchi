using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiddiFranchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConvertTaxWithheldFeesToExpenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO "FinancialOperations"
                    ("Id", "Kind", "OperationDate", "RecordedAt", "Description", "CategoryId", "AmountAmount", "AmountCurrencyCode")
                SELECT
                    gen_random_uuid(),
                    'Expense',
                    o."OperationDate",
                    o."RecordedAt",
                    CASE WHEN o."InvestmentKind" = 'Sell' THEN 'Tasse su plusvalenze' ELSE 'Tasse su cedola' END,
                    c."Id",
                    f."AmountAmount",
                    f."AmountCurrencyCode"
                FROM "InvestmentOperationFees" f
                JOIN "FinancialOperations" o ON o."Id" = f."InvestmentOperationId"
                JOIN "Categories" c ON c."Type" = 'Expense'
                    AND c."Name" = 'Fees'
                    AND c."Subcategory" = 'Tasse Plusvalenze'
                WHERE f."Kind" = 'TaxWithheld';

                DELETE FROM "InvestmentOperationFees" WHERE "Kind" = 'TaxWithheld';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
