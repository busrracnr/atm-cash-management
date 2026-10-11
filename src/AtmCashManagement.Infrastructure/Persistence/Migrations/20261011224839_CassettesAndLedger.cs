using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AtmCashManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CassettesAndLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cassettes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    atm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slot = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    denomination = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cassettes", x => x.id);
                    table.CheckConstraint("ck_cassettes_definition", "(type = 'Deposit' AND currency IS NULL AND denomination IS NULL) OR (type = 'Recycling' AND currency IS NOT NULL AND denomination IN (20, 50, 100, 500))");
                    table.CheckConstraint("ck_cassettes_slot", "slot BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_cassettes_atms_atm_id",
                        column: x => x.atm_id,
                        principalTable: "atms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cassette_balances",
                columns: table => new
                {
                    cassette_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    denomination = table.Column<int>(type: "integer", nullable: false),
                    note_count = table.Column<int>(type: "integer", nullable: false),
                    reserved_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cassette_balances", x => new { x.cassette_id, x.currency, x.denomination });
                    table.CheckConstraint("ck_cassette_balances_note_count", "note_count BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_cassette_balances_reserved_count", "reserved_count BETWEEN 0 AND note_count");
                    table.ForeignKey(
                        name: "fk_cassette_balances_cassettes_cassette_id",
                        column: x => x.cassette_id,
                        principalTable: "cassettes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cassette_movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    atm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cassette_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cassette_slot = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    denomination = table.Column<int>(type: "integer", nullable: false),
                    note_count_delta = table.Column<int>(type: "integer", nullable: false),
                    reserved_count_delta = table.Column<int>(type: "integer", nullable: false),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    performed_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cassette_movements", x => x.id);
                    table.ForeignKey(
                        name: "fk_cassette_movements_atms_atm_id",
                        column: x => x.atm_id,
                        principalTable: "atms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cassette_movements_cassettes_cassette_id",
                        column: x => x.cassette_id,
                        principalTable: "cassettes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cassette_movements_atm_id_sequence",
                table: "cassette_movements",
                columns: new[] { "atm_id", "sequence" });

            migrationBuilder.CreateIndex(
                name: "ix_cassette_movements_cassette_id",
                table: "cassette_movements",
                column: "cassette_id");

            migrationBuilder.CreateIndex(
                name: "ix_cassette_movements_sequence",
                table: "cassette_movements",
                column: "sequence",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cassettes_atm_id_slot",
                table: "cassettes",
                columns: new[] { "atm_id", "slot" },
                unique: true);

            // The ledger is append-only in the database as well: UPDATE, DELETE and TRUNCATE are rejected.
            migrationBuilder.Sql("""
                CREATE FUNCTION reject_cassette_movement_change() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'cassette_movements is append-only (% rejected)', TG_OP;
                END;
                $$;

                CREATE TRIGGER cassette_movements_append_only
                    BEFORE UPDATE OR DELETE ON cassette_movements
                    FOR EACH ROW EXECUTE FUNCTION reject_cassette_movement_change();

                CREATE TRIGGER cassette_movements_no_truncate
                    BEFORE TRUNCATE ON cassette_movements
                    FOR EACH STATEMENT EXECUTE FUNCTION reject_cassette_movement_change();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS reject_cassette_movement_change() CASCADE;");

            migrationBuilder.DropTable(
                name: "cassette_balances");

            migrationBuilder.DropTable(
                name: "cassette_movements");

            migrationBuilder.DropTable(
                name: "cassettes");
        }
    }
}
