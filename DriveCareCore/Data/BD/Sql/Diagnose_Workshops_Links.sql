-- Почему «нет связи» с Workshops: полная диагностика на DriveCareDB.
-- Если здесь Status = OK, связи В БАЗЕ есть. Линии на рисунке SSMS — отдельная настройка.

SET NOCOUNT ON;
PRINT N'База: ' + DB_NAME();
PRINT N'';

-- 1) Есть ли мастерские
PRINT N'=== 1. Workshops (родитель) ===';
SELECT COUNT(*) AS WorkshopCount FROM dbo.Workshops;
SELECT TOP 5 RowId, Name FROM dbo.Workshops;

-- 2) Ожидаемые FK: дочерняя таблица.WorkshopId -> Workshops.RowId
PRINT N'';
PRINT N'=== 2. FK к Workshops (должны быть OK) ===';

DECLARE @expected TABLE (
    ChildTable sysname,
    FkName sysname,
    ColumnName sysname DEFAULT N'WorkshopId'
);

INSERT INTO @expected (ChildTable, FkName) VALUES
(N'WorkshopParts',           N'FK_WorkshopParts_Workshops'),
(N'WorkshopServices',        N'FK_WorkshopServices_Workshops'),
(N'WorkshopServiceUnits',    N'FK_WorkshopServiceUnits_Workshops'),
(N'WorkshopServiceClients',  N'FK_WorkshopServiceClients_Workshops'),
(N'WorkshopGuestCars',       N'FK_WorkshopGuestCars_Workshops'),
(N'Employees',               N'FK_Employees_Workshop');

SELECT
    e.ChildTable,
    e.FkName,
    CASE WHEN fk.name IS NULL THEN N'НЕТ В БАЗЕ' ELSE N'OK в базе' END AS InDatabase,
    CASE WHEN OBJECT_ID(N'dbo.' + e.ChildTable, N'U') IS NULL THEN N'нет таблицы'
         WHEN COL_LENGTH(N'dbo.' + e.ChildTable, N'WorkshopId') IS NULL THEN N'нет колонки WorkshopId'
         ELSE N'колонка есть' END AS ChildSchema
FROM @expected e
LEFT JOIN sys.foreign_keys fk ON fk.name = e.FkName
ORDER BY InDatabase, e.ChildTable;

-- 3) Все FK в базе, где участвует Workshops
PRINT N'';
PRINT N'=== 3. Все FK с Workshops (факт из sys.foreign_keys) ===';
SELECT
    fk.name AS FkName,
    OBJECT_NAME(fk.parent_object_id) AS ChildTable,
    COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS ChildColumn,
    OBJECT_NAME(fk.referenced_object_id) AS ParentTable,
    COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS ParentColumn
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
WHERE OBJECT_NAME(fk.referenced_object_id) = N'Workshops'
   OR OBJECT_NAME(fk.parent_object_id) = N'Workshops'
ORDER BY ChildTable, FkName;

-- 4) Битые WorkshopId (из-за них FK не создаётся)
PRINT N'';
PRINT N'=== 4. Строки с WorkshopId, которого НЕТ в Workshops ===';

IF OBJECT_ID(N'dbo.WorkshopParts', N'U') IS NOT NULL
    SELECT N'WorkshopParts' AS T, COUNT(*) AS BadRows
    FROM dbo.WorkshopParts p
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Workshops w WHERE w.RowId = p.WorkshopId);

IF OBJECT_ID(N'dbo.WorkshopServices', N'U') IS NOT NULL
    SELECT N'WorkshopServices' AS T, COUNT(*) AS BadRows
    FROM dbo.WorkshopServices s
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Workshops w WHERE w.RowId = s.WorkshopId);

IF OBJECT_ID(N'dbo.WorkshopServiceUnits', N'U') IS NOT NULL
    SELECT N'WorkshopServiceUnits' AS T, COUNT(*) AS BadRows
    FROM dbo.WorkshopServiceUnits u
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Workshops w WHERE w.RowId = u.WorkshopId);

IF OBJECT_ID(N'dbo.WorkshopServiceClients', N'U') IS NOT NULL
    SELECT N'WorkshopServiceClients' AS T, COUNT(*) AS BadRows
    FROM dbo.WorkshopServiceClients c
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Workshops w WHERE w.RowId = c.WorkshopId);

IF OBJECT_ID(N'dbo.WorkshopGuestCars', N'U') IS NOT NULL
    SELECT N'WorkshopGuestCars' AS T, COUNT(*) AS BadRows
    FROM dbo.WorkshopGuestCars g
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Workshops w WHERE w.RowId = g.WorkshopId);

PRINT N'';
PRINT N'Если InDatabase = OK, а на Database Diagram линий нет:';
PRINT N'  1) На диаграмму ДОБАВЬТЕ таблицу Workshops (Add Table).';
PRINT N'  2) ПКМ по диаграмме -> Refresh (или пересоздайте диаграмму).';
PRINT N'  3) Линия: от дочерней таблицы (WorkshopParts...) К Workshops, не наоборот.';
GO
