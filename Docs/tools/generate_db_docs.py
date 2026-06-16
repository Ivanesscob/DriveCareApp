# -*- coding: utf-8 -*-
"""Generate ER diagrams, class diagram and table structure markdown from Model1.edmx."""
import re
import xml.etree.ElementTree as ET
from pathlib import Path
from collections import defaultdict

ROOT = Path(__file__).resolve().parents[2]
EDMX = ROOT / "DriveCareCore" / "Data" / "BD" / "Model1.edmx"
DOCS = ROOT / "Docs"

NS = {
    "edmx": "http://schemas.microsoft.com/ado/2009/11/edmx",
    "ssdl": "http://schemas.microsoft.com/ado/2009/11/edm/ssdl",
}

TABLE_DESC = {
    "Addresses": "почтовые и фактические адреса (город, улица, координаты для карты).",
    "Brands": "марки автомобилей (Toyota, BMW и т.д.).",
    "BusinessTypes": "виды деятельности автосервиса (СТО, покраска, шиномонтаж).",
    "CarColors": "история окраски автомобиля и деталей кузова.",
    "Cars": "экземпляры автомобилей (модель, VIN, госномер, год).",
    "CarSalePrices": "история цен объявления о продаже авто.",
    "CarSales": "объявления пользователей о продаже автомобилей.",
    "CarTypes": "типы кузова (седан, внедорожник).",
    "Colors": "справочник названий цветов.",
    "Companies": "юридические лица / организации, владеющие мастерскими.",
    "Countries": "страны для адресов и производителей.",
    "EmployeeNotifications": "уведомления, показанные сотруднику DriveCare Pro.",
    "EmployeeRolesMap": "назначение ролей сотрудникам мастерской.",
    "Employees": "учётные записи сотрудников автосервиса (DriveCare Pro).",
    "FuelTypes": "типы топлива автомобилей.",
    "Models": "модели автомобилей в рамках марки.",
    "Notifications": "шаблоны и типы уведомлений системы.",
    "OrderPickupPoints": "пункты выдачи заказов интернет-магазина.",
    "PartManufacturers": "производители запчастей.",
    "Parts": "общий каталог запчастей (артикул, производитель).",
    "PermissionGroups": "группы прав доступа для настройки ролей.",
    "Permissions": "отдельные права (код, название, группа).",
    "RepairCategories": "категории видов ремонта.",
    "RepairHistory": "история ремонтов автомобиля в мастерской.",
    "RolePermissionsMap": "связь ролей и прав (основная таблица RBAC).",
    "RolePermissions": "связь ролей и прав (альтернативная схема из SQL-скрипта).",
    "Roles": "роли пользователей и сотрудников системы.",
    "ServiceDocumentPartLines": "строки запчастей в итоговом документе обслуживания.",
    "ServiceDocuments": "акты и заказ-наряды по завершённым работам.",
    "ServiceDocumentServiceLines": "строки услуг в документе обслуживания.",
    "Statuses": "справочник статусов (задачи, объявления, ремонт).",
    "StoreOrderLines": "позиции товаров в заказе магазина.",
    "StoreOrders": "заказы клиентов в интернет-магазине DriveCare.",
    "sysdiagrams": "служебная таблица SQL Server для хранения диаграмм БД.",
    "TaskPartLines": "запчасти, указанные в задаче (заказ-наряде).",
    "TaskPurchaseRequestLines": "строки заявки на закупку запчастей.",
    "TaskPurchaseRequests": "заявки на закупку по задаче мастерской.",
    "Tasks": "задачи мастерской (заказ-наряды, работы по авто).",
    "TaskServiceLines": "услуги, указанные в задаче мастерской.",
    "UserCarComponentStatuses": "текущее состояние узлов авто в гараже пользователя.",
    "UserCarMaintenanceHistory": "история обслуживания авто пользователя.",
    "UserCars": "связь пользователя с автомобилем в личном гараже.",
    "UserCarSales": "связь пользователя с его объявлениями о продаже.",
    "UserNotifications": "уведомления, доставленные конкретному пользователю.",
    "UserRoles": "назначение ролей клиентам DriveCare.",
    "Users": "учётные записи клиентов приложения DriveCare.",
    "UserWorkshopPaintInquiries": "запросы пользователей на покраску в мастерской.",
    "WarehouseManagers": "ответственные за склад (контактные данные).",
    "WorkshopBusinessTypeChangeRequests": "заявки на изменение типов деятельности мастерской.",
    "WorkshopBusinessTypeChangeRequestTypes": "типы бизнеса в заявке на смену профиля.",
    "WorkshopBusinessTypes": "дополнительные виды деятельности мастерской.",
    "WorkshopConversations": "диалоги чата между клиентом и мастерской.",
    "WorkshopGuestCars": "автомобили клиентов мастерской (в т.ч. без регистрации).",
    "WorkshopMessages": "сообщения в чате клиент — мастерская.",
    "WorkshopOnlineBookings": "онлайн-записи клиентов на обслуживание.",
    "WorkshopOnlineBookingSettings": "настройки лимита онлайн-записей на день.",
    "WorkshopPaintColors": "доступные цвета покраски в мастерской.",
    "WorkshopPaintServices": "услуги покраски, предлагаемые мастерской.",
    "WorkshopParts": "склад запчастей конкретной мастерской.",
    "Workshops": "филиалы / точки автосервиса компании.",
    "WorkshopServiceClients": "клиентская база мастерской (ФИО, телефон).",
    "WorkshopServices": "каталог услуг мастерской с ценами.",
    "WorkshopServiceUnits": "единицы измерения услуг (н/ч, шт.).",
    "WorkshopWorkSchedules": "расписание работы мастерской по дням недели.",
    "AppActivityEvents": "журнал действий пользователей и сотрудников для статистики.",
}

