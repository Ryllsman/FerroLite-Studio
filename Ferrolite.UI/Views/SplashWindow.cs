using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ferrolite.UI.Views;


public class SplashWindow : Window
{
    public SplashWindow()
    {
        Width = 420;
        Height = 230;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = false;

        var border = new Border
        {
            Background = Palette.Brush(Palette.SidebarBg),
            CornerRadius = new CornerRadius(8),
            BorderBrush = Palette.Brush(Palette.BorderStrong),
            BorderThickness = new Thickness(1),
        };

        var grid = new Grid { Margin = new Thickness(28) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        
        var bolt = Controls.BrandMark.Create(72);
        bolt.HorizontalAlignment = HorizontalAlignment.Center;
        Grid.SetRow(bolt, 0);
        grid.Children.Add(bolt);

        var title = new TextBlock
        {
            Text = "FerroLite Studio",
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Foreground = Palette.Brush(Palette.AccentOrange),
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 8, 0, 2),
        };
        Grid.SetRow(title, 1);
        grid.Children.Add(title);

        var subtitle = new TextBlock
        {
            Text = "Macro Automation Studio",
            FontSize = 12,
            Foreground = Palette.Brush(Palette.FgMuted),
            TextAlignment = TextAlignment.Center,
        };
        Grid.SetRow(subtitle, 2);
        grid.Children.Add(subtitle);

        var bar = new ProgressBar
        {
            Height = 4,
            Maximum = 100,
            Foreground = Palette.Brush(Palette.AccentOrange),
            Background = Palette.Brush(Palette.InputBg),
            BorderThickness = new Thickness(0),
            IsIndeterminate = true,
        };
        Grid.SetRow(bar, 3);
        grid.Children.Add(bar);

        border.Child = grid;
        Content = border;

        
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2000) };
        timer.Tick += (_, _) => { timer.Stop(); Close(); };
        timer.Start();
    }
}
