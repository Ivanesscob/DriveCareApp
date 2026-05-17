-- Поля задания для записи на сервис (связь с ремонтом и данные для карточки).
-- Выполните на DriveCareDB. Обновление EDMX не обязательно — приложение пишет через SQL.

IF COL_LENGTH(N'dbo.Tasks', N'RepairHistoryId') IS NULL
    ALTER TABLE dbo.Tasks ADD RepairHistoryId UNIQUEIDENTIFIER NULL;
GO

IF COL_LENGTH(N'dbo.Tasks', N'ClientName') IS NULL
    ALTER TABLE dbo.Tasks ADD ClientName NVARCHAR(200) NULL;
GO

IF COL_LENGTH(N'dbo.Tasks', N'ClientPhone') IS NULL
    ALTER TABLE dbo.Tasks ADD ClientPhone NVARCHAR(50) NULL;
GO

IF COL_LENGTH(N'dbo.Tasks', N'ClientEmail') IS NULL
    ALTER TABLE dbo.Tasks ADD ClientEmail NVARCHAR(200) NULL;
GO

IF COL_LENGTH(N'dbo.Tasks', N'VisitReason') IS NULL
    ALTER TABLE dbo.Tasks ADD VisitReason NVARCHAR(MAX) NULL;
GO

IF COL_LENGTH(N'dbo.Tasks', N'SpecialNotes') IS NULL
    ALTER TABLE dbo.Tasks ADD SpecialNotes NVARCHAR(MAX) NULL;
GO

IF COL_LENGTH(N'dbo.Tasks', N'ServiceKind') IS NULL
    ALTER TABLE dbo.Tasks ADD ServiceKind NVARCHAR(50) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tasks_RepairHistoryId' AND object_id = OBJECT_ID(N'dbo.Tasks'))
    CREATE INDEX IX_Tasks_RepairHistoryId ON dbo.Tasks(RepairHistoryId);
GO
