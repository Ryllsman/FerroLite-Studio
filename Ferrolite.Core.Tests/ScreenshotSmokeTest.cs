using System.Drawing;
using System.Drawing.Imaging;
using Ferrolite.Core.Lang;

using System.Linq;

namespace Ferrolite.Core.Tests;












public class ScreenshotSmokeTest
{
    [Fact]
    [Trait("Category", "Hardware")]
    public void Screenshot_SavesRealPng_WithNonZeroSize()
    {
        var backend = new WindowsBackend();
        var tmp = Path.Combine(Path.GetTempPath(), $"ferrolite_smoke_{Guid.NewGuid():N}.png");
        try
        {
            var saved = backend.Screenshot(tmp);
            Assert.Equal(tmp, saved);
            Assert.True(File.Exists(tmp), "Screenshot file was not created");
            var info = new FileInfo(tmp);
            Assert.True(info.Length > 0, "Screenshot file is empty");
            using var bmp = new Bitmap(tmp);
            Assert.True(bmp.Width > 0 && bmp.Height > 0, "Screenshot image has no size");
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    [Fact]
    [Trait("Category", "Hardware")]
    public void FindImageOnScreen_FindsTemplate_OnCapturedScreen()
    {
        var backend = new WindowsBackend();
        var tmpScreen = Path.Combine(Path.GetTempPath(), $"ferrolite_screen_{Guid.NewGuid():N}.png");
        var tmpTemplate = Path.Combine(Path.GetTempPath(), $"ferrolite_tmpl_{Guid.NewGuid():N}.png");
        try
        {
            
            backend.Screenshot(tmpScreen);
            Assert.True(File.Exists(tmpScreen));

            var (sw, sh) = backend.ScreenSize();
            Assert.True(sw > 0 && sh > 0);

            
            
            const int size = 40;
            using (var tmpl = new Bitmap(size, size))
            using (var g = Graphics.FromImage(tmpl))
            {
                g.Clear(Color.FromArgb(255, 40, 40, 200));
                g.FillRectangle(Brushes.White, 4, 4, 32, 32);
                tmpl.Save(tmpTemplate, ImageFormat.Png);
            }

            
            
            
            var result = backend.FindImageOnScreen(tmpTemplate, 0.8);
            Assert.Null(result); 
        }
        finally
        {
            if (File.Exists(tmpScreen)) File.Delete(tmpScreen);
            if (File.Exists(tmpTemplate)) File.Delete(tmpTemplate);
        }
    }

    [Fact]
    public void FindImageOnScreen_ReturnsNull_WhenTemplateMissing()
    {
        var backend = new WindowsBackend();
        Assert.Null(backend.FindImageOnScreen("does_not_exist.png", 0.9));
    }

    [Fact]
    [Trait("Category", "Hardware")]
    public void FindImageOnScreen_PositivelyMatches_CroppedRegionOnSameScreen()
    {
        var backend = new WindowsBackend();
        var tmpScreen = Path.Combine(Path.GetTempPath(), $"ferrolite_posscreen_{Guid.NewGuid():N}.png");
        var tmpTemplate = Path.Combine(Path.GetTempPath(), $"ferrolite_postemplate_{Guid.NewGuid():N}.png");
        try
        {
            
            backend.Screenshot(tmpScreen);
            Assert.True(File.Exists(tmpScreen));
            using var screen = new Bitmap(tmpScreen);
            Assert.True(screen.Width >= 200 && screen.Height >= 200,
                $"Desktop capture too small ({screen.Width}x{screen.Height}) to run the positive match test.");

            
            
            
            
            const int cx = 10, cy = 10, cw = 60, ch = 40;
            var crop = screen.Clone(new Rectangle(cx, cy, cw, ch), screen.PixelFormat);
            crop.Save(tmpTemplate, ImageFormat.Png);
            crop.Dispose();

            
            var result = backend.FindImageOnScreen(screen, tmpTemplate, 0.99);
            Assert.NotNull(result);
            var r = (Dictionary<string, object?>)result!;
            Assert.Equal(cx, GetInt32(r, "x"));
            Assert.Equal(cy, GetInt32(r, "y"));
            Assert.Equal(cw, GetInt32(r, "width"));
            Assert.Equal(ch, GetInt32(r, "height"));
        }
        finally
        {
            if (File.Exists(tmpScreen)) File.Delete(tmpScreen);
            if (File.Exists(tmpTemplate)) File.Delete(tmpTemplate);
        }
    }

    [Fact]
    public void FindImageOnScreen_ConfidenceThreshold_ReturnsNull_UnderTolerance()
    {
        var backend = new WindowsBackend();
        var tmpScreen = Path.Combine(Path.GetTempPath(), $"ferrolite_confscreen_{Guid.NewGuid():N}.png");
        var tmpTemplate = Path.Combine(Path.GetTempPath(), $"ferrolite_conftemplate_{Guid.NewGuid():N}.png");
        try
        {
            
            using (var bmp = new Bitmap(100, 100))
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(255, 120, 40, 200));
                bmp.Save(tmpScreen, ImageFormat.Png);
            }
            using (var bmp = new Bitmap(100, 100))
            using (var g = Graphics.FromImage(bmp))
            {
                
                g.Clear(Color.FromArgb(255, 40, 120, 200));
                bmp.Save(tmpTemplate, ImageFormat.Png);
            }

            
            
            var result = backend.FindImageOnScreen(tmpTemplate, 0.5);
            Assert.Null(result);
        }
        finally
        {
            if (File.Exists(tmpScreen)) File.Delete(tmpScreen);
            if (File.Exists(tmpTemplate)) File.Delete(tmpTemplate);
        }
    }

    private static int GetInt32(Dictionary<string, object?> d, string key) =>
        Convert.ToInt32(d[key]!);
}
