# Структура таблиц базы данных DriveCare

Текст для раздела «Физическое проектирование БД». Скопируйте блоки в Word.

Связи между таблицами задаются внешними ключами (FK). Для создания недостающих FK в Microsoft SQL Server выполните скрипт DriveCareCore\Data\BD\Sql\AllTables_ForeignKeys_Report.sql (после основных скриптов создания таблиц).

Таблица **Users** хранит информацию о учётных записях клиентов приложения DriveCare (логин, пароль, контакты). Структура таблицы представлена в таблице 2.1.

**Таблица 2.1 — Структура таблицы Users**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| Login | nvarchar(100) | NOT NULL | Логин для входа |
| Password | nvarchar(255) | NOT NULL | Пароль учётной записи |
| Description | nvarchar(255) | NULL | Описание |
| Email | nvarchar(100) | NOT NULL | Адрес электронной почты |
| BirthDate | date | NULL | BirthDate |
| Phone | nvarchar(50) | NULL | Номер телефона |
| CreatedAt | datetime | NOT NULL | Дата и время создания |

**Связи с другими таблицами:** UserRoles → Roles; UserCars → Cars; UserNotifications; UserCarSales; WorkshopConversations; WorkshopMessages; Tasks (ClientUserId); StoreOrders; WorkshopServiceClients.

Таблица **Roles** хранит информацию о ролях пользователей системы (например: пользователь, администратор, сотрудник). Структура таблицы представлена в таблице 2.2.

**Таблица 2.2 — Структура таблицы Roles**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| Name | nvarchar(100) | NOT NULL | Наименование |
| Description | nvarchar(255) | NULL | Описание |
| WorkshopId | uniqueidentifier | NULL, → Workshops | Ссылка на Workshop |
| IsActive | bit | NULL | Признак активности |
| CompanyId | uniqueidentifier | NULL, → Companies | Ссылка на Company |

**Связи с другими таблицами:** UserRoles; EmployeeRolesMap; RolePermissionsMap; RolePermissions; Companies; Workshops.

Таблица **Employees** хранит информацию о сотрудниках автосервиса для приложения DriveCare Pro. Структура таблицы представлена в таблице 2.3.

**Таблица 2.3 — Структура таблицы Employees**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| FirstName | nvarchar(100) | NOT NULL | FirstName |
| LastName | nvarchar(100) | NOT NULL | LastName |
| MidName | nvarchar(100) | NULL | MidName |
| Login | nvarchar(100) | NOT NULL | Логин для входа |
| Password | nvarchar(255) | NOT NULL | Пароль учётной записи |
| Email | nvarchar(100) | NOT NULL | Адрес электронной почты |
| Phone | nvarchar(50) | NULL | Номер телефона |
| BirthDate | date | NULL | BirthDate |
| HireDate | datetime | NULL | HireDate |
| IsActive | bit | NULL | Признак активности |
| Description | nvarchar(255) | NULL | Описание |
| WorkshopId | uniqueidentifier | NULL, → Workshops | Ссылка на Workshop |

**Связи с другими таблицами:** Workshops; EmployeeRolesMap → Roles; Tasks; RepairHistory; CarSales; WorkshopMessages; TaskPurchaseRequests.

Таблица **Workshops** хранит информацию о мастерских (филиалах автосервиса компании). Структура таблицы представлена в таблице 2.4.

**Таблица 2.4 — Структура таблицы Workshops**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| Name | nvarchar(200) | NOT NULL | Наименование |
| CompanyId | uniqueidentifier | NOT NULL, → Companies | Ссылка на Company |
| AddressId | uniqueidentifier | NULL, → Addresses | Ссылка на Address |
| Description | nvarchar(255) | NULL | Описание |
| BusinessTypeId | uniqueidentifier | NULL, → BusinessTypes | Ссылка на BusinessType |

**Связи с другими таблицами:** Companies; Addresses; BusinessTypes; Employees; WorkshopServiceUnits; WorkshopServices; WorkshopParts; WorkshopServiceClients; WorkshopGuestCars; WorkshopConversations; WorkshopOnlineBookings; WorkshopWorkSchedules; WorkshopBusinessTypes; ServiceDocuments; WorkshopPaintServices; AppActivityEvents.

Таблица **Tasks** хранит информацию о задачах мастерской — заказ-нарядах на ремонт и обслуживание. Структура таблицы представлена в таблице 2.5.

**Таблица 2.5 — Структура таблицы Tasks**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| Title | nvarchar(200) | NOT NULL | Заголовок |
| Description | nvarchar(max) | NULL | Описание |
| EmployeeId | uniqueidentifier | NOT NULL, → Employees | Ссылка на Employee |
| StatusId | uniqueidentifier | NOT NULL, → Statuses | Ссылка на Status |
| CreatedAt | datetime | NOT NULL | Дата и время создания |
| StartDate | datetime | NULL | StartDate |
| EndDate | datetime | NULL | EndDate |
| Deadline | datetime | NULL | Deadline |
| IsCompleted | bit | NOT NULL | IsCompleted |
| WorkHours | float | NULL | WorkHours |
| ReportText | nvarchar(max) | NULL | ReportText |
| ClientUserId | uniqueidentifier | NULL, → Users | Ссылка на ClientUser |
| CarId | uniqueidentifier | NULL, → Cars | Ссылка на Car |
| DocumentId | uniqueidentifier | NULL | Ссылка на Document |

**Связи с другими таблицами:** Employees; Statuses; Cars; Users; RepairHistory; TaskServiceLines; TaskPartLines; TaskPurchaseRequests; ServiceDocuments; EmployeeNotifications.

Таблица **Cars** хранит информацию о конкретных автомобилях (VIN, модель, госномер). Структура таблицы представлена в таблице 2.6.

**Таблица 2.6 — Структура таблицы Cars**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| ModelId | uniqueidentifier | NOT NULL, → Models | Ссылка на Model |
| CarTypeId | uniqueidentifier | NOT NULL, → CarTypes | Ссылка на CarType |
| FuelTypeId | uniqueidentifier | NOT NULL, → FuelTypes | Ссылка на FuelType |
| Year | int | NULL | Year |
| Description | nvarchar(255) | NULL | Описание |
| Vin | nvarchar(50) | NULL | Vin |
| PlateNumber | nvarchar(20) | NULL | PlateNumber |

