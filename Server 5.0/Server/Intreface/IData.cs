using System.Collections.Generic;
using System.ServiceModel;
using System.Xml;
using System.Linq;
using System.Collections;
using System.Data;
using System;
using System.Text.RegularExpressions;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using System.Threading.Tasks;
using RosService.Intreface;
using RosService.Data;
using RosService.Configuration;
using System.Runtime.Serialization;

namespace RosService.Intreface
{
    [ServiceContract]
    public interface IData
    {
        #region Работа с разделами
        [OperationContract(Action = "QND", ReplyAction = "QND")]
        DependencyNodeInfo[] СписокЗависимыхРазделов(object id_node, Хранилище хранилище, string domain);

        [OperationContract(Action = "QN", ReplyAction = "QN")]
        NodeInfo[] СписокРазделов(decimal id_parent, string Тип, string[] Атрибуты, int limit, Хранилище хранилище, string domain);

        [OperationContract(Action = "GN", ReplyAction = "GN")]
        NodeInfo ПолучитьРаздел(decimal id_node, string[] Атрибуты, Хранилище хранилище, string domain);

        [OperationContract(Action = "A", ReplyAction = "A")]
        decimal ДобавитьРаздел(object id_parent, string тип, Dictionary<string, Value> значения, bool ДобавитьВИсторию, Хранилище хранилище, string user, string domain);

        [OperationContract(Action = "A2", ReplyAction = "A2")]
        decimal Add(object parent, string type, Dictionary<string, object> values, Хранилище stage, string user, string domain);
        [OperationContract(Action = "AA2", ReplyAction = "AA2")]
        decimal AddAsync(object parent, string type, Dictionary<string, object> values, Хранилище stage, string user, string domain);


        [OperationContract(Action = "D", ReplyAction = "D")]
        void УдалитьРаздел(bool ВКорзину, bool УдалитьЗависимыеОбъекты, decimal[] id_node, Хранилище хранилище, string user, string domain);
        [OperationContract(Action = "DW", ReplyAction = "DW")]
        void УдалитьРазделБезПодструктуры(bool ВКорзину, decimal[] id_node, Хранилище хранилище, string user, string domain);

        [OperationContract(Action = "DC", ReplyAction = "DC")]
        void УдалитьПодразделы(bool ВКорзину, decimal[] id_node, Хранилище хранилище, string user, string domain);
        [OperationContract(Name = "УдалитьРаздел2", Action = "DQ", ReplyAction = "DQ")]
        void УдалитьРазделПоиск(bool ВКорзину, bool УдалитьЗависимыеОбъекты, Query запрос, Хранилище хранилище, string user, string domain);
        [OperationContract(Action = "RESTORE", ReplyAction = "RESTORE")]
        void ВосстановитьРаздел(object[] id_node, Хранилище хранилище, string user, string domain);


        [OperationContract(Action = "ETYPE", ReplyAction = "ETYPE")]
        void ИзменитьТипРаздела(object id_node, string НовыйТип, Хранилище хранилище, string domain);

        [OperationContract(Action = "COPY", ReplyAction = "COPY")]
        decimal КопироватьРаздел(URI КопироватьИз, URI КопироватьВ, string user, string domain);

        [OperationContract(Action = "MOVE", ReplyAction = "MOVE")]
        void ПереместитьРаздел(object id_node, object ПереместитьВРаздел, bool ОбновитьИндексы, Хранилище хранилище, string domain);
        #endregion

        #region Значения
        [OperationContract(Action = "GF2", ReplyAction = "GF2")]
        Dictionary<string, object> Get(object id, string[] keys, Хранилище stage, string domain);

        [OperationContract(Action = "GF", ReplyAction = "GF")]
        Dictionary<string, Value> ПолучитьЗначение(object id_node, string[] attributes, Хранилище хранилище, string domain);

        [OperationContract(Action = "G", ReplyAction = "G")]
        object ПолучитьЗначениеПростое(object id, string attribute, Хранилище stage, string domain);

        [OperationContract(Action = "FORM", ReplyAction = "FORM")]
        Dictionary<string, Value> ПолучитьЗначенияФормы(string Шаблон, decimal id_node, Хранилище хранилище, string domain);

        [OperationContract(Action = "CONST", ReplyAction = "CONST")]
        object ПолучитьКонстанту(string Имя, string domain);


        [OperationContract(Action = "SF2", ReplyAction = "SF2")]
        void Set(object id, Dictionary<string, object> values, Хранилище stage, string user, string domain);
        [OperationContract(Action = "SFA2", ReplyAction = "SFA2")]
        void SetAsync(object id, Dictionary<string, object> values, Хранилище stage, string user, string domain);

        [OperationContract(Action = "SF", ReplyAction = "SF")]
        void СохранитьЗначение(object id_node, Dictionary<string, Value> значения, bool ДобавитьВИсторию, Хранилище хранилище, string user, string domain);

        [OperationContract(Action = "S", ReplyAction = "S")]
        void СохранитьЗначениеПростое(object id, string attribute, object value, bool history, Хранилище stage, string user, string domain);

