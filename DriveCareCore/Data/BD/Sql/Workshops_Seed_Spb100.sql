-- Мастерские по Санкт-Петербургу: исправление некорректного seed и повторное добавление.
-- 1) Удаляет мастерские с фейковыми координатами (seed SPb с ошибочным WHILE).
-- 2) Добавляет точки по реальным магистралям — lat/lon смещаются ВДОЛЬ той же улицы.
--
-- Безопасно повторять. Цель: не менее 100 мастерских в черте СПб.

SET NOCOUNT ON;
PRINT N'=== Workshops_Seed_Spb100 (исправленный) ===';

IF OBJECT_ID(N'dbo.Workshops', N'U') IS NULL
BEGIN
    RAISERROR(N'Таблица Workshops не найдена.', 16, 1);
    RETURN;
END

-- ---------------------------------------------------------------------------
-- ШАГ 1. Удаление некорректно добавленных мастерских
-- ---------------------------------------------------------------------------
PRINT N'[1/2] Удаление мастерских с некорректными координатами...';

IF OBJECT_ID(N'tempdb..#BadWorkshops') IS NOT NULL DROP TABLE #BadWorkshops;

SELECT w.RowId AS WorkshopId, w.AddressId, w.CompanyId
INTO #BadWorkshops
FROM dbo.Workshops w
LEFT JOIN dbo.Companies c ON c.RowId = w.CompanyId
WHERE
    -- явный маркер прошлого seed
    (c.Description = N'Автосервисная компания (seed SPb)')
    OR (w.Description LIKE N'ТО, диагностика и ремонт. Точка #%')
    OR (w.Name LIKE N'Автосервис % #%')
    OR (w.Name LIKE N'АвтоМастер %' AND c.Description = N'Автосервисная компания (seed SPb)')
    -- нет привязанных сотрудников (не трогаем рабочие мастерские)
    AND NOT EXISTS (SELECT 1 FROM dbo.Employees e WHERE e.WorkshopId = w.RowId);

DECLARE @delCount INT = (SELECT COUNT(1) FROM #BadWorkshops);
PRINT N'  Найдено к удалению (без сотрудников): ' + CAST(@delCount AS NVARCHAR(10));

IF @delCount > 0
BEGIN
    IF OBJECT_ID(N'dbo.WorkshopOnlineBookingSettings', N'U') IS NOT NULL
        DELETE s FROM dbo.WorkshopOnlineBookingSettings s
        INNER JOIN #BadWorkshops b ON b.WorkshopId = s.WorkshopId;

    IF OBJECT_ID(N'dbo.WorkshopWorkSchedules', N'U') IS NOT NULL
        DELETE s FROM dbo.WorkshopWorkSchedules s
        INNER JOIN #BadWorkshops b ON b.WorkshopId = s.WorkshopId;

    IF OBJECT_ID(N'dbo.WorkshopBusinessTypes', N'U') IS NOT NULL
        DELETE t FROM dbo.WorkshopBusinessTypes t
        INNER JOIN #BadWorkshops b ON b.WorkshopId = t.WorkshopId;

    IF OBJECT_ID(N'dbo.WorkshopReviews', N'U') IS NOT NULL
        DELETE r FROM dbo.WorkshopReviews r
        INNER JOIN #BadWorkshops b ON b.WorkshopId = r.WorkshopId;

    DELETE w FROM dbo.Workshops w
    INNER JOIN #BadWorkshops b ON b.WorkshopId = w.RowId;

    DELETE a FROM dbo.Addresses a
    INNER JOIN #BadWorkshops b ON b.AddressId = a.RowId
    WHERE b.AddressId IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM dbo.Workshops w2 WHERE w2.AddressId = a.RowId);

    DELETE c FROM dbo.Companies c
    INNER JOIN #BadWorkshops b ON b.CompanyId = c.RowId
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Workshops w2 WHERE w2.CompanyId = c.RowId);

    PRINT N'  Удалено мастерских: ' + CAST(@delCount AS NVARCHAR(10));
END

DROP TABLE #BadWorkshops;

-- ---------------------------------------------------------------------------
-- ШАГ 2. Добавление с корректными адресами
-- ---------------------------------------------------------------------------
PRINT N'[2/2] Добавление мастерских по реальным улицам СПб...';

DECLARE @CountryRu UNIQUEIDENTIFIER = (
    SELECT TOP 1 RowId FROM dbo.Countries WHERE Name LIKE N'%Росс%' ORDER BY Name
);
IF @CountryRu IS NULL SET @CountryRu = NEWID();