**Связи с другими таблицами:** Models, CarTypes, FuelTypes; CarColors; UserCars; CarSales; RepairHistory; Tasks; WorkshopGuestCars; ServiceDocuments; UserWorkshopPaintInquiries.

Таблица **Addresses** хранит информацию о почтовых и фактических адресах. Структура таблицы представлена в таблице 2.7.

**Таблица 2.7 — Структура таблицы Addresses**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| CountryId | uniqueidentifier | NULL, → Countries | Ссылка на Country |
| City | nvarchar(100) | NULL | City |
| Street | nvarchar(200) | NULL | Street |
| House | nvarchar(50) | NULL | House |
| Apartment | nvarchar(50) | NULL | Apartment |
| FullAddress | nvarchar(404) | NOT NULL, COMPUTED | FullAddress |
| Description | nvarchar(255) | NULL | Описание |
| Latitude | float | NULL | Latitude |
| Longitude | float | NULL | Longitude |

**Связи с другими таблицами:** Countries; Workshops.

Таблица **AppActivityEvents** хранит информацию о действиях пользователей и сотрудников для статистики. Структура таблицы представлена в таблице 2.8.

**Таблица 2.8 — Структура таблицы AppActivityEvents**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID() | Идентификатор события |
| EventCode | nvarchar(80) | NOT NULL | Код события |
| ActorKind | tinyint | NOT NULL | Тип актора (0 — пользователь, 1 — сотрудник, 2 — система) |
| UserId | uniqueidentifier | NULL, FK -> Users | Пользователь |
| EmployeeId | uniqueidentifier | NULL, FK -> Employees | Сотрудник |
| WorkshopId | uniqueidentifier | NULL, FK -> Workshops | Мастерская |
| CompanyId | uniqueidentifier | NULL, FK -> Companies | Компания |
| EntityType | nvarchar(60) | NULL | Тип связанной сущности |
| EntityId | uniqueidentifier | NULL | Идентификатор сущности |
| PayloadJson | nvarchar(max) | NULL | Дополнительные данные (JSON) |
| CreatedAt | datetime2(0) | NOT NULL, DEFAULT SYSUTCDATETIME() | Дата и время события |

**Связи с другими таблицами:** Users; Employees; Workshops; Companies (логические FK).

Таблица **Brands** хранит информацию о марках автомобилей. Структура таблицы представлена в таблице 2.9.

**Таблица 2.9 — Структура таблицы Brands**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| Name | nvarchar(100) | NOT NULL | Наименование |
| Description | nvarchar(255) | NULL | Описание |

**Связи с другими таблицами:** Models.

Таблица **BusinessTypes** хранит информацию о видах деятельности автосервиса. Структура таблицы представлена в таблице 2.10.

**Таблица 2.10 — Структура таблицы BusinessTypes**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| Name | nvarchar(200) | NOT NULL | Наименование |
| Description | nvarchar(510) | NULL | Описание |

**Связи с другими таблицами:** Workshops; WorkshopBusinessTypes; WorkshopBusinessTypeChangeRequestTypes.

Таблица **CarColors** хранит информацию о истории окраски автомобиля. Структура таблицы представлена в таблице 2.11.

**Таблица 2.11 — Структура таблицы CarColors**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| CarId | uniqueidentifier | NOT NULL, → Cars | Ссылка на Car |
| ColorId | uniqueidentifier | NOT NULL, → Colors | Ссылка на Color |
| StartDate | datetime | NOT NULL | StartDate |
| EndDate | datetime | NULL | EndDate |
| Description | nvarchar(255) | NULL | Описание |
| PaintKind | tinyint | NULL | PaintKind |
| PartName | nvarchar(200) | NULL | PartName |

**Связи с другими таблицами:** Cars, Colors.

Таблица **CarSalePrices** хранит информацию о истории цен объявления о продаже. Структура таблицы представлена в таблице 2.12.

**Таблица 2.12 — Структура таблицы CarSalePrices**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| CarSaleId | uniqueidentifier | NOT NULL, → CarSales | Ссылка на CarSale |
| Price | decimal(18,2) | NOT NULL | Price |
| StartDate | datetime | NOT NULL | StartDate |
| EndDate | datetime | NULL | EndDate |
| Description | nvarchar(255) | NULL | Описание |

**Связи с другими таблицами:** CarSales.

Таблица **CarSales** хранит информацию о объявлениях о продаже автомобилей. Структура таблицы представлена в таблице 2.13.

**Таблица 2.13 — Структура таблицы CarSales**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| CarId | uniqueidentifier | NOT NULL, → Cars | Ссылка на Car |
| Title | nvarchar(200) | NOT NULL | Заголовок |
| Description | nvarchar(500) | NULL | Описание |
| PhotoPath | nvarchar(max) | NULL | PhotoPath |
| CreatedAt | datetime | NULL | Дата и время создания |
| StatusId | uniqueidentifier | NULL, → Statuses | Ссылка на Status |
| ModeratedByEmployeeId | uniqueidentifier | NULL, → Employees | Ссылка на ModeratedByEmployee |
| ModerationComment | nvarchar(1000) | NULL | ModerationComment |
| ModeratedAt | datetime | NULL | ModeratedAt |

**Связи с другими таблицами:** Cars; Statuses; Employees; CarSalePrices; UserCarSales.

Таблица **CarTypes** хранит информацию о типах кузова. Структура таблицы представлена в таблице 2.14.

**Таблица 2.14 — Структура таблицы CarTypes**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| Name | nvarchar(50) | NOT NULL | Наименование |
| Description | nvarchar(255) | NULL | Описание |

**Связи с другими таблицами:** Cars.

Таблица **Colors** хранит информацию о справочнике цветов. Структура таблицы представлена в таблице 2.15.

**Таблица 2.15 — Структура таблицы Colors**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| Name | nvarchar(50) | NOT NULL | Наименование |
| Description | nvarchar(255) | NULL | Описание |

**Связи с другими таблицами:** CarColors; Parts; WorkshopPaintColors; UserWorkshopPaintInquiries.

Таблица **Companies** хранит информацию о организациях — владельцах мастерских. Структура таблицы представлена в таблице 2.16.

