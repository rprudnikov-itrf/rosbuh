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
using System.Windows.Threading;
using System.Data;

namespace RosApplication.Конфигуратор
{
    /// <summary>
    /// Interaction logic for Статистика.xaml
    /// </summary>
    public partial class Статистика : UserControl
    {
        public Статистика()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
            {
                //ThreadPool.QueueUserWorkItem((WaitCallback)delegate
                System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    try
                    {
                        using (RosService.Client client = new RosService.Client())
                        {
                            var items = null as EnumerableRowCollection<DataRow>;
                            try
                            {
                                var query = new RosService.Data.Query();
                                query.Sql = @"
                                select Name = '*Данных', count(*) 'Count' from tblValue
                                union
                                select Name = '*Разделов', count(*) 'Count' from tblNode
                                union
                                select [type] 'Name', count(*) 'Count' from tblNode group by [type]
                                ";
                                items = client.Архив.Поиск(query).AsEnumerable().OrderByDescending(p => p.Field<int>("Count"));

                                this.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate
                                {
                                    PART_ItemsControl.ItemsSource = items;
                                    PART_ItemsControl.Visibility = System.Windows.Visibility.Visible;
                                });
                            }
                            catch(Exception ex)
                            {
                                this.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate
                                {
                                    PART_TextBlock1.Text = ex.ToString();
                                    PART_ItemsControl.Visibility = System.Windows.Visibility.Collapsed;
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
        }
    }
}
