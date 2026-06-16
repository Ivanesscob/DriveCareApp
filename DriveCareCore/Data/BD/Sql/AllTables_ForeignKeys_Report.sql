-- Внешние ключи для всех таблиц DriveCare (для отчёта и целостности БД).
-- Выполните на DriveCareDB в SSMS после основных скриптов создания таблиц.
-- Безопасно повторять: существующие FK пропускаются (SKIP).

SET NOCOUNT ON;
PRINT N'=== AllTables_ForeignKeys_Report ===';
GO

SET NOCOUNT ON;

DECLARE @fks TABLE (
    FkName sysname NOT NULL,
    SqlText nvarchar(max) NOT NULL,
    RequiresTable sysname NULL
);

INSERT INTO @fks (FkName, SqlText, RequiresTable) VALUES
-- Уже могут быть из EDMX / Generate_ForeignKeys_FromModel — дубли пропустятся
(N'FK_CarSalePrices_CarSales',
 N'ALTER TABLE dbo.CarSalePrices ADD CONSTRAINT FK_CarSalePrices_CarSales FOREIGN KEY (CarSaleId) REFERENCES dbo.CarSales(RowId) ON DELETE CASCADE',
 N'CarSalePrices'),
(N'FK_EmployeeNotifications_Employee',
 N'ALTER TABLE dbo.EmployeeNotifications ADD CONSTRAINT FK_EmployeeNotifications_Employee FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(RowId) ON DELETE CASCADE',
 N'EmployeeNotifications'),
(N'FK_EmployeeNotifications_Notification',
 N'ALTER TABLE dbo.EmployeeNotifications ADD CONSTRAINT FK_EmployeeNotifications_Notification FOREIGN KEY (NotificationId) REFERENCES dbo.Notifications(RowId) ON DELETE CASCADE',
 N'EmployeeNotifications'),
(N'FK_EmployeeNotifications_Task',
 N'ALTER TABLE dbo.EmployeeNotifications ADD CONSTRAINT FK_EmployeeNotifications_Task FOREIGN KEY (TaskId) REFERENCES dbo.Tasks(RowId) ON DELETE SET NULL',
 N'EmployeeNotifications'),
(N'FK_StoreOrders_Users',
 N'ALTER TABLE dbo.StoreOrders ADD CONSTRAINT FK_StoreOrders_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(RowId)',
 N'StoreOrders'),
(N'FK_StoreOrders_PickupPoint',
 N'ALTER TABLE dbo.StoreOrders ADD CONSTRAINT FK_StoreOrders_PickupPoint FOREIGN KEY (PickupPointId) REFERENCES dbo.OrderPickupPoints(RowId)',
 N'StoreOrders'),
(N'FK_StoreOrderLines_Order',
 N'ALTER TABLE dbo.StoreOrderLines ADD CONSTRAINT FK_StoreOrderLines_Order FOREIGN KEY (OrderId) REFERENCES dbo.StoreOrders(RowId) ON DELETE CASCADE',
 N'StoreOrderLines'),
(N'FK_StoreOrderLines_Parts',
 N'ALTER TABLE dbo.StoreOrderLines ADD CONSTRAINT FK_StoreOrderLines_Parts FOREIGN KEY (ProductId) REFERENCES dbo.Parts(RowId)',
 N'StoreOrderLines'),
(N'FK_UserCarComponentStatuses_UserCar',
 N'ALTER TABLE dbo.UserCarComponentStatuses ADD CONSTRAINT FK_UserCarComponentStatuses_UserCar FOREIGN KEY (UserCarRowId) REFERENCES dbo.UserCars(RowId) ON DELETE CASCADE',
 N'UserCarComponentStatuses'),
(N'FK_UserCarMaintenanceHistory_UserCar',
 N'ALTER TABLE dbo.UserCarMaintenanceHistory ADD CONSTRAINT FK_UserCarMaintenanceHistory_UserCar FOREIGN KEY (UserCarRowId) REFERENCES dbo.UserCars(RowId) ON DELETE CASCADE',
 N'UserCarMaintenanceHistory'),
