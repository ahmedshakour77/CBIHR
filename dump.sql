CREATE TABLE [AdministrativeUnitTypes] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(450) NOT NULL,
    [NameKu] nvarchar(max) NULL,
    [Code] nvarchar(450) NOT NULL,
    [HierarchyLevel] int NOT NULL,
    [Description] nvarchar(max) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_AdministrativeUnitTypes] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [AllowanceTypes] (
    [Id] int NOT NULL IDENTITY,
    [Code] nvarchar(50) NULL,
    [Name] nvarchar(200) NOT NULL,
    [CalculationType] int NOT NULL,
    [DefaultPercentage] decimal(8,2) NULL,
    [DefaultAmount] decimal(18,2) NULL,
    [Description] nvarchar(500) NULL,
    [IsActive] bit NOT NULL,
    [SortOrder] int NOT NULL,
    CONSTRAINT [PK_AllowanceTypes] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [AllowedUsers] (
    [Id] int NOT NULL IDENTITY,
    [Email] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [FullName] nvarchar(max) NULL,
    [Notes] nvarchar(max) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_AllowedUsers] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [AppreciationLetterTitles] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(200) NOT NULL,
    CONSTRAINT [PK_AppreciationLetterTitles] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [AppreciationLetterTypes] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(200) NOT NULL,
    [SeniorityYears] int NOT NULL,
    [SeniorityMonths] int NOT NULL,
    [SeniorityDays] int NOT NULL,
    CONSTRAINT [PK_AppreciationLetterTypes] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [AspNetRoles] (
    [Id] nvarchar(450) NOT NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [AuditLogs] (
    [Id] bigint NOT NULL IDENTITY,
    [UserId] nvarchar(256) NULL,
    [EmployeeID] nvarchar(100) NOT NULL,
    [PhotoId] int NULL,
    [Action] nvarchar(100) NOT NULL,
    [Timestamp] datetimeoffset NOT NULL,
    [IpAddress] nvarchar(64) NULL,
    [UserAgent] nvarchar(500) NULL,
    [Success] bit NOT NULL,
    [FailureReason] nvarchar(1000) NULL,
    [CorrelationId] nvarchar(100) NULL,
    CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [DossierExportAudits] (
    [Id] bigint NOT NULL IDENTITY,
    [VerificationToken] nvarchar(32) NOT NULL,
    [EmployeeId] int NOT NULL,
    [EmployeeIDNumber] nvarchar(100) NOT NULL,
    [EmployeeName] nvarchar(256) NOT NULL,
    [FormationName] nvarchar(256) NULL,
    [JobTitleName] nvarchar(256) NULL,
    [SnapshotDataDigest] nvarchar(64) NOT NULL,
    [ExportedByUserId] nvarchar(256) NULL,
    [ExportedByUserName] nvarchar(256) NULL,
    [ExportedFromIp] nvarchar(64) NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [VerificationCount] int NOT NULL,
    [LastVerifiedAtUtc] datetime2 NULL,
    CONSTRAINT [PK_DossierExportAudits] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [EducationLevels] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(200) NOT NULL,
    CONSTRAINT [PK_EducationLevels] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [EmployeePasskeys] (
    [Id] bigint NOT NULL IDENTITY,
    [UserId] nvarchar(256) NOT NULL,
    [EmployeeID] nvarchar(100) NOT NULL,
    [CredentialId] varbinary(max) NOT NULL,
    [PublicKey] varbinary(max) NOT NULL,
    [UserHandle] varbinary(max) NOT NULL,
    [SignatureCounter] bigint NOT NULL,
    [DeviceName] nvarchar(150) NULL,
    [AaGuid] uniqueidentifier NOT NULL,
    [CredentialType] nvarchar(100) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [LastUsedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_EmployeePasskeys] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [EmployeeStatuses] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    CONSTRAINT [PK_EmployeeStatuses] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Formations] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(300) NOT NULL,
    CONSTRAINT [PK_Formations] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Genders] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(50) NOT NULL,
    CONSTRAINT [PK_Genders] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Governorates] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(150) NOT NULL,
    [NameKu] nvarchar(150) NULL,
    [DisplayOrder] int NOT NULL,
    [DefaultLatitude] float NOT NULL,
    [DefaultLongitude] float NOT NULL,
    CONSTRAINT [PK_Governorates] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Grades] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [Code] nvarchar(50) NULL,
    [SortOrder] int NULL,
    CONSTRAINT [PK_Grades] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [JobTitles] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(200) NOT NULL,
    CONSTRAINT [PK_JobTitles] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [LeaveTypes] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(200) NOT NULL,
    CONSTRAINT [PK_LeaveTypes] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [MaritalStatuses] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    CONSTRAINT [PK_MaritalStatuses] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [MartyrdomTypes] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(250) NOT NULL,
    [Description] nvarchar(500) NULL,
    CONSTRAINT [PK_MartyrdomTypes] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [MartyrKinships] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [Degree] int NOT NULL,
    CONSTRAINT [PK_MartyrKinships] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Nationalities] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    CONSTRAINT [PK_Nationalities] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [PenaltyTypes] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(200) NOT NULL,
    [DelayMonths] int NOT NULL,
    [Description] nvarchar(500) NULL,
    CONSTRAINT [PK_PenaltyTypes] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [PositionTitles] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(200) NOT NULL,
    CONSTRAINT [PK_PositionTitles] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Religions] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    CONSTRAINT [PK_Religions] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Specializations] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(300) NOT NULL,
    CONSTRAINT [PK_Specializations] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [Stages] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [Code] nvarchar(50) NULL,
    [StageNumber] int NULL,
    CONSTRAINT [PK_Stages] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [SystemFeatureSettings] (
    [Id] int NOT NULL IDENTITY,
    [EnableMyFeedback] bit NOT NULL,
    [EnableFeedbackFab] bit NOT NULL,
    [AllowIdeas] bit NOT NULL,
    [AllowNotes] bit NOT NULL,
    [AllowInquiries] bit NOT NULL,
    [AllowComplaints] bit NOT NULL,
    [AllowOthers] bit NOT NULL,
    [AllowAttachments] bit NOT NULL,
    [DisabledMessage] nvarchar(500) NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [UpdatedBy] nvarchar(100) NULL,
    CONSTRAINT [PK_SystemFeatureSettings] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [UserDevices] (
    [Id] bigint NOT NULL IDENTITY,
    [UserId] nvarchar(256) NOT NULL,
    [EmployeeID] nvarchar(100) NOT NULL,
    [DeviceId] nvarchar(256) NOT NULL,
    [Platform] nvarchar(100) NULL,
    [DeviceModel] nvarchar(150) NULL,
    [Manufacturer] nvarchar(100) NULL,
    [OsVersion] nvarchar(100) NULL,
    [AppVersion] nvarchar(100) NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [LastLoginAt] datetimeoffset NOT NULL,
    [LastIpAddress] nvarchar(64) NULL,
    [MetadataJson] nvarchar(1000) NULL,
    CONSTRAINT [PK_UserDevices] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [UserNotifications] (
    [Id] bigint NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [Message] nvarchar(1000) NOT NULL,
    [Type] int NOT NULL,
    [LinkUrl] nvarchar(500) NULL,
    [IsRead] bit NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [ReadAt] datetimeoffset NULL,
    [MetadataJson] nvarchar(max) NULL,
    CONSTRAINT [PK_UserNotifications] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [UserPreferences] (
    [UserId] nvarchar(450) NOT NULL,
    [Language] nvarchar(10) NOT NULL,
    [Appearance] int NOT NULL,
    [BrandTheme] nvarchar(30) NOT NULL,
    [CustomBrandHex] nvarchar(10) NULL,
    [Density] int NOT NULL,
    [Radius] nvarchar(20) NOT NULL,
    [TextScale] nvarchar(20) NOT NULL,
    [FontFamily] nvarchar(50) NOT NULL,
    [HighContrast] bit NOT NULL,
    [ReducedMotion] bit NOT NULL,
    [DefaultLandingPage] nvarchar(100) NULL,
    [DataGridPreferencesJson] nvarchar(max) NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_UserPreferences] PRIMARY KEY ([UserId])
);
GO


CREATE TABLE [WorkflowDefinitions] (
    [Id] bigint NOT NULL IDENTITY,
    [Code] nvarchar(100) NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(1000) NULL,
    [FormSchemaJson] nvarchar(max) NULL,
    [IsEnabled] bit NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] nvarchar(256) NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] nvarchar(256) NULL,
    CONSTRAINT [PK_WorkflowDefinitions] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [WorkflowDelegations] (
    [Id] bigint NOT NULL IDENTITY,
    [DelegatorUserId] nvarchar(256) NOT NULL,
    [DelegateeUserId] nvarchar(256) NOT NULL,
    [WorkflowDefinitionCode] nvarchar(100) NULL,
    [StartDate] datetimeoffset NOT NULL,
    [EndDate] datetimeoffset NOT NULL,
    [IsActive] bit NOT NULL,
    [Reason] nvarchar(500) NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_WorkflowDelegations] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [WorkflowIdempotencyRecords] (
    [Id] bigint NOT NULL IDENTITY,
    [IdempotencyKey] nvarchar(128) NOT NULL,
    [ActionCode] nvarchar(100) NOT NULL,
    [WorkflowInstanceId] bigint NOT NULL,
    [RequestHash] nvarchar(64) NULL,
    [ResponseJson] nvarchar(max) NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_WorkflowIdempotencyRecords] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [WorkflowOutboxMessages] (
    [Id] bigint NOT NULL IDENTITY,
    [MessageType] nvarchar(100) NOT NULL,
    [PayloadJson] nvarchar(max) NOT NULL,
    [OccurredAt] datetimeoffset NOT NULL,
    [ProcessedAt] datetimeoffset NULL,
    [Error] nvarchar(1000) NULL,
    CONSTRAINT [PK_WorkflowOutboxMessages] PRIMARY KEY ([Id])
);
GO


CREATE TABLE [AdministrativeUnits] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(450) NOT NULL,
    [NameKu] nvarchar(max) NULL,
    [Code] nvarchar(450) NOT NULL,
    [UnitTypeId] int NOT NULL,
    [ParentUnitId] int NULL,
    [ManagerTitle] nvarchar(max) NULL,
    [ManagerEmployeeId] nvarchar(max) NULL,
    [OrderIndex] int NOT NULL,
    [IsActive] bit NOT NULL,
    [Notes] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_AdministrativeUnits] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AdministrativeUnits_AdministrativeUnitTypes_UnitTypeId] FOREIGN KEY ([UnitTypeId]) REFERENCES [AdministrativeUnitTypes] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AdministrativeUnits_AdministrativeUnits_ParentUnitId] FOREIGN KEY ([ParentUnitId]) REFERENCES [AdministrativeUnits] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [SalaryScales] (
    [Id] int NOT NULL IDENTITY,
    [GradeId] int NOT NULL,
    [StageId] int NOT NULL,
    [GradeCode] nvarchar(50) NOT NULL,
    [StageCode] nvarchar(50) NOT NULL,
    [StageNumber] int NOT NULL,
    [GradeSortOrder] int NOT NULL,
    [BaseSalary] decimal(18,2) NOT NULL,
    [AnnualAllowance] decimal(18,2) NULL,
    [RequiredServiceYears] int NULL,
    [Notes] nvarchar(300) NULL,
    CONSTRAINT [PK_SalaryScales] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SalaryScales_Grades_GradeId] FOREIGN KEY ([GradeId]) REFERENCES [Grades] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SalaryScales_Stages_StageId] FOREIGN KEY ([StageId]) REFERENCES [Stages] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [WorkflowVersions] (
    [Id] bigint NOT NULL IDENTITY,
    [WorkflowDefinitionId] bigint NOT NULL,
    [VersionNumber] int NOT NULL,
    [Status] int NOT NULL,
    [ChangeDescription] nvarchar(1000) NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] nvarchar(256) NULL,
    [PublishedAt] datetimeoffset NULL,
    [PublishedBy] nvarchar(256) NULL,
    CONSTRAINT [PK_WorkflowVersions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkflowVersions_WorkflowDefinitions_WorkflowDefinitionId] FOREIGN KEY ([WorkflowDefinitionId]) REFERENCES [WorkflowDefinitions] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [AdministrativeUnitClosures] (
    [AncestorId] int NOT NULL,
    [DescendantId] int NOT NULL,
    [Depth] int NOT NULL,
    [LineagePath] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AdministrativeUnitClosures] PRIMARY KEY ([AncestorId], [DescendantId]),
    CONSTRAINT [FK_AdministrativeUnitClosures_AdministrativeUnits_AncestorId] FOREIGN KEY ([AncestorId]) REFERENCES [AdministrativeUnits] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AdministrativeUnitClosures_AdministrativeUnits_DescendantId] FOREIGN KEY ([DescendantId]) REFERENCES [AdministrativeUnits] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [Employees] (
    [Id] int NOT NULL IDENTITY,
    [EmployeeID] nvarchar(100) NOT NULL,
    [Name] nvarchar(300) NOT NULL,
    [NationalId] nvarchar(50) NULL,
    [UnifiedEmployeeNumber] nvarchar(50) NULL,
    [MotherName] nvarchar(150) NULL,
    [FirstName] nvarchar(100) NULL,
    [SecondName] nvarchar(100) NULL,
    [ThirdName] nvarchar(100) NULL,
    [FourthName] nvarchar(100) NULL,
    [FormationId] int NULL,
    [AdministrativeUnitId] int NULL,
    [JobTitleId] int NULL,
    [PositionTitleId] int NULL,
    [StageId] int NULL,
    [GradeId] int NULL,
    [EducationLevelId] int NULL,
    [SpecializationId] int NULL,
    [LastJobTitleDate] date NULL,
    [HireDate] date NULL,
    [GenderId] int NULL,
    [ReligionId] int NULL,
    [NationalityId] int NULL,
    [FirstDepartment] nvarchar(300) NULL,
    [BirthDate] date NULL,
    [EmployeeStatusId] int NULL,
    [MaritalStatusId] int NULL,
    [LeaveBalance] decimal(18,2) NULL,
    CONSTRAINT [PK_Employees] PRIMARY KEY ([Id]),
    CONSTRAINT [AK_Employees_EmployeeID] UNIQUE ([EmployeeID]),
    CONSTRAINT [FK_Employees_AdministrativeUnits_AdministrativeUnitId] FOREIGN KEY ([AdministrativeUnitId]) REFERENCES [AdministrativeUnits] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_Employees_EducationLevels_EducationLevelId] FOREIGN KEY ([EducationLevelId]) REFERENCES [EducationLevels] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Employees_EmployeeStatuses_EmployeeStatusId] FOREIGN KEY ([EmployeeStatusId]) REFERENCES [EmployeeStatuses] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Employees_Formations_FormationId] FOREIGN KEY ([FormationId]) REFERENCES [Formations] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Employees_Genders_GenderId] FOREIGN KEY ([GenderId]) REFERENCES [Genders] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Employees_Grades_GradeId] FOREIGN KEY ([GradeId]) REFERENCES [Grades] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Employees_JobTitles_JobTitleId] FOREIGN KEY ([JobTitleId]) REFERENCES [JobTitles] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Employees_MaritalStatuses_MaritalStatusId] FOREIGN KEY ([MaritalStatusId]) REFERENCES [MaritalStatuses] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Employees_Nationalities_NationalityId] FOREIGN KEY ([NationalityId]) REFERENCES [Nationalities] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Employees_PositionTitles_PositionTitleId] FOREIGN KEY ([PositionTitleId]) REFERENCES [PositionTitles] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Employees_Religions_ReligionId] FOREIGN KEY ([ReligionId]) REFERENCES [Religions] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Employees_Specializations_SpecializationId] FOREIGN KEY ([SpecializationId]) REFERENCES [Specializations] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Employees_Stages_StageId] FOREIGN KEY ([StageId]) REFERENCES [Stages] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [WorkflowSteps] (
    [Id] bigint NOT NULL IDENTITY,
    [WorkflowVersionId] bigint NOT NULL,
    [Code] nvarchar(100) NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [Description] nvarchar(500) NULL,
    [StepType] int NOT NULL,
    [IsStart] bit NOT NULL,
    [IsFinal] bit NOT NULL,
    [SortOrder] int NOT NULL,
    [AllowReject] bit NOT NULL,
    [AllowReturn] bit NOT NULL,
    [AllowCancel] bit NOT NULL,
    [AllowDelegate] bit NOT NULL,
    [AllowReassign] bit NOT NULL,
    [SlaMinutes] int NULL,
    [ConfigurationJson] nvarchar(max) NULL,
    CONSTRAINT [PK_WorkflowSteps] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkflowSteps_WorkflowVersions_WorkflowVersionId] FOREIGN KEY ([WorkflowVersionId]) REFERENCES [WorkflowVersions] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [AppreciationLetters] (
    [Id] int NOT NULL IDENTITY,
    [EmployeeId] int NOT NULL,
    [TitleId] int NULL,
    [Title] nvarchar(500) NULL,
    [LetterDate] date NULL,
    [Count] int NULL,
    [TypeId] int NULL,
    [Notes] nvarchar(1000) NULL,
    [LetterNumber] nvarchar(100) NULL,
    CONSTRAINT [PK_AppreciationLetters] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AppreciationLetters_AppreciationLetterTitles_TitleId] FOREIGN KEY ([TitleId]) REFERENCES [AppreciationLetterTitles] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AppreciationLetters_AppreciationLetterTypes_TypeId] FOREIGN KEY ([TypeId]) REFERENCES [AppreciationLetterTypes] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_AppreciationLetters_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [AspNetUsers] (
    [Id] nvarchar(450) NOT NULL,
    [EmployeeID] nvarchar(100) NULL,
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(256) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUsers_Employees_EmployeeID] FOREIGN KEY ([EmployeeID]) REFERENCES [Employees] ([EmployeeID]) ON DELETE SET NULL
);
GO


