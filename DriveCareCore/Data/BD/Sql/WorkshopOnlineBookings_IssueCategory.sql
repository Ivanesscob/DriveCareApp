-- Категория неисправности для онлайн-записи.
-- Выполнить на DriveCareDB (после WorkshopOnlineBookings_Tables.sql).

IF OBJECT_ID(N'dbo.WorkshopOnlineBookings', N'U') IS NULL
BEGIN
    RAISERROR(N'Сначала создайте dbo.WorkshopOnlineBookings.', 16, 1);
    RETURN;
END
GO

IF COL_LENGTH(N'dbo.WorkshopOnlineBookings', N'IssueCategory') IS NULL
    ALTER TABLE dbo.WorkshopOnlineBookings ADD IssueCategory NVARCHAR(120) NULL;
GO
