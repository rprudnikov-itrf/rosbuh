using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using System.Windows;
using System.Windows.Navigation;
using System.Windows.Controls;
using System.Xml;
using System.IO;
using RosControl.Designer;
using RosService;
using System.Diagnostics;
using System.Deployment.Application;
using System.Runtime.InteropServices;


namespace RosApplication.Command
{
    public static class ApplicationCommand
    {
        public readonly static RoutedCommand Файл = new RoutedCommand();
        public readonly static RoutedCommand Вид = new RoutedCommand();
        public readonly static RoutedCommand Сервис = new RoutedCommand();
        public readonly static RoutedCommand Поиск = new RoutedCommand();
        public readonly static RoutedCommand Справка = new RoutedCommand();
        public readonly static RoutedCommand Почта = new RoutedCommand();
        public readonly static RoutedCommand Задачи = new RoutedCommand();
        public readonly static RoutedCommand Пользователь = new RoutedCommand();

        public readonly static RoutedCommand Журналы = new RoutedCommand();
        public readonly static RoutedCommand Отчеты = new RoutedCommand();
        public readonly static RoutedCommand Справочники = new RoutedCommand();
        public readonly static RoutedCommand ОбновитьСчетчики = new RoutedCommand();
        public readonly static RoutedCommand ClearGC = new RoutedCommand();
        public readonly static RoutedCommand DispatcherWindows = new RoutedCommand();
        public readonly static RoutedCommand UpdateAllDomains = new RoutedCommand();
    };

    internal static class CommandBinding
    {
        [DllImport("kernel32.dll")]
        public static extern bool SetProcessWorkingSetSize(IntPtr handle, int minimumWorkingSetSize, int maximumWorkingSetSize);