SUBMODELS = {
    "ER_01_Users_Rights": [
        "Users", "UserRoles", "Roles", "Permissions", "PermissionGroups",
        "RolePermissionsMap", "RolePermissions", "Employees", "EmployeeRolesMap",
        "Notifications", "UserNotifications", "EmployeeNotifications",
    ],
    "ER_02_References": [
        "Countries", "Brands", "Models", "CarTypes", "FuelTypes", "Colors",
        "BusinessTypes", "Statuses", "RepairCategories", "Parts", "PartManufacturers",
    ],
    "ER_03_Cars": [
        "Cars", "CarColors", "UserCars", "RepairHistory",
        "UserCarMaintenanceHistory", "UserCarComponentStatuses", "Addresses",
    ],
    "ER_04_Companies_Workshops": [
        "Companies", "Workshops", "WorkshopServiceUnits", "WorkshopBusinessTypes",
        "WorkshopBusinessTypeChangeRequests", "WorkshopBusinessTypeChangeRequestTypes",
        "WarehouseManagers",
    ],
    "ER_05_Tasks_Services": [
        "Tasks", "TaskServiceLines", "TaskPartLines", "TaskPurchaseRequests",
        "TaskPurchaseRequestLines", "WorkshopServices", "WorkshopParts",
        "ServiceDocuments", "ServiceDocumentServiceLines", "ServiceDocumentPartLines",
    ],
    "ER_06_Clients_Communications": [
        "WorkshopServiceClients", "WorkshopGuestCars", "CarSales", "CarSalePrices",
        "UserCarSales", "WorkshopConversations", "WorkshopMessages",
        "WorkshopOnlineBookings", "WorkshopOnlineBookingSettings",
        "WorkshopWorkSchedules", "WorkshopPaintServices", "WorkshopPaintColors",
        "UserWorkshopPaintInquiries",
    ],
    "ER_07_Store_Analytics": [
        "OrderPickupPoints", "StoreOrders", "StoreOrderLines", "AppActivityEvents",
    ],
}

