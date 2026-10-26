using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiddiFranchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveInvestmentPortfolios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FinancialOperations_InvestmentPortfolios_PortfolioId",
                table: "FinancialOperations");

            migrationBuilder.DropTable(
                name: "InvestmentPortfolios");

            migrationBuilder.DropIndex(
                name: "IX_FinancialOperations_PortfolioId",
                table: "FinancialOperations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialOperations_InvestmentFields",
                table: "FinancialOperations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialOperations_NonInvestmentFields",
                table: "FinancialOperations");

            migrationBuilder.DropColumn(
                name: "PortfolioId",
                table: "FinancialOperations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialOperations_InvestmentFields",
                table: "FinancialOperations",
                sql: "\"Kind\" <> 'Investment' OR (\"InvestmentKind\" IS NOT NULL AND \"SettlementAccountId\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialOperations_NonInvestmentFields",
                table: "FinancialOperations",
                sql: "\"Kind\" = 'Investment' OR (\"InvestmentKind\" IS NULL AND \"InstrumentId\" IS NULL AND \"SettlementAccountId\" IS NULL AND \"QuantityValue\" IS NULL AND \"QuotedPriceUnit\" IS NULL AND \"QuotedPricePriceAmount\" IS NULL AND \"QuotedPricePriceCurrencyCode\" IS NULL AND \"QuotedPricePercentOfNominal\" IS NULL)");

            // I vecchi ETF erano classificati come «Altro» dalla migrazione precedente.
            // Vengono riclassificati in azionario; le eventuali eccezioni vanno gestite dai dati.
            migrationBuilder.Sql(
                """
                UPDATE "Instruments" SET "AssetClass" = 'Equity'
                WHERE "AssetClass" = 'Other' AND "Name" ILIKE '%ETF%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "Instruments" SET "AssetClass" = 'Other'
                WHERE "AssetClass" = 'Equity' AND "Name" ILIKE '%ETF%';
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialOperations_InvestmentFields",
                table: "FinancialOperations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialOperations_NonInvestmentFields",
                table: "FinancialOperations");

            migrationBuilder.AddColumn<Guid>(
                name: "PortfolioId",
                table: "FinancialOperations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InvestmentPortfolios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ReferenceCurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestmentPortfolios", x => x.Id);
                    table.CheckConstraint("CK_InvestmentPortfolios_ReferenceCurrencyCode", "\"ReferenceCurrencyCode\" IN ('EUR')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialOperations_PortfolioId",
                table: "FinancialOperations",
                column: "PortfolioId");

            // La reversione ricrea l'unico portafoglio e riaggancia le operazioni
            // d'investimento prima di ripristinare il check constraint.
            migrationBuilder.Sql(
                """
                INSERT INTO "InvestmentPortfolios" ("Id", "Name", "ReferenceCurrencyCode")
                VALUES ('d0d0d0d0-1111-4111-8111-111111111111', 'Portafoglio principale', 'EUR')
                ON CONFLICT ("Id") DO NOTHING;

                UPDATE "FinancialOperations"
                SET "PortfolioId" = 'd0d0d0d0-1111-4111-8111-111111111111'
                WHERE "Kind" = 'Investment' AND "PortfolioId" IS NULL;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialOperations_InvestmentFields",
                table: "FinancialOperations",
                sql: "\"Kind\" <> 'Investment' OR (\"InvestmentKind\" IS NOT NULL AND \"PortfolioId\" IS NOT NULL AND \"SettlementAccountId\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialOperations_NonInvestmentFields",
                table: "FinancialOperations",
                sql: "\"Kind\" = 'Investment' OR (\"InvestmentKind\" IS NULL AND \"PortfolioId\" IS NULL AND \"InstrumentId\" IS NULL AND \"SettlementAccountId\" IS NULL AND \"QuantityValue\" IS NULL AND \"QuotedPriceUnit\" IS NULL AND \"QuotedPricePriceAmount\" IS NULL AND \"QuotedPricePriceCurrencyCode\" IS NULL AND \"QuotedPricePercentOfNominal\" IS NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_FinancialOperations_InvestmentPortfolios_PortfolioId",
                table: "FinancialOperations",
                column: "PortfolioId",
                principalTable: "InvestmentPortfolios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
