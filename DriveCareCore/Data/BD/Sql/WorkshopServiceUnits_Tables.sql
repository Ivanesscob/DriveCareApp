-- Справочник единиц измерения для каталога услуг мастерской.
-- Выполните на DriveCareDB после WorkshopServices_Tables.sql.

IF OBJECT_ID(N'dbo.WorkshopServiceUnits', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkshopServiceUnits (
        RowId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_WorkshopServiceUnits PRIMARY KEY,
        WorkshopId  UNIQUEIDENTIFIER NOT NULL,
        Name        NVARCHAR(30)     NOT NULL,
        IsActive    BIT              NOT NULL CONSTRAINT DF_WorkshopServiceUnits_Active DEFAULT(1),
        CreatedAt   DATETIME         NOT NULL CONSTRAINT DF_WorkshopServiceUnits_Created DEFAULT(GETDATE())
    );
    CREATE INDEX IX_WorkshopServiceUnits_Workshop ON dbo.WorkshopServiceUnits(WorkshopId);
    CREATE UNIQUE INDEX UX_WorkshopServiceUnits_Workshop_Name ON dbo.WorkshopServiceUnits(WorkshopId, Name);
END
GO

IF COL_LENGTH(N'dbo.WorkshopServices', N'UnitId') IS NULL
    ALTER TABLE dbo.WorkshopServices ADD UnitId UNIQUEIDENTIFIER NULL;
GO
