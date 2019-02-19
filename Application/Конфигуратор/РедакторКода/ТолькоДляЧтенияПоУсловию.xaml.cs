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
using System.Data;

namespace RosApplication.Конфигуратор.Редакторы
{
    /// <summary>
    /// Interaction logic for ТолькоДляЧтенияПоУсловию.xaml
    /// </summary>
    public partial class ТолькоДляЧтенияПоУсловию : Window
    {
        public ТолькоДляЧтенияПоУсловию()
        {
            InitializeComponent();
        }

        public Binding SelectedBinding { get; set; }



        public object ItemsSourceControls
        {
            get { return (object)GetValue(ItemsSourceControlsProperty); }
            set { SetValue(ItemsSourceControlsProperty, value); }
        }
        public static readonly DependencyProperty ItemsSourceControlsProperty =
            DependencyProperty.Register("ItemsSourceControls", typeof(object), typeof(СкрытьПоУсловию), new UIPropertyMetadata(null));



        public object ItemsSourceAttributes
        {
            get { return (object)GetValue(ItemsSourceAttributesProperty); }
            set { SetValue(ItemsSourceAttributesProperty, value); }
        }
        public static readonly DependencyProperty ItemsSourceAttributesProperty =
            DependencyProperty.Register("ItemsSourceAttributes", typeof(object), typeof(СкрытьПоУсловию), new UIPropertyMetadata(null));


        private void Window_Complite(object sender, EventArgs e)
        {
            var binding = null as Binding;
            switch (Условие.SelectedValue as string)
            {
                case "НовыйРаздел":
                    {
                        binding = new Binding("[@IsНовыйРаздел]");
                        binding.ConverterParameter = "True";
                    }
                    break;

                case "ЗначениеАтрибута":
                    {
                        binding = new Binding(string.Format("[{0}]", Атрибуты.SelectedValue));
                        binding.ConverterParameter = Значение.Text;
                    }
                    break;

                case "ЗначениеКонтрола":
                    {
                        binding = new Binding("SelectedValue");
                        binding.ConverterParameter = Значение.Text;
                        binding.ElementName = Контролы.SelectedValue as string;
                    }
                    break;

                case "ГруппаПользователя":
                    {
                        binding = new Binding("[@Группа]");
                        binding.ConverterParameter = string.Join("|", Группы.SelectedItems.Cast<DataRow>().Select(p => p.Field<string>("НазваниеОбъекта")).ToArray());
                    }
                    break;
            }
            switch (Оператор.SelectedValue as string)
            {
                case "Равно":
                    binding.Converter = new RosControl.Converters.BooleanConverter();
                    break;

                case "Не равно":
                    binding.Converter = new RosControl.Converters.NotBooleanConverter();
                    break;
            }
            SelectedBinding = binding;
            DialogResult = true;
        }
        private void Window_ПроверкаЗначений(object sender, RosControl.UI.EventValidArgs e)
        {
            var sb = new StringBuilder();
            switch (Условие.SelectedValue as string)
            {
                case "ЗначениеАтрибута":
                    {
                        if (Атрибуты.SelectedValue == null)
                            sb.AppendLine("Выберите 'Атрибут'.");
                    }
                    break;

                case "ЗначениеКонтрола":
                    {
                        if (Контролы.SelectedValue == null)
                            sb.AppendLine("Выберите 'Контрол'.");
                    }
                    break;

                case "ГруппаПользователя":
                    {
                        if (Группы.SelectedItems.Count == 0)
                            sb.AppendLine("Выберите хотя бы одну группу.");
                    }
                    break;
            }

            if (!string.IsNullOrEmpty(sb.ToString()))
                throw new Exception(sb.ToString().Trim());
        }
        private void PART_Window_Initialized(object sender, EventArgs e)
        {
            using (RosService.Client client = new RosService.Client())
            {
                var query = new RosService.Data.Query();
                query.ДобавитьТипы("ГруппаПользователей");
                query.ДобавитьВыводимыеКолонки("НазваниеОбъекта");
                query.ДобавитьСортировку("НазваниеОбъекта");
                Группы.ItemsSource = client.Архив.Поиск(query).AsEnumerable();
            }
        }
    }
}
