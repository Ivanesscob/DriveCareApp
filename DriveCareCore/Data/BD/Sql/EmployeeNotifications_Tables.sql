-- Уведомления сотрудников Pro (задания: поручения, закупки)
IF OBJECT_ID(N'dbo.EmployeeNotifications', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmployeeNotifications
    (
        RowId            UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EmployeeNotifications PRIMARY KEY,
        EmployeeId       UNIQUEIDENTIFIER NOT NULL,
        NotificationId   UNIQUEIDENTIFIER NOT NULL,
        TaskId           UNIQUEIDENTIFIER NULL,
        IsRead           BIT              NOT NULL CONSTRAINT DF_EmployeeNotifications_IsRead DEFAULT (0),
        CreatedAt        DATETIME2(0)     NOT NULL CONSTRAINT DF_EmployeeNotifications_CreatedAt DEFAULT (SYSUTCDATETIME())
    );

    CREATE INDEX IX_EmployeeNotifications_Employee ON dbo.EmployeeNotifications (EmployeeId, IsRead, CreatedAt DESC);
    CREATE INDEX IX_EmployeeNotifications_Task ON dbo.EmployeeNotifications (TaskId) WHERE TaskId IS NOT NULL;
END
GO