(N'FK_WorkshopConversations_Workshop',
 N'ALTER TABLE dbo.WorkshopConversations ADD CONSTRAINT FK_WorkshopConversations_Workshop FOREIGN KEY (WorkshopId) REFERENCES dbo.Workshops(RowId) ON DELETE CASCADE',
 N'WorkshopConversations'),
(N'FK_WorkshopConversations_User',
 N'ALTER TABLE dbo.WorkshopConversations ADD CONSTRAINT FK_WorkshopConversations_User FOREIGN KEY (UserId) REFERENCES dbo.Users(RowId)',
 N'WorkshopConversations'),
(N'FK_WorkshopConversations_Client',
 N'ALTER TABLE dbo.WorkshopConversations ADD CONSTRAINT FK_WorkshopConversations_Client FOREIGN KEY (WorkshopServiceClientId) REFERENCES dbo.WorkshopServiceClients(RowId) ON DELETE SET NULL',
 N'WorkshopConversations'),
(N'FK_WorkshopMessages_Conversation',
 N'ALTER TABLE dbo.WorkshopMessages ADD CONSTRAINT FK_WorkshopMessages_Conversation FOREIGN KEY (ConversationId) REFERENCES dbo.WorkshopConversations(RowId) ON DELETE CASCADE',
 N'WorkshopMessages'),
(N'FK_WorkshopMessages_SenderUser',
 N'ALTER TABLE dbo.WorkshopMessages ADD CONSTRAINT FK_WorkshopMessages_SenderUser FOREIGN KEY (SenderUserId) REFERENCES dbo.Users(RowId) ON DELETE SET NULL',
 N'WorkshopMessages'),
(N'FK_WorkshopMessages_SenderEmployee',
 N'ALTER TABLE dbo.WorkshopMessages ADD CONSTRAINT FK_WorkshopMessages_SenderEmployee FOREIGN KEY (SenderEmployeeId) REFERENCES dbo.Employees(RowId) ON DELETE SET NULL',
 N'WorkshopMessages'),
(N'FK_WorkshopPaintServices_Workshop',
 N'ALTER TABLE dbo.WorkshopPaintServices ADD CONSTRAINT FK_WorkshopPaintServices_Workshop FOREIGN KEY (WorkshopId) REFERENCES dbo.Workshops(RowId) ON DELETE CASCADE',
 N'WorkshopPaintServices'),
(N'FK_WorkshopPaintColors_Workshop',
 N'ALTER TABLE dbo.WorkshopPaintColors ADD CONSTRAINT FK_WorkshopPaintColors_Workshop FOREIGN KEY (WorkshopId) REFERENCES dbo.Workshops(RowId) ON DELETE CASCADE',
 N'WorkshopPaintColors'),
(N'FK_WorkshopPaintColors_Color',
 N'ALTER TABLE dbo.WorkshopPaintColors ADD CONSTRAINT FK_WorkshopPaintColors_Color FOREIGN KEY (ColorId) REFERENCES dbo.Colors(RowId) ON DELETE SET NULL',
 N'WorkshopPaintColors'),
(N'FK_UserWorkshopPaintInquiries_User',
 N'ALTER TABLE dbo.UserWorkshopPaintInquiries ADD CONSTRAINT FK_UserWorkshopPaintInquiries_User FOREIGN KEY (UserId) REFERENCES dbo.Users(RowId)',
 N'UserWorkshopPaintInquiries'),
(N'FK_UserWorkshopPaintInquiries_UserCar',
 N'ALTER TABLE dbo.UserWorkshopPaintInquiries ADD CONSTRAINT FK_UserWorkshopPaintInquiries_UserCar FOREIGN KEY (UserCarId) REFERENCES dbo.UserCars(RowId)',
 N'UserWorkshopPaintInquiries'),
