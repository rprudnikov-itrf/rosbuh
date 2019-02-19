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

namespace RosApplication.Конфигуратор
{
    /// <summary>
    /// Interaction logic for ПроиндексироватьДанные.xaml
    /// </summary>
    public partial class ПроиндексироватьДанные : Window
    {
        private DispatcherTimer _timer;
        public ПроиндексироватьДанные()
        {
            InitializeComponent();
        }



        public TimeSpan ПрошлоВремени
        {
            get { return (TimeSpan)GetValue(ПрошлоВремениProperty); }
            set { SetValue(ПрошлоВремениProperty, value); }
        }
        public static readonly DependencyProperty ПрошлоВремениProperty =
            DependencyProperty.Register("ПрошлоВремени", typeof(TimeSpan), typeof(ПроиндексироватьДанные), new UIPropertyMetadata(TimeSpan.Zero));




        private void Window_Complite(object sender, EventArgs e)
        {
            Форма.IsEnabledComplite = false;
            var t = DateTime.Now;
            var Оперативное = PART_Оперативное.IsChecked.Value;
            var Конфигурация = PART_Конфигурация.IsChecked.Value;
            ПрошлоВремени = TimeSpan.Zero;
            PART_ProgressBar.Value = 1;

            _timer = new DispatcherTimer(DispatcherPriority.Normal);
            _timer.Interval = TimeSpan.FromMilliseconds(500);
            _timer.Tick += delegate
            {
                ПрошлоВремени = DateTime.Now - t;
            };
            _timer.Start();

            //ThreadPool.QueueUserWorkItem((WaitCallback)delegate

            var __Асинхронно = Асинхронно.IsChecked.Value;
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                try
                {
                    using (RosService.Client client = new RosService.Client(TimeSpan.FromMinutes(30)))
                    {
                        if (Оперативное) client.Архив.Проиндексировать(0, RosService.Data.Хранилище.Оперативное, RosService.Client.Domain, __Асинхронно);
                        if (Конфигурация) client.Архив.Проиндексировать(0, RosService.Data.Хранилище.Конфигурация, RosService.Client.Domain, __Асинхронно);

                        if (__Асинхронно) 
                            System.Threading.Thread.Sleep(1500);

                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                        {
                            if (__Асинхронно)
                                MessageBox.Show("Индексирование базы данных запущено в асинхронном режиме, подробнее в Сервис > Диспетчер задач.");

                            Форма.IsEnabledComplite = true;
                            PART_ProgressBar.Value = 0;
                            _timer.Stop();
                        });
                    }
                }
                catch (Exception ex)
                {
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate 
                    {
                        Форма.IsEnabledComplite = true;
                        PART_ProgressBar.Value = 0;
                        _timer.Stop();
                        MessageBox.Show(ex.Message); 
                    });
                }
            });
        }

        private void PART_Window_Closed(object sender, EventArgs e)
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer = null;
            }
        }
    }
}
