using System;
using System.Threading;
using System.Windows.Forms;

namespace PiperTray
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            const string mutexName = "PiperTrayApplication_SingleInstance";
            bool createdNew;
            
            using (var mutex = new Mutex(true, mutexName, out createdNew))
            {
                if (!createdNew)
                {
                    // Another instance is already running
                    MessageBox.Show("PiperTray is already running.", "PiperTray", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                
                var trayApp = new TrayApplication();
                Application.Run();
            }
        }
    }
}