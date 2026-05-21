-- Лимит онлайн-записей в день на мастерскую (настраивается в Pro → Расписание работы).
-- Выполнить на DriveCareDB.

IF OBJECT_ID(N'dbo.WorkshopOnlineBookingSettings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkshopOnlineBookingSettings (
        WorkshopId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_WorkshopOnlineBookingSettings PRIMARY KEY,
        MaxBookingsPerDay   INT              NOT NULL CONSTRAINT DF_WorkshopOnlineBookingSettings_Max DEFAULT (5),
        CONSTRAINT FK_WorkshopOnlineBookingSettings_Workshop FOREIGN KEY (WorkshopId) REFERENCES dbo.Workshops (RowId),
        CONSTRAINT CK_WorkshopOnlineBookingSettings_Max CHECK (MaxBookingsPerDay BETWEEN 1 AND 999)
    );
END
GO
