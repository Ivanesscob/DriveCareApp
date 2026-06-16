СПРАВОЧНИК ТАБЛИЦ БАЗЫ ДАННЫХ DRIVECARE

Для кого: чтобы сверить обновлённую ER-диаграмму с реальной логикой системы.
Формат: таблица — зачем нужна — с какими таблицами связана (стрелка означает «ссылается на»).

Одна база Microsoft SQL Server. Внешние ключи связывают строки по полю RowId (уникальный идентификатор), если не указано иное.


КАК ЧИТАТЬ СВЯЗИ

Users (1) —— (много) UserCars —— (1) Cars
означает: у одного пользователя может быть несколько записей в UserCars, каждая ссылается на один автомобиль Cars.

Пустая связь «справочник» — таблица почти не ссылается на другие, на неё ссылаются многие.


================================================================================
БЛОК A. ПОЛЬЗОВАТЕЛИ, СОТРУДНИКИ, ПРАВА
================================================================================

Users
Зачем: учётные записи клиентов приложения DriveCare (логин, пароль, email, телефон, дата рождения).
Связи: UserRoles → Roles; UserCars → Cars; UserNotifications; UserCarSales; WorkshopConversations; WorkshopMessages (как отправитель); Tasks (ClientUserId); StoreOrders; WorkshopServiceClients (опционально UserId).

UserRoles
Зачем: какая роль назначена пользователю (несколько ролей на одного человека).
Связи: Users, Roles.

Roles
Зачем: справочник ролей (клиент, продавец и т.д. — по настройке проекта).
Связи: UserRoles; EmployeeRolesMap; RolePermissionsMap; RolePermissions; может быть привязана к Companies или Workshops (роль «внутри компании/мастерской»).

Employees
Зачем: сотрудники автосервиса для DriveCare Pro (логин, ФИО, привязка к мастерской).
Связи: Workshops; EmployeeRolesMap → Roles; Tasks (исполнитель); RepairHistory; CarSales (модератор); WorkshopMessages (как отправитель); TaskPurchaseRequests (кто заказал/кто покупает).

EmployeeRolesMap
Зачем: связь «сотрудник — роль» (мастер, администратор, закупщик).
Связи: Employees, Roles.

Permissions
Зачем: отдельные права (создать задачу, модерировать объявление, редактировать склад).
Связи: PermissionGroups; RolePermissionsMap; RolePermissions.

PermissionGroups
Зачем: группировка прав в меню настроек.
Связи: Permissions.

RolePermissionsMap
Зачем: какие права входят в роль (основная таблица RBAC в EF-модели).
Связи: Roles, Permissions.

RolePermissions
Зачем: то же назначение прав роли (дублирующая/расширенная схема из SQL-скрипта, если создана).
Связи: Roles, Permissions.

Notifications
Зачем: шаблон или тип уведомления (заголовок, текст, привязка к авто).
Связи: Cars (опционально); UserNotifications.

UserNotifications
Зачем: какое уведомление показано конкретному пользователю, прочитано ли.
Связи: Users, Notifications.

EmployeeNotifications
Зачем: уведомления для сотрудников Pro (новая задача, модерация, заявка на тип мастерской).
Связи: Employees (по логике приложения, через EmployeeId в таблице).


================================================================================
БЛОК B. СПРАВОЧНИКИ (РЕДКО МЕНЯЮТСЯ)
================================================================================

Countries
Зачем: страна (для адресов, производителей).
Связи: Addresses; PartManufacturers.

Brands
Зачем: марка автомобиля (Toyota, BMW).
Связи: Models.

Models
Зачем: модель авто в рамках марки.
Связи: Brands; Cars.

CarTypes
Зачем: тип кузова (седан, внедорожник).
Связи: Cars.

FuelTypes
Зачем: тип топлива.
Связи: Cars.

Colors
Зачем: название цвета в справочнике.
Связи: CarColors; Parts (если у запчасти есть цвет).

BusinessTypes
Зачем: вид деятельности бизнеса (СТО, шиномонтаж, покраска) для мастерских и карты.
Связи: Workshops (основной тип); WorkshopBusinessTypes (дополнительные типы).

Statuses
Зачем: универсальные статусы (задача, ремонт, объявление, запчасть).
Связи: Tasks; RepairHistory; CarSales; Parts.

RepairCategories
Зачем: категория работ при ремонте (ходовая, двигатель).
Связи: RepairHistory.

PartManufacturers
Зачем: производитель запчастей (Bosch и т.д.).
Связи: Countries; Parts.


================================================================================
БЛОК C. ОРГАНИЗАЦИЯ И МАСТЕРСКИЕ
================================================================================

Companies
Зачем: юридическое лицо или сеть, владеющая мастерскими.
Связи: Workshops; Roles (роли уровня компании).

Addresses
Зачем: адрес с координатами (мастерская, пункт выдачи на карте).
Связи: Countries; Workshops.

