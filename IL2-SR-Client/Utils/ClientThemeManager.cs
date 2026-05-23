using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Win32;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Utils
{
    public static class ClientThemeManager
    {
        public const string LightTheme = "Light";
        public const string DarkTheme = "Dark";
        public const string SystemTheme = "System";

        private const string BaseThemePrefix = "pack://application:,,,/MahApps.Metro;component/Styles/Accents/Base";
        private const string WindowsPersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const string WindowsAppsUseLightThemeValue = "AppsUseLightTheme";
        private static string _currentTheme = LightTheme;

        public static string NormalizeTheme(string theme)
        {
            if (string.Equals(theme, DarkTheme, StringComparison.OrdinalIgnoreCase))
            {
                return DarkTheme;
            }

            if (string.Equals(theme, SystemTheme, StringComparison.OrdinalIgnoreCase))
            {
                return SystemTheme;
            }

            return LightTheme;
        }

        public static bool IsDarkTheme(string theme)
        {
            return string.Equals(ResolveTheme(theme), DarkTheme, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSystemTheme(string theme)
        {
            return string.Equals(NormalizeTheme(theme), SystemTheme, StringComparison.OrdinalIgnoreCase);
        }

        public static string ResolveTheme(string theme)
        {
            var normalizedTheme = NormalizeTheme(theme);
            if (!string.Equals(normalizedTheme, SystemTheme, StringComparison.OrdinalIgnoreCase))
            {
                return normalizedTheme;
            }

            return GetWindowsAppTheme();
        }

        public static void ApplyTheme(string theme)
        {
            var normalizedTheme = NormalizeTheme(theme);
            var resolvedTheme = ResolveTheme(normalizedTheme);
            _currentTheme = resolvedTheme;

            var resources = Application.Current?.Resources;
            if (resources == null)
            {
                return;
            }

            var baseThemeDictionary = resources.MergedDictionaries
                .FirstOrDefault(dictionary =>
                    dictionary.Source != null &&
                    dictionary.Source.OriginalString.IndexOf("/Styles/Accents/Base", StringComparison.OrdinalIgnoreCase) >= 0);

            var themeSource = new Uri($"{BaseThemePrefix}{resolvedTheme}.xaml", UriKind.Absolute);
            if (baseThemeDictionary == null)
            {
                resources.MergedDictionaries.Add(new ResourceDictionary { Source = themeSource });
                return;
            }

            if (!string.Equals(baseThemeDictionary.Source.OriginalString, themeSource.OriginalString, StringComparison.OrdinalIgnoreCase))
            {
                baseThemeDictionary.Source = themeSource;
            }

            foreach (Window window in Application.Current.Windows)
            {
                ApplyThemeToWindow(window, resolvedTheme);
            }
        }

        public static void ApplyThemeToWindow(Window window, string theme)
        {
            if (window == null || !string.Equals(window.GetType().Name, "MainWindow", StringComparison.Ordinal))
            {
                return;
            }

            _currentTheme = ResolveTheme(theme);
            ApplyElementTheme(window, IsDarkTheme(_currentTheme) ? ThemePalette.Dark : ThemePalette.Light);
        }

        private static string GetWindowsAppTheme()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(WindowsPersonalizeKey))
                {
                    var value = key?.GetValue(WindowsAppsUseLightThemeValue);
                    if (value is int intValue)
                    {
                        return intValue == 0 ? DarkTheme : LightTheme;
                    }
                }
            }
            catch
            {
                // Fall back to light if Windows theme detection is unavailable.
            }

            return LightTheme;
        }

        private static void ApplyElementTheme(DependencyObject element, ThemePalette palette, ToggleButton parentToggleButton = null)
        {
            var currentToggleButton = element is ToggleButton toggleElement && !(element is RadioButton)
                ? toggleElement
                : parentToggleButton;

            if (element is Window window)
            {
                window.Background = palette.WindowBackground;
                window.Foreground = palette.Foreground;
            }

            if (element is GroupBox groupBox)
            {
                groupBox.Background = palette.PanelBackground;
                groupBox.Foreground = palette.Foreground;
                groupBox.BorderBrush = palette.Border;
            }

            if (element is HeaderedContentControl headeredContentControl)
            {
                headeredContentControl.Foreground = palette.Foreground;
            }

            if (element is TextBlock textBlock)
            {
                textBlock.Foreground = currentToggleButton?.IsChecked == true ? palette.ToggleOnForeground : palette.Foreground;
            }

            if (element is Label label)
            {
                label.Foreground = palette.Foreground;
                label.Background = Brushes.Transparent;
            }

            if (element is RadioButton radioButton)
            {
                radioButton.Foreground = palette.Foreground;
            }

            if (element is CheckBox checkBox)
            {
                checkBox.Foreground = palette.Foreground;
            }

            if (element is ToggleButton toggleButton && !(element is RadioButton))
            {
                ApplyToggleButtonTheme(toggleButton, palette);
                toggleButton.Checked -= ToggleButtonStateChanged;
                toggleButton.Unchecked -= ToggleButtonStateChanged;
                toggleButton.Checked += ToggleButtonStateChanged;
                toggleButton.Unchecked += ToggleButtonStateChanged;
            }
            else if (element is Button button)
            {
                ApplyButtonTheme(button, palette);
                button.IsEnabledChanged -= ButtonIsEnabledChanged;
                button.IsEnabledChanged += ButtonIsEnabledChanged;
                button.MouseEnter -= ButtonMouseEnter;
                button.MouseLeave -= ButtonMouseLeave;
                button.MouseEnter += ButtonMouseEnter;
                button.MouseLeave += ButtonMouseLeave;
            }

            if (element is ComboBox comboBox)
            {
                comboBox.Background = palette.InputBackground;
                comboBox.Foreground = palette.Foreground;
                comboBox.BorderBrush = palette.Border;
            }

            if (element is TextBox textBox)
            {
                textBox.Background = palette.InputBackground;
                textBox.Foreground = palette.Foreground;
                textBox.BorderBrush = palette.Border;
            }

            if (element is TabControl tabControl)
            {
                tabControl.Background = palette.WindowBackground;
                tabControl.Foreground = palette.Foreground;
                tabControl.BorderBrush = palette.Border;
            }

            if (element is TabItem tabItem)
            {
                tabItem.Background = palette.TabBackground;
                tabItem.Foreground = palette.Foreground;
                tabItem.BorderBrush = palette.Border;
            }

            if (element is ScrollViewer scrollViewer)
            {
                scrollViewer.Background = palette.WindowBackground;
                scrollViewer.Foreground = palette.Foreground;
            }

            if (element is DataGrid dataGrid)
            {
                ApplyDataGridTheme(dataGrid, palette);
                dataGrid.LoadingRow -= DataGridLoadingRow;
                dataGrid.LoadingRow += DataGridLoadingRow;
            }

            var childCount = VisualTreeHelper.GetChildrenCount(element);
            for (var i = 0; i < childCount; i++)
            {
                ApplyElementTheme(VisualTreeHelper.GetChild(element, i), palette, currentToggleButton);
            }
        }

        private static void DataGridLoadingRow(object sender, DataGridRowEventArgs e)
        {
            var palette = IsDarkTheme(_currentTheme) ? ThemePalette.Dark : ThemePalette.Light;
            ApplyDataGridRowTheme(e.Row, palette);

            e.Row.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (e.Row.Dispatcher.HasShutdownStarted || e.Row.Dispatcher.HasShutdownFinished)
                {
                    return;
                }

                ApplyElementTheme(e.Row, palette);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private static void ToggleButtonStateChanged(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggleButton)
            {
                var palette = IsDarkTheme(_currentTheme) ? ThemePalette.Dark : ThemePalette.Light;
                ApplyToggleButtonTheme(toggleButton, palette);

                var childCount = VisualTreeHelper.GetChildrenCount(toggleButton);
                for (var i = 0; i < childCount; i++)
                {
                    ApplyElementTheme(VisualTreeHelper.GetChild(toggleButton, i), palette, toggleButton);
                }
            }
        }

        private static void ButtonIsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is Button button)
            {
                var palette = IsDarkTheme(_currentTheme) ? ThemePalette.Dark : ThemePalette.Light;
                ApplyButtonTheme(button, palette);
            }
        }

        private static void ButtonMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is Button button && button.IsEnabled)
            {
                var palette = IsDarkTheme(_currentTheme) ? ThemePalette.Dark : ThemePalette.Light;
                ApplyButtonTheme(button, palette, true);
            }
        }

        private static void ButtonMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is Button button)
            {
                var palette = IsDarkTheme(_currentTheme) ? ThemePalette.Dark : ThemePalette.Light;
                ApplyButtonTheme(button, palette);
            }
        }

        private static void ApplyDataGridTheme(DataGrid dataGrid, ThemePalette palette)
        {
            dataGrid.Background = palette.PanelBackground;
            dataGrid.Foreground = palette.Foreground;
            dataGrid.BorderBrush = palette.Border;
            dataGrid.HorizontalGridLinesBrush = palette.Border;
            dataGrid.VerticalGridLinesBrush = palette.Border;
            dataGrid.RowBackground = palette.InputBackground;
            dataGrid.AlternatingRowBackground = palette.InputBackground;

            dataGrid.ColumnHeaderStyle = CreateDataGridColumnHeaderStyle(palette);
            dataGrid.RowStyle = CreateDataGridRowStyle(palette);
            dataGrid.CellStyle = CreateDataGridCellStyle(palette);

            foreach (var column in dataGrid.Columns.OfType<DataGridTextColumn>())
            {
                column.ElementStyle = CreateDataGridTextBlockStyle(palette);
                column.EditingElementStyle = CreateDataGridTextBoxStyle(palette);
            }
        }

        private static void ApplyDataGridRowTheme(DataGridRow row, ThemePalette palette)
        {
            row.Background = palette.InputBackground;
            row.Foreground = palette.Foreground;
            row.BorderBrush = palette.Border;
        }

        private static Style CreateDataGridColumnHeaderStyle(ThemePalette palette)
        {
            var style = new Style(typeof(DataGridColumnHeader));
            style.Setters.Add(new Setter(Control.BackgroundProperty, palette.InputBackground));
            style.Setters.Add(new Setter(Control.ForegroundProperty, palette.Foreground));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, palette.Border));
            style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
            style.Setters.Add(new Setter(TextElement.ForegroundProperty, palette.Foreground));
            return style;
        }

        private static Style CreateDataGridRowStyle(ThemePalette palette)
        {
            var style = new Style(typeof(DataGridRow));
            style.Setters.Add(new Setter(Control.BackgroundProperty, palette.InputBackground));
            style.Setters.Add(new Setter(Control.ForegroundProperty, palette.Foreground));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, palette.Border));
            style.Setters.Add(new Setter(TextElement.ForegroundProperty, palette.Foreground));

            var selectedTrigger = new Trigger { Property = DataGridRow.IsSelectedProperty, Value = true };
            selectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, palette.ToggleOnBackground));
            selectedTrigger.Setters.Add(new Setter(Control.ForegroundProperty, palette.Foreground));
            style.Triggers.Add(selectedTrigger);

            return style;
        }

        private static Style CreateDataGridCellStyle(ThemePalette palette)
        {
            var style = new Style(typeof(DataGridCell));
            style.Setters.Add(new Setter(Control.BackgroundProperty, palette.InputBackground));
            style.Setters.Add(new Setter(Control.ForegroundProperty, palette.Foreground));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, palette.Border));
            style.Setters.Add(new Setter(TextElement.ForegroundProperty, palette.Foreground));

            var selectedTrigger = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
            selectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, palette.ToggleOnBackground));
            selectedTrigger.Setters.Add(new Setter(Control.ForegroundProperty, palette.Foreground));
            style.Triggers.Add(selectedTrigger);

            return style;
        }

        private static Style CreateDataGridTextBlockStyle(ThemePalette palette)
        {
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(TextBlock.ForegroundProperty, palette.Foreground));
            style.Setters.Add(new Setter(TextElement.ForegroundProperty, palette.Foreground));
            style.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
            return style;
        }

        private static Style CreateDataGridTextBoxStyle(ThemePalette palette)
        {
            var style = new Style(typeof(TextBox));
            style.Setters.Add(new Setter(Control.BackgroundProperty, palette.InputBackground));
            style.Setters.Add(new Setter(Control.ForegroundProperty, palette.Foreground));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, palette.Border));
            style.Setters.Add(new Setter(TextElement.ForegroundProperty, palette.Foreground));
            return style;
        }

        private static void ApplyButtonTheme(Button button, ThemePalette palette, bool isMouseOver = false)
        {
            var foreground = button.IsEnabled ? palette.Foreground : palette.DisabledButtonForeground;
            var background = button.IsEnabled ? palette.ButtonBackground : palette.DisabledButtonBackground;
            if (button.IsEnabled && isMouseOver)
            {
                foreground = palette.ButtonHoverForeground;
                background = palette.ButtonHoverBackground;
            }

            button.Background = background;
            button.Foreground = foreground;
            button.SetValue(TextElement.ForegroundProperty, foreground);
            button.BorderBrush = button.IsEnabled ? palette.Border : palette.DisabledButtonBorder;
            button.Opacity = 1.0;

            button.Resources[SystemColors.GrayTextBrushKey] = foreground;
            button.Resources[SystemColors.ControlTextBrushKey] = foreground;
            button.Resources[SystemColors.ControlBrushKey] = background;
            ApplyButtonContentForeground(button, foreground);

            button.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (button.Dispatcher.HasShutdownStarted || button.Dispatcher.HasShutdownFinished)
                {
                    return;
                }

                button.ApplyTemplate();
                ApplyButtonContentForeground(button, foreground);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private static void ApplyButtonContentForeground(DependencyObject element, Brush foreground)
        {
            if (element is TextBlock textBlock)
            {
                textBlock.Foreground = foreground;
            }
            else if (element is AccessText accessText)
            {
                accessText.Foreground = foreground;
            }
            else if (element is Control control && !(element is ButtonBase))
            {
                control.Foreground = foreground;
            }

            element.SetValue(TextElement.ForegroundProperty, foreground);

            var childCount = VisualTreeHelper.GetChildrenCount(element);
            for (var i = 0; i < childCount; i++)
            {
                ApplyButtonContentForeground(VisualTreeHelper.GetChild(element, i), foreground);
            }
        }

        private static void ApplyToggleButtonTheme(ToggleButton toggleButton, ThemePalette palette)
        {
            var foreground = toggleButton.IsChecked == true ? palette.ToggleOnForeground : palette.Foreground;
            toggleButton.Foreground = foreground;
            toggleButton.SetValue(TextElement.ForegroundProperty, foreground);
            toggleButton.BorderBrush = palette.Border;
            toggleButton.Background = toggleButton.IsChecked == true ? palette.ToggleOnBackground : palette.ToggleOffBackground;
        }

        private sealed class ThemePalette
        {
            public static readonly ThemePalette Light = new ThemePalette(
                Brushes.White,
                Brushes.White,
                Brushes.Black,
                new SolidColorBrush(Color.FromRgb(230, 230, 230)),
                Brushes.White,
                new SolidColorBrush(Color.FromRgb(190, 190, 190)),
                new SolidColorBrush(Color.FromRgb(185, 220, 238)),
                Brushes.Black,
                new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                Brushes.White,
                Brushes.Black,
                new SolidColorBrush(Color.FromRgb(170, 170, 170)),
                new SolidColorBrush(Color.FromRgb(27, 27, 27)),
                new SolidColorBrush(Color.FromRgb(167, 173, 179)),
                new SolidColorBrush(Color.FromRgb(150, 150, 150)));

            public static readonly ThemePalette Dark = new ThemePalette(
                new SolidColorBrush(Color.FromRgb(37, 41, 45)),
                new SolidColorBrush(Color.FromRgb(45, 50, 55)),
                new SolidColorBrush(Color.FromRgb(242, 242, 242)),
                new SolidColorBrush(Color.FromRgb(57, 63, 70)),
                new SolidColorBrush(Color.FromRgb(43, 48, 54)),
                new SolidColorBrush(Color.FromRgb(100, 110, 120)),
                new SolidColorBrush(Color.FromRgb(37, 94, 125)),
                Brushes.Black,
                new SolidColorBrush(Color.FromRgb(60, 66, 73)),
                new SolidColorBrush(Color.FromRgb(45, 50, 55)),
                Brushes.Black,
                new SolidColorBrush(Color.FromRgb(170, 170, 170)),
                new SolidColorBrush(Color.FromRgb(27, 27, 27)),
                new SolidColorBrush(Color.FromRgb(167, 173, 179)),
                new SolidColorBrush(Color.FromRgb(150, 158, 166)));

            private ThemePalette(
                Brush windowBackground,
                Brush panelBackground,
                Brush foreground,
                Brush buttonBackground,
                Brush inputBackground,
                Brush border,
                Brush toggleOnBackground,
                Brush toggleOnForeground,
                Brush toggleOffBackground,
                Brush tabBackground,
                Brush buttonHoverForeground,
                Brush buttonHoverBackground,
                Brush disabledButtonForeground,
                Brush disabledButtonBackground,
                Brush disabledButtonBorder)
            {
                WindowBackground = windowBackground;
                PanelBackground = panelBackground;
                Foreground = foreground;
                ButtonBackground = buttonBackground;
                InputBackground = inputBackground;
                Border = border;
                ToggleOnBackground = toggleOnBackground;
                ToggleOnForeground = toggleOnForeground;
                ToggleOffBackground = toggleOffBackground;
                TabBackground = tabBackground;
                ButtonHoverForeground = buttonHoverForeground;
                ButtonHoverBackground = buttonHoverBackground;
                DisabledButtonForeground = disabledButtonForeground;
                DisabledButtonBackground = disabledButtonBackground;
                DisabledButtonBorder = disabledButtonBorder;
            }

            public Brush WindowBackground { get; }
            public Brush PanelBackground { get; }
            public Brush Foreground { get; }
            public Brush ButtonBackground { get; }
            public Brush InputBackground { get; }
            public Brush Border { get; }
            public Brush ToggleOnBackground { get; }
            public Brush ToggleOnForeground { get; }
            public Brush ToggleOffBackground { get; }
            public Brush TabBackground { get; }
            public Brush ButtonHoverForeground { get; }
            public Brush ButtonHoverBackground { get; }
            public Brush DisabledButtonForeground { get; }
            public Brush DisabledButtonBackground { get; }
            public Brush DisabledButtonBorder { get; }
        }
    }
}
