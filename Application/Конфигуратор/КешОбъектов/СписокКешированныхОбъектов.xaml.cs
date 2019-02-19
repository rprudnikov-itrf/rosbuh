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
using System.Windows.Shapes;
using System.Threading;
using System.Windows.Threading;
using System.Collections;
using RosService.Configuration;
using System.ComponentModel;
using System.Collections.ObjectModel;

namespace RosApplication.Конфигуратор
{
    public partial class СписокКешированныхОбъектов : Window
    {
        public ObservableCollection<object> Items
        {
            get { return (ObservableCollection<object>)GetValue(ItemsProperty); }
            set { SetValue(ItemsProperty, value); }
        }
        public static readonly DependencyProperty ItemsProperty =
            DependencyProperty.Register("Items", typeof(ObservableCollection<object>), typeof(СписокКешированныхОбъектов), new UIPropertyMetadata(null));




        public object ItemsStatistic
        {
            get { return (object)GetValue(ItemsStatisticProperty); }
            set { SetValue(ItemsStatisticProperty, value); }
        }
        public static readonly DependencyProperty ItemsStatisticProperty =
            DependencyProperty.Register("ItemsStatistic", typeof(object), typeof(СписокКешированныхОбъектов), new UIPropertyMetadata(null));



        public object Domains
        {
            get { return (object)GetValue(DomainsProperty); }
            set { SetValue(DomainsProperty, value); }
        }
        public static readonly DependencyProperty DomainsProperty =
            DependencyProperty.Register("Domains", typeof(object), typeof(СписокКешированныхОбъектов), new UIPropertyMetadata(null));



        public string Filter
        {
            get { return (string)GetValue(FilterProperty); }
            set { SetValue(FilterProperty, value); }
        }
        public static readonly DependencyProperty FilterProperty =
            DependencyProperty.Register("Filter", typeof(string), typeof(СписокКешированныхОбъектов), new UIPropertyMetadata(null, new PropertyChangedCallback(FilterPropertyChanged)));

        private static void FilterPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var obj = d as СписокКешированныхОбъектов;
            if (obj == null) return;

            var f = obj.TryFindResource("CollectionViewSource1") as CollectionViewSource;
            if (f != null && f.View != null)
            {
                f.View.Refresh();
            }
        }





        public object CurrentDomain
        {
            get { return (object)GetValue(CurrentDomainProperty); }
            set { SetValue(CurrentDomainProperty, value); }
        }
        public static readonly DependencyProperty CurrentDomainProperty =
            DependencyProperty.Register("CurrentDomain", typeof(object), typeof(СписокКешированныхОбъектов), new UIPropertyMetadata(null, new PropertyChangedCallback(CurrentDomainPropertyChanged)));

