-- SmartCube database structure (no data). Generated 2026-09-30 08:26 from netusers-SmartCubeMobile.
-- Safe to run on an empty database; tables that already exist are skipped.

IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NULL
CREATE TABLE [dbo].[__EFMigrationsHistory] (
    [MigrationId] nvarchar(150) NOT NULL,
    [ProductVersion] nvarchar(32) NOT NULL,
    CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY CLUSTERED ([MigrationId])
);
GO
IF OBJECT_ID(N'[dbo].[AspNetRoleClaims]', N'U') IS NULL
CREATE TABLE [dbo].[AspNetRoleClaims] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [RoleId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY CLUSTERED ([Id])
);
GO
IF OBJECT_ID(N'[dbo].[AspNetRoles]', N'U') IS NULL
CREATE TABLE [dbo].[AspNetRoles] (
    [Id] nvarchar(450) NOT NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY CLUSTERED ([Id])
);
GO
IF OBJECT_ID(N'[dbo].[AspNetUserClaims]', N'U') IS NULL
CREATE TABLE [dbo].[AspNetUserClaims] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [UserId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY CLUSTERED ([Id])
);
GO
IF OBJECT_ID(N'[dbo].[AspNetUserLogins]', N'U') IS NULL
CREATE TABLE [dbo].[AspNetUserLogins] (
    [LoginProvider] nvarchar(128) NOT NULL,
    [ProviderKey] nvarchar(128) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY CLUSTERED ([LoginProvider], [ProviderKey])
);
GO
IF OBJECT_ID(N'[dbo].[AspNetUserRoles]', N'U') IS NULL
CREATE TABLE [dbo].[AspNetUserRoles] (
    [UserId] nvarchar(450) NOT NULL,
    [RoleId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY CLUSTERED ([UserId], [RoleId])
);
GO
IF OBJECT_ID(N'[dbo].[AspNetUsers]', N'U') IS NULL
CREATE TABLE [dbo].[AspNetUsers] (
    [Id] nvarchar(450) NOT NULL,
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset(7) NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    [Administrator] bit NOT NULL CONSTRAINT [DF__AspNetUse__Admin__5070F446] DEFAULT (CONVERT([bit],(0))),
    [ClearPassword] nvarchar(max) NULL CONSTRAINT [DF__AspNetUse__Clear__5165187F] DEFAULT (N''),
    [Expiration1] datetime2(7) NOT NULL CONSTRAINT [DF__AspNetUse__Expir__52593CB8] DEFAULT ('0001-01-01T00:00:00.0000000'),
    [Expiration2] datetime2(7) NOT NULL CONSTRAINT [DF__AspNetUse__Expir__534D60F1] DEFAULT ('0001-01-01T00:00:00.0000000'),
    [Expiration3] datetime2(7) NOT NULL CONSTRAINT [DF__AspNetUse__Expir__5441852A] DEFAULT ('0001-01-01T00:00:00.0000000'),
    [LastLogOnTime] datetime2(7) NOT NULL CONSTRAINT [DF__AspNetUse__LastL__5535A963] DEFAULT ('0001-01-01T00:00:00.0000000'),
    [MultipleMeter] bit NOT NULL CONSTRAINT [DF__AspNetUse__Multi__5629CD9C] DEFAULT (CONVERT([bit],(0))),
    [Subscriber] bit NOT NULL CONSTRAINT [DF__AspNetUse__Subsc__571DF1D5] DEFAULT (CONVERT([bit],(0))),
    [Trace] bit NOT NULL CONSTRAINT [DF__AspNetUse__Trace__5812160E] DEFAULT (CONVERT([bit],(0))),
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY CLUSTERED ([Id])
);
GO
IF OBJECT_ID(N'[dbo].[AspNetUserTokens]', N'U') IS NULL
CREATE TABLE [dbo].[AspNetUserTokens] (
    [UserId] nvarchar(450) NOT NULL,
    [LoginProvider] nvarchar(128) NOT NULL,
    [Name] nvarchar(128) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY CLUSTERED ([UserId], [LoginProvider], [Name])
);
GO
IF OBJECT_ID(N'[dbo].[DataProtectionKeys]', N'U') IS NULL
CREATE TABLE [dbo].[DataProtectionKeys] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [FriendlyName] nvarchar(max) NULL,
    [Xml] nvarchar(max) NULL,
    CONSTRAINT [PK__DataProt__3214EC079E745492] PRIMARY KEY CLUSTERED ([Id])
);
GO
IF OBJECT_ID(N'[dbo].[DeviceTokens]', N'U') IS NULL
CREATE TABLE [dbo].[DeviceTokens] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [TokenHash] nvarchar(64) NOT NULL,
    [UserId] nvarchar(450) NOT NULL,
    [DeviceName] nvarchar(100) NULL,
    [Created] datetime2(7) NOT NULL CONSTRAINT [DF__DeviceTok__Creat__0D7A0286] DEFAULT (sysutcdatetime()),
    [LastUsed] datetime2(7) NULL,
    [Revoked] bit NOT NULL CONSTRAINT [DF__DeviceTok__Revok__0E6E26BF] DEFAULT ((0)),
    [PinHash] nvarchar(300) NULL,
    [PinFailures] int NOT NULL CONSTRAINT [DF_DeviceTokens_PinFailures] DEFAULT ((0)),
    [DeviceKey] nvarchar(500) NULL,
    CONSTRAINT [PK__DeviceTo__3214EC072BB189EB] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ__DeviceTo__BCB33F92FD938E66] UNIQUE NONCLUSTERED ([TokenHash])
);
GO
IF OBJECT_ID(N'[dbo].[DevNoteRevisions]', N'U') IS NULL
CREATE TABLE [dbo].[DevNoteRevisions] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [NoteId] int NOT NULL,
    [Title] nvarchar(400) NULL,
    [Body] nvarchar(max) NULL,
    [SavedBy] nvarchar(256) NULL,
    [SavedAt] datetime2(7) NOT NULL CONSTRAINT [DF__DevNoteRe__Saved__29221CFB] DEFAULT (sysutcdatetime()),
    CONSTRAINT [PK__DevNoteR__3214EC07624F5128] PRIMARY KEY CLUSTERED ([Id])
);
GO
IF OBJECT_ID(N'[dbo].[DevNotes]', N'U') IS NULL
CREATE TABLE [dbo].[DevNotes] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [Type] nvarchar(20) NOT NULL,
    [Title] nvarchar(200) NOT NULL,
    [Body] nvarchar(max) NULL,
    [Status] nvarchar(20) NOT NULL CONSTRAINT [DF__DevNotes__Status__1AD3FDA4] DEFAULT ('Open'),
    [Priority] nvarchar(10) NOT NULL CONSTRAINT [DF__DevNotes__Priori__1BC821DD] DEFAULT ('Normal'),
    [AppVersion] nvarchar(20) NULL,
    [CreatedBy] nvarchar(256) NULL,
    [CreatedAt] datetime2(7) NOT NULL CONSTRAINT [DF__DevNotes__Create__1CBC4616] DEFAULT (sysutcdatetime()),
    [UpdatedBy] nvarchar(256) NULL,
    [UpdatedAt] datetime2(7) NULL,
    CONSTRAINT [PK__DevNotes__3214EC07D2DE39CD] PRIMARY KEY CLUSTERED ([Id])
);
GO
IF OBJECT_ID(N'[dbo].[DownloadLog]', N'U') IS NULL
CREATE TABLE [dbo].[DownloadLog] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [UserId] nvarchar(450) NULL,
    [FileName] nvarchar(200) NOT NULL,
    [IP] nvarchar(64) NULL,
    [At] datetime2(7) NOT NULL CONSTRAINT [DF__DownloadLog__At__14270015] DEFAULT (sysutcdatetime()),
    CONSTRAINT [PK__Download__3214EC07F0C4D73F] PRIMARY KEY CLUSTERED ([Id])
);
GO
IF OBJECT_ID(N'[dbo].[LoginLog]', N'U') IS NULL
CREATE TABLE [dbo].[LoginLog] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [Login] nvarchar(256) NULL,
    [UserId] nvarchar(450) NULL,
    [Method] nvarchar(20) NOT NULL,
    [Success] bit NOT NULL,
    [Error] nvarchar(200) NULL,
    [AppVersion] nvarchar(40) NULL,
    [IP] nvarchar(64) NULL,
    [At] datetime2(7) NOT NULL CONSTRAINT [DF__LoginLog__At__114A936A] DEFAULT (sysutcdatetime()),
    CONSTRAINT [PK__LoginLog__3214EC077CC67EDA] PRIMARY KEY CLUSTERED ([Id])
);
GO
IF OBJECT_ID(N'[dbo].[PostcodeRegions]', N'U') IS NULL
CREATE TABLE [dbo].[PostcodeRegions] (
    [Outward] nvarchar(8) NOT NULL,
    [RegionId] int NOT NULL,
    [Source] nvarchar(20) NOT NULL,
    [UpdatedAt] datetime2(7) NOT NULL CONSTRAINT [DF__PostcodeR__Updat__2645B050] DEFAULT (sysutcdatetime()),
    CONSTRAINT [PK__Postcode__749B11439C6BD264] PRIMARY KEY CLUSTERED ([Outward])
);
GO
IF OBJECT_ID(N'[dbo].[Releases]', N'U') IS NULL
CREATE TABLE [dbo].[Releases] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [Version] nvarchar(20) NOT NULL,
    [Notes] nvarchar(max) NULL,
    [FileName] nvarchar(200) NULL,
    [SizeMb] decimal(8,1) NULL,
    [ReleasedAt] datetime2(7) NOT NULL CONSTRAINT [DF__Releases__Releas__17F790F9] DEFAULT (sysutcdatetime()),
    CONSTRAINT [PK__Releases__3214EC07AB209304] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ__Releases__0F54013438990C7E] UNIQUE NONCLUSTERED ([Version])
);
GO
IF OBJECT_ID(N'[dbo].[SmartScanLicences]', N'U') IS NULL
CREATE TABLE [dbo].[SmartScanLicences] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [LicenceKey] nvarchar(40) NOT NULL,
    [Email] nvarchar(256) NULL,
    [IsActive] bit NOT NULL CONSTRAINT [DF__SmartScan__IsAct__03F0984C] DEFAULT ((1)),
    [ScansUsed] int NOT NULL CONSTRAINT [DF__SmartScan__Scans__04E4BC85] DEFAULT ((0)),
    [ScanLimit] int NOT NULL CONSTRAINT [DF__SmartScan__ScanL__05D8E0BE] DEFAULT ((100)),
    [Expires] datetime2(7) NULL,
    [Created] datetime2(7) NOT NULL CONSTRAINT [DF__SmartScan__Creat__06CD04F7] DEFAULT (sysutcdatetime()),
    [LastUsed] datetime2(7) NULL,
    CONSTRAINT [PK__SmartSca__3214EC075BBBBB0B] PRIMARY KEY CLUSTERED ([Id]),
    CONSTRAINT [UQ__SmartSca__18230DD8FCA43003] UNIQUE NONCLUSTERED ([LicenceKey])
);
GO
IF OBJECT_ID(N'[dbo].[SmartScanLog]', N'U') IS NULL
CREATE TABLE [dbo].[SmartScanLog] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [LicenceKey] nvarchar(40) NOT NULL,
    [Category] nvarchar(20) NULL,
    [InputChars] int NOT NULL,
    [Success] bit NOT NULL,
    [Error] nvarchar(400) NULL,
    [ScannedAt] datetime2(7) NOT NULL CONSTRAINT [DF__SmartScan__Scann__09A971A2] DEFAULT (sysutcdatetime()),
    CONSTRAINT [PK__SmartSca__3214EC07C9B9DE25] PRIMARY KEY CLUSTERED ([Id])
);
GO
IF OBJECT_ID(N'[dbo].[SupportTicketReplies]', N'U') IS NULL
CREATE TABLE [dbo].[SupportTicketReplies] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [TicketId] int NOT NULL,
    [Author] nvarchar(256) NOT NULL,
    [IsStaff] bit NOT NULL,
    [Message] nvarchar(max) NOT NULL,
    [Emailed] bit NOT NULL CONSTRAINT [DF__SupportTi__Email__531856C7] DEFAULT ((0)),
    [CreatedAt] datetime2(7) NOT NULL CONSTRAINT [DF__SupportTi__Creat__540C7B00] DEFAULT (sysutcdatetime()),
    CONSTRAINT [PK__SupportT__3214EC07CE01FA9D] PRIMARY KEY CLUSTERED ([Id])
);
GO
IF OBJECT_ID(N'[dbo].[SupportTickets]', N'U') IS NULL
CREATE TABLE [dbo].[SupportTickets] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [UserId] nvarchar(450) NULL,
    [Email] nvarchar(256) NOT NULL,
    [Name] nvarchar(120) NULL,
    [Area] nvarchar(60) NOT NULL,
    [Subject] nvarchar(200) NOT NULL,
    [Message] nvarchar(max) NOT NULL,
    [Status] nvarchar(30) NOT NULL CONSTRAINT [DF__SupportTi__Statu__4F47C5E3] DEFAULT ('Open'),
    [AppVersion] nvarchar(20) NULL,
    [Ip] nvarchar(64) NULL,
    [CreatedAt] datetime2(7) NOT NULL CONSTRAINT [DF__SupportTi__Creat__503BEA1C] DEFAULT (sysutcdatetime()),
    [UpdatedAt] datetime2(7) NULL,
    [UpdatedBy] nvarchar(256) NULL,
    [Channel] nvarchar(10) NOT NULL CONSTRAINT [DF_SupportTickets_Channel] DEFAULT ('email'),
    [LastCustomerAt] datetime2(7) NULL,
    [LastStaffAt] datetime2(7) NULL,
    CONSTRAINT [PK__SupportT__3214EC07C7583644] PRIMARY KEY CLUSTERED ([Id])
);
GO
IF OBJECT_ID(N'[dbo].[TariffRegions]', N'U') IS NULL
CREATE TABLE [dbo].[TariffRegions] (
    [RegionId] int NOT NULL,
    [Name] nvarchar(60) NOT NULL,
    [Gsp] nvarchar(4) NULL,
    [Postcode] nvarchar(10) NULL,
    [DistributorId] int NULL,
    [ElecKwh] decimal(10,1) NULL,
    [GasKwh] decimal(10,1) NULL,
    [QuoteId] bigint NULL,
    [FetchedAt] datetime2(7) NULL,
    [TariffCount] int NOT NULL CONSTRAINT [DF__TariffReg__Tarif__1F98B2C1] DEFAULT ((0)),
    [ElecTariffCount] int NULL,
    [ElecFetchedAt] datetime2(7) NULL,
    [ElecOnlyKwh] decimal(12,2) NULL,
    [GasTariffCount] int NULL,
    [GasFetchedAt] datetime2(7) NULL,
    [GasOnlyKwh] decimal(12,2) NULL,
    CONSTRAINT [PK__TariffRe__ACD844A3B97B768A] PRIMARY KEY CLUSTERED ([RegionId])
);
GO
IF OBJECT_ID(N'[dbo].[Tariffs]', N'U') IS NULL
CREATE TABLE [dbo].[Tariffs] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [RegionId] int NOT NULL,
    [SupplierId] int NULL,
    [SupplierCode] nvarchar(40) NULL,
    [SupplierName] nvarchar(100) NULL,
    [TariffName] nvarchar(200) NULL,
    [TariffCode] nvarchar(200) NULL,
    [TariffTypes] nvarchar(200) NULL,
    [PaymentType] nvarchar(60) NULL,
    [FixedTermMonths] int NULL,
    [FixedTermEndDate] nvarchar(40) NULL,
    [AnnualCost] decimal(10,2) NOT NULL,
    [Saving] decimal(10,2) NULL,
    [ElecCost] decimal(10,2) NULL,
    [GasCost] decimal(10,2) NULL,
    [ElecUnitRate] decimal(10,5) NULL,
    [ElecStandingCharge] decimal(10,5) NULL,
    [GasUnitRate] decimal(10,5) NULL,
    [GasStandingCharge] decimal(10,5) NULL,
    [ElecExitFee] decimal(10,2) NULL,
    [GasExitFee] decimal(10,2) NULL,
    [MonthlyFee] decimal(10,2) NULL,
    [PaperlessBills] bit NOT NULL CONSTRAINT [DF__Tariffs__Paperle__22751F6C] DEFAULT ((0)),
    [Switchable] bit NOT NULL CONSTRAINT [DF__Tariffs__Switcha__236943A5] DEFAULT ((0)),
    [SignupUrl] nvarchar(600) NULL,
    [FetchedAt] datetime2(7) NOT NULL,
    [FuelType] nvarchar(10) NOT NULL CONSTRAINT [DF_Tariffs_FuelType] DEFAULT ('dual'),
    CONSTRAINT [PK__Tariffs__3214EC0740BAB0BD] PRIMARY KEY CLUSTERED ([Id])
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DevNoteRevisions_NoteId' AND object_id = OBJECT_ID(N'[dbo].[DevNoteRevisions]'))
    CREATE NONCLUSTERED INDEX [IX_DevNoteRevisions_NoteId] ON [dbo].[DevNoteRevisions] ([NoteId], [Id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SupportTicketReplies_Ticket' AND object_id = OBJECT_ID(N'[dbo].[SupportTicketReplies]'))
    CREATE NONCLUSTERED INDEX [IX_SupportTicketReplies_Ticket] ON [dbo].[SupportTicketReplies] ([TicketId], [Id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SupportTickets_Email' AND object_id = OBJECT_ID(N'[dbo].[SupportTickets]'))
    CREATE NONCLUSTERED INDEX [IX_SupportTickets_Email] ON [dbo].[SupportTickets] ([Email], [CreatedAt]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SupportTickets_Status' AND object_id = OBJECT_ID(N'[dbo].[SupportTickets]'))
    CREATE NONCLUSTERED INDEX [IX_SupportTickets_Status] ON [dbo].[SupportTickets] ([Status], [CreatedAt]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tariffs_Region' AND object_id = OBJECT_ID(N'[dbo].[Tariffs]'))
    CREATE NONCLUSTERED INDEX [IX_Tariffs_Region] ON [dbo].[Tariffs] ([RegionId], [AnnualCost]);
GO

IF OBJECT_ID(N'FK_AspNetRoleClaims_AspNetRoles_RoleId', N'F') IS NULL
    ALTER TABLE [dbo].[AspNetRoleClaims] ADD CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[AspNetRoles] ([Id]) ON DELETE CASCADE;
GO
IF OBJECT_ID(N'FK_AspNetUserClaims_AspNetUsers_UserId', N'F') IS NULL
    ALTER TABLE [dbo].[AspNetUserClaims] ADD CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE;
GO
IF OBJECT_ID(N'FK_AspNetUserLogins_AspNetUsers_UserId', N'F') IS NULL
    ALTER TABLE [dbo].[AspNetUserLogins] ADD CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE;
GO
IF OBJECT_ID(N'FK_AspNetUserRoles_AspNetRoles_RoleId', N'F') IS NULL
    ALTER TABLE [dbo].[AspNetUserRoles] ADD CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[AspNetRoles] ([Id]) ON DELETE CASCADE;
GO
IF OBJECT_ID(N'FK_AspNetUserRoles_AspNetUsers_UserId', N'F') IS NULL
    ALTER TABLE [dbo].[AspNetUserRoles] ADD CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE;
GO
IF OBJECT_ID(N'FK_AspNetUserTokens_AspNetUsers_UserId', N'F') IS NULL
    ALTER TABLE [dbo].[AspNetUserTokens] ADD CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE;
GO
IF OBJECT_ID(N'FK_DevNoteRevisions_DevNotes', N'F') IS NULL
    ALTER TABLE [dbo].[DevNoteRevisions] ADD CONSTRAINT [FK_DevNoteRevisions_DevNotes] FOREIGN KEY ([NoteId]) REFERENCES [dbo].[DevNotes] ([Id]) ON DELETE CASCADE;
GO
IF OBJECT_ID(N'FK_SupportTicketReplies_Ticket', N'F') IS NULL
    ALTER TABLE [dbo].[SupportTicketReplies] ADD CONSTRAINT [FK_SupportTicketReplies_Ticket] FOREIGN KEY ([TicketId]) REFERENCES [dbo].[SupportTickets] ([Id]) ON DELETE CASCADE;
GO
