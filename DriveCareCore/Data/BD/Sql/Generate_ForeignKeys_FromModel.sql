-- Связи (FK) из Model1.edmx → на сервер DriveCareDB.
-- Выполните целиком в SSMS. В окне «Сообщения» будет OK / FAIL / SKIP по каждой связи.
-- Если FAIL — чаще всего «битые» RowId (см. блок очистки ниже).

SET NOCOUNT ON;
PRINT N'=== Generate_ForeignKeys_FromModel (из EDMX) ===';
GO

SET NOCOUNT ON;

-- ---------- Проверка: таблицы и колонки ----------
PRINT N'--- Проверка схемы ---';

IF OBJECT_ID(N'dbo.Tasks', N'U') IS NULL PRINT N'  ! Нет таблицы dbo.Tasks';
IF OBJECT_ID(N'dbo.TaskPartLines', N'U') IS NULL PRINT N'  ! Нет таблицы dbo.TaskPartLines';
IF OBJECT_ID(N'dbo.TaskServiceLines', N'U') IS NULL PRINT N'  ! Нет таблицы dbo.TaskServiceLines';
IF OBJECT_ID(N'dbo.TaskPurchaseRequests', N'U') IS NULL PRINT N'  ! Нет таблицы dbo.TaskPurchaseRequests';
IF OBJECT_ID(N'dbo.TaskPurchaseRequestLines', N'U') IS NULL PRINT N'  ! Нет таблицы dbo.TaskPurchaseRequestLines';
IF OBJECT_ID(N'dbo.WorkshopParts', N'U') IS NULL PRINT N'  ! Нет таблицы dbo.WorkshopParts';
IF OBJECT_ID(N'dbo.WorkshopServices', N'U') IS NULL PRINT N'  ! Нет таблицы dbo.WorkshopServices';
IF OBJECT_ID(N'dbo.WorkshopServiceUnits', N'U') IS NULL PRINT N'  ! Нет таблицы dbo.WorkshopServiceUnits';
IF OBJECT_ID(N'dbo.Workshops', N'U') IS NULL PRINT N'  ! Нет таблицы dbo.Workshops';