**Таблица 2.16 — Структура таблицы Companies**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| Name | nvarchar(200) | NOT NULL | Наименование |
| Description | nvarchar(255) | NULL | Описание |

**Связи с другими таблицами:** Workshops; Roles; AppActivityEvents.

Таблица **Countries** хранит информацию о странах. Структура таблицы представлена в таблице 2.17.

**Таблица 2.17 — Структура таблицы Countries**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| Name | nvarchar(100) | NOT NULL | Наименование |
| Code | nvarchar(10) | NULL | Code |
| Description | nvarchar(255) | NULL | Описание |

**Связи с другими таблицами:** Addresses; PartManufacturers.

Таблица **EmployeeNotifications** хранит информацию о уведомлениях для сотрудников Pro. Структура таблицы представлена в таблице 2.18.

**Таблица 2.18 — Структура таблицы EmployeeNotifications**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| EmployeeId | uniqueidentifier | NOT NULL, → Employees | Ссылка на Employee |
| NotificationId | uniqueidentifier | NOT NULL, → Notifications | Ссылка на Notification |
| TaskId | uniqueidentifier | NULL, → Tasks | Ссылка на Task |
| IsRead | bit | NOT NULL | Признак прочтения |
| CreatedAt | datetime2(0) | NOT NULL | Дата и время создания |

**Связи с другими таблицами:** Employees, Notifications, Tasks.

Таблица **EmployeeRolesMap** хранит информацию о назначении ролей сотрудникам. Структура таблицы представлена в таблице 2.19.

**Таблица 2.19 — Структура таблицы EmployeeRolesMap**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| EmployeeId | uniqueidentifier | NOT NULL, → Employees | Ссылка на Employee |
| RoleId | uniqueidentifier | NOT NULL, → Roles | Ссылка на Role |
| Description | nvarchar(255) | NULL | Описание |

**Связи с другими таблицами:** Employees, Roles.

Таблица **FuelTypes** хранит информацию о типах топлива. Структура таблицы представлена в таблице 2.20.

**Таблица 2.20 — Структура таблицы FuelTypes**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| Name | nvarchar(50) | NOT NULL | Наименование |
| Description | nvarchar(255) | NULL | Описание |

**Связи с другими таблицами:** Cars.

Таблица **Models** хранит информацию о моделях автомобилей. Структура таблицы представлена в таблице 2.21.

**Таблица 2.21 — Структура таблицы Models**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| Name | nvarchar(100) | NOT NULL | Наименование |
| BrandId | uniqueidentifier | NOT NULL, → Brands | Ссылка на Brand |
| Description | nvarchar(255) | NULL | Описание |
| MaxSpeed | int | NULL | MaxSpeed |
| FuelConsumption | float | NULL | FuelConsumption |
| FuelType | nvarchar(50) | NULL | FuelType |
| HorsePower | int | NULL | HorsePower |
| EngineVolume | float | NULL | EngineVolume |
| Acceleration | float | NULL | Acceleration |

**Связи с другими таблицами:** Brands; Cars.

Таблица **Notifications** хранит информацию о шаблонах уведомлений. Структура таблицы представлена в таблице 2.22.

**Таблица 2.22 — Структура таблицы Notifications**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| Title | nvarchar(200) | NOT NULL | Заголовок |
| Message | nvarchar(500) | NOT NULL | Текст сообщения |
| Description | nvarchar(255) | NULL | Описание |
| CarId | uniqueidentifier | NULL, → Cars | Ссылка на Car |
| CreatedAt | datetime | NULL | Дата и время создания |
| IsViewed | bit | NOT NULL | IsViewed |

**Связи с другими таблицами:** Cars; UserNotifications; EmployeeNotifications.

Таблица **OrderPickupPoints** хранит информацию о пунктах выдачи заказов магазина. Структура таблицы представлена в таблице 2.23.

**Таблица 2.23 — Структура таблицы OrderPickupPoints**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| Code | nvarchar(16) | NOT NULL | Code |
| Name | nvarchar(120) | NOT NULL | Наименование |
| District | nvarchar(80) | NOT NULL | District |
| AddressLine | nvarchar(250) | NOT NULL | AddressLine |
| City | nvarchar(60) | NOT NULL | City |
| Latitude | float | NULL | Latitude |
| Longitude | float | NULL | Longitude |
| SortOrder | int | NOT NULL | SortOrder |
| IsActive | bit | NOT NULL | Признак активности |
| CreatedAt | datetime | NOT NULL | Дата и время создания |

**Связи с другими таблицами:** StoreOrders.

Таблица **PartManufacturers** хранит информацию о производителях запчастей. Структура таблицы представлена в таблице 2.24.

**Таблица 2.24 — Структура таблицы PartManufacturers**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| Name | nvarchar(200) | NOT NULL | Наименование |
| CountryId | uniqueidentifier | NULL, → Countries | Ссылка на Country |
| Description | nvarchar(255) | NULL | Описание |

**Связи с другими таблицами:** Countries; Parts.

Таблица **Parts** хранит информацию о каталоге запчастей. Структура таблицы представлена в таблице 2.25.

**Таблица 2.25 — Структура таблицы Parts**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| Name | nvarchar(200) | NOT NULL | Наименование |
| Article | nvarchar(100) | NOT NULL | Article |
| PartManufacturerId | uniqueidentifier | NOT NULL, → PartManufacturers | Ссылка на PartManufacturer |
| ColorId | uniqueidentifier | NULL, → Colors | Ссылка на Color |
| StatusId | uniqueidentifier | NULL, → Statuses | Ссылка на Status |
| Description | nvarchar(255) | NULL | Описание |

**Связи с другими таблицами:** PartManufacturers; Colors; Statuses; StoreOrderLines (ProductId).

Таблица **PermissionGroups** хранит информацию о группах прав доступа. Структура таблицы представлена в таблице 2.26.

**Таблица 2.26 — Структура таблицы PermissionGroups**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| Code | nvarchar(50) | NOT NULL | Code |
| Name | nvarchar(100) | NOT NULL | Наименование |
| Description | nvarchar(500) | NULL | Описание |

**Связи с другими таблицами:** Permissions.

Таблица **Permissions** хранит информацию о правах доступа (отдельные операции системы). Структура таблицы представлена в таблице 2.27.

