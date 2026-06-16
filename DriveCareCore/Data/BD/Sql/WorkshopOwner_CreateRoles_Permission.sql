-- Право CREATE_ROLES для роли «Владелец автосервиса» (кнопка и проверки по коду в БД).
-- Выполнить на DriveCareDB.

DECLARE @PermCreateRoles UNIQUEIDENTIFIER = '82198DF6-1639-41BA-A4A9-1E4DAA3B2E26';
IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE RowId = @PermCreateRoles)
    INSERT INTO dbo.Permissions (RowId, Code, Name, Description, PermissionGroupId)
    VALUES (@PermCreateRoles, N'CREATE_ROLES', N'Создание ролей',
            N'Создание и редактирование ролей организации', NULL);
GO

DECLARE @PermCreateRoles2 UNIQUEIDENTIFIER = '82198DF6-1639-41BA-A4A9-1E4DAA3B2E26';
DECLARE @WorkshopOwnerRole UNIQUEIDENTIFIER = 'AAEFDE24-DC8D-46EA-8A31-028CC44E41C7';
IF EXISTS (SELECT 1 FROM dbo.Roles WHERE RowId = @WorkshopOwnerRole)
   AND NOT EXISTS (SELECT 1 FROM dbo.RolePermissionsMap WHERE RoleId = @WorkshopOwnerRole AND PermissionId = @PermCreateRoles2)
    INSERT INTO dbo.RolePermissionsMap (RowId, RoleId, PermissionId)
    VALUES (NEWID(), @WorkshopOwnerRole, @PermCreateRoles2);
GO