DECLARE @BtAuto  UNIQUEIDENTIFIER = (SELECT TOP 1 RowId FROM dbo.BusinessTypes WHERE Name LIKE N'%Автосервис%');
DECLARE @BtPaint UNIQUEIDENTIFIER = (SELECT TOP 1 RowId FROM dbo.BusinessTypes WHERE Name LIKE N'%Покраск%');
DECLARE @BtTire  UNIQUEIDENTIFIER = (SELECT TOP 1 RowId FROM dbo.BusinessTypes WHERE Name LIKE N'%Шин%');
IF @BtAuto IS NULL  SET @BtAuto  = (SELECT TOP 1 RowId FROM dbo.BusinessTypes ORDER BY Name);
IF @BtPaint IS NULL SET @BtPaint = @BtAuto;
IF @BtTire IS NULL  SET @BtTire  = @BtAuto;

DECLARE @TargetCount INT = 100;
DECLARE @CurrentCount INT = (SELECT COUNT(1) FROM dbo.Workshops);
DECLARE @Need INT = @TargetCount - @CurrentCount;

IF @Need <= 0
BEGIN
    PRINT N'Уже ' + CAST(@CurrentCount AS NVARCHAR(10)) + N' мастерских — добавление не требуется.';
    RETURN;
END

PRINT N'  Сейчас: ' + CAST(@CurrentCount AS NVARCHAR(10)) + N', нужно добавить: ' + CAST(@Need AS NVARCHAR(10));

-- Якорные точки реальных магистралей СПб (координаты ≈ Yandex/OSM).
-- StepLat / StepLon — смещение на каждые 10 номеров дома ВДОЛЬ этой улицы.
DECLARE @Streets TABLE (
    StreetId   INT NOT NULL PRIMARY KEY,
    Street     NVARCHAR(100) NOT NULL,
    District   NVARCHAR(60)  NOT NULL,
    BaseLat    FLOAT NOT NULL,
    BaseLon    FLOAT NOT NULL,
    StepLat    FLOAT NOT NULL,
    StepLon    FLOAT NOT NULL,
    HouseStart INT NOT NULL
);

INSERT INTO @Streets (StreetId, Street, District, BaseLat, BaseLon, StepLat, StepLon, HouseStart) VALUES
( 1, N'Невский пр.',              N'Центральный',       59.9310, 30.3500,  0.00002,  0.00014,  10),
( 2, N'Московский пр.',            N'Московский',        59.8480, 30.3180,  0.00010,  0.00001,  20),
( 3, N'пр. Энгельса',             N'Калининский',       59.9500, 30.3200,  0.00011,  0.00002,  15),
( 4, N'ул. Савушкина',             N'Приморский',        59.9800, 30.2050,  0.00008,  0.00005,  20),
( 5, N'Лиговский пр.',             N'Центральный',       59.9200, 30.3550,  0.00003,  0.00011,  30),
( 6, N'пр. Стачек',                N'Кировский',         59.8750, 30.2650,  0.00004, -0.00009,  25),
( 7, N'ул. Дыбенко',               N'Невский',           59.9050, 30.4700,  0.00006,  0.00005,  10),
( 8, N'пр. Просвещения',           N'Выборгский',        59.9600, 30.4000,  0.00010,  0.00004,  12),
( 9, N'ул. Бухарестская',          N'Фрунзенский',       59.8650, 30.3700,  0.00004,  0.00006,  18),
(10, N'пр. Обуховской Обороны',    N'Невский',           59.8650, 30.4300,  0.00005,  0.00008, 120),
(11, N'Каменноостровский пр.',     N'Петроградский',     59.9650, 30.2950,  0.00007, -0.00005,  20),
(12, N'Большой пр. В.О.',          N'Василеостровский',  59.9400, 30.2400,  0.00005,  0.00011,  15),
(13, N'пр. Королёва',              N'Приморский',        60.0100, 30.2500,  0.00008,  0.00006,   8),
(14, N'ул. Коллонтай',              N'Невский',           59.9100, 30.4500,  0.00005,  0.00007,  20),
(15, N'ул. Типанова',              N'Московский',        59.8500, 30.3400,  0.00004,  0.00005,  15),
(16, N'Пулковское ш.',             N'Московский',        59.8200, 30.3300,  0.00009,  0.00003,   5),
(17, N'ул. Благодатная',           N'Фрунзенский',       59.8680, 30.3950,  0.00004,  0.00006,  10),
(18, N'ул. Кораблестроителей',     N'Василеостровский',  59.9500, 30.2150,  0.00006, -0.00004,  20),
(19, N'пр. Маршала Блюхера',       N'Калининский',       59.9700, 30.3800,  0.00008,  0.00005,  10),
(20, N'ул. Руставели',             N'Выборгский',        60.0000, 30.3600,  0.00007,  0.00004,  12),
(21, N'пр. Луначарского',          N'Приморский',        59.9950, 30.2000,  0.00007,  0.00006,  50),
(22, N'ул. Оптиков',               N'Приморский',        59.9900, 30.2150,  0.00006,  0.00005,   5),
(23, N'ул. Есенина',               N'Приморский',        60.0350, 30.3200,  0.00005,  0.00004,   3),
(24, N'пр. Науки',                 N'Выборгский',        60.0100, 30.4200,  0.00008,  0.00003,  10),
(25, N'ул. Большая Пушкарская',    N'Петроградский',     59.9600, 30.3100,  0.00004,  0.00003,   5),
(26, N'наб. Обводного канала',     N'Адмиралтейский',    59.9150, 30.2900,  0.00003,  0.00012,  50),
(27, N'ул. Марата',                N'Центральный',       59.9250, 30.3450,  0.00003,  0.00004,  20),
(28, N'ул. Заставская',            N'Московский',        59.8850, 30.3050,  0.00004,  0.00005,  10),
(29, N'ул. Фучика',                N'Невский',           59.8750, 30.3850,  0.00005,  0.00006,  12),
(30, N'ул. Мурзинская',            N'Невский',           59.8300, 30.5050,  0.00004,  0.00005,   5);

