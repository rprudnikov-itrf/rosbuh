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
    public partial class TransferWindow : System.Windows.Window
    {
        public string PhoneNumber
        {
            get
            {
                return Phone.Text;
            }
        }
        public TransferWindow()
        {
            InitializeComponent();
        }

        private void Window_Complite(object sender, EventArgs e)
        {
            DialogResult = true;
        }
    }
}
