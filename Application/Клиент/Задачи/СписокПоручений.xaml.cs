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
using System.Data;
using System.Windows.Threading;

namespace RosApplication.Клиент.Задачи
{
    /// <summary>
    /// Interaction logic for СписокПоручений.xaml
    /// </summary>
    public partial class СписокПоручений : UserControl
    {
        public object Header
        {
            get { return (object)GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }
        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register("Header", typeof(object), typeof(СписокПоручений), new UIPropertyMetadata(null));


        public object Пользователь
        {
            get { return (object)GetValue(ПользовательProperty); }
            set { SetValue(ПользовательProperty, value); }
        }
        public static readonly DependencyProperty ПользовательProperty =
            DependencyProperty.Register("Пользователь", typeof(object), typeof(СписокПоручений), new UIPropertyMetadata(null, new PropertyChangedCallback(ПользовательPropertyChanged)));
        private static void ПользовательPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var obj = d as СписокПоручений;
            if (obj == null) return;

        }



        public string ТекстСообщения
        {
            get { return (string)GetValue(ТекстСообщенияProperty); }
            set { SetValue(ТекстСообщенияProperty, value); }
        }
        public static readonly DependencyProperty ТекстСообщенияProperty =
            DependencyProperty.Register("ТекстСообщения", typeof(string), typeof(СписокПоручений), new UIPropertyMetadata(null));


            

        public СписокПоручений()
        {
            InitializeComponent();
        }


        private void ТаблицаСообщения_Searched(object sender, RosControl.UI.DataGridArgs e)
        {
            e.query.ДобавитьТипы("СлужебнаяЗадача%");
            if(Пользователь != null)
                e.query.ДобавитьУсловиеПоиска("СсылкаНаОбъект", Пользователь);
            e.query.ДобавитьУсловиеПоиска("СсылкаНаПользователя", RosService.Client.User.id_node);
            e.query.ДобавитьВыводимыеКолонки("ДатаСозданияОбъекта", "СсылкаНаПользователя", "НазваниеОбъекта",
                "Вложения", "Срочно", "Статус", "Срок", "ДатаЗавершения", "СсылкаНаОбъект");
            e.query.ДобавитьСортировку("ДатаСозданияОбъекта", RosService.Data.Query.НаправлениеСортировки.Desc);
        }
        private void ТаблицаСообщения_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
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
                        this.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, (Action)delegate
                        {
                            ТекстСообщения = text;
                        });
                    }
                }
                catch (Exception ex)
                {
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate { ТекстСообщения = ex.Message; });
                }
            });
        }
        private void ТаблицаСообщения_CellEditEnding(object sender, Microsoft.Windows.Controls.DataGridCellEditEndingEventArgs e)
        {
            if (e.Column.DisplayIndex == 7)
            {
                СменитьСтатусЗадачи((DataRowView)e.Row.Item);
                
                //var status = ((DataRowView)e.Row.Item)["Статус"].Equals("В работе") ? "Готово" : "В работе";
                //((DataRowView)e.Row.Item)["Статус"] = status;
                //((DataRowView)e.Row.Item)["ДатаЗавершения"] = status == "Готово" ? (object)DateTime.Now : Convert.DBNull;

                //ThreadPool.QueueUserWorkItem((WaitCallback)delegate(object __e)
                //{
                //    try
                //    {
                //        using (RosService.Client client = new RosService.Client())
                //        {
                //            var v = new Dictionary<string, object>();
                //            v.Add("ДатаЗавершения", status == "Готово" ? (object)DateTime.Now : null);
                //            v.Add("Статус", status);
                //            client.Архив.СохранитьЗначение((decimal)__e, v, RosService.Data.Хранилище.Оперативное, false);
                //        }
                //    }
                //    catch (Exception)
                //    {
                        
                //    }
                //}, ((DataRowView)e.Row.Item)["id_node"]);
            }
        }
        private void СменитьСтатусЗадачи(DataRowView row)
        {
            if (row == null) return;

            var status = row["Статус"].Equals("В работе") ? "Готово" : "В работе";
            row["Статус"] = status;
            row["ДатаЗавершения"] = status == "Готово" ? (object)DateTime.Now : Convert.DBNull;

            //ThreadPool.QueueUserWorkItem((WaitCallback)delegate(object __e)
            System.Threading.Tasks.Task.Factory.StartNew((__e) =>
            {
                try
                {
                    using (RosService.Client client = new RosService.Client())
                    {
                        var v = new Dictionary<string, object>();
                        v.Add("ДатаЗавершения", status == "Готово" ? (object)DateTime.Now : null);
                        v.Add("Статус", status);
                        client.Архив.СохранитьЗначение((decimal)__e, v, RosService.Data.Хранилище.Оперативное, false);
                    }
                }
                catch (Exception)
                {

                }
            }, row["id_node"]);
        }
    }
}
