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
using System.Windows.Controls.Primitives;
using System.Threading;
using System.Xml;
using System.Windows.Markup;
using RosControl.UI;
using System.Collections;
using RosControl.Designer;
using System.ComponentModel;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using System.Data;
using RosService.Data;
using RosControl.Converters;
using RosService;
using RosApplication.Конфигуратор.Редакторы;
using System.Threading.Tasks;
using RosControl.ValidationRules;


namespace RosControl.Designer
{
    public partial class РедакторXaml : UserControl, IDisposable
    {
        private int id = 0;
        public string ТипДанных
        {
            get { return (string)GetValue(ТипДанныхProperty); }
            set { SetValue(ТипДанныхProperty, value); }
        }
        public static readonly DependencyProperty ТипДанныхProperty =
            DependencyProperty.Register("ТипДанных", typeof(string), typeof(РедакторXaml), new UIPropertyMetadata(null));

        public ObservableCollection<РедакторXamlTypeItem> Attributes
        {
            get { return (ObservableCollection<РедакторXamlTypeItem>)GetValue(AttributesProperty); }
            private set { SetValue(AttributesProperty, value); }
        }
        public static readonly DependencyProperty AttributesProperty =
            DependencyProperty.Register("Attributes", typeof(ObservableCollection<РедакторXamlTypeItem>), typeof(РедакторXaml), new UIPropertyMetadata(null, new PropertyChangedCallback(AttributesPropertyChanged)));
        private static void AttributesPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var cv = (CollectionView)CollectionViewSource.GetDefaultView(e.NewValue);
            if (cv != null)
            {
                cv.GroupDescriptions.Add(new PropertyGroupDescription("Type.ReflectedType"));
            }
        }

        public object ParentItemsSource
        {
            get { return (object)GetValue(ParentItemsSourceProperty); }
            set { SetValue(ParentItemsSourceProperty, value); }
        }
        public static readonly DependencyProperty ParentItemsSourceProperty =
            DependencyProperty.Register("ParentItemsSource", typeof(object), typeof(РедакторXaml), new UIPropertyMetadata(null));




        protected ObservableCollection<RosService.Configuration.Binding> Bindings { get; private set; }
        protected ObservableCollection<RosService.Configuration.Event> Events { get; private set; }
        protected List<FrameworkElement> Controls { get; private set; }
        //protected static ReadOnlyCollection<string> Справочники;
        //protected static ReadOnlyCollection<string> Константы;

        protected DesignerManager DesignerManager;
        protected FrameworkElement SelectedControl { get; private set; }



        public bool IsBindingsModifity
        {
            get { return (bool)GetValue(IsBindingsModifityProperty); }
            set { SetValue(IsBindingsModifityProperty, value); }
        }
        public static readonly DependencyProperty IsBindingsModifityProperty =
            DependencyProperty.Register("IsBindingsModifity", typeof(bool), typeof(РедакторXaml), new UIPropertyMetadata(false));

        public bool IsEventsModifity
        {
            get { return (bool)GetValue(IsEventsModifityProperty); }
            set { SetValue(IsEventsModifityProperty, value); }
        }
        public static readonly DependencyProperty IsEventsModifityProperty =
            DependencyProperty.Register("IsEventsModifity", typeof(bool), typeof(РедакторXaml), new UIPropertyMetadata(false));

        public ШаблонПанель Шаблоны
        {
            get { return (ШаблонПанель)GetValue(ШаблоныProperty); }
            set { SetValue(ШаблоныProperty, value); }
        }
        public static readonly DependencyProperty ШаблоныProperty =
            DependencyProperty.Register("Шаблоны", typeof(ШаблонПанель), typeof(РедакторXaml), new UIPropertyMetadata(null));



        public bool IsLoadings
        {
            get { return (bool)GetValue(IsLoadingsProperty); }
            set { SetValue(IsLoadingsProperty, value); }
        }
        public static readonly DependencyProperty IsLoadingsProperty =
            DependencyProperty.Register("IsLoadings", typeof(bool), typeof(РедакторXaml), new UIPropertyMetadata(false));



        private bool IsXamlEdit = true;
        private bool _isedit;
        public bool IsEdit
        {
            get
            {
                return _isedit;
            }
            set
            {
                if (!IsLoadings && _isedit != value)
                {
                    _isedit = value;
                    var documnets = RosControl.UI.NavigationWindow.GetCurrentNavigationWindow();
                    if (documnets != null)
                    {
                        var SelectedDocument = documnets.SelectedDocument;
                        if (SelectedDocument != null)
                            SelectedDocument.IsEdit = value;
                    }
                }
            }
        }


