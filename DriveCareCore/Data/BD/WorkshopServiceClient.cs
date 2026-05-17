using System;

namespace DriveCareCore.Data.BD
{
    public class WorkshopServiceClient
    {
        public Guid RowId { get; set; }
        public Guid WorkshopId { get; set; }
        public Guid? UserId { get; set; }
        public string FullName { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public bool IsRegisteredUser { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
