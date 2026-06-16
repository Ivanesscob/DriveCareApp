РУКОВОДСТВО ДЛЯ ОТЧЁТА. DRIVECARE

Скопируйте нужные блоки в Word. Специальное оформление не требуется.


ЧАСТЬ 1. ТАБЛИЦЫ БАЗЫ ДАННЫХ

Основные таблицы (модель Entity Framework):

- Addresses (адреса);
- Brands (марки авто);
- BusinessTypes (типы бизнеса);
- CarColors (цвета авто);
- Cars (автомобили);
- CarSalePrices (цены продажи);
- CarSales (объявления продажи);
- CarTypes (типы кузова);
- Colors (справочник цветов);
- Companies (компании);
- Countries (страны);
- EmployeeRolesMap (роли сотрудника);
- Employees (сотрудники);
- FuelTypes (типы топлива);
- Models (модели авто);
- Notifications (шаблоны уведомлений);
- PartManufacturers (производители запчастей);
- Parts (каталог запчастей);
- PermissionGroups (группы прав);
- Permissions (права доступа);
- RepairCategories (категории ремонта);
- RepairHistory (история ремонтов);
- RolePermissionsMap (связь роль и право);
- Roles (роли системы);
- Statuses (статусы);
- sysdiagrams (служебная таблица SQL);
- TaskPartLines (запчасти в задаче);
- TaskPurchaseRequestLines (строки закупки);
- TaskPurchaseRequests (заявки на закупку);
- Tasks (задачи мастерской);
- TaskServiceLines (услуги в задаче);
- UserCars (авто пользователя);
- UserCarSales (продажи пользователя);
- UserNotifications (уведомления пользователя);
- UserRoles (роли пользователя);
- Users (пользователи);
- WarehouseManagers (менеджеры склада);
- WorkshopGuestCars (гостевые авто);
- WorkshopParts (склад запчастей);
- Workshops (мастерские);
- WorkshopServiceClients (клиенты сервиса);
- WorkshopServices (услуги мастерской);
- WorkshopServiceUnits (узлы сервиса);

Дополнительные таблицы (скрипты SQL):

- RolePermissions (права ролей);
- EmployeeNotifications (уведомления сотрудников);
- ServiceDocuments (акты и документы);
- ServiceDocumentServiceLines (услуги в документе);
- ServiceDocumentPartLines (запчасти в документе);
- WorkshopConversations (диалоги чата);
- WorkshopMessages (сообщения чата);
- UserCarMaintenanceHistory (история ТО);
- UserCarComponentStatuses (статусы узлов);
- WorkshopOnlineBookings (онлайн-запись);
- WorkshopOnlineBookingSettings (лимит записи);
- WorkshopWorkSchedules (расписание мастерской);
- WorkshopBusinessTypes (типы мастерской);
- WorkshopBusinessTypeChangeRequests (заявка смены типа);
- WorkshopBusinessTypeChangeRequestTypes (типы в заявке);
- WorkshopPaintServices (услуги покраски);
- WorkshopPaintColors (цвета покраски);
- UserWorkshopPaintInquiries (запросы покраски);
- OrderPickupPoints (пункты выдачи);
- StoreOrders (заказы магазина);
- StoreOrderLines (строки заказа);


ЧАСТЬ 2. ВОЗМОЖНОСТИ СИСТЕМЫ И АКТЁРЫ

Система DriveCare состоит из двух приложений с одной базой данных Microsoft SQL Server.

Приложение DriveCare предназначено для клиентов, владельцев автомобилей.

Приложение DriveCare Pro предназначено для сотрудников и руководителей автосервисов (мастерских). Предприятие представлено компанией и одной или несколькими мастерскими.

Возможности, которые должна предоставлять разрабатываемая для предприятия система:

актёр Пользователь (клиент) использует приложение DriveCare для ведения личного гаража, поиска мастерских, онлайн-записи на обслуживание, переписки с сервисом, заказа товаров в магазине, обращений по покраске и работы с объявлениями о продаже автомобилей;

актёр Сотрудник мастерской использует приложение DriveCare Pro для выполнения задач (заказ-нарядов): учёт услуг и запчастей, оформление закупок, работа с автомобилями клиентов;