        static РедакторXaml()
        {
            BindingConvertorHelper.Register<BindingExpressionBase, BindingConvertor>();
            BindingConvertorHelper.SetAttribute(typeof(RosControl.UI.ComboBox), new string[] { "Items", "GroupStyle", "ItemsSource" }, new Attribute[] { new DesignerSerializationVisibilityAttribute(DesignerSerializationVisibility.Hidden), new DesignerSerializationVisibilityAttribute(DesignerSerializationVisibility.Hidden), new DesignerSerializationVisibilityAttribute(DesignerSerializationVisibility.Hidden) });
            BindingConvertorHelper.SetAttribute(typeof(RosControl.UI.DataGrid), new string[] { "Items" }, new Attribute[] { new DesignerSerializationVisibilityAttribute(DesignerSerializationVisibility.Hidden) });
        }
        public РедакторXaml()
        {
            InitializeComponent();
        }
        public РедакторXaml(string Тип)
        {
            id = RosControl.UI.Application.AddWindow(Тип);
            InitializeComponent();
            
            IsLoadings = true;
            ТипДанных = Тип;
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                try
                {
                    using (RosService.Client client = new RosService.Client())
                    {
                        Parallel.Invoke(delegate()
                        {
                            #region Новый алгоритм
                            var xaml = client.Конфигуратор.ПолучитьЗначение<string>(Тип, "@Xaml");
                            if (!string.IsNullOrEmpty(xaml) && xaml.StartsWith("{"))
                            {
                                var form = Newtonsoft.Json.JsonConvert.DeserializeObject<RosControl.UI.КонтентПанель.XamlForm>(xaml);
                                if(form != null)
                                {
                                    this.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (System.Threading.ThreadStart)delegate
                                    {
                                        try
                                        {
                                            if (!string.IsNullOrEmpty(form.Xaml))
                                                form.Xaml = form.Xaml.Replace("VerticalContentAligment=\"Top\"", "");

                                            ЗагрузитьXaml(form.Xaml, true);
                                        }
                                        catch
                                        {
                                            if (string.IsNullOrEmpty(form.Xaml))
                                            {
                                                ЗагрузитьXaml(form.Xaml, false);
                                            }
                                            else
                                            {
                                                tabControlDesigner.SelectedValue = "tabXaml";
                                                txtXaml.Text = form.Xaml;
                                            }
                                        }

                                        txtXaml.Text = form.Xaml;
                                        txtSourceCode.Text = form.Code;
                                        IsLoadings = false;
                                    });

                                    #region Загрузить атрибуты
                                    Events = new ObservableCollection<RosService.Configuration.Event>(form.Events);
                                    Bindings = new ObservableCollection<RosService.Configuration.Binding>(form.Bindings);
                                    Bindings.CollectionChanged += delegate(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
                                    {
                                        if (this.Attributes == null) return;

                                        switch (e.Action)
                                        {
                                            case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                                                {
                                                    foreach (var item in e.NewItems.Cast<RosService.Configuration.Binding>())
                                                    {
                                                        var attr = this.Attributes.SingleOrDefault(p => p.Type.Name == item.attribute);
                                                        if (attr == null) continue;
                                                        attr.IsBinding = true;
                                                    }
                                                }
                                                break;

                                            case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                                                {
                                                    foreach (var item in e.NewItems.Cast<RosService.Configuration.Binding>())
                                                    {
                                                        var attr = this.Attributes.SingleOrDefault(p => p.Type.Name == item.attribute);
                                                        if (attr == null) continue;
                                                        attr.IsBinding = false;
                                                    }
                                                }
                                                break;
                                        }
                                    };
                                    #endregion
                                }
                            }
                            #endregion

                            #region Старый алгоритм
                            else
                            {
                                var form = client.Конфигуратор.ПолучитьФорму(Тип, client.Домен);
                                this.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (System.Threading.ThreadStart)delegate
                                {
                                    //PART_КонтентПанель.ЗагрузитьСистемныеПеременные();
                                    //PART_КонтентПанель.ЗагрузитьЗначения();

                                    try
                                    {
                                        if (!string.IsNullOrEmpty(form.Xaml))
                                            form.Xaml = form.Xaml.Replace("VerticalContentAligment=\"Top\"", "");

                                        ЗагрузитьXaml(form.Xaml, true);
                                    }
                                    catch
                                    {
                                        if (string.IsNullOrEmpty(form.Xaml))
                                        {
                                            ЗагрузитьXaml(form.Xaml, false);
                                        }
                                        else
                                        {
                                            tabControlDesigner.SelectedValue = "tabXaml";
                                            txtXaml.Text = form.Xaml;
                                        }
                                    }

                                    txtXaml.Text = form.Xaml;
                                    txtSourceCode.Text = form.ИсходныйКод;
                                    IsLoadings = false;
                                });

                                #region Загрузить атрибуты
                                Events = new ObservableCollection<RosService.Configuration.Event>(form.Events);
                                Bindings = new ObservableCollection<RosService.Configuration.Binding>(form.Bindings);
                                Bindings.CollectionChanged += delegate(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
                                {
                                    if (this.Attributes == null) return;

                                    switch (e.Action)
                                    {
                                        case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                                            {
                                                foreach (var item in e.NewItems.Cast<RosService.Configuration.Binding>())
                                                {
                                                    var attr = this.Attributes.SingleOrDefault(p => p.Type.Name == item.attribute);
                                                    if (attr == null) continue;
                                                    attr.IsBinding = true;
                                                }
                                            }
                                            break;

                                        case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                                            {
                                                foreach (var item in e.NewItems.Cast<RosService.Configuration.Binding>())
                                                {
                                                    var attr = this.Attributes.SingleOrDefault(p => p.Type.Name == item.attribute);
                                                    if (attr == null) continue;
                                                    attr.IsBinding = false;
                                                }
                                            }
                                            break;
                                    }
                                };
                                #endregion
                            }
                            #endregion
                        },
                        delegate()
                        {
                            var attributes = client.Конфигуратор.СписокАтрибутов(Тип);
                            var _attributes = new ObservableCollection<РедакторXamlTypeItem>(attributes.Select(p => new РедакторXamlTypeItem() { Type = p }));
                            this.Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
                            {
                                Attributes = _attributes;
                            });
                        });
                    }
                }
                catch (Exception ex)
                {
                    this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, (System.Threading.ThreadStart)delegate
                    {
                        MessageBox.Show(ex.Message);
                    });
                }
            });
        }


        protected void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
            {
                //загрузить схему для xaml
                RosControl.UI.TextEditor.LoadSchema(RosApplication.Configuration.Properties.Resources.XamlPresentation2006);
            }
        }
        protected void ОбновитьСписокКонтролов()
        {
            this.Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
            {
                if (PART_ContentContanier == null)
                    return;

                if (this.DesignerManager != null)
                {
                    this.DesignerManager.SelectionChanged -= new RoutedEventHandler(DesignerManager_SelectionChanged);
                    this.DesignerManager.Dispose();
                }
                this.DesignerManager = new DesignerManager(PART_ContentContanier.Content as Visual);
                this.DesignerManager.SelectionChanged += new RoutedEventHandler(DesignerManager_SelectionChanged);

                var Designer_ContextMenu = this.TryFindResource("Designer_ContextMenu") as ContextMenu;
                if (Designer_ContextMenu != null)
                {
                    Designer_ContextMenu.DataContext = this;
                }

                this.Controls = RosControl.Helper.FindChildsControl<FrameworkElement>(PART_ContentContanier).ToList();
                foreach (var item in this.Controls)
                {
                    var a = this.DesignerManager.Add(item);
                    if (a != null)
                        a.ContextMenu = Designer_ContextMenu;
                };
                var names = this.Controls.Where(p => !string.IsNullOrEmpty(p.Name));

                //var t = System.Reflection.Assembly.GetExecutingAssembly().
                txtSourceCode.DefaultCompletionData = names.Select(p => (ICSharpCode.AvalonEdit.CodeCompletion.ICompletionData)new RosControl.UI.CodeCompletion.XmlCompletionData("Значение<string>(\"" + p.Name + "\")"))
                    .Union(names.Select(p => (ICSharpCode.AvalonEdit.CodeCompletion.ICompletionData)new RosControl.UI.CodeCompletion.XmlCompletionData(p.Name)));
            });
        }
        protected void ЗагрузитьXaml(string xaml, bool IsParseThrow)
        {
            IsXamlEdit = false;
            object content = null;
            try
            {
                if (string.IsNullOrEmpty(xaml)) throw new Exception("Empty Xaml");
                content = System.Windows.Markup.XamlReader.Load(new System.Xml.XmlTextReader(new System.IO.StringReader(xaml)));
            }
            catch (Exception ex)
            {
                if (IsParseThrow) throw ex;

                content = new UserControl()
                {
                    Content = new Grid()
                };
            }
            PART_ContentContanier.Content = content;


            this.Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
            {
                ОбновитьСписокКонтролов();

                //Шаблоны = RosControl.Helper.FindChildsControl<ШаблонПанель>(content as DependencyObject).FirstOrDefault();
                //if (Шаблоны != null)
                //{
                //    PART_Шаблоны.ItemsSource = Шаблоны.Items.Cast<Шаблон>().Select(p => p.Имя);
                //}
                //else
                //{
                //    ОбновитьСписокКонтролов();
                //}
            });
        }
        protected string СохранитьXaml()
        {
            if (!IsXamlEdit || PART_ContentContanier.Content == null)
                return txtXaml.Text;

            var sb = new StringBuilder();
            var settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.OmitXmlDeclaration = true;

            var dsm = new XamlDesignerSerializationManager(XmlWriter.Create(sb, settings));
            dsm.XamlWriterMode = XamlWriterMode.Expression;

            XamlWriter.Save(PART_ContentContanier.Content, dsm);
            return sb.ToString();
        }


        private void SelectedControlChanged(FrameworkElement obj)
        {
            this.SelectedControl = obj;
            if (this.SelectedControl is System.Windows.Controls.TabItem && this.SelectedControl.Parent is System.Windows.Controls.TabControl)
            {
                var tabcontrol = ((System.Windows.Controls.TabControl)this.SelectedControl.Parent);
                if (tabcontrol.SelectedItem != this.SelectedControl)
                {
                    tabcontrol.SelectedItem = this.SelectedControl;
                    ОбновитьСписокКонтролов();
                }
            }

            this.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate
            {
                #region Список Родителей
                var items = new List<MenuItem>();
                var parent = LogicalTreeHelper.GetParent(SelectedControl) as FrameworkElement;
                while (parent != null && !(parent is UserControl))
                {
                    items.Add(new MenuItem() { Header = string.Format("{1} : {0}", parent.Name, parent.GetType().Name), Tag = parent });
                    parent = LogicalTreeHelper.GetParent(parent) as FrameworkElement;
                }
                ParentItemsSource = items;
                #endregion

                var propertys = new ObservableCollection<PropertyGridItem>();
                var events = new ObservableCollection<PropertyGridItem>();

                if (this.SelectedControl != null)
                {
                    #region Propertes
                    try
                    {
                        propertys.Add(new PropertyGridItemCustom()
                        {
                            Category = "Основные",
                            Name = "Имя",
                            Value = this.SelectedControl.Name,
                            Type = PropertyGridItemType.MultiTextBox,
                            Sort = 10
                        });

                        var bind = null as RosService.Configuration.Binding;
                        if (!string.IsNullOrEmpty(this.SelectedControl.Name))
                        {
                            bind = Bindings.Where(p => p.control == this.SelectedControl.Name && p.PropertyPath == "").FirstOrDefault();
                        }
                        propertys.Add(new PropertyGridItemCustom()
                        {
                            Category = "Основные",
                            Name = "Value",
                            Value = bind == null ? null : bind.attribute,
                            Items = Attributes.Select(p => p.Type.Name).OrderBy(p => p),
                            Type = PropertyGridItemType.ComboBox,
                            IsReadOnly = true,
                            //DisplayMemberPath = "Name",
                            //SelectedValuePath = "Name",
                            Sort = 9
                        });

                        propertys.Add(new PropertyGridItemCustom()
                        {
                            Category = "Основные",
                            Name = "StringFormat",
                            Value = bind == null ? null : bind.StringFormat,
                            Type = PropertyGridItemType.ComboBox,
                            Items = new string[] { "N2", "F0", "C", "0:0000.0", "d", "g", "t" },
                            Sort = 8
                        });
                        //if (this.SelectedControl is Selector && !(this.SelectedControl is RosControl.UI.DataGrid)
                        //    && Bindings != null)
                        //{
                        //    bind = Bindings.Where(p => p.control == this.SelectedControl.Name && p.PropertyPath == "ItemsSource").SingleOrDefault();
                        //    propertys.Add(new PropertyGridItemCustom()
                        //    {
                        //        Category = "Основные",
                        //        Name = "Items",
                        //        Value = bind == null ? null : bind.attribute,
                        //        Items = binds,
                        //        Type = PropertyGridItemType.ComboBox,
                        //        IsReadOnly = false,
                        //        DisplayMemberPath = "Name",
                        //        SelectedValuePath = "Name",
                        //        Sort = 7
                        //    });
                        //}
                        if (this.SelectedControl is RosControl.UI.DataGrid || this.SelectedControl is Microsoft.Windows.Controls.DataGrid)
                        {
                            propertys.Add(new PropertyGridItemCustom()
                            {
                                Category = "Основные",
                                Name = "Columns",
                                Items = new Type[] { 
                                typeof(Microsoft.Windows.Controls.DataGridTextColumn), 
                                typeof(Microsoft.Windows.Controls.DataGridComboBoxColumn),
                                typeof(Microsoft.Windows.Controls.DataGridCheckBoxColumn),
                                typeof(Microsoft.Windows.Controls.DataGridHyperlinkColumn),
                                typeof(Microsoft.Windows.Controls.DataGridTemplateColumn),
                                typeof(RosControl.UI.DataGridColumnButton),
                                typeof(RosControl.UI.DataGridComboBoxColumn),
                                typeof(RosControl.UI.DataGridDateColumn),
                                typeof(RosControl.UI.DataGridDeleteButton),
                                typeof(RosControl.UI.DataGridSelectedColumn)
                            },
                                Type = PropertyGridItemType.Collection,
                                Sort = 6
                            });
                        }
                        if (this.SelectedControl is RosControl.UI.SearchTextBox)
                        {
                            propertys.Add(new PropertyGridItemCustom()
                            {
                                Category = "Основные",
                                Name = "Columns",
                                Items = new Type[] { typeof(SearchTextBoxColumn) },
                                Type = PropertyGridItemType.Collection,
                                Sort = 6
                            });
                        }

                        //загрузить список свойств контрола
                        var defaultlist = new string[] { "Width", "Height", "SelectedValuePath", "DisplayMemberPath" };
                        var props = TypeDescriptor.GetProperties(this.SelectedControl).Cast<PropertyDescriptor>();

                        var Запрос = props.SingleOrDefault(p => p.Name == "Запрос");
                        if (Запрос != null)
                        {
                            propertys.Add(new PropertyGridItemCustom()
                            {
                                Category = "Основные",
                                Name = "Запрос",
                                Value = Запрос.GetValue(this.SelectedControl),
                                Type = PropertyGridItemType.MultiTextBox,
                                Sort = 6
                            });
                        }

                        var Параметры = props.SingleOrDefault(p => p.Name == "Параметры");
                        if (Параметры != null)
                        {
                            propertys.Add(new PropertyGridItemCustom()
                            {
                                Category = "Основные",
                                Name = "Параметры",
                                Type = PropertyGridItemType.Collection,
                                Items = new Type[] { typeof(RosControl.UI.ПараметрЗапроса) },
                                Sort = 6
                            });
                        }

                        //валидаторы
                        propertys.Add(new PropertyGridItemCustom()
                        {
                            Category = "Проверка",
                            Name = "ПроверкаНаПустоеЗначение",
                            Type = PropertyGridItemType.CheckBox,
                            Items = new Type[] { typeof(RosControl.UI.ПараметрЗапроса) },
                            Sort = 5,
                            Value = "ПроверкаНаПустоеЗначение"
                        });
                        propertys.Add(new PropertyGridItemCustom()
                        {
                            Category = "Проверка",
                            Name = "ВводТолькоЧисел",
                            Type = PropertyGridItemType.CheckBox,
                            Items = new Type[] { typeof(RosControl.UI.ПараметрЗапроса) },
                            Sort = 5,
                            Value = "ВводТолькоЧисел"
                        });

                        foreach (PropertyDescriptor item in props)
                        {
                            if (item.IsBrowsable &&
                                item.SerializationVisibility != DesignerSerializationVisibility.Hidden &&
                                !item.IsReadOnly &&
                                !item.Name.Contains("Typography") &&
                                !item.Name.Contains("ContextMenuService") &&
                                !item.Name.Contains("ToolTipService"))
                            {
                                propertys.Add(new PropertyGridItemControl(this.SelectedControl, item)
                                {
                                    Sort = 0,
                                    Category = item.Category,
                                    IsDefault = defaultlist.Contains(item.Name)
                                });
                            }
                        }
                        //загрузить список событий контрола
                        if (Events != null)
                        {
                            foreach (EventDescriptor item in TypeDescriptor.GetEvents(this.SelectedControl))
                            {
                                if (item.IsBrowsable)
                                {
                                    var source = Events.SingleOrDefault(p => p.control == SelectedControl.Name && p.ИмяСобытия == item.DisplayName);
                                    events.Add(new PropertyGridItemCustom()
                                    {
                                        Name = item.DisplayName,
                                        Category = item.Category,
                                        Value = source != null ? source.ИмяФункции : "",
                                        IsDefault = defaultlist.Contains(item.Name),
                                        Type = PropertyGridItemType.File
                                    });
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex != null)
                        {
                        }
                    }
                    #endregion
                }

                Property.BeginInit();
                Property.Properties = propertys;
                var view = (CollectionView)CollectionViewSource.GetDefaultView(Property.Properties);
                if (view != null)
                {
                    view.SortDescriptions.Add(new SortDescription("Sort", ListSortDirection.Descending));
                    view.SortDescriptions.Add(new SortDescription("Category", ListSortDirection.Ascending));
                    view.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
                }
                Event.Properties = events;
                view = (CollectionView)CollectionViewSource.GetDefaultView(Event.Properties);
                if (view != null)
                {
                    view.SortDescriptions.Add(new SortDescription("Sort", ListSortDirection.Descending));
                    view.SortDescriptions.Add(new SortDescription("Category", ListSortDirection.Ascending));
                    view.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
                }
                Property.EndInit();
            });
        }
        private void DesignerManager_SelectionChanged(object sender, RoutedEventArgs e)
        {
            var adorner = this.DesignerManager.SelectedItems.FirstOrDefault() as Adorner;
            if (adorner == null) return;
            SelectedControlChanged(adorner.AdornedElement as FrameworkElement);
        }

        private void Копировать()
        {
            var window = new AddExistingAttribute();
            if (window.ShowDialog().Value)
            {
                var attribute = window.Дерево.SelectedValue as string;
                if (attribute != null)
                {
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate
                    {
                        try
                        {
                            IsLoadings = true;
                            Cursor = Cursors.Wait;
                            using (RosService.Client client = new RosService.Client())
                            {
                                var form = client.Конфигуратор.ПолучитьФорму(attribute, client.Домен);
                                ЗагрузитьXaml(form.Xaml, false);
                                txtSourceCode.Text = form.ИсходныйКод;

                                client.Конфигуратор.СохранитьЗначение(ТипДанных, "НазваниеОбъекта", client.Конфигуратор.ПолучитьЗначение<string>(attribute, "НазваниеОбъекта"));
                                client.Конфигуратор.СохранитьЗначение(ТипДанных, "@РазмерОкна", client.Конфигуратор.ПолучитьЗначение<string>(attribute, "@РазмерОкна"));

                                //копировать список событий
                                client.Конфигуратор.Event_УдалитьСобытие(ТипДанных, Client.Domain);
                                foreach (var item in form.Events)
                                    client.Конфигуратор.Event_СохранитьСобытие(ТипДанных, item.control, item.ИмяСобытия, item.ИмяФункции, Client.Domain);

                                //копировать список привязок
                                client.Конфигуратор.Binder_УдалитьСвязи(ТипДанных, Client.Domain);
                                foreach (var item in form.Bindings)
                                    client.Конфигуратор.Binder_СохранитьСвязь(ТипДанных, item.attribute, item.control, item.PropertyPath, item.StringFormat, Client.Domain);

                                this.Bindings.Clear();
                                foreach (var item in client.Конфигуратор.Binder_СписокСвязей(ТипДанных, Client.Domain))
                                    this.Bindings.Add(item);
                            }
                        }
                        finally
                        {
                            Cursor = null;
                            IsLoadings = false;
                            IsXamlEdit = true;
                        }
                    });
                }
            }
        }
        private void Сохранить(object sender, ExecutedRoutedEventArgs e)
        {
            saving = true;
            Cursor = Cursors.Wait;

            var __txtXaml = txtXaml.Text;
            var __txtSourceCode = txtSourceCode.Text;

            if (tabControlDesigner.SelectedItem.Equals(tabDesign))
                __txtXaml = СохранитьXaml();

            var __ТипДанных = ТипДанных;
            var __Bindings = new List<RosService.Configuration.Binding>();
            //if (IsBindingsModifity)
            {
                var content = this.PART_ContentContanier.Content as FrameworkElement;
                foreach (var p in Bindings)
                {
                    if (!string.IsNullOrEmpty(p.attribute) && !(content.FindName(p.control) == null))
                    {
                        __Bindings.Add(p);
                    }
                }
                IsBindingsModifity = false;
            }

            var __Events = null as IEnumerable<RosService.Configuration.Event>;
            //if (IsEventsModifity)
            {
                __Events = Events;
                IsEventsModifity = false;
            }

            Task.Factory.StartNew(delegate()
            {
                try
                {
                    using (RosService.Client client = new RosService.Client())
                    {
                        #region Новый формат 
                        var form = new RosControl.UI.КонтентПанель.XamlForm()
                        {
                            Xaml = __txtXaml,
                            Code = __txtSourceCode,
                            Bindings = __Bindings.ToArray(),
                            Events = __Events.ToArray(),
                        };
                        client.Конфигуратор.СохранитьЗначение(__ТипДанных, "@Xaml", Newtonsoft.Json.JsonConvert.SerializeObject(form));
                        client.Конфигуратор.СохранитьЗначение(__ТипДанных, "@UpdateDate", DateTime.Now);
                        #endregion

                        #region Старый формат
                        //Parallel.Invoke(
                        //    delegate()
                        //    {
                        //        client.Конфигуратор.СохранитьЗначение(__ТипДанных, "@Xaml", __txtXaml);
                        //        client.Конфигуратор.СохранитьЗначение(__ТипДанных, "@ИсходныйКод", __txtSourceCode);
                        //        client.Конфигуратор.СохранитьЗначение(__ТипДанных, "@UpdateDate", DateTime.Now);
                        //    },
                        //    delegate()
                        //    {
                        //        if (__Bindings != null && __Bindings.Count() > 0)
                        //        {
                        //            client.Конфигуратор.Binder_УдалитьСвязи(__ТипДанных, RosService.Client.Domain);
                        //            foreach (var item in __Bindings)
                        //            {
                        //                client.Конфигуратор.Binder_СохранитьСвязь(__ТипДанных, item.attribute, item.control,
                        //                    item.PropertyPath, item.StringFormat, RosService.Client.Domain);
                        //            }
                        //        }
                        //    },
                        //    delegate()
                        //    {
                        //        if (__Events != null && __Events.Count() > 0)
                        //        {
                        //            client.Конфигуратор.Event_УдалитьСобытие(__ТипДанных, RosService.Client.Domain);
                        //            foreach (var item in __Events)
                        //            {
                        //                client.Конфигуратор.Event_СохранитьСобытие(
                        //                    __ТипДанных,
                        //                    item.control,
                        //                    item.ИмяСобытия,
                        //                    item.ИмяФункции,
                        //                    RosService.Client.Domain);
                        //            }
                        //        }
                        //    }
                        //);
                        #endregion

                        this.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate
                        {
                            //очистить кеш
                            RosControl.UI.Application.Clear(__ТипДанных);
                            IsEdit = false;
                        });
                    }
                }
                catch (Exception ex)
                {
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                    {
                        MessageBox.Show(ex.Message);
                    });
                }
                finally
                {
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                    {
                        Cursor = null;
                        saving = false;
                    });
                }
            });
        }
        private void propertyGrid_PropertyChanged(object sender, RosControl.UI.PropertyChangedEventArgs e)
        {
            if (e.Item == null || this.SelectedControl == null) return;
            IsEdit = true;
            IsXamlEdit = true;
            try
            {
                switch (e.Item.GetName())
                {
                    case "Value":
                    case "Items":
                        {
                            IsBindingsModifity = true;
                            if (string.IsNullOrEmpty(this.SelectedControl.Name))
                            {
                                var name = string.Empty;
                                var controls = PART_ContentContanier.Content as FrameworkElement;
                                if (controls != null)
                                {
                                    try
                                    {
                                        for (int i = 0; i < 30; i++)
                                        {
                                            name = string.Format("{1}{2}", ТипДанных, e.Item.GetValue().ToString().Replace(".", "_"), i == 0 ? "" : string.Format("_{0:00}", i));
                                            if (controls.FindName(name) == null) break;
                                        }
                                        this.SelectedControl.Name = name;
                                        this.SelectedControl.RegisterName(name, this.SelectedControl);
                                    }
                                    catch
                                    {
                                        for (int i = 0; i < 30; i++)
                                        {
                                            name = string.Format("{0}_{1:00}", this.SelectedControl.GetType().Name, i);
                                            if (controls.FindName(name) == null) break;
                                        }
                                        this.SelectedControl.Name = name;
                                        this.SelectedControl.RegisterName(name, this.SelectedControl);
                                    }
                                }
                            }

                            var PropertyPath = e.Item.GetName() == "Items" ? "ItemsSource" : "";
                            var item = Bindings.SingleOrDefault(p => p.control == this.SelectedControl.Name && p.PropertyPath == PropertyPath);
                            var attr = e.Item.GetValue().ToString().Trim();
                            if (item == null)
                            {
                                Bindings.Add(new RosService.Configuration.Binding()
                                {
                                    control = this.SelectedControl.Name,
                                    PropertyPath = PropertyPath,
                                    attribute = attr
                                });
                            }
                            else
                            {
                                item.attribute = attr;
                            }
                            //подписать для удобсва чем забиндено
                            if (this.SelectedControl is ContentControl && SelectedControl.GetValue(ContentControl.ContentProperty) == null)
                                this.SelectedControl.SetValue(ContentControl.ContentProperty, e.Item.GetValue());
                            if (this.SelectedControl is System.Windows.Controls.TextBox && SelectedControl.GetValue(System.Windows.Controls.TextBox.TextProperty) == null)
                                this.SelectedControl.SetValue(System.Windows.Controls.TextBox.TextProperty, e.Item.GetValue());
                            if (this.SelectedControl is TextBlock && SelectedControl.GetValue(TextBlock.TextProperty) == null)
                                this.SelectedControl.SetValue(TextBlock.TextProperty, e.Item.GetValue());
                            if (this.SelectedControl is System.Windows.Controls.ComboBox && e.Item.GetName() == "Items")
                            {
                                var obj = (this.SelectedControl as System.Windows.Controls.ComboBox);
                                if (string.IsNullOrEmpty(obj.DisplayMemberPath)) obj.DisplayMemberPath = "[НазваниеОбъекта]";
                                if (string.IsNullOrEmpty(obj.SelectedValuePath)) obj.SelectedValuePath = "[НазваниеОбъекта]";
                            }
                        }
                        break;

                    case "StringFormat":
                        {
                            IsBindingsModifity = true;
                            var item = Bindings.SingleOrDefault(p => p.control == this.SelectedControl.Name);
                            if (item != null)
                            {
                                item.StringFormat = e.Item.GetValue().ToString();
                            }
                        }
                        break;

                    case "Имя":
                        {
                            SelectedControl.Name = e.Item.GetValue().ToString();
                        }
                        break;

                    case "ПроверкаНаПустоеЗначение":
                        {
                            Validations.SetПроверкаНаПустоеЗначение(SelectedControl, (bool)e.Item.GetValue());
                        }
                        break;

                    case "ВводТолькоЧисел":
                        {
                            Validations.SetВводТолькоЧисел(SelectedControl, (bool)e.Item.GetValue());
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                if (ex != null)
                {
                }
            }
        }
        private void Properties_ButtonCollectionClick(object sender, RoutedEventArgs e)
        {
            var _param = e.OriginalSource as RosControl.UI.PropertyGridItemCustom;
            if (_param == null) return;

            var window = new RosControl.Designer.CollectionEditor();
            window.DataContext = SelectedControl;
            window.Types = _param.Items as IEnumerable<Type>;
            window.SetBinding(CollectionEditor.ItemsSourceProperty, new Binding(_param.Name));
            window.ShowDialog();
            IsXamlEdit = true;
        }
        protected void Event_ButtonFileClick(object sender, RoutedEventArgs e)
        {
            var obj = e.OriginalSource as PropertyGridItemCustom;
            if (obj == null) return;

            //добавить событие
            //var _event = Events.SingleOrDefault(p => p.control == SelectedControl.Name && p.ИмяСобытия == obj.Name);
            //if (_event == null)
            {
                foreach (EventDescriptor item in TypeDescriptor.GetEvents(this.SelectedControl))
                {
                    if (item.DisplayName == obj.Name)
                    {
                        var ИмяФункции = SelectedControl.Name + "_" + item.Name;
                        if (txtSourceCode.Text == null) txtSourceCode.Text = "";
                        if (!txtSourceCode.Text.Contains(ИмяФункции))
                        {
                            var _params = new StringBuilder();
                            var method = item.EventType.GetMethod("Invoke");
                            var oRow = 0;
                            foreach (var param in method.GetParameters())
                            {
                                if (oRow++ > 0) _params.Append(", ");
                                _params.AppendFormat("{0} {1}",
                                    param.ParameterType == typeof(object) ? "object" : param.ParameterType.Name,
                                    param.Name);
                            }

                            StringBuilder sb = new StringBuilder();
                            sb.AppendLine("\npublic void " + ИмяФункции + "(" + _params.ToString() + ")");
                            sb.AppendLine("{");
                            sb.AppendLine("\t");
                            sb.AppendLine("}");
                            txtSourceCode.Text += sb.ToString();
                            IsEventsModifity = true;
                        }

                        var _event = Events.SingleOrDefault(p => p.control == SelectedControl.Name && p.ИмяСобытия == obj.Name);
                        if (_event == null)
                        {
                            Events.Add(new RosService.Configuration.Event()
                            {
                                control = SelectedControl.Name,
                                ИмяСобытия = item.Name,
                                ИмяФункции = ИмяФункции
                            });
                            IsEventsModifity = true;
                        }
                        obj.Value = ИмяФункции;
                        break;
                    }
                }
            }
            tabControlDesigner.SelectedValue = "tabSourceCode";
        }

        private void tabControlDesigner_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Source == tabDesign)
            {
                try
                {
                    ЗагрузитьXaml(txtXaml.Text, true);
                }
                catch (Exception ex)
                {
                    e.Handled = true;
                    MessageBox.Show(ex.Message);
                }
            }
        }
        private void tabControlDesigner_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Contains(tabDesign))
            {
                //RightColumn.Width = new GridLength(320);
                //PART_GridSplitter1.IsEnabled = true;
            }
            if (e.RemovedItems.Contains(tabDesign))
            {
                txtXaml.Text = СохранитьXaml();
                //RightColumn.Width = new GridLength(0);
                //PART_GridSplitter1.IsEnabled = false;
            }
        }
        private void TextChanged(object sender, EventArgs e)
        {
            IsEdit = true;
        }

        //private void PART_Шаблоны_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //    this.Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
        //    {
        //        ОбновитьСписокКонтролов();
        //    });
        //}
        private void RunWhithDebug()
        {
            RosControl.UI.Application.Clear(ТипДанных);
            RosControl.Helper.СоздатьВкладку(ТипДанных, Хранилище.Оперативное, true);
        }
        private void Run()
        {
            var Name = ТипДанных;
            //ThreadPool.QueueUserWorkItem((WaitCallback)delegate
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                try
                {
                    using (RosService.Client client = new RosService.Client())
                    {
                        var query = new RosService.Data.Query();
                        query.ДобавитьТипы(Name);
                        query.КоличествоВыводимыхДанных = 30;
                        var _Хранилище = Хранилище.Оперативное;
                        var items = client.Архив.Поиск(query, _Хранилище).Значение;
                        if (items.Rows.Count == 0)
                        {
                            _Хранилище = Хранилище.Конфигурация;
                            items = client.Архив.Поиск(query, _Хранилище).Значение;
                            if (items.Rows.Count == 1 && items.Rows[0].Field<string>("HashCode") == "0029b00140")
                            {
                                items = null;
                                _Хранилище = Хранилище.Оперативное;
                            }
                        }

                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                        {
                            RosControl.UI.Application.Clear(ТипДанных);

                            if (items == null || items.Rows.Count == 0)
                            {
                                RosControl.Helper.СоздатьВкладку(ТипДанных, _Хранилище, true);
                            }
                            else
                            {
                                RosControl.Helper.СоздатьВкладку(items.Rows[new Random().Next(0, items.Rows.Count - 1)].Field<decimal>("id_node"), _Хранилище, true);
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate { MessageBox.Show(ex.Message); });
                }
            });
        }

        //        private void ДобавитьШаблон_Click(object sender, RoutedEventArgs e)
        //        {
        //            var template =
        //            @"<r:Window IsWhiteBackground=""True"" ButtonCompliteText=""Добавить"" ButtonCancelText=""Закрыть"" CommandComplite=""Save"" ДействиеПослеДобавления=""Закрыть"" ДействиеПослеСохранения=""Закрыть"" ПоказатьСписокФайлов=""False"" Name=""ВводФорма""
        //                xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" xmlns:r=""http://itrf.ru/2009/xaml"">
        //                <StackPanel Grid.IsSharedSizeScope=""True"">
        //                    <Label FontFamily=""Times New Roman"" FontSize=""22"" FontWeight=""Bold"" Padding=""0,0,0,0"" Margin=""0,0,0,14"">Заголовок</Label>
        //                    <r:ParamItemControl Header=""Название:"">
        //                        <TextBox />
        //                    </r:ParamItemControl>
        //                    <r:ParamItemControl Header=""Значение 1:"" />
        //                    <r:ParamItemControl Header=""Значение 2:"" />
        //                    <r:ParamItemControl Header=""Значение 3:"" />
        //                </StackPanel>
        //            </r:Window>";
        //            var ШаблонПанель = new RosControl.UI.ШаблонПанель();
        //            ШаблонПанель.Items.Add(new RosControl.UI.Шаблон()
        //            {
        //                Имя = "Просмотр",
        //                Content = RosControl.Helper.CloneElement<DependencyObject>((PART_ContentContanier.Content as UserControl).Content as DependencyObject)
        //            });
        //            ШаблонПанель.Items.Add(new RosControl.UI.Шаблон()
        //            {
        //                Имя = "Ввод",
        //                Content = System.Windows.Markup.XamlReader.Load(new System.Xml.XmlTextReader(new System.IO.StringReader(template)))
        //            });

        //            PART_ContentContanier.Content = new UserControl() { Content = ШаблонПанель };
        //            IsXamlEdit = true;
        //            ЗагрузитьXaml(СохранитьXaml(), false);
        //            IsXamlEdit = true;
        //        }
        private void ФорматироватьВесьДокумент()
        {
            IsXamlEdit = true;
            ЗагрузитьXaml(txtXaml.Text, true);
            IsXamlEdit = true;
            txtXaml.Text = СохранитьXaml();
        }

        private void СкрытьПоУсловию_Click(object sender, RoutedEventArgs e)
        {
            if (this.SelectedControl == null) return;

            СкрытьПоУсловию window = new СкрытьПоУсловию();
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.ItemsSourceControls = Controls.Where(p => p is Selector && !string.IsNullOrEmpty(p.Name)).Select(p => new ListBoxItem() { Content = p.Name }).OrderBy(p => p.Content);
            window.ItemsSourceAttributes = Attributes.Select(p => new ListBoxItem() { Content = p.Type.Name }).OrderBy(p => Content);
            window.Closed += delegate
            {
                if (window.DialogResult.Value)
                {
                    this.SelectedControl.SetBinding(FrameworkElement.VisibilityProperty, window.SelectedBinding);
                    IsXamlEdit = true;
                }
            };
            window.ShowDialog();
        }
        private void ТолькоЧтениеПоУсловию_Click(object sender, RoutedEventArgs e)
        {
            if (this.SelectedControl == null) return;

            var window = new ТолькоДляЧтенияПоУсловию();
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.ItemsSourceControls = Controls.Where(p => p is Selector && !string.IsNullOrEmpty(p.Name)).Select(p => new ListBoxItem() { Content = p.Name }).OrderBy(p => p.Content);
            window.ItemsSourceAttributes = Attributes.Select(p => new ListBoxItem() { Content = p.Type.Name }).OrderBy(p => Content);
            window.Closed += delegate
            {
                if (window.DialogResult.Value)
                {
                    if (this.SelectedControl is RosControl.UI.Window)
                        this.SelectedControl.SetBinding(RosControl.UI.Window.IsEnabledCompliteProperty, window.SelectedBinding);
                    else if (this.SelectedControl is System.Windows.Controls.TextBox)
                        this.SelectedControl.SetBinding(System.Windows.Controls.TextBox.IsReadOnlyProperty, window.SelectedBinding);
                    else
                        this.SelectedControl.SetBinding(FrameworkElement.IsEnabledProperty, window.SelectedBinding);
                    IsXamlEdit = true;
                }
            };
            window.ShowDialog();
        }
        private void ВыделитьContextMenu_Click(object sender, RoutedEventArgs e)
        {
            var obj = e.OriginalSource as MenuItem;
            if (obj == null || obj.Tag == null) return;

            if (DesignerManager != null)
            {
                DesignerManager.SetSelection(obj.Tag as FrameworkElement);
            }
        }
        //private void ДобавитьContextMenu_Click(object sender, RoutedEventArgs e)
        //{
        //    var obj = e.OriginalSource as MenuItem;
        //    if (obj == null || obj.Tag == null) return;

        //    var addControl = null as FrameworkElement;
        //    switch (obj.Tag.ToString())
        //    {
        //        case "Button":
        //            addControl = new Button()
        //            {
        //                Content = "Button",
        //                Width = 100,
        //                Height = 22
        //            };
        //            break;

        //        case "TextBox":
        //            addControl = new System.Windows.Controls.TextBox()
        //            {
        //                Text = "TextBox",
        //                Width = 120
        //            };
        //            break;

        //        case "SearchTextBox":
        //            addControl = new RosControl.UI.SearchTextBox()
        //            {
        //                Text = "SearchTextBox",
        //                Width = 120
        //            };
        //            break;

        //        case "Label":
        //            addControl = new Label()
        //            {
        //                Content = "Label",
        //                Width = 120,
        //                Height = 27
        //            };
        //            break;

        //        case "ComboBox":
        //            addControl = new System.Windows.Controls.ComboBox()
        //            {
        //                Width = 160
        //            };
        //            break;

        //        case "ListView":
        //            ListView listView = new ListView()
        //            {
        //                Width = 360,
        //                Height = 200
        //            };
        //            GridView gridView = new GridView();
        //            gridView.Columns.Add(new GridViewColumn()
        //            {
        //                Header = "Column1",
        //                Width = 60
        //            });
        //            gridView.Columns.Add(new GridViewColumn()
        //            {
        //                Header = "Column2",
        //                Width = 220
        //            });
        //            listView.View = gridView;
        //            addControl = listView;
        //            break;

        //        case "DialogWindow":
        //            {
        //                addControl = new RosControl.UI.Window();
        //            }
        //            break;

        //        case "FileViewer":
        //            {
        //                addControl = new RosControl.UI.FileViewer()
        //                {
        //                    Width = 200,
        //                    Height = 200
        //                };
        //            }
        //            break;

        //        case "ParamItemControl":
        //            {
        //                addControl = new RosControl.UI.ParamItemControl()
        //                {
        //                    Content = new System.Windows.Controls.TextBox(),
        //                    Header = "Новый элемент:"
        //                };
        //            }
        //            break;

        //        case "DateTimePicker":
        //            {
        //                addControl = new RosControl.UI.DatePicker();
        //            }
        //            break;
        //    }
        //    if (addControl != null)
        //    {
        //        if (this.SelectedControl is ContentControl)
        //        {
        //            (this.SelectedControl as ContentControl).Content = addControl;
        //        }
        //        else if (this.SelectedControl is Panel)
        //        {
        //            (this.SelectedControl as Panel).Children.Add(addControl);
        //        }
        //        else if (this.SelectedControl is ItemsControl)
        //        {
        //            (this.SelectedControl as ItemsControl).Items.Add(addControl);
        //        }
        //        ОбновитьСписокКонтролов();
        //    }
        //    IsXamlEdit = true;
        //}
        private void RedoUndo_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var editor = null as TextEditor;
            if (tabControlDesigner.SelectedValue.Equals("tabDesign"))
                editor = txtXaml;
            else
                editor = txtSourceCode;

            if (e.Command == ApplicationCommands.Redo)
            {
                editor.Redo();
            }
            else if (e.Command == ApplicationCommands.Undo)
            {
                editor.Undo();
            }
        }
        private void NotACommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            switch (System.Convert.ToString(e.Parameter))
            {
                case "Просмотр":
                    Run();
                    break;

                case "Ввод":
                    RunWhithDebug();
                    break;

                case "ФорматироватьВесьДокумент":
                    ФорматироватьВесьДокумент();
                    break;

                case "Копировать":
                    Копировать();
                    break;
            }
        }

        #region IDisposable Members

        public void Dispose()
        {
            SelectedControl = null;
            ParentItemsSource = null;
            PART_ContentContanier = null;
            Шаблоны = null;
            Property.Properties = null;
            Event.Properties = null;

            if (Attributes != null)
                Attributes.Clear();
            if (Bindings != null)
                Bindings.Clear();
            if (Events != null)
                Events.Clear();
            if (Controls != null)
                Controls.Clear();

            Attributes = null;
            Bindings = null;
            //Справочники = null;
            //Константы = null;
            Controls = null;

            if (DesignerManager != null)
            {
                DesignerManager.SelectionChanged -= new RoutedEventHandler(DesignerManager_SelectionChanged);
                DesignerManager.Dispose();
                DesignerManager = null;
            }
            if (txtSourceCode != null)
            {
                txtSourceCode.Dispose();
                txtSourceCode = null;
            }
            if (txtXaml != null)
            {
                txtXaml.Dispose();
                txtXaml = null;
            }
            if (PART_КонтентПанель != null)
                PART_КонтентПанель.Dispose(true);
        }

        #endregion

        private void Find_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var editor = null as TextEditor;
            if (tabControlDesigner.SelectedValue.Equals("tabDesign")) editor = txtXaml;
            else editor = txtSourceCode;
        }

        ~РедакторXaml()
        {
            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate()
            {
                RosControl.UI.Application.RemoveWindow(id);
            });
        }

        private bool saving;
        private void IsSaving(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = !saving;
        }
    }
    public class РедакторXamlTypeItem
    {
        public RosService.Configuration.Type Type { get; set; }
        public bool IsBinding { get; set; }
    };
}
