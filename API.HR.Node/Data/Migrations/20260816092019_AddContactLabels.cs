using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.HR.Node.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContactLabels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Label",
                table: "EmployeePhones",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Label",
                table: "EmployeeEmails",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Label",
                table: "EmployeePhones");

            migrationBuilder.DropColumn(
                name: "Label",
                table: "EmployeeEmails");
        }
    }
}
