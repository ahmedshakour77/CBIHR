using API.HR.Node.Data.OrganizationalHierarchy;
using API.HR.Node.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace API.HR.Node.Data;

/// <summary>
/// Startup database work: migrations (switchable), the role catalog, the
/// bootstrap administrator, and reference-data seeders.
///
/// The bootstrap administrator is created once and then left alone. Its
/// password comes only from configuration (ADMIN_INITIAL_PASSWORD, kept in
/// user secrets or an environment variable); it is never reset on restart and
/// its roles are never re-granted, so changing the password or revoking access
/// in the Users screen actually sticks.
/// </summary>
public static class StartupSeeder
{
    private const string BootstrapEmail = "systemadmin@hr.node";
    private const string BootstrapUserName = "systemadmin";

    private static readonly string[] AllRoles = ["Admin", "HRManager", "FormationManager", "Supervisor", "Employee"];

    private static readonly string[] AllPermissions = FeaturePermissionCatalog.AllPermissionCodes.ToArray();

    public static async Task RunAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        var logger = services.GetRequiredService<ILogger<ApplicationDbContext>>();

        // Migrate on startup defaults to true ONLY in Development. In Production, migrations
        // must be applied via deployment pipelines unless explicitly configured.
        var defaultMigrate = app.Environment.IsDevelopment();
        if (app.Configuration.GetValue("Database:MigrateOnStartup", defaultMigrate))
        {
            await dbContext.Database.MigrateAsync();
        }

        // Fail-safe raw DDL operations (CREATE/ALTER TABLE) are restricted strictly to Development
        if (app.Environment.IsDevelopment())
        {
            await EnsureEmployeeNationalCardsTableAsync(dbContext, logger);
            await EnsureEmployeePassportsTableAsync(dbContext, logger);
            await EnsureEmployeeVehiclesDriverColumnsAsync(dbContext, logger);
        }

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var roleName in AllRoles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        await EnsureBootstrapAdminAsync(services, app.Configuration, logger);

