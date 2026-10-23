using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.HR.Node.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverToEmployeeVehicles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
            {
                migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EmployeeVehicles') AND name = 'DriverName')
BEGIN
    ALTER TABLE [EmployeeVehicles] ADD [DriverName] nvarchar(150) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EmployeeVehicles') AND name = 'DriverPhone')
BEGIN
    ALTER TABLE [EmployeeVehicles] ADD [DriverPhone] nvarchar(30) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EmployeeVehicles') AND name = 'IsDriverSelf')
BEGIN
    ALTER TABLE [EmployeeVehicles] ADD [IsDriverSelf] bit NOT NULL DEFAULT 1;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EmployeePassports') AND name = 'IsElectronic')
BEGIN
    ALTER TABLE [EmployeePassports] ADD [IsElectronic] bit NOT NULL DEFAULT 0;
END
");
            }
            else
            {
                migrationBuilder.AddColumn<string>(
                    name: "DriverName",
                    table: "EmployeeVehicles",
                    type: "nvarchar(150)",
                    maxLength: 150,
                    nullable: true);

                migrationBuilder.AddColumn<string>(
                    name: "DriverPhone",
                    table: "EmployeeVehicles",
                    type: "nvarchar(30)",
                    maxLength: 30,
                    nullable: true);

                migrationBuilder.AddColumn<bool>(
                    name: "IsDriverSelf",
                    table: "EmployeeVehicles",
                    type: "bit",
                    nullable: false,
                    defaultValue: true);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
            {
                migrationBuilder.Sql(@"
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EmployeeVehicles') AND name = 'DriverName')
BEGIN
    ALTER TABLE [EmployeeVehicles] DROP COLUMN [DriverName];
END

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EmployeeVehicles') AND name = 'DriverPhone')
BEGIN
    ALTER TABLE [EmployeeVehicles] DROP COLUMN [DriverPhone];
END

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EmployeeVehicles') AND name = 'IsDriverSelf')
BEGIN
    ALTER TABLE [EmployeeVehicles] DROP COLUMN [IsDriverSelf];
END
");
            }
            else
            {
                migrationBuilder.DropColumn(
                    name: "DriverName",
                    table: "EmployeeVehicles");

                migrationBuilder.DropColumn(
                    name: "DriverPhone",
                    table: "EmployeeVehicles");

                migrationBuilder.DropColumn(
                    name: "IsDriverSelf",
                    table: "EmployeeVehicles");
            }
        }
    }
}
