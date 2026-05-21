-- Расписание работы мастерской по дням недели (владелец настраивает в Pro).
-- DayOfWeek: 1 = понедельник … 7 = воскресенье.
-- Выполнить на DriveCareDB.

IF OBJECT_ID(N'dbo.WorkshopWorkSchedules', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkshopWorkSchedules (
        RowId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_WorkshopWorkSchedules PRIMARY KEY DEFAULT (NEWID()),
        WorkshopId  UNIQUEIDENTIFIER NOT NULL,
        DayOfWeek   TINYINT          NOT NULL,
        IsClosed    BIT              NOT NULL CONSTRAINT DF_WorkshopWorkSchedules_Closed DEFAULT (0),
        OpenTime    TIME(0)          NULL,
        CloseTime   TIME(0)          NULL,
        CONSTRAINT FK_WorkshopWorkSchedules_Workshop FOREIGN KEY (WorkshopId) REFERENCES dbo.Workshops (RowId),
        CONSTRAINT UQ_WorkshopWorkSchedules_Workshop_Day UNIQUE (WorkshopId, DayOfWeek),
        CONSTRAINT CK_WorkshopWorkSchedules_DayOfWeek CHECK (DayOfWeek BETWEEN 1 AND 7)
    );

    CREATE INDEX IX_WorkshopWorkSchedules_Workshop
        ON dbo.WorkshopWorkSchedules (WorkshopId);
END
GO

DECLARE @PermSchedule UNIQUEIDENTIFIER = 'DC030001-0001-4001-8001-000000000001';
IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE RowId = @PermSchedule)
    INSERT INTO dbo.Permissions (RowId, Code, Name, Description, PermissionGroupId)
    VALUES (@PermSchedule, N'MANAGE_WORKSHOP_SCHEDULE', N'Расписание мастерской',
            N'Настройка часов работы сервиса по дням недели', NULL);
GO

DECLARE @PermSchedule2 UNIQUEIDENTIFIER = 'DC030001-0001-4001-8001-000000000001';
DECLARE @WorkshopOwnerRole UNIQUEIDENTIFIER = 'AAEFDE24-DC8D-46EA-8A31-028CC44E41C7';
IF EXISTS (SELECT 1 FROM dbo.Roles WHERE RowId = @WorkshopOwnerRole)
   AND NOT EXISTS (SELECT 1 FROM dbo.RolePermissionsMap WHERE RoleId = @WorkshopOwnerRole AND PermissionId = @PermSchedule2)
    INSERT INTO dbo.RolePermissionsMap (RowId, RoleId, PermissionId)
    VALUES (NEWID(), @WorkshopOwnerRole, @PermSchedule2);
GO
