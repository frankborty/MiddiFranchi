using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiddiFranchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameInvestmentTaxCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "Categories"
                SET "Subcategory" = 'Tasse Investimenti'
                WHERE "Name" = 'Fees' AND "Subcategory" = 'Tasse Plusvalenze';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "Categories"
                SET "Subcategory" = 'Tasse Plusvalenze'
                WHERE "Name" = 'Fees' AND "Subcategory" = 'Tasse Investimenti';
                """);
        }
    }
}
