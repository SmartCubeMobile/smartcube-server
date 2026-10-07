-- Results of the production backup job (Backup-SmartCube.ps1), shown on the admin page.
IF OBJECT_ID(N'[dbo].[BackupRuns]', N'U') IS NULL
CREATE TABLE [dbo].[BackupRuns] (
    [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_BackupRuns] PRIMARY KEY CLUSTERED,
    [At] datetime2(7) NOT NULL CONSTRAINT [DF_BackupRuns_At] DEFAULT (SYSUTCDATETIME()),
    [Kind] nvarchar(20) NOT NULL,          -- full | log | restore-test | error
    [Ok] bit NOT NULL,
    [Bytes] bigint NOT NULL CONSTRAINT [DF_BackupRuns_Bytes] DEFAULT (0),
    [FileName] nvarchar(260) NULL,
    [Message] nvarchar(1000) NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BackupRuns_Kind_At' AND object_id = OBJECT_ID(N'[dbo].[BackupRuns]'))
    CREATE NONCLUSTERED INDEX [IX_BackupRuns_Kind_At] ON [dbo].[BackupRuns] ([Kind], [At]);
GO
