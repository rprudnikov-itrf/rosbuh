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
using System.Windows.Threading;

namespace RosApplication.Клиент.Задачи
{
    /// <summary>
    /// Interaction logic for МоиЗадачи.xaml
    /// </summary>
    public partial class МоиЗадачи : UserControl
    {
        public bool ПоказатьВсех
        {
            get { return (bool)GetValue(ПоказатьВсехProperty); }
            set { SetValue(ПоказатьВсехProperty, value); }
        }
        public static readonly DependencyProperty ПоказатьВсехProperty =
            DependencyProperty.Register("ПоказатьВсех", typeof(bool), typeof(МоиЗадачи), new UIPropertyMetadata(false, new PropertyChangedCallback(ПоказатьВсехPropertyChanged)));
        private static void ПоказатьВсехPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var obj = d as МоиЗадачи;
            if (obj == null) return;

            var cs = obj.TryFindResource("TasksSource") as CollectionViewSource;
            if (cs != null) cs.View.Refresh();
        }



        public object Входящие
        {
            get { return (object)GetValue(ВходящиеProperty); }
            set { SetValue(ВходящиеProperty, value); }
        }
        public static readonly DependencyProperty ВходящиеProperty =
            DependencyProperty.Register("Входящие", typeof(object), typeof(МоиЗадачи), new UIPropertyMetadata(null));

        public object КИсполнению
        {
            get { return (object)GetValue(КИсполнениюProperty); }
            set { SetValue(КИсполнениюProperty, value); }
        }
        public static readonly DependencyProperty КИсполнениюProperty =
            DependencyProperty.Register("КИсполнению", typeof(object), typeof(МоиЗадачи), new UIPropertyMetadata(null));

        public object Срочные
        {
            get { return (object)GetValue(СрочныеProperty); }
            set { SetValue(СрочныеProperty, value); }
        }
        public static readonly DependencyProperty СрочныеProperty =
            DependencyProperty.Register("Срочные", typeof(object), typeof(МоиЗадачи), new UIPropertyMetadata(null));




        public МоиЗадачи()
        {
            InitializeComponent();
        }

        private void TasksSource_Filter(object sender, FilterEventArgs e)
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
                    : item.Задачи != null && item.Задачи.Количество > 0;
            }
        }
        private void ОткрытьЗадачи(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is RosApplication.Клиент.Main.РабочаОбластьПользователя)
            {
                this.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate
                {
                    PART_Content.Content = new RosApplication.Клиент.Задачи.СписокЗадач()
                    {
                        Header = ((RosApplication.Клиент.Main.РабочаОбластьПользователя)e.NewValue).Пользователь.НазваниеОбъекта,
                        Показать = СписокЗадачПоказать.Отправленые,
                        Пользователь = ((RosApplication.Клиент.Main.РабочаОбластьПользователя)e.NewValue).id_node
                    };

                    //PART_Content.Content = new RosApplication.Клиент.Задачи.СписокПоручений()
                    //{
                    //    Пользователь = ((RosApplication.Клиент.Main.РабочаОбластьПользователя)e.NewValue).id_node,
                    //    Header = ((RosApplication.Клиент.Main.РабочаОбластьПользователя)e.NewValue).Пользователь.НазваниеОбъекта,
                    //};
                });
            }
            else if (e.NewValue is System.Windows.Controls.TreeViewItem)
            {
                var _Показать = Задачи.СписокЗадачПоказать.Входящие;
                switch (((System.Windows.Controls.TreeViewItem)e.NewValue).Name)
                {
                    case "ЗадачиКИсполнению":
                        _Показать = Задачи.СписокЗадачПоказать.КИсполнению;
                        break;

                    case "ЗадачиСрочные":
                        _Показать = Задачи.СписокЗадачПоказать.Срочно;
                        break;

                    case "ЗадачиВходящие":
                        _Показать = Задачи.СписокЗадачПоказать.Входящие;
                        break;

                    case "ЗадачиОтправленные":
                        _Показать = Задачи.СписокЗадачПоказать.Отправленые;
                        break;
                        //PART_Content.Content = new RosApplication.Клиент.Задачи.СписокПоручений()
                        //{
                        //    Header = "Отправленные",
                        //};
                        //return;

                    default:
                        return;
                }
                this.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate
                {
                    PART_Content.Content = new RosApplication.Клиент.Задачи.СписокЗадач()
                    {
                        Header = ((System.Windows.Controls.TreeViewItem)e.NewValue).Header,
                        Показать = _Показать,
                    };
                });
            }
        }
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (IsLoaded && PART_Content.Content == null)
            {
                this.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, (Action)delegate
                {
                     PART_Content.Content = new RosApplication.Клиент.Задачи.СписокЗадач() { Header = "Входящие", Показать = Задачи.СписокЗадачПоказать.Входящие };
                });
            }
        }
    }
}
