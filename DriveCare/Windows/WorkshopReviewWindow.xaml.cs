using DriveCareCore.Reviews;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Task = System.Threading.Tasks.Task;

namespace DriveCare.Windows
{
    public partial class WorkshopReviewWindow : Window
    {
        readonly Guid _userId;
        readonly WorkshopReviewRequest _request;
        byte _rating;

        WorkshopReviewWindow(Guid userId, WorkshopReviewRequest request)
        {
            InitializeComponent();
            _userId = userId;
            _request = request ?? new WorkshopReviewRequest();
            WorkshopText.Text = string.IsNullOrWhiteSpace(_request.WorkshopName)
                ? "Поделитесь впечатлением о визите в сервис."
                : "Мастерская: " + _request.WorkshopName.Trim();
            UpdateStars();
        }

        public static bool TryShow(Window owner, Guid userId, WorkshopReviewRequest request)
        {
            if (userId == Guid.Empty || request == null || request.WorkshopId == Guid.Empty)
                return false;

            var hasReview = WorkshopReviewService.HasReviewForDocumentAsync(userId, request.DocumentId)
                .GetAwaiter().GetResult();
            if (hasReview)
            {
                MessageBox.Show("Вы уже оставили отзыв по этому визиту.", "Оценка сервиса",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            var win = new WorkshopReviewWindow(userId, request) { Owner = owner };
            return win.ShowDialog() == true;
        }

        public static async Task TryShowFromNotificationAsync(Window owner, Guid userId, string description)
        {
            var request = await WorkshopReviewService.TryParseNotificationDescription(description).ConfigureAwait(true);
            if (request == null)
                return;
            TryShow(owner, userId, request);
        }

        private void Star_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && byte.TryParse(btn.Tag?.ToString(), out var r))
            {
                _rating = r;
                UpdateStars();
            }
        }

        void UpdateStars()
        {
            var buttons = StarsPanel.Children.OfType<Button>().ToList();
            for (var i = 0; i < buttons.Count; i++)
            {
                var star = (byte)(i + 1);
                var active = _rating >= star;
                buttons[i].Foreground = active
                    ? new SolidColorBrush(Color.FromRgb(255, 193, 7))
                    : (Brush)FindResource("App.Brush.Muted");
            }

            RatingHintText.Text = _rating switch
            {
                1 => "Плохо",
                2 => "Так себе",
                3 => "Нормально",
                4 => "Хорошо",
                5 => "Отлично",
                _ => "Нажмите на звезду"
            };
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;
            if (_rating < 1)
            {
                ErrorText.Text = "Выберите оценку от 1 до 5 звёзд.";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            var submit = new WorkshopReviewSubmit
            {
                UserId = _userId,
                WorkshopId = _request.WorkshopId,
                DocumentId = _request.DocumentId,
                RepairHistoryId = _request.RepairHistoryId,
                Rating = _rating,
                Pros = ProsBox.Text?.Trim(),
                Cons = ConsBox.Text?.Trim(),
                Comment = CommentBox.Text?.Trim()
            };

            var (ok, error) = await WorkshopReviewService.TrySubmitAsync(submit).ConfigureAwait(true);
            if (!ok)
            {
                ErrorText.Text = error ?? "Не удалось сохранить отзыв.";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
