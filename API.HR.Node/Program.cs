using API.HR.Node.Components;
using API.HR.Node.Components.Account;
using API.HR.Node.Data;
using API.HR.Node.Endpoints;
using API.HR.Node.Options;
using API.HR.Node.Security;
using API.HR.Node.Services.Audit;
using API.HR.Node.Services.Images;
using API.HR.Node.Services.ProfilePhotos;
using API.HR.Node.Services.Logging;
using API.HR.Node.Services.Security;
using API.HR.Node.Services;
using API.HR.Node.Services.Storage;
using API.HR.Node.Services.Workflows;
using API.HR.Node.Services.Localization;
using API.HR.Node.Services.OrganizationalHierarchy;
using API.HR.Node.Services.Notifications;
using API.HR.Node.Services.DesignSystem;
using API.HR.Node.Services.Pdf;
using API.HR.Node.Services.Feedback;
using API.HR.Node.Services.IdentityDocuments;
using API.HR.Node.Data.OrganizationalHierarchy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Fido2NetLib;
using QuestPDF.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;

// Configure QuestPDF Community License and Font Resolution
QuestPDF.Settings.License = LicenseType.Community;
QuestPDF.Settings.UseSystemFonts = true;
QuestPDF.Settings.ThrowOnMissingFontFamilies = false;

var builder = WebApplication.CreateBuilder(args);

// Client secrets live outside the project folder (dotnet user-secrets, or
// environment variables), never in appsettings.json. Development loads user
// secrets by default; this makes the same store available when the app runs
// under another environment name on this server.
if (!builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}

// Disable Kestrel Server header for security
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

// Configure Physical File Logger (logs/app-log-YYYY-MM-DD.log)
builder.Logging.AddFileLogger(options =>
{
    options.LogDirectory = Path.Combine(builder.Environment.ContentRootPath, "logs");
    options.FilePrefix = "app-log-";
});

// Configure Persistent Data Protection Keys (ensures Bearer tokens persist across restarts)
var keysFolder = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtection-Keys");
var dataProtection = builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysFolder))
    .SetApplicationName("API.HR.Node");

// These keys sign every login cookie and bearer token and encrypt the Canva
// token. Encrypt them at rest so a copied App_Data folder (backup, file share)
// cannot mint sessions. Machine scope: they still open if the app moves to a
// service account on this server, but not on any other machine.
if (OperatingSystem.IsWindows())
{
    dataProtection.ProtectKeysWithDpapi(protectToLocalMachine: true);
}

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddProblemDetails();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();

// Rate limiting for auth and sensitive APIs
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth-limit", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });

    // Basic auth checks a password hash on every request, on any endpoint, so it
    // needs its own per-IP budget: it stops password spraying across accounts and
    // keeps a flood of deliberately slow hash checks from pinning the CPU.
    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var client = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // The sign-in form posts to a Razor component, not an API endpoint, so the
        // "auth-limit" policy never reached it. Each post checks a password (against
        // the directory first), so it gets a per-IP budget of its own. LoginThrottle
        // adds the per-account count inside the page.
        if (IsSignInPost(context))
        {
            return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter("signin:" + client, _ =>
                new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });
        }

        var authorization = context.Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("other");
        }

        return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter("basic:" + client, _ =>
            new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });

    // A refused sign-in post goes back to the form with a message, not to a bare 429 page.
    options.OnRejected = (rejected, _) =>
    {
        if (IsSignInPost(rejected.HttpContext))
        {
            rejected.HttpContext.Response.Redirect("/Account/Login?throttled=1");
        }
        return ValueTask.CompletedTask;
    };

    static bool IsSignInPost(HttpContext context) =>
        HttpMethods.IsPost(context.Request.Method)
        && context.Request.Path.StartsWithSegments("/Account", StringComparison.OrdinalIgnoreCase)
        && (context.Request.Path.Value ?? "").Contains("/Login", StringComparison.OrdinalIgnoreCase);
});
builder.Services.AddSingleton<API.HR.Node.Services.Security.LoginThrottle>();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

// Profile Photo Options & Core Services
builder.Services.Configure<ProfilePhotoOptions>(
    builder.Configuration.GetSection(ProfilePhotoOptions.SectionName));

