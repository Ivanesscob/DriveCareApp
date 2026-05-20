using System;

namespace DriveCareCore.Bookings
{
    public enum WorkshopBookingStatus : byte
    {
        Pending = 0,
        Confirmed = 1,
        CancelledByClient = 2,
        RejectedByWorkshop = 3
    }

    public sealed class WorkshopOnlineBookingItem
    {
        public Guid BookingId { get; set; }
        public Guid WorkshopId { get; set; }
        public string WorkshopName { get; set; }
        public Guid UserId { get; set; }
        public string ClientDisplayName { get; set; }
        public string ClientPhone { get; set; }
        public string ClientComment { get; set; }
        public string IssueCategory { get; set; }
        public string CarDisplayName { get; set; }
        public DateTime? PreferredDate { get; set; }
        public byte StatusCode { get; set; }
        public WorkshopBookingStatus Status => (WorkshopBookingStatus)StatusCode;
        public DateTime CreatedAt { get; set; }

        public string IssueCategoryLabel =>
            string.IsNullOrWhiteSpace(IssueCategory)
                ? "—"
                : WorkshopBookingIssueCategories.GetDisplayName(IssueCategory);

        public string StatusLabel
        {
            get
            {
                switch (Status)
                {
                    case WorkshopBookingStatus.Confirmed: return "Подтверждена";
                    case WorkshopBookingStatus.CancelledByClient: return "Отменена клиентом";
                    case WorkshopBookingStatus.RejectedByWorkshop: return "Отклонена";
                    default: return "Ожидает подтверждения";
                }
            }
        }

        public string PreferredDateLabel =>
            PreferredDate.HasValue ? PreferredDate.Value.ToString("dd.MM.yyyy HH:mm") : "Согласуем позже";

        public string CreatedAtLabel => CreatedAt.ToString("dd.MM.yyyy HH:mm");
        public bool CanConfirm => StatusCode == (byte)WorkshopBookingStatus.Pending;
    }

    public sealed class WorkshopMapDetail
    {
        public Guid WorkshopId { get; set; }
        public string WorkshopName { get; set; }
        public string CompanyName { get; set; }
        public string Description { get; set; }
        public string AddressLine { get; set; }
        public string Phone { get; set; }
        public Guid? BusinessTypeId { get; set; }
        public string ServiceKindName { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
