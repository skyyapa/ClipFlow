using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ClipFlow
{
    internal sealed class SettingsWindow : Window
    {
        private readonly CheckBox _startup;
        private readonly ComboBox _modifiers;
        private readonly TextBox _key;
        private readonly ComboBox _maximumItems;
        private readonly TextBox _retentionDays;
        private readonly TextBox _maximumMegabytes;

        internal AppSettings Result { get; private set; }

        internal SettingsWindow(AppSettings settings)
        {
            Title = "ClipFlow 设置";
            Width = 460;
            Height = 500;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            Background = new SolidColorBrush(Color.FromRgb(247, 247, 247));
            FontFamily = new FontFamily("Microsoft YaHei UI");
            UseLayoutRounding = true;

            Grid root = new Grid { Margin = new Thickness(24) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = root;

            TextBlock heading = new TextBlock
            {
                Text = "设置", FontSize = 22, FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.Black, Margin = new Thickness(0, 0, 0, 18)
            };
            root.Children.Add(heading);

            StackPanel form = new StackPanel();
            Grid.SetRow(form, 1);
            root.Children.Add(form);

            _startup = new CheckBox { Content = "开机自动启动 ClipFlow", IsChecked = settings.StartWithWindows, Margin = new Thickness(0, 0, 0, 18) };
            form.Children.Add(_startup);

            Grid hotkeyRow = CreateRow("呼出快捷键");
            StackPanel hotkeyControls = new StackPanel { Orientation = Orientation.Horizontal };
            _modifiers = new ComboBox { Width = 112, Height = 30, Margin = new Thickness(0, 0, 8, 0) };
            _modifiers.Items.Add("Ctrl+Shift"); _modifiers.Items.Add("Ctrl+Alt"); _modifiers.Items.Add("Alt+Shift");
            _modifiers.SelectedItem = settings.HotkeyModifiers;
            _key = new TextBox { Width = 46, Height = 30, MaxLength = 1, Text = settings.HotkeyKey, TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
            hotkeyControls.Children.Add(_modifiers); hotkeyControls.Children.Add(_key);
            Grid.SetColumn(hotkeyControls, 1); hotkeyRow.Children.Add(hotkeyControls); form.Children.Add(hotkeyRow);

            Grid countRow = CreateRow("最大历史条数");
            _maximumItems = new ComboBox { Width = 166, Height = 30 };
            foreach (int value in new[] { 50, 200, 500, 1000, 5000, 10000, 50000 }) _maximumItems.Items.Add(value);
            _maximumItems.SelectedItem = settings.MaximumItems;
            if (_maximumItems.SelectedIndex < 0) _maximumItems.SelectedItem = 5000;
            Grid.SetColumn(_maximumItems, 1); countRow.Children.Add(_maximumItems); form.Children.Add(countRow);

            Grid daysRow = CreateRow("图片保留天数");
            _retentionDays = CreateNumberBox(settings.ImageRetentionDays);
            Grid.SetColumn(_retentionDays, 1); daysRow.Children.Add(_retentionDays); form.Children.Add(daysRow);
            form.Children.Add(new TextBlock { Text = "填 0 表示不按天数清理；收藏图片不会删除。", FontSize = 11, Foreground = Brushes.Gray, Margin = new Thickness(152, -7, 0, 13) });

            Grid sizeRow = CreateRow("图片空间上限（MB）");
            _maximumMegabytes = CreateNumberBox(settings.ImageMaximumMegabytes);
            Grid.SetColumn(_maximumMegabytes, 1); sizeRow.Children.Add(_maximumMegabytes); form.Children.Add(sizeRow);

            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Button cancel = new Button { Content = "取消", Width = 76, Height = 32, Margin = new Thickness(0, 0, 8, 0) };
            Button save = new Button { Content = "保存", Width = 76, Height = 32, IsDefault = true };
            cancel.Click += delegate { DialogResult = false; };
            save.Click += SaveClicked;
            buttons.Children.Add(cancel); buttons.Children.Add(save);
            Grid.SetRow(buttons, 2); root.Children.Add(buttons);
        }

        private void SaveClicked(object sender, RoutedEventArgs eventArgs)
        {
            int days, megabytes;
            if (!int.TryParse(_retentionDays.Text, out days) || !int.TryParse(_maximumMegabytes.Text, out megabytes) || string.IsNullOrWhiteSpace(_key.Text))
            {
                MessageBox.Show("请填写有效的快捷键、保留天数和空间上限。", "ClipFlow", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            Result = new AppSettings
            {
                StartWithWindows = _startup.IsChecked == true,
                HotkeyModifiers = Convert.ToString(_modifiers.SelectedItem), HotkeyKey = _key.Text,
                MaximumItems = Convert.ToInt32(_maximumItems.SelectedItem),
                ImageRetentionDays = days, ImageMaximumMegabytes = megabytes
            };
            Result.Normalize();
            DialogResult = true;
        }

        private static Grid CreateRow(string label)
        {
            Grid row = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(152) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, FontSize = 13 });
            return row;
        }

        private static TextBox CreateNumberBox(int value)
        {
            return new TextBox { Width = 166, Height = 30, Text = value.ToString(), HorizontalAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(7, 0, 7, 0) };
        }
    }
}