builder.Services.AddSingleton<IFileStorageService, LocalFileStorageService>();
// Microsoft Defender when present (every Windows Server); the placeholder only where it is not.
var defenderPath = DefenderFileMalwareScanner.FindEngine();
if (defenderPath is not null)
{
    var quarantine = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "ScanQuarantine");
    builder.Services.AddSingleton<IFileMalwareScanner>(sp => new DefenderFileMalwareScanner(
        defenderPath, quarantine, sp.GetRequiredService<ILogger<DefenderFileMalwareScanner>>()));
}
else
{
    builder.Services.AddSingleton<IFileMalwareScanner, NullFileMalwareScanner>();
}
builder.Services.AddSingleton<IImageProcessor, ImageSharpProcessor>();
builder.Services.AddScoped<IAuditLogger, AuditLogger>();
builder.Services.AddScoped<IEmployeeProfilePhotoService, EmployeeProfilePhotoService>();
builder.Services.AddSingleton<API.HR.Node.Services.ProfilePhotos.IUserAvatarService, API.HR.Node.Services.ProfilePhotos.UserAvatarService>();
builder.Services.AddScoped<IMicrosoftGraphService, MicrosoftGraphService>();
builder.Services.AddScoped<IPasskeyService, PasskeyService>();
builder.Services.AddScoped<IActiveDirectoryAuthService, ActiveDirectoryAuthService>();
builder.Services.AddSingleton<API.HR.Node.Services.Network.AmlConnectivityService>();
builder.Services.AddSingleton<API.HR.Node.Services.Network.IAmlConnectivityService>(sp => sp.GetRequiredService<API.HR.Node.Services.Network.AmlConnectivityService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<API.HR.Node.Services.Network.AmlConnectivityService>());
builder.Services.AddScoped<ILanguageService, LanguageService>();
builder.Services.AddScoped<IToastService, ToastService>();
builder.Services.AddScoped<API.HR.Node.Services.Employees.HomeDashboardViewState>();
builder.Services.AddSingleton<API.HR.Node.Services.Dashboard.WorkforceSnapshotService>();
builder.Services.AddScoped<IUserPreferenceService, UserPreferenceService>();
builder.Services.AddScoped<IOrganizationalHierarchyService, OrganizationalHierarchyService>();
builder.Services.AddScoped<IJobHierarchyService, JobHierarchyService>();
builder.Services.AddScoped<API.HR.Node.Services.OrganizationalHierarchy.IFormationKpiService, API.HR.Node.Services.OrganizationalHierarchy.FormationKpiService>();
builder.Services.AddScoped<IEmployeeDossierPdfService, EmployeeDossierPdfService>();
builder.Services.AddScoped<API.HR.Node.Services.Import.IExcelImportService, API.HR.Node.Services.Import.ExcelImportService>();
builder.Services.AddScoped<API.HR.Node.Services.Salary.ISalaryScaleService, API.HR.Node.Services.Salary.SalaryScaleService>();
builder.Services.AddScoped<API.HR.Node.Services.Salary.IFormationPayrollService, API.HR.Node.Services.Salary.FormationPayrollService>();
builder.Services.AddScoped<API.HR.Node.Services.Allowances.IEmployeeAllowanceService, API.HR.Node.Services.Allowances.EmployeeAllowanceService>();
builder.Services.AddScoped<API.HR.Node.Services.Financial.IEmployeeFinancialService, API.HR.Node.Services.Financial.EmployeeFinancialService>();
builder.Services.AddScoped<API.HR.Node.Services.Employees.IEmployeeLifecycleService, API.HR.Node.Services.Employees.EmployeeLifecycleService>();
builder.Services.AddScoped<API.HR.Node.Services.Employees.IEmployeeVehicleService, API.HR.Node.Services.Employees.EmployeeVehicleService>();
builder.Services.AddScoped<IEmployeeFeedbackService, EmployeeFeedbackService>();
builder.Services.AddScoped<INationalCardService, NationalCardService>();
builder.Services.AddScoped<IPassportReaderService, PassportReaderService>();
builder.Services.AddScoped<IFeedbackModalState, FeedbackModalState>();
builder.Services.AddSingleton<API.HR.Node.Services.Features.ISystemFeatureService, API.HR.Node.Services.Features.SystemFeatureService>();
builder.Services.AddMemoryCache();
builder.Services.Configure<API.HR.Node.Services.Canva.CanvaOptions>(
    builder.Configuration.GetSection(API.HR.Node.Services.Canva.CanvaOptions.SectionName));
