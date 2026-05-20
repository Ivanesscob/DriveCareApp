-- Дозавершение двух FK после Generate_ForeignKeys_FromModel (FAIL).
-- Выполните на DriveCareDB.

SET NOCOUNT ON;
PRINT N'=== Fix_Remaining_ForeignKeys ===';

-- 1) Показать проблемные строки (для справки)
IF OBJECT_ID(N'dbo.TaskPurchaseRequestLines', N'U') IS NOT NULL
BEGIN
    SELECT N'TaskPurchaseRequestLines: битый WorkshopPartId' AS Issue, l.*
    FROM dbo.TaskPurchaseRequestLines l
    WHERE l.WorkshopPartId IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM dbo.WorkshopParts p WHERE p.RowId = l.WorkshopPartId);
END

IF OBJECT_ID(N'dbo.WorkshopServiceUnits', N'U') IS NOT NULL
BEGIN
    SELECT N'WorkshopServiceUnits: битый WorkshopId' AS Issue, u.*
    FROM dbo.WorkshopServiceUnits u
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Workshops w WHERE w.RowId = u.WorkshopId);
END

-- 2) Исправление
IF OBJECT_ID(N'dbo.TaskPurchaseRequestLines', N'U') IS NOT NULL
BEGIN
    UPDATE dbo.TaskPurchaseRequestLines SET WorkshopPartId = NULL
    WHERE WorkshopPartId IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM dbo.WorkshopParts p WHERE p.RowId = WorkshopPartId);
    PRINT N'TaskPurchaseRequestLines: битые WorkshopPartId обнулены.';
END

IF OBJECT_ID(N'dbo.WorkshopServiceUnits', N'U') IS NOT NULL
BEGIN
    DELETE u FROM dbo.WorkshopServiceUnits u
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Workshops w WHERE w.RowId = u.WorkshopId);
    PRINT N'WorkshopServiceUnits: строки с несуществующим WorkshopId удалены.';
END

-- 3) Создать недостающие FK
IF OBJECT_ID(N'dbo.FK_TaskPurchaseRequestLines_WorkshopPart', N'F') IS NULL
   AND OBJECT_ID(N'dbo.TaskPurchaseRequestLines', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.WorkshopParts', N'U') IS NOT NULL
BEGIN
    BEGIN TRY
        ALTER TABLE dbo.TaskPurchaseRequestLines
            ADD CONSTRAINT FK_TaskPurchaseRequestLines_WorkshopPart
            FOREIGN KEY (WorkshopPartId) REFERENCES dbo.WorkshopParts(RowId) ON DELETE SET NULL;
        PRINT N'OK: FK_TaskPurchaseRequestLines_WorkshopPart';
    END TRY
    BEGIN CATCH
        PRINT N'FAIL: FK_TaskPurchaseRequestLines_WorkshopPart — ' + ERROR_MESSAGE();
    END CATCH
END
ELSE
    PRINT N'SKIP: FK_TaskPurchaseRequestLines_WorkshopPart';

IF OBJECT_ID(N'dbo.FK_WorkshopServiceUnits_Workshops', N'F') IS NULL
   AND OBJECT_ID(N'dbo.WorkshopServiceUnits', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.Workshops', N'U') IS NOT NULL
BEGIN
    BEGIN TRY
        ALTER TABLE dbo.WorkshopServiceUnits
            ADD CONSTRAINT FK_WorkshopServiceUnits_Workshops
            FOREIGN KEY (WorkshopId) REFERENCES dbo.Workshops(RowId) ON DELETE CASCADE;
        PRINT N'OK: FK_WorkshopServiceUnits_Workshops';
    END TRY
    BEGIN CATCH
        PRINT N'FAIL: FK_WorkshopServiceUnits_Workshops — ' + ERROR_MESSAGE();
    END CATCH
END
ELSE
    PRINT N'SKIP: FK_WorkshopServiceUnits_Workshops';

PRINT N'=== Готово ===';
GO
