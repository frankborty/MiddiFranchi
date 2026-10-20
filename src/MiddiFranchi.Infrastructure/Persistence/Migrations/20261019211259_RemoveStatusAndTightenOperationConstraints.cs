using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiddiFranchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStatusAndTightenOperationConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "FinancialOperations" AS operation
                        WHERE operation."Status" = 'Reversed'
                          AND NOT EXISTS (
                              SELECT 1
                              FROM "FinancialOperations" AS reversal
                              WHERE reversal."Kind" = 'Reversal'
                                AND reversal."OriginalOperationId" = operation."Id"))
                    THEN
                        RAISE EXCEPTION 'Stato di storno inconsistente: operazione stornata senza storno.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM "FinancialOperations" AS reversal
                        JOIN "FinancialOperations" AS operation
                          ON operation."Id" = reversal."OriginalOperationId"
                        WHERE reversal."Kind" = 'Reversal'
                          AND operation."Status" <> 'Reversed')
                    THEN
                        RAISE EXCEPTION 'Stato di storno inconsistente: storno senza operazione originale stornata.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_FinancialOperations_Status",
                table: "FinancialOperations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialOperations_Status",
                table: "FinancialOperations");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "FinancialOperations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialOperations_AmountByKind",
                table: "FinancialOperations",
                sql: "(\"Kind\" IN ('Income', 'Expense') AND \"AmountAmount\" IS NOT NULL AND \"AmountCurrencyCode\" IS NOT NULL) OR (\"Kind\" = 'Investment' AND \"InvestmentKind\" IN ('Dividend', 'Coupon') AND \"AmountAmount\" IS NOT NULL AND \"AmountCurrencyCode\" IS NOT NULL) OR (\"Kind\" IN ('Transfer', 'Reversal') AND \"AmountAmount\" IS NULL AND \"AmountCurrencyCode\" IS NULL) OR (\"Kind\" = 'Investment' AND \"InvestmentKind\" IN ('Buy', 'Sell', 'StandaloneFee') AND \"AmountAmount\" IS NULL AND \"AmountCurrencyCode\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialOperations_InvestmentDetail",
                table: "FinancialOperations",
                sql: "\"Kind\" <> 'Investment' OR (\"InvestmentKind\" IN ('Buy', 'Sell') AND \"InstrumentId\" IS NOT NULL AND \"QuantityValue\" IS NOT NULL AND \"QuotedPriceUnit\" IS NOT NULL AND \"AmountAmount\" IS NULL AND \"AmountCurrencyCode\" IS NULL) OR (\"InvestmentKind\" IN ('Dividend', 'Coupon') AND \"InstrumentId\" IS NOT NULL AND \"QuantityValue\" IS NULL AND \"QuotedPriceUnit\" IS NULL AND \"QuotedPricePriceAmount\" IS NULL AND \"QuotedPricePriceCurrencyCode\" IS NULL AND \"QuotedPricePercentOfNominal\" IS NULL AND \"AmountAmount\" IS NOT NULL AND \"AmountCurrencyCode\" IS NOT NULL) OR (\"InvestmentKind\" = 'StandaloneFee' AND \"QuantityValue\" IS NULL AND \"QuotedPriceUnit\" IS NULL AND \"QuotedPricePriceAmount\" IS NULL AND \"QuotedPricePriceCurrencyCode\" IS NULL AND \"QuotedPricePercentOfNominal\" IS NULL AND \"AmountAmount\" IS NULL AND \"AmountCurrencyCode\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialOperations_NonInvestmentFields",
                table: "FinancialOperations",
                sql: "\"Kind\" = 'Investment' OR (\"InvestmentKind\" IS NULL AND \"PortfolioId\" IS NULL AND \"InstrumentId\" IS NULL AND \"SettlementAccountId\" IS NULL AND \"QuantityValue\" IS NULL AND \"QuotedPriceUnit\" IS NULL AND \"QuotedPricePriceAmount\" IS NULL AND \"QuotedPricePriceCurrencyCode\" IS NULL AND \"QuotedPricePercentOfNominal\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialOperations_NonOrdinaryFields",
                table: "FinancialOperations",
                sql: "\"Kind\" IN ('Income', 'Expense') OR (\"AccountId\" IS NULL AND \"CategoryId\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialOperations_NonTransferFields",
                table: "FinancialOperations",
                sql: "\"Kind\" = 'Transfer' OR (\"SourceAccountId\" IS NULL AND \"DestinationAccountId\" IS NULL AND \"SourceAmountAmount\" IS NULL AND \"SourceAmountCurrencyCode\" IS NULL AND \"DestinationAmountAmount\" IS NULL AND \"DestinationAmountCurrencyCode\" IS NULL AND \"AppliedRateRate\" IS NULL AND \"AppliedRateCurrencyCode\" IS NULL)");

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION validate_financial_operation_reversal()
                RETURNS trigger AS $$
                DECLARE
                    original_kind text;
                BEGIN
                    IF TG_OP = 'UPDATE' AND
                       (OLD."Kind" IS DISTINCT FROM NEW."Kind" OR
                        OLD."OriginalOperationId" IS DISTINCT FROM NEW."OriginalOperationId") THEN
                        RAISE EXCEPTION 'Tipo e collegamento di storno non modificabili.';
                    END IF;

                    IF NEW."Kind" = 'Reversal' THEN
                        SELECT "Kind" INTO original_kind
                        FROM "FinancialOperations"
                        WHERE "Id" = NEW."OriginalOperationId";

                        IF original_kind IS NULL THEN
                            RAISE EXCEPTION 'Operazione originale dello storno non trovata.';
                        END IF;

                        IF original_kind = 'Reversal' THEN
                            RAISE EXCEPTION 'Una operazione di storno non puo essere stornata.';
                        END IF;
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER "TR_FinancialOperations_ValidateReversal"
                BEFORE INSERT OR UPDATE ON "FinancialOperations"
                FOR EACH ROW EXECUTE FUNCTION validate_financial_operation_reversal();

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

                CREATE OR REPLACE FUNCTION prevent_history_delete()
                RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'Le righe storiche non possono essere eliminate.';
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER "TR_AccountPostings_PreventDelete"
                BEFORE DELETE ON "AccountPostings"
                FOR EACH ROW EXECUTE FUNCTION prevent_history_delete();

                CREATE TRIGGER "TR_InvestmentOperationFees_PreventDelete"
                BEFORE DELETE ON "InvestmentOperationFees"
                FOR EACH ROW EXECUTE FUNCTION prevent_history_delete();

                CREATE TRIGGER "TR_PriceObservations_PreventDelete"
                BEFORE DELETE ON "PriceObservations"
                FOR EACH ROW EXECUTE FUNCTION prevent_history_delete();

                CREATE TRIGGER "TR_ExchangeRateObservations_PreventDelete"
                BEFORE DELETE ON "ExchangeRateObservations"
                FOR EACH ROW EXECUTE FUNCTION prevent_history_delete();

                CREATE OR REPLACE FUNCTION validate_investment_fee()
                RETURNS trigger AS $$
                DECLARE
                    parent_kind text;
                    parent_investment_kind text;
                BEGIN
                    SELECT "Kind", "InvestmentKind"
                    INTO parent_kind, parent_investment_kind
                    FROM "FinancialOperations"
                    WHERE "Id" = NEW."InvestmentOperationId";

                    IF parent_kind IS DISTINCT FROM 'Investment' OR
                       parent_investment_kind IS NULL OR
                       parent_investment_kind NOT IN ('Buy', 'Sell', 'StandaloneFee') THEN
                        RAISE EXCEPTION 'Commissione non valida per il tipo di operazione.';
                    END IF;

                    IF (parent_investment_kind = 'Buy' AND NEW."Kind" <> 'BuyFee') OR
                       (parent_investment_kind = 'Sell' AND NEW."Kind" <> 'SellFee') OR
                       (parent_investment_kind = 'StandaloneFee' AND NEW."Kind" <> 'Standalone') THEN
                        RAISE EXCEPTION 'Tipo di commissione non coerente con la operazione.';
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER "TR_InvestmentOperationFees_ValidateParent"
                BEFORE INSERT OR UPDATE ON "InvestmentOperationFees"
                FOR EACH ROW EXECUTE FUNCTION validate_investment_fee();

                CREATE UNIQUE INDEX "IX_InvestmentOperationFees_StandaloneUnique"
                ON "InvestmentOperationFees" ("InvestmentOperationId")
                WHERE "Kind" = 'Standalone';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER IF EXISTS "TR_InvestmentOperationFees_ValidateParent" ON "InvestmentOperationFees";
                DROP FUNCTION IF EXISTS validate_investment_fee();

                DROP INDEX IF EXISTS "IX_InvestmentOperationFees_StandaloneUnique";

                DROP TRIGGER IF EXISTS "TR_FinancialOperations_PreventReversalDelete" ON "FinancialOperations";
                DROP FUNCTION IF EXISTS prevent_reversal_delete();

                DROP TRIGGER IF EXISTS "TR_AccountPostings_PreventDelete" ON "AccountPostings";
                DROP TRIGGER IF EXISTS "TR_InvestmentOperationFees_PreventDelete" ON "InvestmentOperationFees";
                DROP TRIGGER IF EXISTS "TR_PriceObservations_PreventDelete" ON "PriceObservations";
                DROP TRIGGER IF EXISTS "TR_ExchangeRateObservations_PreventDelete" ON "ExchangeRateObservations";
                DROP FUNCTION IF EXISTS prevent_history_delete();

                DROP TRIGGER IF EXISTS "TR_FinancialOperations_ValidateReversal" ON "FinancialOperations";
                DROP FUNCTION IF EXISTS validate_financial_operation_reversal();
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialOperations_AmountByKind",
                table: "FinancialOperations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialOperations_InvestmentDetail",
                table: "FinancialOperations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialOperations_NonInvestmentFields",
                table: "FinancialOperations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialOperations_NonOrdinaryFields",
                table: "FinancialOperations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialOperations_NonTransferFields",
                table: "FinancialOperations");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "FinancialOperations",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Registered");

            migrationBuilder.Sql(
                """
                UPDATE "FinancialOperations" AS operation
                SET "Status" = 'Reversed'
                WHERE EXISTS (
                    SELECT 1
                    FROM "FinancialOperations" AS reversal
                    WHERE reversal."Kind" = 'Reversal'
                      AND reversal."OriginalOperationId" = operation."Id");
                """);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialOperations_Status",
                table: "FinancialOperations",
                column: "Status");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialOperations_Status",
                table: "FinancialOperations",
                sql: "\"Status\" IN ('Registered', 'Reversed')");
        }
    }
}
