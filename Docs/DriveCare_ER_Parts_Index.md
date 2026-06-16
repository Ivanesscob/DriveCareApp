# ER-диаграммы по частям (DriveCare)

Модель разбита на **10 рисунков**. Общую диаграмму «все таблицы сразу» **не используйте**.

| PNG | Подпись для отчёта |
|-----|-------------------|
| `Docs/out/ER_Part01_User.png` | Подмодель пользователя — регистрация, роли, уведомления |
| `Docs/out/ER_Part02_Employee_Rights.png` | Подмодель сотрудников мастерской и RBAC |
| `Docs/out/ER_Part03_References.png` | Подмодель справочной информации |
| `Docs/out/ER_Part04_User_Garage.png` | Подмодель личного гаража и истории ремонтов |
| `Docs/out/ER_Part05_Workshop_Structure.png` | Подмодель компании, филиала, расписания |
| `Docs/out/ER_Part06_Workshop_Tasks.png` | Подмодель заказ-нарядов, услуг, склада и актов |
| `Docs/out/ER_Part07_Clients_Chat_Booking.png` | Подмодель CRM мастерской и коммуникаций |
| `Docs/out/ER_Part08_CarSales_Paint.png` | Подмодель маркетплейса и покраски |
| `Docs/out/ER_Part09_Store.png` | Подмодель заказов и пунктов выдачи |
| `Docs/out/ER_Part10_Analytics.png` | Подмодель журнала событий |

Перегенерация: `powershell -ExecutionPolicy Bypass -File Docs\tools\generate_db_docs.ps1`
