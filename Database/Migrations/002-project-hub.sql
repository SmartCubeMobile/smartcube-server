-- Project hub on the admin page: the links board and website monitoring history.
IF OBJECT_ID(N'[dbo].[ProjectLinks]', N'U') IS NULL
BEGIN
CREATE TABLE [dbo].[ProjectLinks] (
    [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_ProjectLinks] PRIMARY KEY CLUSTERED,
    [GroupName] nvarchar(60) NOT NULL,
    [Name] nvarchar(100) NOT NULL,
    [Url] nvarchar(500) NOT NULL,
    [Note] nvarchar(300) NULL,
    [SortOrder] int NOT NULL CONSTRAINT [DF_ProjectLinks_SortOrder] DEFAULT (0)
);
INSERT [dbo].[ProjectLinks] (GroupName, Name, Url, Note, SortOrder) VALUES
 (N'Live',      N'Website',              N'https://www.smartcubemobile.com',                    N'Public site (Netlify, behind Cloudflare)', 1),
 (N'Live',      N'API server site',      N'https://api.smartcubemobile.com/site/index.html',    N'Account pages served by this server', 2),
 (N'Live',      N'Admin page',           N'https://api.smartcubemobile.com/admin/',             NULL, 3),
 (N'Hosting',   N'Netlify dashboard',    N'https://app.netlify.com',                            N'Website hosting, deploys, domain', 1),
 (N'Hosting',   N'Cloudflare dashboard', N'https://dash.cloudflare.com',                        N'DNS for smartcubemobile.com and the tunnel to this server', 2),
 (N'Code',      N'GitHub: smartcube-app',    N'https://github.com/SmartCubeMobile/smartcube-app',    N'Shared app development', 1),
 (N'Code',      N'GitHub: smartcube-server', N'https://github.com/SmartCubeMobile/smartcube-server', N'Shared server development', 2),
 (N'Code',      N'GitHub organisation',      N'https://github.com/orgs/SmartCubeMobile/people',      N'Members and invitations', 3),
 (N'Services',  N'TrueLayer console',    N'https://console.truelayer.com',                      N'Bank connections; client secret lives in the server appsettings', 1),
 (N'Services',  N'Anthropic console',    N'https://console.anthropic.com',                      N'Claude API key for Smart Scan; usage and billing', 2),
 (N'Contact',   N'Support inbox',        N'https://mail.google.com/',                           N'smartcubemobilesupport@gmail.com (support tickets and password resets are sent from here)', 1),
 (N'Contact',   N'LinkedIn page',        N'https://www.linkedin.com/',                          N'Replace with the SmartCube page address', 2);
END
GO
IF OBJECT_ID(N'[dbo].[SiteChecks]', N'U') IS NULL
CREATE TABLE [dbo].[SiteChecks] (
    [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_SiteChecks] PRIMARY KEY CLUSTERED,
    [Target] nvarchar(50) NOT NULL,
    [CheckedAt] datetime2(7) NOT NULL CONSTRAINT [DF_SiteChecks_CheckedAt] DEFAULT (SYSUTCDATETIME()),
    [Ok] bit NOT NULL,
    [StatusCode] int NULL,
    [Ms] int NOT NULL,
    [Error] nvarchar(400) NULL
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SiteChecks_Target_CheckedAt' AND object_id = OBJECT_ID(N'[dbo].[SiteChecks]'))
    CREATE NONCLUSTERED INDEX [IX_SiteChecks_Target_CheckedAt] ON [dbo].[SiteChecks] ([Target], [CheckedAt]);
GO
