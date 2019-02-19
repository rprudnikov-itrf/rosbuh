using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;
using System.Windows.Threading;
using System.ComponentModel;
using System.Collections;
using RosControl.UI;
using System.Collections.ObjectModel;
using RosService.Configuration;
using System.Data;
using System.Deployment.Application;
using System.IO;
using Microsoft.Win32;


namespace RosApplication.Конфигуратор
{
    /// <summary>
    /// Interaction logic for ObjectBrowser.xaml
    /// </summary>
    public partial class ObjectBrowser : UserControl, IDisposable
    {
        //protected ObservableCollection<RosService.Configuration.Type> СписокТипов { get; set; }


        public ObservableCollection<RosService.Configuration.Type> СписокТипов
        {
            get { return (ObservableCollection<RosService.Configuration.Type>)GetValue(СписокТиповProperty); }
            set { SetValue(СписокТиповProperty, value); }
        }
        public static readonly DependencyProperty СписокТиповProperty =
            DependencyProperty.Register("СписокТипов", typeof(ObservableCollection<RosService.Configuration.Type>), typeof(ObjectBrowser), new UIPropertyMetadata(null));




        public IEnumerable<RosService.Configuration.Type> СписокАтрибутов
        {
            get { return (IEnumerable<RosService.Configuration.Type>)GetValue(СписокАтрибутовProperty.DependencyProperty); }
        }
        protected static readonly DependencyPropertyKey СписокАтрибутовProperty =
            DependencyProperty.RegisterReadOnly("СписокАтрибутов", typeof(IEnumerable<RosService.Configuration.Type>), typeof(ObjectBrowser), new UIPropertyMetadata(null, new PropertyChangedCallback(СписокАтрибутовPropertyChanged)));
        private static void СписокАтрибутовPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var obj = d as ObjectBrowser;
            if (obj == null) return;

            obj.IsEdit = false;
            //obj.СписокЗначений.Clear();
            var cv = (CollectionView)CollectionViewSource.GetDefaultView(e.NewValue);
            if (cv != null)
            {
                cv.GroupDescriptions.Add(new PropertyGroupDescription("ReflectedType"));
            }
        }

        


        //protected Dictionary<RosService.Configuration.Type, object> СписокЗначений = new Dictionary<RosService.Configuration.Type,object>();
        protected RosService.Configuration.Type SelectedItem
        {
            get
            {
                return Дерево.SelectedItem as RosService.Configuration.Type;
            }
        }
        //protected RosControl.UI.PropertyGrid РедакторСвойств;



        public bool IsFileGroupDownloaded
        {
            get { return (bool)GetValue(IsFileGroupDownloadedProperty); }
            set { SetValue(IsFileGroupDownloadedProperty, value); }
        }
        public static readonly DependencyProperty IsFileGroupDownloadedProperty =
            DependencyProperty.Register("IsFileGroupDownloaded", typeof(bool), typeof(ObjectBrowser), new UIPropertyMetadata(false));



        private bool _isedit;
        public bool IsEdit
        {
            get
            {
                return _isedit;
            }
            set
            {
                if (_isedit != value)
                {
                    _isedit = value;
                    var documnets = RosControl.UI.NavigationWindow.GetCurrentNavigationWindow();
                    if (documnets != null)
                    {
                        var SelectedDocument = documnets.SelectedDocument;
                        if (SelectedDocument != null) SelectedDocument.IsEdit = value;
                    }
                }
            }
        }

        #region Propertis
        public bool IsLoading
        {
            get { return (bool)GetValue(IsLoadingProperty); }
            set { SetValue(IsLoadingProperty, value); }
        }
        public static readonly DependencyProperty IsLoadingProperty =
            DependencyProperty.Register("IsLoading", typeof(bool), typeof(ObjectBrowser), new UIPropertyMetadata(false));

        public bool IsLoadingClass
        {
            get { return (bool)GetValue(IsLoadingClassProperty); }
            set { SetValue(IsLoadingClassProperty, value); }
        }
        public static readonly DependencyProperty IsLoadingClassProperty =
            DependencyProperty.Register("IsLoadingClass", typeof(bool), typeof(ObjectBrowser), new UIPropertyMetadata(false));
        #endregion