CREATE TABLE [EmployeeAllowancePeriods] (
    [Id] int NOT NULL IDENTITY,
    [EmployeeId] int NOT NULL,
    [StartDate] date NOT NULL,
    [EndDate] date NULL,
    [IsCurrent] bit NOT NULL,
    [BasicSalary] decimal(18,2) NOT NULL,
    [TotalPercentageRate] decimal(8,2) NOT NULL,
    [TotalPercentageAmount] decimal(18,2) NOT NULL,
    [TotalFixedAmount] decimal(18,2) NOT NULL,
    [GrossSalary] decimal(18,2) NOT NULL,
    [OrderNumber] nvarchar(100) NULL,
    [OrderDate] date NULL,
    [ChangeReason] nvarchar(300) NULL,
    [Notes] nvarchar(500) NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] nvarchar(100) NULL,
    CONSTRAINT [PK_EmployeeAllowancePeriods] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeAllowancePeriods_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [EmployeeCareerMovements] (
    [Id] int NOT NULL IDENTITY,
    [EmployeeId] int NOT NULL,
    [MovementDate] date NOT NULL,
    [FormationName] nvarchar(300) NOT NULL,
    [CountForAllowance] bit NOT NULL,
    [CountForPromotion] bit NOT NULL,
    [CountForPension] bit NOT NULL,
    [Notes] nvarchar(500) NULL,
    [CreatedAtUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_EmployeeCareerMovements] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeCareerMovements_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [EmployeeCourses] (
    [Id] int NOT NULL IDENTITY,
    [EmployeeId] int NOT NULL,
    [CourseName] nvarchar(300) NOT NULL,
    [StartDate] date NULL,
    [EndDate] date NULL,
    [DaysCount] int NULL,
    [Institution] nvarchar(200) NULL,
    [Status] nvarchar(50) NOT NULL,
    [Notes] nvarchar(500) NULL,
    [CreatedAtUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_EmployeeCourses] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeCourses_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [EmployeeEmails] (
    [Id] int NOT NULL IDENTITY,
    [EmployeeID] nvarchar(100) NOT NULL,
    [Email] nvarchar(256) NOT NULL,
    [IsPrimary] bit NOT NULL,
    [Label] int NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_EmployeeEmails] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeEmails_Employees_EmployeeID] FOREIGN KEY ([EmployeeID]) REFERENCES [Employees] ([EmployeeID]) ON DELETE CASCADE
);
GO


