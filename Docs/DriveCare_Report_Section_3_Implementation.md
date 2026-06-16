# 3 Реализация программного обеспечения

В данном разделе описана практическая реализация информационной системы DriveCare: ключевые классы, методы и сетевой сервер. Внешний вид экранов приведён в разделе 2.3; здесь иллюстрируются фрагменты программного кода из Visual Studio и консоли TCP-сервера.

## 3.1 Реализация основных функций программного обеспечения

Программный комплекс DriveCare реализован в виде решения Visual Studio, включающего проекты DriveCare (клиент для владельцев автомобилей), DriveCarePro (рабочее место автосервиса), DriveCareCore (общая бизнес-логика и доступ к данным) и PhotoServer (TCP-сервер). Взаимодействие с базой данных Microsoft SQL Server выполняется через Entity Framework 6 и контекст Model1; сетевой обмен файлами и push-уведомлениями чата — через TCP-сервер на порту 5000.

### 3.1.1 Авторизация и регистрация пользователей

Модуль авторизации обеспечивает вход в систему и разграничение доступа. Окно авторизации клиентского приложения показано на рисунке 54. Пользователь вводит логин и пароль; проверка учётной записи выполняется по таблице Users, после успешного входа идентификатор сохраняется в классе AppState. Фрагмент обработчика входа LoginExecute приведён на рисунке 56.

Регистрация нового клиента реализована на странице RegisterPage. Форма регистрации представлена на рисунке 55. Пользователь заполняет поля, получает пятизначный код подтверждения на электронную почту через SMTP (класс RegistrationMailHelper), вводит код и после успешной проверки создаётся запись в таблице Users.

В приложении DriveCare Pro аналогичная процедура реализована для сотрудников (таблица Employees). Окно авторизации сотрудника показано на рисунке 57. После входа дополнительно загружаются назначенные роли и права доступа; фрагмент метода SignInEmployeeAsync приведён на рисунке 58.

**[ВСТАВИТЬ СКРИН: запустить проект DriveCare → окно входа LoginPage.xaml — поля «Логин», «Пароль», кнопки входа и перехода к регистрации]**

*Рисунок 54 – Окно авторизации клиентского приложения DriveCare*

**[ВСТАВИТЬ СКРИН: DriveCare → RegisterPage.xaml — форма регистрации (логин, email, телефон, пароль) или шаг ввода кода из письма]**

*Рисунок 55 – Окно регистрации пользователя*

**[ВСТАВИТЬ СКРИН: Visual Studio → LoginPage.xaml.cs, метод LoginExecute (строки с проверкой Users и AppState.SignInUser) — подсветить фрагмент кода]**

*Рисунок 56 – Фрагмент кода авторизации пользователя (LoginPage.xaml.cs)*

**[ВСТАВИТЬ СКРИН: запустить DriveCarePro → LoginPage — окно входа сотрудника]**

*Рисунок 57 – Окно авторизации приложения DriveCare Pro*

**[ВСТАВИТЬ СКРИН: Visual Studio → DriveCarePro\AppState.cs, метод SignInEmployeeAsync — загрузка ролей и EmployeePermissionService]**

*Рисунок 58 – Фрагмент кода авторизации сотрудника (AppState.cs)*

### 3.1.2 Модули клиентского приложения DriveCare

Клиентское приложение DriveCare разделено на страницы WPF и сервисные классы в папке Services. Интерфейс модулей гаража и онлайн-записи описан в разделе 2.3 (рисунки 17–21); ниже показана программная реализация.

Добавление автомобиля в личный гараж выполняется методом Save класса UserGarageService: создаётся или обновляется запись в таблице Cars и связь UserCars с текущим пользователем. Фрагмент метода Save приведён на рисунке 59.

Загрузка списка автомобилей пользователя реализована в методе LoadForUser того же класса с использованием SQL-запроса к связанным таблицам UserCars, Cars, Models и Brands; фрагмент запроса показан на рисунке 60.

Создание онлайн-записи вынесено в общий сервис DriveCareCore — WorkshopOnlineBookingService.CreateBookingAsync: проверяются входные данные, формируется запись WorkshopOnlineBooking и вызывается SaveChanges. Реализация метода представлена на рисунке 61. Вызов сервиса из окна записи выполняется в WorkshopOnlineBookingWindow.xaml.cs, фрагмент показан на рисунке 62.

**[ВСТАВИТЬ СКРИН: Visual Studio → DriveCare\Services\UserGarageService.cs, метод Save (создание Car и UserCar, db.SaveChanges)]**

