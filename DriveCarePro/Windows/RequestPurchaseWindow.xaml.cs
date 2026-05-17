using DriveCarePro.Services;
using System;
using System.Collections.Generic;
using System.Windows;

namespace DriveCarePro.Windows
{
    public partial class RequestPurchaseWindow : Window
    {
        public RequestPurchaseWindow(IList<DelegateEmployeeOption> employees)
        {
            InitializeComponent();
            EmployeesGrid.ItemsSource = employees ?? new List<DelegateEmployeeOption>();
            if (EmployeesGrid.Items.Count > 0)
                EmployeesGrid.SelectedIndex = 0;
        }

        public DelegateEmployeeOption SelectedEmployee =>
            EmployeesGrid.SelectedItem as DelegateEmployeeOption;

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedEmployee == null)
            {
                MessageBox.Show("Выберите сотрудника.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }
    }
}
