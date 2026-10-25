using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiddiFranchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowTaxWithheldCouponFees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
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
                       parent_investment_kind NOT IN ('Buy', 'Sell', 'StandaloneFee', 'Dividend', 'Coupon') THEN
                        RAISE EXCEPTION 'Commissione non valida per il tipo di operazione.';
                    END IF;

                    IF (parent_investment_kind = 'Buy' AND NEW."Kind" <> 'BuyFee') OR
                       (parent_investment_kind = 'Sell' AND NEW."Kind" <> 'SellFee') OR
                       (parent_investment_kind = 'StandaloneFee' AND NEW."Kind" <> 'Standalone') OR
                       (parent_investment_kind IN ('Dividend', 'Coupon') AND NEW."Kind" <> 'TaxWithheld') THEN
                        RAISE EXCEPTION 'Tipo di commissione non coerente con la operazione.';
                    END IF;

                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
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
                """);
        }
    }
}
