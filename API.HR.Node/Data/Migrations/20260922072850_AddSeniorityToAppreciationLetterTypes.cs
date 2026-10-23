using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.HR.Node.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSeniorityToAppreciationLetterTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SeniorityDays",
                table: "AppreciationLetterTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SeniorityMonths",
                table: "AppreciationLetterTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SeniorityYears",
                table: "AppreciationLetterTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SeniorityDays",
                table: "AppreciationLetterTypes");

            migrationBuilder.DropColumn(
                name: "SeniorityMonths",
                table: "AppreciationLetterTypes");

            migrationBuilder.DropColumn(
                name: "SeniorityYears",
                table: "AppreciationLetterTypes");
        }
    }
}
