using System;

namespace DriveCareCore.Painting
{
    public enum CarPaintKind : byte
    {
        Wheels = 1,
        FullCar = 2,
        Part = 3
    }

    public sealed class CarPaintColorOption
    {
        public Guid ColorId { get; set; }
        public string Name { get; set; }
    }

    public sealed class CarPaintCurrentInfo
    {
        public string ColorName { get; set; }
        public DateTime? Since { get; set; }
    }

    public sealed class CarPaintHistoryItem
    {
        public Guid RowId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string ColorName { get; set; }
        public CarPaintKind? PaintKind { get; set; }
        public string PartName { get; set; }
        public string Description { get; set; }
    }
}
