-- Несколько типов услуг у одной мастерской / одного здания (автосервис + покраска + шиномонтаж).
-- Выполнить на DriveCareDB.

IF OBJECT_ID(N'dbo.WorkshopBusinessTypes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkshopBusinessTypes (
        RowId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_WorkshopBusinessTypes PRIMARY KEY DEFAULT (NEWID()),
        WorkshopId       UNIQUEIDENTIFIER NOT NULL,
        BusinessTypeId   UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT FK_WorkshopBusinessTypes_Workshop FOREIGN KEY (WorkshopId) REFERENCES dbo.Workshops (RowId),
        CONSTRAINT FK_WorkshopBusinessTypes_BusinessType FOREIGN KEY (BusinessTypeId) REFERENCES dbo.BusinessTypes (RowId),
        CONSTRAINT UQ_WorkshopBusinessTypes UNIQUE (WorkshopId, BusinessTypeId)
    );

    CREATE INDEX IX_WorkshopBusinessTypes_Workshop ON dbo.WorkshopBusinessTypes (WorkshopId);
    CREATE INDEX IX_WorkshopBusinessTypes_Type ON dbo.WorkshopBusinessTypes (BusinessTypeId);
END
GO

-- Перенос основного типа из Workshops.BusinessTypeId
INSERT INTO dbo.WorkshopBusinessTypes (RowId, WorkshopId, BusinessTypeId)
SELECT NEWID(), w.RowId, w.BusinessTypeId
FROM dbo.Workshops w
WHERE w.BusinessTypeId IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM dbo.WorkshopBusinessTypes m
      WHERE m.WorkshopId = w.RowId AND m.BusinessTypeId = w.BusinessTypeId
  );
GO