EXTRA_TABLES = {
    "RolePermissions": [
        ("RowId", "uniqueidentifier", "PK", "Идентификатор записи"),
        ("RoleId", "uniqueidentifier", "NOT NULL, FK → Roles", "Роль"),
        ("PermissionId", "uniqueidentifier", "NOT NULL, FK → Permissions", "Право доступа"),
    ],
    "AppActivityEvents": [
        ("RowId", "uniqueidentifier", "PK", "Идентификатор события"),
        ("EventCode", "nvarchar(80)", "NOT NULL", "Код события"),
        ("ActorKind", "tinyint", "NOT NULL", "Тип актора (0 — пользователь, 1 — сотрудник, 2 — система)"),
        ("UserId", "uniqueidentifier", "NULL, FK → Users", "Пользователь"),
        ("EmployeeId", "uniqueidentifier", "NULL, FK → Employees", "Сотрудник"),
        ("WorkshopId", "uniqueidentifier", "NULL, FK → Workshops", "Мастерская"),
        ("CompanyId", "uniqueidentifier", "NULL, FK → Companies", "Компания"),
        ("EntityType", "nvarchar(60)", "NULL", "Тип связанной сущности"),
        ("EntityId", "uniqueidentifier", "NULL", "Идентификатор сущности"),
        ("PayloadJson", "nvarchar(max)", "NULL", "Дополнительные данные (JSON)"),
        ("CreatedAt", "datetime2(0)", "NOT NULL, DEFAULT SYSUTCDATETIME()", "Дата и время события"),
    ],
}

FIELD_HINTS = {
    "RowId": "Уникальный идентификатор записи",
    "Name": "Наименование",
    "Description": "Описание / примечание",
    "CreatedAt": "Дата и время создания",
    "UpdatedAt": "Дата и время обновления",
    "Login": "Логин для входа",
    "Password": "Хеш или пароль учётной записи",
    "Email": "Адрес электронной почты",
    "Phone": "Номер телефона",
    "Title": "Заголовок",
    "Message": "Текст сообщения",
    "IsRead": "Признак прочтения",
    "IsActive": "Признак активности записи",
    "StatusId": "Ссылка на статус",
    "UserId": "Ссылка на пользователя",
    "EmployeeId": "Ссылка на сотрудника",
    "WorkshopId": "Ссылка на мастерскую",
    "CompanyId": "Ссылка на компанию",
    "CarId": "Ссылка на автомобиль",
    "TaskId": "Ссылка на задачу",
    "RoleId": "Ссылка на роль",
    "PermissionId": "Ссылка на право доступа",
}


def sql_type(prop):
    t = prop.get("Type", "nvarchar")
    if t == "nvarchar":
        ml = prop.get("MaxLength")
        if ml == "Max":
            return "nvarchar(max)"
        return f"nvarchar({ml or 'max'})"
    if t == "decimal":
        return f"decimal({prop.get('Precision', '18')},{prop.get('Scale', '2')})"
    if t == "datetime2":
        p = prop.get("Precision")
        return f"datetime2({p})" if p else "datetime2"
    if t == "time":
        p = prop.get("Precision")
        return f"time({p})" if p else "time"
    return t


def constraints(prop, pk_set, fk_map):
    parts = []
    name = prop.get("Name")
    if name in pk_set:
        parts.append("PK")
        if name == "RowId":
            parts.append("DEFAULT NEWID()")
    if prop.get("Nullable") == "false":
        parts.append("NOT NULL")
    else:
        parts.append("NULL")
    if prop.get("StoreGeneratedPattern") == "Computed":
        parts.append("COMPUTED")
    if prop.get("StoreGeneratedPattern") == "Identity":
        parts.append("IDENTITY")
    if name in fk_map:
        parts.append(f"FK → {fk_map[name]}")
    return ", ".join(parts)


def field_description(name, table):
    if name in FIELD_HINTS:
        return FIELD_HINTS[name]
    if name.endswith("Id") and name != "RowId":
        ref = name[:-2]
        return f"Ссылка на {ref}"
    if name.startswith("Is"):
        return f"Признак: {name[2:]}"
    return name


