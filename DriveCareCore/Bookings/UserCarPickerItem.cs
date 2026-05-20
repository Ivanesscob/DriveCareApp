using System;

namespace DriveCareCore.Bookings
{
    public sealed class UserCarPickerItem
    {
        public Guid UserCarId { get; set; }
        public Guid CarId { get; set; }
        public string DisplayName { get; set; }
    }
}