IF OBJECT_ID(N'dbo.TaskPartLines', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.TaskPartLines', N'WorkshopPartId') IS NULL
    PRINT N'  ! TaskPartLines: нет колонки WorkshopPartId — выполните WorkshopParts_Tables.sql';

IF OBJECT_ID(N'dbo.Tasks', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.Tasks', N'ParentTaskId') IS NULL
    PRINT N'  ! Tasks: нет ParentTaskId — выполните Tasks_Add_DelegationFields.sql';

PRINT N'--- Очистка битых ссылок (если FK не создаются) ---';

IF OBJECT_ID(N'dbo.TaskPartLines', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Tasks', N'U') IS NOT NULL
BEGIN
    DELETE l FROM dbo.TaskPartLines l
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Tasks t WHERE t.RowId = l.TaskId);
END

IF OBJECT_ID(N'dbo.TaskServiceLines', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Tasks', N'U') IS NOT NULL
BEGIN
    DELETE l FROM dbo.TaskServiceLines l
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Tasks t WHERE t.RowId = l.TaskId);
END

IF OBJECT_ID(N'dbo.TaskPartLines', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.WorkshopParts', N'U') IS NOT NULL
BEGIN
    UPDATE dbo.TaskPartLines SET WorkshopPartId = NULL
    WHERE WorkshopPartId IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM dbo.WorkshopParts p WHERE p.RowId = WorkshopPartId);
END

IF OBJECT_ID(N'dbo.TaskServiceLines', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.WorkshopServices', N'U') IS NOT NULL
BEGIN
    UPDATE dbo.TaskServiceLines SET WorkshopServiceId = NULL
    WHERE WorkshopServiceId IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM dbo.WorkshopServices s WHERE s.RowId = WorkshopServiceId);
END

IF OBJECT_ID(N'dbo.WorkshopParts', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Workshops', N'U') IS NOT NULL
BEGIN
    DELETE p FROM dbo.WorkshopParts p
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Workshops w WHERE w.RowId = p.WorkshopId);
END

IF OBJECT_ID(N'dbo.WorkshopServices', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Workshops', N'U') IS NOT NULL
BEGIN
    DELETE s FROM dbo.WorkshopServices s
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Workshops w WHERE w.RowId = s.WorkshopId);
END

IF OBJECT_ID(N'dbo.TaskPurchaseRequestLines', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.WorkshopParts', N'U') IS NOT NULL
BEGIN
    UPDATE dbo.TaskPurchaseRequestLines SET WorkshopPartId = NULL
    WHERE WorkshopPartId IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM dbo.WorkshopParts p WHERE p.RowId = WorkshopPartId);
END

IF OBJECT_ID(N'dbo.WorkshopServiceUnits', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Workshops', N'U') IS NOT NULL
BEGIN
    DELETE u FROM dbo.WorkshopServiceUnits u
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Workshops w WHERE w.RowId = u.WorkshopId);
END

-- UnitId в услугах: сброс, если единица удалена
IF OBJECT_ID(N'dbo.WorkshopServices', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.WorkshopServiceUnits', N'U') IS NOT NULL
BEGIN
    UPDATE dbo.WorkshopServices SET UnitId = NULL
    WHERE UnitId IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM dbo.WorkshopServiceUnits u WHERE u.RowId = UnitId);
END

PRINT N'--- Создание FK ---';

DECLARE @fks TABLE (
    FkName sysname NOT NULL,
    SqlText nvarchar(max) NOT NULL,
    RequiresTable sysname NULL
);

INSERT INTO @fks (FkName, SqlText, RequiresTable) VALUES
(N'FK_TaskPartLines_Tasks',
 N'ALTER TABLE dbo.TaskPartLines ADD CONSTRAINT FK_TaskPartLines_Tasks FOREIGN KEY (TaskId) REFERENCES dbo.Tasks(RowId) ON DELETE CASCADE',
 N'TaskPartLines'),
(N'FK_TaskPartLines_WorkshopParts',
 N'ALTER TABLE dbo.TaskPartLines ADD CONSTRAINT FK_TaskPartLines_WorkshopParts FOREIGN KEY (WorkshopPartId) REFERENCES dbo.WorkshopParts(RowId) ON DELETE SET NULL',
 N'TaskPartLines'),
(N'FK_TaskServiceLines_Tasks',
 N'ALTER TABLE dbo.TaskServiceLines ADD CONSTRAINT FK_TaskServiceLines_Tasks FOREIGN KEY (TaskId) REFERENCES dbo.Tasks(RowId) ON DELETE CASCADE',
 N'TaskServiceLines'),
(N'FK_TaskServiceLines_WorkshopServices',
 N'ALTER TABLE dbo.TaskServiceLines ADD CONSTRAINT FK_TaskServiceLines_WorkshopServices FOREIGN KEY (WorkshopServiceId) REFERENCES dbo.WorkshopServices(RowId) ON DELETE SET NULL',
 N'TaskServiceLines'),
(N'FK_TaskPurchaseRequests_SourceTask',
 N'ALTER TABLE dbo.TaskPurchaseRequests ADD CONSTRAINT FK_TaskPurchaseRequests_SourceTask FOREIGN KEY (SourceTaskId) REFERENCES dbo.Tasks(RowId)',
 N'TaskPurchaseRequests'),
(N'FK_TaskPurchaseRequests_PurchaseTask',
 N'ALTER TABLE dbo.TaskPurchaseRequests ADD CONSTRAINT FK_TaskPurchaseRequests_PurchaseTask FOREIGN KEY (PurchaseTaskId) REFERENCES dbo.Tasks(RowId)',
 N'TaskPurchaseRequests'),
(N'FK_TaskPurchaseRequests_Requester',
 N'ALTER TABLE dbo.TaskPurchaseRequests ADD CONSTRAINT FK_TaskPurchaseRequests_Requester FOREIGN KEY (RequestedByEmployeeId) REFERENCES dbo.Employees(RowId)',
 N'TaskPurchaseRequests'),
(N'FK_TaskPurchaseRequests_Purchaser',
 N'ALTER TABLE dbo.TaskPurchaseRequests ADD CONSTRAINT FK_TaskPurchaseRequests_Purchaser FOREIGN KEY (PurchaserEmployeeId) REFERENCES dbo.Employees(RowId)',
 N'TaskPurchaseRequests'),
(N'FK_TaskPurchaseRequestLines_Request',
 N'ALTER TABLE dbo.TaskPurchaseRequestLines ADD CONSTRAINT FK_TaskPurchaseRequestLines_Request FOREIGN KEY (RequestId) REFERENCES dbo.TaskPurchaseRequests(RowId) ON DELETE CASCADE',
 N'TaskPurchaseRequestLines'),
(N'FK_TaskPurchaseRequestLines_WorkshopPart',
 N'ALTER TABLE dbo.TaskPurchaseRequestLines ADD CONSTRAINT FK_TaskPurchaseRequestLines_WorkshopPart FOREIGN KEY (WorkshopPartId) REFERENCES dbo.WorkshopParts(RowId) ON DELETE SET NULL',
 N'TaskPurchaseRequestLines'),
(N'FK_WorkshopParts_Workshops',
 N'ALTER TABLE dbo.WorkshopParts ADD CONSTRAINT FK_WorkshopParts_Workshops FOREIGN KEY (WorkshopId) REFERENCES dbo.Workshops(RowId) ON DELETE CASCADE',
 N'WorkshopParts'),
(N'FK_WorkshopServices_Workshops',
 N'ALTER TABLE dbo.WorkshopServices ADD CONSTRAINT FK_WorkshopServices_Workshops FOREIGN KEY (WorkshopId) REFERENCES dbo.Workshops(RowId) ON DELETE CASCADE',
 N'WorkshopServices'),
(N'FK_WorkshopServices_Unit',
 N'ALTER TABLE dbo.WorkshopServices ADD CONSTRAINT FK_WorkshopServices_Unit FOREIGN KEY (UnitId) REFERENCES dbo.WorkshopServiceUnits(RowId) ON DELETE SET NULL',
 N'WorkshopServices'),
(N'FK_WorkshopServiceUnits_Workshops',
 N'ALTER TABLE dbo.WorkshopServiceUnits ADD CONSTRAINT FK_WorkshopServiceUnits_Workshops FOREIGN KEY (WorkshopId) REFERENCES dbo.Workshops(RowId) ON DELETE CASCADE',
 N'WorkshopServiceUnits'),
(N'FK_WorkshopServiceClients_Workshops',
 N'ALTER TABLE dbo.WorkshopServiceClients ADD CONSTRAINT FK_WorkshopServiceClients_Workshops FOREIGN KEY (WorkshopId) REFERENCES dbo.Workshops(RowId) ON DELETE CASCADE',
 N'WorkshopServiceClients'),
(N'FK_WorkshopServiceClients_Users',
 N'ALTER TABLE dbo.WorkshopServiceClients ADD CONSTRAINT FK_WorkshopServiceClients_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(RowId) ON DELETE SET NULL',
 N'WorkshopServiceClients'),
(N'FK_WorkshopGuestCars_Workshops',
 N'ALTER TABLE dbo.WorkshopGuestCars ADD CONSTRAINT FK_WorkshopGuestCars_Workshops FOREIGN KEY (WorkshopId) REFERENCES dbo.Workshops(RowId) ON DELETE CASCADE',
 N'WorkshopGuestCars'),
(N'FK_WorkshopGuestCars_RepairHistory',
 N'ALTER TABLE dbo.WorkshopGuestCars ADD CONSTRAINT FK_WorkshopGuestCars_RepairHistory FOREIGN KEY (RepairHistoryId) REFERENCES dbo.RepairHistory(RowId) ON DELETE SET NULL',
 N'WorkshopGuestCars'),
(N'FK_Tasks_ParentTask',
 N'ALTER TABLE dbo.Tasks ADD CONSTRAINT FK_Tasks_ParentTask FOREIGN KEY (ParentTaskId) REFERENCES dbo.Tasks(RowId)',
 N'Tasks'),
(N'FK_Tasks_DelegateTask',
 N'ALTER TABLE dbo.Tasks ADD CONSTRAINT FK_Tasks_DelegateTask FOREIGN KEY (DelegateTaskId) REFERENCES dbo.Tasks(RowId)',
 N'Tasks'),
(N'FK_Tasks_RepairHistory',
 N'ALTER TABLE dbo.Tasks ADD CONSTRAINT FK_Tasks_RepairHistory FOREIGN KEY (RepairHistoryId) REFERENCES dbo.RepairHistory(RowId) ON DELETE SET NULL',
 N'Tasks');

DECLARE @fkName sysname, @sql nvarchar(max), @req sysname;
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT FkName, SqlText, RequiresTable FROM @fks;
OPEN c;
FETCH NEXT FROM c INTO @fkName, @sql, @req;

WHILE @@FETCH_STATUS = 0
BEGIN
    IF @req IS NOT NULL AND OBJECT_ID(N'dbo.' + @req, N'U') IS NULL
        PRINT N'SKIP (нет таблицы ' + @req + N'): ' + @fkName;
    ELSE IF OBJECT_ID(N'dbo.' + @fkName, N'F') IS NOT NULL
        PRINT N'SKIP (уже есть): ' + @fkName;
    ELSE
    BEGIN
        BEGIN TRY
            EXEC sp_executesql @sql;
            PRINT N'OK: ' + @fkName;
        END TRY
        BEGIN CATCH
            PRINT N'FAIL: ' + @fkName + N' — ' + ERROR_MESSAGE();
        END CATCH
    END

    FETCH NEXT FROM c INTO @fkName, @sql, @req;
END

CLOSE c;
DEALLOCATE c;

PRINT N'--- Готово. Проверка в БД: ---';
PRINT N'SELECT name, OBJECT_NAME(parent_object_id) AS [Table] FROM sys.foreign_keys WHERE name LIKE ''FK_Task%'' OR name LIKE ''FK_Workshop%'' ORDER BY name;';
GO
