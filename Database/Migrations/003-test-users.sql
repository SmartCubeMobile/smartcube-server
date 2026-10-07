-- Test users: accounts flagged by an admin whose app reports what they add, change, remove and upload,
-- so it can be watched on the admin page. Opt-in per account; events are purged after 30 days.
IF COL_LENGTH(N'dbo.AspNetUsers', N'IsTestUser') IS NULL
    ALTER TABLE [dbo].[AspNetUsers] ADD [IsTestUser] bit NOT NULL CONSTRAINT [DF_AspNetUsers_IsTestUser] DEFAULT (0);
GO
IF OBJECT_ID(N'[dbo].[TestEvents]', N'U') IS NULL
CREATE TABLE [dbo].[TestEvents] (
    [Id] bigint IDENTITY(1,1) NOT NULL CONSTRAINT [PK_TestEvents] PRIMARY KEY CLUSTERED,
    [UserId] nvarchar(450) NOT NULL,
    [At] datetime2(7) NOT NULL,
    [ReceivedAt] datetime2(7) NOT NULL CONSTRAINT [DF_TestEvents_ReceivedAt] DEFAULT (SYSUTCDATETIME()),
    [Area] nvarchar(60) NOT NULL,
    [Kind] nvarchar(20) NOT NULL,
    [ItemKey] nvarchar(200) NULL,
    [Summary] nvarchar(400) NOT NULL,
    [Data] nvarchar(max) NULL,
    [AppVersion] nvarchar(40) NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TestEvents_User_Id' AND object_id = OBJECT_ID(N'[dbo].[TestEvents]'))
    CREATE NONCLUSTERED INDEX [IX_TestEvents_User_Id] ON [dbo].[TestEvents] ([UserId], [Id]);
GO
IF OBJECT_ID(N'[dbo].[TestFiles]', N'U') IS NULL
CREATE TABLE [dbo].[TestFiles] (
    [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_TestFiles] PRIMARY KEY CLUSTERED,
    [UserId] nvarchar(450) NOT NULL,
    [At] datetime2(7) NOT NULL CONSTRAINT [DF_TestFiles_At] DEFAULT (SYSUTCDATETIME()),
    [FileName] nvarchar(260) NOT NULL,
    [Area] nvarchar(60) NULL,
    [Note] nvarchar(300) NULL,
    [SizeBytes] bigint NOT NULL,
    [StoredPath] nvarchar(400) NOT NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TestFiles_User_At' AND object_id = OBJECT_ID(N'[dbo].[TestFiles]'))
    CREATE NONCLUSTERED INDEX [IX_TestFiles_User_At] ON [dbo].[TestFiles] ([UserId], [At]);
GO
