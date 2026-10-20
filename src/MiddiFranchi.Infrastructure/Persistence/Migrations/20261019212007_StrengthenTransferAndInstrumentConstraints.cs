using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiddiFranchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StrengthenTransferAndInstrumentConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Instruments_PriceUnitNotional",
                table: "Instruments",
                sql: "(\"PriceUnit\" = 'PercentOfNominal' AND \"NotionalValueAmount\" IS NOT NULL AND \"NotionalValueAmount\" > 0 AND \"NotionalValueCurrencyCode\" IS NOT NULL) OR (\"PriceUnit\" = 'PerUnit' AND \"NotionalValueAmount\" IS NULL AND \"NotionalValueCurrencyCode\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialOperations_AppliedRateNonEur",
                table: "FinancialOperations",
                sql: "\"AppliedRateCurrencyCode\" IS NULL OR \"AppliedRateCurrencyCode\" <> 'EUR'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialOperations_DistinctTransferAccounts",
                table: "FinancialOperations",
                sql: "\"Kind\" <> 'Transfer' OR \"SourceAccountId\" <> \"DestinationAccountId\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialOperations_TransferRateSemantics",
                table: "FinancialOperations",
                sql: "\"Kind\" <> 'Transfer' OR ((\"SourceAmountCurrencyCode\" = \"DestinationAmountCurrencyCode\" AND \"SourceAmountAmount\" = \"DestinationAmountAmount\" AND \"AppliedRateRate\" IS NULL AND \"AppliedRateCurrencyCode\" IS NULL) OR (\"SourceAmountCurrencyCode\" <> \"DestinationAmountCurrencyCode\" AND \"AppliedRateRate\" IS NOT NULL AND \"AppliedRateCurrencyCode\" IS NOT NULL AND ((\"SourceAmountCurrencyCode\" = 'EUR' AND \"AppliedRateCurrencyCode\" = \"DestinationAmountCurrencyCode\") OR (\"DestinationAmountCurrencyCode\" = 'EUR' AND \"AppliedRateCurrencyCode\" = \"SourceAmountCurrencyCode\"))))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExchangeRateObservations_NonEurCurrency",
                table: "ExchangeRateObservations",
                sql: "\"RateCurrencyCode\" <> 'EUR'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Instruments_PriceUnitNotional",
                table: "Instruments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialOperations_AppliedRateNonEur",
                table: "FinancialOperations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialOperations_DistinctTransferAccounts",
                table: "FinancialOperations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialOperations_TransferRateSemantics",
                table: "FinancialOperations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExchangeRateObservations_NonEurCurrency",
                table: "ExchangeRateObservations");
        }
    }
}
