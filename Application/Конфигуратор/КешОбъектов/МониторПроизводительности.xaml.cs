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
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace RosApplication.Конфигуратор
{
    /// <summary>
    /// Логика взаимодействия для МониторПроизводительности.xaml
    /// </summary>
    public partial class МониторПроизводительности : Window
    {
        public ObservableCollection<RosService.Configuration.PerformanceItem> Items
        {
            get { return (ObservableCollection<RosService.Configuration.PerformanceItem>)GetValue(ItemsProperty); }
            set { SetValue(ItemsProperty, value); }
        }
        public static readonly DependencyProperty ItemsProperty =
            DependencyProperty.Register("Items", typeof(ObservableCollection<RosService.Configuration.PerformanceItem>), typeof(МониторПроизводительности), new UIPropertyMetadata(null));


        public string SortMemberPath { get; set; }
        public МониторПроизводительности()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            SortMemberPath = "PercentCount";
            Обновить();
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Обновить();
        }
        private void Обновить()
        {
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                try
                {
                    using (RosService.Client client = new RosService.Client())
                    {
                        var items = client.Конфигуратор.МониторПроизводительности();
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                        {
                            Items = new ObservableCollection<RosService.Configuration.PerformanceItem>(items);
                            var cv = (CollectionView)CollectionViewSource.GetDefaultView(PART_DataGrid.ItemsSource);
                            if (cv != null)
                            {
                                cv.SortDescriptions.Add(new SortDescription(SortMemberPath, ListSortDirection.Descending));
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate { MessageBox.Show(ex.Message); });
                }
            });
        }

        private void PART_DataGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            SortMemberPath = e.Column.SortMemberPath;
        }
    }
}
