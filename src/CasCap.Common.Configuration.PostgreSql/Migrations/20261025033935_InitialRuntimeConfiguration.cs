using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CasCap.Common.Configuration.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class InitialRuntimeConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "runtime_configuration");

            migrationBuilder.CreateTable(
                name: "revisions",
                schema: "runtime_configuration",
                columns: table => new
                {
                    application = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    environment = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    contract_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    contract_version = table.Column<int>(type: "integer", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    recorded_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    restored_from_revision = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_revisions", x => new { x.application, x.environment, x.contract_id, x.revision });
                    table.CheckConstraint("ck_revisions_positive", "revision > 0 AND contract_version > 0");
                });

            migrationBuilder.CreateTable(
                name: "heads",
                schema: "runtime_configuration",
                columns: table => new
                {
                    application = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    environment = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    contract_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_heads", x => new { x.application, x.environment, x.contract_id });
                    table.ForeignKey(
                        name: "fk_heads_revisions",
                        columns: x => new { x.application, x.environment, x.contract_id, x.revision },
                        principalSchema: "runtime_configuration",
                        principalTable: "revisions",
                        principalColumns: ["application", "environment", "contract_id", "revision"],
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_heads_revision",
                schema: "runtime_configuration",
                table: "heads",
                columns: ["application", "environment", "contract_id", "revision"]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "heads",
                schema: "runtime_configuration");

            migrationBuilder.DropTable(
                name: "revisions",
                schema: "runtime_configuration");
        }
    }
}
