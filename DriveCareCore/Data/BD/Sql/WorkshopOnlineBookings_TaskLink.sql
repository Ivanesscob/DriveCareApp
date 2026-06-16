-- Связь онлайн-записи с заданием и заказ-нарядом.
IF COL_LENGTH(N'dbo.WorkshopOnlineBookings', N'TaskId') IS NULL
    ALTER TABLE dbo.WorkshopOnlineBookings ADD TaskId UNIQUEIDENTIFIER NULL;
GO

IF COL_LENGTH(N'dbo.WorkshopOnlineBookings', N'RepairHistoryId') IS NULL
    ALTER TABLE dbo.WorkshopOnlineBookings ADD RepairHistoryId UNIQUEIDENTIFIER NULL;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_WorkshopOnlineBookings_Task')
   AND COL_LENGTH(N'dbo.WorkshopOnlineBookings', N'TaskId') IS NOT NULL
BEGIN
    ALTER TABLE dbo.WorkshopOnlineBookings
        ADD CONSTRAINT FK_WorkshopOnlineBookings_Task
        FOREIGN KEY (TaskId) REFERENCES dbo.Tasks (RowId);
END
GO
