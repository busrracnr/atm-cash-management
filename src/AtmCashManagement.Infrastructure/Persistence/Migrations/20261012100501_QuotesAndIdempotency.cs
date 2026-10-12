using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AtmCashManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class QuotesAndIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "idempotency_records",
                columns: table => new
                {
                    scope = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_body = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_idempotency_records", x => new { x.scope, x.key });
                });

            migrationBuilder.CreateTable(
                name: "withdrawal_quotes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    atm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_hash = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    requested_amount = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    limit_amount = table.Column<int>(type: "integer", nullable: false),
                    confirmed_option = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    dispensed_amount = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    business_date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_withdrawal_quotes", x => x.id);
                    table.CheckConstraint("ck_withdrawal_quotes_amounts", "requested_amount > 0 AND limit_amount >= 0");
                    table.ForeignKey(
                        name: "fk_withdrawal_quotes_atms_atm_id",
                        column: x => x.atm_id,
                        principalTable: "atms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "withdrawal_quote_lines",
                columns: table => new
                {
                    option = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    slot = table.Column<int>(type: "integer", nullable: false),
                    quote_id = table.Column<Guid>(type: "uuid", nullable: false),
                    denomination = table.Column<int>(type: "integer", nullable: false),
                    count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_withdrawal_quote_lines", x => new { x.quote_id, x.option, x.slot });
                    table.ForeignKey(
                        name: "fk_withdrawal_quote_lines_withdrawal_quotes_quote_id",
                        column: x => x.quote_id,
                        principalTable: "withdrawal_quotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_withdrawal_quotes_atm_id_expires_at",
                table: "withdrawal_quotes",
                columns: new[] { "atm_id", "expires_at" },
                filter: "status = 'Open'");

            migrationBuilder.CreateIndex(
                name: "ix_withdrawal_quotes_card_hash_currency_business_date",
                table: "withdrawal_quotes",
                columns: new[] { "card_hash", "currency", "business_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idempotency_records");

            migrationBuilder.DropTable(
                name: "withdrawal_quote_lines");

            migrationBuilder.DropTable(
                name: "withdrawal_quotes");
        }
    }
}
