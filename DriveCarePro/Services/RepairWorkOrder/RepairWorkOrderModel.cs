using System;
using System.Collections.Generic;
using System.Linq;

namespace DriveCarePro.Services.RepairWorkOrder
{
    /// <summary>Данные заказ-наряда для печати (пока без сохранения в БД).</summary>
    public sealed class RepairWorkOrderModel
    {
        public string CompanyName { get; set; } = string.Empty;
        public string CompanyLegalAddress { get; set; } = string.Empty;
        public string CompanyPhone { get; set; } = string.Empty;

        public string OrderDate { get; set; } = string.Empty;
        public string OrderTime { get; set; } = string.Empty;

        public string ClientName { get; set; } = string.Empty;
        public string ClientAddress { get; set; } = string.Empty;
        public string ClientPhone { get; set; } = string.Empty;

        public string CarDescription { get; set; } = string.Empty;
        public string Vin { get; set; } = string.Empty;
        public string Year { get; set; } = string.Empty;
        public string EngineNumber { get; set; } = string.Empty;
        public string Mileage { get; set; } = string.Empty;
        public string BodyNumber { get; set; } = string.Empty;
        public string PlateNumber { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;

        public string RepairType { get; set; } = string.Empty;
        public string VisitReason { get; set; } = string.Empty;
        public string SpecialNotes { get; set; } = string.Empty;

        public List<RepairWorkOrderWorkLine> WorkLines { get; set; } = new List<RepairWorkOrderWorkLine>();

        public string WorkCode { get; set; } = string.Empty;
        public string WorkName { get; set; } = string.Empty;
        public string WorkMultiplicity { get; set; } = string.Empty;
        public string WorkCoefficient { get; set; } = string.Empty;
        public string WorkPricePerHour { get; set; } = string.Empty;
        public string WorkTime { get; set; } = string.Empty;
        public string WorkCost { get; set; } = string.Empty;
        public string WorkDiscount { get; set; } = string.Empty;
        public string WorkAmount { get; set; } = string.Empty;
        public string WorkExecutor { get; set; } = string.Empty;

        public string PartsNumber { get; set; } = string.Empty;
        public string PartsName { get; set; } = string.Empty;
        public string PartsUnit { get; set; } = string.Empty;
        public string PartsQuantity { get; set; } = string.Empty;
        public string PartsPrice { get; set; } = string.Empty;
        public string PartsDiscount { get; set; } = string.Empty;
        public string PartsAmount { get; set; } = string.Empty;

        public string WorksTotal { get; set; } = string.Empty;
        public string PartsTotal { get; set; } = string.Empty;
        public string CustomerPartsNumber { get; set; } = string.Empty;
        public string CustomerPartsName { get; set; } = string.Empty;
        public string CustomerPartsQuantity { get; set; } = string.Empty;
        public string CustomerPartsTotal { get; set; } = string.Empty;

        public string LaborCostSum { get; set; } = string.Empty;
        public string PartsCostSum { get; set; } = string.Empty;
        public string Subtotal { get; set; } = string.Empty;
        public string TotalDiscount { get; set; } = string.Empty;
        public string TotalToPay { get; set; } = string.Empty;

        public IReadOnlyList<RepairWorkOrderWorkLine> GetEffectiveWorkLines()
        {
            if (WorkLines != null && WorkLines.Count > 0)
                return WorkLines.Where(l => l != null && !string.IsNullOrWhiteSpace(l.Name)).ToList();

            if (!string.IsNullOrWhiteSpace(VisitReason))
            {
                return VisitReason
                    .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .Where(line => line.Length > 0)
                    .Select(line => new RepairWorkOrderWorkLine { Name = line, Executor = WorkExecutor ?? string.Empty })
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(WorkName))
                return new List<RepairWorkOrderWorkLine> { new RepairWorkOrderWorkLine { Name = WorkName, Executor = WorkExecutor ?? string.Empty } };

            return new List<RepairWorkOrderWorkLine>();
        }

        public IDictionary<string, string> ToTokenMap(bool includeWorkRowTokens = false)
        {
            var lines = GetEffectiveWorkLines();
            var first = lines.FirstOrDefault();

            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [RepairWorkOrderTokens.CompanyName] = CompanyName ?? string.Empty,
                [RepairWorkOrderTokens.CompanyAddress] = CompanyLegalAddress ?? string.Empty,
                [RepairWorkOrderTokens.CompanyPhone] = CompanyPhone ?? string.Empty,
                [RepairWorkOrderTokens.OrderDate] = OrderDate ?? string.Empty,
                [RepairWorkOrderTokens.OrderTime] = OrderTime ?? string.Empty,
                [RepairWorkOrderTokens.ClientName] = ClientName ?? string.Empty,
                [RepairWorkOrderTokens.ClientAddress] = ClientAddress ?? string.Empty,
                [RepairWorkOrderTokens.ClientPhone] = ClientPhone ?? string.Empty,
                [RepairWorkOrderTokens.CarDescription] = CarDescription ?? string.Empty,
                [RepairWorkOrderTokens.Vin] = Vin ?? string.Empty,
                [RepairWorkOrderTokens.Year] = Year ?? string.Empty,
                [RepairWorkOrderTokens.EngineNumber] = EngineNumber ?? string.Empty,
                [RepairWorkOrderTokens.Mileage] = Mileage ?? string.Empty,
                [RepairWorkOrderTokens.BodyNumber] = BodyNumber ?? string.Empty,
                [RepairWorkOrderTokens.PlateNumber] = PlateNumber ?? string.Empty,
                [RepairWorkOrderTokens.Color] = Color ?? string.Empty,
                [RepairWorkOrderTokens.RepairType] = RepairType ?? string.Empty,
                [RepairWorkOrderTokens.VisitReason] = FormatVisitReasonSummary(lines),
                [RepairWorkOrderTokens.SpecialNotes] = SpecialNotes ?? string.Empty,
                [RepairWorkOrderTokens.WorkCode] = includeWorkRowTokens ? (first?.Code ?? WorkCode ?? string.Empty) : string.Empty,
                [RepairWorkOrderTokens.WorkName] = includeWorkRowTokens ? (first?.Name ?? WorkName ?? string.Empty) : string.Empty,
                [RepairWorkOrderTokens.WorkMultiplicity] = includeWorkRowTokens ? (first?.Multiplicity ?? WorkMultiplicity ?? string.Empty) : string.Empty,
                [RepairWorkOrderTokens.WorkCoefficient] = includeWorkRowTokens ? (first?.Coefficient ?? WorkCoefficient ?? string.Empty) : string.Empty,
                [RepairWorkOrderTokens.WorkPricePerHour] = includeWorkRowTokens ? (first?.PricePerHour ?? WorkPricePerHour ?? string.Empty) : string.Empty,
                [RepairWorkOrderTokens.WorkTime] = includeWorkRowTokens ? (first?.Time ?? WorkTime ?? string.Empty) : string.Empty,
                [RepairWorkOrderTokens.WorkCost] = includeWorkRowTokens ? (first?.Cost ?? WorkCost ?? string.Empty) : string.Empty,
                [RepairWorkOrderTokens.WorkDiscount] = includeWorkRowTokens ? (first?.Discount ?? WorkDiscount ?? string.Empty) : string.Empty,
                [RepairWorkOrderTokens.WorkAmount] = includeWorkRowTokens ? (first?.Amount ?? WorkAmount ?? string.Empty) : string.Empty,
                [RepairWorkOrderTokens.WorkExecutor] = includeWorkRowTokens ? (first?.Executor ?? WorkExecutor ?? string.Empty) : string.Empty,
                [RepairWorkOrderTokens.WorksTotal] = WorksTotal ?? string.Empty,
                [RepairWorkOrderTokens.PartsNumber] = PartsNumber ?? string.Empty,
                [RepairWorkOrderTokens.PartsName] = PartsName ?? string.Empty,
                [RepairWorkOrderTokens.PartsUnit] = PartsUnit ?? string.Empty,
                [RepairWorkOrderTokens.PartsQuantity] = PartsQuantity ?? string.Empty,
                [RepairWorkOrderTokens.PartsPrice] = PartsPrice ?? string.Empty,
                [RepairWorkOrderTokens.PartsDiscount] = PartsDiscount ?? string.Empty,
                [RepairWorkOrderTokens.PartsAmount] = PartsAmount ?? string.Empty,
                [RepairWorkOrderTokens.PartsTotal] = PartsTotal ?? string.Empty,
                [RepairWorkOrderTokens.CustomerPartsNumber] = CustomerPartsNumber ?? string.Empty,
                [RepairWorkOrderTokens.CustomerPartsName] = CustomerPartsName ?? string.Empty,
                [RepairWorkOrderTokens.CustomerPartsQuantity] = CustomerPartsQuantity ?? string.Empty,
                [RepairWorkOrderTokens.CustomerPartsTotal] = CustomerPartsTotal ?? string.Empty,
                [RepairWorkOrderTokens.LaborCostSum] = LaborCostSum ?? string.Empty,
                [RepairWorkOrderTokens.PartsCostSum] = PartsCostSum ?? string.Empty,
                [RepairWorkOrderTokens.Subtotal] = Subtotal ?? string.Empty,
                [RepairWorkOrderTokens.TotalDiscount] = TotalDiscount ?? string.Empty,
                [RepairWorkOrderTokens.TotalToPay] = TotalToPay ?? string.Empty
            };
        }

        private static string FormatVisitReasonSummary(IReadOnlyList<RepairWorkOrderWorkLine> lines)
        {
            if (lines == null || lines.Count == 0)
                return string.Empty;
            return string.Join(Environment.NewLine, lines.Select(l => l.Name?.Trim()).Where(n => !string.IsNullOrEmpty(n)));
        }
    }
}