актёр Руководитель мастерской использует DriveCare Pro для управления мастерской: персонал, каталог услуг, склад запчастей, расписание, подтверждение онлайн-записей, аналитика;

актёр Администратор системы использует DriveCare Pro для модерации объявлений о продаже авто, согласования типов мастерских и административных функций платформы.

На основании вышеизложенного, а также концептуальной модели предметной области, была создана логическая модель базы данных и выполнено физическое проектирование в СУБД Microsoft SQL Server.


ЧАСТЬ 3. ДАТАЛОГИЧЕСКАЯ МОДЕЛЬ (ТЕКСТ К РИСУНКАМ ER)

На основании концептуальной модели, в виде диаграмм ER-типа, описания сущностей и их атрибутов была создана логическая модель базы данных в терминах сущностей и связей. Логическая модель позволяет перейти к физическому проектированию базы данных с помощью СУБД.

Даталогическая модель (рисунки 2–8) — описание структуры базы на языке Microsoft SQL Server. Модель разделена на семь подмоделей для наглядности. Реального разделения базы на отдельные схемы нет, показ по частям обусловлен большим количеством таблиц.

Рисунок 2. Подмодель пользователей и прав доступа.

Центральные таблицы Users (клиенты DriveCare) и Employees (сотрудники Pro). Также UserRoles, Roles, Permissions, RolePermissionsMap, уведомления UserNotifications и EmployeeNotifications. В других проектах аналогом могут быть отдельные таблицы типа PlayerUser и TMUser; здесь клиенты и персонал разделены через Users и Employees.

Рисунок 3. Подмодель справочников.

Справочная информация: Countries, Brands, Models, CarTypes, FuelTypes, Colors, BusinessTypes, Statuses, RepairCategories, Parts.

Рисунок 4. Подмодель автомобилей и обслуживания.

Cars, UserCars, Addresses, RepairHistory, UserCarMaintenanceHistory, UserCarComponentStatuses.

Рисунок 5. Подмодель компаний и мастерских.

Companies, Workshops, WorkshopServiceUnits, WorkshopBusinessTypes, заявки на смену типов, WarehouseManagers. Здесь задаётся структура предприятия и филиалов.

Рисунок 6. Подмодель задач, услуг и запчастей.

Самая важная операционная часть для мастерской. Tasks — заказ-наряд. WorkshopServices и TaskServiceLines — услуги. WorkshopParts и TaskPartLines — запчасти. TaskPurchaseRequests — закупки. ServiceDocuments — итоговые документы. Мастерская через Workshops связана с каталогами, задача Tasks объединяет услуги и детали.

Рисунок 7. Подмодель клиентов, записи и коммуникаций.

WorkshopServiceClients, WorkshopGuestCars, CarSales, чат WorkshopConversations и WorkshopMessages, онлайн-запись WorkshopOnlineBookings, покраска, расписание WorkshopWorkSchedules.

Рисунок 8. Подмодель интернет-магазина.

OrderPickupPoints, StoreOrders, StoreOrderLines.


ЧАСТЬ 4. РАЗДЕЛ 6 ОТЧЁТА. ЛИСТИНГИ ИСХОДНОГО КОДА

В отчёт вставляют не весь проект, а фрагменты кода с подписью «Листинг N — название модуля». Обычно 15–40 строк на листинг.

Модуль TCP-сервера (файл Server.cs):

Листинг 1. Метод Main — запуск сервера, порт 5000, перечень команд UPLOAD, GET, CHAT_SUBSCRIBE, CHAT_PUSH, CHAT_PING.

Листинг 2. Метод HandleClient — разбор входящих команд в цикле.

Листинг 3. Обработка UPLOAD и GET — сохранение и выдача файлов фотографий.

Листинг 4. Класс ChatHub — регистрация подписчиков чата и рассылка уведомлений CHAT_PUSH.

Модуль обмена сообщениями (DriveCareCore):

Листинг 5. WorkshopMessagingService — отправка и загрузка сообщений из базы данных.

Листинг 6. WorkshopChatRealtimeClient — подключение к TCP-серверу, подписка и отправка push.

Модуль клиентского приложения DriveCare:

