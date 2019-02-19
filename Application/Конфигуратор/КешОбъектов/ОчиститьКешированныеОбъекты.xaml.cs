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

namespace RosApplication.Конфигуратор
{
    /// <summary>
    /// Логика взаимодействия для ОчиститьКешированныеОбъекты.xaml
    /// </summary>
    public partial class ОчиститьКешированныеОбъекты : Window
    {
        public bool Значения
        {
            get { return (bool)GetValue(ЗначенияProperty); }
            set { SetValue(ЗначенияProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Значения.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ЗначенияProperty =
            DependencyProperty.Register("Значения", typeof(bool), typeof(ОчиститьКешированныеОбъекты), new UIPropertyMetadata(false));



        public bool Запросы
        {
            get { return (bool)GetValue(ЗапросыProperty); }
            set { SetValue(ЗапросыProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Запросы.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ЗапросыProperty =
            DependencyProperty.Register("Запросы", typeof(bool), typeof(ОчиститьКешированныеОбъекты), new UIPropertyMetadata(false));




        public bool СписокАтрибутов
        {
            get { return (bool)GetValue(СписокАтрибутовProperty); }
            set { SetValue(СписокАтрибутовProperty, value); }
        }

        // Using a DependencyProperty as the backing store for СписокАтрибутов.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty СписокАтрибутовProperty =
            DependencyProperty.Register("СписокАтрибутов", typeof(bool), typeof(ОчиститьКешированныеОбъекты), new UIPropertyMetadata(false));



        public bool Счётчики
        {
            get { return (bool)GetValue(СчётчикиProperty); }
            set { SetValue(СчётчикиProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Счётчики.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty СчётчикиProperty =
            DependencyProperty.Register("Счётчики", typeof(bool), typeof(ОчиститьКешированныеОбъекты), new UIPropertyMetadata(false));



        public bool КешТаблицы
        {
            get { return (bool)GetValue(КешТаблицыProperty); }
            set { SetValue(КешТаблицыProperty, value); }
        }

        // Using a DependencyProperty as the backing store for КешТаблицы.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty КешТаблицыProperty =
            DependencyProperty.Register("КешТаблицы", typeof(bool), typeof(ОчиститьКешированныеОбъекты), new UIPropertyMetadata(false));





        public bool Индентификаторы
        {
            get { return (bool)GetValue(ИндентификаторыProperty); }
            set { SetValue(ИндентификаторыProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Индентификаторы.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ИндентификаторыProperty =
            DependencyProperty.Register("Индентификаторы", typeof(bool), typeof(ОчиститьКешированныеОбъекты), new UIPropertyMetadata(false));





        public bool ВсёОстальное
        {
            get { return (bool)GetValue(ВсёОстальноеProperty); }
            set { SetValue(ВсёОстальноеProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ВсёОстальное.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ВсёОстальноеProperty =
            DependencyProperty.Register("ВсёОстальное", typeof(bool), typeof(ОчиститьКешированныеОбъекты), new UIPropertyMetadata(false));





        public ОчиститьКешированныеОбъекты()
        {
            InitializeComponent();
        }

        private void Window_Complite(object sender, EventArgs e)
        {
            using (RosService.Client client = new RosService.Client())
            {
                if (Значения)
                {
                    client.Конфигуратор.УдалитьКешированныеЗначения(client.Пользователь, client.Домен);
                }

                var items = new List<string>();
                if (Запросы)
                {
                    items.Add(client.Домен + ":Z:КешЗапрос*");
                    items.Add(client.Домен + ":Z:КешЗапросХешьТаблица*");
                }

                if (СписокАтрибутов)
                    items.Add(client.Домен + ":Z:КешСписокАтрибутов*");

                if (Счётчики)
                    items.Add(client.Домен + ":Z:КешСчётчик*");

                if (КешТаблицы)
                    items.Add(client.Домен + ":Z:КешХешьТаблица*");

                if(Индентификаторы)
                    items.Add(client.Домен + ":Z:КешИдентификаторРаздела*");

                if (ВсёОстальное)
                {
                    items.Add(client.Домен + ":Z:КешДобавитьРаздел*");
                    items.Add(client.Домен + ":Z:КешПолучитьТип*");
                    items.Add(client.Домен + ":Z:КешФорма*");
                    items.Add(client.Домен + ":Z:КешХешьТаблицаПамять*");
                    items.Add(client.Домен + ":Z:ХешьАтрибут*");
                }

                if(items.Count > 0)
                    client.Конфигуратор.УдалитьКешированныеОбъекты(items.ToArray(), client.Пользователь, client.Домен);

                DialogResult = true;
            }
        }
    }
}
