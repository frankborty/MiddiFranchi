using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiddiFranchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameCasaUtenzeAndAddIaSubcategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "Categories"
                SET "Name" = 'Casa & Utenze'
                WHERE "Type" = 'Expense'
                  AND "Name" = 'Abitazione & Utenze';

                INSERT INTO "Categories" ("Id", "Name", "Type", "Description", "Subcategory", "Icon", "ColorHex")
                SELECT gen_random_uuid(), 'Casa & Utenze', 'Expense', NULL, 'IA', 'bulb', '#2563eb'
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "Categories"
                    WHERE "Type" = 'Expense'
                      AND "Name" = 'Casa & Utenze'
                      AND "Subcategory" = 'IA');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM "Categories"
                WHERE "Type" = 'Expense'
                  AND "Name" = 'Casa & Utenze'
                  AND "Subcategory" = 'IA';

                UPDATE "Categories"
                SET "Name" = 'Abitazione & Utenze'
                WHERE "Type" = 'Expense'
                  AND "Name" = 'Casa & Utenze';
                """);
        }
    }
}