CREATE TABLE [EmployeeFeedbacks] (
    [Id] int NOT NULL IDENTITY,
    [ReferenceNumber] nvarchar(50) NOT NULL,
    [FeedbackType] nvarchar(50) NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [WordCount] int NOT NULL,
    [EmployeeId] int NULL,
    [UserId] nvarchar(450) NULL,
    [SubmitterName] nvarchar(250) NULL,
    [SubmitterEmail] nvarchar(250) NULL,
    [SubmitterJobTitle] nvarchar(100) NULL,
    [SubmitterDepartment] nvarchar(150) NULL,
    [AttachmentFileName] nvarchar(300) NULL,
    [AttachmentStorageKey] nvarchar(500) NULL,
    [AttachmentSizeBytes] bigint NULL,
    [AttachmentContentType] nvarchar(100) NULL,
    [Status] int NOT NULL,
    [AdminNotes] nvarchar(2000) NULL,
    [RespondedByUserId] nvarchar(450) NULL,
    [RespondedByUserName] nvarchar(250) NULL,
    [RespondedAt] datetimeoffset NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NULL,
    CONSTRAINT [PK_EmployeeFeedbacks] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeFeedbacks_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE SET NULL
);
GO


CREATE TABLE [EmployeeLeaves] (
    [Id] int NOT NULL IDENTITY,
    [EmployeeId] int NOT NULL,
    [LeaveTypeId] int NULL,
    [StartDate] date NOT NULL,
    [EndDate] date NOT NULL,
    [DaysCount] int NOT NULL,
    [OrderNumber] nvarchar(100) NULL,
    [OrderDate] date NULL,
    [Notes] nvarchar(1000) NULL,
    [Status] nvarchar(50) NOT NULL,
    [CreatedAtUtc] datetimeoffset NOT NULL,
    CONSTRAINT [PK_EmployeeLeaves] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeLeaves_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_EmployeeLeaves_LeaveTypes_LeaveTypeId] FOREIGN KEY ([LeaveTypeId]) REFERENCES [LeaveTypes] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [EmployeeMartyrs] (
    [Id] int NOT NULL IDENTITY,
    [EmployeeId] int NOT NULL,
    [EmployeeIdNumber] nvarchar(50) NOT NULL,
    [MartyrName] nvarchar(250) NOT NULL,
    [MartyrdomTypeId] int NULL,
    [KinshipId] int NULL,
    [DecisionNumber] nvarchar(150) NULL,
    [Notes] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_EmployeeMartyrs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeMartyrs_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_EmployeeMartyrs_MartyrKinships_KinshipId] FOREIGN KEY ([KinshipId]) REFERENCES [MartyrKinships] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_EmployeeMartyrs_MartyrdomTypes_MartyrdomTypeId] FOREIGN KEY ([MartyrdomTypeId]) REFERENCES [MartyrdomTypes] ([Id]) ON DELETE SET NULL
);
GO


