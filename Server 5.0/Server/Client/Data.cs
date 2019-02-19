using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.ServiceModel;
using System.Runtime.Serialization;
using System.IO;
using RosService.Intreface;


namespace RosService.Data
{
    public partial class DataClient
    {
        public Dictionary<string, object> ПолучитьЗначение(object id_node, string[] attributes)
        {
            return ПолучитьЗначение(id_node, attributes, Хранилище.Оперативное);
        }
        public Dictionary<string, object> ПолучитьЗначение(object id_node, string[] attributes, Хранилище хранилище)
        {
            return RosService.Helper.ConvertHelper.ConvertDataValue(ПолучитьЗначение(id_node, attributes, хранилище, Client.Domain));
        }
        public Dictionary<string, object> ПолучитьЗначенияФормы(decimal id_node, string Шаблон, Хранилище хранилище)
        {
            return RosService.Helper.ConvertHelper.ConvertDataValue(ПолучитьЗначенияФормы(Шаблон, id_node, хранилище, Client.Domain));
        }
        public Dictionary<string, object> ПолучитьЗначенияФормы(decimal id_node, Хранилище хранилище)
        {
            return RosService.Helper.ConvertHelper.ConvertDataValue(ПолучитьЗначенияФормы(null, id_node, хранилище, Client.Domain));
        }


        public T ПолучитьЗначение<T>(object id_node, string attribute)
        {
            return ПолучитьЗначение<T>(id_node, attribute, Хранилище.Оперативное);
        }
        public T ПолучитьЗначение<T>(object id_node, string attribute, Хранилище хранилище)
        {
            try
            {
                var value = ПолучитьЗначение(id_node, new string[] { attribute }, хранилище, Client.Domain)[attribute];
                if (value.IsСписок)
                {
                    return (T)(object)value.Таблица;
                }
                else if (value.Значение is T)
                {
                    return (T)value.Значение;
                }
                else
                {
                    return (T)Convert.ChangeType(value.Значение, typeof(T));
                }
            }
            catch (InvalidCastException)
            {
                return default(T);
            }
            catch (FormatException)
            {
                return default(T);
            }
            catch (OverflowException)
            {
                return default(T);
            }
            catch (ArgumentNullException)
            {
                return default(T);
            }
        }
        public T ПолучитьЗначение<T>(string attribute, Хранилище хранилище)
        {
            return ПолучитьЗначение<T>(0, attribute, хранилище);
        }
        public T ПолучитьКонстанту<T>(string Имя)
        {
            try
            {
                var value = ПолучитьКонстанту(Имя, Client.Domain);
                if (value is T)
                {
                    return (T)value;
                }
                else
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
            }
            catch (InvalidCastException)
            {
                return default(T);
            }
            catch (FormatException)
            {
                return default(T);
            }
            catch (OverflowException)
            {
                return default(T);
            }
            catch (ArgumentNullException)
            {
                return default(T);
            }
        }

        public void СохранитьЗначение(object id_node, string attribute, object value)
        {
            СохранитьЗначение(id_node, attribute, value, Хранилище.Оперативное);
        }
        public void СохранитьЗначение(object id_node, string attribute, object value, bool ДобавитьВИсторию)
        {
            var values = new Dictionary<string, object>();
            values.Add(attribute, value);
            СохранитьЗначение(id_node, values, Хранилище.Оперативное, ДобавитьВИсторию);
        }
        public void СохранитьЗначение(object id_node, string attribute, object value, Хранилище хранилище)
        {
            var values = new Dictionary<string, object>();
            values.Add(attribute, value);
            СохранитьЗначение(id_node, values, хранилище);
        }
        public void СохранитьЗначение(object id_node, Dictionary<string, object> значения, Хранилище хранилище)
        {
            СохранитьЗначение(id_node, значения, хранилище, true);
        }
        public void СохранитьЗначение(object id_node, Dictionary<string, object> значения, Хранилище хранилище, bool ДобавитьВИсторию)
        {
            var values = RosService.Helper.ConvertHelper.ConvertDataValue(значения);
            if (values != null && values.Count() > 0)
            {
                СохранитьЗначение(id_node, values, ДобавитьВИсторию, хранилище, Client.UserName, Client.Domain);
            }
        }

