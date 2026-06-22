/*
================================================================================
  DriveCareDB — полная очистка и наполнение тестовыми данными
  Выполнить в SSMS на базе DriveCareDB (одним запуском или по секциям).

  ВНИМАНИЕ: удаляет ВСЕ данные из всех пользовательских таблиц!

  Тестовые учётные записи (пароль в открытом виде, как в приложении):
  ┌──────────────────┬─────────────┬────────────────────────────────────────┐
  │ Логин            │ Пароль      │ Назначение                             │
  ├──────────────────┼─────────────┼────────────────────────────────────────┤
  │ admin            │ admin123    │ Администратор платформы (Pro)          │
  │ nord.owner       │ owner123    │ Владелец «Северный автосервис»         │
  │ paint.owner      │ owner123    │ Владелец «Краска Про»                  │
  │ tire.owner       │ owner123    │ Владелец «Колёса+»                     │
  │ purchaser.nord   │ purchase123 │ Закупщик Северного сервиса             │
  │ service.nord     │ service123  │ Мастер-приёмщик                        │
  │ client.ivanov    │ client123   │ Клиент DriveCare (приложение)          │
  │ client.petrova   │ client123   │ Клиент DriveCare                       │
  └──────────────────┴─────────────┴────────────────────────────────────────┘

  Создаёт: 120+ авто, 220+ запчастей (каталог), 60+ услуг, клиентов,
  сотрудников, ремонты, задания, заказы, отзывы, справочники, разрешения.

  Требования: база DriveCareDB со всеми таблицами и миграциями из
  DriveCareCore/Data/BD/Sql/ (WorkshopReviews, ServiceDocuments, Tasks_* и т.д.)
================================================================================
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

PRINT N'';
PRINT N'========================================';
PRINT N'  DriveCareDB — очистка и seed';
PRINT N'  ' + CONVERT(NVARCHAR(30), GETDATE(), 120);
PRINT N'========================================';
GO

-- ============================================================================
-- ЧАСТЬ 1. ОЧИСТКА ВСЕХ ДАННЫХ
-- ============================================================================
PRINT N'[1/3] Очистка таблиц...';

IF OBJECT_ID(N'dbo.Tasks', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.Tasks', N'ParentTaskId') IS NOT NULL
        EXEC(N'UPDATE dbo.Tasks SET ParentTaskId = NULL');
    IF COL_LENGTH(N'dbo.Tasks', N'DelegateTaskId') IS NOT NULL
        EXEC(N'UPDATE dbo.Tasks SET DelegateTaskId = NULL');
    IF COL_LENGTH(N'dbo.Tasks', N'DocumentId') IS NOT NULL
        EXEC(N'UPDATE dbo.Tasks SET DocumentId = NULL');
    IF COL_LENGTH(N'dbo.Tasks', N'RepairHistoryId') IS NOT NULL
        EXEC(N'UPDATE dbo.Tasks SET RepairHistoryId = NULL');
END

IF OBJECT_ID(N'dbo.WorkshopOnlineBookings', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.WorkshopOnlineBookings', N'TaskId') IS NOT NULL
        EXEC(N'UPDATE dbo.WorkshopOnlineBookings SET TaskId = NULL');
    IF COL_LENGTH(N'dbo.WorkshopOnlineBookings', N'RepairHistoryId') IS NOT NULL
        EXEC(N'UPDATE dbo.WorkshopOnlineBookings SET RepairHistoryId = NULL');
END

EXEC sp_MSforeachtable N'ALTER TABLE ? NOCHECK CONSTRAINT ALL';

DECLARE @sql NVARCHAR(MAX) = N'';
SELECT @sql = @sql + N'DELETE FROM ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N';' + CHAR(13)
FROM sys.tables
WHERE is_ms_shipped = 0 AND name <> N'sysdiagrams';
EXEC sp_executesql @sql;

EXEC sp_MSforeachtable N'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';

PRINT N'      Очистка завершена.';
GO

-- ============================================================================
-- ЧАСТЬ 2. СПРАВОЧНИКИ И РАЗРЕШЕНИЯ
-- ============================================================================
PRINT N'[2/3] Справочники, роли, разрешения...';

-- --- Страны ---
DECLARE @CountryRu UNIQUEIDENTIFIER = 'A1000001-0001-4001-8001-000000000001';
INSERT INTO dbo.Countries (RowId, Name, Code, Description)
VALUES (@CountryRu, N'Россия', N'RU', N'Российская Федерация');

-- --- Статусы ---
DECLARE @StTaskNew        UNIQUEIDENTIFIER = 'B1000001-0001-4001-8001-000000000001';
DECLARE @StTaskProgress   UNIQUEIDENTIFIER = 'B1000002-0002-4002-8002-000000000002';
DECLARE @StTaskDone       UNIQUEIDENTIFIER = 'B1000003-0003-4003-8003-000000000003';
DECLARE @StTaskCancelled  UNIQUEIDENTIFIER = 'B1000004-0004-4004-8004-000000000004';
DECLARE @StPartActive     UNIQUEIDENTIFIER = 'B1000005-0005-4005-8005-000000000005';
DECLARE @StPartModeration UNIQUEIDENTIFIER = '0393CC67-C871-42E8-8068-4BFCE4DAC8A4';
DECLARE @StSaleSent       UNIQUEIDENTIFIER = 'B1000006-0006-4006-8006-000000000006';
DECLARE @StSaleApproved   UNIQUEIDENTIFIER = 'B1000007-0007-4007-8007-000000000007';
DECLARE @StSaleReturned   UNIQUEIDENTIFIER = 'B1000008-0008-4008-8008-000000000008';
DECLARE @StRepairOpen     UNIQUEIDENTIFIER = 'B1000009-0009-4009-8009-000000000009';
DECLARE @StRepairClosed   UNIQUEIDENTIFIER = 'B1000010-0010-4010-8010-000000001010';

INSERT INTO dbo.Statuses (RowId, Name, Description) VALUES
(@StTaskNew,        N'Новое',                        N'Задание создано'),
(@StTaskProgress,   N'В работе',                     N'Задание выполняется'),
(@StTaskDone,       N'Завершено',                    N'Задание закрыто'),
(@StTaskCancelled,  N'Отменено',                     N'Задание отменено'),
(@StPartActive,     N'Активна',                      N'Запчасть в каталоге'),
(@StPartModeration, N'Ожидает модерации',            N'На проверке администратором'),
(@StSaleSent,       N'Отправлено на модерацию',      N'Объявление на модерации'),
(@StSaleApproved,   N'Одобрено модерацией',          N'Объявление опубликовано'),
(@StSaleReturned,   N'Возвращено на корректировку',  N'Требует правок'),
(@StRepairOpen,     N'В ремонте',                    N'Автомобиль в сервисе'),
(@StRepairClosed,   N'Ремонт завершён',              N'Работы выполнены');

-- --- Типы бизнеса ---
DECLARE @BtAuto  UNIQUEIDENTIFIER = 'DC010001-0001-4001-8001-000000000001';
DECLARE @BtPaint UNIQUEIDENTIFIER = 'DC010002-0002-4002-8002-000000000002';
DECLARE @BtTire  UNIQUEIDENTIFIER = 'DC010003-0003-4003-8003-000000000003';
INSERT INTO dbo.BusinessTypes (RowId, Name, Description) VALUES
(@BtAuto,  N'Автосервис',  N'Ремонт и обслуживание автомобилей'),
(@BtPaint, N'Покраска',    N'Кузовной ремонт и покраска'),
(@BtTire,  N'Шиномонтаж',  N'Шины, диски, сезонное хранение');

-- --- Марки и модели ---
DECLARE @Brands TABLE (RowId UNIQUEIDENTIFIER PRIMARY KEY, Name NVARCHAR(100));
INSERT INTO @Brands VALUES
('C1000001-0001-4001-8001-000000000001', N'Lada'),
('C1000002-0002-4002-8002-000000000002', N'Toyota'),
('C1000003-0003-4003-8003-000000000003', N'Volkswagen'),
('C1000004-0004-4004-8004-000000000004', N'Hyundai'),
('C1000005-0005-4005-8005-000000000005', N'Kia'),
('C1000006-0006-4006-8006-000000000006', N'BMW'),
('C1000007-0007-4007-8007-000000000007', N'Mercedes-Benz'),
('C1000008-0008-4008-8008-000000000008', N'Audi'),
('C1000009-0009-4009-8009-000000000009', N'Skoda'),
('C1000010-0010-4010-8010-000000001010', N'Renault');

INSERT INTO dbo.Brands (RowId, Name) SELECT RowId, Name FROM @Brands;

DECLARE @Models TABLE (RowId UNIQUEIDENTIFIER PRIMARY KEY, BrandId UNIQUEIDENTIFIER, Name NVARCHAR(100), HP INT, Vol FLOAT);
INSERT INTO @Models VALUES
('D1000001-0001-4001-8001-000000000001','C1000001-0001-4001-8001-000000000001',N'Vesta',106,1.6),
('D1000002-0002-4002-8002-000000000002','C1000001-0001-4001-8001-000000000001',N'Granta',90,1.6),
('D1000003-0003-4003-8003-000000000003','C1000001-0001-4001-8001-000000000001',N'Niva Travel',80,1.7),
('D1000004-0004-4004-8004-000000000004','C1000002-0002-4002-8002-000000000002',N'Camry',181,2.5),
('D1000005-0005-4005-8005-000000000005','C1000002-0002-4002-8002-000000000002',N'RAV4',199,2.5),
('D1000006-0006-4006-8006-000000000006','C1000002-0002-4002-8002-000000000002',N'Corolla',132,1.6),
('D1000007-0007-4007-8007-000000000007','C1000003-0003-4003-8003-000000000003',N'Polo',110,1.6),
('D1000008-0008-4008-8008-000000000008','C1000003-0003-4003-8003-000000000003',N'Tiguan',150,2.0),
('D1000009-0009-4009-8009-000000000009','C1000004-0004-4004-8004-000000000004',N'Solaris',123,1.6),
('D1000010-0010-4010-8010-000000001010','C1000004-0004-4004-8004-000000000004',N'Creta',150,2.0),
('D1000011-0011-4011-8011-000000001011','C1000005-0005-4005-8005-000000000005',N'Rio',123,1.6),
('D1000012-0012-4012-8012-000000001012','C1000005-0005-4005-8005-000000000005',N'Sportage',150,2.0),
('D1000013-0013-4013-8013-000000001013','C1000006-0006-4006-8006-000000000006',N'3 Series',184,2.0),
('D1000014-0014-4014-8014-000000001014','C1000006-0006-4006-8006-000000000006',N'X5',249,3.0),
('D1000015-0015-4015-8015-000000001015','C1000007-0007-4007-8007-000000000007',N'C-Class',204,2.0),
('D1000016-0016-4016-8016-000000001016','C1000008-0008-4008-8008-000000000008',N'A4',190,2.0),
('D1000017-0017-4017-8017-000000001017','C1000009-0009-4009-8009-000000000009',N'Octavia',150,1.4),
('D1000018-0018-4018-8018-000000001018','C1000010-0010-4010-8010-000000001010',N'Logan',102,1.6),
('D1000019-0019-4019-8019-000000001019','C1000010-0010-4010-8010-000000001010',N'Duster',143,1.6),
('D1000020-0020-4020-8020-000000002020','C1000003-0003-4003-8003-000000000003',N'Passat',150,1.8);

INSERT INTO dbo.Models (RowId, Name, BrandId, HorsePower, EngineVolume)
SELECT RowId, Name, BrandId, HP, Vol FROM @Models;

-- --- Типы авто, топливо, цвета ---
DECLARE @CtSedan UNIQUEIDENTIFIER = 'E1000001-0001-4001-8001-000000000001';
DECLARE @CtSuv   UNIQUEIDENTIFIER = 'E1000002-0002-4002-8002-000000000002';
DECLARE @CtHatch UNIQUEIDENTIFIER = 'E1000003-0003-4003-8003-000000000003';
DECLARE @CtWagon UNIQUEIDENTIFIER = 'E1000004-0004-4004-8004-000000000004';
INSERT INTO dbo.CarTypes (RowId, Name) VALUES
(@CtSedan, N'Седан'), (@CtSuv, N'Кроссовер'), (@CtHatch, N'Хэтчбек'), (@CtWagon, N'Универсал');

DECLARE @FtPetrol UNIQUEIDENTIFIER = 'F1000001-0001-4001-8001-000000000001';
DECLARE @FtDiesel UNIQUEIDENTIFIER = 'F1000002-0002-4002-8002-000000000002';
DECLARE @FtHybrid UNIQUEIDENTIFIER = 'F1000003-0003-4003-8003-000000000003';
INSERT INTO dbo.FuelTypes (RowId, Name) VALUES
(@FtPetrol, N'Бензин'), (@FtDiesel, N'Дизель'), (@FtHybrid, N'Гибрид');

DECLARE @Colors TABLE (RowId UNIQUEIDENTIFIER PRIMARY KEY, Name NVARCHAR(50));
INSERT INTO @Colors VALUES
('DC160001-0001-4001-8001-000000000001',N'Белый'),
('DC160002-0002-4002-8002-000000000002',N'Чёрный'),
('DC160003-0003-4003-8003-000000000003',N'Серебристый'),
('DC160004-0004-4004-8004-000000000004',N'Серый'),
('DC160005-0005-4005-8005-000000000005',N'Синий'),
('DC160006-0006-4006-8006-000000000006',N'Красный'),
('DC160007-0007-4007-8007-000000000007',N'Зелёный'),
('DC160008-0008-4008-8008-000000000008',N'Бежевый');
INSERT INTO dbo.Colors (RowId, Name) SELECT RowId, Name FROM @Colors;

-- --- Категории ремонта ---
DECLARE @RcTo    UNIQUEIDENTIFIER = 'DC170001-0001-4001-8001-000000000001';
DECLARE @RcEng   UNIQUEIDENTIFIER = 'DC170002-0002-4002-8002-000000000002';
DECLARE @RcSusp  UNIQUEIDENTIFIER = 'DC170003-0003-4003-8003-000000000003';
DECLARE @RcElec  UNIQUEIDENTIFIER = 'DC170004-0004-4004-8004-000000000004';
DECLARE @RcBody  UNIQUEIDENTIFIER = 'DC170005-0005-4005-8005-000000000005';
DECLARE @RcMaint UNIQUEIDENTIFIER = 'DC170006-0006-4006-8006-000000000006';
INSERT INTO dbo.RepairCategories (RowId, Name, Description) VALUES
(@RcTo,    N'ТО и обслуживание', N'Регламентные работы'),
(@RcEng,   N'Двигатель',         N'Ремонт силового агрегата'),
(@RcSusp,  N'Подвеска',          N'Ходовая часть'),
(@RcElec,  N'Электрика',         N'Электрооборудование'),
(@RcBody,  N'Кузовной ремонт',   N'Покраска и кузов'),
(@RcMaint, N'Диагностика',       N'Компьютерная диагностика');

-- --- Производители запчастей ---
DECLARE @PmBosch   UNIQUEIDENTIFIER = 'DC180001-0001-4001-8001-000000000001';
DECLARE @PmMann    UNIQUEIDENTIFIER = 'DC180002-0002-4002-8002-000000000002';
DECLARE @PmFebi    UNIQUEIDENTIFIER = 'DC180003-0003-4003-8003-000000000003';
DECLARE @PmLuk     UNIQUEIDENTIFIER = 'DC180004-0004-4004-8004-000000000004';
DECLARE @PmSachs   UNIQUEIDENTIFIER = 'DC180005-0005-4005-8005-000000000005';
DECLARE @PmNgnk    UNIQUEIDENTIFIER = 'DC180006-0006-4006-8006-000000000006';
DECLARE @PmValeo   UNIQUEIDENTIFIER = 'DC180007-0007-4007-8007-000000000007';
DECLARE @PmDenso   UNIQUEIDENTIFIER = 'DC180008-0008-4008-8008-000000000008';
INSERT INTO dbo.PartManufacturers (RowId, Name, CountryId) VALUES
(@PmBosch, N'Bosch', @CountryRu), (@PmMann, N'Mann-Filter', @CountryRu),
(@PmFebi,  N'Febi', @CountryRu),  (@PmLuk,  N'LUK', @CountryRu),
(@PmSachs, N'Sachs', @CountryRu), (@PmNgnk, N'NGK', @CountryRu),
(@PmValeo, N'Valeo', @CountryRu), (@PmDenso, N'Denso', @CountryRu);

-- --- Складские менеджеры ---
INSERT INTO dbo.WarehouseManagers (RowId, FirstName, LastName, MidName, Phone, Email) VALUES
('DC190001-0001-4001-8001-000000000001',N'Андрей',N'Смирнов',N'Петрович',N'+7 (921) 111-22-33',N'smirnov.andrey@drivecare.ru'),
('DC190002-0002-4002-8002-000000000002',N'Ольга',N'Козлова',N'Игоревна',N'+7 (921) 222-33-44',N'kozlova.olga@drivecare.ru');

-- --- Пункты выдачи (СПб) ---
INSERT INTO dbo.OrderPickupPoints (RowId, Code, Name, District, AddressLine, City, Latitude, Longitude, SortOrder, IsActive, CreatedAt) VALUES
('DC020001-0001-4001-8001-000000000001',N'vas-01',N'DriveCare · Васильевский',N'Василеостровский',N'наб. Макарова, д. 12',N'Санкт-Петербург',59.945,30.262,1,1,GETDATE()),
('DC020001-0001-4001-8001-000000000002',N'cen-01',N'DriveCare · Невский',N'Центральный',N'Невский проспект, д. 85',N'Санкт-Петербург',59.931,30.360,2,1,GETDATE()),
('DC020001-0001-4001-8001-000000000003',N'pet-01',N'DriveCare · Петроградка',N'Петроградский',N'Большой пр. П.С., д. 52',N'Санкт-Петербург',59.965,30.311,3,1,GETDATE()),
('DC020001-0001-4001-8001-000000000004',N'mos-01',N'DriveCare · Московский',N'Московский',N'Московский пр., д. 189',N'Санкт-Петербург',59.869,30.320,4,1,GETDATE()),
('DC020001-0001-4001-8001-000000000005',N'fru-01',N'DriveCare · Фрунзенский',N'Фрунзенский',N'ул. Бухарестская, д. 30',N'Санкт-Петербург',59.869,30.385,5,1,GETDATE());

-- --- Группы и разрешения (кнопки Pro) ---
DECLARE @GrpAdmin UNIQUEIDENTIFIER = 'DC1E0001-0001-4001-8001-000000000001';
DECLARE @GrpOrg   UNIQUEIDENTIFIER = 'DC1E0002-0002-4002-8002-000000000002';
DECLARE @GrpWs    UNIQUEIDENTIFIER = 'DC1E0003-0003-4003-8003-000000000003';
INSERT INTO dbo.PermissionGroups (RowId, Code, Name, Description) VALUES
(@GrpAdmin, N'ADMIN',      N'Администрирование платформы', N'Панель администратора DriveCare Pro'),
(@GrpOrg,   N'ORGANIZATION',N'Организация',              N'Управление сотрудниками и ролями'),
(@GrpWs,    N'WORKSPACE', N'Рабочая панель',              N'Сервис, задания, автомобили');

INSERT INTO dbo.Permissions (RowId, Code, Name, Description, PermissionGroupId) VALUES
('CEA68AB4-2741-4982-B729-166ED196BC9C',N'ADMIN_PANEL',              N'Панель администратора',        N'Организации, справочники, модерация',@GrpAdmin),
('32A54FDC-2A14-4C90-A715-0016DAE61E19',N'MODERATE_SALES',           N'Модерация объявлений',         N'Проверка продаж авто',@GrpAdmin),
('EF2671E5-6A8F-4CBE-85A7-187A94D47C21',N'VIEW_NOTIFICATIONS',       N'Уведомления платформы',        N'Системные уведомления',@GrpAdmin),
('5F25F657-D110-4344-9AAE-81AA1C346E1E',N'VIEW_EMPLOYEES',           N'Просмотр сотрудников',         NULL,@GrpOrg),
('45618BBC-E8F0-4F4C-A89C-80BA4998A67E',N'EDIT_EMPLOYEES',           N'Редактирование сотрудников',   NULL,@GrpOrg),
('663B84F9-7A73-4B24-9B2D-8D816ED6B229',N'CREATE_EMPLOYEES',         N'Добавление сотрудников',       NULL,@GrpOrg),
('41ABB304-77C5-41F6-9BDF-C0F982D856F7',N'DELETE_EMPLOYEES',         N'Удаление сотрудников',         NULL,@GrpOrg),
('82198DF6-1639-41BA-A4A9-1E4DAA3B2E26',N'CREATE_ROLES',             N'Создание ролей',               N'Конструктор ролей',@GrpOrg),
('A91C811F-360B-4849-A89B-C2E78516EA8A',N'VIEW_REPAIRS',             N'Просмотр ремонтов',            NULL,@GrpWs),
('9C173222-6BC6-4A5E-88AC-4A220B7CE3AF',N'EDIT_REPAIRS',             N'Редактирование ремонтов',      N'Услуги, покраска',@GrpWs),
('5039480B-D5CE-4A33-981D-CC678E38A9E8',N'CREATE_REPAIRS',           N'Запись на ремонт',             N'Онлайн-запись',@GrpWs),
('6D51012E-BEC0-41E5-841B-CF3437CC724B',N'VIEW_TASKS',               N'Просмотр заданий',             NULL,@GrpWs),
('28CFDDE0-2E89-4953-BB2F-05D31A80AC1B',N'EDIT_TASKS',               N'Редактирование заданий',       NULL,@GrpWs),
('6F8DF7A3-5CE6-4243-BBD5-E8060374ADDC',N'CREATE_TASKS',             N'Создание заданий',             NULL,@GrpWs),
('D22C667E-C639-4D20-BB08-5451AC04789C',N'DELETE_TASKS',             N'Удаление заданий',             NULL,@GrpWs),
('B74C53F4-DA47-4056-A17B-4F33D4FC2729',N'VIEW_CARS',                N'Просмотр автомобилей',         NULL,@GrpWs),
('28002FDA-0391-4846-8CC9-C880FE636E3C',N'EDIT_CARS',                N'Редактирование автомобилей',  NULL,@GrpWs),
('547361ED-FC2F-4E2B-A62D-4CE187ACA642',N'DELETE_CARS',              N'Удаление автомобилей',         NULL,@GrpWs),
('DFF0B938-22B4-40AE-8FC1-589020E77E1C',N'VIEW_SALES',               N'Просмотр продаж',              NULL,@GrpWs),
('49EEE6AE-148D-4EE2-BE97-577C80CC0D49',N'CREATE_SALES',             N'Создание продаж',              NULL,@GrpWs),
('03F24D58-7616-4E5E-8671-1484322AF40E',N'VIEW_ANALYTICS',           N'Просмотр статистики',          NULL,@GrpWs),
('B8E2F4A1-3C5D-4E9F-A2B1-7D8E9F0A1B2C',N'CONFIRM_WORKSHOP_BOOKING', N'Подтверждение онлайн-записей', NULL,@GrpWs),
('DC030001-0001-4001-8001-000000000001',N'MANAGE_WORKSHOP_SCHEDULE', N'Расписание мастерской',        NULL,@GrpWs),
('DC500004-0004-4004-8004-000000000004',N'PURCHASE_PARTS',           N'Закупка запчастей',            N'Магазин закупщика',@GrpWs);

-- --- Роли ---
DECLARE @RoleAdmin    UNIQUEIDENTIFIER = 'DC300001-0001-4001-8001-000000000001';
DECLARE @RoleOwner    UNIQUEIDENTIFIER = 'AAEFDE24-DC8D-46EA-8A31-028CC44E41C7';
DECLARE @RolePurchaser UNIQUEIDENTIFIER = 'DC300002-0002-4002-8002-000000000002';
DECLARE @RoleService  UNIQUEIDENTIFIER = 'DC300003-0003-4003-8003-000000000003';
DECLARE @RoleBooking  UNIQUEIDENTIFIER = 'DC020002-0002-4002-8002-000000000002';
INSERT INTO dbo.Roles (RowId, Name, Description, WorkshopId, IsActive, CompanyId) VALUES
(@RoleAdmin,    N'Администратор платформы', N'Полный доступ к панели администратора', NULL, 1, NULL),
(@RoleOwner,    N'Владелец автосервиса',    N'Управление организацией и сервисом',    NULL, 1, NULL),
(@RolePurchaser,N'Закупщик',                N'Закупка запчастей по заданиям',         NULL, 1, NULL),
(@RoleService,  N'Мастер-приёмщик',         N'Приёмка и ведение заданий',             NULL, 1, NULL),
(@RoleBooking,  N'Подтверждение записей',   N'Подтверждает онлайн-записи клиентов',   NULL, 1, NULL);

-- Привязка разрешений к ролям
INSERT INTO dbo.RolePermissionsMap (RowId, RoleId, PermissionId)
SELECT NEWID(), @RoleAdmin, p.RowId FROM dbo.Permissions p
WHERE p.Code IN (N'ADMIN_PANEL',N'MODERATE_SALES',N'VIEW_NOTIFICATIONS',N'VIEW_ANALYTICS');

INSERT INTO dbo.RolePermissionsMap (RowId, RoleId, PermissionId)
SELECT NEWID(), @RoleOwner, p.RowId FROM dbo.Permissions p
WHERE p.Code IN (
    N'VIEW_EMPLOYEES',N'EDIT_EMPLOYEES',N'CREATE_EMPLOYEES',N'DELETE_EMPLOYEES',N'CREATE_ROLES',
    N'VIEW_REPAIRS',N'EDIT_REPAIRS',N'CREATE_REPAIRS',N'VIEW_TASKS',N'EDIT_TASKS',N'CREATE_TASKS',N'DELETE_TASKS',
    N'VIEW_CARS',N'EDIT_CARS',N'DELETE_CARS',N'VIEW_SALES',N'CREATE_SALES',N'VIEW_ANALYTICS',
    N'PURCHASE_PARTS',N'CONFIRM_WORKSHOP_BOOKING',N'MANAGE_WORKSHOP_SCHEDULE');

INSERT INTO dbo.RolePermissionsMap (RowId, RoleId, PermissionId)
SELECT NEWID(), @RolePurchaser, p.RowId FROM dbo.Permissions p WHERE p.Code = N'PURCHASE_PARTS';

INSERT INTO dbo.RolePermissionsMap (RowId, RoleId, PermissionId)
SELECT NEWID(), @RoleService, p.RowId FROM dbo.Permissions p
WHERE p.Code IN (N'VIEW_TASKS',N'EDIT_TASKS',N'CREATE_TASKS',N'VIEW_REPAIRS',N'VIEW_CARS',N'CREATE_REPAIRS');

INSERT INTO dbo.RolePermissionsMap (RowId, RoleId, PermissionId)
SELECT NEWID(), @RoleBooking, p.RowId FROM dbo.Permissions p WHERE p.Code = N'CONFIRM_WORKSHOP_BOOKING';

GO

-- ============================================================================
-- ЧАСТЬ 3. ОРГАНИЗАЦИИ, ПОЛЬЗОВАТЕЛИ, АВТО, КАТАЛОГИ
-- ============================================================================
PRINT N'[3/3] Организации, пользователи, автомобили, каталоги, бизнес-данные...';

DECLARE @CountryRu2 UNIQUEIDENTIFIER = 'A1000001-0001-4001-8001-000000000001';
DECLARE @BtAuto2  UNIQUEIDENTIFIER = 'DC010001-0001-4001-8001-000000000001';
DECLARE @BtPaint2 UNIQUEIDENTIFIER = 'DC010002-0002-4002-8002-000000000002';
DECLARE @BtTire2  UNIQUEIDENTIFIER = 'DC010003-0003-4003-8003-000000000003';
DECLARE @RoleAdmin2    UNIQUEIDENTIFIER = 'DC300001-0001-4001-8001-000000000001';
DECLARE @RoleOwner2    UNIQUEIDENTIFIER = 'AAEFDE24-DC8D-46EA-8A31-028CC44E41C7';
DECLARE @RolePurchaser2 UNIQUEIDENTIFIER = 'DC300002-0002-4002-8002-000000000002';
DECLARE @RoleService2  UNIQUEIDENTIFIER = 'DC300003-0003-4003-8003-000000000003';
DECLARE @StPartActive2 UNIQUEIDENTIFIER = 'B1000005-0005-4005-8005-000000000005';
DECLARE @StTaskNew2    UNIQUEIDENTIFIER = 'B1000001-0001-4001-8001-000000000001';
DECLARE @StTaskProg2   UNIQUEIDENTIFIER = 'B1000002-0002-4002-8002-000000000002';
DECLARE @StTaskDone2   UNIQUEIDENTIFIER = 'B1000003-0003-4003-8003-000000000003';
DECLARE @StRepairOpen2 UNIQUEIDENTIFIER = 'B1000009-0009-4009-8009-000000000009';
DECLARE @StRepairDone2 UNIQUEIDENTIFIER = 'B1000010-0010-4010-8010-000000001010';
DECLARE @StSaleSent2   UNIQUEIDENTIFIER = 'B1000006-0006-4006-8006-000000000006';
DECLARE @StSaleAppr2   UNIQUEIDENTIFIER = 'B1000007-0007-4007-8007-000000000007';
DECLARE @CtSedan2 UNIQUEIDENTIFIER = 'E1000001-0001-4001-8001-000000000001';
DECLARE @CtSuv2   UNIQUEIDENTIFIER = 'E1000002-0002-4002-8002-000000000002';
DECLARE @CtHatch2 UNIQUEIDENTIFIER = 'E1000003-0003-4003-8003-000000000003';
DECLARE @FtPetrol2 UNIQUEIDENTIFIER = 'F1000001-0001-4001-8001-000000000001';
DECLARE @FtDiesel2 UNIQUEIDENTIFIER = 'F1000002-0002-4002-8002-000000000002';
DECLARE @RcTo2    UNIQUEIDENTIFIER = 'DC170001-0001-4001-8001-000000000001';
DECLARE @RcEng2   UNIQUEIDENTIFIER = 'DC170002-0002-4002-8002-000000000002';
DECLARE @RcSusp2  UNIQUEIDENTIFIER = 'DC170003-0003-4003-8003-000000000003';
DECLARE @RcBody2  UNIQUEIDENTIFIER = 'DC170005-0005-4005-8005-000000000005';
DECLARE @PmBosch2 UNIQUEIDENTIFIER = 'DC180001-0001-4001-8001-000000000001';

-- --- Адреса и компании ---
DECLARE @AddrNord  UNIQUEIDENTIFIER = 'DC1A0001-0001-4001-8001-000000000001';
DECLARE @AddrPaint UNIQUEIDENTIFIER = 'DC1A0002-0002-4002-8002-000000000002';
DECLARE @AddrTire  UNIQUEIDENTIFIER = 'DC1A0003-0003-4003-8003-000000000003';
DECLARE @CoNord  UNIQUEIDENTIFIER = 'DC1B0001-0001-4001-8001-000000000001';
DECLARE @CoPaint UNIQUEIDENTIFIER = 'DC1B0002-0002-4002-8002-000000000002';
DECLARE @CoTire  UNIQUEIDENTIFIER = 'DC1B0003-0003-4003-8003-000000000003';
DECLARE @WsNord  UNIQUEIDENTIFIER = 'DC1C0001-0001-4001-8001-000000000001';
DECLARE @WsPaint UNIQUEIDENTIFIER = 'DC1C0002-0002-4002-8002-000000000002';
DECLARE @WsTire  UNIQUEIDENTIFIER = 'DC1C0003-0003-4003-8003-000000000003';

INSERT INTO dbo.Addresses (RowId, CountryId, City, Street, House, Apartment, Latitude, Longitude) VALUES
(@AddrNord,  @CountryRu2, N'Санкт-Петербург', N'ул. Савушкина',      N'112', N'оф. 4', 59.987, 30.210),
(@AddrPaint, @CountryRu2, N'Санкт-Петербург', N'пр. Энгельса',       N'154', NULL,     60.015, 30.335),
(@AddrTire,  @CountryRu2, N'Санкт-Петербург', N'Московский пр.',     N'205', N'корп. 2',59.865, 30.318);

INSERT INTO dbo.Companies (RowId, Name, Description) VALUES
(@CoNord,  N'ООО «СеверАвто»',   N'Сеть автосервисов на севере города'),
(@CoPaint, N'ООО «Краска Про»',  N'Кузовной ремонт и покраска'),
(@CoTire,  N'ООО «Колёса+»',     N'Шиномонтаж и хранение шин');

INSERT INTO dbo.Workshops (RowId, Name, CompanyId, AddressId, Description, BusinessTypeId) VALUES
(@WsNord,  N'Северный автосервис', @CoNord,  @AddrNord,  N'Полный цикл ТО и ремонта', @BtAuto2),
(@WsPaint, N'Краска Про',          @CoPaint, @AddrPaint, N'Покраска и кузовной ремонт',@BtPaint2),
(@WsTire,  N'Колёса+',             @CoTire,  @AddrTire,  N'Шиномонтаж 24/7',           @BtTire2);

INSERT INTO dbo.WorkshopBusinessTypes (RowId, WorkshopId, BusinessTypeId) VALUES
(NEWID(),@WsNord, @BtAuto2), (NEWID(),@WsPaint,@BtPaint2), (NEWID(),@WsTire, @BtTire2);

-- Расписание (пн–сб 9–20, вс выходной)
DECLARE @d TINYINT = 1;
WHILE @d <= 7
BEGIN
    INSERT INTO dbo.WorkshopWorkSchedules (RowId, WorkshopId, DayOfWeek, IsClosed, OpenTime, CloseTime)
    SELECT NEWID(), w.RowId, @d, CASE WHEN @d = 7 THEN 1 ELSE 0 END,
           CASE WHEN @d = 7 THEN NULL ELSE CAST('09:00' AS TIME) END,
           CASE WHEN @d = 7 THEN NULL ELSE CAST('20:00' AS TIME) END
    FROM (VALUES (@WsNord),(@WsPaint),(@WsTire)) w(RowId);
    SET @d += 1;
END

IF OBJECT_ID(N'dbo.WorkshopOnlineBookingSettings', N'U') IS NOT NULL
    INSERT INTO dbo.WorkshopOnlineBookingSettings (WorkshopId, MaxBookingsPerDay) VALUES
    (@WsNord, 12), (@WsPaint, 8), (@WsTire, 15);

-- --- Сотрудники ---
DECLARE @EmpAdmin UNIQUEIDENTIFIER = 'DC1D0001-0001-4001-8001-000000000001';
DECLARE @EmpNordOwner UNIQUEIDENTIFIER = 'DC1D0002-0002-4002-8002-000000000002';
DECLARE @EmpPaintOwner UNIQUEIDENTIFIER = 'DC1D0003-0003-4003-8003-000000000003';
DECLARE @EmpTireOwner UNIQUEIDENTIFIER = 'DC1D0004-0004-4004-8004-000000000004';
DECLARE @EmpPurchaser UNIQUEIDENTIFIER = 'DC1D0005-0005-4005-8005-000000000005';
DECLARE @EmpService UNIQUEIDENTIFIER = 'DC1D0006-0006-4006-8006-000000000006';
DECLARE @EmpMech1 UNIQUEIDENTIFIER = 'DC1D0007-0007-4007-8007-000000000007';
DECLARE @EmpMech2 UNIQUEIDENTIFIER = 'DC1D0008-0008-4008-8008-000000000008';

INSERT INTO dbo.Employees (RowId, FirstName, LastName, MidName, Login, Password, Email, Phone, BirthDate, HireDate, IsActive, WorkshopId) VALUES
(@EmpAdmin,      N'Дмитрий',N'Админов',    N'Сергеевич', N'admin',         N'admin123',    N'dmitry.adminov@drivecare.ru',    N'+7 (812) 100-00-01','1985-03-15',DATEADD(YEAR,-3,GETDATE()),1,NULL),
(@EmpNordOwner,  N'Алексей',N'Северов',    N'Иванович',  N'nord.owner',    N'owner123',    N'aleksey.severov@severauto.ru',   N'+7 (812) 200-10-20','1980-07-22',DATEADD(YEAR,-5,GETDATE()),1,@WsNord),
(@EmpPaintOwner, N'Марина', N'Краскина',   N'Петровна',  N'paint.owner',   N'owner123',    N'marina.kraskina@kraskapro.ru',   N'+7 (812) 200-20-30','1988-11-08',DATEADD(YEAR,-4,GETDATE()),1,@WsPaint),
(@EmpTireOwner,  N'Игорь',  N'Колесов',    N'Андреевич', N'tire.owner',    N'owner123',    N'igor.kolesov@kolesa-plus.ru',    N'+7 (812) 200-30-40','1979-01-30',DATEADD(YEAR,-6,GETDATE()),1,@WsTire),
(@EmpPurchaser,  N'Павел',  N'Снабженцев', N'Олегович',  N'purchaser.nord',N'purchase123', N'pavel.snab@severauto.ru',        N'+7 (921) 333-44-55','1990-05-12',DATEADD(YEAR,-2,GETDATE()),1,@WsNord),
(@EmpService,    N'Елена',  N'Приёмкина',  N'Викторовна',N'service.nord',  N'service123',  N'elena.priem@severauto.ru',       N'+7 (921) 444-55-66','1992-09-03',DATEADD(YEAR,-1,GETDATE()),1,@WsNord),
(@EmpMech1,      N'Сергей', N'Моторин',    N'Николаевич',N'mech.nord1',    N'mech123',     N'sergey.motorin@severauto.ru',    N'+7 (921) 555-66-77','1987-12-18',DATEADD(MONTH,-8,GETDATE()),1,@WsNord),
(@EmpMech2,      N'Андрей', N'Токарев',    N'Владимирович',N'mech.nord2',  N'mech123',     N'andrey.tokarev@severauto.ru',    N'+7 (921) 666-77-88','1991-04-25',DATEADD(MONTH,-6,GETDATE()),1,@WsNord);

INSERT INTO dbo.EmployeeRolesMap (RowId, EmployeeId, RoleId) VALUES
(NEWID(),@EmpAdmin,      @RoleAdmin2),
(NEWID(),@EmpNordOwner,  @RoleOwner2),
(NEWID(),@EmpPaintOwner, @RoleOwner2),
(NEWID(),@EmpTireOwner,  @RoleOwner2),
(NEWID(),@EmpPurchaser,  @RolePurchaser2),
(NEWID(),@EmpService,    @RoleService2),
(NEWID(),@EmpMech1,      @RoleService2),
(NEWID(),@EmpMech2,      @RoleService2);

-- Обновить CompanyId у ролей организаций
UPDATE dbo.Roles SET CompanyId = @CoNord  WHERE RowId IN (@RolePurchaser2,@RoleService2);

-- --- Клиенты (Users) ---
DECLARE @Users TABLE (
    RowId UNIQUEIDENTIFIER PRIMARY KEY, Login NVARCHAR(100), Email NVARCHAR(100),
    Phone NVARCHAR(50), Fn NVARCHAR(50), Ln NVARCHAR(50)
);
INSERT INTO @Users VALUES
('DC1F0001-0001-4001-8001-000000000001',N'client.ivanov',  N'ivanov.alexandr@gmail.com',  N'+7 (921) 100-01-01',N'Александр',N'Иванов'),
('DC1F0002-0002-4002-8002-000000000002',N'client.petrova', N'petrova.maria@mail.ru',      N'+7 (921) 100-02-02',N'Мария',    N'Петрова'),
('DC1F0003-0003-4003-8003-000000000003',N'client.sidorov', N'sidorov.dmitry@yandex.ru',   N'+7 (921) 100-03-03',N'Дмитрий',  N'Сидоров'),
('DC1F0004-0004-4004-8004-000000000004',N'client.kozlov',  N'kozlov.sergey@gmail.com',    N'+7 (921) 100-04-04',N'Сергей',   N'Козлов'),
('DC1F0005-0005-4005-8005-000000000005',N'client.volkova', N'volkova.anna@inbox.ru',      N'+7 (921) 100-05-05',N'Анна',     N'Волкова');

-- +45 клиентов
DECLARE @ui INT = 6;
DECLARE @uid UNIQUEIDENTIFIER;
DECLARE @fn NVARCHAR(50);
DECLARE @ln NVARCHAR(50);
DECLARE @em NVARCHAR(100);
WHILE @ui <= 50
BEGIN
    SET @uid = NEWID();
    SET @fn = CASE (@ui % 10)
        WHEN 0 THEN N'Максим' WHEN 1 THEN N'Артём' WHEN 2 THEN N'Кирилл' WHEN 3 THEN N'Никита'
        WHEN 4 THEN N'Ольга'  WHEN 5 THEN N'Татьяна' WHEN 6 THEN N'Ирина' WHEN 7 THEN N'Екатерина'
        WHEN 8 THEN N'Светлана' ELSE N'Юлия' END;
    SET @ln = CASE (@ui % 12)
        WHEN 0 THEN N'Смирнов' WHEN 1 THEN N'Кузнецов' WHEN 2 THEN N'Попов' WHEN 3 THEN N'Васильев'
        WHEN 4 THEN N'Соколов' WHEN 5 THEN N'Михайлов' WHEN 6 THEN N'Новиков' WHEN 7 THEN N'Фёдоров'
        WHEN 8 THEN N'Морозов' WHEN 9 THEN N'Волков' WHEN 10 THEN N'Алексеев' ELSE N'Лебедев' END;
    SET @em = LOWER(REPLACE(@ln,N'ё',N'e')) + N'.' + LOWER(@fn) + N'@mail.ru';
    INSERT INTO @Users VALUES (@uid, N'client' + CAST(@ui AS NVARCHAR(10)), @em,
        N'+7 (921) ' + RIGHT('000'+CAST(100+@ui AS NVARCHAR(10)),3) + N'-'
        + RIGHT('00'+CAST(@ui AS NVARCHAR(10)),2) + N'-' + RIGHT('00'+CAST(@ui AS NVARCHAR(10)),2),
        @fn, @ln);
    SET @ui += 1;
END

INSERT INTO dbo.Users (RowId, Login, Password, Email, Phone, BirthDate, CreatedAt, Description)
SELECT RowId, Login, N'client123', Email, Phone,
       DATEADD(YEAR, -25 - (ABS(CHECKSUM(RowId)) % 20), GETDATE()),
       DATEADD(DAY, -(ABS(CHECKSUM(RowId)) % 365), GETDATE()),
       N'Клиент DriveCare: ' + Ln + N' ' + Fn
FROM @Users;

-- --- 120 автомобилей ---
DECLARE @ModelIds TABLE (Idx INT IDENTITY(1,1), RowId UNIQUEIDENTIFIER);
INSERT INTO @ModelIds SELECT RowId FROM dbo.Models;

DECLARE @ColorIds TABLE (Idx INT IDENTITY(1,1), RowId UNIQUEIDENTIFIER);
INSERT INTO @ColorIds SELECT RowId FROM dbo.Colors;

DECLARE @Cars TABLE (Idx INT IDENTITY(1,1) PRIMARY KEY, RowId UNIQUEIDENTIFIER, ColorId UNIQUEIDENTIFIER);
DECLARE @ci INT = 1;
DECLARE @carId UNIQUEIDENTIFIER;
DECLARE @mid UNIQUEIDENTIFIER;
DECLARE @colId UNIQUEIDENTIFIER;
DECLARE @yr INT;
DECLARE @ct UNIQUEIDENTIFIER;
DECLARE @ft UNIQUEIDENTIFIER;
DECLARE @vin NVARCHAR(50);
DECLARE @plate NVARCHAR(20);
WHILE @ci <= 120
BEGIN
    SET @carId = NEWID();
    SET @mid = (SELECT RowId FROM @ModelIds WHERE Idx = ((@ci - 1) % (SELECT COUNT(*) FROM @ModelIds)) + 1);
    SET @colId = (SELECT RowId FROM @ColorIds WHERE Idx = ((@ci - 1) % (SELECT COUNT(*) FROM @ColorIds)) + 1);
    SET @yr = 2012 + (@ci % 13);
    SET @ct = CASE @ci % 4 WHEN 0 THEN @CtSuv2 WHEN 1 THEN @CtSedan2 WHEN 2 THEN @CtHatch2 ELSE @CtSedan2 END;
    SET @ft = CASE WHEN @ci % 7 = 0 THEN @FtDiesel2 ELSE @FtPetrol2 END;
    SET @vin = UPPER(CONCAT(
        CHAR(65+(@ci%26)), CHAR(66+((@ci*3)%26)), CHAR(67+((@ci*5)%26)),
        RIGHT('000000'+CAST(@ci AS VARCHAR(10)),6),
        CHAR(68+((@ci*7)%26)), CHAR(69+((@ci*11)%26)),
        RIGHT('00000'+CAST(@yr AS VARCHAR(10)),4),
        CHAR(70+((@ci*13)%26))));
    SET @plate = N'А' + RIGHT('000'+CAST(100+@ci AS NVARCHAR(10)),3)
        + N'ВС' + RIGHT('0'+CAST(78+(@ci%5) AS NVARCHAR(2)),2);
    INSERT INTO dbo.Cars (RowId, ModelId, CarTypeId, FuelTypeId, Year, Vin, PlateNumber, Description)
    VALUES (@carId, @mid, @ct, @ft, @yr, @vin, @plate, N'Тестовый автомобиль #' + CAST(@ci AS NVARCHAR(10)));
    INSERT INTO @Cars (RowId, ColorId) VALUES (@carId, @colId);
    INSERT INTO dbo.CarColors (RowId, CarId, ColorId, StartDate)
    VALUES (NEWID(), @carId, @colId, DATEFROMPARTS(@yr, 1, 1));
    SET @ci += 1;
END

-- Привязка авто к клиентам (у каждого 1–3 авто, без повторного использования одного CarId)
DECLARE @uc INT = 0;
DECLARE @uRowId UNIQUEIDENTIFIER, @uLogin NVARCHAR(100);
DECLARE @carsPerUser INT;
DECLARE @j INT;
DECLARE @carIdx INT;
DECLARE @linkCar UNIQUEIDENTIFIER;
DECLARE @nextCarIdx INT = 1;
DECLARE curU CURSOR LOCAL FAST_FORWARD FOR SELECT RowId, Login FROM @Users ORDER BY Login;
OPEN curU;
FETCH NEXT FROM curU INTO @uRowId, @uLogin;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @uc += 1;
    SET @carsPerUser = 1 + (@uc % 3);
    SET @j = 0;
    WHILE @j < @carsPerUser AND @nextCarIdx <= 120
    BEGIN
        SET @carIdx = @nextCarIdx;
        SET @nextCarIdx += 1;
        SET @linkCar = (SELECT RowId FROM @Cars WHERE Idx = @carIdx);
        IF NOT EXISTS (SELECT 1 FROM dbo.UserCars WHERE UserId = @uRowId AND CarId = @linkCar)
            INSERT INTO dbo.UserCars (RowId, UserId, CarId, Description)
            VALUES (NEWID(), @uRowId, @linkCar, N'Личный автомобиль');
        SET @j += 1;
    END
    FETCH NEXT FROM curU INTO @uRowId, @uLogin;
END
CLOSE curU; DEALLOCATE curU;

-- --- 220+ запчастей (глобальный каталог Parts) ---
DECLARE @PartNames TABLE (Idx INT IDENTITY(1,1), Cat NVARCHAR(40), Name NVARCHAR(200));
INSERT INTO @PartNames (Cat, Name) VALUES
(N'Фильтры',N'Масляный фильтр'),(N'Фильтры',N'Воздушный фильтр'),(N'Фильтры',N'Салонный фильтр'),(N'Фильтры',N'Топливный фильтр'),
(N'Тормоза',N'Колодки передние'),(N'Тормоза',N'Колодки задние'),(N'Тормоза',N'Диск тормозной передний'),(N'Тормоза',N'Диск тормозной задний'),
(N'Тормоза',N'Суппорт передний'),(N'Тормоза',N'Тормозной шланг'),(N'Тормоза',N'Тормозная жидкость DOT-4'),
(N'Подвеска',N'Амортизатор передний'),(N'Подвеска',N'Амортизатор задний'),(N'Подвеска',N'Пружина передняя'),(N'Подвеска',N'Сайлентблок рычага'),
(N'Подвеска',N'Шаровая опора'),(N'Подвеска',N'Рулевой наконечник'),(N'Подвеска',N'Стойка стабилизатора'),(N'Подвеска',N'Опора амортизатора'),
(N'Двигатель',N'Ремень ГРМ'),(N'Двигатель',N'Ролик натяжителя'),(N'Двигатель',N'Помпа водяная'),(N'Двигатель',N'Термостат'),
(N'Двигатель',N'Свеча зажигания'),(N'Двигатель',N'Катушка зажигания'),(N'Двигатель',N'Прокладка ГБЦ'),(N'Двигатель',N'Маслосъёмные колпачки'),
(N'Электрика',N'Аккумулятор 60 А·ч'),(N'Электрика',N'Генератор'),(N'Электрика',N'Стартер'),(N'Электрика',N'Лампа H7'),
(N'Электрика',N'Датчик кислорода'),(N'Электрика',N'Датчик ABS'),(N'Расходники',N'Моторное масло 5W-30'),(N'Расходники',N'Моторное масло 5W-40'),
(N'Расходники',N'Антифриз G12'),(N'Расходники',N'Омыватель стекла'),(N'Кузов',N'Бампер передний'),(N'Кузов',N'Крыло переднее'),
(N'Кузов',N'Фара левая'),(N'Кузов',N'Фара правая'),(N'Кузов',N'Зеркало боковое'),(N'Кузов',N'Решётка радиатора'),
(N'Трансмиссия',N'Сцепление комплект'),(N'Трансмиссия',N'Выжимной подшипник'),(N'Трансмиссия',N'Маховик'),(N'Трансмиссия',N'Приводной вал'),
(N'Трансмиссия',N'Пыльник ШРУС'),(N'Трансмиссия',N'Сальник КПП'),(N'Шины',N'Шина 195/65 R15'),(N'Шины',N'Шина 205/55 R16'),
(N'Шины',N'Шина 225/45 R17'),(N'Шины',N'Диск литой R16'),(N'Шины',N'Диск штампованный R15');

DECLARE @pmIds TABLE (Idx INT IDENTITY(1,1), RowId UNIQUEIDENTIFIER);
INSERT INTO @pmIds SELECT RowId FROM dbo.PartManufacturers;
DECLARE @pi INT = 1;
DECLARE @pName NVARCHAR(200);
DECLARE @pCat NVARCHAR(40);
DECLARE @pmId UNIQUEIDENTIFIER;
WHILE @pi <= 220
BEGIN
    SET @pName = (SELECT Name FROM @PartNames WHERE Idx = ((@pi-1) % (SELECT COUNT(*) FROM @PartNames)) + 1);
    SET @pCat = (SELECT Cat FROM @PartNames WHERE Idx = ((@pi-1) % (SELECT COUNT(*) FROM @PartNames)) + 1);
    SET @pmId = (SELECT RowId FROM @pmIds WHERE Idx = ((@pi-1) % (SELECT COUNT(*) FROM @pmIds)) + 1);
    INSERT INTO dbo.Parts (RowId, Name, Article, PartManufacturerId, StatusId, Description)
    VALUES (NEWID(), @pName + N' #' + CAST(@pi AS NVARCHAR(10)),
            N'DC-' + RIGHT('000000'+CAST(@pi AS VARCHAR(10)),6), @pmId, @StPartActive2,
            N'Категория: ' + @pCat);
    SET @pi += 1;
END

-- --- Единицы и услуги мастерских (60+ на Северном + по 15 на остальных) ---
DECLARE @SvcNames TABLE (Idx INT IDENTITY(1,1), Name NVARCHAR(300), Price DECIMAL(18,2));
INSERT INTO @SvcNames (Name, Price) VALUES
(N'Замена масла двигателя',2500),(N'Замена масляного фильтра',800),(N'Замена воздушного фильтра',600),
(N'Замена салонного фильтра',700),(N'Замена свечей зажигания',1200),(N'Диагностика ходовой части',1500),
(N'Сход-развал 3D',3500),(N'Замена передних колодок',2800),(N'Замена задних колодок',2200),
(N'Замена передних дисков',4500),(N'Прокачка тормозной системы',1800),(N'Замена тормозной жидкости',2000),
(N'Замена амортизатора (1 сторона)',3500),(N'Замена сайлентблоков рычага',4200),(N'Замена шаровой опоры',2800),
(N'Замена рулевого наконечника',2200),(N'Замена ремня ГРМ',12000),(N'Замена помпы',8500),
(N'Замена термостата',3500),(N'Замена генератора',7500),(N'Замена стартера',6500),
(N'Замена аккумулятора',500),(N'Замена лампы ближнего света',400),(N'Компьютерная диагностика',2000),
(N'Чистка форсунок',4500),(N'Промывка инжектора',3500),(N'Замена антифриза',2500),
(N'Замена сцепления',18000),(N'Замена приводного вала',7500),(N'Замена пыльника ШРУС',2800),
(N'Шиномонтаж 1 колеса R15',600),(N'Шиномонтаж 1 колеса R16',700),(N'Шиномонтаж 1 колеса R17',800),
(N'Балансировка 1 колеса',500),(N'Ремонт прокола',900),(N'Сезонное хранение шин (комплект)',4000),
(N'Покраска бампера',15000),(N'Покраска крыла',12000),(N'Покраска двери',14000),
(N'Покраска капота',18000),(N'Локальная покраска',8000),(N'Полировка кузова',6000),
(N'Антикоррозийная обработка днища',12000),(N'Замена лобового стекла',8000),(N'Тонировка задних стёкол',5000),
(N'Установка сигнализации',6000),(N'Установка парктроников',4500),(N'Замена ремня генератора',1500),
(N'Замена ролика натяжителя',2200),(N'Замена топливного фильтра',1800),(N'Замена катушки зажигания',3500),
(N'Замена датчика кислорода',4200),(N'Замена датчика ABS',5500),(N'Замена опоры двигателя',4500),
(N'Замена втулок стабилизатора',1800),(N'Замена подшипника ступицы',5500),(N'Замена сальника КПП',6500),
(N'Замена маховика',14000),(N'Регулировка фар',800),(N'Заправка кондиционера',3500);

DECLARE @UnitNord UNIQUEIDENTIFIER = NEWID();
DECLARE @UnitPaint UNIQUEIDENTIFIER = NEWID();
DECLARE @UnitTire UNIQUEIDENTIFIER = NEWID();
INSERT INTO dbo.WorkshopServiceUnits (RowId, WorkshopId, Name, IsActive, CreatedAt) VALUES
(@UnitNord, @WsNord, N'усл.', 1, GETDATE()),
(@UnitPaint, @WsPaint, N'усл.', 1, GETDATE()),
(@UnitTire, @WsTire, N'усл.', 1, GETDATE());

DECLARE @si INT = 1;
DECLARE @sName NVARCHAR(300);
DECLARE @sPrice DECIMAL(18,2);
WHILE @si <= (SELECT COUNT(*) FROM @SvcNames)
BEGIN
    SET @sName = (SELECT Name FROM @SvcNames WHERE Idx = @si);
    SET @sPrice = (SELECT Price FROM @SvcNames WHERE Idx = @si);
    INSERT INTO dbo.WorkshopServices (RowId, WorkshopId, Name, Description, Price, UnitName, UnitId, IsActive, SortOrder, CreatedAt)
    VALUES (NEWID(), @WsNord, @sName, N'Стандартная услуга сервиса', @sPrice, N'усл.', @UnitNord, 1, @si, GETDATE());
    IF @si <= 15
    BEGIN
        INSERT INTO dbo.WorkshopServices (RowId, WorkshopId, Name, Description, Price, UnitName, UnitId, IsActive, SortOrder, CreatedAt)
        VALUES (NEWID(), @WsPaint, @sName, N'Услуга покрасочного цеха', @sPrice * 1.2, N'усл.', @UnitPaint, 1, @si, GETDATE());
        INSERT INTO dbo.WorkshopServices (RowId, WorkshopId, Name, Description, Price, UnitName, UnitId, IsActive, SortOrder, CreatedAt)
        VALUES (NEWID(), @WsTire, @sName, N'Услуга шиномонтажа', @sPrice * 0.8, N'усл.', @UnitTire, 1, @si, GETDATE());
    END
    SET @si += 1;
END

-- Склад мастерской (WorkshopParts) — по 70 позиций на каждую мастерскую
DECLARE @wi UNIQUEIDENTIFIER;
DECLARE @wpName NVARCHAR(300);
DECLARE @wpCat NVARCHAR(40);
DECLARE curW CURSOR LOCAL FAST_FORWARD FOR SELECT RowId FROM (VALUES (@WsNord),(@WsPaint),(@WsTire)) w(RowId);
OPEN curW; FETCH NEXT FROM curW INTO @wi;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @pi = 1;
    WHILE @pi <= 70
    BEGIN
        SET @wpName = (SELECT Name FROM @PartNames WHERE Idx = ((@pi-1) % (SELECT COUNT(*) FROM @PartNames)) + 1);
        SET @wpCat = (SELECT Cat FROM @PartNames WHERE Idx = ((@pi-1) % (SELECT COUNT(*) FROM @PartNames)) + 1);
        INSERT INTO dbo.WorkshopParts (RowId, WorkshopId, Name, Article, Description, Price, UnitName, QuantityOnHand, Category, IsActive, CreatedAt)
        VALUES (NEWID(), @wi, @wpName, N'WS-' + RIGHT('0000'+CAST(@pi AS VARCHAR(10)),4),
                N'Складская позиция', 500 + (@pi * 37) % 15000, N'шт.', 5 + (@pi % 50), @wpCat, 1, GETDATE());
        SET @pi += 1;
    END
    FETCH NEXT FROM curW INTO @wi;
END
CLOSE curW; DEALLOCATE curW;

-- Покрасочные услуги и цвета
IF OBJECT_ID(N'dbo.WorkshopPaintServices', N'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.WorkshopPaintServices (RowId, WorkshopId, PaintKind, Name, Description, PriceFrom, IsActive, SortOrder, CreatedAt) VALUES
    (NEWID(),@WsPaint,1,N'Полная покраска кузова',N'С полной разборкой',120000,1,1,SYSUTCDATETIME()),
    (NEWID(),@WsPaint,2,N'Локальная покраска',N'1 элемент',8000,1,2,SYSUTCDATETIME()),
    (NEWID(),@WsPaint,3,N'Покраска бампера',N'С подготовкой',15000,1,3,SYSUTCDATETIME());
END
IF OBJECT_ID(N'dbo.WorkshopPaintColors', N'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.WorkshopPaintColors (RowId, WorkshopId, ColorId, ColorName, IsActive, SortOrder, CreatedAt)
    SELECT NEWID(), @WsPaint, c.RowId, c.Name, 1, ROW_NUMBER() OVER (ORDER BY c.Name), SYSUTCDATETIME()
    FROM dbo.Colors c;
END

GO

-- ============================================================================
-- ЧАСТЬ 4. БИЗНЕС-ДАННЫЕ: ремонты, задания, заказы, отзывы
-- ============================================================================
PRINT N'[4/4] Ремонты, задания, заказы, отзывы...';

DECLARE @WsNord3  UNIQUEIDENTIFIER = 'DC1C0001-0001-4001-8001-000000000001';
DECLARE @WsPaint3 UNIQUEIDENTIFIER = 'DC1C0002-0002-4002-8002-000000000002';
DECLARE @EmpService3 UNIQUEIDENTIFIER = 'DC1D0006-0006-4006-8006-000000000006';
DECLARE @EmpMech1_3 UNIQUEIDENTIFIER = 'DC1D0007-0007-4007-8007-000000000007';
DECLARE @EmpAdmin3 UNIQUEIDENTIFIER = 'DC1D0001-0001-4001-8001-000000000001';
DECLARE @StTaskNew3    UNIQUEIDENTIFIER = 'B1000001-0001-4001-8001-000000000001';
DECLARE @StTaskProg3   UNIQUEIDENTIFIER = 'B1000002-0002-4002-8002-000000000002';
DECLARE @StTaskDone3   UNIQUEIDENTIFIER = 'B1000003-0003-4003-8003-000000000003';
DECLARE @StRepairOpen3 UNIQUEIDENTIFIER = 'B1000009-0009-4009-8009-000000000009';
DECLARE @StRepairDone3 UNIQUEIDENTIFIER = 'B1000010-0010-4010-8010-000000001010';
DECLARE @StSaleSent3   UNIQUEIDENTIFIER = 'B1000006-0006-4006-8006-000000000006';
DECLARE @StSaleAppr3   UNIQUEIDENTIFIER = 'B1000007-0007-4007-8007-000000000007';
DECLARE @RcTo3    UNIQUEIDENTIFIER = 'DC170001-0001-4001-8001-000000000001';
DECLARE @RcSusp3  UNIQUEIDENTIFIER = 'DC170003-0003-4003-8003-000000000003';
DECLARE @RcBody3  UNIQUEIDENTIFIER = 'DC170005-0005-4005-8005-000000000005';
DECLARE @UserIvanov UNIQUEIDENTIFIER = 'DC1F0001-0001-4001-8001-000000000001';
DECLARE @UserPetrova UNIQUEIDENTIFIER = 'DC1F0002-0002-4002-8002-000000000002';
DECLARE @Pickup1 UNIQUEIDENTIFIER = 'DC020001-0001-4001-8001-000000000001';

-- Клиенты сервиса
DECLARE @Sc1 UNIQUEIDENTIFIER = NEWID();
DECLARE @Sc2 UNIQUEIDENTIFIER = NEWID();
INSERT INTO dbo.WorkshopServiceClients (RowId, WorkshopId, UserId, FullName, Phone, Email, IsRegisteredUser, CreatedAt) VALUES
(@Sc1, @WsNord3, @UserIvanov,  N'Иванов Александр Сергеевич', N'+7 (921) 100-01-01', N'ivanov.alexandr@gmail.com', 1, DATEADD(MONTH,-6,GETDATE())),
(@Sc2, @WsNord3, @UserPetrova, N'Петрова Мария Ивановна',     N'+7 (921) 100-02-02', N'petrova.maria@mail.ru',     1, DATEADD(MONTH,-4,GETDATE()));

-- Онлайн-записи (до отзывов: UQ_WorkshopReviews_UserBooking = UserId + OnlineBookingId)
IF OBJECT_ID(N'dbo.WorkshopOnlineBookings', N'U') IS NOT NULL
BEGIN
    DECLARE @bi INT = 1;
    DECLARE @bUser UNIQUEIDENTIFIER;
    DECLARE @bUc UNIQUEIDENTIFIER;
    WHILE @bi <= 50
    BEGIN
        SET @bUser = (SELECT RowId FROM dbo.Users ORDER BY RowId OFFSET (@bi-1) % 50 ROWS FETCH NEXT 1 ROW ONLY);
        SET @bUc = (SELECT TOP 1 RowId FROM dbo.UserCars WHERE UserId = @bUser);
        INSERT INTO dbo.WorkshopOnlineBookings (RowId, WorkshopId, UserId, UserCarId, ClientPhone, ClientComment, PreferredDate, Status, CreatedAt, IssueCategory)
        VALUES (NEWID(), @WsNord3, @bUser, @bUc,
                N'+7 (921) 100-00-00',
                N'Нужна диагностика, стучит подвеска',
                DATEADD(DAY, @bi, GETDATE()),
                @bi % 4, DATEADD(DAY, -@bi, GETDATE()),
                CASE @bi % 3 WHEN 0 THEN N'brakes' WHEN 1 THEN N'engine' ELSE N'suspension' END);
        SET @bi += 1;
    END
END

-- 80 ремонтов + задания + документы
DECLARE @ri INT = 1;
DECLARE @RepairIds TABLE (RowId UNIQUEIDENTIFIER PRIMARY KEY, CarId UNIQUEIDENTIFIER, WorkshopId UNIQUEIDENTIFIER, UserId UNIQUEIDENTIFIER);
DECLARE @rCar UNIQUEIDENTIFIER;
DECLARE @rUser UNIQUEIDENTIFIER;
DECLARE @rWs UNIQUEIDENTIFIER;
DECLARE @rEmp UNIQUEIDENTIFIER;
DECLARE @rCat UNIQUEIDENTIFIER;
DECLARE @rSt UNIQUEIDENTIFIER;
DECLARE @rId UNIQUEIDENTIFIER;
DECLARE @rDate DATETIME;
DECLARE @rCost DECIMAL(18,2);
DECLARE @tSt UNIQUEIDENTIFIER;
DECLARE @tId UNIQUEIDENTIFIER;
DECLARE @tDone BIT;
DECLARE @wsId UNIQUEIDENTIFIER;
DECLARE @wpId UNIQUEIDENTIFIER;
DECLARE @docId UNIQUEIDENTIFIER;
DECLARE @rClientName NVARCHAR(200);
DECLARE @rClientEmail NVARCHAR(200);
DECLARE @reviewBookingId UNIQUEIDENTIFIER;
DECLARE @reviewId UNIQUEIDENTIFIER;
DECLARE @reviewRating TINYINT;
DECLARE @reviewComment NVARCHAR(1000);
DECLARE @reviewPros NVARCHAR(500);
DECLARE @reviewCons NVARCHAR(500);
DECLARE @reviewDt DATETIME;
DECLARE @canInsertReview BIT;
DECLARE @hasReviewBookingCol BIT = CASE WHEN COL_LENGTH(N'dbo.WorkshopReviews', N'OnlineBookingId') IS NOT NULL THEN 1 ELSE 0 END;
WHILE @ri <= 80
BEGIN
    SET @rCar = (SELECT RowId FROM dbo.Cars ORDER BY RowId OFFSET (@ri - 1) % 120 ROWS FETCH NEXT 1 ROW ONLY);
    SET @rUser = (SELECT TOP 1 UserId FROM dbo.UserCars WHERE CarId = @rCar ORDER BY RowId);
    IF @rUser IS NULL
        SET @rUser = (SELECT RowId FROM dbo.Users ORDER BY RowId OFFSET (@ri - 1) % 50 ROWS FETCH NEXT 1 ROW ONLY);
    SET @rWs = CASE @ri % 3 WHEN 0 THEN @WsPaint3 ELSE @WsNord3 END;
    SET @rEmp = CASE @ri % 2 WHEN 0 THEN @EmpMech1_3 ELSE @EmpService3 END;
    SET @rCat = CASE @ri % 3 WHEN 0 THEN @RcSusp3 WHEN 1 THEN @RcTo3 ELSE @RcBody3 END;
    SET @rSt = CASE WHEN @ri % 5 = 0 THEN @StRepairOpen3 ELSE @StRepairDone3 END;
    SET @rId = NEWID();
    SET @rDate = DATEADD(DAY, -(@ri * 3), GETDATE());
    SET @rCost = 3000 + (@ri * 417) % 45000;
    INSERT INTO dbo.RepairHistory (RowId, CarId, EmployeeId, Title, Description, RepairDate, EndDate, Mileage, Cost, StatusId, CreatedAt, CategoryId)
    VALUES (@rId, @rCar, @rEmp,
            CASE @ri % 4 WHEN 0 THEN N'ТО и замена масла' WHEN 1 THEN N'Ремонт подвески' WHEN 2 THEN N'Замена тормозных колодок' ELSE N'Диагностика и ремонт' END,
            N'Выполнены работы по заявке клиента. Пробег на момент приёма: ' + CAST(40000 + @ri * 500 AS NVARCHAR(20)) + N' км.',
            @rDate, CASE WHEN @rSt = @StRepairDone3 THEN DATEADD(DAY, 2, @rDate) ELSE NULL END,
            40000 + @ri * 500, @rCost, @rSt, @rDate, @rCat);
    INSERT INTO @RepairIds VALUES (@rId, @rCar, @rWs, @rUser);

    -- Задание
    SET @tSt = CASE @ri % 5 WHEN 0 THEN @StTaskNew3 WHEN 1 THEN @StTaskProg3 ELSE @StTaskDone3 END;
    SET @tId = NEWID();
    SET @tDone = CASE WHEN @tSt = @StTaskDone3 THEN 1 ELSE 0 END;
    SET @rClientName = (SELECT TOP 1 Description FROM dbo.Users WHERE RowId = @rUser);
    SET @rClientEmail = (SELECT TOP 1 Email FROM dbo.Users WHERE RowId = @rUser);

    INSERT INTO dbo.Tasks (RowId, Title, Description, EmployeeId, StatusId, CreatedAt, StartDate, EndDate, IsCompleted, WorkHours, ClientUserId, CarId)
    VALUES (@tId,
            N'Заказ-наряд #' + RIGHT('000'+CAST(@ri AS NVARCHAR(10)),3),
            N'Работы по ремонту автомобиля клиента',
            @rEmp, @tSt, @rDate,
            CASE WHEN @tSt <> @StTaskNew3 THEN @rDate ELSE NULL END,
            CASE WHEN @tDone = 1 THEN DATEADD(DAY, 1, @rDate) ELSE NULL END,
            @tDone, 2.0 + (@ri % 8), @rUser, @rCar);

    IF COL_LENGTH(N'dbo.Tasks', N'RepairHistoryId') IS NOT NULL
        EXEC sp_executesql
            N'UPDATE dbo.Tasks SET RepairHistoryId = @rh WHERE RowId = @tid',
            N'@rh UNIQUEIDENTIFIER, @tid UNIQUEIDENTIFIER', @rh = @rId, @tid = @tId;
    IF COL_LENGTH(N'dbo.Tasks', N'ClientName') IS NOT NULL
        EXEC sp_executesql
            N'UPDATE dbo.Tasks SET ClientName = @n, ClientPhone = @p, ClientEmail = @e, VisitReason = @vr, ServiceKind = @sk WHERE RowId = @tid',
            N'@n NVARCHAR(200), @p NVARCHAR(50), @e NVARCHAR(200), @vr NVARCHAR(MAX), @sk NVARCHAR(50), @tid UNIQUEIDENTIFIER',
            @n = @rClientName, @p = N'+7 (921) 100-00-00', @e = @rClientEmail,
            @vr = N'Плановое обслуживание / устранение неисправности', @sk = N'Сервис', @tid = @tId;

    -- Строки услуг и запчастей
    SET @wsId = (SELECT TOP 1 RowId FROM dbo.WorkshopServices WHERE WorkshopId = @rWs ORDER BY SortOrder);
    IF @wsId IS NOT NULL
        INSERT INTO dbo.TaskServiceLines (RowId, TaskId, WorkshopServiceId, ServiceName, Quantity, UnitName, UnitPrice, DiscountPercent, LineAmount, SortOrder)
        SELECT NEWID(), @tId, RowId, Name, 1, ISNULL(UnitName,N'усл.'), Price, 0, Price, 1
        FROM dbo.WorkshopServices WHERE RowId = @wsId;

    SET @wpId = (SELECT TOP 1 RowId FROM dbo.WorkshopParts WHERE WorkshopId = @rWs ORDER BY CreatedAt);
    IF @wpId IS NOT NULL
        INSERT INTO dbo.TaskPartLines (RowId, TaskId, WorkshopPartId, PartName, Quantity, UnitName, UnitPrice, LineAmount, SortOrder)
        SELECT NEWID(), @tId, RowId, Name, 1, UnitName, Price, Price, 1
        FROM dbo.WorkshopParts WHERE RowId = @wpId;

    -- Заказ-наряд (ServiceDocument)
    IF OBJECT_ID(N'dbo.ServiceDocuments', N'U') IS NOT NULL AND @tDone = 1
    BEGIN
        SET @docId = NEWID();
        INSERT INTO dbo.ServiceDocuments (RowId, RootTaskId, RepairHistoryId, WorkshopId, CarId, ClientUserId, Title, ClientName, Status, CreatedAt, CompletedAt, ServiceKind)
        VALUES (@docId, @tId, @rId, @rWs, @rCar, @rUser, N'Акт выполненных работ #' + CAST(@ri AS NVARCHAR(10)),
                (SELECT TOP 1 Description FROM dbo.Users WHERE RowId = @rUser), 2, @rDate, DATEADD(DAY,1,@rDate), N'Сервис');
        IF EXISTS (SELECT 1 FROM dbo.TaskServiceLines WHERE TaskId = @tId)
            INSERT INTO dbo.ServiceDocumentServiceLines (RowId, DocumentId, WorkshopServiceId, ServiceName, Quantity, UnitName, UnitPrice, DiscountPercent, LineAmount, SortOrder)
            SELECT NEWID(), @docId, WorkshopServiceId, ServiceName, Quantity, UnitName, UnitPrice, DiscountPercent, LineAmount, SortOrder
            FROM dbo.TaskServiceLines WHERE TaskId = @tId;
        IF EXISTS (SELECT 1 FROM dbo.TaskPartLines WHERE TaskId = @tId)
            INSERT INTO dbo.ServiceDocumentPartLines (RowId, DocumentId, WorkshopPartId, PartName, Quantity, UnitName, UnitPrice, LineAmount, SortOrder)
            SELECT NEWID(), @docId, WorkshopPartId, PartName, Quantity, UnitName, UnitPrice, LineAmount, SortOrder
            FROM dbo.TaskPartLines WHERE TaskId = @tId;

        -- Отзыв: UX (UserId, DocumentId) и UQ (UserId, OnlineBookingId) — без NULL в OnlineBookingId
        IF @ri % 2 = 0 AND OBJECT_ID(N'dbo.WorkshopReviews', N'U') IS NOT NULL AND @rUser IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM dbo.WorkshopReviews WHERE UserId = @rUser AND DocumentId = @docId)
        BEGIN
            SET @reviewBookingId = NULL;
            IF @hasReviewBookingCol = 1
                EXEC sp_executesql
                    N'SELECT TOP 1 @ob = b.RowId
                      FROM dbo.WorkshopOnlineBookings b
                      WHERE b.UserId = @u
                        AND NOT EXISTS (
                            SELECT 1 FROM dbo.WorkshopReviews r
                            WHERE r.UserId = @u AND r.OnlineBookingId = b.RowId)
                      ORDER BY b.CreatedAt DESC',
                    N'@u UNIQUEIDENTIFIER, @ob UNIQUEIDENTIFIER OUTPUT',
                    @u = @rUser, @ob = @reviewBookingId OUTPUT;

            SET @canInsertReview = CASE
                WHEN @hasReviewBookingCol = 0 THEN 1
                WHEN @reviewBookingId IS NOT NULL THEN 1
                ELSE 0
            END;

            IF @canInsertReview = 1
            BEGIN
                SET @reviewId = NEWID();
                SET @reviewRating = 3 + (@ri % 3);
                SET @reviewComment = N'Обслуживание прошло ' + CASE @ri % 3 WHEN 0 THEN N'отлично' WHEN 1 THEN N'хорошо' ELSE N'удовлетворительно' END + N'. Рекомендую сервис.';
                SET @reviewPros = N'Вежливый персонал, чистая зона ожидания';
                SET @reviewCons = CASE WHEN @ri % 4 = 0 THEN N'Долгое ожидание запчастей' ELSE NULL END;
                SET @reviewDt = DATEADD(DAY, 2, @rDate);

                IF @hasReviewBookingCol = 1
                    EXEC sp_executesql
                        N'INSERT INTO dbo.WorkshopReviews (RowId, WorkshopId, UserId, Rating, Comment, Status, CreatedAt, DocumentId, RepairHistoryId, Pros, Cons, OnlineBookingId)
                          VALUES (@id, @ws, @u, @rating, @comment, 1, @dt, @doc, @rh, @pros, @cons, @ob)',
                        N'@id UNIQUEIDENTIFIER, @ws UNIQUEIDENTIFIER, @u UNIQUEIDENTIFIER, @rating TINYINT, @comment NVARCHAR(1000), @dt DATETIME, @doc UNIQUEIDENTIFIER, @rh UNIQUEIDENTIFIER, @pros NVARCHAR(500), @cons NVARCHAR(500), @ob UNIQUEIDENTIFIER',
                        @id = @reviewId, @ws = @rWs, @u = @rUser, @rating = @reviewRating, @comment = @reviewComment,
                        @dt = @reviewDt, @doc = @docId, @rh = @rId, @pros = @reviewPros, @cons = @reviewCons, @ob = @reviewBookingId;
                ELSE
                    INSERT INTO dbo.WorkshopReviews (RowId, WorkshopId, UserId, Rating, Comment, Status, CreatedAt, DocumentId, RepairHistoryId, Pros, Cons)
                    VALUES (@reviewId, @rWs, @rUser, @reviewRating, @reviewComment, 1, @reviewDt, @docId, @rId, @reviewPros, @reviewCons);
            END
        END
    END
    SET @ri += 1;
END

-- Заказы магазина
DECLARE @oi INT = 1;
DECLARE @oUser UNIQUEIDENTIFIER;
DECLARE @oId UNIQUEIDENTIFIER;
DECLARE @partId UNIQUEIDENTIFIER;
DECLARE @partName NVARCHAR(200);
DECLARE @partPrice DECIMAL(18,2);
DECLARE @qty INT;
WHILE @oi <= 30
BEGIN
    SET @oUser = (SELECT RowId FROM dbo.Users ORDER BY RowId OFFSET (@oi-1) % 50 ROWS FETCH NEXT 1 ROW ONLY);
    SET @oId = NEWID();
    SET @partId = (SELECT RowId FROM dbo.Parts ORDER BY RowId OFFSET (@oi-1) % 220 ROWS FETCH NEXT 1 ROW ONLY);
    SET @partName = (SELECT Name FROM dbo.Parts WHERE RowId = @partId);
    SET @partPrice = 500 + (@oi * 123) % 8000;
    SET @qty = 1 + (@oi % 3);
    INSERT INTO dbo.StoreOrders (RowId, UserId, PickupPointId, OrderNumber, Status, TotalAmount, CreatedAt, PaidAt)
    VALUES (@oId, @oUser, @Pickup1, N'DC-' + FORMAT(GETDATE(),'yyyyMM') + N'-' + RIGHT('0000'+CAST(@oi AS NVARCHAR(10)),4),
            CASE WHEN @oi % 3 = 0 THEN 0 ELSE 1 END, @partPrice * @qty, DATEADD(DAY,-@oi,GETDATE()),
            CASE WHEN @oi % 3 <> 0 THEN DATEADD(DAY,-@oi+1,GETDATE()) ELSE NULL END);
    INSERT INTO dbo.StoreOrderLines (RowId, OrderId, ProductId, ProductName, Category, Quantity, UnitPrice, SortOrder)
    VALUES (NEWID(), @oId, @partId, @partName, N'Запчасти', @qty, @partPrice, 1);
    SET @oi += 1;
END

-- Продажи авто
DECLARE @saleI INT = 1;
DECLARE @saleCar UNIQUEIDENTIFIER;
DECLARE @saleId UNIQUEIDENTIFIER;
DECLARE @saleSt UNIQUEIDENTIFIER;
WHILE @saleI <= 15
BEGIN
    SET @saleCar = (SELECT RowId FROM dbo.Cars ORDER BY RowId OFFSET 100 + @saleI ROWS FETCH NEXT 1 ROW ONLY);
    IF @saleCar IS NOT NULL
    BEGIN
        SET @saleId = NEWID();
        SET @saleSt = CASE WHEN @saleI % 3 = 0 THEN @StSaleSent3 ELSE @StSaleAppr3 END;
        INSERT INTO dbo.CarSales (RowId, CarId, Title, Description, CreatedAt, StatusId, ModeratedByEmployeeId, ModeratedAt)
        VALUES (@saleId, @saleCar,
                N'Продажа автомобиля в отличном состоянии',
                N'Один владелец, сервисная книжка, без ДТП.',
                DATEADD(DAY,-@saleI*5,GETDATE()), @saleSt,
                CASE WHEN @saleSt = @StSaleAppr3 THEN @EmpAdmin3 ELSE NULL END,
                CASE WHEN @saleSt = @StSaleAppr3 THEN DATEADD(DAY,-@saleI*5+1,GETDATE()) ELSE NULL END);
        INSERT INTO dbo.CarSalePrices (RowId, CarSaleId, Price, StartDate)
        VALUES (NEWID(), @saleId, 500000 + @saleI * 75000, DATEADD(DAY,-@saleI*5,GETDATE()));
    END
    SET @saleI += 1;
END

-- Уведомления
DECLARE @notifId UNIQUEIDENTIFIER = NEWID();
INSERT INTO dbo.Notifications (RowId, Title, Message, CreatedAt, IsViewed)
VALUES (@notifId, N'Напоминание о ТО', N'Пора пройти плановое техническое обслуживание вашего автомобиля.', GETDATE(), 0);
INSERT INTO dbo.UserNotifications (RowId, UserId, NotificationId, IsRead)
VALUES (NEWID(), @UserIvanov, @notifId, 0);

-- История обслуживания авто клиента
IF OBJECT_ID(N'dbo.UserCarMaintenanceHistory', N'U') IS NOT NULL
BEGIN
    DECLARE @ucIvanov UNIQUEIDENTIFIER = (SELECT TOP 1 RowId FROM dbo.UserCars WHERE UserId = @UserIvanov);
    IF @ucIvanov IS NOT NULL
    BEGIN
        INSERT INTO dbo.UserCarMaintenanceHistory (RowId, UserCarRowId, ServiceDate, MileageKm, Title, Notes, ComponentCode, WorkshopName, SeverityAfter) VALUES
        (NEWID(), @ucIvanov, DATEADD(MONTH,-6,GETDATE()), 62000, N'Замена масла', N'Масло 5W-30, фильтр Mann', N'oil', N'Северный автосервис', 1),
        (NEWID(), @ucIvanov, DATEADD(MONTH,-12,GETDATE()), 52000, N'Замена колодок', N'Передние колодки Bosch', N'brakes', N'Северный автосервис', 1);
        IF OBJECT_ID(N'dbo.UserCarComponentStatuses', N'U') IS NOT NULL
        BEGIN
            INSERT INTO dbo.UserCarComponentStatuses (RowId, UserCarRowId, ComponentCode, StatusLevel, LastServiceDate, LastMileageKm, RemainingKmHint, ShortHint, UpdatedAt) VALUES
            (NEWID(), @ucIvanov, N'oil', 2, DATEADD(MONTH,-6,GETDATE()), 62000, 3000, N'Замена масла через ~3000 км', GETDATE()),
            (NEWID(), @ucIvanov, N'brakes', 1, DATEADD(MONTH,-12,GETDATE()), 52000, 25000, N'Тормоза в норме', GETDATE());
        END
    END
END

-- Переписка клиент ↔ сервис
IF OBJECT_ID(N'dbo.WorkshopConversations', N'U') IS NOT NULL
BEGIN
    DECLARE @convId UNIQUEIDENTIFIER = NEWID();
    INSERT INTO dbo.WorkshopConversations (RowId, WorkshopId, UserId, WorkshopServiceClientId, Subject, LastMessageAt, LastMessagePreview, UnreadForUser, UnreadForWorkshop, CreatedAt)
    VALUES (@convId, @WsNord3, @UserIvanov, @Sc1, N'Вопрос по ремонту подвески', GETDATE(), N'Спасибо, запишите на пятницу', 0, 0, DATEADD(DAY,-3,GETDATE()));
    IF OBJECT_ID(N'dbo.WorkshopMessages', N'U') IS NOT NULL
    BEGIN
        INSERT INTO dbo.WorkshopMessages (RowId, ConversationId, SenderKind, SenderUserId, SenderEmployeeId, Body, CreatedAt) VALUES
        (NEWID(), @convId, 0, @UserIvanov, NULL, N'Здравствуйте! Стучит передняя подвеска на кочках. Можно записаться?', DATEADD(DAY,-3,GETDATE())),
        (NEWID(), @convId, 1, NULL, @EmpService3, N'Добрый день! Можем принять в пятницу в 10:00. Диагностика займёт около часа.', DATEADD(DAY,-2,GETDATE())),
        (NEWID(), @convId, 0, @UserIvanov, NULL, N'Спасибо, запишите на пятницу', DATEADD(DAY,-1,GETDATE()));
    END
END

-- Журнал активности (статистика)
IF OBJECT_ID(N'dbo.AppActivityEvents', N'U') IS NOT NULL
BEGIN
    INSERT INTO dbo.AppActivityEvents (RowId, EventCode, ActorKind, UserId, EmployeeId, WorkshopId, EntityType, CreatedAt)
    VALUES
    (NEWID(), N'car_sale_view', 0, @UserIvanov, NULL, NULL, N'CarSale', DATEADD(DAY,-1,GETDATE())),
    (NEWID(), N'store_order_created', 0, @UserPetrova, NULL, NULL, N'StoreOrder', DATEADD(DAY,-2,GETDATE())),
    (NEWID(), N'task_completed', 1, NULL, @EmpMech1_3, @WsNord3, N'Task', DATEADD(DAY,-1,GETDATE()));
END

-- Итоговая статистика
PRINT N'';
PRINT N'========================================';
PRINT N'  SEED ЗАВЕРШЁН';
PRINT N'========================================';
SELECT N'Users' AS [Таблица], COUNT(*) AS [Записей] FROM dbo.Users
UNION ALL SELECT N'Cars', COUNT(*) FROM dbo.Cars
UNION ALL SELECT N'Parts', COUNT(*) FROM dbo.Parts
UNION ALL SELECT N'WorkshopServices', COUNT(*) FROM dbo.WorkshopServices
UNION ALL SELECT N'WorkshopParts', COUNT(*) FROM dbo.WorkshopParts
UNION ALL SELECT N'Employees', COUNT(*) FROM dbo.Employees
UNION ALL SELECT N'RepairHistory', COUNT(*) FROM dbo.RepairHistory
UNION ALL SELECT N'Tasks', COUNT(*) FROM dbo.Tasks
UNION ALL SELECT N'StoreOrders', COUNT(*) FROM dbo.StoreOrders
UNION ALL SELECT N'Permissions', COUNT(*) FROM dbo.Permissions
UNION ALL SELECT N'RolePermissionsMap', COUNT(*) FROM dbo.RolePermissionsMap
ORDER BY [Таблица];
IF OBJECT_ID(N'dbo.WorkshopReviews', N'U') IS NOT NULL
    SELECT N'WorkshopReviews' AS [Таблица], COUNT(*) AS [Записей] FROM dbo.WorkshopReviews;
IF OBJECT_ID(N'dbo.WorkshopOnlineBookings', N'U') IS NOT NULL
    SELECT N'WorkshopOnlineBookings' AS [Таблица], COUNT(*) AS [Записей] FROM dbo.WorkshopOnlineBookings;
GO
