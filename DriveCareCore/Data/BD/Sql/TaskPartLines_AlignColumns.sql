-- Приведение TaskPartLines к схеме, которую использует DriveCarePro (UnitName, LineAmount).
-- Выполните, если таблица создана скриптом WorkshopServices_Catalog.sql (колонки Unit, Amount).

IF OBJECT_ID(N'dbo.TaskPartLines', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.TaskPartLines', N'UnitName') IS NULL AND COL_LENGTH(N'dbo.TaskPartLines', N'Unit') IS NOT NULL
        EXEC sp_rename N'dbo.TaskPartLines.Unit', N'UnitName', N'COLUMN';

    IF COL_LENGTH(N'dbo.TaskPartLines', N'LineAmount') IS NULL AND COL_LENGTH(N'dbo.TaskPartLines', N'Amount') IS NOT NULL
        EXEC sp_rename N'dbo.TaskPartLines.Amount', N'LineAmount', N'COLUMN';
END
GO