CREATE TABLE [EmployeePenalties] (
    [Id] int NOT NULL IDENTITY,
    [EmployeeId] int NOT NULL,
    [PenaltyTypeId] int NULL,
    [PenaltyName] nvarchar(250) NULL,
    [PenaltyDate] date NULL,
    [OrderNumber] nvarchar(100) NULL,
    [Notes] nvarchar(1000) NULL,
    [Directorate] nvarchar(300) NULL,
    [JobTitle] nvarchar(200) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_EmployeePenalties] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeePenalties_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_EmployeePenalties_PenaltyTypes_PenaltyTypeId] FOREIGN KEY ([PenaltyTypeId]) REFERENCES [PenaltyTypes] ([Id]) ON DELETE SET NULL
);
GO


CREATE TABLE [EmployeePhones] (
    [Id] int NOT NULL IDENTITY,
    [EmployeeID] nvarchar(100) NOT NULL,
    [PhoneNumber] nvarchar(50) NOT NULL,
    [IsPrimary] bit NOT NULL,
    [Label] int NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_EmployeePhones] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeePhones_Employees_EmployeeID] FOREIGN KEY ([EmployeeID]) REFERENCES [Employees] ([EmployeeID]) ON DELETE CASCADE
);
GO


CREATE TABLE [EmployeeProfilePhotos] (
    [Id] int NOT NULL IDENTITY,
    [EmployeeID] nvarchar(100) NOT NULL,
    [StorageKey] nvarchar(500) NOT NULL,
    [ThumbnailStorageKey] nvarchar(500) NOT NULL,
    [OriginalFileName] nvarchar(255) NOT NULL,
    [ContentType] nvarchar(100) NOT NULL,
    [FileSize] bigint NOT NULL,
    [Width] int NOT NULL,
    [Height] int NOT NULL,
    [Sha256] nvarchar(64) NOT NULL,
    [Version] int NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [CreatedBy] nvarchar(256) NOT NULL,
    [UpdatedAt] datetimeoffset NULL,
    [UpdatedBy] nvarchar(256) NULL,
    [DeletedAt] datetimeoffset NULL,
    [DeletedBy] nvarchar(256) NULL,
    CONSTRAINT [PK_EmployeeProfilePhotos] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeProfilePhotos_Employees_EmployeeID] FOREIGN KEY ([EmployeeID]) REFERENCES [Employees] ([EmployeeID]) ON DELETE NO ACTION
);
GO


CREATE TABLE [EmployeeResidences] (
    [Id] int NOT NULL IDENTITY,
    [EmployeeId] int NOT NULL,
    [EmployeeIdNumber] nvarchar(50) NOT NULL,
    [GovernorateId] int NULL,
    [InformationOffice] nvarchar(250) NULL,
    [FamilyHeadName] nvarchar(250) NULL,
    [HousingType] nvarchar(50) NOT NULL,
    [Mahalla] nvarchar(100) NULL,
    [Zuqaq] nvarchar(100) NULL,
    [Dar] nvarchar(100) NULL,
    [ComplexName] nvarchar(200) NULL,
    [BuildingNumber] nvarchar(100) NULL,
    [ApartmentNumber] nvarchar(100) NULL,
    [FormNumber] nvarchar(100) NULL,
    [FormDate] date NULL,
    [LocationUrl] nvarchar(1000) NULL,
    [Latitude] float NULL,
    [Longitude] float NULL,
    [CardFrontKey] nvarchar(500) NULL,
    [CardFrontFileName] nvarchar(255) NULL,
    [CardFrontContentType] nvarchar(100) NULL,
    [CardBackKey] nvarchar(500) NULL,
    [CardBackFileName] nvarchar(255) NULL,
    [CardBackContentType] nvarchar(100) NULL,
    [FromDate] date NULL,
    [ToDate] date NULL,
    [Notes] nvarchar(1000) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_EmployeeResidences] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeResidences_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_EmployeeResidences_Governorates_GovernorateId] FOREIGN KEY ([GovernorateId]) REFERENCES [Governorates] ([Id]) ON DELETE SET NULL
);
GO


