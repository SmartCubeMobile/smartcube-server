-- Production operations queued from the admin page and run by run-jobs.ps1 on Dan's PC.
IF OBJECT_ID(N'[dbo].[ProdJobs]', N'U') IS NULL
CREATE TABLE [dbo].[ProdJobs] (
    [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ProdJobs] PRIMARY KEY CLUSTERED,
    [Job] nvarchar(40) NOT NULL,
    [Args] nvarchar(1000) NULL,
    [RequestedBy] nvarchar(256) NOT NULL,
    [RequestedAt] datetime2(7) NOT NULL CONSTRAINT [DF_ProdJobs_RequestedAt] DEFAULT (SYSUTCDATETIME()),
    [StartedAt] datetime2(7) NULL,
    [FinishedAt] datetime2(7) NULL,
    [Status] nvarchar(20) NOT NULL CONSTRAINT [DF_ProdJobs_Status] DEFAULT (N'queued'),
    [Output] nvarchar(max) NULL
);
GO
-- What production currently has checked out, so the admin page can say what's waiting on GitHub.
IF OBJECT_ID(N'[dbo].[ProdState]', N'U') IS NULL
CREATE TABLE [dbo].[ProdState] (
    [Part] nvarchar(20) NOT NULL CONSTRAINT [PK_ProdState] PRIMARY KEY CLUSTERED,
    [Sha] nvarchar(64) NULL,
    [Dirty] bit NOT NULL CONSTRAINT [DF_ProdState_Dirty] DEFAULT (0),
    [RunnerSeenAt] datetime2(7) NOT NULL CONSTRAINT [DF_ProdState_RunnerSeenAt] DEFAULT (SYSUTCDATETIME())
);
GO
