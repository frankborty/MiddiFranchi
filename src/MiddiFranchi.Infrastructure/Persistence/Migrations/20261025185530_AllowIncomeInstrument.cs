using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiddiFranchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowIncomeInstrument : Migration
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
                sql: "\"Kind\" = 'Investment' OR (\"InvestmentKind\" IS NULL AND (\"InstrumentId\" IS NULL OR \"Kind\" = 'Income') AND \"SettlementAccountId\" IS NULL AND \"QuantityValue\" IS NULL AND \"QuotedPriceUnit\" IS NULL AND \"QuotedPricePriceAmount\" IS NULL AND \"QuotedPricePriceCurrencyCode\" IS NULL AND \"QuotedPricePercentOfNominal\" IS NULL)");
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
                sql: "\"Kind\" = 'Investment' OR (\"InvestmentKind\" IS NULL AND \"InstrumentId\" IS NULL AND \"SettlementAccountId\" IS NULL AND \"QuantityValue\" IS NULL AND \"QuotedPriceUnit\" IS NULL AND \"QuotedPricePriceAmount\" IS NULL AND \"QuotedPricePriceCurrencyCode\" IS NULL AND \"QuotedPricePercentOfNominal\" IS NULL)");
        }
    }
}
