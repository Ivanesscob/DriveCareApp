-- Справочник разрешений и связь с ролями (выполните, если таблиц ещё нет в БД).
IF OBJECT_ID(N'dbo.Permissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Permissions (
        RowId uniqueidentifier NOT NULL CONSTRAINT PK_Permissions PRIMARY KEY,
        Code nvarchar(100) NOT NULL,
        Name nvarchar(200) NOT NULL,
        Description nvarchar(500) NULL,
        Category nvarchar(100) NULL,
        CONSTRAINT UQ_Permissions_Code UNIQUE (Code)
    );
END
GO

IF OBJECT_ID(N'dbo.RolePermissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RolePermissions (
        RowId uniqueidentifier NOT NULL CONSTRAINT PK_RolePermissions PRIMARY KEY,
        RoleId uniqueidentifier NOT NULL,
        PermissionId uniqueidentifier NOT NULL,
        CONSTRAINT FK_RolePermissions_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (RowId),
        CONSTRAINT FK_RolePermissions_Permissions FOREIGN KEY (PermissionId) REFERENCES dbo.Permissions (RowId),
        CONSTRAINT UQ_RolePermissions_Role_Permission UNIQUE (RoleId, PermissionId)
    );
END
GO