(N'FK_UserWorkshopPaintInquiries_Car',
 N'ALTER TABLE dbo.UserWorkshopPaintInquiries ADD CONSTRAINT FK_UserWorkshopPaintInquiries_Car FOREIGN KEY (CarId) REFERENCES dbo.Cars(RowId)',
 N'UserWorkshopPaintInquiries'),
(N'FK_UserWorkshopPaintInquiries_Workshop',
 N'ALTER TABLE dbo.UserWorkshopPaintInquiries ADD CONSTRAINT FK_UserWorkshopPaintInquiries_Workshop FOREIGN KEY (WorkshopId) REFERENCES dbo.Workshops(RowId)',
 N'UserWorkshopPaintInquiries'),
(N'FK_UserWorkshopPaintInquiries_PaintService',
 N'ALTER TABLE dbo.UserWorkshopPaintInquiries ADD CONSTRAINT FK_UserWorkshopPaintInquiries_PaintService FOREIGN KEY (WorkshopPaintServiceId) REFERENCES dbo.WorkshopPaintServices(RowId) ON DELETE SET NULL',
 N'UserWorkshopPaintInquiries'),
(N'FK_UserWorkshopPaintInquiries_Color',
 N'ALTER TABLE dbo.UserWorkshopPaintInquiries ADD CONSTRAINT FK_UserWorkshopPaintInquiries_Color FOREIGN KEY (ColorId) REFERENCES dbo.Colors(RowId) ON DELETE SET NULL',
 N'UserWorkshopPaintInquiries'),
(N'FK_ServiceDocuments_RootTask',
 N'ALTER TABLE dbo.ServiceDocuments ADD CONSTRAINT FK_ServiceDocuments_RootTask FOREIGN KEY (RootTaskId) REFERENCES dbo.Tasks(RowId)',
 N'ServiceDocuments'),
(N'FK_ServiceDocuments_Workshop',
 N'ALTER TABLE dbo.ServiceDocuments ADD CONSTRAINT FK_ServiceDocuments_Workshop FOREIGN KEY (WorkshopId) REFERENCES dbo.Workshops(RowId)',
 N'ServiceDocuments'),
(N'FK_ServiceDocuments_Car',
 N'ALTER TABLE dbo.ServiceDocuments ADD CONSTRAINT FK_ServiceDocuments_Car FOREIGN KEY (CarId) REFERENCES dbo.Cars(RowId) ON DELETE SET NULL',
 N'ServiceDocuments'),
(N'FK_ServiceDocuments_ClientUser',
 N'ALTER TABLE dbo.ServiceDocuments ADD CONSTRAINT FK_ServiceDocuments_ClientUser FOREIGN KEY (ClientUserId) REFERENCES dbo.Users(RowId) ON DELETE SET NULL',
 N'ServiceDocuments'),
(N'FK_ServiceDocuments_RepairHistory',
 N'ALTER TABLE dbo.ServiceDocuments ADD CONSTRAINT FK_ServiceDocuments_RepairHistory FOREIGN KEY (RepairHistoryId) REFERENCES dbo.RepairHistory(RowId) ON DELETE SET NULL',
 N'ServiceDocuments'),
(N'FK_ServiceDocumentPartLines_Document',
 N'ALTER TABLE dbo.ServiceDocumentPartLines ADD CONSTRAINT FK_ServiceDocumentPartLines_Document FOREIGN KEY (DocumentId) REFERENCES dbo.ServiceDocuments(RowId) ON DELETE CASCADE',
 N'ServiceDocumentPartLines'),
(N'FK_ServiceDocumentPartLines_WorkshopPart',
 N'ALTER TABLE dbo.ServiceDocumentPartLines ADD CONSTRAINT FK_ServiceDocumentPartLines_WorkshopPart FOREIGN KEY (WorkshopPartId) REFERENCES dbo.WorkshopParts(RowId) ON DELETE SET NULL',
 N'ServiceDocumentPartLines'),
