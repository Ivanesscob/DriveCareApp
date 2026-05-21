-- Пример данных для экрана «Обслуживание» (тест).
-- 1) Выполните UserCarMaintenance_Extend.sql
-- 2) Подставьте RowId из: SELECT TOP 1 RowId, UserId, CarId FROM dbo.UserCars ORDER BY RowId;

/*
DECLARE @uc UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';

INSERT INTO dbo.UserCarMaintenanceHistory
    (RowId, UserCarRowId, ServiceDate, MileageKm, Title, Notes, ComponentCode, WorkshopName, SeverityAfter)
VALUES
(NEWID(), @uc, DATEADD(WEEK, -2, GETDATE()), 92100, N'Замена масла и фильтра', N'5W-30 синтетика', N'oil', N'СТО DriveCare', 0),
(NEWID(), @uc, DATEADD(MONTH, -1, GETDATE()), 90500, N'Пробег с одометра', NULL, NULL, NULL, NULL),
(NEWID(), @uc, DATEADD(MONTH, -4, GETDATE()), 86800, N'Плановое ТО', N'Осмотр, диагностика, жидкости', N'service', N'Официальный сервис', 0),
(NEWID(), @uc, DATEADD(MONTH, -7, GETDATE()), 81200, N'Тормозные колодки перед', N'Износ ~40%', N'brakes', N'СТО DriveCare', 1),
(NEWID(), @uc, DATEADD(MONTH, -10, GETDATE()), 76500, N'Шиномонтаж и балансировка', N'Летний комплект', N'tires', N'Шинный центр', 0),
(NEWID(), @uc, DATEADD(YEAR, -1, GETDATE()), 68000, N'Замена салонного фильтра', NULL, N'filters', N'СТО DriveCare', 0),
(NEWID(), @uc, DATEADD(YEAR, -2, GETDATE()), 52000, N'Замена ремня ГРМ', N'Комплект с помпой', N'timing', N'Сервис ГРМ', 0),
(NEWID(), @uc, DATEADD(YEAR, -3, GETDATE()), 41000, N'Аккумулятор', N'70 А·ч', N'battery', N'Автоэлектрик', 0),
(NEWID(), @uc, DATEADD(MONTH, -27, GETDATE()), 73500, N'Антифриз', N'G12', N'coolant', N'СТО DriveCare', 2);

-- Явные статусы узлов (перекрывают расчёт, если нужно показать «как в сервисе»):
INSERT INTO dbo.UserCarComponentStatuses
    (RowId, UserCarRowId, ComponentCode, StatusLevel, LastServiceDate, LastMileageKm, RemainingKmHint, ShortHint, UpdatedAt)
VALUES
(NEWID(), @uc, N'brakes', 1, DATEADD(MONTH, -7, GETDATE()), 81200, 12000, N'Колодки ещё на ~12 тыс. км — держать на контроле', GETDATE()),
(NEWID(), @uc, N'coolant', 2, DATEADD(MONTH, -27, GETDATE()), 73500, NULL, N'По записи сервиса — пора менять охлаждающую жидкость', GETDATE());
*/
