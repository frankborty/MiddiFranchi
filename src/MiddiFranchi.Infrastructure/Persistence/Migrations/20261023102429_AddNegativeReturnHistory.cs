using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiddiFranchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNegativeReturnHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Quotazioni storiche dimostrative: a fine giugno 2026 il valore delle posizioni
            // era inferiore al costo residuo, così il grafico mostra un periodo negativo.
            migrationBuilder.Sql(
                """
                INSERT INTO "PriceObservations"
                    ("Id", "InstrumentId", "Unit", "Price", "PriceCurrencyCode", "ValidAt", "ObservedAt", "Source")
                SELECT gen_random_uuid(), i."Id", 'PerUnit', 11.50, 'EUR',
                       DATE '2026-06-30', TIMESTAMPTZ '2026-06-30 18:00:00+00', 'Storico dimostrativo'
                FROM "Instruments" i
                WHERE UPPER(i."Symbol") = 'SUAS'
                  AND NOT EXISTS (
                      SELECT 1 FROM "PriceObservations" p
                      WHERE p."InstrumentId" = i."Id"
                        AND p."ValidAt" = DATE '2026-06-30'
                        AND p."Source" = 'Storico dimostrativo')
                LIMIT 1;

                INSERT INTO "PriceObservations"
                    ("Id", "InstrumentId", "Unit", "Price", "PriceCurrencyCode", "ValidAt", "ObservedAt", "Source")
                SELECT gen_random_uuid(), i."Id", 'PercentOfNominal', 80.00, NULL,
                       DATE '2026-06-30', TIMESTAMPTZ '2026-06-30 18:00:00+00', 'Storico dimostrativo'
                FROM "Instruments" i
                WHERE UPPER(i."Symbol") = 'BTPITAL'
                  AND NOT EXISTS (
                      SELECT 1 FROM "PriceObservations" p
                      WHERE p."InstrumentId" = i."Id"
                        AND p."ValidAt" = DATE '2026-06-30'
                        AND p."Source" = 'Storico dimostrativo')
                LIMIT 1;

                INSERT INTO "PriceObservations"
                    ("Id", "InstrumentId", "Unit", "Price", "PriceCurrencyCode", "ValidAt", "ObservedAt", "Source")
                SELECT gen_random_uuid(), i."Id", 'PerUnit', 92.00, 'EUR',
                       DATE '2026-06-30', TIMESTAMPTZ '2026-06-30 18:00:00+00', 'Storico dimostrativo'
                FROM "Instruments" i
                WHERE UPPER(i."Symbol") = 'VWCE'
                  AND NOT EXISTS (
                      SELECT 1 FROM "PriceObservations" p
                      WHERE p."InstrumentId" = i."Id"
                        AND p."ValidAt" = DATE '2026-06-30'
                        AND p."Source" = 'Storico dimostrativo')
                LIMIT 1;

                INSERT INTO "PriceObservations"
                    ("Id", "InstrumentId", "Unit", "Price", "PriceCurrencyCode", "ValidAt", "ObservedAt", "Source")
                SELECT gen_random_uuid(), i."Id", 'PerUnit', 77.00, 'EUR',
                       DATE '2026-06-30', TIMESTAMPTZ '2026-06-30 18:00:00+00', 'Storico dimostrativo'
                FROM "Instruments" i
                WHERE UPPER(i."Symbol") = 'XEON'
                  AND NOT EXISTS (
                      SELECT 1 FROM "PriceObservations" p
                      WHERE p."InstrumentId" = i."Id"
                        AND p."ValidAt" = DATE '2026-06-30'
                        AND p."Source" = 'Storico dimostrativo')
                LIMIT 1;

                INSERT INTO "PriceObservations"
                    ("Id", "InstrumentId", "Unit", "Price", "PriceCurrencyCode", "ValidAt", "ObservedAt", "Source")
                SELECT gen_random_uuid(), i."Id", 'PerUnit', 5.00, 'EUR',
                       DATE '2026-06-30', TIMESTAMPTZ '2026-06-30 18:00:00+00', 'Storico dimostrativo'
                FROM "Instruments" i
                WHERE UPPER(i."Symbol") = 'V3AA'
                  AND NOT EXISTS (
                      SELECT 1 FROM "PriceObservations" p
                      WHERE p."InstrumentId" = i."Id"
                        AND p."ValidAt" = DATE '2026-06-30'
                        AND p."Source" = 'Storico dimostrativo')
                LIMIT 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Le osservazioni dimostrative sono dati storici append-only e non vengono rimosse.
        }
    }
}
