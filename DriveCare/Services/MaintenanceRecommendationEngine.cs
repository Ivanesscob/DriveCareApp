using DriveCare.Data;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DriveCare.Services
{
    /// <summary>
    /// Ориентиры по пробегу после последнего ТО (усреднённые регламенты; у разных авто интервалы различаются).
    /// </summary>
    internal static class MaintenanceRecommendationEngine
    {
        private sealed class IntervalRule
        {
            public string Title { get; }
            public int IntervalKm { get; }
            public string Comment { get; }

            public IntervalRule(string title, int intervalKm, string comment)
            {
                Title = title;
                IntervalKm = intervalKm;
                Comment = comment;
            }
        }

        private static readonly IntervalRule[] Rules =
        {
            new IntervalRule("Регламентное ТО (осмотр + расходники)", 15_000,
                "Типичный интервал между плановыми визитами; уточняйте по сервисной книжке."),
            new IntervalRule("Моторное масло и масляный фильтр", 10_000,
                "Для турбомоторов и тяжёлых условий чаще; для длинного интервала — по маслу."),
            new IntervalRule("ГРМ (ремень / цепь по регламенту)", 30_000,
                "Часто 60–120 тыс. км; здесь взят ориентир 30 тыс. как «контрольная точка» — сверяйте с инструкцией."),
            new IntervalRule("Тормозные диски и колодки", 80_000,
                "Сильно зависит от стиля езды; ориентир по пробегу."),
            new IntervalRule("Свечи зажигания", 30_000,
                "Для иридиевых свечей интервал может быть больше."),
            new IntervalRule("Фильтр салона", 20_000,
                "При аллергии и пыли — чаще.")
        };

        public static IReadOnlyList<MaintenanceRecommendationVm> Build(
            IReadOnlyList<MaintenanceHistoryItemVm> historyNewestFirst,
            int? currentMileageKm)
        {
            var list = new List<MaintenanceRecommendationVm>();
            var last = historyNewestFirst?.FirstOrDefault();
            var lastMileage = last?.MileageKm;

            if (!currentMileageKm.HasValue)
            {
                list.Add(new MaintenanceRecommendationVm
                {
                    PartName = "Пробег",
                    IntervalHint = "—",
                    Summary = "Нет данных для примерного пробега (нужен километраж в истории визитов) — тогда можно оценить «осталось до … км» по каждому пункту.",
                    Urgency = "Info"
                });
                return list;
            }

            if (!lastMileage.HasValue)
            {
                list.Add(new MaintenanceRecommendationVm
                {
                    PartName = "Последнее ТО",
                    IntervalHint = "—",
                    Summary = "В последней записи истории не указан пробег. Добавьте пробег при следующем визите в сервис — расчёт станет точнее.",
                    Urgency = "Info"
                });
                foreach (var rule in Rules)
                    list.Add(MakeUnknownIntervalRow(rule));
                return list;
            }

            var kmSince = currentMileageKm.Value - lastMileage.Value;
            if (kmSince < 0)
            {
                list.Add(new MaintenanceRecommendationVm
                {
                    PartName = "Данные",
                    IntervalHint = "—",
                    Summary = "Примерный пробег меньше, чем в последней записи ТО. Проверьте даты и километраж в истории.",
                    Urgency = "Watch"
                });
                return list;
            }

            foreach (var rule in Rules)
                list.Add(EvaluateRule(rule, kmSince, last));

            return list;
        }

        private static MaintenanceRecommendationVm MakeUnknownIntervalRow(IntervalRule rule)
        {
            return new MaintenanceRecommendationVm
            {
                PartName = rule.Title,
                IntervalHint = $"ориентир ~{rule.IntervalKm.ToString("N0", CultureInfo.InvariantCulture)} км",
                Summary = string.IsNullOrEmpty(rule.Comment) ? null : rule.Comment,
                Urgency = "Info"
            };
        }

        private static MaintenanceRecommendationVm EvaluateRule(
            IntervalRule rule,
            int kmSinceLastService,
            MaintenanceHistoryItemVm last)
        {
            var remaining = rule.IntervalKm - kmSinceLastService;
            var sb = new StringBuilder();
            sb.Append("После последнего ТО (");
            sb.Append(last.DateLabel);
            if (last.MileageKm.HasValue)
                sb.Append(", ").Append(last.MileageLabel);
            sb.Append(") прошло примерно ");
            sb.Append(kmSinceLastService.ToString("N0", CultureInfo.InvariantCulture)).Append(" км. ");

            string urgency;
            if (remaining > 8_000)
            {
                sb.Append("До ориентира «").Append(rule.Title).Append("» ещё порядка ")
                    .Append(remaining.ToString("N0", CultureInfo.InvariantCulture)).Append(" км.");
                urgency = "Good";
            }
            else if (remaining > 0)
            {
                sb.Append("До ориентира осталось около ")
                    .Append(remaining.ToString("N0", CultureInfo.InvariantCulture))
                    .Append(" км — имеет смысл запланировать работы.");
                urgency = "Watch";
            }
            else
            {
                sb.Append("Ориентир по пробегу уже пройден примерно на ")
                    .Append((-remaining).ToString("N0", CultureInfo.InvariantCulture))
                    .Append(" км — стоит проверить состояние узла у мастера.");
                urgency = "Due";
            }

            if (!string.IsNullOrEmpty(rule.Comment))
            {
                sb.Append(" ");
                sb.Append(rule.Comment);
            }

            return new MaintenanceRecommendationVm
            {
                PartName = rule.Title,
                IntervalHint = $"ориентир ~{rule.IntervalKm.ToString("N0", CultureInfo.InvariantCulture)} км",
                Summary = sb.ToString(),
                Urgency = urgency
            };
        }
    }
}