        public string РазмерОкна
        {
            get { return (string)GetValue(РазмерОкнаProperty); }
            set { SetValue(РазмерОкнаProperty, value); }
        }
        public static readonly DependencyProperty РазмерОкнаProperty =
            DependencyProperty.Register("РазмерОкна", typeof(string), typeof(ObjectBrowser), new UIPropertyMetadata(null));



        public DateTime? Обновление
        {
            get { return (DateTime?)GetValue(ОбновлениеProperty); }
            set { SetValue(ОбновлениеProperty, value); }
        }
        public static readonly DependencyProperty ОбновлениеProperty =
            DependencyProperty.Register("Обновление", typeof(DateTime?), typeof(ObjectBrowser), new UIPropertyMetadata(null));




        public ObjectBrowser()
        {
            InitializeComponent();
            ЗагрузитьСписокТипов();

            //ThreadPool.QueueUserWorkItem((WaitCallback)delegate
            //{
            //this.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, (Action)delegate
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                try
                {
                    //загрузить в память схему WPF для подстановки в редакторе
                    if (ApplicationDeployment.IsNetworkDeployed)
                    {
                        var deployment = ApplicationDeployment.CurrentDeployment;
                        if (!deployment.IsFileGroupDownloaded("Resources"))
                        {
                            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                            {
                                IsFileGroupDownloaded = true;
                            });
                            deployment.DownloadFileGroupCompleted += delegate
                            {
                                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                                {
                                    IsFileGroupDownloaded = false;
                                });
                            };
                            deployment.DownloadFileGroupProgressChanged += delegate(object _sender, DeploymentProgressChangedEventArgs _e)
                            {
                                this.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, (Action)delegate
                                {
                                    PART_DownloadResources.Text = string.Format("{0} / {1}",
                                        new RosControl.Converters.FileLengthConverter().Convert(_e.BytesCompleted, typeof(long), null, System.Globalization.CultureInfo.CurrentCulture),
                                        new RosControl.Converters.FileLengthConverter().Convert(_e.BytesTotal, typeof(long), null, System.Globalization.CultureInfo.CurrentCulture));
                                });
                            };
                            deployment.DownloadFileGroupAsync("Resources");
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                    {
                        PART_DownloadResources.Text = ex.Message;
                    });
                }
                //});
            });
        }
        private void ObjectBrowser_Loaded(object sender, RoutedEventArgs e)
        {
            Фильтр.Focus();
        }
        private void ObjectBrowser_Unloaded(object sender, RoutedEventArgs e)
        {
        }


        private void ЗагрузитьСписокТипов()
        {
            IsLoading = true;
            IsEdit = false;

            //ThreadPool.QueueUserWorkItem((WaitCallback)delegate
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        using (RosService.Client client = new RosService.Client())
                        {
                            var items = new ObservableCollection<RosService.Configuration.Type>(client.Конфигуратор.СписокТипов());
                            this.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, (Action)delegate
                            {
                                //ОбновитьДерево();
                                this.СписокТипов = items;
                                IsLoading = false;
                            });
                            break;
                        }
                    }
                    catch (TimeoutException)
                    {
                        continue;
                    }
                    catch (Exception ex)
                    {
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                        {
                            IsLoading = false;
                            throw ex;
                        });
                        break;
                    }
                }
            });
        }
        private void ЗагрузитьСписокАтрибутов(string Имя)
        {
            IsLoadingClass = true;

            //ThreadPool.QueueUserWorkItem((WaitCallback)delegate
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                try
                {
                    using (RosService.Client client = new RosService.Client())
                    {
                        var items = client.Конфигуратор.СписокАтрибутов(Имя);
                        var _РазмерОкна = client.Конфигуратор.ПолучитьЗначение<string>(Имя, "@РазмерОкна");
                        var _UpdateDate = client.Конфигуратор.ПолучитьЗначение<DateTime?>(Имя, "@UpdateDate");
                        if (string.IsNullOrEmpty(_РазмерОкна)) _РазмерОкна = "пусто";

                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                        {
                            if(IsSystemAtribute.IsChecked.Value)
                                SetValue(СписокАтрибутовProperty, items.Where(p => !p.IsReadOnly));
                            else
                                SetValue(СписокАтрибутовProperty, items);

                            РазмерОкна = _РазмерОкна;
                            Обновление = _UpdateDate;
                            IsLoadingClass = false;
                        });

                        this.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, (Action)delegate
                        {
                            if(РедакторСвойств != null)
                                РедакторСвойств.Properties = СписокСвойств(Имя, SelectedItem);
                        });
                    }
                }
                catch (Exception)
                {
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                    {
                        IsLoadingClass = false;
                    });
                }
            });
        }
        private void РедактироватьШаблон()
        {
            if (SelectedItem == null) 
                return;

            var tab = RosControl.Helper.СоздатьВкладку(new RosControl.Designer.РедакторXaml(SelectedItem.Name), SelectedItem.Описание, DateTime.Now.Ticks.ToString(), true, true);
            tab.IsFull = true;
        }
        private void ДобавитьТипДанных()
        {
            var window = new ДобавитьТипДанных();
            if (SelectedItem != null)
            {
                window.Категория.Text = SelectedItem.Namespace;
            }
            else if (Дерево.SelectedValue != null)
            {
                window.Категория.Text = Дерево.SelectedValue as string;
            }
            if (window.ShowDialog().Value)
            {
                var _Имя = window.Имя.Text;
                var _Описание = window.Описание.Text;
                var _Категория = window.Категория.Text;
                var _БазовыйТип = Convert.ToString(window.БазовыйТип.SelectedValue);
                var _Группа = window.ГруппаЖурналов.Text;

                var icon = null as byte[];
                if (!string.IsNullOrEmpty(window.File1.FileName) &&
                    File.Exists(window.File1.FileName))
                {
                    icon = File.ReadAllBytes(window.File1.FileName);
                }
                System.Threading.Tasks.Task.Factory.StartNew(delegate()
                {
                    try
                    {
                        using (RosService.Client client = new RosService.Client())
                        {
                            var ТипДанных = client.Конфигуратор.ДобавитьТип(0, _Имя, _Описание,
                                _Категория, _БазовыйТип, false, true, RosService.Client.UserName, RosService.Client.Domain);

                            if (icon != null)
                            {
                                client.Конфигуратор.СохранитьЗначение(ТипДанных, "ИконкаПоУмолчанию", icon);
                            }

                            if (_БазовыйТип == "Журнал")
                            {
                                client.Конфигуратор.СохранитьЗначение(ТипДанных, "ГруппаЖурналов", _Группа);
                            }

                            var _type = client.Конфигуратор.ПолучитьТип(ТипДанных);
                            this.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate
                            {
                                if(СписокТипов != null)
                                    СписокТипов.Add(_type);
                            });
                        }
                    }
                    catch
                    {
                    }
                });
            }
        }
        private void ДобавитьАтрибут()
        {
            if (SelectedItem == null) return;

            var window = new ДобавитьАтрибут();
            window.ДобавитьВ.Text = SelectedItem.Описание;
            if (window.ShowDialog().Value)
            {
                this.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate
                {
                    using (RosService.Client client = new RosService.Client())
                    {
                        if (window.Описание.SelectedItem == null)
                        {
                            var ТипДанных = client.Конфигуратор.ДобавитьТип(
                                0, window.Имя.Text, 
                                window.Описание.Text, "System.Default",
                                Convert.ToString(window.ТипДанных.SelectedValue),
                                false, //window.Массив.IsChecked.Value,
                                true,
                                RosService.Client.UserName,
                                RosService.Client.Domain);

                            client.Конфигуратор.ДобавитьАтрибут(
                                SelectedItem.Name, ТипДанных,
                                true,
                                RosService.Client.UserName,
                                RosService.Client.Domain);

                            client.Конфигуратор.СохранитьЗначение(ТипДанных, "@UpdateDate", DateTime.Now);
                        }
                        else
                        {
                            client.Конфигуратор.ДобавитьАтрибут(
                                SelectedItem.Name,
                                Convert.ToString(window.Описание.Value),
                                true,
                                RosService.Client.UserName,
                                RosService.Client.Domain);
                            client.Конфигуратор.СохранитьЗначение(SelectedItem.Name, "@UpdateDate", DateTime.Now);
                        }
                        ЗагрузитьСписокАтрибутов(this.SelectedItem.Name);
                    }
                });
            }
        }
        private void УдалитьТипДанных()
        {
            if (SelectedItem != null && MessageBox.Show(string.Format("Вы действительно хотите удалить тип '{0}'?", SelectedItem.Name), 
                "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                using (RosService.Client client = new RosService.Client())
                {
                    client.Конфигуратор.УдалитьТип(SelectedItem.Name, RosService.Client.Domain);

                    if (this.СписокТипов != null)
                    {
                        this.СписокТипов.Remove(SelectedItem);
                    }
                }
            }
            else if (SelectedItem == null && MessageBox.Show("Вы действительно хотите удалить группу типов данных?", 
                "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                //using (RosService.Client client = new RosService.Client())
                //foreach (var item in (Дерево.SelectedItem as RosControl.UI.TreeViewItem).Items.Cast<RosControl.UI.TreeViewItem>())
                //{
                //    if (item.Tag is RosService.Configuration.Type)
                //    {
                //        client.Конфигуратор.УдалитьТип(((RosService.Configuration.Type)item.Tag).Name, RosService.Client.Domain);
                //    }
                //}
                //(Дерево.SelectedItem as RosControl.UI.TreeViewItem).ItemsSource = null;
            }
        }
        private void УдалитьАтрибут()
        {
            var Атрибут = Convert.ToString(Диаграмма.SelectedValue);
            if (SelectedItem == null || string.IsNullOrEmpty(Атрибут)) return;
            if (MessageBox.Show(string.Format("Вы действительно хотите удалить атрибут '{0}'?", Атрибут), "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                using (RosService.Client client = new RosService.Client())
                {
                    client.Конфигуратор.УдалитьАтрибут(SelectedItem.Name, Атрибут);
                    ЗагрузитьСписокАтрибутов(SelectedItem.Name);
                }
            }
        }


        private void Фильтр_TextSearched(object sender, RosControl.UI.SearchTextBoxChangedEventArgs e)
        {
            if (e.Фильтр != null && e.Фильтр.Length >= 2)
            {
                Дерево.ItemContainerStyle = TryFindResource("TreeViewItem2") as Style;
            }
            else
            {
                Дерево.ItemContainerStyle = TryFindResource("TreeViewItem1") as Style;
            }

            var s = TryFindResource("TypeItemSource") as CollectionViewSource;
            if (s != null && s.View != null)
            {
                s.View.Refresh();
            }
        }
        private void Дерево_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (this.SelectedItem == null) return;
            ЗагрузитьСписокАтрибутов(this.SelectedItem.Name);
        }
        private void Дерево_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            РедактироватьШаблон();
        }
        private void Диаграмма_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (Диаграмма.SelectedItem == null) return;
            this.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate
            {
                РедакторСвойств.Properties = СписокСвойств(SelectedItem.Name, Диаграмма.SelectedItem as RosService.Configuration.Type);
            });
        }

        private void CommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            switch (Convert.ToString(e.Parameter))
            {
                case "РедактироватьШаблон":
                    РедактироватьШаблон();
                    break;

                case "ДобавитьТипДанных":
                    ДобавитьТипДанных();
                    break;

                case "ДобавитьАтрибут":
                    ДобавитьАтрибут();
                    break;

                case "УдалитьТипДанных":
                    {
                        var menu = sender as ContextMenu;
                        if (menu == null) break;
                        if (menu.PlacementTarget == Диаграмма)
                        {
                            УдалитьАтрибут();
                        }
                        else
                        {
                            УдалитьТипДанных();
                        }
                    }
                    break;


                case "КопироватьИмя":
                    {
                        var menu = sender as ContextMenu;
                        if (menu == null) break;
                        if (menu.PlacementTarget == Диаграмма)
                        {
                            Clipboard.SetText(Convert.ToString(Диаграмма.SelectedValue));
                        }
                        else if (SelectedItem != null)
                        {
                            Clipboard.SetText(SelectedItem.Name);
                        }
                    }
                    break;

                case "КопироватьАтрибуты":
                    {
                        if (СписокАтрибутов != null && СписокАтрибутов.Count() > 0)
                        {
                            var sb = new StringBuilder();
                            sb.AppendFormat("{0,-14}{1,-34}{2}", "Тип", "Имя", "Описание");
                            sb.AppendLine();
                            sb.AppendFormat("---------------------------------------------------------------------------------------");
                            sb.AppendLine();
                            foreach (var g in СписокАтрибутов.GroupBy(p => p.ReflectedType))
                            {
                                sb.AppendFormat("Класс: {0}", g.Key);
                                sb.AppendLine();
                                sb.AppendLine();
                                foreach (var item in g.OrderBy(p => p.Name))
                                {
                                    sb.AppendFormat("{0,-14}{1,-34}{2}", item.BaseType, item.Name.Substring(0, item.Name.Length > 30 ? 30 : item.Name.Length), item.Описание);
                                    sb.AppendLine();
                                }
                                sb.AppendLine();
                                sb.AppendLine();
                                sb.AppendLine();
                            }
                            Clipboard.SetText(sb.ToString());
                        }
                    }
                    break;

                case "Обновить":
                    ЗагрузитьСписокТипов();
                    break;
            }
        }
        private void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            switch (e.Parameter as string)
            {
                case "РедактироватьШаблон":
                //case "УдалитьТипДанных":
                case "ДобавитьАтрибут":
                    e.CanExecute = SelectedItem != null;
                    break;

                default:
                    e.CanExecute = true;
                    break;
            }
        }
        private void КопироватьТипДанных_Click(object sender, RoutedEventArgs e)
        {
            var window = new Конфигуратор.КопироватьТипДанных();
            if (window.ShowDialog().Value)
            {
                ЗагрузитьСписокТипов();
            }
        }


        private void РедакторСвойств_PropertyChanged(object sender, RosControl.UI.PropertyChangedEventArgs e)
        {
            //IsEdit = true;
            switch (e.Item.GetName())
            {
                case "Значение":
                    {
                        var item = Диаграмма.SelectedItem as RosService.Configuration.Type;
                        if (item == null) break;

                        //if (!СписокЗначений.ContainsKey(item)) СписокЗначений.Add(item, null);
                        //СписокЗначений[item] = e.Item.GetValue();

                        var _type = SelectedItem.Name;
                        var _value = e.Item.GetValue();
                        //ThreadPool.QueueUserWorkItem((WaitCallback)delegate
                        System.Threading.Tasks.Task.Factory.StartNew(() =>
                        {
                            try
                            {
                                using (RosService.Client client = new RosService.Client())
                                {
                                    client.Конфигуратор.СохранитьЗначение(_type, item.Name, _value);
                                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                                    {
                                        //сбросить кеш
                                        RosControl.Helper.ClearCache(_type);
                                    });
                                }
                            }
                            catch
                            {
                                
                            }
                        });
                    }
                    break;
            }
        }
        private void РедакторСвойств_ЗагрузитьФайл(object sender, RoutedEventArgs e)
        {
            var attr = Convert.ToString(Диаграмма.SelectedValue);
            if (!string.IsNullOrEmpty(attr))
            {
                var openFileDialog1 = new OpenFileDialog();
                if (openFileDialog1.ShowDialog().Value)
                {
                    using (RosService.Client client = new RosService.Client())
                    {
                        var _Name = (Дерево.SelectedValue as RosService.Configuration.Type).Name;
                        var window = new RosControl.Forms.РаботаСФайлами();
                        window.Complite += delegate
                        {
                            if (RosControl.UI.FilePreview.cache.ContainsKey(_Name))
                            {
                                RosControl.UI.FilePreview.CacheBitmap remove;
                                RosControl.UI.FilePreview.cache.TryRemove(_Name, out remove);
                            }
                        };
                        window.СохранитьИконку(openFileDialog1.FileName, _Name, attr);
                        window.Show();
                    }
                }
            }
        }
        private void ЗагрузитьИконку_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem == null) return;

            var openFileDialog1 = new OpenFileDialog();
            if (openFileDialog1.ShowDialog().Value)
            {
                using (RosService.Client client = new RosService.Client())
                {
                    var Name = SelectedItem.Name; //(Дерево.SelectedValue as RosService.Configuration.Type).Name;
                    var window = new RosControl.Forms.РаботаСФайлами();
                    window.Complite += delegate
                    {
                        if (RosControl.UI.FilePreview.cache.ContainsKey(Name))
                        {
                            RosControl.UI.FilePreview.CacheBitmap remove;
                            RosControl.UI.FilePreview.cache.TryRemove(Name, out remove);
                        }
                    };
                    window.СохранитьИконку(openFileDialog1.FileName, Name, "ИконкаПоУмолчанию");
                    window.Show();
                }
            }
        }
        public ObservableCollection<PropertyGridItem> СписокСвойств(string Name, RosService.Configuration.Type type)
        {
            using (RosService.Client client = new RosService.Client())
            {
                PropertyDescriptorCollection pd = TypeDescriptor.GetProperties(type);
                ObservableCollection<PropertyGridItem> propertys = new ObservableCollection<PropertyGridItem>();

                propertys.Add(new PropertyGridItemCustom()
                {
                    IsReadOnly = true,
                    Type = PropertyGridItemType.TextBox,
                    Name = "Имя",
                    Category = "Основные",
                    Value = type.Name
                });

                propertys.Add(new PropertyGridItemCustom()
                {
                    IsReadOnly = true,
                    Type = PropertyGridItemType.TextBox,
                    Name = "Основные",
                    Category = "Основные",
                    Value = type.Описание
                });

                propertys.Add(new PropertyGridItemCustom()
                {
                    Category = "Основные",
                    Name = "Тип",
                    Type = PropertyGridItemType.TextBox,
                    IsReadOnly = true,
                    Value = type.BaseType
                });


                //propertys.Add(new PropertyGridItemCustom()
                //{
                //    Category = "Основные",
                //    Name = "HashCode",
                //    Type = PropertyGridItemType.TextBox,
                //    IsReadOnly = true,
                //    Value = type.HashCode
                //});

                propertys.Add(new PropertyGridItemControl(type, pd["IsReadOnly"])
                {
                    Category = "Атрибуты"
                });

                //propertys.Add(new PropertyGridItemControl(type, pd["IsSystem"])
                //{
                //    Category = "Атрибуты"
                //});

                propertys.Add(new PropertyGridItemControl(type, pd["IsAutoIncrement"])
                {
                    Category = "Атрибуты"
                });

                propertys.Add(new PropertyGridItemControl(type, pd["IsSetDefaultValue"])
                {
                    Category = "Атрибуты"
                });

                switch (type.MemberType)
                {
                    case MemberTypes.Object:
                        {
                            propertys.Add(new PropertyGridItemCustom()
                            {
                                Category = "Основные",
                                Name = "Namespace",
                                Type = PropertyGridItemType.TextBox,
                                Value = type.Namespace,
                                IsReadOnly = true,
                                //Items = client.Конфигуратор.СписокКатегорий(RosService.Client.Domain)
                            });
                        }
                        break;

                    case MemberTypes.String:
                    case MemberTypes.Таблица:
                        {
                            //if (type.DeclaringType > 0)
                            {
                                propertys.Add(new PropertyGridItemCustom()
                                {
                                    Category = "Основные",
                                    Name = "Значение",
                                    Type = PropertyGridItemType.MultiTextBox,
                                    Value = client.Конфигуратор.ПолучитьЗначение<object>(Name, type.Name),
                                    ToolTip = "Шаблон: {Атрибут:Формат}\nНапример: Договор №{ПорядковыйНомер} от {ДатаЗаключенияДоговора:d}"
                                });
                            }
                        }
                        break;

                    case MemberTypes.Int:
                    case MemberTypes.Double:
                    case MemberTypes.Ссылка:
                    case MemberTypes.DateTime:
                        {
                            //if (type.DeclaringType > 0)
                            {
                                propertys.Add(new PropertyGridItemCustom()
                                {
                                    Category = "Основные",
                                    Name = "Значение",
                                    Type = PropertyGridItemType.TextBox,
                                    Value = client.Конфигуратор.ПолучитьЗначение<object>(Name, type.Name)
                                });
                            }
                        }
                        break;

                    case MemberTypes.Bool:
                        {
                            //if (type.DeclaringType > 0)
                            {
                                propertys.Add(new PropertyGridItemCustom()
                                {
                                    Category = "Основные",
                                    Name = "Значение",
                                    Type = PropertyGridItemType.CheckBox,
                                    Value = client.Конфигуратор.ПолучитьЗначение<object>(Name, type.Name)
                                });
                            }
                        }
                        break;

                    case MemberTypes.Byte:
                        {
                            //if (type.DeclaringType > 0)
                            {
                                propertys.Add(new PropertyGridItemCustom()
                                {
                                    Category = "Файл",
                                    Name = "Значение",
                                    Type = PropertyGridItemType.TextBox,
                                    Value = client.Конфигуратор.ПолучитьЗначение<object>(Name, type.Name)
                                });
                            }
                            if (type.IsReadOnly)
                            {
                                propertys.Add(new PropertyGridItemCustom()
                                {
                                    Category = "Файл",
                                    Name = "Загрузить",
                                    Type = PropertyGridItemType.File
                                });
                            }
                        }
                        break;
                }
                return propertys;
            }
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (РедакторСвойств != null)
            {
                РедакторСвойств.Properties = null;
                РедакторСвойств = null;
            }
        }

        #endregion

        private void Delete_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            УдалитьТипДанных();
        }
        private void DropDownButton_Click(object sender, RoutedEventArgs e)
        {
            var _type = SelectedItem.Name;
            var _value = (e.OriginalSource as FrameworkElement).Tag;
            //ThreadPool.QueueUserWorkItem((WaitCallback)delegate
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                try
                {
                    using (RosService.Client client = new RosService.Client())
                    {
                        client.Конфигуратор.СохранитьЗначение(_type, "@РазмерОкна", _value);
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                        {
                            РазмерОкна = Convert.ToString(_value);
                            //сбросить кеш
                            RosControl.Helper.ClearCache(_type);
                        });
                    }
                }
                catch
                {

                }
            });
        }

        private void CollectionViewSource_Filter(object sender, FilterEventArgs e)
        {
            var item = e.Item as RosService.Configuration.Type;
            if (string.IsNullOrEmpty(Фильтр.Text))
            {
                e.Accepted = true;
            }
            else if (item == null)
            {
                e.Accepted = false;
            }
            else
            {
                e.Accepted = item.Описание.ToLower().Contains(Convert.ToString(Фильтр.Text).ToLower());
            }
        }

        private void IsSystemAtribute_Click(object sender, RoutedEventArgs e)
        {
            Дерево_MouseLeftButtonUp(sender, null);
        }
    }

    internal class WHConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (parameter == null) return null;

            var s = System.Convert.ToString(value, culture).Split(',');
            var val = s[parameter.Equals("w") ? 0 : 1];
            if (val.EndsWith("%"))
                return 20;

            return System.Convert.ToDouble(val) * 0.1;
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    };
    //internal class IsExpanderConverter : IValueConverter
    //{
    //    public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
    //    {
    //        return true;
    //    }

    //    public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
    //    {
    //        throw new NotImplementedException();
    //    }
    //};
}
