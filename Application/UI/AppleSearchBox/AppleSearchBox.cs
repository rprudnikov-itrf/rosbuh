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

namespace RosApplication.UI
{
    public class AppleSearchBox : TextBox
    {
        public bool IsDropDownOpen
        {
            get { return (bool)GetValue(IsDropDownOpenProperty); }
            set { SetValue(IsDropDownOpenProperty, value); }
        }
        public static readonly DependencyProperty IsDropDownOpenProperty =
            DependencyProperty.Register("IsDropDownOpen", typeof(bool), typeof(AppleSearchBox), new UIPropertyMetadata(false));


        static AppleSearchBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(AppleSearchBox), new FrameworkPropertyMetadata(typeof(AppleSearchBox)));
        }
    }
}
