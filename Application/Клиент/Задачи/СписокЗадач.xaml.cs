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
using System.Data;
using System.Threading;
using System.Windows.Threading;
using RosService.Data;

namespace RosApplication.Клиент.Задачи
{
    public enum СписокЗадачПоказать
    {
        Входящие,
        КИсполнению,
        Срочно,
        Отправленые
    }

    /// <summary>
    /// Interaction logic for СписокЗадач.xaml
    /// </summary>
    public partial class СписокЗадач : UserControl
    {
        public object Header
        {
            get { return (object)GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }
        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register("Header", typeof(object), typeof(СписокЗадач), new UIPropertyMetadata(null));

        public object Пользователь
        {
            get { return (object)GetValue(ПользовательProperty); }
            set { SetValue(ПользовательProperty, value); }
        }
        public static readonly DependencyProperty ПользовательProperty =
            DependencyProperty.Register("Пользователь", typeof(object), typeof(СписокЗадач), new UIPropertyMetadata(null));

        public string ТекстСообщения
        {
            get { return (string)GetValue(ТекстСообщенияProperty); }
            set { SetValue(ТекстСообщенияProperty, value); }
        }
        public static readonly DependencyProperty ТекстСообщенияProperty =
            DependencyProperty.Register("ТекстСообщения", typeof(string), typeof(СписокЗадач), new UIPropertyMetadata(null));

        public СписокЗадачПоказать Показать
        {
            get { return (СписокЗадачПоказать)GetValue(ПоказатьProperty); }
            set { SetValue(ПоказатьProperty, value); }
        }
        public static readonly DependencyProperty ПоказатьProperty =
            DependencyProperty.Register("Показать", typeof(СписокЗадачПоказать), typeof(СписокЗадач), new UIPropertyMetadata(СписокЗадачПоказать.Входящие, new PropertyChangedCallback(ПоказатьPropertyChanged)));
        private static void ПоказатьPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var obj = d as СписокЗадач;
            if (obj == null) return;

            switch ((СписокЗадачПоказать)e.NewValue)
            {
                case СписокЗадачПоказать.Входящие:
                case СписокЗадачПоказать.КИсполнению:
                case СписокЗадачПоказать.Срочно:
                    obj.ТаблицаСообщения.ItemContainerStyle = obj.TryFindResource("Inbox") as Style;
                    break;

                case СписокЗадачПоказать.Отправленые:
                    obj.ТаблицаСообщения.ItemContainerStyle = obj.TryFindResource("Outbox") as Style;
                    break;
            }

        }

        public object ItemsSource
        {
            get { return (object)GetValue(ItemsSourceProperty); }
            set { SetValue(ItemsSourceProperty, value); }
        }
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register("ItemsSource", typeof(object), typeof(СписокЗадач), new UIPropertyMetadata(null));






        public СписокЗадач()
        {
            InitializeComponent();
        }
        private void ТаблицаСообщения_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var row = ТаблицаСообщения.SelectedItem as DataRowView;
            if (row != null && Convert.ToBoolean(row["@Новый"]))
            {
                this.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, (Action)delegate
                {
                    row["@Новый"] = false;

                    //сбросить счетчик
                    var tasks = RosControl.Helper.FindParentControl<МоиЗадачи>(this);
                    if (tasks != null)
                    {
                        var count = Convert.ToInt32(tasks.Входящие) - 1;
                        tasks.Входящие = count > 0 ? (object)count : null;
                    }
                });

                System.Threading.Tasks.Task.Factory.StartNew((__e) =>
                {
                    try
                    {
                        using (RosService.Client client = new RosService.Client())
                        {
                            client.Архив.СохранитьЗначение((decimal)__e, "@Новый", false);
                        }
                    }
                    catch (Exception)
                    {

                    }
                }, row["id_node"]);
            }


            #region Загрузить сообщение
            var id_node = Convert.ToDecimal(ТаблицаСообщения.SelectedValue);
            if (id_node == 0)
            {
                ТекстСообщения = string.Empty;
                return;
            }

