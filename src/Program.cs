using System;
using System.Threading;
using System.Windows.Forms;

namespace ShotTray
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool firstInstance;
            using (var mutex = new Mutex(true, "ShotTray.SingleInstance", out firstInstance))
            {
                if (!firstInstance) return;

                Native.SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += (s, e) =>
                    MessageBox.Show(e.Exception.Message, "ShotTray", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Run(new TrayApp());
            }
        }
    }
}
