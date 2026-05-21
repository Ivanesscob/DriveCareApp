-- Расширение истории обслуживания и статусы узлов автомобиля.
IF OBJECT_ID(N'dbo.UserCarMaintenanceHistory', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.UserCarMaintenanceHistory', N'ComponentCode') IS NULL
        ALTER TABLE dbo.UserCarMaintenanceHistory ADD ComponentCode NVARCHAR(32) NULL;

    IF COL_LENGTH(N'dbo.UserCarMaintenanceHistory', N'WorkshopName') IS NULL
        ALTER TABLE dbo.UserCarMaintenanceHistory ADD WorkshopName NVARCHAR(120) NULL;

    IF COL_LENGTH(N'dbo.UserCarMaintenanceHistory', N'SeverityAfter') IS NULL
        ALTER TABLE dbo.UserCarMaintenanceHistory ADD SeverityAfter TINYINT NULL; -- 0 норма, 1 скоро, 2 заменить
END
GO

IF OBJECT_ID(N'dbo.UserCarComponentStatuses', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserCarComponentStatuses (
        RowId              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_UserCarComponentStatuses PRIMARY KEY DEFAULT (NEWID()),
        UserCarRowId        UNIQUEIDENTIFIER NOT NULL,
        ComponentCode       NVARCHAR(32)     NOT NULL,
        StatusLevel         TINYINT          NOT NULL, -- 0 Good, 1 Watch, 2 Due, 3 Unknown
        LastServiceDate     DATETIME         NULL,
        LastMileageKm        INT              NULL,
        RemainingKmHint     INT              NULL,
        ShortHint           NVARCHAR(200)    NULL,
        UpdatedAt           DATETIME         NOT NULL CONSTRAINT DF_UCCS_Updated DEFAULT (GETDATE()),
        CONSTRAINT UQ_UserCarComponentStatuses UNIQUE (UserCarRowId, ComponentCode)
    );

    CREATE INDEX IX_UserCarComponentStatuses_UserCar ON dbo.UserCarComponentStatuses (UserCarRowId);
END
GO