builder.Services.AddHttpClient<API.HR.Node.Services.Canva.ICanvaService, API.HR.Node.Services.Canva.CanvaService>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, ApplicationUserClaimsPrincipalFactory>();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
});

// General Workflow Engine DI Registrations
builder.Services.AddScoped<IWorkflowAssignmentResolver, WorkflowAssignmentResolver>();
builder.Services.AddSingleton<IWorkflowConditionEvaluator, WorkflowConditionEvaluator>();
builder.Services.AddScoped<IWorkflowTimelineService, WorkflowTimelineService>();
builder.Services.AddScoped<IWorkflowAuthorizationService, WorkflowAuthorizationService>();
builder.Services.AddScoped<IWorkflowActionHandler, AppreciationLetterWorkflowHandler>();
builder.Services.AddScoped<IWorkflowActionHandler, WorkContinuationWorkflowHandler>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IWorkflowEngine, WorkflowEngine>();

// Passkey Server-Side Challenge Store
builder.Services.AddSingleton<IPasskeyChallengeStore, PasskeyChallengeStore>();

// FIDO2 / WebAuthn Passkeys Configuration
var isDevEnvironment = builder.Environment.IsDevelopment();
var fidoServerDomain = builder.Configuration["Fido2:ServerDomain"] ?? "portal.aml.lan";
var fidoServerName = builder.Configuration["Fido2:ServerName"] ?? "API.HR.Node Passkeys";

var allowedFidoOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

if (isDevEnvironment)
{
    allowedFidoOrigins.Add("https://localhost:7266");
    allowedFidoOrigins.Add("http://localhost:5058");
    allowedFidoOrigins.Add("https://localhost:5058");
    allowedFidoOrigins.Add("https://localhost");
    allowedFidoOrigins.Add("http://localhost");
    allowedFidoOrigins.Add("https://127.0.0.1:7266");
    allowedFidoOrigins.Add("http://127.0.0.1:5058");
    allowedFidoOrigins.Add("https://127.0.0.1:5058");
    allowedFidoOrigins.Add("http://127.0.0.1");
    allowedFidoOrigins.Add("https://127.0.0.1");
    allowedFidoOrigins.Add("android:apk-key-hash:g19ckDQXRW8bvU93OuYGM1-SFSdIhhncew9p6l3NUBM");
}
else
{
    // Production Mode: Strict enterprise HTTPS origins only, static domain, NO http, NO localhost, NO 127.0.0.1
    var configuredOrigins = builder.Configuration.GetSection("Fido2:Origins").Get<string[]>();
    if (configuredOrigins != null && configuredOrigins.Length > 0)
    {
        foreach (var o in configuredOrigins.Where(o => o.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || o.StartsWith("android:", StringComparison.OrdinalIgnoreCase)))
        {
            allowedFidoOrigins.Add(o);
        }
    }
    allowedFidoOrigins.Add("https://portal.aml.lan");
    allowedFidoOrigins.Add("https://apihr.iraqservice.uk");
    allowedFidoOrigins.Add("android:apk-key-hash:g19ckDQXRW8bvU93OuYGM1-SFSdIhhncew9p6l3NUBM");
}

var fido2Config = new Fido2Configuration
{
    ServerDomain = fidoServerDomain,
    ServerName = fidoServerName,
    Origins = allowedFidoOrigins,
    TimestampDriftTolerance = 300000
};
builder.Services.AddSingleton(fido2Config);

