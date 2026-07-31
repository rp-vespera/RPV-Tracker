using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace RPV_Tracker.Domains.TimeTracking.Services
{
    /// <summary>
    /// Counts system-wide keyboard and mouse-button activity using low-level Windows hooks.
    /// </summary>
    /// <remarks>
    /// This is an activity counter, not a keylogger, and that distinction is deliberate:
    /// the keyboard callback increments a tally on each key-down and never inspects
    /// <c>lParam</c>, which is where the key's identity lives. No keystroke content is read,
    /// stored, or transmitted anywhere — only how many keys and clicks occurred. That is all
    /// an activity-level metric needs, and capturing more would turn a time tracker into spyware.
    ///
    /// The hooks are global (they see input regardless of which app has focus) so activity is
    /// still measured while the employee works in other applications. Callbacks run on the
    /// thread that installed them — the UI thread — so they do the absolute minimum (one
    /// interlocked increment) and return immediately to stay well inside the low-level hook
    /// timeout.
    /// </remarks>
    internal sealed class InputMonitor : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;

        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_XBUTTONDOWN = 0x020B;

        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        // The delegates are held in fields for the life of the monitor. If they were only
        // passed to SetWindowsHookEx the GC would collect them and the callback would crash.
        private readonly HookProc keyboardProc;
        private readonly HookProc mouseProc;

        private IntPtr keyboardHook = IntPtr.Zero;
        private IntPtr mouseHook = IntPtr.Zero;

        private long keyCount;
        private long clickCount;

        public InputMonitor()
        {
            keyboardProc = KeyboardCallback;
            mouseProc = MouseCallback;
        }

        public long KeyCount { get { return Interlocked.Read(ref keyCount); } }

        public long ClickCount { get { return Interlocked.Read(ref clickCount); } }

        public bool IsRunning { get { return keyboardHook != IntPtr.Zero; } }

        public void Start()
        {
            if (IsRunning)
            {
                return;
            }

            IntPtr module;
            using (Process process = Process.GetCurrentProcess())
            using (ProcessModule main = process.MainModule)
            {
                module = GetModuleHandle(main.ModuleName);
            }

            keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, keyboardProc, module, 0);
            mouseHook = SetWindowsHookEx(WH_MOUSE_LL, mouseProc, module, 0);

            if (keyboardHook == IntPtr.Zero || mouseHook == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                Stop();
                throw new Win32Exception(error, "Could not install input hooks for activity tracking.");
            }
        }

        public void Stop()
        {
            if (keyboardHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(keyboardHook);
                keyboardHook = IntPtr.Zero;
            }
            if (mouseHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(mouseHook);
                mouseHook = IntPtr.Zero;
            }
        }

        public void ResetCounts()
        {
            Interlocked.Exchange(ref keyCount, 0);
            Interlocked.Exchange(ref clickCount, 0);
        }

        private IntPtr KeyboardCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int message = wParam.ToInt32();
                if (message == WM_KEYDOWN || message == WM_SYSKEYDOWN)
                {
                    Interlocked.Increment(ref keyCount);
                }
                // lParam carries the virtual key code. It is intentionally never read:
                // we count that a key was pressed, never which one.
            }

            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        private IntPtr MouseCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int message = wParam.ToInt32();
                if (message == WM_LBUTTONDOWN || message == WM_RBUTTONDOWN
                    || message == WM_MBUTTONDOWN || message == WM_XBUTTONDOWN)
                {
                    Interlocked.Increment(ref clickCount);
                }
            }

            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