def parse_edmx():
    tree = ET.parse(EDMX)
    schema = tree.find(".//ssdl:Schema", NS)
    entities = {}
    fks = defaultdict(dict)  # table -> {col: ref_table}

    for et in schema.findall("ssdl:EntityType", NS):
        name = et.get("Name")
        if name in ("sysdiagram",):
            continue
        pk = {pr.get("Name") for pr in et.find("ssdl:Key", NS).findall("ssdl:PropertyRef", NS)}
        cols = []
        for p in et.findall("ssdl:Property", NS):
            cols.append({
                "name": p.get("Name"),
                "type": sql_type(p.attrib),
                "constraints": constraints(p.attrib, pk, {}),
                "desc": field_description(p.get("Name"), name),
            })
        entities[name] = {"columns": cols, "pk": pk}

    for assoc in schema.findall("ssdl:Association", NS):
        rc = assoc.find("ssdl:ReferentialConstraint", NS)
        if rc is None:
            continue
        dep_role = rc.find("ssdl:Dependent", NS).get("Role")
        dep_col = rc.find("ssdl:Dependent/ssdl:PropertyRef", NS).get("Name")
        princ_role = rc.find("ssdl:Principal", NS).get("Role")
        dep_end = assoc.find(f"ssdl:End[@Role='{dep_role}']", NS)
        princ_end = assoc.find(f"ssdl:End[@Role='{princ_role}']", NS)
        if dep_end is None or princ_end is None:
            continue
        dep_table = dep_end.get("Type").replace("Self.", "")
        princ_table = princ_end.get("Type").replace("Self.", "")
        fks[dep_table][dep_col] = princ_table

    for table, cols in entities.items():
        fk_map = fks.get(table, {})
        pk = entities[table]["pk"]
        for c in entities[table]["columns"]:
            prop = next(p for p in schema.find(f"ssdl:EntityType[@Name='{table}']", NS).findall("ssdl:Property", NS) if p.get("Name") == c["name"])
            c["constraints"] = constraints(prop.attrib, pk, fk_map)

    return entities, fks


def add_extra_tables(entities):
    for tname, rows in EXTRA_TABLES.items():
        entities[tname] = {
            "columns": [
                {"name": r[0], "type": r[1], "constraints": r[2], "desc": r[3]}
                for r in rows
            ],
            "pk": {"RowId"} if rows[0][0] == "RowId" else set(),
        }


def gen_markdown(entities):
    lines = [
        "# Структура таблиц базы данных DriveCare",
        "",
        "Текст для раздела «Физическое проектирование БД» пояснительной записки.",
        "Скопируйте блоки в Word. Нумерацию таблиц подстройте под ваш отчёт.",
        "",
    ]
    table_num = 1
    skip = {"sysdiagrams"}
    order = sorted(k for k in entities if k not in skip)
    # move Users, Roles near top
    priority = ["Users", "Roles", "Employees", "Workshops", "Tasks", "Cars"]
    order = [t for t in priority if t in entities] + [t for t in order if t not in priority]

    for tname in order:
        desc = TABLE_DESC.get(tname, "данные предметной области DriveCare.")
        lines.append(f"Таблица **{tname}** хранит информацию о {desc} Структура таблицы представлена в таблице 2.{table_num}.")
        lines.append("")
        lines.append(f"**Таблица 2.{table_num} — Структура таблицы {tname}**")
        lines.append("")
        lines.append("| Поле | Тип данных | Ограничения | Описание |")
        lines.append("|------|------------|-------------|----------|")
        for c in entities[tname]["columns"]:
            lines.append(f"| {c['name']} | {c['type']} | {c['constraints']} | {c['desc']} |")
        lines.append("")
        table_num += 1
    return "\n".join(lines)


def puml_entity(name, cols, compact=False):
    lines = [f"entity \"{name}\" {{"]
    for c in cols:
        pk = " *" if "PK" in c["constraints"] else ""
        if compact:
            lines.append(f"  {c['name']}{pk}")
        else:
            lines.append(f"  {c['name']} : {c['type']}{pk}")
    lines.append("}")
    return "\n".join(lines)


def gen_er_submodel(filename, tables, entities, fks):
    parts = [
        f"@startuml {filename}",
        "hide circle",
        "skinparam linetype ortho",
        "skinparam shadowing false",
        "",
    ]
    present = [t for t in tables if t in entities]
    alias = {t: re.sub(r"[^A-Za-z0-9_]", "_", t) for t in present}

    for t in present:
        parts.append(puml_entity(t, entities[t]["columns"], compact=False))
        parts.append("")

    seen = set()
    for t in present:
        for col, ref in fks.get(t, {}).items():
            if ref in present and (t, ref) not in seen:
                parts.append(f"{alias[t]} }}o--|| {alias[ref]} : {col}")
                seen.add((t, ref))
    parts.append("@enduml")
    return "\n".join(parts)


