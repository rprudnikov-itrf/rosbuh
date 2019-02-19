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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Data;
using RosControl.UI.CodeCompletion;
using Microsoft.Win32;

namespace RosApplication.Конфигуратор.Службы
{
    /// <summary>
    /// Логика взаимодействия для ПоискИЗамена.xaml
    /// </summary>
    public partial class ПоискИЗамена : Window
    {
        private IEnumerable<РедакторЗначенийValue> Items
        {
            get { return (IEnumerable<РедакторЗначенийValue>)GetValue(ItemsProperty); }
            set { SetValue(ItemsProperty, value); }
        }
        protected static readonly DependencyProperty ItemsProperty =
            DependencyProperty.Register("Items", typeof(IEnumerable<РедакторЗначенийValue>), typeof(ПоискИЗамена), new UIPropertyMetadata(null));

        public ПоискИЗамена()
        {
            InitializeComponent();
        }
        private bool isEdited { get; set; }
        private void PART_DataGrid_Searched(object sender, RosControl.UI.DataGridArgs e)
        {
            //if (!isEdited) 
            //    return;
            if (e.query.СтрокаЗапрос != null
                && !e.query.СтрокаЗапрос.Contains("КоличествоВыводимыхДанных=") 
                && !e.query.СтрокаЗапрос.Contains("Количество="))
            {
                e.query.КоличествоВыводимыхДанных = 50;
            }

            #region Задать колонки
            if (!string.IsNullOrEmpty(e.query.СтрокаЗапрос))
            {
                PART_DataGrid.Columns.Clear();
                PART_DataGrid.Columns.Add(new RosControl.UI.DataGridReadOnlyColumn
                {
                    Header = "#",
                    Binding = new Binding("[НомерСтроки]") { Mode = BindingMode.OneTime },
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Width = new Microsoft.Windows.Controls.DataGridLength(40),
                });

                foreach (var item in Колонки(e.query.СтрокаЗапрос))
                {
                    PART_DataGrid.Columns.Add(new Microsoft.Windows.Controls.DataGridTextColumn()
                    {
                        Header = item,
                        Binding = new Binding("[" + item + "]")
                    });
                }
            }
            else
            {
                PART_DataGrid.Columns.Clear();
                PART_DataGrid.Columns.Add(new Microsoft.Windows.Controls.DataGridTextColumn()
                {
                    Header = " ",
                    Width = new Microsoft.Windows.Controls.DataGridLength(1, Microsoft.Windows.Controls.DataGridLengthUnitType.Star)
                });
            }
            #endregion
        }

        private void Замена(object sender, RoutedEventArgs e)
        {
            if(Items == null)
                return;

            var values = Items.Where(p => p.IsEdit).ToDictionary(p => p.Name, q => (object)q.Value);
            if(values.Count == 0)
            {
                MessageBox.Show("Укажите хоты бы одно значение для замены");
                return;
            }

            if (MessageBox.Show("Выполнить замену?","Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                PART_Замена.IsEnabled = false;
                using (RosService.Client client = new RosService.Client())
                {
                    client.Архив.СохранитьЗначениеПоискАсинхронно(
                        new RosService.Data.Query(PART_ЗаменаЗапрос.Text),
                        RosService.Helper.ConvertDataValue(values), 
                        true, RosService.Data.Хранилище.Оперативное,
                        client.Пользователь, client.Домен);
                    PART_Замена.IsEnabled = true;
                    //MessageBox.Show("Готово");
                }
            }
        }
        private void РедакторЗапросов(object sender, RoutedEventArgs e)
        {
            PART_Query.Text = "[Типы=;Колонки=(НазваниеОбъекта);Сортировки=(НазваниеОбъекта,Asc)]";
        }
        private IEnumerable<string> Колонки(string запрос)
        {
            var list = new List<string>();
            try
            {
                var matchs = Regex.Matches(запрос.Trim("[]".ToArray()), @"(?<FUNCTION>([\w]+?))=(?<PARAM>([^;]+))[;]?", RegexOptions.Singleline | RegexOptions.Compiled);
                Parallel.ForEach(matchs.Cast<Match>(), (item) =>
                {
                    switch (item.Groups["FUNCTION"].Value.Trim())
                    {
                        case "ВыводимыеКолонки":
                        case "Колонки":
                            {
                                var _ВыводимыеКолонки = item.Groups["PARAM"].Value;
                                if (_ВыводимыеКолонки.StartsWith(@"(") && _ВыводимыеКолонки.EndsWith(")"))
                                    _ВыводимыеКолонки = _ВыводимыеКолонки.Substring(1, _ВыводимыеКолонки.Length - 2);

                                //Parallel.ForEach(_ВыводимыеКолонки.Split(','), (i) =>
                                foreach (var i in _ВыводимыеКолонки.Split(','))
                                {
                                    var match = Regex.Match(i, @"\(?(?<FUNCTION>(\w+))?\((?<PARAM>(.+))\)|(?<PARAM>(.+))\)?");
                                    list.Add(match.Groups["PARAM"].Value.Trim());
                                }
                            }
                            break;
                    }
                });
            }
            catch (Exception)
            {
            }
            return list;
        }

        private void PART_DataGrid_ЗапросChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var q = e.NewValue as string;
            if (q == null)
                Items = null;
            else
                Items = Колонки(q).Select(p => new РедакторЗначенийValue { Name = p, FullName = "=" + p }).ToArray();

            isEdited = true;
        }
        private class РедакторЗначенийValue
        {
            public bool IsEdit { get; set; }
            public string Name { get; set; }
            public string FullName { get; set; }
            public string Value { get; set; }
        }