**Таблица 2.27 — Структура таблицы Permissions**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| Code | nvarchar(100) | NOT NULL | Code |
| Name | nvarchar(200) | NOT NULL | Наименование |
| Description | nvarchar(500) | NULL | Описание |
| PermissionGroupId | uniqueidentifier | NULL, → PermissionGroups | Ссылка на PermissionGroup |

**Связи с другими таблицами:** PermissionGroups; RolePermissionsMap; RolePermissions.

Таблица **RepairCategories** хранит информацию о категориях ремонта. Структура таблицы представлена в таблице 2.28.

**Таблица 2.28 — Структура таблицы RepairCategories**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| Name | nvarchar(100) | NOT NULL | Наименование |
| Description | nvarchar(500) | NULL | Описание |

**Связи с другими таблицами:** RepairHistory.

Таблица **RepairHistory** хранит информацию о истории ремонтов автомобиля. Структура таблицы представлена в таблице 2.29.

**Таблица 2.29 — Структура таблицы RepairHistory**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| CarId | uniqueidentifier | NOT NULL, → Cars | Ссылка на Car |
| EmployeeId | uniqueidentifier | NULL, → Employees | Ссылка на Employee |
| Title | nvarchar(200) | NOT NULL | Заголовок |
| Description | nvarchar(max) | NULL | Описание |
| RepairDate | datetime | NOT NULL | RepairDate |
| EndDate | datetime | NULL | EndDate |
| Mileage | int | NULL | Mileage |
| Cost | decimal(18,2) | NULL | Cost |
| StatusId | uniqueidentifier | NULL, → Statuses | Ссылка на Status |
| CreatedAt | datetime | NOT NULL | Дата и время создания |
| CategoryId | uniqueidentifier | NULL, → RepairCategories | Ссылка на Category |

**Связи с другими таблицами:** Cars; RepairCategories; Employees; Statuses; Tasks; ServiceDocuments; WorkshopGuestCars.

Таблица **RolePermissions** хранит информацию о связи ролей и прав (альтернативная SQL-таблица). Структура таблицы представлена в таблице 2.30.

**Таблица 2.30 — Структура таблицы RolePermissions**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID() | Уникальный идентификатор записи |
| RoleId | uniqueidentifier | NOT NULL, FK -> Roles | Ссылка на роль |
| PermissionId | uniqueidentifier | NOT NULL, FK -> Permissions | Ссылка на право доступа |

**Связи с другими таблицами:** Roles, Permissions.

Таблица **RolePermissionsMap** хранит информацию о связи ролей и прав (RBAC). Структура таблицы представлена в таблице 2.31.

**Таблица 2.31 — Структура таблицы RolePermissionsMap**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| RoleId | uniqueidentifier | NOT NULL, → Roles | Ссылка на Role |
| PermissionId | uniqueidentifier | NOT NULL, → Permissions | Ссылка на Permission |

**Связи с другими таблицами:** Roles, Permissions.

Таблица **ServiceDocumentPartLines** хранит информацию о строках запчастей в документе обслуживания. Структура таблицы представлена в таблице 2.32.

**Таблица 2.32 — Структура таблицы ServiceDocumentPartLines**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| DocumentId | uniqueidentifier | NOT NULL, → ServiceDocuments | Ссылка на Document |
| WorkshopPartId | uniqueidentifier | NULL, → WorkshopParts | Ссылка на WorkshopPart |
| PartName | nvarchar(300) | NOT NULL | PartName |
| Quantity | decimal(18,3) | NOT NULL | Quantity |
| UnitName | nvarchar(30) | NULL | UnitName |
| UnitPrice | decimal(18,2) | NOT NULL | UnitPrice |
| LineAmount | decimal(18,2) | NOT NULL | LineAmount |
| SortOrder | int | NOT NULL | SortOrder |

**Связи с другими таблицами:** ServiceDocuments; WorkshopParts.

Таблица **ServiceDocuments** хранит информацию о актах и заказ-нарядах по завершённым работам. Структура таблицы представлена в таблице 2.33.

**Таблица 2.33 — Структура таблицы ServiceDocuments**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| RootTaskId | uniqueidentifier | NOT NULL, → Tasks | Ссылка на RootTask |
| RepairHistoryId | uniqueidentifier | NULL, → RepairHistory | Ссылка на RepairHistory |
| WorkshopId | uniqueidentifier | NOT NULL, → Workshops | Ссылка на Workshop |
| CarId | uniqueidentifier | NULL, → Cars | Ссылка на Car |
| ClientUserId | uniqueidentifier | NULL, → Users | Ссылка на ClientUser |
| Title | nvarchar(300) | NOT NULL | Заголовок |
| ClientName | nvarchar(200) | NULL | ClientName |
| ClientPhone | nvarchar(50) | NULL | ClientPhone |
| ClientEmail | nvarchar(200) | NULL | ClientEmail |
| VisitReason | nvarchar(max) | NULL | VisitReason |
| SpecialNotes | nvarchar(max) | NULL | SpecialNotes |
| ServiceKind | nvarchar(50) | NULL | ServiceKind |
| ReportText | nvarchar(max) | NULL | ReportText |
| Status | tinyint | NOT NULL | Status |
| CreatedAt | datetime | NOT NULL | Дата и время создания |
| CompletedAt | datetime | NULL | CompletedAt |

**Связи с другими таблицами:** Tasks (RootTaskId); Workshops; Cars; Users; RepairHistory; ServiceDocumentServiceLines; ServiceDocumentPartLines.

Таблица **ServiceDocumentServiceLines** хранит информацию о строках услуг в документе. Структура таблицы представлена в таблице 2.34.

**Таблица 2.34 — Структура таблицы ServiceDocumentServiceLines**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| DocumentId | uniqueidentifier | NOT NULL, → ServiceDocuments | Ссылка на Document |
| WorkshopServiceId | uniqueidentifier | NULL, → WorkshopServices | Ссылка на WorkshopService |
| ServiceName | nvarchar(300) | NOT NULL | ServiceName |
| Quantity | decimal(18,3) | NOT NULL | Quantity |
| UnitName | nvarchar(30) | NULL | UnitName |
| UnitPrice | decimal(18,2) | NOT NULL | UnitPrice |
| DiscountPercent | decimal(9,2) | NOT NULL | DiscountPercent |
| LineAmount | decimal(18,2) | NOT NULL | LineAmount |
| SortOrder | int | NOT NULL | SortOrder |

