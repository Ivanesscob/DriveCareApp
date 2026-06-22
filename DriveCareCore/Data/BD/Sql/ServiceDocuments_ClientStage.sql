-- Этапы для клиента: принята → в ремонте → готова к выдаче → завершено.
-- Status: 0 = открыт, 1 = закрыт (выдан). ClientStage — текст на главной у пользователя.

IF OBJECT_ID(N'dbo.ServiceDocuments', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.ServiceDocuments', N'ClientStage') IS NULL
BEGIN
    ALTER TABLE dbo.ServiceDocuments
        ADD ClientStage TINYINT NOT NULL
            CONSTRAINT DF_ServiceDocuments_ClientStage DEFAULT (0);
END
GO

IF OBJECT_ID(N'dbo.ServiceDocuments', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.ServiceDocuments', N'ClientStage') IS NOT NULL
BEGIN
    -- Закрытые документы
    UPDATE dbo.ServiceDocuments SET ClientStage = 4 WHERE Status = 1 AND ClientStage IN (0, 4);

    -- Открытые без этапа — считаем «в ремонте»
    UPDATE dbo.ServiceDocuments SET ClientStage = 2 WHERE Status = 0 AND ClientStage = 0;
END
GO
