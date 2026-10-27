param(
    [Parameter(Mandatory = $true)][long]$ParentHandle,
    [Parameter(Mandatory = $true)][int]$ScrcpyProcessId,
    [int]$LeftPaneWidth = 390
)

$ErrorActionPreference = "Stop"

try {
    $nativeCode = @"
using System;
using System.Runtime.InteropServices;

public static class SpectraWindowNative
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr SetParent(IntPtr child, IntPtr parent);

    [DllImport("user32.dll")]
    public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int command);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern IntPtr GetWindowLong32(IntPtr hWnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int index, IntPtr value);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern IntPtr SetWindowLong32(IntPtr hWnd, int index, IntPtr value);

    public static IntPtr GetWindowLongPtr(IntPtr hWnd, int index)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(hWnd, index)
            : GetWindowLong32(hWnd, index);
    }

    public static IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value)
    {
        return IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, index, value)
            : SetWindowLong32(hWnd, index, value);
    }

    public static IntPtr FindVisibleWindowForProcess(int wantedProcessId)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
        {
            uint processId;
            GetWindowThreadProcessId(hWnd, out processId);
            if (processId == (uint)wantedProcessId && IsWindowVisible(hWnd))
            {
                found = hWnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }
}
"@

    Add-Type -TypeDefinition $nativeCode

    $parent = [IntPtr]::new($ParentHandle)
    if (-not [SpectraWindowNative]::IsWindow($parent)) {
        exit 0
    }

    $child = [IntPtr]::Zero
    for ($attempt = 0; $attempt -lt 100 -and $child -eq [IntPtr]::Zero; $attempt++) {
        try {
            $null = Get-Process -Id $ScrcpyProcessId -ErrorAction Stop
        } catch {
            exit 0
        }

        $child = [SpectraWindowNative]::FindVisibleWindowForProcess($ScrcpyProcessId)
        if ($child -eq [IntPtr]::Zero) {
            Start-Sleep -Milliseconds 200
        }
    }

    if ($child -eq [IntPtr]::Zero) {
        exit 0
    }

    # Change the native scrcpy window into a borderless child of SPECTRA.
    $GWL_STYLE = -16
    $WS_CHILD = [long]0x40000000
    $WS_POPUP = [long]2147483648
    $WS_CAPTION = [long]0x00C00000
    $WS_THICKFRAME = [long]0x00040000
    $WS_MINIMIZEBOX = [long]0x00020000
    $WS_MAXIMIZEBOX = [long]0x00010000
    $WS_SYSMENU = [long]0x00080000
    $WS_VISIBLE = [long]0x10000000

    $style = [SpectraWindowNative]::GetWindowLongPtr($child, $GWL_STYLE).ToInt64()
    $removeFlags = $WS_POPUP -bor $WS_CAPTION -bor $WS_THICKFRAME -bor $WS_MINIMIZEBOX -bor $WS_MAXIMIZEBOX -bor $WS_SYSMENU
    $style = ($style -band (-bnot $removeFlags)) -bor $WS_CHILD -bor $WS_VISIBLE
    [void][SpectraWindowNative]::SetWindowLongPtr($child, $GWL_STYLE, [IntPtr]::new($style))
    [void][SpectraWindowNative]::SetParent($child, $parent)
    [void][SpectraWindowNative]::ShowWindow($child, 5)

    $SWP_NOZORDER = [uint32]0x0004
    $SWP_NOACTIVATE = [uint32]0x0010
    $SWP_FRAMECHANGED = [uint32]0x0020
    $SWP_SHOWWINDOW = [uint32]0x0040
    $positionFlags = $SWP_NOZORDER -bor $SWP_NOACTIVATE -bor $SWP_FRAMECHANGED -bor $SWP_SHOWWINDOW
    $process = Get-Process -Id $ScrcpyProcessId -ErrorAction Stop

    # Keep the native video surface aligned with the reserved right-hand pane.
    while ([SpectraWindowNative]::IsWindow($parent) -and [SpectraWindowNative]::IsWindow($child)) {
        $process.Refresh()
        if ($process.HasExited) { break }

        $rect = New-Object SpectraWindowNative+RECT
        if ([SpectraWindowNative]::GetClientRect($parent, [ref]$rect)) {
            $width = [Math]::Max(100, $rect.Right - $LeftPaneWidth)
            $height = [Math]::Max(100, $rect.Bottom)
            [void][SpectraWindowNative]::SetWindowPos(
                $child, [IntPtr]::Zero, $LeftPaneWidth, 0, $width, $height, $positionFlags)
        }
        Start-Sleep -Milliseconds 150
    }
} catch {
    # Best-effort embedding. If the platform window layout differs, scrcpy stays usable
    # as its own window and its failure does not stop SPECTRA itself.
    $logPath = Join-Path $env:TEMP "spectra-window-embed.log"
    "$(Get-Date -Format s) $($_.Exception.Message)" | Add-Content -Path $logPath
}