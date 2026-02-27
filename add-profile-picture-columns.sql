-- Add ProfilePicturePath columns to existing database
-- Run this script against the SmartLog database

USE SmartLog;
GO

-- Add ProfilePicturePath to AspNetUsers table (if not exists)
IF NOT EXISTS (SELECT * FROM sys.columns
               WHERE object_id = OBJECT_ID(N'[dbo].[AspNetUsers]')
               AND name = 'ProfilePicturePath')
BEGIN
    ALTER TABLE [dbo].[AspNetUsers]
    ADD [ProfilePicturePath] NVARCHAR(500) NULL;
    PRINT 'Added ProfilePicturePath to AspNetUsers';
END
ELSE
BEGIN
    PRINT 'ProfilePicturePath already exists in AspNetUsers';
END
GO

-- Add ProfilePicturePath to Students table (if not exists)
IF NOT EXISTS (SELECT * FROM sys.columns
               WHERE object_id = OBJECT_ID(N'[dbo].[Students]')
               AND name = 'ProfilePicturePath')
BEGIN
    ALTER TABLE [dbo].[Students]
    ADD [ProfilePicturePath] NVARCHAR(500) NULL;
    PRINT 'Added ProfilePicturePath to Students';
END
ELSE
BEGIN
    PRINT 'ProfilePicturePath already exists in Students';
END
GO

-- Add ProfilePicturePath to Faculties table (if not exists)
IF NOT EXISTS (SELECT * FROM sys.columns
               WHERE object_id = OBJECT_ID(N'[dbo].[Faculties]')
               AND name = 'ProfilePicturePath')
BEGIN
    ALTER TABLE [dbo].[Faculties]
    ADD [ProfilePicturePath] NVARCHAR(500) NULL;
    PRINT 'Added ProfilePicturePath to Faculties';
END
ELSE
BEGIN
    PRINT 'ProfilePicturePath already exists in Faculties';
END
GO

PRINT 'Profile picture columns added successfully!';
GO
