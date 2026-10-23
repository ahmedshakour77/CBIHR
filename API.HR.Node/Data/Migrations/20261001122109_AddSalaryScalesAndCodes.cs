using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.HR.Node.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSalaryScalesAndCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Stages",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StageNumber",
                table: "Stages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Grades",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Grades",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SalaryScales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GradeId = table.Column<int>(type: "int", nullable: false),
                    StageId = table.Column<int>(type: "int", nullable: false),
                    GradeCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StageCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StageNumber = table.Column<int>(type: "int", nullable: false),
                    GradeSortOrder = table.Column<int>(type: "int", nullable: false),
                    BaseSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AnnualAllowance = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    RequiredServiceYears = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryScales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalaryScales_Grades_GradeId",
                        column: x => x.GradeId,
                        principalTable: "Grades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalaryScales_Stages_StageId",
                        column: x => x.StageId,
                        principalTable: "Stages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalaryScales_GradeCode_StageCode",
                table: "SalaryScales",
                columns: new[] { "GradeCode", "StageCode" });

            migrationBuilder.CreateIndex(
                name: "IX_SalaryScales_GradeId_StageId",
                table: "SalaryScales",
                columns: new[] { "GradeId", "StageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalaryScales_GradeSortOrder",
                table: "SalaryScales",
                column: "GradeSortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryScales_StageId",
                table: "SalaryScales",
                column: "StageId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryScales_StageNumber",
                table: "SalaryScales",
                column: "StageNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalaryScales");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Stages");

            migrationBuilder.DropColumn(
                name: "StageNumber",
                table: "Stages");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Grades");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "Grades");
        }
    }
}