        [OperationContract(Action = "SQ", ReplyAction = "SQ")]
        void СохранитьЗначениеПоиск(Query запрос, Dictionary<string, Value> значения, bool ДобавитьВИсторию, Хранилище хранилище, string user, string domain);

        [OperationContract(Action = "UCO", ReplyAction = "UCO")]
        string UpdateCacheObject(Query q, string domain);

        [OperationContract(Action = "SCO", ReplyAction = "SCO")]
        string SaveCacheObjects();
        #endregion

        #region Асинхронно
        [OperationContract(IsOneWay = true, Action = "SQA")]
        void СохранитьЗначениеПоискАсинхронно(Query запрос, Dictionary<string, Value> значения, bool ДобавитьВИсторию, Хранилище хранилище, string user, string domain);
        [OperationContract(IsOneWay = true, Action = "SFA")]
        void СохранитьЗначениеАсинхронно(object id_node, Dictionary<string, Value> значения, bool ДобавитьВИсторию, Хранилище хранилище, string user, string domain);
        [OperationContract(Action = "AA", ReplyAction = "AA")]
        decimal ДобавитьРазделАсинхронно(object id_parent, string тип, Dictionary<string, Value> значения, bool ДобавитьВИсторию, Хранилище хранилище, string user, string domain);
        [OperationContract(IsOneWay = true, Action = "DA")]
        void УдалитьРазделАсинхронно(bool ВКорзину, bool УдалитьЗависимыеОбъекты, decimal[] id_node, Хранилище хранилище, string user, string domain);
        [OperationContract(IsOneWay = true, Action = "DQA")]
        void УдалитьРазделПоискАсинхронно(bool ВКорзину, bool УдалитьЗависимыеОбъекты, Query запрос, Хранилище хранилище, string user, string domain);
        [OperationContract(IsOneWay = true, Action = "DCA")]
        void УдалитьПодразделыАсинхронно(bool ВКорзину, decimal[] id_node, Хранилище хранилище, string user, string domain);
        #endregion

        #region Файлы
        [OperationContract(Action = "FILES", ReplyAction = "FILES")]
        IEnumerable<ФайлИнформация> СписокФайлов(decimal id_node, Хранилище хранилище, string domain);

        [OperationContract(Action = "FCOUNT", ReplyAction = "FCOUNT")]
        int КоличествоФайлов(decimal id_node, Хранилище хранилище, string domain);
        #endregion

        #region Поиск
        [OperationContract(Action = "Q", ReplyAction = "Q")]
        TableValue Поиск(Query запрос, Хранилище хранилище, string domain);

        [OperationContract(Action = "QU", ReplyAction = "QU")]
        decimal ПоискРазделаПоИдентификаторуОбъекта(string ИдентификаторОбъекта, Хранилище хранилище, string domain);

        [OperationContract(Action = "QG", ReplyAction = "QG")]
        decimal ПоискРазделаПоКлючу(string HashCode, Хранилище хранилище, string domain);


        [OperationContract(Action = "QH", ReplyAction = "QH")]
        TableValue ПоискИстории(Query запрос, Хранилище хранилище, string domain);

        [OperationContract(Action = "GH", ReplyAction = "GH")]
        TableValue ПолучитьИсторию(decimal id_node, Хранилище хранилище, string domain);
        #endregion

        #region Сервисные
        [OperationContract(Action = "INDEXED", ReplyAction = "INDEXED")]
        void Проиндексировать(decimal id_node, Хранилище хранилище, string domain, bool async);

        [OperationContract(IsOneWay = true, Action = "MSG")]
        void ОтправитьПисьмо(string Кому, string Тема, string Содержание, Файл[] СписокФайлов, bool IsBodyHtml, string user, string domain);
        #endregion

        #region Кеш
        [OperationContract(Action = "GK", ReplyAction = "GK")]
        object ПолучитьКешЗначение(string key, string domain);
        [OperationContract(IsOneWay = true, Action = "SK")]
        void СохранитьКешЗначение(string key, object value, int timeout, string domain);

        [OperationContract(Action = "UV", ReplyAction = "UV")]
        void ОбновитьЗначениеВКеше(string ИмяКеша, decimal[] id_nodes, Dictionary<string, object> values, string user, string domain);
        #endregion
    }
}

namespace RosService.Data
{
    public enum Хранилище
    {
        Оперативное = 0,
        Конфигурация = 1,
        //Корзина = 2,
        //Статистика = 3
    }
    public enum ФорматОтчета
    {
        ПоУмолчанию,
        Xaml,
        Xps
    }
    public enum MimeType
    {
        НеОпределен,
        Изображение,
        Word,
        Excel,
        Xps,
        Text
    }

    public class NodeInfo
    {
        public decimal id_node;
        public decimal id_parent;
        public string ТипДанных;

        public string Описание;
        public bool IsHidden;
        public bool IsNew;
        public string HashCode;
        public int Children;
        public Dictionary<string, object> Data;
    };
    public class DependencyNodeInfo
    {
        public decimal id_node;
        public string Тип;
        public string НазваниеОбъекта;
        public string Атрибут;
        public string Группа;
    };

