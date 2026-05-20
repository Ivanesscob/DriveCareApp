-- Тип мастерской: автосервис / покраска / шиномонтаж (связь Workshops.BusinessTypeId).
-- Выполнить на DriveCareDB.
-- Формат GUID: 8-4-4-4-12 hex (в последней группе ровно 12 символов).

IF OBJECT_ID(N'dbo.BusinessTypes', N'U') IS NULL
BEGIN
    RAISERROR(N'Таблица dbo.BusinessTypes не найдена.', 16, 1);
    RETURN;
END
GO

DECLARE @Auto  UNIQUEIDENTIFIER = 'DC010001-0001-4001-8001-000000000001';
DECLARE @Paint UNIQUEIDENTIFIER = 'DC010002-0002-4002-8002-000000000002';
DECLARE @Tire  UNIQUEIDENTIFIER = 'DC010003-0003-4003-8003-000000000003';

IF NOT EXISTS (SELECT 1 FROM dbo.BusinessTypes WHERE RowId = @Auto)
    INSERT INTO dbo.BusinessTypes (RowId, Name, Description)
    VALUES (@Auto, N'Автосервис', N'Ремонт и обслуживание автомобилей');

IF NOT EXISTS (SELECT 1 FROM dbo.BusinessTypes WHERE RowId = @Paint)
    INSERT INTO dbo.BusinessTypes (RowId, Name, Description)
    VALUES (@Paint, N'Покраска', N'Кузовной ремонт и покраска');

IF NOT EXISTS (SELECT 1 FROM dbo.BusinessTypes WHERE RowId = @Tire)
    INSERT INTO dbo.BusinessTypes (RowId, Name, Description)
    VALUES (@Tire, N'Шиномонтаж', N'Шины, диски, сезонное хранение');
GO

-- Пример: назначить тип существующим мастерским без типа
-- UPDATE dbo.Workshops
-- SET BusinessTypeId = 'DC010001-0001-4001-8001-000000000001'
-- WHERE BusinessTypeId IS NULL;
GO
