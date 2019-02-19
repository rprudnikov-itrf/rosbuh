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

namespace RosApplication.Конфигуратор.Службы
{
    /// <summary>
    /// Логика взаимодействия для ОткрытьЗапрос.xaml
    /// </summary>
    public partial class ОткрытьЗапрос : Window
    {
        public ОткрытьЗапрос()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            Path.ItemsSource = System.IO.Directory.GetFiles(App.AppPath, "*.rql", System.IO.SearchOption.TopDirectoryOnly);
        }

        private void Path_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var file = Convert.ToString(Path.SelectedItem);
            if (!string.IsNullOrEmpty(file) && System.IO.File.Exists(file))
            {
                PART_Query.Text = System.IO.File.ReadAllText(file);
            }
        }

        private void Path_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (Path.SelectedItem != null)
            {
                DialogResult = true;
            }
        }
    }
}
