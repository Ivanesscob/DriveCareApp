-- Каталог покрасочных услуг мастерских, доступные цвета и запросы клиентов.
-- Выполните на DriveCareDB.

IF OBJECT_ID(N'dbo.WorkshopPaintServices', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkshopPaintServices (
        RowId           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_WorkshopPaintServices PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        WorkshopId      UNIQUEIDENTIFIER NOT NULL,
        PaintKind       TINYINT          NOT NULL, -- 1 диски, 2 вся машина, 3 деталь
        Name            NVARCHAR(200)    NOT NULL,
        Description     NVARCHAR(500)    NULL,
        PriceFrom       DECIMAL(18, 2)   NULL,
        IsActive        BIT              NOT NULL CONSTRAINT DF_WorkshopPaintServices_Active DEFAULT(1),
        SortOrder       INT              NOT NULL CONSTRAINT DF_WorkshopPaintServices_Sort DEFAULT(0),
        CreatedAt       DATETIME2        NOT NULL CONSTRAINT DF_WorkshopPaintServices_Created DEFAULT(SYSUTCDATETIME())
    );
    CREATE INDEX IX_WorkshopPaintServices_Workshop ON dbo.WorkshopPaintServices(WorkshopId);
END
GO

IF OBJECT_ID(N'dbo.WorkshopPaintColors', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkshopPaintColors (
        RowId           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_WorkshopPaintColors PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        WorkshopId      UNIQUEIDENTIFIER NOT NULL,
        ColorId         UNIQUEIDENTIFIER NULL,
        ColorName       NVARCHAR(120)    NOT NULL,
        IsActive        BIT              NOT NULL CONSTRAINT DF_WorkshopPaintColors_Active DEFAULT(1),
        SortOrder       INT              NOT NULL CONSTRAINT DF_WorkshopPaintColors_Sort DEFAULT(0),
        CreatedAt       DATETIME2        NOT NULL CONSTRAINT DF_WorkshopPaintColors_Created DEFAULT(SYSUTCDATETIME())
    );
    CREATE INDEX IX_WorkshopPaintColors_Workshop ON dbo.WorkshopPaintColors(WorkshopId);
END
GO

IF OBJECT_ID(N'dbo.UserWorkshopPaintInquiries', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserWorkshopPaintInquiries (
        RowId                   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_UserWorkshopPaintInquiries PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        UserId                  UNIQUEIDENTIFIER NOT NULL,
        UserCarId               UNIQUEIDENTIFIER NOT NULL,
        CarId                   UNIQUEIDENTIFIER NOT NULL,
        WorkshopId              UNIQUEIDENTIFIER NOT NULL,
        WorkshopPaintServiceId  UNIQUEIDENTIFIER NULL,
        PaintKind               TINYINT          NOT NULL,
        ColorId                 UNIQUEIDENTIFIER NULL,
        ColorName               NVARCHAR(120)    NOT NULL,
        PartName                NVARCHAR(200)    NULL,
        Notes                   NVARCHAR(500)    NULL,
        StatusCode              TINYINT          NOT NULL CONSTRAINT DF_UserWorkshopPaintInquiries_Status DEFAULT(0), -- 0 новая, 1 принята, 2 отклонена
        CreatedAt               DATETIME2        NOT NULL CONSTRAINT DF_UserWorkshopPaintInquiries_Created DEFAULT(SYSUTCDATETIME())
    );
    CREATE INDEX IX_UserWorkshopPaintInquiries_User ON dbo.UserWorkshopPaintInquiries(UserId, CreatedAt DESC);
    CREATE INDEX IX_UserWorkshopPaintInquiries_Workshop ON dbo.UserWorkshopPaintInquiries(WorkshopId, CreatedAt DESC);
END
GO

-- Пример каталога для мастерских с типом «Покраска» (BusinessType DC010002... или WorkshopBusinessTypes)
-- Раскомментируйте и подставьте реальные WorkshopId при необходимости.