**Связи с другими таблицами:** ServiceDocuments; WorkshopServices.

Таблица **Statuses** хранит информацию о статусах (задачи, объявления, ремонт). Структура таблицы представлена в таблице 2.35.

**Таблица 2.35 — Структура таблицы Statuses**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| Name | nvarchar(100) | NOT NULL | Наименование |
| Description | nvarchar(255) | NULL | Описание |

**Связи с другими таблицами:** Tasks; RepairHistory; CarSales; Parts.

Таблица **StoreOrderLines** хранит информацию о позициях заказа интернет-магазина. Структура таблицы представлена в таблице 2.36.

**Таблица 2.36 — Структура таблицы StoreOrderLines**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| OrderId | uniqueidentifier | NOT NULL, → StoreOrders | Ссылка на Order |
| ProductId | uniqueidentifier | NOT NULL, → Parts | Ссылка на Product |
| ProductName | nvarchar(200) | NOT NULL | ProductName |
| Category | nvarchar(40) | NULL | Category |
| Quantity | int | NOT NULL | Quantity |
| UnitPrice | decimal(18,2) | NOT NULL | UnitPrice |
| SortOrder | int | NOT NULL | SortOrder |

**Связи с другими таблицами:** StoreOrders; Parts (ProductId).

Таблица **StoreOrders** хранит информацию о заказах клиентов в магазине DriveCare. Структура таблицы представлена в таблице 2.37.

**Таблица 2.37 — Структура таблицы StoreOrders**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| UserId | uniqueidentifier | NOT NULL, → Users | Ссылка на User |
| PickupPointId | uniqueidentifier | NOT NULL, → OrderPickupPoints | Ссылка на PickupPoint |
| OrderNumber | nvarchar(32) | NOT NULL | OrderNumber |
| Status | tinyint | NOT NULL | Status |
| TotalAmount | decimal(18,2) | NOT NULL | TotalAmount |
| QrPayload | nvarchar(500) | NULL | QrPayload |
| CreatedAt | datetime | NOT NULL | Дата и время создания |
| PaidAt | datetime | NULL | PaidAt |

**Связи с другими таблицами:** Users; OrderPickupPoints; StoreOrderLines.

Таблица **TaskPartLines** хранит информацию о запчастях в задаче мастерской. Структура таблицы представлена в таблице 2.38.

**Таблица 2.38 — Структура таблицы TaskPartLines**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| TaskId | uniqueidentifier | NOT NULL, → Tasks | Ссылка на Task |
| PartName | nvarchar(300) | NOT NULL | PartName |
| Quantity | decimal(18,3) | NOT NULL | Quantity |
| UnitName | nvarchar(30) | NULL | UnitName |
| UnitPrice | decimal(18,2) | NOT NULL | UnitPrice |
| LineAmount | decimal(18,2) | NOT NULL | LineAmount |
| SortOrder | int | NOT NULL | SortOrder |
| WorkshopPartId | uniqueidentifier | NULL | Ссылка на WorkshopPart |

**Связи с другими таблицами:** Tasks; WorkshopParts.

Таблица **TaskPurchaseRequestLines** хранит информацию о строках заявки на закупку. Структура таблицы представлена в таблице 2.39.

**Таблица 2.39 — Структура таблицы TaskPurchaseRequestLines**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| RequestId | uniqueidentifier | NOT NULL, → TaskPurchaseRequests | Ссылка на Request |
| WorkshopPartId | uniqueidentifier | NULL | Ссылка на WorkshopPart |
| PartName | nvarchar(300) | NOT NULL | PartName |
| Quantity | decimal(18,3) | NOT NULL | Quantity |
| UnitName | nvarchar(30) | NULL | UnitName |
| UnitPrice | decimal(18,2) | NOT NULL | UnitPrice |
| SortOrder | int | NOT NULL | SortOrder |

**Связи с другими таблицами:** TaskPurchaseRequests; WorkshopParts.

Таблица **TaskPurchaseRequests** хранит информацию о заявках на закупку запчастей по задаче. Структура таблицы представлена в таблице 2.40.

**Таблица 2.40 — Структура таблицы TaskPurchaseRequests**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| SourceTaskId | uniqueidentifier | NOT NULL, → Tasks | Ссылка на SourceTask |
| PurchaseTaskId | uniqueidentifier | NOT NULL, → Tasks | Ссылка на PurchaseTask |
| RequestedByEmployeeId | uniqueidentifier | NOT NULL, → Employees | Ссылка на RequestedByEmployee |
| PurchaserEmployeeId | uniqueidentifier | NOT NULL, → Employees | Ссылка на PurchaserEmployee |
| IsFulfilled | bit | NOT NULL | IsFulfilled |
| CreatedAt | datetime | NOT NULL | Дата и время создания |

**Связи с другими таблицами:** Tasks; Employees; TaskPurchaseRequestLines.

Таблица **TaskServiceLines** хранит информацию о услугах в задаче мастерской. Структура таблицы представлена в таблице 2.41.

**Таблица 2.41 — Структура таблицы TaskServiceLines**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| TaskId | uniqueidentifier | NOT NULL, → Tasks | Ссылка на Task |
| WorkshopServiceId | uniqueidentifier | NULL | Ссылка на WorkshopService |
| ServiceName | nvarchar(300) | NOT NULL | ServiceName |
| Quantity | decimal(18,3) | NOT NULL | Quantity |
| UnitName | nvarchar(30) | NULL | UnitName |
| UnitPrice | decimal(18,2) | NOT NULL | UnitPrice |
| DiscountPercent | decimal(9,2) | NOT NULL | DiscountPercent |
| LineAmount | decimal(18,2) | NOT NULL | LineAmount |
| SortOrder | int | NOT NULL | SortOrder |

**Связи с другими таблицами:** Tasks; WorkshopServices.

Таблица **UserCarComponentStatuses** хранит информацию о состоянии узлов авто в гараже пользователя. Структура таблицы представлена в таблице 2.42.

