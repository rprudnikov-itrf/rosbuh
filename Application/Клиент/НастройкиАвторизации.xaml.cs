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
using System.Collections.ObjectModel;
using System.Collections;
using System.ComponentModel;
using Microsoft.Win32;

namespace RosApplication.Клиент
{
    /// <summary>
    /// Interaction logic for НастройкиАвторизации.xaml
    /// </summary>
    public partial class НастройкиАвторизации : Window
    {
        [Bindable(true)]
        public string АдресСервера
        {
            get
            {
                //return Properties.Settings.Default.АдресСервера;
                return App.ServerName;
            }
            set
            {
                //if (Properties.Settings.Default.ИмяПользователя != value)
                if (App.UserName != value)
                {
                    //Properties.Settings.Default.АдресСервера = (value ?? "").Trim();
                    App.ServerName = (value ?? "").Trim();
                }
            }
        }
        //[Bindable(true)]
        //public IEnumerable СписокПользователей
        //{
        //    get
        //    {
        //        if (Properties.Settings.Default.History != null)
        //        {
        //            return Properties.Settings.Default.History.Cast<string>().ToArray().OrderBy(p => p);
        //        }
        //        return null;
        //    }
        //}

        public НастройкиАвторизации()
        {
            InitializeComponent();

            try
            {
                if (AppDomain.CurrentDomain.SetupInformation.ActivationArguments != null)
                {
                    Путь.Text = string.Join("|", AppDomain.CurrentDomain.SetupInformation.ActivationArguments.ActivationData);
                }

                var proxy = System.Net.WebProxy.GetDefaultProxy();
                if (proxy != null)
                {
                    proxyServer.Text = proxy.Address.ToString();
                }
            }
            catch
            {
            }
        }

        //private void Очистить_Click(object sender, RoutedEventArgs e)
        //{
        //    if (MessageBox.Show("Вы действительно хотите очисть историю?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        //    {
        //        Properties.Settings.Default.History = null;
        //        Properties.Settings.Default.Save();
        //    }
        //}

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog()
            {
                Filter = "(*.txt)|*.txt",
                RestoreDirectory = true,
                FileName = "logon.txt"
            };
            if (dialog.ShowDialog().Value)
            {
                System.IO.File.WriteAllLines(dialog.FileName, Properties.Settings.Default.History.Cast<string>().ToArray());
            }
        }
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog()
            {
                Filter = "(*.txt)|*.txt",
                RestoreDirectory = true
            };
            if (dialog.ShowDialog().Value)
            {
                Properties.Settings.Default.History.Clear();
                Properties.Settings.Default.History.AddRange(System.IO.File.ReadAllLines(dialog.FileName));
                Properties.Settings.Default.Save();
            }
        }

        private void Delete_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            //if (PART_ListBox.SelectedItem == null) return;
            //foreach (var item in PART_ListBox.SelectedItems.Cast<object>().ToArray())
            //{
            //    Properties.Settings.Default.History.Remove(Convert.ToString(item));
            //    PART_ListBox.ItemsSource = null;
            //    PART_ListBox.ItemsSource = СписокПользователей;
            //}
        }
        private void Delete_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            //e.CanExecute = PART_ListBox.SelectedItem != null;
        }

        private void Сохранить(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.Save();
            RosService.Client.Host = АдресСервера;

            //if (Accounts.IsEdit)
            //{
            //    App.SaveHistoryAccounts((ObservableCollection<App.HistoryAccount>)Accounts.ItemsSource);
            //    App.HistoryAccounts = ((ObservableCollection<App.HistoryAccount>)Accounts.ItemsSource).ToArray();
            //}
            DialogResult = true;
        }

        private void Accounts_SearchedComplite(object sender, RosControl.UI.DataGridArgs e)
        {
            Accounts.ItemsSource = new ObservableCollection<App.HistoryAccount>(App.HistoryAccounts.OrderBy(p => p.Login));
        }
    }
}
