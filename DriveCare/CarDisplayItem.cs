using System;
using System.Windows.Media;

namespace DriveCare
{
    public sealed class CarDisplayItem
    {
        public ImageSource Photo { get; set; }
        public string Name { get; set; }
        // Идентификаторы нужны для открытия нового окна с нужной инфой.
        public Guid CarId { get; set; }
        public Guid UserCarId { get; set; }
    }
}
