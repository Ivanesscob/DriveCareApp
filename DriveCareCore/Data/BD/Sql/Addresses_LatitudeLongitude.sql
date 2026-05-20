-- Координаты для карты автосервисов (Яндекс). Выполнить на DriveCareDB.
IF COL_LENGTH(N'dbo.Addresses', N'Latitude') IS NULL
    ALTER TABLE dbo.Addresses ADD Latitude FLOAT NULL;

IF COL_LENGTH(N'dbo.Addresses', N'Longitude') IS NULL
    ALTER TABLE dbo.Addresses ADD Longitude FLOAT NULL;

GO