*Рисунок 59 – Метод сохранения автомобиля в гараже (UserGarageService.cs)*

**[ВСТАВИТЬ СКРИН: Visual Studio → UserGarageService.cs, метод LoadForUser — SQL SELECT с JOIN UserCars, Cars, Models, Brands]**

*Рисунок 60 – Метод загрузки автомобилей пользователя (UserGarageService.cs)*

**[ВСТАВИТЬ СКРИН: Visual Studio → DriveCareCore\Bookings\WorkshopOnlineBookingService.cs, метод CreateBookingAsync — вставка WorkshopOnlineBooking]**

*Рисунок 61 – Метод создания онлайн-записи (WorkshopOnlineBookingService.cs)*

**[ВСТАВИТЬ СКРИН: Visual Studio → DriveCare\Windows\WorkshopOnlineBookingWindow.xaml.cs — вызов WorkshopOnlineBookingService.CreateBookingAsync]**

*Рисунок 62 – Вызов сервиса онлайн-записи из окна приложения (WorkshopOnlineBookingWindow.xaml.cs)*

### 3.1.3 Задания и заказ-наряды в DriveCare Pro

Рабочий процесс автосервиса строится вокруг сущности Task. При оформлении сервисной записи автоматически создаётся задание методом ServiceBookingTaskService.CreateForBookingAsync; фрагмент создания объекта Task приведён на рисунке 63.

Редактирование заказ-наряда выполняется на странице EmployeeTaskCardPage. Сохранение услуг, запчастей и комментария реализовано в методе SaveTaskReportAsync; фрагмент метода показан на рисунке 64.

Обработка входящих онлайн-записей реализована в WorkshopOnlineBookingsPage.xaml.cs: обработчик Accept_Click вызывает WorkshopOnlineBookingService.AcceptBookingAsync. Фрагмент обработчика приведён на рисунке 65. Подтверждение записи в сервисе показано на рисунке 66 (метод AcceptBookingAsync).

**[ВСТАВИТЬ СКРИН: Visual Studio → DriveCarePro\Services\ServiceBooking\ServiceBookingTaskService.cs, метод CreateForBookingAsync]**

*Рисунок 63 – Метод создания задания при сервисной записи (ServiceBookingTaskService.cs)*

**[ВСТАВИТЬ СКРИН: Visual Studio → DriveCarePro\Pages\EmployeeTaskCardPage.xaml.cs, метод SaveTaskReportAsync — сохранение строк услуг и запчастей]**

*Рисунок 64 – Метод сохранения заказ-наряда (EmployeeTaskCardPage.xaml.cs)*

**[ВСТАВИТЬ СКРИН: Visual Studio → DriveCarePro\Pages\WorkshopOnlineBookingsPage.xaml.cs, метод Accept_Click — вызов AcceptBookingAsync]**

*Рисунок 65 – Обработчик подтверждения онлайн-записи (WorkshopOnlineBookingsPage.xaml.cs)*

**[ВСТАВИТЬ СКРИН: Visual Studio → DriveCareCore\Bookings\WorkshopOnlineBookingService.cs, метод AcceptBookingAsync — обновление статуса записи]**

*Рисунок 66 – Метод принятия онлайн-записи (WorkshopOnlineBookingService.cs)*

### 3.1.4 Управление сотрудниками организации

Управление персоналом мастерской реализовано в EmployeeManagementService. При добавлении сотрудника метод SaveCoreAsync проверяет уникальность логина, создаёт объект Employee и синхронизирует назначенные роли; фрагмент блока isNew приведён на рисунке 67.

Вызов сервиса из интерфейса выполняется в EmployeeEditWindow.xaml.cs: формируется модель EmployeeEditModel и вызывается EmployeeManagementService.SaveAsync. Фрагмент обработчика показан на рисунке 68.

После входа сотрудника список прав доступа загружается в EmployeePermissionService.RefreshForEmployeeAsync; фрагмент LINQ-запроса представлен на рисунке 69.

**[ВСТАВИТЬ СКРИН: Visual Studio → DriveCarePro\Services\EmployeeManagementService.cs, метод SaveCoreAsync, блок if (isNew) — new Employee]**

*Рисунок 67 – Метод сохранения нового сотрудника (EmployeeManagementService.cs)*

**[ВСТАВИТЬ СКРИН: Visual Studio → DriveCarePro\Windows\EmployeeEditWindow.xaml.cs — вызов EmployeeManagementService.SaveAsync]**

