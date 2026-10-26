using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiddiFranchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowInstrumentOnExpenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialOperations_NonInvestmentFields",
                table: "FinancialOperations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialOperations_NonInvestmentFields",
                table: "FinancialOperations",
                sql: "\"Kind\" = 'Investment' OR (\"InvestmentKind\" IS NULL AND (\"InstrumentId\" IS NULL OR \"Kind\" IN ('Income', 'Expense')) AND \"SettlementAccountId\" IS NULL AND \"QuantityValue\" IS NULL AND \"QuotedPriceUnit\" IS NULL AND \"QuotedPricePriceAmount\" IS NULL AND \"QuotedPricePriceCurrencyCode\" IS NULL AND \"QuotedPricePercentOfNominal\" IS NULL)");

            // Le sottocategorie non hanno un'icona propria: l'aspetto appartiene alla categoria.
            migrationBuilder.Sql(
                """
                UPDATE "Categories"
                SET "Icon" = 'receipt', "ColorHex" = '#64748b'
                WHERE "Name" = 'Fees' AND "Type" = 'Expense';
                """);

            // Collega le spese esistenti allo strumento, come per le cedole.
            migrationBuilder.Sql(
                """
                UPDATE "FinancialOperations" e
                SET "InstrumentId" = i."Id"
                FROM "FinancialOperations" e2
                JOIN "Instruments" i ON e2."Description" = 'Tasse su cedola ' || i."Name"
                JOIN "Categories" c ON c."Id" = e2."CategoryId"
                    AND c."Name" = 'Fees' AND c."Subcategory" = 'Tasse Investimenti'
                WHERE e."Id" = e2."Id"
                    AND e."Kind" = 'Expense' AND e."InstrumentId" IS NULL;

                UPDATE "FinancialOperations" e
                SET "InstrumentId" = o."InstrumentId"
                FROM "FinancialOperations" o
                WHERE e."Kind" = 'Expense'
                    AND e."Description" LIKE 'Commissioni su %'
                    AND e."InstrumentId" IS NULL
                    AND o."Kind" = 'Investment'
                    AND o."InvestmentKind" IN ('Buy', 'Sell')
                    AND o."OperationDate" = e."OperationDate"
                    AND EXISTS (
                        SELECT 1 FROM "InvestmentOperationFees" f
                        WHERE f."InvestmentOperationId" = o."Id"
                            AND f."AmountAmount" = e."AmountAmount");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialOperations_NonInvestmentFields",
                table: "FinancialOperations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialOperations_NonInvestmentFields",
                table: "FinancialOperations",
                sql: "\"Kind\" = 'Investment' OR (\"InvestmentKind\" IS NULL AND (\"InstrumentId\" IS NULL OR \"Kind\" = 'Income') AND \"SettlementAccountId\" IS NULL AND \"QuantityValue\" IS NULL AND \"QuotedPriceUnit\" IS NULL AND \"QuotedPricePriceAmount\" IS NULL AND \"QuotedPricePriceCurrencyCode\" IS NULL AND \"QuotedPricePercentOfNominal\" IS NULL)");
        }
    }
}
