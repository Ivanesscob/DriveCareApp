-- Цепочка передачи поручений (любая глубина).
-- ParentTaskId — у копии: ссылка на задание, с которого передали.
-- DelegateTaskId — у задания-отправителя: ссылка на переданную копию (следующее звено).

IF COL_LENGTH(N'dbo.Tasks', N'ParentTaskId') IS NULL
    ALTER TABLE dbo.Tasks ADD ParentTaskId UNIQUEIDENTIFIER NULL;
GO

IF COL_LENGTH(N'dbo.Tasks', N'DelegateTaskId') IS NULL
    ALTER TABLE dbo.Tasks ADD DelegateTaskId UNIQUEIDENTIFIER NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tasks_ParentTaskId' AND object_id = OBJECT_ID(N'dbo.Tasks'))
    CREATE INDEX IX_Tasks_ParentTaskId ON dbo.Tasks(ParentTaskId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tasks_DelegateTaskId' AND object_id = OBJECT_ID(N'dbo.Tasks'))
    CREATE INDEX IX_Tasks_DelegateTaskId ON dbo.Tasks(DelegateTaskId);
GO
