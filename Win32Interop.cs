using System.Runtime.InteropServices;

namespace PomodoroTimer;

/// <summary>
/// Win32 交互工具：把窗口强制顶到前台/所有普通窗口之上。
/// SetForegroundWindow 对后台进程有系统限制，用 AttachThreadInput 绕过；
/// 即使焦点夺取被拒，TOPMOST→NOTOPMOST 两次 SetWindowPos 也保证窗口抬到最前。
/// </summary>
internal static class Win32Interop
{
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const int SW_RESTORE = 9;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new(-2);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>
    /// 把窗口顶到所有普通窗口之上，并尽力夺取前台焦点。
    /// 用于主窗口：TOPMOST→NOTOPMOST 抬升兜底，即使焦点被系统拒绝也保证可见。
    /// </summary>
    internal static void ForceToForeground(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return;
        }
        try
        {
            if (IsIconic(hWnd))
            {
                _ = ShowWindow(hWnd, SW_RESTORE);
            }

            TryActivate(hWnd);

            _ = SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            _ = SetWindowPos(hWnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        }
        catch
        {
            // 静默失败：置顶卡片本身是 TopMost，可见性另有兜底
        }
    }

    /// <summary>
    /// 仅尝试夺取前台焦点，不改 Z 序、不动 TopMost 属性。
    /// 用于置顶提醒卡片：卡片已在最上层，只差焦点。
    /// </summary>
    internal static void TryActivate(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return;
        }
        try
        {
            IntPtr foreground = GetForegroundWindow();
            uint foregroundThread = foreground == IntPtr.Zero ? 0 : GetWindowThreadProcessId(foreground, out _);
            uint currentThread = GetCurrentThreadId();
            bool attached = foregroundThread != 0
                && foregroundThread != currentThread
                && AttachThreadInput(currentThread, foregroundThread, true);

            try
            {
                _ = BringWindowToTop(hWnd);
                _ = SetForegroundWindow(hWnd);
            }
            finally
            {
                if (attached)
                {
                    _ = AttachThreadInput(currentThread, foregroundThread, false);
                }
            }
        }
        catch
        {
        }
    }

    /// <summary>Win11 圆角窗口偏好。</summary>
    internal static void PreferRoundedCorners(IntPtr hWnd)
    {
        try
        {
            int preference = DWMWCP_ROUND;
            _ = DwmSetWindowAttribute(hWnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
        }
        catch
        {
        }
    }
}