    [DataContract]
    [Serializable]
    public class Value
    {
        //[XmlIgnore]
        //public string Id { get; set; }

        [DataMember]
        public bool IsСписок { get; set; }
        [DataMember]
        public object Значение { get; set; }
        [DataMember]
        public byte[] buffer { get; set; }

        [XmlIgnore]
        public DataTable Таблица
        {
            get
            {
                if (buffer == null || buffer.Length == 0)
                    return new DataTable("tblValue");

                return RosService.ServiceModel.DataSerializer.DeserializeTable(new MemoryStream(buffer));
            }
        }
        public void SetTable(DataTable value)
        {
            if (value == null) {
                buffer = null;
            }
            else {
                buffer = RosService.ServiceModel.DataSerializer.SerializeDataTable(value);
            }
        }

        public Value()
        {
        }
        public Value(object Значение)
        {
            this.Значение = Значение;
        }
    };
    [DataContract]
    [Serializable]
    public class TableValue
    {
        [XmlIgnore]
        public DataTable Значение
        {
            get
            {
                if (buffer == null || buffer.Length == 0)
                    return new DataTable("tblValue");

                return RosService.ServiceModel.DataSerializer.DeserializeTable(new MemoryStream(buffer));
            }
        }
        public void SetTable(DataTable value)
        {
            if (value == null) {
                buffer = null;
            }
            else {
                buffer = RosService.ServiceModel.DataSerializer.SerializeDataTable(value);
            }
        }
        [DataMember]
        public byte[] buffer;
        [DataMember]
        public int Count;
        [DataMember]
        public int Page;
        [DataMember]
        public int PageCount;
        [DataMember]
        public long ВремяПодготовкиДанных;
    };
    
    [DataContract]
    [Serializable]
    public class CustomValue
    {
        [DataMember]
        public object Value { get; set; }
        [DataMember]
        public string HashCode { get; set; }
        [DataMember]
        public MemberTypes MemberType { get; set; }

        public CustomValue()
        {
        }
        public CustomValue(MemberTypes MemberType, string HashCode, object Value)
        {
            this.HashCode = HashCode;
            this.Value = Value;
            this.MemberType = MemberType;
        }
    }
    
    [DataContract]
    [Serializable]
    public class Query
    {
        [DataContract]
        public enum ФорматДаты
        {
            [EnumMember]
            ПоУмолчанию,
            [EnumMember]
            День,
            [EnumMember]
            Неделя,
            [EnumMember]
            Месяц,
            [EnumMember]
            Квартал,
            [EnumMember]
            Год
        };
        [DataContract]
        public enum ФункцияАгрегации
        {
            [EnumMember]
            None = 0,
            [EnumMember]
            Sum,
            [EnumMember]
            Count,
            [EnumMember]
            Max,
            [EnumMember]
            Min,
            [EnumMember]
            Avg,
            [EnumMember]
            Функция,
            [EnumMember]
            Sql,
            [EnumMember]
            Distinct
        };
        [DataContract]
        public enum Оператор
        {
            [EnumMember]
            Равно = 0,
            [EnumMember]
            Соодержит,
            [EnumMember]
            НеРавно,
            [EnumMember]
            Больше,
            [EnumMember]
            БольшеРавно,
            [EnumMember]
            Меньше,
            [EnumMember]
            МеньшеРавно,
            [EnumMember]
            Функция,
            [EnumMember]
            СоодержитСлева,
            [EnumMember]
            СоодержитСправа,
            [EnumMember]
            Sql,
            [EnumMember]
            ТочноРавно
            //РавноИлиПусто
        }
        [DataContract]
        public enum НаправлениеСортировки
        {
            [EnumMember]
            Asc = 0,
            [EnumMember]
            Desc = 1,
            [EnumMember]
            Rand = 2,
            [EnumMember]
            Sql = 3
        }
        [DataContract]
        public enum OutputCacheLocation
        {
            [EnumMember]
            Server,
            [EnumMember]
            Memory
        }
        [DataContract]
        public class УсловиеПоиска
        {
            [DataMember]
            public string Атрибут { get; set; }
            [DataMember]
            public object Значение { get; set; }
            [DataMember]
            public Оператор Оператор { get; set; }
            [DataMember]
            public bool УчитыватьВремя { get; set; }

            //если написать @РодительскийРаздел будет ошибка тк распознает как параметр
            internal bool ИгрнорироватьПараметр { get; set; }
        }
        [DataContract]
        public class МестоПоиска
        {
            [DataMember]
            public object id_node { get; set; }
            [DataMember]
            public int МаксимальнаяГлубина { get; set; }
        }
        [DataContract]
        public class Сортировка
        {
            [DataMember]
            public string Атрибут { get; set; }
            [DataMember]
            public НаправлениеСортировки Направление { get; set; }
        }
        [DataContract]
        public class Колонка
        {
            [DataMember]
            public string Атрибут { get; set; }
            [DataMember]
            public ФункцияАгрегации Функция { get; set; }
            [DataMember]
            public MemberTypes Тип { get; set; }
            [DataMember]
            public bool? ПолнотекстовыйВывод { get; set; }