Листинг 7. MessagesPage — экран переписки пользователя с мастерской.

Листинг 8. StoreOrderService или страница оформления заказа — создание заказа в магазине.

Листинг 9. VehicleComponentStatusService или ServiceCarPage — учёт обслуживания автомобиля.

Модуль приложения DriveCare Pro:

Листинг 10. EmployeeTaskCardPage — карточка задачи, услуги и запчасти.

Листинг 11. ProHomePage — главный экран сотрудника.

Листинг 12. WorkshopMessagesPage — чат со стороны мастерской.

Листинг 13. AdminCarSaleModerationPage — модерация объявлений (для администратора).

К каждому листингу в тексте отчёта добавьте одно-два предложения: что делает этот фрагмент и как связан с остальной системой.


ЧАСТЬ 5. РАЗДЕЛ 7 ОТЧЁТА. СКРИНШОТЫ ПРОГРАММЫ

Подписи к рисункам в Word: «Рисунок N — краткое название экрана или модуля».


5.1. TCP-сервер (Server.cs)

Что заскринить:

Скрин 1. Окно консоли при запуске сервера. Должны быть видны строки о порте 5000, папке для фото и списке команд.

Скрин 2. Консоль во время работы: подключение клиента, строка CHAT_SUBSCRIBE с подписью пользователя или мастерской, строка CHAT_PUSH разослано N подписчикам после отправки сообщения в приложении.

Скрин 3. По желанию: папка на диске с загруженными фотографиями после публикации объявления.

Скрин 4. Фрагмент кода Server.cs в Visual Studio (для пары с листингом).

Что написать в отчёте про сервер:

TCP-сервер — сетевой модуль системы DriveCare. Слушает порт 5000. Принимает команды от приложений DriveCare и DriveCare Pro.

Команда UPLOAD загружает файл изображения на сервер. Команда GET отдаёт файл клиенту. Используется для фотографий в объявлениях о продаже автомобилей.

Команда CHAT_SUBSCRIBE регистрирует клиентское приложение для получения уведомлений о новых сообщениях в чате. Сами тексты сообщений хранятся в базе данных SQL Server, сервер только сообщает подписчикам, что нужно обновить экран переписки.

Команда CHAT_PUSH вызывается приложением после записи сообщения в базу. Сервер рассылает сигнал активным подписчикам.

Команда CHAT_PING проверяет, что соединение с сервером не разорвано.


5.2. Приложение DriveCare (клиент)

Что заскринить:

Скрин 5. Экран входа или регистрации.

Скрин 6. Главная страница пользователя с кнопками: гараж, сервис, покраска, магазин и другие разделы.

Скрин 7. Мой гараж или карточка автомобиля пользователя.

Скрин 8. Раздел обслуживание автомобиля: статусы узлов, история.

Скрин 9. Карта мастерских или выбор сервиса.

Скрин 10. Онлайн-запись на обслуживание.

Скрин 11. Экран сообщений, переписка с мастерской (желательно с открытым диалогом и отправленным сообщением).

Скрин 12. Интернет-магазин: каталог, оформление заказа, выбор пункта выдачи, QR или статус оплаты.

Скрин 13. Мои заказы.

Скрин 14. Запрос на покраску или объявление о продаже авто (по наличию в вашей версии).

Что написать в отчёте про DriveCare:

Клиентское приложение DriveCare предназначено для владельца автомобиля. Позволяет вести список своих машин, смотреть состояние и историю обслуживания, находить мастерские, записываться онлайн, общаться с сервисом в чате, заказывать товары с доставкой в пункт выдачи, отправлять запросы на покраску и работать с объявлениями о продаже. Для чата и фотографий приложение обращается к TCP-серверу, основные данные хранятся в SQL Server.


5.3. Приложение DriveCare Pro (мастерская)

Что заскринить:

Скрин 15. Вход сотрудника в DriveCare Pro.

Скрин 16. Главная страница Pro: список задач, уведомления.

Скрин 17. Карточка задачи (заказ-наряд): услуги, запчасти, статус, закупка.

Скрин 18. Каталог услуг мастерской.

Скрин 19. Склад запчастей мастерской.

Скрин 20. Список клиентов мастерской.