CREATE TABLE [WorkflowInstances] (
    [Id] bigint NOT NULL IDENTITY,
    [WorkflowVersionId] bigint NOT NULL,
    [EntityType] nvarchar(100) NOT NULL,
    [EntityId] nvarchar(100) NOT NULL,
    [CurrentStepId] bigint NOT NULL,
    [Status] int NOT NULL,
    [InitiatorUserId] nvarchar(256) NOT NULL,
    [StartedAt] datetimeoffset NOT NULL,
    [CompletedAt] datetimeoffset NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_WorkflowInstances] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkflowInstances_WorkflowSteps_CurrentStepId] FOREIGN KEY ([CurrentStepId]) REFERENCES [WorkflowSteps] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkflowInstances_WorkflowVersions_WorkflowVersionId] FOREIGN KEY ([WorkflowVersionId]) REFERENCES [WorkflowVersions] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [WorkflowTransitions] (
    [Id] bigint NOT NULL IDENTITY,
    [WorkflowVersionId] bigint NOT NULL,
    [FromStepId] bigint NOT NULL,
    [ToStepId] bigint NOT NULL,
    [ActionCode] nvarchar(100) NOT NULL,
    [Priority] int NOT NULL,
    [IsAutomatic] bit NOT NULL,
    [ConditionConfiguration] nvarchar(max) NULL,
    [RequiredPermission] nvarchar(200) NULL,
    [ConfigurationJson] nvarchar(max) NULL,
    CONSTRAINT [PK_WorkflowTransitions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkflowTransitions_WorkflowSteps_FromStepId] FOREIGN KEY ([FromStepId]) REFERENCES [WorkflowSteps] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkflowTransitions_WorkflowSteps_ToStepId] FOREIGN KEY ([ToStepId]) REFERENCES [WorkflowSteps] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_WorkflowTransitions_WorkflowVersions_WorkflowVersionId] FOREIGN KEY ([WorkflowVersionId]) REFERENCES [WorkflowVersions] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(128) NOT NULL,
    [ProviderKey] nvarchar(128) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [AspNetUserPasskeys] (
    [CredentialId] varbinary(1024) NOT NULL,
    [UserId] nvarchar(450) NOT NULL,
    [Data] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_AspNetUserPasskeys] PRIMARY KEY ([CredentialId]),
    CONSTRAINT [FK_AspNetUserPasskeys_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [AspNetUserRoles] (
    [UserId] nvarchar(450) NOT NULL,
    [RoleId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [AspNetUserTokens] (
    [UserId] nvarchar(450) NOT NULL,
    [LoginProvider] nvarchar(128) NOT NULL,
    [Name] nvarchar(128) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [EmployeeAllowanceItems] (
    [Id] int NOT NULL IDENTITY,
    [PeriodId] int NOT NULL,
    [AllowanceTypeId] int NULL,
    [Name] nvarchar(200) NOT NULL,
    [CalculationType] int NOT NULL,
    [Percentage] decimal(8,2) NULL,
    [FixedAmount] decimal(18,2) NULL,
    [CalculatedAmount] decimal(18,2) NOT NULL,
    [Notes] nvarchar(300) NULL,
    CONSTRAINT [PK_EmployeeAllowanceItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeAllowanceItems_AllowanceTypes_AllowanceTypeId] FOREIGN KEY ([AllowanceTypeId]) REFERENCES [AllowanceTypes] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_EmployeeAllowanceItems_EmployeeAllowancePeriods_PeriodId] FOREIGN KEY ([PeriodId]) REFERENCES [EmployeeAllowancePeriods] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [WorkflowEvents] (
    [Id] bigint NOT NULL IDENTITY,
    [WorkflowInstanceId] bigint NOT NULL,
    [WorkflowVersionId] bigint NOT NULL,
    [StepId] bigint NULL,
    [EventType] nvarchar(100) NOT NULL,
    [ActionCode] nvarchar(100) NULL,
    [FromStepId] bigint NULL,
    [ToStepId] bigint NULL,
    [PerformedByUserId] nvarchar(256) NOT NULL,
    [PerformedByDisplayName] nvarchar(300) NULL,
    [OccurredAt] datetimeoffset NOT NULL,
    [Comment] nvarchar(2000) NULL,
    [MetadataJson] nvarchar(max) NULL,
    [CorrelationId] nvarchar(100) NULL,
    [IpAddress] nvarchar(64) NULL,
    [UserAgent] nvarchar(500) NULL,
    CONSTRAINT [PK_WorkflowEvents] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkflowEvents_WorkflowInstances_WorkflowInstanceId] FOREIGN KEY ([WorkflowInstanceId]) REFERENCES [WorkflowInstances] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [WorkflowTasks] (
    [Id] bigint NOT NULL IDENTITY,
    [WorkflowInstanceId] bigint NOT NULL,
    [WorkflowStepId] bigint NOT NULL,
    [Status] int NOT NULL,
    [AssignedUserId] nvarchar(256) NULL,
    [AssignedRoleId] nvarchar(256) NULL,
    [AssignedDepartmentId] int NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [AssignedAt] datetimeoffset NULL,
    [StartedAt] datetimeoffset NULL,
    [DueAt] datetimeoffset NULL,
    [CompletedAt] datetimeoffset NULL,
    [Priority] int NOT NULL,
    [DelegatedFromUserId] nvarchar(256) NULL,
    [ReassignedFromUserId] nvarchar(256) NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_WorkflowTasks] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkflowTasks_WorkflowInstances_WorkflowInstanceId] FOREIGN KEY ([WorkflowInstanceId]) REFERENCES [WorkflowInstances] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_WorkflowTasks_WorkflowSteps_WorkflowStepId] FOREIGN KEY ([WorkflowStepId]) REFERENCES [WorkflowSteps] ([Id]) ON DELETE NO ACTION
);
GO


CREATE TABLE [WorkflowConditions] (
    [Id] bigint NOT NULL IDENTITY,
    [TransitionId] bigint NOT NULL,
    [FieldName] nvarchar(100) NOT NULL,
    [Operator] nvarchar(20) NOT NULL,
    [Value] nvarchar(500) NOT NULL,
    [LogicGroup] nvarchar(50) NULL,
    CONSTRAINT [PK_WorkflowConditions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkflowConditions_WorkflowTransitions_TransitionId] FOREIGN KEY ([TransitionId]) REFERENCES [WorkflowTransitions] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [WorkflowComments] (
    [Id] bigint NOT NULL IDENTITY,
    [WorkflowInstanceId] bigint NOT NULL,
    [WorkflowTaskId] bigint NULL,
    [UserId] nvarchar(256) NOT NULL,
    [CommentType] nvarchar(50) NOT NULL,
    [Content] nvarchar(2000) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_WorkflowComments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkflowComments_WorkflowInstances_WorkflowInstanceId] FOREIGN KEY ([WorkflowInstanceId]) REFERENCES [WorkflowInstances] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_WorkflowComments_WorkflowTasks_WorkflowTaskId] FOREIGN KEY ([WorkflowTaskId]) REFERENCES [WorkflowTasks] ([Id])
);
GO


IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'DefaultLatitude', N'DefaultLongitude', N'DisplayOrder', N'Name', N'NameKu') AND [object_id] = OBJECT_ID(N'[Governorates]'))
    SET IDENTITY_INSERT [Governorates] ON;
