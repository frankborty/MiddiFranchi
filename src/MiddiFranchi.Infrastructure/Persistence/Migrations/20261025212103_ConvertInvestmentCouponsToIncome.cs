using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiddiFranchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConvertInvestmentCouponsToIncome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO "FinancialOperations"
                    ("Id", "Kind", "OperationDate", "RecordedAt", "Description", "CategoryId", "InstrumentId", "AmountAmount", "AmountCurrencyCode")
                SELECT
                    gen_random_uuid(),
                    'Income',
                    o."OperationDate",
                    o."RecordedAt",
                    COALESCE(NULLIF(TRIM(o."Description"), ''), 'Cedola'),
                    c."Id",
                    o."InstrumentId",
                    o."AmountAmount",
                    o."AmountCurrencyCode"
                FROM "FinancialOperations" o
                JOIN "Categories" c ON c."Type" = 'Income' AND c."Name" = 'Cedole'
                WHERE o."Kind" = 'Investment'
                    AND o."InvestmentKind" IN ('Dividend', 'Coupon');

                DELETE FROM "InvestmentOperationFees" f
                USING "FinancialOperations" o
                WHERE f."InvestmentOperationId" = o."Id"
                    AND o."Kind" = 'Investment'
                    AND o."InvestmentKind" IN ('Dividend', 'Coupon');

                DELETE FROM "AccountPostings" p
                USING "FinancialOperations" o
                WHERE p."FinancialOperationId" = o."Id"
                    AND o."Kind" = 'Investment'
                    AND o."InvestmentKind" IN ('Dividend', 'Coupon');

                DELETE FROM "FinancialOperations"
                WHERE "Kind" = 'Investment'
                    AND "InvestmentKind" IN ('Dividend', 'Coupon');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
