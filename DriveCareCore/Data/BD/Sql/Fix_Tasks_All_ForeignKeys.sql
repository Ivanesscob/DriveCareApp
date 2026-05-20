-- Все связи таблицы Tasks (основные + делегирование + ремонт).
-- Workshop-скрипты не создавали FK_Tasks_Employees / Statuses / Cars / Users.
-- Выполните на DriveCareDB.

SET NOCOUNT ON;
PRINT N'=== Fix_Tasks_All_ForeignKeys ===';

-- Проверка: какие FK у Tasks уже есть
SELECT fk.name AS FkName,
       OBJECT_NAME(fk.parent_object_id) AS FromTable,
       OBJECT_NAME(fk.referenced_object_id) AS ToTable
FROM sys.foreign_keys fk
WHERE OBJECT_NAME(fk.parent_object_id) = N'Tasks'
   OR OBJECT_NAME(fk.referenced_object_id) = N'Tasks'
ORDER BY FromTable, FkName;

PRINT N'--- Очистка битых ссылок в Tasks ---';

IF COL_LENGTH(N'dbo.Tasks', N'EmployeeId') IS NOT NULL
    DELETE t FROM dbo.Tasks t
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Employees e WHERE e.RowId = t.EmployeeId);

IF COL_LENGTH(N'dbo.Tasks', N'StatusId') IS NOT NULL
    DELETE t FROM dbo.Tasks t
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Statuses s WHERE s.RowId = t.StatusId);

IF COL_LENGTH(N'dbo.Tasks', N'CarId') IS NOT NULL
    UPDATE dbo.Tasks SET CarId = NULL
    WHERE CarId IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM dbo.Cars c WHERE c.RowId = CarId);

IF COL_LENGTH(N'dbo.Tasks', N'ClientUserId') IS NOT NULL
    UPDATE dbo.Tasks SET ClientUserId = NULL
    WHERE ClientUserId IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM dbo.Users u WHERE u.RowId = ClientUserId);

IF COL_LENGTH(N'dbo.Tasks', N'RepairHistoryId') IS NOT NULL
    UPDATE dbo.Tasks SET RepairHistoryId = NULL
    WHERE RepairHistoryId IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM dbo.RepairHistory r WHERE r.RowId = RepairHistoryId);

IF COL_LENGTH(N'dbo.Tasks', N'ParentTaskId') IS NOT NULL
    UPDATE dbo.Tasks SET ParentTaskId = NULL
    WHERE ParentTaskId IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM dbo.Tasks p WHERE p.RowId = ParentTaskId);

IF COL_LENGTH(N'dbo.Tasks', N'DelegateTaskId') IS NOT NULL
    UPDATE dbo.Tasks SET DelegateTaskId = NULL
    WHERE DelegateTaskId IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM dbo.Tasks d WHERE d.RowId = DelegateTaskId);

PRINT N'--- Создание FK (если нет) ---';

DECLARE @fks TABLE (FkName sysname, SqlText nvarchar(max));
INSERT INTO @fks VALUES
(N'FK_Tasks_Employees',
 N'ALTER TABLE dbo.Tasks ADD CONSTRAINT FK_Tasks_Employees FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(RowId)'),
(N'FK_Tasks_Statuses',
 N'ALTER TABLE dbo.Tasks ADD CONSTRAINT FK_Tasks_Statuses FOREIGN KEY (StatusId) REFERENCES dbo.Statuses(RowId)'),
(N'FK_Tasks_Cars',
 N'ALTER TABLE dbo.Tasks ADD CONSTRAINT FK_Tasks_Cars FOREIGN KEY (CarId) REFERENCES dbo.Cars(RowId)'),
(N'FK_Tasks_Users',
 N'ALTER TABLE dbo.Tasks ADD CONSTRAINT FK_Tasks_Users FOREIGN KEY (ClientUserId) REFERENCES dbo.Users(RowId)'),
(N'FK_Tasks_RepairHistory',
 N'ALTER TABLE dbo.Tasks ADD CONSTRAINT FK_Tasks_RepairHistory FOREIGN KEY (RepairHistoryId) REFERENCES dbo.RepairHistory(RowId) ON DELETE SET NULL'),
(N'FK_Tasks_ParentTask',
 N'ALTER TABLE dbo.Tasks ADD CONSTRAINT FK_Tasks_ParentTask FOREIGN KEY (ParentTaskId) REFERENCES dbo.Tasks(RowId)'),
(N'FK_Tasks_DelegateTask',
 N'ALTER TABLE dbo.Tasks ADD CONSTRAINT FK_Tasks_DelegateTask FOREIGN KEY (DelegateTaskId) REFERENCES dbo.Tasks(RowId)');

DECLARE @n sysname, @sql nvarchar(max);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT FkName, SqlText FROM @fks;
OPEN c;
FETCH NEXT FROM c INTO @n, @sql;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID(N'dbo.' + @n, N'F') IS NOT NULL
        PRINT N'SKIP: ' + @n;
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
    FETCH NEXT FROM c INTO @n, @sql;
END
CLOSE c; DEALLOCATE c;

PRINT N'--- Итог: FK где Tasks — родитель ---';
SELECT fk.name, OBJECT_NAME(fk.parent_object_id) AS FromTable, OBJECT_NAME(fk.referenced_object_id) AS ToTable
FROM sys.foreign_keys fk
WHERE OBJECT_NAME(fk.parent_object_id) = N'Tasks'
ORDER BY fk.name;
GO
