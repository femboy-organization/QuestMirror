using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using SPECTRA.Core.Streaming;

namespace SPECTRA.App.Platforms;

/// <summary>
/// Hosts the native scrcpy SDL window as a child of an Avalonia native control.
/// This avoids showing an independent scrcpy top-level window during streaming.
/// </summary>
public sealed class ScrcpyVideoHost : NativeControlHost, IVideoSurface
{
    private const uint WsChild = 0x40000000;
    private const uint WsClipSiblings = 0x04000000;
    private const uint WsClipChildren = 0x02000000;
    private const uint WsVisible = 0x10000000;
    private const uint WsPopup = 0x80000000;
    private const uint WsCaption = 0x00C00000;
    private const uint WsThickFrame = 0x00040000;
    private const uint WsSystemMenu = 0x00080000;
    private const uint WsMinimizeBox = 0x00020000;
    private const uint WsMaximizeBox = 0x00010000;
    private const int GwlStyle = -16;
    private const uint SwHide = 0;
    private const uint SwShow = 5;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    private IntPtr _hostHandle;
    private IntPtr _scrcpyHandle;

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Embedded scrcpy video is currently supported on Windows.");

        _hostHandle = CreateWindowExW(
            0, "STATIC", string.Empty,
            WsChild | WsClipSiblings | WsClipChildren | 0x00000004, // SS_BLACKRECT, initially hidden
            0, 0, 1, 1, parent.Handle, IntPtr.Zero, GetModuleHandleW(null), IntPtr.Zero);

        if (_hostHandle == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the native video surface.");

        ShowWindow(_hostHandle, SwHide);
        return new PlatformHandle(_hostHandle, "HWND");
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        Detach();
        var handle = control.Handle;
        if (handle != IntPtr.Zero && IsWindow(handle))
            DestroyWindow(handle);
        _hostHandle = IntPtr.Zero;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var result = base.ArrangeOverride(finalSize);
        ResizeHostedWindows(finalSize);
        return result;
    }

    public async Task AttachAsync(ScrcpySession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Embedded scrcpy video is currently supported on Windows.");
        if (_hostHandle == IntPtr.Zero || !IsWindow(_hostHandle))
            throw new InvalidOperationException("The video area is not ready. Wait for the main window to finish opening.");

        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        IntPtr window = IntPtr.Zero;

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (session.HasExited)
            {
                var details = session.RecentOutput;
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(details)
                        ? "scrcpy exited before its video window appeared. Verify the headset is authorized in ADB."
                        : $"scrcpy exited before its video window appeared: {details}");
            }

            window = FindProcessWindow(session.ProcessId, session.WindowTitle);
            if (window != IntPtr.Zero)
                break;

            await Task.Delay(100, cancellationToken);
        }

        if (window == IntPtr.Zero)
            throw new TimeoutException("scrcpy started, but its video window did not appear within 15 seconds.");

        var style = unchecked((uint)GetWindowLongPtrW(window, GwlStyle).ToInt64());
        style &= ~(WsPopup | WsCaption | WsThickFrame | WsSystemMenu | WsMinimizeBox | WsMaximizeBox);
        style |= WsChild | WsClipSiblings | WsClipChildren | WsVisible;
        SetWindowLongPtrW(window, GwlStyle, new IntPtr(unchecked((long)style)));

        SetParent(window, _hostHandle);
        _scrcpyHandle = window;

        ShowWindow(_hostHandle, SwShow);
        ShowWindow(_scrcpyHandle, SwShow);
        ResizeHostedWindows(Bounds.Size);
    }

    public void Detach()
    {
        if (!OperatingSystem.IsWindows())
            return;

        if (_scrcpyHandle != IntPtr.Zero && IsWindow(_scrcpyHandle))
            ShowWindow(_scrcpyHandle, SwHide);

        _scrcpyHandle = IntPtr.Zero;

        if (_hostHandle != IntPtr.Zero && IsWindow(_hostHandle))
            ShowWindow(_hostHandle, SwHide);
    }

    private void ResizeHostedWindows(Size size)
    {
        if (!OperatingSystem.IsWindows() || _hostHandle == IntPtr.Zero || !IsWindow(_hostHandle))
            return;

        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        var width = Math.Max(1, (int)Math.Round(size.Width * scale));
        var height = Math.Max(1, (int)Math.Round(size.Height * scale));

        SetWindowPos(_hostHandle, IntPtr.Zero, 0, 0, width, height,
            SwpNoZOrder | SwpNoActivate);

        if (_scrcpyHandle != IntPtr.Zero && IsWindow(_scrcpyHandle))
        {
            SetWindowPos(_scrcpyHandle, IntPtr.Zero, 0, 0, width, height,
                SwpNoZOrder | SwpNoActivate | SwpShowWindow);
        }
    }

    private static IntPtr FindProcessWindow(int processId, string expectedTitle)
    {
        var expected = IntPtr.Zero;
        EnumWindows((handle, _) =>
        {
            if (!IsWindowVisible(handle))
                return true;

            GetWindowThreadProcessId(handle, out var ownerPid);
            if (ownerPid != (uint)processId)
                return true;

            var length = GetWindowTextLengthW(handle);
            if (length <= 0)
                return true;

            var buffer = new System.Text.StringBuilder(length + 1);
            _ = GetWindowTextW(handle, buffer, buffer.Capacity);
            if (string.Equals(buffer.ToString(), expectedTitle, StringComparison.Ordinal))
            {
                expected = handle;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return expected;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(
        uint dwExStyle, string lpClassName, string? lpWindowName, uint dwStyle,
        int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu,
        IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtrW(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtrW(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, uint nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextW(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLengthW(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? lpModuleName);
}
