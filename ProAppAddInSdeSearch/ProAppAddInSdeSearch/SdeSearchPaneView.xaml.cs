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
        /// Handle Enter key in search box to trigger search
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
        /// Handle click on a result item to show details
        /// </summary>
        private void ResultItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is SdeDatasetItem item)
            {
                var vm = DataContext as SdeSearchPaneViewModel;
                if (vm != null)
                {
                    // Only show details for feature classes and tables (items with fields)
                    if (item.DatasetType == "Feature Class" || item.DatasetType == "Table")
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
        /// Handle Add to Map button click — set the selected item first
        /// </summary>
        private void AddToMapButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is SdeDatasetItem item)
            {
                var vm = DataContext as SdeSearchPaneViewModel;
                if (vm != null)
                {
                    vm.SelectedResult = item;
                    // The AddToMapCommand will use SelectedResult
                }
            }
            // Stop the click from bubbling up to the ResultItem_Click handler
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
            if (value is bool b)
                return !b;
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;
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
            if (value is bool b)
                return b ? Visibility.Collapsed : Visibility.Visible;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility v)
                return v != Visibility.Visible;
            return false;
        }
    }
}
