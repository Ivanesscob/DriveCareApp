# Диаграммы и описание таблиц БД DriveCare

Материалы для раздела «Проектирование базы данных» пояснительной записки.

## 1. Описание каждой таблицы (Word)

| Файл | Назначение |
|------|------------|
| **`DriveCare_Database_Table_Structures.docx`** | Word с таблицами полей (основной файл) |
| **`DriveCare_Database_Table_Structures.md`** | То же в Markdown |

```powershell
powershell -ExecutionPolicy Bypass -File Docs\tools\export_word_tables.ps1
```

FK в БД (опционально): `DriveCareCore/Data/BD/Sql/AllTables_ForeignKeys_Report.sql`

---

## 2. ER-диаграммы — **10 частей** (для отчёта)

**Не вставляйте одну общую картинку со всеми таблицами.** Используйте части по смыслу:

| № | PNG | О чём |
|---|-----|--------|
| 1 | `Docs/out/ER_Part01_User.png` | **Пользователь** — учётная запись, роли, уведомления |
| 2 | `Docs/out/ER_Part02_Employee_Rights.png` | **Сотрудники и права** Pro |
| 3 | `Docs/out/ER_Part03_References.png` | **Справочники** |
| 4 | `Docs/out/ER_Part04_User_Garage.png` | **Гараж** и обслуживание авто |
| 5 | `Docs/out/ER_Part05_Workshop_Structure.png` | **Организация мастерской** |
| 6 | `Docs/out/ER_Part06_Workshop_Tasks.png` | **Работа мастерской** — задачи, склад, документы |
| 7 | `Docs/out/ER_Part07_Clients_Chat_Booking.png` | **Клиенты, чат, онлайн-запись** |
| 8 | `Docs/out/ER_Part08_CarSales_Paint.png` | **Объявления** и **покраска** |
| 9 | `Docs/out/ER_Part09_Store.png` | **Интернет-магазин** |
| 10 | `Docs/out/ER_Part10_Analytics.png` | **Статистика** |

Подписи в Word: «Рисунок 2 — Подмодель пользователя DriveCare» … «Рисунок 11 — Подмодель статистики».

Исходники: `Docs/ER_Part01_User.puml` … `Docs/ER_Part10_Analytics.puml`  
Оглавление: **`DriveCare_ER_Parts_Index.md`**

Перегенерация:
```powershell
powershell -ExecutionPolicy Bypass -File Docs\tools\generate_db_docs.ps1
```
Затем `Alt+D` на `.puml` или PNG уже в `Docs/out/`.

---

## 3. Диаграмма классов

| Файл | PNG |
|------|-----|
| `DriveCare_Classes_Main.puml` | `Docs/out/DriveCare_Classes_Main.png` |

---

## 4. Источник

`DriveCareCore/Data/BD/Model1.edmx`, справочник: `DriveCare_Database_Tables_Guide.md`.
