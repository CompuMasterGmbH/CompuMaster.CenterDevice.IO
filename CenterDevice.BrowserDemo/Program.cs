using System;
using System.Windows.Forms;

namespace CenterDevice.BrowserDemo
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new LoginForm());
        }
    }
}