        private static void CurrentDomainPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var obj = d as СписокКешированныхОбъектов;
            if (obj == null) return;
            obj.CurrentDomainChanged((string)e.NewValue);
        }
        protected void CurrentDomainChanged(string domain)
        {
            this.Items = null;
            //ThreadPool.QueueUserWorkItem((WaitCallback)delegate
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                try
                {
                    using (RosService.Client client = new RosService.Client())
                    {
                        var _items = client.Конфигуратор.СписокКешированныхОбъектов(client.Пользователь, domain);
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                        {
                            this.Items = new ObservableCollection<object>(_items);
                            this.ItemsStatistic = _items.GroupBy(p => p.Type).SelectMany(p => p.OrderByDescending(g => g.Count).Take(10));
                        });
                    }
                }
                catch (Exception ex)
                {
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate { MessageBox.Show(ex.Message); });
                }
            });
        }


        public СписокКешированныхОбъектов()
        {
            InitializeComponent();

            EventManager.RegisterClassHandler(typeof(TreeViewItem), Mouse.MouseDownEvent, new MouseButtonEventHandler(OnTreeViewItemMouseDown), false);
        }
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            CurrentDomain = RosService.Client.Domain;

            //ThreadPool.QueueUserWorkItem((WaitCallback)delegate
            //System.Threading.Tasks.Task.Factory.StartNew(() =>
            //{
            //    try
            //    {
            //        using (RosService.Client client = new RosService.Client())
            //        {
            //            var _items = client.Конфигуратор.СписокДоменов();
            //            if (_items.Count() == 0) _items = new string[] { client.Домен };
            //            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
            //            {
            //                Domains = _items;
            //                CurrentDomain = client.Домен;
            //            });
            //        }
            //    }
            //    catch (Exception ex)
            //    {
            //        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate { MessageBox.Show(ex.Message); });
            //    }
            //});
        }
        private void TreeView1_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            TextEditor1.Text = TreeView1.SelectedValue as string;
        }

        private void CollectionViewSource_Filter(object sender, FilterEventArgs e)
        {
            var row = e.Item as RosService.Configuration.CacheObject;
            e.Accepted = string.IsNullOrEmpty(Filter) ? true : row.Content.ToLower().Contains(Filter.ToLower());
        }
        private void CommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            CurrentDomainChanged(CurrentDomain as string);
        }

        private void Delete_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = TreeView1.SelectedItem != null;
        }

        private void Delete_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var items = null as string[];
            var domain = RosService.Client.Domain;
            var isUpdate = false;
            switch (Convert.ToString(e.Parameter))
            {
                case "All":
                    domain = (CurrentDomain as string) ?? RosService.Client.Domain;
                    isUpdate = true;
                    break;

                default:
                    if (TreeView1.SelectedItem is RosService.Configuration.CacheObject)
                    {
                        items = new string[] { ((RosService.Configuration.CacheObject)TreeView1.SelectedItem).Value };

                        if (Items != null)
                            Items.Remove(TreeView1.SelectedItem);
                    }
                    else if (TreeView1.SelectedItem is System.Windows.Data.CollectionViewGroup)
                    {
                        var _n =((System.Windows.Data.CollectionViewGroup)(TreeView1.SelectedItem)).Items.Cast<CacheObject>();
                        items = _n.Select(p => p.Value).ToArray();

                        if (Items != null)
                        {
                            foreach (var item in _n.ToArray())
                                Items.Remove(item);
                        }
                    }
                    break;
            }


            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                try
                {
                    using (RosService.Client client = new RosService.Client())
                    {
                        client.Конфигуратор.УдалитьКешированныеОбъекты(items, client.Пользователь, client.Домен);
                        if (isUpdate)
                        {
                            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                            {
                                CurrentDomainChanged(CurrentDomain as string);
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate { MessageBox.Show(ex.Message); });
                }
            });
        }

        public static void OnTreeViewItemMouseDown(object sender, MouseButtonEventArgs e)
        {
            var treeViewitem = sender as TreeViewItem;
            if (e.RightButton == MouseButtonState.Pressed)
            {
                treeViewitem.Focus();
                e.Handled = true;
            }
        }

        private void CorrectionList_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var sv = TryFindResource("CollectionViewSource1") as CollectionViewSource;
            if (sv != null)
            {
                sv.SortDescriptions.Clear();
                sv.SortDescriptions.Add(new SortDescription("Type", ListSortDirection.Ascending));
                sv.SortDescriptions.Add(new SortDescription((e.Parameter as string ?? "Count"), ListSortDirection.Descending));
                sv.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));

                foreach (var item in PART_Sorts.Items.Cast<MenuItem>())
                    item.IsChecked = item == e.Source;
            }
        }

        private void ВсегдаКешировать_Click(object sender, RoutedEventArgs e)
        {
            //var item = TreeView1.SelectedItem as RosService.Configuration.CacheObject;
            //if (item == null) return;

            //var isTrue = ((CheckBox)sender).IsChecked.Value;
            //System.Threading.Tasks.Task.Factory.StartNew(() =>
            //{
            //    try
            //    {
            //        using (RosService.Client client = new RosService.Client())
            //        {
            //            client.Конфигуратор.ИзменитьКешированныйОбъект(item.Value, "ВсегдаКешировать", isTrue, client.Пользователь, client.Домен);
            //        }
            //    }
            //    catch (Exception ex)
            //    {
            //        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate { MessageBox.Show(ex.Message); });
            //    }
            //});
        }

        //private void МониторПроизводительности_Click(object sender, RoutedEventArgs e)
        //{
        //    new МониторПроизводительности().Show();
        //}

        private void ОчиститьКешЗначений_Click(object sender, RoutedEventArgs e)
        {
            if (new ОчиститьКешированныеОбъекты().ShowDialog().Value)
            {
                CurrentDomainChanged(CurrentDomain as string);
            }
            
            //if (MessageBox.Show("Удалить кешь???????", "Предупреждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            //{
            //    System.Threading.Tasks.Task.Factory.StartNew(() =>
            //    {
            //        try
            //        {
            //            using (RosService.Client client = new RosService.Client())
            //            {
            //                client.Конфигуратор.УдалитьКешированныеЗначения(client.Пользователь, client.Домен);
            //                client.Конфигуратор.УдалитьКешированныеОбъекты(null, client.Пользователь, client.Домен);
            //                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
            //                {
            //                    CurrentDomainChanged(CurrentDomain as string);
            //                    MessageBox.Show("Кеш значений очищен.");
            //                });
            //            }
            //        }
            //        catch (Exception ex)
            //        {
            //            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate { MessageBox.Show(ex.Message); });
            //        }
            //    });
            //}
        }
    }

    public class СписокКешированныхОбъектовВсегдаКешироватьConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is string)
            {
                return ((string)value).Contains("<ВсегдаКешировать>true</ВсегдаКешировать>");
            }
            return false;
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class СписокКешированныхОбъектовВсегдаКешироватьVisible : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (("True").Equals(parameter) && value is string && ((string)value).Contains("<ВсегдаКешировать>true</ВсегдаКешировать>"))
            {
                return Visibility.Visible;
            }
            else if (parameter == null && value is string && ((string)value).Contains("<ВсегдаКешировать>"))
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
