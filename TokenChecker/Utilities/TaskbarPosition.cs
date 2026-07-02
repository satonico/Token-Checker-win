using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using System.Windows.Forms;

namespace TokenChecker.Utilities;

/// <summary>
/// タスクバーと通知領域の位置・サイズを Windows API から取得する。
/// </summary>
public static class TaskbarPosition
{
    [DllImport("user32.dll")] private static extern IntPtr FindWindow(string cls, string? win);
    [DllImport("user32.dll")] private static extern IntPtr FindWindowEx(IntPtr p, IntPtr a, string cls, string? win);
    [DllImport("user32.dll")] private static extern bool   EnumWindows(EnumWindowsProc proc, IntPtr parameter);
    [DllImport("user32.dll")] private static extern int    GetClassName(IntPtr hwnd, StringBuilder name, int count);
    [DllImport("user32.dll")] private static extern bool   GetWindowRect(IntPtr hwnd, out RECT r);

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr parameter);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    public record Info(
        double TaskbarTop,    double TaskbarLeft,
        double TaskbarBottom, double TaskbarRight,
        double TaskbarHeight,
        double NotifyLeft,     // 通知領域の左端 X (WPF 論理ピクセル)
        double? WidgetsRight,  // 天気/ウィジェット ボタンの右端 X
        double? ContentLeft,   // 中央側タスクバー項目の左端 X
        double? ContentRight); // 中央側タスクバー項目の右端 X

    // AutomationElement.FindAll() は COM ラッパーを大量生成するため、タスクバー RECT が
    // 変化していない間はキャッシュを返してフィナライザ負荷を抑える。
    private static readonly Dictionary<int, ((int L, int T, int R, int B), Info)> _cache = new();

    public static Info? Get(int screenIndex = 0)
    {
        var screens = Screen.AllScreens;
        var target = screenIndex < screens.Length ? screens[screenIndex] : screens[0];
        var taskbar = FindTaskbar(target.Bounds);
        if (taskbar == IntPtr.Zero) return null;

        if (!GetWindowRect(taskbar, out var tb)) return null;

        // タスクバーが自動非表示のとき幅か高さが 4px 以下になる。
        // この場合は null を返して PositionAtScreenEdge フォールバックを使わせる。
        if (tb.Bottom - tb.Top <= 4 || tb.Right - tb.Left <= 4) return null;

        // タスクバーの位置・サイズが前回と同じなら AutomationElement クエリをスキップする。
        var tbKey = (tb.Left, tb.Top, tb.Right, tb.Bottom);
        if (_cache.TryGetValue(screenIndex, out var cached) && cached.Item1 == tbKey)
            return cached.Item2;

        // 通知領域 (TrayNotifyWnd) の左端を取得 → ウィジェットをその左に置く
        var notify = FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
        int notifyLeft = tb.Right;                 // 見つからない場合は右端で代用
        if (notify != IntPtr.Zero && GetWindowRect(notify, out var nr))
            notifyLeft = nr.Left;

        using var g = Graphics.FromHwnd(taskbar);
        var dpi = g.DpiX / 96.0;
        var widgetsRight = TryGetLeftWidgetsRight(taskbar, tb, dpi);
        var rightAnchorLeft = notify != IntPtr.Zero
            ? notifyLeft / dpi
            : TryGetClockLeft(taskbar, tb, dpi) ?? tb.Right / dpi;
        var contentBounds = TryGetContentBounds(taskbar, tb, dpi, widgetsRight, rightAnchorLeft);

        var info = new Info(
            TaskbarTop:    tb.Top    / dpi,
            TaskbarLeft:   tb.Left   / dpi,
            TaskbarBottom: tb.Bottom / dpi,
            TaskbarRight:  tb.Right  / dpi,
            TaskbarHeight: (tb.Bottom - tb.Top) / dpi,
            NotifyLeft:    rightAnchorLeft,
            WidgetsRight:  widgetsRight,
            ContentLeft:   contentBounds.Left,
            ContentRight:  contentBounds.Right);
        _cache[screenIndex] = (tbKey, info);
        return info;
    }

    private static IntPtr FindTaskbar(Rectangle targetBounds)
    {
        var match = IntPtr.Zero;
        EnumWindows((hwnd, _) =>
        {
            var className = new StringBuilder(64);
            GetClassName(hwnd, className, className.Capacity);
            if (className.ToString() is not ("Shell_TrayWnd" or "Shell_SecondaryTrayWnd") ||
                !GetWindowRect(hwnd, out var rect))
                return true;

            var bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
            if (!bounds.IntersectsWith(targetBounds)) return true;

            match = hwnd;
            return false;
        }, IntPtr.Zero);
        return match;
    }

    private static double? TryGetLeftWidgetsRight(IntPtr taskbar, RECT taskbarRect, double dpi)
    {
        try
        {
            var root = AutomationElement.FromHandle(taskbar);
            var buttons = root.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));

            double? right = null;
            foreach (AutomationElement button in buttons)
            {
                var rect = button.Current.BoundingRectangle;
                if (rect.IsEmpty ||
                    rect.Left > taskbarRect.Left + 250 ||
                    rect.Top < taskbarRect.Top ||
                    rect.Bottom > taskbarRect.Bottom)
                    continue;

                right = right is null ? rect.Right / dpi : Math.Max(right.Value, rect.Right / dpi);
            }
            return right;
        }
        catch { return null; }
    }

    private static double? TryGetClockLeft(IntPtr taskbar, RECT taskbarRect, double dpi)
    {
        try
        {
            var root = AutomationElement.FromHandle(taskbar);
            var buttons = root.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));

            foreach (AutomationElement button in buttons)
            {
                var name = button.Current.Name;
                var rect = button.Current.BoundingRectangle;
                if (name.StartsWith("時計", StringComparison.Ordinal) &&
                    !rect.IsEmpty &&
                    rect.Top >= taskbarRect.Top &&
                    rect.Bottom <= taskbarRect.Bottom)
                    return rect.Left / dpi;
            }
        }
        catch { }
        return null;
    }

    private static (double? Left, double? Right) TryGetContentBounds(
        IntPtr taskbar, RECT taskbarRect, double dpi, double? widgetsRight, double rightAnchorLeft)
    {
        try
        {
            var root = AutomationElement.FromHandle(taskbar);
            var buttons = root.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));

            var afterWidgets = (widgetsRight ?? taskbarRect.Left / dpi) + 1;
            double? left = null;
            double? right = null;
            foreach (AutomationElement button in buttons)
            {
                var rect = button.Current.BoundingRectangle;
                var rectLeft = rect.Left / dpi;
                var rectRight = rect.Right / dpi;
                if (rect.IsEmpty ||
                    rect.Top < taskbarRect.Top ||
                    rect.Bottom > taskbarRect.Bottom ||
                    rectLeft <= afterWidgets ||
                    rectRight >= rightAnchorLeft)
                    continue;

                left = left is null ? rectLeft : Math.Min(left.Value, rectLeft);
                right = right is null ? rectRight : Math.Max(right.Value, rectRight);
            }
            return (left, right);
        }
        catch { return (null, null); }
    }
}