            ТекстСообщения = "Загрузка...";
            //ThreadPool.QueueUserWorkItem((WaitCallback)delegate
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                try
                {
                    using (RosService.Client client = new RosService.Client())
                    {
                        var text = client.Архив.ПолучитьЗначение<string>(id_node, "Содержание");
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate
                        {
                            ТекстСообщения = text;
                        });
                    }
                }
                catch (Exception ex)
                {
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate { ТекстСообщения = ex.Message; });
                }
            });
            #endregion
        }

        //private void СменитьСтатусЗадачи(DataRowView row)
        //{
        //    if (row == null) return;

        //    var status = row.Field<string>("Статус");
        //    row["ДатаЗавершения"] = status == "Готово" ? (object)DateTime.Now : Convert.DBNull;

        //    this.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, (Action)delegate
        //    {
        //        var tik = status == "Готово" ? -1 : 1;
        //        var tasks = RosControl.Helper.FindParentControl<МоиЗадачи>(this);
        //        if (tasks != null)
        //        {
        //            var count = Convert.ToInt32(tasks.КИсполнению) + tik;
        //            tasks.КИсполнению = count > 0 ? (object)count : null;

        //            if (row.Field<bool>("Срочно"))
        //            {
        //                count = Convert.ToInt32(tasks.Срочные) + tik;
        //                tasks.Срочные = count > 0 ? (object)count : null;
        //            }
        //        }

        //        var main = RosControl.Helper.FindParentControl<Main>(this);
        //        if (main != null)
        //        {
        //            var count = Convert.ToInt32(main.КоличествоНовыхЗадач) + tik;
        //            main.КоличествоНовыхЗадач = count > 0 ? (object)count : null;
        //        }
        //    });

        //    System.Threading.Tasks.Task.Factory.StartNew((__e) =>
        //    {
        //        try
        //        {
        //            using (RosService.Client client = new RosService.Client())
        //            {
        //                var v = new Dictionary<string, object>();
        //                v.Add("ДатаЗавершения", status == "Готово" ? (object)DateTime.Now : null);
        //                v.Add("Статус", status);
        //                client.Архив.СохранитьЗначение((decimal)__e, v, RosService.Data.Хранилище.Оперативное, false);
        //            }
        //        }
        //        catch (Exception)
        //        {

        //        }
        //    }, row["id_node"]);
        //}
        private void Main_Loaded(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
            {
                Обновить();
            }
        }
        private void Обновить()
        {
            #region Запрос
            var query = new Query();
            query.ДобавитьТипы("СлужебнаяЗадача%");
            query.ДобавитьВыводимыеКолонки("ДатаСозданияОбъекта", "СсылкаНаПользователя", "СсылкаНаОбъект", "НазваниеОбъекта", 
                "@Новый", "Срок", "Вложения", "Статус", "Срочно", "ДатаЗавершения");
            query.ДобавитьСортировку("Срок", RosService.Data.Query.НаправлениеСортировки.Desc);

            switch (Показать)
            {
                case СписокЗадачПоказать.Входящие:
                    query.ДобавитьУсловиеПоиска("СсылкаНаОбъект", RosService.Client.User.id_node);
                    break;

                case СписокЗадачПоказать.КИсполнению:
                    query.ДобавитьУсловиеПоиска("СсылкаНаОбъект", RosService.Client.User.id_node);
                    query.ДобавитьУсловиеПоиска("Статус", "В работе");
                    break;
                case СписокЗадачПоказать.Срочно:
                    query.ДобавитьУсловиеПоиска("СсылкаНаОбъект", RosService.Client.User.id_node);
                    query.ДобавитьУсловиеПоиска("Статус", "В работе");
                    query.ДобавитьУсловиеПоиска("Срочно", true);
                    break;

                case СписокЗадачПоказать.Отправленые:
                    if (Пользователь != null) 
                        query.ДобавитьУсловиеПоиска("СсылкаНаОбъект", Пользователь);
                    query.ДобавитьУсловиеПоиска("СсылкаНаПользователя", RosService.Client.User.id_node);
                    break;
            }
            #endregion

            ItemsSource = null;

            System.Threading.Tasks.Task.Factory.StartNew((e) =>
            {
                try
                {
                    using (RosService.Client client = new RosService.Client())
                    {
                        var items = client.Архив.Поиск((Query)e).Значение.AsDataView();
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate
                        {
                            ItemsSource = items;
                        });
                    }
                }
                catch (Exception)
                {
                }
            }, query);
        }

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            var item = sender as FrameworkElement;
            if (item == null) 
                return;

            //СменитьСтатусЗадачи(item.DataContext as DataRowView);
        }
        private void Find_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Обновить();
        }
        private void Чат_ОтправкаСообщения(object sender, RosControl.UI.ЧатEventArgs e)
        {
            var obj = sender as RosControl.UI.Чат;
            if (obj == null || obj.id_node == 0) return;

            System.Threading.Tasks.Task.Factory.StartNew((id) =>
            {
                try
                {
                    using (RosService.Client client = new RosService.Client())
                    {
                        client.Архив.СохранитьЗначение((decimal)id, "@Новый", true);
                    }
                }
                catch (Exception)
                {
                }
            }, obj.id_node);
        }
        private void Delete_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var row = ТаблицаСообщения.SelectedItem as DataRowView;
            if (row != null && RosControl.Helper.УдалитьРаздел(row.Field<string>("НазваниеОбъекта"), row.Field<decimal>("id_node"), Хранилище.Оперативное, null))
            {
                Обновить();
            }
        }
    }

    public class StatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if ("Готово".Equals(value))
                return true;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (true.Equals(value))
                return "Готово";
            return "В работе";
        }
    }
    public class UserTaskConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var row = value as System.Data.DataRowView;
            if (row != null && row["СсылкаНаПользователя"].Equals(RosService.Client.User.id_node))
                return null;
            return row["СсылкаНаПользователя.НазваниеОбъекта"];
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
        }
    }
    public class UserTaskOutboxConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var row = value as System.Data.DataRowView;
            if (row != null && row["СсылкаНаОбъект"].Equals(RosService.Client.User.id_node))
                return null;
            return row["СсылкаНаОбъект.НазваниеОбъекта"];
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
        }
    }
}
