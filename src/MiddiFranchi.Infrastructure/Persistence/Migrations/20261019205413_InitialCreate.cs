using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MiddiFranchi.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                    table.CheckConstraint("CK_Accounts_CurrencyCode", "\"CurrencyCode\" IN ('EUR', 'USD', 'GBP', 'JPY')");
                    table.CheckConstraint("CK_Accounts_Status", "\"Status\" IN ('Active', 'Closed')");
                    table.CheckConstraint("CK_Accounts_Type", "\"Type\" IN ('Checking', 'Card', 'Cash', 'Liability')");
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.CheckConstraint("CK_Categories_Type", "\"Type\" IN ('Income', 'Expense')");
                });

            migrationBuilder.CreateTable(
                name: "ExchangeRateObservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RateCurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Rate = table.Column<decimal>(type: "numeric", nullable: false),
                    ValidAt = table.Column<DateOnly>(type: "date", nullable: false),
                    ObservedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRateObservations", x => x.Id);
                    table.CheckConstraint("CK_ExchangeRateObservations_CurrencyCode", "\"RateCurrencyCode\" IN ('EUR', 'USD', 'GBP', 'JPY')");
                    table.CheckConstraint("CK_ExchangeRateObservations_Rate", "\"Rate\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "Instruments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    QuoteCurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PriceUnit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NotionalValueAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    NotionalValueCurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Instruments", x => x.Id);
                    table.CheckConstraint("CK_Instruments_NotionalValue", "(\"NotionalValueAmount\" IS NULL AND \"NotionalValueCurrencyCode\" IS NULL) OR (\"NotionalValueAmount\" IS NOT NULL AND \"NotionalValueAmount\" > 0 AND \"NotionalValueCurrencyCode\" IN ('EUR', 'USD', 'GBP', 'JPY'))");
                    table.CheckConstraint("CK_Instruments_NotionalValueCurrencyMatchesQuote", "\"NotionalValueCurrencyCode\" IS NULL OR \"NotionalValueCurrencyCode\" = \"QuoteCurrencyCode\"");
                    table.CheckConstraint("CK_Instruments_PriceUnit", "\"PriceUnit\" IN ('PerUnit', 'PercentOfNominal')");
                    table.CheckConstraint("CK_Instruments_QuoteCurrencyCode", "\"QuoteCurrencyCode\" IN ('EUR', 'USD', 'GBP', 'JPY')");
                });

            migrationBuilder.CreateTable(
                name: "InvestmentPortfolios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ReferenceCurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestmentPortfolios", x => x.Id);
                    table.CheckConstraint("CK_InvestmentPortfolios_ReferenceCurrencyCode", "\"ReferenceCurrencyCode\" IN ('EUR', 'USD', 'GBP', 'JPY')");
                });

            migrationBuilder.CreateTable(
                name: "PriceObservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    PriceCurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    ValidAt = table.Column<DateOnly>(type: "date", nullable: false),
                    ObservedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceObservations", x => x.Id);
                    table.CheckConstraint("CK_PriceObservations_Price", "\"Price\" > 0");
                    table.CheckConstraint("CK_PriceObservations_PriceCurrency", "(\"Unit\" = 'PerUnit' AND \"PriceCurrencyCode\" IS NOT NULL AND \"PriceCurrencyCode\" IN ('EUR', 'USD', 'GBP', 'JPY')) OR (\"Unit\" = 'PercentOfNominal' AND \"PriceCurrencyCode\" IS NULL)");
                    table.CheckConstraint("CK_PriceObservations_Unit", "\"Unit\" IN ('PerUnit', 'PercentOfNominal')");
                    table.ForeignKey(
                        name: "FK_PriceObservations_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinancialOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OperationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OriginalOperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    AmountAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    AmountCurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    SourceAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    DestinationAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceAmountAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    SourceAmountCurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    DestinationAmountAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    DestinationAmountCurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    AppliedRateRate = table.Column<decimal>(type: "numeric", nullable: true),
                    AppliedRateCurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    InvestmentKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    PortfolioId = table.Column<Guid>(type: "uuid", nullable: true),
                    InstrumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    SettlementAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuantityValue = table.Column<decimal>(type: "numeric", nullable: true),
                    QuotedPriceUnit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    QuotedPricePriceAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    QuotedPricePriceCurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    QuotedPricePercentOfNominal = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialOperations", x => x.Id);
                    table.CheckConstraint("CK_FinancialOperations_AmountCurrency", "\"AmountCurrencyCode\" IS NULL OR \"AmountCurrencyCode\" IN ('EUR', 'USD', 'GBP', 'JPY')");
                    table.CheckConstraint("CK_FinancialOperations_AmountPair", "(\"AmountAmount\" IS NULL AND \"AmountCurrencyCode\" IS NULL) OR (\"AmountAmount\" IS NOT NULL AND \"AmountAmount\" > 0 AND \"AmountCurrencyCode\" IS NOT NULL)");
                    table.CheckConstraint("CK_FinancialOperations_AppliedRateCurrency", "\"AppliedRateCurrencyCode\" IS NULL OR \"AppliedRateCurrencyCode\" IN ('EUR', 'USD', 'GBP', 'JPY')");
                    table.CheckConstraint("CK_FinancialOperations_AppliedRatePair", "(\"AppliedRateRate\" IS NULL AND \"AppliedRateCurrencyCode\" IS NULL) OR (\"AppliedRateRate\" IS NOT NULL AND \"AppliedRateRate\" > 0 AND \"AppliedRateCurrencyCode\" IS NOT NULL)");
                    table.CheckConstraint("CK_FinancialOperations_DestinationAmountCurrency", "\"DestinationAmountCurrencyCode\" IS NULL OR \"DestinationAmountCurrencyCode\" IN ('EUR', 'USD', 'GBP', 'JPY')");
                    table.CheckConstraint("CK_FinancialOperations_DestinationAmountPair", "(\"DestinationAmountAmount\" IS NULL AND \"DestinationAmountCurrencyCode\" IS NULL) OR (\"DestinationAmountAmount\" IS NOT NULL AND \"DestinationAmountAmount\" > 0 AND \"DestinationAmountCurrencyCode\" IS NOT NULL)");
                    table.CheckConstraint("CK_FinancialOperations_InvestmentFields", "\"Kind\" <> 'Investment' OR (\"InvestmentKind\" IS NOT NULL AND \"PortfolioId\" IS NOT NULL AND \"SettlementAccountId\" IS NOT NULL)");
                    table.CheckConstraint("CK_FinancialOperations_InvestmentKind", "\"InvestmentKind\" IS NULL OR \"InvestmentKind\" IN ('Buy', 'Sell', 'Dividend', 'Coupon', 'StandaloneFee')");
                    table.CheckConstraint("CK_FinancialOperations_Kind", "\"Kind\" IN ('Income', 'Expense', 'Transfer', 'Investment', 'Reversal')");
                    table.CheckConstraint("CK_FinancialOperations_OrdinaryFields", "\"Kind\" NOT IN ('Income', 'Expense') OR (\"AccountId\" IS NOT NULL AND \"CategoryId\" IS NOT NULL AND \"AmountAmount\" IS NOT NULL AND \"AmountCurrencyCode\" IS NOT NULL)");
                    table.CheckConstraint("CK_FinancialOperations_Quantity", "\"QuantityValue\" IS NULL OR \"QuantityValue\" > 0");
                    table.CheckConstraint("CK_FinancialOperations_QuotedPriceCurrency", "\"QuotedPricePriceCurrencyCode\" IS NULL OR \"QuotedPricePriceCurrencyCode\" IN ('EUR', 'USD', 'GBP', 'JPY')");
                    table.CheckConstraint("CK_FinancialOperations_QuotedPriceShape", "(\"QuotedPriceUnit\" IS NULL AND \"QuotedPricePriceAmount\" IS NULL AND \"QuotedPricePriceCurrencyCode\" IS NULL AND \"QuotedPricePercentOfNominal\" IS NULL) OR (\"QuotedPriceUnit\" = 'PerUnit' AND \"QuotedPricePriceAmount\" IS NOT NULL AND \"QuotedPricePriceAmount\" > 0 AND \"QuotedPricePriceCurrencyCode\" IS NOT NULL AND \"QuotedPricePercentOfNominal\" IS NULL) OR (\"QuotedPriceUnit\" = 'PercentOfNominal' AND \"QuotedPricePercentOfNominal\" IS NOT NULL AND \"QuotedPricePercentOfNominal\" > 0 AND \"QuotedPricePriceAmount\" IS NULL AND \"QuotedPricePriceCurrencyCode\" IS NULL)");
                    table.CheckConstraint("CK_FinancialOperations_QuotedPriceUnit", "\"QuotedPriceUnit\" IS NULL OR \"QuotedPriceUnit\" IN ('PerUnit', 'PercentOfNominal')");
                    table.CheckConstraint("CK_FinancialOperations_ReversalLink", "(\"Kind\" = 'Reversal' AND \"OriginalOperationId\" IS NOT NULL) OR (\"Kind\" <> 'Reversal' AND \"OriginalOperationId\" IS NULL)");
                    table.CheckConstraint("CK_FinancialOperations_SourceAmountCurrency", "\"SourceAmountCurrencyCode\" IS NULL OR \"SourceAmountCurrencyCode\" IN ('EUR', 'USD', 'GBP', 'JPY')");
                    table.CheckConstraint("CK_FinancialOperations_SourceAmountPair", "(\"SourceAmountAmount\" IS NULL AND \"SourceAmountCurrencyCode\" IS NULL) OR (\"SourceAmountAmount\" IS NOT NULL AND \"SourceAmountAmount\" > 0 AND \"SourceAmountCurrencyCode\" IS NOT NULL)");
                    table.CheckConstraint("CK_FinancialOperations_Status", "\"Status\" IN ('Registered', 'Reversed')");
                    table.CheckConstraint("CK_FinancialOperations_TransferFields", "\"Kind\" <> 'Transfer' OR (\"SourceAccountId\" IS NOT NULL AND \"DestinationAccountId\" IS NOT NULL AND \"SourceAmountAmount\" IS NOT NULL AND \"SourceAmountCurrencyCode\" IS NOT NULL AND \"DestinationAmountAmount\" IS NOT NULL AND \"DestinationAmountCurrencyCode\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_FinancialOperations_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialOperations_Accounts_DestinationAccountId",
                        column: x => x.DestinationAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialOperations_Accounts_SettlementAccountId",
                        column: x => x.SettlementAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialOperations_Accounts_SourceAccountId",
                        column: x => x.SourceAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialOperations_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialOperations_FinancialOperations_OriginalOperationId",
                        column: x => x.OriginalOperationId,
                        principalTable: "FinancialOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialOperations_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialOperations_InvestmentPortfolios_PortfolioId",
                        column: x => x.PortfolioId,
                        principalTable: "InvestmentPortfolios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountPostings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FinancialOperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    SignedAmount = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountPostings", x => x.Id);
                    table.CheckConstraint("CK_AccountPostings_CurrencyCode", "\"CurrencyCode\" IN ('EUR', 'USD', 'GBP', 'JPY')");
                    table.CheckConstraint("CK_AccountPostings_Kind", "\"Kind\" IN ('Capital', 'Fee', 'Income', 'TransferLeg')");
                    table.CheckConstraint("CK_AccountPostings_SignByKind", "(\"Kind\" <> 'Income' OR \"SignedAmount\" > 0) AND (\"Kind\" <> 'Fee' OR \"SignedAmount\" < 0)");
                    table.CheckConstraint("CK_AccountPostings_SignedAmount", "\"SignedAmount\" <> 0");
                    table.ForeignKey(
                        name: "FK_AccountPostings_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountPostings_FinancialOperations_FinancialOperationId",
                        column: x => x.FinancialOperationId,
                        principalTable: "FinancialOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvestmentOperationFees",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvestmentOperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AmountAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    AmountCurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DebitAccountId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestmentOperationFees", x => x.Id);
                    table.CheckConstraint("CK_InvestmentOperationFees_Amount", "\"AmountAmount\" > 0");
                    table.CheckConstraint("CK_InvestmentOperationFees_CurrencyCode", "\"AmountCurrencyCode\" IN ('EUR', 'USD', 'GBP', 'JPY')");
                    table.CheckConstraint("CK_InvestmentOperationFees_Kind", "\"Kind\" IN ('BuyFee', 'SellFee', 'Standalone', 'TaxWithheld', 'Other')");
                    table.CheckConstraint("CK_InvestmentOperationFees_Ordinal", "\"Ordinal\" >= 0");
                    table.ForeignKey(
                        name: "FK_InvestmentOperationFees_Accounts_DebitAccountId",
                        column: x => x.DebitAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvestmentOperationFees_FinancialOperations_InvestmentOpera~",
                        column: x => x.InvestmentOperationId,
                        principalTable: "FinancialOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountPostings_AccountId",
                table: "AccountPostings",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountPostings_AccountId_FinancialOperationId",
                table: "AccountPostings",
                columns: new[] { "AccountId", "FinancialOperationId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountPostings_FinancialOperationId",
                table: "AccountPostings",
                column: "FinancialOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRateObservations_RateCurrencyCode_ValidAt_ObservedAt",
                table: "ExchangeRateObservations",
                columns: new[] { "RateCurrencyCode", "ValidAt", "ObservedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancialOperations_AccountId",
                table: "FinancialOperations",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialOperations_CategoryId",
                table: "FinancialOperations",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialOperations_DestinationAccountId",
                table: "FinancialOperations",
                column: "DestinationAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialOperations_InstrumentId",
                table: "FinancialOperations",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialOperations_Kind",
                table: "FinancialOperations",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialOperations_OperationDate",
                table: "FinancialOperations",
                column: "OperationDate");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialOperations_OriginalOperationId",
                table: "FinancialOperations",
                column: "OriginalOperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialOperations_PortfolioId",
                table: "FinancialOperations",
                column: "PortfolioId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialOperations_SettlementAccountId",
                table: "FinancialOperations",
                column: "SettlementAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialOperations_SourceAccountId",
                table: "FinancialOperations",
                column: "SourceAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialOperations_Status",
                table: "FinancialOperations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentOperationFees_DebitAccountId",
                table: "InvestmentOperationFees",
                column: "DebitAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentOperationFees_InvestmentOperationId",
                table: "InvestmentOperationFees",
                column: "InvestmentOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestmentOperationFees_InvestmentOperationId_Ordinal",
                table: "InvestmentOperationFees",
                columns: new[] { "InvestmentOperationId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceObservations_InstrumentId_ValidAt_ObservedAt",
                table: "PriceObservations",
                columns: new[] { "InstrumentId", "ValidAt", "ObservedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountPostings");

            migrationBuilder.DropTable(
                name: "ExchangeRateObservations");

            migrationBuilder.DropTable(
                name: "InvestmentOperationFees");

            migrationBuilder.DropTable(
                name: "PriceObservations");

            migrationBuilder.DropTable(
                name: "FinancialOperations");

            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Instruments");

            migrationBuilder.DropTable(
                name: "InvestmentPortfolios");
        }
    }
}
