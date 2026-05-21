-- Заявки владельца на смену типов мастерской (автосервис / покраска / шиномонтаж) — после одобрения админом.
IF OBJECT_ID(N'dbo.WorkshopBusinessTypeChangeRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkshopBusinessTypeChangeRequests (
        RowId                   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_WorkshopBusinessTypeChangeRequests PRIMARY KEY DEFAULT (NEWID()),
        WorkshopId              UNIQUEIDENTIFIER NOT NULL,
        RequestedByEmployeeId   UNIQUEIDENTIFIER NOT NULL,
        Status                  TINYINT          NOT NULL CONSTRAINT DF_WBTCR_Status DEFAULT (0), -- 0 ожидает, 1 одобрено, 2 отклонено
        OwnerComment            NVARCHAR(500)    NULL,
        ModerationComment       NVARCHAR(500)    NULL,
        ModeratedByEmployeeId   UNIQUEIDENTIFIER NULL,
        CreatedAt               DATETIME         NOT NULL CONSTRAINT DF_WBTCR_CreatedAt DEFAULT (GETDATE()),
        ModeratedAt             DATETIME         NULL,
        CONSTRAINT FK_WBTCR_Workshop FOREIGN KEY (WorkshopId) REFERENCES dbo.Workshops (RowId),
        CONSTRAINT FK_WBTCR_Employee FOREIGN KEY (RequestedByEmployeeId) REFERENCES dbo.Employees (RowId)
    );

    CREATE INDEX IX_WBTCR_Workshop_Status ON dbo.WorkshopBusinessTypeChangeRequests (WorkshopId, Status);
    CREATE INDEX IX_WBTCR_Status_Created ON dbo.WorkshopBusinessTypeChangeRequests (Status, CreatedAt DESC);
END
GO

IF OBJECT_ID(N'dbo.WorkshopBusinessTypeChangeRequestTypes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkshopBusinessTypeChangeRequestTypes (
        RequestId        UNIQUEIDENTIFIER NOT NULL,
        BusinessTypeId   UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT PK_WorkshopBusinessTypeChangeRequestTypes PRIMARY KEY (RequestId, BusinessTypeId),
        CONSTRAINT FK_WBTCRT_Request FOREIGN KEY (RequestId) REFERENCES dbo.WorkshopBusinessTypeChangeRequests (RowId) ON DELETE CASCADE,
        CONSTRAINT FK_WBTCRT_BusinessType FOREIGN KEY (BusinessTypeId) REFERENCES dbo.BusinessTypes (RowId)
    );
END
GO
