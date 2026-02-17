using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ProAppAddInSdeSearch
{
    public partial class SdeSearchPaneView : UserControl
    {
        public SdeSearchPaneView()
        {
            InitializeComponent();
            ApplyTheme(true); // default dark
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is INotifyPropertyChanged oldVm)
                oldVm.PropertyChanged -= Vm_PropertyChanged;
            if (e.NewValue is INotifyPropertyChanged newVm)
                newVm.PropertyChanged += Vm_PropertyChanged;

            // Apply initial theme from ViewModel
            if (e.NewValue is SdeSearchPaneViewModel vm)
                ApplyTheme(vm.IsDarkMode);
        }

        private void Vm_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SdeSearchPaneViewModel.IsDarkMode))
            {
                if (sender is SdeSearchPaneViewModel vm)
                    ApplyTheme(vm.IsDarkMode);
            }
        }

        // ═══════════════════════════════════════════
        //  THEME SWITCHING
        // ═══════════════════════════════════════════

        private void ApplyTheme(bool dark)
        {
            var res = this.Resources;

            if (dark)
            {
                res["PanelBg"]       = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
                res["FieldBg"]       = new SolidColorBrush(Color.FromRgb(0x24, 0x24, 0x24));
                res["BorderBrush"]   = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A));
                res["PrimaryText"]   = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
                res["SecondaryText"] = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA));
                res["Accent"]        = new SolidColorBrush(Color.FromRgb(0xF7, 0x93, 0x1A)); // Bitcoin Yellow
                res["AccentHover"]   = new SolidColorBrush(Color.FromRgb(0xE8, 0x85, 0x0F));
                res["AccentLight"]   = new SolidColorBrush(Color.FromRgb(0xFF, 0xB3, 0x47));
                res["AccentText"]    = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
            }
            else
            {
                res["PanelBg"]       = new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xFA));
                res["FieldBg"]       = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
                res["BorderBrush"]   = new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD0));
                res["PrimaryText"]   = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
                res["SecondaryText"] = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));
                res["Accent"]        = new SolidColorBrush(Color.FromRgb(0xD4, 0x7B, 0x0E)); // Darker gold for contrast
                res["AccentHover"]   = new SolidColorBrush(Color.FromRgb(0xBB, 0x6C, 0x0A));
                res["AccentLight"]   = new SolidColorBrush(Color.FromRgb(0xA0, 0x5E, 0x08));
                res["AccentText"]    = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            }
        }

        // ═══════════════════════════════════════════
        //  EVENT HANDLERS
        // ═══════════════════════════════════════════

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var vm = DataContext as SdeSearchPaneViewModel;
                if (vm?.SearchCommand?.CanExecute(null) == true)
                    vm.SearchCommand.Execute(null);
            }
        }

        private void ManualPath_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var vm = DataContext as SdeSearchPaneViewModel;
                if (vm?.AddManualPathCommand?.CanExecute(null) == true)
                    vm.AddManualPathCommand.Execute(null);
            }
        }

        private void ResultItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.DataContext is SdeDatasetItem item)
            {
                var vm = DataContext as SdeSearchPaneViewModel;
                if (vm != null && (item.DatasetType == "Feature Class" || item.DatasetType == "Table" || item.DatasetType == "Feature Dataset"))
                    _ = vm.LoadItemDetails(item);
            }
        }

        private void AddToMapButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement el && el.DataContext is SdeDatasetItem item)
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
}
