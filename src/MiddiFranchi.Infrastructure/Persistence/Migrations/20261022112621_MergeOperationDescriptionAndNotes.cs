using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiddiFranchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MergeOperationDescriptionAndNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Unifica i due campi in un'unica descrizione prima di eliminare "Notes".
            migrationBuilder.Sql(
                """
                UPDATE "FinancialOperations"
                SET "Description" = CASE
                    WHEN "Notes" IS NULL OR btrim("Notes") = '' THEN "Description"
                    WHEN "Description" IS NULL OR btrim("Description") = '' THEN "Notes"
                    ELSE "Description" || ' · ' || "Notes"
                END;
                """);

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "FinancialOperations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "FinancialOperations",
                type: "text",
                nullable: true);
        }
    }
}
