-- Проверка: все FK Workshop/Task в базе (не в диаграмме VS).
-- Выполните на DriveCareDB. Должно быть 21 строка со Status = OK.

SET NOCOUNT ON;

DECLARE @expected TABLE (FkName sysname PRIMARY KEY);
INSERT INTO @expected (FkName) VALUES
(N'FK_TaskPartLines_Tasks'),
(N'FK_TaskPartLines_WorkshopParts'),
(N'FK_TaskServiceLines_Tasks'),
(N'FK_TaskServiceLines_WorkshopServices'),
(N'FK_TaskPurchaseRequests_SourceTask'),
(N'FK_TaskPurchaseRequests_PurchaseTask'),
(N'FK_TaskPurchaseRequests_Requester'),
(N'FK_TaskPurchaseRequests_Purchaser'),
(N'FK_TaskPurchaseRequestLines_Request'),
(N'FK_TaskPurchaseRequestLines_WorkshopPart'),
(N'FK_WorkshopParts_Workshops'),
(N'FK_WorkshopServices_Workshops'),
(N'FK_WorkshopServices_Unit'),
(N'FK_WorkshopServiceUnits_Workshops'),
(N'FK_WorkshopServiceClients_Workshops'),
(N'FK_WorkshopServiceClients_Users'),
(N'FK_WorkshopGuestCars_Workshops'),
(N'FK_WorkshopGuestCars_RepairHistory'),
(N'FK_Tasks_ParentTask'),
(N'FK_Tasks_DelegateTask'),
(N'FK_Tasks_RepairHistory');

SELECT
    e.FkName,
    CASE WHEN fk.name IS NULL THEN N'MISSING' ELSE N'OK' END AS Status,
    OBJECT_NAME(fk.parent_object_id) AS [FromTable],
    OBJECT_NAME(fk.referenced_object_id) AS [ToTable]
FROM @expected e
LEFT JOIN sys.foreign_keys fk ON fk.name = e.FkName
ORDER BY Status DESC, e.FkName;

DECLARE @missing int = (
    SELECT COUNT(*) FROM @expected e
    WHERE NOT EXISTS (SELECT 1 FROM sys.foreign_keys fk WHERE fk.name = e.FkName)
);
PRINT N'Итого MISSING: ' + CAST(@missing AS nvarchar(10)) + N' из 21';
GO