        public static void SetCommandBinding(FrameworkElement source)
        {
            CommandManager.RegisterClassCommandBinding(source.GetType(), new System.Windows.Input.CommandBinding(ApplicationCommand.Вид, Вид_Executed));
            CommandManager.RegisterClassCommandBinding(source.GetType(), new System.Windows.Input.CommandBinding(ApplicationCommand.Файл, Файл_Executed));
            CommandManager.RegisterClassCommandBinding(source.GetType(), new System.Windows.Input.CommandBinding(ApplicationCommand.Сервис, Сервис_Executed));
            CommandManager.RegisterClassCommandBinding(source.GetType(), new System.Windows.Input.CommandBinding(ApplicationCommand.Поиск, Поиск_Executed));
            CommandManager.RegisterClassCommandBinding(source.GetType(), new System.Windows.Input.CommandBinding(ApplicationCommand.Справка, Справка_Executed));
            CommandManager.RegisterClassCommandBinding(source.GetType(), new System.Windows.Input.CommandBinding(ApplicationCommand.Почта, Почта_Executed));
            CommandManager.RegisterClassCommandBinding(source.GetType(), new System.Windows.Input.CommandBinding(ApplicationCommands.New, Почта_Executed));
            CommandManager.RegisterClassCommandBinding(source.GetType(), new System.Windows.Input.CommandBinding(ApplicationCommand.Задачи, Задачи_Executed));
            CommandManager.RegisterClassCommandBinding(source.GetType(), new System.Windows.Input.CommandBinding(ApplicationCommand.Пользователь, Пользователь_Executed));
            CommandManager.RegisterClassCommandBinding(source.GetType(), new System.Windows.Input.CommandBinding(ApplicationCommands.Properties, Пользователь_Executed));
            CommandManager.RegisterClassCommandBinding(source.GetType(), new System.Windows.Input.CommandBinding(ApplicationCommands.Help, Справка_Executed));


            CommandManager.RegisterClassCommandBinding(source.GetType(), new System.Windows.Input.CommandBinding(ApplicationCommand.Журналы, Журналы_Executed, delegate(object sender, CanExecuteRoutedEventArgs e) { e.CanExecute = RosService.Client.User.Группа.Equals("Администратор"); }));
            CommandManager.RegisterClassCommandBinding(source.GetType(), new System.Windows.Input.CommandBinding(ApplicationCommand.Отчеты, Отчеты_Executed, delegate(object sender, CanExecuteRoutedEventArgs e) { e.CanExecute = RosService.Client.User.Группа.Equals("Администратор"); }));
            CommandManager.RegisterClassCommandBinding(source.GetType(), new System.Windows.Input.CommandBinding(ApplicationCommand.Справочники, Справочники_Executed, delegate(object sender, CanExecuteRoutedEventArgs e) { e.CanExecute = RosService.Client.User.Группа.Equals("Администратор"); }));
            CommandManager.RegisterClassCommandBinding(source.GetType(), new System.Windows.Input.CommandBinding(ApplicationCommand.ОбновитьСчетчики, delegate { RosControl.UI.Application.ОбновитьСчетчики(); }));
            CommandManager.RegisterClassCommandBinding(source.GetType(), new System.Windows.Input.CommandBinding(ApplicationCommand.ClearGC, ClearGC_Executed));
            CommandManager.RegisterClassCommandBinding(source.GetType(), new System.Windows.Input.CommandBinding(ApplicationCommand.DispatcherWindows, DispatcherWindows_Executed));

            CommandManager.RegisterClassInputBinding(source.GetType(), new InputBinding(ApplicationCommand.Журналы, new KeyGesture(Key.F1, ModifierKeys.Control, "Ctrl+F1")));
            CommandManager.RegisterClassInputBinding(source.GetType(), new InputBinding(ApplicationCommand.Отчеты, new KeyGesture(Key.F2, ModifierKeys.Control, "Ctrl+F2")));
            CommandManager.RegisterClassInputBinding(source.GetType(), new InputBinding(ApplicationCommand.Справочники, new KeyGesture(Key.F3, ModifierKeys.Control, "Ctrl+F3")));
            CommandManager.RegisterClassInputBinding(source.GetType(), new InputBinding(ApplicationCommand.ОбновитьСчетчики, new KeyGesture(Key.F5, ModifierKeys.Control, "Ctrl+F5")));
            CommandManager.RegisterClassInputBinding(source.GetType(), new InputBinding(ApplicationCommand.ClearGC, new KeyGesture(Key.G, ModifierKeys.Control, "Ctrl+G")));
            CommandManager.RegisterClassInputBinding(source.GetType(), new InputBinding(ApplicationCommand.DispatcherWindows, new KeyGesture(Key.Escape, ModifierKeys.Shift, "Shift+Esc")));
            //CommandManager.RegisterClassInputBinding(source.GetType(), new InputBinding(ApplicationCommand.Вид, new KeyGesture(Key.K, ModifierKeys.Control & ModifierKeys.Alt)));

             
            //отключить кнопку F5 в броузере
            NavigationCommands.BrowseBack.InputGestures.Clear();
            NavigationCommands.BrowseForward.InputGestures.Clear();
            NavigationCommands.Refresh.InputGestures.Clear();
            NavigationCommands.BrowseHome.InputGestures.Clear(); 

            CommandManager.RegisterClassCommandBinding(source.GetType(), new System.Windows.Input.CommandBinding(NavigationCommands.Refresh, delegate(object sender, ExecutedRoutedEventArgs e) { e.Handled = true; }));
            CommandManager.RegisterClassCommandBinding(source.GetType(), new System.Windows.Input.CommandBinding(NavigationCommands.BrowseBack, delegate(object sender, ExecutedRoutedEventArgs e) { e.Handled = true; }));
            CommandManager.RegisterClassCommandBinding(source.GetType(), new System.Windows.Input.CommandBinding(NavigationCommands.BrowseHome, delegate(object sender, ExecutedRoutedEventArgs e) { e.Handled = true; }));
            CommandManager.RegisterClassCommandBinding(source.GetType(), new System.Windows.Input.CommandBinding(NavigationCommands.BrowseForward, delegate(object sender, ExecutedRoutedEventArgs e) { e.Handled = true; }));

            //UpdateAllDomains
            CommandManager.RegisterClassCommandBinding(source.GetType(), new System.Windows.Input.CommandBinding(ApplicationCommand.UpdateAllDomains, UpdateAllDomains_Executed));
            CommandManager.RegisterClassInputBinding(source.GetType(), new InputBinding(ApplicationCommand.UpdateAllDomains, new KeyGesture(Key.U, ModifierKeys.Alt | ModifierKeys.Control)));
        }

