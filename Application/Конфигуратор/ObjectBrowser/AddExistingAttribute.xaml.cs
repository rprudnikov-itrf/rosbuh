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
using RosService.Configuration;
using System.Threading;
using System.Windows.Threading;
using System.ComponentModel;

namespace RosControl.Designer
{
    /// <summary>
    /// Interaction logic for AddExistingAttribute.xaml
    /// </summary>
    public partial class AddExistingAttribute : Window
    {
        protected List<RosService.Configuration.Type> СписокТипов { get; private set; }

        public bool IsLoading
        {
            get { return (bool)GetValue(IsLoadingProperty); }
            set { SetValue(IsLoadingProperty, value); }
        }
        public static readonly DependencyProperty IsLoadingProperty =
            DependencyProperty.Register("IsLoading", typeof(bool), typeof(AddExistingAttribute), new UIPropertyMetadata(false));

        public AddExistingAttribute()
        {
            InitializeComponent();
        }

        private void Дерево_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            DialogResult = true;
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            IsLoading = true;
            //ThreadPool.QueueUserWorkItem((WaitCallback)delegate
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                try
                {
                    using (RosService.Client client = new RosService.Client())
                    {
                        this.СписокТипов = client.Конфигуратор.СписокТипов().ToList();
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate
                        {
                            ОбновитьДерево();
                            IsLoading = false;
                        });
                    }
                }
                catch (Exception ex)
                {
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                    {
                        IsLoading = false;
                        throw ex;
                    });
                }
            });
        }

        private void ОбновитьДерево()
        {
            ОбновитьДерево(Фильтр.Text);
        }
        private void ОбновитьДерево(string filter)
        {
            Дерево.BeginInit();
            var cv = (CollectionView)CollectionViewSource.GetDefaultView(СписокТипов);
            if (cv != null)
            {
                cv.GroupDescriptions.Clear();
                cv.SortDescriptions.Clear();
                cv.Filter = new Predicate<object>(delegate(object obj)
                {
                    var item = obj as RosService.Configuration.Type;
                    if (item == null) return false;
                    return item.Описание.ToLower().Contains(Convert.ToString(filter).ToLower());
                });
                cv.SortDescriptions.Add(new SortDescription("Namespace", ListSortDirection.Ascending));
                cv.SortDescriptions.Add(new SortDescription("Описание", ListSortDirection.Ascending));
                cv.GroupDescriptions.Add(new PropertyGroupDescription("Namespace"));
                Дерево.ItemsSource = cv.Groups.Cast<CollectionViewGroup>().Select(p => new RosControl.UI.TreeViewItem()
                {
                    Header = p.Name,
                    IconType = 270,
                    IsExpanded = !string.IsNullOrEmpty(filter),
                    ItemsSource = p.Items.Cast<RosService.Configuration.Type>().Select(g => new RosControl.UI.TreeViewItem()
                    {
                        Header = g.Описание,
                        IconType = g.Name,
                        Tag = g.Name
                    })
                });
            }
            Дерево.EndInit();
        }
        private void Фильтр_TextSearched(object sender, RosControl.UI.SearchTextBoxChangedEventArgs e)
        {
            ОбновитьДерево(e.Фильтр);
        }

        private void Window_Complite(object sender, EventArgs e)
        {
            DialogResult = true;
        }
    }
}
