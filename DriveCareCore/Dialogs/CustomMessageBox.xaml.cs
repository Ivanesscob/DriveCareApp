using System.Windows;
using System.Windows.Input;

namespace DriveCareCore.Dialogs
{
    public partial class CustomMessageBox : Window
    {
        private readonly MessageBoxButton _buttons;

        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        public CustomMessageBox(string message, string caption, MessageBoxButton button, MessageBoxImage icon)
        {
            InitializeComponent();
            _buttons = button;

            MessageText.Text = message ?? string.Empty;
            TitleText.Text = string.IsNullOrWhiteSpace(caption) ? "DriveCare" : caption;

            ApplyIcon(icon);
            ApplyButtons(button);

            Loaded += (_, __) =>
            {
                if (BtnOk.Visibility == Visibility.Visible)
                    BtnOk.Focus();
                else if (BtnYes.Visibility == Visibility.Visible)
                    BtnYes.Focus();
                else if (BtnCancel.Visibility == Visibility.Visible)
                    BtnCancel.Focus();
            };

            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    HandleEscape();
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.Return && Keyboard.Modifiers == ModifierKeys.None)
                {
                    HandleEnterDefault();
                    e.Handled = true;
                }
            };
        }

        private void ApplyIcon(MessageBoxImage icon)
        {
            IconInfo.Visibility = Visibility.Collapsed;
            IconWarning.Visibility = Visibility.Collapsed;
            IconError.Visibility = Visibility.Collapsed;
            IconQuestion.Visibility = Visibility.Collapsed;

            // У MessageBoxImage дублируются числовые значения (Information=Asterisk, Warning=Exclamation,
            // Error=Stop и т.д.) — в switch нельзя писать два case с одним значением (CS0152).
            if (icon == MessageBoxImage.Information)
                IconInfo.Visibility = Visibility.Visible;
            else if (icon == MessageBoxImage.Warning)
                IconWarning.Visibility = Visibility.Visible;
            else if (icon == MessageBoxImage.Error || icon == MessageBoxImage.Hand)
                IconError.Visibility = Visibility.Visible;
            else if (icon == MessageBoxImage.Question)
                IconQuestion.Visibility = Visibility.Visible;

            var showIcon = IconInfo.Visibility == Visibility.Visible
                           || IconWarning.Visibility == Visibility.Visible
                           || IconError.Visibility == Visibility.Visible
                           || IconQuestion.Visibility == Visibility.Visible;
            IconColumn.Width = showIcon ? GridLength.Auto : new GridLength(0);
        }

        private void ApplyButtons(MessageBoxButton button)
        {
            BtnOk.Visibility = Visibility.Collapsed;
            BtnCancel.Visibility = Visibility.Collapsed;
            BtnYes.Visibility = Visibility.Collapsed;
            BtnNo.Visibility = Visibility.Collapsed;

            switch (button)
            {
                case MessageBoxButton.OK:
                    BtnOk.Visibility = Visibility.Visible;
                    BtnOk.IsDefault = true;
                    break;
                case MessageBoxButton.OKCancel:
                    BtnCancel.Visibility = Visibility.Visible;
                    BtnOk.Visibility = Visibility.Visible;
                    BtnOk.IsDefault = true;
                    break;
                case MessageBoxButton.YesNo:
                    BtnNo.Visibility = Visibility.Visible;
                    BtnYes.Visibility = Visibility.Visible;
                    BtnYes.IsDefault = true;
                    break;
                case MessageBoxButton.YesNoCancel:
                    BtnCancel.Visibility = Visibility.Visible;
                    BtnNo.Visibility = Visibility.Visible;
                    BtnYes.Visibility = Visibility.Visible;
                    BtnYes.IsDefault = true;
                    break;
            }
        }

        private void HandleEnterDefault()
        {
            switch (_buttons)
            {
                case MessageBoxButton.OK:
                case MessageBoxButton.OKCancel:
                    if (BtnOk.Visibility == Visibility.Visible)
                        BtnOk_OnClick(BtnOk, new RoutedEventArgs());
                    break;
                case MessageBoxButton.YesNo:
                case MessageBoxButton.YesNoCancel:
                    if (BtnYes.Visibility == Visibility.Visible)
                        BtnYes_OnClick(BtnYes, new RoutedEventArgs());
                    break;
            }
        }

        private void HandleEscape()
        {
            if (BtnCancel.Visibility == Visibility.Visible)
                BtnCancel_OnClick(BtnCancel, new RoutedEventArgs());
            else if (BtnNo.Visibility == Visibility.Visible && BtnYes.Visibility == Visibility.Visible && BtnCancel.Visibility != Visibility.Visible)
            {
                Result = MessageBoxResult.None;
                Close();
            }
            else if (BtnOk.Visibility == Visibility.Visible)
                BtnOk_OnClick(BtnOk, new RoutedEventArgs());
        }

        private void TitleBar_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                try { DragMove(); } catch { /* ignore */ }
        }

        private void BtnClose_OnClick(object sender, RoutedEventArgs e)
        {
            // Поведение как у системного MessageBox при закрытии крестиком
            if (BtnCancel.Visibility == Visibility.Visible)
            {
                Result = MessageBoxResult.Cancel;
                Close();
                return;
            }
            if (BtnYes.Visibility == Visibility.Visible && BtnNo.Visibility == Visibility.Visible)
            {
                Result = BtnCancel.Visibility == Visibility.Visible ? MessageBoxResult.Cancel : MessageBoxResult.None;
                Close();
                return;
            }
            if (BtnOk.Visibility == Visibility.Visible)
            {
                Result = MessageBoxResult.OK;
                Close();
            }
        }

        private void BtnOk_OnClick(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.OK;
            Close();
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Cancel;
            Close();
        }

        private void BtnYes_OnClick(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Yes;
            Close();
        }

        private void BtnNo_OnClick(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.No;
            Close();
        }
    }
}