        public void СохранитьЗначениеПоиск(Query запрос, Dictionary<string, object> значения, Хранилище хранилище)
        {
            var values = RosService.Helper.ConvertHelper.ConvertDataValue(значения);
            if (values != null && values.Count() > 0)
            {
                СохранитьЗначениеПоиск(запрос, values, true, хранилище, RosService.Client.UserName, RosService.Client.Domain);
            }
        }
        public void СохранитьЗначениеПоиск(Query запрос, Dictionary<string, object> значения, Хранилище хранилище, bool ДобавитьВИсторию)
        {
            var values = RosService.Helper.ConvertHelper.ConvertDataValue(значения);
            if (values != null && values.Count() > 0)
            {
                СохранитьЗначениеПоиск(запрос, values, ДобавитьВИсторию, хранилище, RosService.Client.UserName, RosService.Client.Domain);
            }
        }

        //[Obsolete("Устаревший метод 'СохранитьЗначение(decimal id_node, FrameworkElement content)'", false)]
        //public void СохранитьЗначение(decimal id_node, System.Windows.FrameworkElement content)
        //{
        //    if (content == null) return;

        //    using (ConfigurationClient conf = new ConfigurationClient(RosService.Client.DefaultBinding, new EndpointAddress(string.Format(RosService.Client.DefaultEndpointAddress, "Configuration"))))
        //    {
        //        var node = ПолучитьРаздел(id_node, Хранилище.Оперативное);
        //        if (node == null) return;

        //        var values = new Dictionary<string, object>();
        //        foreach (var item in conf.СписокАтрибутов(node.ТипДанных))
        //        {
        //            if (item.IsReadOnly) continue;

        //            var name = string.Format("{0}_{1}", node.ТипДанных, item.Name.Replace(".", "_"));
        //            var control = content.FindName(name) as System.Windows.FrameworkElement;
        //            if (control == null) continue;

        //            //MessageBox.Show(string.Format("{0} : {1}", control.Name, control.GetType().FullName));

        //            object value = null;
        //            if (control is System.Windows.Controls.TextBox) value = control.GetValue(System.Windows.Controls.TextBox.TextProperty);
        //            else if (control is System.Windows.Controls.TextBlock) value = control.GetValue(System.Windows.Controls.TextBlock.TextProperty);
        //            else if (control is System.Windows.Controls.Primitives.ToggleButton) value = control.GetValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty);
        //            else if (control is System.Windows.Controls.ContentControl) value = control.GetValue(System.Windows.Controls.ContentControl.ContentProperty);
        //            else if (control is System.Windows.Controls.ComboBox) value = control.GetValue(System.Windows.Controls.ComboBox.SelectedValueProperty);
        //            else if (control is System.Windows.Controls.Primitives.Selector) value = control.GetValue(System.Windows.Controls.Primitives.Selector.SelectedValueProperty);
        //            else if (control.GetType().FullName == "RosControl.UI.DatePicker")
        //            {
        //                var field = control.GetType().GetField("SelectedDateProperty");
        //                if (field != null)
        //                {
        //                    var dp = field.GetValue(control) as System.Windows.DependencyProperty;
        //                    if (dp != null)
        //                    {
        //                        value = control.GetValue(dp);
        //                    }
        //                }
        //            }
        //            //else if (control.GetType().FullName == "RosService.UI.SearchTextBox") throw new Exception("Не реализованно"); // value = control.GetValue(Selector.SelectedValueProperty);
        //            else if (control is System.Windows.Controls.ItemsControl && control.GetValue(System.Windows.Controls.ItemsControl.ItemsSourceProperty) is System.Data.DataTable) value = control.GetValue(System.Windows.Controls.ItemsControl.ItemsSourceProperty);

