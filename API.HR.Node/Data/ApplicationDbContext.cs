using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using API.HR.Node.Data.Workflows;
using API.HR.Node.Data.OrganizationalHierarchy;
using API.HR.Node.Data.Notifications;
using API.HR.Node.Data.Feedback;
using API.HR.Node.Data.Allowances;
using API.HR.Node.Services.DesignSystem;

namespace API.HR.Node.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Formation> Formations => Set<Formation>();
    public DbSet<JobFamily> JobFamilies => Set<JobFamily>();
    public DbSet<JobTitle> JobTitles => Set<JobTitle>();
    public DbSet<PositionTitle> PositionTitles => Set<PositionTitle>();
    public DbSet<Stage> Stages => Set<Stage>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<EducationLevel> EducationLevels => Set<EducationLevel>();
    public DbSet<Specialization> Specializations => Set<Specialization>();
    public DbSet<Gender> Genders => Set<Gender>();
    public DbSet<Religion> Religions => Set<Religion>();
    public DbSet<Nationality> Nationalities => Set<Nationality>();
    public DbSet<EmployeeStatus> EmployeeStatuses => Set<EmployeeStatus>();
    public DbSet<MaritalStatus> MaritalStatuses => Set<MaritalStatus>();
    public DbSet<AppreciationLetter> AppreciationLetters => Set<AppreciationLetter>();
    public DbSet<AppreciationLetterTitle> AppreciationLetterTitles => Set<AppreciationLetterTitle>();
    public DbSet<AppreciationLetterType> AppreciationLetterTypes => Set<AppreciationLetterType>();
    public DbSet<EmployeeLeave> EmployeeLeaves => Set<EmployeeLeave>();
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<EmployeePenalty> EmployeePenalties => Set<EmployeePenalty>();
    public DbSet<PenaltyType> PenaltyTypes => Set<PenaltyType>();
    public DbSet<EmployeeMartyr> EmployeeMartyrs => Set<EmployeeMartyr>();
    public DbSet<MartyrdomType> MartyrdomTypes => Set<MartyrdomType>();
    public DbSet<MartyrKinship> MartyrKinships => Set<MartyrKinship>();
    public DbSet<EmployeeResidence> EmployeeResidences => Set<EmployeeResidence>();
    public DbSet<Governorate> Governorates => Set<Governorate>();
    public DbSet<EmployeeCareerMovement> EmployeeCareerMovements => Set<EmployeeCareerMovement>();
    public DbSet<EmployeeCourse> EmployeeCourses => Set<EmployeeCourse>();
    public DbSet<EmployeeVehicle> EmployeeVehicles => Set<EmployeeVehicle>();
    public DbSet<EmployeeProfilePhoto> EmployeeProfilePhotos => Set<EmployeeProfilePhoto>();
    public DbSet<DossierExportAudit> DossierExportAudits => Set<DossierExportAudit>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AllowedUser> AllowedUsers => Set<AllowedUser>();
    public DbSet<EmployeeEmail> EmployeeEmails => Set<EmployeeEmail>();
    public DbSet<EmployeePhone> EmployeePhones => Set<EmployeePhone>();
    public DbSet<UserDevice> UserDevices => Set<UserDevice>();
    public DbSet<EmployeePasskey> EmployeePasskeys => Set<EmployeePasskey>();
    public DbSet<SalaryScale> SalaryScales => Set<SalaryScale>();

    // General Workflow Engine DbSets
    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();
    public DbSet<WorkflowVersion> WorkflowVersions => Set<WorkflowVersion>();
    public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();
    public DbSet<WorkflowTransition> WorkflowTransitions => Set<WorkflowTransition>();
    public DbSet<WorkflowCondition> WorkflowConditions => Set<WorkflowCondition>();
    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();
    public DbSet<WorkflowTask> WorkflowTasks => Set<WorkflowTask>();
    public DbSet<WorkflowEvent> WorkflowEvents => Set<WorkflowEvent>();
    public DbSet<WorkflowComment> WorkflowComments => Set<WorkflowComment>();
    public DbSet<WorkflowDelegation> WorkflowDelegations => Set<WorkflowDelegation>();
    public DbSet<WorkflowIdempotencyRecord> WorkflowIdempotencyRecords => Set<WorkflowIdempotencyRecord>();
    public DbSet<WorkflowOutboxMessage> WorkflowOutboxMessages => Set<WorkflowOutboxMessage>();

    // Dynamic Organizational Hierarchy DbSets
    public DbSet<AdministrativeUnitType> AdministrativeUnitTypes => Set<AdministrativeUnitType>();
    public DbSet<AdministrativeUnit> AdministrativeUnits => Set<AdministrativeUnit>();
    public DbSet<AdministrativeUnitClosure> AdministrativeUnitClosures => Set<AdministrativeUnitClosure>();

    // User Notifications DbSet
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

    // User Design System Preferences DbSet
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();

    // Employee Feedback and Inquiries DbSet
    public DbSet<EmployeeFeedback> EmployeeFeedbacks => Set<EmployeeFeedback>();

    // Employee Allowances DbSets
    public DbSet<AllowanceType> AllowanceTypes => Set<AllowanceType>();
    public DbSet<EmployeeAllowancePeriod> EmployeeAllowancePeriods => Set<EmployeeAllowancePeriod>();
    public DbSet<EmployeeAllowanceItem> EmployeeAllowanceItems => Set<EmployeeAllowanceItem>();

    // Employee Financial Account & Balance Transactions
    public DbSet<Financial.EmployeeFinancialAccount> EmployeeFinancialAccounts => Set<Financial.EmployeeFinancialAccount>();
    public DbSet<Financial.EmployeeBalanceTransaction> EmployeeBalanceTransactions => Set<Financial.EmployeeBalanceTransaction>();

    // System Feature Settings (Admin Control Panel)
    public DbSet<SystemFeatureSettings> SystemFeatureSettings => Set<SystemFeatureSettings>();

    // Employee Identity Documents (National ID Cards)
    public DbSet<EmployeeNationalCard> EmployeeNationalCards => Set<EmployeeNationalCard>();

    // Employee Identity Documents (Passports)
    public DbSet<EmployeePassport> EmployeePassports => Set<EmployeePassport>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<SystemFeatureSettings>().HasData(new SystemFeatureSettings
        {
            Id = 1,
            EnableMyFeedback = true,
            EnableFeedbackFab = true,
            AllowIdeas = true,
            AllowNotes = true,
            AllowInquiries = true,
            AllowComplaints = true,
            AllowOthers = true,
            AllowAttachments = true,
            DisabledMessage = "خدمة تقديم الأفكار والمقترحات متوقفة مؤقتاً بقرار الإدارة.",
            UpdatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedBy = "System"
        });
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        ConfigureLookup<Formation>(builder);
        ConfigureLookup<JobTitle>(builder);
        ConfigureLookup<PositionTitle>(builder);
        ConfigureLookup<Stage>(builder);
        ConfigureLookup<Grade>(builder);
        ConfigureLookup<EducationLevel>(builder);
        ConfigureLookup<Specialization>(builder);
        ConfigureLookup<Gender>(builder);
        ConfigureLookup<Religion>(builder);
        ConfigureLookup<Nationality>(builder);
        ConfigureLookup<EmployeeStatus>(builder);
        ConfigureLookup<MaritalStatus>(builder);
        ConfigureLookup<AppreciationLetterTitle>(builder);
        ConfigureLookup<AppreciationLetterType>(builder);
        ConfigureLookup<LeaveType>(builder);
        ConfigureLookup<PenaltyType>(builder);
        ConfigureLookup<MartyrdomType>(builder);
        ConfigureLookup<MartyrKinship>(builder);
        ConfigureLookup<Governorate>(builder);
        ConfigureLookup<JobFamily>(builder);
        builder.Entity<EmployeeNationalCard>(card =>
        {
            card.ToTable("EmployeeNationalCards");
            card.HasKey(c => c.Id);
            card.HasIndex(c => c.DocumentNumber);
            card.HasIndex(c => c.NationalIdNumber);
            card.HasIndex(c => c.EmployeeId);

            card.HasOne(c => c.Employee)
                .WithMany(e => e.NationalCards)
                .HasForeignKey(c => c.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EmployeePassport>(p =>
        {
            p.ToTable("EmployeePassports");
            p.HasKey(x => x.Id);
            p.HasIndex(x => x.PassportNumber);
            p.HasIndex(x => x.NationalIdNumber);
            p.HasIndex(x => x.EmployeeId);

            p.HasOne(x => x.Employee)
                .WithMany(e => e.Passports)
                .HasForeignKey(x => x.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<JobFamily>(family =>
        {
            family.HasIndex(f => f.Name);
            family.HasIndex(f => f.Code);
        });

        builder.Entity<JobTitle>(job =>
        {
            job.HasOne(j => j.Grade)
                .WithMany()
                .HasForeignKey(j => j.GradeId)
                .OnDelete(DeleteBehavior.SetNull);

            job.HasOne(j => j.JobFamily)
                .WithMany()
                .HasForeignKey(j => j.JobFamilyId)
                .OnDelete(DeleteBehavior.SetNull);

            job.HasIndex(j => j.Code);
            job.HasIndex(j => j.JobFamilyId);
        });

        builder.Entity<UserNotification>(notif =>
        {
            notif.HasIndex(n => new { n.UserId, n.IsRead });
            notif.HasIndex(n => n.CreatedAt);
        });

        builder.Entity<Employee>(employee =>
        {
            employee.HasQueryFilter(value => !value.IsArchived);
            employee.HasIndex(value => value.IsArchived);
            employee.HasIndex(value => value.EmployeeID).IsUnique();
            employee.HasIndex(value => value.NationalId);
            employee.HasIndex(value => value.UnifiedEmployeeNumber);

            employee.HasOne(value => value.Formation)
                .WithMany()
                .HasForeignKey(value => value.FormationId)
                .OnDelete(DeleteBehavior.Restrict);

            employee.HasOne(value => value.AdministrativeUnit)
                .WithMany(unit => unit.Employees)
                .HasForeignKey(value => value.AdministrativeUnitId)
                .OnDelete(DeleteBehavior.SetNull);

            employee.HasOne(value => value.JobTitle)
                .WithMany(j => j.Employees)
                .HasForeignKey(value => value.JobTitleId)
                .OnDelete(DeleteBehavior.Restrict);

            employee.HasOne(value => value.PositionTitle)
                .WithMany()
                .HasForeignKey(value => value.PositionTitleId)
                .OnDelete(DeleteBehavior.Restrict);

            employee.HasOne(value => value.Stage)
                .WithMany()
                .HasForeignKey(value => value.StageId)
                .OnDelete(DeleteBehavior.Restrict);

            employee.HasOne(value => value.Grade)
                .WithMany()
                .HasForeignKey(value => value.GradeId)
                .OnDelete(DeleteBehavior.Restrict);

            employee.HasOne(value => value.EducationLevel)
                .WithMany()
                .HasForeignKey(value => value.EducationLevelId)
                .OnDelete(DeleteBehavior.Restrict);

            employee.HasOne(value => value.Specialization)
                .WithMany()
                .HasForeignKey(value => value.SpecializationId)
                .OnDelete(DeleteBehavior.Restrict);

            employee.HasOne(value => value.Gender)
                .WithMany()
                .HasForeignKey(value => value.GenderId)
                .OnDelete(DeleteBehavior.Restrict);

            employee.HasOne(value => value.Religion)
                .WithMany()
                .HasForeignKey(value => value.ReligionId)
                .OnDelete(DeleteBehavior.Restrict);

            employee.HasOne(value => value.Nationality)
                .WithMany()
                .HasForeignKey(value => value.NationalityId)
                .OnDelete(DeleteBehavior.Restrict);

            employee.HasOne(value => value.EmployeeStatus)
                .WithMany()
                .HasForeignKey(value => value.EmployeeStatusId)
                .OnDelete(DeleteBehavior.Restrict);

            employee.HasOne(value => value.MaritalStatus)
                .WithMany()
                .HasForeignKey(value => value.MaritalStatusId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AppreciationLetter>(letter =>
        {
            // Deleting an employee removes their letters; the type lookup is protected.
            letter.HasOne(value => value.Employee)
                .WithMany(e => e.AppreciationLetters)
                .HasForeignKey(value => value.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            letter.HasOne(value => value.TitleLookup)
                .WithMany()
                .HasForeignKey(value => value.TitleId)
                .OnDelete(DeleteBehavior.Restrict);

            letter.HasOne(value => value.Type)
                .WithMany()
                .HasForeignKey(value => value.TypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // Speeds up the (EmployeeId + LetterDate) upsert match. Not unique:
            // an employee may legitimately have two letters dated the same day.
            letter.HasIndex(value => new { value.EmployeeId, value.LetterDate });
        });

        builder.Entity<EmployeeLeave>(leave =>
        {
            leave.HasOne(l => l.Employee)
                .WithMany(e => e.Leaves)
                .HasForeignKey(l => l.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            leave.HasOne(l => l.LeaveType)
                .WithMany()
                .HasForeignKey(l => l.LeaveTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            leave.HasIndex(l => new { l.EmployeeId, l.StartDate });
            leave.HasIndex(l => l.StartDate);
            leave.HasIndex(l => l.EndDate);
        });

        builder.Entity<EmployeePenalty>(penalty =>
        {
            penalty.HasOne(p => p.Employee)
                .WithMany(e => e.Penalties)
                .HasForeignKey(p => p.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            penalty.HasOne(p => p.PenaltyType)
                .WithMany()
                .HasForeignKey(p => p.PenaltyTypeId)
                .OnDelete(DeleteBehavior.SetNull);

            penalty.HasIndex(p => new { p.EmployeeId, p.PenaltyDate });
            penalty.HasIndex(p => p.PenaltyDate);
        });

        builder.Entity<EmployeeMartyr>(martyr =>
        {
            martyr.HasOne(m => m.Employee)
                .WithMany(e => e.Martyrs)
                .HasForeignKey(m => m.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            martyr.HasOne(m => m.MartyrdomType)
                .WithMany()
                .HasForeignKey(m => m.MartyrdomTypeId)
                .OnDelete(DeleteBehavior.SetNull);

            martyr.HasOne(m => m.Kinship)
                .WithMany()
                .HasForeignKey(m => m.KinshipId)
                .OnDelete(DeleteBehavior.SetNull);

            martyr.HasIndex(m => m.EmployeeId);
            martyr.HasIndex(m => m.EmployeeIdNumber);
        });

        builder.Entity<EmployeeResidence>(res =>
        {
            res.HasOne(r => r.Employee)
                .WithMany(e => e.Residences)
                .HasForeignKey(r => r.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            res.HasOne(r => r.Governorate)
                .WithMany()
                .HasForeignKey(r => r.GovernorateId)
                .OnDelete(DeleteBehavior.SetNull);

            res.HasIndex(r => r.EmployeeId);
            res.HasIndex(r => r.EmployeeIdNumber);
            res.HasIndex(r => r.GovernorateId);
            res.HasIndex(r => r.FromDate);
            res.HasIndex(r => r.ToDate);
        });

        builder.Entity<Governorate>().HasData(
            new Governorate { Id = 1, Name = "بغداد", NameKu = "بەغدا", DisplayOrder = 1, DefaultLatitude = 33.3152, DefaultLongitude = 44.3661 },
            new Governorate { Id = 2, Name = "البصرة", NameKu = "بەسرە", DisplayOrder = 2, DefaultLatitude = 30.5081, DefaultLongitude = 47.7835 },
            new Governorate { Id = 3, Name = "نينوى", NameKu = "نەینەوا", DisplayOrder = 3, DefaultLatitude = 36.3489, DefaultLongitude = 43.1577 },
            new Governorate { Id = 4, Name = "أربيل", NameKu = "هەولێر", DisplayOrder = 4, DefaultLatitude = 36.1901, DefaultLongitude = 44.0091 },
            new Governorate { Id = 5, Name = "النجف الأشرف", NameKu = "نەجەف", DisplayOrder = 5, DefaultLatitude = 32.0259, DefaultLongitude = 44.3462 },
            new Governorate { Id = 6, Name = "كربلاء المقدسة", NameKu = "کەربەلا", DisplayOrder = 6, DefaultLatitude = 32.6160, DefaultLongitude = 44.0249 },
            new Governorate { Id = 7, Name = "كركوك", NameKu = "کەرکووک", DisplayOrder = 7, DefaultLatitude = 35.4681, DefaultLongitude = 44.3922 },
            new Governorate { Id = 8, Name = "الأنبار", NameKu = "ئەنبار", DisplayOrder = 8, DefaultLatitude = 33.4233, DefaultLongitude = 43.3033 },
            new Governorate { Id = 9, Name = "بابل", NameKu = "بابل", DisplayOrder = 9, DefaultLatitude = 32.4835, DefaultLongitude = 44.4300 },
            new Governorate { Id = 10, Name = "السليمانية", NameKu = "سلێمانی", DisplayOrder = 10, DefaultLatitude = 35.5669, DefaultLongitude = 45.4165 },
            new Governorate { Id = 11, Name = "دهوك", NameKu = "دهۆک", DisplayOrder = 11, DefaultLatitude = 36.8679, DefaultLongitude = 42.9906 },
            new Governorate { Id = 12, Name = "ديالى", NameKu = "دیالە", DisplayOrder = 12, DefaultLatitude = 33.7463, DefaultLongitude = 44.6433 },
            new Governorate { Id = 13, Name = "صلاح الدين", NameKu = "سەڵاحەدین", DisplayOrder = 13, DefaultLatitude = 34.6062, DefaultLongitude = 43.6783 },
            new Governorate { Id = 14, Name = "واسط", NameKu = "واسیت", DisplayOrder = 14, DefaultLatitude = 32.5125, DefaultLongitude = 45.8197 },
            new Governorate { Id = 15, Name = "ميسان", NameKu = "مەیسان", DisplayOrder = 15, DefaultLatitude = 31.8417, DefaultLongitude = 47.1444 },
            new Governorate { Id = 16, Name = "ذي قار", NameKu = "زیقار", DisplayOrder = 16, DefaultLatitude = 31.0579, DefaultLongitude = 46.2573 },
            new Governorate { Id = 17, Name = "المثنى", NameKu = "موسەننا", DisplayOrder = 17, DefaultLatitude = 31.3142, DefaultLongitude = 45.2811 },
            new Governorate { Id = 18, Name = "القادسية", NameKu = "قادسیە", DisplayOrder = 18, DefaultLatitude = 31.9922, DefaultLongitude = 44.9244 },
            new Governorate { Id = 19, Name = "حلبجة", NameKu = "هەڵەبجە", DisplayOrder = 19, DefaultLatitude = 35.1778, DefaultLongitude = 45.9861 }
        );

        builder.Entity<EmployeeCourse>(course =>
        {
            course.HasOne(c => c.Employee)
                .WithMany()
                .HasForeignKey(c => c.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            course.HasIndex(c => new { c.EmployeeId, c.CourseName });
            course.HasIndex(c => c.CourseName);
            course.HasIndex(c => c.StartDate);
        });

        builder.Entity<EmployeeProfilePhoto>(photo =>
        {
            photo.HasKey(p => p.Id);

            photo.HasIndex(p => p.EmployeeID);

            // Filtered Unique Index: Guarantees at most ONE active profile photo per employee
            photo.HasIndex(p => new { p.EmployeeID, p.IsActive })
                .IsUnique()
                .HasFilter("[IsActive] = 1");

            photo.HasOne(p => p.Employee)
                .WithMany()
                .HasPrincipalKey(e => e.EmployeeID)
                .HasForeignKey(p => p.EmployeeID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AuditLog>(audit =>
        {
            audit.HasKey(a => a.Id);
            audit.HasIndex(a => a.EmployeeID);
            audit.HasIndex(a => a.Timestamp);
        });

        builder.Entity<EmployeeEmail>(email =>
        {
            email.HasKey(e => e.Id);
            email.HasIndex(e => e.EmployeeID);
            email.HasIndex(e => e.Email).IsUnique();

            email.HasOne(e => e.Employee)
                .WithMany()
                .HasPrincipalKey(emp => emp.EmployeeID)
                .HasForeignKey(e => e.EmployeeID)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EmployeePhone>(phone =>
        {
            phone.HasKey(p => p.Id);
            phone.HasIndex(p => p.EmployeeID);
            phone.HasIndex(p => p.PhoneNumber);

            phone.HasOne(p => p.Employee)
                .WithMany()
                .HasPrincipalKey(emp => emp.EmployeeID)
                .HasForeignKey(p => p.EmployeeID)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ApplicationUser>(user =>
        {
            user.HasOne(u => u.Employee)
                .WithMany()
                .HasPrincipalKey(emp => emp.EmployeeID)
                .HasForeignKey(u => u.EmployeeID)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<UserDevice>(device =>
        {
            device.HasKey(d => d.Id);
            device.HasIndex(d => new { d.UserId, d.IsActive });
            device.HasIndex(d => new { d.EmployeeID, d.IsActive });
            device.HasIndex(d => d.DeviceId);
        });

        builder.Entity<EmployeePasskey>(pk =>
        {
            pk.HasKey(p => p.Id);
            pk.HasIndex(p => new { p.UserId, p.IsActive });
            pk.HasIndex(p => new { p.EmployeeID, p.IsActive });
        });

        // Dynamic Organizational Hierarchy Configurations
        builder.Entity<AdministrativeUnitType>(type =>
        {
            type.HasKey(t => t.Id);
            type.HasIndex(t => t.Name);
            type.HasIndex(t => t.Code);
            type.HasIndex(t => t.HierarchyLevel);
        });

        builder.Entity<AdministrativeUnit>(unit =>
        {
            unit.HasKey(u => u.Id);
            unit.HasIndex(u => u.Name);
            unit.HasIndex(u => u.Code);
            unit.HasIndex(u => u.ParentUnitId);

            unit.HasOne(u => u.UnitType)
                .WithMany(t => t.Units)
                .HasForeignKey(u => u.UnitTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            unit.HasOne(u => u.ParentUnit)
                .WithMany(p => p.SubUnits)
                .HasForeignKey(u => u.ParentUnitId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<AdministrativeUnitClosure>(closure =>
        {
            closure.HasKey(c => new { c.AncestorId, c.DescendantId });

            closure.HasOne(c => c.Ancestor)
                .WithMany(u => u.DescendantClosures)
                .HasForeignKey(c => c.AncestorId)
                .OnDelete(DeleteBehavior.Restrict);

            closure.HasOne(c => c.Descendant)
                .WithMany(u => u.AncestorClosures)
                .HasForeignKey(c => c.DescendantId)
                .OnDelete(DeleteBehavior.Cascade);

            closure.HasIndex(c => c.Depth);
            closure.HasIndex(c => c.LineagePath);
        });

        builder.Entity<UserPreference>(pref =>
        {
            pref.HasKey(p => p.UserId);
            pref.Property(p => p.UserId).HasMaxLength(450);
            pref.Property(p => p.Language).HasMaxLength(10);
            pref.Property(p => p.BrandTheme).HasMaxLength(30);
            pref.Property(p => p.CustomBrandHex).HasMaxLength(10);
            pref.Property(p => p.Radius).HasMaxLength(20);
            pref.Property(p => p.TextScale).HasMaxLength(20);
            pref.Property(p => p.FontFamily).HasMaxLength(50);
            pref.Property(p => p.DefaultLandingPage).HasMaxLength(100);
        });

        builder.Entity<EmployeeCareerMovement>(movement =>
        {
            movement.HasKey(m => m.Id);
            movement.HasIndex(m => new { m.EmployeeId, m.MovementDate });
            movement.HasIndex(m => m.MovementDate);

            movement.HasOne(m => m.Employee)
                .WithMany()
                .HasForeignKey(m => m.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SalaryScale>(scale =>
        {
            scale.HasKey(s => s.Id);
            scale.HasIndex(s => new { s.GradeId, s.StageId }).IsUnique();
            scale.HasIndex(s => new { s.GradeCode, s.StageCode });
            scale.HasIndex(s => s.GradeSortOrder);
            scale.HasIndex(s => s.StageNumber);

            scale.HasOne(s => s.Grade)
                .WithMany()
                .HasForeignKey(s => s.GradeId)
                .OnDelete(DeleteBehavior.Restrict);

            scale.HasOne(s => s.Stage)
                .WithMany()
                .HasForeignKey(s => s.StageId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeFeedback>(feedback =>
        {
            feedback.HasKey(f => f.Id);
            feedback.HasIndex(f => f.ReferenceNumber).IsUnique();
            feedback.HasIndex(f => f.EmployeeId);
            feedback.HasIndex(f => f.Status);
            feedback.HasIndex(f => f.CreatedAt);
            feedback.HasIndex(f => f.FeedbackType);

            feedback.HasOne(f => f.Employee)
                .WithMany()
                .HasForeignKey(f => f.EmployeeId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<AllowanceType>(type =>
        {
            type.HasIndex(t => t.Code);
            type.HasIndex(t => t.Name);
        });

        builder.Entity<EmployeeAllowancePeriod>(period =>
        {
            period.HasKey(p => p.Id);
            period.HasIndex(p => new { p.EmployeeId, p.IsCurrent });
            period.HasIndex(p => p.StartDate);

            period.HasOne(p => p.Employee)
                .WithMany()
                .HasForeignKey(p => p.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            period.HasMany(p => p.Items)
                .WithOne(i => i.Period)
                .HasForeignKey(i => i.PeriodId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EmployeeAllowanceItem>(item =>
        {
            item.HasKey(i => i.Id);
            item.HasIndex(i => i.PeriodId);

            item.HasOne(i => i.AllowanceType)
                .WithMany()
                .HasForeignKey(i => i.AllowanceTypeId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Financial.EmployeeFinancialAccount>(account =>
        {
            account.HasIndex(a => a.EmployeeId).IsUnique();
            account.HasOne(a => a.Employee)
                .WithOne(e => e.FinancialAccount)
                .HasForeignKey<Financial.EmployeeFinancialAccount>(a => a.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Financial.EmployeeBalanceTransaction>(trx =>
        {
            trx.HasIndex(t => t.EmployeeId);
            trx.HasIndex(t => t.TransactionDate);
            trx.HasOne(t => t.FinancialAccount)
                .WithMany(a => a.Transactions)
                .HasForeignKey(t => t.EmployeeFinancialAccountId)
                .OnDelete(DeleteBehavior.Cascade);
            trx.HasOne(t => t.Employee)
                .WithMany(e => e.FinancialTransactions)
                .HasForeignKey(t => t.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeVehicle>(veh =>
        {
            veh.HasKey(v => v.Id);
            veh.HasIndex(v => v.EmployeeId);
            veh.HasIndex(v => v.PlateNumber);
            veh.HasIndex(v => v.Vin);
            veh.HasIndex(v => v.IsAuthorized);

            veh.HasOne(v => v.Employee)
                .WithMany(e => e.Vehicles)
                .HasForeignKey(v => v.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureLookup<TEntity>(ModelBuilder builder)
        where TEntity : class, ILookupEntity
    {
        builder.Entity<TEntity>()
            .HasIndex(value => value.Name)
            .IsUnique();
    }
}
