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
using System.Reflection;

namespace RosApplication.UI.Windows
{
    /// <summary>
    /// Логика взаимодействия для RecordsWindow.xaml
    /// </summary>
    public partial class RecordsWindow : System.Windows.Window
    {
        public string Sip { get; set; }

        public RecordsWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //var db = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(RosService.Client.Domain));
            //if(RosService.Client.Host != RosService.Client.DefaultHost)
            //    db = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(RosService.Client.Domain + "@" + RosService.Client.Host));
            //WebBrowser1.Source = new Uri(string.Format("http://api.itrf.ru/voip/records/?db={0}&sip={1}", db, Sip));
        }
    }
}
