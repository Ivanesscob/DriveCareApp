-- Журнал действий для статистики (просмотры объявлений, заказы, задания и т.д.)
-- Выполнить на DriveCareDB.

IF OBJECT_ID(N'dbo.AppActivityEvents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppActivityEvents
    (
        RowId        UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AppActivityEvents PRIMARY KEY,
        EventCode    NVARCHAR(80)     NOT NULL,
        ActorKind    TINYINT          NOT NULL, -- 0=пользователь, 1=сотрудник, 2=система
        UserId       UNIQUEIDENTIFIER NULL,
        EmployeeId   UNIQUEIDENTIFIER NULL,
        WorkshopId   UNIQUEIDENTIFIER NULL,
        CompanyId    UNIQUEIDENTIFIER NULL,
        EntityType   NVARCHAR(60)     NULL,
        EntityId     UNIQUEIDENTIFIER NULL,
        PayloadJson  NVARCHAR(MAX)    NULL,
        CreatedAt    DATETIME2(0)     NOT NULL CONSTRAINT DF_AppActivityEvents_CreatedAt DEFAULT (SYSUTCDATETIME())
    );

    CREATE INDEX IX_AppActivityEvents_Code_Date ON dbo.AppActivityEvents (EventCode, CreatedAt DESC);
    CREATE INDEX IX_AppActivityEvents_CreatedAt ON dbo.AppActivityEvents (CreatedAt DESC);
    CREATE INDEX IX_AppActivityEvents_Workshop ON dbo.AppActivityEvents (WorkshopId, CreatedAt DESC) WHERE WorkshopId IS NOT NULL;
    CREATE INDEX IX_AppActivityEvents_Company ON dbo.AppActivityEvents (CompanyId, CreatedAt DESC) WHERE CompanyId IS NOT NULL;
    CREATE INDEX IX_AppActivityEvents_Entity ON dbo.AppActivityEvents (EntityType, EntityId, CreatedAt DESC)
        WHERE EntityType IS NOT NULL AND EntityId IS NOT NULL;
END
GO

-- Разрешение «Статистика» (VIEW_ANALYTICS) — если ещё нет в справочнике
IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE RowId = '03F24D58-7616-4E5E-8671-1484322AF40E')
BEGIN
    DECLARE @GroupId UNIQUEIDENTIFIER;
    SELECT TOP 1 @GroupId = RowId FROM dbo.PermissionGroups
    WHERE LOWER(ISNULL(Code, N'') + N' ' + ISNULL(Name, N'')) LIKE N'%организац%'
       OR LOWER(ISNULL(Code, N'') + N' ' + ISNULL(Name, N'')) LIKE N'%pro%'
    ORDER BY Name;

    IF @GroupId IS NULL
        SELECT TOP 1 @GroupId = RowId FROM dbo.PermissionGroups ORDER BY Name;

    INSERT INTO dbo.Permissions (RowId, PermissionGroupId, Code, Name, Description)
    VALUES (
        '03F24D58-7616-4E5E-8671-1484322AF40E',
        @GroupId,
        N'VIEW_ANALYTICS',
        N'Просмотр статистики',
        N'Доступ к вкладке статистики организации'
    );
END
GO
