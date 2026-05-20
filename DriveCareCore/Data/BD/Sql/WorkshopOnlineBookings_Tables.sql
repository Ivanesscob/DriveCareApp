-- Онлайн-запись клиента на автосервис + роль/разрешение «Подтвердить запись».
-- Выполнить на DriveCareDB.

IF OBJECT_ID(N'dbo.WorkshopOnlineBookings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkshopOnlineBookings (
        RowId                   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_WorkshopOnlineBookings PRIMARY KEY,
        WorkshopId              UNIQUEIDENTIFIER NOT NULL,
        UserId                  UNIQUEIDENTIFIER NOT NULL,
        UserCarId               UNIQUEIDENTIFIER NULL,
        ClientPhone             NVARCHAR(50)     NULL,
        ClientComment           NVARCHAR(500)    NULL,
        PreferredDate           DATETIME         NULL,
        Status                  TINYINT          NOT NULL CONSTRAINT DF_WorkshopOnlineBookings_Status DEFAULT (0),
        -- 0 ожидает, 1 подтверждена, 2 отменена клиентом, 3 отклонена сервисом
        CreatedAt               DATETIME         NOT NULL CONSTRAINT DF_WorkshopOnlineBookings_Created DEFAULT (GETDATE()),
        ConfirmedAt             DATETIME         NULL,
        ConfirmedByEmployeeId   UNIQUEIDENTIFIER NULL,
        CONSTRAINT FK_WorkshopOnlineBookings_Workshop FOREIGN KEY (WorkshopId) REFERENCES dbo.Workshops (RowId),
        CONSTRAINT FK_WorkshopOnlineBookings_User FOREIGN KEY (UserId) REFERENCES dbo.Users (RowId),
        CONSTRAINT FK_WorkshopOnlineBookings_ConfirmedBy FOREIGN KEY (ConfirmedByEmployeeId) REFERENCES dbo.Employees (RowId)
    );

    CREATE INDEX IX_WorkshopOnlineBookings_Workshop_Status
        ON dbo.WorkshopOnlineBookings (WorkshopId, Status, CreatedAt DESC);

    CREATE INDEX IX_WorkshopOnlineBookings_User
        ON dbo.WorkshopOnlineBookings (UserId, CreatedAt DESC);
END
GO

-- Разрешение
DECLARE @PermConfirm UNIQUEIDENTIFIER = 'B8E2F4A1-3C5D-4E9F-A2B1-7D8E9F0A1B2C';
IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE RowId = @PermConfirm)
    INSERT INTO dbo.Permissions (RowId, Code, Name, Description, PermissionGroupId)
    VALUES (@PermConfirm, N'CONFIRM_WORKSHOP_BOOKING', N'Подтверждение онлайн-записей', N'Подтверждать записи клиентов на сервис', NULL);
GO

-- Роль (назначьте сотрудникам в Pro → роли)
-- Было 8E9F0A1B2D3 (11 символов) — невалидный GUID; исправлено:
DECLARE @RoleBookingConfirmer UNIQUEIDENTIFIER = 'DC020002-0002-4002-8002-000000000002';
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RowId = @RoleBookingConfirmer)
    INSERT INTO dbo.Roles (RowId, Name, Description, WorkshopId, IsActive, CompanyId)
    VALUES (@RoleBookingConfirmer, N'Подтверждение записей', N'Подтверждает онлайн-записи клиентов на автосервис', NULL, 1, NULL);
GO

DECLARE @PermConfirm2 UNIQUEIDENTIFIER = 'B8E2F4A1-3C5D-4E9F-A2B1-7D8E9F0A1B2C';
DECLARE @RoleBookingConfirmer2 UNIQUEIDENTIFIER = 'DC020002-0002-4002-8002-000000000002';
IF NOT EXISTS (SELECT 1 FROM dbo.RolePermissionsMap WHERE RoleId = @RoleBookingConfirmer2 AND PermissionId = @PermConfirm2)
    INSERT INTO dbo.RolePermissionsMap (RowId, RoleId, PermissionId)
    VALUES (NEWID(), @RoleBookingConfirmer2, @PermConfirm2);
GO

-- Владельцу автосервиса тоже даём право подтверждать (если роль есть в БД)
DECLARE @PermConfirm3 UNIQUEIDENTIFIER = 'B8E2F4A1-3C5D-4E9F-A2B1-7D8E9F0A1B2C';
DECLARE @WorkshopOwnerRole UNIQUEIDENTIFIER = 'AAEFDE24-DC8D-46EA-8A31-028CC44E41C7';
IF EXISTS (SELECT 1 FROM dbo.Roles WHERE RowId = @WorkshopOwnerRole)
   AND NOT EXISTS (SELECT 1 FROM dbo.RolePermissionsMap WHERE RoleId = @WorkshopOwnerRole AND PermissionId = @PermConfirm3)
    INSERT INTO dbo.RolePermissionsMap (RowId, RoleId, PermissionId)
    VALUES (NEWID(), @WorkshopOwnerRole, @PermConfirm3);
GO
