-- Склад запчастей мастерской и связь со строками отчёта по заданию.
-- Выполните на DriveCareDB после WorkshopServices_Tables.sql.

IF OBJECT_ID(N'dbo.WorkshopParts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkshopParts (
        RowId           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_WorkshopParts PRIMARY KEY,
        WorkshopId      UNIQUEIDENTIFIER NOT NULL,
        Name            NVARCHAR(300)    NOT NULL,
        Article         NVARCHAR(80)     NULL,
        Description     NVARCHAR(500)    NULL,
        Price           DECIMAL(18, 2)   NOT NULL CONSTRAINT DF_WorkshopParts_Price DEFAULT(0),
        UnitName        NVARCHAR(30)     NOT NULL CONSTRAINT DF_WorkshopParts_Unit DEFAULT(N'шт.'),
        QuantityOnHand  DECIMAL(18, 3)   NOT NULL CONSTRAINT DF_WorkshopParts_Qty DEFAULT(0),
        Category        NVARCHAR(40)     NOT NULL CONSTRAINT DF_WorkshopParts_Cat DEFAULT(N'Accessories'),
        IsActive        BIT              NOT NULL CONSTRAINT DF_WorkshopParts_Active DEFAULT(1),
        CreatedAt       DATETIME         NOT NULL CONSTRAINT DF_WorkshopParts_Created DEFAULT(GETDATE())
    );
    CREATE INDEX IX_WorkshopParts_Workshop ON dbo.WorkshopParts(WorkshopId);
    CREATE INDEX IX_WorkshopParts_Stock ON dbo.WorkshopParts(WorkshopId, IsActive, QuantityOnHand);
END
GO

IF COL_LENGTH(N'dbo.TaskPartLines', N'WorkshopPartId') IS NULL
BEGIN
    ALTER TABLE dbo.TaskPartLines ADD WorkshopPartId UNIQUEIDENTIFIER NULL;
END
GO
