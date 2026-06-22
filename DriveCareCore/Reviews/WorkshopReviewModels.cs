using System;
using System.Globalization;

namespace DriveCareCore.Reviews
{
    public sealed class WorkshopReviewDisplay
    {
        public byte Rating { get; set; }
        public string Comment { get; set; }
        public string Pros { get; set; }
        public string Cons { get; set; }
        public string AuthorName { get; set; }
        public DateTime CreatedAt { get; set; }

        public string StarsDisplay =>
            new string('★', Math.Max(0, Math.Min(5, (int)Rating))) +
            new string('☆', Math.Max(0, 5 - Math.Min(5, (int)Rating)));

        public string DateLabel => CreatedAt.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("ru-RU"));

        public bool HasComment => !string.IsNullOrWhiteSpace(Comment);
        public bool HasPros => !string.IsNullOrWhiteSpace(Pros);
        public bool HasCons => !string.IsNullOrWhiteSpace(Cons);
    }

    public sealed class WorkshopRatingSummary
    {
        public decimal? AvgRating { get; set; }
        public int ReviewCount { get; set; }

        public bool HasReviews => ReviewCount > 0;

        public string AvgRatingDisplay => HasReviews
            ? AvgRating.GetValueOrDefault().ToString("0.0", CultureInfo.GetCultureInfo("ru-RU"))
            : "—";

        public string StarsDisplay
        {
            get
            {
                if (!HasReviews)
                    return "☆☆☆☆☆";
                var rounded = (int)Math.Round(AvgRating.GetValueOrDefault(), MidpointRounding.AwayFromZero);
                rounded = Math.Max(1, Math.Min(5, rounded));
                return new string('★', rounded) + new string('☆', 5 - rounded);
            }
        }

        public string SummaryLine => HasReviews
            ? $"{StarsDisplay} {AvgRatingDisplay} ({ReviewCount} отз.)"
            : "Пока нет отзывов";
    }

    public sealed class WorkshopReviewRequest
    {
        public Guid WorkshopId { get; set; }
        public Guid DocumentId { get; set; }
        public Guid? RepairHistoryId { get; set; }
        public string WorkshopName { get; set; }
    }

    public sealed class WorkshopReviewSubmit
    {
        public Guid WorkshopId { get; set; }
        public Guid UserId { get; set; }
        public Guid? DocumentId { get; set; }
        public Guid? RepairHistoryId { get; set; }
        public byte Rating { get; set; }
        public string Comment { get; set; }
        public string Pros { get; set; }
        public string Cons { get; set; }
    }
}