        await API.HR.Node.Services.Workflows.WorkflowSeeder.SeedWorkflowsAsync(services);
        await OrganizationalHierarchySeeder.SeedAsync(services);
        await API.HR.Node.Services.Salary.SalaryScaleDataSeeder.SeedAsync(dbContext);
        await API.HR.Node.Services.Allowances.AllowanceDataSeeder.SeedAsync(dbContext);
        await MaritalStatusDataSeeder.SeedAsync(dbContext); // الحالة الاجتماعية
        await MartyrDataSeeder.SeedAsync(dbContext);        // ذوي الشهداء
        await PenaltyTypeDataSeeder.SeedAsync(dbContext);   // العقوبات الانضباطية
    }

    private static async Task EnsureBootstrapAdminAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        var existing = await userManager.FindByEmailAsync(BootstrapEmail)
            ?? await userManager.FindByNameAsync(BootstrapUserName);
        if (existing is not null)
        {
            return;
        }

        var initialPassword = configuration["ADMIN_INITIAL_PASSWORD"];
        if (string.IsNullOrWhiteSpace(initialPassword))
        {
            logger.LogWarning(
                "No administrator account exists and ADMIN_INITIAL_PASSWORD is not configured; skipping bootstrap. " +
                "Set it with 'dotnet user-secrets set ADMIN_INITIAL_PASSWORD <password>' and restart.");
            return;
        }

        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        if (!await dbContext.AllowedUsers.AnyAsync(a => a.Email == BootstrapEmail))
        {
            dbContext.AllowedUsers.Add(new AllowedUser
            {
                Email = BootstrapEmail,
                FullName = "System Administrator",
                Notes = "Bootstrap administrator",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        var admin = new ApplicationUser
        {
            UserName = BootstrapUserName,
            Email = BootstrapEmail,
            EmailConfirmed = true
        };
        var created = await userManager.CreateAsync(admin, initialPassword);
        if (!created.Succeeded)
        {
            logger.LogError("Failed to create the bootstrap administrator: {Errors}",
                string.Join(", ", created.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRolesAsync(admin, AllRoles);
        foreach (var permission in AllPermissions)
        {
            await userManager.AddClaimAsync(admin, new System.Security.Claims.Claim("permission", permission));
        }

        logger.LogInformation("Created the bootstrap administrator '{UserName}'. Change its password after first sign-in.", BootstrapUserName);
    }

    private static async Task EnsureEmployeeNationalCardsTableAsync(ApplicationDbContext dbContext, ILogger logger)
    {
        try
        {
            await dbContext.Database.ExecuteSqlRawAsync(@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EmployeeNationalCards')
BEGIN
    CREATE TABLE [EmployeeNationalCards] (
        [Id] int NOT NULL IDENTITY(1,1),
        [EmployeeId] int NOT NULL,
        [DocumentNumber] nvarchar(50) NOT NULL,
        [NationalIdNumber] nvarchar(50) NULL,
        [FullNameAr] nvarchar(300) NULL,
        [FirstNameEn] nvarchar(150) NULL,
        [LastNameEn] nvarchar(150) NULL,
        [FullNameEn] nvarchar(300) NULL,
        [DateOfBirth] date NULL,
        [Gender] nvarchar(20) NULL,
        [Nationality] nvarchar(100) NULL,
        [IssuingState] nvarchar(10) NOT NULL DEFAULT 'IRQ',
        [IssueDate] date NULL,
        [ExpiryDate] date NULL,
        [RawMrz] nvarchar(200) NULL,
        [BiometricPhotoPath] nvarchar(500) NULL,
        [FrontImagePath] nvarchar(500) NULL,
        [BackImagePath] nvarchar(500) NULL,
        [IsPassiveAuthenticationValid] bit NOT NULL DEFAULT 0,
        [VerificationTimestamp] datetime2 NULL,
        [SodHash] nvarchar(256) NULL,
        [CertificateIssuer] nvarchar(250) NULL,
        [IsActive] bit NOT NULL DEFAULT 1,
        [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
        [CreatedBy] nvarchar(150) NULL,
        [Notes] nvarchar(500) NULL,
        CONSTRAINT [PK_EmployeeNationalCards] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmployeeNationalCards_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_EmployeeNationalCards_DocumentNumber] ON [EmployeeNationalCards] ([DocumentNumber]);
    CREATE INDEX [IX_EmployeeNationalCards_NationalIdNumber] ON [EmployeeNationalCards] ([NationalIdNumber]);
    CREATE INDEX [IX_EmployeeNationalCards_EmployeeId] ON [EmployeeNationalCards] ([EmployeeId]);
END");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not run EnsureEmployeeNationalCardsTableAsync. Migrations will handle table creation.");
        }
    }

    private static async Task EnsureEmployeePassportsTableAsync(ApplicationDbContext dbContext, ILogger logger)
    {
        try
        {
            await dbContext.Database.ExecuteSqlRawAsync(@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EmployeePassports')
BEGIN
    CREATE TABLE [EmployeePassports] (
        [Id] int NOT NULL IDENTITY(1,1),
        [EmployeeId] int NOT NULL,
        [PassportNumber] nvarchar(50) NOT NULL,
        [PassportType] nvarchar(30) NOT NULL DEFAULT 'عادي (P)',
        [CountryCode] nvarchar(10) NOT NULL DEFAULT 'IRQ',
        [IssuingCountry] nvarchar(100) NOT NULL DEFAULT N'العراق',
        [IssuingAuthority] nvarchar(150) NULL,
        [NationalIdNumber] nvarchar(50) NULL,
        [FullNameAr] nvarchar(300) NULL,
        [FullNameEn] nvarchar(300) NULL,
        [MotherName] nvarchar(150) NULL,
        [PlaceOfBirth] nvarchar(150) NULL,
        [DateOfBirth] date NULL,
        [Gender] nvarchar(20) NULL,
        [IssueDate] date NULL,
        [ExpiryDate] date NULL,
        [RawMrz] nvarchar(200) NULL,
        [PassportImageKey] nvarchar(500) NULL,
        [PassportImageFileName] nvarchar(255) NULL,
        [PassportImageContentType] nvarchar(100) NULL,
        [BiometricPhotoKey] nvarchar(500) NULL,
        [BiometricPhotoFileName] nvarchar(255) NULL,
        [BiometricPhotoContentType] nvarchar(100) NULL,
        [IsElectronic] bit NOT NULL DEFAULT 0,
        [IsActive] bit NOT NULL DEFAULT 1,
        [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
        [Notes] nvarchar(500) NULL,
        CONSTRAINT [PK_EmployeePassports] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmployeePassports_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_EmployeePassports_PassportNumber] ON [EmployeePassports] ([PassportNumber]);
    CREATE INDEX [IX_EmployeePassports_NationalIdNumber] ON [EmployeePassports] ([NationalIdNumber]);
    CREATE INDEX [IX_EmployeePassports_EmployeeId] ON [EmployeePassports] ([EmployeeId]);
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EmployeePassports') AND name = 'IsElectronic')
    BEGIN
        ALTER TABLE [EmployeePassports] ADD [IsElectronic] bit NOT NULL DEFAULT 0;
    END
END");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not run EnsureEmployeePassportsTableAsync. Migrations will handle table creation.");
        }
    }

    private static async Task EnsureEmployeeVehiclesDriverColumnsAsync(ApplicationDbContext dbContext, ILogger logger)
    {
        try
        {
            await dbContext.Database.ExecuteSqlRawAsync(@"
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'EmployeeVehicles')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EmployeeVehicles') AND name = 'IsDriverSelf')
    BEGIN
        ALTER TABLE [EmployeeVehicles] ADD [IsDriverSelf] bit NOT NULL DEFAULT 1;
    END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EmployeeVehicles') AND name = 'DriverName')
    BEGIN
        ALTER TABLE [EmployeeVehicles] ADD [DriverName] nvarchar(150) NULL;
    END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('EmployeeVehicles') AND name = 'DriverPhone')
    BEGIN
        ALTER TABLE [EmployeeVehicles] ADD [DriverPhone] nvarchar(30) NULL;
    END
END");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not run EnsureEmployeeVehiclesDriverColumnsAsync. Migrations will handle schema update.");
        }
    }
}
