using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiddiFranchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforcePostingAccountCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccountPostings_Accounts_AccountId",
                table: "AccountPostings");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Accounts_Id_CurrencyCode",
                table: "Accounts",
                columns: new[] { "Id", "CurrencyCode" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountPostings_AccountId_CurrencyCode",
                table: "AccountPostings",
                columns: new[] { "AccountId", "CurrencyCode" });

            migrationBuilder.AddForeignKey(
                name: "FK_AccountPostings_Accounts_AccountId_CurrencyCode",
                table: "AccountPostings",
                columns: new[] { "AccountId", "CurrencyCode" },
                principalTable: "Accounts",
                principalColumns: new[] { "Id", "CurrencyCode" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccountPostings_Accounts_AccountId_CurrencyCode",
                table: "AccountPostings");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Accounts_Id_CurrencyCode",
                table: "Accounts");

            migrationBuilder.DropIndex(
                name: "IX_AccountPostings_AccountId_CurrencyCode",
                table: "AccountPostings");

            migrationBuilder.AddForeignKey(
                name: "FK_AccountPostings_Accounts_AccountId",
                table: "AccountPostings",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
