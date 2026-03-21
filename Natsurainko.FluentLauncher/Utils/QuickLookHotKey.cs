using FluentLauncher.Infra.UI.Windows;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Natsurainko.FluentLauncher.Utils;

internal static class QuickLookHotKey
{
    private const int HotKeyId = 1;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkO = 0x4F;
    private const uint WmHotKey = 0x0312;
    private const uint WmDestroy = 0x0002;
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;

    private static readonly object _gate = new();
    private static Thread? _thread;
    private static IntPtr _hwnd;
    private static WndProc? _wndProc;
    private static int _threadId;
    private static string _className = "FluentLauncher.QuickLookHotKey";

    private static IntPtr _hookHandle;
    private static LowLevelKeyboardProc? _hookProc;
    private static bool _oDown;

    public static void Register()
    {
        lock (_gate)
        {
            if (_thread is not null)
                return;

            _thread = new Thread(MessageLoop)
            {
                IsBackground = true,
                Name = "QuickLookHotKey"
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }
    }

    public static void Unregister()
    {
        lock (_gate)
        {
            if (_threadId != 0)
            {
                PostThreadMessage((uint)_threadId, 0x0012 /* WM_QUIT */, IntPtr.Zero, IntPtr.Zero);
            }

            _thread = null;
            _threadId = 0;
        }
    }

    private static void MessageLoop()
    {
        _threadId = GetCurrentThreadId();

        _wndProc = WndProcImpl;

        IntPtr hInstance = GetModuleHandle(null);
        var wc = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = hInstance,
            lpszClassName = _className
        };

        _ = RegisterClassEx(ref wc);

        // Message-only window: receives WM_HOTKEY without showing anything.
        _hwnd = CreateWindowEx(
            0,
            _className,
            "",
            0,
            0, 0, 0, 0,
            new IntPtr(-3), // HWND_MESSAGE
            IntPtr.Zero,
            hInstance,
            IntPtr.Zero);

        bool hotKeyRegistered = false;
        if (_hwnd != IntPtr.Zero)
        {
            hotKeyRegistered = RegisterHotKey(_hwnd, HotKeyId, ModWin | ModNoRepeat, VkO);
        }

        // Win+O is commonly reserved by Windows (orientation lock). If RegisterHotKey fails,
        // fall back to a low-level keyboard hook to intercept the combo.
        if (!hotKeyRegistered)
        {
            _hookProc = HookProcImpl;
            _hookHandle = SetWindowsHookEx(WhKeyboardLl, _hookProc, hInstance, 0);
        }

        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) != 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        if (_hookHandle != IntPtr.Zero)
        {
            _ = UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
            _hookProc = null;
        }

        if (_hwnd != IntPtr.Zero)
        {
            _ = UnregisterHotKey(_hwnd, HotKeyId);
            _ = DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }
    }

    private static IntPtr WndProcImpl(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WmHotKey && wParam == (IntPtr)HotKeyId)
        {
            try
            {
                App.DispatcherQueue.TryEnqueue(() =>
                    App.GetService<IActivationService>().ActivateWindow("QuickLookWindow"));
            }
            catch
            {
                // Ignore activation failures.
            }

            return IntPtr.Zero;
        }

        if (msg == WmDestroy)
            PostQuitMessage(0);

        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private static IntPtr HookProcImpl(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0 && (wParam == (IntPtr)WmKeyDown || wParam == (IntPtr)WmSysKeyDown))
            {
                var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                if (info.vkCode == VkO)
                {
                    bool winDown =
                        (GetAsyncKeyState(VkLWin) & 0x8000) != 0 ||
                        (GetAsyncKeyState(VkRWin) & 0x8000) != 0;

                    // Avoid auto-repeat.
                    if (winDown && !_oDown)
                    {
                        _oDown = true;
                        App.DispatcherQueue.TryEnqueue(() =>
                            App.GetService<IActivationService>().ActivateWindow("QuickLookWindow"));
                        return (IntPtr)1; // swallow
                    }
                }
                else
                {
                    _oDown = false;
                }
            }
            else
            {
                _oDown = false;
            }
        }
        catch
        {
            // ignore
        }

        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public int vkCode;
        public int scanCode;
        public int flags;
        public int time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("kernel32.dll")]
    private static extern int GetCurrentThreadId();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx([In] ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern sbyte GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage([In] ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage([In] ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int nExitCode);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
}
