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
using System.Collections.ObjectModel;
using System.Windows.Threading;
using System.Data;
using System.Threading.Tasks;

namespace RosApplication.Конфигуратор.Службы
{
    public partial class ДиспетчерЗадач : Window
    {
        public object ItemsProc
        {
            get { return (object)GetValue(ItemsProcProperty); }
            set { SetValue(ItemsProcProperty, value); }
        }
        public static readonly DependencyProperty ItemsProcProperty =
            DependencyProperty.Register("ItemsProc", typeof(object), typeof(ДиспетчерЗадач), new UIPropertyMetadata(null));

        private DispatcherTimer timer;


        public ДиспетчерЗадач()
        {
            InitializeComponent();

            timer = new DispatcherTimer(DispatcherPriority.Loaded);
            timer.Interval = TimeSpan.FromSeconds(10);
            timer.Tick += new EventHandler(timer_Tick);
            Обновить();
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            Обновить();
        }

        public void Обновить()
        {
            if (timer != null) timer.Stop();
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                try
                {
                    using (RosService.Client client = new RosService.Client())
                    {
                        var query = new RosService.Data.Query(50);
                        query.ДобавитьТипы("Процесс%");
                        query.ДобавитьВыводимыеКолонки("Описание", "СтатусПроцесса", "ТекущееСостояниеПроцесса", "ВремяРаботыПроцесса");
                        query.ДобавитьСортировку("ДатаСозданияОбъекта", RosService.Data.Query.НаправлениеСортировки.Desc);
                        query.ДобавитьУсловиеПоиска("@СкрытьРаздел", false);
                        var items = new ObservableCollection<DataRow>(client.Архив.Поиск(query).AsEnumerable());

                        this.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, (Action)delegate
                        {
                            ItemsProc = items;
                            if (timer != null) 
                                timer.Start();
                        });
                    }
                }
                catch (Exception)
                {
                }
            });
        }
        private void Обновить_Click(object sender, RoutedEventArgs e)
        {
            Обновить();
        }

        private void CommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (PART_ListBox.SelectedItem != null)
            {
                if (MessageBox.Show("Вы действительно хотите скрыть выбранные записи?", "Предупреждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    using (RosService.Client client = new RosService.Client())
                    {
                        Parallel.ForEach(PART_ListBox.SelectedItems.Cast<DataRow>(), i =>
                        {
                            client.Архив.СохранитьЗначение(i.Field<decimal>("id_node"), "@СкрытьРаздел", true);
                        });
                    }
                    Обновить();
                }
            }
            else
            {
                var row = Convert.ToDecimal(e.Parameter);
                if (row > 0)
                {
                    using (RosService.Client client = new RosService.Client())
                    {
                        client.Архив.СохранитьЗначение(row, "@СкрытьРаздел", true);
                    }
                    Обновить();
                }
            }
        }
    }

    public class IntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return System.Convert.ToInt32(value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class TimerConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var row = value as DataRow;
            if (row != null && row.Field<string>("СтатусПроцесса") == "В работе")
            {
                return " " + new RosControl.Converters.TimerConverter()
                    .Convert((DateTime.Now - row.Field<DateTime>("ДатаСозданияОбъекта")).TotalMilliseconds,
                    typeof(TimeSpan), null, null);
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
