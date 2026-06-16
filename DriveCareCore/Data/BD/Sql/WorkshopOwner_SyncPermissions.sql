-- Синхронизация прав роли «Владелец автосервиса»: все Permissions, кроме админских и клиентских групп.
-- Логика совпадает с OrganizationPermissionRules в DriveCare Pro.
-- Выполнить на DriveCareDB (можно повторно).

DECLARE @WorkshopOwnerRole UNIQUEIDENTIFIER = 'AAEFDE24-DC8D-46EA-8A31-028CC44E41C7';

IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RowId = @WorkshopOwnerRole)
BEGIN
    RAISERROR (N'Роль владельца автосервиса не найдена.', 16, 1);
    RETURN;
END

INSERT INTO dbo.RolePermissionsMap (RowId, RoleId, PermissionId)
SELECT NEWID(), @WorkshopOwnerRole, p.RowId
FROM dbo.Permissions p
LEFT JOIN dbo.PermissionGroups g ON g.RowId = p.PermissionGroupId
WHERE p.Code IS NOT NULL AND LTRIM(RTRIM(p.Code)) <> N''
  AND NOT EXISTS (
      SELECT 1 FROM dbo.RolePermissionsMap m
      WHERE m.RoleId = @WorkshopOwnerRole AND m.PermissionId = p.RowId)
  -- не админ
  AND NOT (
      LOWER(ISNULL(g.Code, N'') + N' ' + ISNULL(g.Name, N'') + N' ' + ISNULL(p.Code, N'')) LIKE N'%админ%'
      OR LOWER(ISNULL(g.Code, N'') + N' ' + ISNULL(g.Name, N'') + N' ' + ISNULL(p.Code, N'')) LIKE N'%admin%'
      OR LOWER(ISNULL(g.Code, N'') + N' ' + ISNULL(g.Name, N'') + N' ' + ISNULL(p.Code, N'')) LIKE N'%модерац%'
      OR LOWER(ISNULL(g.Code, N'') + N' ' + ISNULL(g.Name, N'') + N' ' + ISNULL(p.Code, N'')) LIKE N'%moderation%'
      OR LOWER(ISNULL(p.Code, N'')) IN (N'admin_panel', N'moderate_sales', N'view_notifications')
  )
  -- не клиент / пользователь DriveCare
  AND NOT (
      LOWER(ISNULL(g.Code, N'') + N' ' + ISNULL(g.Name, N'')) LIKE N'%пользовател%'
      OR LOWER(ISNULL(g.Code, N'') + N' ' + ISNULL(g.Name, N'')) LIKE N'%клиент%'
      OR LOWER(ISNULL(g.Code, N'') + N' ' + ISNULL(g.Name, N'')) LIKE N'%client%'
      OR LOWER(ISNULL(g.Code, N'') + N' ' + ISNULL(g.Name, N'')) IN (N'user', N'users')
      OR LOWER(ISNULL(p.Code, N'')) LIKE N'user.%'
      OR LOWER(ISNULL(p.Code, N'')) LIKE N'users.%'
  )
  -- не чисто сервис Pro (уже покрыто группами сотрудников; оставляем сотруднические коды)
  AND NOT (
      LOWER(ISNULL(g.Code, N'') + N' ' + ISNULL(g.Name, N'')) LIKE N'%сотрудник%'
      AND LOWER(ISNULL(g.Code, N'') + N' ' + ISNULL(g.Name, N'')) NOT LIKE N'%организац%'
  );

-- Рабочие коды владельца: часть лежит в группе ADMIN, но нужна организации (не платформе).
DECLARE @WorkshopOwnerRole2 UNIQUEIDENTIFIER = 'AAEFDE24-DC8D-46EA-8A31-028CC44E41C7';
INSERT INTO dbo.RolePermissionsMap (RowId, RoleId, PermissionId)
SELECT NEWID(), @WorkshopOwnerRole2, p.RowId
FROM dbo.Permissions p
WHERE p.Code IN (
    N'VIEW_EMPLOYEES', N'EDIT_EMPLOYEES', N'CREATE_EMPLOYEES', N'DELETE_EMPLOYEES',
    N'PURCHASE_PARTS', N'VIEW_ANALYTICS', N'CONFIRM_WORKSHOP_BOOKING', N'MANAGE_WORKSHOP_SCHEDULE',
    N'VIEW_REPAIRS', N'EDIT_REPAIRS', N'CREATE_REPAIRS',
    N'VIEW_TASKS', N'EDIT_TASKS', N'CREATE_TASKS', N'DELETE_TASKS',
    N'VIEW_CARS', N'EDIT_CARS', N'DELETE_CARS',
    N'VIEW_SALES', N'CREATE_SALES', N'CREATE_ROLES')
  AND NOT EXISTS (
      SELECT 1 FROM dbo.RolePermissionsMap m
      WHERE m.RoleId = @WorkshopOwnerRole2 AND m.PermissionId = p.RowId);
GO