        //            values.Add(item.Name, value);
        //        }
        //        СохранитьЗначение(id_node, values, Хранилище.Оперативное);
        //    }
        //}


        public TableValue Поиск(Query запрос)
        {
            return Поиск(запрос, Хранилище.Оперативное, Client.Domain);
        }
        public TableValue Поиск(Query запрос, Хранилище хранилище)
        {
            return Поиск(запрос, хранилище, Client.Domain);
        }

        public TableValue ПоискИстории(Query запрос)
        {
            return ПоискИстории(запрос, Хранилище.Оперативное, Client.Domain);
        }
        public TableValue ПоискИстории(Query запрос, Хранилище хранилище)
        {
            return ПоискИстории(запрос, хранилище, Client.Domain);
        }

        public decimal ПоискРазделаПоИдентификаторуОбъекта(string ИдентификаторОбъекта)
        {
            return ПоискРазделаПоИдентификаторуОбъекта(ИдентификаторОбъекта, Хранилище.Оперативное, Client.Domain);
        }
        public decimal ПоискРазделаПоИдентификаторуОбъекта(string ИдентификаторОбъекта, Хранилище хранилище)
        {
            return ПоискРазделаПоИдентификаторуОбъекта(ИдентификаторОбъекта, хранилище, Client.Domain);
        }

        public decimal ПоискРазделаПоКлючу(string HashCode)
        {
            return ПоискРазделаПоКлючу(HashCode, Хранилище.Оперативное, Client.Domain);
        }
        public decimal ПоискРазделаПоКлючу(string HashCode, Хранилище хранилище)
        {
            return ПоискРазделаПоКлючу(HashCode, хранилище, Client.Domain);
        }
        
        #region Перегруженные функции
        public decimal ДобавитьРаздел(object id_parent, string тип)
        {
            return ДобавитьРаздел(id_parent, тип, null, Хранилище.Оперативное);
        }
        public decimal ДобавитьРаздел(object id_parent, string тип, Dictionary<string, object> значения)
        {
            return ДобавитьРаздел(id_parent, тип, значения, Хранилище.Оперативное);
        }
        public decimal ДобавитьРаздел(object id_parent, string тип, Dictionary<string, object> значения, Хранилище хранилище)
        {
            if (Client.ПередДобавлением != null)
                Client.ПередДобавлением(this, new AddClientEventArgs(id_parent, тип, значения));

            var values = RosService.Helper.ConvertHelper.ConvertDataValue(значения);
            var id_node = ДобавитьРаздел(id_parent, тип, values, true, хранилище, Client.UserName, Client.Domain);

            if (Client.ПослеДобавления != null)
                Client.ПослеДобавления(this, new AddClientPostEventArgs(id_node, id_parent, тип, значения));

            return id_node;
        }
        public decimal ДобавитьРаздел(object id_parent, string тип, Dictionary<string, object> значения, Хранилище хранилище, bool ДобавитьВИсторию)
        {
            if (Client.ПередДобавлением != null)
                Client.ПередДобавлением(this, new AddClientEventArgs(id_parent, тип, значения));

            var values = RosService.Helper.ConvertHelper.ConvertDataValue(значения);
            var id_node = ДобавитьРаздел(id_parent, тип, values, ДобавитьВИсторию, хранилище, Client.UserName, Client.Domain);

            if (Client.ПослеДобавления != null)
                Client.ПослеДобавления(this, new AddClientPostEventArgs(id_node, id_parent, тип, значения));

            return id_node;
        }
        public decimal ДобавитьРаздел(object id_parent, string тип, Dictionary<string, object> значения, string user)
        {
            if (Client.ПередДобавлением != null)
                Client.ПередДобавлением(this, new AddClientEventArgs(id_parent, тип, значения));

            var values = RosService.Helper.ConvertHelper.ConvertDataValue(значения);
            var id_node = ДобавитьРаздел(id_parent, тип, values, true, Хранилище.Оперативное, user, Client.Domain);

            if (Client.ПослеДобавления != null)
                Client.ПослеДобавления(this, new AddClientPostEventArgs(id_node, id_parent, тип, значения));

            return id_node;
        }