            //ограничить при объединениях
            internal object id_node { get; set; }
        };
        [DataContract]
        public class Параметр
        {
            [DataMember]
            public string Имя { get; set; }
            [DataMember]
            public object Значение { get; set; }
        }
        [DataContract]
        public class Объединение
        {
            [DataMember]
            public string Атрибут { get; set; }
            [DataMember]
            public МестоПоиска МестоПоиска { get; set; }
        };

        [DataMember]
        public List<string> Типы = new List<string>();
        [DataMember]
        public List<УсловиеПоиска> УсловияПоиска = new List<УсловиеПоиска>();
        [DataMember]
        public List<МестоПоиска> МестаПоиска = new List<МестоПоиска>();
        [DataMember]
        public List<Колонка> ВыводимыеКолонки = new List<Колонка>();
        [DataMember]
        public List<Сортировка> Сортировки = new List<Сортировка>();
        [DataMember]
        public List<Параметр> Параметры = new List<Параметр>();
        [DataMember]
        public List<string> Группировки = new List<string>();
        [DataMember]
        public List<Объединение> Объединения = new List<Объединение>();


        [DataMember]
        public int КоличествоВыводимыхДанных = 0;
        [DataMember]
        public int КоличествоВыводимыхСтраниц = 0;
        [DataMember]
        public int Страница = 0;
        [DataMember]
        public string Sql = string.Empty;
        [DataMember]
        public string СтрокаЗапрос = string.Empty;
        //выводит запрос
        [DataMember]
        public bool IsDebug;
        //конвертировать даты
        [DataMember]
        public ФорматДаты ФорматДат;

        [DataMember]
        public TimeSpan CacheDuration { get; set; }
        [DataMember]
        public OutputCacheLocation CacheLocation { get; set; }
        [DataMember]
        public string CacheName { get; set; }
        [IgnoreDataMember]
        public bool CacheReadOnly { get; set; }
        [IgnoreDataMember]
        internal bool IsCache
        {
            get
            {
                return !string.IsNullOrEmpty(CacheName);
            }
        }

        [DataMember]
        public bool ВКорзине { get; set; }

        [IgnoreDataMember]
        internal string Файл { get; set; }
        [IgnoreDataMember]
        internal string Атрибут { get; set; }

        public Query()
        {
        }
        public Query(string запрос)
        {
            this.СтрокаЗапрос = запрос;
        }
        public Query(int КоличествоВыводимыхДанных)
        {
            this.КоличествоВыводимыхДанных = КоличествоВыводимыхДанных;
        }
        public Query(int КоличествоВыводимыхДанных, int КоличествоВыводимыхСтраниц)
        {
            this.КоличествоВыводимыхДанных = КоличествоВыводимыхДанных;
            this.КоличествоВыводимыхСтраниц = КоличествоВыводимыхСтраниц;
        }

        #region Добавить
        public void ДобавитьВыводимыеКолонки(IEnumerable<string> items)
        {
            ВыводимыеКолонки.AddRange(items.Select(p => new Колонка() { Атрибут = p }));
        }
        public void ДобавитьПараметр(string Имя, object Значение)
        {
            this.Параметры.Add(new Параметр()
            {
                Имя = Имя,
                Значение = Значение
            });
        }

        public void ДобавитьСортировку(string Атрибут)
        {
            ДобавитьСортировку(Атрибут, НаправлениеСортировки.Asc);
        }
        public void ДобавитьСортировку(string Атрибут, НаправлениеСортировки Направление)
        {
            Сортировки.Add(new Сортировка()
            {
                Атрибут = Атрибут,
                Направление = Направление
            });
        }

        public void ДобавитьМестоПоиска(decimal id_node, int МаксимальнаяГлубина)
        {
            this.МестаПоиска.Add(new МестоПоиска()
            {
                id_node = id_node,
                МаксимальнаяГлубина = МаксимальнаяГлубина
            });
        }
        public void ДобавитьМестоПоиска(object Имя, int МаксимальнаяГлубина)
        {
            this.МестаПоиска.Add(new МестоПоиска()
            {
                id_node = Имя,
                МаксимальнаяГлубина = МаксимальнаяГлубина
            });
        }

        public Колонка ДобавитьВыводимыеКолонки(string Атрибут)
        {
            var column = new Колонка() { Атрибут = Атрибут };
            this.ВыводимыеКолонки.Add(column);
            return column;
        }
        public void ДобавитьВыводимыеКолонки(params string[] Колонки)
        {
            this.ВыводимыеКолонки.AddRange(Колонки.Select(p => new Колонка() { Атрибут = p }));
        }
        public void ДобавитьВыводимыеКолонки(string Атрибут, Query.ФункцияАгрегации Функция)
        {
            this.ВыводимыеКолонки.AddRange(new Колонка[] { new Колонка() { Атрибут = Атрибут, Функция = Функция } });
        }
        public void ДобавитьВычисляемыеКолонки(params string[] Колонки)
        {
            this.ВыводимыеКолонки.AddRange(Колонки.Select(p => new Колонка() { Атрибут = p, Функция = ФункцияАгрегации.Sql /*IsВычисляемое = true*/ }));
        }

