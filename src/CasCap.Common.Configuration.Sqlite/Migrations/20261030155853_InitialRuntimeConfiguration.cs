using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CasCap.Common.Configuration.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class InitialRuntimeConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cascap_config_revisions",
                columns: table => new
                {
                    application = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    environment = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    contract_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    revision = table.Column<long>(type: "INTEGER", nullable: false),
                    contract_version = table.Column<int>(type: "INTEGER", nullable: false),
                    payload = table.Column<string>(type: "TEXT", nullable: false),
                    recorded_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    actor = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    restored_from_revision = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_revisions", x => new { x.application, x.environment, x.contract_id, x.revision });
                    table.CheckConstraint("ck_revisions_positive", "revision > 0 AND contract_version > 0");
                });

            migrationBuilder.CreateTable(
                name: "cascap_config_heads",
                columns: table => new
                {
                    application = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    environment = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    contract_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    revision = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_heads", x => new { x.application, x.environment, x.contract_id });
                    table.ForeignKey(
                        name: "fk_heads_revisions",
                        columns: x => new { x.application, x.environment, x.contract_id, x.revision },
                        principalTable: "cascap_config_revisions",
                        principalColumns: ["application", "environment", "contract_id", "revision"],
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cascap_config_heads_revision",
                table: "cascap_config_heads",
                columns: ["application", "environment", "contract_id", "revision"]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cascap_config_heads");

            migrationBuilder.DropTable(
                name: "cascap_config_revisions");
        }
    }
}