        public NodeInfo[] СписокРазделов(decimal id_parent, int limit, Хранилище хранилище)
        {
            return СписокРазделов(id_parent, null, null, limit, хранилище, Client.Domain);
        }
        public NodeInfo[] СписокРазделов(decimal id_parent, string Тип, string[] Атрибуты, int limit, Хранилище хранилище)
        {
            return СписокРазделов(id_parent, Тип, Атрибуты, limit, хранилище, Client.Domain);
        }
        public NodeInfo ПолучитьРаздел(decimal id_node, Хранилище хранилище)
        {
            return ПолучитьРаздел(id_node, null, хранилище, Client.Domain);
        }
        public NodeInfo ПолучитьРаздел(decimal id_node, string[] Атрибуты, Хранилище хранилище)
        {
            return ПолучитьРаздел(id_node, Атрибуты, хранилище, Client.Domain);
        }

        public void УдалитьРаздел(decimal id_node, Хранилище хранилище)
        {
            УдалитьРаздел(false, false, new decimal[] { id_node }, хранилище, Client.UserName, Client.Domain);
        }
        public void УдалитьРаздел(bool УдалитьЗависимыеОбъекты, decimal id_node, Хранилище хранилище)
        {
            УдалитьРаздел(false, УдалитьЗависимыеОбъекты, new decimal[] { id_node }, хранилище, Client.UserName, Client.Domain);
        }
        public void УдалитьРаздел(bool ВКорзину, bool УдалитьЗависимыеОбъекты, decimal id_node, Хранилище хранилище, string user, string domain)
        {
            УдалитьРаздел(ВКорзину, УдалитьЗависимыеОбъекты, new decimal[] { id_node }, хранилище, user, domain);
        }
        public void УдалитьРаздел(RosService.Data.Query query, Хранилище хранилище)
        {
            УдалитьРазделПоиск(false, true, query, хранилище, Client.UserName, Client.Domain);
        }
        public void УдалитьПодразделы(decimal id_node, Хранилище хранилище)
        {
            УдалитьПодразделы(false, new decimal[] { id_node }, хранилище, Client.UserName, Client.Domain);
        }



        /// <summary>
        /// Отправка почты с учетом переадресации в значении '//Константы/Почта.Переадресация'
        /// </summary>
        public void ОтправитьПисьмо(string Тема, string Содержание)
        {
            ОтправитьПисьмо(string.Empty, Тема, Содержание, null, false, Client.UserName, Client.Domain);
        }
        public void ОтправитьПисьмо(string Кому, string Тема, string Содержание, Файл[] СписокФайлов, bool IsBodyHtml, string user)
        {
            ОтправитьПисьмо(Кому, Тема, Содержание, СписокФайлов, IsBodyHtml, user, Client.Domain);
        }
        public void ОтправитьПисьмо(string Кому, string Тема, string Содержание, Файл[] СписокФайлов, bool IsBodyHtml)
        {
            ОтправитьПисьмо(Кому, Тема, Содержание, СписокФайлов, IsBodyHtml, Client.UserName, Client.Domain);
        }
        public void ОтправитьПисьмо(string Кому, string Тема, string Содержание)
        {
            ОтправитьПисьмо(Кому, Тема, Содержание, null, false, Client.UserName, Client.Domain);
        }