**Таблица 2.42 — Структура таблицы UserCarComponentStatuses**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| UserCarRowId | uniqueidentifier | NOT NULL, → UserCars | Ссылка на UserCarRow |
| ComponentCode | nvarchar(32) | NOT NULL | ComponentCode |
| StatusLevel | tinyint | NOT NULL | StatusLevel |
| LastServiceDate | datetime | NULL | LastServiceDate |
| LastMileageKm | int | NULL | LastMileageKm |
| RemainingKmHint | int | NULL | RemainingKmHint |
| ShortHint | nvarchar(200) | NULL | ShortHint |
| UpdatedAt | datetime | NOT NULL | UpdatedAt |

**Связи с другими таблицами:** UserCars.

Таблица **UserCarMaintenanceHistory** хранит информацию о истории обслуживания авто пользователя. Структура таблицы представлена в таблице 2.43.

**Таблица 2.43 — Структура таблицы UserCarMaintenanceHistory**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| UserCarRowId | uniqueidentifier | NOT NULL, → UserCars | Ссылка на UserCarRow |
| ServiceDate | datetime | NOT NULL | ServiceDate |
| MileageKm | int | NULL | MileageKm |
| Title | nvarchar(200) | NULL | Заголовок |
| Notes | nvarchar(max) | NULL | Notes |
| ComponentCode | nvarchar(32) | NULL | ComponentCode |
| WorkshopName | nvarchar(120) | NULL | WorkshopName |
| SeverityAfter | tinyint | NULL | SeverityAfter |

**Связи с другими таблицами:** UserCars.

Таблица **UserCars** хранит информацию о связи пользователя с автомобилем в личном гараже. Структура таблицы представлена в таблице 2.44.

**Таблица 2.44 — Структура таблицы UserCars**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| UserId | uniqueidentifier | NOT NULL, → Users | Ссылка на User |
| CarId | uniqueidentifier | NOT NULL, → Cars | Ссылка на Car |
| Description | nvarchar(255) | NULL | Описание |

**Связи с другими таблицами:** Users, Cars; UserCarMaintenanceHistory; UserCarComponentStatuses; WorkshopOnlineBookings.

Таблица **UserCarSales** хранит информацию о связи пользователя с объявлениями о продаже. Структура таблицы представлена в таблице 2.45.

**Таблица 2.45 — Структура таблицы UserCarSales**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| UserId | uniqueidentifier | NOT NULL, → Users | Ссылка на User |
| CarSaleId | uniqueidentifier | NOT NULL, → CarSales | Ссылка на CarSale |
| Description | nvarchar(255) | NULL | Описание |

**Связи с другими таблицами:** Users, CarSales.

Таблица **UserNotifications** хранит информацию о уведомлениях, доставленных пользователю. Структура таблицы представлена в таблице 2.46.

**Таблица 2.46 — Структура таблицы UserNotifications**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| UserId | uniqueidentifier | NOT NULL, → Users | Ссылка на User |
| NotificationId | uniqueidentifier | NOT NULL, → Notifications | Ссылка на Notification |
| IsRead | bit | NULL | Признак прочтения |
| Description | nvarchar(255) | NULL | Описание |

**Связи с другими таблицами:** Users, Notifications.

Таблица **UserRoles** хранит информацию о назначении ролей клиентам DriveCare. Структура таблицы представлена в таблице 2.47.

**Таблица 2.47 — Структура таблицы UserRoles**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| UserId | uniqueidentifier | NOT NULL, → Users | Ссылка на User |
| RoleId | uniqueidentifier | NOT NULL, → Roles | Ссылка на Role |
| Description | nvarchar(255) | NULL | Описание |

**Связи с другими таблицами:** Users, Roles.

Таблица **UserWorkshopPaintInquiries** хранит информацию о запросах пользователей на покраску. Структура таблицы представлена в таблице 2.48.

**Таблица 2.48 — Структура таблицы UserWorkshopPaintInquiries**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| UserId | uniqueidentifier | NOT NULL, → Users | Ссылка на User |
| UserCarId | uniqueidentifier | NOT NULL, → UserCars | Ссылка на UserCar |
| CarId | uniqueidentifier | NOT NULL, → Cars | Ссылка на Car |
| WorkshopId | uniqueidentifier | NOT NULL, → Workshops | Ссылка на Workshop |
| WorkshopPaintServiceId | uniqueidentifier | NULL, → WorkshopPaintServices | Ссылка на WorkshopPaintService |
| PaintKind | tinyint | NOT NULL | PaintKind |
| ColorId | uniqueidentifier | NULL, → Colors | Ссылка на Color |
| ColorName | nvarchar(120) | NOT NULL | ColorName |
| PartName | nvarchar(200) | NULL | PartName |
| Notes | nvarchar(500) | NULL | Notes |
| StatusCode | tinyint | NOT NULL | StatusCode |
| CreatedAt | datetime2(7) | NOT NULL | Дата и время создания |

**Связи с другими таблицами:** Users; UserCars; Cars; Workshops; WorkshopPaintServices; Colors.

Таблица **WarehouseManagers** хранит информацию о ответственных за склад. Структура таблицы представлена в таблице 2.49.

**Таблица 2.49 — Структура таблицы WarehouseManagers**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| FirstName | nvarchar(100) | NOT NULL | FirstName |
| LastName | nvarchar(100) | NOT NULL | LastName |
| MidName | nvarchar(100) | NOT NULL | MidName |
| Phone | nvarchar(50) | NULL | Номер телефона |
| Email | nvarchar(100) | NULL | Адрес электронной почты |
| Description | nvarchar(255) | NULL | Описание |

**Связи с другими таблицами:** Справочник контактов склада (связь с Workshops — на уровне приложения, без FK в БД).

Таблица **WorkshopBusinessTypeChangeRequests** хранит информацию о заявках на изменение типов мастерской. Структура таблицы представлена в таблице 2.50.

**Таблица 2.50 — Структура таблицы WorkshopBusinessTypeChangeRequests**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| WorkshopId | uniqueidentifier | NOT NULL, → Workshops | Ссылка на Workshop |
| RequestedByEmployeeId | uniqueidentifier | NOT NULL, → Employees | Ссылка на RequestedByEmployee |
| Status | tinyint | NOT NULL | Status |
| OwnerComment | nvarchar(500) | NULL | OwnerComment |
| ModerationComment | nvarchar(500) | NULL | ModerationComment |
| ModeratedByEmployeeId | uniqueidentifier | NULL | Ссылка на ModeratedByEmployee |
| CreatedAt | datetime | NOT NULL | Дата и время создания |
| ModeratedAt | datetime | NULL | ModeratedAt |

