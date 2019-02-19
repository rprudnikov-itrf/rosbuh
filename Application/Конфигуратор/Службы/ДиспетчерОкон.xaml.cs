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
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RosApplication.Конфигуратор.Службы
{
    /// <summary>
    /// Interaction logic for ДиспетчерОкон.xaml
    /// </summary>
    public partial class ДиспетчерОкон : Window
    {
        [DllImport("kernel32.dll")]
        public static extern bool SetProcessWorkingSetSize(IntPtr handle, int minimumWorkingSetSize, int maximumWorkingSetSize);

        public ДиспетчерОкон()
        {
            InitializeComponent();
            Update();
        }

        private void HyperLink_Click(object sender, RoutedEventArgs e)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            //очистить память
            if(Environment.OSVersion.Platform == PlatformID.Win32NT)
                SetProcessWorkingSetSize(System.Diagnostics.Process.GetCurrentProcess().Handle, -1, -1);

            Update();
        }

        void Update()
        {
            Memory.Text = string.Format("{0:N0}Мб", Process.GetCurrentProcess().PrivateMemorySize64 / 1024 / 1024);
        }
    }
}
