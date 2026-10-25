using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiddiFranchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInstrumentTaxRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TaxRate",
                table: "Instruments",
                type: "numeric",
                nullable: false,
                defaultValue: 26m);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Instruments_TaxRate",
                table: "Instruments",
                sql: "\"TaxRate\" >= 0 AND \"TaxRate\" <= 100");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Instruments_TaxRate",
                table: "Instruments");

            migrationBuilder.DropColumn(
                name: "TaxRate",
                table: "Instruments");
        }
    }
}
