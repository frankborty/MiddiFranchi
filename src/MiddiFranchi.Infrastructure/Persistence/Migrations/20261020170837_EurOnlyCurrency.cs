using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiddiFranchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EurOnlyCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "ManualBalances"
                    DROP CONSTRAINT "FK_ManualBalances_Accounts_AccountId_AmountCurrencyCode";
                ALTER TABLE "AccountPostings"
                    DROP CONSTRAINT "FK_AccountPostings_Accounts_AccountId_CurrencyCode";
                ALTER TABLE "FinancialOperations"
                    DROP CONSTRAINT IF EXISTS "CK_FinancialOperations_TransferRateSemantics";

                CREATE OR REPLACE FUNCTION mf_eur_rate(p_currency text, p_date date)
                RETURNS numeric
                LANGUAGE sql
                STABLE
                AS $$
                    SELECT er."Rate"
                    FROM "ExchangeRateObservations" er
                    WHERE er."RateCurrencyCode" = p_currency
                      AND er."ValidAt" <= p_date
                    ORDER BY er."ValidAt" DESC, er."ObservedAt" DESC, er."Id" DESC
                    LIMIT 1
                $$;

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "ManualBalances" mb
                        WHERE mb."AmountCurrencyCode" <> 'EUR'
                          AND mf_eur_rate(mb."AmountCurrencyCode", mb."ValidAt") IS NULL)
                    THEN
                        RAISE EXCEPTION 'Migrazione EUR interrotta: manca un cambio per una rilevazione manuale non-EUR.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM "PriceObservations" po
                        WHERE po."Unit" = 'PerUnit'
                          AND po."PriceCurrencyCode" <> 'EUR'
                          AND mf_eur_rate(po."PriceCurrencyCode", po."ValidAt") IS NULL)
                    THEN
                        RAISE EXCEPTION 'Migrazione EUR interrotta: manca un cambio per una quotazione non-EUR.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM "Instruments" i
                        WHERE i."NotionalValueCurrencyCode" IS NOT NULL
                          AND i."NotionalValueCurrencyCode" <> 'EUR'
                          AND mf_eur_rate(i."NotionalValueCurrencyCode", CURRENT_DATE) IS NULL)
                    THEN
                        RAISE EXCEPTION 'Migrazione EUR interrotta: manca un cambio per un valore nominale non-EUR.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM "FinancialOperations" o
                        WHERE (o."AmountCurrencyCode" IS NOT NULL
                               AND o."AmountCurrencyCode" <> 'EUR'
                               AND mf_eur_rate(o."AmountCurrencyCode", o."OperationDate") IS NULL)
                           OR (o."SourceAmountCurrencyCode" IS NOT NULL
                               AND o."SourceAmountCurrencyCode" <> 'EUR'
                               AND mf_eur_rate(o."SourceAmountCurrencyCode", o."OperationDate") IS NULL)
                           OR (o."DestinationAmountCurrencyCode" IS NOT NULL
                               AND o."DestinationAmountCurrencyCode" <> 'EUR'
                               AND mf_eur_rate(o."DestinationAmountCurrencyCode", o."OperationDate") IS NULL)
                           OR (o."QuotedPricePriceCurrencyCode" IS NOT NULL
                               AND o."QuotedPricePriceCurrencyCode" <> 'EUR'
                               AND mf_eur_rate(o."QuotedPricePriceCurrencyCode", o."OperationDate") IS NULL))
                    THEN
                        RAISE EXCEPTION 'Migrazione EUR interrotta: manca un cambio per una operazione non-EUR.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM "InvestmentOperationFees" f
                        JOIN "FinancialOperations" o ON o."Id" = f."InvestmentOperationId"
                        WHERE f."AmountCurrencyCode" <> 'EUR'
                          AND mf_eur_rate(f."AmountCurrencyCode", o."OperationDate") IS NULL)
                    THEN
                        RAISE EXCEPTION 'Migrazione EUR interrotta: manca un cambio per una commissione non-EUR.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM "AccountPostings" p
                        JOIN "FinancialOperations" o ON o."Id" = p."FinancialOperationId"
                        WHERE o."Kind" <> 'Transfer'
                          AND p."CurrencyCode" <> 'EUR'
                          AND mf_eur_rate(p."CurrencyCode", o."OperationDate") IS NULL)
                    THEN
                        RAISE EXCEPTION 'Migrazione EUR interrotta: manca un cambio per un movimento non-EUR.';
                    END IF;
                END $$;

                UPDATE "ManualBalances"
                SET "AmountAmount" = CASE
                        WHEN "AmountCurrencyCode" = 'EUR' THEN "AmountAmount"
                        ELSE "AmountAmount" / mf_eur_rate("AmountCurrencyCode", "ValidAt")
                    END,
                    "AmountCurrencyCode" = 'EUR';

                UPDATE "PriceObservations"
                SET "Price" = CASE
                        WHEN "Unit" = 'PerUnit' AND "PriceCurrencyCode" <> 'EUR'
                            THEN "Price" / mf_eur_rate("PriceCurrencyCode", "ValidAt")
                        ELSE "Price"
                    END,
                    "PriceCurrencyCode" = CASE
                        WHEN "Unit" = 'PerUnit' THEN 'EUR'
                        ELSE NULL
                    END;

                UPDATE "Instruments"
                SET "QuoteCurrencyCode" = 'EUR',
                    "NotionalValueAmount" = CASE
                        WHEN "NotionalValueAmount" IS NOT NULL AND "NotionalValueCurrencyCode" <> 'EUR'
                            THEN "NotionalValueAmount" / mf_eur_rate("NotionalValueCurrencyCode", CURRENT_DATE)
                        ELSE "NotionalValueAmount"
                    END,
                    "NotionalValueCurrencyCode" = CASE
                        WHEN "NotionalValueCurrencyCode" IS NULL THEN NULL
                        ELSE 'EUR'
                    END;

                UPDATE "InvestmentPortfolios" SET "ReferenceCurrencyCode" = 'EUR';
                UPDATE "Accounts" SET "CurrencyCode" = 'EUR';

                UPDATE "FinancialOperations"
                SET "AmountAmount" = CASE
                        WHEN "AmountCurrencyCode" IS NULL OR "AmountCurrencyCode" = 'EUR' THEN "AmountAmount"
                        ELSE "AmountAmount" / mf_eur_rate("AmountCurrencyCode", "OperationDate")
                    END,
                    "AmountCurrencyCode" = CASE
                        WHEN "AmountCurrencyCode" IS NULL THEN NULL
                        ELSE 'EUR'
                    END;

                UPDATE "FinancialOperations"
                SET "QuotedPricePriceAmount" = CASE
                        WHEN "QuotedPricePriceCurrencyCode" IS NULL OR "QuotedPricePriceCurrencyCode" = 'EUR' THEN "QuotedPricePriceAmount"
                        ELSE "QuotedPricePriceAmount" / mf_eur_rate("QuotedPricePriceCurrencyCode", "OperationDate")
                    END,
                    "QuotedPricePriceCurrencyCode" = CASE
                        WHEN "QuotedPricePriceCurrencyCode" IS NULL THEN NULL
                        ELSE 'EUR'
                    END;

                UPDATE "FinancialOperations"
                SET "SourceAmountAmount" = CASE
                        WHEN "SourceAmountCurrencyCode" = 'EUR' THEN "SourceAmountAmount"
                        ELSE "SourceAmountAmount" / mf_eur_rate("SourceAmountCurrencyCode", "OperationDate")
                    END,
                    "DestinationAmountAmount" = CASE
                        WHEN "DestinationAmountCurrencyCode" = 'EUR' THEN "DestinationAmountAmount"
                        ELSE "DestinationAmountAmount" / mf_eur_rate("DestinationAmountCurrencyCode", "OperationDate")
                    END,
                    "SourceAmountCurrencyCode" = 'EUR',
                    "DestinationAmountCurrencyCode" = 'EUR'
                WHERE "Kind" = 'Transfer';

                UPDATE "FinancialOperations"
                SET "DestinationAmountAmount" = "SourceAmountAmount"
                WHERE "Kind" = 'Transfer';

                UPDATE "InvestmentOperationFees" f
                SET "AmountAmount" = CASE
                        WHEN f."AmountCurrencyCode" = 'EUR' THEN f."AmountAmount"
                        ELSE f."AmountAmount" / mf_eur_rate(f."AmountCurrencyCode", o."OperationDate")
                    END,
                    "AmountCurrencyCode" = 'EUR'
                FROM "FinancialOperations" o
                WHERE o."Id" = f."InvestmentOperationId";

                UPDATE "AccountPostings" p
                SET "SignedAmount" = CASE
                        WHEN p."CurrencyCode" = 'EUR' THEN p."SignedAmount"
                        ELSE p."SignedAmount" / mf_eur_rate(p."CurrencyCode", o."OperationDate")
                    END,
                    "CurrencyCode" = 'EUR'
                FROM "FinancialOperations" o
                WHERE o."Id" = p."FinancialOperationId"
                  AND o."Kind" <> 'Transfer';

                UPDATE "AccountPostings" p
                SET "SignedAmount" = CASE
                        WHEN p."AccountId" = o."SourceAccountId" THEN -o."SourceAmountAmount"
                        ELSE o."DestinationAmountAmount"
                    END,
                    "CurrencyCode" = 'EUR'
                FROM "FinancialOperations" o
                WHERE o."Id" = p."FinancialOperationId"
                  AND o."Kind" = 'Transfer';

                ALTER TABLE "ManualBalances"
                    ADD CONSTRAINT "FK_ManualBalances_Accounts_AccountId_AmountCurrencyCode"
                    FOREIGN KEY ("AccountId", "AmountCurrencyCode")
                    REFERENCES "Accounts" ("Id", "CurrencyCode")
                    ON DELETE RESTRICT;
                ALTER TABLE "AccountPostings"
                    ADD CONSTRAINT "FK_AccountPostings_Accounts_AccountId_CurrencyCode"
                    FOREIGN KEY ("AccountId", "CurrencyCode")
                    REFERENCES "Accounts" ("Id", "CurrencyCode")
                    ON DELETE RESTRICT;

                DROP FUNCTION mf_eur_rate(text, date);
                """);

            migrationBuilder.DropTable(
                name: "ExchangeRateObservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PriceObservations_PriceCurrency",
                table: "PriceObservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ManualBalances_CurrencyCode",
                table: "ManualBalances");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InvestmentPortfolios_ReferenceCurrencyCode",
                table: "InvestmentPortfolios");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InvestmentOperationFees_CurrencyCode",
                table: "InvestmentOperationFees");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Instruments_NotionalValue",
                table: "Instruments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Instruments_QuoteCurrencyCode",
                table: "Instruments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialOperations_AmountCurrency",
                table: "FinancialOperations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialOperations_AppliedRateCurrency",
                table: "FinancialOperations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialOperations_AppliedRateNonEur",
                table: "FinancialOperations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialOperations_AppliedRatePair",
                table: "FinancialOperations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialOperations_DestinationAmountCurrency",
                table: "FinancialOperations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialOperations_NonTransferFields",
                table: "FinancialOperations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialOperations_QuotedPriceCurrency",
                table: "FinancialOperations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialOperations_SourceAmountCurrency",
                table: "FinancialOperations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Accounts_CurrencyCode",
                table: "Accounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountPostings_CurrencyCode",
                table: "AccountPostings");

            migrationBuilder.DropColumn(
                name: "AppliedRateCurrencyCode",
                table: "FinancialOperations");

            migrationBuilder.DropColumn(
                name: "AppliedRateRate",
                table: "FinancialOperations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PriceObservations_PriceCurrency",
                table: "PriceObservations",
                sql: "(\"Unit\" = 'PerUnit' AND \"PriceCurrencyCode\" IS NOT NULL AND \"PriceCurrencyCode\" IN ('EUR')) OR (\"Unit\" = 'PercentOfNominal' AND \"PriceCurrencyCode\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ManualBalances_CurrencyCode",
                table: "ManualBalances",
                sql: "\"AmountCurrencyCode\" IN ('EUR')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvestmentPortfolios_ReferenceCurrencyCode",
                table: "InvestmentPortfolios",
                sql: "\"ReferenceCurrencyCode\" IN ('EUR')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvestmentOperationFees_CurrencyCode",
                table: "InvestmentOperationFees",
                sql: "\"AmountCurrencyCode\" IN ('EUR')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Instruments_NotionalValue",
                table: "Instruments",
                sql: "(\"NotionalValueAmount\" IS NULL AND \"NotionalValueCurrencyCode\" IS NULL) OR (\"NotionalValueAmount\" IS NOT NULL AND \"NotionalValueAmount\" > 0 AND \"NotionalValueCurrencyCode\" IN ('EUR'))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Instruments_QuoteCurrencyCode",
                table: "Instruments",
                sql: "\"QuoteCurrencyCode\" IN ('EUR')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialOperations_AmountCurrency",
                table: "FinancialOperations",
                sql: "\"AmountCurrencyCode\" IS NULL OR \"AmountCurrencyCode\" IN ('EUR')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialOperations_DestinationAmountCurrency",
                table: "FinancialOperations",
                sql: "\"DestinationAmountCurrencyCode\" IS NULL OR \"DestinationAmountCurrencyCode\" IN ('EUR')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialOperations_NonTransferFields",
                table: "FinancialOperations",
                sql: "\"Kind\" = 'Transfer' OR (\"SourceAccountId\" IS NULL AND \"DestinationAccountId\" IS NULL AND \"SourceAmountAmount\" IS NULL AND \"SourceAmountCurrencyCode\" IS NULL AND \"DestinationAmountAmount\" IS NULL AND \"DestinationAmountCurrencyCode\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialOperations_QuotedPriceCurrency",
                table: "FinancialOperations",
                sql: "\"QuotedPricePriceCurrencyCode\" IS NULL OR \"QuotedPricePriceCurrencyCode\" IN ('EUR')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialOperations_SourceAmountCurrency",
                table: "FinancialOperations",
                sql: "\"SourceAmountCurrencyCode\" IS NULL OR \"SourceAmountCurrencyCode\" IN ('EUR')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialOperations_TransferEurAmounts",
                table: "FinancialOperations",
                sql: "\"Kind\" <> 'Transfer' OR (\"SourceAmountCurrencyCode\" = 'EUR' AND \"DestinationAmountCurrencyCode\" = 'EUR' AND \"SourceAmountAmount\" = \"DestinationAmountAmount\")");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Accounts_CurrencyCode",
                table: "Accounts",
                sql: "\"CurrencyCode\" IN ('EUR')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountPostings_CurrencyCode",
                table: "AccountPostings",
                sql: "\"CurrencyCode\" IN ('EUR')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PriceObservations_PriceCurrency",
                table: "PriceObservations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ManualBalances_CurrencyCode",
                table: "ManualBalances");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InvestmentPortfolios_ReferenceCurrencyCode",
                table: "InvestmentPortfolios");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InvestmentOperationFees_CurrencyCode",
                table: "InvestmentOperationFees");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Instruments_NotionalValue",
                table: "Instruments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Instruments_QuoteCurrencyCode",
                table: "Instruments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialOperations_AmountCurrency",
                table: "FinancialOperations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialOperations_DestinationAmountCurrency",
                table: "FinancialOperations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialOperations_NonTransferFields",
                table: "FinancialOperations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialOperations_QuotedPriceCurrency",
                table: "FinancialOperations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialOperations_SourceAmountCurrency",
                table: "FinancialOperations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FinancialOperations_TransferEurAmounts",
                table: "FinancialOperations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Accounts_CurrencyCode",
                table: "Accounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountPostings_CurrencyCode",
                table: "AccountPostings");

            migrationBuilder.AddColumn<string>(
                name: "AppliedRateCurrencyCode",
                table: "FinancialOperations",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AppliedRateRate",
                table: "FinancialOperations",
                type: "numeric",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExchangeRateObservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ObservedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric", nullable: false),
                    RateCurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    ValidAt = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRateObservations", x => x.Id);
                    table.CheckConstraint("CK_ExchangeRateObservations_CurrencyCode", "\"RateCurrencyCode\" IN ('EUR', 'USD', 'GBP', 'JPY')");
                    table.CheckConstraint("CK_ExchangeRateObservations_NonEurCurrency", "\"RateCurrencyCode\" <> 'EUR'");
                    table.CheckConstraint("CK_ExchangeRateObservations_Rate", "\"Rate\" > 0");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_PriceObservations_PriceCurrency",
                table: "PriceObservations",
                sql: "(\"Unit\" = 'PerUnit' AND \"PriceCurrencyCode\" IS NOT NULL AND \"PriceCurrencyCode\" IN ('EUR', 'USD', 'GBP', 'JPY')) OR (\"Unit\" = 'PercentOfNominal' AND \"PriceCurrencyCode\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ManualBalances_CurrencyCode",
                table: "ManualBalances",
                sql: "\"AmountCurrencyCode\" IN ('EUR', 'USD', 'GBP', 'JPY')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvestmentPortfolios_ReferenceCurrencyCode",
                table: "InvestmentPortfolios",
                sql: "\"ReferenceCurrencyCode\" IN ('EUR', 'USD', 'GBP', 'JPY')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvestmentOperationFees_CurrencyCode",
                table: "InvestmentOperationFees",
                sql: "\"AmountCurrencyCode\" IN ('EUR', 'USD', 'GBP', 'JPY')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Instruments_NotionalValue",
                table: "Instruments",
                sql: "(\"NotionalValueAmount\" IS NULL AND \"NotionalValueCurrencyCode\" IS NULL) OR (\"NotionalValueAmount\" IS NOT NULL AND \"NotionalValueAmount\" > 0 AND \"NotionalValueCurrencyCode\" IN ('EUR', 'USD', 'GBP', 'JPY'))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Instruments_QuoteCurrencyCode",
                table: "Instruments",
                sql: "\"QuoteCurrencyCode\" IN ('EUR', 'USD', 'GBP', 'JPY')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialOperations_AmountCurrency",
                table: "FinancialOperations",
                sql: "\"AmountCurrencyCode\" IS NULL OR \"AmountCurrencyCode\" IN ('EUR', 'USD', 'GBP', 'JPY')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialOperations_AppliedRateCurrency",
                table: "FinancialOperations",
                sql: "\"AppliedRateCurrencyCode\" IS NULL OR \"AppliedRateCurrencyCode\" IN ('EUR', 'USD', 'GBP', 'JPY')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialOperations_AppliedRateNonEur",
                table: "FinancialOperations",
                sql: "\"AppliedRateCurrencyCode\" IS NULL OR \"AppliedRateCurrencyCode\" <> 'EUR'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialOperations_AppliedRatePair",
                table: "FinancialOperations",
                sql: "(\"AppliedRateRate\" IS NULL AND \"AppliedRateCurrencyCode\" IS NULL) OR (\"AppliedRateRate\" IS NOT NULL AND \"AppliedRateRate\" > 0 AND \"AppliedRateCurrencyCode\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialOperations_DestinationAmountCurrency",
                table: "FinancialOperations",
                sql: "\"DestinationAmountCurrencyCode\" IS NULL OR \"DestinationAmountCurrencyCode\" IN ('EUR', 'USD', 'GBP', 'JPY')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialOperations_NonTransferFields",
                table: "FinancialOperations",
                sql: "\"Kind\" = 'Transfer' OR (\"SourceAccountId\" IS NULL AND \"DestinationAccountId\" IS NULL AND \"SourceAmountAmount\" IS NULL AND \"SourceAmountCurrencyCode\" IS NULL AND \"DestinationAmountAmount\" IS NULL AND \"DestinationAmountCurrencyCode\" IS NULL AND \"AppliedRateRate\" IS NULL AND \"AppliedRateCurrencyCode\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialOperations_QuotedPriceCurrency",
                table: "FinancialOperations",
                sql: "\"QuotedPricePriceCurrencyCode\" IS NULL OR \"QuotedPricePriceCurrencyCode\" IN ('EUR', 'USD', 'GBP', 'JPY')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialOperations_SourceAmountCurrency",
                table: "FinancialOperations",
                sql: "\"SourceAmountCurrencyCode\" IS NULL OR \"SourceAmountCurrencyCode\" IN ('EUR', 'USD', 'GBP', 'JPY')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FinancialOperations_TransferRateSemantics",
                table: "FinancialOperations",
                sql: "\"Kind\" <> 'Transfer' OR ((\"SourceAmountCurrencyCode\" = \"DestinationAmountCurrencyCode\" AND \"SourceAmountAmount\" = \"DestinationAmountAmount\" AND \"AppliedRateRate\" IS NULL AND \"AppliedRateCurrencyCode\" IS NULL) OR (\"SourceAmountCurrencyCode\" <> \"DestinationAmountCurrencyCode\" AND \"AppliedRateRate\" IS NOT NULL AND \"AppliedRateCurrencyCode\" IS NOT NULL AND ((\"SourceAmountCurrencyCode\" = 'EUR' AND \"AppliedRateCurrencyCode\" = \"DestinationAmountCurrencyCode\") OR (\"DestinationAmountCurrencyCode\" = 'EUR' AND \"AppliedRateCurrencyCode\" = \"SourceAmountCurrencyCode\"))))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Accounts_CurrencyCode",
                table: "Accounts",
                sql: "\"CurrencyCode\" IN ('EUR', 'USD', 'GBP', 'JPY')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountPostings_CurrencyCode",
                table: "AccountPostings",
                sql: "\"CurrencyCode\" IN ('EUR', 'USD', 'GBP', 'JPY')");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRateObservations_RateCurrencyCode_ValidAt_ObservedAt",
                table: "ExchangeRateObservations",
                columns: new[] { "RateCurrencyCode", "ValidAt", "ObservedAt" });
        }
    }
}