Скрин 21. Сообщения, переписка с клиентом со стороны мастерской.

Скрин 22. Онлайн-записи: список, подтверждение или отклонение.

Скрин 23. Расписание работы мастерской.

Скрин 24. Модерация объявлений о продаже авто (экран администратора).

Скрин 25. Модерация смены типов мастерской или хаб модерации с красными бейджами (по наличию).

Что написать в отчёте про DriveCare Pro:

Приложение DriveCare Pro предназначено для автосервиса. Сотрудник ведёт задачи на ремонт: добавляет услуги и запчасти, оформляет закупки, закрывает работы. Руководитель управляет мастерской, персоналом, каталогами и расписанием. Администратор платформы модерирует объявления и заявки на изменение типов мастерских. Чат с клиентом работает через ту же базу данных и TCP-сервер уведомлений, что и клиентское приложение.


ЧАСТЬ 6. КАК СВЯЗАТЬ ЛИСТИНГИ И СКРИНШОТЫ В ОТЧЁТЕ

Рекомендуемый порядок в пояснительной записке:

Сначала описание актёров и возможностей системы.

Затем даталогическая модель по рисункам ER.

Затем листинги кода по модулям.

Затем скриншоты: сервер, DriveCare, DriveCare Pro.

Пример связки в тексте: «На листинге 4 показана рассылка уведомлений чата. На рисунке 2 видна работа того же модуля в консоли сервера при отправке сообщения из приложения (рисунок 11)».

Нумерацию рисунков и листингов подстройте под требования вашего вуза. Если рисунки ER уже заняли номера 2–8, скриншоты программы начинайте с следующего номера.


ЧАСТЬ 7. ГОТОВЫЕ ЛИСТИНГИ ДЛЯ КОПИРОВАНИЯ В WORD

Ниже код для вставки в отчёт. Шрифт Courier New 9–10 пт. Перед каждым блоком в отчёте напишите подпись «Листинг N — …».


Листинг 1 — Запуск TCP-сервера (Server.cs, метод Main)

    static async Task Main()
    {
        if (!Directory.Exists(saveFolder))
            Directory.CreateDirectory(saveFolder);

        TcpListener listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        Console.WriteLine($"DriveCare TCP: порт {port}, фото + чат.");
        Console.WriteLine($"Папка фото: {saveFolder}");
        Console.WriteLine("Команды: UPLOAD, GET, CHAT_SUBSCRIBE, CHAT_PUSH, CHAT_PING");

        while (true)
        {
            TcpClient client = await listener.AcceptTcpClientAsync();
            _ = HandleClient(client);
        }
    }


Листинг 2 — Обработка команд клиента (Server.cs, фрагмент HandleClient)

                string command = Encoding.UTF8.GetString(cmdBytes).ToUpperInvariant();

                if (command == "UPLOAD")
                {
                    await HandleUploadAsync(stream).ConfigureAwait(false);
                }
                else if (command == "GET")
                {
                    await HandleGetAsync(stream).ConfigureAwait(false);
                }
                else if (command == "CHAT_SUBSCRIBE")
                {
                    string payload = await ReadUtf8PayloadAsync(stream).ConfigureAwait(false);
                    chatSub = ChatHub.TryRegister(stream, payload);
                    await WriteInt32Async(stream, 1).ConfigureAwait(false);
                    Console.WriteLine("CHAT_SUBSCRIBE: " + chatSub.Label);
                }
                else if (command == "CHAT_PUSH")
                {
                    string payload = await ReadUtf8PayloadAsync(stream).ConfigureAwait(false);
                    int sent = await ChatHub.BroadcastNewMessageAsync(payload, stream).ConfigureAwait(false);
                    await WriteInt32Async(stream, 1).ConfigureAwait(false);
                    Console.WriteLine($"CHAT_PUSH: разослано {sent} подписчикам");
                }


