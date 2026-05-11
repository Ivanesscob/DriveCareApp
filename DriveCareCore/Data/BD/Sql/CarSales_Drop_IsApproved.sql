-- Опционально: удалить колонку IsApproved, если она осталась от старой схемы (приложение её больше не использует).
IF COL_LENGTH(N'dbo.CarSales', N'IsApproved') IS NOT NULL
BEGIN
    DECLARE @sql nvarchar(max) = N'';
    SELECT @sql += N'ALTER TABLE dbo.CarSales DROP CONSTRAINT ' + QUOTENAME(d.name) + N';' + CHAR(10)
    FROM sys.default_constraints d
    INNER JOIN sys.columns c ON c.default_object_id = d.object_id
    INNER JOIN sys.tables t ON t.object_id = d.parent_object_id
    WHERE t.name = N'CarSales' AND c.name = N'IsApproved' AND SCHEMA_NAME(t.schema_id) = N'dbo';
    IF LEN(@sql) > 0
        EXEC sp_executesql @sql;
    ALTER TABLE dbo.CarSales DROP COLUMN IsApproved;
END
GO
