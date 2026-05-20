-- Создать ВСЕ недостающие FK: *.*.WorkshopId -> Workshops.RowId
-- У вас в базе только 3 связи; нужно ещё 4+ (Parts, Services, Clients, GuestCars).
-- Выполните на DriveCareDB. Смотрите «Сообщения».

SET NOCOUNT ON;
PRINT N'=== Fix_All_Workshops_ForeignKeys ===';
PRINT N'База: ' + DB_NAME();

IF OBJECT_ID(N'dbo.Workshops', N'U') IS NULL
BEGIN
    PRINT N'ОШИБКА: нет таблицы Workshops';
    RETURN;
END

DECLARE @w int = (SELECT COUNT(*) FROM dbo.Workshops);
PRINT N'Мастерских в Workshops: ' + CAST(@w AS nvarchar(20));
IF @w = 0
    PRINT N'! Сначала добавьте хотя бы одну строку в Workshops, иначе FK бессмысленны.';

-- Удалить строки с несуществующим WorkshopId
IF OBJECT_ID(N'dbo.WorkshopParts', N'U') IS NOT NULL
    DELETE p FROM dbo.WorkshopParts p
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Workshops w WHERE w.RowId = p.WorkshopId);

IF OBJECT_ID(N'dbo.WorkshopServices', N'U') IS NOT NULL
BEGIN
    UPDATE s SET UnitId = NULL FROM dbo.WorkshopServices s
    INNER JOIN dbo.WorkshopServiceUnits u ON u.RowId = s.UnitId
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Workshops w WHERE w.RowId = u.WorkshopId);
    DELETE s FROM dbo.WorkshopServices s
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Workshops w WHERE w.RowId = s.WorkshopId);
END

IF OBJECT_ID(N'dbo.WorkshopServiceClients', N'U') IS NOT NULL
    DELETE c FROM dbo.WorkshopServiceClients c
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Workshops w WHERE w.RowId = c.WorkshopId);

IF OBJECT_ID(N'dbo.WorkshopGuestCars', N'U') IS NOT NULL
    DELETE g FROM dbo.WorkshopGuestCars g
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Workshops w WHERE w.RowId = g.WorkshopId);

PRINT N'--- Создание FK к Workshops ---';

DECLARE @fks TABLE (FkName sysname, SqlText nvarchar(max), NeedTable sysname);
INSERT INTO @fks VALUES
(N'FK_WorkshopParts_Workshops',
 N'ALTER TABLE dbo.WorkshopParts ADD CONSTRAINT FK_WorkshopParts_Workshops FOREIGN KEY (WorkshopId) REFERENCES dbo.Workshops(RowId) ON DELETE CASCADE',
 N'WorkshopParts'),
(N'FK_WorkshopServices_Workshops',
 N'ALTER TABLE dbo.WorkshopServices ADD CONSTRAINT FK_WorkshopServices_Workshops FOREIGN KEY (WorkshopId) REFERENCES dbo.Workshops(RowId) ON DELETE CASCADE',
 N'WorkshopServices'),
(N'FK_WorkshopServiceUnits_Workshops',
 N'ALTER TABLE dbo.WorkshopServiceUnits ADD CONSTRAINT FK_WorkshopServiceUnits_Workshops FOREIGN KEY (WorkshopId) REFERENCES dbo.Workshops(RowId) ON DELETE CASCADE',
 N'WorkshopServiceUnits'),
(N'FK_WorkshopServiceClients_Workshops',
 N'ALTER TABLE dbo.WorkshopServiceClients ADD CONSTRAINT FK_WorkshopServiceClients_Workshops FOREIGN KEY (WorkshopId) REFERENCES dbo.Workshops(RowId) ON DELETE CASCADE',
 N'WorkshopServiceClients'),
(N'FK_WorkshopGuestCars_Workshops',
 N'ALTER TABLE dbo.WorkshopGuestCars ADD CONSTRAINT FK_WorkshopGuestCars_Workshops FOREIGN KEY (WorkshopId) REFERENCES dbo.Workshops(RowId) ON DELETE CASCADE',
 N'WorkshopGuestCars');

DECLARE @n sysname, @sql nvarchar(max), @t sysname;
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT FkName, SqlText, NeedTable FROM @fks;
OPEN c;
FETCH NEXT FROM c INTO @n, @sql, @t;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID(N'dbo.' + @t, N'U') IS NULL
        PRINT N'SKIP (нет таблицы ' + @t + N'): ' + @n;
    ELSE IF OBJECT_ID(N'dbo.' + @n, N'F') IS NOT NULL
        PRINT N'SKIP (уже есть): ' + @n;
    ELSE
    BEGIN
        BEGIN TRY
            EXEC sp_executesql @sql;
            PRINT N'OK: ' + @n;
        END TRY
        BEGIN CATCH
            PRINT N'FAIL: ' + @n + N' — ' + ERROR_MESSAGE();
        END CATCH
    END
    FETCH NEXT FROM c INTO @n, @sql, @t;
END
CLOSE c; DEALLOCATE c;

PRINT N'';
PRINT N'--- Итог: все FK на Workshops ---';
SELECT fk.name, OBJECT_NAME(fk.parent_object_id) AS ChildTable
FROM sys.foreign_keys fk
WHERE OBJECT_NAME(fk.referenced_object_id) = N'Workshops'
ORDER BY ChildTable;
GO
