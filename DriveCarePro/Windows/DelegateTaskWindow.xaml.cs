using DriveCarePro.Services;
using System;
using System.Collections.Generic;
using System.Windows;

namespace DriveCarePro.Windows
{
    public partial class DelegateTaskWindow : Window
    {
        private readonly Guid _sourceTaskId;
        private readonly Guid _fromEmployeeId;

        public DelegateTaskWindow(Guid sourceTaskId, Guid fromEmployeeId, IList<DelegateEmployeeOption> employees)
        {
            _sourceTaskId = sourceTaskId;
            _fromEmployeeId = fromEmployeeId;
            InitializeComponent();
            EmployeesGrid.ItemsSource = employees ?? new List<DelegateEmployeeOption>();
            if (EmployeesGrid.Items.Count > 0)
                EmployeesGrid.SelectedIndex = 0;
        }

        public bool Delegated { get; private set; }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (!(EmployeesGrid.SelectedItem is DelegateEmployeeOption selected))
            {
                MessageBox.Show("Выберите сотрудника.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var (ok, error, _) = await TaskDelegationService.DelegateAsync(
                _sourceTaskId, _fromEmployeeId, selected.EmployeeId).ConfigureAwait(true);

            if (!ok)
            {
                MessageBox.Show(error ?? "Не удалось передать.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Delegated = true;
            DialogResult = true;
            Close();
        }
    }
}
