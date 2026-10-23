using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.HR.Node.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddResidenceCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "DefaultLatitude",
                table: "Governorates",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "DefaultLongitude",
                table: "Governorates",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "EmployeeResidences",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "EmployeeResidences",
                type: "float",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Governorates",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "DefaultLatitude", "DefaultLongitude" },
                values: new object[] { 33.315199999999997, 44.366100000000003 });

            migrationBuilder.UpdateData(
                table: "Governorates",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "DefaultLatitude", "DefaultLongitude" },
                values: new object[] { 30.508099999999999, 47.783499999999997 });

            migrationBuilder.UpdateData(
                table: "Governorates",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "DefaultLatitude", "DefaultLongitude" },
                values: new object[] { 36.3489, 43.157699999999998 });

            migrationBuilder.UpdateData(
                table: "Governorates",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "DefaultLatitude", "DefaultLongitude" },
                values: new object[] { 36.190100000000001, 44.009099999999997 });

            migrationBuilder.UpdateData(
                table: "Governorates",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "DefaultLatitude", "DefaultLongitude" },
                values: new object[] { 32.0259, 44.346200000000003 });

            migrationBuilder.UpdateData(
                table: "Governorates",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "DefaultLatitude", "DefaultLongitude" },
                values: new object[] { 32.616, 44.024900000000002 });

            migrationBuilder.UpdateData(
                table: "Governorates",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "DefaultLatitude", "DefaultLongitude" },
                values: new object[] { 35.4681, 44.392200000000003 });

            migrationBuilder.UpdateData(
                table: "Governorates",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "DefaultLatitude", "DefaultLongitude" },
                values: new object[] { 33.423299999999998, 43.3033 });

            migrationBuilder.UpdateData(
                table: "Governorates",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "DefaultLatitude", "DefaultLongitude" },
                values: new object[] { 32.483499999999999, 44.43 });

            migrationBuilder.UpdateData(
                table: "Governorates",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "DefaultLatitude", "DefaultLongitude" },
                values: new object[] { 35.566899999999997, 45.416499999999999 });

            migrationBuilder.UpdateData(
                table: "Governorates",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "DefaultLatitude", "DefaultLongitude" },
                values: new object[] { 36.867899999999999, 42.990600000000001 });

            migrationBuilder.UpdateData(
                table: "Governorates",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "DefaultLatitude", "DefaultLongitude" },
                values: new object[] { 33.746299999999998, 44.643300000000004 });

            migrationBuilder.UpdateData(
                table: "Governorates",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "DefaultLatitude", "DefaultLongitude" },
                values: new object[] { 34.606200000000001, 43.6783 });

            migrationBuilder.UpdateData(
                table: "Governorates",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "DefaultLatitude", "DefaultLongitude" },
                values: new object[] { 32.512500000000003, 45.819699999999997 });

            migrationBuilder.UpdateData(
                table: "Governorates",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "DefaultLatitude", "DefaultLongitude" },
                values: new object[] { 31.841699999999999, 47.144399999999997 });

            migrationBuilder.UpdateData(
                table: "Governorates",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "DefaultLatitude", "DefaultLongitude" },
                values: new object[] { 31.0579, 46.257300000000001 });

            migrationBuilder.UpdateData(
                table: "Governorates",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "DefaultLatitude", "DefaultLongitude" },
                values: new object[] { 31.3142, 45.281100000000002 });

            migrationBuilder.UpdateData(
                table: "Governorates",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "DefaultLatitude", "DefaultLongitude" },
                values: new object[] { 31.9922, 44.924399999999999 });

            migrationBuilder.UpdateData(
                table: "Governorates",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "DefaultLatitude", "DefaultLongitude" },
                values: new object[] { 35.177799999999998, 45.9861 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultLatitude",
                table: "Governorates");

            migrationBuilder.DropColumn(
                name: "DefaultLongitude",
                table: "Governorates");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "EmployeeResidences");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "EmployeeResidences");
        }
    }
}
