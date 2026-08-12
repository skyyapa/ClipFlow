using System;
using System.Threading;
using System.Windows;

namespace ClipFlow
{
    internal static class Program
    {
        private static Mutex _mutex;

        [STAThread]
        public static void Main(string[] args)
        {
            bool created;
            _mutex = new Mutex(true, "Local\\ClipFlow.SingleInstance", out created);
            if (!created)
            {
                MessageBox.Show("ClipFlow 已经在运行，请按 Ctrl+Shift+V 呼出。", "ClipFlow");
                return;
            }

            Application application = new Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };
            MainWindow window = new MainWindow();
            window.StartBackground();
            if (args != null && Array.IndexOf(args, "--show") >= 0)
            {
                application.Dispatcher.BeginInvoke(new Action(window.ShowPalette));
            }
            application.Run();

            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }
}