def gen_er_all_compact(entities, fks):
    parts = [
        "@startuml DriveCare_ER_AllTables",
        "hide circle",
        "skinparam shadowing false",
        "skinparam linetype polyline",
        "left to right direction",
        "",
    ]
    skip = {"sysdiagrams"}
    tables = sorted(t for t in entities if t not in skip)
    alias = {t: re.sub(r"[^A-Za-z0-9_]", "_", t) for t in tables}

    for t in tables:
        parts.append(puml_entity(t, entities[t]["columns"], compact=True))
        parts.append("")

    seen = set()
    for t in tables:
        for col, ref in fks.get(t, {}).items():
            if ref in entities and ref not in skip:
                key = (t, ref, col)
                if key not in seen:
                    parts.append(f"{alias[t]} }}o--|| {alias[ref]}")
                    seen.add(key)
    parts.append("@enduml")
    return "\n".join(parts)


def gen_class_diagram():
    return r"""@startuml DriveCare_Classes_Main
skinparam shadowing false
skinparam classAttributeIconSize 0

package "DriveCareCore — сущности БД (Entity Framework)" {
  class User {
    +RowId : Guid
    +Login : string
    +Email : string
    +Phone : string
  }
  class Employee {
    +RowId : Guid
    +Login : string
    +WorkshopId : Guid?
  }
  class Role {
    +RowId : Guid
    +Name : string
  }
  class Workshop {
    +RowId : Guid
    +Name : string
    +CompanyId : Guid
  }
  class Car {
    +RowId : Guid
    +Vin : string
    +ModelId : Guid
  }
  class Task {
    +RowId : Guid
    +Title : string
    +EmployeeId : Guid
    +ClientUserId : Guid?
    +CarId : Guid?
  }
  class StoreOrder {
    +RowId : Guid
    +OrderNumber : string
    +TotalAmount : decimal
  }
  User "1" -- "*" UserCar
  Car "1" -- "*" UserCar
  Employee "*" -- "1" Workshop
  Workshop "*" -- "1" Company
  Task "*" -- "1" Employee
  Task "*" -- "0..1" User : client
  Task "*" -- "0..1" Car
  User "*" -- "*" Role
  Employee "*" -- "*" Role
}

package "DriveCareCore — сервисы" {
  class AppConnect {
    {static} +model1 : DriveCareDBEntities
  }
  class WorkshopMessagingService {
    +SendFromUserAsync()
    +SendFromEmployeeAsync()
    +LoadMessagesAsync()
  }
  class StoreOrderService {
    +CreateOrderAsync()
  }
  class ActivityEventService {
    +TrackAsync()
  }
  class WorkshopOnlineBookingService {
    +CreateBookingAsync()
  }
  class PhotoTcpStorageService {
    +UploadAsync()
    +DownloadAsync()
  }
}

package "DriveCare — клиент (WPF)" {
  class AppState {
    {static} CurrentUserId
    {static} CanAccessStatistics
  }
  class MessagesPage
  class StoreCheckoutPage
}

package "DriveCarePro — мастерская (WPF)" {
  class ProHomePage
  class EmployeeTaskCardPage
  class StatisticsPage
}

AppConnect --> User : EF DbSet
AppConnect --> Task
WorkshopMessagingService ..> AppConnect
StoreOrderService ..> AppConnect
ActivityEventService ..> AppConnect
MessagesPage ..> WorkshopMessagingService
EmployeeTaskCardPage ..> Task
StatisticsPage ..> ActivityEventService
@enduml
"""


def main():
    entities, fks = parse_edmx()
    add_extra_tables(entities)

    DOCS.mkdir(exist_ok=True)
    (DOCS / "DriveCare_Database_Table_Structures.md").write_text(
        gen_markdown(entities), encoding="utf-8"
    )

    for fname, tables in SUBMODELS.items():
        (DOCS / f"{fname}.puml").write_text(
            gen_er_submodel(fname, tables, entities, fks), encoding="utf-8"
        )

    (DOCS / "DriveCare_ER_AllTables.puml").write_text(
        gen_er_all_compact(entities, fks), encoding="utf-8"
    )
    (DOCS / "DriveCare_Classes_Main.puml").write_text(
        gen_class_diagram(), encoding="utf-8"
    )

    print(f"Generated {len(SUBMODELS)+2} puml files and table structures markdown.")
    print(f"Tables: {len(entities)}")


if __name__ == "__main__":
    main()
