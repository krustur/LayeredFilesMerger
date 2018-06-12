using System;
using System.Windows.Forms;
using Serilog;
using Unity;

namespace LayeredFilesMergerForm
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            var container = new UnityContainer();
            var logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.RollingFile(@"E:\Amiga\KrustWB2\Logs\LayeredFilesMerger_{Date}.log")
                .CreateLogger();

            IocConfiguration.Configure(container, logger);


            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var mainForm = container.Resolve<IMainForm>();
            Application.Run((Form)mainForm);
        }
    }
}