Workshops
Зачем: конкретный филиал/мастерская — центр операционной части Pro.
Связи: Companies; Addresses; BusinessTypes (основной); Employees; WorkshopServiceUnits; WorkshopServices; WorkshopParts; WorkshopServiceClients; WorkshopGuestCars; Tasks (через сотрудников и документы); WorkshopConversations; WorkshopOnlineBookings; WorkshopWorkSchedules; WorkshopBusinessTypes; ServiceDocuments; WarehouseManagers.

WorkshopServiceUnits
Зачем: пост/бокс/узел внутри мастерской (кузовной, слесарный).
Связи: Workshops; WorkshopServices.

WorkshopBusinessTypes
Зачем: у одной мастерской может быть несколько типов услуг (не только один BusinessTypes).
Связи: Workshops, BusinessTypes.

WorkshopBusinessTypeChangeRequests
Зачем: заявка руководителя изменить набор типов мастерской (на модерацию).
Связи: Workshops; Employees (кто подал); статус модерации.

WorkshopBusinessTypeChangeRequestTypes
Зачем: строки заявки — какие типы хотят добавить или убрать.
Связи: WorkshopBusinessTypeChangeRequests, BusinessTypes.

WarehouseManagers
Зачем: привязка сотрудника к роли «менеджер склада» мастерской (по модели EF).
Связи: Workshops, Employees (логика в приложении).


================================================================================
БЛОК D. АВТОМОБИЛИ И КЛИЕНТСКИЙ ГАРАЖ
================================================================================

Cars
Зачем: карточка автомобиля (модель, VIN, госномер, год).
Связи: Models, CarTypes, FuelTypes; CarColors; UserCars; CarSales; RepairHistory; Tasks; WorkshopGuestCars; ServiceDocuments.

CarColors
Зачем: текущий/исторический цвет кузова конкретного авто.
Связи: Cars, Colors.

UserCars
Зачем: «этот пользователь владеет/вёл этот авто в гараже».
Связи: Users, Cars.

UserCarMaintenanceHistory
Зачем: записи ТО и работ по авто пользователя (история для раздела «Обслуживание»).
Связи: UserCars или Cars, Users (по полям в таблице).

UserCarComponentStatuses
Зачем: текущая оценка узлов (масло — хорошо, тормоза — пора менять).
Связи: UserCars / Cars, код узла в строке.

RepairHistory
Зачем: визит/ремонт в мастерской (дата, категория, статус, авто, мастер).
Связи: Cars; RepairCategories; Employees; Statuses; Tasks (RepairHistoryId); ServiceDocuments; WorkshopGuestCars.


================================================================================
БЛОК E. ЗАДАЧИ, УСЛУГИ, СКЛАД (ЯДРО PRO)
================================================================================

Tasks
Зачем: заказ-наряд / задание мастерской (главная рабочая сущность).
Связи: Employees (исполнитель); Statuses; Cars; Users (клиент); RepairHistory; ParentTaskId / DelegateTaskId (цепочки); TaskServiceLines; TaskPartLines; TaskPurchaseRequests; ServiceDocuments (RootTaskId).

TaskServiceLines
Зачем: строка услуги внутри задачи (название, цена, количество).
Связи: Tasks; WorkshopServices (каталог, опционально).

TaskPartLines
Зачем: строка запчасти в задаче (со склада мастерской).
Связи: Tasks; WorkshopParts.

WorkshopServices
Зачем: каталог услуг мастерской с ценами.
Связи: Workshops; WorkshopServiceUnits; TaskServiceLines.

WorkshopParts
Зачем: склад запчастей мастерской (остаток, цена, артикул).
Связи: Workshops; TaskPartLines; TaskPurchaseRequestLines; ServiceDocumentPartLines.

TaskPurchaseRequests
Зачем: заявка «купить детали по задаче» (связь исходной и закупочной задачи).
Связи: Tasks (исходная и задача-закупка); Employees; TaskPurchaseRequestLines.

TaskPurchaseRequestLines
Зачем: позиции в заявке на закупку.
Связи: TaskPurchaseRequests; WorkshopParts.

ServiceDocuments
Зачем: сводный документ по цепочке задач (единый акт на приём/работы).
Связи: RootTaskId → Tasks; Workshops; Cars; Users; RepairHistory; ServiceDocumentServiceLines; ServiceDocumentPartLines.

ServiceDocumentServiceLines
Зачем: услуги в сводном документе.
Связи: ServiceDocuments; WorkshopServices.

ServiceDocumentPartLines
Зачем: запчасти в сводном документе.
Связи: ServiceDocuments; WorkshopParts.

Parts
Зачем: общий эталонный каталог запчастей (не склад мастерской, а «типовая деталь»).
Связи: PartManufacturers; Colors; Statuses.


================================================================================
БЛОК F. КЛИЕНТЫ МАСТЕРСКОЙ, ПРОДАЖА АВТО
================================================================================

WorkshopServiceClients
Зачем: клиент мастерской (ФИО, телефон, связь с Users если зарегистрирован в DriveCare).
Связи: Workshops; Users (опционально); WorkshopConversations; WorkshopOnlineBookings.

WorkshopGuestCars
Зачем: авто гостя без полноценной регистрации в DriveCare (разовый визит).
Связи: Workshops; Cars; RepairHistory; WorkshopServiceClients.