INSERT INTO [Governorates] ([Id], [DefaultLatitude], [DefaultLongitude], [DisplayOrder], [Name], [NameKu])
VALUES (1, 33.315199999999997E0, 44.366100000000003E0, 1, N'بغداد', N'بەغدا'),
(2, 30.508099999999999E0, 47.783499999999997E0, 2, N'البصرة', N'بەسرە'),
(3, 36.3489E0, 43.157699999999998E0, 3, N'نينوى', N'نەینەوا'),
(4, 36.190100000000001E0, 44.009099999999997E0, 4, N'أربيل', N'هەولێر'),
(5, 32.0259E0, 44.346200000000003E0, 5, N'النجف الأشرف', N'نەجەف'),
(6, 32.616E0, 44.024900000000002E0, 6, N'كربلاء المقدسة', N'کەربەلا'),
(7, 35.4681E0, 44.392200000000003E0, 7, N'كركوك', N'کەرکووک'),
(8, 33.423299999999998E0, 43.3033E0, 8, N'الأنبار', N'ئەنبار'),
(9, 32.483499999999999E0, 44.43E0, 9, N'بابل', N'بابل'),
(10, 35.566899999999997E0, 45.416499999999999E0, 10, N'السليمانية', N'سلێمانی'),
(11, 36.867899999999999E0, 42.990600000000001E0, 11, N'دهوك', N'دهۆک'),
(12, 33.746299999999998E0, 44.643300000000004E0, 12, N'ديالى', N'دیالە'),
(13, 34.606200000000001E0, 43.6783E0, 13, N'صلاح الدين', N'سەڵاحەدین'),
(14, 32.512500000000003E0, 45.819699999999997E0, 14, N'واسط', N'واسیت'),
(15, 31.841699999999999E0, 47.144399999999997E0, 15, N'ميسان', N'مەیسان'),
(16, 31.0579E0, 46.257300000000001E0, 16, N'ذي قار', N'زیقار'),
(17, 31.3142E0, 45.281100000000002E0, 17, N'المثنى', N'موسەننا'),
(18, 31.9922E0, 44.924399999999999E0, 18, N'القادسية', N'قادسیە'),
(19, 35.177799999999998E0, 45.9861E0, 19, N'حلبجة', N'هەڵەبجە');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'DefaultLatitude', N'DefaultLongitude', N'DisplayOrder', N'Name', N'NameKu') AND [object_id] = OBJECT_ID(N'[Governorates]'))
    SET IDENTITY_INSERT [Governorates] OFF;
GO


IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AllowAttachments', N'AllowComplaints', N'AllowIdeas', N'AllowInquiries', N'AllowNotes', N'AllowOthers', N'DisabledMessage', N'EnableFeedbackFab', N'EnableMyFeedback', N'UpdatedAtUtc', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[SystemFeatureSettings]'))
    SET IDENTITY_INSERT [SystemFeatureSettings] ON;
INSERT INTO [SystemFeatureSettings] ([Id], [AllowAttachments], [AllowComplaints], [AllowIdeas], [AllowInquiries], [AllowNotes], [AllowOthers], [DisabledMessage], [EnableFeedbackFab], [EnableMyFeedback], [UpdatedAtUtc], [UpdatedBy])
VALUES (1, CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), CAST(1 AS bit), N'خدمة تقديم الأفكار والمقترحات متوقفة مؤقتاً بقرار الإدارة.', CAST(1 AS bit), CAST(1 AS bit), '2026-01-01T00:00:00.0000000Z', N'System');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AllowAttachments', N'AllowComplaints', N'AllowIdeas', N'AllowInquiries', N'AllowNotes', N'AllowOthers', N'DisabledMessage', N'EnableFeedbackFab', N'EnableMyFeedback', N'UpdatedAtUtc', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[SystemFeatureSettings]'))
    SET IDENTITY_INSERT [SystemFeatureSettings] OFF;
GO


CREATE INDEX [IX_AdministrativeUnitClosures_Depth] ON [AdministrativeUnitClosures] ([Depth]);
GO


CREATE INDEX [IX_AdministrativeUnitClosures_DescendantId] ON [AdministrativeUnitClosures] ([DescendantId]);
GO


CREATE INDEX [IX_AdministrativeUnitClosures_LineagePath] ON [AdministrativeUnitClosures] ([LineagePath]);
GO


CREATE INDEX [IX_AdministrativeUnits_Code] ON [AdministrativeUnits] ([Code]);
GO


CREATE INDEX [IX_AdministrativeUnits_Name] ON [AdministrativeUnits] ([Name]);
GO


CREATE INDEX [IX_AdministrativeUnits_ParentUnitId] ON [AdministrativeUnits] ([ParentUnitId]);
GO


CREATE INDEX [IX_AdministrativeUnits_UnitTypeId] ON [AdministrativeUnits] ([UnitTypeId]);
GO


CREATE INDEX [IX_AdministrativeUnitTypes_Code] ON [AdministrativeUnitTypes] ([Code]);
GO


CREATE INDEX [IX_AdministrativeUnitTypes_HierarchyLevel] ON [AdministrativeUnitTypes] ([HierarchyLevel]);
GO


CREATE INDEX [IX_AdministrativeUnitTypes_Name] ON [AdministrativeUnitTypes] ([Name]);
GO


CREATE INDEX [IX_AllowanceTypes_Code] ON [AllowanceTypes] ([Code]);
GO


CREATE INDEX [IX_AllowanceTypes_Name] ON [AllowanceTypes] ([Name]);
GO


CREATE INDEX [IX_AppreciationLetters_EmployeeId_LetterDate] ON [AppreciationLetters] ([EmployeeId], [LetterDate]);
GO


CREATE INDEX [IX_AppreciationLetters_TitleId] ON [AppreciationLetters] ([TitleId]);
GO


CREATE INDEX [IX_AppreciationLetters_TypeId] ON [AppreciationLetters] ([TypeId]);
GO


CREATE UNIQUE INDEX [IX_AppreciationLetterTitles_Name] ON [AppreciationLetterTitles] ([Name]);
GO


CREATE UNIQUE INDEX [IX_AppreciationLetterTypes_Name] ON [AppreciationLetterTypes] ([Name]);
GO


CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
GO


CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;
GO


CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
GO


CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
GO


CREATE INDEX [IX_AspNetUserPasskeys_UserId] ON [AspNetUserPasskeys] ([UserId]);
GO


CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
GO


CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
GO


CREATE INDEX [IX_AspNetUsers_EmployeeID] ON [AspNetUsers] ([EmployeeID]);
GO


CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;
GO


CREATE INDEX [IX_AuditLogs_EmployeeID] ON [AuditLogs] ([EmployeeID]);
GO


CREATE INDEX [IX_AuditLogs_Timestamp] ON [AuditLogs] ([Timestamp]);
GO


CREATE INDEX [IX_DossierExportAudits_CreatedAtUtc] ON [DossierExportAudits] ([CreatedAtUtc]);
GO


CREATE INDEX [IX_DossierExportAudits_EmployeeIDNumber] ON [DossierExportAudits] ([EmployeeIDNumber]);
GO


CREATE UNIQUE INDEX [IX_DossierExportAudits_VerificationToken] ON [DossierExportAudits] ([VerificationToken]);
GO


