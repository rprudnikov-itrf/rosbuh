using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;
using RosControl.Forms.Пользователи;
using RosControl.UI;
using RosService.Data;
using System.Xml.Linq;


namespace RosApplication.Клиент
{
    public partial class Main : Page, IDisposable
    {
        #region ПоказатьРабочийСтол
        public bool ПоказатьРабочийСтол
        {
            get { return (bool)GetValue(ПоказатьРабочийСтолProperty); }
            set { SetValue(ПоказатьРабочийСтолProperty, value); }
        }
        public static readonly DependencyProperty ПоказатьРабочийСтолProperty =
            DependencyProperty.Register("ПоказатьРабочийСтол", typeof(bool), typeof(Main), new UIPropertyMetadata(false));
        #endregion

        #region ПользовательскоеМеню
        public object ПользовательскоеМеню
        {
            get { return (object)GetValue(ПользовательскоеМенюProperty); }
            set { SetValue(ПользовательскоеМенюProperty, value); }
        }
        public static readonly DependencyProperty ПользовательскоеМенюProperty =
            DependencyProperty.Register("ПользовательскоеМеню", typeof(object), typeof(Main), new UIPropertyMetadata(null));
        #endregion

        public bool IsTaxi
        {
            get
            {
                return "Рос.Такси".Equals(App.AppName);
            }
        }

        public Main()
        {
            //пользователи
            _списокПользователей = new ObservableCollection<РабочаОбластьПользователя>();

            InitializeComponent();

            //создать привязки комманд
            RosApplication.Command.CommandBinding.SetCommandBinding(this);

            #region создать таймер для обновления пользователей
            if (!IsTaxi)
            {
                _timerUpdateUsers = new DispatcherTimer(DispatcherPriority.ContextIdle) { Interval = TimeSpan.FromMinutes(60) };
                _timerUpdateUsers.Tick += (object sender, EventArgs e) => { ОбновитьСписокПользователей(); };

                _timerUpdateMasseges = new DispatcherTimer(DispatcherPriority.ContextIdle) { Interval = TimeSpan.FromMinutes(25) };
                _timerUpdateMasseges.Tick += (object sender, EventArgs e) => { ОбновитьСписокСообщений(); };
            }
            #endregion

            //Application.Current.MainWindow.Closing += new CancelEventHandler(MainWindow_Closing);
            //new RosService.Client().Архив.СохранитьКешЗначение("1", 1m);
            //new RosService.Client().Архив.ПолучитьКешЗначение("1", RosService.Client.Domain);

            if(string.Equals("Техподдержка", RosService.Client.UserName, StringComparison.CurrentCultureIgnoreCase)
                || string.Equals("ЛокальнаяСистема", RosService.Client.UserName, StringComparison.CurrentCultureIgnoreCase))
                menu_Configurator.Visibility = System.Windows.Visibility.Visible;
        }