CarSales
Зачем: объявление о продаже автомобиля.
Связи: Cars; Statuses (модерация); Employees (кто создал/модерировал); CarSalePrices; UserCarSales.

CarSalePrices
Зачем: история или варианты цены объявления.
Связи: CarSales.

UserCarSales
Зачем: связь пользователя с объявлением (владелец, покупатель, избранное — по логике).
Связи: Users, CarSales.


================================================================================
БЛОК G. ЧАТ, ЗАПИСЬ, РАСПИСАНИЕ
================================================================================

WorkshopConversations
Зачем: один диалог «клиент — мастерская» (не путать с одним сообщением).
Связи: Workshops, Users; WorkshopServiceClients (опционально); WorkshopMessages; уникальная пара WorkshopId + UserId.

WorkshopMessages
Зачем: текст сообщения в диалоге (кто отправил: клиент или сотрудник).
Связи: WorkshopConversations; Users (SenderUserId); Employees (SenderEmployeeId); SenderKind 0/1.

WorkshopOnlineBookings
Зачем: заявка клиента на онлайн-запись (дата, статус, причина).
Связи: Workshops; Users; WorkshopServiceClients; может порождать Tasks и RepairHistory.

WorkshopOnlineBookingSettings
Зачем: лимит записей в день на мастерскую.
Связи: Workshops (WorkshopId).

WorkshopWorkSchedules
Зачем: расписание по дням недели (открыто/закрыто, часы работы).
Связи: Workshops.


================================================================================
БЛОК H. ПОКРАСКА
================================================================================

WorkshopPaintServices
Зачем: услуги покраски в конкретной мастерской (цена, описание).
Связи: Workshops.

WorkshopPaintColors
Зачем: доступные цвета/типы покраски у мастерской.
Связи: Workshops; WorkshopPaintServices.

UserWorkshopPaintInquiries
Зачем: запрос клиента «хочу покрасить» в мастерскую.
Связи: Users; Workshops; Cars (по полям).


================================================================================
БЛОК I. ИНТЕРНЕТ-МАГАЗИН
================================================================================

OrderPickupPoints
Зачем: пункт выдачи заказа на карте (адрес, район, координаты).
Связи: StoreOrders.

StoreOrders
Зачем: заказ из магазина DriveCare (номер, сумма, статус оплаты, QR).
Связи: Users; OrderPickupPoints; StoreOrderLines.

StoreOrderLines
Зачем: товарные строки заказа.
Связи: StoreOrders; ProductId (идентификатор товара в каталоге приложения).


================================================================================
БЛОК J. СЛУЖЕБНОЕ
================================================================================

sysdiagrams
Зачем: служебная таблица SQL Server для хранения диаграмм БД в SSMS. В приложении не используется.


================================================================================
СХЕМА «ОТ ЦЕНТРА К КРАЮ» (ДЛЯ СВЕРКИ С ДИАГРАММОЙ)
================================================================================

Центр бизнеса мастерской:

Workshops
  ├── Employees → Tasks → TaskServiceLines → WorkshopServices
  │                      → TaskPartLines → WorkshopParts
  │                      → TaskPurchaseRequests
  ├── WorkshopServiceClients → WorkshopConversations → WorkshopMessages
  ├── WorkshopOnlineBookings → (Tasks, RepairHistory)
  └── ServiceDocuments (свод по цепочке Tasks)

Центр клиента DriveCare:

Users
  ├── UserCars → Cars → RepairHistory → Tasks
  ├── StoreOrders → StoreOrderLines
  ├── WorkshopConversations → WorkshopMessages
  └── UserCarMaintenanceHistory / UserCarComponentStatuses

Справочники подключаются сбоку: Brands → Models → Cars; Statuses везде; BusinessTypes к Workshops.

TCP-сервер (Server.cs) в БД таблиц не имеет — только файлы фото и push чата поверх таблиц WorkshopMessages.


================================================================================
ЧАСТЫЕ ВОПРОСЫ ПРИ РАЗБОРЕ ДИАГРАММЫ
================================================================================

Чем Tasks отличается от RepairHistory?
RepairHistory — факт визита/ремонта в журнале. Tasks — рабочее задание сотруднику (услуги, детали, статус выполнения). Обычно связаны через RepairHistoryId.

Чем WorkshopParts отличается от Parts?
Parts — общий каталог. WorkshopParts — склад конкретной мастерской с остатками и ценами.

Чем Users отличается от WorkshopServiceClients?
Users — аккаунт в DriveCare. WorkshopServiceClients — карточка клиента в CRM мастерской; может ссылаться на того же Users.

Почему два RolePermissionsMap и RolePermissions?
В проекте могла использоваться одна из схем; на диаграмме оставьте ту, что реально есть в вашей БД (проверка: SELECT * FROM INFORMATION_SCHEMA.TABLES).

Сколько диалогов у пары клиент-мастерская?
Один: уникальный индекс WorkshopId + UserId в WorkshopConversations. Много сообщений — в WorkshopMessages.
