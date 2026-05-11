-- Колонка StatusId в dbo.CarSales (ссылка на Statuses.RowId). Приложение не использует ModerationStatus.
IF COL_LENGTH(N'dbo.CarSales', N'StatusId') IS NULL
BEGIN
    ALTER TABLE dbo.CarSales ADD StatusId uniqueidentifier NULL;
END
GO
