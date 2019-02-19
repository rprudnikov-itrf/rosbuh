using System;
using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.IO.Packaging;
using System.Windows.Documents;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Threading;
using System.Windows.Media;
using System.Windows.Shapes;
using System.ServiceModel;
using System.Windows.Data;
using System.Windows.Controls.Primitives;
using RosControl.UI;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Collections.ObjectModel;


namespace RosApplication
{
    public partial class logon : Page
    {
        
        public static string Версия
        {
            get 
            {
                if (System.Deployment.Application.ApplicationDeployment.IsNetworkDeployed)
                {
                    return System.Deployment.Application.ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString();
                }
                else
                {
                    return System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString();
                }
            }
        }
        public static int ТекущийГод
        {
            get
            {
                return DateTime.Today.Year;
            }
        }

        public ObservableCollection<App.HistoryAccount> СписокПользователей
        {
            get { return (ObservableCollection<App.HistoryAccount>)GetValue(СписокПользователейProperty); }
            set { SetValue(СписокПользователейProperty, value); }
        }
        public static readonly DependencyProperty СписокПользователейProperty =
            DependencyProperty.Register("СписокПользователей", typeof(ObservableCollection<App.HistoryAccount>), typeof(logon), new UIPropertyMetadata(null));





        public string CurrentKeyboard
        {
            get { return (string)GetValue(CurrentKeyboardProperty); }
            set { SetValue(CurrentKeyboardProperty, value); }
        }
        public static readonly DependencyProperty CurrentKeyboardProperty =
            DependencyProperty.Register("CurrentKeyboard", typeof(string), typeof(logon), new UIPropertyMetadata(null));



        [Bindable(true)]
        public string Пользователь
        {
            get
            {
                //return Properties.Settings.Default.ИмяПользователя;
                return App.UserName;
            }
            set
            {
                if (App.UserName != value)
                {
                    App.UserName = value.Trim();
                    PART_ListBox.UnselectAll();
                    ОбновитьОкноВхода(false, false);
                }
                //if (Properties.Settings.Default.ИмяПользователя != value)
                //{
                //    Properties.Settings.Default.ИмяПользователя = value.Trim();
                //    PART_ListBox.UnselectAll();
                //    ОбновитьОкноВхода(false, false);
                //}
            }
        }
        [Bindable(true)]
        public static bool IsЗапомнить
        {
            get
            {
                return Properties.Settings.Default.IsЗапомнить;
            }
            set
            {
                if (Properties.Settings.Default.IsЗапомнить != value)
                {
                    Properties.Settings.Default.IsЗапомнить = value;
                }
            }
        }
        public string domain { get; set; }



        public int CurrentPage
        {
            get { return (int)GetValue(CurrentPageProperty); }
            set { SetValue(CurrentPageProperty, value); }
        }
        public static readonly DependencyProperty CurrentPageProperty =
            DependencyProperty.Register("CurrentPage", typeof(int), typeof(logon), new UIPropertyMetadata(0, new PropertyChangedCallback(CurrentPagePropertyChanged)));

        private static void CurrentPagePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var obj = d as logon;
            if (obj == null) return;

            switch (Convert.ToInt32(e.NewValue))
            {
                case 0:
                    obj.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                    {
                        if (obj.PART_Password != null)
                        {
                            obj.PART_Password.Focus();
                        }
                    });
                    break;