        public static void UpdateAllDomains_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            //if (MessageBox.Show("Выполнить обновление системы?", "Предупреждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            //{
            //    new RosService.Client().Конфигуратор.ОбновитьКонфигурацию(RosService.Client.UserName, RosService.Client.Domain);
            //}
        }
        public static void DispatcherWindows_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            new RosApplication.Конфигуратор.Службы.ДиспетчерОкон().Show();
        }
        public static void ClearGC_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        public static void Журналы_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            RosControl.Helper.СоздатьОкно("ЖурналыПользователя", RosService.Data.Хранилище.Оперативное);
        }
        public static void Отчеты_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            RosControl.Helper.СоздатьОкно("ОтчетыПользователя", RosService.Data.Хранилище.Оперативное);
        }
        public static void Справочники_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            RosControl.Helper.СоздатьОкно("СправочникиПользователя", RosService.Data.Хранилище.Оперативное);
        }

        public static void Пользователь_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            switch (Convert.ToString(e.Parameter))
            {
                case "РедакторПользователей":
                    {
                        RosControl.Helper.СоздатьВкладку("Пользователи", RosService.Data.Хранилище.Оперативное, true);
                    }
                    break;

                case "Настройки":
                    {
                        //RosControl.Helper.ОткрытьВОкне(Client.User.id_node, RosService.Data.Хранилище.Оперативное);
                        RosControl.Forms.НастройкиПользователя.Открыть(Client.User.id_node);
                    }
                    break;
            }
        }
        public static void Почта_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var Parameter = Convert.ToString(e.Parameter);
            if (Parameter.Contains("mailto:"))
            {
                new Window()
                {
                    Title = "Письмо",
                    ShowActivated = true,
                    Width = 900,
                    Height = 640,
                    WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
                    Content = new RosControl.Forms.СоздатьПисьмо() 
                    { 
                        Кому = Parameter.Split(':').ElementAtOrDefault(1),
                        IsРассылка = Parameter == @"mailto:all"
                    }
                }.Show();
            }
            else if (Parameter.Contains("http:"))
            {
                var tab = RosControl.Helper.СоздатьВкладку(
                    new WebBrowser() { Source = new Uri("http:" + Parameter.Split(':').ElementAtOrDefault(1)) },
                    Parameter.Split(':').ElementAtOrDefault(2),
                    Parameter.Split(':').ElementAtOrDefault(2),
                    true, true);
                tab.IsFull = true;
            }
            else if (e.Command == ApplicationCommand.Почта)
            {
                RosControl.Helper.СоздатьВкладку(
                    new RosControl.Forms.РаботаСПочтой(),
                    "@Отправленные",
                    "@Отправленные",
                    true, true);
            }
        }

        public static void СоздатьЗадачу(IEnumerable<RosControl.Forms.Пользователи.ВыбранныйПользователь> items)
        {
            //var window = new Window()
            //{
            //    Title = "Задача",
            //    WindowStartupLocation = WindowStartupLocation.CenterScreen,
            //    Width = 820,
            //    Height = 580,
            //    Content = new RosApplication.Клиент.Задачи.НоваяЗадача() { Users = items }
            //};
            //window.Show();
        }
        public static void Задачи_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            //switch (Convert.ToString(e.Parameter))
            //{
            //    case "Создать":
            //        СоздатьЗадачу(null);
            //        break;

            //    //default:
            //    //    {
            //    //        RosControl.Helper.СоздатьВкладку("ЗадачиПользователя", RosService.Data.Хранилище.Оперативное, true);
            //    //    }
            //    //    break;
            //}
        }

        public static void Справка_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            switch (Convert.ToString(e.Parameter))
            {
                case "ТехническаяПоддержка":
                    {
                        var window = new RosControl.Forms.ТехническаяПоддержка();
                        //window.Owner = Application.Current.MainWindow;
                        window.Show();
                    }
                    break;

                case "ОПрограмме":
                    {
                        var window = new RosControl.Forms.ОПрограмме();
                        //window.Owner = Application.Current.MainWindow;
                        window.ShowDialog();
                    }
                    break;

                //case "ПроверитьОбновления":
                //    {
                //        UpdateCheckInfo info = null;
                //        if (ApplicationDeployment.IsNetworkDeployed)
                //        {
                //            ApplicationDeployment ad = ApplicationDeployment.CurrentDeployment;

                //            try
                //            {
                //                info = ad.CheckForDetailedUpdate();
                //            }
                //            catch (DeploymentDownloadException dde)
                //            {
                //                MessageBox.Show("The new version of the application cannot be downloaded at this time. \n\nPlease check your network connection, or try again later. Error: " + dde.Message);
                //                return;
                //            }
                //            catch (InvalidDeploymentException ide)
                //            {
                //                MessageBox.Show("Cannot check for a new version of the application. The ClickOnce deployment is corrupt. Please redeploy the application and try again. Error: " + ide.Message);
                //                return;
                //            }
                //            catch (InvalidOperationException ioe)
                //            {
                //                MessageBox.Show("This application cannot be updated. It is likely not a ClickOnce application. Error: " + ioe.Message);
                //                return;
                //            }
                //            if (info.UpdateAvailable)
                //            {
                //                Boolean doUpdate = true;

                //                if (!info.IsUpdateRequired)
                //                {
                //                    var dr = MessageBox.Show("Доступна новая версия программы. Хотите ли обновить сейчас?", "Обновление", MessageBoxButton.OKCancel);
                //                    if (!(dr == MessageBoxResult.OK))
                //                    {
                //                        doUpdate = false;
                //                    }
                //                }
                //                else
                //                {
                //                    // Display a message that the app MUST reboot. Display the minimum required version.
                //                    MessageBox.Show("This application has detected a mandatory update from your current " +
                //                        "version to version " + info.MinimumRequiredVersion.ToString() +
                //                        ". The application will now install the update and restart.",
                //                        "Обновление", MessageBoxButton.OK, MessageBoxImage.Information);
                //                }

                //                if (doUpdate)
                //                {
                //                    try
                //                    {
                //                        ad.Update();
                //                        MessageBox.Show("The application has been upgraded, and will now restart.");
                //                    }
                //                    catch (DeploymentDownloadException dde)
                //                    {
                //                        MessageBox.Show("Cannot install the latest version of the application. \n\nPlease check your network connection, or try again later. Error: " + dde);
                //                        return;
                //                    }
                //                }
                //            }
                //            else
                //            {
                //                MessageBox.Show("Вы используете последнию версию программы.");
                //            }
                //        }
                //    }
                //    break;

                default:
                    {
                        var tab = RosControl.Helper.СоздатьВкладку(
                            new WebBrowser() { Source = new Uri("http://www.itrf.ru/") },
                            "Справка",
                            "Справка",
                            true, true);
                        tab.IsFull = true;
                    }
                    break;
            }
        }
        public static void Поиск_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var page = RosControl.Helper.FindParentControl<Конфигуратор.Main>(sender as DependencyObject);
            if (page != null)
            {
                var _content = new RosControl.Forms.РасшириныйПоиск();
                _content.МестоПоиска = "Конфигурация";
                RosControl.Helper.СоздатьВкладку(
                    _content,
                    "Расшириный поиск",
                    "Расшириный поиск",
                    true, true);
            }
            else
            {
                RosControl.Helper.СоздатьВкладку(
                    new RosControl.Forms.РасшириныйПоиск(),
                    "Расшириный поиск",
                    "Расшириный поиск",
                    true, true);
            }
        }
        public static void Вид_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var contanier = sender as DependencyObject;
            switch (e.Parameter.ToString())
            {
                case "Архив":
                    {
                        var page = RosControl.Helper.FindParentControl<Page>(contanier);
                        if (page != null)
                        {
                            //очистить все вкладки
                            if (page is IDisposable)
                                ((IDisposable)page).Dispose();

                            page.NavigationService.RemoveBackEntry();
                            page.NavigationService.Navigate(App.ГлавнаяФорма);
                            page.DataContext = null;
                            page = null;

                            System.Threading.Tasks.Task.Factory.StartNew(delegate() 
                            { 
                                GC.Collect();

                                //очистить память
                                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                                    SetProcessWorkingSetSize(System.Diagnostics.Process.GetCurrentProcess().Handle, -1, -1);
                            });
                        }
                    }
                    break;

                case "Конфигуратор":
                    {
                        var page = RosControl.Helper.FindParentControl<Page>(contanier);
                        if (page != null)
                        {
                            //очистить все вкладки
                            if (page is IDisposable)
                                ((IDisposable)page).Dispose();

                            page.NavigationService.RemoveBackEntry();
                            page.NavigationService.Navigate(App.Конфигуратор);
                            page.DataContext = null;
                            page = null;

                            System.Threading.Tasks.Task.Factory.StartNew(delegate()
                            {
                                GC.Collect();

                                //очистить память
                                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                                    SetProcessWorkingSetSize(System.Diagnostics.Process.GetCurrentProcess().Handle, -1, -1);
                            });
                        }
                    }
                    break;

                case "Журналы":
                    {
                        RosControl.Helper.СоздатьОкно("ЖурналыПользователя", RosService.Data.Хранилище.Оперативное);
                    }
                    break;

                case "Отчеты":
                    {
                        RosControl.Helper.СоздатьОкно("ОтчетыПользователя", RosService.Data.Хранилище.Оперативное);
                    }
                    break;

                case "Справочники":
                    {
                        RosControl.Helper.СоздатьОкно("СправочникиПользователя", RosService.Data.Хранилище.Оперативное);
                    }
                    break;

                case "Проводник":
                    {
                        RosControl.Helper.СоздатьВкладку(
                            new RosControl.Forms.Проводник(),
                            "Проводник",
                            "Проводник",
                            true, true);
                    }
                    break;

                case "РедакторТиповДанных":
                    {
                        RosControl.Helper.СоздатьВкладку(
                            new Конфигуратор.ObjectBrowser(),
                            "ObjectBrowser",
                            "ObjectBrowser",
                            true, true);
                    }
                    break;

                case "Калькулятор":
                    Process.Start("calc");
                    break;

                case "Word":
                    Process.Start("winword");
                    break;

                case "Excel":
                    Process.Start("excel");
                    break;
            }
        }
        public static void Файл_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            switch (e.Parameter.ToString())
            {
                case "ЗавершениеРаботы":
                    {
                        ((RosControl.UI.Application)RosControl.UI.Application.Current).Logout();  

                        //Page page = RosControl.Helper.FindParentControl<Page>(sender as DependencyObject);
                        //if (page != null)
                        //{
                        //    Properties.Settings.Default.IsЗапомнить = false;
                        //    Properties.Settings.Default.Save();

                        //    if (page is IDisposable)
                        //        ((IDisposable)page).Dispose();

                        //    page.NavigationService.RemoveBackEntry();
                        //    page.NavigationService.Navigate(App.Авторизация);
                        //    System.Threading.Tasks.Task.Factory.StartNew(delegate() { GC.Collect(); });
                        //}
                    }
                    break;
            }
        }
        public static void Сервис_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            switch (e.Parameter.ToString())
            {                    
                case "ОткомпилироватьКонфигурацию":
                    {
                        var page = RosControl.Helper.FindParentControl<Page>(sender as DependencyObject);
                        try
                        {
                            if (page != null) page.Cursor = Cursors.Wait;
                            using (RosService.Client client = new RosService.Client())
                            {
                                client.Конфигуратор.КомпилироватьКонфигурацию(Client.Domain);
                                MessageBox.Show("Готово");
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                        }
                        finally
                        {
                            if (page != null) page.Cursor = null;
                        }
                    }
                    break;
            }
        }
    }
}