*Рисунок 68 – Вызов сервиса управления персоналом (EmployeeEditWindow.xaml.cs)*

**[ВСТАВИТЬ СКРИН: Visual Studio → DriveCarePro\Services\EmployeePermissionService.cs, метод RefreshForEmployeeAsync — загрузка кодов прав]**

*Рисунок 69 – Метод загрузки прав доступа сотрудника (EmployeePermissionService.cs)*

### 3.1.5 Обмен сообщениями и TCP-сервер

Сохранение сообщения реализовано в WorkshopMessagingService.SendFromUserAsync. Фрагмент метода приведён на рисунке 70.

На стороне клиента отправка инициируется в MessagesPage.xaml.cs: после сохранения вызывается WorkshopChatRealtimeClient.NotifyNewMessage. Фрагмент метода SendMessageAsync показан на рисунке 71.

Класс WorkshopChatRealtimeClient отправляет на сервер команду CHAT_PUSH; реализация метода NotifyNewMessage представлена на рисунке 72.

TCP-сервер реализован в файле Server.cs. Фрагмент обработки CHAT_PUSH приведён на рисунке 73.

**[ВСТАВИТЬ СКРИН: Visual Studio → DriveCareCore\Messaging\WorkshopMessagingService.cs, метод SendFromUserAsync]**

*Рисунок 70 – Метод отправки сообщения пользователем (WorkshopMessagingService.cs)*

**[ВСТАВИТЬ СКРИН: Visual Studio → DriveCare\Pages\User\MessagesPage.xaml.cs, метод SendMessageAsync — вызов SendFromUserAsync и NotifyNewMessage]**

*Рисунок 71 – Обработчик отправки сообщения в клиентском приложении (MessagesPage.xaml.cs)*

**[ВСТАВИТЬ СКРИН: Visual Studio → DriveCareCore\Messaging\WorkshopChatRealtimeClient.cs, метод NotifyNewMessage]**

*Рисунок 72 – Метод push-уведомления через TCP (WorkshopChatRealtimeClient.cs)*

**[ВСТАВИТЬ СКРИН: Visual Studio → Server.cs — ветка else if (command == "CHAT_PUSH") и BroadcastNewMessageAsync]**

*Рисунок 73 – Обработка команды CHAT_PUSH на TCP-сервере (Server.cs)*

### 3.1.6 Доступ к базе данных

Все клиентские приложения обращаются к базе данных через контекст Entity Framework DriveCareDBEntities. Инициализация контекста выполняется в классе AppConnect; его реализация показана на рисунке 74.

Для единообразной работы с контекстом в сервисах DriveCareCore используется вспомогательный метод WithDb в WorkshopOnlineBookingService. Фрагмент метода приведён на рисунке 75.

**[ВСТАВИТЬ СКРИН: Visual Studio → DriveCareCore\Data\BD\AppConnect.cs — поле model1 типа DriveCareDBEntities]**

*Рисунок 74 – Класс подключения к базе данных (AppConnect.cs)*

**[ВСТАВИТЬ СКРИН: Visual Studio → DriveCareCore\Bookings\WorkshopOnlineBookingService.cs, метод WithDb — using (var db = new DriveCareDBEntities())]**

*Рисунок 75 – Вспомогательный метод работы с контекстом Entity Framework (WorkshopOnlineBookingService.cs)*

## 3.2 Тестирование программного обеспечения

Тестирование информационной системы DriveCare выполнялось вручную по основным сценариям использования. Проверялись: регистрация и вход клиента и сотрудника, добавление автомобиля в гараж, создание онлайн-записи, обработка записи в DriveCare Pro, создание и завершение задания, отправка сообщения в чате с обновлением экрана, добавление сотрудника владельцем, загрузка изображения объявления через TCP-сервер.

При обнаружении ошибок приложение выводит информационные сообщения через диалоговые окна. Для проверки сетевого модуля чата при отправке сообщения в консоли TCP-сервера появляется строка о рассылке CHAT_PUSH подписчикам; пример вывода при тестировании показан на рисунке 76.

**[ВСТАВИТЬ СКРИН: запустить Server.cs + DriveCare, отправить сообщение в чат — в консоли сервера строки CHAT_SUBSCRIBE и CHAT_PUSH: разослано N подписчикам]**

*Рисунок 76 – Вывод TCP-сервера при тестировании обмена сообщениями*
