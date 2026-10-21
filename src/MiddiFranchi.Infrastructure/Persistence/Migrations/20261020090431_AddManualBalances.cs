using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiddiFranchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManualBalances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ManualBalances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    AmountCurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ValidAt = table.Column<DateOnly>(type: "date", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManualBalances", x => x.Id);
                    table.CheckConstraint("CK_ManualBalances_CurrencyCode", "\"AmountCurrencyCode\" IN ('EUR', 'USD', 'GBP', 'JPY')");
                    table.ForeignKey(
                        name: "FK_ManualBalances_Accounts_AccountId_AmountCurrencyCode",
                        columns: x => new { x.AccountId, x.AmountCurrencyCode },
                        principalTable: "Accounts",
                        principalColumns: new[] { "Id", "CurrencyCode" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ManualBalances_AccountId_AmountCurrencyCode",
                table: "ManualBalances",
                columns: new[] { "AccountId", "AmountCurrencyCode" });

            migrationBuilder.CreateIndex(
                name: "IX_ManualBalances_AccountId_RecordedAt",
                table: "ManualBalances",
                columns: new[] { "AccountId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ManualBalances_AccountId_ValidAt",
                table: "ManualBalances",
                columns: new[] { "AccountId", "ValidAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ManualBalances");
        }
    }
}
