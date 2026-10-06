using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestModules.Outbox.Migrations;

/// <inheritdoc />
// Código gerado por dotnet ef migrations add com timestamp no nome do arquivo (SPEC-0004)
#pragma warning disable MA0048
public partial class Initial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "outbox_test");

        migrationBuilder.CreateTable(
            name: "inbox_messages",
            schema: "outbox_test",
            columns: table => new
            {
                message_id = table.Column<Guid>(type: "uuid", nullable: false),
                consumer = table.Column<string>(type: "text", nullable: false),
                processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_inbox_messages", x => new { x.message_id, x.consumer });
            });

        migrationBuilder.CreateTable(
            name: "items",
            schema: "outbox_test",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                venue_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_items", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "outbox_messages",
            schema: "outbox_test",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                type = table.Column<string>(type: "text", nullable: false),
                payload = table.Column<string>(type: "jsonb", nullable: false),
                occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                status = table.Column<string>(type: "text", nullable: false),
                attempts = table.Column<int>(type: "integer", nullable: false),
                next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                last_error = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_outbox_messages", x => x.id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "inbox_messages",
            schema: "outbox_test");

        migrationBuilder.DropTable(
            name: "items",
            schema: "outbox_test");

        migrationBuilder.DropTable(
            name: "outbox_messages",
            schema: "outbox_test");
    }
}