CREATE UNIQUE INDEX [IX_EducationLevels_Name] ON [EducationLevels] ([Name]);
GO


CREATE INDEX [IX_EmployeeAllowanceItems_AllowanceTypeId] ON [EmployeeAllowanceItems] ([AllowanceTypeId]);
GO


CREATE INDEX [IX_EmployeeAllowanceItems_PeriodId] ON [EmployeeAllowanceItems] ([PeriodId]);
GO


CREATE INDEX [IX_EmployeeAllowancePeriods_EmployeeId_IsCurrent] ON [EmployeeAllowancePeriods] ([EmployeeId], [IsCurrent]);
GO


CREATE INDEX [IX_EmployeeAllowancePeriods_StartDate] ON [EmployeeAllowancePeriods] ([StartDate]);
GO


CREATE INDEX [IX_EmployeeCareerMovements_EmployeeId_MovementDate] ON [EmployeeCareerMovements] ([EmployeeId], [MovementDate]);
GO


CREATE INDEX [IX_EmployeeCareerMovements_MovementDate] ON [EmployeeCareerMovements] ([MovementDate]);
GO


CREATE INDEX [IX_EmployeeCourses_CourseName] ON [EmployeeCourses] ([CourseName]);
GO


CREATE INDEX [IX_EmployeeCourses_EmployeeId_CourseName] ON [EmployeeCourses] ([EmployeeId], [CourseName]);
GO


CREATE INDEX [IX_EmployeeCourses_StartDate] ON [EmployeeCourses] ([StartDate]);
GO


CREATE UNIQUE INDEX [IX_EmployeeEmails_Email] ON [EmployeeEmails] ([Email]);
GO


CREATE INDEX [IX_EmployeeEmails_EmployeeID] ON [EmployeeEmails] ([EmployeeID]);
GO


CREATE INDEX [IX_EmployeeFeedbacks_CreatedAt] ON [EmployeeFeedbacks] ([CreatedAt]);
GO


CREATE INDEX [IX_EmployeeFeedbacks_EmployeeId] ON [EmployeeFeedbacks] ([EmployeeId]);
GO


CREATE INDEX [IX_EmployeeFeedbacks_FeedbackType] ON [EmployeeFeedbacks] ([FeedbackType]);
GO


CREATE UNIQUE INDEX [IX_EmployeeFeedbacks_ReferenceNumber] ON [EmployeeFeedbacks] ([ReferenceNumber]);
GO


CREATE INDEX [IX_EmployeeFeedbacks_Status] ON [EmployeeFeedbacks] ([Status]);
GO


CREATE INDEX [IX_EmployeeLeaves_EmployeeId_StartDate] ON [EmployeeLeaves] ([EmployeeId], [StartDate]);
GO


CREATE INDEX [IX_EmployeeLeaves_EndDate] ON [EmployeeLeaves] ([EndDate]);
GO


CREATE INDEX [IX_EmployeeLeaves_LeaveTypeId] ON [EmployeeLeaves] ([LeaveTypeId]);
GO


CREATE INDEX [IX_EmployeeLeaves_StartDate] ON [EmployeeLeaves] ([StartDate]);
GO


CREATE INDEX [IX_EmployeeMartyrs_EmployeeId] ON [EmployeeMartyrs] ([EmployeeId]);
GO


CREATE INDEX [IX_EmployeeMartyrs_EmployeeIdNumber] ON [EmployeeMartyrs] ([EmployeeIdNumber]);
GO


CREATE INDEX [IX_EmployeeMartyrs_KinshipId] ON [EmployeeMartyrs] ([KinshipId]);
GO


CREATE INDEX [IX_EmployeeMartyrs_MartyrdomTypeId] ON [EmployeeMartyrs] ([MartyrdomTypeId]);
GO


CREATE INDEX [IX_EmployeePasskeys_EmployeeID_IsActive] ON [EmployeePasskeys] ([EmployeeID], [IsActive]);
GO


CREATE INDEX [IX_EmployeePasskeys_UserId_IsActive] ON [EmployeePasskeys] ([UserId], [IsActive]);
GO


CREATE INDEX [IX_EmployeePenalties_EmployeeId_PenaltyDate] ON [EmployeePenalties] ([EmployeeId], [PenaltyDate]);
GO


CREATE INDEX [IX_EmployeePenalties_PenaltyDate] ON [EmployeePenalties] ([PenaltyDate]);
GO


CREATE INDEX [IX_EmployeePenalties_PenaltyTypeId] ON [EmployeePenalties] ([PenaltyTypeId]);
GO


CREATE INDEX [IX_EmployeePhones_EmployeeID] ON [EmployeePhones] ([EmployeeID]);
GO


CREATE INDEX [IX_EmployeePhones_PhoneNumber] ON [EmployeePhones] ([PhoneNumber]);
GO


CREATE INDEX [IX_EmployeeProfilePhotos_EmployeeID] ON [EmployeeProfilePhotos] ([EmployeeID]);
GO


CREATE UNIQUE INDEX [IX_EmployeeProfilePhotos_EmployeeID_IsActive] ON [EmployeeProfilePhotos] ([EmployeeID], [IsActive]) WHERE [IsActive] = 1;
GO


CREATE INDEX [IX_EmployeeResidences_EmployeeId] ON [EmployeeResidences] ([EmployeeId]);
GO


CREATE INDEX [IX_EmployeeResidences_EmployeeIdNumber] ON [EmployeeResidences] ([EmployeeIdNumber]);
GO


CREATE INDEX [IX_EmployeeResidences_FromDate] ON [EmployeeResidences] ([FromDate]);
GO


CREATE INDEX [IX_EmployeeResidences_GovernorateId] ON [EmployeeResidences] ([GovernorateId]);
GO


CREATE INDEX [IX_EmployeeResidences_ToDate] ON [EmployeeResidences] ([ToDate]);
GO


CREATE INDEX [IX_Employees_AdministrativeUnitId] ON [Employees] ([AdministrativeUnitId]);
GO


CREATE INDEX [IX_Employees_EducationLevelId] ON [Employees] ([EducationLevelId]);
GO


CREATE UNIQUE INDEX [IX_Employees_EmployeeID] ON [Employees] ([EmployeeID]);
GO


CREATE INDEX [IX_Employees_EmployeeStatusId] ON [Employees] ([EmployeeStatusId]);
GO


CREATE INDEX [IX_Employees_FormationId] ON [Employees] ([FormationId]);
GO


CREATE INDEX [IX_Employees_GenderId] ON [Employees] ([GenderId]);
GO


CREATE INDEX [IX_Employees_GradeId] ON [Employees] ([GradeId]);
GO


CREATE INDEX [IX_Employees_JobTitleId] ON [Employees] ([JobTitleId]);
GO


CREATE INDEX [IX_Employees_MaritalStatusId] ON [Employees] ([MaritalStatusId]);
GO


CREATE INDEX [IX_Employees_NationalId] ON [Employees] ([NationalId]);
GO


