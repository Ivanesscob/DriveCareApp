-- Таблицы для записи на сервис без обязательной регистрации клиента в приложении.
-- Выполните на DriveCareDB перед использованием мастера записи.

IF OBJECT_ID(N'dbo.WorkshopServiceClients', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkshopServiceClients (
        RowId           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_WorkshopServiceClients PRIMARY KEY,
        WorkshopId      UNIQUEIDENTIFIER NOT NULL,
        UserId          UNIQUEIDENTIFIER NULL,
        FullName        NVARCHAR(200)    NOT NULL,
        Phone           NVARCHAR(50)     NULL,
        Email           NVARCHAR(200)    NULL,
        IsRegisteredUser BIT             NOT NULL CONSTRAINT DF_WorkshopServiceClients_IsReg DEFAULT(0),
        CreatedAt       DATETIME         NOT NULL CONSTRAINT DF_WorkshopServiceClients_Created DEFAULT(GETDATE())
    );
    CREATE INDEX IX_WorkshopServiceClients_Workshop ON dbo.WorkshopServiceClients(WorkshopId);
    CREATE INDEX IX_WorkshopServiceClients_User ON dbo.WorkshopServiceClients(UserId);
END
GO

IF OBJECT_ID(N'dbo.WorkshopGuestCars', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WorkshopGuestCars (
        RowId              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_WorkshopGuestCars PRIMARY KEY,
        WorkshopId         UNIQUEIDENTIFIER NOT NULL,
        ServiceClientId    UNIQUEIDENTIFIER NOT NULL,
        CarId              UNIQUEIDENTIFIER NOT NULL,
        RepairHistoryId    UNIQUEIDENTIFIER NULL,
        Vin                NVARCHAR(50)     NULL,
        PlateNumber        NVARCHAR(20)     NULL,
        BrandModelText     NVARCHAR(300)    NULL,
        [Year]             INT              NULL,
        Color              NVARCHAR(100)    NULL,
        Mileage            INT              NULL,
        IsLinkedToUser     BIT              NOT NULL CONSTRAINT DF_WorkshopGuestCars_Linked DEFAULT(0),
        UserCarId          UNIQUEIDENTIFIER NULL,
        CreatedAt          DATETIME         NOT NULL CONSTRAINT DF_WorkshopGuestCars_Created DEFAULT(GETDATE()),
        CONSTRAINT FK_WorkshopGuestCars_Client FOREIGN KEY (ServiceClientId) REFERENCES dbo.WorkshopServiceClients(RowId),
        CONSTRAINT FK_WorkshopGuestCars_Car FOREIGN KEY (CarId) REFERENCES dbo.Cars(RowId)
    );
    CREATE INDEX IX_WorkshopGuestCars_Workshop ON dbo.WorkshopGuestCars(WorkshopId);
    CREATE INDEX IX_WorkshopGuestCars_Client ON dbo.WorkshopGuestCars(ServiceClientId);
END
GO
