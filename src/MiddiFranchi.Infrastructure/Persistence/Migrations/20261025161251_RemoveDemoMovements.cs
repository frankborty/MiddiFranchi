using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiddiFranchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDemoMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // I movimenti dimostrativi della migrazione AddHistoricalDemoMovements
            // non hanno un conto collegato e usano descrizioni generate.
            migrationBuilder.Sql(
                """
                DELETE FROM "FinancialOperations"
                WHERE "AccountId" IS NULL
                  AND "Kind" IN ('Income', 'Expense')
                  AND (
                      "Description" LIKE 'Stipendio __/____'
                      OR "Description" LIKE 'Premio produzione ____'
                      OR "Description" LIKE 'Assicurazione auto ____'
                      OR "Description" LIKE 'Regali di Natale ____'
                      OR "Description" LIKE 'Affitto __/____'
                      OR "Description" LIKE 'Spesa alimentari __/____'
                      OR "Description" LIKE 'Rifornimento __/____'
                      OR "Description" LIKE 'Ristorante __/____'
                      OR "Description" LIKE 'Streaming __/____'
                      OR "Description" LIKE 'Palestra __/____'
                  );
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Pulizia dati: i movimenti dimostrativi non vengono ripristinati.
        }
    }
}
