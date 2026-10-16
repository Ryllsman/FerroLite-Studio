using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ferrolite.UI.Controls;







public static class BrandMark
{
    public static FrameworkElement Create(double size)
    {
        var image = new Image
        {
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center,
        };
        try
        {
            var uri = new Uri("pack://application:,,,/Resources/ferrolite-mark.png", UriKind.Absolute);
            image.Source = BitmapFrame.Create(uri);
            return image;
        }
        catch
        {
            return new TextBlock
            {
                Text = "F",
                FontSize = size,
                FontWeight = FontWeights.Bold,
                FontStyle = FontStyles.Italic,
                Foreground = Palette.Brush(Palette.AccentOrange),
                VerticalAlignment = VerticalAlignment.Center,
            };
        }
    }
}
