using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.HR.Node.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAppreciationLetterTitlesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TitleId",
                table: "AppreciationLetters",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppreciationLetterTitles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppreciationLetterTitles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppreciationLetters_TitleId",
                table: "AppreciationLetters",
                column: "TitleId");

            migrationBuilder.CreateIndex(
                name: "IX_AppreciationLetterTitles_Name",
                table: "AppreciationLetterTitles",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AppreciationLetters_AppreciationLetterTitles_TitleId",
                table: "AppreciationLetters",
                column: "TitleId",
                principalTable: "AppreciationLetterTitles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppreciationLetters_AppreciationLetterTitles_TitleId",
                table: "AppreciationLetters");

            migrationBuilder.DropTable(
                name: "AppreciationLetterTitles");

            migrationBuilder.DropIndex(
                name: "IX_AppreciationLetters_TitleId",
                table: "AppreciationLetters");

            migrationBuilder.DropColumn(
                name: "TitleId",
                table: "AppreciationLetters");
        }
    }
}
