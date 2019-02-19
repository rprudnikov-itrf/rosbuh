using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RosService.Intreface;
using System.IO;
using System.Xml;
using System.Runtime.Serialization;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Runtime.Serialization.Formatters;
using RosService.Data;
using RosService.Configuration;
using RosService.Helper;
using System.Threading.Tasks;
using System.Threading;
using System.Data;
using System.Data.SqlClient;
using System.Xml.Linq;

namespace RosService.Caching
{
    public class Cache
    {
        protected static readonly ConcurrentDictionary<string, КешЭлемент> Items;
        protected static readonly ConcurrentDictionary<string, КешИдентификаторРаздела> ItemsResolve;

        #region config
        private static readonly object lockCacheXml = new System.Object();
        public static void Сохранить(string directory, bool ОставитьПредыдущююВерсию, bool async)
        {
            //не сохранять если используется внешний кеш
            //if (MemoryCache.IsMemoryCacheClient)
            //    return;
            var task = System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                lock (lockCacheXml)
                {
                    try
                    {
                        var sb = new StringBuilder();
                        var settings = new XmlWriterSettings();
                        settings.Indent = true;
                        settings.OmitXmlDeclaration = true;

                        if (!System.IO.Directory.Exists(directory))
                            System.IO.Directory.CreateDirectory(directory);

                        var last_directory = Path.Combine(directory, string.Format("{0:yyyy.MM.dd__hh.mm.ss}", DateTime.Now));
                        if (ОставитьПредыдущююВерсию && !System.IO.Directory.Exists(last_directory))
                            System.IO.Directory.CreateDirectory(last_directory);
                        
                        var ser = new System.Runtime.Serialization.DataContractSerializer(typeof(Dictionary<string, КешЭлемент>), КешЭлемент.KnownTypeList());
                        foreach (var item in Items.AsParallel().GroupBy(p => Regex.Match(p.Key, @"(.+?):").Groups[1].Value))
                        {
                            try
                            {
                                sb = new StringBuilder();
                                using (var xml = XmlWriter.Create(sb, settings))
                                {
                                    ser.WriteObject(xml, item.ToDictionary(p => p.Key, e => e.Value));
                                    xml.Flush();

                                    var file = System.IO.Path.Combine(directory, item.Key + ".cache");
                                    if (ОставитьПредыдущююВерсию && System.IO.File.Exists(file))
                                    {
                                        System.IO.File.Move(file, System.IO.Path.Combine(last_directory, item.Key + ".cache"));
                                    }
                                    System.IO.File.WriteAllText(file, sb.ToString());
                                    System.Threading.Thread.Sleep(100);
                                }
                            }
                            catch (Exception ex)
                            {
                                ConfigurationClient.WindowsLog(ex.ToString(), "", item.Key, "Cache.Сохранить");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ConfigurationClient.WindowsLog(ex.ToString(), "", "system", "Cache.Сохранить");
                    }
                }
            });

            if (!async)
            {
                task.Wait();
            }
        }
        public static void Загрузить(string directory)
        {
            //не загружать если используется внешний кеш
            //if (MemoryCache.IsMemoryCacheClient)
            //    return;

            if (!System.IO.Directory.Exists(directory))
                return;

            lock (lockCacheXml)
            {
                Items.Clear();
                var ser = new System.Runtime.Serialization.DataContractSerializer(typeof(Dictionary<string, КешЭлемент>), КешЭлемент.KnownTypeList());
                foreach (var file in System.IO.Directory.GetFiles(directory, "*.cache"))
                {
                    if (new FileInfo(file).Length == 0)
                        continue;

                    //<KeyValueOfstringКешЭлементh9JraY4p>
                    //  <Key>5элемент:Z:КешИдентификаторРаздела:&amp;11d97d4f-2153-e211-bb73-0030487e046b</Key>
                    //  <Value xmlns:d3p1="http://schemas.datacontract.org/2004/07/RosService.Caching" i:type="d3p1:КешИдентификаторРаздела">
                    //    <d3p1:ДатаСоздания>2013-01-01T01:49:00.796875+04:00</d3p1:ДатаСоздания>
                    //    <d3p1:id_node>967854</d3p1:id_node>
                    //  </Value>
                    //</KeyValueOfstringКешЭлементh9JraY4p>

                    var fileXml = System.IO.File.ReadAllText(file);
                    //var xml = System.Xml.Linq.XDocument.Load(new StringReader(fileXml));
                    //foreach (var item in xml.Root.Elements().ToArray())
                    //{
                    //    if (item.Element(XName.Get("Key", "http://schemas.microsoft.com/2003/10/Serialization/Arrays")).Value.Contains("КешИдентификаторРаздела"))
                    //        item.Remove();
                    //}

                    //var xml = System.Text.RegularExpressions.Regex.Replace(fileXml, @"<KeyValueOfstringКешЭлемент[\a\d]>(.+?)</KeyValueOfstringКешЭлемент[\a\d]>", "", RegexOptions.Multiline);
                    var obj = ser.ReadObject(XmlReader.Create(new StringReader(fileXml))) as Dictionary<string, КешЭлемент>;
                    if (obj != null)
                    {
                        foreach (var item in obj)
                        {
                            if (item.Value is КешХешьТаблицаПамять) 
                                continue;
                            //else if (item.Value is КешСчётчик) continue;

                            Items.AddOrUpdate(item.Key, item.Value, (k, e) => e = item.Value);
                        }
                    }
                }
            }
        }
        #endregion