builder.Services.AddScoped<IFido2>(sp =>
{
    var baseConfig = sp.GetRequiredService<Fido2Configuration>();
    var httpContextAccessor = sp.GetService<IHttpContextAccessor>();
    var httpContext = httpContextAccessor?.HttpContext;

    string domain = baseConfig.ServerDomain ?? "portal.aml.lan";
    var origins = baseConfig.Origins != null
        ? new HashSet<string>(baseConfig.Origins, StringComparer.OrdinalIgnoreCase)
        : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    // Dynamic Host and local schemes are permitted exclusively in Development
    if (isDevEnvironment && httpContext != null)
    {
        var reqHost = httpContext.Request.Host.Host;
        if (!string.IsNullOrEmpty(reqHost))
        {
            if (string.Equals(reqHost, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(reqHost, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                domain = "localhost";
            }
            origins.Add($"{httpContext.Request.Scheme}://{httpContext.Request.Host.Value}");
        }
    }

    return new Fido2(new Fido2Configuration
    {
        ServerDomain = domain,
        ServerName = baseConfig.ServerName ?? "API.HR.Node Passkeys",
        Origins = origins,
        TimestampDriftTolerance = baseConfig.TimestampDriftTolerance
    });
});

var authentication = builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
});

authentication.AddIdentityCookies();
authentication.AddBearerToken(IdentityConstants.BearerScheme);
authentication.AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>(
    BasicAuthenticationHandler.SchemeName, _ => { });

var msClientId = builder.Configuration["Authentication:Microsoft:ClientId"];
var msClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"];
if (!string.IsNullOrEmpty(msClientId) && !string.IsNullOrEmpty(msClientSecret))
{
    authentication.AddMicrosoftAccount(options =>
    {
        options.ClientId = msClientId;
        options.ClientSecret = msClientSecret;
    });
}

