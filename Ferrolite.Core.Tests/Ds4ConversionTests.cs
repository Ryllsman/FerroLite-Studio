using Ferrolite.Core.Lang;

namespace Ferrolite.Core.Tests;





public class Ds4ConversionTests
{
    [Fact]
    public void FaceButtons_Map_CrossCircleSquareTriangle()
    {
        VigemBus.XusbToDs4(0x1000, 0, 0, 0, 0, 0, 0, out var db, out _, out _, out _, out _, out _, out _, out _);
        Assert.Equal(0x0020, db & 0x0020); 

        VigemBus.XusbToDs4(0x2000, 0, 0, 0, 0, 0, 0, out db, out _, out _, out _, out _, out _, out _, out _);
        Assert.Equal(0x0040, db & 0x0040); 

        VigemBus.XusbToDs4(0x4000, 0, 0, 0, 0, 0, 0, out db, out _, out _, out _, out _, out _, out _, out _);
        Assert.Equal(0x0010, db & 0x0010); 

        VigemBus.XusbToDs4(0x8000, 0, 0, 0, 0, 0, 0, out db, out _, out _, out _, out _, out _, out _, out _);
        Assert.Equal(0x0080, db & 0x0080); 
    }

    [Fact]
    public void ShouldersAndSystemButtons_Map()
    {
        
        VigemBus.XusbToDs4(0x0100 | 0x0200 | 0x0020 | 0x0010 | 0x0040 | 0x0080 | 0x0400,
            0, 0, 0, 0, 0, 0, out var db, out var sp, out _, out _, out _, out _, out _, out _);
        Assert.Equal(0x0100, db & 0x0100);
        Assert.Equal(0x0200, db & 0x0200);
        Assert.Equal(0x1000, db & 0x1000);
        Assert.Equal(0x2000, db & 0x2000);
        Assert.Equal(0x4000, db & 0x4000);
        Assert.Equal(0x8000, db & 0x8000);
        Assert.Equal(0x01, sp);
    }

    [Fact]
    public void Dpad_Directions_TranslateBitsToHat()
    {
        
        var cases = new (ushort bits, byte hat)[]
        {
            (0x0001, 0), (0x0009, 1), (0x0008, 2), (0x000A, 3),
            (0x0002, 4), (0x0006, 5), (0x0004, 6), (0x0005, 7), (0x0000, 8),
        };
        foreach (var (bits, hat) in cases)
        {
            VigemBus.XusbToDs4(bits, 0, 0, 0, 0, 0, 0, out var db, out _, out _, out _, out _, out _, out _, out _);
            Assert.Equal(hat, db & 0xF);
        }
    }

    [Fact]
    public void Sticks_Center_And_YInverted()
    {
        
        VigemBus.XusbToDs4(0, 0, 0, 0, 0, 0, 0, out var db, out _, out _, out _, out var lx, out var ly, out var rx, out var ry);
        Assert.Equal((byte)128, lx);
        Assert.Equal((byte)128, ly);
        Assert.Equal((byte)128, rx);
        Assert.Equal((byte)128, ry);

        
        VigemBus.XusbToDs4(0, 0, 0, 0, short.MaxValue, 0, 0, out _, out _, out _, out _, out _, out ly, out _, out _);
        Assert.Equal((byte)0, ly);

        
        VigemBus.XusbToDs4(0, 0, 0, 0, short.MinValue, 0, 0, out _, out _, out _, out _, out _, out ly, out _, out _);
        Assert.Equal((byte)255, ly);

        
        VigemBus.XusbToDs4(0, 0, 0, short.MinValue, 0, 0, 0, out _, out _, out _, out _, out lx, out _, out _, out _);
        Assert.Equal((byte)0, lx);
        VigemBus.XusbToDs4(0, 0, 0, short.MaxValue, 0, 0, 0, out _, out _, out _, out _, out lx, out _, out _, out _);
        Assert.Equal((byte)255, lx);
    }

    [Fact]
    public void Triggers_PassThrough_And_SetDigitalBits()
    {
        VigemBus.XusbToDs4(0, 200, 0, 0, 0, 0, 0, out var db, out _, out var lt, out var rt, out _, out _, out _, out _);
        Assert.Equal(200, lt);
        Assert.Equal(0x0400, db & 0x0400); 
        Assert.Equal(0, rt);

        VigemBus.XusbToDs4(0, 0, 0, 0, 0, 0, 0, out db, out _, out lt, out rt, out _, out _, out _, out _);
        Assert.Equal(0, lt);
        Assert.Equal(0, db & 0x0400);      
    }
}