        #region Обслуживание архива значений
        //internal static readonly object lookHistoryValues = new System.Object();
        //internal static readonly ConcurrentQueue<HistoryValueContanier> HistoryValues = new ConcurrentQueue<HistoryValueContanier>();

        private static bool? _ОтключитьАрхивЗначений;
        public static bool ОтключитьАрхивЗначений
        {
            get
            {
                if (_ОтключитьАрхивЗначений == null)
                {
                    if (System.Configuration.ConfigurationManager.AppSettings["Хранилище.ОтключитьАрхивЗначений"] == null)
                        _ОтключитьАрхивЗначений = false;
                    else
                        _ОтключитьАрхивЗначений = Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings["Хранилище.ОтключитьАрхивЗначений"]);
                }
                return _ОтключитьАрхивЗначений.Value;
            }
        }

        private static bool? _ОтключитьКешированиеТаблиц;
        public static bool ОтключитьКешированиеТаблиц
        {
            get
            {
                if (_ОтключитьКешированиеТаблиц == null)
                {
                    if (System.Configuration.ConfigurationManager.AppSettings["Хранилище.ОтключитьКешированиеТаблиц"] == null)
                        _ОтключитьКешированиеТаблиц = false;
                    else
                        _ОтключитьКешированиеТаблиц = Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings["Хранилище.ОтключитьКешированиеТаблиц"]);
                }
                return _ОтключитьКешированиеТаблиц.Value;
            }
        }

        private static bool? _НеУдалятьСвязанныеОбъекты;
        public static bool НеУдалятьСвязанныеОбъекты
        {
            get
            {
                if (_НеУдалятьСвязанныеОбъекты == null)
                {
                    if (System.Configuration.ConfigurationManager.AppSettings["Хранилище.НеУдалятьСвязанныеОбъекты"] == null)
                        _НеУдалятьСвязанныеОбъекты = false;
                    else
                        _НеУдалятьСвязанныеОбъекты = Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings["Хранилище.НеУдалятьСвязанныеОбъекты"]);
                }
                return _НеУдалятьСвязанныеОбъекты.Value;
            }
        }

        //public static void SaveHistoryValues()
        //{
        //    lock (lookHistoryValues)
        //    {
        //        while (!HistoryValues.IsEmpty)
        //        {
        //            try
        //            {
        //                var item = null as HistoryValueContanier;
        //                if (!HistoryValues.TryDequeue(out item))
        //                    continue;

        //                using (var db = new RosService.DataClasses.ClientDataContext(item.domain, true))
        //                {
        //                    try
        //                    {
        //                        if (db.Connection.State != ConnectionState.Open)
        //                            db.Connection.Open();
        //                        var __command = (db.Connection as SqlConnection).CreateCommand();
        //                        var sql = new StringBuilder();
        //                        #region sql
        //                        switch (item.registerType)
        //                        {
        //                            #region double_value
        //                            case RegisterTypes.double_value:
        //                                sql.AppendLine("insert into tblHistoryValue ([date],[storage],[id_node],[type],[user],[double_value]) values (@date,@storage,@id_node,@HashCode,@user,@value);");
        //                                break;
        //                            #endregion

        //                            #region datetime_value
        //                            case RegisterTypes.datetime_value:
        //                                sql.AppendLine("insert into tblHistoryValue ([date],[storage],[id_node],[type],[user],[datetime_value]) values (@date,@storage,@id_node,@HashCode,@user,@value);");
        //                                break;
        //                            #endregion

        //                            #region byte_value
        //                            case RegisterTypes.byte_value:
        //                                sql.AppendLine("insert into tblHistoryValue ([date],[storage],[id_node],[type],[user],[byte_value]) values (@date,@storage,@id_node,@HashCode,@user,convert(varbinary(max),@value));");
        //                                break;
        //                            #endregion

        //                            #region string_value
        //                            case RegisterTypes.string_value:
        //                            default:
        //                                sql.AppendLine("insert into tblHistoryValue ([date],[storage],[id_node],[type],[user],[string_value]) values (@date,@storage,@id_node,@HashCode,@user,@value);");
        //                                break;
        //                            #endregion
        //                        }
        //                        #endregion
        //                        __command.CommandText = sql.ToString();
        //                        //__command.Parameters.AddWithValue("@value", item.value ?? Convert.DBNull);
        //                        RosService.QueryBuilder.AddWithValue(__command, "@value", item.value ?? Convert.DBNull);
        //                        __command.Parameters.AddWithValue("@HashCode", item.attribute).SqlDbType = SqlDbType.VarChar;

        //                        __command.Parameters.AddWithValue("@storage", (int)item.хранилище).SqlDbType = SqlDbType.Int;
        //                        __command.Parameters.AddWithValue("@date", item.date).SqlDbType = SqlDbType.DateTime;
        //                        __command.Parameters.AddWithValue("@user", item.user ?? string.Empty).SqlDbType = SqlDbType.VarChar;
        //                        __command.Parameters.AddWithValue("@id_node", item.id_node).SqlDbType = SqlDbType.Decimal;
        //                        __command.CommandTimeout = 300;
        //                        __command.ExecuteNonQuery();

        //                        Thread.Sleep(3);
        //                    }
        //                    finally
        //                    {
        //                        db.Connection.Close();
        //                    }
        //                }

        //            }
        //            catch (Exception ex)
        //            {
        //                ConfigurationClient.WindowsLog("SaveHistoryValues: " + ex.Message, string.Empty, "all");
        //            }
        //        }
        //    }
        //}
        #endregion

        static Cache()
        {
            Items = new ConcurrentDictionary<string, КешЭлемент>(Environment.ProcessorCount, 50000);
            ItemsResolve = new ConcurrentDictionary<string, КешИдентификаторРаздела>(Environment.ProcessorCount, 50000);

            //Загрузить(@"R:\cache");
        }

        #region Resolve
        public static void SetResolve(string key, КешИдентификаторРаздела value)
        {
            if (string.IsNullOrEmpty(key))
                return;

            ItemsResolve.AddOrUpdate(key, value, (k, e) => e = value);
        }
        public static КешИдентификаторРаздела GetResolve(string key)
        {
            try
            {
                var item = null as КешИдентификаторРаздела;
                if (!ItemsResolve.TryGetValue(key, out item))
                    return null;

                return item;
            }
            catch (Exception ex)
            {
                ConfigurationClient.WindowsLog(ex.ToString(), string.Empty, "system", "GetResolve<T>");
                return null;
            }
        }
        public static string KeyResolve(string domain, string value)
        {
            return domain + ":" + value ?? string.Empty;
        }

        public static Dictionary<string, КешИдентификаторРаздела> GetAllResolve(string domain)
        {
            var dic = new Dictionary<string, КешИдентификаторРаздела>();
            var key = domain + ":";
            foreach (var item in ItemsResolve.Where(p => p.Key != null && p.Key.StartsWith(key)).ToArray())
            {
                dic.Add(item.Key, item.Value);
            }
            return dic;
        }

        public static void RemoveResolve(string key)
        {
            var dump = null as КешИдентификаторРаздела;
            ItemsResolve.TryRemove(key, out dump);
        }

        public static void RemoveAllResolve(string domain)
        {
            try
            {
                foreach (var item in GetAllResolve(domain))
                    RemoveResolve(item.Key);
            }
            catch (Exception ex)
            {
                new ConfigurationClient().ЖурналСобытийДобавитьОшибку("Ошибка: Cache.Clear", ex.ToString(), "RosService.Caching.RemoveAllResolve", domain);
            }
        }
        #endregion

        public static string Key<T>(string domain, string value) where T : КешЭлемент
        {
            return domain + ":Z:" + typeof(T).Name + ":" + value ?? string.Empty;
        }
        public static T Get<T>(string key) where T : КешЭлемент
        {
            if (string.IsNullOrEmpty(key))
                return (T)null;

            try
            {
                var item = null as КешЭлемент;
                if (!Items.TryGetValue(key, out item))
                    return null;

                return (T)item;
            }
            catch (Exception ex)
            {
                ConfigurationClient.WindowsLog(ex.ToString(), string.Empty, "system", "Get<T>");
                return null;
            }
        }
        public static void Set<T>(string key, T value) where T : КешЭлемент
        {
            if (string.IsNullOrEmpty(key))
                return;

            Items.AddOrUpdate(key, value, (k, e) => e = value);
        }

        public static void Remove(string key)
        {
            var dump = null as КешЭлемент;
            Items.TryRemove(key, out dump);
        }
        public static void RemoveAll(string pattern)
        {
            try
            {
                foreach (var item in Items.Where(p => p.Key != null && p.Key.StartsWith(pattern)).ToArray())
                {
                    var dump = null as КешЭлемент;
                    Items.TryRemove(item.Key, out dump);
                }
            }
            catch (Exception ex)
            {
                new ConfigurationClient().ЖурналСобытийДобавитьОшибку("Ошибка: Cache.Clear", ex.ToString(), "RosService.Caching.RemoveAll", pattern);
            }
        }


        public static Dictionary<string, КешЭлемент> GetAllForDomain(string domain)
        {
            var key = domain + ":Z:";
            var dic = new Dictionary<string, КешЭлемент>();
            //if (MemoryCache.IsMemoryCacheClient)
            //{
            //    var items = null as List<string>;
            //    using (var redis = ClientRedis.Clients.GetReadOnlyClient())
            //    {
            //        items = redis.SearchKeys(key + "*");
            //    }
            //    if (items.Count > 0)
            //    {
            //        foreach (var item in ClientRedis.Clients.GetAll<КешЭлемент>(items))
            //        {
            //            try
            //            {
            //                dic.Add(item.Key, item.Value);
            //            }
            //            catch (Exception)
            //            {
            //            }
            //        }
            //    }
            //}
            //else
            //{
            foreach (var item in Items.Where(p => p.Key != null && p.Key.StartsWith(key)).ToArray())
            {
                try
                {
                    dic.Add(item.Key, item.Value);
                }
                catch (Exception)
                {
                }
            }
            //}
            return dic;
        }
        public static Dictionary<string, T> GetAll<T>(string key) where T : КешЭлемент
        {
            var dic = new Dictionary<string, T>();
            //if (MemoryCache.IsMemoryCacheClient)
            //{
            //    var items = null as List<string>;
            //    using (var redis = ClientRedis.Clients.GetReadOnlyClient())
            //    {
            //        items = redis.SearchKeys(key + "*");
            //    }
            //    if (items.Count > 0)
            //    {
            //        foreach (var item in ClientRedis.Clients.GetAll<T>(items))
            //        {
            //            try
            //            {
            //                dic.Add(item.Key, (T)item.Value);
            //            }
            //            catch (Exception)
            //            {
            //            }
            //        }
            //    }
            //}
            //else
            //{
            foreach (var item in Items.Where(p => p.Key != null && p.Key.StartsWith(key)).ToArray())
            {
                try
                {
                    dic.Add(item.Key, (T)item.Value);
                }
                catch (Exception)
                {
                }
            }
            //}
            return dic;
        }
        public static IEnumerable<КешХешьТаблица> СписокЗависимыхТаблиц(string тип, Хранилище хранилище, string domain)
        {
            try
            {
                var key = domain + ":Z:КешХешьТаблица:";
                //if (MemoryCache.IsMemoryCacheClient)
                //{
                //    var items = null as List<string>;
                //    using (var redis = ClientRedis.Clients.GetReadOnlyClient())
                //    {
                //        items = redis.SearchKeys(key + "*");
                //    }
                //    if (items.Count > 0)
                //    {
                //        var caching = ClientRedis.Clients.GetAll<КешХешьТаблица>(items)
                //            .Where(p => p.Value.Хранилище == хранилище && p.Value.ВремяЖизни == null)
                //            .Select(p => p.Value);

                //        if (caching.Count() == 0)
                //            return new КешХешьТаблица[0];

                //        var tables = caching.Where(p =>
                //                p.IsComplite
                //                && p.ЗависимыеТипы != null
                //                && p.ЗависимыеТипы.Length > 0
                //                && p.ЗависимыеТипы.FirstOrDefault(e => e == тип) != null);
                //        if (tables.Count() == 0)
                //            return new КешХешьТаблица[0];

                //        return tables.ToArray();
                //    }
                //}
                //else
                //{
                var caching = Caching.Cache.Items
                    .AsParallel()
                    .Where(p => p.Value is КешХешьТаблица
                        && p.Key != null
                        && p.Key.StartsWith(key)
                        && ((КешХешьТаблица)p.Value).Хранилище == хранилище
                        && ((КешХешьТаблица)p.Value).ВремяЖизни == null)
                    .Select(p => p.Value).Cast<КешХешьТаблица>();
                if (caching.Count() == 0)
                    return new КешХешьТаблица[0];

                var tables = caching.Where(p =>
                    p.IsComplite
                    && p.ЗависимыеТипы != null
                    && p.ЗависимыеТипы.Length > 0
                    && p.ЗависимыеТипы.FirstOrDefault(e => e == тип) != null);
                if (tables.Count() == 0)
                    return new КешХешьТаблица[0];

                return tables.ToArray();
                //}
            }
            catch (Exception ex)
            {
                ConfigurationClient.WindowsLog("RosService.Caching.СписокЗависимыхТаблиц", string.Empty, domain, тип, ex.ToString());
            }
            return new КешХешьТаблица[0];
        }

        public static void Clear()
        {
            //очистить идентификаторы через 48 часов
            //foreach (var item in Cache.Items.Where(p => (p.Key ?? "").Contains("_КешИдентификаторРаздела_")).ToArray())
            foreach (var item in Cache.ItemsResolve.ToArray())
            {
                if (item.Value != null && item.Value.delete < DateTime.Now)
                {
                    Cache.RemoveResolve(item.Key);
                }
            }
        }
    }

    #region class
    [DataContract]
    [Serializable]
    public class КешЭлемент
    {
        [DataMember]
        public DateTime ДатаСоздания
        {
            get;
            set;
        }
        //[IgnoreDataMember]
        //public int Count
        //{
        //    get;
        //    set;
        //}

        //private uint? _avgTime;
        //[IgnoreDataMember]
        //public uint? AvgTime
        //{
        //    get
        //    {
        //        return _avgTime;
        //    }
        //    set
        //    {
        //        if (_avgTime == null || _avgTime.Value == 0)
        //        {
        //            _avgTime = value;
        //        }
        //        else
        //        {
        //            _avgTime = (_avgTime + value) / 2u;
        //        }
        //    }
        //}

        public КешЭлемент()
        {
            ДатаСоздания = DateTime.Now;
        }
        public override string ToString()
        {
            var sb = new StringBuilder();
            var settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.OmitXmlDeclaration = true;

            var ser = new System.Runtime.Serialization.DataContractSerializer(this.GetType());
            using (var xml = XmlWriter.Create(sb, settings))
            {
                ser.WriteObject(xml, this);
                xml.Flush();
                return sb.ToString();
            }
        }
        public static List<System.Type> KnownTypeList()
        {
            var knownTypeList = new List<System.Type>();
            knownTypeList.Add(typeof(КешЭлемент));
            knownTypeList.Add(typeof(КешЗапрос));
            knownTypeList.Add(typeof(КешЗапросХешьТаблица));
            knownTypeList.Add(typeof(КешХешьТаблица));
            knownTypeList.Add(typeof(КешДобавитьРаздел));
            knownTypeList.Add(typeof(КешПолучитьТип));
            knownTypeList.Add(typeof(КешФорма));
            //knownTypeList.Add(typeof(КешИдентификаторРаздела));
            knownTypeList.Add(typeof(КешСчётчик));
            knownTypeList.Add(typeof(КешХешьТаблицаПамять));
            knownTypeList.Add(typeof(КешСписокАтрибутов));
            return knownTypeList;
        }
    }
    [DataContract]
    [Serializable]
    public class КешЗапрос : КешЭлемент
    {
        [DataMember]
        public string Sql
        {
            get;
            set;
        }
        [DataMember]
        public Dictionary<string, MemberTypes?> Параметры
        {
            get;
            set;
        }
        [DataMember]
        public bool IsПоискПоСодержимому
        {
            get;
            set;
        }
        [DataMember]
        public bool ВсегдаКешировать
        {
            get;
            set;
        }
        //[DataMember]
        [IgnoreDataMember]
        public IEnumerable<string> Типы
        {
            get;
            set;
        }
    };
    [DataContract]
    [Serializable]
    public class КешЗапросХешьТаблица : КешЗапрос
    {
    };
    [DataContract]
    [Serializable]
    public class КешХешьТаблица : КешЭлемент
    {
        [IgnoreDataMember]
        internal static readonly object lockObject = new System.Object();

        [IgnoreDataMember]
        internal decimal id_proc;

        [DataMember]
        public bool IsComplite
        {
            get;
            set;
        }
        [DataMember]
        public DateTime? ВремяЖизни
        {
            get;
            set;
        }
        [DataMember]
        public string ВремяПодготовки
        {
            get;
            set;
        }
        [DataMember]
        public ХешьАтрибут[] ЗависимыеАтрибуты
        {
            get;
            set;
        }
        [DataMember]
        public string[] ЗависимыеТипы
        {
            get;
            set;
        }
        [DataMember]
        public string Таблица
        {
            get;
            set;
        }
        [DataMember]
        public Хранилище Хранилище
        {
            get;
            set;
        }
        [DataMember]
        public string Error
        {
            get;
            set;
        }
        [DataMember]
        public float _Процент
        {
            get;
            set;
        }
    }
    [DataContract]
    [Serializable]
    public class КешДобавитьРаздел : КешЭлемент
    {
        [DataMember]
        public Dictionary<string, object> DefaultValue
        {
            get;
            set;
        }
        [DataMember]
        public Dictionary<string, string> AutoIncrimentValue
        {
            get;
            set;
        }
        [DataMember]
        public string НазваниеОбъекта
        {
            get;
            set;
        }
    };

    [DataContract]
    [Serializable]
    public class КешПолучитьТип : КешЭлемент
    {
        [DataMember]
        public RosService.Configuration.Type Тип { get; set; }
    };
    [DataContract]
    [Serializable]
    public class КешФорма : КешЭлемент
    {
        [DataMember]
        public RosService.Configuration.Binding[] Items
        {
            get;
            set;
        }
    };

    public class КешИдентификаторРаздела
    {
        public DateTime delete { get; private set; }
        public decimal id_node { get; set; }

        public КешИдентификаторРаздела()
        {
            delete = DateTime.Now.Add(TimeSpan.FromHours(12));
        }
    }

    [DataContract]
    [Serializable]
    public class КешСчётчик : КешЭлемент
    {
        internal static readonly object lockObject = new System.Object();
        [DataMember]
        public long Значение
        {
            get;
            set;
        }
    }
    [DataContract]
    [Serializable]
    public class КешХешьТаблицаПамять : КешЭлемент
    {
        [DataMember]
        public DateTime? ВремяЖизни
        {
            get;
            set;
        }
        [IgnoreDataMember]
        public TableValue Результат
        {
            get;
            set;
        }
    };
    [DataContract]
    [Serializable]
    public class КешСписокАтрибутов : КешЭлемент
    {
        [DataMember]
        public RosService.Configuration.Type[] Атрибуты
        {
            get;
            set;
        }
    };
    [DataContract]
    [Serializable]
    public class ХешьАтрибут
    {
        [DataMember]
        public string Атрибут
        {
            get;
            set;
        }
        [DataMember]
        public MemberTypes MemberType
        {
            get;
            set;
        }
        [DataMember]
        public bool ПолнотекстовыйВывод
        {
            get;
            set;
        }
    }

    //internal class HistoryValueContanier
    //{
    //    public string domain;
    //    public Хранилище хранилище;
    //    public string user;
    //    public string attribute;
    //    public RegisterTypes registerType;
    //    public decimal id_node;
    //    public object value;
    //    public DateTime date;
    //}
    #endregion
}
