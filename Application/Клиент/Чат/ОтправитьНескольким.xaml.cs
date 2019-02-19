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
using RosControl.Forms.Пользователи;

namespace RosApplication.Клиент.Чат
{
    /// <summary>
    /// Interaction logic for ОтправитьНескольким.xaml
    /// </summary>
    public partial class ОтправитьНескольким : Window
    {
        public IEnumerable<ВыбранныйПользователь> Items
        {
            get { return (IEnumerable<ВыбранныйПользователь>)GetValue(ItemsProperty); }
            set { SetValue(ItemsProperty, value); }
        }
        public static readonly DependencyProperty ItemsProperty =
            DependencyProperty.Register("Items", typeof(IEnumerable<ВыбранныйПользователь>), typeof(ОтправитьНескольким), new UIPropertyMetadata(null));

        public ОтправитьНескольким()
        {
            InitializeComponent();
        }

        private void Кому_Click(object sender, RoutedEventArgs e)
        {
            var window = new RosControl.Forms.Пользователи.НайтиПользователя();
            if (window.ShowDialog().Value)
            {
                Items = window.SelectedItems.Union(Items ?? Enumerable.Empty<ВыбранныйПользователь>())
                    .Distinct().OrderBy(p => p.НазваниеОбъекта)
                    .ToArray();
            }
        }
        private void Delete_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Items = Items.Except(((ListBox)sender).SelectedItems.Cast<ВыбранныйПользователь>()).ToArray();
        }
        private void CommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (Items == null) return;

            var id_nodes = ((IEnumerable<object>)Items).Cast<ВыбранныйПользователь>().Select(p => (object)p.id_node).ToArray();
            var _text = PART_Сообщение.Text;
            //ThreadPool.QueueUserWorkItem((WaitCallback)delegate
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                try
                {
                    using (RosService.Client client = new RosService.Client())
                    {
                        client.Сервисы.СообщенияПользователя_Добавить(client.СведенияПользователя.id_node, id_nodes, _text, client.Пользователь, client.Домен);
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                        {
                            this.Close();
                        });
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
