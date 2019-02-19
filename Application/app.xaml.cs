using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Timers;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Deployment.Application;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.IO;
using RosApplication.Properties;
using System.Collections.ObjectModel;
using Microsoft.Win32;

namespace RosApplication
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : RosControl.UI.Application
    {
        public const string ApplicationName = "Программа 5.0";

        static App()
        {
        }
        public static Uri ГлавнаяФорма
        {
            get
            {
                return new Uri("Клиент/main.xaml", UriKind.Relative);
            }
        }
        public static Uri Конфигуратор
        {
            get
            {
                return new Uri("Конфигуратор/main.xaml", UriKind.Relative);
            }
        }
        public static Uri Авторизация
        {
            get
            {
                return new Uri("logon.xaml", UriKind.Relative);
            }
        }
        public static string Title
        {
            get
            {
                if (Application.Current.MainWindow != null)
                {
                    return Application.Current.MainWindow.Title;
                }
                return null;
            }
            set
            {
                if (Application.Current.MainWindow != null)
                {
                    Application.Current.MainWindow.Title = value + " — РосИнфоТех";
                }
            }
        }

        public static string AppName;

        #region HistoryAccount
        [Serializable]
        public class HistoryAccount
        {
            public string Login { get; set; }
            public string Password { get; set; }
            public int Версия { get; set; }
            public string Group { get; set; }

            public override string ToString()
            {
                return Login;
            }
        }

        private static object lockHistoryAccounts = new System.Object();
        private static List<HistoryAccount> _HistoryAccounts = null;
        public static List<HistoryAccount> HistoryAccounts
        {
            get
            {
                if (_HistoryAccounts == null)
                {
                    LoadHistoryAccounts();
                }

                return _HistoryAccounts;
            }
            set
            {
                _HistoryAccounts = value;
            }
        }
        internal static void LoadHistoryAccounts()
        {
            _HistoryAccounts = null;

            var file = Path.Combine(AppPath, @"Accounts.xml");
            if (File.Exists(file))
            {
                var serializer = new System.Xml.Serialization.XmlSerializer(typeof(HistoryAccount[]));
                using (var stream = File.Open(file, FileMode.Open))
                {
                    var obj = serializer.Deserialize(stream);
                    _HistoryAccounts = new List<HistoryAccount>(obj as HistoryAccount[] ?? new HistoryAccount[0]);

                    foreach (var item in _HistoryAccounts)
                    {
                        if (string.IsNullOrEmpty(item.Group))
                            item.Group = "Рос.Бухгалтерия";
                    }
                }
            }
            else
            {
                _HistoryAccounts = new List<HistoryAccount>();
            }
        }
        internal static void SaveHistoryAccounts()
        {
            if (HistoryAccounts != null)
            {
                System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    try
                    {
                        lock (lockHistoryAccounts)
                        {
                            if (!Directory.Exists(AppPath))
                                Directory.CreateDirectory(AppPath);

                            var file = Path.Combine(AppPath, @"Accounts.xml");
                            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(HistoryAccount[]));
                            using (var stream = File.Create(file))
                            {
                                serializer.Serialize(stream, HistoryAccounts.ToArray());
                            }
                        }
                    }
                    catch
                    {
                    }
                });
            }
        }

        public static string AppPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Program 4.0");
            }
        }
        #endregion

        #region Session
        private static System.Timers.Timer _SesionTimer = null;
        public static void SessionStart()
        {
            Title = string.Format(@"{0}\{1}", RosService.Client.Domain, RosService.Client.User.Логин);
            НеПоказыватьСообщениеОбУдалении = Settings.Default.НеПоказыватьСообщениеОбУдалении;

            _SesionTimer = new System.Timers.Timer(TimeSpan.FromMinutes(18).TotalMilliseconds);
            _SesionTimer.AutoReset = true;
            _SesionTimer.Elapsed += delegate(object sender, ElapsedEventArgs e)
            {
                var obj = sender as System.Timers.Timer;
                if (obj == null) return;

                #region ПродлитьСессиюПользователя
                System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    try
                    {
                        RosService.Client.ПродлитьСессиюПользователя(TimeSpan.FromMinutes(20));
                    }
                    catch
                    {
                    }
                });
                #endregion
            };
            _SesionTimer.Start();
        }
        public static void SessionStop()
        {
            Title = ApplicationName;       

            if (_SesionTimer != null)
            {
                _SesionTimer.Stop();
                _SesionTimer.Dispose();
                _SesionTimer = null;
            }

            //закрыть все окна
            foreach (var item in Application.Current.Windows.Cast<Window>())
            {
                if (item == Application.Current.MainWindow) 
                    continue;
                item.Close();
            }

            RosControl.UI.Phone.Shutdown();

            //очистить кеш
            Clear();

            System.Threading.Tasks.Task.Factory.StartNew(delegate()
            {
                try
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }
                catch
                {
                }
            });
        }
        #endregion

        #region Settings
        private static RegistryKey Registry
        {
            get
            {
                return Microsoft.Win32.Registry.CurrentUser.CreateSubKey("itrf.ru");
            }
        }
        public static string ServerName
        {
            get
            {
                var itrf = Registry;
                if (itrf == null)
                {
                    return null;
                }
                return Convert.ToString(itrf.GetValue("ServerName"));
            }

            set
            {
                var itrf = Registry;
                if (itrf != null)
                {
                    itrf.SetValue("ServerName", value);
                }
            }
        }


        public static string UserName
        {
            get
            {
                var itrf = Registry;
                if (itrf == null)
                {
                    return null;
                }
                return Convert.ToString(itrf.GetValue("UserName"));
            }

            set
            {
                var itrf = Registry;
                if (itrf != null)
                {
                    itrf.SetValue("UserName", value);
                }
            }
        }
        #endregion

        public override void Logout()
        {
            if (Application.Current.MainWindow is NavigationWindow)
            {
                var page = ((NavigationWindow)Application.Current.MainWindow).Content as Page;
                if (page != null)
                {
                    RosApplication.Properties.Settings.Default.IsЗапомнить = false;
                    RosApplication.Properties.Settings.Default.Save();

                    if (page is IDisposable)
                        ((IDisposable)page).Dispose();

                    page.NavigationService.RemoveBackEntry();
                    page.NavigationService.Navigate(App.Авторизация);
                }
            }
        }
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            //#region Оптимизация
            //Установить кодировку по-умолчанию
            FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)));



            //снизить расходы на отрисову сцены, оптимизация загрузки CPU
            //switch (RenderCapability.Tier >> 16)
            //{
            //    case 0: // software rendering 
            //        Timeline.DesiredFrameRateProperty.OverrideMetadata(typeof(Timeline), new PropertyMetadata(2));
            //        break;

            //    case 1: // middle ground
            //        Timeline.DesiredFrameRateProperty.OverrideMetadata(typeof(Timeline), new PropertyMetadata(4));
            //        break;

            //    case 2: // hardware
            //        Timeline.DesiredFrameRateProperty.OverrideMetadata(typeof(Timeline), new PropertyMetadata(8));
            //        break;
            //}
            ////#endregion

            #region Перенести настройки
            if (string.IsNullOrEmpty(ServerName))
            {
                ServerName = Settings.Default.АдресСервера;
            }

            if (string.IsNullOrEmpty(UserName))
            {
                UserName = Settings.Default.ИмяПользователя;
            }
            #endregion


            Title = ApplicationName;

            #region Загрузка файла
            //if (AppDomain.CurrentDomain.SetupInformation.ActivationArguments != null
            //    && AppDomain.CurrentDomain.SetupInformation.ActivationArguments.ActivationData != null
            //    && AppDomain.CurrentDomain.SetupInformation.ActivationArguments.ActivationData.Length > 0)
            //{
            //    var commandLineFile = AppDomain.CurrentDomain.SetupInformation.ActivationArguments.ActivationData[0];
            //    var fileUri = new Uri(commandLineFile);
            //    string filePath = Uri.UnescapeDataString(fileUri.AbsolutePath);
            //    if (System.IO.File.Exists(filePath))
            //    {
            //        var content = new RosApplication.Конфигуратор.Службы.ПоискИЗамена();
            //        content.PART_Query.Text = System.IO.File.ReadAllText(filePath);
            //        Application.Current.MainWindow.Visibility = Visibility.Hidden;
            //        content.ShowDialog();
            //        Application.Current.Shutdown(0);
            //        return;
            //    }
            //    else
            //    {
            //        MessageBox.Show(filePath);
            //    }
            //}
            #endregion

            #region Автоматический вход
            if (RosApplication.Properties.Settings.Default.IsЗапомнить)
            {
                ((NavigationWindow)MainWindow).Navigate(new RosControl.UI.DocumentsTabControl());

                System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    try
                    {
                        var mode = RosService.ClientMode.None;
                        if (Enum.TryParse<RosService.ClientMode>(RosApplication.Properties.Settings.Default.ТипСоединения, out mode))
                            RosService.Client.Mode = mode;

                        //RosService.Client.Version = RosApplication.Properties.Settings.Default.Версия;
                        //var account = (HistoryAccounts ?? new HistoryAccount[0]).FirstOrDefault(p => p.Login == RosApplication.Properties.Settings.Default.ИмяПользователя);

                        //RosService.Client.Host = RosApplication.Properties.Settings.Default.АдресСервера;
                        //RosService.Client.Authorization(RosApplication.Properties.Settings.Default.ИмяПользователя, RosApplication.Properties.Settings.Default.Пароль, true);

                        RosService.Client.Host = App.ServerName;
                        RosService.Client.Authorization(App.UserName, RosApplication.Properties.Settings.Default.Пароль, true);
                        
                        using (RosService.Client client = new RosService.Client())
                        {
                            App.AppName = client.Архив.ПолучитьЗначение<string>("КорневойРаздел", "НазваниеОбъекта");
                        }

                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                        {
                            SessionStart();
                            ((NavigationWindow)MainWindow).Navigate(ГлавнаяФорма);
                            ((NavigationWindow)MainWindow).RemoveBackEntry();
                        });
                    }
                    catch
                    {
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                        {
                            ((NavigationWindow)MainWindow).Navigate(new logon());
                            ((NavigationWindow)MainWindow).RemoveBackEntry();
                        });
                    }
                });
            }
            else
            {
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                {
                    ((NavigationWindow)MainWindow).Navigate(new logon());
                    ((NavigationWindow)MainWindow).RemoveBackEntry();
                });
            }
            #endregion

            #region jumplist
            //if (((NavigationWindow)MainWindow).Content is logon && e.Args.Length > 0)
            //{
            //    ((logon)((NavigationWindow)MainWindow).Content).Пользователь = e.Args.ElementAtOrDefault(0);
            //}
            #endregion

            #region АвтоматическоеОбновление
            if (RosApplication.Properties.Settings.Default.АвтоматическоеОбновление)
            {
                //var timer = new System.Timers.Timer(25000);
                //timer.AutoReset = false;
                //timer.Elapsed += new ElapsedEventHandler(timer_Elapsed);
                //timer.Start();

                System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    try
                    {
                        Thread.Sleep(5000);
                        this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, (Action)delegate
                        {
                            RosControl.ApplicationDeployment.CurrentDeployment.ПроверитьОбновлениеСистемы();
                        });
                    }
                    catch
                    {
                    }
                });
            }
            #endregion

            #region Прокси
            //var proxy = System.Net.WebProxy.GetDefaultProxy();
            //if (proxy != null)
            //{
            //}
            #endregion


        }

        //void timer_Elapsed(object sender, ElapsedEventArgs e)
        //{
        //    this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, (Action)delegate
        //    {
        //        RosControl.ApplicationDeployment.CurrentDeployment.ПроверитьОбновлениеСистемы();
        //    });
        //}
        private void Application_Exit(object sender, ExitEventArgs e)
        {
            SessionStop();
            RosService.Client.Shutdown();
        }
        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                var exception = null as Exception;
                if (e != null && e.Exception != null)
                {
                    //исключить сообщения о разрыве связи
                    if (e.Exception is TimeoutException || e.Exception is DeploymentDownloadException)
                    {
                        e.Handled = true;
                        return;
                    }
                    else if (e.Exception is InvalidOperationException &&
                           e.Exception.Message == "Dispatcher processing has been suspended, but messages are still being processed.")
                    {
                        e.Handled = true;
                        return;
                    }
                    else if (e.Exception is InvalidOperationException &&
                           e.Exception.Message.EndsWith("VisualLineElementGenerator, but it is not collapsed."))
                    {
                        e.Handled = true;
                        return;
                    }
                    else if(e.Exception != null && e.Exception.InnerException != null)
                    {
                        exception = e.Exception.InnerException;
                    }
                    else if (e.Exception != null)
                    {
                        exception = e.Exception;
                    }

                    if (exception != null)
                    {
                        AddLog(exception);
                    }
                }

                if (!App.РежимТестирования)
                {
                    e.Handled = true;
                    return;
                }
                else if(exception != null)
                {
                    MessageBox.Show(exception.Message, "Ошибка в приложении", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception)
            {

            }
            finally
            {
                e.Handled = true;
            }
        }

        public static void AddLog(Exception exception)
        {
            if (exception == null)
                return;

            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                try
                {
                    //Записать в Windows-log
                    var source = (RosService.Client.Domain ?? "") + "@" + (RosService.Client.UserName ?? "");
                    if (!EventLog.SourceExists(source))
                    {
                        EventLog.CreateEventSource(source, "Программа 5.0");
                    }
                    EventLog.WriteEntry(source, exception.ToString(), System.Diagnostics.EventLogEntryType.Error, 1, 1);
                }
                catch
                {
                }
            });
        }
    }
}