CREATE INDEX [IX_Employees_NationalityId] ON [Employees] ([NationalityId]);
GO


CREATE INDEX [IX_Employees_PositionTitleId] ON [Employees] ([PositionTitleId]);
GO


CREATE INDEX [IX_Employees_ReligionId] ON [Employees] ([ReligionId]);
GO


CREATE INDEX [IX_Employees_SpecializationId] ON [Employees] ([SpecializationId]);
GO


CREATE INDEX [IX_Employees_StageId] ON [Employees] ([StageId]);
GO


CREATE INDEX [IX_Employees_UnifiedEmployeeNumber] ON [Employees] ([UnifiedEmployeeNumber]);
GO


CREATE UNIQUE INDEX [IX_EmployeeStatuses_Name] ON [EmployeeStatuses] ([Name]);
GO


CREATE UNIQUE INDEX [IX_Formations_Name] ON [Formations] ([Name]);
GO


CREATE UNIQUE INDEX [IX_Genders_Name] ON [Genders] ([Name]);
GO


CREATE UNIQUE INDEX [IX_Governorates_Name] ON [Governorates] ([Name]);
GO


CREATE UNIQUE INDEX [IX_Grades_Name] ON [Grades] ([Name]);
GO


CREATE UNIQUE INDEX [IX_JobTitles_Name] ON [JobTitles] ([Name]);
GO


CREATE UNIQUE INDEX [IX_LeaveTypes_Name] ON [LeaveTypes] ([Name]);
GO


CREATE UNIQUE INDEX [IX_MaritalStatuses_Name] ON [MaritalStatuses] ([Name]);
GO


CREATE UNIQUE INDEX [IX_MartyrdomTypes_Name] ON [MartyrdomTypes] ([Name]);
GO


CREATE UNIQUE INDEX [IX_MartyrKinships_Name] ON [MartyrKinships] ([Name]);
GO


CREATE UNIQUE INDEX [IX_Nationalities_Name] ON [Nationalities] ([Name]);
GO


CREATE UNIQUE INDEX [IX_PenaltyTypes_Name] ON [PenaltyTypes] ([Name]);
GO


CREATE UNIQUE INDEX [IX_PositionTitles_Name] ON [PositionTitles] ([Name]);
GO


CREATE UNIQUE INDEX [IX_Religions_Name] ON [Religions] ([Name]);
GO


CREATE INDEX [IX_SalaryScales_GradeCode_StageCode] ON [SalaryScales] ([GradeCode], [StageCode]);
GO


CREATE UNIQUE INDEX [IX_SalaryScales_GradeId_StageId] ON [SalaryScales] ([GradeId], [StageId]);
GO


CREATE INDEX [IX_SalaryScales_GradeSortOrder] ON [SalaryScales] ([GradeSortOrder]);
GO


CREATE INDEX [IX_SalaryScales_StageId] ON [SalaryScales] ([StageId]);
GO


CREATE INDEX [IX_SalaryScales_StageNumber] ON [SalaryScales] ([StageNumber]);
GO


CREATE UNIQUE INDEX [IX_Specializations_Name] ON [Specializations] ([Name]);
GO


CREATE UNIQUE INDEX [IX_Stages_Name] ON [Stages] ([Name]);
GO


CREATE INDEX [IX_UserDevices_DeviceId] ON [UserDevices] ([DeviceId]);
GO


CREATE INDEX [IX_UserDevices_EmployeeID_IsActive] ON [UserDevices] ([EmployeeID], [IsActive]);
GO


CREATE INDEX [IX_UserDevices_UserId_IsActive] ON [UserDevices] ([UserId], [IsActive]);
GO


CREATE INDEX [IX_UserNotifications_CreatedAt] ON [UserNotifications] ([CreatedAt]);
GO


CREATE INDEX [IX_UserNotifications_UserId_IsRead] ON [UserNotifications] ([UserId], [IsRead]);
GO


CREATE INDEX [IX_WorkflowComments_WorkflowInstanceId] ON [WorkflowComments] ([WorkflowInstanceId]);
GO


CREATE INDEX [IX_WorkflowComments_WorkflowTaskId] ON [WorkflowComments] ([WorkflowTaskId]);
GO


CREATE INDEX [IX_WorkflowConditions_TransitionId] ON [WorkflowConditions] ([TransitionId]);
GO


CREATE UNIQUE INDEX [IX_WorkflowDefinitions_Code] ON [WorkflowDefinitions] ([Code]);
GO


CREATE INDEX [IX_WorkflowEvents_WorkflowInstanceId_OccurredAt] ON [WorkflowEvents] ([WorkflowInstanceId], [OccurredAt]);
GO


CREATE UNIQUE INDEX [IX_WorkflowIdempotencyRecords_IdempotencyKey] ON [WorkflowIdempotencyRecords] ([IdempotencyKey]);
GO


CREATE INDEX [IX_WorkflowInstances_CurrentStepId] ON [WorkflowInstances] ([CurrentStepId]);
GO


CREATE INDEX [IX_WorkflowInstances_EntityType_EntityId] ON [WorkflowInstances] ([EntityType], [EntityId]);
GO


CREATE INDEX [IX_WorkflowInstances_InitiatorUserId] ON [WorkflowInstances] ([InitiatorUserId]);
GO


CREATE INDEX [IX_WorkflowInstances_WorkflowVersionId_Status] ON [WorkflowInstances] ([WorkflowVersionId], [Status]);
GO


CREATE UNIQUE INDEX [IX_WorkflowSteps_WorkflowVersionId_Code] ON [WorkflowSteps] ([WorkflowVersionId], [Code]);
GO


CREATE INDEX [IX_WorkflowTasks_AssignedDepartmentId_Status] ON [WorkflowTasks] ([AssignedDepartmentId], [Status]);
GO


CREATE INDEX [IX_WorkflowTasks_AssignedRoleId_Status] ON [WorkflowTasks] ([AssignedRoleId], [Status]);
GO


CREATE INDEX [IX_WorkflowTasks_AssignedUserId_Status] ON [WorkflowTasks] ([AssignedUserId], [Status]);
GO


CREATE INDEX [IX_WorkflowTasks_WorkflowInstanceId] ON [WorkflowTasks] ([WorkflowInstanceId]);
GO


CREATE INDEX [IX_WorkflowTasks_WorkflowStepId] ON [WorkflowTasks] ([WorkflowStepId]);
GO


CREATE INDEX [IX_WorkflowTransitions_FromStepId] ON [WorkflowTransitions] ([FromStepId]);
GO


CREATE INDEX [IX_WorkflowTransitions_ToStepId] ON [WorkflowTransitions] ([ToStepId]);
GO


CREATE INDEX [IX_WorkflowTransitions_WorkflowVersionId] ON [WorkflowTransitions] ([WorkflowVersionId]);
GO


CREATE UNIQUE INDEX [IX_WorkflowVersions_WorkflowDefinitionId_VersionNumber] ON [WorkflowVersions] ([WorkflowDefinitionId], [VersionNumber]);
GO


