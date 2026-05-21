-- Пункты выдачи заказов магазина (Санкт-Петербург и область).
-- Выполнить на DriveCareDB перед OrderPickupPoints_SpbSeed.sql

IF OBJECT_ID(N'dbo.OrderPickupPoints', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrderPickupPoints (
        RowId           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_OrderPickupPoints PRIMARY KEY,
        Code            NVARCHAR(16)     NOT NULL,
        Name            NVARCHAR(120)    NOT NULL,
        District        NVARCHAR(80)     NOT NULL,
        AddressLine     NVARCHAR(250)    NOT NULL,
        City            NVARCHAR(60)     NOT NULL CONSTRAINT DF_OPP_City DEFAULT (N'Санкт-Петербург'),
        Latitude        FLOAT            NULL,
        Longitude       FLOAT            NULL,
        SortOrder       INT              NOT NULL CONSTRAINT DF_OPP_Sort DEFAULT (0),
        IsActive        BIT              NOT NULL CONSTRAINT DF_OPP_Active DEFAULT (1),
        CreatedAt       DATETIME         NOT NULL CONSTRAINT DF_OPP_Created DEFAULT (GETDATE())
    );

    CREATE UNIQUE INDEX UQ_OrderPickupPoints_Code ON dbo.OrderPickupPoints (Code);
    CREATE INDEX IX_OrderPickupPoints_District ON dbo.OrderPickupPoints (District, SortOrder);
END
GO

IF OBJECT_ID(N'dbo.StoreOrders', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StoreOrders (
        RowId           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_StoreOrders PRIMARY KEY,
        UserId          UNIQUEIDENTIFIER NOT NULL,
        PickupPointId   UNIQUEIDENTIFIER NOT NULL,
        OrderNumber     NVARCHAR(32)     NOT NULL,
        Status          TINYINT          NOT NULL, -- 0 ожидает оплаты, 1 оплачен, 2 готов к выдаче, 3 выдан, 4 отменён
        TotalAmount     DECIMAL(18,2)    NOT NULL,
        QrPayload       NVARCHAR(500)    NULL,
        CreatedAt       DATETIME         NOT NULL CONSTRAINT DF_SO_Created DEFAULT (GETDATE()),
        PaidAt          DATETIME         NULL
    );

    CREATE UNIQUE INDEX UQ_StoreOrders_OrderNumber ON dbo.StoreOrders (OrderNumber);
    CREATE INDEX IX_StoreOrders_User ON dbo.StoreOrders (UserId, CreatedAt DESC);
END
GO

IF OBJECT_ID(N'dbo.StoreOrderLines', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StoreOrderLines (
        RowId           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_StoreOrderLines PRIMARY KEY,
        OrderId         UNIQUEIDENTIFIER NOT NULL,
        ProductId       UNIQUEIDENTIFIER NOT NULL,
        ProductName     NVARCHAR(200)    NOT NULL,
        Category        NVARCHAR(40)     NULL,
        Quantity        INT              NOT NULL,
        UnitPrice       DECIMAL(18,2)    NOT NULL,
        SortOrder       INT              NOT NULL CONSTRAINT DF_SOL_Sort DEFAULT (0)
    );

    CREATE INDEX IX_StoreOrderLines_Order ON dbo.StoreOrderLines (OrderId, SortOrder);
END
GO
