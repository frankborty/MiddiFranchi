using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiddiFranchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowOperationHardDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La cancellazione delle operazioni ordinarie deve rimuovere davvero
            // operazione e posting: i trigger che proteggevano la storia vengono rimossi.
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS "TR_AccountPostings_PreventDelete" ON "AccountPostings";
                DROP TRIGGER IF EXISTS "TR_FinancialOperations_PreventReversalDelete" ON "FinancialOperations";
                DROP FUNCTION IF EXISTS prevent_reversal_delete();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION prevent_reversal_delete()
                RETURNS trigger AS $$
                BEGIN
                    IF OLD."Kind" = 'Reversal' THEN
                        RAISE EXCEPTION 'Le operazioni di storno non possono essere eliminate.';
                    END IF;

                    RETURN OLD;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER "TR_FinancialOperations_PreventReversalDelete"
                BEFORE DELETE ON "FinancialOperations"
                FOR EACH ROW EXECUTE FUNCTION prevent_reversal_delete();

                CREATE TRIGGER "TR_AccountPostings_PreventDelete"
                BEFORE DELETE ON "AccountPostings"
                FOR EACH ROW EXECUTE FUNCTION prevent_history_delete();
                """);
        }
    }
}
