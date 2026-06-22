-- Отзывы о мастерских (оценка после завершения ремонта).
-- Важно: после ALTER TABLE ADD колонки нужен GO, иначе CREATE INDEX не видит новое поле.

IF OBJECT_ID(N'dbo.WorkshopReviews', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkshopReviews (
        RowId           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_WorkshopReviews PRIMARY KEY,
        WorkshopId      UNIQUEIDENTIFIER NOT NULL,
        UserId          UNIQUEIDENTIFIER NOT NULL,
        Rating          TINYINT          NOT NULL,
        Comment         NVARCHAR(1000)   NULL,
        Status          TINYINT          NOT NULL CONSTRAINT DF_WorkshopReviews_Status DEFAULT (1),
        CreatedAt       DATETIME         NOT NULL CONSTRAINT DF_WorkshopReviews_Created DEFAULT (GETDATE()),
        CONSTRAINT CK_WorkshopReviews_Rating CHECK (Rating BETWEEN 1 AND 5)
    );
END
GO

IF OBJECT_ID(N'dbo.WorkshopReviews', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.WorkshopReviews', N'DocumentId') IS NULL
        ALTER TABLE dbo.WorkshopReviews ADD DocumentId UNIQUEIDENTIFIER NULL;

    IF COL_LENGTH(N'dbo.WorkshopReviews', N'RepairHistoryId') IS NULL
        ALTER TABLE dbo.WorkshopReviews ADD RepairHistoryId UNIQUEIDENTIFIER NULL;

    IF COL_LENGTH(N'dbo.WorkshopReviews', N'Pros') IS NULL
        ALTER TABLE dbo.WorkshopReviews ADD Pros NVARCHAR(500) NULL;

    IF COL_LENGTH(N'dbo.WorkshopReviews', N'Cons') IS NULL
        ALTER TABLE dbo.WorkshopReviews ADD Cons NVARCHAR(500) NULL;

    IF COL_LENGTH(N'dbo.WorkshopReviews', N'OnlineBookingId') IS NULL
        ALTER TABLE dbo.WorkshopReviews ADD OnlineBookingId UNIQUEIDENTIFIER NULL;
END
GO

IF OBJECT_ID(N'dbo.WorkshopReviews', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.WorkshopReviews', N'OnlineBookingId') IS NOT NULL
   AND OBJECT_ID(N'dbo.WorkshopOnlineBookings', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1 FROM sys.foreign_keys
       WHERE name = N'FK_WorkshopReviews_OnlineBooking'
         AND parent_object_id = OBJECT_ID(N'dbo.WorkshopReviews'))
BEGIN
    ALTER TABLE dbo.WorkshopReviews
        ADD CONSTRAINT FK_WorkshopReviews_OnlineBooking
        FOREIGN KEY (OnlineBookingId) REFERENCES dbo.WorkshopOnlineBookings (RowId);
END
GO

IF OBJECT_ID(N'dbo.WorkshopReviews', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_WorkshopReviews_Workshop'
          AND object_id = OBJECT_ID(N'dbo.WorkshopReviews'))
    BEGIN
        CREATE INDEX IX_WorkshopReviews_Workshop
            ON dbo.WorkshopReviews (WorkshopId, CreatedAt DESC);
    END

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_WorkshopReviews_User'
          AND object_id = OBJECT_ID(N'dbo.WorkshopReviews'))
    BEGIN
        CREATE INDEX IX_WorkshopReviews_User
            ON dbo.WorkshopReviews (UserId, CreatedAt DESC);
    END

    IF COL_LENGTH(N'dbo.WorkshopReviews', N'DocumentId') IS NOT NULL
       AND NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'UX_WorkshopReviews_UserDocument'
          AND object_id = OBJECT_ID(N'dbo.WorkshopReviews'))
    BEGIN
        CREATE UNIQUE INDEX UX_WorkshopReviews_UserDocument
            ON dbo.WorkshopReviews (UserId, DocumentId)
            WHERE DocumentId IS NOT NULL;
    END

    IF COL_LENGTH(N'dbo.WorkshopReviews', N'OnlineBookingId') IS NOT NULL
       AND NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'UQ_WorkshopReviews_UserBooking'
          AND object_id = OBJECT_ID(N'dbo.WorkshopReviews'))
    BEGIN
        CREATE UNIQUE INDEX UQ_WorkshopReviews_UserBooking
            ON dbo.WorkshopReviews (UserId, OnlineBookingId);
    END
END
GO
