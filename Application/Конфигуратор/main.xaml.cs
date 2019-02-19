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
using System.Threading;
using System.Windows.Threading;
using System.Text.RegularExpressions;
using RosControl.UI;
using RosService.Data;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Xml;
using System.IO;
using System.Windows.Controls.Primitives;
using Microsoft.Win32;
using System.Data;

namespace RosApplication.Конфигуратор
{
    public partial class Main : Page, IDisposable
    {
        #region ПользовательскоеМеню
        public object ПользовательскоеМеню
        {
            get { return (object)GetValue(ПользовательскоеМенюProperty); }
            set { SetValue(ПользовательскоеМенюProperty, value); }
        }
        public static readonly DependencyProperty ПользовательскоеМенюProperty =
            DependencyProperty.Register("ПользовательскоеМеню", typeof(object), typeof(Main), new UIPropertyMetadata(null));

        protected int _menuWidth;
        public int MenuWidth
        {
            get { return (int)GetValue(MenuWidthProperty); }
            set { SetValue(MenuWidthProperty, value); }
        }
        public static readonly DependencyProperty MenuWidthProperty =
            DependencyProperty.Register("MenuWidth", typeof(int), typeof(Main), new UIPropertyMetadata(220));

        public bool IsПользовательскоеМеню
        {
            get { return (bool)GetValue(IsПользовательскоеМенюProperty); }
            set { SetValue(IsПользовательскоеМенюProperty, value); }
        }
        public static readonly DependencyProperty IsПользовательскоеМенюProperty =
            DependencyProperty.Register("IsПользовательскоеМеню", typeof(bool), typeof(Main), new UIPropertyMetadata(true, new PropertyChangedCallback(IsПользовательскоеМенюPropertyChanged)));
        private static void IsПользовательскоеМенюPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var obj = d as Main;
            if (obj == null) return;

            if ((bool)e.NewValue)
            {
                obj.MenuWidth = obj._menuWidth;
            }
            else
            {
                obj._menuWidth = obj.MenuWidth;
                obj.MenuWidth = 0;
            }
        }
        #endregion


        public Main()
        {
            InitializeComponent();

            //создать привязки комманд
            RosApplication.Command.CommandBinding.SetCommandBinding(this);
        }
        private void Page_Initialized(object sender, EventArgs e)
        {
            Title = string.Format(@"{0}\{1}", RosService.Client.Domain, RosService.Client.UserName);
            
            //загрузить веб-сервисы
            RosControl.Compile.CodeCompiler.BuildWebServices();

            //debug
            IsDebug.IsChecked = RosControl.UI.Application.РежимТестирования;
        }

