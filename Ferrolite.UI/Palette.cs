using System.Windows.Media;

namespace Ferrolite.UI;





public static class Palette
{
    
    public static readonly Color Canvas = Hex("#161616");        
    public static readonly Color EditorBg = Hex("#1A1A1A");      
    public static readonly Color ToolbarBg = Hex("#1E1E1E");     
    public static readonly Color SidebarBg = Hex("#202020");     
    public static readonly Color PanelBg = Hex("#202020");
    public static readonly Color PanelHeaderBg = Hex("#252525"); 
    public static readonly Color TerminalBg = Hex("#141414");    
    public static readonly Color InputBg = Hex("#2A2A2A");
    public static readonly Color HoverBg = FromArgb(0x10, 0xFF, 0xFF, 0xFF); 
    public static readonly Color ActiveBg = Hex("#2E2E2E");
    public static readonly Color SelectionBg = Hex("#264F78");
    public static readonly Color LineHighlightBg = FromArgb(0x12, 0xFF, 0xFF, 0xFF); 

    
    public static readonly Color Border = Hex("#2E2E2E");        
    public static readonly Color BorderStrong = Hex("#3A3A3A");  
    public static readonly Color FocusBorder = Hex("#FF9142");
    public static readonly Color ScrollbarBg = Hex("#3A3A3A");

    
    public static readonly Color FgDefault = Hex("#D6D6D6");
    public static readonly Color FgMuted = Hex("#9B9B9B");
    public static readonly Color FgFaint = Hex("#6E6E6E");
    public static readonly Color FgBright = Hex("#F0F0F0");
    public static readonly Color FgLineNumber = Hex("#6E6E6E");
    public static readonly Color FgLineNumberActive = Hex("#E6E6E6");
    public static readonly Color StatusTextNormal = Hex("#D6D6D6");

    
    public static readonly Color Accent = Hex("#FF8C42");        
    public static readonly Color AccentOrange = Hex("#FF8C42");
    public static readonly Color AccentLight = Hex("#FFB27D");
    public static readonly Color AccentOrangeDim = Hex("#D9772E");
    public static readonly Color Success = Hex("#4EC9B0");
    public static readonly Color Warning = Hex("#CCA700");
    public static readonly Color Error = Hex("#E5484D");
    public static readonly Color Recording = Hex("#E5484D");
    public static readonly Color RunGreen = Hex("#1E9E6E");
    public static readonly Color RunGreenHover = Hex("#22B57E");
    public static readonly Color StopRed = Hex("#C0392B");
    public static readonly Color StopRedHover = Hex("#D0493A");
    public static readonly Color CloseHover = Hex("#E5484D");
    public static readonly Color ButtonSecondaryBg = Hex("#2E2E2E");

    
    public static readonly Color TitlebarBg = Hex("#1E1E1E");    
    public static readonly Color TitleBarRowBg = Hex("#1E1E1E"); 
    public static readonly Color StatusbarBg = Hex("#141414");
    public static readonly Color ActivitybarBg = Hex("#202020");
    public static readonly Color ScriptsTitleBg = Hex("#252525");
    public static readonly Color TabInactiveBg = Hex("#2E2E2E"); 
    public static readonly Color TabInactiveFg = Hex("#9B9B9B");

    
    public static readonly Color SynKeyword = Hex("#569CD6");
    public static readonly Color SynString = Hex("#CE9178");
    public static readonly Color SynNumber = Hex("#B5CEA8");
    public static readonly Color SynComment = Hex("#6A9955");
    public static readonly Color SynFunction = Hex("#DCDCAA");
    public static readonly Color SynBuiltin = Hex("#4EC9B0");
    public static readonly Color SynOperator = Hex("#D4D4D4");
    public static readonly Color SynVariable = Hex("#9CDCFE");

    public static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    public static Color FromArgb(byte a, byte r, byte g, byte b) => Color.FromArgb(a, r, g, b);

    public static Brush Brush(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    public static readonly FontFamily UiFont = new("Segoe UI");
    public static readonly FontFamily CodeFont = new("Cascadia Mono, Consolas");
}
