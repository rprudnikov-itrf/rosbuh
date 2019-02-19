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
using System.Windows.Controls.Primitives;
using System.Threading;

namespace RosApplication.Клиент.Чат
{
    /// <summary>
    /// Логика взаимодействия для МоиСообщения.xaml
    /// </summary>
    public partial class МоиСообщения : UserControl
    {
        private Dictionary<object, System.Windows.Window> chats = new Dictionary<object, System.Windows.Window>();

        public МоиСообщения()
        {
            InitializeComponent();
        }

        public bool ПоказатьВсех
        {
            get { return (bool)GetValue(ПоказатьВсехProperty); }
            set { SetValue(ПоказатьВсехProperty, value); }
        }
        public static readonly DependencyProperty ПоказатьВсехProperty =
            DependencyProperty.Register("ПоказатьВсех", typeof(bool), typeof(МоиСообщения), new UIPropertyMetadata(false, new PropertyChangedCallback(ПоказатьВсехPropertyChanged)));
        private static void ПоказатьВсехPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var obj = d as МоиСообщения;
            if (obj == null) return;

            var cs = obj.TryFindResource("MessagesSource") as CollectionViewSource;
            if (cs != null) cs.View.Refresh();
        }

        private void ОткрытьЧат(object sender)
        {
            var item = ((Selector)sender).SelectedValue;
            if (item == null) return;

            if (PART_Content.Content is IDisposable)
                ((IDisposable)PART_Content.Content).Dispose();

            PART_Content.Content = new RosControl.Forms.ЧатПользователей() { id_node = item };
            PART_UserInfo.DataContext = ((Selector)sender).SelectedItem;
            PART_UserInfo.Visibility = System.Windows.Visibility.Visible;


            var main = RosControl.Helper.FindParentControl<Main>(this);
            if (main != null)
            {
                var user = main.СписокПользователей.FirstOrDefault(p => p.id_node.Equals(item));
                if (user == null || user.Сообщения == 0) return;
                user.Сообщения = 0;
                main.ОбновитьКоличествоНовыхСообщений();

                System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    try
                    {
                        using (RosService.Client client = new RosService.Client())
                        {
                            client.Сервисы.СообщенияПользователя_Очистить(client.СведенияПользователя.id_node, Convert.ToDecimal(item), client.Пользователь, client.Домен);
                        }
                    }
                    catch (Exception)
                    {
                        //this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate { MessageBox.Show(ex.Message); });
                    }
                });
            }
        }
        private void ОткрытьЧатВОкне(object sender)
        {
            var item = ((Selector)sender).SelectedValue;
            if (item == null) return;

            if (chats.ContainsKey(item))
            {
                chats[item].Activate();
            }
            else
            {
                var window = new System.Windows.Window()
                {
                    Title = string.Format("Чат: {0}", ((RosApplication.Клиент.Main.РабочаОбластьПользователя)((Selector)sender).SelectedItem).Пользователь.НазваниеОбъекта),
                    Content = new RosControl.Forms.ЧатПользователей() { id_node = item },
                    Width = 580,
                    Height = 640,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ShowActivated = true
                };
                window.Closed += (object _sender, EventArgs _e) =>
                {
                    chats.Remove(((RosControl.Forms.ЧатПользователей)((System.Windows.Window)_sender).Content).id_node);
                };
                window.Activated += (object _sender, EventArgs _e) =>
                {
                    var main = RosControl.Helper.FindParentControl<Main>(this);
                    if (main == null) return;

                    var id_node = Convert.ToDecimal(((RosControl.Forms.ЧатПользователей)((System.Windows.Window)_sender).Content).id_node);
                    var user = main.СписокПользователей.FirstOrDefault(p => p.id_node == id_node);
                    if (user == null || user.Сообщения == 0) return;
                    user.Сообщения = 0;
                    main.ОбновитьКоличествоНовыхСообщений();

                    System.Threading.Tasks.Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            using (RosService.Client client = new RosService.Client())
                            {
                                client.Сервисы.СообщенияПользователя_Очистить(client.СведенияПользователя.id_node, id_node, client.Пользователь, client.Домен);
                            }
                        }
                        catch (Exception)
                        {
                            //this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate { MessageBox.Show(ex.Message); });
                        }
                    });
                };
                window.Show();
                chats.Add(item, window);
            }
        }
        private void ОткрытьЧат(object sender, ExecutedRoutedEventArgs e)
        {
            switch (Convert.ToString(e.Parameter))
            {
                case "Рассылка":
                    {
                        var window = new RosApplication.Клиент.Чат.ОтправитьНескольким()
                        {
                            Items = e.Source is ListBox ? ((ListBox)e.Source).SelectedItems.Cast<RosApplication.Клиент.Main.РабочаОбластьПользователя>()
                            .Select(p => new RosControl.Forms.Пользователи.ВыбранныйПользователь() { id_node = p.id_node, НазваниеОбъекта = p.Пользователь.НазваниеОбъекта })
                            .ToArray() : null
                        };
                        window.Show();
                    }
                    break;

                case "НовоеОкно":
                    {
                        ОткрытьЧатВОкне(e.Source);
                    }
                    break;

                default:
                    {
                        ОткрытьЧат(e.Source);
                    }
                    break;
            }
            e.Handled = true;
        }
        private void ОткрытьЧат_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = e.Parameter != null || (e.Source is Selector && ((Selector)e.Source).SelectedItem != null);
        }
        private void MessagesSource_Filter(object sender, FilterEventArgs e)
        {
            var item = e.Item as RosApplication.Клиент.Main.РабочаОбластьПользователя;
            if (item == null || item.id_node == RosService.Client.User.id_node)
            {
                e.Accepted = false;
            }
            else
            {
                e.Accepted = ПоказатьВсех
                    ? true
                    : item.Пользователь.ВСети || item.Сообщения > 0;
            }
        }
        //private void ТаблицаСообщения_Searched(object sender, RosControl.UI.DataGridArgs e)
        //{
        //    e.query.КоличествоВыводимыхДанных = 50;
        //    e.query.ДобавитьТипы("СообщениеПользователя");
        //    e.query.ДобавитьУсловиеПоиска("СсылкаНаОбъект", RosService.Client.User.id_node);
        //    e.query.ДобавитьСортировку("ДатаСозданияОбъекта", RosService.Data.Query.НаправлениеСортировки.Desc);
        //    e.query.ДобавитьВыводимыеКолонки("СсылкаНаПользователя", "ТекстСообщения");
        //    e.query.ДобавитьВыводимыеКолонки("cast(ДатаСозданияОбъекта.[value] as date) 'Date'", RosService.Data.Query.ФункцияАгрегации.Sql);
        //}
        private void ТаблицаСообщения_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ОткрытьЧат(e.Source);
            e.Handled = true;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (PART_Content.Content is IDisposable)
                ((IDisposable)PART_Content.Content).Dispose();

            PART_Content.Content = null;
            PART_UserInfo.DataContext = null;
            PART_UserInfo.Visibility = System.Windows.Visibility.Collapsed;
        }
    }
}