        private void КомпилироватьКонфигурацию(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Вы действительно хотите продолжить действие?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                var start = System.Diagnostics.Stopwatch.StartNew();
                System.Threading.Tasks.Task.Factory.StartNew(() =>
                {
                    try
                    {
                        using (RosService.Client client = new RosService.Client(TimeSpan.FromMinutes(5)))
                        {
                            client.Конфигуратор.КомпилироватьКонфигурацию(RosService.Client.Domain);
                            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                            {
                                MessageBox.Show(string.Format("Конфигурация обновлена {0}.", start.Elapsed.ToString()), "Сообщение", MessageBoxButton.OK, MessageBoxImage.Information);
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate { MessageBox.Show(ex.Message); });
                    }
                });
            }
        }
        private void Сервис_Click(object sender, RoutedEventArgs e)
        {
            var menu = sender as MenuItem;
            if (menu == null) return;

            switch (Convert.ToString(menu.Tag))
            {
                case "Проиндексировать":
                    {
                        var window = new ПроиндексироватьДанные();
                        //window.Owner = Application.Current.MainWindow;
                        window.ShowDialog();
                    }
                    break;

                //case "ОчиститьКонфигурацию":
                //    {
                //        using (RosService.Client client = new RosService.Client(TimeSpan.FromMinutes(10)))
                //        {
                //            var t = DateTime.Now;
                //            if (MessageBox.Show("Продолжить выполнение действия?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                //            {
                //                client.Конфигуратор.ОчиститьКонфигурацию(RosService.Client.Domain);
                //                MessageBox.Show(string.Format("Готово {0}", DateTime.Now - t));
                //            }
                //        }
                //    }
                //    break;

                //case "СоздатьКонфигурацию":
                //    {
                //        new СоздатьКонфигурацию().ShowDialog();
                //    }
                //    break;

                //case "ОбновитьКонфигурацию":
                //    {
                //        if (MessageBox.Show("Вы действительно хотите продолжить действие?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                //        {
                //            var s = System.Diagnostics.Stopwatch.StartNew();
                //            //ThreadPool.QueueUserWorkItem((WaitCallback)delegate
                //            System.Threading.Tasks.Task.Factory.StartNew(() =>
                //            {
                //                try
                //                {
                //                    using (RosService.Client client = new RosService.Client(TimeSpan.FromMinutes(5)))
                //                    {
                //                        client.Конфигуратор.ОбновитьКонфигурацию(client.Пользователь, client.Домен);

                //                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                //                        {
                //                            s.Stop();
                //                            MessageBox.Show(string.Format("Готово - {0}.", s.Elapsed.ToString()));
                //                        });
                //                    }
                //                }
                //                catch (Exception ex)
                //                {
                //                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate { MessageBox.Show(ex.Message); });
                //                }
                //            });
                //        }
                //    }
                //    break;
            }
        }
        private void TreeView_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.Source is RosControl.UI.TreeViewItemSeparator)
            {
                return;
            }
            else if (e.Source is System.Windows.Controls.TreeViewItem)
            {
                switch (Convert.ToString(БыстроеМеню.SelectedValue))
                {
                    case "Статистика":
                        {
                            var window = new System.Windows.Window()
                            {
                                Title = "Статистика",
                                Content = new Статистика(),
                                Width = 800,
                                Height = 600,
                                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                                ResizeMode = ResizeMode.NoResize
                            };
                            window.ShowDialog();
                        }
                        break;

                    //case "API":
                    //    {
                    //        var dialog = new SaveFileDialog()
                    //        {
                    //            FileName = string.Format("RosService.API.{0}", RosService.Client.Domain),
                    //            Filter = "Ros.API (*.dll)|*.dll",
                    //            RestoreDirectory = true,
                    //            AddExtension = true,
                    //            DefaultExt = "dll"
                    //        };
                    //        if (dialog.ShowDialog().Value)
                    //        {
                    //            using (RosService.Client client = new RosService.Client())
                    //            {
                    //                var source = client.Конфигуратор.ПолучитьСборкуApi(client.Пользователь, client.Домен);
                    //                File.WriteAllText(dialog.FileName + ".cs", source.ToString());
                    //                using (var codeProvider = new Microsoft.CSharp.CSharpCodeProvider(new Dictionary<string, string>() { { "CompilerVersion", "v4.0" } }))
                    //                {
                    //                    //add compiler parameters
                    //                    var compilerParams = new System.CodeDom.Compiler.CompilerParameters();
                    //                    compilerParams.CompilerOptions = "/target:library /optimize";
                    //                    compilerParams.GenerateExecutable = false;
                    //                    compilerParams.GenerateInMemory = false;
                    //                    compilerParams.IncludeDebugInformation = false;
                    //                    compilerParams.OutputAssembly = dialog.FileName;

                    //                    // add some basic references
                    //                    compilerParams.ReferencedAssemblies.Add("mscorlib.dll");
                    //                    compilerParams.ReferencedAssemblies.Add("System.dll");
                    //                    compilerParams.ReferencedAssemblies.Add("System.Data.dll");
                    //                    compilerParams.ReferencedAssemblies.Add("System.Xml.dll");
                    //                    compilerParams.ReferencedAssemblies.Add(System.Reflection.Assembly.GetAssembly(typeof(RosService.Client)).Location);
                    //                    var results = codeProvider.CompileAssemblyFromSource(compilerParams, source.ToString());
                    //                    if (results.Errors.Count > 0)
                    //                    {
                    //                        var errors = new List<string>();
                    //                        foreach (System.CodeDom.Compiler.CompilerError error in results.Errors)
                    //                            errors.Add(string.Format("Line: {0}; Compile Error: {1}", error.Line, error.ErrorText));
                    //                        throw new Exception(String.Join("\n", errors.ToArray()));
                    //                    }
                    //                }
                    //            }
                    //            MessageBox.Show(string.Format("Сборка сохранена, имя '{0}'", dialog.FileName));
                    //        }
                    //    }
                    //    break;

                    default:
                        {
                            if (БыстроеМеню.SelectedItem is RosControl.UI.TreeViewItem)
                            {
                                if (((RosControl.UI.TreeViewItem)БыстроеМеню.SelectedItem).ОткрытьВОкне)
                                {
                                    RosControl.Helper.СоздатьОкно(БыстроеМеню.SelectedValue, Хранилище.Конфигурация, 0);
                                }
                                else
                                {
                                    var tab = RosControl.Helper.СоздатьВкладку(БыстроеМеню.SelectedValue, Хранилище.Конфигурация, true);
                                    if ("ГруппыПользователей".Equals(БыстроеМеню.SelectedValue))
                                    {
                                        tab.IsFull = true;
                                    }
                                }
                            }
                        }
                        break;
                }
            }
        }
        private void СоздатьТелефонныеИндексы_Click(object sender, RoutedEventArgs e)
        {
            using (RosService.Client client = new RosService.Client())
            {
                client.Сервисы.Данные_СписокТелефонов_Проиндекировать(0, client.Пользователь, client.Домен);
                //client.Сервисы.Данные_СоздатьТелефонныйИндекс(0, new string[] { "ТелефонныйНомер" }, "ТелефонныйНомерИндекс", client.Пользователь, client.Домен);
                MessageBox.Show("Готово");
            }
        }
        private void ОсвободитьПамять_Click(object sender, RoutedEventArgs e)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private void СостояниеКеша_Click(object sender, RoutedEventArgs e)
        {
            new СписокКешированныхОбъектов().Show();
        }
        //private void Find_Executed(object sender, ExecutedRoutedEventArgs e)
        //{
        //    (new RosApplication.Конфигуратор.Службы.ПоискИЗамена()).Show();
        //}

        private void РедакторЗначений(object sender, RoutedEventArgs e)
        {
            new System.Windows.Window()
            {
                Title = "Редактор значений",
                Content = new RosControl.Forms.ПоказатьВсеЗначенияРаздела() { НеЗакрыватьПриСохранении = true, id_node = 0, Хранилище = Хранилище.Оперативное },
                Width = 984,
                Height = 720,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            }.Show();
        }
        private void ДиспетчерЗадач(object sender, RoutedEventArgs e)
        {
            new RosApplication.Конфигуратор.Службы.ДиспетчерЗадач()
            {
                //Owner = Application.Current.MainWindow
            }.Show();
        }

        private void Find_Click(object sender, RoutedEventArgs e)
        {
            (new RosApplication.Конфигуратор.Службы.ПоискИЗамена()).Show();
        }

        private void MainWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            Dispose();
        }

        public void Dispose()
        {
            //закрыть все вкладки
            if (PART_Documents != null)
            {
                PART_Documents.SelectedItem = null;

                foreach (var item in PART_Documents.Items.Cast<RosControl.UI.DocumentsTabItem>())
                {
                    if (item.Content is IDisposable)
                        ((IDisposable)item.Content).Dispose();
                    item.Content = null;
                }
                PART_Documents.Items.Clear();
            }
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            var window = new RosControl.Forms.Импорт()
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            window.Show();
        }

        private void ЖурналУдалений(object sender, RoutedEventArgs e)
        {
            var window = new RosApplication.Конфигуратор.Службы.ЖурналУдалений()
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            window.Show();
        }

        private void MenuItem_Click_1(object sender, RoutedEventArgs e)
        {
            RosControl.UI.Application.РежимТестирования = ((MenuItem)sender).IsChecked;
        }

        private void Выключить(object sender, RoutedEventArgs e)
        {
            var item = ((MenuItem)sender);
            if (item.IsChecked)
            {
                item.Header = "Включить блокирование";
            }
            else
            {
                item.Header = "Выключить блокирование";
            }

            var off = item.IsChecked;
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                try
                {
                    using (RosService.Client client = new RosService.Client())
                    {
                        client.Конфигуратор.АвторизацияБлокировки(off);
                    }
                }
                catch
                {
                }
            });
        }

        //~Main()
        //{
        //    if (true)
        //    {
        //    }
        //}
    }


    public class ConverterРедакторXaml : IValueConverter
    {
        #region IValueConverter Members
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value is RosControl.Designer.РедакторXaml ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}

