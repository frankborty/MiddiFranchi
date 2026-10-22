using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiddiFranchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryAppearanceAndResetExpenseCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ColorHex",
                table: "Categories",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Icon",
                table: "Categories",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.Sql(
                """
                CREATE TEMP TABLE new_expense_categories (
                    "Name" text NOT NULL,
                    "Subcategory" text NOT NULL,
                    "Icon" text NOT NULL,
                    "ColorHex" text NOT NULL
                ) ON COMMIT DROP;

                INSERT INTO new_expense_categories ("Name", "Subcategory", "Icon", "ColorHex") VALUES
                    ('Abitazione & Utenze', 'Elettricità Maranello', 'home', '#2563eb'),
                    ('Abitazione & Utenze', 'Elettricità NonnaMaria', 'home', '#2563eb'),
                    ('Abitazione & Utenze', 'Elettricità Mamma', 'home', '#2563eb'),
                    ('Abitazione & Utenze', 'Gas', 'home', '#2563eb'),
                    ('Abitazione & Utenze', 'Telefono', 'home', '#2563eb'),
                    ('Abitazione & Utenze', 'Internet', 'home', '#2563eb'),
                    ('Abitazione & Utenze', 'AltroCasa', 'home', '#2563eb'),
                    ('Trasporti', 'Mezzi pubblici', 'bus', '#0ea5e9'),
                    ('Trasporti', 'Pedaggio', 'bus', '#0ea5e9'),
                    ('Trasporti', 'Nave', 'bus', '#0ea5e9'),
                    ('Trasporti', 'Aereo', 'bus', '#0ea5e9'),
                    ('Trasporti', 'AltroTrasporti', 'bus', '#0ea5e9'),
                    ('Auto', 'Assicurazione', 'car', '#f59e0b'),
                    ('Auto', 'Bollo', 'car', '#f59e0b'),
                    ('Auto', 'Tagliando', 'car', '#f59e0b'),
                    ('Auto', 'Revisione', 'car', '#f59e0b'),
                    ('Auto', 'Penumatici', 'car', '#f59e0b'),
                    ('Auto', 'Rifornimento', 'car', '#f59e0b'),
                    ('Auto', 'Meccanico', 'car', '#f59e0b'),
                    ('Auto', 'AltroAuto', 'car', '#f59e0b'),
                    ('Fees', 'Postepay', 'receipt', '#64748b'),
                    ('Fees', 'Conto Titoli', 'receipt', '#64748b'),
                    ('Fees', 'Conto Corrente', 'receipt', '#64748b'),
                    ('Fees', 'AltroFees', 'receipt', '#64748b'),
                    ('Fun & Shopping', 'Vestiti', 'shopping-bag', '#ec4899'),
                    ('Fun & Shopping', 'Tech', 'shopping-bag', '#ec4899'),
                    ('Fun & Shopping', 'AltroShopping', 'shopping-bag', '#ec4899'),
                    ('Fun & Shopping', 'Mare', 'shopping-bag', '#ec4899'),
                    ('Fun & Shopping', 'Bar', 'shopping-bag', '#ec4899'),
                    ('Fun & Shopping', 'Ristoranti', 'shopping-bag', '#ec4899'),
                    ('Fun & Shopping', 'AltroFun', 'shopping-bag', '#ec4899'),
                    ('Fun & Shopping', 'Viaggi', 'shopping-bag', '#ec4899'),
                    ('Fun & Shopping', 'Feste', 'shopping-bag', '#ec4899'),
                    ('Salute & Benessere', 'Medico', 'heart-pulse', '#ef4444'),
                    ('Salute & Benessere', 'Occhiali & Lenti', 'heart-pulse', '#ef4444'),
                    ('Salute & Benessere', 'Barbiere', 'heart-pulse', '#ef4444'),
                    ('Salute & Benessere', 'Medicine', 'heart-pulse', '#ef4444'),
                    ('Salute & Benessere', 'Palestra', 'heart-pulse', '#ef4444'),
                    ('Salute & Benessere', 'AltroSalute', 'heart-pulse', '#ef4444'),
                    ('Alimentazione', 'Spesa', 'utensils', '#22c55e'),
                    ('Alimentazione', 'Delivery', 'utensils', '#22c55e'),
                    ('Alimentazione', 'AltroSpesa', 'utensils', '#22c55e'),
                    ('Streaming', 'Prime', 'tv', '#8b5cf6'),
                    ('Streaming', 'Dazn', 'tv', '#8b5cf6'),
                    ('Streaming', 'Now', 'tv', '#8b5cf6'),
                    ('Streaming', 'HBO', 'tv', '#8b5cf6'),
                    ('Streaming', 'TimVision', 'tv', '#8b5cf6'),
                    ('Varie', 'Regali', 'gift', '#14b8a6'),
                    ('Varie', 'Superenalotto', 'gift', '#14b8a6'),
                    ('Varie', 'AltroVarie', 'gift', '#14b8a6');

                INSERT INTO "Categories" ("Id", "Name", "Type", "Description", "Subcategory", "Icon", "ColorHex")
                SELECT gen_random_uuid(), n."Name", 'Expense', NULL, n."Subcategory", n."Icon", n."ColorHex"
                FROM new_expense_categories n
                WHERE NOT EXISTS (
                    SELECT 1 FROM "Categories" c
                    WHERE c."Type" = 'Expense'
                      AND c."Name" = n."Name"
                      AND c."Subcategory" IS NOT DISTINCT FROM n."Subcategory");

                UPDATE "Categories" c
                SET "Icon" = n."Icon", "ColorHex" = n."ColorHex"
                FROM new_expense_categories n
                WHERE c."Type" = 'Expense'
                  AND c."Name" = n."Name"
                  AND c."Subcategory" IS NOT DISTINCT FROM n."Subcategory";

                CREATE TEMP TABLE expense_category_mapping (
                    "OldName" text NOT NULL,
                    "NewName" text NOT NULL,
                    "NewSubcategory" text NOT NULL
                ) ON COMMIT DROP;

                INSERT INTO expense_category_mapping ("OldName", "NewName", "NewSubcategory") VALUES
                    ('Spesa alimentare', 'Alimentazione', 'Spesa'),
                    ('Spesa', 'Alimentazione', 'Spesa'),
                    ('Casa', 'Abitazione & Utenze', 'AltroCasa'),
                    ('Trasporti', 'Trasporti', 'AltroTrasporti'),
                    ('Svago', 'Fun & Shopping', 'AltroFun'),
                    ('Ristorazione', 'Fun & Shopping', 'Ristoranti'),
                    ('Shopping', 'Fun & Shopping', 'AltroShopping'),
                    ('Salute', 'Salute & Benessere', 'Medico'),
                    ('Servizi', 'Fees', 'AltroFees'),
                    ('Tasse', 'Fees', 'AltroFees');

                UPDATE "FinancialOperations" f
                SET "CategoryId" = c."Id"
                FROM "Categories" oldc
                JOIN expense_category_mapping m ON m."OldName" = oldc."Name"
                JOIN "Categories" c
                  ON c."Type" = 'Expense'
                 AND c."Name" = m."NewName"
                 AND c."Subcategory" = m."NewSubcategory"
                WHERE f."CategoryId" = oldc."Id"
                  AND oldc."Type" = 'Expense'
                  AND NOT EXISTS (
                      SELECT 1 FROM new_expense_categories n
                      WHERE n."Name" = oldc."Name"
                        AND n."Subcategory" IS NOT DISTINCT FROM oldc."Subcategory");

                UPDATE "FinancialOperations" f
                SET "CategoryId" = (
                    SELECT "Id" FROM "Categories"
                    WHERE "Type" = 'Expense' AND "Name" = 'Varie' AND "Subcategory" = 'AltroVarie'
                    LIMIT 1)
                FROM "Categories" oldc
                WHERE f."CategoryId" = oldc."Id"
                  AND oldc."Type" = 'Expense'
                  AND NOT EXISTS (
                      SELECT 1 FROM new_expense_categories n
                      WHERE n."Name" = oldc."Name"
                        AND n."Subcategory" IS NOT DISTINCT FROM oldc."Subcategory")
                  AND NOT EXISTS (
                      SELECT 1 FROM expense_category_mapping m WHERE m."OldName" = oldc."Name");

                DELETE FROM "Categories" c
                WHERE c."Type" = 'Expense'
                  AND NOT EXISTS (
                      SELECT 1 FROM new_expense_categories n
                      WHERE n."Name" = c."Name"
                        AND n."Subcategory" IS NOT DISTINCT FROM c."Subcategory");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ColorHex",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "Icon",
                table: "Categories");
        }
    }
}