        //[Obsolete("Устаревший вызов", false)]
        //public string Отчет(string НазваниеОтчета, Dictionary<string, object> Параметры)
        //{
        //    return Отчет(НазваниеОтчета, Параметры, ФорматОтчета.ПоУмолчанию);
        //}
        //[Obsolete("Устаревший вызов", false)]
        //public string Отчет(string НазваниеОтчета, Dictionary<string, object> Параметры, ФорматОтчета ФорматОтчета)
        //{
        //    var file = Отчет(НазваниеОтчета,
        //        Параметры.Select(p => new Query.Параметр() { Имя = p.Key, Значение = p.Value }).ToArray(),
        //        ФорматОтчета,
        //        Client.UserName, Client.Domain);
        //    if (file != null && file.Stream != null)
        //    {
        //        return Encoding.Default.GetString(file.Stream);
        //    }
        //    return string.Empty;
        //}

        public void ПереместитьРаздел(decimal id_node, decimal ПереместитьВРаздел, Хранилище хранилище, string domain)
        {
            ПереместитьРаздел(id_node, ПереместитьВРаздел, true, хранилище, domain);
        }
        public void ПереместитьРаздел(decimal id_node, decimal ПереместитьВРаздел, bool ОбновитьИндексы, Хранилище хранилище)
        {
            ПереместитьРаздел(id_node, ПереместитьВРаздел, true, хранилище, Client.Domain);
        }
        #endregion

        #region Перегруженные функции для API
        public T ПолучитьЗначение<T>(decimal id_node) where T : Object
        {
            return (T)ПолучитьЗначение<T>(id_node, Хранилище.Оперативное);
        }
        public T ПолучитьЗначение<T>(decimal id_node, Хранилище хранилище) where T : Object
        {
            var item = default(T);
            ((Object)item).client = this;
            ((Object)item).id_node = id_node;
            ((Object)item).хранилище = хранилище;
            return item;
        }

      
        public decimal ДобавитьРаздел(object id_parent, Object value)
        {
            return ДобавитьРаздел(id_parent, value.GetType().Name, RosService.Helper.ConvertHelper.ConvertDataValue(value.v), true, Хранилище.Оперативное, 
                RosService.Client.UserName, RosService.Client.Domain);
        }
        public decimal ДобавитьРаздел(object id_parent, Object value, Хранилище хранилище)
        {
            return ДобавитьРаздел(id_parent, value.GetType().Name,
                RosService.Helper.ConvertHelper.ConvertDataValue(value.v),
                true, хранилище, RosService.Client.UserName, RosService.Client.Domain);
        }
        public decimal ДобавитьРаздел(object id_parent, Object value, Хранилище хранилище, bool ДобавитьВИсторию)
        {
            return ДобавитьРаздел(id_parent, value.GetType().Name,
                RosService.Helper.ConvertHelper.ConvertDataValue(value.v),
                ДобавитьВИсторию, хранилище, RosService.Client.UserName, RosService.Client.Domain);
        }


        public void СохранитьЗначение(object id_node, Object value)
        {
            СохранитьЗначение(id_node, value.v, Хранилище.Оперативное);
        }
        public void СохранитьЗначение(object id_node, Object value, Хранилище хранилище)
        {
            СохранитьЗначение(id_node, value.v, хранилище);
        }
        public void СохранитьЗначение(object id_node, Object value, Хранилище хранилище, bool ДобавитьВИсторию)
        {
            СохранитьЗначение(id_node, value.v, хранилище, ДобавитьВИсторию);
        }

        public IEnumerable<ФайлИнформация> СписокФайлов(object id_node)
        {
            return _СписокФайлов(id_node, Хранилище.Оперативное, RosService.Client.Domain);
        }
        public IEnumerable<ФайлИнформация> СписокФайлов(object id_node, Хранилище хранилище)
        {
            return _СписокФайлов(id_node, хранилище, RosService.Client.Domain);
        }

