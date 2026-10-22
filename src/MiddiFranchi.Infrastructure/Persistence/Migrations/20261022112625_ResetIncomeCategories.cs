using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiddiFranchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ResetIncomeCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TEMP TABLE new_income_categories (
                    "Name" text NOT NULL,
                    "Icon" text NOT NULL,
                    "ColorHex" text NOT NULL
                ) ON COMMIT DROP;

                INSERT INTO new_income_categories ("Name", "Icon", "ColorHex") VALUES
                    ('Stipendio', 'wallet', '#16a34a'),
                    ('Regali', 'gift', '#ec4899'),
                    ('ExtraStipendio', 'cash', '#14b8a6'),
                    ('Patto', 'doc', '#2563eb'),
                    ('Concorrenza', 'briefcase', '#8b5cf6'),
                    ('Premio Produzione', 'trophy', '#f59e0b'),
                    ('Rimborsi', 'receipt', '#64748b'),
                    ('AltroIncome', 'tag', '#94a3b8');

                INSERT INTO "Categories" ("Id", "Name", "Type", "Description", "Subcategory", "Icon", "ColorHex")
                SELECT gen_random_uuid(), n."Name", 'Income', NULL, NULL, n."Icon", n."ColorHex"
                FROM new_income_categories n
                WHERE NOT EXISTS (
                    SELECT 1 FROM "Categories" c
                    WHERE c."Type" = 'Income'
                      AND c."Name" = n."Name"
                      AND c."Subcategory" IS NULL);

                UPDATE "Categories" c
                SET "Icon" = n."Icon", "ColorHex" = n."ColorHex"
                FROM new_income_categories n
                WHERE c."Type" = 'Income'
                  AND c."Name" = n."Name"
                  AND c."Subcategory" IS NULL;

                CREATE TEMP TABLE income_category_mapping (
                    "OldName" text NOT NULL,
                    "NewName" text NOT NULL
                ) ON COMMIT DROP;

                INSERT INTO income_category_mapping ("OldName", "NewName") VALUES
                    ('Salario', 'Stipendio'),
                    ('Stipendio', 'Stipendio'),
                    ('Entrate extra', 'ExtraStipendio'),
                    ('Lavoro autonomo', 'Patto'),
                    ('Entrate straordinarie', 'Regali'),
                    ('Investimenti', 'AltroIncome'),
                    ('Altre entrate', 'AltroIncome');

                UPDATE "FinancialOperations" f
                SET "CategoryId" = c."Id"
                FROM "Categories" oldc
                JOIN income_category_mapping m ON m."OldName" = oldc."Name"
                JOIN "Categories" c
                  ON c."Type" = 'Income'
                 AND c."Name" = m."NewName"
                 AND c."Subcategory" IS NULL
                WHERE f."CategoryId" = oldc."Id"
                  AND oldc."Type" = 'Income'
                  AND NOT EXISTS (
                      SELECT 1 FROM new_income_categories n
                      WHERE n."Name" = oldc."Name" AND oldc."Subcategory" IS NULL);

                UPDATE "FinancialOperations" f
                SET "CategoryId" = (
                    SELECT "Id" FROM "Categories"
                    WHERE "Type" = 'Income' AND "Name" = 'AltroIncome' AND "Subcategory" IS NULL
                    LIMIT 1)
                FROM "Categories" oldc
                WHERE f."CategoryId" = oldc."Id"
                  AND oldc."Type" = 'Income'
                  AND NOT EXISTS (
                      SELECT 1 FROM new_income_categories n
                      WHERE n."Name" = oldc."Name" AND oldc."Subcategory" IS NULL)
                  AND NOT EXISTS (
                      SELECT 1 FROM income_category_mapping m WHERE m."OldName" = oldc."Name");

                DELETE FROM "Categories" c
                WHERE c."Type" = 'Income'
                  AND NOT EXISTS (
                      SELECT 1 FROM new_income_categories n
                      WHERE n."Name" = c."Name" AND c."Subcategory" IS NULL);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Migrazione dati non reversibile: le categorie di entrata precedenti non sono conservate.
        }
    }
}
