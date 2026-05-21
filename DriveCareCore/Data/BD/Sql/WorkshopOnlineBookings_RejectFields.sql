-- Поля отклонения онлайн-записи. Выполнить на DriveCareDB.

IF OBJECT_ID(N'dbo.WorkshopOnlineBookings', N'U') IS NULL
BEGIN
    RAISERROR(N'Сначала создайте dbo.WorkshopOnlineBookings.', 16, 1);
    RETURN;
END
GO

IF COL_LENGTH(N'dbo.WorkshopOnlineBookings', N'RejectReason') IS NULL
    ALTER TABLE dbo.WorkshopOnlineBookings ADD RejectReason NVARCHAR(500) NULL;
GO

IF COL_LENGTH(N'dbo.WorkshopOnlineBookings', N'RejectedAt') IS NULL
    ALTER TABLE dbo.WorkshopOnlineBookings ADD RejectedAt DATETIME NULL;
GO

IF COL_LENGTH(N'dbo.WorkshopOnlineBookings', N'RejectedByEmployeeId') IS NULL
    ALTER TABLE dbo.WorkshopOnlineBookings ADD RejectedByEmployeeId UNIQUEIDENTIFIER NULL;
GO