        public int КоличествоФайлов(object id_node)
        {
            return КоличествоФайлов((decimal)id_node, Хранилище.Оперативное, RosService.Client.Domain);
        }
        public int КоличествоФайлов(object id_node, Хранилище хранилище)
        {
            return КоличествоФайлов((decimal)id_node, хранилище, RosService.Client.Domain);
        }
        #endregion
    }

    //public partial class Value
    //{
    //    private DataTable __Значение;
    //    [System.Xml.Serialization.XmlIgnore]
    //    internal DataTable Таблица
    //    {
    //        get
    //        {
    //            if (__Значение != null)
    //                return __Значение;

    //            if (buffer == null || buffer.Length == 0)
    //                __Значение = new DataTable("tblValue");
    //            else
    //                __Значение = RosService.ServiceModel.DataSerializer.DeserializeTable(new MemoryStream(buffer));
    //            return __Значение;
    //        }
    //    }
    //    public void SetTable(DataTable value)
    //    {
    //        if (value == null)
    //        {
    //            buffer = null;
    //        }
    //        else
    //        {
    //            buffer = RosService.ServiceModel.DataSerializer.SerializeDataTable(value);
    //        }
    //        __Значение = null;
    //    }
    //}
    //public partial class TableValue
    //{
    //    private DataTable __Значение;
    //    [System.Xml.Serialization.XmlIgnore]
    //    public DataTable Значение
    //    {
    //        get
    //        {
    //            if (__Значение != null)
    //                return __Значение;

    //            if (buffer == null || buffer.Length == 0)
    //                __Значение = new DataTable("tblValue");
    //            else
    //                __Значение = RosService.ServiceModel.DataSerializer.DeserializeTable(new MemoryStream(buffer));
    //            return __Значение;
    //        }
    //        set
    //        {
    //            if (value == null)
    //            {
    //                buffer = null;
    //            }
    //            else
    //            {
    //                buffer = RosService.ServiceModel.DataSerializer.SerializeDataTable(value);
    //            }
    //            __Значение = null;
    //        }
    //    }
    //}
    //public partial class Query 
    //{
    //    public Query()
    //    {
    //        InitializeComponent();
    //    }
    //    public Query(string запрос)
    //    {
    //        InitializeComponent();
    //        this.СтрокаЗапрос = запрос;
    //    }
    //    public Query(int КоличествоВыводимыхДанных)
    //    {
    //        InitializeComponent();
    //        this.КоличествоВыводимыхДанных = КоличествоВыводимыхДанных;
    //    }
    //    public Query(int КоличествоВыводимыхДанных, int КоличествоВыводимыхСтраниц)
    //    {
    //        InitializeComponent();
    //        this.КоличествоВыводимыхДанных = КоличествоВыводимыхДанных;
    //        this.КоличествоВыводимыхСтраниц = КоличествоВыводимыхСтраниц;
    //    }

    //    protected void InitializeComponent()
    //    {
    //        this.МестаПоиска = new МестоПоиска[0];
    //        this.ВыводимыеКолонки = new Колонка[0];
    //        this.Сортировки = new Сортировка[0];
    //        this.Типы = new string[0];
    //        this.УсловияПоиска = new УсловиеПоиска[0];
    //        this.Параметры = new Параметр[0];
    //        this.Группировки = new string[0];
    //        this.Объединения = new Объединение[0];
    //    }
    //    public void ДобавитьСортировку(string Атрибут)
    //    {
    //        ДобавитьСортировку(Атрибут, НаправлениеСортировки.Asc);
    //    }
    //    public void ДобавитьСортировку(string Атрибут, НаправлениеСортировки Направление)
    //    {
    //        var items = this.Сортировки.ToList();
    //        items.Add(new Сортировка()
    //        {
    //            Атрибут = Атрибут,
    //            Направление = Направление
    //        });
    //        this.Сортировки = items.ToArray();
    //    }