        private void Page_Initialized(object sender, EventArgs e)
        {
            Title = string.Format(@"{0}\{1}", RosService.Client.Domain, RosService.Client.UserName);

            //загрузить основное меню
            ЗагрузитьМеню();

            //загрузить веб-сервисы
            RosControl.Compile.CodeCompiler.BuildWebServices();
        }
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
            {
                if (!IsTaxi)
                {
                    PART_Сообщения.Visibility = System.Windows.Visibility.Visible;

                    #region Загрузить пользователей
                    System.Threading.Tasks.Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            Thread.Sleep(60000);
                            ОбновитьСписокПользователей();
                        }
                        catch
                        {
                        }
                    });
                    #endregion
                }

                //if (RosService.Client.User.Права.ЗапретитьРаботуСЗадачами
                //    && RosService.Client.User.Права.ЗапретитьРаботуСПочтой)
                //    Separator1.Visibility = System.Windows.Visibility.Collapsed;
            }
        }


        public void ЗагрузитьМеню()
        {
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        using (RosService.Client client = new RosService.Client())
                        {
                            if (client.СведенияПользователя.ГруппаРаздел == 0) 
                                throw new Exception("Интерфейс пользователя не задан в настройках");

                            var xamlLoaded = client.Архив.ПолучитьЗначение<string>(client.СведенияПользователя.ГруппаРаздел, "ИнтерфейсКод", Хранилище.Оперативное);
                            if (string.IsNullOrEmpty(xamlLoaded))
                            {
                                var query = new RosService.Data.Query();
                                query.ДобавитьУсловиеПоиска("НазваниеОбъекта", "БыстроеМеню", Query.Оператор.Равно);
                                query.ДобавитьМестоПоиска(client.СведенияПользователя.ГруппаРаздел, 1);
                                var БыстроеМеню = client.Архив.Поиск(query, Хранилище.Оперативное).AsEnumerable().SingleOrDefault();
                                xamlLoaded = client.Архив.ПолучитьЗначение<string>(БыстроеМеню.Field<decimal>("id_node"), "XamlКод", Хранилище.Оперативное);
                            }

                            if (string.IsNullOrEmpty(xamlLoaded))
                                return;

                            this.Dispatcher.Invoke(DispatcherPriority.Loaded, (Action<string>)delegate(string xaml)
                            {
                                try
                                {
                                    //var menu = System.Windows.Markup.XamlReader.Load(new System.Xml.XmlTextReader(new System.IO.StringReader(xaml))) as FrameworkElement;
                                    //if (menu is System.Windows.Controls.TabControl)
                                    if (xaml.StartsWith("<TabControl"))
                                    {
                                        //var tab = menu as System.Windows.Controls.TabControl;
                                        var oRow = PART_DocumentsHost.Items.Count - 1;
                                        foreach (var item in System.Xml.Linq.XDocument.Parse(xaml).Root.Elements())
                                        {
                                            var tag = item.Attribute("Tag").Value;
                                            if ("@@Главная".Equals(tag))
                                            {
                                                //ПользовательскоеМеню = RosControl.Helper.CloneElement<DependencyObject>(item.Content as DependencyObject);
                                                ПользовательскоеМеню = XamlReader.Parse(item.FirstNode.ToString()) as FrameworkElement;
                                            }
                                            else
                                            {
                                                var __Icon = null as object;
                                                var strIcon = TryGetValue(item, "Icon");
                                                if (!string.IsNullOrEmpty(strIcon))
                                                    __Icon = new FilePreview() { Mode = FilePreviewMode.Icon, id_node = strIcon };
                                                
                                                PART_DocumentsHost.Items.Insert(oRow++, new DocumentsTabItem()
                                                {
                                                    Header = TryGetValue(item, "Header"),
                                                    Tag = tag,
                                                    IsReadOnly = true,
                                                    Icon = __Icon,
                                                    MinWidth = 0,
                                                    ToolTip = TryGetValue(item, "ToolTip"),
                                                    Navigation = XamlReader.Parse(item.FirstNode.ToString()) as FrameworkElement
                                                });
                                            }
                                        }
                                    }
                                    else
                                    {
                                        ПользовательскоеМеню = XamlReader.Parse(xaml) as FrameworkElement;
                                    }
                                    ЗагрузитьВкладки();
                                }
                                catch (Exception ex)
                                {
                                    ПользовательскоеМеню = new TextBlock()
                                    {
                                        Margin = new Thickness(10),
                                        TextWrapping = TextWrapping.Wrap,
                                        TextAlignment = TextAlignment.Center,
                                        Text = ex.Message
                                    };
                                }
                            }, xamlLoaded);
                            break;
                        }
                    }
                    catch (TimeoutException)
                    {
                        System.Threading.Thread.Sleep(1000);
                        continue;
                    }
                    catch (Exception ex)
                    {
                        this.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, (Action)delegate
                        {
                            ПользовательскоеМеню = new TextBlock()
                            {
                                Margin = new Thickness(10),
                                TextWrapping = TextWrapping.Wrap,
                                TextAlignment = TextAlignment.Center,
                                Text = ex.Message
                            };
                        });
                    }
                }
            });

        }
        private void NavigationPanel_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var obj = e.Source as RosControl.UI.TreeViewItem;
            if (obj == null) return;

            if (obj.ОткрытьВОкне)
                RosControl.Helper.СоздатьОкно(obj.Tag, Хранилище.Оперативное, 0);
            else
            {
                RosControl.Helper.СоздатьВкладку(obj.Tag, Хранилище.Оперативное, false);
            }
        }
        private string TryGetValue(XElement element, string key)
        {
            var a = element.Attribute(key);
            if (a == null)
                return null;

            return a.Value;
        }

        #region Вкладки
        public void СохранитьВкладки()
        {
            if (Properties.Settings.Default.СохранятьОткрытыеВкладки 
                && PART_DocumentsHost != null
                && PART_DocumentsHost.Items.Count > 0)
            {
                if (Properties.Settings.Default.Pages == null)
                    Properties.Settings.Default.Pages = new System.Collections.Specialized.StringCollection();
                
                foreach (var item in Properties.Settings.Default.Pages.Cast<string>().Where(p => p.StartsWith(RosService.Client.Domain + ":")).ToArray())
                    Properties.Settings.Default.Pages.Remove(item);

                var items = PART_DocumentsHost.Items.Cast<RosControl.UI.DocumentsTabItem>()
                    .Where(p => !p.IsReadOnly && p.Content is RosControl.UI.КонтентПанель)
                    .Select(p => string.Format("{0}://{1:f0}?{2}", RosService.Client.Domain, ((RosControl.UI.КонтентПанель)p.Content).id_node, ((RosControl.UI.КонтентПанель)p.Content).Тип))
                    .ToArray();
                Properties.Settings.Default.Pages.AddRange(items);
                Properties.Settings.Default.Save();
            }
        }
        public void ЗагрузитьВкладки()
        {
            if (Properties.Settings.Default.Pages == null ||
                !Properties.Settings.Default.СохранятьОткрытыеВкладки) 
                return;

            var pagesSource = Properties.Settings.Default.Pages.Cast<string>().Where(p => p.StartsWith(RosService.Client.Domain + ":"));
            pagesSource.Reverse();
            foreach (var item in pagesSource)
            {
                var tab = System.Text.RegularExpressions.Regex.Match(item, @"://(?<id_node>.+?)\?(?<type>.+)", System.Text.RegularExpressions.RegexOptions.Singleline);
                if (tab == null) 
                    continue;

                var id_node = 0m;
                if (decimal.TryParse(tab.Groups["id_node"].Value, out id_node) && id_node > 0)
                {
                    RosControl.Helper.СоздатьВкладку(id_node, Хранилище.Оперативное, tab.Groups["type"].Value, true, false);
                }
                else
                {
                    RosControl.Helper.СоздатьВкладку((object)tab.Groups["type"].Value, Хранилище.Оперативное, true, false);
                }
            }
        }
        #endregion

        #region Пользователи
        public class РабочаОбластьПользователя : INotifyPropertyChanged
        {
            public decimal id_node
            {
                get
                {
                    if (Пользователь == null) return 0;
                    return Пользователь.id_node;
                }
            }

            private RosService.Services.СведенияПользователя _Пользователь;
            public RosService.Services.СведенияПользователя Пользователь
            {
                get { return _Пользователь; }
                set { _Пользователь = value; NotifyPropertyChanged("Пользователь"); }
            }

            private int _Сообщения;
            public int Сообщения
            {
                get { return _Сообщения; }
                set { _Сообщения = value; NotifyPropertyChanged("Сообщения"); }
            }

            //private RosService.Services.ЗадачиПользователя _Задачи;
            //public RosService.Services.ЗадачиПользователя Задачи
            //{
            //    get { return _Задачи; }
            //    set { _Задачи = value; NotifyPropertyChanged("Задачи"); }
            //}

            #region INotifyPropertyChanged Members
            public event PropertyChangedEventHandler PropertyChanged;
            protected void NotifyPropertyChanged(string property)
            {
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(property));
                }
            }
            #endregion

            public override bool Equals(object obj)
            {
                var item = obj as РабочаОбластьПользователя;
                if(item == null) return false;
                return id_node.Equals(item.id_node);
            }
            public override int GetHashCode()
            {
                return id_node.GetHashCode();
            }
        };
        protected static object lookobject = new object();
        private DispatcherTimer _timerUpdateUsers;


        private ObservableCollection<РабочаОбластьПользователя> _списокПользователей;
        public ObservableCollection<РабочаОбластьПользователя> СписокПользователей
        {
            get { return _списокПользователей; }
        }

        private void Обновить(object sender, ExecutedRoutedEventArgs e)
        {
            switch (Convert.ToString(e.Parameter))
            {
                //case "Задачи":
                //    ОбновитьСписокЗадач();
                //    break;

                case "Пользователи":
                    ОбновитьСписокПользователей();
                    break;
            }
        }
        private void ОбновитьСписокПользователей()
        {
            if (_timerUpdateUsers != null)
                _timerUpdateUsers.Stop();

            if (IsTaxi)
                return;

            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                lock (lookobject)
                {
                    try
                    {
                        using (RosService.Client client = new RosService.Client())
                        {
                            var users = client.Сервисы.Пользователи_Список(client.Пользователь, client.Домен);
                            this.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, (Action)delegate
                            {
                                try
                                {
                                    if (СписокПользователей == null)
                                        return;

                                    var _userHash = СписокПользователей.ToDictionary(p => p.id_node);
                                    foreach (var item in users)
                                    {
                                        if (!_userHash.ContainsKey(item.id_node))
                                        {
                                            СписокПользователей.Add(new РабочаОбластьПользователя() { Пользователь = item });
                                        }
                                        else
                                        {
                                            _userHash[item.id_node].Пользователь = item;
                                        }
                                    }

                                    //удалить не нужных
                                    var u = СписокПользователей.Select(p => p.id_node).Except(users.Select(p => p.id_node)).ToArray();
                                    foreach (var item in u)
                                    {
                                        var _user = СписокПользователей.FirstOrDefault(p => p.id_node == item);
                                        if (_user == null) continue;
                                        СписокПользователей.Remove(_user);
                                    }

                                    #region ICQ & Task
                                    ОбновитьСписокСообщений();

                                    //if (!RosService.Client.User.Права.ЗапретитьРаботуСЗадачами)
                                    //{
                                    //    ОбновитьСписокЗадач();
                                    //}
                                    #endregion
                                }
                                catch(Exception ex)
                                {
                                    MessageBox.Show(ex.Message);
                                }
                            });
                        }
                    }
                    catch
                    {
                    }
                    finally
                    {
                        if (_timerUpdateUsers != null)
                            _timerUpdateUsers.Start();
                    }
                }
            });
        }
        #endregion

        #region ICQ
        private DispatcherTimer _timerUpdateMasseges;

        #region propertys
        public object КоличествоНовыхСообщений
        {
            get { return (object)GetValue(КоличествоНовыхСообщенийProperty); }
            set { SetValue(КоличествоНовыхСообщенийProperty, value); }
        }
        public static readonly DependencyProperty КоличествоНовыхСообщенийProperty =
            DependencyProperty.Register("КоличествоНовыхСообщений", typeof(object), typeof(Main), new UIPropertyMetadata(null));

        public void ОбновитьКоличествоНовыхСообщений()
        {
            if (СписокПользователей == null)
            {
                КоличествоНовыхСообщений = 0;
            }
            else
            {
                var sum = СписокПользователей.Sum(p => p.Сообщения);
                var new_count = sum > 0 ? (object)sum : null;
                if (new_count != null && Convert.ToInt32(new_count) > Convert.ToInt32(КоличествоНовыхСообщений ?? 0))
                {
                    try
                    {
                        System.Media.SystemSounds.Asterisk.Play();
                    }
                    catch
                    {
                    }
                }
                КоличествоНовыхСообщений = new_count;
                //if (sum > 0 && !Application.Current.MainWindow.IsActive)
                //{
                //    Application.Current.MainWindow.Activate();
                //}
            }

            if (PART_Сообщения.Content is RosApplication.Клиент.Чат.МоиСообщения)
            {
                var cs = ((RosApplication.Клиент.Чат.МоиСообщения)PART_Сообщения.Content).TryFindResource("MessagesSource") as CollectionViewSource;
                if (cs != null) cs.View.Refresh();
            }
        }
        #endregion

        public void ОбновитьСписокСообщений()
        {
            if (_timerUpdateMasseges != null)
                _timerUpdateMasseges.Stop();

            if (IsTaxi)
                return;

            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                lock (lookobject)
                {
                    try
                    {
                        using (RosService.Client client = new RosService.Client())
                        {
                            var users = client.Сервисы.СообщенияПользователя_Список(client.СведенияПользователя.id_node, client.Пользователь, client.Домен)
                                .ToDictionary(p => p.id_node);
                            this.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, (Action)delegate
                            {
                                try
                                {
                                    if (СписокПользователей == null) return;
                                    foreach (var item in СписокПользователей)
                                    {
                                        if (users.ContainsKey(item.id_node))
                                            item.Сообщения = users[item.id_node].Количество;
                                        else
                                            item.Сообщения = 0;
                                    }
                                    ОбновитьКоличествоНовыхСообщений();
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show(ex.Message);
                                }
                            });
                        }
                    }
                    catch
                    {
                        
                    }
                    finally
                    {
                        if (_timerUpdateMasseges != null)
                            _timerUpdateMasseges.Start();
                    }
                }
            });
        }
        #endregion

        #region Задачи
        //private DispatcherTimer _timerUpdateTasks;

        //public object КоличествоНовыхЗадач
        //{
        //    get { return (object)GetValue(КоличествоНовыхЗадачProperty); }
        //    set { SetValue(КоличествоНовыхЗадачProperty, value); }
        //}
        //public static readonly DependencyProperty КоличествоНовыхЗадачProperty =
        //    DependencyProperty.Register("КоличествоНовыхЗадач", typeof(object), typeof(Main), new UIPropertyMetadata(null));

        //private void ОбновитьКоличествоНовыхЗадач()
        //{
        //    if (СписокПользователей == null)
        //    {
        //        КоличествоНовыхСообщений = 0;
        //    }
        //    else if (PART_Задачи.Content is RosApplication.Клиент.Задачи.МоиЗадачи)
        //    {
        //        var МоиЗадачи = PART_Задачи.Content as RosApplication.Клиент.Задачи.МоиЗадачи;

        //        var taskNode = СписокПользователей.FirstOrDefault(p => p.id_node == RosService.Client.User.id_node);
        //        МоиЗадачи.КИсполнению = КоличествоНовыхЗадач = taskNode != null && taskNode.Задачи != null && taskNode.Задачи.Количество > 0 ? (object)taskNode.Задачи.Количество : null;
        //        МоиЗадачи.Срочные = taskNode != null && taskNode.Задачи != null && taskNode.Задачи.Срочные > 0 ? (object)taskNode.Задачи.Срочные : null;
        //        МоиЗадачи.Входящие = taskNode != null && taskNode.Задачи != null && taskNode.Задачи.Новые > 0 ? (object)taskNode.Задачи.Новые : null; ;


        //        var cs = МоиЗадачи.TryFindResource("TasksSource") as CollectionViewSource;
        //        if (cs != null) cs.View.Refresh();

        //        //if (sum > 0 && !Application.Current.MainWindow.IsActive)
        //        //{
        //        //    Application.Current.MainWindow.Activate();
        //        //}
        //    }
        //}
        //public void ОбновитьСписокЗадач()
        //{
        //    if(_timerUpdateTasks != null) 
        //        _timerUpdateTasks.Stop();

        //    System.Threading.Tasks.Task.Factory.StartNew(() =>
        //    {
        //        lock (lookobject)
        //        {
        //            try
        //            {
        //                using (RosService.Client client = new RosService.Client())
        //                {
        //                    var tasks = client.Сервисы.ЗадачаПользователя_Список(client.СведенияПользователя.id_node, client.Пользователь, client.Домен)
        //                        .ToDictionary(p => p.id_node);
        //                    this.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, (Action)delegate
        //                    {
        //                        try
        //                        {
        //                            if (СписокПользователей == null) return;
        //                            foreach (var item in СписокПользователей)
        //                            {
        //                                if (tasks.ContainsKey(item.id_node))
        //                                    item.Задачи = tasks[item.id_node];
        //                                else
        //                                    item.Задачи = null;
        //                            }
        //                            ОбновитьКоличествоНовыхЗадач();
        //                        }
        //                        catch (Exception ex)
        //                        {
        //                            MessageBox.Show(ex.Message);
        //                        }
        //                    });
        //                }
        //            }
        //            catch
        //            {
        //            }
        //            finally
        //            {
        //                if (_timerUpdateTasks != null)
        //                    _timerUpdateUsers.Start();
        //            }
        //        }
        //    });
        //}
        //private void ContextMenu_Click(object sender, RoutedEventArgs e)
        //{
        //    if (e.OriginalSource is MenuItem && ((MenuItem)e.OriginalSource).DataContext is РабочаОбластьПользователя)
        //    {
        //        var user = (РабочаОбластьПользователя)((MenuItem)e.OriginalSource).DataContext;
        //        RosApplication.Command.CommandBinding.СоздатьЗадачу(
        //            new ВыбранныйПользователь[] 
        //            {  
        //                new ВыбранныйПользователь() { id_node = user.id_node, НазваниеОбъекта = user.Пользователь.НазваниеОбъекта} 
        //            });
        //    }
        //}
        #endregion

        public void Dispose()
        {
            if (_timerUpdateUsers != null)
                _timerUpdateUsers.Stop();

            //if (_timerUpdateTasks != null)
            //    _timerUpdateTasks.Stop();

            if (_timerUpdateMasseges != null)
                _timerUpdateMasseges.Stop();

            if (_списокПользователей != null)
            {
                _списокПользователей.Clear();
                _списокПользователей = null;
            }

            СохранитьВкладки();

            //закрыть все вкладки
            if (PART_DocumentsHost != null)
            {
                PART_DocumentsHost.SelectedItem = null;

                foreach (var item in PART_DocumentsHost.Items.Cast<RosControl.UI.DocumentsTabItem>())
                {
                    if (item.Content is IDisposable)
                        ((IDisposable)item.Content).Dispose();
                    item.Content = null;
                }
                PART_DocumentsHost.Items.Clear();
            }
        }

        private void ЖурналУдалений(object sender, RoutedEventArgs e)
        {
            var window = new RosApplication.Конфигуратор.Службы.ЖурналУдалений()
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            window.Show();
        }

        //private void TextBox_KeyDown(object sender, KeyEventArgs e)
        //{
        //    if (e.Key == Key.Return)
        //    {
        //        var search = new RosControl.Forms.РасшириныйПоиск() { Short = false };
        //        search.Search(((System.Windows.Controls.TextBox)sender).Text);
        //        RosControl.Helper.СоздатьВкладку(search, "Расширенный поиск", "@search", false, true);

        //        this.Dispatcher.BeginInvoke(DispatcherPriority.Input, (Action)delegate
        //        {
        //            SearchQuery.Focus();
        //        });
        //    }   
        //}

        //private void ГлавнаяСтраница_AppNameLoad(object sender, RoutedEventArgs e)
        //{
        //    var label = e.OriginalSource as string;
        //    if (!string.IsNullOrEmpty(label))
        //    {
                
        //    }
        //}
    }

    public class IsAlertConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null || value.Equals(0))
                return false;
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class ToolButtonConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is КонтентПанель)
                return Visibility.Visible;
            else if (value is RosControl.Forms.ГлавнаяСтраница)
                return Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
