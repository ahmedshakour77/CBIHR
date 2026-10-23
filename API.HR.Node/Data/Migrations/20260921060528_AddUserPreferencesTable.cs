using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.HR.Node.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPreferencesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserPreferences",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Appearance = table.Column<int>(type: "int", nullable: false),
                    BrandTheme = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CustomBrandHex = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Density = table.Column<int>(type: "int", nullable: false),
                    Radius = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TextScale = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FontFamily = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    HighContrast = table.Column<bool>(type: "bit", nullable: false),
                    ReducedMotion = table.Column<bool>(type: "bit", nullable: false),
                    DefaultLandingPage = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DataGridPreferencesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPreferences", x => x.UserId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserPreferences");
        }
    }
}