var authBuilder = builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthorizationPolicies.ApiBearer, policy =>
    {
        policy.AddAuthenticationSchemes(
            IdentityConstants.BearerScheme,
            IdentityConstants.ApplicationScheme);
        policy.RequireAuthenticatedUser();
    })
    .AddPolicy(AuthorizationPolicies.IntegrationBasic, policy =>
    {
        policy.AddAuthenticationSchemes(BasicAuthenticationHandler.SchemeName);
        policy.RequireAuthenticatedUser();
    })
    .AddPolicy(AuthorizationPolicies.ProfilePhotoRead, policy =>
    {
        policy.AddAuthenticationSchemes(IdentityConstants.BearerScheme, IdentityConstants.ApplicationScheme);
        policy.AddRequirements(new PermissionRequirement(Permissions.ProfilePhotoRead));
    })
    .AddPolicy(AuthorizationPolicies.ProfilePhotoUpload, policy =>
    {
        policy.AddAuthenticationSchemes(IdentityConstants.BearerScheme, IdentityConstants.ApplicationScheme);
        policy.AddRequirements(new PermissionRequirement(Permissions.ProfilePhotoUpload));
    })
    .AddPolicy(AuthorizationPolicies.ProfilePhotoDelete, policy =>
    {
        policy.AddAuthenticationSchemes(IdentityConstants.BearerScheme, IdentityConstants.ApplicationScheme);
        policy.AddRequirements(new PermissionRequirement(Permissions.ProfilePhotoDelete));
    })
    .AddPolicy(AuthorizationPolicies.ProfilePhotoReplace, policy =>
    {
        policy.AddAuthenticationSchemes(IdentityConstants.BearerScheme, IdentityConstants.ApplicationScheme);
        policy.AddRequirements(new PermissionRequirement(Permissions.ProfilePhotoReplace));
    })
    .AddPolicy(AuthorizationPolicies.ManageUsers, policy =>
    {
        policy.AddAuthenticationSchemes(IdentityConstants.BearerScheme, IdentityConstants.ApplicationScheme);
        policy.AddRequirements(new PermissionRequirement(Permissions.ManageUsers));
    })
    .AddPolicy(AuthorizationPolicies.ManageDevices, policy =>
    {
        policy.AddAuthenticationSchemes(IdentityConstants.BearerScheme, IdentityConstants.ApplicationScheme);
        policy.AddRequirements(new PermissionRequirement(Permissions.ManageDevices));
    })
    .AddPolicy(AuthorizationPolicies.ViewAuditLogs, policy =>
    {
        policy.AddAuthenticationSchemes(IdentityConstants.BearerScheme, IdentityConstants.ApplicationScheme);
        policy.AddRequirements(new PermissionRequirement(Permissions.ViewAuditLogs));
    })
    .AddPolicy(AuthorizationPolicies.ManageWorkflows, policy =>
    {
        policy.AddAuthenticationSchemes(IdentityConstants.BearerScheme, IdentityConstants.ApplicationScheme);
        policy.AddRequirements(new PermissionRequirement(Permissions.ManageWorkflows));
    })
    .AddPolicy(AuthorizationPolicies.ImportEmployees, policy =>
    {
        policy.AddAuthenticationSchemes(IdentityConstants.BearerScheme, IdentityConstants.ApplicationScheme);
        policy.AddRequirements(new PermissionRequirement(Permissions.ImportEmployees));
    })
    .AddPolicy(AuthorizationPolicies.ImportAppreciationLetters, policy =>
    {
        policy.AddAuthenticationSchemes(IdentityConstants.BearerScheme, IdentityConstants.ApplicationScheme);
        policy.AddRequirements(new PermissionRequirement(Permissions.ImportAppreciationLetters));
    })
    .AddPolicy(AuthorizationPolicies.ImportJobTitles, policy =>
    {
        policy.AddAuthenticationSchemes(IdentityConstants.BearerScheme, IdentityConstants.ApplicationScheme);
        policy.RequireAssertion(ctx =>
            ctx.User.IsInRole("Admin") ||
            ctx.User.HasClaim(c => (c.Type == "permission" || c.Type == "Permission") &&
                                   (c.Value.Equals(Permissions.ImportJobTitles, StringComparison.OrdinalIgnoreCase) ||
                                    c.Value.Equals(Permissions.ImportEmployees, StringComparison.OrdinalIgnoreCase))));
    })
    .AddPolicy(AuthorizationPolicies.ImportLastAllowanceDate, policy =>
    {
        policy.AddAuthenticationSchemes(IdentityConstants.BearerScheme, IdentityConstants.ApplicationScheme);
        policy.RequireAssertion(ctx =>
            ctx.User.IsInRole("Admin") ||
            ctx.User.HasClaim(c => (c.Type == "permission" || c.Type == "Permission") &&
                                   (c.Value.Equals(Permissions.ImportLastAllowanceDate, StringComparison.OrdinalIgnoreCase) ||
                                    c.Value.Equals(Permissions.ImportEmployees, StringComparison.OrdinalIgnoreCase))));
    })
    .AddPolicy(AuthorizationPolicies.ManageFeedback, policy =>
    {
        policy.AddAuthenticationSchemes(IdentityConstants.BearerScheme, IdentityConstants.ApplicationScheme);
        policy.AddRequirements(new PermissionRequirement(Permissions.ManageFeedback));
    })
    .AddPolicy(AuthorizationPolicies.AdminOnly, policy =>
    {
        policy.AddAuthenticationSchemes(IdentityConstants.BearerScheme, IdentityConstants.ApplicationScheme);
        policy.RequireRole("Admin");
    })
    .AddPolicy(AuthorizationPolicies.Staff, policy =>
    {
        policy.AddAuthenticationSchemes(IdentityConstants.BearerScheme, IdentityConstants.ApplicationScheme);
        policy.RequireRole(StaffRoles.All);
    })
    .AddPolicy(AuthorizationPolicies.ManageCanva, policy =>
    {
        policy.AddAuthenticationSchemes(IdentityConstants.BearerScheme, IdentityConstants.ApplicationScheme);
        policy.RequireRole("Admin", "HRManager");
    })
    .AddPolicy(AuthorizationPolicies.FormationDashboard, policy =>
    {
        policy.AddAuthenticationSchemes(IdentityConstants.BearerScheme, IdentityConstants.ApplicationScheme);
        policy.RequireAssertion(ctx =>
            ctx.User.IsInRole("Admin") ||
            ctx.User.IsInRole("HRManager") ||
            ctx.User.IsInRole("FormationManager") ||
            ctx.User.HasClaim(c => (c.Type == "permission" || c.Type == "Permission") && c.Value == Permissions.ViewFormationDashboard) ||
            ctx.User.HasClaim(c => c.Type == "formation_id"));
    });

