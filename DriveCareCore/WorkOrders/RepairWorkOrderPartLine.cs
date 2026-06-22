namespace DriveCareCore.WorkOrders
{
    public sealed class RepairWorkOrderPartLine
    {
        public string Number { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public string Quantity { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
        public string Discount { get; set; } = string.Empty;
        public string Amount { get; set; } = string.Empty;
    }
}
