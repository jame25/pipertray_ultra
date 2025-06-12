using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PiperTray
{
    public class GlobalHotkeyManager : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentThreadId();

        // Modifier key constants
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;

        // Virtual key codes for common keys
        private const uint VK_M = 0x4D; // M key
        private const uint VK_S = 0x53; // S key
        private const uint VK_P = 0x50; // P key
        private const uint VK_V = 0x56; // V key
        private const uint VK_LEFT = 0x25; // Left arrow
        private const uint VK_RIGHT = 0x27; // Right arrow

        // Message window for receiving hotkey messages
        private HotkeyMessageWindow messageWindow;
        private Dictionary<int, Action> registeredHotkeys = new Dictionary<int, Action>();
        private int nextHotkeyId = 1000;
        private bool disposed = false;

        public delegate void HotkeyPressedEventHandler(int hotkeyId);
        public event HotkeyPressedEventHandler? HotkeyPressed;

        public GlobalHotkeyManager()
        {
            messageWindow = new HotkeyMessageWindow(this);
        }

        public bool RegisterHotkey(string hotkeyString, Action callback)
        {
            try
            {
                var (modifiers, virtualKey) = ParseHotkeyString(hotkeyString);
                if (virtualKey == 0)
                {
                    Logger.Warn($"Invalid hotkey string: {hotkeyString}");
                    return false;
                }

                int hotkeyId = nextHotkeyId++;
                
                if (RegisterHotKey(messageWindow.Handle, hotkeyId, modifiers, virtualKey))
                {
                    registeredHotkeys[hotkeyId] = callback;
                    Logger.Info($"Registered global hotkey: {hotkeyString} (ID: {hotkeyId})");
                    return true;
                }
                else
                {
                    Logger.Warn($"Failed to register global hotkey: {hotkeyString}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error registering hotkey {hotkeyString}", ex);
                return false;
            }
        }

        public void UnregisterAllHotkeys()
        {
            foreach (var hotkeyId in registeredHotkeys.Keys)
            {
                UnregisterHotKey(messageWindow.Handle, hotkeyId);
                Logger.Debug($"Unregistered hotkey ID: {hotkeyId}");
            }
            registeredHotkeys.Clear();
        }

        private (uint modifiers, uint virtualKey) ParseHotkeyString(string hotkeyString)
        {
            if (string.IsNullOrEmpty(hotkeyString) || hotkeyString == "None")
            {
                return (0, 0);
            }

            uint modifiers = 0;
            uint virtualKey = 0;

            string[] parts = hotkeyString.Split('+');
            
            foreach (string part in parts)
            {
                string trimmedPart = part.Trim().ToUpper();
                
                switch (trimmedPart)
                {
                    case "CTRL":
                        modifiers |= MOD_CONTROL;
                        break;
                    case "ALT":
                        modifiers |= MOD_ALT;
                        break;
                    case "SHIFT":
                        modifiers |= MOD_SHIFT;
                        break;
                    case "WIN":
                        modifiers |= MOD_WIN;
                        break;
                    case "M":
                        virtualKey = VK_M;
                        break;
                    case "S":
                        virtualKey = VK_S;
                        break;
                    case "P":
                        virtualKey = VK_P;
                        break;
                    case "V":
                        virtualKey = VK_V;
                        break;
                    case "LEFT":
                        virtualKey = VK_LEFT;
                        break;
                    case "RIGHT":
                        virtualKey = VK_RIGHT;
                        break;
                    default:
                        // Try to parse as a single character key
                        if (trimmedPart.Length == 1)
                        {
                            char keyChar = trimmedPart[0];
                            if (keyChar >= 'A' && keyChar <= 'Z')
                            {
                                virtualKey = (uint)keyChar;
                            }
                        }
                        break;
                }
            }

            return (modifiers, virtualKey);
        }

        internal void OnHotkeyPressed(int hotkeyId)
        {
            if (registeredHotkeys.TryGetValue(hotkeyId, out Action? callback))
            {
                try
                {
                    callback?.Invoke();
                    Logger.Debug($"Executed hotkey callback for ID: {hotkeyId}");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error executing hotkey callback for ID {hotkeyId}", ex);
                }
            }
            else
            {
                Logger.Warn($"Received hotkey press for unregistered ID: {hotkeyId}");
            }
        }

        public void Dispose()
        {
            if (!disposed)
            {
                UnregisterAllHotkeys();
                messageWindow?.DestroyHandle();
                messageWindow = null;
                disposed = true;
                Logger.Info("Global hotkey manager disposed");
            }
        }

        private class HotkeyMessageWindow : NativeWindow
        {
            private const int WM_HOTKEY = 0x0312;
            private GlobalHotkeyManager manager;

            public HotkeyMessageWindow(GlobalHotkeyManager manager)
            {
                this.manager = manager;
                CreateHandle(new CreateParams());
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_HOTKEY)
                {
                    int hotkeyId = m.WParam.ToInt32();
                    manager.OnHotkeyPressed(hotkeyId);
                }

                base.WndProc(ref m);
            }
        }
    }
}