Листинг 3 — Загрузка файла на сервер (Server.cs, метод HandleUploadAsync)

    static async Task HandleUploadAsync(NetworkStream stream)
    {
        // чтение имени и размера файла
        string originalFileName = Encoding.UTF8.GetString(nameBytes);
        long fileSize = BitConverter.ToInt64(fileSizeBytes, 0);

        string extension = Path.GetExtension(originalFileName);
        string generatedFileName = Guid.NewGuid().ToString() + extension;
        string fullPath = Path.Combine(saveFolder, generatedFileName);

        using (FileStream fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
        {
            // запись байтов файла в каталог saveFolder
        }

        // ответ клиенту: сгенерированное имя файла
        byte[] generatedNameBytes = Encoding.UTF8.GetBytes(generatedFileName);
        await stream.WriteAsync(BitConverter.GetBytes(generatedNameBytes.Length), 0, 4);
        await stream.WriteAsync(generatedNameBytes, 0, generatedNameBytes.Length);
    }


Листинг 4 — Подписка и рассылка чата (Server.cs, класс ChatHub)

    public static ChatSubscriber TryRegister(NetworkStream stream, string payload)
    {
        var sub = new ChatSubscriber { Stream = stream };
        if (payload.StartsWith("U:", StringComparison.OrdinalIgnoreCase))
        {
            sub.ForUser = true;
            sub.UserId = Guid.Parse(payload.Substring(2).Trim());
            sub.Label = "user " + sub.UserId;
        }
        else if (payload.StartsWith("W:", StringComparison.OrdinalIgnoreCase))
        {
            // список идентификаторов мастерских через ; или ,
            sub.ForUser = false;
            sub.Label = "workshops x" + sub.WorkshopIds.Count;
        }
        lock (SubscribersLock)
            Subscribers.Add(sub);
        return sub;
    }

    public static async Task<int> BroadcastNewMessageAsync(string payload, NetworkStream excludeStream)
    {
        foreach (var sub in copy)
        {
            bool match = sub.ForUser && sub.UserId == userId
                           || !sub.ForUser && sub.WorkshopIds.Contains(workshopId);
            if (match)
                await SendNewMessageEventAsync(sub, payload);
        }
        return sent;
    }


Листинг 5 — Загрузка сообщений из БД (WorkshopMessagingService.cs)

            const string sql = @"
SELECT m.RowId AS MessageId, m.SenderKind, ...
       m.Body AS Body, m.CreatedAt AS CreatedAt
FROM dbo.WorkshopMessages m
WHERE m.ConversationId = @cid
ORDER BY m.CreatedAt ASC;";

            var rows = await db.Database.SqlQuery<ChatMessageRow>(sql,
                    new SqlParameter("@cid", conversationId)).ToListAsync();

            return rows.Select(r => new ChatMessageItem
            {
                MessageId = r.MessageId,
                Body = r.Body ?? string.Empty,
                CreatedAt = r.CreatedAt,
                IsMine = forUserSide
                    ? (MessageSenderKind)r.SenderKind == MessageSenderKind.User
                    : (MessageSenderKind)r.SenderKind == MessageSenderKind.Employee
            }).ToList();


Листинг 6 — Сохранение сообщения в БД (WorkshopMessagingService.cs, InsertMessageAsync)

            var now = DateTime.Now;
            var sentAt = new SqlParameter("@p_dt", now);
            await db.Database.ExecuteSqlCommandAsync(
                @"INSERT INTO dbo.WorkshopMessages
                  (RowId, ConversationId, SenderKind, SenderUserId, SenderEmployeeId, Body, CreatedAt)
                  VALUES (@p_id, @p_cid, @p_kind, @p_uid, @p_eid, @p_body, @p_dt)",
                new SqlParameter("@p_id", Guid.NewGuid()),
                new SqlParameter("@p_cid", conversationId),
                new SqlParameter("@p_kind", (byte)kind),
                new SqlParameter("@p_body", body),
                sentAt);

            await db.Database.ExecuteSqlCommandAsync(
                @"UPDATE dbo.WorkshopConversations
                  SET LastMessageAt = @p_dt, LastMessagePreview = @p_pr, UnreadForWorkshop = UnreadForWorkshop + 1
                  WHERE RowId = @p_cid", sentAt, ...);


Листинг 7 — Клиент чата: подключение к серверу (WorkshopChatRealtimeClient.cs)

        public static void NotifyNewMessage(Guid conversationId, Guid workshopId, Guid userId, MessageSenderKind senderKind)
        {
            var payload = BuildPayload(conversationId, workshopId, userId, senderKind);
            Task.Run(() => SendPushAsync(payload));
        }

        static async Task RunSubscribeSessionAsync(string subscribePayload, CancellationToken token)
        {
            using (var client = new TcpClient())
            {
                await client.ConnectAsync(DefaultServerIp, DefaultPort);
                using (var stream = client.GetStream())
                {
                    await SendCommandWithPayloadAsync(stream, "CHAT_SUBSCRIBE", subscribePayload);
                    var ok = await ReadInt32Async(stream);
                    while (!token.IsCancellationRequested)
                    {
                        await SendCommandAsync(stream, "CHAT_PING");
                        // ожидание события NEWMSG от сервера
                    }
                }
            }
        }


Листинг 8 — Отправка сообщения пользователем (MessagesPage.xaml.cs)

        private async Task SendMessageAsync()
        {
            var (ok, error, _) = await WorkshopMessagingService.SendFromUserAsync(
                AppState.CurrentUserId,
                _selectedConversationId,
                MessageInput.Text);

            if (!ok)
            {
                MessageBox.Show(error ?? "Не удалось отправить.", "Сообщения", ...);
                return;
            }

            MessageInput.Clear();
            await LoadMessagesOnlyAsync();

            if (ConversationsList.SelectedItem is ConversationListItem conv)
            {
                WorkshopChatRealtimeClient.NotifyNewMessage(
                    _selectedConversationId, conv.WorkshopId, conv.UserId, MessageSenderKind.User);
            }
        }


Листинг 9 — Создание заказа в магазине (StoreOrderService.cs)

            var orderId = Guid.NewGuid();
            var orderNumber = BuildOrderNumber();
            var qrPayload = $"DRIVECARE|{orderNumber}|{totalAmount:0}|{pickupPointId:N}";

            db.Database.ExecuteSqlCommand(@"
INSERT INTO dbo.StoreOrders (RowId, UserId, PickupPointId, OrderNumber, Status, TotalAmount, QrPayload, CreatedAt)
VALUES (@p_id, @p_uid, @p_pp, @p_num, @p_status, @p_tot, @p_qr, GETDATE());",
                new SqlParameter("@p_id", SqlDbType.UniqueIdentifier) { Value = orderId },
                new SqlParameter("@p_uid", SqlDbType.UniqueIdentifier) { Value = userId },
                new SqlParameter("@p_pp", SqlDbType.UniqueIdentifier) { Value = pickupPointId },
                new SqlParameter("@p_num", SqlDbType.NVarChar, 32) { Value = orderNumber },
                new SqlParameter("@p_tot", SqlDbType.Decimal) { Value = totalAmount, ... });


Листинг 10 — Учёт состояния узлов автомобиля (VehicleComponentStatusService.cs)

        static readonly ComponentDef[] Components =
        {
            new ComponentDef { Code = "service", Name = "Плановое ТО", Sort = 1, IntervalKm = 15_000, ... },
            new ComponentDef { Code = "oil", Name = "Моторное масло", Sort = 2, IntervalKm = 10_000, ... },
            new ComponentDef { Code = "brakes", Name = "Тормоза", Sort = 4, IntervalKm = 80_000, ... },
            new ComponentDef { Code = "tires", Name = "Шины", Sort = 6, IntervalKm = 40_000, ... },
        };

        public static bool StatusTableExists()
        {
            const string sql = @"SELECT CASE WHEN OBJECT_ID(N'dbo.UserCarComponentStatuses', N'U')
                IS NOT NULL THEN 1 ELSE 0 END;";
            return AppConnect.model1.Database.SqlQuery<int>(sql).FirstOrDefault() == 1;
        }


Листинг 11 — Сохранение задачи мастерской (EmployeeTaskCardPage.xaml.cs)

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (_isPageBusy || !HasReportChanges())
                return;

            SetPageBusy(true, "Сохранение…");
            try
            {
                var (ok, error) = await SaveTaskReportAsync();
                if (!ok)
                {
                    MessageBox.Show(error ?? "Не удалось сохранить задание.", "Сохранение", ...);
                    return;
                }
                MessageBox.Show("Задание сохранено.\n\nУслуги, детали и комментарий записаны.",
                    "Сохранение", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            finally
            {
                SetPageBusy(false, null);
            }
        }


Листинг 12 — Главная страница DriveCare Pro (ProHomePage.xaml.cs)

            var emp = AppState.CurrentEmployee;
            var forest = await ServiceDocumentService.BuildForestForEmployeeAsync(emp.RowId);
            if (forest.Count == 0)
            {
                var rows = await ProHomeDataService.LoadTasksAsync(emp.RowId);
                TasksTree.ItemsSource = rows.Select(r => new TaskTreeNodeVm
                {
                    TaskId = r.TaskId,
                    Title = r.Title,
                    StatusDisplay = r.StatusDisplay,
                    IsCompleted = r.CompletedDisplay == "Да"
                }).ToList();
            }
            else
            {
                TasksTree.ItemsSource = forest;
            }


Листинг 13 — Ответ мастерской в чате (WorkshopMessagesPage.xaml.cs)

        private async Task SendMessageAsync()
        {
            var emp = AppState.CurrentEmployee;
            var (ok, error) = await WorkshopMessagingService.SendFromEmployeeAsync(
                emp.RowId, _selectedConversationId, MessageInput.Text);

            if (!ok)
            {
                MessageBox.Show(error ?? "Не удалось отправить.", "Сообщения", ...);
                return;
            }

            MessageInput.Clear();
            await LoadMessagesOnlyAsync();

            WorkshopChatRealtimeClient.NotifyNewMessage(
                _selectedConversationId, conv.WorkshopId, conv.UserId, MessageSenderKind.Employee);
        }


Листинг 14 — Модерация объявлений (AdminCarSaleModerationPage.xaml.cs)

        private async Task RefreshQueueAsync()
        {
            var list = await DatabaseExecutor.WithDbAsync(async db =>
            {
                var raw = await db.CarSales
                    .OrderByDescending(c => c.CreatedAt)
                    .Take(3000)
                    .ToListAsync();

                return raw
                    .Where(c => CarSaleModerationStatuses.IsInModerationQueue(db, c.StatusId))
                    .Take(200)
                    .Select(c => new ModerationQueueRow
                    {
                        RowId = c.RowId,
                        Title = c.Title ?? "—",
                        ModerationStatus = CarSaleModerationStatuses.FormatModerationStatusDisplay(...)
                    })
                    .ToList();
            });

            QueueGrid.ItemsSource = list;
        }


ЧАСТЬ 8. ЛИСТИНГИ 3.1 — 3.5 ДЛЯ ОТЧЁТА (КАК В ШАБЛОНЕ ВУЗА)

Вставьте в отчёт вводный абзац:

В процессе разработки информационной системы были реализованы основные программные модули клиентской и серверной частей приложения. Для реализации функциональности системы использовались современные технологии разработки (C#, WPF, Entity Framework, Microsoft SQL Server, TCP-сокеты), обеспечивающие стабильную работу приложения, взаимодействие с базой данных и обработку пользовательских запросов.

Ниже представлены листинги основных фрагментов программного кода, реализующих ключевые функции системы.


Листинг 3.1 — Реализация регистрации пользователя и отправки кода подтверждения на электронную почту.

Файлы: RegisterPage.xaml.cs, RegistrationMailHelper.cs

            var code = new Random().Next(10000, 100000).ToString("D5", ...);
            _pendingEmailCode = code;

            Task.Factory.StartNew(() => RegistrationMailHelper.TrySendVerificationCodeAsyncSafe(emailTrim, code))
                .ContinueWith(t =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (r.Outcome == RegistrationMailHelper.SendOutcome.Sent)
                        {
                            AppMessageBox.Show("Код отправлен на почту.", "DriveCare", ...);
                            ShowFormStep = false;
                            ShowVerificationStep = true;
                        }
                    }));
                });

            // RegistrationMailHelper.cs — отправка письма SMTP
            msg.Subject = "DriveCare — код подтверждения";
            msg.Body = string.Format(
                "Ваш код подтверждения: {0}\r\n\r\nЕсли вы не регистрировались в DriveCare, проигнорируйте это письмо.",
                code);
            using (var client = new SmtpClient(host.Trim(), port))
            {
                client.EnableSsl = ssl;
                client.Send(msg);
            }