(N'FK_ServiceDocumentServiceLines_Document',
 N'ALTER TABLE dbo.ServiceDocumentServiceLines ADD CONSTRAINT FK_ServiceDocumentServiceLines_Document FOREIGN KEY (DocumentId) REFERENCES dbo.ServiceDocuments(RowId) ON DELETE CASCADE',
 N'ServiceDocumentServiceLines'),
(N'FK_ServiceDocumentServiceLines_WorkshopService',
 N'ALTER TABLE dbo.ServiceDocumentServiceLines ADD CONSTRAINT FK_ServiceDocumentServiceLines_WorkshopService FOREIGN KEY (WorkshopServiceId) REFERENCES dbo.WorkshopServices(RowId) ON DELETE SET NULL',
 N'ServiceDocumentServiceLines'),
(N'FK_WorkshopOnlineBookings_UserCar',
 N'ALTER TABLE dbo.WorkshopOnlineBookings ADD CONSTRAINT FK_WorkshopOnlineBookings_UserCar FOREIGN KEY (UserCarId) REFERENCES dbo.UserCars(RowId) ON DELETE SET NULL',
 N'WorkshopOnlineBookings'),
(N'FK_WorkshopOnlineBookings_RejectedBy',
 N'ALTER TABLE dbo.WorkshopOnlineBookings ADD CONSTRAINT FK_WorkshopOnlineBookings_RejectedBy FOREIGN KEY (RejectedByEmployeeId) REFERENCES dbo.Employees(RowId) ON DELETE SET NULL',
 N'WorkshopOnlineBookings'),
(N'FK_WorkshopGuestCars_UserCar',
 N'ALTER TABLE dbo.WorkshopGuestCars ADD CONSTRAINT FK_WorkshopGuestCars_UserCar FOREIGN KEY (UserCarId) REFERENCES dbo.UserCars(RowId) ON DELETE SET NULL',
 N'WorkshopGuestCars'),
(N'FK_AppActivityEvents_User',
 N'ALTER TABLE dbo.AppActivityEvents ADD CONSTRAINT FK_AppActivityEvents_User FOREIGN KEY (UserId) REFERENCES dbo.Users(RowId) ON DELETE SET NULL',
 N'AppActivityEvents'),
(N'FK_AppActivityEvents_Employee',
 N'ALTER TABLE dbo.AppActivityEvents ADD CONSTRAINT FK_AppActivityEvents_Employee FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(RowId) ON DELETE SET NULL',
 N'AppActivityEvents'),
(N'FK_AppActivityEvents_Workshop',
 N'ALTER TABLE dbo.AppActivityEvents ADD CONSTRAINT FK_AppActivityEvents_Workshop FOREIGN KEY (WorkshopId) REFERENCES dbo.Workshops(RowId) ON DELETE SET NULL',
 N'AppActivityEvents'),
(N'FK_AppActivityEvents_Company',
 N'ALTER TABLE dbo.AppActivityEvents ADD CONSTRAINT FK_AppActivityEvents_Company FOREIGN KEY (CompanyId) REFERENCES dbo.Companies(RowId) ON DELETE SET NULL',
 N'AppActivityEvents'),
(N'FK_RolePermissions_Roles',
 N'ALTER TABLE dbo.RolePermissions ADD CONSTRAINT FK_RolePermissions_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(RowId) ON DELETE CASCADE',
 N'RolePermissions'),
(N'FK_RolePermissions_Permissions',
 N'ALTER TABLE dbo.RolePermissions ADD CONSTRAINT FK_RolePermissions_Permissions FOREIGN KEY (PermissionId) REFERENCES dbo.Permissions(RowId) ON DELETE CASCADE',
 N'RolePermissions');

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

PRINT N'--- Также выполните (если ещё не): Generate_ForeignKeys_FromModel.sql, Fix_Remaining_ForeignKeys.sql ---';
PRINT N'=== Готово ===';
GO
