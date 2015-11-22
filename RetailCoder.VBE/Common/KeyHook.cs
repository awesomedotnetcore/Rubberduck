﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Vbe.Interop;

namespace Rubberduck.Common
{
    public interface IKeyHook
    {
        void Attach();
        void Detach();
        event EventHandler<KeyHookEventArgs> KeyPressed;
    }

    public class KeyHook : IKeyHook, IDisposable
    {
        private readonly VBE _vbe;
        // reference: http://blogs.msdn.com/b/toub/archive/2006/05/03/589423.aspx

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;

        private readonly LowLevelKeyboardProc _proc;
        private static readonly IntPtr HookId = IntPtr.Zero;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        private static extern IntPtr GetWindowThreadProcessId(IntPtr handle, out int processID);

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (var curProcess = Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private static readonly Keys[] IgnoredKeys = 
        {
            Keys.Down,
            Keys.Up,
            Keys.Left,
            Keys.Right,
            Keys.PageDown,
            Keys.PageUp,
            Keys.CapsLock,
            Keys.Escape,
            Keys.Home,
            Keys.End,
            Keys.Shift,
            Keys.ShiftKey,
            Keys.LShiftKey,
            Keys.RShiftKey,
            Keys.Control,
            Keys.ControlKey,
            Keys.LControlKey,
            Keys.RControlKey,
        };

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            // This is the window handle.  See if this is the value given by the ActiveWindow handle in the VBE.
            var windowHandle = GetForegroundWindow();
            var vbeWindow = _vbe.MainWindow.HWnd;

            if (windowHandle != (IntPtr)vbeWindow || nCode < 0 || wParam != (IntPtr)WM_KEYUP)
            {
                return CallNextHookEx(HookId, nCode, wParam, lParam);
            }

            // These two lines tell us what key is pressed
            var vkCode = Marshal.ReadInt32(lParam);
            var key = (Keys)vkCode;
            if (IgnoredKeys.Contains(key))
            {
                return CallNextHookEx(HookId, nCode, wParam, lParam);
            }

            // If the above does not work, this gives us the process handle
            int processId;
            GetWindowThreadProcessId(windowHandle, out processId);

            //var process = Process.GetProcessById(processId);

            // The process name must be something like "EXCEL" or "WINWORD"
            // The MainWindowTitle must be something like "Microsoft Visual Basic for Applications - *"
            //Console.WriteLine(process.ProcessName);
            //Console.WriteLine(process.MainWindowTitle);
            var codePane = _vbe.ActiveCodePane;
            if (codePane != null)
            {
                var component = codePane.CodeModule.Parent;
                var args = new KeyHookEventArgs(key, component);
                OnKeyPressed(args);
            }

            return CallNextHookEx(HookId, nCode, wParam, lParam);
        }

        public KeyHook(VBE vbe)
        {
            _vbe = vbe;
            _proc = HookCallback;
        }

        public void Attach()
        {
            SetHook(_proc);
        }

        public void Detach()
        {
            UnhookWindowsHookEx(HookId);
        }

        public event EventHandler<KeyHookEventArgs> KeyPressed;

        private void OnKeyPressed(KeyHookEventArgs e)
        {
            var handler = KeyPressed;
            if (handler != null)
            {
                handler.Invoke(this, e);
            }
        }

        public void Dispose()
        {
            Detach();
        }
    }

    /// <summary>
    /// Contains information about a captured key press resulting in modified code for a VBComponent.
    /// </summary>
    public class KeyHookEventArgs : EventArgs
    {
        private readonly Keys _key;
        private readonly VBComponent _component;

        public KeyHookEventArgs(Keys key, VBComponent component)
        {
            _key = key;
            _component = component;
        }

        public Keys Key { get { return _key; } }
        public VBComponent Component { get { return _component; } }
    }
}