                case 1:
                    obj.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                    {
                        if (obj.Search != null)
                        {
                            obj.Search.Focus();
                        }
                    });
                    break;
            }
        }



        protected void ClearDefaultBackground()
        {
            ClearValue(DefaultBackgroundProperty);
            ClearValue(DefaultForegroundProperty);
            ClearValue(DefaultLinkForegroundProperty);
        }

        public Brush DefaultBackground
        {
            get { return (Brush)GetValue(DefaultBackgroundProperty); }
            set { SetValue(DefaultBackgroundProperty, value); }
        }
        public static readonly DependencyProperty DefaultBackgroundProperty =
            DependencyProperty.Register("DefaultBackground", typeof(Brush), typeof(logon), new UIPropertyMetadata(Brushes.White));

        public Brush DefaultForeground
        {
            get { return (Brush)GetValue(DefaultForegroundProperty); }
            set { SetValue(DefaultForegroundProperty, value); }
        }
        public static readonly DependencyProperty DefaultForegroundProperty =
            DependencyProperty.Register("DefaultForeground", typeof(Brush), typeof(logon), new UIPropertyMetadata(Brushes.Black));

        public Brush DefaultLinkForeground
        {
            get { return (Brush)GetValue(DefaultLinkForegroundProperty); }
            set { SetValue(DefaultLinkForegroundProperty, value); }
        }
        public static readonly DependencyProperty DefaultLinkForegroundProperty =
            DependencyProperty.Register("DefaultLinkForeground", typeof(Brush), typeof(logon), new UIPropertyMetadata(Brushes.Silver));






        #region Автоматически выделять текст
        private static void SelectAllText(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is System.Windows.Controls.TextBox)
                ((System.Windows.Controls.TextBox)e.OriginalSource).SelectAll();
            else if (e.OriginalSource is PasswordBox)
                ((PasswordBox)e.OriginalSource).SelectAll();
        }
        #endregion


        public logon()
        {
            InitializeComponent();

            AddHandler(System.Windows.Controls.TextBox.GotKeyboardFocusEvent, new RoutedEventHandler(SelectAllText), true);
            AddHandler(PasswordBox.GotKeyboardFocusEvent, new RoutedEventHandler(SelectAllText), true);
        }
        private void Page_Initialized(object sender, EventArgs e)
        {
            App.SessionStop();
            RosService.Client.Shutdown();

            ОбновитьОкноВхода(false, false);
            //foreach (var item in App.HistoryAccounts)
            //    СписокПользователей.Add(item);

            СписокПользователей = new ObservableCollection<App.HistoryAccount>(App.HistoryAccounts);
            //Пользователь = Properties.Settings.Default.ИмяПользователя;
            Пользователь = App.UserName;
            PART_ListBox.UnselectAll();
        }
        private void Вход_Click(object sender, RoutedEventArgs e)
        {
            //var factory = new System.ServiceModel.Web.WebChannelFactory<RosService.Configuration.IConfigurationChannel>(new Uri("http://itrf.ru:8080/RosService/Configuration/basic"));
            //var Proxy = factory.CreateChannel();
            //var user = Proxy.Авторизация("Техподдержка", "nfrcb", false, "ювтранс");
            //if (user != null)
            //{
            //}


            //var xml = System.Xml.Linq.XDocument.Parse(RosControl.Properties.Resources.country);
            //foreach (var item in xml.Descendants("Country"))
            //{
            //    var name = item.Element("Eng").Value.ToLower();
            //    if (!string.IsNullOrEmpty(name))
            //    {
            //        try
            //        {
            //            using (var web = new System.Net.WebClient())
            //            {
            //                web.DownloadFile(string.Format("http://flags.redpixart.com/img/{0}_preview.gif", name), string.Format("R:\\111\\{0}.gif", name));
            //            }
            //        }
            //        catch
            //        {
            //        }
            //    }
            //}


            //using (var client = new HyperСloud.Client("регистр@localhost"))
            //{
            //    //client.Set(254388, "GuidCode", Guid.NewGuid().ToString());
            //    //client.Set(254388, "GuidCode", Guid.NewGuid().ToString());
            //    //client.Set(254388, "GuidCode", Guid.NewGuid().ToString());
            //    //client.Set(254388, "GuidCode", Guid.NewGuid().ToString());
            //}
            //try
            //{
            //    using (var client = new HyperСloud.Files("азбука@crmapp.azb24.ru"))
            //    //using (var client = new HyperСloud.Files("росинфотех@itrf.ru"))
            //    {
            //        var file = File.ReadAllBytes("r:\\1.jpg");
            //        client.Set(1, "1.jpg", file);
            //    }

            //    //using (var client = new HyperСloud.Client("росинфотех"))
            //    //{
            //    //    var types = client.Get<string>(1, "НазваниеОбъекта");
            //    //    if (types != null)
            //    //    {
            //    //    }
            //    //}

            //    //using (var client = new HyperСloud.Config("росинфотех"))
            //    //{
            //    //    var types = client.GetTypes();
            //    //    if (types != null)
            //    //    {
            //    //    }
            //    //}
            //}
            //catch (Exception ex)
            //{
            //    if (ex != null)
            //    {
            //    }
            //}

            
            //RosService.Client.Host = Properties.Settings.Default.АдресСервера;
            RosService.Client.maxService = 1;
            RosService.Client.Host = App.ServerName;

            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                try
                {
                    var mode = RosService.ClientMode.None;
                    if (Enum.TryParse<RosService.ClientMode>(Properties.Settings.Default.ТипСоединения, out mode))
                        RosService.Client.Mode = mode;

  
                    if (RosService.Client.Authorization(Пользователь, PART_Password.Password, true)
                        && RosService.Client.User.id_node > 0)
                    {
                        using (RosService.Client client = new RosService.Client())
                        {
                            App.AppName = client.Архив.ПолучитьЗначение<string>("КорневойРаздел", "НазваниеОбъекта");
                        }

                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                        {
                            try
                            {
                                #region Сохранить настройки после входа
                                if (Пользователь.Contains("@"))
                                {
                                    //Properties.Settings.Default.ИмяПользователя = string.Format("{0}\\{1}@{2}", RosService.Client.Domain, RosService.Client.UserName, RosService.Client.Host);
                                    App.UserName = string.Format("{0}\\{1}@{2}", RosService.Client.Domain, RosService.Client.UserName, RosService.Client.Host);
                                }
                                else
                                {
                                    //Properties.Settings.Default.ИмяПользователя = string.Format("{0}\\{1}", RosService.Client.Domain, RosService.Client.UserName);
                                    App.UserName = string.Format("{0}\\{1}", RosService.Client.Domain, RosService.Client.UserName);
                                }

                                var userName = App.UserName ?? string.Empty;
                                if (IsЗапомнить) Properties.Settings.Default.Пароль = PART_Password.Password;
                                var account = App.HistoryAccounts.FirstOrDefault(p => p.Login.ToLower().Equals(userName.ToLower()));
                                if (account == null)
                                {
                                    account = new App.HistoryAccount() { Login = userName, Версия = RosApplication.Properties.Settings.Default.Версия };
                                    App.HistoryAccounts.Add(account);
                                    App.SaveHistoryAccounts();
                                }
                                System.Threading.Tasks.Task.Factory.StartNew(() =>
                                {
                                    try
                                    {
                                        Properties.Settings.Default.Save();

                                        if (account != null)
                                        {
                                            //using (RosService.Client client = new RosService.Client())
                                            {
                                                //App.AppName = client.Архив.ПолучитьЗначение<string>("КорневойРаздел", "НазваниеОбъекта");
                                                if (!string.IsNullOrEmpty(App.AppName) && !"Предприятие".Equals(App.AppName)
                                                    && account.Group != App.AppName)
                                                {
                                                    account.Group = App.AppName;
                                                    App.SaveHistoryAccounts();
                                                }
                                            }
                                        }
                                    }
                                    catch
                                    {
                                    }
                                });
                                #endregion

                                //включить автоматическое продление сессии
                                App.SessionStart();

                                this.NavigationService.RemoveBackEntry();
                                this.NavigationService.Navigate(App.ГлавнаяФорма);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(ex.Message, "Ошибка авторизации", MessageBoxButton.OK);
                                PART_Password.Focus();
                                КнопкаВход.EndCallback();
                            }
                        });
                    }
                    //else if (RosService.Client.User.id_node == -1)
                    //{
                    //    //redirect to 5.0
                    //    RosService.Client.Shutdown();
                    //    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                    //    {
                    //        if (PART_User.SelectedItem is App.HistoryAccount)
                    //        {
                    //            ((App.HistoryAccount)PART_User.SelectedItem).Версия = 5;
                    //            App.SaveHistoryAccounts(СписокПользователей);
                    //        }
                    //        else
                    //        {
                    //            Properties.Settings.Default.Версия = 5;
                    //        }

                    //        Вход_Click(sender, e);
                    //    });
                    //}
                    else
                    {
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                        {
                            КнопкаВход.EndCallback();
                        });
                    }
                }
                catch (Exception ex)
                {
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                    {
                        MessageBox.Show(ex.Message, "Ошибка авторизации", MessageBoxButton.OK);
                        КнопкаВход.EndCallback();
                    });
                }
            });
        }
        private void Настройки_Click(object sender, RoutedEventArgs e)
        {
            new Клиент.НастройкиАвторизации().ShowDialog();

            //window.Owner = Application.Current.MainWindow;
            //if (window.ShowDialog().Value)
            //{
            //    if (window.Accounts.IsEdit)
            //    {
            //        СписокПользователей.Clear();
            //        foreach (var item in (ObservableCollection<App.HistoryAccount>)window.Accounts.ItemsSource)
            //            СписокПользователей.Add(item);
            //    }
            //}
        }

        private static readonly object lockobj = new object();
        private void ОбновитьОкноВхода(bool IsUpdate, bool IsDelete)
        {
            //if (IsUpdate)
            ClearDefaultBackground();


            var IsEmpty = DefaultBackground == DefaultBackgroundProperty.DefaultMetadata.DefaultValue;
            //ThreadPool.QueueUserWorkItem((WaitCallback)delegate
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                lock (lockobj)
                {
                    try
                    {
                        if (!(Пользователь.Contains(@"\") || Пользователь.Contains(@"/"))) throw new Exception();
                        var token = Regex.Match(Пользователь, @"^(?<DOMAIN>(.+?))[\\/](?<USER>(.+?))(@(?<HOST>(.+)))?$", RegexOptions.Compiled | RegexOptions.Singleline);
                        if (domain == token.Groups["DOMAIN"].Value.ToLower() && !IsEmpty) return;
                        domain = token.Groups["DOMAIN"].Value.ToLower();
                        var host = !string.IsNullOrEmpty(token.Groups["HOST"].Value) ? token.Groups["HOST"].Value : RosService.Client.Host;
                        
                        using (var data = new RosService.Data.DataClient(RosService.Client.DefaultBinding, new EndpointAddress(string.Format(RosService.Client.GetUrl(host, RosService.ClientMode.GZip), "Data"))))
                        using (var file = new RosService.Files.FileClient(RosService.Client.DefaultStreamingBinding, new EndpointAddress(string.Format(RosService.Client.GetUrlStreaming(host, RosService.ClientMode.GZip), "File"))))
                        {
                            var filestreeam = null as byte[];
                            var colors = null as string[];

                            var logonPath = System.IO.Path.Combine(App.AppPath, "logon");
                            if (!System.IO.Directory.Exists(logonPath))
                                System.IO.Directory.CreateDirectory(logonPath);

                            var path_logon = System.IO.Path.Combine(logonPath, string.Format("logon_{0}.png", domain));
                            var path_style = System.IO.Path.Combine(logonPath, string.Format("style_{0}.css", domain));
                            if (IsDelete) 
                                File.Delete(path_logon);

                            if (File.Exists(path_logon))
                            {
                                filestreeam = File.ReadAllBytes(path_logon);
                                if (File.Exists(path_style))
                                {
                                    colors = File.ReadAllText(path_style).Split(';');
                                }
                            }
                            else
                            {
                                var ОкноАвторизации = data.ПоискРазделаПоИдентификаторуОбъекта("ОкноАвторизации", RosService.Data.Хранилище.Конфигурация, domain);
                                filestreeam = file.ПолучитьФайлПолностьюПоНазванию(ОкноАвторизации, "Logon", RosService.Files.Хранилище.Конфигурация, domain);
                                if (file != null && filestreeam != null && filestreeam.Length > 0)
                                {
                                    File.WriteAllBytes(path_logon, filestreeam);

                                    var values = data.ПолучитьЗначение(ОкноАвторизации, new string[] { "ЦветТекста", "ЦветСсылок" }, RosService.Data.Хранилище.Конфигурация, domain);
                                    colors = values.Select(p => Convert.ToString(p.Value.Значение)).ToArray();
                                    File.WriteAllText(path_style, string.Join(";", colors));
                                }
                                else
                                {
                                    throw new Exception();
                                }
                            }

                            var src = new BitmapImage();
                            src.BeginInit();
                            src.StreamSource = new MemoryStream(filestreeam);
                            src.EndInit();
                            src.Freeze();
                            this.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, (Action)delegate
                            {
                                try
                                {
                                    DefaultBackground = new ImageBrush(src) { Stretch = Stretch.None, TileMode = TileMode.None };
                                    
                                    if (colors != null && colors.Length >= 1 && !string.IsNullOrEmpty(colors[0]))
                                        DefaultForeground = new BrushConverter().ConvertFromString(colors[0]) as Brush;
                                    else
                                        ClearValue(DefaultForegroundProperty);

                                    if (colors != null && colors.Length >= 2 && !string.IsNullOrEmpty(colors[1]))
                                        DefaultLinkForeground = new BrushConverter().ConvertFromString(colors[1]) as Brush;
                                    else
                                        ClearValue(DefaultLinkForegroundProperty);
                                }
                                catch
                                {
                                    ClearDefaultBackground();
                                }
                            });
                        }
                    }
                    catch
                    {
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate { ClearDefaultBackground(); });
                    }
                }
            });
        }
        //private void ListBox_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        //{
        //    //Пользователь = ((System.Windows.Controls.ListBox)sender).SelectedValue as string;
        //    PART_User.Text = ((System.Windows.Controls.ListBox)sender).SelectedValue as string;
        //    CurrentPage = 0;
        //    if (PART_Password != null) PART_Password.Focus();
        //}
        private void DocumentsTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (((Selector)sender).SelectedValue as string == "Help" &&
                ((DocumentsTabItem)((Selector)sender).SelectedItem).Content == null)
            {
                ((DocumentsTabItem)((Selector)sender).SelectedItem).Content = new WebBrowser()
                {
                    Source = new Uri("http://itrf.ru/page.aspx?id_page=256405")
                };
            }
        }

        private void ОбновитьИзображение_Executed(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            ОбновитьОкноВхода(true, true);
            e.Handled = true;
        }

        private void УдалитьВход_Executed(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            var item = RosControl.Helper.FindParentControl<ListBoxItem>(e.OriginalSource as DependencyObject);
            if (item == null || СписокПользователей == null) return;

            //var row = PART_Page.ItemContainerGenerator.ItemFromContainer(item) as App.HistoryAccount;
            var row = item.DataContext as App.HistoryAccount;
            СписокПользователей.Remove(row);
            App.HistoryAccounts.Remove(row);
            App.SaveHistoryAccounts();
        }

        private void UserSource_Filter(object sender, FilterEventArgs e)
        {
            if (string.IsNullOrEmpty(Search.Text) || e.Item == null)
            {
                e.Accepted = true;
                return;
            }
            e.Accepted = Convert.ToString((e.Item as App.HistoryAccount).Login).ToLower().Contains(Search.Text.ToLower());
        }
        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, (Action)delegate
            {
                var cs = TryFindResource("UserSourceGroup") as CollectionViewSource;
                if (cs != null) cs.View.Refresh();
            });
        }
        private void СоздатьНовыйАккаунт(object sender, RoutedEventArgs e)
        {
            new RosApplication.Клиент.СоздатьНовыйАккаунт().ShowDialog();
        }

        #region RU & EN
        private void KeyboardChange()
        {
            CurrentKeyboard = System.Windows.Input.InputLanguageManager.Current.CurrentInputLanguage.TwoLetterISOLanguageName.ToUpper();
        }
        private void PART_Password_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (Keyboard.IsKeyUp(Key.LeftCtrl) || Keyboard.IsKeyUp(Key.RightCtrl)
                || Keyboard.IsKeyUp(Key.LeftAlt) || Keyboard.IsKeyUp(Key.RightAlt))
            {
                KeyboardChange();
            }
        }
        private void PART_Password_IsKeyboardFocusedChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Convert.ToBoolean(e.NewValue))
            {
                KeyboardChange();
            }
        }
        #endregion
        
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
            {
                PART_Password.Focus();
            }
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = ((System.Windows.Controls.ListBox)sender).SelectedValue;
            if (item != null)
            {
                PART_User.Text = item as string;
                CurrentPage = 0;

                if (PART_Password != null)
                    PART_Password.Focus();
            }
        }
    }

    public class LogonImageConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var token = System.Convert.ToString(value, culture).Trim().Split(@"\/".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            if (token.Length < 1) return null;

            var logonPath = System.IO.Path.Combine(App.AppPath, "logon");
            var path = System.IO.Path.Combine(logonPath, string.Format("logon_{0}.png", token.First()));
            if (File.Exists(path))
            {
                var src = new BitmapImage();
                src.BeginInit();
                src.CacheOption = BitmapCacheOption.OnLoad;
                src.CreateOptions = BitmapCreateOptions.None;
                src.StreamSource = new MemoryStream(File.ReadAllBytes(path));
                src.EndInit();
                return src;
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
    public class UserNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var token = System.Convert.ToString(value, culture).Trim().Split(@"\/".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            return token.ElementAtOrDefault(System.Convert.ToInt32(parameter));
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
