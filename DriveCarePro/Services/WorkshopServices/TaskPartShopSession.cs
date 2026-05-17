using System;
using System.Collections.Generic;
using System.Linq;

namespace DriveCarePro.Services.WorkshopServices
{
    /// <summary>Корзина магазина при переходе из карточки задания.</summary>
    public static class TaskPartShopSession
    {
        private static readonly List<TaskPartLineRow> _pending = new List<TaskPartLineRow>();

        public static Guid? TaskId { get; private set; }
        public static Guid WorkshopId { get; private set; }

        public static void Begin(Guid taskId, Guid workshopId)
        {
            TaskId = taskId;
            WorkshopId = workshopId;
            _pending.Clear();
        }

        public static void AddLines(IEnumerable<TaskPartLineRow> lines)
        {
            if (lines == null)
                return;
            foreach (var line in lines)
            {
                if (line == null || string.IsNullOrWhiteSpace(line.PartName))
                    continue;
                line.RecalculateAmount();
                _pending.Add(line);
            }
        }

        public static List<TaskPartLineRow> TakePendingLines()
        {
            if (_pending.Count == 0)
                return new List<TaskPartLineRow>();

            var copy = _pending.Select(CloneRow).ToList();
            _pending.Clear();
            return copy;
        }

        private static TaskPartLineRow CloneRow(TaskPartLineRow src) => new TaskPartLineRow
        {
            WorkshopPartId = src.WorkshopPartId,
            PartName = src.PartName,
            Quantity = src.Quantity,
            UnitName = src.UnitName,
            UnitPrice = src.UnitPrice,
            LineAmount = src.LineAmount
        };
    }
}
