-- Запросы на закупку запчастей по заданию.
-- Выполните на DriveCareDB.

IF OBJECT_ID(N'dbo.TaskPurchaseRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TaskPurchaseRequests (
        RowId                   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TaskPurchaseRequests PRIMARY KEY,
        SourceTaskId            UNIQUEIDENTIFIER NOT NULL,
        PurchaseTaskId          UNIQUEIDENTIFIER NOT NULL,
        RequestedByEmployeeId   UNIQUEIDENTIFIER NOT NULL,
        PurchaserEmployeeId     UNIQUEIDENTIFIER NOT NULL,
        IsFulfilled             BIT              NOT NULL CONSTRAINT DF_TaskPurchaseRequests_Fulfilled DEFAULT(0),
        CreatedAt               DATETIME         NOT NULL CONSTRAINT DF_TaskPurchaseRequests_Created DEFAULT(GETDATE())
    );
    CREATE INDEX IX_TaskPurchaseRequests_Source ON dbo.TaskPurchaseRequests(SourceTaskId);
    CREATE INDEX IX_TaskPurchaseRequests_Purchase ON dbo.TaskPurchaseRequests(PurchaseTaskId);
END
GO

IF OBJECT_ID(N'dbo.TaskPurchaseRequestLines', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TaskPurchaseRequestLines (
        RowId               UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TaskPurchaseRequestLines PRIMARY KEY,
        RequestId           UNIQUEIDENTIFIER NOT NULL,
        WorkshopPartId      UNIQUEIDENTIFIER NULL,
        PartName            NVARCHAR(300)    NOT NULL,
        Quantity            DECIMAL(18, 3)   NOT NULL CONSTRAINT DF_TaskPurchaseReqLines_Qty DEFAULT(1),
        UnitName            NVARCHAR(30)     NULL,
        UnitPrice           DECIMAL(18, 2)   NOT NULL CONSTRAINT DF_TaskPurchaseReqLines_Price DEFAULT(0),
        SortOrder           INT              NOT NULL CONSTRAINT DF_TaskPurchaseReqLines_Sort DEFAULT(0)
    );
    CREATE INDEX IX_TaskPurchaseRequestLines_Request ON dbo.TaskPurchaseRequestLines(RequestId);
END
GO

-- Опционально: разрешение для уполномоченных закупщиков (код PURCHASE_PARTS).
-- INSERT INTO Permissions (RowId, Code, Name, Description) VALUES (NEWID(), N'PURCHASE_PARTS', N'Закупка запчастей', N'Может выполнять закупки по запросам');
