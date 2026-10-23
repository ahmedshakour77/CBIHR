using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.HR.Node.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJobClassificationFieldsToJobTitle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "JobTitles",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GradeId",
                table: "JobTitles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GradeName",
                table: "JobTitles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JobFamily",
                table: "JobTitles",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobTitles_Code",
                table: "JobTitles",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_JobTitles_GradeId",
                table: "JobTitles",
                column: "GradeId");

            migrationBuilder.CreateIndex(
                name: "IX_JobTitles_JobFamily",
                table: "JobTitles",
                column: "JobFamily");

            migrationBuilder.AddForeignKey(
                name: "FK_JobTitles_Grades_GradeId",
                table: "JobTitles",
                column: "GradeId",
                principalTable: "Grades",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JobTitles_Grades_GradeId",
                table: "JobTitles");

            migrationBuilder.DropIndex(
                name: "IX_JobTitles_Code",
                table: "JobTitles");

            migrationBuilder.DropIndex(
                name: "IX_JobTitles_GradeId",
                table: "JobTitles");

            migrationBuilder.DropIndex(
                name: "IX_JobTitles_JobFamily",
                table: "JobTitles");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "JobTitles");

            migrationBuilder.DropColumn(
                name: "GradeId",
                table: "JobTitles");

            migrationBuilder.DropColumn(
                name: "GradeName",
                table: "JobTitles");

            migrationBuilder.DropColumn(
                name: "JobFamily",
                table: "JobTitles");
        }
    }
}
