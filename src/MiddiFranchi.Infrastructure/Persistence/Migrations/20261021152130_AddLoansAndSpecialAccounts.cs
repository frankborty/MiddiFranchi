using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiddiFranchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLoansAndSpecialAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Accounts_Type",
                table: "Accounts");

            migrationBuilder.CreateTable(
                name: "Loans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Borrower = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Loans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Loans_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LoanReadings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LoanId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    AmountCurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ValidAt = table.Column<DateOnly>(type: "date", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanReadings", x => x.Id);
                    table.CheckConstraint("CK_LoanReadings_CurrencyCode", "\"AmountCurrencyCode\" IN ('EUR')");
                    table.ForeignKey(
                        name: "FK_LoanReadings_Loans_LoanId",
                        column: x => x.LoanId,
                        principalTable: "Loans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Accounts_Type",
                table: "Accounts",
                sql: "\"Type\" IN ('Checking', 'Card', 'Cash', 'Liability', 'Loans')");

            migrationBuilder.CreateIndex(
                name: "IX_LoanReadings_LoanId_RecordedAt",
                table: "LoanReadings",
                columns: new[] { "LoanId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LoanReadings_LoanId_ValidAt",
                table: "LoanReadings",
                columns: new[] { "LoanId", "ValidAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Loans_AccountId",
                table: "Loans",
                column: "AccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoanReadings");

            migrationBuilder.DropTable(
                name: "Loans");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Accounts_Type",
                table: "Accounts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Accounts_Type",
                table: "Accounts",
                sql: "\"Type\" IN ('Checking', 'Card', 'Cash', 'Liability')");
        }
    }
}
