using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Juker.IntegrationTests.CleanMigration.Migrations;

/// <inheritdoc />
// Código gerado por dotnet ef migrations add com timestamp no nome do arquivo (SPEC-0027:IT-01)
#pragma warning disable MA0048
public partial class CreateGadgets : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "clean_migration");

        migrationBuilder.CreateTable(
            name: "gadgets",
            schema: "clean_migration",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_gadgets", x => x.Id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "gadgets",
            schema: "clean_migration");
    }
}
