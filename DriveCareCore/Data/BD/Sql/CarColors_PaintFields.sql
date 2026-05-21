-- Тип покраски и деталь для истории CarColors.
-- PaintKind: 1 = диски, 2 = вся машина, 3 = деталь
IF COL_LENGTH(N'dbo.CarColors', N'PaintKind') IS NULL
    ALTER TABLE dbo.CarColors ADD PaintKind TINYINT NULL;
GO

IF COL_LENGTH(N'dbo.CarColors', N'PartName') IS NULL
    ALTER TABLE dbo.CarColors ADD PartName NVARCHAR(200) NULL;
GO
