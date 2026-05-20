-- Связи (FK) для закупок, отчёта по заданию, склада и сервиса.
-- Выполните на DriveCareDB после создания таблиц (WorkshopServices_Tables.sql, TaskPurchaseRequests_Tables.sql, WorkshopParts_Tables.sql и т.д.).
-- Затем в Visual Studio: Update Model from Database для Model1.edmx.

-- TaskPartLines
IF OBJECT_ID(N'dbo.TaskPartLines', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.FK_TaskPartLines_Tasks', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.TaskPartLines WITH CHECK
        ADD CONSTRAINT FK_TaskPartLines_Tasks FOREIGN KEY (TaskId) REFERENCES dbo.Tasks(RowId) ON DELETE CASCADE;
END
GO

IF OBJECT_ID(N'dbo.TaskPartLines', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.TaskPartLines', N'WorkshopPartId') IS NOT NULL
   AND OBJECT_ID(N'dbo.WorkshopParts', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.FK_TaskPartLines_WorkshopParts', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.TaskPartLines WITH CHECK
        ADD CONSTRAINT FK_TaskPartLines_WorkshopParts FOREIGN KEY (WorkshopPartId) REFERENCES dbo.WorkshopParts(RowId) ON DELETE SET NULL;
END
GO

-- TaskServiceLines
IF OBJECT_ID(N'dbo.TaskServiceLines', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.FK_TaskServiceLines_Tasks', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.TaskServiceLines WITH CHECK
        ADD CONSTRAINT FK_TaskServiceLines_Tasks FOREIGN KEY (TaskId) REFERENCES dbo.Tasks(RowId) ON DELETE CASCADE;
END
GO

IF OBJECT_ID(N'dbo.TaskServiceLines', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.TaskServiceLines', N'WorkshopServiceId') IS NOT NULL
   AND OBJECT_ID(N'dbo.WorkshopServices', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.FK_TaskServiceLines_WorkshopServices', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.TaskServiceLines WITH CHECK
        ADD CONSTRAINT FK_TaskServiceLines_WorkshopServices FOREIGN KEY (WorkshopServiceId) REFERENCES dbo.WorkshopServices(RowId) ON DELETE SET NULL;
END
GO

-- TaskPurchaseRequests
IF OBJECT_ID(N'dbo.TaskPurchaseRequests', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.FK_TaskPurchaseRequests_SourceTask', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.TaskPurchaseRequests WITH CHECK
        ADD CONSTRAINT FK_TaskPurchaseRequests_SourceTask FOREIGN KEY (SourceTaskId) REFERENCES dbo.Tasks(RowId);
END
GO

IF OBJECT_ID(N'dbo.TaskPurchaseRequests', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.FK_TaskPurchaseRequests_PurchaseTask', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.TaskPurchaseRequests WITH CHECK
        ADD CONSTRAINT FK_TaskPurchaseRequests_PurchaseTask FOREIGN KEY (PurchaseTaskId) REFERENCES dbo.Tasks(RowId);
END
GO

IF OBJECT_ID(N'dbo.TaskPurchaseRequests', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.FK_TaskPurchaseRequests_Requester', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.TaskPurchaseRequests WITH CHECK
        ADD CONSTRAINT FK_TaskPurchaseRequests_Requester FOREIGN KEY (RequestedByEmployeeId) REFERENCES dbo.Employees(RowId);
END
GO

IF OBJECT_ID(N'dbo.TaskPurchaseRequests', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.FK_TaskPurchaseRequests_Purchaser', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.TaskPurchaseRequests WITH CHECK
        ADD CONSTRAINT FK_TaskPurchaseRequests_Purchaser FOREIGN KEY (PurchaserEmployeeId) REFERENCES dbo.Employees(RowId);
END
GO

-- TaskPurchaseRequestLines
IF OBJECT_ID(N'dbo.TaskPurchaseRequestLines', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.FK_TaskPurchaseRequestLines_Request', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.TaskPurchaseRequestLines WITH CHECK
        ADD CONSTRAINT FK_TaskPurchaseRequestLines_Request FOREIGN KEY (RequestId) REFERENCES dbo.TaskPurchaseRequests(RowId) ON DELETE CASCADE;
END
GO

IF OBJECT_ID(N'dbo.TaskPurchaseRequestLines', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.TaskPurchaseRequestLines', N'WorkshopPartId') IS NOT NULL
   AND OBJECT_ID(N'dbo.WorkshopParts', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.FK_TaskPurchaseRequestLines_WorkshopPart', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.TaskPurchaseRequestLines WITH CHECK
        ADD CONSTRAINT FK_TaskPurchaseRequestLines_WorkshopPart FOREIGN KEY (WorkshopPartId) REFERENCES dbo.WorkshopParts(RowId) ON DELETE SET NULL;
END
GO

-- WorkshopParts
IF OBJECT_ID(N'dbo.WorkshopParts', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.FK_WorkshopParts_Workshops', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.WorkshopParts WITH CHECK
        ADD CONSTRAINT FK_WorkshopParts_Workshops FOREIGN KEY (WorkshopId) REFERENCES dbo.Workshops(RowId) ON DELETE CASCADE;
END
GO

-- WorkshopServices
IF OBJECT_ID(N'dbo.WorkshopServices', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.FK_WorkshopServices_Workshops', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.WorkshopServices WITH CHECK
        ADD CONSTRAINT FK_WorkshopServices_Workshops FOREIGN KEY (WorkshopId) REFERENCES dbo.Workshops(RowId) ON DELETE CASCADE;
END
GO

IF OBJECT_ID(N'dbo.WorkshopServices', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.WorkshopServices', N'UnitId') IS NOT NULL
   AND OBJECT_ID(N'dbo.WorkshopServiceUnits', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.FK_WorkshopServices_Unit', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.WorkshopServices WITH CHECK
        ADD CONSTRAINT FK_WorkshopServices_Unit FOREIGN KEY (UnitId) REFERENCES dbo.WorkshopServiceUnits(RowId) ON DELETE SET NULL;
END
GO

-- WorkshopServiceUnits
IF OBJECT_ID(N'dbo.WorkshopServiceUnits', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.FK_WorkshopServiceUnits_Workshops', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.WorkshopServiceUnits WITH CHECK
        ADD CONSTRAINT FK_WorkshopServiceUnits_Workshops FOREIGN KEY (WorkshopId) REFERENCES dbo.Workshops(RowId) ON DELETE CASCADE;
END
GO

-- WorkshopServiceClients
IF OBJECT_ID(N'dbo.WorkshopServiceClients', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.FK_WorkshopServiceClients_Workshops', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.WorkshopServiceClients WITH CHECK
        ADD CONSTRAINT FK_WorkshopServiceClients_Workshops FOREIGN KEY (WorkshopId) REFERENCES dbo.Workshops(RowId) ON DELETE CASCADE;
END
GO

IF OBJECT_ID(N'dbo.WorkshopServiceClients', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.WorkshopServiceClients', N'UserId') IS NOT NULL
   AND OBJECT_ID(N'dbo.FK_WorkshopServiceClients_Users', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.WorkshopServiceClients WITH CHECK
        ADD CONSTRAINT FK_WorkshopServiceClients_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(RowId) ON DELETE SET NULL;
END
GO

-- WorkshopGuestCars
IF OBJECT_ID(N'dbo.WorkshopGuestCars', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.FK_WorkshopGuestCars_Workshops', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.WorkshopGuestCars WITH CHECK
        ADD CONSTRAINT FK_WorkshopGuestCars_Workshops FOREIGN KEY (WorkshopId) REFERENCES dbo.Workshops(RowId) ON DELETE CASCADE;
END
GO

IF OBJECT_ID(N'dbo.WorkshopGuestCars', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.WorkshopGuestCars', N'RepairHistoryId') IS NOT NULL
   AND OBJECT_ID(N'dbo.FK_WorkshopGuestCars_RepairHistory', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.WorkshopGuestCars WITH CHECK
        ADD CONSTRAINT FK_WorkshopGuestCars_RepairHistory FOREIGN KEY (RepairHistoryId) REFERENCES dbo.RepairHistory(RowId) ON DELETE SET NULL;
END
GO

-- Tasks — делегирование и ремонт
IF COL_LENGTH(N'dbo.Tasks', N'ParentTaskId') IS NOT NULL
   AND OBJECT_ID(N'dbo.FK_Tasks_ParentTask', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.Tasks WITH CHECK
        ADD CONSTRAINT FK_Tasks_ParentTask FOREIGN KEY (ParentTaskId) REFERENCES dbo.Tasks(RowId);
END
GO

IF COL_LENGTH(N'dbo.Tasks', N'DelegateTaskId') IS NOT NULL
   AND OBJECT_ID(N'dbo.FK_Tasks_DelegateTask', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.Tasks WITH CHECK
        ADD CONSTRAINT FK_Tasks_DelegateTask FOREIGN KEY (DelegateTaskId) REFERENCES dbo.Tasks(RowId);
END
GO

IF COL_LENGTH(N'dbo.Tasks', N'RepairHistoryId') IS NOT NULL
   AND OBJECT_ID(N'dbo.FK_Tasks_RepairHistory', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.Tasks WITH CHECK
        ADD CONSTRAINT FK_Tasks_RepairHistory FOREIGN KEY (RepairHistoryId) REFERENCES dbo.RepairHistory(RowId) ON DELETE SET NULL;
END
GO
