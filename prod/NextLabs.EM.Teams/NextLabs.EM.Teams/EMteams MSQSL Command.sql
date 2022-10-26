IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'EMTeamsDB')
    BEGIN
        CREATE DATABASE [EMTeamsDB];
    END;
GO

IF SERVERPROPERTY('EngineEdition') <> 5
    BEGIN
        ALTER DATABASE [EMTeamsDB] SET READ_COMMITTED_SNAPSHOT ON;
    END;

GO
	
USE [EMTeamsDB];

GO

CREATE TABLE [Team] (
    [Id] nvarchar(450) NOT NULL,
    [Name] nvarchar(max) NULL,
    [DoEnforce] int NOT NULL,
	[JsonClassifications] nvarchar(max) NULL,
    CONSTRAINT [PK_Team] PRIMARY KEY ([Id])
);

GO