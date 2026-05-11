using DriveCareCore.Data.BD;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DriveCareCore.Data.Services
{
    /// <summary>Статусы модерации продажи авто: CarSales.StatusId ссылается на Statuses.RowId.</summary>
    public static class CarSaleModerationStatuses
    {
        /// <summary>RowId в Statuses для «на модерации», если в справочнике нет строк с нужными именами.</summary>
        public const string AwaitingModerationStatusId = "0393CC67-C871-42E8-8068-4BFCE4DAC8A4";

        public static readonly Guid AwaitingModerationStatusGuid = new Guid(AwaitingModerationStatusId);

        public const string AwaitingModeration = "Ожидает модерации";
        public const string SentForModeration = "Отправлено на модерацию";
        public const string ApprovedModeration = "Одобрено модерацией";
        public const string ReturnedForCorrection = "Возвращено на корректировку";

        public static Guid? ResolveCarSaleStatusIdByName(DriveCareDBEntities db, string statusName)
        {
            if (db == null || string.IsNullOrWhiteSpace(statusName))
                return null;
            var t = statusName.Trim();
            var exact = db.Statuses.FirstOrDefault(s => s.Name == t);
            if (exact != null)
                return exact.RowId;
            return db.Statuses
                .ToList()
                .Where(s => s.Name != null)
                .Select(s => new { s.RowId, Name = s.Name.Trim() })
                .FirstOrDefault(x => string.Equals(x.Name, t, StringComparison.OrdinalIgnoreCase))
                ?.RowId;
        }

        /// <summary>RowId для новой заявки: «Отправлено на модерацию», иначе «Ожидает модерации», иначе константа GUID.</summary>
        public static Guid ResolveStatusIdForNewCarSale(DriveCareDBEntities db)
        {
            return ResolveCarSaleStatusIdByName(db, SentForModeration)
                ?? ResolveCarSaleStatusIdByName(db, AwaitingModeration)
                ?? AwaitingModerationStatusGuid;
        }

        public static bool IsCatalogApprovedStatusId(DriveCareDBEntities db, Guid? statusId)
        {
            var approved = ResolveCarSaleStatusIdByName(db, ApprovedModeration);
            return statusId.HasValue && approved.HasValue && statusId.Value == approved.Value;
        }

        /// <summary>Очередь модерации: всё, что не статус «Одобрено модерацией» (по RowId из Statuses).</summary>
        public static bool IsInModerationQueue(DriveCareDBEntities db, Guid? statusId)
        {
            var approved = ResolveCarSaleStatusIdByName(db, ApprovedModeration);
            if (!approved.HasValue)
                return true;
            return !statusId.HasValue || statusId.Value != approved.Value;
        }

        public static string FormatModerationStatusDisplay(DriveCareDBEntities db, Guid? statusId)
        {
            if (db == null)
                return statusId?.ToString() ?? "—";
            if (!statusId.HasValue)
                return AwaitingModeration;
            var name = db.Statuses.Where(s => s.RowId == statusId.Value).Select(s => s.Name).FirstOrDefault();
            return string.IsNullOrWhiteSpace(name) ? statusId.Value.ToString() : name.Trim();
        }

        public static string FormatModerationStatusDisplay(IDictionary<Guid, string> statusNamesByRowId, Guid? statusId)
        {
            if (!statusId.HasValue)
                return AwaitingModeration;
            if (statusNamesByRowId != null && statusNamesByRowId.TryGetValue(statusId.Value, out var name) &&
                !string.IsNullOrWhiteSpace(name))
                return name.Trim();
            return statusId.Value.ToString();
        }
    }
}