        private void Удалить(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Выполнить удаление?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                КнопкаУдалить.IsEnabled = false;
                var q = new RosService.Data.Query(PART_ЗапросУдалить.Text);
                var _Корзина = Корзина.IsChecked.Value;
                var _Связи = Связи.IsChecked.Value;
                System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    try
                    {
                        using (RosService.Client client = new RosService.Client())
                        {
                            client.Архив.УдалитьРазделПоискАсинхронно(_Корзина, _Связи, q,
                                RosService.Data.Хранилище.Оперативное,
                                client.Пользователь, client.Домен);

                            //this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                            //{
                            //    MessageBox.Show("Удаление завершено.");
                            //});
                        }
                    }
                    catch (Exception ex)
                    {
                        using (RosService.Client client = new RosService.Client())
                        {
                            client.Конфигуратор.ЖурналСобытийДобавитьОшибку("УдалениеПоиском", ex.ToString());
                        }
                    }
                    finally
                    {
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                        {
                            КнопкаУдалить.IsEnabled = true;
                        });
                    }
                });
            }
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            //if (PART_Query.DefaultCompletionData == null)
            //{
            //    System.Threading.Tasks.Task.Factory.StartNew(() =>                
            //    {
            //        try
            //        {
            //            using (RosService.Client client = new RosService.Client())
            //            {
            //                var query = new RosService.Data.Query();
            //                query.ДобавитьТипы("ТипДанных");
            //                query.ДобавитьВыводимыеКолонки("ИмяТипаДанных");
            //                var items = client.Архив.Поиск(query, RosService.Data.Хранилище.Конфигурация).AsEnumerable()
            //                    .OrderBy(p => p["ИмяТипаДанных"])
            //                    .Where(p => !string.IsNullOrEmpty(p.Field<string>("ИмяТипаДанных")))
            //                    .Select(p => new XmlCompletionData(p.Field<string>("ИмяТипаДанных"), XmlCompletionData.DataType.XmlAttribute)); 

            //                this.Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
            //                {
            //                    PART_Query.DefaultCompletionData = items;
            //                });
            //            }
            //        }
            //        catch (Exception ex)
            //        {
            //            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate { MessageBox.Show(ex.Message); });
            //        }
            //    });
            //}
        }

        private void Form_Loaded(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
                PART_Query.Focus();
        }

        private void Open_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var dialog = new RosApplication.Конфигуратор.Службы.ОткрытьЗапрос();
            if (dialog.ShowDialog().Value)
            {
                PART_Query.Text = dialog.PART_Query.Text;
            }
        }
        private void CommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (PART_Query != null && !string.IsNullOrEmpty(PART_Query.Text))
            {
                var dialog = new SaveFileDialog()
                {
                    FileName = string.Format("{0}_{1:yyyy_MM_dd_hhmm}", RosService.Client.Domain, DateTime.Now),
                    Filter = "Запрос (*.rql)|*.rql",
                    RestoreDirectory = false,
                    InitialDirectory = App.AppPath,
                };

                if (dialog.ShowDialog().Value)
                    System.IO.File.WriteAllText(dialog.FileName, PART_Query.Text);
            }
            else
            {
                MessageBox.Show("Строка запроса пуста.");
            }
        }
        private void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            //e.CanExecute = PART_Query != null && !string.IsNullOrEmpty(PART_Query.Text);
            e.CanExecute = true;
        }

        private void ОбновитьКэш(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Вы действительно хотите обновить запрос в кэше?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                using (RosService.Client client = new RosService.Client())
                {
                    var result = client.Архив.UpdateCacheObject(new RosService.Data.Query(PART_Query.Text), client.Домен);
                    if (string.IsNullOrEmpty(result))
                        result = "OK";

                    MessageBox.Show(result, "Результат");
                }
            }
        }
    }
}