    //    public void ДобавитьВыводимыеКолонки(params string[] Колонки)
    //    {
    //        this.ВыводимыеКолонки = ВыводимыеКолонки.Union(Колонки.Select(p => new Колонка() { Атрибут = p })).ToArray();
    //    }
    //    public void ДобавитьВыводимыеКолонки(string Атрибут, Data.Query.ФункцияАгрегации Функция)
    //    {
    //        this.ВыводимыеКолонки = ВыводимыеКолонки.Union(new Колонка[] { new Колонка() { Атрибут = Атрибут, Функция = Функция } }).ToArray();
    //    }
    //    public void ДобавитьВыводимыеКолонки(string Атрибут, MemberTypes Тип)
    //    {
    //        this.ВыводимыеКолонки = ВыводимыеКолонки.Union(new Колонка[] 
    //        { 
    //            new Колонка() { Атрибут = Атрибут, Тип = Тип } 
    //        }).ToArray();
    //    }
    //    public Колонка ДобавитьВыводимыеКолонки(string Атрибут)
    //    {
    //        var column = new Колонка() { Атрибут = Атрибут };
    //        this.ВыводимыеКолонки = ВыводимыеКолонки.Union(new Колонка[] { column }).ToArray();
    //        return column;
    //    }
    //    public void ДобавитьВычисляемыеКолонки(params string[] Колонки)
    //    {
    //        this.ВыводимыеКолонки = ВыводимыеКолонки.Union(Колонки.Select(p => new Колонка() { Атрибут = p, Функция = ФункцияАгрегации.Sql /*IsВычисляемое = true*/ })).ToArray();
    //    }
    //    public void ДобавитьГруппировки(params string[] Группировки)
    //    {
    //        this.Группировки = Группировки;
    //    }

    //    public void ДобавитьМестоПоиска(decimal id_node, int МаксимальнаяГлубина)
    //    {
    //        var items = this.МестаПоиска.ToList();
    //        items.Add(new МестоПоиска()
    //        {
    //            id_node = id_node,
    //            МаксимальнаяГлубина = МаксимальнаяГлубина
    //        });
    //        this.МестаПоиска = items.ToArray();
    //    }
    //    /// <summary>
    //    /// Поиск места из параметра в запросе или по 'Идентификатору объекта'
    //    /// </summary>
    //    /// <param name="Имя">При указании @ поиск производится из параметра в запросе</param>
    //    /// <param name="МаксимальнаяГлубина">Максимальная глубина при поиске</param>
    //    public void ДобавитьМестоПоиска(string Имя, int МаксимальнаяГлубина)
    //    {
    //        var items = this.МестаПоиска.ToList();
    //        items.Add(new МестоПоиска()
    //        {
    //            id_node = Имя,
    //            МаксимальнаяГлубина = МаксимальнаяГлубина
    //        });
    //        this.МестаПоиска = items.ToArray();
    //    }

    //    [Obsolete("Используйте новый метод ДобавитьМестоПоиска(string Имя, int МаксимальнаяГлубина)", false)]
    //    public void ДобавитьМестоПоиска(string ИдентификаторОбъекта, int МаксимальнаяГлубина, Хранилище Хранилище)
    //    {
    //        var items = new List<МестоПоиска>(this.МестаПоиска);
    //        items.Add(new МестоПоиска()
    //        {
    //            id_node = ИдентификаторОбъекта,
    //            МаксимальнаяГлубина = МаксимальнаяГлубина
    //        });
    //        this.МестаПоиска = items.ToArray();
    //    }