// Register all fine-grained feature permissions (*.View and *.Edit) dynamically as authorization policies
foreach (var permissionCode in FeaturePermissionCatalog.AllPermissionCodes)
{
    authBuilder.AddPolicy(permissionCode, policy =>
    {
        policy.AddAuthenticationSchemes(IdentityConstants.BearerScheme, IdentityConstants.ApplicationScheme);
        policy.AddRequirements(new PermissionRequirement(permissionCode));
    });
}

var connectionString = builder.Configuration.GetConnectionString("liveDB")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'liveDB' or 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString);
    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString);
    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
}, ServiceLifetime.Scoped);

builder.Services.AddQuickGridEntityFrameworkAdapter();
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        // Length beats composition rules (NIST 800-63B). Existing passwords keep
        // working; the rule applies whenever a password is set or changed.
        options.Password.RequiredLength = 10;
        options.Password.RequiredUniqueChars = 4;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});
builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

// OpenAPI / Swagger Documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "API.HR.Node - Enterprise HR & Biometric Passkey API",
        Version = "v1",
        Description = "Comprehensive Enterprise HR API providing Authentication (Passkeys / WebAuthn FIDO2 Biometrics, Microsoft Entra ID, Local Identity), Employee Profile Management (/api/employees/me), Single Mobile Device Binding, Remote Force Logout, Photo Storage, and Security Audit Logging."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Enter JWT Bearer token",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var app = builder.Build();

// Migrations, roles, the one-time bootstrap administrator, reference data.
await StartupSeeder.RunAsync(app);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "API.HR.Node v1");
        c.DocumentTitle = "API.HR.Node - API Documentation";
    });
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Security Headers Middleware
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=(self)");
    context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
    // Scripts only from this origin: an injected <script src> or a plugin object
    // cannot load, forms cannot post to other sites, and <base> cannot be moved.
    // 'unsafe-inline' stays for now: the login page and Blazor's dev tooling use
    // inline scripts, and nearly every page uses style="" attributes.
    // Map tiles come from several HTTPS tile servers, hence img-src https:.
    context.Response.Headers.Append("Content-Security-Policy", string.Join("; ",
        "default-src 'self'",
        "script-src 'self' 'unsafe-inline' 'wasm-unsafe-eval' 'unsafe-eval'",
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com",
        "font-src 'self' data: https://fonts.gstatic.com",
        "img-src 'self' data: blob: https:",
        "connect-src 'self' ws: wss: blob: data:",
        "frame-src 'self' blob:",
        "worker-src 'self' blob:",
        "object-src 'none'",
        "base-uri 'self'",
        "frame-ancestors 'self'",
        "form-action 'self' https://login.microsoftonline.com https://login.live.com"));
    await next();
});

app.UseRateLimiter();

app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/api"),
    branch => branch.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));

app.UseHttpsRedirection();
app.UseAntiforgery();

var staticFileProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
staticFileProvider.Mappings[".wasm"] = "application/wasm";
staticFileProvider.Mappings[".traineddata"] = "application/octet-stream";
staticFileProvider.Mappings[".gz"] = "application/gzip";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = staticFileProvider
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();
app.MapAuthEndpoints();
app.MapPasskeyEndpoints();
app.MapEmployeeEndpoints();
app.MapEmployeeProfilePhotoEndpoints();
app.MapEmployeeImportEndpoints();
app.MapExcelImportEndpoints();
app.MapEmployeeStatisticsEndpoints();
app.MapAppreciationLetterImportEndpoints();
app.MapLeaveEndpoints();
app.MapWorkflowEndpoints();
app.MapWorkContinuationEndpoints();
app.MapNotificationEndpoints();
app.MapEmployeeDossierPdfEndpoints();
app.MapCareerMovementEndpoints();
app.MapCourseEndpoints();
app.MapEmployeeCivilDataEndpoints();
app.MapSalaryScaleEndpoints();
app.MapCanvaEndpoints();
app.MapEmployeeFeedbackEndpoints();
app.MapEmployeeResidenceEndpoints();
app.MapNationalCardEndpoints();
app.MapEmployeePassportEndpoints();
app.MapEmployeeDocumentsEndpoints();

app.MapGet("/api/network/aml-connectivity", (API.HR.Node.Services.Network.IAmlConnectivityService net) =>
    Results.Ok(new { isConnected = net.IsAmlConnected }));

app.Run();