DECLARE @Seed TABLE (
    Seq          INT NOT NULL PRIMARY KEY,
    WorkshopName NVARCHAR(120) NOT NULL,
    CompanyName  NVARCHAR(120) NOT NULL,
    Street       NVARCHAR(100) NOT NULL,
    House        NVARCHAR(20)  NOT NULL,
    FullAddress  NVARCHAR(250) NOT NULL,
    Lat          FLOAT NOT NULL,
    Lon          FLOAT NOT NULL,
    Kind         TINYINT NOT NULL
);

DECLARE @seq INT = 1;
DECLARE @streetCount INT = (SELECT COUNT(1) FROM @Streets);
DECLARE @sid INT, @street NVARCHAR(100), @district NVARCHAR(60);
DECLARE @baseLat FLOAT, @baseLon FLOAT, @stepLat FLOAT, @stepLon FLOAT, @houseStart INT;
DECLARE @houseNum INT, @houseStr NVARCHAR(20), @lat FLOAT, @lon FLOAT, @kind TINYINT;
DECLARE @fullAddr NVARCHAR(250), @wsName NVARCHAR(120), @coName NVARCHAR(120);

WHILE @seq <= @Need + 5
BEGIN
    SELECT
        @sid = StreetId, @street = Street, @district = District,
        @baseLat = BaseLat, @baseLon = BaseLon,
        @stepLat = StepLat, @stepLon = StepLon, @houseStart = HouseStart
    FROM @Streets
    WHERE StreetId = ((@seq - 1) % @streetCount) + 1;

    SET @houseNum = @houseStart + ((@seq * 13) % 170);
    IF @houseNum % 2 = 0 SET @houseNum += 1;  -- нечётные номера
    SET @houseStr = CAST(@houseNum AS NVARCHAR(20));

    DECLARE @offset FLOAT = (@houseNum - @houseStart) / 10.0;
    SET @lat = @baseLat + @stepLat * @offset;
    SET @lon = @baseLon + @stepLon * @offset;

    SET @kind = CASE
        WHEN @seq % 11 = 0 THEN 3  -- шиномонтаж
        WHEN @seq % 7  = 0 THEN 2  -- покраска
        ELSE 1 END;

    SET @wsName = CASE @kind
        WHEN 3 THEN N'Шиномонтаж ' + @district + N' · ' + @street
        WHEN 2 THEN N'Кузовной центр ' + @district
        ELSE N'Автосервис ' + @district + N' · ' + @street
    END + N' ' + @houseStr;

    SET @coName = N'ООО «' + REPLACE(@wsName, N' · ', N' ') + N'»';
    SET @fullAddr = N'Санкт-Петербург, ' + @street + N', д. ' + @houseStr;

    IF NOT EXISTS (
        SELECT 1 FROM dbo.Workshops w
        INNER JOIN dbo.Addresses a ON a.RowId = w.AddressId
        WHERE w.Name = @wsName
           OR (a.FullAddress = @fullAddr)
           OR (a.Street = @street AND a.House = @houseStr AND a.City = N'Санкт-Петербург')
    )
    BEGIN
        INSERT INTO @Seed (Seq, WorkshopName, CompanyName, Street, House, FullAddress, Lat, Lon, Kind)
        VALUES (@seq, @wsName, @coName, @street, @houseStr, @fullAddr, @lat, @lon, @kind);
    END

    SET @seq += 1;
