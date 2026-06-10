using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GitShare.Api.Data.Migrations;

/// <inheritdoc />
public partial class FixAnalyzedAtColumnType : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "AnalyzedProfiles"
                ALTER COLUMN "AnalyzedAt" TYPE timestamp with time zone
                USING "AnalyzedAt"::timestamptz;
                """);
        }
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (migrationBuilder.ActiveProvider == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "AnalyzedAt",
                table: "AnalyzedProfiles",
                type: "text",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");
        }
    }
}
