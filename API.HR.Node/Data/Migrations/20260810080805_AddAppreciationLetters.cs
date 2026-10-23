using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.HR.Node.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAppreciationLetters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppreciationLetterTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppreciationLetterTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppreciationLetters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LetterDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Count = table.Column<int>(type: "int", nullable: true),
                    TypeId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LetterNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppreciationLetters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppreciationLetters_AppreciationLetterTypes_TypeId",
                        column: x => x.TypeId,
                        principalTable: "AppreciationLetterTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppreciationLetters_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppreciationLetters_EmployeeId_LetterDate",
                table: "AppreciationLetters",
                columns: new[] { "EmployeeId", "LetterDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AppreciationLetters_TypeId",
                table: "AppreciationLetters",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppreciationLetterTypes_Name",
                table: "AppreciationLetterTypes",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppreciationLetters");

            migrationBuilder.DropTable(
                name: "AppreciationLetterTypes");
        }
    }
}