**Связи с другими таблицами:** Workshops; Employees.

Таблица **WorkshopBusinessTypeChangeRequestTypes** хранит информацию о типах бизнеса в заявке на смену профиля. Структура таблицы представлена в таблице 2.51.

**Таблица 2.51 — Структура таблицы WorkshopBusinessTypeChangeRequestTypes**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RequestId | uniqueidentifier | PK, NOT NULL, → WorkshopBusinessTypeChangeRequests | Ссылка на Request |
| BusinessTypeId | uniqueidentifier | PK, NOT NULL, → BusinessTypes | Ссылка на BusinessType |

**Связи с другими таблицами:** WorkshopBusinessTypeChangeRequests, BusinessTypes.

Таблица **WorkshopBusinessTypes** хранит информацию о дополнительных видах деятельности мастерской. Структура таблицы представлена в таблице 2.52.

**Таблица 2.52 — Структура таблицы WorkshopBusinessTypes**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| WorkshopId | uniqueidentifier | NOT NULL, → Workshops | Ссылка на Workshop |
| BusinessTypeId | uniqueidentifier | NOT NULL, → BusinessTypes | Ссылка на BusinessType |

**Связи с другими таблицами:** Workshops, BusinessTypes.

Таблица **WorkshopConversations** хранит информацию о диалогах чата между клиентом и мастерской. Структура таблицы представлена в таблице 2.53.

**Таблица 2.53 — Структура таблицы WorkshopConversations**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| WorkshopId | uniqueidentifier | NOT NULL, → Workshops | Ссылка на Workshop |
| UserId | uniqueidentifier | NOT NULL, → Users | Ссылка на User |
| WorkshopServiceClientId | uniqueidentifier | NULL, → WorkshopServiceClients | Ссылка на WorkshopServiceClient |
| Subject | nvarchar(200) | NULL | Subject |
| LastMessageAt | datetime | NOT NULL | LastMessageAt |
| LastMessagePreview | nvarchar(200) | NULL | LastMessagePreview |
| UnreadForUser | int | NOT NULL | UnreadForUser |
| UnreadForWorkshop | int | NOT NULL | UnreadForWorkshop |
| CreatedAt | datetime | NOT NULL | Дата и время создания |

**Связи с другими таблицами:** Workshops, Users; WorkshopServiceClients; WorkshopMessages.

Таблица **WorkshopGuestCars** хранит информацию о автомобилях клиентов мастерской. Структура таблицы представлена в таблице 2.54.

**Таблица 2.54 — Структура таблицы WorkshopGuestCars**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| WorkshopId | uniqueidentifier | NOT NULL, → Workshops | Ссылка на Workshop |
| ServiceClientId | uniqueidentifier | NOT NULL, → WorkshopServiceClients | Ссылка на ServiceClient |
| CarId | uniqueidentifier | NOT NULL, → Cars | Ссылка на Car |
| RepairHistoryId | uniqueidentifier | NULL | Ссылка на RepairHistory |
| Vin | nvarchar(50) | NULL | Vin |
| PlateNumber | nvarchar(20) | NULL | PlateNumber |
| BrandModelText | nvarchar(300) | NULL | BrandModelText |
| Year | int | NULL | Year |
| Color | nvarchar(100) | NULL | Color |
| Mileage | int | NULL | Mileage |
| IsLinkedToUser | bit | NOT NULL | IsLinkedToUser |
| UserCarId | uniqueidentifier | NULL, → UserCars | Ссылка на UserCar |
| CreatedAt | datetime | NOT NULL | Дата и время создания |

**Связи с другими таблицами:** Workshops; Cars; RepairHistory; WorkshopServiceClients; UserCars.

Таблица **WorkshopMessages** хранит информацию о сообщениях в чате. Структура таблицы представлена в таблице 2.55.

**Таблица 2.55 — Структура таблицы WorkshopMessages**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| ConversationId | uniqueidentifier | NOT NULL, → WorkshopConversations | Ссылка на Conversation |
| SenderKind | tinyint | NOT NULL | SenderKind |
| SenderUserId | uniqueidentifier | NULL, → Users | Ссылка на SenderUser |
| SenderEmployeeId | uniqueidentifier | NULL, → Employees | Ссылка на SenderEmployee |
| Body | nvarchar(2000) | NOT NULL | Body |
| CreatedAt | datetime | NOT NULL | Дата и время создания |

**Связи с другими таблицами:** WorkshopConversations; Users; Employees.

Таблица **WorkshopOnlineBookings** хранит информацию о онлайн-записях на обслуживание. Структура таблицы представлена в таблице 2.56.

**Таблица 2.56 — Структура таблицы WorkshopOnlineBookings**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| WorkshopId | uniqueidentifier | NOT NULL, → Workshops | Ссылка на Workshop |
| UserId | uniqueidentifier | NOT NULL, → Users | Ссылка на User |
| UserCarId | uniqueidentifier | NULL, → UserCars | Ссылка на UserCar |
| ClientPhone | nvarchar(50) | NULL | ClientPhone |
| ClientComment | nvarchar(500) | NULL | ClientComment |
| PreferredDate | datetime | NULL | PreferredDate |
| Status | tinyint | NOT NULL | Status |
| CreatedAt | datetime | NOT NULL | Дата и время создания |
| ConfirmedAt | datetime | NULL | ConfirmedAt |
| ConfirmedByEmployeeId | uniqueidentifier | NULL, → Employees | Ссылка на ConfirmedByEmployee |
| IssueCategory | nvarchar(120) | NULL | IssueCategory |
| RejectReason | nvarchar(500) | NULL | RejectReason |
| RejectedAt | datetime | NULL | RejectedAt |
| RejectedByEmployeeId | uniqueidentifier | NULL, → Employees | Ссылка на RejectedByEmployee |

**Связи с другими таблицами:** Workshops; Users; UserCars; Employees (подтверждение).

Таблица **WorkshopOnlineBookingSettings** хранит информацию о настройках лимита онлайн-записей. Структура таблицы представлена в таблице 2.57.

