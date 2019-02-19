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

namespace RosApplication.Конфигуратор.Службы
{
    /// <summary>
    /// Логика взаимодействия для ЖурналУдалений.xaml
    /// </summary>
    public partial class ЖурналУдалений : Window
    {
        public ЖурналУдалений()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                using (RosService.Client client = new RosService.Client())
                {
                    var items = client.Конфигуратор.ЖурналУдалений(RosService.Client.Domain);
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                    {
                        PART_DataGrid.ItemsSource = items;
                    });
                }
            });
        }

        private void Form_Loaded(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
                Поиск.Focus();
        }
    }
}
