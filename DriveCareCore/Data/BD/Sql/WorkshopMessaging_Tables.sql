-- Переписка мастерской (компании) с посетителем (пользователем DriveCare).
IF OBJECT_ID(N'dbo.WorkshopConversations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkshopConversations (
        RowId               UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_WorkshopConversations PRIMARY KEY,
        WorkshopId          UNIQUEIDENTIFIER NOT NULL,
        UserId              UNIQUEIDENTIFIER NOT NULL,
        WorkshopServiceClientId UNIQUEIDENTIFIER NULL,
        Subject             NVARCHAR(200)    NULL,
        LastMessageAt       DATETIME         NOT NULL,
        LastMessagePreview  NVARCHAR(200)    NULL,
        UnreadForUser       INT              NOT NULL CONSTRAINT DF_WorkshopConversations_UnreadUser DEFAULT (0),
        UnreadForWorkshop   INT              NOT NULL CONSTRAINT DF_WorkshopConversations_UnreadWs DEFAULT (0),
        CreatedAt           DATETIME         NOT NULL
    );

    CREATE UNIQUE INDEX UX_WorkshopConversations_WorkshopUser
        ON dbo.WorkshopConversations (WorkshopId, UserId);

    CREATE INDEX IX_WorkshopConversations_User
        ON dbo.WorkshopConversations (UserId, LastMessageAt DESC);

    CREATE INDEX IX_WorkshopConversations_Workshop
        ON dbo.WorkshopConversations (WorkshopId, LastMessageAt DESC);
END
GO

IF OBJECT_ID(N'dbo.WorkshopMessages', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkshopMessages (
        RowId               UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_WorkshopMessages PRIMARY KEY,
        ConversationId      UNIQUEIDENTIFIER NOT NULL,
        SenderKind          TINYINT          NOT NULL,
        SenderUserId        UNIQUEIDENTIFIER NULL,
        SenderEmployeeId    UNIQUEIDENTIFIER NULL,
        Body                NVARCHAR(2000)   NOT NULL,
        CreatedAt           DATETIME         NOT NULL
    );

    CREATE INDEX IX_WorkshopMessages_Conversation
        ON dbo.WorkshopMessages (ConversationId, CreatedAt);
END
GO
