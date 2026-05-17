-- Примеры разрешений (по желанию).
IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE Code = N'cars.repair.view')
    INSERT INTO dbo.Permissions (RowId, Code, Name, Description, Category)
    VALUES (NEWID(), N'cars.repair.view', N'Просмотр авто в ремонте', N'Справочник ремонтов', N'Сервис');
IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE Code = N'employees.view')
    INSERT INTO dbo.Permissions (RowId, Code, Name, Description, Category)
    VALUES (NEWID(), N'employees.view', N'Просмотр сотрудников', N'Список сотрудников', N'Персонал');
IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE Code = N'moderation.carsales')
    INSERT INTO dbo.Permissions (RowId, Code, Name, Description, Category)
    VALUES (NEWID(), N'moderation.carsales', N'Модерация объявлений', N'Одобрение продаж авто', N'Автосалон');
GO
