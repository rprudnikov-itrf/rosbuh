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
using System.IO;
using System.Collections.ObjectModel;
using RosControl.Forms.Пользователи;
using Microsoft.Win32;

namespace RosApplication.Клиент.Задачи
{
    /// <summary>
    /// Interaction logic for НоваяЗадача.xaml
    /// </summary>
    public partial class НоваяЗадача : UserControl
    {
        private ObservableCollection<FileInfo> _files = new ObservableCollection<FileInfo>();
        public ObservableCollection<FileInfo> Files
        {
            get
            {
                return _files;
            }
        }

        public IEnumerable<ВыбранныйПользователь> Users
        {
            get { return (IEnumerable<ВыбранныйПользователь>)GetValue(UsersProperty); }
            set { SetValue(UsersProperty, value); }
        }
        public static readonly DependencyProperty UsersProperty =
            DependencyProperty.Register("Users", typeof(IEnumerable<ВыбранныйПользователь>), typeof(НоваяЗадача), new UIPropertyMetadata(null));




        public НоваяЗадача()
        {
            InitializeComponent();
        }

        private void ПрикрепитьФайлы_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog() { Filter = "(*.*)|*.*", RestoreDirectory = true, Multiselect = true };
            if (dialog.ShowDialog().Value)
            {
                foreach (var item in dialog.FileNames)
                {
                    Files.Add(new FileInfo(item));
                }
            }
        }
        private void Delete_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            ((ListBox)sender).ItemsSource = ((ListBox)sender).ItemsSource.Cast<object>()
                .Except(((ListBox)sender).SelectedItems.Cast<object>()).ToArray();
        }
        private void DeleteFiles_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            foreach (var item in ((ListBox)sender).SelectedItems.Cast<FileInfo>().ToArray())
            {
                Files.Remove(item);
            }
        }
        private void Кому_Click(object sender, RoutedEventArgs e)
        {
            var window = new RosControl.Forms.Пользователи.НайтиПользователя();
            if (window.ShowDialog().Value)
            {
                Users = window.SelectedItems.Union(Users ?? Enumerable.Empty<ВыбранныйПользователь>())
                    .Distinct().OrderBy(p => p.НазваниеОбъекта)
                    .ToArray();

                PART_Сообщение.Focus();
            }
        }
        private void Save_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var sb = new StringBuilder();
            if (Users == null || Users.Count() == 0)
                sb.Append("В поле 'Кому'  необходимо указать по крайней мере одного получателя\n");
            if (PART_Сообщение.Text.Length == 0)
                sb.Append("Заполните текст задачи");
            if (sb.Length > 0)
                throw new Exception(sb.ToString());



            var _id_nodes = Users.Select(p => (object)p.id_node).ToArray();
            var _text = PART_Сообщение.Text;
            var _high = Срочно.IsChecked.Value;
            var _end = Срок.SelectedDate;
            //ThreadPool.QueueUserWorkItem((WaitCallback)delegate
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                try
                {
                    using (RosService.Client client = new RosService.Client())
                    {
                        client.Сервисы.ЗадачаПользователя_Добавить(
                            client.СведенияПользователя.id_node,
                            _id_nodes, _text, _high, _end,
                            _files.ToDictionary(p => p.Name, q => File.ReadAllBytes(q.FullName)),
                            client.Пользователь, client.Домен);
                    }
                }
                catch (Exception ex)
                {
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate { MessageBox.Show(ex.Message); });
                }
            });
            var window = RosControl.Helper.FindParentControl<Window>(this);
            if (window != Application.Current.MainWindow)
            {
                window.Close();
            }
        }

        private void Window1_ПроверкаЗначений(object sender, RosControl.UI.EventValidArgs e)
        {
            //var sb = new StringBuilder();

            //if (Users == null || Users.Count() == 0)
            //    sb.Append("В поле 'Кому'  необходимо указать по крайней мере одного получателя\n");

            //if (sb.Length > 0)
            //    throw new Exception(sb.ToString());
        }

        private void CanSave_Executed(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
            //e.CanExecute = Users != null && Users.Count() > 0 && PART_Сообщение.Text.Length > 0;
        }

        private void Main_Loaded(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
            {
                PART_Сообщение.Focus();
            }
        }
    }
}
