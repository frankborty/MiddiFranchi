using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiddiFranchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MakeInvestmentOperationsAccountless : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialOperations_InvestmentFields",
                table: "FinancialOperations");

            migrationBuilder.AlterColumn<Guid>(
                name: "DebitAccountId",
                table: "InvestmentOperationFees",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialOperations_InvestmentFields",
                table: "FinancialOperations",
                sql: "\"Kind\" <> 'Investment' OR \"InvestmentKind\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialOperations_InvestmentFields",
                table: "FinancialOperations");

            migrationBuilder.AlterColumn<Guid>(
                name: "DebitAccountId",
                table: "InvestmentOperationFees",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialOperations_InvestmentFields",
                table: "FinancialOperations",
                sql: "\"Kind\" <> 'Investment' OR (\"InvestmentKind\" IS NOT NULL AND \"SettlementAccountId\" IS NOT NULL)");
        }
    }
}