Листинг 3.2 — Реализация механизма авторизации пользователя в системе.

Файл: LoginPage.xaml.cs (клиент DriveCare)

        private void LoginExecute()
        {
            var password = PasswordInput?.Password ?? string.Empty;
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(password))
            {
                AppMessageBox.Show("Введите логин и пароль.", "DriveCare", ...);
                return;
            }

            var user = AppConnect.model1.Users.FirstOrDefault(u =>
                u.Login == Username && u.Password == password);

            if (user != null)
            {
                AppState.SignInUser(user);
                AppState.SetFrame<UserHomePage>();
            }
            else
            {
                AppMessageBox.Show("Неверный логин или пароль.", "DriveCare", ...);
            }
        }

Файл: LoginPage.xaml.cs (DriveCare Pro — авторизация сотрудника)

        var user = await DatabaseExecutor.WithDbAsync(db =>
            db.Employees.FirstOrDefaultAsync(u =>
                u.Login == login && u.Password == password));

        if (user != null)
        {
            await AppState.SignInEmployeeAsync(user);
            AppState.SetFrame<ProHomePage>();
        }


Листинг 3.3 — Реализация серверной обработки запросов и взаимодействия с базой данных.

Файл: Server.cs — приём TCP-команд

                if (command == "UPLOAD")
                    await HandleUploadAsync(stream);
                else if (command == "GET")
                    await HandleGetAsync(stream);
                else if (command == "CHAT_PUSH")
                {
                    string payload = await ReadUtf8PayloadAsync(stream);
                    int sent = await ChatHub.BroadcastNewMessageAsync(payload, stream);
                    await WriteInt32Async(stream, 1);
                }

