using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiddiFranchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHistoricalDemoMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Movimenti dimostrativi per rendere leggibili i grafici storici.
            migrationBuilder.Sql(
                """
                INSERT INTO "FinancialOperations"
                    ("Id", "Kind", "OperationDate", "RecordedAt", "Description", "AccountId",
                     "CategoryId", "AmountAmount", "AmountCurrencyCode")
                SELECT gen_random_uuid(), 'Income', (m + interval '26 days')::date, now(),
                       'Stipendio ' || to_char(m, 'MM/YYYY'), NULL, c."Id",
                       1850 + (EXTRACT(YEAR FROM m)::int - 2023) * 50, 'EUR'
                FROM generate_series('2023-01-01'::date, '2026-08-01'::date, interval '1 month') AS m
                JOIN "Categories" c
                  ON c."Type" = 'Income' AND c."Name" = 'Stipendio' AND c."Subcategory" IS NULL;

                INSERT INTO "FinancialOperations"
                    ("Id", "Kind", "OperationDate", "RecordedAt", "Description", "AccountId",
                     "CategoryId", "AmountAmount", "AmountCurrencyCode")
                SELECT gen_random_uuid(), 'Expense', (m + interval '14 days')::date, now(),
                       x.description || ' ' || to_char(m, 'MM/YYYY'), NULL, c."Id", x.amount, 'EUR'
                FROM generate_series('2023-01-01'::date, '2026-08-01'::date, interval '1 month') AS m
                CROSS JOIN LATERAL (
                    SELECT *
                    FROM (VALUES
                        (1, 'Abitazione & Utenze', 'AltroCasa', 'Affitto', 700.00::numeric),
                        (2, 'Alimentazione', 'Spesa', 'Spesa alimentari', 350.00::numeric),
                        (3, 'Auto', 'Rifornimento', 'Rifornimento', 90.00::numeric),
                        (4, 'Fun & Shopping', 'Ristoranti', 'Ristorante', 65.00::numeric),
                        (5, 'Streaming', 'Prime', 'Streaming', 12.99::numeric),
                        (0, 'Salute & Benessere', 'Palestra', 'Palestra', 40.00::numeric)
                    ) AS v(mn, cat, sub, description, amount)
                    WHERE v.mn = EXTRACT(MONTH FROM m)::int % 6
                ) AS x
                JOIN "Categories" c
                  ON c."Type" = 'Expense' AND c."Name" = x.cat AND c."Subcategory" = x.sub;

                INSERT INTO "FinancialOperations"
                    ("Id", "Kind", "OperationDate", "RecordedAt", "Description", "AccountId",
                     "CategoryId", "AmountAmount", "AmountCurrencyCode")
                SELECT gen_random_uuid(), 'Income', (m + interval '13 days')::date, now(),
                       'Premio produzione ' || to_char(m, 'YYYY'), NULL, c."Id", 500.00, 'EUR'
                FROM generate_series('2023-03-01'::date, '2026-06-01'::date, interval '3 months') AS m
                JOIN "Categories" c
                  ON c."Type" = 'Income' AND c."Name" = 'Premio Produzione' AND c."Subcategory" IS NULL;

                INSERT INTO "FinancialOperations"
                    ("Id", "Kind", "OperationDate", "RecordedAt", "Description", "AccountId",
                     "CategoryId", "AmountAmount", "AmountCurrencyCode")
                SELECT gen_random_uuid(), 'Expense', make_date(y, 1, 20), now(),
                       'Assicurazione auto ' || y, NULL, c."Id", 600.00, 'EUR'
                FROM generate_series(2023, 2026) AS y
                JOIN "Categories" c
                  ON c."Type" = 'Expense' AND c."Name" = 'Auto' AND c."Subcategory" = 'Assicurazione';

                INSERT INTO "FinancialOperations"
                    ("Id", "Kind", "OperationDate", "RecordedAt", "Description", "AccountId",
                     "CategoryId", "AmountAmount", "AmountCurrencyCode")
                SELECT gen_random_uuid(), 'Expense', make_date(y, 12, 20), now(),
                       'Regali di Natale ' || y, NULL, c."Id", 200.00, 'EUR'
                FROM generate_series(2023, 2025) AS y
                JOIN "Categories" c
                  ON c."Type" = 'Expense' AND c."Name" = 'Fun & Shopping' AND c."Subcategory" = 'Feste';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dati dimostrativi non rimossi automaticamente: si eliminano dalla UI.
        }
    }
}
