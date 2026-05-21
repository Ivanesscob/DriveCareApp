-- VIN и госномер для автомобилей в гараже пользователя.
IF COL_LENGTH(N'dbo.Cars', N'Vin') IS NULL
    ALTER TABLE dbo.Cars ADD Vin NVARCHAR(50) NULL;
GO
IF COL_LENGTH(N'dbo.Cars', N'PlateNumber') IS NULL
    ALTER TABLE dbo.Cars ADD PlateNumber NVARCHAR(20) NULL;
GO
