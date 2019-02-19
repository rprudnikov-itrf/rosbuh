using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using System.ServiceModel;
using RosService.Data;
using RosService.Configuration;
using RosService.Finance;
using RosService.Services;
using RosService.Files;

namespace RosService
{
    static class Program
    {
        public static string SERVICE_NAME = "ROSSERVICE5";
        public static string SERVICE_DISPLAY = "Ros.Service 5.0 (www.itrf.ru)";


        static void Main()
        {
            if (System.Configuration.ConfigurationManager.AppSettings["ServerName"] != null)
                SERVICE_NAME = System.Configuration.ConfigurationManager.AppSettings["ServerName"];

            if (System.Configuration.ConfigurationManager.AppSettings["ServerDisplay"] != null)
                SERVICE_DISPLAY = System.Configuration.ConfigurationManager.AppSettings["ServerDisplay"];

            var arg = Environment.GetCommandLineArgs();
            var IsInstalled = ServiceTools.ServiceInstaller.ServiceIsInstalled(SERVICE_NAME);
          
            if (arg.Contains("-u"))
            {
                if (MessageBox.Show(string.Format("Вы действительно хотите удалить '{0}'?", SERVICE_DISPLAY), "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    if (IsInstalled)
                        ServiceTools.ServiceInstaller.Uninstall(SERVICE_NAME);

                    MessageBox.Show(string.Format("{0} :удален", SERVICE_DISPLAY));
                }
            }
            if (arg.Contains("-i"))
            {
                if (IsInstalled)
                    ServiceTools.ServiceInstaller.Uninstall(SERVICE_NAME);

                Install();
                MessageBox.Show(string.Format("{0} :установлен", SERVICE_DISPLAY));
            }
            if (arg.Contains("-a"))
            {
                Console.WriteLine("Архивирование файлов");
                new RosService.ServerClient().АрхивироватьФайлы();
                return;
            }
            if (arg.Contains("-start") || arg.Contains("-s"))
            {
                Console.WriteLine("Сервер запущен...");
                #region wcf
                new ServiceHost(typeof(DataClient)).Open();
                new ServiceHost(typeof(ConfigurationClient)).Open();
                new ServiceHost(typeof(FinanceClient)).Open();
                new ServiceHost(typeof(ServicesClient)).Open();
                new ServiceHost(typeof(FileClient)).Open();
                #endregion
                return;
            }
            //запустить конфигуратор
            //if (arg.Contains("-config"))
            //{
            //    Application.EnableVisualStyles();
            //    Application.SetCompatibleTextRenderingDefault(false);
            //    Application.Run(new Настройки());
            //}
            //при первом пуске установить и запустить
            else if (!IsInstalled)
            {
                Install();
            }
            else
            {
#if !DEBUG
                ServiceBase.Run(new RosServiceHost());
#endif
            }
        }

        public static void Install()
        {
            ServiceTools.ServiceInstaller.InstallAndStart(SERVICE_NAME, SERVICE_DISPLAY, System.Reflection.Assembly.GetExecutingAssembly().Location);

            //создать ярлыки
            //настройки
            //var shortcutFileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), string.Format("Настройки - {0}.lnk", SERVICE_DISPLAY));
            //var shortcut = new ShellLink();
            //shortcut.Target = Application.ExecutablePath;
            //shortcut.Arguments = "-config";
            //shortcut.WorkingDirectory = Path.GetDirectoryName(Application.ExecutablePath);
            //shortcut.DisplayMode = ShellLink.LinkDisplayMode.edmNormal;
            //shortcut.Save(shortcutFileName);

            //удалить
            //shortcutFileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), string.Format("Удалить - {0}.lnk", SERVICE_DISPLAY));
            //shortcut = new ShellLink();
            //shortcut.Target = Application.ExecutablePath;
            //shortcut.Arguments = "-u";
            //shortcut.WorkingDirectory = Path.GetDirectoryName(Application.ExecutablePath);
            //shortcut.DisplayMode = ShellLink.LinkDisplayMode.edmNormal;
            //shortcut.Save(shortcutFileName);

            //установить
            //shortcutFileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), string.Format("Установить - {0}.lnk", SERVICE_DISPLAY));
            //shortcut = new ShellLink();
            //shortcut.Target = Application.ExecutablePath;
            //shortcut.Arguments = "-i";
            //shortcut.WorkingDirectory = Path.GetDirectoryName(Application.ExecutablePath);
            //shortcut.DisplayMode = ShellLink.LinkDisplayMode.edmNormal;
            //shortcut.Save(shortcutFileName);
        }
    }
}