Файл: WorkshopMessagingService.cs — запись в SQL Server

            await db.Database.ExecuteSqlCommandAsync(
                @"INSERT INTO dbo.WorkshopMessages
                  (RowId, ConversationId, SenderKind, SenderUserId, SenderEmployeeId, Body, CreatedAt)
                  VALUES (@p_id, @p_cid, @p_kind, @p_uid, @p_eid, @p_body, @p_dt)",
                new SqlParameter("@p_id", Guid.NewGuid()),
                new SqlParameter("@p_cid", conversationId),
                new SqlParameter("@p_kind", (byte)kind),
                new SqlParameter("@p_body", body),
                new SqlParameter("@p_dt", DateTime.Now));


Листинг 3.4 — Реализация системы обмена сообщениями между пользователями и мастерскими.

            // Сохранение в БД (клиент)
            await WorkshopMessagingService.SendFromUserAsync(
                AppState.CurrentUserId, _selectedConversationId, MessageInput.Text);

            // Уведомление подписчиков через TCP-сервер
            WorkshopChatRealtimeClient.NotifyNewMessage(
                _selectedConversationId, conv.WorkshopId, conv.UserId, MessageSenderKind.User);

            // Server.cs — рассылка подписчикам
            foreach (var sub in copy)
            {
                bool match = sub.ForUser && sub.UserId == userId
                    || !sub.ForUser && sub.WorkshopIds.Contains(workshopId);
                if (match)
                    await SendNewMessageEventAsync(sub, payload);
            }


Листинг 3.5 — Реализация механизма создания задач и управления статусами выполнения.

Файл: ServiceBookingTaskService.cs — создание задачи

            var task = new TaskEntity
            {
                RowId = Guid.NewGuid(),
                Title = title,
                Description = description,
                EmployeeId = employee.RowId,
                StatusId = statusId,
                CreatedAt = DateTime.Now,
                IsCompleted = false,
                CarId = carId,
                ClientUserId = ctx.FoundUser?.RowId
            };
            db.Tasks.Add(task);
            await db.SaveChangesAsync();

Файл: TaskDelegationService.cs — завершение задачи

        private static bool TryMarkAutoCompleted(TaskEntity task, string reportNote, DateTime now)
        {
            if (task.IsCompleted)
                return false;
            task.IsCompleted = true;
            task.EndDate = now;
            task.ReportText = AppendAutoReportNote(task.ReportText, reportNote);
            return true;
        }