        public УсловиеПоиска ДобавитьУсловиеПоиска(string Атрибут, object Значение)
        {
            return ДобавитьУсловиеПоиска(Атрибут, Значение, Оператор.Равно);
        }
        public УсловиеПоиска ДобавитьУсловиеПоиска(string Атрибут, object Значение, Оператор Оператор)
        {
            var items = this.УсловияПоиска.ToList();
            var item = new УсловиеПоиска()
            {
                Атрибут = Атрибут,
                Значение = Значение,
                Оператор = Оператор
            };
            this.УсловияПоиска.Add(item);
            return item;
        }
        public УсловиеПоиска ДобавитьУсловиеПоиска(string Значение)
        {
            var items = this.УсловияПоиска.ToList();
            var item = new УсловиеПоиска()
            {
                Атрибут = string.Empty,
                Значение = Значение,
                Оператор = Оператор.Функция
            };
            this.УсловияПоиска.Add(item);
            return item;
        }

        public void ДобавитьТипы(params string[] Имя)
        {
            this.Типы.AddRange(Имя);
        }
        public void ДобавитьГруппировки(params string[] Группировки)
        {
            this.Группировки.AddRange(Группировки);
        }
        #endregion

        internal bool IsAggregate()
        {
            if(Группировки.Count > 0)
                return true;

            if(ВыводимыеКолонки.Where(p => p.Функция != Query.ФункцияАгрегации.None && p.Функция != Query.ФункцияАгрегации.Sql).Count() > 0)
                return true;

            return false;
        }
        internal void Parse(string domain)
        {
            if (!string.IsNullOrEmpty(СтрокаЗапрос))
            {
                if (СтрокаЗапрос.Contains("<Запрос>"))
                {
                    var xml = new XmlDocument();
                    xml.LoadXml(ParseSql(СтрокаЗапрос, domain));
                    var xmlNode = xml.SelectSingleNode("//Sql");
                    if (xmlNode != null)
                    {
                        Sql = xmlNode.InnerText;
                    }
                }
                else if (СтрокаЗапрос.StartsWith("http://") || СтрокаЗапрос.StartsWith("https://"))
                {
                    //загрузка xml
                }
                else if (СтрокаЗапрос.StartsWith("["))
                {
                    #region parse
                    var matchs = Regex.Matches(СтрокаЗапрос.Trim("[]".ToArray()), @"(?<FUNCTION>([\w]+?))=(?<PARAM>([^;]+))[;]?", RegexOptions.Singleline | RegexOptions.Compiled);
                    foreach (Match item in matchs)
                    {
                        switch (item.Groups["FUNCTION"].Value.Trim())
                        {
                            case "Типы":
                                {
                                    var _Типы = item.Groups["PARAM"].Value;
                                    //удалить первые скобочки
                                    if (_Типы.StartsWith(@"(") && _Типы.EndsWith(")"))
                                        _Типы = _Типы.Substring(1, _Типы.Length - 2);

                                    Типы.AddRange(_Типы.Split(',').Select(p => p.Trim()));
                                }
                                break;

                            case "ВыводимыеКолонки":
                            case "Колонки":
                                {
                                    var _ВыводимыеКолонки = item.Groups["PARAM"].Value;
                                    //удалить первые скобочки
                                    if (_ВыводимыеКолонки.StartsWith(@"(") && _ВыводимыеКолонки.EndsWith(")"))
                                        _ВыводимыеКолонки = _ВыводимыеКолонки.Substring(1, _ВыводимыеКолонки.Length - 2);

                                    foreach (var i in _ВыводимыеКолонки.Split(','))
                                    {
                                        var match = Regex.Match(i, @"\(?(?<FUNCTION>(\w+))?\((?<PARAM>(.+))\)|(?<PARAM>(.+))\)?");
                                        var _FUNCTION = match.Groups["FUNCTION"].Value.Trim();
                                        var aggr = string.IsNullOrEmpty(_FUNCTION)
                                                   ? ФункцияАгрегации.None
                                                   : (ФункцияАгрегации)Enum.Parse(typeof(ФункцияАгрегации), _FUNCTION, true);

                                        ВыводимыеКолонки.Add(new Колонка()
                                        {
                                            Атрибут = match.Groups["PARAM"].Value.Trim(),
                                            Функция = aggr
                                        });
                                    }
                                }
                                break;

                            case "МестаПоиска":
                            case "Места":
                                {
                                    foreach (Match i in Regex.Matches(item.Groups["PARAM"].Value, @"\((?<Attr1>(.+?))\,(?<Attr2>(.+?))\)"))
                                    {
                                        try
                                        {
                                            МестаПоиска.Add(new Query.МестоПоиска()
                                            {
                                                id_node = Convert.ToDecimal(i.Groups["Attr1"].Value),
                                                МаксимальнаяГлубина = Convert.ToInt32(i.Groups["Attr2"].Value)
                                            });
                                        }
                                        catch
                                        {
                                            МестаПоиска.Add(new Query.МестоПоиска()
                                            {
                                                id_node = i.Groups["Attr1"].Value,
                                                МаксимальнаяГлубина = Convert.ToInt32(i.Groups["Attr2"].Value)
                                            });
                                        }
                                    }
                                }
                                break;

                            case "УсловияПоиска":
                            case "Условия":
                                {
                                    foreach (Match i in Regex.Matches(item.Groups["PARAM"].Value, @"\((?<Attr1>(.+?))\,(\""(?<Attr2>(.+?))\""|'(?<Attr2>(.+?))'|(?<Attr2>(.+?)))\,(?<Attr3>(.+?))\)"))
                                    {
                                        var cond = new Query.УсловиеПоиска()
                                        {
                                            Атрибут = i.Groups["Attr1"].Value,
                                            Значение = i.Groups["Attr2"].Value,
                                            Оператор = (Query.Оператор)Enum.Parse(typeof(Query.Оператор), i.Groups["Attr3"].Value)
                                        };

                                        if (cond.Атрибут != null && cond.Атрибут.EndsWith("%"))
                                        {
                                            cond.УчитыватьВремя = true;
                                            cond.Атрибут = cond.Атрибут.Substring(0, cond.Атрибут.Length - 1);
                                        }

                                        УсловияПоиска.Add(cond);
                                    }
                                }
                                break;

                            case "Сортировки":
                                {
                                    foreach (Match i in Regex.Matches(item.Groups["PARAM"].Value, @"\((?<Attr1>(.+?))\,(?<Attr2>(.+?))\)"))
                                    {
                                        Сортировки.Add(new Query.Сортировка()
                                        {
                                            Атрибут = i.Groups["Attr1"].Value,
                                            Направление = (Query.НаправлениеСортировки)Enum.Parse(typeof(Query.НаправлениеСортировки), i.Groups["Attr2"].Value)
                                        });
                                    }
                                }
                                break;

                            case "КоличествоВыводимыхДанных":
                            case "Количество":
                                {
                                    КоличествоВыводимыхДанных = Convert.ToInt32(item.Groups["PARAM"].Value);
                                }
                                break;

                            case "КоличествоВыводимыхСтраниц":
                            case "КоличествоСтраниц":
                            case "Страниц":
                                {
                                    КоличествоВыводимыхСтраниц = Convert.ToInt32(item.Groups["PARAM"].Value);
                                }
                                break;

                            case "Группировки":
                                {
                                    Группировки.AddRange(item.Groups["PARAM"].Value.Trim("()".ToCharArray()).Split(','));
                                }
                                break;

                            case "Объединения":
                                {
                                    foreach (Match i in Regex.Matches(item.Groups["PARAM"].Value, @"\((?<Attr1>(.+?))\,(?<Attr2>(.+?))\)"))
                                    {
                                        Объединения.Add(new Query.Объединение()
                                        {
                                            Атрибут = i.Groups["Attr1"].Value,
                                            МестоПоиска = new МестоПоиска() { id_node = i.Groups["Attr2"].Value, МаксимальнаяГлубина = 0 }
                                        });
                                    }
                                }
                                break;

                            case "ВКорзине":
                                {
                                    ВКорзине = item.Groups["PARAM"].Value == "1";
                                }
                                break;

                            case "CacheName":
                                {
                                    CacheName = item.Groups["PARAM"].Value;
                                }
                                break;

                            case "Файл":
                                {
                                    Файл = item.Groups["PARAM"].Value;
                                }
                                break;


                            case "Атрибут":
                                {
                                    Атрибут = item.Groups["PARAM"].Value;
                                }
                                break;

                            case "ФорматДат":
                                {
                                    ФорматДат = (ФорматДаты)Enum.Parse(typeof(ФорматДаты), item.Groups["PARAM"].Value);
                                }
                                break;

                            default:
                                throw new Exception(string.Format("Не известный параметр {0}", item.Groups["FUNCTION"].Value));
                        }
                    }
                    #endregion
                }
                else
                {
                    Sql = СтрокаЗапрос;
                }
            }

            //проверить все ли параметры в запросе есть
            if (УсловияПоиска != null)
            {
                foreach (var item in УсловияПоиска.Where(p => !p.ИгрнорироватьПараметр))
                {
                    if (Convert.ToString(item.Значение).StartsWith("@"))
                    {
                        var param = Параметры.SingleOrDefault(p => p.Имя == (string)item.Значение);
                        if (param == null) throw new Exception(string.Format("Параметр '{0}' не задан.", item.Значение));
                    }
                }

                //проверить если ли условия поиска по всем атрибутам
                var itemFull = УсловияПоиска.FirstOrDefault(p => p.Атрибут.Equals("*"));
                if (itemFull != null && !(itemFull.Значение == null ||
                    itemFull.Значение.Equals(string.Empty) ||
                    itemFull.Значение.Equals("%%") || itemFull.Значение.Equals("%")))
                {
                    IsПоискПоСодержимому = true;

                    //проверить если указано как параметр
                    if (Convert.ToString(itemFull.Значение).StartsWith("@") &&
                        Параметры != null &&
                        Параметры.FirstOrDefault(p => p.Имя.Equals(itemFull.Значение) &&
                        string.IsNullOrEmpty(p.Значение as string)
                        ) != null)
                    {
                        IsПоискПоСодержимому = false;
                    }
                }
            }
            if (МестаПоиска != null)
            {
                foreach (var item in МестаПоиска)
                {
                    if (Convert.ToString(item.id_node).StartsWith("@"))
                    {
                        var param = Параметры.SingleOrDefault(p => p.Имя == (string)item.id_node);
                        if (param == null) throw new Exception(string.Format("Параметр '{0}' не задан.", item.id_node));
                    }
                }
            }
            if (!string.IsNullOrEmpty(CacheName) && CacheName.Length >= 2 && CacheName.First() == '{' && CacheName.Last() == '}')
            {
                CacheReadOnly = true;
                CacheName = CacheName.Substring(1, CacheName.Length - 2);
            }

            #region Подставить параметры, если они есть
            if (Параметры != null && Параметры.Count > 0)
            {
                foreach (var item in УсловияПоиска.Where(p => !p.ИгрнорироватьПараметр &&
                    p.Значение is string && ((string)p.Значение).StartsWith("@")))
                {
                    item.Значение = Параметры.SingleOrDefault(p => p.Имя == (string)item.Значение).Значение;
                }
            }
            #endregion
        }
        internal string ParseSql(string sql, string domain)
        {
            var parts = Regex.Matches(sql, @"\<(?<Tag>type|id)(?<AttrSuffix>_id)?:(?<AttrType>(.+?))\>", RegexOptions.IgnoreCase).Cast<Match>()
                        .Select(p => new { Part = p.Value, Tag = p.Groups["Tag"].Value.ToLower(), Type = p.Groups["AttrType"].Value, Suffux = p.Groups["AttrSuffix"].Value });

            foreach (var item in parts)
            {
                if (item.Type.EndsWith("%"))
                {
                    switch (item.Tag)
                    {
                        case "type":
                        case "id":
                            sql = sql.Replace(item.Part, string.Join(",", new ConfigurationClient().СписокНаследуемыхТипов(item.Type.TrimEnd(new char[] { '%' }), true, domain).Select(p => "'" + p.Name + "'").ToArray()));
                            break;
                    }
                }
                else
                {
                    switch (item.Tag)
                    {
                        case "type":
                            sql = sql.Replace(item.Part, item.Type);
                            break;

                        case "id":
                            sql = sql.Replace(item.Part, "'" + item.Type + "'");
                            break;
                    }
                }
            }
            return sql;
        }
        internal static Query Deserialize(string xml)
        {
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(Query));
            return (Query)serializer.Deserialize(new StringReader(xml));
        }
        internal static string Serialize(Query запрос)
        {
            var sb = new StringBuilder();
            var settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.OmitXmlDeclaration = true;

            var ser = new System.Runtime.Serialization.DataContractSerializer(запрос.GetType());
            using (var xml = XmlWriter.Create(sb, settings))
            {
                ser.WriteObject(xml, запрос);
                xml.Flush();
                return sb.ToString();
            }
        }

