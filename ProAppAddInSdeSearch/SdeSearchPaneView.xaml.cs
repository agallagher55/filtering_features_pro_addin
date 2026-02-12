using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace ProAppAddInSdeSearch
{
    /// <summary>
    /// Interaction logic for SdeSearchPaneView.xaml
    /// </summary>
    public partial class SdeSearchPaneView : UserControl
    {
        public SdeSearchPaneView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Handle Enter key in search box to trigger filter
        /// </summary>
        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var vm = DataContext as SdeSearchPaneViewModel;
                if (vm?.SearchCommand?.CanExecute(null) == true)
                {
                    vm.SearchCommand.Execute(null);
                }
            }
        }

        /// <summary>
        /// Handle Enter key in manual path box
        /// </summary>
        private void ManualPath_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var vm = DataContext as SdeSearchPaneViewModel;
                if (vm?.AddManualPathCommand?.CanExecute(null) == true)
                {
                    vm.AddManualPathCommand.Execute(null);
                }
            }
        }

        /// <summary>
        /// Handle click on a result item to show details
        /// </summary>
        private void ResultItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is SdeDatasetItem item)
            {
                var vm = DataContext as SdeSearchPaneViewModel;
                if (vm != null)
                {
                    // Show details for items that have fields or metadata to display
                    if (item.DatasetType == "Feature Class" || 
                        item.DatasetType == "Table" ||
                        item.DatasetType == "Feature Dataset")
                    {
                        _ = vm.LoadItemDetails(item);
                    }
                    else
                    {
                        vm.SelectedResult = item;
                    }
                }
            }
        }

        /// <summary>
        /// Handle Add to Map button — set selected item and stop event bubbling
        /// </summary>
        private void AddToMapButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is SdeDatasetItem item)
            {
                var vm = DataContext as SdeSearchPaneViewModel;
                if (vm != null)
                {
                    vm.SelectedResult = item;
                    if (vm.AddToMapCommand?.CanExecute(null) == true)
                        vm.AddToMapCommand.Execute(null);
                }
            }
            e.Handled = true;
        }
    }

    /// <summary>
    /// Converts a boolean to its inverse
    /// </summary>
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return value;
        }
    }

    /// <summary>
    /// Converts an inverted boolean to Visibility
    /// </summary>
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return b ? Visibility.Collapsed : Visibility.Visible;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility v) return v != Visibility.Visible;
            return false;
        }
    }

    /// <summary>
    /// Shows element only when string is non-empty
    /// </summary>
    public class NonEmptyStringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrWhiteSpace(value as string)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
