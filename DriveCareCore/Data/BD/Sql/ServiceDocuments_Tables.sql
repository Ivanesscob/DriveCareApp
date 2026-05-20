-- Единый документ заказ-наряда: создаётся при записи на сервис, обновляется всеми заданиями цепочки.
-- Выполните на DriveCareDB после Tasks_Add_ServiceBookingFields.sql и WorkshopServices_Tables.sql.

IF OBJECT_ID(N'dbo.ServiceDocuments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ServiceDocuments (
        RowId               UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ServiceDocuments PRIMARY KEY,
        RootTaskId          UNIQUEIDENTIFIER NOT NULL,
        RepairHistoryId     UNIQUEIDENTIFIER NULL,
        WorkshopId          UNIQUEIDENTIFIER NOT NULL,
        CarId               UNIQUEIDENTIFIER NULL,
        ClientUserId        UNIQUEIDENTIFIER NULL,
        Title               NVARCHAR(300)    NOT NULL,
        ClientName          NVARCHAR(200)    NULL,
        ClientPhone         NVARCHAR(50)     NULL,
        ClientEmail         NVARCHAR(200)    NULL,
        VisitReason         NVARCHAR(MAX)    NULL,
        SpecialNotes        NVARCHAR(MAX)    NULL,
        ServiceKind         NVARCHAR(50)     NULL,
        ReportText          NVARCHAR(MAX)    NULL,
        Status              TINYINT          NOT NULL CONSTRAINT DF_ServiceDocuments_Status DEFAULT(0),
        CreatedAt           DATETIME         NOT NULL CONSTRAINT DF_ServiceDocuments_Created DEFAULT(GETDATE()),
        CompletedAt         DATETIME         NULL
    );
    CREATE INDEX IX_ServiceDocuments_RootTask ON dbo.ServiceDocuments(RootTaskId);
    CREATE INDEX IX_ServiceDocuments_Repair ON dbo.ServiceDocuments(RepairHistoryId);
    CREATE INDEX IX_ServiceDocuments_Workshop ON dbo.ServiceDocuments(WorkshopId);
END
GO

IF OBJECT_ID(N'dbo.ServiceDocumentServiceLines', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ServiceDocumentServiceLines (
        RowId               UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ServiceDocumentServiceLines PRIMARY KEY,
        DocumentId          UNIQUEIDENTIFIER NOT NULL,
        WorkshopServiceId   UNIQUEIDENTIFIER NULL,
        ServiceName         NVARCHAR(300)    NOT NULL,
        Quantity            DECIMAL(18, 3)   NOT NULL CONSTRAINT DF_ServiceDocSvc_Qty DEFAULT(1),
        UnitName            NVARCHAR(30)     NULL,
        UnitPrice           DECIMAL(18, 2)   NOT NULL CONSTRAINT DF_ServiceDocSvc_Price DEFAULT(0),
        DiscountPercent     DECIMAL(9, 2)    NOT NULL CONSTRAINT DF_ServiceDocSvc_Disc DEFAULT(0),
        LineAmount          DECIMAL(18, 2)   NOT NULL CONSTRAINT DF_ServiceDocSvc_Amount DEFAULT(0),
        SortOrder           INT              NOT NULL CONSTRAINT DF_ServiceDocSvc_Sort DEFAULT(0)
    );
    CREATE INDEX IX_ServiceDocumentServiceLines_Doc ON dbo.ServiceDocumentServiceLines(DocumentId);
END
GO

IF OBJECT_ID(N'dbo.ServiceDocumentPartLines', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ServiceDocumentPartLines (
        RowId           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ServiceDocumentPartLines PRIMARY KEY,
        DocumentId      UNIQUEIDENTIFIER NOT NULL,
        WorkshopPartId  UNIQUEIDENTIFIER NULL,
        PartName        NVARCHAR(300)    NOT NULL,
        Quantity        DECIMAL(18, 3)   NOT NULL CONSTRAINT DF_ServiceDocPart_Qty DEFAULT(1),
        UnitName        NVARCHAR(30)     NULL,
        UnitPrice       DECIMAL(18, 2)   NOT NULL CONSTRAINT DF_ServiceDocPart_Price DEFAULT(0),
        LineAmount      DECIMAL(18, 2)   NOT NULL CONSTRAINT DF_ServiceDocPart_Amount DEFAULT(0),
        SortOrder       INT              NOT NULL CONSTRAINT DF_ServiceDocPart_Sort DEFAULT(0)
    );
    CREATE INDEX IX_ServiceDocumentPartLines_Doc ON dbo.ServiceDocumentPartLines(DocumentId);
END
GO

IF COL_LENGTH(N'dbo.Tasks', N'DocumentId') IS NULL
    ALTER TABLE dbo.Tasks ADD DocumentId UNIQUEIDENTIFIER NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Tasks_DocumentId' AND object_id = OBJECT_ID(N'dbo.Tasks'))
    CREATE INDEX IX_Tasks_DocumentId ON dbo.Tasks(DocumentId);
GO

IF OBJECT_ID(N'dbo.ServiceDocuments', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.FK_ServiceDocuments_RootTask', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.ServiceDocuments WITH CHECK
        ADD CONSTRAINT FK_ServiceDocuments_RootTask FOREIGN KEY (RootTaskId) REFERENCES dbo.Tasks(RowId);
END
GO

IF OBJECT_ID(N'dbo.ServiceDocumentServiceLines', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.FK_ServiceDocumentServiceLines_Document', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.ServiceDocumentServiceLines WITH CHECK
        ADD CONSTRAINT FK_ServiceDocumentServiceLines_Document FOREIGN KEY (DocumentId) REFERENCES dbo.ServiceDocuments(RowId) ON DELETE CASCADE;
END
GO

IF OBJECT_ID(N'dbo.ServiceDocumentPartLines', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.FK_ServiceDocumentPartLines_Document', N'F') IS NULL
BEGIN
    ALTER TABLE dbo.ServiceDocumentPartLines WITH CHECK
        ADD CONSTRAINT FK_ServiceDocumentPartLines_Document FOREIGN KEY (DocumentId) REFERENCES dbo.ServiceDocuments(RowId) ON DELETE CASCADE;
END
GO
