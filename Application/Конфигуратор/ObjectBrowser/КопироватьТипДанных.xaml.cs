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
using System.Windows.Threading;
using System.Threading;
using RosService.Configuration;
using System.ComponentModel;
using System.Collections.ObjectModel;

namespace RosApplication.Конфигуратор
{
    /// <summary>
    /// Interaction logic for КопироватьТипДанных.xaml
    /// </summary>
    public partial class КопироватьТипДанных : Window
    {
        //public IEnumerable<string> СписокДоменов;

        public ObservableCollection<string> КопироватьВыбранные { get; set; }


        public object Типы
        {
            get { return (object)GetValue(ТипыProperty); }
            set { SetValue(ТипыProperty, value); }
        }
        public static readonly DependencyProperty ТипыProperty =
            DependencyProperty.Register("Типы", typeof(object), typeof(КопироватьТипДанных), new UIPropertyMetadata(null));

        private System.Collections.ObjectModel.ObservableCollection<RosService.Configuration.Type> _SelectedTypes;
        public System.Collections.ObjectModel.ObservableCollection<RosService.Configuration.Type> SelectedTypes
        {
            get { return _SelectedTypes; }
        }





        public object ItemsSource
        {
            get { return (object)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register("ItemsSource", typeof(object), typeof(КопироватьТипДанных), new UIPropertyMetadata(null));


        public КопироватьТипДанных()
        {
            _SelectedTypes = new System.Collections.ObjectModel.ObservableCollection<RosService.Configuration.Type>();
            КопироватьВыбранные = new ObservableCollection<string>();
            КопироватьВыбранные.Add(RosService.Client.Domain);

            InitializeComponent();
        }

        private void Window_Complite(object sender, EventArgs e)
        {
            var domains = null as IEnumerable<string>;
            //if (IsВсеДомены.IsChecked.Value)
            //    domains = СписокДоменов.Where(p => p != RosService.Client.Domain);
            //else
            //domains = new string[] { RosService.Client.Domain };
            domains = КопироватьВыбранные.ToArray();


            //var _Тип = ""; //Тип.SelectedValue as string;
            var _Домен = Домен.Text;
            if (string.IsNullOrEmpty(_Домен))
            {
                MessageBox.Show("Не указана база данных");
                return;
            }

            //var _IsКопироватьВ = IsКопироватьВ.IsChecked.Value ? Convert.ToString(КопироватьВТип.SelectedValue) : null;
            var _УсловиеКопирования = УсловияКопирования.НеОпределено;
            if (IsАтрибуты.IsChecked.Value) _УсловиеКопирования |= УсловияКопирования.Атрибуты;
            if (IsШаблон.IsChecked.Value) _УсловиеКопирования |= УсловияКопирования.Шаблон;
            if (IsИсходныйКод.IsChecked.Value) _УсловиеКопирования |= УсловияКопирования.ИсходныйКод;
            if (IsЗначенияПоУмолчанию.IsChecked.Value) _УсловиеКопирования |= УсловияКопирования.ЗначенияПоУмолчанию;
            if (IsИконка.IsChecked.Value) _УсловиеКопирования |= УсловияКопирования.Иконка;
            if (IsВсеДомены.IsChecked.Value) _УсловиеКопирования |= УсловияКопирования.ВсеДомены;

            var log = new List<string>();
            this.Window.IsEnabledComplite = false;
            //ThreadPool.QueueUserWorkItem((WaitCallback)delegate
            var types = SelectedTypes.Select(p => p.Name).ToArray();
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                try
                {
                    using (RosService.Client client = new RosService.Client(TimeSpan.FromMinutes(1)))
                    {
                        foreach (var item in domains)
                        {
                            try
                            {
                                foreach (var type in types)
                                {
                                    var s = DateTime.Now;
                                    try
                                    {
                                        log.Insert(0, string.Format("{0} - start", item));

                                        client.Конфигуратор.КопироватьТипДанных(
                                            type,
                                            _Домен,
                                            type, //_IsКопироватьВ,
                                            item,
                                            _УсловиеКопирования,
                                            client.Пользователь,
                                            item);

                                        log.Insert(0, string.Format("{0} - end - {1}", item, DateTime.Now - s));
                                    }
                                    catch
                                    {
                                        log.Insert(0, string.Format("{0} - error - {1}", item, DateTime.Now - s));
                                    }
                                    finally
                                    {
                                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                                        {
                                            ItemsSource = null;
                                            ItemsSource = log;
                                        });
                                    }
                                }
                            }
                            finally
                            {
                                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                                {
                                    КопироватьВыбранные.Remove(item);
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
                finally
                {
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                    {
                        foreach (var item in SelectedTypes)
                            RosControl.UI.Application.Clear(item.Name);
                        
                        this.Window.IsEnabledComplite = true;
                        DialogResult = true;
                    });
                }
            });
        }

        private void Домен_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            var domain = Домен.Text as string;
            if (string.IsNullOrEmpty(domain))
            {
                Типы = null;
                return;
            }

            this.Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
            {
                System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    for (int i = 0; i < 3; i++)
                    {
                        try
                        {
                            using (RosService.Client client = new RosService.Client(TimeSpan.FromSeconds(20)))
                            {
                                var types = client.Конфигуратор.СписокТипов(null, domain);
                                this.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate
                                {
                                    Типы = types;

                                    //var cv = (CollectionView)CollectionViewSource.GetDefaultView(types);
                                    //if (cv != null)
                                    //{
                                    //    cv.SortDescriptions.Add(new SortDescription("Namespace", ListSortDirection.Ascending));
                                    //    cv.SortDescriptions.Add(new SortDescription("Описание", ListSortDirection.Ascending));
                                    //    //Тип.ItemsSource = cv;
                                    //    //Тип.IsEnabled = true;
                                    //}
                                });
                            }
                            break;
                        }
                        catch (TimeoutException)
                        {
                            continue;
                        }
                        catch (Exception ex)
                        {
                            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate { MessageBox.Show(ex.Message); });
                            break;
                        }
                    }
                });
            });
        }
        private void Домен_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //var domain = Домен.SelectedValue as string;

            //this.Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
            //{
            //    System.Threading.Tasks.Task.Factory.StartNew(() =>
            //    {
            //        for (int i = 0; i < 3; i++)
            //        {
            //            try
            //            {
            //                using (RosService.Client client = new RosService.Client(TimeSpan.FromSeconds(20)))
            //                {
            //                    var types = client.Конфигуратор.СписокТипов(null, domain);
            //                    this.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate
            //                    {
            //                        Типы = types;

            //                        //var cv = (CollectionView)CollectionViewSource.GetDefaultView(types);
            //                        //if (cv != null)
            //                        //{
            //                        //    cv.SortDescriptions.Add(new SortDescription("Namespace", ListSortDirection.Ascending));
            //                        //    cv.SortDescriptions.Add(new SortDescription("Описание", ListSortDirection.Ascending));
            //                        //    //Тип.ItemsSource = cv;
            //                        //    //Тип.IsEnabled = true;
            //                        //}
            //                    });
            //                }
            //                break;
            //            }
            //            catch (TimeoutException)
            //            {
            //                continue;
            //            }
            //            catch (Exception ex)
            //            {
            //                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate { MessageBox.Show(ex.Message); });
            //                break;
            //            }
            //        }
            //    });
            //});
        }
        private void IsКопироватьВ_Checked(object sender, RoutedEventArgs e)
        {
            //if (КопироватьВТип.ItemsSource == null)
            //{
            //    КопироватьВТип.ItemsSource = new object[] { new { Описание = "Загрузка..." } };
            //    КопироватьВТип.SelectedIndex = 0;
            //    //ThreadPool.QueueUserWorkItem((WaitCallback)delegate
            //    System.Threading.Tasks.Task.Factory.StartNew(() =>
            //    {
            //        try
            //        {
            //            using (RosService.Client client = new RosService.Client())
            //            {
            //                var types = client.Конфигуратор.СписокТипов();
            //                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
            //                {
            //                    var cv = (CollectionView)CollectionViewSource.GetDefaultView(types);
            //                    if (cv != null)
            //                    {
            //                        cv.SortDescriptions.Add(new SortDescription("Namespace", ListSortDirection.Ascending));
            //                        cv.SortDescriptions.Add(new SortDescription("Описание", ListSortDirection.Ascending));
            //                        КопироватьВТип.ItemsSource = cv;
            //                    }
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
        private void Window_Initialized(object sender, EventArgs e)
        {
            //ThreadPool.QueueUserWorkItem((WaitCallback)delegate
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                try
                {
                    using (RosService.Client client = new RosService.Client())
                    {
                        var СписокДоменов = client.Конфигуратор.СписокДоменов().Select(p => p.ToLower());
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                        {
                            Домен.ItemsSource = СписокДоменов;
                        });
                    }
                }
                catch (Exception ex)
                {
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate { MessageBox.Show(ex.Message); });
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
                var f = Convert.ToString(Фильтр.Text).ToLower();
                e.Accepted = item.Описание.ToLower().Contains(f)
                    || item.Namespace.ToLower().Contains(f);
            }
        }

        private void Фильтр_TextSearched(object sender, RosControl.UI.SearchTextBoxChangedEventArgs e)
        {
            var s = TryFindResource("TypeItemSource") as CollectionViewSource;
            if (s != null && s.View != null)
            {
                s.View.Refresh();

                //if (PART_DataGrid.Items.Count <= 50)
                //{
                //    PART_DataGrid.ГруппироватьПо = "Namespace";
                //}
            }

            //try
            //{
            //    PART_DataGrid.BeginInit();
                
            //}
            //finally
            //{
            //    PART_DataGrid.EndInit();
            //}
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (PART_DataGrid.SelectedItem == null)
                return;

            if (!SelectedTypes.Contains(PART_DataGrid.SelectedItem))
            {
                SelectedTypes.Add(PART_DataGrid.SelectedItem as RosService.Configuration.Type);
            }
        }

        private void ListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (PART_ListBox.SelectedItem == null)
                return;

            if (SelectedTypes.Contains(PART_ListBox.SelectedItem))
            {
                SelectedTypes.Remove(PART_ListBox.SelectedItem as RosService.Configuration.Type);
            }
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (((CheckBox)sender).IsChecked.Value)
            {
                PART_DataGrid.ГруппироватьПо = "Namespace";
            }
            else
            {
                PART_DataGrid.ГруппироватьПо = null;
            }
        }

        private void Delete_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            КопироватьВыбранные.Remove(((ListBox)sender).SelectedValue as string);
        }

        private void КопироватьВ_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrEmpty(КопироватьВ.Text))
            {
                КопироватьВыбранные.Add(КопироватьВ.Text);
                КопироватьВ.Text = "";
                e.Handled = true;
            }
        }

        private void Домен_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrEmpty(Домен.Text))
            {
                Фильтр.Focus();
                e.Handled = true;
            }
        }
    }
}
