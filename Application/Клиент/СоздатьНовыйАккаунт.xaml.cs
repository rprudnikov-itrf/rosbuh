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

namespace RosApplication.Клиент
{
    /// <summary>
    /// Логика взаимодействия для СоздатьНовыйАккаунт.xaml
    /// </summary>
    public partial class СоздатьНовыйАккаунт : Window
    {
        public СоздатьНовыйАккаунт()
        {
            InitializeComponent();
        }

        private void Window_Complite(object sender, EventArgs e)
        {
            using (RosService.Client client = new RosService.Client())
            {
                var sb = new StringBuilder();
                sb.AppendLine("---------------");
                sb.AppendFormat("Имя базы: {0}\r\n", Домен.Text);
                sb.AppendFormat("e-mail: {0}\r\n", "**" /*Почта.Text*/);
                sb.AppendFormat("Логин: {0}\r\n", Логин.Text);
                sb.AppendFormat("Пароль: {0}\r\n", Пароль.Password);

                client.Конфигуратор.ОтправитьПисьмоВТехническуюПоддержку(
                    Домен.Text, Логин.Text, "заявка на создание аккаунта",
                    sb.ToString(), null, "");
            }
        }

        private void Window_ПроверкаЗначений(object sender, RosControl.UI.EventValidArgs e)
        {
            var sb = new StringBuilder();

            if (string.IsNullOrEmpty(Домен.Text))
                sb.AppendLine("Заполните поле 'Имя базы'");

            //if (string.IsNullOrEmpty(Почта.Text))
            //    sb.AppendLine("Заполните поле 'e-mail'");

            if (string.IsNullOrEmpty(Логин.Text))
                sb.AppendLine("Заполните поле 'Логин'");

            if (sb.Length > 0)
                throw new Exception(sb.ToString().Trim());
        }
    }
}
