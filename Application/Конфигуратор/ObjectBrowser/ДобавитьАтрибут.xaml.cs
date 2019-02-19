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
using System.ComponentModel;
using System.Threading;
using System.Windows.Threading;


namespace RosApplication.Конфигуратор
{
    public partial class ДобавитьАтрибут : Window
    {
        public ДобавитьАтрибут()
        {
            InitializeComponent();
        }

        private void Window_Complite(object sender, EventArgs e)
        {
            DialogResult = true;
        }
        private void Window_Initialized(object sender, EventArgs e)
        {
            //ThreadPool.QueueUserWorkItem((WaitCallback)delegate
            //System.Threading.Tasks.Task.Factory.StartNew(() =>
            //{
            //    try
            //    {
            //        using (RosService.Client client = new RosService.Client())
            //        {
            //            var types = client.Конфигуратор.СписокТипов();
            //            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
            //            {
            //                ТипДанных.ItemsSource = types;
            //            });
            //        }
            //    }
            //    catch
            //    {
            //    }
            //});
        }

        private void Описание_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(Описание.Text))
            {
                Имя.Text = null;
                return;
            }
            using (RosService.Client client = new RosService.Client())
            {
                Имя.Text = client.Конфигуратор.ОписаниеВИмя(Описание.Text);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
            {
                this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                {
                    Описание.Focus();
                });
            }
        }

        private void Описание_ValueChanged(object sender, RosControl.UI.SearchTextBoxValueChangedEventArgs e)
        {
            using (RosService.Client client = new RosService.Client())
            {
                var type = client.Конфигуратор.ПолучитьТип(Convert.ToString(Описание.Value));
                if (type != null) ТипДанныхОписание.Text = type.BaseType;
            }
        }
    }
}
