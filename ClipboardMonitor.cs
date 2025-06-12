using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PiperTray
{
    public class ClipboardMonitor : NativeWindow, IDisposable
    {
        private static class NativeMethods
        {
            [DllImport("User32.dll", CharSet = CharSet.Auto)]
            public static extern IntPtr SetClipboardViewer(IntPtr hWndNewViewer);

            [DllImport("User32.dll", CharSet = CharSet.Auto)]
            public static extern bool ChangeClipboardChain(IntPtr hWndRemove, IntPtr hWndNewNext);

            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            public static extern int SendMessage(IntPtr hwnd, int wMsg, IntPtr wParam, IntPtr lParam);

            public const int WM_DRAWCLIPBOARD = 0x0308;
            public const int WM_CHANGECBCHAIN = 0x030D;
        }

        private IntPtr nextClipboardViewer;
        private bool disposed = false;

        public event EventHandler<string>? ClipboardChanged;

        public void Start()
        {
            if (Handle == IntPtr.Zero)
            {
                CreateHandle(new CreateParams());
            }
            nextClipboardViewer = NativeMethods.SetClipboardViewer(Handle);
        }

        public void Stop()
        {
            if (Handle != IntPtr.Zero)
            {
                NativeMethods.ChangeClipboardChain(Handle, nextClipboardViewer);
                DestroyHandle();
            }
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case NativeMethods.WM_DRAWCLIPBOARD:
                    OnClipboardChanged();
                    NativeMethods.SendMessage(nextClipboardViewer, m.Msg, m.WParam, m.LParam);
                    break;

                case NativeMethods.WM_CHANGECBCHAIN:
                    if (m.WParam == nextClipboardViewer)
                        nextClipboardViewer = m.LParam;
                    else
                        NativeMethods.SendMessage(nextClipboardViewer, m.Msg, m.WParam, m.LParam);
                    break;

                default:
                    base.WndProc(ref m);
                    break;
            }
        }

        private void OnClipboardChanged()
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    string clipboardText = Clipboard.GetText();
                    ClipboardChanged?.Invoke(this, clipboardText);
                }
            }
            catch (Exception)
            {
                // Clipboard access can fail, ignore silently
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    Stop();
                }
                disposed = true;
            }
        }

        ~ClipboardMonitor()
        {
            Dispose(false);
        }
    }
}