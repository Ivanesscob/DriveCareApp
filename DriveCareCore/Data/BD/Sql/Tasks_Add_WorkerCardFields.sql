-- Поля карточки задания для сотрудника: авто, клиент, отчёт, часы работы.
IF COL_LENGTH(N'dbo.Tasks', N'CarId') IS NULL
    ALTER TABLE dbo.Tasks ADD CarId uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.Tasks', N'ClientUserId') IS NULL
    ALTER TABLE dbo.Tasks ADD ClientUserId uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.Tasks', N'ReportText') IS NULL
    ALTER TABLE dbo.Tasks ADD ReportText nvarchar(max) NULL;
IF COL_LENGTH(N'dbo.Tasks', N'WorkHours') IS NULL
    ALTER TABLE dbo.Tasks ADD WorkHours decimal(9, 2) NULL;
GO
