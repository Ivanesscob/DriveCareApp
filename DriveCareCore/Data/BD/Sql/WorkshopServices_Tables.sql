-- Каталог услуг мастерской (автосалон/сервис) и строки отчёта по заданию.
-- Выполните на DriveCareDB.

IF OBJECT_ID(N'dbo.WorkshopServices', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkshopServices (
        RowId           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_WorkshopServices PRIMARY KEY,
        WorkshopId      UNIQUEIDENTIFIER NOT NULL,
        Name            NVARCHAR(300)    NOT NULL,
        Description     NVARCHAR(MAX)    NULL,
        Price           DECIMAL(18, 2)   NOT NULL CONSTRAINT DF_WorkshopServices_Price DEFAULT(0),
        UnitName        NVARCHAR(30)     NULL,
        IsActive        BIT              NOT NULL CONSTRAINT DF_WorkshopServices_Active DEFAULT(1),
        SortOrder       INT              NOT NULL CONSTRAINT DF_WorkshopServices_Sort DEFAULT(0),
        CreatedAt       DATETIME         NOT NULL CONSTRAINT DF_WorkshopServices_Created DEFAULT(GETDATE())
    );
    CREATE INDEX IX_WorkshopServices_Workshop ON dbo.WorkshopServices(WorkshopId);
END
GO

IF OBJECT_ID(N'dbo.TaskServiceLines', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TaskServiceLines (
        RowId               UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TaskServiceLines PRIMARY KEY,
        TaskId              UNIQUEIDENTIFIER NOT NULL,
        WorkshopServiceId   UNIQUEIDENTIFIER NULL,
        ServiceName         NVARCHAR(300)    NOT NULL,
        Quantity            DECIMAL(18, 3)   NOT NULL CONSTRAINT DF_TaskServiceLines_Qty DEFAULT(1),
        UnitName            NVARCHAR(30)     NULL,
        UnitPrice           DECIMAL(18, 2)   NOT NULL CONSTRAINT DF_TaskServiceLines_Price DEFAULT(0),
        DiscountPercent     DECIMAL(9, 2)    NOT NULL CONSTRAINT DF_TaskServiceLines_Disc DEFAULT(0),
        LineAmount          DECIMAL(18, 2)   NOT NULL CONSTRAINT DF_TaskServiceLines_Amount DEFAULT(0),
        SortOrder           INT              NOT NULL CONSTRAINT DF_TaskServiceLines_Sort DEFAULT(0)
    );
    CREATE INDEX IX_TaskServiceLines_Task ON dbo.TaskServiceLines(TaskId);
END
GO

IF OBJECT_ID(N'dbo.TaskPartLines', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TaskPartLines (
        RowId           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TaskPartLines PRIMARY KEY,
        TaskId          UNIQUEIDENTIFIER NOT NULL,
        PartName        NVARCHAR(300)    NOT NULL,
        Quantity        DECIMAL(18, 3)   NOT NULL CONSTRAINT DF_TaskPartLines_Qty DEFAULT(1),
        UnitName        NVARCHAR(30)     NULL,
        UnitPrice       DECIMAL(18, 2)   NOT NULL CONSTRAINT DF_TaskPartLines_Price DEFAULT(0),
        LineAmount      DECIMAL(18, 2)   NOT NULL CONSTRAINT DF_TaskPartLines_Amount DEFAULT(0),
        SortOrder       INT              NOT NULL CONSTRAINT DF_TaskPartLines_Sort DEFAULT(0)
    );
    CREATE INDEX IX_TaskPartLines_Task ON dbo.TaskPartLines(TaskId);
END
GO
