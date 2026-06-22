-- Поля для витрины магазина (DriveCare): цена и категория запчасти.
IF COL_LENGTH(N'dbo.Parts', N'Price') IS NULL
    ALTER TABLE dbo.Parts ADD Price DECIMAL(18, 2) NULL;
GO

IF COL_LENGTH(N'dbo.Parts', N'StoreCategory') IS NULL
    ALTER TABLE dbo.Parts ADD StoreCategory NVARCHAR(40) NULL;
GO

-- Категория из Description «Категория: …»
UPDATE dbo.Parts
SET StoreCategory = CASE
    WHEN Description LIKE N'%Двигатель%' THEN N'Engine'
    WHEN Description LIKE N'%Кузов%' THEN N'Body'
    WHEN Description LIKE N'%Трансмисс%' THEN N'Transmission'
    WHEN Description LIKE N'%Шин%' THEN N'Tires'
    ELSE N'Accessories'
END
WHERE StoreCategory IS NULL OR LTRIM(RTRIM(StoreCategory)) = N'';
GO

-- Цена по артикулу (стабильная, если не задана вручную)
UPDATE dbo.Parts
SET Price = 1200 + (ABS(CHECKSUM(RowId)) % 18000)
WHERE Price IS NULL OR Price <= 0;
GO
