using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.HR.Node.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdministrativeUnitToEmployee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AdministrativeUnitId",
                table: "Employees",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_AdministrativeUnitId",
                table: "Employees",
                column: "AdministrativeUnitId");

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_AdministrativeUnits_AdministrativeUnitId",
                table: "Employees",
                column: "AdministrativeUnitId",
                principalTable: "AdministrativeUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_AdministrativeUnits_AdministrativeUnitId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_AdministrativeUnitId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "AdministrativeUnitId",
                table: "Employees");
        }
    }
}