        internal bool ИгнорироватьСтраницы;
        internal bool IsПоискПоСодержимому;
        internal bool IsSql
        {
            get
            {
                return !string.IsNullOrEmpty(Sql);
            }
        }
        internal bool IsEmpty
        {
            get
            {
                return (Типы == null || Типы.Count == 0) 
                    && (УсловияПоиска == null || УсловияПоиска.Count == 0) 
                    && (МестаПоиска == null || МестаПоиска.Count == 0) 
                    && (ВыводимыеКолонки == null || ВыводимыеКолонки.Count == 0) 
                    && (Сортировки == null || Сортировки.Count == 0) 
                    && string.IsNullOrEmpty(Sql) && string.IsNullOrEmpty(СтрокаЗапрос);
            }
        }
        internal string GetHash(Хранилище Хранилище)
        {
            var sb = new StringBuilder(4096);
            sb.Append(Хранилище.ToString());
            sb.Append(this.IsПоискПоСодержимому.ToString());
            sb.Append(this.ФорматДат.ToString());
            sb.Append(this.Sql);
            sb.Append(string.Join("", this.ВыводимыеКолонки.Select(p => p.Атрибут + p.Функция.ToString() + 
                (p.ПолнотекстовыйВывод != null && p.ПолнотекстовыйВывод.Value ? "1" : "")).ToArray()));
            sb.Append(string.Join("", this.Группировки.ToArray()));
            sb.Append(string.Join("", this.Типы.ToArray()));

            foreach (var item in this.МестаПоиска)
            {
                if (item.id_node == null)
                {
                    continue;
                }
                else if (item.id_node is string)
                {
                    if (((string)item.id_node).StartsWith("@"))
                    {
                        var param = Параметры.FirstOrDefault(p => p.Имя.Equals(item.id_node));
                        if (param != null && (param.Значение == null || (0m).Equals(Convert.ToDecimal(param.Значение))))
                            continue;
                    }
                }
                else if (Convert.ToDecimal(item.id_node) == 0m)
                {
                    continue;
                }
                sb.Append(item.МаксимальнаяГлубина);
            }
            sb.Append(string.Join("", this.Параметры.Select(p => p.Имя).ToArray()));
            sb.Append(string.Join("", this.Сортировки.Select(p => p.Атрибут + p.Направление.ToString()).ToArray()));
            sb.Append(string.Join("", this.УсловияПоиска.Select(p => p.Атрибут + p.Оператор.ToString() + p.УчитыватьВремя.ToString()
                + (p.Оператор == Оператор.Функция || p.Оператор == Оператор.Sql ? Convert.ToString(p.Значение) : string.Empty)).ToArray()));
            //sb.Append(string.Join("", this.Объединения.Select(p => p.Атрибут /*+ p.МестоПоиска.id_node.ToString() + p.МестоПоиска.МаксимальнаяГлубина.ToString()*/).ToArray()));
            sb.Append(КоличествоВыводимыхСтраниц == 0 || КоличествоВыводимыхСтраниц == int.MaxValue ? "P0" : "P1");
            if (!string.IsNullOrEmpty(CacheName))
                sb.Append(КоличествоВыводимыхДанных == 0 || КоличествоВыводимыхДанных == int.MaxValue ? "S0" : "S1");
            return getMd5Hash(sb.ToString(), true);
        }
        internal string GetPageHash(string _baseHash)
        {
            var sb = new StringBuilder(4096);
            sb.Append(_baseHash);
            if (УсловияПоиска != null && УсловияПоиска.Count > 0)
            {
                foreach (var item in УсловияПоиска.Select(p => (p.Значение ?? "").ToString()))
                    sb.Append(item);
            }
            if (МестаПоиска != null && МестаПоиска.Count > 0)
            {
                foreach (var item in МестаПоиска.Select(p => (p.id_node ?? "").ToString()))
                    sb.Append(item);
            }
            return getMd5Hash(sb.ToString(), false);
        }
        internal string getMd5Hash(string input, bool appendType)
        {
            // Create a new instance of the MD5CryptoServiceProvider object.
            System.Security.Cryptography.MD5 md5Hasher = System.Security.Cryptography.MD5.Create();

            // Convert the input string to a byte array and compute the hash.
            var data = md5Hasher.ComputeHash(System.Text.Encoding.Default.GetBytes(input));

            // Create a new Stringbuilder to collect the bytes
            // and create a string.
            var sBuilder = new System.Text.StringBuilder();
            if (appendType && Типы != null && Типы.Count > 0)
            {
                foreach (var item in Типы)
                {
                    sBuilder.Append(item + "--");
                }
            }

            // Loop through each byte of the hashed data 
            // and format each one as a hexadecimal string.
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            // Return the hexadecimal string.
            return sBuilder.ToString();
        }
    };
    [DataContract]
    [Serializable]
    public class URI
    {
        [DataMember]
        public string domain;
        [DataMember]
        public Хранилище хранилище;
        [DataMember]
        public object node;
    }

    public enum ФайловоеХранилище
    {
        Оперативное,
        Архив,
        Просмотр,
        Корзина,
        Почта
    };
    public class Файл
    {
        public string Name;
        public MimeType MimeType;
        public byte[] Stream;
    }
    public class ФайлИнформация
    {
        public decimal id_node;
        public string Имя;
        public double Размер;
        public MimeType MimeType;
        public DateTime ДатаСоздания;
        public string Описание;
        public string ИдентификаторФайла;
        public string Создатель;
        public string ПолноеИмяФайла;
    }

    [Flags]
    public enum ПраваПользователя
    {
        Пусто = 0,
        ДобавлениеРазделов = 2,
        УдалениеРазделов = 4,
        РедактированиеРазделов = 8,
        ПоказатьВсеДерево = 16,
        ПоказатьОбъектыСозданныеПользователем = 32,
        ПоказатьОбъектыПодструктуры = 64,
        УправлениеПользователями = 128,
        ПоказатьОбъектыПоАтрибуту = 256,

        СкрытьРекламу = 512,
        ЗапретитьРаботуСПочтой = 1024,
        ЗапретитьРаботуСЗадачами = 2048,
        ЗапретитьПоиск = 4096,
        ЗапретитьРасширенныйПоиск = 8192,
        //ЗапретитьУдаленныеПодключения = 16384,
        //ПрявязатьIp = 32768
    };
    public class Пользователь
    {
        public decimal id_node { get; set; }
        public string Логин { get; set; }
        public string Интерфейс { get; set; }
        public string Группа { get; set; }
        public string Тип { get; set; }
        public decimal ГруппаРаздел { get; set; }
        public ПраваПользователя Права { get; set; }
        public string[] Роли { get; set; }
        public decimal МестоПоиска { get; set; }
        public string ПоисковыйАтрибут { get; set; }
    };
}


