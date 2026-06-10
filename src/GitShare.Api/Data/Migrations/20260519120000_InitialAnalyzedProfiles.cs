using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GitShare.Api.Data.Migrations;

/// <inheritdoc />
public partial class InitialAnalyzedProfiles : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AnalyzedProfiles",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                    .Annotation("Sqlite:Autoincrement", true),
                Username = table.Column<string>(maxLength: 256, nullable: false),
                FullDataJson = table.Column<string>(nullable: false),
                AnalyzedAt = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AnalyzedProfiles", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AnalyzedProfiles_Username",
            table: "AnalyzedProfiles",
            column: "Username",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AnalyzedProfiles");
    }
}
