-- Одна недостающая связь: FK_WorkshopServiceUnits_Workshops
-- Выполните на DriveCareDB. Смотрите вкладку «Сообщения».

SET NOCOUNT ON;
PRINT N'=== Fix FK_WorkshopServiceUnits_Workshops ===';

IF OBJECT_ID(N'dbo.WorkshopServiceUnits', N'U') IS NULL
BEGIN
    PRINT N'Таблица WorkshopServiceUnits не найдена. Сначала WorkshopServiceUnits_Tables.sql';
    RETURN;
END

IF OBJECT_ID(N'dbo.Workshops', N'U') IS NULL
BEGIN
    PRINT N'Таблица Workshops не найдена.';
    RETURN;
END

-- Диагностика
DECLARE @units int = (SELECT COUNT(*) FROM dbo.WorkshopServiceUnits);
DECLARE @workshops int = (SELECT COUNT(*) FROM dbo.Workshops);
DECLARE @orphans int = (
    SELECT COUNT(*) FROM dbo.WorkshopServiceUnits u
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Workshops w WHERE w.RowId = u.WorkshopId)
);

PRINT N'WorkshopServiceUnits: ' + CAST(@units AS nvarchar(20));
PRINT N'Workshops: ' + CAST(@workshops AS nvarchar(20));
PRINT N'Строк с несуществующим WorkshopId: ' + CAST(@orphans AS nvarchar(20));

IF @orphans > 0
BEGIN
    PRINT N'--- Проблемные строки (будут удалены) ---';
    SELECT u.RowId, u.WorkshopId, u.Name, u.CreatedAt
    FROM dbo.WorkshopServiceUnits u
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Workshops w WHERE w.RowId = u.WorkshopId);
END

-- Сброс UnitId у услуг, если единица будет удалена
IF OBJECT_ID(N'dbo.WorkshopServices', N'U') IS NOT NULL AND @orphans > 0
BEGIN
    UPDATE s SET UnitId = NULL
    FROM dbo.WorkshopServices s
    INNER JOIN dbo.WorkshopServiceUnits u ON u.RowId = s.UnitId
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Workshops w WHERE w.RowId = u.WorkshopId);
    PRINT N'WorkshopServices: UnitId обнулён у услуг с «битыми» единицами.';
END

DELETE u FROM dbo.WorkshopServiceUnits u
WHERE NOT EXISTS (SELECT 1 FROM dbo.Workshops w WHERE w.RowId = u.WorkshopId);

IF @@ROWCOUNT > 0
    PRINT N'Удалено строк WorkshopServiceUnits: ' + CAST(@@ROWCOUNT AS nvarchar(20));

-- Создание FK
IF OBJECT_ID(N'dbo.FK_WorkshopServiceUnits_Workshops', N'F') IS NOT NULL
BEGIN
    PRINT N'SKIP: FK уже существует.';
END
ELSE IF @workshops = 0 AND @units > 0
BEGIN
    PRINT N'! В Workshops нет ни одной мастерской, а единицы измерения есть.';
    PRINT N'  Создайте мастерскую или удалите лишние строки в WorkshopServiceUnits.';
END
ELSE
BEGIN
    BEGIN TRY
        ALTER TABLE dbo.WorkshopServiceUnits WITH CHECK
            ADD CONSTRAINT FK_WorkshopServiceUnits_Workshops
            FOREIGN KEY (WorkshopId) REFERENCES dbo.Workshops(RowId) ON DELETE CASCADE;
        PRINT N'OK: FK_WorkshopServiceUnits_Workshops создана.';
    END TRY
    BEGIN CATCH
        PRINT N'FAIL: ' + ERROR_MESSAGE();
        PRINT N'Пришлите текст ошибки — возможны дубликаты или другое ограничение.';
    END CATCH
END

-- Проверка
IF OBJECT_ID(N'dbo.FK_WorkshopServiceUnits_Workshops', N'F') IS NOT NULL
    SELECT N'OK' AS Status, name, OBJECT_NAME(parent_object_id) AS FromTable, OBJECT_NAME(referenced_object_id) AS ToTable
    FROM sys.foreign_keys WHERE name = N'FK_WorkshopServiceUnits_Workshops';
ELSE
    SELECT N'MISSING' AS Status, N'FK_WorkshopServiceUnits_Workshops' AS name;

GO
