using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiddiFranchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameCouponCategoryToCedole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "Categories"
                SET "Name" = 'Cedole'
                WHERE "Type" = 'Income' AND "Name" = 'Cedole & Dividendi';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "Categories"
                SET "Name" = 'Cedole & Dividendi'
                WHERE "Type" = 'Income' AND "Name" = 'Cedole';
                """);
        }
    }
}
