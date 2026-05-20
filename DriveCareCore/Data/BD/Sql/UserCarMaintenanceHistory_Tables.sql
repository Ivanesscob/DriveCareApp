-- История обслуживания по машине пользователя (пробег с одометра, визиты).
IF OBJECT_ID(N'dbo.UserCarMaintenanceHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserCarMaintenanceHistory (
        RowId           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_UserCarMaintenanceHistory PRIMARY KEY,
        UserCarRowId    UNIQUEIDENTIFIER NOT NULL,
        ServiceDate     DATETIME         NOT NULL,
        MileageKm       INT              NULL,
        Title           NVARCHAR(200)    NULL,
        Notes           NVARCHAR(MAX)    NULL
    );

    CREATE INDEX IX_UserCarMaintenanceHistory_UserCar
        ON dbo.UserCarMaintenanceHistory (UserCarRowId, ServiceDate DESC);
END
GO
