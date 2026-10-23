using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiddiFranchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvestmentAssetDetailsAndEditing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssetClass",
                table: "Instruments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Instruments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.AddColumn<decimal>(
                name: "CouponRate",
                table: "Instruments",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Isin",
                table: "Instruments",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "MaturityDate",
                table: "Instruments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Instruments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceUrl",
                table: "Instruments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            // Il vecchio tipo tecnico viene convertito nella classe di attività. ETF e valori
            // non riconosciuti finiscono in "Other": la riclassificazione avviene dalla pagina.
            migrationBuilder.Sql(
                """
                UPDATE "Instruments"
                SET "AssetClass" = CASE "Type"
                    WHEN 'Share' THEN 'Equity'
                    WHEN 'Bond'  THEN 'Bond'
                    WHEN 'Fund'  THEN 'Mixed'
                    ELSE 'Other'
                END;

                ALTER TABLE "Instruments" ALTER COLUMN "AssetClass" SET NOT NULL;
                ALTER TABLE "Instruments" ALTER COLUMN "Status" DROP DEFAULT;
                ALTER TABLE "Instruments" DROP COLUMN "Type";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Instruments_Isin",
                table: "Instruments",
                column: "Isin",
                unique: true,
                filter: "\"Isin\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Instruments_AssetClass",
                table: "Instruments",
                sql: "\"AssetClass\" IN ('Equity', 'Bond', 'MoneyMarket', 'RealEstate', 'Commodity', 'Crypto', 'Mixed', 'Pension', 'Other')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Instruments_BondDetails",
                table: "Instruments",
                sql: "\"AssetClass\" = 'Bond' OR (\"MaturityDate\" IS NULL AND \"CouponRate\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Instruments_CouponRate",
                table: "Instruments",
                sql: "\"CouponRate\" IS NULL OR (\"CouponRate\" > 0 AND \"CouponRate\" <= 100)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Instruments_IsinFormat",
                table: "Instruments",
                sql: "\"Isin\" IS NULL OR \"Isin\" ~ '^[A-Z0-9]{12}$'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Instruments_Status",
                table: "Instruments",
                sql: "\"Status\" IN ('Active', 'Archived')");

            // La modifica e l'eliminazione delle operazioni d'investimento devono poter
            // sostituire le commissioni. La protezione sulle quotazioni resta attiva.
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS "TR_InvestmentOperationFees_PreventDelete" ON "InvestmentOperationFees";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Instruments_Isin",
                table: "Instruments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Instruments_AssetClass",
                table: "Instruments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Instruments_BondDetails",
                table: "Instruments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Instruments_CouponRate",
                table: "Instruments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Instruments_IsinFormat",
                table: "Instruments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Instruments_Status",
                table: "Instruments");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Instruments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Instruments"
                SET "Type" = CASE "AssetClass"
                    WHEN 'Equity' THEN 'Share'
                    WHEN 'Bond'   THEN 'Bond'
                    WHEN 'Mixed'  THEN 'Fund'
                    ELSE 'Other'
                END;

                ALTER TABLE "Instruments" ALTER COLUMN "Type" SET NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "AssetClass",
                table: "Instruments");

            migrationBuilder.DropColumn(
                name: "CouponRate",
                table: "Instruments");

            migrationBuilder.DropColumn(
                name: "Isin",
                table: "Instruments");

            migrationBuilder.DropColumn(
                name: "MaturityDate",
                table: "Instruments");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Instruments");

            migrationBuilder.DropColumn(
                name: "ReferenceUrl",
                table: "Instruments");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Instruments");

            migrationBuilder.Sql(
                """
                CREATE TRIGGER "TR_InvestmentOperationFees_PreventDelete"
                BEFORE DELETE ON "InvestmentOperationFees"
                FOR EACH ROW EXECUTE FUNCTION prevent_history_delete();
                """);
        }
    }
}