END

DECLARE @added INT = 0;
DECLARE @rowSeq INT;
DECLARE @streetName NVARCHAR(100), @houseNo NVARCHAR(20);
DECLARE @latitude FLOAT, @longitude FLOAT, @kindCode TINYINT;
DECLARE @addrId UNIQUEIDENTIFIER, @coId UNIQUEIDENTIFIER, @wsId UNIQUEIDENTIFIER, @btId UNIQUEIDENTIFIER;
DECLARE @fullAddress NVARCHAR(250);

DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT TOP (@Need) Seq, WorkshopName, CompanyName, Street, House, FullAddress, Lat, Lon, Kind
    FROM @Seed
    ORDER BY Seq;

OPEN cur;
FETCH NEXT FROM cur INTO @rowSeq, @wsName, @coName, @streetName, @houseNo, @fullAddress, @latitude, @longitude, @kindCode;

WHILE @@FETCH_STATUS = 0 AND @added < @Need
BEGIN
    SET @addrId = NEWID();
    SET @coId   = NEWID();
    SET @wsId   = NEWID();
    SET @btId   = CASE @kindCode WHEN 2 THEN @BtPaint WHEN 3 THEN @BtTire ELSE @BtAuto END;

    INSERT INTO dbo.Addresses (RowId, CountryId, City, Street, House, Latitude, Longitude)
    VALUES (@addrId, @CountryRu, N'Санкт-Петербург', @streetName, @houseNo, @latitude, @longitude);

    INSERT INTO dbo.Companies (RowId, Name, Description)
    VALUES (@coId, @coName, N'Автосервис (seed SPb v2, привязка к улице)');

    INSERT INTO dbo.Workshops (RowId, Name, CompanyId, AddressId, Description, BusinessTypeId)
    VALUES (@wsId, @wsName, @coId, @addrId,
            N'ТО и ремонт · ' + @fullAddress,
            @btId);

    IF OBJECT_ID(N'dbo.WorkshopBusinessTypes', N'U') IS NOT NULL
        INSERT INTO dbo.WorkshopBusinessTypes (RowId, WorkshopId, BusinessTypeId)
        VALUES (NEWID(), @wsId, @btId);

    IF OBJECT_ID(N'dbo.WorkshopWorkSchedules', N'U') IS NOT NULL
    BEGIN
        DECLARE @d TINYINT = 1;
        WHILE @d <= 7
        BEGIN
            INSERT INTO dbo.WorkshopWorkSchedules (RowId, WorkshopId, DayOfWeek, IsClosed, OpenTime, CloseTime)
            VALUES (NEWID(), @wsId, @d, CASE WHEN @d = 7 THEN 1 ELSE 0 END,
                    CASE WHEN @d = 7 THEN NULL ELSE CAST('09:00' AS TIME) END,
                    CASE WHEN @d = 7 THEN NULL ELSE CAST('20:00' AS TIME) END);
            SET @d += 1;
        END
    END

    IF OBJECT_ID(N'dbo.WorkshopOnlineBookingSettings', N'U') IS NOT NULL
        INSERT INTO dbo.WorkshopOnlineBookingSettings (WorkshopId, MaxBookingsPerDay)
        VALUES (@wsId, 10);

    SET @added += 1;
    FETCH NEXT FROM cur INTO @rowSeq, @wsName, @coName, @streetName, @houseNo, @fullAddress, @latitude, @longitude, @kindCode;
END

CLOSE cur;
DEALLOCATE cur;

DECLARE @FinalCount INT = (SELECT COUNT(1) FROM dbo.Workshops);
PRINT N'Добавлено: ' + CAST(@added AS NVARCHAR(10)) + N'. Всего мастерских: ' + CAST(@FinalCount AS NVARCHAR(10)) + N'.';
PRINT N'Готово. Обновите карту в приложении (кнопка «Обновить»).';
GO