**Таблица 2.57 — Структура таблицы WorkshopOnlineBookingSettings**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| WorkshopId | uniqueidentifier | PK, NOT NULL, → Workshops | Ссылка на Workshop |
| MaxBookingsPerDay | int | NOT NULL | MaxBookingsPerDay |

**Связи с другими таблицами:** Workshops.

Таблица **WorkshopPaintColors** хранит информацию о доступных цветах покраски. Структура таблицы представлена в таблице 2.58.

**Таблица 2.58 — Структура таблицы WorkshopPaintColors**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| WorkshopId | uniqueidentifier | NOT NULL, → Workshops | Ссылка на Workshop |
| ColorId | uniqueidentifier | NULL, → Colors | Ссылка на Color |
| ColorName | nvarchar(120) | NOT NULL | ColorName |
| IsActive | bit | NOT NULL | Признак активности |
| SortOrder | int | NOT NULL | SortOrder |
| CreatedAt | datetime2(7) | NOT NULL | Дата и время создания |

**Связи с другими таблицами:** Workshops; Colors.

Таблица **WorkshopPaintServices** хранит информацию о услугах покраски мастерской. Структура таблицы представлена в таблице 2.59.

**Таблица 2.59 — Структура таблицы WorkshopPaintServices**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| WorkshopId | uniqueidentifier | NOT NULL, → Workshops | Ссылка на Workshop |
| PaintKind | tinyint | NOT NULL | PaintKind |
| Name | nvarchar(200) | NOT NULL | Наименование |
| Description | nvarchar(500) | NULL | Описание |
| PriceFrom | decimal(18,2) | NULL | PriceFrom |
| IsActive | bit | NOT NULL | Признак активности |
| SortOrder | int | NOT NULL | SortOrder |
| CreatedAt | datetime2(7) | NOT NULL | Дата и время создания |

**Связи с другими таблицами:** Workshops; UserWorkshopPaintInquiries.

Таблица **WorkshopParts** хранит информацию о складе запчастей мастерской. Структура таблицы представлена в таблице 2.60.

**Таблица 2.60 — Структура таблицы WorkshopParts**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| WorkshopId | uniqueidentifier | NOT NULL, → Workshops | Ссылка на Workshop |
| Name | nvarchar(300) | NOT NULL | Наименование |
| Article | nvarchar(80) | NULL | Article |
| Description | nvarchar(500) | NULL | Описание |
| Price | decimal(18,2) | NOT NULL | Price |
| UnitName | nvarchar(30) | NOT NULL | UnitName |
| QuantityOnHand | decimal(18,3) | NOT NULL | QuantityOnHand |
| Category | nvarchar(40) | NOT NULL | Category |
| IsActive | bit | NOT NULL | Признак активности |
| CreatedAt | datetime | NOT NULL | Дата и время создания |

**Связи с другими таблицами:** Workshops; TaskPartLines; TaskPurchaseRequestLines; ServiceDocumentPartLines.

Таблица **WorkshopServiceClients** хранит информацию о клиентской базе мастерской. Структура таблицы представлена в таблице 2.61.

**Таблица 2.61 — Структура таблицы WorkshopServiceClients**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| WorkshopId | uniqueidentifier | NOT NULL, → Workshops | Ссылка на Workshop |
| UserId | uniqueidentifier | NULL | Ссылка на User |
| FullName | nvarchar(200) | NOT NULL | FullName |
| Phone | nvarchar(50) | NULL | Номер телефона |
| Email | nvarchar(200) | NULL | Адрес электронной почты |
| IsRegisteredUser | bit | NOT NULL | IsRegisteredUser |
| CreatedAt | datetime | NOT NULL | Дата и время создания |

**Связи с другими таблицами:** Workshops; Users; WorkshopConversations; WorkshopOnlineBookings; WorkshopGuestCars.

Таблица **WorkshopServices** хранит информацию о каталоге услуг с ценами. Структура таблицы представлена в таблице 2.62.

**Таблица 2.62 — Структура таблицы WorkshopServices**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| WorkshopId | uniqueidentifier | NOT NULL, → Workshops | Ссылка на Workshop |
| Name | nvarchar(300) | NOT NULL | Наименование |
| Description | nvarchar(max) | NULL | Описание |
| Price | decimal(18,2) | NOT NULL | Price |
| UnitName | nvarchar(30) | NULL | UnitName |
| IsActive | bit | NOT NULL | Признак активности |
| SortOrder | int | NOT NULL | SortOrder |
| CreatedAt | datetime | NOT NULL | Дата и время создания |
| UnitId | uniqueidentifier | NULL, → WorkshopServiceUnits | Ссылка на Unit |

**Связи с другими таблицами:** Workshops; WorkshopServiceUnits; TaskServiceLines; ServiceDocumentServiceLines.

Таблица **WorkshopServiceUnits** хранит информацию о единицах измерения услуг. Структура таблицы представлена в таблице 2.63.

**Таблица 2.63 — Структура таблицы WorkshopServiceUnits**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| WorkshopId | uniqueidentifier | NOT NULL, → Workshops | Ссылка на Workshop |
| Name | nvarchar(30) | NOT NULL | Наименование |
| IsActive | bit | NOT NULL | Признак активности |
| CreatedAt | datetime | NOT NULL | Дата и время создания |

**Связи с другими таблицами:** Workshops; WorkshopServices.

Таблица **WorkshopWorkSchedules** хранит информацию о расписании работы по дням недели. Структура таблицы представлена в таблице 2.64.

**Таблица 2.64 — Структура таблицы WorkshopWorkSchedules**

| Поле | Тип данных | Ограничения | Описание |
|------|------------|-------------|----------|
| RowId | uniqueidentifier | PK, DEFAULT NEWID(), NOT NULL | Уникальный идентификатор записи |
| WorkshopId | uniqueidentifier | NOT NULL, → Workshops | Ссылка на Workshop |
| DayOfWeek | tinyint | NOT NULL | DayOfWeek |
| IsClosed | bit | NOT NULL | IsClosed |
| OpenTime | time(0) | NULL | OpenTime |
| CloseTime | time(0) | NULL | CloseTime |

**Связи с другими таблицами:** Workshops.