    //    public УсловиеПоиска ДобавитьУсловиеПоиска(string Атрибут, object Значение)
    //    {
    //        return ДобавитьУсловиеПоиска(Атрибут, Значение, Оператор.Равно);
    //    }
    //    public УсловиеПоиска ДобавитьУсловиеПоиска(string Атрибут, object Значение, Оператор Оператор)
    //    {
    //        var items = this.УсловияПоиска.ToList();
    //        var item = new УсловиеПоиска()
    //        {
    //            Атрибут = Атрибут,
    //            Значение = Значение,
    //            Оператор = Оператор
    //        };
    //        items.Add(item);
    //        this.УсловияПоиска = items.ToArray();
    //        return item;
    //    }
    //    public УсловиеПоиска ДобавитьУсловиеПоиска(string Значение)
    //    {
    //        var items = this.УсловияПоиска.ToList();
    //        var item = new УсловиеПоиска()
    //        {
    //            Атрибут = string.Empty,
    //            Значение = Значение,
    //            Оператор = Оператор.Функция
    //        };
    //        items.Add(item);
    //        this.УсловияПоиска = items.ToArray();
    //        return item;
    //    }
    //    public void ДобавитьТипы(params string[] Имя)
    //    {
    //        this.Типы = Имя;
    //    }
    //    public void ДобавитьПараметр(string Имя, object Значение)
    //    {
    //        var items = this.Параметры.ToList();
    //        items.Add(new Параметр()
    //        {
    //            Имя = Имя,
    //            Значение = Значение
    //        });
    //        this.Параметры = items.ToArray();
    //    }


    //    public void ДобавитьОбъединение(string Атрибут, decimal id_node)
    //    {
    //        var items = this.Объединения.ToList();
    //        items.Add(new Объединение()
    //        {
    //            Атрибут = Атрибут,
    //            МестоПоиска = new МестоПоиска() { id_node = id_node }
    //        });
    //        this.Объединения = items.ToArray();
    //    }


    //    /// <summary>
    //    /// Преобраховать запрос в Xml
    //    /// </summary>
    //    /// <returns></returns>
    //    public static string Serialize(object Запрос)
    //    {
    //        var query = (Запрос is string) ? new Query() { СтрокаЗапрос = (string)Запрос } : Запрос as Query;
    //        var serializer = new System.Xml.Serialization.XmlSerializer(typeof(Query));
    //        var sb = new System.Text.StringBuilder();
    //        using (var sw = new System.IO.StringWriter(sb))
    //        {
    //            serializer.Serialize(sw, query);
    //            return sb.ToString();
    //        }
    //    }
    //}
    //public partial class CustomValue
    //{
    //    public CustomValue(MemberTypes MemberType, string HashCode, object Value)
    //    {
    //        this.HashCode = HashCode;
    //        this.Value = Value;
    //        this.MemberType = MemberType;
    //    }
    //    public CustomValue(string HashCode, string Value)
    //    {
    //        this.HashCode = HashCode;
    //        this.Value = Value;
    //        this.MemberType = MemberTypes.String;
    //    }
    //    public CustomValue(string HashCode, decimal Value)
    //    {
    //        this.HashCode = HashCode;
    //        this.Value = Value;
    //        this.MemberType = MemberTypes.Double;
    //    }
    //    public CustomValue(string HashCode, DateTime Value)
    //    {
    //        this.HashCode = HashCode;
    //        this.Value = Value;
    //        this.MemberType = MemberTypes.DateTime;
    //    }
    //}

    public abstract class Object
    {
        internal protected Dictionary<string, object> v;
        internal protected Data.DataClient client;
        internal protected decimal id_node;
        internal protected Хранилище хранилище;

        public Object()
        {
        }

        protected object GetValue(string name)
        {
            if (this.id_node > 0)
            {
                return client.ПолучитьЗначение<object>(id_node, name);
            }
            else
            {
                return v != null && v.ContainsKey(name) ? v[name] : null;
            }
        }
        protected void SetValue(string name, object value)
        {
            if (this.id_node > 0)
            {
                client.СохранитьЗначение(id_node, name, value, true);
            }
            else
            {
                if (v == null)
                    v = new Dictionary<string, object>();
                v[name] = value;
            }
        }
    }
}