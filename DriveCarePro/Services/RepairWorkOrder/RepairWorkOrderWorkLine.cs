namespace DriveCarePro.Services.RepairWorkOrder
{
    public sealed class RepairWorkOrderWorkLine
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Multiplicity { get; set; } = string.Empty;
        public string Coefficient { get; set; } = string.Empty;
        public string PricePerHour { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public string Cost { get; set; } = string.Empty;
        public string Discount { get; set; } = string.Empty;
        public string Amount { get; set; } = string.Empty;
        public string Executor { get; set; } = string.Empty;
    }
}
